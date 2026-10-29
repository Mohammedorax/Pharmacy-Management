[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('(?i).+_(test|qa|sandbox)$')]
    [string]$DatabaseName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Test', 'Qa', 'Sandbox')]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$VerifiedBackupPath,

    # يلغي سؤال التأكيد فقط؛ لا يتجاوز WhatIf أو حواجز قاعدة الاختبار والنسخة.
    [switch]$Force,

    [string]$MigrationDirectory = (Join-Path $PSScriptRoot '..\database\migrations')
)

$ErrorActionPreference = 'Stop'
$toolVersion = '1.0.1'
if ($Force) { $ConfirmPreference = 'None' }
$resolvedBackup = [System.IO.Path]::GetFullPath($VerifiedBackupPath)
$resolvedMigrations = [System.IO.Path]::GetFullPath($MigrationDirectory)

if ([System.IO.Path]::GetExtension($resolvedBackup) -ine '.bak') {
    throw 'يلزم تمرير مسار نسخة .bak ليتحقق منه SQL Server؛ لا يسمح المشغّل بتخمين دفعات تاريخية أو التشغيل على إنتاج.'
}
if (-not (Test-Path -LiteralPath $resolvedMigrations -PathType Container)) {
    throw "لم يتم العثور على مجلد الترحيلات: $resolvedMigrations"
}
if ($DatabaseName -notmatch ('(?i)_' + [regex]::Escape($Environment) + '$')) {
    throw "اسم القاعدة يجب أن ينتهي بـ _$Environment ليتطابق مع البيئة المختارة."
}

function New-Connection([string]$catalog) {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    # يمرر PowerShell بعض خصائص SqlConnectionStringBuilder كفهرس قاموسي؛
    # استعمال المفاتيح القياسية يمنع توليد DataSource غير المدعوم.
    $builder['Data Source'] = $ServerInstance
    $builder['Initial Catalog'] = $catalog
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $ServerInstance -eq '.' -or $ServerInstance -eq '(local)' -or $ServerInstance -eq 'localhost'
    $builder['Connect Timeout'] = 15
    return New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
}

function Invoke-Scalar([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql) {
    $command = $Connection.CreateCommand()
    try { $command.CommandTimeout = 60; $command.CommandText = $Sql; return $command.ExecuteScalar() }
    finally { $command.Dispose() }
}

# معاينة الملفات فقط، قبل إنشاء أي اتصال. لا تدعي التحقق من النسخة أو سجل SQL.
if ($WhatIfPreference) {
    $previewScripts = @(Get-ChildItem -LiteralPath $resolvedMigrations -File -Filter '*.sql' |
        Where-Object { $_.Name -match '^\d{4}_.+\.sql$' } | Sort-Object Name)
    if ($previewScripts.Count -eq 0) { throw 'لا توجد ملفات ترحيل مرقمة قابلة للتطبيق.' }
    foreach ($previewScript in $previewScripts) {
        [void]$PSCmdlet.ShouldProcess($DatabaseName, "تطبيق $($previewScript.Name)")
    }
    Write-Output 'Offline preview only. SQL connections=0; backup and migration registry not verified.'
    return
}

$master = New-Connection 'master'
$database = New-Connection $DatabaseName
try {
    $master.Open()
    $verify = $master.CreateCommand()
    try {
        $verify.CommandTimeout = 120
        $verify.CommandText = 'RESTORE VERIFYONLY FROM DISK = @BackupPath;'
        [void]$verify.Parameters.Add('@BackupPath', [System.Data.SqlDbType]::NVarChar, 260)
        $verify.Parameters['@BackupPath'].Value = $resolvedBackup
        [void]$verify.ExecuteNonQuery()
    }
    finally { $verify.Dispose() }

    $database.Open()
    $actualDatabase = [string](Invoke-Scalar $database 'SELECT DB_NAME();')
    if (-not [string]::Equals($actualDatabase, $DatabaseName, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "اتصال SQL لا يشير إلى قاعدة الاختبار المطلوبة. المتوقع: $DatabaseName؛ الفعلي: $actualDatabase"
    }

    $scripts = Get-ChildItem -LiteralPath $resolvedMigrations -File -Filter '*.sql' |
        Where-Object { $_.Name -match '^\d{4}_.+\.sql$' } |
        Sort-Object Name
    if ($scripts.Count -eq 0) { throw 'لا توجد ملفات ترحيل مرقمة قابلة للتطبيق.' }

    # 0004 يؤسس السجل نفسه. في أول تشغيل نطبق 0003 ثم 0004 بالترتيب،
    # ثم نسجل بصمتيهما بعد إنشاء السجل. لا نعيد ترحيلاً معتمداً لاحقاً.
    $registryExists = [int](Invoke-Scalar $database "SELECT CASE WHEN OBJECT_ID(N'dbo.CommercialMigrationRegistryTb', N'U') IS NULL THEN 0 ELSE 1 END;")
    if ($registryExists -eq 0) {
        $legacyScript = $scripts | Where-Object { $_.Name -eq '0003_saudi_feature_settings.sql' } | Select-Object -First 1
        $registryScript = $scripts | Where-Object { $_.Name -eq '0004_commercial_migration_registry.sql' } | Select-Object -First 1
        if ($null -eq $legacyScript -or $null -eq $registryScript) {
            throw 'ملفا التأسيس 0003 و0004 مطلوبان عند إنشاء سجل الترحيلات لأول مرة.'
        }
        if (-not $PSCmdlet.ShouldProcess($DatabaseName, 'تطبيق ترحيلي التأسيس 0003 ثم 0004')) { return }

        foreach ($bootstrapScript in @($legacyScript, $registryScript)) {
            $bootstrap = $database.CreateCommand()
            try {
                $bootstrap.CommandTimeout = 120
                $bootstrap.CommandText = Get-Content -LiteralPath $bootstrapScript.FullName -Raw -Encoding UTF8
                [void]$bootstrap.ExecuteNonQuery()
            }
            finally { $bootstrap.Dispose() }
        }

        foreach ($bootstrapScript in @($legacyScript, $registryScript)) {
            $id = [System.IO.Path]::GetFileNameWithoutExtension($bootstrapScript.Name)
            $hash = (Get-FileHash -LiteralPath $bootstrapScript.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
            $record = $database.CreateCommand()
            try {
                $record.CommandText = @'
INSERT INTO dbo.CommercialMigrationRegistryTb (MigrationId, ScriptSha256, ToolVersion, AppliedBy)
VALUES (@MigrationId, @ScriptSha256, @ToolVersion, @AppliedBy);
'@
                [void]$record.Parameters.Add('@MigrationId', [System.Data.SqlDbType]::NVarChar, 200)
                [void]$record.Parameters.Add('@ScriptSha256', [System.Data.SqlDbType]::Char, 64)
                [void]$record.Parameters.Add('@ToolVersion', [System.Data.SqlDbType]::NVarChar, 40)
                [void]$record.Parameters.Add('@AppliedBy', [System.Data.SqlDbType]::NVarChar, 256)
                $record.Parameters['@MigrationId'].Value = $id
                $record.Parameters['@ScriptSha256'].Value = $hash
                $record.Parameters['@ToolVersion'].Value = $toolVersion
                $record.Parameters['@AppliedBy'].Value = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
                [void]$record.ExecuteNonQuery()
            }
            finally { $record.Dispose() }
        }
    }

    foreach ($script in $scripts) {
        $id = [System.IO.Path]::GetFileNameWithoutExtension($script.Name)
        $hash = (Get-FileHash -LiteralPath $script.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        $escapedId = $id.Replace("'", "''")
        $recordedHash = Invoke-Scalar $database "SELECT ScriptSha256 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = N'$escapedId';"
        if ($null -ne $recordedHash -and $recordedHash -ne [System.DBNull]::Value) {
            if (-not [string]::Equals([string]$recordedHash, $hash, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "تم تعديل ترحيل مسجل: $($script.Name). لا تعدل ترحيلاً معتمداً؛ أضف ملفاً جديداً."
            }
            Write-Host "مطبق سابقاً وبصمته مطابقة: $($script.Name)" -ForegroundColor DarkGray
            continue
        }

        if (-not $PSCmdlet.ShouldProcess($DatabaseName, "تطبيق $($script.Name)")) { continue }
        $sql = Get-Content -LiteralPath $script.FullName -Raw -Encoding UTF8
        $command = $database.CreateCommand()
        try {
            $command.CommandTimeout = 120
            $command.CommandText = $sql
            [void]$command.ExecuteNonQuery()
        }
        finally { $command.Dispose() }

        $record = $database.CreateCommand()
        try {
            $record.CommandText = @'
INSERT INTO dbo.CommercialMigrationRegistryTb (MigrationId, ScriptSha256, ToolVersion, AppliedBy)
VALUES (@MigrationId, @ScriptSha256, @ToolVersion, @AppliedBy);
'@
            [void]$record.Parameters.Add('@MigrationId', [System.Data.SqlDbType]::NVarChar, 200)
            [void]$record.Parameters.Add('@ScriptSha256', [System.Data.SqlDbType]::Char, 64)
            [void]$record.Parameters.Add('@ToolVersion', [System.Data.SqlDbType]::NVarChar, 40)
            [void]$record.Parameters.Add('@AppliedBy', [System.Data.SqlDbType]::NVarChar, 256)
            $record.Parameters['@MigrationId'].Value = $id
            $record.Parameters['@ScriptSha256'].Value = $hash
            $record.Parameters['@ToolVersion'].Value = $toolVersion
            $record.Parameters['@AppliedBy'].Value = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
            [void]$record.ExecuteNonQuery()
        }
        finally { $record.Dispose() }
        Write-Host "تم تطبيق وتسجيل: $($script.Name)" -ForegroundColor Green
    }
}
finally {
    if ($null -ne $database) { $database.Dispose() }
    if ($null -ne $master) { $master.Dispose() }
}
