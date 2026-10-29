param([Parameter(Mandatory=$true)][string]$Label)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path ($PSScriptRoot+'/output') -Force | Out-Null
$root='D:/Comp/Pharmacy-Management-master'
$stage=Join-Path $root ('artifacts/company-demo-20261008/login-edited-'+$Label+'-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory $stage|Out-Null
Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/LoginCloseGuardRegression.cs" -Destination "$stage/Probe.cs"
Copy-Item -LiteralPath "$PSScriptRoot/RequireOwnedPrivateDesktop.cs" -Destination "$stage/RequireOwnedPrivateDesktop.cs"
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($item in $config.configuration.connectionStrings.add){$item.connectionString='Data Source=report-preview.invalid;Initial Catalog=PharmacyLoginEdited_test;Integrated Security=True;Connect Timeout=1;Pooling=False'}
$force=$config.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config");$config.Save("$stage/runtime/project_fo_3.exe.config")
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Configuration.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll','AntdUI.dll'|ForEach-Object{"$stage/runtime/$_"})
& $compiler /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/Probe.cs" "$stage/RequireOwnedPrivateDesktop.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Edited login fixture compilation failed'}
& $compiler /nologo /target:exe "/out:$stage/Launcher.exe" "$PSScriptRoot/OwnedPrivateDesktopLauncher.cs" *> "$stage/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Private launcher compilation failed'}
$worker=@'
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path ($PSScriptRoot+'/output') -Force | Out-Null;$lock=$null;$state=[ordered]@{State='running';ExitCode=1;Runs=@();SqlUsed=$false;SuccessfulAuthentication=$false;RememberedUserStoreUsed=$false;NativeInput=$false;PhysicalPrinting=$false;FullPlanComplete=$false}
function Save{$state|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/state.json" -Encoding utf8}
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1';Save
 foreach($mode in @('Invalid','Exception','Setup')){foreach($outcome in @('Close')){
  $scenario=$mode+'-'+$outcome
  $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($mode,$outcome) -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/$scenario.stdout.log" -RedirectStandardError "$PSScriptRoot/$scenario.stderr.log"
  $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;Save
  $p.WaitForExit();$p.Refresh();$state.Runs+=@{Mode=$mode;Outcome=$outcome;ExitCode=$p.ExitCode};Save
  if($p.ExitCode-gt1-or(Get-Item "$PSScriptRoot/$scenario.stderr.log").Length-ne0){throw "Owned characterization setup/diagnostic failure: $mode"}
 }}
 $language=Start-Process "$PSScriptRoot/runtime/project_fo_3.exe" -ArgumentList '--verify-ui-language-catalog' -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/language.stdout.log" -RedirectStandardError "$PSScriptRoot/language.stderr.log"
 $language.WaitForExit();$language.Refresh();if($language.ExitCode-ne0-or(Get-Item "$PSScriptRoot/language.stderr.log").Length-ne0){throw 'Language catalog failed'}
 $state.LanguageCatalogPassed=$true
 $state.ExitCode=if(@($state.Runs|Where-Object ExitCode -ne 0).Count){1}else{0}
 $state.State=if($state.ExitCode-eq0){'completed'}else{'characterized-product-gap'}
}catch{$state.State='failed';$state.Error=$_.Exception.ToString()}
finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};Save}
exit $state.ExitCode
'@
[IO.File]::WriteAllText("$stage/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
$p=Start-Process "$stage/Launcher.exe" -ArgumentList @(('"'+$stage+'/Run-Isolated.ps1"'),('"'+$stage+'"')) -WorkingDirectory $stage -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/launcher.stdout.log" -RedirectStandardError "$stage/launcher.stderr.log"
[ordered]@{Directory=$stage;LauncherPid=$p.Id;LauncherStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash;ProbeSHA256=(Get-FileHash -LiteralPath "$stage/Probe.cs").Hash;SuccessfulAuthentication=$false;RememberedUserStoreUsed=$false;SqlUsed=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/login-edited-$Label-job.json" -Encoding utf8
$stage
