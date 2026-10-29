[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ExistingBackupPath)
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Apply-CommercialMigrations.ps1'
$arguments = @{
    ServerInstance = 'invalid-preview-host'
    DatabaseName = 'Preview_qa'
    Environment = 'Qa'
    VerifiedBackupPath = $ExistingBackupPath
    WhatIf = $true
}
# The deliberately invalid server makes any accidental SQL access fail the test.
foreach ($forceValue in @($false, $true)) {
    $output = @(& $runner @arguments -Force:$forceValue)
    if ($output -notcontains 'Offline preview only. SQL connections=0; backup and migration registry not verified.') {
        throw "Offline preview did not finish. Force=$forceValue"
    }
}
$arguments.DatabaseName = 'Production'
$rejected = $false
try { & $runner @arguments -Force | Out-Null }
catch [System.Management.Automation.ParameterBindingException] { $rejected = $true }
if (-not $rejected) { throw 'Production database name was accepted.' }
Write-Output 'PASS: WhatIf with/without Force, and production-name rejection. No SQL writes.'
# لا نعيد رمز آخر أمر خارجي في حالة الرفض المتوقعة أعلاه؛ نجاح هذا الاختبار
# يجب أن يكون قابلاً للاستهلاك من بوابة CI/QA.
exit 0
