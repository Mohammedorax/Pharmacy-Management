[CmdletBinding()]
param(
    [string]$ServerInstance = '.',
    [string]$SourceDatabase = 'PharmacyManagement_qa',
    [string]$ExecutablePath,
    [switch]$RehearseReceiptMigration
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath=Join-Path $PSScriptRoot '..\project_fo_3\bin\Release\PurchaseWriteOwnedSqlAcceptance.exe'
}
if ($SourceDatabase -notmatch '^[A-Za-z0-9_]+_qa$') { throw 'Source database must be an explicitly named _qa database.' }
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) { throw 'Release executable is missing.' }

Add-Type -AssemblyName System.Data
$masterConnection = [System.Data.SqlClient.SqlConnection]::new("Server=$ServerInstance;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
$sandboxDatabase = 'PharmacyConcurrency_{0}_{1}_sandbox' -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0,8))
$backupPath = $null
$ownerToken=[Guid]::NewGuid().ToString('N')
$ownedDatabaseId=$null
$sourceBefore=$null
$acceptanceRun=$null
$cleanupVerified=$false
$savedConfigurationBytes=$null
$sourceUnchanged=$false
function Read-SourceFingerprint {
    $parts=[Collections.Generic.List[string]]::new()
    foreach($entry in @(@('SalesInvoicesTb','InvoiceId'),@('SalesInvoiceLinesTb','LineId'),@('CommercialReturnHeadersTb','ReturnId'),@('CommercialReturnLinesTb','ReturnLineId'),@('JournalEntriesTb','JournalEntryId'),@('JournalEntryLinesTb','JournalEntryLineId'),@('CustomersBillsTb','CustomerId'),@('CustomerAccountLedgerTb','LedgerEntryId'),@('AuditLogTb','AuditId'),@('EmployeeTb1','EmpId'),@('EmployeeOperationPermissionsTb','EmpId, OperationName'),@('MedicineTb1','MedName'),@('CompanyTb1','CompName'))) {
        $c=$masterConnection.CreateCommand()
        try {
            $c.CommandTimeout=60
            $c.CommandText='SELECT CONVERT(nvarchar(max),(SELECT * FROM ['+$SourceDatabase+'].dbo.['+$entry[0]+'] ORDER BY '+(($entry[1] -split ',\s*' | ForEach-Object {'['+$_+']'}) -join ',')+' FOR XML RAW,ROOT(''rows''),BINARY BASE64));'
            $parts.Add([string]$c.ExecuteScalar())
        } finally {$c.Dispose()}
    }
    $sha=[Security.Cryptography.SHA256]::Create()
    try {return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($parts -join '|')))).Replace('-','')} finally {$sha.Dispose()}
}
$created = $false

function Invoke-NonQuery([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters) {
    $command=$Connection.CreateCommand()
    try {
        $command.CommandTimeout=600; $command.CommandText=$Sql
        foreach($key in $Parameters.Keys) { [void]$command.Parameters.AddWithValue($key,$Parameters[$key]) }
        [void]$command.ExecuteNonQuery()
    }
    finally { $command.Dispose() }
}

try {
    $masterConnection.Open()
    $sourceBefore=Read-SourceFingerprint
    $metadata=$masterConnection.CreateCommand()
    try {
        $metadata.CommandText=@"
SELECT CASE WHEN DB_ID(@Source) IS NOT NULL AND @Source LIKE N'%[_]qa' THEN 1 ELSE 0 END,
       CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));
"@
        [void]$metadata.Parameters.AddWithValue('@Source',$SourceDatabase)
        $reader=$metadata.ExecuteReader()
        try {
            if(-not $reader.Read() -or [int]$reader[0] -ne 1) { throw 'Explicit QA source database was not found.' }
            $dataDirectory=if($reader.IsDBNull(1)){ $null } else { [string]$reader[1] }
        }
        finally { $reader.Dispose() }
    }
    finally { $metadata.Dispose() }

    if([string]::IsNullOrWhiteSpace($dataDirectory)) {
        $pathCommand=$masterConnection.CreateCommand()
        try {
            $pathCommand.CommandText='SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id=DB_ID(@Source) AND type=0 ORDER BY file_id;'
            [void]$pathCommand.Parameters.AddWithValue('@Source',$SourceDatabase)
            $dataDirectory=[IO.Path]::GetDirectoryName([string]$pathCommand.ExecuteScalar())
        }
        finally { $pathCommand.Dispose() }
    }
    $backupPath=Join-Path $dataDirectory ($sandboxDatabase+'.bak')
    $sourceIdentifier='['+$SourceDatabase.Replace(']',']]')+']'
    Invoke-NonQuery $masterConnection ("BACKUP DATABASE $sourceIdentifier TO DISK=@Backup WITH COPY_ONLY, INIT, CHECKSUM;") @{ '@Backup'=$backupPath }
    Invoke-NonQuery $masterConnection 'RESTORE VERIFYONLY FROM DISK=@Backup WITH CHECKSUM;' @{ '@Backup'=$backupPath }

    $fileCommand=$masterConnection.CreateCommand()
    $fileTable=[Data.DataTable]::new()
    try {
        $fileCommand.CommandTimeout=600; $fileCommand.CommandText='RESTORE FILELISTONLY FROM DISK=@Backup;'; [void]$fileCommand.Parameters.AddWithValue('@Backup',$backupPath)
        $adapter=[Data.SqlClient.SqlDataAdapter]::new($fileCommand); try { [void]$adapter.Fill($fileTable) } finally { $adapter.Dispose() }
    }
    finally { $fileCommand.Dispose() }
    if($fileTable.Rows.Count -eq 0) { throw 'Backup file list is empty.' }

    $moves=[Collections.Generic.List[string]]::new(); $index=0
    foreach($row in $fileTable.Rows) {
        $index++
        $logical=([string]$row['LogicalName']).Replace("'","''")
        $extension=if([string]$row['Type'] -eq 'L'){'.ldf'}elseif($index -eq 1){'.mdf'}else{'.ndf'}
        $target=Join-Path $dataDirectory ($sandboxDatabase+'_'+$index+$extension)
        $moves.Add("MOVE N'$logical' TO N'$($target.Replace("'","''"))'")
    }
    $sandboxIdentifier='['+$sandboxDatabase.Replace(']',']]')+']'
    $restoreSql="RESTORE DATABASE $sandboxIdentifier FROM DISK=@Backup WITH CHECKSUM, RECOVERY, "+($moves -join ', ')+';'
    Invoke-NonQuery $masterConnection $restoreSql @{ '@Backup'=$backupPath }
    $created=$true
    Invoke-NonQuery $masterConnection ("USE $sandboxIdentifier; EXEC sys.sp_addextendedproperty @name=N'QaTest.Owner',@value=@Owner; USE [master];") @{ '@Owner'=$ownerToken }
    $idCommand=$masterConnection.CreateCommand()
    try {$idCommand.CommandText='SELECT DB_ID(@Name);';[void]$idCommand.Parameters.AddWithValue('@Name',$sandboxDatabase);$ownedDatabaseId=[int]$idCommand.ExecuteScalar()} finally {$idCommand.Dispose()}
    $dbcc=$masterConnection.CreateCommand()
    try {
        $dbcc.CommandTimeout=600;$dbcc.CommandText="DBCC CHECKDB ($sandboxIdentifier) WITH NO_INFOMSGS,TABLERESULTS;"
        $adapter=[Data.SqlClient.SqlDataAdapter]::new($dbcc);$results=[Data.DataTable]::new()
        try {[void]$adapter.Fill($results);if($results.Rows.Count -ne 0){throw 'Owned QA DBCC returned integrity findings.'}} finally {$adapter.Dispose();$results.Dispose()}
    } finally {$dbcc.Dispose()}

    $receiptMigrationRehearsal=$null
    if($RehearseReceiptMigration){
        $runner=Join-Path $PSScriptRoot 'Apply-PurchaseWriteIdentityMigration.ps1'
        $migrationArguments=@{ServerInstance=$ServerInstance;DatabaseName=$sandboxDatabase;VerifiedBackupPath=$backupPath;BackupDatabaseName=$SourceDatabase;OwnerToken=$ownerToken;ExpectedDatabaseId=$ownedDatabaseId;Confirm=$false}
        $targetConnection=[Data.SqlClient.SqlConnection]::new("Server=$ServerInstance;Database=$sandboxDatabase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
        $targetConnection.Open()
        try{
            $targetCommand=$targetConnection.CreateCommand()
            try{
                $targetCommand.CommandText="IF OBJECT_ID(N'dbo.PharmacySchemaMigrationRegistryTb',N'U') IS NULL CREATE TABLE dbo.PharmacySchemaMigrationRegistryTb(MigrationId nvarchar(200) NOT NULL CONSTRAINT PK_PharmacySchemaMigrationRegistryTb PRIMARY KEY,ScriptSha256 char(64) NOT NULL,ToolVersion nvarchar(40) NOT NULL,AppliedBy nvarchar(256) NOT NULL,AppliedAtUtc datetime2 NOT NULL CONSTRAINT DF_PharmacySchemaMigrationRegistryTb_AppliedAtUtc DEFAULT SYSUTCDATETIME());"
                [void]$targetCommand.ExecuteNonQuery()
                $targetCommand.CommandText="SELECT CASE WHEN OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL THEN 0 ELSE 1 END;"
                $preMigratedFixture=[int]$targetCommand.ExecuteScalar() -eq 1
                if($preMigratedFixture){
                    $targetCommand.CommandText='SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb;'
                    if([int]$targetCommand.ExecuteScalar() -ne 0){throw 'Fresh migration rehearsal requires an empty receipt table; historical receipts will not be removed.'}
                    $targetCommand.CommandText="BEGIN TRAN;DROP TABLE dbo.PurchaseWriteReceiptsTb;DELETE dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N'0025_purchase_write_request_identity';COMMIT;"
                    [void]$targetCommand.ExecuteNonQuery()
                }
                $targetCommand.CommandText="CREATE TRIGGER dbo.QaReceiptMigrationRegistryFailure ON dbo.PharmacySchemaMigrationRegistryTb AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE MigrationId=N'0025_purchase_write_request_identity') THROW 51098,N'Owned receipt migration registry failure',1;"
                [void]$targetCommand.ExecuteNonQuery()
                $failed=$false
                try{& $runner @migrationArguments | Out-Null}catch{if($_.Exception.ToString() -notmatch 'Owned receipt migration registry failure'){throw};$failed=$true}
                if(-not $failed){throw 'Injected registry failure was not observed.'}
                $targetCommand.CommandText="SELECT CASE WHEN OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N'0025_purchase_write_request_identity') THEN 1 ELSE 0 END;"
                if([int]$targetCommand.ExecuteScalar() -ne 1){throw 'Receipt DDL/registry failure did not roll back atomically.'}
                $targetCommand.CommandText='DROP TRIGGER dbo.QaReceiptMigrationRegistryFailure;';[void]$targetCommand.ExecuteNonQuery()
                $first=& $runner @migrationArguments
                $second=& $runner @migrationArguments
                if($first.Status -ne 'Applied' -or $second.Status -ne 'AlreadyApplied'){throw 'Migration initial/repeat status mismatch.'}
                function Assert-MigrationReject($parameters,[string]$message){
                    $rejected=$false
                    try{& $runner @parameters | Out-Null}catch{if($_.Exception.ToString() -notmatch [regex]::Escape($message)){throw};$rejected=$true}
                    if(-not $rejected){throw "Expected migration rejection missing: $message"}
                }
                $wrong=@{}+$migrationArguments;$wrong.OwnerToken='00000000000000000000000000000000'
                Assert-MigrationReject $wrong 'Owned target owner/id mismatch.'
                $wrong=@{}+$migrationArguments;$wrong.BackupDatabaseName=$SourceDatabase+'_wrong_qa'
                Assert-MigrationReject $wrong 'Backup source/type/family does not match the intended target.'
                $foreignName='PharmacyReceiptFamily_'+[Guid]::NewGuid().ToString('N')+'_qa'
                $foreignIdentifier='['+$foreignName+']'
                $foreignBackup=Join-Path $dataDirectory ($foreignName+'.bak')
                $foreignId=$null;$foreignCreated=$false;$foreignCleanup=$false
                try{
                    Invoke-NonQuery $masterConnection ("CREATE DATABASE $foreignIdentifier;") @{};$foreignCreated=$true
                    Invoke-NonQuery $masterConnection ("USE $foreignIdentifier;EXEC sys.sp_addextendedproperty @name=N'QaTest.Owner',@value=@Owner;USE [master];") @{'@Owner'=$ownerToken}
                    $identityCommand=$masterConnection.CreateCommand()
                    try{$identityCommand.CommandText='SELECT DB_ID(@Name)';[void]$identityCommand.Parameters.AddWithValue('@Name',$foreignName);$foreignId=[int]$identityCommand.ExecuteScalar()}finally{$identityCommand.Dispose()}
                    Invoke-NonQuery $masterConnection ("BACKUP DATABASE $foreignIdentifier TO DISK=@Backup WITH COPY_ONLY,INIT,CHECKSUM;") @{'@Backup'=$foreignBackup}
                    $wrong=@{}+$migrationArguments;$wrong.VerifiedBackupPath=$foreignBackup;$wrong.BackupDatabaseName=$foreignName
                    Assert-MigrationReject $wrong 'Backup source/type/family does not match the intended target.'
                }finally{
                    if($foreignCreated -and $foreignId){
                        $identityCommand=$masterConnection.CreateCommand()
                        try{
                            $identityCommand.CommandText="SELECT CASE WHEN DB_ID(@Name)=@Id AND EXISTS(SELECT 1 FROM $foreignIdentifier.sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner' AND CONVERT(nvarchar(64),value)=@Owner) THEN 1 ELSE 0 END;"
                            [void]$identityCommand.Parameters.AddWithValue('@Name',$foreignName);[void]$identityCommand.Parameters.AddWithValue('@Id',$foreignId);[void]$identityCommand.Parameters.AddWithValue('@Owner',$ownerToken)
                            if([int]$identityCommand.ExecuteScalar() -ne 1){throw 'Foreign family fixture identity mismatch; cleanup refused.'}
                        }finally{$identityCommand.Dispose()}
                        Invoke-NonQuery $masterConnection ("ALTER DATABASE $foreignIdentifier SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE $foreignIdentifier;") @{}
                        $identityCommand=$masterConnection.CreateCommand()
                        try{$identityCommand.CommandText='SELECT DB_ID(@Name)';[void]$identityCommand.Parameters.AddWithValue('@Name',$foreignName);$foreignCleanup=$identityCommand.ExecuteScalar() -is [DBNull]}finally{$identityCommand.Dispose()}
                    }
                    if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($foreignBackup)) -ne [IO.Path]::GetFullPath($dataDirectory).TrimEnd('\')){throw 'Foreign backup cleanup path is outside SQL data directory.'}
                    Invoke-NonQuery $masterConnection "EXEC master.dbo.xp_delete_file 0,@Backup,N'bak';" @{'@Backup'=$foreignBackup}
                }
                if(-not $foreignCleanup){throw 'Foreign family fixture cleanup not verified.'}
                $targetCommand.CommandText="SELECT ScriptSha256 FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N'0025_purchase_write_request_identity';"
                if([string]$targetCommand.ExecuteScalar() -cne $first.MigrationSHA256){throw 'Rejected target/backup changed the registry hash.'}
                $targetCommand.CommandText="UPDATE dbo.PharmacySchemaMigrationRegistryTb SET ScriptSha256=REPLICATE('0',64) WHERE MigrationId=N'0025_purchase_write_request_identity';";[void]$targetCommand.ExecuteNonQuery()
                try{
                    Assert-MigrationReject $migrationArguments 'An applied migration hash differs; create a new migration instead of replacing it.'
                    $targetCommand.CommandText="SELECT ScriptSha256 FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N'0025_purchase_write_request_identity';"
                    if([string]$targetCommand.ExecuteScalar() -cne ('0'*64)){throw 'Hash conflict was silently rewritten.'}
                }finally{
                    $targetCommand.CommandText="UPDATE dbo.PharmacySchemaMigrationRegistryTb SET ScriptSha256=@Hash WHERE MigrationId=N'0025_purchase_write_request_identity';"
                    [void]$targetCommand.Parameters.AddWithValue('@Hash',$first.MigrationSHA256);[void]$targetCommand.ExecuteNonQuery();$targetCommand.Parameters.Clear()
                }
                $targetCommand.CommandText='ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerUser nvarchar(199) NOT NULL;';[void]$targetCommand.ExecuteNonQuery()
                try{
                    Assert-MigrationReject $migrationArguments 'مخطط استعادة المشتريات غير متوافق'
                    $targetCommand.CommandText="SELECT max_length FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb') AND name=N'OwnerUser';"
                    if([int]$targetCommand.ExecuteScalar() -ne 398){throw 'Incompatible schema was silently changed.'}
                }finally{$targetCommand.CommandText='ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerUser nvarchar(200) NOT NULL;';[void]$targetCommand.ExecuteNonQuery()}
                $final=& $runner @migrationArguments
                $targetCommand.CommandText='SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb;';if([int]$targetCommand.ExecuteScalar() -ne 0){throw 'Migration rejections inserted a receipt.'}
                $receiptMigrationRehearsal=[ordered]@{RegistryFailureRolledBackSchemaAndRecord=$true;SourceCloneInitiallyHadEmptyReceiptSchema=$preMigratedFixture;First=$first;Repeat=$second;WrongOwnerRejected=$true;WrongBackupSourceRejected=$true;MatchingBackupNameWrongFamilyRejected=$true;ForeignFamilyFixture=$foreignName;ForeignFamilyFixtureCleanupVerified=$foreignCleanup;RegistryHashConflictRejectedAndNotRewritten=$true;AppliedSchemaMismatchRejectedAndNotChanged=$true;FinalRepeat=$final}
                Write-Output 'PASS owned receipt migration: verified source/family backup; DDL+registry failure atomic rollback; applied once and repeat validated.'
            }finally{$targetCommand.Dispose()}
        }finally{$targetConnection.Dispose()}
    }

    $acceptanceRun=Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\purchase-write-main-native-sql-'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $acceptanceRun)
    $output=Join-Path $acceptanceRun 'cancel.stdout.log';$errorOutput=Join-Path $acceptanceRun 'cancel.stderr.log'
    $configurationPath=$ExecutablePath+'.config'
    $savedConfigurationBytes=[IO.File]::ReadAllBytes($configurationPath)
    [xml]$configuration=([Text.Encoding]::UTF8.GetString($savedConfigurationBytes).TrimStart([char]0xFEFF))
    $entry=$configuration.configuration.connectionStrings.add | Where-Object {$_.name -eq 'PharmacyConnection'}
    $entry.connectionString="Server=$ServerInstance;Database=$sandboxDatabase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False"
    $force=$configuration.configuration.appSettings.add | Where-Object {$_.key -eq 'ForceBuildConnectionProfile'}
    if($null -eq $force){$force=$configuration.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');$force.SetAttribute('value','true');[void]$configuration.configuration.appSettings.AppendChild($force)}else{$force.value='true'}
    $configuration.Save($configurationPath)
    $p=Start-Process -FilePath $ExecutablePath -ArgumentList $ServerInstance,$sandboxDatabase,$ownerToken -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $output -RedirectStandardError $errorOutput
    $readOutput=Get-Content $output -Raw
    if($p.ExitCode -ne 0 -or $readOutput -notmatch 'purchase receipt main owned SQL: checks='){throw 'Purchase write owned SQL acceptance failed'}
    [ordered]@{NativePurchaseReadsAccepted=$true;Status='purchase main owned SQL passed before cleanup';Database=$sandboxDatabase;ExecutableSHA256=(Get-FileHash $ExecutablePath).Hash;Output=$readOutput;CleanupVerified=$false} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $acceptanceRun 'read-evidence.json') -Encoding utf8
    if($receiptMigrationRehearsal){$receiptMigrationRehearsal | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $acceptanceRun 'migration-rehearsal.json') -Encoding utf8}
    Write-Output $readOutput
}
finally {
    if($masterConnection.State -ne [Data.ConnectionState]::Open) { try { $masterConnection.Open() } catch {} }
    if($created -and $masterConnection.State -eq [Data.ConnectionState]::Open) {
        $sandboxIdentifier='['+$sandboxDatabase.Replace(']',']]')+']'
        $ownerCommand=$masterConnection.CreateCommand()
        try {
            $ownerCommand.CommandText="SELECT CASE WHEN DB_ID(@Name)=@Id AND EXISTS(SELECT 1 FROM $sandboxIdentifier.sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner' AND CONVERT(nvarchar(64),value)=@Owner) THEN 1 ELSE 0 END;"
            [void]$ownerCommand.Parameters.AddWithValue('@Name',$sandboxDatabase);[void]$ownerCommand.Parameters.AddWithValue('@Id',$ownedDatabaseId);[void]$ownerCommand.Parameters.AddWithValue('@Owner',$ownerToken)
            if([int]$ownerCommand.ExecuteScalar() -ne 1){throw 'Temporary database owner/id mismatch; cleanup refused.'}
        } finally {$ownerCommand.Dispose()}
        Invoke-NonQuery $masterConnection ("ALTER DATABASE $sandboxIdentifier SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $sandboxIdentifier;") @{}
        $absence=$masterConnection.CreateCommand()
try {$absence.CommandText='SELECT DB_ID(@Name)';[void]$absence.Parameters.AddWithValue('@Name',$sandboxDatabase);$cleanupVerified=($absence.ExecuteScalar() -is [DBNull])} finally {$absence.Dispose()}
    }
    if($backupPath -and $masterConnection.State -eq [Data.ConnectionState]::Open) {
        try { Invoke-NonQuery $masterConnection "EXEC master.dbo.xp_delete_file 0,@Backup,N'bak';" @{ '@Backup'=$backupPath } } catch { Write-Warning "Temporary backup cleanup failed: $($_.Exception.Message)" }
    }
    if($masterConnection.State -eq [Data.ConnectionState]::Open -and $sourceBefore){
        $sourceUnchanged=($sourceBefore -eq (Read-SourceFingerprint))
    }
    if($acceptanceRun -and (Test-Path (Join-Path $acceptanceRun 'read-evidence.json'))){
        $e=Get-Content (Join-Path $acceptanceRun 'read-evidence.json') -Raw | ConvertFrom-Json
        $e.CleanupVerified=$cleanupVerified
        $e | Add-Member -NotePropertyName SourceFinancialUnchanged -NotePropertyValue $sourceUnchanged
        $e | Add-Member -NotePropertyName SourceFinancialSHA256 -NotePropertyValue $sourceBefore
        $e.Status=if($cleanupVerified -and $sourceUnchanged){'purchase desktop root passed; cleanup and thirteen source table fingerprints verified'}else{'acceptance incomplete'}
        $e | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $acceptanceRun 'read-evidence.json') -Encoding utf8
    }
    if($savedConfigurationBytes){[IO.File]::WriteAllBytes($configurationPath,$savedConfigurationBytes)}
    $masterConnection.Dispose()
    if($created -and (-not $cleanupVerified -or -not $sourceUnchanged)){throw 'Owned QA cleanup/source fingerprint acceptance failed.'}
}












