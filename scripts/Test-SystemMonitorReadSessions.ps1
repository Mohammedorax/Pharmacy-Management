[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ApplicationPath,
 [Parameter(Mandatory)][string]$EvidenceDirectory,
 [ValidateSet('Run','RunSql')][string]$Mode='Run'
)
$ErrorActionPreference='Stop'
$lock=& (Join-Path $PSScriptRoot 'Enter-DesktopQaLock.ps1')
try {
 $root=Split-Path -Parent $PSScriptRoot
 $ApplicationPath=(Resolve-Path -LiteralPath $ApplicationPath).Path
 $bin=Split-Path -Parent $ApplicationPath
 $EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
 if(Test-Path -LiteralPath $EvidenceDirectory){throw 'Use a fresh evidence directory'}
 New-Item -ItemType Directory -Path $EvidenceDirectory|Out-Null
 Copy-Item -Path (Join-Path $bin '*') -Destination $EvidenceDirectory -Recurse
 $main=Join-Path $EvidenceDirectory 'project_fo_3.exe'
 $runner=Join-Path $EvidenceDirectory 'PharmacyManagement.MonitorDesktopTests.exe'
 $source=Join-Path $root 'tests\PharmacyManagement.MonitorDesktop'
 $vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
 $ms=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe'|Select-Object -First 1
 $csc=Join-Path (Split-Path -Parent $ms) 'Roslyn\csc.exe'
 & $csc /nologo "/out:$runner" "/r:$main" "/r:$EvidenceDirectory\PharmacyManagement.Application.dll" "/r:$EvidenceDirectory\PharmacyManagement.Infrastructure.SqlServer.dll" "/r:$EvidenceDirectory\AntdUI.dll" /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Configuration.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Accessibility.dll (Join-Path $source 'Runner.cs') (Join-Path $source 'MonitorDesktopProbe.cs') *> (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE){throw 'Monitor external host compilation failed'}
 Copy-Item -LiteralPath ($main+'.config') -Destination ($runner+'.config')
 if($Mode -eq 'RunSql'){
  [xml]$config=Get-Content -LiteralPath ($runner+'.config') -Raw
  $force=$config.configuration.appSettings.add|Where-Object key -eq 'ForceBuildConnectionProfile'|Select-Object -First 1
  if(!$force){$force=$config.CreateElement('add');$force.SetAttribute('key','ForceBuildConnectionProfile');$config.configuration.appSettings.AppendChild($force)|Out-Null}
  $force.SetAttribute('value','true')
  foreach($name in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')){
   $node=$config.configuration.connectionStrings.add|Where-Object name -eq $name|Select-Object -First 1
   if(!$node){throw "Missing connection string $name"}
   $node.SetAttribute('connectionString','Server=.;Database=PharmacyManagement_qa;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=ExternalMonitorQa')
  }
  $config.Save($runner+'.config')
 }
 $stdout=Join-Path $EvidenceDirectory 'stdout.log';$stderr=Join-Path $EvidenceDirectory 'stderr.log'
 $process=Start-Process -FilePath $runner -ArgumentList @(('"' + $main + '"'),$Mode) -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
 $count=if($Mode -eq 'Run'){62}else{26}
 $marker=if($Mode -eq 'Run'){'CHECKS=62'}else{'SQL_COMPOSITE_CHECKS=26'}
 if($process.ExitCode -ne 0 -or (Get-Item -LiteralPath $stderr).Length -ne 0 -or !(Select-String -LiteralPath $stdout -SimpleMatch $marker -Quiet) -or !(Select-String -LiteralPath $stdout -SimpleMatch 'MonitorFixtureInShippingAssembly=False' -Quiet)){throw "Monitor probe failed: $stderr"}
 if((Get-FileHash -LiteralPath $main).Hash -ne (Get-FileHash -LiteralPath $ApplicationPath).Hash){throw 'Selected application changed'}
 @{Accepted=$true;MainSHA256=(Get-FileHash -LiteralPath $main).Hash;ApplicationSHA256=(Get-FileHash -LiteralPath (Join-Path $EvidenceDirectory 'PharmacyManagement.Application.dll')).Hash;InfrastructureSHA256=(Get-FileHash -LiteralPath (Join-Path $EvidenceDirectory 'PharmacyManagement.Infrastructure.SqlServer.dll')).Hash;Mode=$Mode;Checks=$count;FixtureOutsideShippingAssembly=$true;AuthenticationServiceExercised=$false;NativeInputExercised=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'accepted-audit.json') -Encoding utf8
 Get-Content -LiteralPath $stdout -Tail 3
} finally {$lock.ReleaseMutex();$lock.Dispose()}
