[CmdletBinding()]
param([ValidateSet('Release','Debug')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root "project_fo_3\bin\$Configuration"
$csc='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
foreach($path in @($csc,(Join-Path $output 'project_fo_3.exe'),(Join-Path $output 'PharmacyManagement.Application.dll'),(Join-Path $output 'PharmacyManagement.Infrastructure.SqlServer.dll'))){
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Build the main $Configuration application first; required file missing: $path"}
}
& $csc /nologo /langversion:7.3 /target:exe "/out:$output\PurchaseWriteOwnedSqlAcceptance.exe" /r:System.Data.dll /r:System.Configuration.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$output\PharmacyManagement.Application.dll" "/r:$output\PharmacyManagement.Infrastructure.SqlServer.dll" (Join-Path $root 'tests\PurchaseWriteOwnedSqlAcceptance.cs')
if($LASTEXITCODE -ne 0){throw 'Owned SQL acceptance compilation failed.'}
& $csc /nologo /langversion:7.3 /target:exe "/out:$output\PurchasePendingStoreContract.exe" /r:System.Data.dll /r:System.Security.dll /r:System.Configuration.dll "/r:$output\PharmacyManagement.Application.dll" (Join-Path $root 'tests\PurchasePendingStoreContract.cs') (Join-Path $root 'project_fo_3\PurchasePendingStore.cs')
if($LASTEXITCODE -ne 0){throw 'Pending store contract compilation failed.'}
Copy-Item -LiteralPath (Join-Path $output 'project_fo_3.exe.config') -Destination (Join-Path $output 'PurchaseWriteOwnedSqlAcceptance.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'database\migrations\0025_purchase_write_request_identity.sql') -Destination (Join-Path $output 'purchase_write_receipts_candidate.sql') -Force
Copy-Item -LiteralPath (Join-Path $root 'tests\PurchaseRecoveryTranslationKeys.txt') -Destination (Join-Path $output 'recovery-translation-keys.txt') -Force
Write-Output "Built purchase acceptance runners for $Configuration. SQL was not accessed."