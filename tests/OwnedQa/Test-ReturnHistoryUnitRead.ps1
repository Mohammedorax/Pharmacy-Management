param([Parameter(Mandatory=$true)][string]$Database,[Parameter(Mandatory=$true)][string]$Owner)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($Database-cne('PharmacyUnits_'+$Owner+'_qa')-or$Owner-cnotmatch'^[a-f0-9]{32}$'){throw 'Exact owned units QA identity required'}
$stage=Join-Path $root ('artifacts/return-history-read-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $stage|Out-Null
Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/ReturnHistoryUnitReadProbe.cs" -Destination $stage
$runtime=@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{[ordered]@{File=$_;SHA256=(Get-FileHash -LiteralPath "$stage/runtime/$_").Hash}})
$references=@('System.dll','System.Core.dll','System.Data.dll')+@('PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{"$stage/runtime/$_"})
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/ReturnHistoryUnitReadProbe.exe" @($references|ForEach-Object{'/reference:'+$_}) "$stage/ReturnHistoryUnitReadProbe.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Saved-history probe compilation failed'}
& "$stage/runtime/ReturnHistoryUnitReadProbe.exe" $Database $Owner *> "$stage/probe.log"
$code=$LASTEXITCODE
foreach($file in $runtime){if((Get-FileHash -LiteralPath "$stage/runtime/$($file.File)").Hash-cne$file.SHA256){throw 'Frozen runtime drift'}}
[ordered]@{Observed=(Get-Date).ToString('o');Directory=$stage;Database=$Database;Owner=$Owner;RuntimeFiles=$runtime;FixtureSHA256=(Get-FileHash "$stage/ReturnHistoryUnitReadProbe.cs").Hash;ExitCode=$code;SqlWrites=$false;UiUsed=$false;PhysicalPrint=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 8|Set-Content "$stage/result.json" -Encoding utf8
Get-Content "$stage/probe.log"
Write-Output $stage
if($code-ne0){throw 'Saved-return history unit regression failed'}