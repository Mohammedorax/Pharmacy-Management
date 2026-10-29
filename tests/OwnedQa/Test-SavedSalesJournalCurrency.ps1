[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Database,[Parameter(Mandatory=$true)][string]$Owner,[switch]$ConfirmOwnedTemporaryQaWrites)
$ErrorActionPreference='Stop'
$ownerId=[guid]::Empty
if(!$ConfirmOwnedTemporaryQaWrites-or![guid]::TryParseExact($Owner,'N',[ref]$ownerId)-or$Database-cne('PharmacyUnits_'+$Owner+'_qa')){throw 'Exact owned PharmacyUnits QA and explicit temporary write confirmation required'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!(Test-Path "$root/project_fo_3/project_fo_3.csproj")){throw 'Run the permanent test from tests/OwnedQa in the source checkout'}
$stage=Join-Path $root ('artifacts/company-demo-20261008/saved-sales-journal-currency-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory $stage)
foreach($name in @('PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll')){Copy-Item -LiteralPath "$root/project_fo_3/bin/Release/$name" -Destination $stage}
Copy-Item -LiteralPath "$PSScriptRoot/SavedSalesJournalCurrencySqlProbe.cs" -Destination $stage
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Xml.dll')+@('PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{"$stage/$_"})
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /codepage:65001 /langversion:latest "/out:$stage/SavedSalesJournalCurrencySqlProbe.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/SavedSalesJournalCurrencySqlProbe.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Saved currency compilation failed'}
& "$stage/SavedSalesJournalCurrencySqlProbe.exe" $Database $Owner *> "$stage/result.log"
$code=$LASTEXITCODE;Get-Content "$stage/result.log"
$files=@(Get-ChildItem -LiteralPath $stage -File|ForEach-Object{[ordered]@{File=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
[ordered]@{Directory=$stage;Database=$Database;Owner=$Owner;ExitCode=$code;TemporarySchemaRolledBack=($code-eq0);WorkDatabaseUsed=$false;PhysicalPrint=$false;Files=$files;FullPlanComplete=$false}|ConvertTo-Json -Depth 6|Set-Content "$stage/result.json" -Encoding utf8
if($code-ne0){throw 'Saved currency matrix failed; inspect result.log'}
$stage
