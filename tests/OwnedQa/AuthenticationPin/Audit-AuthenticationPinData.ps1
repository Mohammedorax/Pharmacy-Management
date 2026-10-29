param([string]$VerifiedBackupJobPath = "$PSScriptRoot/output/login-edited-demo-backup-job.json")
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path ($PSScriptRoot+'/output') -Force | Out-Null
$root='D:/Comp/Pharmacy-Management-master'
$job=Get-Content "$PSScriptRoot/output/authentication-pin-sql-qa-job.json" -Raw|ConvertFrom-Json
$original=Get-Content $VerifiedBackupJobPath -Raw|ConvertFrom-Json
$reference=Get-Content "$($original.Directory)/source-before.json" -Raw|ConvertFrom-Json
function Hash([string]$text){$sha=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','')}finally{$sha.Dispose()}}
$audits=@()
foreach($database in @($job.Databases)+@($job.OriginalSource)){
 if($database-cne$job.OriginalSource-and$database-notin$job.Databases){throw 'Exact QA targets only'}
 $c=[Data.SqlClient.SqlConnection]::new("Data Source=.;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False")
 try{
  $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText="SELECT s.name SchemaName,t.name TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name";$dt=[Data.DataTable]::new();$ad=[Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$ad.Fill($dt);$ad.Dispose();$cmd.Dispose()
  $snapshot=[ordered]@{}
  foreach($table in $dt.Rows){
   $schema=([string]$table.SchemaName).Replace(']',']]');$name=([string]$table.TableName).Replace(']',']]');$cmd=$c.CreateCommand();$cmd.CommandText="SELECT CONVERT(nvarchar(max),(SELECT * FROM [$schema].[$name] FOR XML RAW,BINARY BASE64,ROOT('rows'),TYPE))";[xml]$xml=[string]$cmd.ExecuteScalar();$cmd.Dispose()
   $rows=[string[]]@($xml.rows.ChildNodes|ForEach-Object{$_.OuterXml});[Array]::Sort($rows,[StringComparer]::Ordinal)
   $snapshot[([string]$table.SchemaName+'.'+[string]$table.TableName)]=@{Rows=$rows.Count;SHA256=(Hash ($rows-join"`n"))}
  }
  if($snapshot.Count-ne69-or@($reference.PSObject.Properties).Count-ne69){throw 'Expected exact69 table set'}
  $changed=@();foreach($key in $snapshot.Keys){$r=$reference.$key;if(!$r){throw 'Table set drift'};if($r.Rows-ne$snapshot[$key].Rows-or$r.SHA256-cne$snapshot[$key].SHA256){$changed+=$key}}
  if($database-ceq$job.OriginalSource){if($changed.Count){throw 'Original source changed'}}
  elseif(($changed|Sort-Object)-join'|'-cne'dbo.AuditLogTb|dbo.EmployeeTb1'){throw ('Unexpected changed tables: '+($changed-join','))}
  $snapshot|ConvertTo-Json -Depth 8|Set-Content "$($job.Directory)/$database-after.json" -Encoding utf8
  $cmd=$c.CreateCommand();$cmd.CommandText='DBCC CHECKDB ('+('['+$database.Replace(']',']]')+']')+') WITH NO_INFOMSGS,ALL_ERRORMSGS';$cmd.CommandTimeout=120;[void]$cmd.ExecuteNonQuery();$cmd.Dispose()
  $audits+=@{Database=$database;TableCount=$snapshot.Count;ChangedTables=$changed;CheckDbPassed=$true;Original=$database-ceq$job.OriginalSource}
 }finally{$c.Dispose()}
}
@{Accepted=$true;Directory=$job.Directory;Owner=$job.Owner;Audits=$audits;SourceUnchanged=$true;NonEmployeeAuditTablesUnchanged=$true;PhysicalPrinting=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 10|Set-Content "$PSScriptRoot/output/authentication-pin-data-accepted.json" -Encoding utf8
Write-Output 'PASS all69 tables each: original QA unchanged; clones changed only owned employee/audit tables; DBCC all3 passed'
