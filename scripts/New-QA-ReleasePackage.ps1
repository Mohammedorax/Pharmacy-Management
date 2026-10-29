[CmdletBinding()]
param(
    [string]$QaDatabase = 'PharmacyManagement_qa',
    [string]$ServerName = '.',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$ReleaseDirectory,
    [string]$StagingRoot = [System.IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'

function Require-TestDatabaseName {
    param([string]$DatabaseName)

    if ([string]::IsNullOrWhiteSpace($DatabaseName) -or $DatabaseName -notmatch '(_qa|_test|_sandbox)$') {
        throw 'اسم قاعدة حزمة QA يجب أن ينتهي بـ _qa أو _test أو _sandbox.'
    }
}

function Get-ConnectionNode {
    param([xml]$Config, [string]$Name)

    $node = @($Config.configuration.connectionStrings.add | Where-Object { $_.name -eq $Name }) | Select-Object -First 1
    if ($null -eq $node) {
        throw "لم يعثر منشئ حزمة QA على اتصال $Name في ملف الإعداد."
    }

    return $node
}

Require-TestDatabaseName $QaDatabase
if ([string]::IsNullOrWhiteSpace($ServerName)) {
    throw 'اسم خادم SQL مطلوب.'
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $releaseDirectory = Join-Path $repositoryRoot 'project_fo_3\bin\Release'
} else {
    $releaseDirectory = [System.IO.Path]::GetFullPath($ReleaseDirectory)
    if ([System.IO.Path]::GetPathRoot($releaseDirectory) -notin @('C:\', 'D:\')) {
        throw 'An explicit ReleaseDirectory must be on C: or D: for QA package staging.'
    }
}
$releaseExecutable = Join-Path $releaseDirectory 'project_fo_3.exe'
$releaseConfig = Join-Path $releaseDirectory 'project_fo_3.exe.config'
if (-not (Test-Path -LiteralPath $releaseExecutable) -or -not (Test-Path -LiteralPath $releaseConfig)) {
    throw 'ملفات Release غير مكتملة. ابنِ المشروع بتكوين Release قبل إنشاء حزمة QA.'
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$packageId = 'PharmacyManagement-QA-Release-{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$zipPath = Join-Path $resolvedOutput ($packageId + '.zip')
$stageRoot = [System.IO.Path]::GetFullPath($StagingRoot)
$stageDirectory = [System.IO.Path]::GetFullPath((Join-Path $stageRoot $packageId))
if (-not $stageDirectory.StartsWith($stageRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'QA staging directory escapes the temporary root.'
}

try {
    [System.IO.Directory]::CreateDirectory($stageDirectory) | Out-Null
    # Ship runtime files, never mutable recovery stores or local acceptance tools.
    Copy-Item -LiteralPath $releaseExecutable -Destination $stageDirectory
    Copy-Item -LiteralPath $releaseConfig -Destination $stageDirectory
    Get-ChildItem -LiteralPath $releaseDirectory -File -Filter '*.dll' | Copy-Item -Destination $stageDirectory
    foreach ($resourceName in @('Fonts', 'Licenses')) {
        $resourceSource = Join-Path $releaseDirectory ('Resources\' + $resourceName)
        if (-not (Test-Path -LiteralPath $resourceSource -PathType Container)) {
            throw "Required runtime resources missing: $resourceName"
        }
        $resourceTarget = Join-Path $stageDirectory 'Resources'
        [System.IO.Directory]::CreateDirectory($resourceTarget) | Out-Null
        Copy-Item -LiteralPath $resourceSource -Destination $resourceTarget -Recurse
    }

    # الحزمة القابلة للمراجعة تضم التطبيق ومصدر الترحيلات ودليل تشغيلها؛ لا
    # يكفي ملف exe وحده عندما يكون اعتماد ميزة تجارية مرتبطاً بمخطط SQL.
    $deploymentDirectory = Join-Path $stageDirectory 'Deployment'
    [System.IO.Directory]::CreateDirectory($deploymentDirectory) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'database\migrations') -Destination (Join-Path $deploymentDirectory 'migrations') -Recurse -Force
    [System.IO.Directory]::CreateDirectory((Join-Path $deploymentDirectory 'scripts')) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Apply-CommercialMigrations.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\Apply-CommercialMigrations.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-CommercialMigrationPreview.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\Test-CommercialMigrationPreview.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-CommercialReturnConcurrencySandbox.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\Test-CommercialReturnConcurrencySandbox.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-QaBackupRestore.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\Test-QaBackupRestore.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\QA_COMMERCIAL_RETURN_RUNBOOK_AR.md') -Destination (Join-Path $stageDirectory 'README-QA-COMMERCIAL-RETURN-AR.md') -Force

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\QA_DELIVERY_BACKUP_PERMISSIONS_AR.md') -Destination (Join-Path $stageDirectory 'README-QA-DELIVERY-AR.md') -Force

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-PackageManifest.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\Test-PackageManifest.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\New-CommercialReturnPrintSandbox.ps1') -Destination (Join-Path $deploymentDirectory 'scripts\New-CommercialReturnPrintSandbox.ps1') -Force
    $stagedConfigPath = Join-Path $stageDirectory 'project_fo_3.exe.config'
    [xml]$config = Get-Content -LiteralPath $stagedConfigPath -Raw
    foreach ($connectionName in @('PharmacyConnection', 'project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
        $node = Get-ConnectionNode $config $connectionName
        $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$node.connectionString)
        # استخدم مفاتيح SqlConnectionStringBuilder القياسية؛ PowerShell يعامل بعض
        # خصائص هذا النوع كفهرس اتصال بدلاً من setter CLR المباشر.
        $builder['Data Source'] = $ServerName.Trim()
        $builder['Initial Catalog'] = $QaDatabase.Trim()
        $builder['Integrated Security'] = $true
        $builder['Encrypt'] = $true
        $builder['TrustServerCertificate'] = $true
        $node.connectionString = $builder.ConnectionString
    }

    $forceNode = @($config.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' }) | Select-Object -First 1
    if ($null -eq $forceNode) {
        $forceNode = $config.CreateElement('add')
        [void]$forceNode.SetAttribute('key', 'ForceBuildConnectionProfile')
        [void]$config.configuration.appSettings.AppendChild($forceNode)
    }
    [void]$forceNode.SetAttribute('value', 'true')
    $config.Save($stagedConfigPath)

    # تحقق مستقل من الملف المجهز: لا يكفي أن يكون اسم الحزمة أو تعليق المصدر صحيحاً.
    [xml]$verifiedConfig = Get-Content -LiteralPath $stagedConfigPath -Raw
    $verifiedForceNode = @($verifiedConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' }) | Select-Object -First 1
    if ($null -eq $verifiedForceNode -or $verifiedForceNode.value -ne 'true') {
        throw 'لم يتم تثبيت حاجز ملف الإعداد ForceBuildConnectionProfile في حزمة QA.'
    }
    foreach ($connectionName in @('PharmacyConnection', 'project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
        $verifiedNode = Get-ConnectionNode $verifiedConfig $connectionName
        $verifiedBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$verifiedNode.connectionString)
        if ($verifiedBuilder['Data Source'] -ne $ServerName.Trim() -or $verifiedBuilder['Initial Catalog'] -ne $QaDatabase.Trim() -or -not [bool]$verifiedBuilder['Integrated Security']) {
            throw "اتصال $connectionName لا يطابق خادم وقاعدة QA المطلوبين."
        }
    }

    $requiredPackageFiles = @(
        'Deployment\migrations\0008_commercial_partial_returns.sql',
        'Deployment\migrations\0009_accounting_period_lock.sql',
        'Deployment\migrations\0010_commercial_return_integrity_hardening.sql',
        'Deployment\migrations\0011_customer_account_ledger.sql',
        'Deployment\migrations\0012_quarantine_resolution.sql',
        'Deployment\migrations\0013_active_ingredient_evidence.sql',
        'Deployment\migrations\0014_sales_invoice_payments_and_credit_use.sql',
        'Deployment\migrations\0015_sales_payment_trigger_scope.sql',
        'Deployment\migrations\0020_cashbox_definitions.sql',
        'Deployment\migrations\0022_cashbox_cancellation_links.sql',
        'Deployment\migrations\0021_cashbox_journal_links.sql',
        'project_fo_3.exe',
        'project_fo_3.exe.config',
        'Deployment\migrations\0023_voucher_request_identity.sql',
        'Deployment\migrations\0024_customer_master_request_identity.sql',
        'Deployment\migrations\0025_purchase_write_request_identity.sql',
        'Deployment\migrations\0026_voucher_request_identity_verified.sql',
        'Deployment\migrations\0027_journal_system_currency.sql',
        'Deployment\scripts\Apply-CommercialMigrations.ps1',
        'Deployment\scripts\Test-CommercialMigrationPreview.ps1',
        'Deployment\scripts\Test-CommercialReturnConcurrencySandbox.ps1',
        'Deployment\scripts\Test-QaBackupRestore.ps1',
        'README-QA-COMMERCIAL-RETURN-AR.md',
        'README-QA-DELIVERY-AR.md',
        'Deployment\scripts\Test-PackageManifest.ps1',
        'Deployment\scripts\New-CommercialReturnPrintSandbox.ps1'
    )
    foreach ($relativePath in $requiredPackageFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory $relativePath) -PathType Leaf)) {
            throw "ملف مراجعة QA المطلوب مفقود من الحزمة: $relativePath"
        }
    }
    $manifest = @(Get-ChildItem -LiteralPath $stageDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relativePath = $_.FullName.Substring($stageDirectory.Length + 1)
        [PSCustomObject]@{ Path = $relativePath; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stageDirectory 'MANIFEST.json') -Encoding UTF8

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stageDirectory, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Output "QA package created: $zipPath"
    Write-Output "QA database pinned: $($ServerName.Trim()) / $($QaDatabase.Trim())"
    Write-Output 'QA deployment material and MANIFEST.json included.'
    Write-Output 'بعد فك الحزمة شغّل: project_fo_3.exe --verify-qa-package-profile'
}
finally {
    if (Test-Path -LiteralPath $stageDirectory) {
        Remove-Item -LiteralPath $stageDirectory -Recurse -Force
    }
}

