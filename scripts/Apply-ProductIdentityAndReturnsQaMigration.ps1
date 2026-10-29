[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('(?i).+_qa$')]
    [string]$DatabaseName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$VerifiedBackupPath,

    [string]$MigrationPath = (Join-Path $PSScriptRoot '..\database\migrations\0007_product_identity_and_partial_returns.sql')
)

$ErrorActionPreference = 'Stop'
$toolVersion = '1.0.0'
$resolvedBackup = [System.IO.Path]::GetFullPath($VerifiedBackupPath)
$resolvedMigration = [System.IO.Path]::GetFullPath($MigrationPath)

if (-not (Test-Path -LiteralPath $resolvedBackup -PathType Leaf) -or [System.IO.Path]::GetExtension($resolvedBackup) -ine '.bak') {
    throw 'يلزم تمرير نسخة .bak موجودة ومتحقق منها؛ لا يعمل هذا المشغّل على الإنتاج.'
}
if (-not (Test-Path -LiteralPath $resolvedMigration -PathType Leaf)) {
    throw "لم يعثر على ملف الترحيل: $resolvedMigration"
}

function New-Connection([string]$catalog) {
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $ServerInstance
    $builder['Initial Catalog'] = $catalog
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $ServerInstance -eq '.' -or $ServerInstance -eq '(local)' -or $ServerInstance -eq 'localhost'
    $builder['Connect Timeout'] = 15
    return [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
}

function Invoke-NonQuery([System.Data.SqlClient.SqlConnection]$connection, [string]$sql, [int]$timeout = 120) {
    $command = $connection.CreateCommand()
    try {
        $command.CommandTimeout = $timeout
        $command.CommandText = $sql
        [void]$command.ExecuteNonQuery()
    }
    finally { $command.Dispose() }
}

$migrationId = [System.IO.Path]::GetFileNameWithoutExtension($resolvedMigration)
$migrationHash = (Get-FileHash -LiteralPath $resolvedMigration -Algorithm SHA256).Hash.ToUpperInvariant()
$master = New-Connection 'master'
$database = New-Connection $DatabaseName
try {
    $master.Open()
    $verify = $master.CreateCommand()
    try {
        $verify.CommandTimeout = 300
        $verify.CommandText = 'RESTORE VERIFYONLY FROM DISK = @BackupPath WITH CHECKSUM;'
        [void]$verify.Parameters.Add('@BackupPath', [System.Data.SqlDbType]::NVarChar, 260)
        $verify.Parameters['@BackupPath'].Value = $resolvedBackup
        [void]$verify.ExecuteNonQuery()
    }
    finally { $verify.Dispose() }

    $database.Open()
    $identity = $database.CreateCommand()
    try {
        $identity.CommandText = 'SELECT DB_NAME();'
        $actualDatabase = [string]$identity.ExecuteScalar()
    }
    finally { $identity.Dispose() }
    if (-not [string]::Equals($actualDatabase, $DatabaseName, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "اتصال SQL لا يشير إلى قاعدة QA المطلوبة. المتوقع: $DatabaseName؛ الفعلي: $actualDatabase"
    }

    if (-not $PSCmdlet.ShouldProcess($DatabaseName, "تطبيق ترحيل $migrationId")) { return }

    Invoke-NonQuery $database @'
IF OBJECT_ID(N'dbo.PharmacySchemaMigrationRegistryTb', N'U') IS NULL
CREATE TABLE dbo.PharmacySchemaMigrationRegistryTb (
    MigrationId NVARCHAR(200) NOT NULL CONSTRAINT PK_PharmacySchemaMigrationRegistryTb PRIMARY KEY,
    ScriptSha256 CHAR(64) NOT NULL,
    ToolVersion NVARCHAR(40) NOT NULL,
    AppliedBy NVARCHAR(256) NOT NULL,
    AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_PharmacySchemaMigrationRegistryTb_AppliedAtUtc DEFAULT SYSUTCDATETIME()
);
'@

    $record = $database.CreateCommand()
    try {
        $record.CommandText = 'SELECT ScriptSha256 FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId = @MigrationId;'
        [void]$record.Parameters.Add('@MigrationId', [System.Data.SqlDbType]::NVarChar, 200)
        $record.Parameters['@MigrationId'].Value = $migrationId
        $recordedHash = $record.ExecuteScalar()
    }
    finally { $record.Dispose() }
    if ($null -ne $recordedHash -and $recordedHash -ne [System.DBNull]::Value) {
        if (-not [string]::Equals([string]$recordedHash, $migrationHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "تم تعديل ترحيل مسجل: $migrationId. أضف ترحيلاً جديداً بدلاً من تعديله."
        }
        Write-Output "مطبق سابقاً وبصمته مطابقة: $migrationId"
        return
    }

    Invoke-NonQuery $database (Get-Content -LiteralPath $resolvedMigration -Raw -Encoding UTF8) 180

    $insert = $database.CreateCommand()
    try {
        $insert.CommandText = @'
INSERT INTO dbo.PharmacySchemaMigrationRegistryTb (MigrationId, ScriptSha256, ToolVersion, AppliedBy)
VALUES (@MigrationId, @ScriptSha256, @ToolVersion, @AppliedBy);
'@
        [void]$insert.Parameters.Add('@MigrationId', [System.Data.SqlDbType]::NVarChar, 200)
        [void]$insert.Parameters.Add('@ScriptSha256', [System.Data.SqlDbType]::Char, 64)
        [void]$insert.Parameters.Add('@ToolVersion', [System.Data.SqlDbType]::NVarChar, 40)
        [void]$insert.Parameters.Add('@AppliedBy', [System.Data.SqlDbType]::NVarChar, 256)
        $insert.Parameters['@MigrationId'].Value = $migrationId
        $insert.Parameters['@ScriptSha256'].Value = $migrationHash
        $insert.Parameters['@ToolVersion'].Value = $toolVersion
        $insert.Parameters['@AppliedBy'].Value = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        [void]$insert.ExecuteNonQuery()
    }
    finally { $insert.Dispose() }
    Write-Output "تم تطبيق وتسجيل: $migrationId على $DatabaseName"
}
finally {
    if ($null -ne $database) { $database.Dispose() }
    if ($null -ne $master) { $master.Dispose() }
}
