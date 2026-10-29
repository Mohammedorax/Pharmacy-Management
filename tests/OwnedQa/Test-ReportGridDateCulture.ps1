param([Parameter(Mandatory=$true)][string]$Label)
$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$stage=Join-Path $root ('artifacts/company-demo-20261008/report-grid-date-'+$Label+'-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory $stage|Out-Null
Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/ReportGridDateCultureProbe.cs" -Destination "$stage/Probe.cs"
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($item in $config.configuration.connectionStrings.add){$item.connectionString='Data Source=report-preview.invalid;Initial Catalog=PharmacyReportPeriod_test;Integrated Security=True;Connect Timeout=1;Pooling=False'}
$force=$config.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config");$config.Save("$stage/runtime/project_fo_3.exe.config")
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Configuration.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll','AntdUI.dll'|ForEach-Object{"$stage/runtime/$_"})
& $compiler /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/Probe.cs" "$PSScriptRoot/DesktopIsolation/RequireOwnedPrivateDesktop.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Period fixture compilation failed'}
& $compiler /nologo /target:exe "/out:$stage/Launcher.exe" "$PSScriptRoot/DesktopIsolation/OwnedPrivateDesktopLauncher.cs" *> "$stage/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Private launcher compilation failed'}
$worker=@'
$ErrorActionPreference='Stop';$lock=$null;$state=[ordered]@{State='running';ExitCode=1;SqlUsed=$false;NativeInput=$false;PhysicalPrinting=$false;FullPlanComplete=$false}
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1'
 $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/probe.stdout.log" -RedirectStandardError "$PSScriptRoot/probe.stderr.log"
 $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json" -Encoding utf8
 $p.WaitForExit();$p.Refresh();$state.ExitCode=$p.ExitCode;$state.State=if($p.ExitCode-eq0){'completed'}else{'failed'}
}catch{$state.State='failed';$state.Error=$_.Exception.ToString()}
finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};$state|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/state.json" -Encoding utf8}
exit $state.ExitCode
'@
[IO.File]::WriteAllText("$stage/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
$p=Start-Process "$stage/Launcher.exe" -ArgumentList @(('"'+$stage+'/Run-Isolated.ps1"'),('"'+$stage+'"')) -WorkingDirectory $stage -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/launcher.stdout.log" -RedirectStandardError "$stage/launcher.stderr.log"
[ordered]@{Directory=$stage;LauncherPid=$p.Id;LauncherStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash;FormSourceSHA256=(Get-FileHash -LiteralPath "$root/project_fo_3/OperationalReportBuilderForm.cs").Hash;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$root/artifacts/original-plan-scope-audit/report-grid-date-$Label-job.json" -Encoding utf8
$stage
