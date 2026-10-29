param([Parameter(Mandatory=$true)][string]$Database,[Parameter(Mandatory=$true)][string]$Owner,[switch]$ConfirmOwnedQaReadLocks)
$ErrorActionPreference='Stop'
if(!$ConfirmOwnedQaReadLocks){throw 'Explicit owned QA read-lock confirmation required'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$qa=[pscustomobject]@{Database=$Database;Owner=$Owner}
$ownerId=[guid]::Empty
if(![guid]::TryParseExact($qa.Owner,'N',[ref]$ownerId)-or$qa.Database-cne('PharmacyUnits_'+$qa.Owner+'_qa')){throw 'Exact owned QA identity required'}
$stage=Join-Path $root ('artifacts/company-demo-20261008/unit-editor-read-lifetime-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory $stage|Out-Null
Copy-Item "$root/project_fo_3/bin/Release" "$stage/runtime" -Recurse
Copy-Item "$PSScriptRoot/UnitEditorCloseSqlProbe.cs","$PSScriptRoot/DesktopIsolation/RequireOwnedPrivateDesktop.cs","$PSScriptRoot/DesktopIsolation/OwnedPrivateDesktopLauncher.cs" $stage
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
$cs="Data Source=.;Initial Catalog=$($qa.Database);Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=5;Application Name=UnitEditorCloseOwnedQa_$([guid]::NewGuid().ToString('N'))"
foreach($item in $config.configuration.connectionStrings.add){$item.connectionString=$cs}
$force=$config.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true')
$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config")
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Configuration.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll','AntdUI.dll'|ForEach-Object{"$stage/runtime/$_"})
& $compiler /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/UnitEditorCloseSqlProbe.cs" "$stage/RequireOwnedPrivateDesktop.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'Probe compile failed'}
& $compiler /nologo /target:exe "/out:$stage/Launcher.exe" "$stage/OwnedPrivateDesktopLauncher.cs" *> "$stage/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Launcher compile failed'}
$worker=@'

$ErrorActionPreference='Stop'
;$lock=$null
$expected=Get-Content "$PSScriptRoot/expected.json" -Raw|ConvertFrom-Json
$state=[ordered]@{State='running';ExitCode=1;SqlUsed=$true;BusinessWrites=$false;NativeInput=$false;Paper=$false;FullPlanComplete=$false}
function Save{$state|ConvertTo-Json -Depth 5|Set-Content "$PSScriptRoot/state.json"}
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1'
 foreach($file in $expected.RuntimeFiles){if((Get-FileHash -LiteralPath "$PSScriptRoot/runtime/$($file.File)").Hash-cne$file.SHA256){throw 'Prepared runtime drift'}}
 $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($expected.Database,$expected.Owner) -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/probe.stdout.log" -RedirectStandardError "$PSScriptRoot/probe.stderr.log"
 $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;Save
 $p.WaitForExit();$p.Refresh();$state.ExitCode=$p.ExitCode;$state.State=if($p.ExitCode-eq0){'completed'}else{'failed'}
}catch{$state.State='failed';$state.Error=$_.Exception.ToString()}finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};Save}
exit $state.ExitCode
'@
[IO.File]::WriteAllText("$stage/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
$runtime=@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{[ordered]@{File=$_;SHA256=(Get-FileHash -LiteralPath "$stage/runtime/$_").Hash}})
$expected=[ordered]@{State='prepared';Directory=$stage;Database=$qa.Database;Owner=$qa.Owner;RuntimeFiles=$runtime;ProbeSHA256=(Get-FileHash -LiteralPath "$stage/UnitEditorCloseSqlProbe.cs").Hash;SqlUsedYet=$false;Started=$false;FullPlanComplete=$false}
$expected|ConvertTo-Json -Depth 8|Set-Content "$stage/expected.json"

$p=Start-Process "$stage/Launcher.exe" -ArgumentList @(('"'+$stage+'/Run-Isolated.ps1"'),('"'+$stage+'"')) -WorkingDirectory $stage -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/launcher.stdout.log" -RedirectStandardError "$stage/launcher.stderr.log"
$expected.Started=$true;$expected.State='started';$expected['LauncherPid']=$p.Id;$expected['LauncherStartUtcTicks']=$p.StartTime.ToUniversalTime().Ticks
$expected|ConvertTo-Json -Depth 10|Set-Content "$stage/job.json"
$p.WaitForExit();$p.Refresh()
$terminal=Get-Content "$stage/desktop-terminal.json" -Raw|ConvertFrom-Json
$state=Get-Content "$stage/state.json" -Raw|ConvertFrom-Json
Get-Content "$stage/probe.stdout.log"
if($p.ExitCode-ne0-or$state.State-ne'completed'-or$state.ExitCode-ne0-or!$terminal.DesktopHandleClosed-or$terminal.SwitchedInputDesktop-or(Get-Item "$stage/probe.stderr.log").Length-ne0){throw "Owned unit editor read/close/save lifetime check failed; retain $stage"}
[ordered]@{Accepted=$true;Directory=$stage;RuntimeFiles=$runtime;Database=$Database;Owner=$Owner;BusinessWrites=$false;NativeInput=$false;PhysicalPrint=$false;ProbeSHA256=$expected.ProbeSHA256;FullPlanComplete=$false}|ConvertTo-Json -Depth 10|Set-Content "$stage/result.json"
Write-Output "PASS permanent owned unit editor lifetime: $stage"
