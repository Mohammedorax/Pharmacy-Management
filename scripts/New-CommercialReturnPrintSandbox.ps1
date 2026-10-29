[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [string]$OutputRoot = 'D:\PharmacyQA\work',
    [switch]$ConfirmIsolatedQaWrites,
    [string]$ResumeSandboxManifest
)
$ErrorActionPreference='Stop'
if(-not $ConfirmIsolatedQaWrites){throw 'Explicit -ConfirmIsolatedQaWrites is required.'}
if($OutputRoot -notmatch '^[dD]:[\\/]'){throw 'Print sandbox preparation requires an absolute D: output path.'}
$ExecutablePath=[IO.Path]::GetFullPath($ExecutablePath)
if($ExecutablePath -notmatch '^[dD]:\\' -or [IO.Path]::GetExtension($ExecutablePath) -ine '.exe' -or -not [IO.File]::Exists($ExecutablePath)){throw 'An existing D: application executable is required.'}
$token=[Guid]::NewGuid().ToString('N')
$target='PharmacyReturnPrint_'+(Get-Date -Format 'yyyyMMddHHmmss')+'_'+$token.Substring(0,8)+'_qa'
$run=[IO.Path]::GetFullPath((Join-Path $OutputRoot ('return-print-'+$token)))
if($ResumeSandboxManifest){
    $manifest=[IO.Path]::GetFullPath($ResumeSandboxManifest)
    if($manifest -notmatch '^[dD]:\\' -or [IO.Path]::GetFileName($manifest) -ne 'sandbox.json'){throw 'Resume requires a D: sandbox.json manifest.'}
    $saved=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    $token=[string]$saved.OwnerToken;$target=[string]$saved.Target;$run=[IO.Path]::GetDirectoryName($manifest)
    if($token -notmatch '^[a-f0-9]{32}$' -or $target -notmatch '^PharmacyReturnPrint_[0-9]{14}_[a-f0-9]{8}_qa$' -or
        -not $target.EndsWith('_'+$token.Substring(0,8)+'_qa') -or $saved.Source -ne 'PharmacyManagement_qa' -or
        [int]$saved.TargetId -le 0 -or $saved.Status -ne 'Failed' -or -not $saved.SourceUnchanged){throw 'Resume manifest is not a failed owned print sandbox with unchanged source.'}
}else{
    if(Test-Path -LiteralPath $run){throw 'Run directory already exists.'}
    [void][IO.Directory]::CreateDirectory($run)
}
$dataRoot=Join-Path $run 'sql-data'
[void][IO.Directory]::CreateDirectory($dataRoot)
$evidence=[ordered]@{Source='PharmacyManagement_qa';Target=$target;OwnerToken=$token;Status='Preparing';Run=$run;TargetId=$null;SourceUnchanged=$false;RetainedForNativeAcceptance=$true}
if($ResumeSandboxManifest){$evidence.TargetId=[int]$saved.TargetId;$evidence.PreviousFailure=$saved.Error;$evidence.Resumed=$true;$evidence.SourceBackupSha256=$saved.SourceBackupSha256}
$master=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy.ReturnPrintSandbox')
function Command([string]$sql,[hashtable]$parameters=@{}){
    $c=$master.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=180
    foreach($k in $parameters.Keys){[void]$c.Parameters.AddWithValue($k,$parameters[$k])};return $c
}
function Execute([string]$sql,[hashtable]$parameters=@{}){$c=Command $sql $parameters;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
function Scalar([string]$sql,[hashtable]$parameters=@{}){$c=Command $sql $parameters;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Table([string]$sql,[hashtable]$parameters=@{}){
    $c=Command $sql $parameters;$t=[Data.DataTable]::new()
    try{$a=[Data.SqlClient.SqlDataAdapter]::new($c);try{[void]$a.Fill($t)}finally{$a.Dispose()}}finally{$c.Dispose()};return ,$t
}
function SourceFingerprint{
    $tables=Table 'SELECT s.name AS SchemaName,t.name AS TableName FROM [PharmacyManagement_qa].sys.tables t JOIN [PharmacyManagement_qa].sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;'
    $result=[ordered]@{}
    foreach($t in $tables.Rows){
        $schema=([string]$t.SchemaName).Replace(']',']]');$name=([string]$t.TableName).Replace(']',']]')
        [xml]$xml=[string](Scalar ('SELECT CONVERT(nvarchar(max),(SELECT * FROM [PharmacyManagement_qa].['+$schema+'].['+$name+'] FOR XML RAW,ROOT(''rows''),BINARY BASE64));'))
        $rows=[Collections.Generic.List[string]]::new();foreach($r in $xml.rows.ChildNodes){$rows.Add($r.OuterXml)};$rows.Sort([StringComparer]::Ordinal)
        $sha=[Security.Cryptography.SHA256]::Create();try{$result[([string]$t.SchemaName+'.'+$t.TableName)]=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]::Join("`n",$rows)))).Replace('-','')}finally{$sha.Dispose()}
    };$tables.Dispose();if($result.Count -eq 0){throw 'Empty source inventory.'};return $result
}
function AssertOwned{
    $id=Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$target}
    $owner=Scalar ('SELECT CONVERT(nvarchar(64),value) FROM ['+$target+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';')
    if([int]$id -ne $evidence.TargetId -or [string]$owner -cne $token){throw 'Sandbox identity or ownership changed.'}
}
function Worker([string]$name,[string[]]$arguments){
    AssertOwned
    $p=Start-Process -FilePath $ExecutablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru -WorkingDirectory (Split-Path -Parent $ExecutablePath) -RedirectStandardOutput (Join-Path $run ($name+'.stdout.log')) -RedirectStandardError (Join-Path $run ($name+'.stderr.log'))
    try{if(-not $p.WaitForExit(180000)){throw ($name+' did not finish; owned worker PID='+$p.Id)};if($p.ExitCode -ne 0){throw ($name+' failed, see logs. ExitCode='+$p.ExitCode)}}finally{$p.Dispose()}
}
$before=$null
try{
    $master.Open()
    if($ResumeSandboxManifest){AssertOwned}else{if((Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$target}) -isnot [DBNull]){throw 'Target already exists; overwrite refused.'}}
    $before=SourceFingerprint;$evidence.SourceBefore=$before
    if(-not $ResumeSandboxManifest){
    $backup=Join-Path $run 'source-copy-only.bak'
    Execute 'BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;' @{'@Path'=$backup}
    Execute 'RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;' @{'@Path'=$backup}
    $header=Table 'RESTORE HEADERONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($header.Rows.Count -ne 1 -or $header.Rows[0].DatabaseName -ne 'PharmacyManagement_qa' -or [int]$header.Rows[0].BackupType -ne 1 -or -not $header.Rows[0].HasBackupChecksums -or $header.Rows[0].IsDamaged){throw 'Unexpected source backup header.'};$header.Dispose()
    $evidence.SourceBackupSha256=(Get-FileHash -LiteralPath $backup).Hash
    $files=Table 'RESTORE FILELISTONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($files.Rows.Count -eq 0){throw 'Empty source backup file list.'}
    $moves=@();$index=0
    foreach($f in $files.Rows){
        $index++;if($f.Type -notin @('D','L')){throw 'Unsupported SQL file type.'}
        $ext=if($f.Type -eq 'L'){'.ldf'}else{'.mdf'}
        $path=[IO.Path]::GetFullPath((Join-Path $dataRoot ($target+'_'+$index+$ext)))
        if(-not $path.StartsWith($dataRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $path) -or [int](Scalar 'SELECT COUNT(*) FROM sys.master_files WHERE physical_name=@Path;' @{'@Path'=$path}) -ne 0){throw 'Unsafe SQL restore path.'}
        $moves += "MOVE N'$(([string]$f.LogicalName).Replace("'","''"))' TO N'$($path.Replace("'","''"))'"
    };$files.Dispose()
    Execute ('RESTORE DATABASE ['+$target+'] FROM DISK=@Path WITH CHECKSUM,RECOVERY, '+($moves -join ',')+';') @{'@Path'=$backup}
    $evidence.TargetId=[int](Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$target})
    Execute ('USE ['+$target+']; EXEC sys.sp_addextendedproperty @name=N''QaTest.Owner'',@value=@Token;') @{'@Token'=$token};$master.ChangeDatabase('master')
    AssertOwned
    $schemaBackup=Join-Path $run 'before-commercial-schema.bak'
    Execute ('BACKUP DATABASE ['+$target+'] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;') @{'@Path'=$schemaBackup}
    }
    $schemaBackup=Join-Path $run 'before-commercial-schema.bak'
    $evidence.SchemaBackupSha256=(Get-FileHash -LiteralPath $schemaBackup).Hash
    Worker 'install-commercial' @('--install-commercial-schema-qa','.',$target,$schemaBackup)
    Worker 'schema-readonly' @('--verify-qa-commercial-return-schema-readonly','.',$target)
    Worker 'seed-print-fixture' @('--verify-owned-return-print-fixture','.',$target,$token,[string]$evidence.TargetId,'--confirm-owned-print-fixture-writes')
    AssertOwned
    Execute ('DBCC CHECKDB (['+$target+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;')
    $documents=Table ('SELECT h.ReturnId,h.OriginalInvoiceId,h.ReturnInvoiceId,h.RequestId,i.InvoiceNumber,i.IssuedAtUtc,i.NetTotal FROM ['+$target+'].dbo.CommercialReturnHeadersTb h JOIN ['+$target+'].dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId ORDER BY i.InvoiceNumber;')
    if($documents.Rows.Count -lt 1){throw 'No saved return documents available for print acceptance.'}
    $items=@();foreach($row in $documents.Rows){$r=[ordered]@{};foreach($col in $documents.Columns){$r[$col.ColumnName]=$row[$col]};$items += [pscustomobject]$r};$documents.Dispose();$evidence.Documents=$items
    $evidence.BinarySha256=(Get-FileHash -LiteralPath $ExecutablePath).Hash
    $evidence.Status='ReadyForPackaging'
}catch{$evidence.Status='Failed';$evidence.Error=$_.Exception.Message;throw}
finally{
    try{
        if($null -ne $before -and $master.State -eq [Data.ConnectionState]::Open){
            $master.ChangeDatabase('master');$after=SourceFingerprint;$evidence.SourceAfter=$after
            $same=$before.Count -eq $after.Count
            foreach($k in $before.Keys){if(-not $after.Contains($k) -or $before[$k] -ne $after[$k]){$same=$false}}
            $evidence.SourceUnchanged=$same;if(-not $same){$evidence.Status='SourceChanged';throw 'Source QA changed during preparation; review concurrent activity.'}
        }
    }finally{$master.Dispose();$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'sandbox.json') -Encoding utf8;Write-Output ('Evidence: '+$run)}
}
