[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$ExecutablePath,
    [string]$ArtifactsDirectory,
    [ValidateSet("login", "login-dark", "login-english", "login-english-dark", "settings", "settings-english", "currency-settings", "currency-settings-dark", "currency-settings-english", "currency-settings-english-dark", "language-settings", "language-settings-dark", "language-settings-english", "language-settings-english-dark", "device-settings", "device-settings-dark", "device-settings-english", "device-settings-english-dark", "database-settings-readonly", "database-settings-readonly-dark", "database-settings-readonly-english", "database-settings-readonly-english-dark", "database-settings", "database-settings-dark", "database-settings-english", "database-settings-english-dark", "accounting-period", "accounting-period-dark", "accounting-period-english", "accounting-period-english-dark", "period-readiness", "period-readiness-dark", "period-readiness-english", "period-readiness-english-dark", "accounting-integrity", "accounting-integrity-dark", "accounting-integrity-english", "accounting-integrity-english-dark", "inventory-integrity", "inventory-integrity-dark", "inventory-integrity-english", "inventory-integrity-english-dark", "tax-settings-readonly", "tax-settings-readonly-dark", "tax-settings-readonly-english", "tax-settings-readonly-english-dark", "tax-settings", "tax-settings-dark", "tax-settings-english", "tax-settings-english-dark", "saudi-settings", "saudi-settings-dark", "saudi-settings-english", "saudi-settings-english-dark", "pharmacy-profile", "pharmacy-profile-dark", "pharmacy-profile-english", "pharmacy-profile-english-dark", "bootstrap-manager", "bootstrap-manager-dark", "bootstrap-manager-english", "bootstrap-manager-english-dark", "commercial-return", "commercial-return-dark", "commercial-return-english", "commercial-return-english-dark", "return-history", "return-history-dark", "return-history-english", "return-history-english-dark", "quarantine-resolution", "quarantine-resolution-dark", "quarantine-resolution-english", "quarantine-resolution-english-dark", "permissions", "manufacturer", "manufacturer-dark", "manufacturer-english", "manufacturer-english-dark", "quick-journal", "journal-entry", "journal-entry-dark", "voucher-actions", "voucher-actions-dark", "agent-password", "agent", "agent-dark", "agent-english", "agent-english-dark", "quick-search", "quick-search-dark", "quick-search-english", "quick-search-english-dark", "medicine-editor", "medicine-editor-dark", "medicine-editor-english", "medicine-editor-english-dark", "purchases-inputs", "purchases-inputs-dark", "purchases-inputs-english", "purchases-inputs-english-dark", "billing-customer-type", "billing-customer-type-dark", "billing-customer-type-english", "billing-customer-type-english-dark", "customer-management", "customer-management-dark", "customer-management-english", "customer-management-english-dark", "inventory-operation", "inventory-operation-english", "inventory-movement", "inventory-movement-english", "inventory-summary", "inventory-summary-english", "inventory-stock-dark", "inventory-stock-english-dark", "inventory-stock", "inventory-stock-english", "inventory-lots-dark", "inventory-lots-english-dark", "inventory-lots", "inventory-lots-english", "inventory-stock-liveqa", "inventory-lots-liveqa", "inventory-summary-liveqa", "inventory-turnover-liveqa", "inventory-alerts-liveqa", "inventory-movement-liveqa", "inventory-integrity-liveqa", "inventory-turnover", "inventory-turnover-english", "inventory-alerts", "inventory-alerts-english", "antdui-arabic", "home", "home-english", "home-workspace", "home-workspace-english", "dashboard", "dashboard-english", "operational-report", "operational-report-dark", "operational-report-english", "operational-report-english-dark", "system-monitor", "system-monitor-english", "report-print-options", "report-print-options-dark", "report-print-options-english", "report-print-options-english-dark", "report-columns", "report-columns-dark", "report-columns-english", "report-columns-english-dark", "report-template-name", "report-template-name-dark", "report-template-name-english", "report-template-name-english-dark", "invoice-drafts", "invoice-drafts-dark", "invoice-drafts-english", "invoice-drafts-english-dark", "invoice-drafts-direct", "invoice-drafts-direct-dark", "invoice-drafts-direct-english", "invoice-drafts-direct-english-dark", "invoice-drafts-source", "invoice-drafts-source-dark", "invoice-drafts-source-english", "invoice-drafts-source-english-dark", "cashboxes", "cashboxes-dark", "cashboxes-english", "cashboxes-english-dark", "excel-import", "excel-export", "excel-export-dark", "excel-export-english", "excel-export-english-dark", "active-ingredients", "medicine-catalog-import", "medicine-catalog-import-dark", "medicine-catalog-import-english", "medicine-catalog-import-english-dark", "medicine-catalog-errors", "medicine-catalog-errors-dark", "medicine-catalog-errors-english", "medicine-catalog-errors-english-dark")]
    [string]$Scenario = "login"
)

$ErrorActionPreference = "Stop"
$desktopQaUiLock = & (Join-Path $PSScriptRoot 'Enter-DesktopQaLock.ps1')
try {
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $repositoryRoot "project_fo_3\bin\$Configuration\project_fo_3.exe"
}
if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $repositoryRoot "artifacts\ui-smoke"
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "لم يتم العثور على التطبيق: $ExecutablePath. ابنِ الحل أولاً."
}

$runner = Join-Path $repositoryRoot "tests\PharmacyManagement.UiAutomation\PharmacyManagement.UiAutomation.csproj"
$runnerAssembly = Join-Path $repositoryRoot "tests\PharmacyManagement.UiAutomation\bin\Release\net10.0-windows\PharmacyManagement.UiAutomation.dll"
if (-not (Test-Path -LiteralPath $runnerAssembly)) {
    dotnet build $runner --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "تعذر بناء مشغّل FlaUI (رمز $LASTEXITCODE)."
    }
}

$OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
dotnet $runnerAssembly --exe $ExecutablePath --artifacts $ArtifactsDirectory --scenario $Scenario
if ($LASTEXITCODE -ne 0) {
    throw "فشل اختبار FlaUI برمز $LASTEXITCODE."
}





} finally {
    $desktopQaUiLock.ReleaseMutex()
    $desktopQaUiLock.Dispose()
}