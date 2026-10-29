param([Parameter(Mandatory=$true)][string]$Database,[Parameter(Mandatory=$true)][string]$Owner,[switch]$ConfirmOwnedQaReads)
if(!$ConfirmOwnedQaReads-or$Owner-notmatch'^[a-f0-9]{32}$'-or$Database-cne('PharmacyUnits_'+$Owner+'_qa')){throw 'Exact owned QA and explicit read-only confirmation required'}
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$qa=[pscustomobject]@{Database=$Database;Owner=$Owner}
if($qa.Database-cne('PharmacyUnits_'+$qa.Owner+'_qa')){throw 'Owned QA required'}
$parent="$root/artifacts/company-demo-20261008/unit-editor-pending-process-owned-"+[guid]::NewGuid().ToString('N')
New-Item -ItemType Directory "$parent/recovery"|Out-Null
[IO.File]::WriteAllText("$parent/recovery/owner.txt",$qa.Owner)
Copy-Item "$root/project_fo_3/bin/Release" "$parent/compiled" -Recurse
Copy-Item "$PSScriptRoot/UnitEditorPendingRecoveryProbe.cs","$PSScriptRoot/DesktopIsolation/RequireOwnedPrivateDesktop.cs","$PSScriptRoot/DesktopIsolation/OwnedPrivateDesktopLauncher.cs" $parent
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Configuration.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll','AntdUI.dll'|ForEach-Object{"$parent/compiled/$_"})
& $compiler /nologo /codepage:65001 /langversion:latest "/out:$parent/compiled/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$parent/UnitEditorPendingRecoveryProbe.cs" "$parent/RequireOwnedPrivateDesktop.cs" *> "$parent/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$parent/compile.log";throw 'UI recovery probe compile failed'}
& $compiler /nologo /target:exe "/out:$parent/Launcher.exe" "$parent/OwnedPrivateDesktopLauncher.cs" *> "$parent/launcher-compile.log"
if($LASTEXITCODE-ne0){throw 'Launcher compilation failed'}
$cfg=[xml][IO.File]::ReadAllText("$parent/compiled/project_fo_3.exe.config")
foreach($entry in $cfg.configuration.connectionStrings.add){$entry.connectionString="Data Source=.;Initial Catalog=$($qa.Database);Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=5"}
$force=$cfg.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$cfg.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$cfg.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$cfg.Save("$parent/compiled/PharmacyManagement.MonitorDesktopTests.exe.config")
$results=@()
foreach($mode in @('write','read')){
 $dir="$parent/$mode";New-Item -ItemType Directory $dir|Out-Null
 Copy-Item "$parent/compiled" "$dir/runtime" -Recurse
 [ordered]@{Database=$qa.Database;Owner=$qa.Owner;Mode=$mode;Recovery="$parent/recovery"}|ConvertTo-Json|Set-Content "$dir/expected.json"
 $worker=@'
$ErrorActionPreference='Stop';$lock=$null;$state=[ordered]@{State='running';ExitCode=1;SqlWrites=$false;NativeInput=$false;Paper=$false}
try{
 $lock=& 'D:/Comp/Pharmacy-Management-master/scripts/Enter-DesktopQaLock.ps1'
 $e=Get-Content "$PSScriptRoot/expected.json" -Raw|ConvertFrom-Json
 $p=Start-Process "$PSScriptRoot/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($e.Database,$e.Owner,$e.Mode,('"'+$e.Recovery+'"')) -WorkingDirectory "$PSScriptRoot/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PSScriptRoot/probe.stdout.log" -RedirectStandardError "$PSScriptRoot/probe.stderr.log"
 $state.ChildPid=$p.Id;$state.ChildStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json"
 $p.WaitForExit();$p.Refresh();$state.ExitCode=$p.ExitCode;$state.State=if($p.ExitCode-eq0){'completed'}else{'failed'}
}catch{$state.State='failed';$state.Error=$_.Exception.ToString()}finally{if($lock){$lock.ReleaseMutex();$lock.Dispose()};$state|ConvertTo-Json|Set-Content "$PSScriptRoot/state.json"}
exit $state.ExitCode
'@
 [IO.File]::WriteAllText("$dir/Run-Isolated.ps1",$worker,[Text.UTF8Encoding]::new($false))
 $p=Start-Process "$parent/Launcher.exe" -ArgumentList @(('"'+$dir+'/Run-Isolated.ps1"'),('"'+$dir+'"')) -WorkingDirectory $dir -WindowStyle Hidden -PassThru -RedirectStandardOutput "$dir/launcher.stdout.log" -RedirectStandardError "$dir/launcher.stderr.log"
 [ordered]@{Directory=$parent;Mode=$mode;LauncherPid=$p.Id;LauncherStartUtcTicks=$p.StartTime.ToUniversalTime().Ticks}|ConvertTo-Json|Set-Content "$root/artifacts/original-plan-scope-audit/unit-editor-pending-process-current-job.json"
 $p.WaitForExit();$p.Refresh();$terminal=Get-Content "$dir/desktop-terminal.json" -Raw|ConvertFrom-Json
 Get-Content "$dir/probe.stdout.log"
 if($p.ExitCode-ne0-or!$terminal.DesktopHandleClosed-or$terminal.SwitchedInputDesktop-or(Get-Item "$dir/probe.stderr.log").Length-ne0){Get-Content "$dir/probe.stderr.log";throw "UI recovery process failed: $mode; preserve $parent"}
 $results+=[ordered]@{Mode=$mode;ExitCode=0;DesktopClosed=$true}
}
[ordered]@{Passed=$true;Directory=$parent;MainSHA256=(Get-FileHash -LiteralPath "$parent/compiled/project_fo_3.exe").Hash;Database=$qa.Database;Owner=$qa.Owner;Results=$results;SqlWrites=$false;FakeWriterUsed=$true;NativeInput=$false;PhysicalPrint=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 8|Set-Content "$root/artifacts/original-plan-scope-audit/unit-editor-pending-process-current-verified.json"
