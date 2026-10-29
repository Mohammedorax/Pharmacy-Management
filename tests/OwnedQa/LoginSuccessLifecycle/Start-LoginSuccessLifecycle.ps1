param([Parameter(Mandatory=$true)][ValidateSet('Baseline','Fixed')][string]$Mode)
$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$job=Get-Content "$PSScriptRoot/output/login-success-lifecycle-job.json" -Raw|ConvertFrom-Json
$stage="$($job.Directory)/$Mode"
if(Test-Path -LiteralPath $stage){throw 'Original run exists; inspect without overwrite'}
[void](New-Item -ItemType Directory $stage)
if($Mode-eq'Fixed'){Copy-Item -LiteralPath "$root/project_fo_3/Login.cs" -Destination "$($job.Directory)/Source/project_fo_3/Login.cs"}
$build='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe'
& $build "$($job.Directory)/Source/project_fo_3.sln" /t:Build /p:Configuration=Release '/p:Platform=Any CPU' /m:1 /nr:false /p:UseSharedCompilation=false /nologo /v:minimal *> "$stage/source-build.log"
if($LASTEXITCODE-ne0){throw 'Fixture build failed; inspect retained build log'}
Copy-Item -LiteralPath "$($job.Directory)/Source/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/LoginSuccessLifecycleProbe.cs" -Destination "$stage/Probe.cs"
Copy-Item -LiteralPath "$PSScriptRoot/RequireOwnedPrivateDesktop.cs","$PSScriptRoot/OwnedPrivateDesktopLauncher.cs" -Destination $stage
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($add in $config.configuration.connectionStrings.add){$add.connectionString=$job.ConnectionString}
$force=$config.configuration.appSettings.add|Where-Object key -eq 'ForceBuildConnectionProfile'
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$config.Save("$stage/runtime/project_fo_3.exe.config")
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Configuration.dll','System.Security.dll')+@('project_fo_3.exe','AntdUI.dll','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{"$stage/runtime/$_"})
& $compiler /nologo /target:exe /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/Probe.cs" "$stage/RequireOwnedPrivateDesktop.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){throw 'Probe compile failed; inspect retained compile log'}
Copy-Item -LiteralPath "$stage/runtime/project_fo_3.exe.config" -Destination "$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config"
& $compiler /nologo /target:exe "/out:$stage/Launcher.exe" "$stage/OwnedPrivateDesktopLauncher.cs" *> "$stage/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Owned launcher compile failed'}
$worker=@'
$ErrorActionPreference='Stop'
$j=Get-Content "$PSScriptRoot/../job.json" -Raw|ConvertFrom-Json
$mode=Split-Path $PSScriptRoot -Leaf
$state=[ordered]@{State='running';ExitCode=1;Mode=$mode;NativeInput=$false;PhysicalPrinting=$false;ActualSql=$true;SourceFixtureOnly=$true;FullPlanComplete=$false}
$lock=$null
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1'
 $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($j.Database,$mode,$j.RememberFolder) -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/probe.stdout.log" -RedirectStandardError "$PSScriptRoot/probe.stderr.log"
 $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json" -Encoding utf8
 $p.WaitForExit();$p.Refresh();$state.ExitCode=$p.ExitCode
 if($p.ExitCode-ne0-or(Get-Item "$PSScriptRoot/probe.stderr.log").Length-ne0){throw 'Original lifecycle probe failed; preserve first evidence'}
 $state.State='completed'
}catch{$state.State='failed';$state.Error=$_.Exception.ToString()}
finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};$state|ConvertTo-Json -Depth 8|Set-Content "$PSScriptRoot/state.json" -Encoding utf8}
exit $state.ExitCode
'@
Copy-Item -LiteralPath "$PSScriptRoot/output/login-success-lifecycle-job.json" -Destination "$($job.Directory)/job.json"
[IO.File]::WriteAllText("$stage/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
$p=Start-Process "$stage/Launcher.exe" -ArgumentList @(('"'+$stage+'/Run-Isolated.ps1"'),('"'+$stage+'"')) -WorkingDirectory $stage -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/launcher.stdout.log" -RedirectStandardError "$stage/launcher.stderr.log"
[ordered]@{Directory=$stage;Mode=$Mode;LauncherPid=$p.Id;LauncherStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;InstrumentedRememberFolderOnly=$true;LoginSourceSHA256=(Get-FileHash -LiteralPath "$($job.Directory)/Source/project_fo_3/Login.cs").Hash;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash;FullPlanComplete=$false}|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/output/login-success-lifecycle-$Mode-job.json" -Encoding utf8
Write-Output "START $Mode original owned lifecycle probe: $stage"
