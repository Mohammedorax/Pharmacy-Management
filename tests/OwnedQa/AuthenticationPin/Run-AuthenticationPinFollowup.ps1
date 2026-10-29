param([ValidateSet('followup')][string]$Mode)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path ($PSScriptRoot+'/output') -Force | Out-Null
$root='D:/Comp/Pharmacy-Management-master'
$job=Get-Content "$PSScriptRoot/output/authentication-pin-sql-qa-job.json" -Raw|ConvertFrom-Json
$stage="$($job.Directory)/$Mode"
if(Test-Path $stage){throw 'Preserve existing SQL fixture; no repeat'}
New-Item -ItemType Directory $stage|Out-Null
Copy-Item -LiteralPath "$root/project_fo_3/bin/Release" -Destination "$stage/runtime" -Recurse
Copy-Item "$PSScriptRoot/AuthenticationPinFollowupProbe.cs" "$stage/Probe.cs"
$config=[xml][IO.File]::ReadAllText("$stage/runtime/project_fo_3.exe.config")
foreach($item in $config.configuration.connectionStrings.add){$item.connectionString='Data Source=.;Initial Catalog='+$job.Databases[0]+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False'}
$force=$config.configuration.appSettings.add|Where-Object key -eq ForceBuildConnectionProfile
if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)}
$force.SetAttribute('value','true');$config.Save("$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe.config")
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Configuration.dll','System.Windows.Forms.dll')+@('project_fo_3.exe','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object{"$stage/runtime/$_"})
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /codepage:65001 /langversion:latest "/out:$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" @($refs|ForEach-Object{'/reference:'+$_}) "$stage/Probe.cs" *> "$stage/compile.log"
if($LASTEXITCODE-ne0){Get-Content "$stage/compile.log";throw 'SQL fixture compilation failed'}
$p=Start-Process "$stage/runtime/PharmacyManagement.MonitorDesktopTests.exe" -ArgumentList @($job.Owner,$job.Databases[0],$job.Databases[1],$Mode) -WorkingDirectory "$stage/runtime" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$stage/stdout.log" -RedirectStandardError "$stage/stderr.log"
@{Directory=$stage;Owner=$job.Owner;Databases=$job.Databases;Mode=$Mode;ProcessId=$p.Id;StartUtcTicks=$p.StartTime.ToUniversalTime().Ticks;MainSHA256=(Get-FileHash -LiteralPath "$stage/runtime/project_fo_3.exe").Hash;SqlUsed=$true;ProductionTouched=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-pin-$Mode-job.json" -Encoding utf8
$p.WaitForExit();$p.Refresh()
@{State=if($p.ExitCode-eq0){'completed'}else{'failed'};ExitCode=$p.ExitCode;PhysicalPrinting=$false;SqlUsed=$true;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$stage/state.json" -Encoding utf8
Get-Content "$stage/stdout.log";if($p.ExitCode-ne0){Get-Content "$stage/stderr.log";throw 'SQL characterization failed; preserve original evidence'}
