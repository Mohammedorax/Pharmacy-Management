param([Parameter(Mandatory=$true)][ValidateSet('baseline','fixed')][string]$Mode)
$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$job=Get-Content "$PSScriptRoot/output/authentication-readiness-qa-job.json" -Raw|ConvertFrom-Json
$stage="$($job.Directory)/$Mode";if(Test-Path -LiteralPath $stage){throw 'Original run exists; inspect without overwrite'}
[void](New-Item -ItemType Directory $stage)
Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/AuthenticationReadinessProbe.cs" -Destination "$stage/Probe.cs"
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($add in $config.configuration.connectionStrings.add){$add.connectionString='Data Source=.;Initial Catalog='+$job.Databases[0]+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False'}
$force=$config.configuration.appSettings.add|Where-Object key -eq 'ForceBuildConnectionProfile'
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config")
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Configuration.dll','System.Windows.Forms.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{"$stage/runtime/$_"})
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/Probe.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){throw 'Readiness probe compile failed'}
$p=Start-Process "$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($job.Owner,$job.Databases[0],$job.Databases[1],$job.Databases[2],$Mode) -WorkingDirectory "$stage/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/stdout.log" -RedirectStandardError "$stage/stderr.log"
@{Directory=$stage;Mode=$Mode;ProcessId=$p.Id;StartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-readiness-$Mode-job.json" -Encoding utf8
$p.WaitForExit();$p.Refresh()
@{State=if($p.ExitCode-eq0){'completed'}else{'failed'};ExitCode=$p.ExitCode;SqlUsed=$true;PhysicalPrinting=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$stage/state.json" -Encoding utf8
Get-Content "$stage/stdout.log";if($p.ExitCode-ne0){Get-Content "$stage/stderr.log";throw 'Original readiness probe failed; preserve evidence'}
