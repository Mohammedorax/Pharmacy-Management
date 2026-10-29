[CmdletBinding()]
param(
    [string]$ServerInstance = '.',
    [string]$SourceDatabase = 'PharmacyManagement_qa',
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $sourceExecutable = Join-Path $PSScriptRoot '..\project_fo_3\bin\Release\project_fo_3.exe'
    $packageExecutable = Join-Path $PSScriptRoot '..\..\project_fo_3.exe'
    $ExecutablePath = @($sourceExecutable, $packageExecutable) |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}
if ($SourceDatabase -notmatch '^[A-Za-z0-9_]+_qa$') { throw 'Source database must be an explicitly named _qa database.' }
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) { throw 'Release executable is missing.' }

Add-Type -AssemblyName System.Data
$masterConnection = [System.Data.SqlClient.SqlConnection]::new("Server=$ServerInstance;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
$sandboxDatabase = 'PharmacyConcurrency_{0}_{1}_sandbox' -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0,8))
$backupPath = $null
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

    $process=Start-Process -FilePath $ExecutablePath -ArgumentList '--verify-qa-commercial-return-concurrency',$ServerInstance,$sandboxDatabase,'--confirm-temporary-concurrency-database-writes' -Wait -PassThru -NoNewWindow
    if($process.ExitCode -ne 0) { throw "Concurrency verifier failed with exit code $($process.ExitCode)." }
    Write-Output "PASS: two committed full return requests raced on $sandboxDatabase; exactly one succeeded."
}
finally {
    if($masterConnection.State -ne [Data.ConnectionState]::Open) { try { $masterConnection.Open() } catch {} }
    if($created -and $masterConnection.State -eq [Data.ConnectionState]::Open) {
        $sandboxIdentifier='['+$sandboxDatabase.Replace(']',']]')+']'
        try { Invoke-NonQuery $masterConnection ("ALTER DATABASE $sandboxIdentifier SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $sandboxIdentifier;") @{} } catch { Write-Warning "Temporary sandbox cleanup failed: $($_.Exception.Message)" }
    }
    if($backupPath -and $masterConnection.State -eq [Data.ConnectionState]::Open) {
        try { Invoke-NonQuery $masterConnection "EXEC master.dbo.xp_delete_file 0,@Backup,N'bak';" @{ '@Backup'=$backupPath } } catch { Write-Warning "Temporary backup cleanup failed: $($_.Exception.Message)" }
    }
    $masterConnection.Dispose()
}
