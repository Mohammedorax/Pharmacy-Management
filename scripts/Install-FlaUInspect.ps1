[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/FlaUI/FlaUInspect/releases/latest" -Headers @{
    "User-Agent" = "PharmacyManagement-ToolInstaller"
}
$asset = $release.assets | Where-Object { $_.name -match "^FlaUInspect\..*\.zip$" } | Select-Object -First 1
if ($null -eq $asset) {
    throw "لم يوجد ملف FlaUInspect ZIP في أحدث إصدار."
}

$toolsRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".tools\FlaUInspect"))
$versionDirectory = [IO.Path]::GetFullPath((Join-Path $toolsRoot $release.tag_name))
if (-not $versionDirectory.StartsWith($toolsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "مسار تثبيت FlaUInspect خرج عن مجلد الأدوات المحلي."
}

New-Item -ItemType Directory -Path $versionDirectory -Force | Out-Null
$archivePath = Join-Path ([IO.Path]::GetTempPath()) $asset.name
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath -UseBasicParsing

$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedHash = [string]$asset.digest
if (-not [string]::IsNullOrWhiteSpace($expectedHash)) {
    $expectedHash = $expectedHash.Replace("sha256:", "").ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "فشل التحقق من SHA-256 لملف FlaUInspect."
    }
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $versionDirectory -Force
$executable = Get-ChildItem -LiteralPath $versionDirectory -Recurse -Filter "FlaUInspect*.exe" |
    Sort-Object FullName |
    Select-Object -First 1
if ($null -eq $executable) {
    throw "اكتمل التنزيل ولكن لم يوجد ملف تشغيل FlaUInspect."
}

$manifest = [ordered]@{
    name = "FlaUInspect"
    version = $release.tag_name
    installedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    executable = $executable.FullName
    sha256 = $actualHash
    source = $asset.browser_download_url
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $toolsRoot "installed.json") -Encoding UTF8
Write-Output ("FlaUInspect installed: " + $executable.FullName)
Write-Output ("SHA256=" + $actualHash)