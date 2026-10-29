$ErrorActionPreference='Stop'
$owner=[guid]::NewGuid().ToString('N');$dir="D:/Comp/Pharmacy-Management-master/artifacts/company-demo-20261008/unit-pending-store-owned-$owner"
New-Item -ItemType Directory $dir|Out-Null
[IO.File]::WriteAllText("$dir/owner.txt",$owner)
Copy-Item 'D:/Comp/Pharmacy-Management-master/project_fo_3/bin/Release' "$dir/runtime" -Recurse
Copy-Item "$PSScriptRoot/UnitPendingStoreProbe.cs" "$dir/UnitPendingStoreProbe.cs"
$refs=@('System.dll','System.Core.dll','System.Windows.Forms.dll','System.Security.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll'|ForEach-Object{"$dir/runtime/$_"})
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /codepage:65001 /langversion:latest "/out:$dir/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$dir/UnitPendingStoreProbe.cs" *> "$dir/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$dir/compile.log";throw 'Store probe compilation failed'}
foreach($mode in @('write','read')){
 & "$dir/runtime/PharmacyManagement.MonitorDesktopTests.exe" $dir $owner $mode *> "$dir/$mode.log"
 $code=$LASTEXITCODE;Get-Content "$dir/$mode.log";if($code-ne0){throw "Store probe failed: $mode; preserve $dir"}
}
[ordered]@{Directory=$dir;Owner=$owner;Passed=$true;MainSHA256=(Get-FileHash -LiteralPath "$dir/runtime/project_fo_3.exe").Hash;SqlUsed=$false;UiUsed=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "D:/Comp/Pharmacy-Management-master/artifacts/original-plan-scope-audit/unit-pending-store-process-current.json"
