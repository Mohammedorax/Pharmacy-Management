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
$ownerToken=[Guid]::NewGuid().ToString('N')
$ownedDatabaseId=$null
$sourceBefore=$null
$acceptanceRun=$null
$cleanupVerified=$false
$savedConfigurationBytes=$null
$originalExecutable=[IO.Path]::GetFullPath($ExecutablePath)
$originalConfigurationHash=(Get-FileHash -LiteralPath ($originalExecutable+'.config') -Algorithm SHA256).Hash
$ownedBinaryRoot=Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\return-owned-binary-'+[Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $ownedBinaryRoot)
Get-ChildItem -LiteralPath (Split-Path -Parent $originalExecutable) | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $ownedBinaryRoot -Recurse}
$ExecutablePath=Join-Path $ownedBinaryRoot ([IO.Path]::GetFileName($originalExecutable))
if((Get-FileHash -LiteralPath $ExecutablePath).Hash -ne (Get-FileHash -LiteralPath $originalExecutable).Hash){throw 'Owned executable copy differs from input.'}
$sourceUnchanged=$false
function Read-SourceFingerprint {
    $parts=[Collections.Generic.List[string]]::new()
    foreach($entry in @(@('SalesInvoicesTb','InvoiceId'),@('SalesInvoiceLinesTb','LineId'),@('CommercialReturnHeadersTb','ReturnId'),@('CommercialReturnLinesTb','ReturnLineId'),@('JournalEntriesTb','JournalEntryId'),@('JournalEntryLinesTb','JournalEntryLineId'))) {
        $c=$masterConnection.CreateCommand()
        try {
            $c.CommandTimeout=60
            $c.CommandText='SELECT CONVERT(nvarchar(max),(SELECT * FROM ['+$SourceDatabase+'].dbo.['+$entry[0]+'] ORDER BY ['+$entry[1]+'] FOR XML RAW,ROOT(''rows''),BINARY BASE64));'
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

    $allowedBuildRoot=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    $fullExecutable=[IO.Path]::GetFullPath($ExecutablePath)
    if(-not $fullExecutable.StartsWith($allowedBuildRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Forced live UI config is allowed only on isolated artifact executable.'}
    $configurationPath=$fullExecutable+'.config'
    $savedConfigurationBytes=[IO.File]::ReadAllBytes($configurationPath)
    [xml]$configuration=[IO.File]::ReadAllText($configurationPath)
    $forced=$configuration.configuration.appSettings.SelectSingleNode('add[@key="ForceBuildConnectionProfile"]')
    if(-not $forced){$forced=$configuration.CreateElement('add');$forced.SetAttribute('key','ForceBuildConnectionProfile');[void]$configuration.configuration.appSettings.AppendChild($forced)}
    $forced.SetAttribute('value','true')
    foreach($entry in $configuration.configuration.connectionStrings.add){
        $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new($entry.connectionString);$builder.set_DataSource($ServerInstance);$builder.set_InitialCatalog($sandboxDatabase)
        $entry.SetAttribute('connectionString',$builder.ConnectionString)
    }
    $configuration.Save($configurationPath)
    $process=Start-Process -FilePath $ExecutablePath -ArgumentList '--verify-qa-commercial-return-concurrency',$ServerInstance,$sandboxDatabase,'--confirm-temporary-concurrency-database-writes' -Wait -PassThru -WindowStyle Hidden
    if($process.ExitCode -ne 0) { throw "Concurrency verifier failed with exit code $($process.ExitCode)." }
    $postDbcc=$masterConnection.CreateCommand()
    try {
        $postDbcc.CommandTimeout=600;$postDbcc.CommandText="DBCC CHECKDB ($sandboxIdentifier) WITH NO_INFOMSGS,TABLERESULTS;"
        $postAdapter=[Data.SqlClient.SqlDataAdapter]::new($postDbcc);$postResults=[Data.DataTable]::new()
        try {[void]$postAdapter.Fill($postResults);if($postResults.Rows.Count -ne 0){throw 'Owned QA post-commit DBCC returned integrity findings.'}} finally {$postAdapter.Dispose();$postResults.Dispose()}
    } finally {$postDbcc.Dispose()}
    Write-Output "PASS: two committed full return requests raced on $sandboxDatabase; exactly one succeeded."
    $acceptanceToken=[Guid]::NewGuid().ToString('N')
    $acceptanceRun=Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\return-populated-'+$acceptanceToken)
    [void](New-Item -ItemType Directory -Path $acceptanceRun)
    $acceptanceLog=Join-Path $acceptanceRun 'reads.stdout.log'
    $acceptanceError=Join-Path $acceptanceRun 'reads.stderr.log'
    $acceptanceHash=(Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
    $readProcess=Start-Process -FilePath $ExecutablePath -ArgumentList '--verify-qa-commercial-return-reads',$ServerInstance,$sandboxDatabase -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $acceptanceLog -RedirectStandardError $acceptanceError
    $readOutput=Get-Content -LiteralPath $acceptanceLog -Raw
    if($readProcess.ExitCode -ne 0 -or $readOutput -notmatch 'approved documents=([1-9][0-9]*)'){throw 'Populated return read acceptance failed or had no approved document.'}
    if($readOutput -notmatch 'observed active SQL cancellations=2'){throw 'Both active SQL cancellation paths were not accepted.'}
    $approvedCount=[int][regex]::Match($readOutput,'approved documents=([1-9][0-9]*)').Groups[1].Value
    if($readOutput -notmatch 'live UI cases=4'){throw 'Four actual SQL UI cases were not accepted.'}
    [ordered]@{LiveUiCases=4;ActiveSqlCanceled=2;PostCommitDbccPassed=$true;Status='populated read passed before cleanup';Database=$sandboxDatabase;ExecutableSHA256=$acceptanceHash;ApprovedDocuments=$approvedCount;Output=$readOutput;CleanupVerified=$false} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $acceptanceRun 'read-evidence.json') -Encoding utf8
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
        $e.Status=if($cleanupVerified -and $sourceUnchanged){'populated read passed; cleanup and source financial fingerprints verified'}else{'acceptance incomplete'}
        $e | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $acceptanceRun 'read-evidence.json') -Encoding utf8
    }
    if($savedConfigurationBytes){[IO.File]::WriteAllBytes($configurationPath,$savedConfigurationBytes)}
    $masterConnection.Dispose()
if((Get-FileHash -LiteralPath ($originalExecutable+'.config') -Algorithm SHA256).Hash -ne $originalConfigurationHash){throw 'Original executable configuration changed.'}
    if($created -and (-not $cleanupVerified -or -not $sourceUnchanged)){throw 'Owned QA cleanup/source fingerprint acceptance failed.'}
}
