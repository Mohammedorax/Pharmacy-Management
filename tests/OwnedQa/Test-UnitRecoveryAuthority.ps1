param([string]$Label='regression')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$stage=Join-Path $root ('artifacts/company-demo-20261008/unit-authority-'+$Label+'-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory $stage|Out-Null
Copy-Item "$root/project_fo_3/bin/Release" "$stage/runtime" -Recurse


$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($c in $config.configuration.connectionStrings.add){$c.connectionString='Data Source=report-preview.invalid;Initial Catalog=PharmacyDailyTemplate_test;Integrated Security=True;Connect Timeout=1;Pooling=False'}
$force=$config.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true')
$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config")
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Configuration.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll','AntdUI.dll'|ForEach-Object{"$stage/runtime/$_"})
& $compiler /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$PSScriptRoot/UnitRecoveryAuthorityProbe.cs" "$PSScriptRoot/DesktopIsolation/RequireOwnedPrivateDesktop.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Compile failed'}
& $compiler /nologo /target:exe "/out:$stage/Launcher.exe" "$PSScriptRoot/DesktopIsolation/OwnedPrivateDesktopLauncher.cs" *> "$stage/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Launcher compile failed'}
$worker=@'
$ErrorActionPreference='Stop';$lock=$null;$state=[ordered]@{State='running';ExitCode=1;SqlUsed=$false;Paper=$false;NativeInput=$false}
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1'
 $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList ('"'+$PSScriptRoot+'/evidence"') -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/probe.stdout.log" -RedirectStandardError "$PSScriptRoot/probe.stderr.log"
 $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json"
 $p.WaitForExit();$p.Refresh();$state.ExitCode=$p.ExitCode;$state.State=if($p.ExitCode-eq0){'completed'}else{'failed'}
}catch{$state.Error=$_.Exception.ToString();$state.State='failed'}finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json"}
exit $state.ExitCode
'@
[IO.File]::WriteAllText("$stage/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
$p=Start-Process "$stage/Launcher.exe" -ArgumentList @(('"'+$stage+'/Run-Isolated.ps1"'),('"'+$stage+'"')) -WorkingDirectory $stage -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/launcher.stdout.log" -RedirectStandardError "$stage/launcher.stderr.log"
[ordered]@{Directory=$stage;LauncherPid=$p.Id;LauncherStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash}|ConvertTo-Json|Set-Content "$root/artifacts/original-plan-scope-audit/unit-authority-$Label-job.json"
$stage
