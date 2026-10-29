[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rules = @{
    'PharmacyManagement.Domain' = @('System', 'System.Core')
    'PharmacyManagement.Application' = @('System', 'System.Core')
    'PharmacyManagement.Infrastructure.SqlServer' = @('System', 'System.Core', 'System.Data', 'System.Xml', 'System.Xml.Linq')
}
foreach ($name in $rules.Keys) {
    $path = Join-Path $root "src\$name\$name.csproj"
    [xml]$project = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $project.Project.ItemGroup.Reference) {
        if ($null -ne $reference -and $rules[$name] -notcontains [string]$reference.Include) {
            throw "Forbidden assembly in ${name}: $($reference.Include)"
        }
    }
    foreach ($reference in $project.Project.ItemGroup.ProjectReference) {
        if ($null -eq $reference) { continue }
        $allowed = ($name -eq 'PharmacyManagement.Application' -and $reference.Name -eq 'PharmacyManagement.Domain') -or ($name -eq 'PharmacyManagement.Infrastructure.SqlServer' -and $reference.Name -in @('PharmacyManagement.Application','PharmacyManagement.Domain'))
        if (-not $allowed) { throw "Forbidden project dependency: $name -> $($reference.Name)" }
    }
    foreach ($compile in $project.Project.ItemGroup.Compile) {
        if ($null -eq $compile) { continue }
        $source = Join-Path (Split-Path -Parent $path) ([string]$compile.Include)
        $text = Get-Content -LiteralPath $source -Raw
        $banned = 'System\.Windows\.Forms|System\.Drawing|AntdUI|MessageBox|DataGridView|Properties\.Setings'
        if ($name -ne 'PharmacyManagement.Infrastructure.SqlServer') { $banned += '|System\.Data|SqlConnection|SqlCommand' }
        if ($text -match $banned) { throw "Forbidden dependency in source: $source" }
    }
}
foreach ($form in @(
    'Login.cs',
    'manufacturer.cs',
    'Medicine.cs',
    'ActiveIngredientManagementForm.cs',
    'Agent.cs',
    'EmployeeOperationPermissionsForm.cs',
    'MedicineQuickSearchForm.cs',
    'SettingsForm.cs',
    'SettingsForm.Antd.cs',
    'AntdComponentsForm.cs',
    'Purchases.cs',
    'Dashboard.cs',
    'PharmacyCompanyForm.cs',
    'SystemMonitorForm.cs',
    'TaxSettingsForm.cs',
    'SaudiCommercialSettingsForm.cs',
    'QuarantineResolutionForm.cs',
    'CommercialSalesReturnHistoryForm.cs',
    'CommercialSalesReturnForm.cs',
    'OperationalReportBuilderForm.cs',
    'OperationalReportReadService.cs',
    'QaCsvImportForm.cs'
)) {
    $text = Get-Content -LiteralPath (Join-Path $root "project_fo_3\$form") -Raw
    if ($text -match 'SqlConnection|SqlCommand|SqlDataAdapter|Database\.CreateConnection') { throw "SQL returned to migrated screen: $form" }
}
$monitorReadText = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\SystemMonitorReadService.cs') -Raw
$monitorFormText = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\SystemMonitorForm.cs') -Raw
if ($monitorReadText -match 'Database\.Get|SqlConnection|SqlCommand|SqlDataAdapter|Task\.Run' -or
    $monitorReadText -notmatch 'IAsyncSystemMonitorLogReadService' -or
    $monitorReadText -notmatch 'GetRecentAuditLogsAsync' -or
    $monitorReadText -notmatch 'GetRecentInventoryMovementsAsync' -or
    $monitorReadText -notmatch 'CancellationToken' -or
    $monitorFormText -match 'SystemMonitorReadService\.Load|Task\.Run|SqlConnection|SqlCommand|Database\.Get' -or
    $monitorFormText -notmatch 'ISystemMonitorReadSession' -or
    $monitorFormText -notmatch 'session\.LoadAsync' -or
    $monitorFormText -notmatch 'UiSessionSubscription\.Attach') {
    throw 'System monitor must use an injected typed asynchronous session with cancellation and session invalidation; SQL and legacy synchronous reads belong outside the screen.'
}
$stockReportSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryForms.cs') -Raw
$stockReportStart = $stockReportSource.IndexOf('internal sealed partial class InventoryStockReportForm')
$stockReportEnd = $stockReportSource.IndexOf('internal sealed partial class InventoryMovementForm', $stockReportStart)
if ($stockReportStart -lt 0 -or $stockReportEnd -le $stockReportStart) { throw 'Inventory stock report screen boundary could not be inspected.' }
$stockReportBody = $stockReportSource.Substring($stockReportStart, $stockReportEnd - $stockReportStart)
$stockReadDesktop = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryStockReportReads.cs') -Raw
if ($stockReportBody -match 'InventoryService\.Get(StockSnapshot|LotStockSnapshot)' -or
    $stockReadDesktop -match 'SqlConnection|SqlCommand|SqlDataReader|Database\.CreateConnection' -or
    $stockReadDesktop -notmatch 'InventoryStockReadSession\.LoadAsync|session\.LoadAsync') {
    throw 'Inventory stock report must use the typed asynchronous service; SQL and legacy synchronous reads belong outside the screen.'
}
$inventoryText = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryService.cs') -Raw
$accountingIntegrityText=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingService.cs') -Raw
$accountingIntegrityStart=$accountingIntegrityText.IndexOf('public static DataTable GetJournalIntegrityIssues(')
$accountingIntegrityEnd=$accountingIntegrityText.IndexOf('// ملخص قرار للإقفال',$accountingIntegrityStart)
if($accountingIntegrityStart -lt 0 -or $accountingIntegrityEnd -le $accountingIntegrityStart) { throw 'Accounting integrity boundary is missing.' }
$accountingIntegrityBody=$accountingIntegrityText.Substring($accountingIntegrityStart,$accountingIntegrityEnd-$accountingIntegrityStart)
if($accountingIntegrityBody -match 'SqlCommand|SqlDataAdapter|command\.CommandText' -or
    $accountingIntegrityBody -notmatch 'ApplicationServices\.AccountingIntegrity\.Load\(') {
    throw 'Accounting integrity reads must use the application service and SQL repository.'
}
if ($inventoryText -match 'WITH OrderedMovements AS|LAG\(mv\.QuantityAfter\)' -or
    $inventoryText -notmatch 'ApplicationServices\.InventoryIntegrity\.Load\(') {
    throw 'Inventory integrity reads must use the application service and SQL repository.'
}
if ($inventoryText -match 'BuildInventoryAlertsQuery|ExpiryStock AS' -or
    $inventoryText -notmatch 'ApplicationServices\.InventoryAlerts') {
    throw 'Inventory alerts must use the typed application service and SQL repository.'
}
# بوابة عامة: ملفات العرض لا تنفذ أو تنشئ أوامر SQL مباشرة. تبقى نماذج
# الاتصال نفسها مسموحة، لكن القراءة والكتابة تمر عبر خدمات أو مستودعات.
$presentationFiles = Get-ChildItem -LiteralPath (Join-Path $root 'project_fo_3') -Filter '*.cs' -File |
    Where-Object { $_.Name -match '(Form|Forms|Home|Dashboard|Billing|Purchases|Login)\.cs$' }
foreach ($presentationFile in $presentationFiles) {
    $presentationText = Get-Content -LiteralPath $presentationFile.FullName -Raw
    if ($presentationText -match 'SqlCommand|SqlDataReader|SqlDataAdapter|Database\.CreateConnection|new\s+SqlConnection') {
        throw "Direct SQL access returned to presentation file: $($presentationFile.Name)"
    }
}
$currencyPresentationFiles = @('AccountingForms.cs', 'CurrencySettingsForm.cs', 'Billing.cs', 'Purchases.cs')
foreach ($currencyPresentationFile in $currencyPresentationFiles) {
    $currencyPresentationText = Get-Content -LiteralPath (Join-Path $root "project_fo_3\$currencyPresentationFile") -Raw
    if ($currencyPresentationText -match 'Database\.GetCurrencies\(|Database\.GetSystemCurrency\(|Database\.GetSystemCurrencyDisplay\(|Database\.SaveCurrency\(|Database\.DeleteCurrency\(|Database\.SetSystemCurrency\(|Database\.SaveCurrency\(') {
        throw "Currency database access returned to presentation file: $currencyPresentationFile"
    }
}
$pharmacyProfileFormPath = Join-Path $root 'project_fo_3\PharmacyCompanyForm.cs'
$pharmacyProfileFormText = Get-Content -LiteralPath $pharmacyProfileFormPath -Raw
if ([regex]::IsMatch($pharmacyProfileFormText, '(?<![A-Za-z])Database\.')) {
    throw 'Database access returned to PharmacyCompanyForm; use PharmacyProfileService.'
}
$connectionPresentationFiles = @('Login.cs', 'DatabaseSettingsForm.cs')
foreach ($connectionPresentationFile in $connectionPresentationFiles) {
    $connectionPresentationText = Get-Content -LiteralPath (Join-Path $root "project_fo_3\$connectionPresentationFile") -Raw
    if ([regex]::IsMatch($connectionPresentationText, '(?<![A-Za-z])Database\.')) {
        throw "Database access returned to connection presentation file: $connectionPresentationFile"
    }
}
$bootstrapFormPath = Join-Path $root 'project_fo_3\BootstrapManagerForm.cs'
$bootstrapFormText = Get-Content -LiteralPath $bootstrapFormPath -Raw
if ([regex]::IsMatch($bootstrapFormText, '(?<![A-Za-z])Database\.')) {
    throw 'Database access returned to BootstrapManagerForm; use BootstrapManagerService.'
}
$qaImportFormPath = Join-Path $root 'project_fo_3\QaCsvImportForm.cs'
$qaImportFormText = Get-Content -LiteralPath $qaImportFormPath -Raw
if ([regex]::IsMatch($qaImportFormText, '(?<![A-Za-z])Database\.')) {
    throw 'Database access returned to QaCsvImportForm; use QaCsvImportService.'
}
if ($qaImportFormText -match 'بيئة الاختبار|ليست QA') {
    throw 'Test-environment terminology returned to the normal import presentation.'
}
$homePath = Join-Path $root 'project_fo_3\Home.cs'
$homeText = Get-Content -LiteralPath $homePath -Raw
if ([regex]::IsMatch($homeText, '(?<![A-Za-z])Database\.')) {
    throw 'Database access returned to Home; use HomeWorkspaceService.'
}
# Declared navigation and onboarding actions use the shared accessible AntdUI button.
foreach ($name in @('screenModeButton','layoutModeButton','workspaceButton','workspaceToggleButton','monitorButton','addFirstMedicineButton','openFirstPurchaseButton')) {
    if ($homeText -notmatch ('private AntdUI\.Button ' + $name + ';') -or
        $homeText -notmatch ($name + ' = new AccessibleAntdButton')) {
        throw ('Home declared action returned to a native button: ' + $name)
    }
}
if ($homeText -notmatch 'TextRenderer.MeasureText' -or $homeText -notmatch 'DesktopComponents.StyleOutlinedFooterAction') {
    throw 'Home actions must retain measured responsive layout and shared theme.'
}
$accountingFormsPath = Join-Path $root 'project_fo_3\AccountingForms.cs'
$accountingFormsText = Get-Content -LiteralPath $accountingFormsPath -Raw
function Get-AccountingFormBody([string]$className, [string]$nextClassName) {
    $start = $accountingFormsText.IndexOf("public partial class $className")
    $end = $accountingFormsText.IndexOf("public partial class $nextClassName", $start)
    if ($start -lt 0 -or $end -le $start) { throw "Accounting form boundary could not be inspected: $className" }
    return $accountingFormsText.Substring($start, $end - $start)
}
$journalFormText = Get-AccountingFormBody 'JournalEntriesForm' 'JournalEntriesQuickForm'
if ($journalFormText -match 'AccountingService\.Get|CurrencyWorkspaceService\.Get' -or $journalFormText -notmatch 'JournalEntryLookupService\.Load') {
    throw 'Journal entries reads must remain behind JournalEntryLookupService.'
}
$quickJournalText = Get-AccountingFormBody 'JournalEntriesQuickForm' 'OpeningEntriesForm'
if ($quickJournalText -match 'AccountingService\.GetJournalEntries|LoadAccounts\(' -or $quickJournalText -notmatch 'JournalEntriesQuickReadService\.Load') {
    throw 'Quick journal reads must remain behind its read service.'
}
$accountingReportsText = Get-AccountingFormBody 'AccountingReportsForm' 'AccountingDataForm'
$accountingReportsAsyncText = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingReportsAsyncRead.cs') -Raw
$reportScreenText = $accountingReportsText + $accountingReportsAsyncText
if ($reportScreenText -match 'AccountingService\.Get(TaxReport|CostCenters|CostCenterAnalysis|GeneralJournalReport|TrialBalance|AccountSystemReport)|AccountingReportReadService\.Load|Task\.Run|SqlConnection|SqlCommand' -or
    $accountingReportsText -match 'private decimal SumColumn|movementCredit\s*-\s*movementDebit' -or
    $accountingReportsText -notmatch 'AccountingReportsPresentation.Summarize' -or
    $accountingReportsText -notmatch 'private AntdUI.Panel CreateReportCard' -or
    $accountingReportsText -match 'private Panel Create(Summary|Profit|Metric)Card' -or
    $accountingReportsText -notmatch 'CreateAccountingReportsReadSession' -or
    $accountingReportsAsyncText -notmatch 'await session\.LoadAsync\(request,cancellation.Token\)' -or
    $accountingReportsAsyncText -notmatch 'session.EnsureContext\(\)' -or
    $accountingReportsAsyncText -notmatch 'generation!=reportLoadGeneration' -or
    $accountingReportsAsyncText -notmatch 'UiSessionSubscription.Attach' -or
    $accountingReportsAsyncText -notmatch 'SameReportOutputContext') {
    throw 'Accounting report screens must use the captured async read session and guard stale outputs.'
}$legacyVoucherText = Get-AccountingFormBody 'VoucherForm' 'ReceiptVoucherForm'
if ($legacyVoucherText -match 'AccountingService\.Get(Vouchers|Accounts)' -or $legacyVoucherText -match 'LoadAccounts\(' -or
    $legacyVoucherText -notmatch 'CreateJournalEditorReadSession' -or $legacyVoucherText -notmatch 'session.Service.LoadAsync\(session.Actor,token\)' -or $legacyVoucherText -notmatch 'ReceiptVoucherWorkspaceService\.LoadHistory') {
    throw 'Legacy voucher reads must remain behind the shared read services.'
}$receiptVoucherText = Get-AccountingFormBody 'ReceiptVoucherForm' 'GeneralLedgerForm'
if ($receiptVoucherText -match 'AccountingService\.Get(Vouchers|Ledger|Accounts|CostCenters)' -or $receiptVoucherText -notmatch 'ReceiptVoucherWorkspaceService') {
    throw 'Receipt voucher reads must remain behind ReceiptVoucherWorkspaceService.'
}
$generalLedgerText = Get-AccountingFormBody 'GeneralLedgerForm' 'AccountingReportsForm'
if ($generalLedgerText -match 'AccountingService\.Get(Ledger|AccountBalanceBefore|Accounts|CostCenters)' -or $generalLedgerText -notmatch 'CreateGeneralLedgerReadSession') {
    throw 'General ledger reads must remain behind the pinned typed GeneralLedgerReadSession.'
}$billingPath = Join-Path $root 'project_fo_3\Billing.cs'
$billingText = Get-Content -LiteralPath $billingPath -Raw
if ($billingText -match 'SqlConnection|SqlCommand|SqlDataReader|SqlDataAdapter|Database\.CreateConnection') {
    throw 'SQL returned to the migrated Billing screen.'
}
$normalUiFiles = @('Login.cs', 'Home.cs', 'UiTheme.cs', 'DatabaseSettingsForm.cs')
foreach ($normalUiFile in $normalUiFiles) {
    $normalUiText = Get-Content -LiteralPath (Join-Path $root "project_fo_3\$normalUiFile") -Raw
    if ($normalUiText -match 'ProfileSourceText|__EnvironmentBadge|تنبيه بيئة الاختبار') {
        throw "A QA environment badge or source note returned to the normal UI: $normalUiFile"
    }
}
if ($billingText -notmatch 'ApplicationServices\.SalesInvoices\.Issue\(request\)') {
    throw 'Billing no longer delegates invoice issue to the application operation boundary.'
}
if ($billingText -match 'SaveDetailsToDatabaseLegacyReference') {
    throw 'The duplicate legacy invoice-save path returned to Billing.'
}
if ($billingText -match 'SalesInvoiceCancellationService\.' -or
    $billingText -notmatch 'ApplicationServices\.SalesInvoiceCancellations' -or
    $billingText -notmatch 'salesCancellationOperation\.Cancel') {
    throw 'Billing no longer delegates invoice cancellation to the application operation boundary.'
}
$saveStart = $billingText.IndexOf('private void SaveDetailsToDatabase(')
$saveEnd = $billingText.IndexOf('// دالة لاستخراج القيمة بعد العلامة', $saveStart)
if ($saveStart -lt 0 -or $saveEnd -le $saveStart) { throw 'Billing save boundary could not be inspected.' }
$saveBody = $billingText.Substring($saveStart, $saveEnd - $saveStart)
if ($saveBody -match 'SqlConnection|SqlCommand|SqlTransaction|Database\.CreateConnection') {
    throw 'SQL returned to the migrated Billing save boundary.'
}
$historyStart = $billingText.IndexOf('private void LoadInvoiceHistory()')
$historyEnd = $billingText.IndexOf('// زر الرجوع مع تنبيه', $historyStart)
if ($historyStart -lt 0 -or $historyEnd -le $historyStart) { throw 'Billing history boundary could not be inspected.' }
$historyBody = $billingText.Substring($historyStart, $historyEnd - $historyStart)
if ($historyBody -match 'SqlConnection|SqlCommand|SqlDataReader|Database\.CreateConnection') {
    throw 'SQL returned to the migrated Billing history and preview boundary.'
}
if (($historyBody -notmatch 'ApplicationServices\.SalesHistory\.Load\(' -and $historyBody -notmatch 'salesHistoryLoader\(') -or
    $historyBody -notmatch 'ApplicationServices\.SalesHistory\.LoadCustomerLines' -or
    $historyBody -notmatch 'ApplicationServices\.SalesHistory\.LoadLegacyInvoiceLines' -or
    $historyBody -notmatch 'ApplicationServices\.SalesHistory\.LoadCommercialInvoiceLines') {
    throw 'Billing no longer delegates all sales history reads to the application service.'
}
foreach ($workspacePresentationFile in @('Billing.cs', 'Purchases.cs')) {
    $workspacePresentationText = Get-Content -LiteralPath (Join-Path $root "project_fo_3\$workspacePresentationFile") -Raw
    if ([regex]::IsMatch($workspacePresentationText, '(?<![A-Za-z])Database\.')) {
        throw "Database access returned to workspace presentation file: $workspacePresentationFile"
    }
}
$purchasesText = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Purchases.cs') -Raw
$purchaseHistoryStart = $purchasesText.IndexOf('private void LoadPurchaseHistory()')
$purchaseHistoryEnd = $purchasesText.IndexOf('private void btnBack_Click', $purchaseHistoryStart)
if ($purchaseHistoryStart -lt 0 -or $purchaseHistoryEnd -le $purchaseHistoryStart) { throw 'Purchase history boundary could not be inspected.' }
$purchaseHistoryBody = $purchasesText.Substring($purchaseHistoryStart, $purchaseHistoryEnd - $purchaseHistoryStart)
if ($purchaseHistoryBody -match 'SqlConnection|SqlCommand|SqlDataReader|SqlDataAdapter|Database\.CreateConnection') {
    throw 'SQL returned to the migrated purchase history and preview boundary.'
}
if (($purchaseHistoryBody -notmatch 'ApplicationServices\.PurchaseWorkspace\.GetHistory\(' -and $purchaseHistoryBody -notmatch 'purchaseHistoryLoader\(') -or
    $purchaseHistoryBody -notmatch 'ApplicationServices\.PurchaseWorkspace\.GetHistoryLines') {
    throw 'Purchases no longer delegates history reads to the application service.'
}
$purchaseSaveStart = $purchasesText.IndexOf('private bool SavePurchaseToDatabase()')
$purchaseSaveEnd = $purchasesText.IndexOf('private void PrintPurchasePage', $purchaseSaveStart)
if ($purchaseSaveStart -lt 0 -or $purchaseSaveEnd -le $purchaseSaveStart) { throw 'Purchase save boundary could not be inspected.' }
$purchaseSaveBody = $purchasesText.Substring($purchaseSaveStart, $purchaseSaveEnd - $purchaseSaveStart)
if ($purchaseSaveBody -match 'SqlConnection|SqlCommand|SqlTransaction|Database\.CreateConnection') {
    throw 'SQL returned to the migrated purchase save boundary.'
}
$purchaseCoordinator = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\PurchaseWriteRecoveryCoordinator.cs') -Raw
$purchaseOperation = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\PurchaseInvoiceOperation.cs') -Raw
if ($purchaseSaveBody -notmatch 'purchaseRecovery\.SubmitAsync' -or
    $purchaseSaveBody -notmatch 'purchaseRecovery\.CompleteDisplayedAsync' -or
    $purchaseCoordinator -notmatch 'operation\.IssueIdentifiedAsync' -or
    $purchaseCoordinator -notmatch 'operation\.ObserveIdentifiedAsync' -or
    $purchaseCoordinator -notmatch 'store\.Save\(intent\)' -or
    $purchaseOperation -notmatch ':\s*IPurchaseInvoiceOperation') {
    throw 'Purchases must save through the protected attempt coordinator and identified application operation boundary.'
}
[xml]$desktop = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\project_fo_3.csproj') -Raw
$compiled = @($desktop.Project.ItemGroup.Compile | ForEach-Object { $_.Include })
if ($compiled -contains 'ThemeGuideForm.cs' -or $compiled -contains 'ThemeGuideForm.Designer.cs') {
    throw 'The deprecated MDL2 theme guide returned to the application build.'
}
if ($compiled -contains 'CommercialDomain.cs' -or $compiled -contains 'CommercialReturnDomain.cs') {
    throw 'Desktop duplicates domain compilation.'
}
Write-Output 'PASS: layer references, domain ownership, and migrated screen SQL boundaries.'






$currencyWorkspace = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\CurrencyWorkspaceService.cs') -Raw
if ((Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ReceiptVoucherWorkspaceService.cs') -Raw) -match 'GetBalance\(|GetLedger\(') {
    throw 'Voucher balance reads returned to the legacy desktop ledger projection.'
}
if ((Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ReceiptVoucherWorkspaceService.cs') -Raw) -match 'AccountingService.GetVouchers') {
    throw 'Voucher workspace history must use the typed application read service.'
}
$accountingSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingForms.cs') -Raw
$quickRead = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalEntriesQuickReadService.cs') -Raw
if ($quickRead -match 'AccountingService\.|SqlConnection|SqlCommand' -or $quickRead -notmatch 'session.History.LoadAsync') {
    throw 'Quick journal history must use the typed application service.'
}
$quickStart = $accountingSource.IndexOf('public partial class JournalEntriesQuickForm')
$quickEnd = $accountingSource.IndexOf('public partial class OpeningEntriesForm', $quickStart)
$quickBody = $accountingSource.Substring($quickStart, $quickEnd - $quickStart)
if ($quickBody -match 'Task.Run\(.*source.Text' -or $quickBody -notmatch 'JournalEntriesQuickReadService.LoadAsync\(request,cancellation.Token\)') {
    throw 'Quick journal queries must capture filter values before background reads.'
}
$quickSaveStart = $quickBody.IndexOf('private async void SaveManualEntry()')
$quickSaveEnd = $quickBody.IndexOf('internal async System.Threading.Tasks.Task<int> RunQuickJournalWriteAsync', $quickSaveStart)
$quickSave = $quickBody.Substring($quickSaveStart, $quickSaveEnd - $quickSaveStart)
$quickRecoverySource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\QuickJournalRecovery.cs') -Raw
$quickPreparationStart=$quickRecoverySource.IndexOf('private async Task<int> SaveQuickEntryAsync()')
if($quickPreparationStart -lt 0){throw 'Quick journal preparation boundary missing'}
$quickPreparation=$quickRecoverySource.Substring($quickPreparationStart)
if ($quickSave -notmatch 'await SaveQuickEntryAsync\(\)' -or
    $quickPreparation -match 'amount.Clear|description.Clear|LoadData\(' -or
    $quickPreparation -notmatch 'if \(!EnsureQuickEditor\(\) \|\| previewMode \|\| quickWriteBusy\) return 0;' -or
    $quickPreparation -notmatch 'await RunQuickJournalWriteAsync\(\(\) => ExecuteQuickRecoveryAsync\(recovery\)\)' -or
    $quickBody -notmatch 'acceptedQuickJournalId = acceptedId;[\s\S]*CompleteSavedQuickJournalPresentationAsync') {
    throw 'Quick journal accepted presentation must stay outside preparation/posting-failure and preview must never post.'
}
$balanceStart = $accountingSource.IndexOf('internal async System.Threading.Tasks.Task RefreshBalanceAsync()')
$balanceEnd = $accountingSource.IndexOf('private async System.Threading.Tasks.Task<int> SaveVoucherAsync()', $balanceStart)
if ($balanceStart -lt 0 -or $balanceEnd -le $balanceStart) { throw 'Voucher balance boundary is missing.' }
$balanceBody = $accountingSource.Substring($balanceStart, $balanceEnd - $balanceStart)
if ($balanceBody -match 'SqlConnection|SqlCommand|SqlDataAdapter|Database\.CreateConnection|AccountingService\.' -or
    $balanceBody -notmatch 'balanceReader.LoadAsync') {
    throw 'Voucher balance must delegate to its typed application read service.'
}
$voucherStart = $accountingSource.IndexOf('public partial class ReceiptVoucherForm')
$legacyStart = $accountingSource.IndexOf('public partial class VoucherForm')
$legacyBody = $accountingSource.Substring($legacyStart, $voucherStart - $legacyStart)
$legacySaveStart = $legacyBody.IndexOf('private async void SaveVoucher()')
$legacySaveEnd = $legacyBody.IndexOf('internal async System.Threading.Tasks.Task CompleteSavedVoucherPresentationAsync', $legacySaveStart)
if ($legacySaveStart -lt 0 -or $legacySaveEnd -le $legacySaveStart) { throw 'Legacy voucher accepted-document boundary is missing.' }
$legacySaveBody = $legacyBody.Substring($legacySaveStart, $legacySaveEnd - $legacySaveStart)
$legacySubmitEnd=$legacySaveBody.IndexOf('internal async System.Threading.Tasks.Task<bool> RunVoucherWriteAsync')
if($legacySubmitEnd -le 0){throw 'Legacy voucher submission boundary is missing.'}
$legacySubmit=$legacySaveBody.Substring(0,$legacySubmitEnd)
$legacyWrite=$legacySaveBody.Substring($legacySubmitEnd)
if($legacySubmit -notmatch 'if\(voucherPreview\|\|voucherWriteBusy\|\|!EnsureVoucherEditor\(\)\)return;' -or
   $legacySubmit -notmatch 'await RunVoucherWriteAsync' -or $legacySubmit -match 'amount.Clear|notes.Clear|LoadData\(' -or
   $legacyWrite -notmatch 'knownCommittedVoucherId=id;' -or $legacyWrite -notmatch 'voucherPresentationPending=true' -or
   $legacyWrite -notmatch 'CompleteSavedVoucherPresentationAsync' -or $legacyWrite -match 'amount.Clear|notes.Clear|LoadData\('){
    throw 'Legacy voucher preview/stale editor must not post; known acceptance and presentation must stay separate.'
}
$voucherEnd = $accountingSource.IndexOf('public partial class GeneralLedgerForm', $voucherStart)
if ($voucherStart -lt 0 -or $voucherEnd -le $voucherStart) { throw 'Voucher conversion boundary is missing.' }
$voucherBody = $accountingSource.Substring($voucherStart, $voucherEnd - $voucherStart)
if ($voucherBody -notmatch 'VoucherAmountCalculator.TryCalculate' -or
    $voucherBody -match 'CalculateLocalVoucherAmount|Math\.Round\(|(?:originalAmount|value)\s*\*\s*rate') {
    throw 'Voucher display and save must delegate conversion and rounding to the domain calculator.'
}
$saveStart = $voucherBody.IndexOf('private async System.Threading.Tasks.Task<int> SaveVoucherAsync()')
$saveEnd = $voucherBody.IndexOf('internal async System.Threading.Tasks.Task<int> RunReceiptWriteAsync', $saveStart)
if ($saveStart -lt 0 -or $saveEnd -le $saveStart) { throw 'Voucher accepted-document presentation boundary is missing.' }
$saveBody = $voucherBody.Substring($saveStart, $saveEnd - $saveStart)
if ($saveBody.LastIndexOf('return await RunReceiptWriteAsync(') -le $saveBody.LastIndexOf('return 0;')) {
    throw 'Accepted voucher refresh must run outside the posting-failure path.'
}
if($legacySubmit -notmatch 'InitializeVoucherRecovery' -or $legacySubmit -notmatch 'voucherRecovery\.Prepare\(request\)' -or $legacySubmit -notmatch 'voucherRecovery\.ExecuteAsync\(' -or
   $saveBody -notmatch 'InitializeReceiptRecovery' -or $saveBody -notmatch 'receiptRecovery\.Prepare\(request,draftInputs\)' -or $saveBody -notmatch 'receiptRecovery\.ExecuteAsync\(' -or
   $legacySubmit -match 'VoucherPostingDesktopService\.PostAsync' -or $saveBody -match 'VoucherPostingDesktopService\.PostAsync'){
    throw 'Both voucher editors must persist/freeze and recover the same request; direct legacy writes are forbidden.'
}
$printStart = $voucherBody.IndexOf('private async System.Threading.Tasks.Task<PharmacyManagement.Application.VoucherDocumentSnapshot> ReadVoucherPrintDocumentAsync')
$printEnd = $voucherBody.IndexOf('private void CaptureSelectedHistoryVoucher', $printStart)
if ($printStart -lt 0 -or $printEnd -le $printStart) { throw 'Saved voucher print read boundary is missing.' }
$printBody = $voucherBody.Substring($printStart, $printEnd - $printStart)
if ($printBody -notmatch 'reader.LoadAsync' -or
    $printBody -match 'history\.CurrentRow|lastSavedVoucherSummary|amount\.Text|notes\.Text|partyName\.Text|SqlCommand|SqlConnection|AccountingService\.') {
    throw 'Voucher printing must read the saved typed document, never current grid/input fallback data.'
}
if ($currencyWorkspace -match 'Database\.GetCurrencies\(|Database\.GetSystemCurrency\(|Database\.GetSystemCurrencyDisplay\(|Database\.SaveCurrency\(|Database\.DeleteCurrency\(|Database\.SetSystemCurrency\(') {
    throw 'Currency operations must use typed application services, not the legacy database facade.'
}


# Shared accounting choices must not reintroduce the legacy SQL facade into migrated readers.
foreach ($name in @('AccountingLookupDesktopService.cs','AccountingLookupTableProjection.cs','JournalEntryLookupService.cs',
    'ReceiptVoucherWorkspaceService.cs','GeneralLedgerWorkspaceService.cs','AccountingReportsDesktopRead.cs')) {
    $content = Get-Content -LiteralPath (Join-Path $root ('project_fo_3\' + $name)) -Raw
    if ($content -match 'AccountingService\.GetAccounts\(|AccountingService\.GetCostCenters\(|SqlConnection|SqlCommand|SqlDataAdapter|Database\.CreateConnection') {
        throw ('Accounting lookup reader crossed its typed boundary: ' + $name)
    }
}
# Six accounting reports have one active SQL implementation after the migration.
$legacyAccountingSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingService.cs') -Raw
if ($legacyAccountingSource -match 'public static DataTable Get(GeneralJournalReport|CostCenterAnalysis|TrialBalance|AccountSystemReport|TaxReport)\(' -or
    (Test-Path -LiteralPath (Join-Path $root 'project_fo_3\AccountingReportReadService.cs'))) {
    throw 'Unused legacy accounting report SQL or its facade returned; use the captured typed read session.'
}
$journalPostingSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingService.cs') -Raw
$postingStart = $journalPostingSource.IndexOf('public static int CreateJournalEntry(DateTime')
$postingEnd = $journalPostingSource.IndexOf('public static int CreateVoucher(', $postingStart)
if ($postingStart -lt 0 -or $postingEnd -le $postingStart) { throw 'Journal facade boundary missing.' }
$postingBody = $journalPostingSource.Substring($postingStart,$postingEnd-$postingStart)
if ($postingBody -notmatch 'JournalPostingDesktopService.Post' -or $postingBody -notmatch 'SqlJournalWriter.Create' -or
    $postingBody -match 'new SqlCommand|BeginTransaction|Database.WriteAudit|Math.Round\(') { throw 'Journal facade must delegate posting without SQL or rounding.' }
$writerSource = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalWriter.cs') -Raw
if ($writerSource -notmatch 'JournalEntryPreparation.Prepare' -or $writerSource -match 'Math.Round\(|debitTotal|creditTotal|transaction.Commit') {
    throw 'Shared SQL writer must use domain rules and leave transaction ownership to the caller.'
}
$desktopPosting = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalPostingDesktopService.cs') -Raw
if ($desktopPosting -match 'SqlCommand|SqlConnection|Database.CreateConnection|Math.Round\(' -or $desktopPosting -notmatch 'session.Service.Post') {
    throw 'Desktop journal posting must use its typed application service.'
}
$periodFacade = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\CommercialAccountingPeriodPolicy.cs') -Raw
if ($periodFacade -match 'new SqlCommand|AccountingPeriodsTb' -or $periodFacade -notmatch 'SqlBusinessPeriodPolicy.EnsureBusinessDateOpen') {
    throw 'Period SQL must live in Infrastructure, not the desktop compatibility facade.'
}


# Voucher posting uses one application and SQL transaction boundary, never desktop SQL.
$voucherPostSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingService.cs') -Raw
$voucherFacadeStart = $voucherPostSource.IndexOf('public static int CreateVoucher(')
$voucherFacadeEnd = $voucherPostSource.IndexOf('public static DataTable GetAccounts(', $voucherFacadeStart)
if ($voucherFacadeStart -lt 0 -or $voucherFacadeEnd -le $voucherFacadeStart) { throw 'Voucher facade boundary missing.' }
$voucherFacadeBody = $voucherPostSource.Substring($voucherFacadeStart,$voucherFacadeEnd-$voucherFacadeStart)
if ($voucherFacadeBody -notmatch 'VoucherPostingDesktopService.Post' -or $voucherFacadeBody -match 'new SqlCommand|BeginTransaction|Database.WriteAudit|Math.Round|JournalLineInput') { throw 'Voucher facade must delegate without SQL or line calculations.' }
$voucherDesktop = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\VoucherPostingDesktopService.cs') -Raw
if ($voucherDesktop -notmatch 'CreateVoucherPostSession' -or $voucherDesktop -match 'SqlCommand|SqlConnection|Database.CreateConnection|Math.Round') { throw 'Voucher desktop adapter crossed its application boundary.' }
if ($accountingSource -match 'AccountingService.CreateVoucher\(') { throw 'Voucher screens must use the posting adapter.' }
$voucherWriter = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlVoucherPostRepository.cs') -Raw
if ($voucherWriter -notmatch 'SqlJournalWriter.Create' -or $voucherWriter -match 'Math.Round|new JournalLineValues|Database.WriteAudit|SessionContext') { throw 'Voucher SQL must consume domain preparation and captured actor.' }

$accountSource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingService.cs') -Raw
$accountStart=$accountSource.IndexOf('public static void SaveAccount(');$accountEnd=$accountSource.IndexOf('private static bool IsSourcePosted(', $accountStart)
if($accountStart -lt 0 -or $accountEnd -le $accountStart){throw 'Account facade bounds missing.'}
$accountFacade=$accountSource.Substring($accountStart,$accountEnd-$accountStart)
if($accountFacade -notmatch 'AccountWritingDesktopService.Save' -or $accountFacade -notmatch 'AccountWritingDesktopService.Delete' -or $accountFacade -match 'SqlCommand|Database.CreateConnection|Database.WriteAudit'){throw 'Account facades must delegate without SQL.'}
$chartStart=$accountingSource.IndexOf('public partial class ChartOfAccountsForm');$chartEnd=$accountingSource.IndexOf('public partial class JournalEntriesForm',$chartStart)
$chartBody=$accountingSource.Substring($chartStart,$chartEnd-$chartStart)
if($chartBody -match 'AccountingService.SaveAccount|AccountingService.DeleteAccount|SqlCommand|Database.CreateConnection' -or $chartBody -notmatch 'AccountWritingDesktopService.SaveAsync' -or $chartBody -notmatch 'AccountWritingDesktopService.DeleteAsync'){throw 'Chart writes must use the typed application adapter.'}
if($chartBody -match 'else LoadData\(' -or $chartBody -notmatch 'generation!=chartReadGeneration'){throw 'Chart opening and refresh must be asynchronous and discard stale reads.'}
# The short voucher editor must never wait synchronously for SQL posting.
$shortVoucherStart=$accountingSource.IndexOf('public partial class VoucherForm :')
$shortVoucherEnd=$accountingSource.IndexOf('public partial class ReceiptVoucherForm :',$shortVoucherStart)
if($shortVoucherStart -lt 0 -or $shortVoucherEnd -le $shortVoucherStart){throw 'Short voucher boundary missing.'}
$shortVoucher=$accountingSource.Substring($shortVoucherStart,$shortVoucherEnd-$shortVoucherStart)
if($shortVoucher -match 'VoucherPostingDesktopService.Post\(' -or $shortVoucher -notmatch 'voucherRecovery.ExecuteAsync\('  -or $shortVoucher -notmatch 'voucherWriteBusy' -or $shortVoucher -notmatch 'CloseReason.UserClosing'){throw 'Short voucher must post asynchronously with duplicate and close guards.'}
# Detailed voucher save and save-and-print keep one captured request and accepted print identity.
if($voucherBody -match 'VoucherPostingDesktopService.Post\(' -or $voucherBody -notmatch 'receiptRecovery.ExecuteAsync\('  -or $voucherBody -notmatch 'receiptWriteBusy' -or $voucherBody -notmatch 'PrintCashierVoucherAsync\(acceptedId\)' -or $voucherBody -notmatch 'ResolveVoucherPrintId\(documentId\)'){throw 'Detailed voucher must post asynchronously and print its accepted identity.'}
# Quick journal writes capture a typed request and must not block the desktop thread with SQL.
if($quickBody -match 'AccountingService.CreateJournalEntry|JournalPostingDesktopService.Post\(' -or $quickRecoverySource -match 'Task.Run|writer.Service.Post\(' -or $quickRecoverySource -notmatch 'IAsyncJournalPostService' -or $quickRecoverySource -notmatch 'native.PostAsync\(request,writer.Actor,token\)' -or $quickBody -notmatch 'quickWriteBusy' -or $quickBody -notmatch 'CloseReason.UserClosing'){throw 'Quick journal must use native cancellable posting with duplicate and close guards.'}
$mainJournalStart=$accountingSource.IndexOf('public partial class JournalEntriesForm');
$mainJournalEnd=$accountingSource.IndexOf('public partial class JournalEntriesQuickForm',$mainJournalStart);
if($mainJournalStart -lt 0 -or $mainJournalEnd -le $mainJournalStart){throw 'Main journal boundary missing.'}
$mainJournal=$accountingSource.Substring($mainJournalStart,$mainJournalEnd-$mainJournalStart);
$mainRecoverySource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalEntriesRecovery.cs') -Raw
if($mainJournal -match 'AccountingService.CreateJournalEntry|JournalPostingDesktopService.Post\(|lines.Copy\(' -or $mainJournal -notmatch 'ExecuteJournalRecoveryAsync\(recovery\)' -or $mainRecoverySource -match 'Task.Run|postSession.Service.Post\(' -or $mainRecoverySource -notmatch 'IAsyncJournalPostService' -or $mainRecoverySource -notmatch 'native.PostAsync\(request,postSession.Actor,token\)' -or $mainJournal -notmatch 'journalDraftAccepted' -or $mainJournal -notmatch 'JournalDocumentDesktopService.ReadAsync' -or $mainJournal -notmatch 'JournalDocumentDesktopService.PrintLines'){throw 'Main journal must use native cancellable posting and print a saved document.'}
$journalDocumentAdapter=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalDocumentDesktopService.cs') -Raw;
if($journalDocumentAdapter -match 'SqlCommand|SqlConnection|Database.CreateConnection|Math.Round' -or $journalDocumentAdapter -notmatch 'ApplicationServices.CreateJournalDocumentReadSession'){throw 'Journal document adapter crossed its application boundary.'}
$journalLookupStart=$mainJournal.IndexOf('internal async System.Threading.Tasks.Task<bool> LoadInitialLookupsAsync(');
$journalLookupEnd=$mainJournal.IndexOf('private void NewEntry()',$journalLookupStart);
if($journalLookupStart -lt 0 -or $journalLookupEnd -le $journalLookupStart -or $mainJournal.Substring($journalLookupStart,$journalLookupEnd-$journalLookupStart) -match 'journalDraftAccepted\s*='){throw 'Lookup completion must not clear accepted journal draft state.'}
# The migrated journal editor delegates currency conversion, preview and totals to Domain.
if($mainJournal -match 'Math.Round|Math.Max\(ParseMoney|totalDebit\s*\+=' -or $mainJournal -notmatch 'JournalAmountCalculator.ToLocal' -or $mainJournal -notmatch 'JournalAmountCalculator.Preview' -or $mainJournal -notmatch 'JournalAmountCalculator.Sum'){throw 'Journal editor currency arithmetic returned to Desktop.'}

# Inventory writes use the Application contract and a captured actor in the desktop adapter.
$inventoryForms=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryForms.cs') -Raw;
$inventorySaveStart=$inventoryForms.IndexOf('private async void SaveOperation()');
$inventorySaveEnd=$inventoryForms.IndexOf('private bool ValidateBeforeSave(',$inventorySaveStart);
if($inventorySaveStart -lt 0 -or $inventorySaveEnd -le $inventorySaveStart){throw 'Inventory async save boundary missing.'}
$inventorySave=$inventoryForms.Substring($inventorySaveStart,$inventorySaveEnd-$inventorySaveStart);
if($inventorySave -match 'InventoryService.Apply|SqlCommand|SqlConnection' -or $inventorySave -notmatch 'PostInventorySubmissionAsync\(request\)' -or $inventorySave -notmatch 'await write\(\)' -or $inventorySave -notmatch 'inventoryWriteBusy'){throw 'Inventory writes must use the async application boundary and busy guard.'}
$inventoryAdapter=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryPostingDesktopService.cs') -Raw;
if($inventoryAdapter -match 'SqlCommand|SqlConnection|MessageBox|Math.Round' -or $inventoryAdapter -notmatch 'ApplicationServices.CreateInventoryPostingSession\(\)'){throw 'Inventory posting adapter crossed its application boundary.'}
$inventoryLegacy=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryService.cs') -Raw;
$inventoryWriteStart=$inventoryLegacy.IndexOf('public static void ApplyOpeningStock(');
$inventoryWriteEnd=$inventoryLegacy.IndexOf('private static void AddDateParameters(',$inventoryWriteStart);
if($inventoryWriteStart -lt 0 -or $inventoryWriteEnd -le $inventoryWriteStart -or $inventoryLegacy.Substring($inventoryWriteStart,$inventoryWriteEnd-$inventoryWriteStart) -match 'new SqlCommand|BeginTransaction|transaction.Commit'){throw 'Inventory writing SQL returned to Desktop.'}
$movementAdapter=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryMovementService.cs') -Raw;
if($movementAdapter -match 'new SqlCommand|INSERT INTO|UPDATE dbo' -or $movementAdapter -notmatch 'SqlInventoryMovementWriter.Record'){throw 'Movement writer SQL returned to Desktop.'}

# Pending inventory retries reuse their original source; SQL serializes that source before reading history.
if($inventorySave -notmatch 'request=BindInventorySubmission\(request\)' -or $inventoryForms -notmatch 'pendingInventoryRequest.HasSameContent\(candidate\)'){throw 'Inventory save lost its stable pending source or content check.'}
$inventoryRepository=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlInventoryPostRepository.cs') -Raw;
if($inventoryRepository -notmatch 'sp_getapplock' -or $inventoryRepository -notmatch 'LockRequest\(connection,transaction,request.SourceId\)' -or $inventoryRepository -notmatch 'FindCommittedRequest\(connection,transaction,request,actor\)'){throw 'Inventory source locking or committed snapshot lookup missing.'}
# Manual inventory preserves SQL server-local dates and guards the same date before stock mutation.
$manualWrite=$inventoryRepository.Substring($inventoryRepository.IndexOf('public static InventoryPostResult ExecuteStockOperation('));
$periodGuard=$manualWrite.IndexOf('SqlBusinessPeriodPolicy.EnsureBusinessDateOpen(connection,transaction,movementDate)');
$stockLock=$manualWrite.IndexOf('SqlInventoryMovementWriter.GetCurrentQuantity(');
if($periodGuard -lt 0 -or $stockLock -le $periodGuard -or $manualWrite -notmatch 'RecordAtDatabaseTime\(' -or $manualWrite -notmatch 'actorName, movementDate\)'){throw 'Manual inventory period guard or shared server-local movement date missing.'}

# A rejected source is editable only after a verified rollback with no earlier movement and no Commit attempt.
if($inventoryRepository -notmatch 'if\(newSource&&!commitAttempted&&rolledBack\)' -or $inventoryRepository -notmatch 'onNewSource\?\.Invoke\(\)'){throw 'Inventory rejection lost its confirmed rollback boundary.'}
$inventoryRejectionStart=$inventoryForms.IndexOf('internal void HandleInventoryWriteIssue(Exception ex)');
$inventoryIssueWrapperStart=$inventoryForms.IndexOf('private void ShowInventoryWriteIssue(Exception error)');
$inventoryIssueWrapperEnd=$inventoryForms.IndexOf('private string InventoryInputIssue(Exception error)');
if($inventoryRejectionStart -lt 0 -or $inventoryIssueWrapperStart -le $inventoryRejectionStart -or $inventoryIssueWrapperEnd -le $inventoryIssueWrapperStart){throw 'Inventory rejection handler or notification wrapper boundary missing.'}
$inventoryRejection=$inventoryForms.Substring($inventoryRejectionStart,$inventoryIssueWrapperStart-$inventoryRejectionStart);
$inventoryIssueWrapper=$inventoryForms.Substring($inventoryIssueWrapperStart,$inventoryIssueWrapperEnd-$inventoryIssueWrapperStart);
if($inventoryForms -notmatch 'ShowInventoryWriteIssue\(ex\)' -or $inventoryIssueWrapper -notmatch 'HandleInventoryWriteIssue\(error\)' -or
   $inventoryRejection -notmatch 'ex as PharmacyManagement.Application.InventoryPostNotCommittedException' -or
   $inventoryRejection -notmatch 'if\(rejected!=null&&pendingInventoryRequest!=null&&string.Equals\(rejected.SourceId,pendingInventoryRequest.SourceId,StringComparison.Ordinal\)\)'){
 throw 'Inventory UI must reset only its own confirmed rejected source through the notification wrapper.'
}

# Retained inventory requests must keep the first service and its unopened captured connection.
$composition=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw;
if($composition -notmatch 'new System.Data.SqlClient.SqlConnection\(connectionString\)' -or $inventoryForms -notmatch 'pendingInventorySession.PostAsync\(request\)' -or $inventoryAdapter -notmatch 'EnsureContext\(\)'){throw 'Inventory retained context or frozen connection missing.'}

# The immutable recovery record must be flushed before dispatch; cleanup runs after known acceptance.
$inventoryDispatch=$inventoryForms.Substring($inventoryForms.IndexOf('internal System.Threading.Tasks.Task<PharmacyManagement.Application.InventoryPostResult> PostInventorySubmissionAsync('));
$persist=$inventoryDispatch.IndexOf('pendingInventoryStore?.Save(request)');
$dispatch=$inventoryDispatch.IndexOf('return pendingInventorySession.PostAsync(request)');
if($persist -lt 0 -or $dispatch -le $persist){throw 'Inventory must persist its pending source before SQL dispatch.'}
$operationStart=$inventoryForms.IndexOf('    internal sealed partial class InventoryOperationForm');
$operationEnd=$inventoryForms.IndexOf('    internal sealed partial class InventoryAlertsForm',$operationStart);
if($operationStart -lt 0 -or $operationEnd -lt 0){throw 'Inventory operation boundary missing.'}
$operationUi=$inventoryForms.Substring($operationStart,$operationEnd-$operationStart);
if($operationUi -match 'InventoryService\.|SqlCommand|SqlConnection' -or $operationUi -notmatch 'await refresh\(\)' -or $operationUi -notmatch 'inventoryAsyncReads&&!inventoryReadReady'){throw 'Inventory operation must use typed async reads and guard pending balance.'}
$inventoryRecovery=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryOperationRecovery.cs') -Raw;
if($inventoryRecovery -match 'SqlCommand|SqlConnection|\.PostAsync\(' -or $inventoryRecovery -notmatch 'pendingInventoryStore\?\.Resolve\(request.SourceId\)' -or $inventoryForms -notmatch 'PrepareNextInventoryDraft\(request\)'){throw 'Inventory recovery must not post automatically and must resolve only known acceptance.'}
# Movement reports must share typed asynchronous reads and invalidate stale outputs.
$movementStart=$inventoryForms.IndexOf('    internal sealed partial class InventoryMovementForm :');
$movementEnd=$inventoryForms.IndexOf('    internal sealed partial class InventoryMovementSummaryForm :',$movementStart);
if($movementStart -lt 0 -or $movementEnd -lt 0){throw 'Movement report class boundary missing.'}
$movementUi=$inventoryForms.Substring($movementStart,$movementEnd-$movementStart);
$movementReads=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryMovementReads.cs') -Raw;
$movementDesigner=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryMovementForm.Designer.cs') -Raw;
if(($movementUi+$movementReads+$movementDesigner) -match 'InventoryService\.|SqlCommand|SqlConnection|\.PostAsync\('){throw 'Movement report must not contain SQL, legacy inventory reads or posting.'}
if($movementReads -notmatch 'await session.LoadSnapshotAsync\(' -or $movementReads -notmatch 'generation != movementGeneration' -or $movementReads -notmatch 'InventoryMovementPresentation.ToTable\(snapshot\)' -or $movementDesigner -notmatch 'if \(movementResultReady\)' -or $movementDesigner -notmatch 'Disposed.*CancelMovementRead'){throw 'Movement report lost cancellation, shared projection, disposal or stale output protection.'}
Write-Output 'PASS movement report typed async read and stale output architecture boundary.'
$alertsStart=$inventoryForms.IndexOf('    internal sealed partial class InventoryAlertsForm :');
$alertsEnd=$inventoryForms.IndexOf('    internal sealed partial class InventoryTurnoverForm :',$alertsStart);
if($alertsStart -lt 0 -or $alertsEnd -le $alertsStart){throw 'Inventory alert screen boundary missing.'}
$alertsUi=$inventoryForms.Substring($alertsStart,$alertsEnd-$alertsStart);
$alertReads=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryMovementReads.cs') -Raw;
$alertApplication=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\InventoryAlertReads.cs') -Raw;
$alertInfrastructure=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlInventoryAlertRepository.cs') -Raw;
if($alertsUi -match 'InventoryService\.GetInventoryAlerts' -or $alertsUi -notmatch 'await RefreshAlertsAsync\(' -or
   $alertReads -notmatch 'await session\.LoadAsync\(stockLimit, expiryLimit' -or
   $alertReads -notmatch 'generation != alertReadGeneration' -or
   $alertApplication -notmatch 'IInventoryAlertSessionReadService' -or
   $alertInfrastructure -notmatch 'ExecuteReaderAsync\(CommandBehavior\.SequentialAccess, cancellation\)'){
    throw 'Inventory alerts must use an authorized, cancellable async read session with stale-result protection.'
}
Write-Output 'PASS inventory alerts async SQL read, authorization, cancellation and stale-result boundary.'
$summaryStart=$inventoryForms.IndexOf('    internal sealed partial class InventoryMovementSummaryForm :');
$summaryEnd=$inventoryForms.IndexOf('    internal sealed partial class InventoryOperationForm :',$summaryStart);
$turnoverStart=$inventoryForms.IndexOf('    internal sealed partial class InventoryTurnoverForm :');
if($summaryStart -lt 0 -or $summaryEnd -le $summaryStart -or $turnoverStart -lt 0){throw 'Inventory summary/turnover screen boundaries missing.'}
$summaryUi=$inventoryForms.Substring($summaryStart,$summaryEnd-$summaryStart);
$turnoverUi=$inventoryForms.Substring($turnoverStart);
$operationalApplication=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\InventoryOperationalReportReads.cs') -Raw;
if(($summaryUi+$turnoverUi) -match 'InventoryService\.Get(MovementSummary|TurnoverReport)' -or
   $summaryUi -notmatch 'await RefreshMovementSummaryAsync\(' -or $turnoverUi -notmatch 'await RefreshTurnoverAsync\(' -or
   $alertReads -notmatch 'await session\.LoadMovementSummaryAsync\(request, cancellation\.Token\)' -or
   $alertReads -notmatch 'await session\.LoadTurnoverAsync\(request, cancellation\.Token\)' -or
   $alertReads -notmatch 'generation != summaryReadGeneration' -or $alertReads -notmatch 'generation != turnoverReadGeneration' -or
   $operationalApplication -notmatch 'IInventoryOperationalReportReadService'){
    throw 'Inventory summary and turnover must use typed async reads with stale-result protection.'
}
Write-Output 'PASS inventory movement summary and turnover async read, cancellation and stale-result boundaries.'
$integrityForm=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryIntegrityForm.cs') -Raw;
$integrityReads=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryMovementReads.cs') -Raw;
$integrityApplication=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\InventoryIntegrityRead.cs') -Raw;
$integrityInfrastructure=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlInventoryIntegrityReadRepository.cs') -Raw;
if($integrityForm -match 'InventoryService\.GetMovementIntegrityIssues' -or
   $integrityForm -notmatch 'await session\.LoadAsync\(cancellation\.Token\)' -or
   $integrityForm -notmatch 'generation != readGeneration' -or
   $integrityForm -notmatch 'FormClosed.*CancelRead' -or
   $integrityReads -notmatch 'IInventoryIntegritySessionReadService' -or
   $integrityApplication -notmatch 'IAsyncInventoryIntegrityReadRepository' -or
   $integrityInfrastructure -notmatch 'ExecuteReaderAsync\(CommandBehavior\.SequentialAccess, cancellation\)'){
    throw 'Inventory integrity must use an authorized async SQL read with cancellation and stale-result protection.'
}
Write-Output 'PASS inventory integrity async SQL read, authorization, cancellation and stale-result boundary.'
$invoiceAlertPresenter=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InvoiceInventoryAlertPresenter.cs') -Raw;
if($invoiceAlertPresenter -match 'Task\.Run|InventoryService\.GetInventoryAlerts' -or
   $invoiceAlertPresenter -notmatch 'CreateInvoiceInventoryAlertReadSession' -or
   $invoiceAlertPresenter -notmatch 'session\.LoadAsync\(10, 30, names, cancellation\.Token\)' -or
   $invoiceAlertPresenter -notmatch 'owner\.FormClosed \+= cancelOnClose' -or
   (Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw) -notmatch '(?s)CanExecute\(AppOperation\.PostSalesInvoice\).*CanExecute\(AppOperation\.PostPurchaseInvoice\)' -or
   $alertReads -notmatch 'affectedMedicines\)'){
    throw 'Post-save invoice inventory alerts must use the cancellable authorized async alert session.'
}
Write-Output 'PASS post-save invoice inventory alerts use async session with affected medicines and close cancellation.'

$customerScreen = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\CustomerSupplierManagementForm.cs') -Raw
$customerRepo = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlSalesCustomerRepository.cs') -Raw
if ($customerScreen -match 'customerService\.GetAll\(' -or $customerScreen -match 'Task\.Run\(' -or
    $customerScreen -notmatch 'await readService\.GetAllAsync\(actor, cancellation\.Token\)' -or
    $customerScreen -notmatch 'version != readVersion' -or $customerScreen -notmatch 'IsSessionCurrent' -or
    $customerRepo -notmatch 'OpenAsync\(cancellationToken\)' -or
    $customerRepo -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
    $customerRepo -notmatch 'ReadAsync\(cancellationToken\)') {
    throw 'Customer directory must use SQL async reads with cancellation, session and stale-result protection.'
}
Write-Output 'PASS customer directory SQL async, cancellation, session and stale-result boundary.'

$supplierScreen = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Manufacturer.cs') -Raw
$supplierRepository = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlManufacturerRepository.cs') -Raw
if ($supplierScreen -match 'Service\.GetAll\(' -or $supplierScreen -match 'Task\.Run\(' -or
    $supplierScreen -notmatch 'await readService\.GetAllAsync\(actor, cancellation\.Token\)' -or
    $supplierScreen -notmatch 'version != readVersion' -or $supplierScreen -notmatch 'IsCurrentSession' -or
    $supplierRepository -notmatch 'OpenAsync\(cancellationToken\)' -or
    $supplierRepository -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
    $supplierRepository -notmatch 'ReadAsync\(cancellationToken\)') {
    throw 'Supplier screen must use cancellable SQL async reads and reject stale session results.'
}
Write-Output 'PASS supplier SQL async, cancellation, session and stale-result boundary.'

$medicineService = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\Medicines.cs') -Raw
$medicineComposition = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
if ($medicineService -match '"Unknown"' -or
    $medicineService -notmatch 'GetAll\(OperationActor actor\)' -or
    $medicineService -notmatch 'GenerateAvailableBarcode\(string excludedMedicineName, OperationActor actor\)' -or
    $medicineService -notmatch 'RequireActor\(actor, false\)' -or
    $medicineService -notmatch 'this.canManage = canManage \?\? throw' -or
    $medicineComposition -notmatch 'internal static IMedicineService Medicines => CreateMedicineSession\(\).Service;' -or
    $medicineComposition -notmatch 'canManage=actor=>matches\(actor\)&&PermissionService.CanOpen\(AppPermission.ManageMedicines\)' -or
    $medicineComposition -notmatch 'canRead=actor=>matches\(actor\)' -or
    $medicineComposition -notmatch 'SessionContext.CurrentSessionRevision==revision' -or
    $medicineComposition -notmatch 'SqlMedicineRepository\(\(\)=>new System.Data.SqlClient.SqlConnection\(connectionString\)\)') {
    throw 'Medicine directory, barcode and mutations require explicit trusted session policies; no Unknown audit identity fallback.'
}
Write-Output 'PASS medicine typed actors and mandatory read/manage session policy boundary.'

$medicineScreen = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Medicine.cs') -Raw
$medicineRepository = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlMedicineRepository.cs') -Raw
if ($medicineScreen -match 'Service\.(GetAll|GetCompanyNames)\(' -or $medicineScreen -match 'Task\.Run\(' -or
    $medicineScreen -notmatch 'Task.WhenAll\(medicineRead, companyRead\)' -or
    $medicineScreen -notmatch 'version != readVersion' -or $medicineScreen -notmatch 'IsCurrentSession' -or
    $medicineRepository -notmatch 'OpenAsync\(cancellationToken\)' -or
    $medicineRepository -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
    $medicineRepository -notmatch 'ReadAsync\(cancellationToken\)') {
    throw 'Medicine directory/options must use SQL async cancellation with atomic UI and stale/session protection.'
}
Write-Output 'PASS medicine directory/options true SQL async and stale/session boundary.'

if ($medicineScreen -match 'Service\.GenerateAvailableBarcode\(' -or
    $medicineScreen -notmatch 'await barcodeService.GenerateAvailableBarcodeAsync' -or
    $medicineScreen -notmatch 'CancelBarcodeRead' -or
    $medicineRepository -notmatch 'ExecuteScalarAsync\(cancellationToken\)') {
    throw 'Medicine barcode UI must use cancellable async SQL and reject stale/session results.'
}

if ($medicineScreen -match 'Service\.(Create|Update|Delete)\(' -or
    $medicineScreen -notmatch 'await operationService.CreateAsync' -or $medicineScreen -notmatch 'await operationService.UpdateAsync' -or
    $medicineScreen -notmatch 'await operationService.DeleteAsync' -or $medicineScreen -notmatch 'if \(saving\) args.Cancel = true') {
    throw 'Medicine writes must await Application services, guard repeated requests and prevent closing pending saves.'
}

foreach ($name in @('CreateAsync','UpdateAsync','DeleteAsync')) {
    $body = [regex]::Match($medicineRepository, 'public async Task(?:<bool>)? '+$name+'\([\s\S]*?(?=\r?\n        (?:public|private) )').Value
    if (!$body -or $body -match '\bconnection.BeginTransaction\(' -or $body -match '\btransaction\.(Commit|Rollback)\(' -or
        $body -notmatch 'await SqlTransactionBoundaryDispatcher.BeginAsync' -or
        $body -notmatch 'await SqlTransactionBoundaryDispatcher.CommitAsync' -or
        $body -notmatch 'await SqlTransactionBoundaryDispatcher.RollbackAsync') {
        throw "Medicine $name must await each unsupported transaction boundary, preserving one sequential transaction."
    }
}
Write-Output 'PASS medicine async write transaction start/commit/rollback dispatch boundary.'

$billingMedicineScreen = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Billing.cs') -Raw
$medicineApplication = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\Medicines.cs') -Raw
if ($billingMedicineScreen -match 'ApplicationServices.Medicines.GetAll\(' -or
    $billingMedicineScreen -match 'private (?:int GetSellingPrice|int GetAvailableQuantity|MedicineRecord FindMedicine)\(' -or
    $billingMedicineScreen -notmatch 'await medicineService.GetNamesAsync' -or
    $billingMedicineScreen -match 'await medicineService.GetAllAsync' -or
    $medicineApplication -notmatch 'Task<IReadOnlyList<string>> GetNamesAsync\(OperationActor actor, CancellationToken cancellationToken\)' -or
    $medicineRepository -notmatch 'DirectoryNamesSql = @"SELECT MedName\s+FROM dbo\.MedicineTb1\s+ORDER BY MedName;"' -or
    $medicineRepository -notmatch 'command\.CommandText = DirectoryNamesSql;' -or
    $billingMedicineScreen -notmatch 'await medicineService.FindByNameAsync' -or
    $billingMedicineScreen -notmatch 'medicineCartPending' -or $billingMedicineScreen -notmatch 'IsMedicineReadSession' -or
    $medicineRepository -notmatch 'WHERE MedName=@MedicineName' -or $medicineRepository -notmatch 'StringComparison.OrdinalIgnoreCase') {
    throw 'POS medicine names and selected/cart quote must use typed async reads, one targeted record and stale/session guards.'
}
Write-Output 'PASS POS names/selection/cart targeted async medicine read boundary.'
$barcodeApplication = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\MedicineSearch.cs') -Raw
$barcodeRepository = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlMedicineSearchRepository.cs') -Raw
if ($billingMedicineScreen -match 'MedicineLookupService.FindByBarcode\(' -or
    $billingMedicineScreen -notmatch 'await medicineSearchService.FindByBarcodeAsync' -or
    $billingMedicineScreen -notmatch 'CancelMedicineBarcodeRead' -or
    $barcodeApplication -notmatch 'RequireBarcodeActor\(current\)' -or
    $barcodeRepository -notmatch 'ValidateBarcodeMatch\(match, duplicate\)') {
    throw 'POS barcode must use actor-authorized cancellable async SQL and preserve duplicate rejection.'
}
Write-Output 'PASS POS actor-authorized async barcode boundary.'
$currencyPos = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Billing.cs') -Raw
$currencySale = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\SalesInvoiceOperation.cs') -Raw
$currencyGuard = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ImportedInvoiceApprovalGuard.cs') -Raw
if ($currencyPos -match 'CurrencyWorkspaceService.GetSystemCurrencyDisplay' -or
    $currencyPos -notmatch 'await currencyReadService.GetSystemCurrencySnapshotAsync' -or
    $currencyPos -notmatch 'ExpectedCurrencyCode = RequireCurrencySnapshot\(\).CurrencyCode' -or
    $currencyPos -notmatch 'savedLegacyCurrencyText = currencySnapshot.DisplayText' -or
    $currencySale.IndexOf('EnsureDraftCurrency') -lt 0 -or
    $currencySale.IndexOf('EnsureDraftCurrency') -gt $currencySale.IndexOf('ResolveCustomerId') -or
    $currencyGuard -notmatch 'WITH \(HOLDLOCK\)') {
    throw 'POS display must use frozen async currency identity, pass expected code before financial writes, and print saved identity.'
}
Write-Output 'PASS POS frozen currency read/expected transaction identity/saved print boundary.'  

$quickSearchContract = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\MedicineSearch.cs') -Raw
$quickSearchPresentation = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\MedicineQuickSearchForm.cs') -Raw
$quickSearchRoot = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
if ($quickSearchContract -notmatch 'SearchAsync\(string query, OperationActor actor, CancellationToken cancellationToken\)' -or
    $quickSearchContract -notmatch 'RequireSearchActor\(current\)' -or
    $quickSearchContract -match 'public async Task<IReadOnlyList<MedicineSearchItem>> SearchAsync\(string query, CancellationToken') {
    throw 'Actor-free medicine search or post-read authorization regression.'
}
if ($quickSearchPresentation -match 'ApplicationServices\.MedicineSearch\.SearchAsync|SqlConnection|SqlCommand|Database\.' -or
    $quickSearchPresentation -notmatch 'searchSession\.Service\.SearchAsync\(query, searchSession\.Actor' -or
    $quickSearchPresentation -notmatch 'if \(!CurrentContext\) \{ RejectInvalidSearchContext\(\); return; \}' -or
    $quickSearchPresentation -notmatch 'CurrentContext => searchSession\.IsCurrent && \(frequentSession == null \|\| frequentSession\.IsCurrent\)' -or
    $quickSearchPresentation -notmatch 'ApplicationServices\.CreateFrequentMedicineSession\(\)') {
    throw 'Quick search returned to unscoped reads or lost context guards.'
}
if ($quickSearchRoot -notmatch 'CreateMedicineQuickSearchSession' -or
    $quickSearchRoot -notmatch 'new SqlMedicineSearchRepository\(\(\) => new System.Data.SqlClient.SqlConnection\(connectionString\)\)') {
    throw 'Quick search no longer pins its repository connection at the composition root.'
}
Write-Output 'PASS quick search mandatory actor, post-read authorization and pinned-context presentation boundary.'
$searchSessionSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
$searchSessionStart = $searchSessionSource.IndexOf('internal static MedicineQuickSearchSession CreateMedicineQuickSearchSession(System.Func<string>')
$searchSessionEnd = $searchSessionSource.IndexOf('internal static InventoryStockReadSession', $searchSessionStart)
if ($searchSessionStart -lt 0 -or $searchSessionEnd -le $searchSessionStart) { throw 'Quick search composition session boundary missing.' }
$searchSessionBody = $searchSessionSource.Substring($searchSessionStart, $searchSessionEnd - $searchSessionStart)
if ($searchSessionBody -notmatch 'CurrentSessionRevision == sessionRevision' -or
    $searchSessionBody -notmatch 'connectionStringProvider\(\)' -or
    $searchSessionBody -notmatch 'CurrentEmployeeId' -or
    $searchSessionBody -notmatch 'CanOpen\(AppPermission.Sales\)' -or
    $searchSessionBody -notmatch 'CanOpen\(AppPermission.Purchases\)') {
    throw 'Quick search lost session generation, connection identity, employee or sales/purchase read policy.'
}
Write-Output 'PASS quick search root session generation, employee/profile and sales/purchase policy boundary.'
$inventoryFactorySource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
$inventoryFactoryStart = $inventoryFactorySource.IndexOf('internal static InventoryStockReadSession CreateInventoryStockReadSession()')
$inventoryFactoryEnd = $inventoryFactorySource.IndexOf('internal static readonly IInventoryPostService InventoryPosting', $inventoryFactoryStart)
if ($inventoryFactoryStart -lt 0 -or $inventoryFactoryEnd -le $inventoryFactoryStart) { throw 'Inventory read/posting factories cannot be inspected.' }
$inventoryFactoryBody = $inventoryFactorySource.Substring($inventoryFactoryStart, $inventoryFactoryEnd - $inventoryFactoryStart)
if ([regex]::Matches($inventoryFactoryBody, 'long sessionRevision = SessionContext.CurrentSessionRevision;').Count -ne 5 -or
    [regex]::Matches($inventoryFactoryBody, 'CurrentSessionRevision == sessionRevision').Count -ne 5) {
    throw 'Inventory stock/alerts/operational/integrity/posting factory lost login generation guard.'
}
Write-Output 'PASS all five inventory read/posting root factories capture and verify login generation.'
$posSessionSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Billing.cs') -Raw
if ($posSessionSource -notmatch 'CurrentSessionRevision == sessionRevision' -or
    $posSessionSource -notmatch 'connectionIsCurrent\(\) &&' -or
    [regex]::Matches($posSessionSource, 'CaptureMedicineReadContext\(actor, employee\)').Count -ne 5 -or
    $posSessionSource -notmatch 'currencyConnectionIsCurrent = readContextIsCurrent;' -or
    $posSessionSource -notmatch 'currencyConnectionIsCurrent = CaptureMedicineReadContext\(currencyActor, currencyEmployee\);') {
    throw 'POS names/selection/barcode/cart/currency lost captured login revision or profile context guard.'
}
Write-Output 'PASS POS five asynchronous read paths and frozen currency capture login revision and runtime profile identity.'

$customerRootSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
$customerRootStart = $customerRootSource.IndexOf('internal static SalesCustomerReadSession CreateSalesCustomerReadSession()')
$customerRootEnd = $customerRootSource.IndexOf('internal static InventoryStockReadSession CreateInventoryStockReadSession()', $customerRootStart)
if ($customerRootStart -lt 0 -or $customerRootEnd -le $customerRootStart) { throw 'Customer scoped read root is missing.' }
$customerRootBody = $customerRootSource.Substring($customerRootStart, $customerRootEnd - $customerRootStart)
if ($customerRootBody -notmatch 'CurrentSessionRevision == revision' -or $customerRootBody -notmatch 'actor => false' -or
    $customerRootBody -notmatch 'SqlConnection\(connectionString\)' -or $customerRootBody -notmatch 'CurrentEmployeeId') {
    throw 'Customer read session lost login/profile/employee identity or read-only composition.'
}
$customerUiSource = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\CustomerSupplierManagementForm.cs') -Raw
if ($customerUiSource -notmatch 'CreateSalesCustomerReadSession\(\)' -or $customerUiSource -notmatch 'customerListContextIsCurrent = readContextIsCurrent;' -or
    [regex]::Matches($customerUiSource, 'if \(!EnsureCurrentCustomerList\(\)\) return;').Count -ne 3) {
    throw 'Customer cached list/search/editor/selection lost retained context guards.'
}
$customerApplicationSource = Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\SalesCustomers.cs') -Raw
if ($customerApplicationSource -notmatch 'await repository.GetAllAsync' -or $customerApplicationSource -notmatch 'if \(!canReadCustomers\(actor\)\)') {
    throw 'Customer asynchronous service lost post-read authorization.'
}
Write-Output 'PASS customer pinned read-only root, post-read authorization and retained list selection/editor context.'

$supplierRoot = [regex]::Match($medicineComposition, '(?s)internal static ManufacturerReadSession CreateManufacturerReadSession\(\).*?(?=internal static SalesCustomerReadSession)').Value
if ($supplierRoot -notmatch 'CurrentSessionRevision == revision' -or $supplierRoot -notmatch 'employee, SessionContext.CurrentEmployeeId' -or
    $supplierRoot -notmatch 'new System.Data.SqlClient.SqlConnection\(connectionString\)' -or $supplierRoot -notmatch 'actor => false' -or
    $supplierScreen -notmatch 'CreateManufacturerReadSession\(\)' -or $supplierScreen -notmatch 'listContextIsCurrent = readContextIsCurrent;' -or
    $supplierScreen -notmatch 'SessionContext.CurrentSessionRevision == revision' -or $supplierScreen -notmatch 'profileIsCurrent\(\)' -or
    $supplierScreen -notmatch 'CompanyGV.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;' -or
    $supplierScreen -notmatch 'CompanyGV.KeyDown \+= ModernTableKeyDown;' -or
    $supplierScreen -notmatch 'if \(!operationContextIsCurrent\(\)\) \{ InvalidateCachedContext\(\); return; \}') {
    throw 'Supplier pinned async root, cached list/editor or post-confirmation session guard is missing.'
}
Write-Output 'PASS supplier pinned async read-only root and retained list/editor/confirmation login context.'

$medicineRoot=[regex]::Match($medicineComposition,'(?s)internal static MedicineSession CreateMedicineSession\(.*?(?=internal static ManufacturerReadSession)').Value
if ($medicineRoot -notmatch 'CurrentSessionRevision==revision' -or $medicineRoot -notmatch 'employee,SessionContext.CurrentEmployeeId' -or
    $medicineRoot -notmatch 'new System.Data.SqlClient.SqlConnection\(connectionString\)' -or
    $medicineScreen -notmatch 'listContextIsCurrent=ReferenceEquals\(grid,MedicineGV\)\?readContextIsCurrent:null;' -or
    $medicineScreen -notmatch 'if \(!operationContextIsCurrent\(\)\) \{ InvalidateCachedContext\(\); return; \}' -or
    $medicineScreen -notmatch 'MedicineGV.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;' -or
    $medicineService -notmatch 'ReadAuthorizedMedicineAsync' -or $medicineService -notmatch 'RequireActor\(actor, manage\);') {
    throw 'Medicine pinned session root, directory/editor/barcode/confirmation/clipboard or post-read authorization guards missing.'
}
Write-Output 'PASS medicine pinned session and retained directory/editor/barcode/write-completion/clipboard authorization boundaries.'

$accountingLookupSource=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\AccountingLookupRead.cs') -Raw
$journalHistorySource=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\JournalHistoryRead.cs') -Raw
foreach($source in @($accountingLookupSource,$journalHistorySource)) {
    if($source -match 'Task.Run' -or $source -notmatch 'CaptureActor' -or $source -notmatch 'cancellationToken.ThrowIfCancellationRequested\(\)' -or $source -notmatch 'Authorize\(current\)') {
        throw 'Accounting read services require actor snapshots, true async storage and post-read cancellation/authorization.'
    }
}
foreach($path in @('src\PharmacyManagement.Infrastructure.SqlServer\SqlAccountingLookupReadRepository.cs','src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalHistoryReadRepository.cs')) {
    $source=Get-Content -LiteralPath (Join-Path $root $path) -Raw
    foreach($pattern in @('OpenAsync\(cancellationToken\)','ExecuteReaderAsync\(cancellationToken\)','ReadAsync\(cancellationToken\)')) {if($source -notmatch $pattern){throw 'Accounting repository lost cancellable SQL async IO.'}}
}
$accountingRoot=[regex]::Match($medicineComposition,'(?s)internal static AccountingReadSession CreateAccountingReadSession\(\).*?(?=internal static MedicineSession)').Value
if($accountingRoot -notmatch 'CurrentSessionRevision==revision' -or $accountingRoot -notmatch 'employee,SessionContext.CurrentEmployeeId' -or
    [regex]::Matches($accountingRoot,'new System.Data.SqlClient.SqlConnection\(connectionString\)').Count -ne 2 -or
    $quickRead -notmatch 'CreateAccountingReadSession\(\)') {throw 'Accounting read root must pin login/employee/profile and preserve lookup/history policy boundaries.'}
Write-Output 'PASS accounting lookup/history true async IO, captured actor/cancellation and pinned root policies.'

$accountingDesktop=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingLookupDesktopService.cs') -Raw
if([regex]::Matches($accountingDesktop,'AcceptCurrent\(').Count -ne 5 -or $accountingDesktop -notmatch 'catch\{result\?\.Dispose\(\);throw;\}' -or
    [regex]::Matches($quickRead,'AccountingLookupDesktopService.AcceptCurrent\(').Count -ne 2) {
    throw 'Accounting desktop projection must revalidate context on delivery and dispose rejected DataTables.'
}
Write-Output 'PASS accounting desktop sync/async projection delivery rechecks session and disposes rejected data.'

$chartSessionBody=[regex]::Match($accountingSource,'(?s)public partial class ChartOfAccountsForm.*?(?=public partial class JournalEntriesForm)').Value
$accountWriterRoot=[regex]::Match($medicineComposition,'(?s)internal static AccountWriteSession CreateAccountWriteSession\(\).*?(?=internal static AccountingReadSession)').Value
$accountWriterDesktop=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountWritingDesktopService.cs') -Raw
if($chartSessionBody -notmatch 'chartContextCurrent,chartEditorCurrent' -or
    $chartSessionBody -notmatch 'if\(!EnsureChartContext\(\)\)return;' -or
    $chartSessionBody -notmatch 'if\(!confirmed\|\|!EnsureChartContext\(true\)\)return;' -or
    [regex]::Matches($chartSessionBody,'if\(!IsChartContextCurrent\(writeContext\)\)').Count -ne 4 -or
    $chartSessionBody -notmatch 'LoadAccountsAsync\(false,cancellation.Token\)' -or
    $chartSessionBody -notmatch 'Disposed \+= .*CancelChartRead' -or
    $accountWriterRoot -notmatch 'CurrentSessionRevision==revision' -or
    $accountWriterRoot -notmatch 'employee,SessionContext.CurrentEmployeeId' -or
    $accountWriterRoot -notmatch 'new System.Data.SqlClient.SqlConnection\(connectionString\)' -or
    [regex]::Matches($accountWriterDesktop,'CreateAccountWriteSession\(\)').Count -ne 4 -or
    $accountWriterDesktop -match 'ApplicationServices.AccountWriting\.') {
    throw 'Chart retained context, cancellation, confirmation/completed-write guard or pinned account writer boundary missing.'
}
Write-Output 'PASS chart retained login context, cancellable reads and pinned account write boundary.'

$quickSessionBody=[regex]::Match($accountingSource,'(?s)public partial class JournalEntriesQuickForm.*?(?=public partial class OpeningEntriesForm)').Value
$journalPostingRoot=[regex]::Match($medicineComposition,'(?s)internal static JournalPostSession CreateJournalPostSession\(\).*?(?=internal static AccountWriteSession)').Value
$quickLookupSource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalEntryLookupService.cs') -Raw
$quickLookupBody=[regex]::Match($quickLookupSource,'(?s)internal static async .*?LoadQuickAccountsAsync\(.*?(?=internal static JournalEntryLookupSnapshot Load)').Value
if($quickSessionBody -match 'Task.Run|JournalEntryLookupService.Load\(' -or
    $quickSessionBody -notmatch 'LoadQuickAccountsAsync\(cancellation.Token\)' -or
    $quickLookupBody -notmatch 'CreateJournalEditorReadSession\(\)' -or
    $quickLookupBody -notmatch 'session.Service.LoadAsync\(session.Actor,token\)' -or
    $quickLookupBody -notmatch 'if\(!session.IsCurrent\)' -or
    $quickLookupBody -notmatch 'ExtendedProperties\["SystemCurrencyCode"\]=snapshot.SystemCurrency.Code' -or
    $quickSessionBody -notmatch 'LoadAsync\(request,cancellation.Token\)' -or
    $quickSessionBody -notmatch 'Grid.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable' -or
    $quickSessionBody -notmatch 'if\(!IsQuickCurrent\(writeContext\)\)\{InvalidateQuickContext\(\);return acceptedId;\}' -or
    $journalPostingRoot -notmatch 'CurrentSessionRevision==revision' -or
    $journalPostingRoot -notmatch 'employee,SessionContext.CurrentEmployeeId' -or
    $journalPostingRoot -notmatch 'new System.Data.SqlClient.SqlConnection\(connectionString\)' -or
    [regex]::Matches($desktopPosting,'CreateJournalPostSession\(\)').Count -ne 2) {
    throw 'Quick journal lookup/history/editor/copy/cancellation or pinned journal posting boundary missing.'
}
Write-Output 'PASS quick journal cancellable reads, retained editor/history/clipboard context and pinned journal posting root.'

# Full/opening journal retains draft/options and saved-print context separately from service acceptance.
if($mainJournal -notmatch 'journalOptionsReady&&JournalContextCurrent\(journalEditorCurrent\)' -or
   $mainJournal -notmatch 'var writeContext=journalEditorCurrent;CancelJournalLookup\(\)' -or
   $mainJournal -notmatch 'if\(!JournalContextCurrent\(writeContext\)\)\{InvalidateJournalContext\(\);return id;\}' -or
   $mainJournal -notmatch 'linesGrid.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable' -or
   $mainJournal -notmatch 'generation!=lookupLoadGeneration\|\|cancellation.IsCancellationRequested'){
    throw 'Full/opening journal cached editor, lookup lifetime or accepted write/print context boundary missing.'
}
Write-Output 'PASS full/opening journal retained draft/options, guarded copy, cancellable delivery and accepted write/print context.'

$editorLookupDesktop=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\JournalEntryLookupService.cs'))
$editorLookupSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalEditorLookupReadRepository.cs'))
if($mainJournal -match 'Task.Run|JournalEntryLookupService.Load\(\)' -or
   $mainJournal -notmatch 'JournalEntryLookupService.LoadAsync\(cancellation.Token\)' -or
   $mainJournal -notmatch 'foreach\(string center in snapshot.CostCenters\)' -or
   $editorLookupDesktop -notmatch 'CreateJournalEditorReadSession\(\)' -or
   $editorLookupSql -notmatch 'NextResultAsync\(cancellationToken\)' -or $editorLookupSql -match 'Task.Run'){
    throw 'Full journal options must use pinned typed cancellable SQL reads and active cost centers without Task.Run or invented currency.'
}
Write-Output 'PASS full/opening journal typed async SQL lookup, active cost centers and explicit missing currency failure.'

$savedJournalApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\JournalDocumentRead.cs'))
$savedJournalSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalDocumentReadRepository.cs'))
if($savedJournalApp -match 'Task.Run' -or $savedJournalApp -notmatch 'repository.LoadAsync\(request,cancellationToken\)' -or
   $savedJournalSql -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
   $journalDocumentAdapter -notmatch 'if\(!session.IsCurrent\)' -or
   $mainJournal -notmatch 'JournalDocumentDesktopService.ReadAsync\(id,printToken\)' -or
   $mainJournal -notmatch '!journalPrintPending' -or $mainJournal -notmatch 'CancelJournalPrint\(\)'){
    throw 'Saved journal read must be pinned/cancellable SQL and closing after commit must cancel print while preserving accepted id.'
}
Write-Output 'PASS saved journal pinned async SQL/cancellation and post-commit print lifetime boundary.'

$voucherHistoryApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\VoucherHistoryRead.cs'))
$voucherHistorySql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlVoucherHistoryReadRepository.cs'))
$voucherWorkspace=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\ReceiptVoucherWorkspaceService.cs'))
if($voucherHistoryApp -match 'Task.Run' -or $voucherHistoryApp -notmatch 'repository.LoadAsync\(request,cancellationToken\)' -or
   $voucherHistorySql -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
   $voucherWorkspace -match 'ApplicationServices.VoucherHistory' -or
   $voucherWorkspace -notmatch 'CreateVoucherHistoryReadSession' -or $voucherWorkspace -notmatch 'if\(!session.IsCurrent\)'){
    throw 'Voucher history must use pinned/cancellable SQL with post-read context authorization.'
}

$legacyVoucherStart=$accountingSource.IndexOf('public partial class VoucherForm :')
$legacyVoucherEnd=$accountingSource.IndexOf('public partial class ReceiptVoucherForm :',$legacyVoucherStart)
$legacyVoucher=$accountingSource.Substring($legacyVoucherStart,$legacyVoucherEnd-$legacyVoucherStart)
$voucherWriter=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\VoucherPostingDesktopService.cs'))
if($legacyVoucher -match 'Task.Run|JournalEntryLookupService.Load\(' -or
   $legacyVoucher -notmatch 'CreateJournalEditorReadSession' -or $legacyVoucher -notmatch 'session.Service.LoadAsync\(session.Actor,token\)' -or $legacyVoucher -notmatch 'null,voucherSystemCurrencyCode\)' -or
   $legacyVoucher -notmatch 'LoadHistoryAsync\(voucherType,cancellation.Token\)' -or
   $legacyVoucher -notmatch 'SelectedVoucherHistoryText' -or $legacyVoucher -notmatch 'CancelVoucherReads' -or
   $voucherWriter -match 'ApplicationServices.VoucherPosting' -or $voucherWriter -notmatch 'CreateVoucherPostSession'){
    throw 'Legacy voucher must protect retained context, cancel SQL reads and use the pinned voucher posting boundary.'
}
Write-Output 'PASS legacy voucher retained context, async accounts/history cancellation, guarded copy and pinned voucher writer.'

# Detailed voucher initialization must use cancellable typed readers, never invented currencies.
$receiptWorkspace=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\ReceiptVoucherWorkspaceService.cs'))
$receiptStart=$accountingSource.IndexOf('public partial class ReceiptVoucherForm :')
$receiptEnd=$accountingSource.IndexOf('public partial class GeneralLedgerForm :',$receiptStart)
$receiptBody=$accountingSource.Substring($receiptStart,$receiptEnd-$receiptStart)
$receiptInitStart=$receiptBody.IndexOf('internal async System.Threading.Tasks.Task<bool> LoadReceiptInitialAsync(')
$receiptInitEnd=$receiptBody.IndexOf('private void ApplyAccountOptions(',$receiptInitStart)
if($receiptInitStart -lt 0 -or $receiptInitEnd -le $receiptInitStart){throw 'Detailed voucher initial async boundary is missing.'}
$receiptInitial=$receiptBody.Substring($receiptInitStart,$receiptInitEnd-$receiptInitStart)
if($receiptInitial -match 'Task.Run|Items.Add\("RS"' -or
   $receiptInitial -notmatch 'LoadInitialAsync\(voucherType, token\)' -or
   $receiptInitial -notmatch 'token.IsCancellationRequested' -or
   $receiptInitial -notmatch 'SetReceiptOptionsEnabled\(false\)' -or
   $receiptBody -notmatch 'CancelReceiptInitialRead' -or
   $receiptWorkspace -match 'Task.Run|CurrencyWorkspaceService|LoadAccounts\(' -or
   $receiptWorkspace -notmatch 'CreateJournalEditorReadSession' -or
   $receiptWorkspace -notmatch 'history.Service.LoadAsync\(HistoryRequest\(voucherType\), history.Actor, cancellationToken\)'){
    throw 'Detailed voucher options must be typed, session-pinned, cancellable and fail closed without a fabricated system currency.'
}
Write-Output 'PASS detailed voucher initial typed async options, cancellation and strict currency boundary.'

$balanceApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\AccountBalanceRead.cs'))
$balanceSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlAccountBalanceReadRepository.cs'))
if($balanceApp -match 'Task.Run' -or $balanceApp -notmatch 'Authorize\(captured\)' -or $balanceApp -notmatch 'repository.LoadAsync\(request, cancellationToken\)' -or
   $balanceSql -notmatch 'ExecuteScalarAsync\(cancellationToken\)' -or $balanceBody -notmatch 'CreateAccountBalanceReadSession' -or $balanceBody -notmatch 'token.IsCancellationRequested' -or
   $composition -match 'readonly IAccountBalanceReadService AccountBalances'){
    throw 'Voucher balance must use actor-authorized true SQL async reads with cancellation and a pinned root.'
}
Write-Output 'PASS voucher balance actor authorization, true SQL async cancellation and pinned root boundary.'

$voucherDocumentApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\VoucherDocumentRead.cs'))
$voucherDocumentSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlVoucherDocumentReadRepository.cs'))
if($voucherDocumentApp -match 'Task.Run' -or $voucherDocumentApp -notmatch 'repository.LoadAsync\(request, cancellationToken\)' -or
   $voucherDocumentSql -notmatch 'ExecuteReaderAsync' -or $voucherDocumentSql -notmatch 'ReadAsync\(cancellationToken\)' -or
   $printBody -notmatch 'CreateVoucherDocumentReadSession' -or $printBody -notmatch 'token.ThrowIfCancellationRequested' -or
   $composition -match 'readonly IVoucherDocumentReadService VoucherDocuments'){
    throw 'Saved voucher document must use cancellable actor-authorized true SQL async and a pinned reader.'
}
Write-Output 'PASS saved voucher document pinned async SQL, actor authorization and UI lifetime cancellation boundary.'

# Compatibility wrapper may supply the draft currency explicitly; never infer it from live settings.
$voucherCompatibility=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\AccountingService.cs'))
if($voucherCompatibility -notmatch 'costCenterId, expectedSystemCurrencyCode\)' -or
   $voucherWriter -notmatch 'cash,center,expectedSystemCurrencyCode\)' -or
   $voucherWriter -match 'GetSystemCurrency|CurrencySettingsTb'){
    throw 'Voucher compatibility currency must flow unchanged to the prepared request, without live currency inference.'
}
Write-Output 'PASS voucher compatibility wrapper explicit draft currency forwarding; legacy omitted-code callers remain unguarded.'

$ledgerSource=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\AccountingForms.cs'))
$ledgerStart=$ledgerSource.IndexOf('    public partial class GeneralLedgerForm : Form')
$ledgerEnd=$ledgerSource.IndexOf('    public partial class AccountingReportsForm : Form',$ledgerStart)
if($ledgerStart -lt 0 -or $ledgerEnd -le $ledgerStart){throw 'General ledger architecture region missing.'}
$ledgerBody=$ledgerSource.Substring($ledgerStart,$ledgerEnd-$ledgerStart)
$ledgerSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlGeneralLedgerReadRepository.cs'))
$ledgerRoot=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\GeneralLedgerDesktopRead.cs'))
if($ledgerBody -match 'Task.Run|GeneralLedgerWorkspaceService|SumColumn\(|SqlConnection|SqlCommand' -or
   $ledgerBody -notmatch 'ledgerSessionFactory' -or $ledgerBody -notmatch 'CancelLedgerRead' -or
   $ledgerBody -notmatch 'UiSessionSubscription.Attach' -or $ledgerBody -notmatch 'EnsureLedgerOutputCurrent' -or
   $ledgerBody -notmatch 'result.PeriodDebit,result.PeriodCredit,result.EndingBalance' -or
   $ledgerSql -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or $ledgerSql -notmatch 'ReadAsync\(cancellationToken\)' -or
   $ledgerSql -match 'Task.Run' -or $ledgerRoot -notmatch 'cancellation.ThrowIfCancellationRequested' -or
   $composition -notmatch 'CreateGeneralLedgerReadSession'){
    throw 'General ledger must use typed cancellable pinned reads and service-owned summary, with stale output guards; no desktop SQL/Task.Run/sums.'
}
Write-Output 'PASS general ledger typed async SQL, service-owned summary, session/filter cancellation and output lifetime boundary.'
# The reports screen migration is separate; guard the new read composition now.
$reportsApplication=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\AccountingReportsRead.cs'))
$reportsSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlAccountingReportsReadRepository.cs'))
$reportsSession=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\AccountingReportsDesktopRead.cs'))
$reportsFactoryStart=$composition.IndexOf('        internal static AccountingReportsReadSession CreateAccountingReportsReadSession()')
$reportsFactoryEnd=$composition.IndexOf('        internal static GeneralLedgerReadSession',$reportsFactoryStart)
if($reportsFactoryStart -lt 0 -or $reportsFactoryEnd -le $reportsFactoryStart){throw 'Accounting reports captured factory missing.'}
$reportsFactory=$composition.Substring($reportsFactoryStart,$reportsFactoryEnd-$reportsFactoryStart)
if($reportsApplication -match 'System.Data|Windows.Forms|SqlConnection|SqlCommand|ConfigurationManager' -or
   $reportsSql -match 'Task.Run|Database.ConnectionString|Windows.Forms' -or
   $reportsSql -notmatch 'OpenAsync\(cancellationToken\)' -or $reportsSql -notmatch 'ExecuteReaderAsync\(cancellationToken\)' -or
   $reportsSql -notmatch 'ReadAsync\(cancellationToken\)' -or $reportsSession -notmatch 'EnsureContext\(\)' -or
   $reportsSession -notmatch 'cancellation.ThrowIfCancellationRequested' -or
   $reportsFactory -notmatch 'CurrentSessionRevision' -or $reportsFactory -notmatch 'ViewAccountingReports' -or
   $reportsFactory -notmatch 'SqlConnection\(connectionString\)' -or $reportsFactory -notmatch 'AccountingService.InventoryAccount'){
    throw 'Accounting reports read service/repository/session must preserve captured authority and true cancellable SQL boundaries.'
}
Write-Output 'PASS accounting reports captured read factory/session and async SQL boundaries; screen migration remains separate.'

$homeAlertReads=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\HomeOperationalAlertReads.cs') -Raw
$homeAlertFactoryStart=$composition.IndexOf('internal static HomeOperationalAlertReadSession CreateHomeOperationalAlertReadSession()')
$homeAlertFactoryEnd=$composition.IndexOf('internal static InventoryAlertReadSession CreateInventoryAlertReadSession()', $homeAlertFactoryStart)
if($homeAlertFactoryStart -lt 0 -or $homeAlertFactoryEnd -le $homeAlertFactoryStart){throw 'Home alert factory missing.'}
$homeAlertFactory=$composition.Substring($homeAlertFactoryStart,$homeAlertFactoryEnd-$homeAlertFactoryStart)
if($homeAlertFactory -notmatch 'AppPermission.SystemMonitor' -or
   $homeAlertFactory -notmatch 'CreateInventoryAlertReadSession' -or
   $homeAlertReads -match 'SqlConnection|SqlCommand|Task.Run|MessageBox|System.Windows.Forms' -or
   $homeAlertReads -notmatch 'inventory.LoadAsync\(10, 30, cancellation\)' -or
   $homeAlertReads -notmatch 'source.Copy\(\)' -or $homeAlertReads -notmatch 'ThrowIfCancellationRequested'){
    throw 'Home alerts must reuse the captured inventory service with SystemMonitor authority, cancellable 10/30 policy and isolated mapping.'
}
Write-Output 'PASS home alert read foundation: SystemMonitor captured inventory session, shared pure mapping, cancellation and stable defaults; desktop migration remains open.'

$homeDesktopAlerts=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\HomeAlertDesktopReads.cs') -Raw
$homeMain=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Home.cs') -Raw
if($homeMain -match 'LoadOperationalAlerts\(' -or $homeDesktopAlerts -match 'SqlCommand|SqlConnection|Task.Run' -or
   $homeMain -notmatch 'UiSessionSubscription.Attach\(this, InvalidateHomeAlertReads\)' -or
   $homeDesktopAlerts -notmatch 'generation != homeAlertGeneration' -or
   $homeDesktopAlerts -notmatch 'generation != homeDetailGeneration' -or
   $homeDesktopAlerts -notmatch 'activeHomeAlertDialog.Close\(\)' -or
   $homeDesktopAlerts -notmatch 'session.LoadAsync\(cancellation.Token\)' -or
   $homeDesktopAlerts -notmatch 'finally' -or $homeMain -notmatch 'Disposed\s*\+=\s*\(sender, args\)\s*=>\s*\{\s*CancelHomeAlertReads\(\);'){
    throw 'Home alert desktop must retain async reads, cancellation, generations, session invalidation and dialog lifetime protection.'
}
Write-Output 'PASS home alert async desktop: no synchronous alert read, session invalidation, both request generations, owned dialog close and disposal cancellation.'

$homeAlertCardStart=$homeMain.IndexOf('private Control CreateAlertCard(')
$homeAlertCardEnd=$homeMain.IndexOf('internal Form CreateAlertDetailsDialog(', $homeAlertCardStart)
if($homeAlertCardStart -lt 0 -or $homeAlertCardEnd -le $homeAlertCardStart){throw 'Home alert cards missing.'}
$homeAlertCard=$homeMain.Substring($homeAlertCardStart,$homeAlertCardEnd-$homeAlertCardStart)
if($homeAlertCard -notmatch 'new AccessibleAntdActionCard' -or $homeAlertCard -notmatch 'CardRadius' -or
   $homeAlertCard -notmatch 'Shadow = 0' -or $homeAlertCard -notmatch 'MeasureText' -or
   $homeAlertCard -notmatch 'WrapContents = true' -or $homeAlertCard -notmatch 'RightToLeft.No'){
    throw 'Home alerts must use accessible AntdUI cards with shared radius, no shadow, measured text and single RTL flow.'
}
Write-Output 'PASS home AntdUI alert cards: shared accessible component, measured responsive layout, no shadows and language flow.'

$voucherShell = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\VoucherForm.Designer.cs') -Raw
$accountingShell = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingDataForm.Designer.cs') -Raw
if ($accountingShell -notmatch 'private AntdUI.Button CloseButton' -or
    $accountingShell -notmatch 'DesktopComponents.Action\("accountingDataCloseButton"' -or
    $accountingShell -notmatch 'Dock = DockStyle.Bottom' -or
    $accountingFormsText -notmatch 'AttachAccountingCloseAction\(quickActions\)' -or
    $accountingFormsText -notmatch 'ApplyAccountingClosePresentation\(voucherLeftToRight\)' -or
    $voucherShell -notmatch 'AttachAccountingCloseAction\(actions\)' -or
    $voucherShell -notmatch 'ColumnCount = 4' -or
    $accountingShell -match 'CloseButton.DialogResult|CancelButton = CloseButton') {
    throw 'Shared accounting close must remain an AntdUI bottom action and preserve existing financial closing guards.'
}
Write-Output 'PASS shared accounting AntdUI close action, bottom placement and existing closing semantics.'
$inventoryShared = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryDataForm.cs') -Raw
$inventoryTheme = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\UiTheme.cs') -Raw
$inventoryMetricStart = $inventoryShared.IndexOf('private Control CreateMetricCard(')
$inventoryMetricEnd = $inventoryShared.IndexOf('private void ApplyInventoryDataVisualTheme()', $inventoryMetricStart)
if ($inventoryMetricStart -lt 0 -or $inventoryMetricEnd -le $inventoryMetricStart) { throw 'Inventory metric surface missing.' }
$inventoryMetricBody = $inventoryShared.Substring($inventoryMetricStart, $inventoryMetricEnd - $inventoryMetricStart)
if ($inventoryMetricBody -notmatch 'DesktopComponents.Card' -or $inventoryMetricBody -notmatch 'CardRadius' -or
    $inventoryMetricBody -notmatch 'Shadow = 0' -or $inventoryMetricBody -notmatch 'card.Tag = title' -or
    $inventoryMetricBody -notmatch 'ContentAlignment.MiddleCenter' -or $inventoryMetricBody -notmatch 'value.RightToLeft = RightToLeft.No' -or
    $inventoryShared -notmatch 'panel == SummaryPanel.*AutoScrollMinSize' -or
    $inventoryTheme -notmatch 'inventorySurface.RestoreInventorySharedAppearance\(\)' -or
    $inventoryShared -notmatch 'ApplyInventoryPresentation\(leftToRight\);\s*ApplyQuantityColumnFormats\(\);') {
    throw 'Inventory metrics must retain AntdUI shared cards, original captions, centered numbers, complete scroll extent and theme restoration.'
}
Write-Output 'PASS inventory AntdUI metrics, original captions, centered values, complete scroll extent and theme restoration.'
$inventoryDesigner = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryDataForm.Designer.cs') -Raw
$inventoryOperationDesigner = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryOperationForm.Designer.cs') -Raw
if ($inventoryShared -match 'protected Button AddActionButton|protected void AddGridOutputActions' -or
    $inventoryDesigner -match 'inventoryDataCloseButton' -or
    $inventoryShared -notmatch 'AddOutlinedFooterAction\(footer, "inventoryDataCloseButton", "إغلاق", AppIcon.Close' -or
    $inventoryShared -notmatch '\(sender, args\) => Close\(\)' -or
    $inventoryShared -notmatch 'footer.ControlAdded' -or $inventoryShared -notmatch 'footer.ControlRemoved' -or
    $inventoryShared -notmatch 'LayoutInventoryActionFooter' -or $inventoryShared -notmatch 'TextRenderer.MeasureText\(action.Text, action.Font\)' -or
    $inventoryOperationDesigner -notmatch 'AddOutlinedActionFooter\("inventoryOperationFooter"\)' -or
    $inventoryOperationDesigner -match 'Controls.Add\(actionFooter\)') {
    throw 'Inventory actions must use one measured AntdUI footer and normal guarded form closure, without dead native actions.'
}
Write-Output 'PASS shared inventory AntdUI action footer, measured captions and normal guarded closure.'
$desktopCheckbox = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\DesktopDesign.cs') -Raw
$inventoryCheckbox = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryForms.cs') -Raw
$checkboxStart = $desktopCheckbox.IndexOf('internal sealed class AccessibleAntdCheckbox')
$checkboxEnd = $desktopCheckbox.IndexOf('internal sealed class AccessibleAntdButton', $checkboxStart)
if ($checkboxStart -lt 0 -or $checkboxEnd -le $checkboxStart) { throw 'Shared accessible AntdUI checkbox missing.' }
$checkboxBody = $desktopCheckbox.Substring($checkboxStart, $checkboxEnd - $checkboxStart)
if ($inventoryCheckbox -notmatch 'DesktopComponents.Checkbox\(' -or
    $checkboxBody -notmatch 'AccessibleRole.CheckButton' -or
    $checkboxBody -notmatch 'AccessibleStates.Checked' -or
    $checkboxBody -notmatch 'AccessibilityNotifyClients' -or
    $checkboxBody -notmatch 'DoDefaultAction' -or
    $checkboxBody -notmatch 'owner.BeginInvoke' -or
    $checkboxBody -notmatch '!Enabled \|\| !Visible' -or
    $checkboxBody -notmatch 'base.OnKeyUp\(e\)' -or
    $checkboxBody -notmatch 'base.OnClick\(e\)' -or
    $checkboxBody -notmatch 'DesktopDesignTokens.ActionRadius' -or
    $desktopCheckbox -notmatch 'checkbox is AccessibleAntdCheckbox \? Color.Transparent') {
    throw 'Inventory checkbox must retain shared AntdUI semantics, access state/action, guarded lifetime, library toggling and theme integration.'
}
Write-Output 'PASS inventory accessible AntdUI checkbox: shared component, guarded activation, checked state and theme integration.'
$ledgerCheckboxDesigner = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\GeneralLedgerForm.Designer.cs') -Raw
$checkboxDirection = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\UiDirection.cs') -Raw
if ($ledgerBody -notmatch 'DesktopComponents.Checkbox\("generalLedgerIncludeOpening"' -or
    $ledgerBody -notmatch 'DesktopComponents.Checkbox\("generalLedgerIncludePrevious"' -or
    $ledgerBody -notmatch 'includeOpening.CheckedChanged.*InvalidateLedgerFilters' -or
    $ledgerBody -notmatch 'includePrevious.CheckedChanged.*InvalidateLedgerFilters' -or
    $ledgerCheckboxDesigner -notmatch 'includeOpening.Height = includePrevious.Height = DesktopDesignTokens.InputHeight' -or
    $ledgerCheckboxDesigner -notmatch 'includeOpening.Padding = includePrevious.Padding = Padding.Empty' -or
    $checkboxDirection -notmatch 'AntdUI.Checkbox antdCheckbox' -or
    $checkboxDirection -notmatch 'antdCheckbox.TextAlign = leftToRight \? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight') {
    throw 'Ledger checkboxes must keep shared accessibility, field height, existing cancellation callbacks and the central RTL/LTR caption policy.'
}
Write-Output 'PASS ledger shared accessible AntdUI checkbox, field height, filter cancellation and central caption direction.'

# Home navigation and logout use accessible AntdUI controls with local semantic SVGs.
$homeModuleUi = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Home.cs') -Raw
$homeModuleDesigner = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Home.Designer.cs') -Raw
if ($homeModuleUi -match 'CreateHomeTileImage|DrawHomeOutlinedIcon|DrawDashboardIcon|FindGeneratedTileIcon|HomeTile_KeyDown' -or
    $homeModuleUi -match 'new\s+Sql(Connection|Command|Transaction)\b') {
    throw 'Home modules regained legacy bitmap rendering, duplicate key dispatch or direct SQL.'
}
foreach ($module in @('butSetings','DashBoard','pictureBox6','pictureBox5','pictureBox4','pictureBox3','pictureBox2','pictureBox1')) {
    if ($homeModuleDesigner -notmatch ('private\s+AntdUI\.Button\s+' + [regex]::Escape($module) + ';')) {
        throw ('Home module is not an AntdUI button: ' + $module)
    }
}
if ($homeModuleUi -notmatch 'accountingTile = new AccessibleAntdButton' -or
    $homeModuleUi -notmatch 'inventoryTile = new AccessibleAntdButton') {
    throw 'Runtime home modules lost accessible AntdUI construction.'
}
Write-Output 'PASS home module AntdUI construction, no legacy tile bitmap/duplicate key dispatch/direct SQL.'
if ($homeModuleUi -match 'SetTileImage|pictureBox5\.(Image|SizeMode)|RenderBitmap\(AppIcon.Logout' -or
    $homeModuleUi -notmatch 'pictureBox5.IconSvg = OutlinedIcons.Svg\(AppIcon.Logout\)' -or
    $homeModuleDesigner -notmatch 'pictureBox5 = new AccessibleAntdButton\(\)' -or
    $homeModuleDesigner -notmatch 'pictureBox5.Click \+= new System.EventHandler\(this.pictureBox5_Click\)' -or
    $homeModuleUi -notmatch '(?s)private void pictureBox5_Click\(.*?RecordSecurityAudit\("Logout".*?Close\(\);') {
    throw 'Home logout regained a bitmap or lost its accessible semantic icon/audit/Close route.'
}
Write-Output 'PASS home logout AntdUI/SVG and retained audit/Close route.'
# The viewport shim belongs only to owned UI test fixtures, never production navigation.
$ownedViewport = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\OwnedTestWindowViewport.cs') -Raw
if ($ownedViewport -notmatch 'GetClientRect' -or $ownedViewport -notmatch 'GetWindowRect' -or
    $ownedViewport -notmatch '0x14' -or $ownedViewport -notmatch 'form.ShowInTaskbar' -or
    $ownedViewport -notmatch 'form.ClientSize == requested' -or $ownedViewport -notmatch 'form.Size == actualOuter' -or
    $ownedViewport -match 'form.MinimumSize\s*=') {
    throw 'Owned UI viewport must check actual native geometry and avoid activating windows.'
}
foreach ($sourceFile in Get-ChildItem -LiteralPath (Join-Path $root 'project_fo_3') -Filter '*.cs') {
    if ($sourceFile.Name -eq 'OwnedTestWindowViewport.cs' -or $sourceFile.Name -like '*SelfTests.cs') { continue }
    if ((Get-Content -LiteralPath $sourceFile.FullName -Raw) -match 'OwnedTestWindowViewport') {
        throw ('Test viewport must not enter application navigation: ' + $sourceFile.Name)
    }
}
Write-Output 'PASS owned test viewport isolation and native geometry checks.'
# Printed business values must not be rewritten by caption cleanup in individual renderers.
foreach ($name in @('Billing.cs','Purchases.cs')) {
    $body = Get-Content -LiteralPath (Join-Path $root ('project_fo_3\' + $name)) -Raw
    if ($name -eq 'Purchases.cs') { $body += Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Purchases.A4Print.cs') -Raw }
    $thermalCapture = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/Invoice.ThermalPrint.cs') -Raw
    $purchaseStart = $thermalCapture.IndexOf('    public partial class Purchases')
    if ($name -eq 'Billing.cs') { $body += $thermalCapture.Substring(0,$purchaseStart) }
    else { $body += $thermalCapture.Substring($purchaseStart) }
    if ($body -match '(Customer(Id|Name|Phone|Address)Label|Employee(Id)?Label|TimingLabel|lblPurchase(Supplier|Employee|Timing))\.Text\.Replace\(' -or
        [regex]::Matches($body, 'var printHeader = CaptureInvoicePrintHeader\(\);').Count -ne $(if ($name -eq 'Billing.cs') { 0 } else { 1 })) {
        throw ('Invoice print paths must share typed header capture without global business-text replacement: ' + $name)
    }
}
$a4Capture = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Billing.A4Print.cs') -Raw
$billingPrint = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Billing.cs') -Raw
$billingPrint += Get-Content -LiteralPath (Join-Path $root 'project_fo_3/SalesA4PrintLayout.cs') -Raw
if ([regex]::Matches($a4Capture, 'CaptureInvoicePrintHeader\(\)').Count -ne 1 -or
    $a4Capture -notmatch 'new A4InvoicePrintJob\(rows, CaptureInvoicePrintHeader\(\)' -or
    $a4Capture -notmatch 'a4PrintJob = CaptureA4PrintJob\(\);' -or
    $billingPrint -notmatch 'var printHeader = job\.Header;' -or
    $billingPrint -notmatch 'if \(a4PrintJob == null \|\| a4PrintJob\.Completed\) a4PrintJob = CaptureA4PrintJob\(\);' -or
    [regex]::Matches($billingPrint, 'printDoc\.BeginPrint \+= BeginA4Print;').Count -ne 3 -or
    $a4Capture -match '(Customer(Id|Name|Phone|Address)Label|Employee(Id)?Label|TimingLabel)\.Text\.Replace\(') {
    throw 'A4 print paths must capture the typed saved header once per print job and retain all three BeginPrint resets.'
}
$headerBoundary = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InvoicePrintHeader.cs') -Raw
if ($headerBoundary -match 'System\.Windows\.Forms|System\.Data|new\s+Sql|text\.Replace\(') {
    throw 'Invoice print header regained UI/SQL dependencies or global caption replacement.'
}
Write-Output 'PASS shared typed invoice print header and no renderer business-text replacement.'

# Accepted owned report fixtures must measure native geometry after Show, not only assign managed sizes.
foreach ($fixture in @('GeneralLedgerPresentationSelfTests.cs','GeneralLedgerReadSelfTests.cs','AccountingReportsAsyncSelfTests.cs')) {
    $body = Get-Content -LiteralPath (Join-Path $root ('project_fo_3\' + $fixture)) -Raw
    if ($body -match '\bform\.ClientSize\s*=\s*size\s*;' -or
        $body -notmatch 'OwnedTestWindowViewport.Resize\(form, size\)') {
        throw ('Owned report fixture regained a direct managed viewport assignment: ' + $fixture)
    }
}
$layoutFixtures = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ResponsiveLayoutSelfTests.cs') -Raw
$excelMinimum = [regex]::Match($layoutFixtures, '(?s)private static void VerifyExcelMinimumWindow\(.*?(?=private static )').Value
if ($excelMinimum -notmatch 'OwnedTestWindowViewport.ResizeOuter\(form, form.MinimumSize\)' -or
    $excelMinimum -match '\bform\.Size\s*=') {
    throw 'Owned Excel minimum fixture must measure the original native frame.'
}
Write-Output 'PASS native report and Excel minimum fixture regression guard.'
# Component evidence must render the verified native viewport; keep the adapter in the test boundary.
$componentChecks = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AntdComponentChecks.cs') -Raw
if ($componentChecks -match '\bform\.ClientSize\s*=' -or
    $componentChecks -notmatch 'AntdComponentViewportSelfTests.Render\(form, size, path\)') {
    throw 'Component evidence regained an unchecked managed viewport assignment.'
}
$componentViewport = Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AntdComponentViewportSelfTests.cs') -Raw
if ($componentViewport -notmatch 'OwnedTestWindowViewport.Resize\(form, size\)' -or
    $componentViewport -match 'form.MinimumSize\s*=') {
    throw 'Component evidence must verify native size without changing the original minimum.'
}
Write-Output 'PASS component evidence native viewport and retained minimum.'

# Mixed account business values retain native bidi editing; operations remain in the existing services.
$chartMixedBody = [regex]::Match($accountingSource, '(?s)public partial class ChartOfAccountsForm.*?(?=public partial class JournalEntriesForm)').Value
if ($chartMixedBody -notmatch 'private readonly TextBox txtName' -or
    $chartMixedBody -notmatch 'private readonly ComboBox parentAccount' -or
    $chartMixedBody -notmatch 'DesktopComponents.NativeTextFrame' -or
    $chartMixedBody -notmatch 'DesktopComponents.NativeSelectFrame' -or
    $chartMixedBody -match 'private readonly AntdUI.Input txtName|private readonly AntdUI.Select parentAccount') {
    throw 'Chart mixed business values must keep the accepted native editor inside the shared AntdUI frame.'
}
Write-Output 'PASS chart mixed business values use native bidi editors within AntdUI frames.'

$chartSharedSurfaceFixture = [regex]::Match($layoutFixtures, '(?s)private static void VerifyChartSharedSurfaces\(\).*?(?=private static )').Value
if ($chartSharedSurfaceFixture -notmatch 'ChartMixedTextSelfTests.Verify\(\)') { throw 'Chart mixed text regression must remain in the existing responsive quality gate.' }
Write-Output 'PASS chart mixed text regression included in shared-surface quality gate.'

$journalMixedBody = [regex]::Match($accountingSource, '(?s)public partial class JournalEntriesForm.*?(?=public partial class JournalEntriesQuickForm)').Value
if ($journalMixedBody -notmatch 'private readonly TextBox headerDescription' -or
    $journalMixedBody -notmatch 'private readonly TextBox lineDescription' -or
    $journalMixedBody -notmatch 'DesktopComponents.NativeTextFrame' -or
    $journalMixedBody -match 'private readonly AntdUI.Input (headerDescription|lineDescription)') { throw 'Journal descriptions must retain native bidi editing inside shared AntdUI shells.' }
$journalLayoutFixture = [regex]::Match($layoutFixtures, '(?s)private static void VerifyJournalEntriesLayout\(\).*?(?=private static )').Value
if ($journalLayoutFixture -notmatch 'JournalMixedTextSelfTests.Verify\(\)') { throw 'Journal description regression must remain in the existing layout gate.' }
Write-Output 'PASS journal mixed descriptions and integrated native editing regression.'

foreach ($field in @('entryNo','entryStatus')) {
    if ($journalMixedBody -notmatch ('private readonly TextBox '+$field) -or $journalMixedBody -match ('private readonly AntdUI.Input '+$field)) { throw ('Journal readonly business value lost native bidi: '+$field) }
}
foreach ($field in @('branch','costCenter')) {
    if ($journalMixedBody -notmatch ('private readonly ComboBox '+$field) -or $journalMixedBody -match ('private readonly AntdUI.Select '+$field)) { throw ('Journal business selector lost native bidi: '+$field) }
}
if ($journalLayoutFixture -notmatch 'JournalContextValueSelfTests.Verify\(\)') { throw 'Journal context value regression must remain in the existing layout gate.' }
Write-Output 'PASS journal readonly/selector context values and integrated bidi regression.'

# The guide must demonstrate the accepted Arabic fallback and verify actual editing.
$guideSource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AntdComponentsForm.cs') -Raw
if ($guideSource -notmatch 'DesktopComponents.NativeTextFrame\(editor' -or
    $guideSource -notmatch 'DesktopComponents.NativeSelectFrame\(choice' -or
    $componentChecks -notmatch 'ComponentMixedTextSelfTests.Verify\(guide, evidenceDirectory, mode\)') {
    throw 'Component guide regained the failed mixed-text editors or lost edit/undo coverage.'
}
Write-Output 'PASS component guide native bidi fallback and integrated editing coverage.'

# Shared inventory metric values are data, distinct from localizable captions.
$metricValueSource=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\InventoryDataForm.cs') -Raw
$metricValueLayout=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ResponsiveLayoutSelfTests.cs') -Raw
if ($metricValueSource -notmatch 'new BusinessValueLabel \{ Name = "inventoryMetricValue"' -or
    $metricValueLayout -notmatch 'InventoryMetricValueSelfTests.Verify\(\)') {
    throw 'Inventory metrics lost raw-value classification or its existing quality gate coverage.'
}
Write-Output 'PASS raw inventory metric values and integrated caption/value boundary coverage.'

# Recovery uses the posting preparation contract; status reads are independent of UI.
$journalPreparation=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\JournalPosting.cs') -Raw
$journalPending=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Application\JournalPendingRequest.cs') -Raw
$journalStatusSql=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalSubmissionStatusRepository.cs') -Raw
if ($journalPreparation -notmatch 'var normalized = PrepareRequest\(request\)' -or
    $journalPending -notmatch 'JournalPostService.PrepareRequest\(request\)' -or
    $journalStatusSql -notmatch 'UPDLOCK, HOLDLOCK' -or $journalStatusSql -notmatch 'ExecuteReaderAsync\(token\)' -or
    $journalStatusSql -match '\bINSERT\s+INTO|\bDELETE\s+FROM|\bUPDATE\s+dbo') {
    throw 'Journal recovery preparation/status boundary lost its invariant.'
}
Write-Output 'PASS journal immutable request preparation and cancellable read-only status foundation.'

$journalStore=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalPendingStore.cs') -Raw
$journalPersistence=Get-Content -LiteralPath (Join-Path $root 'tests\PharmacyManagement.DesktopPersistenceTests\InventoryPendingStoreProbe.cs') -Raw
$journalPersistenceProject=Get-Content -LiteralPath (Join-Path $root 'tests\PharmacyManagement.DesktopPersistenceTests\PharmacyManagement.DesktopPersistenceTests.csproj') -Raw
if($journalStore -notmatch 'DataProtectionScope.CurrentUser' -or $journalStore -notmatch 'FileShare.None' -or
    $journalStore -notmatch 'Flush\(true\)' -or $journalStore -notmatch 'DtdProcessing.Prohibit' -or
    $journalStore -match '\bSqlConnection\b|MessageBox|JournalPostingDesktopService|\.PostAsync\(' -or
    $journalPersistence -notmatch 'JournalPendingStoreSelfTests.Verify\(\)' -or
    $journalPersistence -notmatch 'RunChild\("--journal-restore-child",directory\)' -or
    $journalPersistenceProject -notmatch 'JournalPendingStore.cs'){
    throw 'Journal passive encrypted recovery lost its persistence/gate boundary.'
}
Write-Output 'PASS journal encrypted recovery store and existing persistence gate integration.'

$journalRoot=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ApplicationServices.cs') -Raw
$journalStatusQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalSubmissionStatusQaSelfTests.cs') -Raw
$journalStatusOwner=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\PermissionSelfTests.cs') -Raw
$start=$journalRoot.IndexOf('internal static JournalSubmissionStatusSession CreateJournalSubmissionStatusSession()')
$end=$journalRoot.IndexOf('internal static AccountWriteSession CreateAccountWriteSession()', $start)
if($start -lt 0 -or $end -le $start){throw 'Journal status root missing'}
$statusRoot=$journalRoot.Substring($start,$end-$start)
foreach($required in @('CurrentSessionRevision','CurrentEmployeeId','Database.ConnectionString','AppPermission.Accounting','AppOperation.PostAccountingEntry','SqlJournalSubmissionStatusRepository','InventoryPendingStore.CreateContextKey')){
    if(!$statusRoot.Contains($required)){throw ('Journal status root lost '+$required)}
}
if($journalStatusQa -notmatch 'sys.dm_exec_requests' -or $journalStatusQa -notmatch 'cancellation.Cancel\(\)' -or
    $journalStatusQa -notmatch 'reauthenticate\(\)' -or $journalStatusOwner -notmatch 'JournalSubmissionStatusQaSelfTests.Verify\('){
    throw 'Owned status SQL cancellation/session acceptance detached from existing QA gate.'
}
Write-Output 'PASS pinned journal recovery status root and owned SQL cancellation/reauthentication gate.'

$journalForms=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\AccountingForms.cs') -Raw
$journalRecovery=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalEntriesRecovery.cs') -Raw
$journalOperation=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalOperationRecovery.cs') -Raw
$journalLayout=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ResponsiveLayoutSelfTests.cs') -Raw
$start=$journalForms.IndexOf('private async System.Threading.Tasks.Task<int> SaveEntryAsync(bool printAfterSave)')
$end=$journalForms.IndexOf('private void ScheduleJournalStatusLayout()', $start)
if($start -lt 0 -or $end -le $start){throw 'Full journal save boundary missing'}
$body=$journalForms.Substring($start,$end-$start)
if($body -notmatch 'JournalPendingRequest.Create' -or $body -notmatch 'ExecuteJournalRecoveryAsync\(recovery\)' -or
    $body -match 'entrySourceType, null' -or $journalRecovery -notmatch 'native.PostAsync\(request,postSession.Actor,token\)' -or
    $journalOperation -notmatch 'store.Save\(pending\)' -or $journalOperation -notmatch 'await status\(' -or
    $journalLayout -notmatch 'new KeyValuePair<string,Action>\("VerifyJournalRecoveryEditor", JournalEntriesRecoverySelfTests.Verify\)'){
    throw 'Full/opening editor lost frozen durable request, captured writer or integrated recovery check.'
}
Write-Output 'PASS full/opening journal persistent frozen retry and editor recovery quality gate.'

$journalLiveQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalEntriesRecoveryQaSelfTests.cs') -Raw
$journalQaScript=Get-Content -LiteralPath (Join-Path $root 'scripts\Test-QaOwnedJournalPosting.ps1') -Raw
$journalQaProgram=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\Program.cs') -Raw
foreach($required in @('RequireOwnedTemporaryQa','SaveEntryAsync','CommitConcurrentOpening','sys.dm_exec_requests','AlreadyPosted','DataProtectionScope.CurrentUser','VerifyRestartChild')){
    if(!$journalLiveQa.Contains($required)){throw ('Owned editor QA lost '+$required)}
}
if($journalQaProgram -notmatch 'args.Length == 5 && args\[0\] == "--verify-qa-journal-editor-restart"' -or
    $journalQaProgram -notmatch 'args\[4\] == "--confirm-owned-qa-journal-editor-restart"' -or
    $journalQaScript -notmatch 'owned-editor-journals.xml' -or $journalQaScript -notmatch 'JournalEntryId=@Journal' -or
    $journalQaScript -notmatch 'CreatedBy=@User' -or $journalQaScript -notmatch '\$cloneBefore\.Audit\+13' -or
    $journalQaScript -notmatch '\$cloneBefore\.JournalHeaders\+6' -or $journalQaScript -notmatch '\$cloneBefore\.JournalLines\+12'){
    throw 'Owned editor/restart gate lost explicit confirmation or exact financial normalization.'
}
Write-Output 'PASS owned journal editor SQL commit/concurrency/restart gate and strict fixture normalization.'

$journalComparison=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalRequestComparison.cs') -Raw
$journalWriter=Get-Content -LiteralPath (Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlJournalPostRepository.cs') -Raw
if($journalOperation -notmatch 'new JournalSubmissionStatusRequest\(request.SourceType, request.SourceId, request\)' -or
   $journalOperation -notmatch 'ContentMatches.HasValue' -or $journalStatusSql -notmatch 'ORDER BY JournalEntryLineId' -or
   $journalComparison -match '\bINSERT\s+INTO|\bDELETE\s+FROM|\bUPDATE\s+dbo' -or
   $journalComparison -notmatch 'new SqlDateTime' -or $journalWriter -notmatch 'ExistingMatchesAsync'){
    throw 'Journal accepted identity lost saved-payload proof or introduced comparison writes.'
}
if($journalWriter -notmatch '!commitStarted && rollbackConfirmed' -or
   $journalWriter -notmatch '!\(error is JournalAcceptedRequestConflictException\)' -or
   $journalWriter -notmatch '!\(error is UnauthorizedAccessException\)' -or
   $journalLiveQa -notmatch 'catch\(UnauthorizedAccessException\)\{ownerDenied=true;\}' -or
   $journalLiveQa -notmatch 'correctable rejected draft' -or
   $journalOperation -notmatch 'rejection.SourceId != request.SourceId' -or
   $journalOperation -notmatch 'store.ReplaceRejected\(Pending, corrected\)' -or
   $journalStore -notmatch 'request.SourceId != existing.Request.SourceId' -or
   $journalStore -notmatch 'File.Replace\(temporary, recordPath, null\)' -or
   $body -notmatch 'journalRecovery.Pending.Request.SourceId' -or
   $journalLiveQa -notmatch 'request.SourceId==rejectedPending.Request.SourceId'){
    throw 'Confirmed journal rejection lost precommit rollback, same-identity atomic correction or actual QA coverage.'
}
Write-Output 'PASS journal payload proof and acknowledged precommit rollback with atomic same-GUID correction boundaries.'
$quickRecoveryQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\QuickJournalRecoveryQaSelfTests.cs') -Raw
if($quickRecoverySource -match 'SqlConnection|SqlCommand|Database\.CreateConnection|JournalPostRequest\([^;]*"Manual", null' -or
   $quickRecoverySource -notmatch 'CreateJournalSubmissionStatusSession' -or $quickRecoverySource -notmatch 'CreateJournalPostSession' -or
   $quickRecoverySource -notmatch '"pending-journals", "quick"' -or
   $quickPreparation -notmatch 'JournalPendingRequest.Create' -or $quickPreparation -notmatch 'quickRecovery.Pending.Request.SourceId' -or
   $quickRecoverySource -notmatch 'amount.ReadOnly = description.ReadOnly = !editable' -or
   $journalLayout -notmatch 'new KeyValuePair<string,Action>\("VerifyQuickJournalRecoveryEditor", QuickJournalRecoverySelfTests.Verify\)' -or
   $quickRecoveryQa -notmatch 'store.Load\(\).HasSameContent\(request\)' -or
   $quickRecoveryQa -notmatch 'SessionContext.Start\(user, "Manager", employee\)' -or
   $journalQaProgram -notmatch 'args\[0\] == "--verify-qa-quick-journal-editor-restart"' -or
   $journalQaProgram -notmatch 'args\[4\] == "--confirm-owned-qa-quick-journal-editor-restart"' -or
   $journalQaScript -notmatch '\$editorDocs.Count -ne 4'){
    throw 'Quick journal lost captured actors, encrypted identity/isolated slot, frozen editable fields, same-ID correction or owned independent recovery coverage.'
}
Write-Output 'PASS quick journal captured posting/status, encrypted isolated GUID, scrollable read-only payload and strict actual SQL restart boundaries.'
$ledgerQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\GeneralLedgerReadQaSelfTests.cs') -Raw
$statusQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\JournalSubmissionStatusQaSelfTests.cs') -Raw
if($ledgerQa -notmatch 'RequireOwnedTemporaryQa' -or $ledgerQa -notmatch 'CreateGeneralLedgerReadSession' -or
   $ledgerQa -notmatch 'sys.dm_exec_sql_text' -or $ledgerQa -notmatch 'VerifyPositiveCostCenter' -or
   $ledgerQa -notmatch 'e.SourceId=@Token AND e.CreatedBy=@User' -or
   $statusQa -notmatch 'ledgerFixture.VerifyCancellation' -or $statusQa -notmatch 'ledgerFixture.VerifyStale' -or
   $journalQaScript -notmatch 'LedgerPositiveCenterPassed' -or $journalQaScript -notmatch 'Expected exactly two observed owned ledger SQL waits'){
    throw 'Owned general ledger QA lost root SQL parity, exact token ownership, observed cancellation/stale waits or restored positive center coverage.'
}
Write-Output 'PASS owned general ledger captured root, independent raw parity, observed SQL waits and exact positive-center fixture ownership/restoration boundaries.'
$ledgerSnapshotQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\GeneralLedgerContentionQaSelfTests.cs') -Raw
if($ledgerSql -notmatch 'BeginTransaction\(IsolationLevel.Serializable\)' -or
   $ledgerSnapshotQa -notmatch 'r.statement_start_offset' -or $ledgerSnapshotQa -notmatch 'SELECT e.EntryDate' -or
   $ledgerSnapshotQa -notmatch 'WaitForWriter\(observer,writerSession,readerId\)' -or
   $ledgerSnapshotQa -notmatch 'scene.VerifyResult\(filter,acceptedOld\)' -or
   $ledgerSnapshotQa -notmatch 'changed.VerifyResult\(filter,acceptedNew\)' -or
   $ledgerSnapshotQa -notmatch 'RestoreOwnedAmounts\(token,user,openingId,periodId,amounts\)' -or
   $ledgerSnapshotQa -notmatch "e.SourceType=N'QAJournalAcceptance' AND e.SourceId=@Source AND e.CreatedBy=@User" -or
   $statusQa -notmatch 'ledgerFixture.VerifyConcurrentSnapshot' -or $journalQaScript -notmatch 'LedgerSnapshotRestorationPassed'){
    throw 'General ledger lost serializable opening/rows transaction or actual observed owned writer contention/restoration proof.'
}
Write-Output 'PASS general ledger serializable snapshot: actual second SELECT, owned writer waits on reader, old/new parity and exact source/user/id restoration boundaries.'

$ledgerPerformanceQa=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\GeneralLedgerPerformanceQaSelfTests.cs') -Raw
if($ledgerPerformanceQa -notmatch 'RequireOwnedTemporaryQa' -or $ledgerPerformanceQa -notmatch 'warm<2' -or
   $ledgerPerformanceQa -notmatch 'round<10' -or $ledgerPerformanceQa -notmatch 'ComparePair\(pair\)' -or
   $ledgerPerformanceQa -notmatch 'CreateGeneralLedgerReadSession' -or $statusQa -notmatch 'ledgerFixture.VerifyReadPerformance' -or
   $journalQaScript -notmatch 'LedgerReadPerformanceWithin15Percent' -or $journalQaScript -notmatch 'RequireLedgerPerformanceAcceptance' -or
   $journalQaScript -notmatch 'Ledger timing summary does not match its raw pairs'){
    throw 'Owned ledger performance gate lost paired warmups, actual root, exact parity, raw timing validation or independent acceptance result.'
}
Write-Output 'PASS owned ledger paired ten-round measurement, exact raw parity and independently enforced performance acceptance.'
  $ledgerIndexes=Get-Content -LiteralPath (Join-Path $root 'database\migrations\0032_ledger_read_indexes.sql') -Raw
  if($ledgerSql -notmatch 'ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW' -or
     $ledgerSql -notmatch 'ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId;' -or
     $ledgerSql -match 'OPTION \(FORCE ORDER' -or $ledgerSql -match 'OPTION \(RECOMPILE|OPTIMIZE FOR UNKNOWN' -or
     $ledgerIndexes -notmatch 'IX_JournalEntriesTb_EntryDate' -or $ledgerIndexes -notmatch 'IX_JournalEntryLinesTb_Account_JournalEntry' -or
     $ledgerIndexes -notmatch 'IX_JournalEntryLinesTb_JournalEntry_Account' -or
     $ledgerSql -notmatch 'reader\.FieldCount != 9' -or $ledgerSql -notmatch 'reader\.GetDecimal\(8\)' -or
     $ledgerSql -match 'var values = new object\[9\]' -or $ledgerSql -match 'reader\.GetValues\(' -or
     $ledgerPerformanceQa -notmatch 'orderingKeysUnique' -or $ledgerPerformanceQa -notmatch 'coldEmptyAccountPlanVerified' -or
     $ledgerPerformanceQa -notmatch 'after all ten timed pairs' -or $journalQaScript -notmatch 'LedgerColdEmptyAccountPlanPassed'){
      throw 'Ledger plan fix lost deterministic ROWS/order, the index driven read, a complete typed row without per-row boxing, separated diagnostics or actual unique-key/cold-plan proof.'
  }
  Write-Output 'PASS ledger deterministic key/frame, index driven read, default async row buffering and owned cold-cache plan proof; no isolation weakening.'
$settingsFlowDesign=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\DesktopDesign.cs') -Raw
$settingsFlowScreen=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\SettingsForm.Antd.cs') -Raw
$settingsFlowTests=Get-Content -LiteralPath (Join-Path $root 'project_fo_3\ResponsiveLayoutSelfTests.cs') -Raw
if($settingsFlowDesign -notmatch 'ThemedCardFlowPanel : AntdUI.FlowPanel' -or
   $settingsFlowDesign -notmatch 'CardViewport.PointToClient\(control.PointToScreen' -or
   $settingsFlowDesign -notmatch 'Controls.SetChildIndex\(physical\[index\]' -or
   $settingsFlowDesign -notmatch 'if \(control is AntdUI.Button \|\| control is ButtonBase\) control.KeyDown' -or
   $settingsFlowDesign -notmatch 'if \(next != null\) next.Focus' -or
   $settingsFlowDesign -notmatch 'YScroll.BackColor = UiTheme.Background' -or
   $settingsFlowScreen -notmatch 'new ThemedCardFlowPanel' -or
   $settingsFlowScreen -notmatch 'ArrangeReadingOrder\(leftToRight, columns\)' -or
   $settingsFlowTests -notmatch 'new KeyValuePair<string,Action>\(nameof\(VerifySettingsScrollNavigationOnly\)' -or
   $settingsFlowTests -notmatch 'Settings compact resize hides/losses focused final action'){
    throw 'Settings lost shared AntdUI scrolling, semantic RTL/Tab order, nested focus coordinates, visible paging focus or integrated native navigation coverage.'
}
Write-Output 'PASS settings shared AntdUI scrollbar, semantic RTL/Tab order, nested focus visibility, editing-key preservation and native viewport/navigation gate.'# Open accepted-document previews must retain editor and document authorization throughout their lifetime.
$previewAccounting=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\AccountingForms.cs'))
if($previewAccounting -notmatch 'isCurrent:CreateJournalPrintContext\(writeContext\)' -or
   $previewAccounting -notmatch 'isCurrent:CreateVoucherPrintContext\(current,requestedId,documentId.HasValue\)' -or
   $previewAccounting -notmatch 'CreateJournalPrintContext[\s\S]*?CreateJournalDocumentReadSession' -or
   $previewAccounting -notmatch 'CreateVoucherPrintContext[\s\S]*?CreateVoucherDocumentReadSession'){
    throw 'Accepted journal and voucher previews must retain pinned document/editor context until closed.'
}
Write-Output 'PASS accepted journal/voucher preview lifetime context wiring; runtime dialog/callback checks remain separate.'
# Inventory Excel output must retain the displayed result's bound reader and identity.
$inventoryOutput=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\InventoryDataForm.cs'))
$inventoryExport=[regex]::Match($inventoryOutput,'protected void ExportCurrentGrid\(\)([\s\S]*?)private DataTable CreateGridOutputSnapshot').Groups[1].Value
if($inventoryOutput -notmatch 'BindGrid\(DataTable table, Action validateContext' -or
   $inventoryOutput -notmatch 'ShowWithOptions\(this, Text, subtitle, snapshot, null, out selected,\s*leftToRight, isCurrent: outputContext\)' -or
   $inventoryOutput -notmatch 'DataTable snapshot = currentTable.Copy\(\);' -or
   $inventoryOutput -notmatch 'ReferenceEquals\(context,gridOutputContext\)' -or
   $inventoryExport -notmatch 'outputContext=CaptureGridOutputContext\(\);if\(!EnsureGridOutputContext\(outputContext\)\)return;' -or
   $inventoryExport -notmatch 'dialog.Filter = "Excel Workbook \(\*\.xlsx\)\|\*\.xlsx"' -or
   $inventoryExport -notmatch 'dialog.DefaultExt = "xlsx"' -or
   $inventoryExport -notmatch 'dialog.ShowDialog\(this\)[\s\S]*?if\(!EnsureGridOutputContext\(outputContext\)\)return;\s*using \(DataTable snapshot = CreateGridOutputSnapshot\(\)\)\s*\{\s*if\(!EnsureGridOutputContext\(outputContext\)\)return;\s*ExcelWorkbookWriter.WriteReportCompleteFile\(snapshot, Text, dialog.FileName, leftToRight\);'){
    throw 'Inventory output must retain bound result context through preview, snapshot and Excel commit.'
}
foreach($inventoryReaderPath in @('InventoryStockReportReads.cs','InventoryMovementReads.cs','InventoryOperationRecovery.cs')){
    $inventoryReader=[IO.File]::ReadAllText((Join-Path $root ('project_fo_3\'+$inventoryReaderPath)))
    if($inventoryReader -match 'BindGrid\(table\);'){throw "Inventory reader $inventoryReaderPath lost its output context."}
}
Write-Output 'PASS inventory bound reader/result preview and Excel authorization boundaries; runtime acceptance remains separate.'
# Operational builder must read through pinned authorized true async SQL and retain accepted output context.
$operationalBuilder=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\OperationalReportBuilderForm.cs'))
$operationalSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlOperationalReportRepository.cs'))
$operationalApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\OperationalReportReads.cs'))
if($operationalBuilder -match 'Task.Run|OperationalReportCatalog.Load\(' -or
   $operationalBuilder -notmatch 'session.LoadAsync\(request,token\)' -or
   $operationalBuilder -notmatch 'isCurrent:outputContext' -or
   $operationalBuilder -notmatch 'outputContext=CaptureOperationalOutputContext\(\);if\(!EnsureOperationalOutput\(outputContext\)\)return;' -or
   $operationalBuilder -notmatch 'using \(DataTable exportTable = GetVisibleTable\(\)\)' -or
   $operationalBuilder -notmatch 'Filter = "Excel Workbook \(\*\.xlsx\)\|\*\.xlsx", DefaultExt = "xlsx"' -or
   $operationalBuilder -notmatch 'dialog.ShowDialog\(this\) != DialogResult.OK\) return;\s*if\(!EnsureOperationalOutput\(outputContext\)\)return;\s*ExcelWorkbookWriter.WriteReportCompleteFile\(exportTable, Text, dialog.FileName, leftToRight\);' -or
   $operationalSql -notmatch 'OpenAsync\(cancellationToken\)' -or
   $operationalSql -notmatch 'ReadAsync\(cancellationToken\)' -or
   $operationalApp -notmatch 'Authorize\(captured\);return result'){
    throw 'Operational builder must retain captured async authorization/cancellation and output context.'
}
Write-Output 'PASS operational pinned async authorization, filter/session cancellation and preview/Excel boundary; live SQL parity is separate.'
# Approved return history/preview must use cancellable actor-authorized reads pinned to the original invoice.
$returnUi=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\CommercialSalesReturnHistoryForm.cs'))
$returnSql=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Infrastructure.SqlServer\SqlCommercialReturnReadRepository.cs'))
$returnApp=[IO.File]::ReadAllText((Join-Path $root 'src\PharmacyManagement.Application\CommercialReturnReads.cs'))
if($returnUi -match 'CommercialSalesReturnReadService.LoadHistory|LoadPrintDetails|Task.Run' -or
   $returnUi -notmatch 'session.LoadHistoryAsync' -or $returnUi -notmatch 'session.LoadDocumentAsync' -or
   $returnUi -notmatch 'BuildDocumentDetails\(document.Header,presentationLeftToRight\),context' -or
   $returnUi -notmatch 'snapshot.OriginalInvoiceId!=originalInvoiceId' -or
   $returnUi -notmatch 'invoiceConnection!=Database.ConnectionString' -or
   ([regex]::Matches($returnSql,'WHERE (?:h|line)\.ReturnId = @ReturnId AND h\.OriginalInvoiceId = @OriginalInvoiceId')).Count -ne 2 -or
   $returnSql -notmatch 'ReadAsync\(cancellationToken\)' -or
   $returnApp -notmatch 'result.OriginalInvoiceId!=request.OriginalInvoiceId'){
    throw 'Return preview lost pinned authorization, async cancellation or original-invoice scope.'
}
$returnLegacy=[IO.File]::ReadAllText((Join-Path $root 'project_fo_3\CommercialSalesReturnReadService.cs'))
$returnHistoryQueries=[regex]::Matches($returnLegacy,'command.CommandText\s*=\s*@"([\s\S]*?)";')
$returnHistoryNew=[regex]::Match($returnSql,'HistorySql=@"([\s\S]*?)";').Groups[1].Value
# Line endings do not change SQL; retain every other character in the strict parity check.
if($returnHistoryQueries.Count -ne 3 -or $returnHistoryQueries[1].Groups[1].Value.Replace("`r`n","`n") -cne $returnHistoryNew.Replace("`r`n","`n")){throw 'Return history read query drifted from original approved-invoice contract.'}
Write-Output 'PASS return pinned actor/async cancellation, original-invoice document scope, unchanged approved history SQL and preview lifetime wiring; populated SQL parity remains separate.'







$purchasePrint = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/Purchases.cs') -Raw
$purchaseCapture = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/Purchases.A4Print.cs') -Raw
$purchaseLayout = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/PurchaseA4PrintLayout.cs') -Raw
if ([regex]::Matches($purchasePrint,'printDoc\.BeginPrint \+= BeginPurchaseA4Print;').Count -ne 3 -or
    $purchaseCapture -notmatch 'purchaseA4PrintJob = CapturePurchaseA4PrintJob\(\)' -or
    $purchaseCapture -match 'new\s+Sql|Database\.|Save|Task.Run' -or
    $purchaseLayout -match 'DataGridView|SessionContext|Database\.|new\s+Sql|Properties\.' -or
    $purchaseLayout -notmatch 'source.Select\(r=>new PurchaseA4PrintRow' -or
    $purchaseLayout -notmatch 'InvoiceNoteRenderer.DrawA4Notes') {
    throw 'Purchase A4 must reset all three paths, capture UI once, deep-copy rows and use shared notes without SQL/live-control reads in renderer.'
}
Write-Output 'PASS purchase A4 frozen job/three BeginPrint hooks/row cursor/shared notes/no live UI or SQL renderer boundary.'

$thermalCapture = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/Invoice.ThermalPrint.cs') -Raw
$thermalRenderer = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/ThermalInvoicePrintLayout.cs') -Raw
if ($thermalCapture -notmatch 'var captured = CaptureA4PrintJob\(\)' -or
    $thermalCapture -notmatch 'rows, captured.Header, captured.Reference, captured.Currency' -or
    [regex]::Matches($thermalCapture,'CaptureInvoicePrintHeader\(\)').Count -ne 1 -or
    $thermalCapture -match 'Database\.|new\s+Sql|Task.Run' -or
    $thermalRenderer -match 'DataGridView|Database\.|new\s+Sql|SessionContext|Properties\.' -or
    $thermalRenderer -notmatch 'InvoiceNoteRenderer.DrawA4Notes' -or
    $thermalRenderer -notmatch 'identity\?\.Copy\(\)') {
    throw 'Receipts must capture typed header/canonical money once, own identity pixels and page shared notes without live UI/SQL.'
}
foreach ($pair in @(@('Billing.cs','ThermalPrint'),@('Purchases.cs','PurchaseThermalPrint'))) {
    $source = Get-Content -LiteralPath (Join-Path $root ('project_fo_3/'+$pair[0])) -Raw
    foreach ($prefix in @('Begin','End')) {
        if ([regex]::Matches($source,'printDoc\.'+$prefix+'Print \+= '+$prefix+$pair[1]+';').Count -ne 1) {
            throw 'Each receipt preview must reset at BeginPrint and release its job at EndPrint.'
        }
    }
}
Write-Output 'PASS thermal frozen header/totals/notes, page cursor, copied identity and BeginPrint/EndPrint lifecycle.'

$salesCancellationOperation = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/SalesInvoiceCancellationOperation.cs') -Raw
$salesCancellationService = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/SalesInvoiceCancellationService.cs') -Raw
$purchaseCancellation = Get-Content -LiteralPath (Join-Path $root 'project_fo_3/PurchaseInvoiceCancellationOperation.cs') -Raw
if ($salesCancellationOperation -notmatch 'SessionContext\.CurrentSessionRevision' -or
    $salesCancellationOperation -notmatch 'PermissionService\.CanExecute\(AppOperation\.CancelSalesInvoice\)' -or
    $salesCancellationService -match 'Database\.WriteAudit' -or
    $salesCancellationService -notmatch 'WriteAudit\(connection,\s*transaction,' -or
    $salesCancellationService -notmatch 'SessionCommitGate\.Commit\(current\)' -or
    $purchaseCancellation -notmatch 'using\s*\(SessionCommitGate\.Commit\(current\)\)\s*transaction\.Commit\(\)') {
    throw 'Sales and purchase cancellation must bind the actor to the current session, commit their audit and financial effects atomically, and hold the session gate through COMMIT.'
}
Write-Output 'PASS cancellation audit atomicity and session identity held through sales/purchase COMMIT.'
