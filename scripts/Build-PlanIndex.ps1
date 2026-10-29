#requires -Version 5.1
<#
.SYNOPSIS
    Parses the two Arabic plan files into a deterministic JSON index.

.DESCRIPTION
    Read-only over the plan markdown. Emits items (state/section/text/evidence) so the backlog
    can be queried instead of read linearly (2600+ lines). Feed the JSON to Import-PlanIndex.py
    which stores it in SQLite for the local `plan` MCP server.

.PARAMETER OutputDirectory
    Where items.json and summary.json are written. Defaults to artifacts/plan-index.

.EXAMPLE
    .\Build-PlanIndex.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\plan-index' }
if (-not (Test-Path -LiteralPath $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null }

$sources = @(
    @{ File = 'خطط تطويرية.md';              Prefix = 'PLAN';  Mode = 'checkbox' }
    @{ File = 'خطة تطوير نظام الصيدليات 2.md'; Prefix = 'PLAN2'; Mode = 'numbered' }
)

$itemPattern = [regex]::new('^\s*-\s*\[(?<state>[xX ])\]\s*(?<text>.+)$')
$numberedPattern = [regex]::new('^\s*(?<num>\d+(?:\.\d+)?)\s*\[(?<state>[xX ])\]\s*(?<text>.+)$')
$headingPattern = [regex]::new('^(?<level>#{1,4})\s+(?<title>.+)$')
$evidencePattern = [regex]::new('(docs[\\/](?:qa|audit)[\\/][^\s)\]]+|artifacts[\\/][^\s)\]]+)')

$items = New-Object System.Collections.Generic.List[object]
$missingFiles = New-Object System.Collections.Generic.List[string]

foreach ($source in $sources) {
    $path = Join-Path $repo $source.File
    if (-not (Test-Path -LiteralPath $path)) {
        $missingFiles.Add($source.File)
        continue
    }

    $lines = [System.IO.File]::ReadAllLines($path)
    $section = '(none)'
    $sectionLevel = 0
    $sequence = 0

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]

        $heading = $headingPattern.Match($line)
        if ($heading.Success) {
            $level = $heading.Groups['level'].Value.Length
            # Keep the nearest heading of any level as the domain, preferring deeper headings.
            $section = $heading.Groups['title'].Value.Trim()
            $sectionLevel = $level
            continue
        }

        $match = $itemPattern.Match($line)
        $id = $null
        if ($source.Mode -eq 'numbered') {
            $match = $numberedPattern.Match($line)
            if (-not $match.Success) { continue }
            $id = '{0}-{1}' -f $source.Prefix, $match.Groups['num'].Value
        }
        else {
            if (-not $match.Success) { continue }
            $sequence++
            $id = '{0}-{1:D4}' -f $source.Prefix, $sequence
        }

        $state = if ($match.Groups['state'].Value -eq ' ') { 'open' } else { 'done' }
        $text = $match.Groups['text'].Value.Trim()
        $evidence = @()
        foreach ($hit in $evidencePattern.Matches($text)) { $evidence += $hit.Groups[1].Value }
        $clean = $text -replace '\*\*|~~', ''

        $items.Add([pscustomobject]@{
            id            = $id
            source        = $source.File
            line          = $i + 1
            section       = $section
            section_level = $sectionLevel
            state         = $state
            text          = $clean
            evidence      = @($evidence)
            has_evidence  = [bool]($evidence.Count -gt 0)
        })
    }
}

$done = @($items | Where-Object { $_.state -eq 'done' }).Count
$open = @($items | Where-Object { $_.state -eq 'open' }).Count
$summary = [pscustomobject]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString('O')
    items        = $items.Count
    done         = $done
    open         = $open
    openWithoutEvidence = @($items | Where-Object { $_.state -eq 'open' -and -not $_.has_evidence }).Count
    byFile       = @($items | Group-Object source | ForEach-Object { [pscustomobject]@{ source = $_.Name; count = $_.Count } })
    missingFiles = @($missingFiles)
}

$itemsPath = Join-Path $OutputDirectory 'items.json'
$summaryPath = Join-Path $OutputDirectory 'summary.json'
$json = $items | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($itemsPath, $json, (New-Object System.Text.UTF8Encoding($false)))
[System.IO.File]::WriteAllText($summaryPath, ($summary | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))

if ($missingFiles.Count -gt 0) { throw ('Plan source missing: ' + ($missingFiles -join ', ')) }
if ($items.Count -eq 0) { throw 'refusing to build an empty index' }

Write-Host ("PASS plan index: items={0}; done={1}; open={2}; open-without-evidence={3}; out={4}" -f $items.Count, $done, $open, $summary.openWithoutEvidence, $OutputDirectory)
foreach ($missing in $missingFiles) { Write-Warning ("Plan file not found: {0}" -f $missing) }
