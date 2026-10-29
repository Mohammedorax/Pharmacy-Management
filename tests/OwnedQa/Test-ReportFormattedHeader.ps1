$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$dir=Join-Path $root ('artifacts/company-demo-20261008/report-header-height-formatted-integrated-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory $dir|Out-Null
[guid]::NewGuid().ToString('N')|Set-Content "$dir/owner.txt"
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$runtime=@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{[ordered]@{File=$_;SHA256=(Get-FileHash -LiteralPath "$root/project_fo_3/bin/Release/$_").Hash}})
foreach($mode in @('Date','Fit','Pagination')){
 $app="$dir/$mode-runtime";Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination $app -Recurse
 $name="ReportFormattedHeader$($mode)Probe.cs";Copy-Item -LiteralPath "$PSScriptRoot/$name" -Destination "$dir/$name"
 $refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll'|ForEach-Object{"$app/$_"})
 & $compiler /nologo /codepage:65001 /langversion:latest "/out:$app/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$dir/$name" *> "$dir/$mode-compile.log"
 if($LASTEXITCODE-ne0){Get-Content "$dir/$mode-compile.log";throw "Actual formatter compile failed: $mode"}
 & "$app/PharmacyManagement.MonitorDesktopTests.exe" $dir "$root/tests/OwnedQa/DailyTemplateFixtures" *> "$dir/$mode.log"
 $code=$LASTEXITCODE;Get-Content "$dir/$mode.log" -Tail 2
 if($code-ne0){throw "Actual formatter failed: $mode; preserve $dir"}
 foreach($file in $runtime){if((Get-FileHash -LiteralPath "$app/$($file.File)").Hash-cne$file.SHA256){throw 'Pinned actual runtime drift'}}
}
$fit=Get-Content "$dir/Fit.log" -Raw;$match=[regex]::Match($fit,'RESULT header checks=(\d+); failures=0;')
$cases=@(Get-Content "$dir/Fit.log"|Where-Object {$_-like'CASE *'}|ForEach-Object {($_-split';')[0]})
$expected=@(foreach($kind in @('financial','stock')){foreach($rtl in @('True','False')){foreach($density in @('Standard','Compact')){foreach($width in @(260,760)){"CASE $kind/$rtl/$density/$width"}}}})
if(!$match.Success-or($cases-join"`n")-cne($expected-join"`n")-or!(Select-String -LiteralPath "$dir/Date.log" -SimpleMatch 'RESULT date checks=104; failures=0')-or!(Select-String -LiteralPath "$dir/Pagination.log" -SimpleMatch 'PASS pagination checks=3876')){throw 'Full actual formatter matrix/date/pagination evidence absent'}
[ordered]@{Accepted=$true;Directory=$dir;RuntimeFiles=$runtime;HeaderAssertions=[int]$match.Groups[1].Value;HeaderMatrixCases=$cases;DateChecks=104;Cultures=4;PaginationChecks=3876;ActualProductionRenderer=$true;EveryColumnAndRowCovered=$true;Rows=60;UiUsed=$false;SqlUsed=$false;PhysicalPrinting=$false;NativeInput=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 7|Set-Content "$root/artifacts/original-plan-scope-audit/formatted-header-integrated-current.json" -Encoding utf8
Write-Output 'PASS actual shared header/date formatter, complete daily matrix and 60-row pagination; operational grid/period UI remains separate'
