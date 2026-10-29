[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [ValidateNotNullOrEmpty()]
    [string]$DatabaseName = 'PharmacyManagementDb',

    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\project_fo_3\bin\Release\project_fo_3.exe')
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)

if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw "لم يتم العثور على التطبيق: $resolvedExecutable`nابنِ المشروع بوضع Release أولاً أو مرر -ExecutablePath لمسار المشروع التنفيذي الصحيح."
}

# التنفيذ يستدعي نفس مُنشئ المخطط الموجود في التطبيق؛ لذلك تبقى الجداول والفهارس والقيم النظامية متطابقة مع البرنامج.
& $resolvedExecutable '--initialize-core-schema' $ServerInstance $DatabaseName
if ($LASTEXITCODE -ne 0) {
    throw "فشل إنشاء/ترقية قاعدة البيانات. راجع رسالة التطبيق السابقة؛ لم يفعّل السكربت الوضع التجاري أو أي تكامل حي."
}

Write-Host "تم إنشاء القاعدة الأساسية والتحقق منها بنجاح: $DatabaseName على $ServerInstance" -ForegroundColor Green
