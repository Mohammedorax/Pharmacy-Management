$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$dir=Join-Path $root ('artifacts/company-demo-20261008/report-cell-height-integrated-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $dir|Out-Null
[guid]::NewGuid().ToString('N')|Set-Content "$dir/owner.txt"
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$runtime=@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{[ordered]@{File=$_;SHA256=(Get-FileHash -LiteralPath "$root/project_fo_3/bin/Release/$_").Hash}})
foreach($mode in @('Measurement','Pagination')){
 $name=if($mode-eq'Measurement'){'ReportCellHeightProbe.cs'}else{'ReportRowPaginationProbe.cs'}
 $app="$dir/$mode-runtime"
 Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination $app -Recurse
 Copy-Item -LiteralPath "$PSScriptRoot/$name" -Destination "$dir/$name"
 $refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll'|ForEach-Object{"$app/$_"})
 & $compiler /nologo /codepage:65001 /langversion:latest "/out:$app/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$dir/$name" *> "$dir/$mode-compile.log"
 if($LASTEXITCODE-ne0){Get-Content "$dir/$mode-compile.log";throw "Compile failed: $mode"}
 & "$app/PharmacyManagement.MonitorDesktopTests.exe" $dir *> "$dir/$mode.log"
 $exitCode=$LASTEXITCODE;Get-Content "$dir/$mode.log" -Tail 3
 if($exitCode-ne0){throw "Actual shared renderer failed: $mode; retain $dir"}
 foreach($file in $runtime){if((Get-FileHash -LiteralPath "$app/$($file.File)").Hash-cne$file.SHA256){throw 'Pinned renderer runtime drift'}}
}
if(!(Select-String -LiteralPath "$dir/Measurement.log" -SimpleMatch 'RESULT checks=64; failures=0')-or!(Select-String -LiteralPath "$dir/Pagination.log" -SimpleMatch 'PASS pagination checks=3156')){throw 'Complete raw renderer evidence required'}
[ordered]@{Accepted=$true;Directory=$dir;RuntimeFiles=$runtime;MeasurementChecks=64;PaginationChecks=3156;ActualProductionRenderer=$true;Rows=60;UiUsed=$false;SqlUsed=$false;PhysicalPrinting=$false;NativeInput=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 6|Set-Content "$root/artifacts/original-plan-scope-audit/report-cell-height-integrated-current.json" -Encoding utf8
Write-Output 'PASS actual production renderer full text, 60-row pagination and bilingual oversized-row guards; no SQL/UI/paper'
