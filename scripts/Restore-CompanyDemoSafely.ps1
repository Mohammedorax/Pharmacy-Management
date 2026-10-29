[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$BackupPath,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSHA256,
 [ValidateNotNullOrEmpty()][string]$ServerInstance='.',
 [switch]$ConfirmIndependentDemoRestore,
 [switch]$PlanOnly
)
$ErrorActionPreference='Stop'
$target='PharmacyCompanyDemo_20261008_4925d6ed_qa'
$resolved=(Resolve-Path -LiteralPath $BackupPath).Path
if((Get-Item -LiteralPath $resolved).PSIsContainer){throw 'Backup must be a file.'}
$actual=(Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
if($actual -cne $ExpectedSHA256.ToUpperInvariant()){throw 'Backup SHA256 mismatch; restore refused before SQL access.'}
if(!$PlanOnly -and !$ConfirmIndependentDemoRestore){throw 'Use -PlanOnly to inspect, or explicitly pass -ConfirmIndependentDemoRestore on the destination demo machine.'}
if($PlanOnly){
 [pscustomobject]@{Server=$ServerInstance;TargetDatabase=$target;BackupPath=$resolved;BackupSHA256=$actual;SqlOpened=$false;RestoreExecuted=$false;ExistingDatabasePolicy='Refuse; never overwrite';ClientInstallAccepted=$false}|ConvertTo-Json
 return
}
# A builder keeps a supplied instance name from becoming connection-string options.
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source']=$ServerInstance
$builder['Initial Catalog']='master'
$builder['Integrated Security']=$true
# LocalDB is a private local pipe and this installed engine rejects TLS.
# Named/network SQL Server instances retain the encrypted connection policy.
$builder['Encrypt']=($ServerInstance -notmatch '^\(localdb\)\\[A-Za-z0-9_.-]+$')
$builder['TrustServerCertificate']=$true
$builder['Pooling']=$false
$builder['Connect Timeout']=10
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Command([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=180;$cmd.CommandText=$sql;return $cmd}
function Scalar([string]$sql){$q=Command $sql;try{return $q.ExecuteScalar()}finally{$q.Dispose()}}
try{
 $connection.Open()
 if([int](Scalar "SELECT CASE WHEN DB_ID(N'$target') IS NULL THEN 0 ELSE 1 END") -ne 0){throw 'Target demo database already exists. No database is overwritten or dropped.'}
 $q=Command 'RESTORE VERIFYONLY FROM DISK=@Backup WITH CHECKSUM'
 try{[void]$q.Parameters.AddWithValue('@Backup',$resolved);[void]$q.ExecuteNonQuery()}finally{$q.Dispose()}
 $q=Command 'RESTORE FILELISTONLY FROM DISK=@Backup'
 try{[void]$q.Parameters.AddWithValue('@Backup',$resolved);$table=[Data.DataTable]::new();$adapter=[Data.SqlClient.SqlDataAdapter]::new($q);try{[void]$adapter.Fill($table)}finally{$adapter.Dispose()}}finally{$q.Dispose()}
 if($table.Rows.Count -ne 2 -or @($table.Rows|Where-Object Type -eq 'D').Count -ne 1 -or @($table.Rows|Where-Object Type -eq 'L').Count -ne 1){throw 'This demo restorer supports exactly one data and one log file; no restore executed.'}
 $dataPath=[string](Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))")
 $logPath=[string](Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))")
 if([string]::IsNullOrWhiteSpace($dataPath) -or [string]::IsNullOrWhiteSpace($logPath)){throw 'SQL default storage directories unavailable; no restore executed.'}
 $suffix=[Guid]::NewGuid().ToString('N')
 $moves=@(foreach($row in $table.Rows){
  $folder=if($row.Type -eq 'L'){$logPath}else{$dataPath}
  $extension=if($row.Type -eq 'L'){'.ldf'}else{'.mdf'}
  $destination=[IO.Path]::Combine($folder,'PharmacyCompanyDemo_'+$suffix+$extension)
  "MOVE N'"+([string]$row.LogicalName).Replace("'","''")+"' TO N'"+$destination.Replace("'","''")+"'"
 })
 # Acquire an instance-local application lock and re-check under that lock.
 $q=Command "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'PharmacyCompanyDemo.SafeRestore',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;"
 try{if([int]$q.ExecuteScalar() -lt 0){throw 'Another demo restore owns the SQL lock; no retry or overwrite.'}}finally{$q.Dispose()}
 if([int](Scalar "SELECT CASE WHEN DB_ID(N'$target') IS NULL THEN 0 ELSE 1 END") -ne 0){throw 'Target created meanwhile; restore refused.'}
 $q=Command ("RESTORE DATABASE [$target] FROM DISK=@Backup WITH CHECKSUM,RECOVERY,"+($moves -join ','))
 try{[void]$q.Parameters.AddWithValue('@Backup',$resolved);[void]$q.ExecuteNonQuery()}finally{$q.Dispose()}
 $q=Command "DBCC CHECKDB ([$target]) WITH NO_INFOMSGS;"
 try{[void]$q.ExecuteNonQuery()}finally{$q.Dispose()}
 $owner=[string](Scalar "SELECT CONVERT(nvarchar(32),value) FROM [$target].sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'")
 if($owner -ne '4925d6edb1644f348ae7dd9c4594170a'){throw 'Restored database owner marker differs; inspect destination, no automatic delete.'}
 $medicines=[int](Scalar "SELECT COUNT(*) FROM [$target].dbo.MedicineTb1 WHERE MedName LIKE N'عرض60-4925d6-%'")
 $account=[int](Scalar "SELECT COUNT(*) FROM [$target].dbo.EmployeeTb1 WHERE EmpId=N'3' AND EmpName=N'demo4925'")
 if($medicines -ne 60 -or $account -ne 1){throw 'Restored demo contents differ; inspect destination, no automatic delete.'}
 [pscustomobject]@{Server=$ServerInstance;Database=$target;BackupSHA256=$actual;RestoreExecuted=$true;CheckDbPassed=$true;DemoMedicines=$medicines;DemoAccountPresent=$true;NativeLoginAccepted=$false;CleanClientInstallAccepted=$false}|ConvertTo-Json
}finally{$connection.Dispose()}



