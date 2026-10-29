#requires -Version 5.1
<#
.SYNOPSIS
    Refuses to accept a build when the ledger read indexes from migration 0032 are absent or wrong.

.DESCRIPTION
    The general ledger read drives from the date range. Without the three indexes from
    database/migrations/0032_ledger_read_indexes.sql the read is measured ~2.8x slower, so a binary
    that contains the new read must not be accepted against a database that lacks them.

    The contract is the full index definition, not just its name: schema, every key column in order,
    every included column, and the index must be enabled, unfiltered and not hypothetical.

    Read-only against a real database: it inspects sys.indexes/sys.index_columns only.
    Never writes, never creates, never drops (except the owned probe it creates itself in
    -ProveWithOwnedProbe mode, which it always tries to drop, even after a failure).

.PARAMETER ServerInstance
    SQL Server instance, e.g. localhost.

.PARAMETER Database
    Target database. Must end with _qa, _test or _sandbox (same rule as the migration itself)
    unless -AllowProductionMetadata is given for a read-only deployment check.

.PARAMETER AllowProductionMetadata
    Allows a strictly read-only metadata check against a non-QA database name.

.PARAMETER ProveWithOwnedProbe
    Creates an owned QA database with the minimal ledger schema and no indexes, proves the guard
    fails red there, applies migration 0032, proves it passes green, then drops the probe.

.PARAMETER SelfTest
    Exercises the detection logic with in-memory rows only. No SQL connection is opened.

.EXAMPLE
    .\Test-SchemaPrerequisites.ps1 -SelfTest

.EXAMPLE
    .\Test-SchemaPrerequisites.ps1 -ServerInstance localhost -ProveWithOwnedProbe

.EXAMPLE
    .\Test-SchemaPrerequisites.ps1 -ServerInstance localhost -Database PharmacyRestore_..._qa
#>
[CmdletBinding(DefaultParameterSetName = 'Check')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Check')]
    [Parameter(Mandatory, ParameterSetName = 'Probe')]
    [Parameter(Mandatory, ParameterSetName = 'Production')]
    [string]$ServerInstance,

    [Parameter(Mandatory, ParameterSetName = 'Check')]
    [Parameter(Mandatory, ParameterSetName = 'Production')]
    [string]$Database,

    [Parameter(Mandatory, ParameterSetName = 'Production')]
    [switch]$AllowProductionMetadata,

    [Parameter(Mandatory, ParameterSetName = 'Probe')]
    [switch]$ProveWithOwnedProbe,

    [Parameter(Mandatory, ParameterSetName = 'Self')]
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The exact contract of migration 0032: index name, owning table, every key column in order, and
# every included column (order-insensitive).
$script:RequiredIndexes = @(
    [pscustomobject]@{
        Index = 'IX_JournalEntriesTb_EntryDate'
        Table = 'JournalEntriesTb'
        Keys = 'EntryDate,JournalEntryId'
        Includes = 'Description,SourceId,SourceType'
    }
    [pscustomobject]@{
        Index = 'IX_JournalEntryLinesTb_JournalEntry_Account'
        Table = 'JournalEntryLinesTb'
        Keys = 'JournalEntryId,AccountCode'
        Includes = 'CostCenterId,Credit,Debit'
    }
    [pscustomobject]@{
        Index = 'IX_JournalEntryLinesTb_Account_JournalEntry'
        Table = 'JournalEntryLinesTb'
        Keys = 'AccountCode,JournalEntryId'
        Includes = 'CostCenterId,Credit,Debit,JournalEntryLineId'
    }
)

$script:IndexQuery = @'
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName,
       t.name AS TableName,
       i.name AS IndexName,
       i.is_disabled AS IsDisabled,
       i.has_filter AS HasFilter,
       i.is_hypothetical AS IsHypothetical,
       ISNULL(STUFF((SELECT ',' + c.name
                     FROM sys.index_columns k
                     JOIN sys.columns c ON c.object_id = k.object_id AND c.column_id = k.column_id
                     WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.is_included_column = 0
                     ORDER BY k.key_ordinal
                     FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, ''), '') AS KeyColumns,
       ISNULL(STUFF((SELECT ',' + c.name
                     FROM sys.index_columns k
                     JOIN sys.columns c ON c.object_id = k.object_id AND c.column_id = k.column_id
                     WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.is_included_column = 1
                     ORDER BY c.name
                     FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, ''), '') AS IncludeColumns
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IN (N'IX_JournalEntriesTb_EntryDate',
                 N'IX_JournalEntryLinesTb_JournalEntry_Account',
                 N'IX_JournalEntryLinesTb_Account_JournalEntry');
'@

function ConvertTo-OrderedKeyList {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    return @($Value -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
}

function ConvertTo-IncludeSet {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    return (($Value -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Sort-Object) -join ',')
}

function Test-KeyList {
    param([string]$Observed, [string]$Required)
    $left = @(ConvertTo-OrderedKeyList -Value $Observed)
    $right = @(ConvertTo-OrderedKeyList -Value $Required)
    if ($left.Count -ne $right.Count) { return $false }
    for ($index = 0; $index -lt $right.Count; $index++) {
        if ($left[$index] -ne $right[$index]) { return $false }
    }
    return $true
}

function Get-MissingLedgerIndexes {
    param([object[]]$Observed)

    $missing = New-Object System.Collections.Generic.List[object]
    foreach ($required in $script:RequiredIndexes) {
        $hit = $null
        foreach ($row in @($Observed)) {
            if ($null -eq $row) { continue }
            if ([string]$row.IndexName -ne $required.Index) { continue }
            if ([string]$row.TableName -ne $required.Table) { continue }
            if (-not [string]::IsNullOrWhiteSpace([string]$row.SchemaName) -and [string]$row.SchemaName -ne 'dbo') { continue }
            if ([bool]$row.IsDisabled) { continue }
            if ([bool]$row.HasFilter) { continue }
            if ($null -ne $row.PSObject.Properties['IsHypothetical'] -and [bool]$row.IsHypothetical) { continue }
            if (-not (Test-KeyList -Observed ([string]$row.KeyColumns) -Required $required.Keys)) { continue }
            if ((ConvertTo-IncludeSet -Value ([string]$row.IncludeColumns)) -ne (ConvertTo-IncludeSet -Value $required.Includes)) { continue }
            $hit = $row
            break
        }
        if ($null -eq $hit) { $missing.Add($required) }
    }
    ,$missing.ToArray()
}

function New-Connection {
    param([string]$Catalog)
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = $ServerInstance
    if ($Catalog) { $builder['Initial Catalog'] = $Catalog }
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $false
    $builder['TrustServerCertificate'] = $true
    $builder['Connect Timeout'] = 30
    New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
}

function Invoke-NonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql)
    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql
        $command.CommandTimeout = 120
        [void]$command.ExecuteNonQuery()
    }
    finally { $command.Dispose() }
}

function Get-LedgerIndexRows {
    param([System.Data.SqlClient.SqlConnection]$Connection)
    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $script:IndexQuery
        $command.CommandTimeout = 30
        $rows = New-Object System.Collections.Generic.List[object]
        $reader = $command.ExecuteReader()
        try {
            while ($reader.Read()) {
                $rows.Add([pscustomobject]@{
                    SchemaName      = $reader.GetString(0)
                    TableName       = $reader.GetString(1)
                    IndexName       = [string]$reader.GetString(2)
                    IsDisabled      = $reader.GetBoolean(3)
                    HasFilter       = $reader.GetBoolean(4)
                    IsHypothetical  = $reader.GetBoolean(5)
                    KeyColumns      = $reader.GetString(6)
                    IncludeColumns  = $reader.GetString(7)
                })
            }
        }
        finally { $reader.Dispose() }
        ,$rows.ToArray()
    }
    finally { $command.Dispose() }
}

function Write-MissingReport {
    param([object[]]$Missing)
    foreach ($item in @($Missing)) {
        Write-Host ("MISSING or MISMATCHED: dbo.{0} key({1}) include({2})" -f $item.Index, $item.Keys, $item.Includes)
    }
    Write-Host 'Action: apply database/migrations/0032_ledger_read_indexes.sql before running this binary.'
    Write-Host 'Reason: the ledger read is ~2.8x slower without these three indexes (owned QA measurement).'
}

function Invoke-SelfTest {
    function Row([string]$index, [string]$table, [string]$keys, [string]$includes, [bool]$disabled = $false, [bool]$filtered = $false, [bool]$hypothetical = $false, [string]$schema = 'dbo') {
        [pscustomobject]@{
            SchemaName = $schema; TableName = $table; IndexName = $index
            IsDisabled = $disabled; HasFilter = $filtered; IsHypothetical = $hypothetical
            KeyColumns = $keys; IncludeColumns = $includes
        }
    }

    $all = @(
        Row 'IX_JournalEntriesTb_EntryDate' 'JournalEntriesTb' 'EntryDate,JournalEntryId' 'Description,SourceId,SourceType'
        Row 'IX_JournalEntryLinesTb_JournalEntry_Account' 'JournalEntryLinesTb' 'JournalEntryId,AccountCode' 'CostCenterId,Credit,Debit'
        Row 'IX_JournalEntryLinesTb_Account_JournalEntry' 'JournalEntryLinesTb' 'AccountCode,JournalEntryId' 'CostCenterId,Credit,Debit,JournalEntryLineId'
    )
    $missing = Get-MissingLedgerIndexes -Observed $all
    if (@($missing).Count -ne 0) { throw ('Self-test A failed: expected 0 missing, got ' + @($missing).Count) }
    Write-Host 'PASS self-test A: the complete definition set reports nothing missing.'

    $disabled = @(
        Row 'IX_JournalEntriesTb_EntryDate' 'JournalEntriesTb' 'EntryDate,JournalEntryId' 'Description,SourceId,SourceType'
        Row 'IX_JournalEntryLinesTb_JournalEntry_Account' 'JournalEntryLinesTb' 'JournalEntryId,AccountCode' 'CostCenterId,Credit,Debit' $true
        Row 'IX_JournalEntryLinesTb_Account_JournalEntry' 'JournalEntryLinesTb' 'AccountCode,JournalEntryId' 'CostCenterId,Credit,Debit,JournalEntryLineId'
    )
    $missing = Get-MissingLedgerIndexes -Observed $disabled
    if (@($missing).Count -ne 1 -or $missing[0].Index -ne 'IX_JournalEntryLinesTb_JournalEntry_Account') {
        throw 'Self-test B failed: a disabled index must be reported missing.'
    }
    Write-Host 'PASS self-test B: a disabled index is reported missing.'

    $wrongSecondKey = @(
        Row 'IX_JournalEntriesTb_EntryDate' 'JournalEntriesTb' 'EntryDate' 'Description,SourceId,SourceType'
        Row 'IX_JournalEntryLinesTb_JournalEntry_Account' 'JournalEntryLinesTb' 'JournalEntryId,AccountCode' 'CostCenterId,Credit,Debit'
        Row 'IX_JournalEntryLinesTb_Account_JournalEntry' 'JournalEntryLinesTb' 'AccountCode,JournalEntryId' 'CostCenterId,Credit,Debit,JournalEntryLineId'
    )
    $missing = Get-MissingLedgerIndexes -Observed $wrongSecondKey
    if (@($missing).Count -ne 1 -or $missing[0].Index -ne 'IX_JournalEntriesTb_EntryDate') {
        throw 'Self-test C failed: a same-named index with a missing key column must be reported missing.'
    }
    Write-Host 'PASS self-test C: a same-named index with a missing key column is reported missing.'

    $missingInclude = @(
        Row 'IX_JournalEntriesTb_EntryDate' 'JournalEntriesTb' 'EntryDate,JournalEntryId' 'Description,SourceId'
        Row 'IX_JournalEntryLinesTb_JournalEntry_Account' 'JournalEntryLinesTb' 'JournalEntryId,AccountCode' 'CostCenterId,Credit,Debit'
        Row 'IX_JournalEntryLinesTb_Account_JournalEntry' 'JournalEntryLinesTb' 'AccountCode,JournalEntryId' 'CostCenterId,Credit,Debit,JournalEntryLineId'
    )
    $missing = Get-MissingLedgerIndexes -Observed $missingInclude
    if (@($missing).Count -ne 1) { throw 'Self-test D failed: a missing included column must be reported.' }
    Write-Host 'PASS self-test D: a missing included column is reported missing.'

    $wrongSchema = @(
        Row 'IX_JournalEntriesTb_EntryDate' 'JournalEntriesTb' 'EntryDate,JournalEntryId' 'Description,SourceId,SourceType' $false $false $false 'audit'
    )
    $missing = Get-MissingLedgerIndexes -Observed $wrongSchema
    if (@($missing).Count -ne 3) { throw 'Self-test E failed: an index outside dbo must not satisfy the contract.' }
    Write-Host 'PASS self-test E: an index outside dbo does not satisfy the contract.'

    $missing = Get-MissingLedgerIndexes -Observed @()
    if (@($missing).Count -ne 3) { throw 'Self-test F failed: an empty catalog must report all three missing.' }
    Write-Host 'PASS self-test F: an empty catalog reports all three missing.'

    Write-Host 'PASS schema prerequisite self-test: 6 assertions; no SQL connection was opened.'
}

function Assert-QaDatabaseName {
    param([string]$Name)
    if ($Name -match '_(qa|test|sandbox)$') { return }
    if ($AllowProductionMetadata) { return }
    throw ("Database '{0}' is not an owned QA/test/sandbox database. Pass -AllowProductionMetadata for a strictly read-only deployment check." -f $Name)
}

function Invoke-Check {
    param([string]$Catalog)
    Assert-QaDatabaseName -Name $Catalog
    $connection = New-Connection -Catalog $Catalog
    try {
        $connection.Open()
        $actual = $connection.Database
        if ($AllowProductionMetadata) {
            Write-Host ("OBSERVED read-only metadata check: server={0}; database={1}." -f $ServerInstance, $actual)
        }
        elseif (-not ($actual -match '_(qa|test|sandbox)$')) {
            throw ('Refusing to inspect {0}: it is not an owned QA database.' -f $actual)
        }
        $rows = Get-LedgerIndexRows -Connection $connection
        $missing = Get-MissingLedgerIndexes -Observed $rows
        if (@($missing).Count -gt 0) {
            Write-MissingReport -Missing $missing
            Write-Host ("FAIL: {0} of 3 ledger read indexes are missing or mismatched in {1}." -f @($missing).Count, $actual)
            exit 1
        }
        foreach ($item in $script:RequiredIndexes) {
            Write-Host ("PASS index {0} on dbo.{1} key({2}) include({3})" -f $item.Index, $item.Table, $item.Keys, $item.Includes)
        }
        Write-Host ("PASS ledger read indexes: 3 of 3 match migration 0032 in {0}; deployment prerequisite satisfied." -f $actual)
        exit 0
    }
    finally {
        if ($connection.State -ne 'Closed') { $connection.Close() }
        $connection.Dispose()
    }
}

function Invoke-OwnedProbeProof {
    $token = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
    $probe = 'PlanGateProbe_{0}_{1}_qa' -f $token, ([guid]::NewGuid().ToString('N').Substring(0,8))
    $migration = Join-Path (Split-Path -Parent $PSScriptRoot) 'database\migrations\0032_ledger_read_indexes.sql'
    if (-not (Test-Path -LiteralPath $migration)) { throw ('Migration not found: {0}' -f $migration) }

    # Creation, tagging, proof and cleanup share one protected region so a failure can never leave an
    # owned probe database behind; cleanup is best effort and never hides the original error.
    try {
        $master = New-Connection
        try {
            $master.Open()
            Write-Host ("OBSERVED owned probe: creating {0} on {1} (materials are created and dropped by this run)." -f $probe, $ServerInstance)
            Invoke-NonQuery -Connection $master -Sql ("CREATE DATABASE [{0}];" -f $probe)
            Invoke-NonQuery -Connection $master -Sql ("ALTER DATABASE [{0}] SET RECOVERY SIMPLE;" -f $probe)
        }
        finally {
            if ($master.State -ne 'Closed') { $master.Close() }
            $master.Dispose()
        }

        $owner = New-Connection -Catalog $probe
        try {
            $owner.Open()
            if ($owner.Database -ne $probe) { throw ('Probe connection escaped {0}: {1}' -f $probe, $owner.Database) }
            # The ownership tag belongs to the probe database, so it is added inside that context.
            Invoke-NonQuery -Connection $owner -Sql "EXEC sys.sp_addextendedproperty @name=N'QaTest.Owner', @value=N'scripts/Test-SchemaPrerequisites.ps1';"
            Invoke-NonQuery -Connection $owner -Sql @'
-- A real migrated schema always carries the commercial version table. The probe simulates that
-- shape because migration 0032 references it (its IF guard does not prevent compile-time name
-- resolution of the INSERT inside the same batch, which is a known T-SQL behaviour).
CREATE TABLE dbo.CommercialSchemaVersionsTb(VersionNo INT NOT NULL CONSTRAINT PK_CommercialSchemaVersionsTb_Probe PRIMARY KEY);
CREATE TABLE dbo.JournalEntriesTb(
    JournalEntryId INT NOT NULL CONSTRAINT PK_JournalEntriesTb_Probe PRIMARY KEY,
    EntryDate DATETIME2 NOT NULL,
    SourceType NVARCHAR(50) NULL,
    SourceId NVARCHAR(50) NULL,
    Description NVARCHAR(400) NULL);
CREATE TABLE dbo.JournalEntryLinesTb(
    JournalEntryLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_JournalEntryLinesTb_Probe PRIMARY KEY,
    JournalEntryId INT NOT NULL,
    AccountCode NVARCHAR(20) NOT NULL,
    Debit DECIMAL(18,2) NOT NULL,
    Credit DECIMAL(18,2) NOT NULL,
    CostCenterId INT NULL);
'@
            $before = Get-MissingLedgerIndexes -Observed (Get-LedgerIndexRows -Connection $owner)
            if (@($before).Count -ne 3) {
                throw ('RED proof failed: a schema without indexes reported {0} missing instead of 3.' -f @($before).Count)
            }
            Write-Host 'PASS red proof: guard reports 3 of 3 missing on a schema without migration 0032.'

            $sql = [System.IO.File]::ReadAllText($migration)
            Invoke-NonQuery -Connection $owner -Sql $sql

            $after = Get-MissingLedgerIndexes -Observed (Get-LedgerIndexRows -Connection $owner)
            if (@($after).Count -ne 0) {
                Write-MissingReport -Missing $after
                throw ('GREEN proof failed: migration 0032 was applied but {0} indexes still do not match.' -f @($after).Count)
            }
            Write-Host 'PASS green proof: the same guard reports 0 missing after migration 0032.'
            Write-Host ("PASS owned probe: red-then-green proven on {0}; the probe is dropped next." -f $probe)
        }
        finally {
            if ($owner.State -ne 'Closed') { $owner.Close() }
            $owner.Dispose()
        }
    }
    finally {
        # Name-based cleanup that never destroys another run's probe: the ownership tag must match.
        $cleanup = New-Connection
        try {
            $cleanup.Open()
            $tagCommand = $cleanup.CreateCommand()
            try {
                $tagCommand.CommandText = ("SELECT CONVERT(NVARCHAR(200), value) FROM [{0}].sys.extended_properties WHERE name=N'QaTest.Owner'" -f $probe)
                $tag = [string]$tagCommand.ExecuteScalar()
            }
            catch { $tag = '' }
            finally { $tagCommand.Dispose() }
            if (-not [string]::IsNullOrWhiteSpace($tag) -and $tag -ne 'scripts/Test-SchemaPrerequisites.ps1') {
                Write-Warning ("Probe {0} carries a different owner tag ({1}); leaving it untouched." -f $probe, $tag)
            }
            elseif ($tag -eq 'scripts/Test-SchemaPrerequisites.ps1') {
                Invoke-NonQuery -Connection $cleanup -Sql ("ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{0}];" -f $probe)
            }
            $exists = $cleanup.CreateCommand()
            try { $exists.CommandText = ("SELECT DB_ID(N'{0}')" -f $probe); $remaining = $exists.ExecuteScalar() }
            finally { $exists.Dispose() }
            if ($null -ne $remaining -and $remaining -ne [DBNull]::Value) {
                throw ("Owned probe {0} still exists after cleanup." -f $probe)
            }
            Write-Host ("PASS owned probe cleanup: {0} is absent and its tag was verified." -f $probe)
        }
        catch {
            Write-Warning ("Probe cleanup failed for {0}: {1}" -f $probe, $_.Exception.Message)
        }
        finally {
            if ($cleanup.State -ne 'Closed') { $cleanup.Close() }
            $cleanup.Dispose()
        }
    }
}

switch ($PSCmdlet.ParameterSetName) {
    'Self'  { Invoke-SelfTest }
    'Probe' { Invoke-OwnedProbeProof }
    default { Invoke-Check -Catalog $Database }
}
