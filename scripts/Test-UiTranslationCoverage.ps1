[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$projectDirectory = Join-Path $Root 'project_fo_3'
$projectPath = Join-Path $projectDirectory 'project_fo_3.csproj'
[xml]$project = [System.IO.File]::ReadAllText($projectPath)
$catalogPath = Join-Path $Root 'project_fo_3\UiLanguageCatalog.cs'
$catalog = [System.IO.File]::ReadAllText($catalogPath)
$keys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$literalKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$entryPattern = '\{\s*"(?<key>(?:\\.|[^"\\])*)"\s*,\s*"(?:\\.|[^"\\])*"\s*\}'
foreach ($entry in [regex]::Matches($catalog, $entryPattern)) {
    if (-not $literalKeys.Add($entry.Groups['key'].Value)) {
        throw ('Duplicate translation catalog key: ' + $entry.Groups['key'].Value)
    }
    $null = $keys.Add($entry.Groups['key'].Value.Trim())
}
$callPattern = '\b(?:(?:Present|Localize|JournalText|ReceiptText|ImportText|ChartText|VoucherText|LedgerText|UiLanguageCatalog\.(?:Translate|GetDisplayText))\s*\(\s*"(?<text>(?:\\.|[^"\\])*)"\s*\)|(?:UiValidationException|QaCsvImportIssue|PresentFormat|JournalFormat|ImportFormat|SetStatusFormat|SetBillingStatusFailure|SetPurchaseStatusFailure|ShowDashboardError|ShowSavedInvoiceFollowUpFailure|ShowPurchaseOutputFailure|ShowInvoiceOutputFailure)\s*\(\s*"(?<text>(?:\\.|[^"\\])*)"\s*(?:,|\)))'
$missing = New-Object 'System.Collections.Generic.List[string]'
$checked = 0
$compiled = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $project.SelectNodes("//*[local-name()='Compile' and @Include]")) {
    $include = [string]$entry.Include
    if ($include.IndexOfAny([char[]]'*?') -ge 0) { throw "Wildcard compile entries are unsupported: $include" }
    $source = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $include))
    if (-not [System.IO.File]::Exists($source)) { throw "Compiled source is missing: $include" }
    $null = $compiled.Add($source)
}
if ($compiled.Count -eq 0) { throw 'No explicit compiled sources found; translation coverage is inconclusive.' }
if (-not $compiled.Contains([System.IO.Path]::GetFullPath($catalogPath))) { throw 'Translation catalog must be explicitly compiled.' }
foreach ($source in ($compiled | Sort-Object)) {
    if ([string]::Equals($source, [System.IO.Path]::GetFullPath($catalogPath), [StringComparison]::OrdinalIgnoreCase)) { continue }
    $content = [System.IO.File]::ReadAllText($source)
    foreach ($call in [regex]::Matches($content, $callPattern)) {
        $value = $call.Groups['text'].Value.Trim()
        if ($value -notmatch '[\u0600-\u06FF]') { continue }
        $checked++
        if (-not $keys.Contains($value)) {
            $line = 1 + [regex]::Matches($content.Substring(0, $call.Index), "`n").Count
            $missing.Add("${source}:${line}: $value")
        }
    }
}
if ($missing.Count -gt 0) {
    $missing | ForEach-Object { Write-Output "MISSING: $_" }
    throw "Found $($missing.Count) direct Arabic translation calls without catalog entries."
}
if ($checked -lt 1500) { throw "Only $checked direct Arabic translation calls were inspected; the coverage pattern is too narrow." }

# A raw Arabic literal assigned to a control text bypasses the catalog and stays Arabic in
# English mode. Only preview fixtures and self-test values may keep a literal.
$fixtureLiterals = @(
    'عميل تجريبي',
    'بيانات توضيحية للمعاينة فقط',
    '2,500.00 ر.ي',
    'عبوة غير مفتوحة — Batch 500 mg',
    'مدير Admin',
    'باراسيتامول 500 mg',
    'قيمة عربية جديدة',
    'معاينة: '
    'ر.س',
    'عميل عام',
    'عام'
)
$assignmentPattern = '(?<![.\w])Text\s*=\s*"(?<text>(?:\\.|[^"\\])*)"'
$rawAssignments = New-Object 'System.Collections.Generic.List[string]'
$inspectedAssignments = 0
foreach ($source in ($compiled | Sort-Object)) {
    if ([string]::Equals($source, [System.IO.Path]::GetFullPath($catalogPath), [StringComparison]::OrdinalIgnoreCase)) { continue }
    # Self-test sources carry their own Arabic fixtures, and the Arabic direction translator
    # intentionally renders Arabic literals. Designer captions are translated when the form is applied.
    if ([System.IO.Path]::GetFileName($source) -match 'SelfTests\.cs$' -or
        [System.IO.Path]::GetFileName($source) -match '\.Designer\.cs$' -or
        [System.IO.Path]::GetFileName($source) -eq 'ArabicUi.cs') { continue }
    $content = [System.IO.File]::ReadAllText($source)
    foreach ($assignment in [regex]::Matches($content, $assignmentPattern)) {
        $value = $assignment.Groups['text'].Value
        if ($value -notmatch '[\u0600-\u06FF]') { continue }
        $allowed = $false
        foreach ($fixture in $fixtureLiterals) {
            if ($value.StartsWith($fixture, [StringComparison]::Ordinal)) { $allowed = $true; break }
        }
        if ($allowed) { continue }
        # A caption that has no catalog entry can never be translated, so it stays Arabic forever.
        if ($keys.Contains($value.Trim())) { $inspectedAssignments++; continue }
        $inspectedAssignments++
        $line = 1 + [regex]::Matches($content.Substring(0, $assignment.Index), "`n").Count
        $rawAssignments.Add("${source}:${line}: $value")
    }
}
if ($rawAssignments.Count -gt 0) {
    $rawAssignments | ForEach-Object { Write-Output "UNTRANSLATED: $_" }
    throw "Found $($rawAssignments.Count) control texts assigned a raw Arabic literal; route them through the screen translation helper."
}
if ($inspectedAssignments -lt 10) { throw "Only $inspectedAssignments raw control texts were inspected; the assignment pattern is too narrow." }
Write-Output "PASS: $checked direct Arabic translation calls have catalog entries across $($compiled.Count) explicit compiled sources (including subfolders), and $inspectedAssignments control texts carry no raw Arabic literal outside the preview fixtures. Static literal-call coverage only; dynamic text and live UI are not assessed."
