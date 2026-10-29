[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='High')]
param(
 [Parameter(Mandatory=$true)][string]$ServerInstance,
 [Parameter(Mandatory=$true)][string]$DatabaseName,
 [Parameter(Mandatory=$true)][string]$VerifiedBackupPath,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_]+_qa$')][string]$BackupDatabaseName,
 [string]$OwnerToken,[int]$ExpectedDatabaseId=0
)
$ErrorActionPreference='Stop'
if($ExpectedDatabaseId -le 0){throw 'Exact expected database id is required.'}
$owned=$DatabaseName -match '^(PharmacyConcurrency_\d{14}_[0-9a-f]{8}_sandbox|PharmacyRestore_\d{14}_[0-9a-f]{32}_qa)$'
if(-not $owned -and $DatabaseName -notmatch '^[A-Za-z0-9_]+_qa$'){throw 'Only an explicitly named QA database or an owned sandbox is supported.'}
if($owned -and ($OwnerToken -notmatch '^[0-9a-f]{32}$' -or $ExpectedDatabaseId -le 0)){throw 'Owned sandbox requires its exact owner token and database id.'}
if([IO.Path]::GetExtension($VerifiedBackupPath) -ine '.bak'){throw 'A verified full .bak backup is required.'}
$migration=Join-Path $PSScriptRoot '..\database\migrations\0026_voucher_request_identity_verified.sql'
$sql=[IO.File]::ReadAllText($migration)
$marker='-- Voucher identity compatibility validation v1';$offset=$sql.IndexOf($marker,[StringComparison]::Ordinal)
if($offset -lt 0){throw 'Receipt validation contract is missing.'}
$end=$sql.IndexOf('    COMMIT TRANSACTION;',$offset,[StringComparison]::Ordinal);if($end -le $offset){throw 'Validation boundary missing.'};$validation=$sql.Substring($offset,$end-$offset);$hash=(Get-FileHash -LiteralPath $migration -Algorithm SHA256).Hash
$id='0026_voucher_request_identity_verified'
Add-Type -AssemblyName System.Data
function Connection([string]$catalog){
 $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new()
 $builder['Data Source']=$ServerInstance;$builder['Initial Catalog']=$catalog;$builder['Integrated Security']=$true;$builder['Encrypt']=$true
 $builder['TrustServerCertificate']=$ServerInstance -in @('.','(local)','localhost');$builder['Connect Timeout']=15
 [Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
}
function Command([Data.SqlClient.SqlConnection]$connection,[string]$text,[Data.SqlClient.SqlTransaction]$transaction,$parameters){
 $command=$connection.CreateCommand();$command.CommandText=$text;$command.CommandTimeout=300
 if($transaction){$command.Transaction=$transaction}
 foreach($name in $parameters.Keys){[void]$command.Parameters.AddWithValue($name,$parameters[$name])}
 return $command
}
$master=Connection 'master';$database=Connection $DatabaseName;$transaction=$null
try{
 $database.Open()
 $command=Command $database 'SELECT DB_ID(),DB_NAME(),CONVERT(nvarchar(64),(SELECT value FROM sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'')),CONVERT(nvarchar(36),family_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID();' $null @{}
 try{$reader=$command.ExecuteReader();try{if(-not $reader.Read()){throw 'Target database identity is unavailable.'};$actualId=[Convert]::ToInt32($reader.GetValue(0));$actualName=$reader.GetString(1);$actualOwner=if($reader.IsDBNull(2)){''}else{$reader.GetString(2)};$family=$reader.GetString(3)}finally{$reader.Dispose()}}finally{$command.Dispose()}
 if(-not [string]::Equals($actualName,$DatabaseName,[StringComparison]::OrdinalIgnoreCase)){throw 'Target database mismatch.'}
 if($ExpectedDatabaseId -gt 0 -and $actualId -ne $ExpectedDatabaseId){throw 'Target database id mismatch.'}
 if($owned -and ($actualId -ne $ExpectedDatabaseId -or $actualOwner -cne $OwnerToken)){throw 'Owned target owner/id mismatch.'}
 $master.Open()
 $command=Command $master 'RESTORE VERIFYONLY FROM DISK=@Backup WITH CHECKSUM;' $null @{'@Backup'=$VerifiedBackupPath}
 try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
 $command=Command $master 'RESTORE HEADERONLY FROM DISK=@Backup;' $null @{'@Backup'=$VerifiedBackupPath}
 try{$adapter=[Data.SqlClient.SqlDataAdapter]::new($command);$headers=[Data.DataTable]::new();try{[void]$adapter.Fill($headers)}finally{$adapter.Dispose()}}finally{$command.Dispose()}
 try{
  if($headers.Rows.Count -ne 1){throw 'A single full database backup set is required.'}
  $header=$headers.Rows[0]
  if([int]$header.BackupType -ne 1 -or [string]$header.DatabaseName -ine $BackupDatabaseName -or [string]$header.FamilyGUID -ine $family){throw 'Backup source/type/family does not match the intended target.'}
 }finally{$headers.Dispose()}
 if(-not $PSCmdlet.ShouldProcess($DatabaseName,"Apply and record $id atomically")){return}
 $transaction=$database.BeginTransaction([Data.IsolationLevel]::Serializable)
 $lockSql="DECLARE @Code int; EXEC @Code=sys.sp_getapplock @Resource=N'PharmacyManagement.Schema.0026VoucherIdentity',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000; IF @Code<0 THROW 51096,N'Receipt migration lock unavailable.',1; IF OBJECT_ID(N'dbo.PharmacySchemaMigrationRegistryTb',N'U') IS NULL CREATE TABLE dbo.PharmacySchemaMigrationRegistryTb(MigrationId nvarchar(200) NOT NULL CONSTRAINT PK_PharmacySchemaMigrationRegistryTb PRIMARY KEY,ScriptSha256 char(64) NOT NULL,ToolVersion nvarchar(40) NOT NULL,AppliedBy nvarchar(256) NOT NULL,AppliedAtUtc datetime2 NOT NULL CONSTRAINT DF_PharmacySchemaMigrationRegistryTb_AppliedAtUtc DEFAULT SYSUTCDATETIME()); SELECT ScriptSha256 FROM dbo.PharmacySchemaMigrationRegistryTb WITH(UPDLOCK,HOLDLOCK) WHERE MigrationId=@Id;"
 $command=Command $database $lockSql $transaction @{'@Id'=$id}
 try{$recorded=$command.ExecuteScalar()}finally{$command.Dispose()}
 $existing=$null -ne $recorded -and $recorded -isnot [DBNull]
 if($existing -and -not [string]::Equals([string]$recorded,$hash,[StringComparison]::OrdinalIgnoreCase)){throw 'An applied migration hash differs; create a new migration instead of replacing it.'}
 $command=Command $database $(if($existing){$validation}else{$sql}) $transaction @{}
 try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
 if(-not $existing){
  $command=Command $database 'INSERT dbo.PharmacySchemaMigrationRegistryTb(MigrationId,ScriptSha256,ToolVersion,AppliedBy) VALUES(@Id,@Hash,N''voucher-identity-1.0'',ORIGINAL_LOGIN());' $transaction @{'@Id'=$id;'@Hash'=$hash}
  try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
 }
 $transaction.Commit();$transaction.Dispose();$transaction=$null
 [pscustomobject]@{Status=$(if($existing){'AlreadyApplied'}else{'Applied'});Database=$actualName;DatabaseId=$actualId;MigrationId=$id;MigrationSHA256=$hash;BackupVerified=$true;BackupSourceFamilyMatched=$true;SchemaValidated=$true;RegistryAtomic=$true;OwnedSandbox=$owned}
}
catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$database.Dispose();$master.Dispose()}