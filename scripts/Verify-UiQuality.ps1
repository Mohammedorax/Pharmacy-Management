[CmdletBinding()]
param(
    [switch]$SkipInteractiveSmoke,
    [switch]$IsolatedDebugBuild,
    [switch]$IncludeQaReadOnly,
    [switch]$IncludeQaTransactional,
    [switch]$IncludeQaOwnedSalesIdempotency,
    [switch]$IncludeQaJournalRollback,
    [switch]$IncludeQaVoucherRollback,
    [switch]$IncludeQaAccountRollback,
    [string]$QaVerifiedBackupPath,
    [string]$QaServer = ".",
    [string]$QaDatabase = "PharmacyManagement_qa"
)

$ErrorActionPreference = "Stop"

$desktopQaUiLock = & (Join-Path $PSScriptRoot 'Enter-DesktopQaLock.ps1')
try {
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot "project_fo_3.sln"
$application = Join-Path $repositoryRoot "project_fo_3\bin\Release\project_fo_3.exe"
$uiRunner = Join-Path $repositoryRoot "tests\PharmacyManagement.UiAutomation\PharmacyManagement.UiAutomation.csproj"
$coreTests = Join-Path $repositoryRoot "tests\PharmacyManagement.CoreTests\PharmacyManagement.CoreTests.csproj"
$coreRunner = Join-Path $repositoryRoot "tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe"
$vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"

$applicationCheckEvidenceDirectory = Join-Path $repositoryRoot ('artifacts\quality-checks-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $applicationCheckEvidenceDirectory -Force | Out-Null
$script:applicationCheckSequence = 0
Write-Output ('Application check evidence: ' + $applicationCheckEvidenceDirectory)

function Invoke-ApplicationCheck {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $script:applicationCheckSequence++
    $checkName = ('{0:D3}-' -f $script:applicationCheckSequence) + ($Arguments[0] -replace '[^a-zA-Z0-9_-]', '_')
    $outputPath = Join-Path $applicationCheckEvidenceDirectory ($checkName + '.stdout.log')
    $errorPath = Join-Path $applicationCheckEvidenceDirectory ($checkName + '.stderr.log')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Write-Output ('BEGIN ' + ($Arguments -join ' ') + '; stdout=' + $outputPath)
    $process = Start-Process -FilePath $application -ArgumentList $Arguments -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath
    $timer.Stop()
    [pscustomobject]@{Arguments=$Arguments;ExitCode=$process.ExitCode;ElapsedMilliseconds=$timer.ElapsedMilliseconds;StandardOutput=$outputPath;StandardError=$errorPath} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $applicationCheckEvidenceDirectory ($checkName + '.result.json')) -Encoding UTF8
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath $outputPath -Tail 30
        Get-Content -LiteralPath $errorPath -Tail 30
        throw "فشل $($Arguments -join ' ') برمز $($process.ExitCode). سجلات الفحص: $applicationCheckEvidenceDirectory"
    }
    Write-Output ('PASS ' + ($Arguments -join ' ') + '; elapsed_ms=' + $timer.ElapsedMilliseconds)
}
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "لم توجد أداة vswhere لتحديد MSBuild."
}
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "لم يوجد MSBuild متوافق مع مشروع .NET Framework."
}

& (Join-Path $PSScriptRoot 'Test-LayerArchitecture.ps1')
if (-not $?) { throw 'The layer architecture guard failed.' }
& (Join-Path $PSScriptRoot 'Test-UiTranslationCoverage.ps1')
if (-not $?) { throw 'The translation coverage guard failed.' }
if ($IsolatedDebugBuild) {
    # Keep a running Debug application untouched while still compiling every Debug project.
    $debugOutput = Join-Path $repositoryRoot 'artifacts\build-verify-debug'
    New-Item -ItemType Directory -Path $debugOutput -Force | Out-Null
    $debugOutput = [System.IO.Path]::GetFullPath($debugOutput) + [System.IO.Path]::DirectorySeparatorChar
    & $msbuild $solution /t:Build /p:Configuration=Debug "/p:OutDir=$debugOutput" /m:1 /v:minimal
} else {
    & $msbuild $solution /t:Build /p:Configuration=Debug /m:1 /v:minimal
}
if ($LASTEXITCODE -ne 0) { throw 'Debug build failed.' }
& $msbuild $solution /t:Build /p:Configuration=Release /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "فشل بناء Release."
}

& $msbuild $coreTests /t:Build /p:Configuration=Release /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Core test build failed.' }
& $coreRunner
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }

& (Join-Path $PSScriptRoot 'Test-ReceiptThemeDirection.ps1') -ApplicationPath $application -EvidenceDirectory (Join-Path $applicationCheckEvidenceDirectory 'receipt-theme-direction')
if (-not $?) { throw 'Receipt theme direction regression failed.' }

# Pending-file recovery is isolated from SQL and the interactive desktop.
$persistenceTests=Join-Path $repositoryRoot 'tests\PharmacyManagement.DesktopPersistenceTests\PharmacyManagement.DesktopPersistenceTests.csproj'
foreach($configuration in @('Debug','Release')) {
    & $msbuild $persistenceTests /t:Build "/p:Configuration=$configuration" /m:1 /v:minimal
    if($LASTEXITCODE -ne 0){throw "Desktop persistence $configuration build failed."}
}
$persistenceRunner=Join-Path $repositoryRoot 'tests\PharmacyManagement.DesktopPersistenceTests\bin\Release\PharmacyManagement.DesktopPersistenceTests.exe'
$persistenceData=Join-Path ([IO.Path]::GetTempPath()) ('PharmacyInventoryPersistence-'+[guid]::NewGuid().ToString('N'))
& $persistenceRunner $persistenceData
if($LASTEXITCODE -ne 0){throw 'Pending inventory persistence checks failed.'}


if ($IncludeQaAccountRollback) {
    if ([string]::IsNullOrWhiteSpace($QaVerifiedBackupPath)) { throw 'Account rollback requires a recent verified QA backup.' }
    & $coreRunner --qa-account-write-rollback $QaServer $QaDatabase $QaVerifiedBackupPath --confirm-qa-account-writes
    if ($LASTEXITCODE -ne 0) { throw 'Account writing SQL rollback checks failed.' }
}
if ($IncludeQaVoucherRollback) {
    if ([string]::IsNullOrWhiteSpace($QaVerifiedBackupPath)) { throw 'Voucher rollback requires a recent verified QA backup.' }
    & $coreRunner --qa-voucher-post-rollback $QaServer $QaDatabase $QaVerifiedBackupPath --confirm-qa-voucher-writes
    if ($LASTEXITCODE -ne 0) { throw 'Voucher posting SQL rollback checks failed.' }
}
if ($IncludeQaJournalRollback) {
    if ([string]::IsNullOrWhiteSpace($QaVerifiedBackupPath)) { throw 'Journal rollback requires a recent verified QA backup.' }
    & $coreRunner --qa-journal-post-rollback $QaServer $QaDatabase $QaVerifiedBackupPath --confirm-qa-journal-writes
    if ($LASTEXITCODE -ne 0) { throw 'Journal posting SQL rollback checks failed.' }
}
if (-not $SkipInteractiveSmoke) {
    # The FlaUI runner is only needed by the interactive scenarios, so a machine without the
    # .NET SDK can still run the full non-interactive gate instead of aborting before the checks.
    dotnet build $uiRunner --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "فشل بناء مشغل FlaUI."
    }
}

# Invalid automation requests are expected failures, never an interactive startup.
$rejectedEvidence = Join-Path $applicationCheckEvidenceDirectory 'rejected-automation'
New-Item -ItemType Directory -Path $rejectedEvidence -Force | Out-Null
$rejectedCases = @(
    @{ Name='unknown'; Arguments=@('--verify-no-such-command') },
    @{ Name='mistyped-preview'; Arguments=@('--verify-accounting-period-previews', (Join-Path $rejectedEvidence 'unused')) },
    @{ Name='missing-preview-path'; Arguments=@('--verify-period-previews') },
    @{ Name='extra-policy-argument'; Arguments=@('--verify-permission-policy', 'unexpected') },
    @{ Name='missing-qa-confirmation'; Arguments=@('--verify-qa-sales-roundtrip', 'preview.invalid', 'OwnedFixture_qa') },
    @{ Name='missing-quick-restart-confirmation'; Arguments=@('--verify-qa-quick-journal-editor-restart', 'preview.invalid', '00000000000000000000000000000000', 'unused.protected') }
)
foreach ($rejectedCase in $rejectedCases) {
    $stdout = Join-Path $rejectedEvidence ($rejectedCase.Name + '.stdout.log')
    $stderr = Join-Path $rejectedEvidence ($rejectedCase.Name + '.stderr.log')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $application -ArgumentList $rejectedCase.Arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $exited = $process.WaitForExit(10000)
    if (-not $exited) { Stop-Process -Id $process.Id -ErrorAction Stop; $process.WaitForExit() }
    $timer.Stop()
    $errorText = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    $accepted = $exited -and $process.ExitCode -eq 1 -and $errorText.Contains('Unsupported automation command or invalid arguments.') -and $errorText.Contains('ExceptionType=System.ArgumentException')
    [pscustomobject]@{Name=$rejectedCase.Name;Arguments=$rejectedCase.Arguments;ExpectedExitCode=1;ActualExitCode=$process.ExitCode;ExitedWithinTimeout=$exited;Accepted=$accepted;ElapsedMilliseconds=$timer.ElapsedMilliseconds} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $rejectedEvidence ($rejectedCase.Name + '.result.json')) -Encoding UTF8
    if (-not $accepted) { throw ('Invalid automation command did not fail safely: ' + $rejectedCase.Name) }
    Write-Output ('PASS rejected automation: ' + $rejectedCase.Name)
}
$checks = @(
    "--verify-commercial-domain",
    "--verify-permission-policy",
    "--verify-manufacturer-async",
    "--verify-medicine-async",
    "--verify-billing-medicine-reads",
    "--verify-billing-barcode-async",
    "--verify-purchase-barcode-async",
    "--verify-invoice-alert-status",
    "--verify-billing-currency-snapshot",
    "--verify-customer-write-recovery",
    "--verify-billing-customer-reads",
    "--verify-quick-search-authorization",
    "--verify-customer-recovery",
    "--verify-appearance-persistence",
    "--verify-device-settings",
    "--verify-ui-language-catalog",
    "--verify-qa-readonly-guards",
    "--verify-report-preview-lifetime",
    "--verify-report-templates",
    "--verify-operational-reports",
    "--verify-operational-report-async",
    "--verify-commercial-return-reads",
    "--verify-commercial-return-contract",
    "--verify-sales-idempotency",
    "--verify-window-stability",
    "--verify-qa-csv-import",
    "--verify-excel-workbook"
)

foreach ($check in $checks) {
    Invoke-ApplicationCheck -Arguments @($check)
}

# The layout sweep runs as independent groups in parallel: the same 155 checks with a
# fraction of the wall-clock time and a smaller slice to inspect after a failure.
$layoutGroupCount = 4
$layoutProcesses = @()
$originalLocalAppData = $env:LOCALAPPDATA
for ($group = 0; $group -lt $layoutGroupCount; $group++) {
    $groupName = ('layout-group-{0}' -f $group)
    $layoutStdout = Join-Path $applicationCheckEvidenceDirectory ($groupName + '.stdout.log')
    $layoutStderr = Join-Path $applicationCheckEvidenceDirectory ($groupName + '.stderr.log')
    # Each group gets its own user settings file so the groups cannot fight over the saved theme.
    $groupLocalAppData = Join-Path $applicationCheckEvidenceDirectory ('group-profile-' + $group)
    New-Item -ItemType Directory -Path $groupLocalAppData -Force | Out-Null
    $env:LOCALAPPDATA = $groupLocalAppData
    try {
          $layoutProcesses += [pscustomobject]@{
              Index = $group
              Stdout = $layoutStdout
              Stderr = $layoutStderr
              Process = Start-Process -FilePath $application -ArgumentList @('--verify-responsive-layout-group', $group, $layoutGroupCount) `
                  -PassThru -WindowStyle Hidden -RedirectStandardOutput $layoutStdout -RedirectStandardError $layoutStderr
          }
    }
      finally { $env:LOCALAPPDATA = $originalLocalAppData }
  }
$layoutTimer = [Diagnostics.Stopwatch]::StartNew()
try {
    foreach ($entry in $layoutProcesses) {
        $entry.Process.WaitForExit()
        $layoutTimer.Stop()
        if ($entry.Process.ExitCode -ne 0) {
            # A failing group must not leave its sibling groups running on the desktop while the
            # desktop mutex is released; kill every straggler before reporting the failure.
            foreach ($other in $layoutProcesses) {
                try { if (-not $other.Process.HasExited) { $other.Process.Kill() } } catch { }
            }
            Get-Content -LiteralPath $entry.Stdout -Tail 20 -ErrorAction SilentlyContinue
            Get-Content -LiteralPath $entry.Stderr -Tail 20 -ErrorAction SilentlyContinue
            throw ("فشل فحص التخطيط في المجموعة " + $entry.Index + " من " + $layoutGroupCount + " برمز " + $entry.Process.ExitCode)
        }
        $layoutTimer.Start()
    }
}
finally {
    foreach ($entry in $layoutProcesses) {
        try { if (-not $entry.Process.HasExited) { $entry.Process.Kill(); [void]$entry.Process.WaitForExit(5000) } } catch { }
        try { $entry.Process.Dispose() } catch { }
    }
}
# The groups must together cover the same checks as the single command they replace.
$layoutTotal = 0
$layoutCovered = 0
foreach ($entry in $layoutProcesses) {
    $summary = Select-String -LiteralPath $entry.Stdout -Pattern 'checks=(\d+); total=(\d+)' | Select-Object -Last 1
    if (-not $summary) {
        Get-Content -LiteralPath $entry.Stdout -Tail 20 -ErrorAction SilentlyContinue
        throw ("Layout group " + $entry.Index + " did not report its check coverage.")
    }
    $layoutCovered += [int]$summary.Matches[0].Groups[1].Value
    $total = [int]$summary.Matches[0].Groups[2].Value
    if ($layoutTotal -eq 0) { $layoutTotal = $total }
    elseif ($total -ne $layoutTotal) { throw ("Layout groups disagree on the check total: " + $layoutTotal + " vs " + $total) }
}
if ($layoutCovered -ne $layoutTotal) { throw ("Layout groups covered " + $layoutCovered + " checks out of " + $layoutTotal + ".") }
if ($layoutTotal -lt 155) { throw ("Layout groups covered only " + $layoutTotal + " checks; the sweep needs at least 155.") }
$layoutNames = @()
foreach ($entry in $layoutProcesses) {
    $matches = Select-String -LiteralPath $entry.Stdout -Pattern 'PASS layout: ([^;]+);' -AllMatches -ErrorAction SilentlyContinue
    foreach ($match in $matches) { foreach ($m in $match.Matches) { $layoutNames += $m.Groups[1].Value.Trim() } }
}
$distinctLayout = @($layoutNames | Sort-Object -Unique)
if ($distinctLayout.Count -gt 0 -and $distinctLayout.Count -lt 155) { throw ("Layout groups covered only " + $distinctLayout.Count + " distinct checks; the sweep needs at least 155.") }
Write-Output ('PASS --verify-responsive-layout groups=' + $layoutGroupCount + '; checks=' + $layoutCovered + '; total=' + $layoutTotal + '; elapsed_ms=' + $layoutTimer.ElapsedMilliseconds)

# The live accessibility sweep opens visible windows and moves focus, so it runs after the layout
# groups; running it earlier made the module keyboard-focus check flaky.
Invoke-ApplicationCheck -Arguments @('--verify-live-accessibility')

Invoke-ApplicationCheck -Arguments @('--verify-billing-barcode-previews', (Join-Path $repositoryRoot 'artifacts\billing-barcode-previews-20261003'))
Invoke-ApplicationCheck -Arguments @('--verify-antdui-components', (Join-Path $repositoryRoot 'artifacts\antdui-migration'))
Invoke-ApplicationCheck -Arguments @('--verify-print-layout-simulation', (Join-Path $repositoryRoot 'artifacts\print-layout-simulation'))

if ($IncludeQaTransactional) {
    $IncludeQaReadOnly = $true
}

if ($IncludeQaReadOnly) {
    & $coreRunner --qa-inventory-operation-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Inventory operation async read SQL parity failed.' }

    & $coreRunner --qa-journal-preparation-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Journal preparation QA compatibility failed.' }
    & $coreRunner --qa-accounting-lookups-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed accounting lookup QA read failed.' }
    & $coreRunner --qa-journal-documents-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Saved journal document QA read failed.' }
    & $coreRunner --qa-journal-history-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed journal history QA read failed.' }
    & $coreRunner --qa-voucher-history-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed voucher history QA read failed.' }
    & $coreRunner --qa-voucher-documents-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed saved voucher QA read failed.' }
    & $coreRunner --qa-currencies-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed currency QA read failed.' }
    & $coreRunner --qa-search $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed search QA comparison failed.' }
    & $coreRunner --qa-manufacturers-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed manufacturer QA read failed.' }
    & $coreRunner --qa-medicines-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed medicine QA read failed.' }
    & $coreRunner --qa-active-ingredients-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed active ingredient QA read failed.' }
    & $coreRunner --qa-employees-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed employee QA read failed.' }
    & $coreRunner --qa-sales-customers-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed sales customer QA read failed.' }
    & $coreRunner --qa-sales-history-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed sales history QA read failed.' }
    & $coreRunner --qa-purchase-workspace-readonly $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Typed purchase workspace QA read failed.' }
    $qaReadOnlyChecks = @(
        "--verify-qa-core-schema-readonly",
        "--verify-qa-integrity-readonly",
        "--verify-qa-period-close-readiness-readonly",
        "--verify-qa-operational-report-sources-readonly",
        "--verify-qa-stock-readonly",
        "--verify-qa-lot-stock-readonly",
        "--verify-qa-inventory-alerts-readonly",
          "--verify-qa-excel-export-readonly",
        "--verify-qa-manual-acceptance-readiness",
        "--verify-qa-product-identity-return-schema-readonly",
        "--verify-qa-commercial-return-schema-readonly"
    )
    foreach ($check in $qaReadOnlyChecks) {
        Invoke-ApplicationCheck -Arguments @($check, $QaServer, $QaDatabase)
    }
    & (Join-Path $PSScriptRoot 'Test-QaMonitorReadComparison.ps1') -Server $QaServer -DatabaseName $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'System monitor QA read comparison failed.' }
    & (Join-Path $PSScriptRoot 'Test-QaInventoryAlertReadComparison.ps1') -Server $QaServer -DatabaseName $QaDatabase
    if (-not $?) { throw 'Inventory alert QA read comparison failed.' }
}

if ($IncludeQaOwnedSalesIdempotency) {
    # Live SQL proof of the sales idempotency contract inside a disposable owned QA clone.
    & (Join-Path $PSScriptRoot 'Test-QaOwnedSalesIdempotency.ps1') -ServerInstance $QaServer `
        -ReleaseDirectory (Split-Path -Parent $application) -ConfirmOwnedQaSalesIdempotency
    if (-not $?) { throw 'The owned sales idempotency SQL check failed.' }
}

if ($IncludeQaTransactional) {
    & $coreRunner --qa-cashbox-journal-rollback $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Cashbox sale/cancellation attribution rollback failed; migration 0022 is required.' }
    & $coreRunner --qa-cashbox-cancellation-integrity $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Cashbox cancellation SQL integrity checks failed.' }
    & $coreRunner --qa-catalog-import-rollback $QaServer $QaDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Zero-stock medicine catalog import rollback failed.' }
    Invoke-ApplicationCheck -Arguments @("--verify-qa-purchase-invoice-rollback", $QaServer, $QaDatabase, "--confirm-qa-purchase-rollback")
    Invoke-ApplicationCheck -Arguments @("--verify-qa-customer-lookup-concurrency", $QaServer, $QaDatabase)
    Invoke-ApplicationCheck -Arguments @("--verify-qa-active-ingredient-search-rollback", $QaServer, $QaDatabase)
    Invoke-ApplicationCheck -Arguments @("--verify-qa-lot-stock-rollback", $QaServer, $QaDatabase, "--confirm-qa-lot-stock-writes")
    Invoke-ApplicationCheck -Arguments @(
        "--verify-qa-commercial-return-roundtrip",
        $QaServer,
        $QaDatabase,
        "--confirm-qa-commercial-return-writes"
    )
    Invoke-ApplicationCheck -Arguments @("--verify-qa-integrity-readonly", $QaServer, $QaDatabase)
}

if (-not $SkipInteractiveSmoke) {
    foreach ($scenario in @("login", "settings", "language-settings", "device-settings", "tax-settings", "antdui-arabic", "operational-report", "report-print-options", "excel-import", "medicine-catalog-import", "medicine-catalog-errors")) {
        $scenarioArtifacts = Join-Path $repositoryRoot ("artifacts\ui-smoke\" + $scenario)
        & (Join-Path $PSScriptRoot "Test-UiSmoke.ps1") -ExecutablePath $application -ArtifactsDirectory $scenarioArtifacts -Scenario $scenario
        if ($LASTEXITCODE -ne 0) {
            throw "فشل اختبار FlaUI للسيناريو: $scenario."
        }
    }
    foreach ($scenario in @("invoice-drafts", "invoice-drafts-dark", "invoice-drafts-english", "invoice-drafts-english-dark")) {
        $scenarioArtifacts = Join-Path $repositoryRoot "artifacts\ui-smoke\invoice-drafts"
        dotnet run --project $uiRunner --configuration Release --no-build -- --exe $application --artifacts $scenarioArtifacts --scenario $scenario
        if ($LASTEXITCODE -ne 0) { throw "فشل اختبار FlaUI للسيناريو: $scenario." }
    }
    foreach ($scenario in @("inventory-stock", "inventory-stock-english", "inventory-stock-dark", "inventory-stock-english-dark",
        "inventory-lots", "inventory-lots-english", "inventory-lots-dark", "inventory-lots-english-dark")) {
        $scenarioArtifacts = Join-Path $repositoryRoot "artifacts\ui-smoke\inventory-stock-and-lots"
        dotnet run --project $uiRunner --configuration Release --no-build -- --exe $application --artifacts $scenarioArtifacts --scenario $scenario
        if ($LASTEXITCODE -ne 0) { throw "فشل اختبار FlaUI للسيناريو: $scenario." }
    }
}

if ($SkipInteractiveSmoke) {
    Write-Output 'Selected quality checks passed. Interactive FlaUI smoke was SKIPPED; UI acceptance remains open.'
} else {
    Write-Output 'UI quality gate passed. Physical printer, barcode device, and untested DPI acceptance remain separate.'
}










} finally {
    $desktopQaUiLock.ReleaseMutex()
    $desktopQaUiLock.Dispose()
}
