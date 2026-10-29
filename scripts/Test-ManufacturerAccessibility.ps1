[CmdletBinding()]
param([string]$ApplicationPath, [string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$desktopQaUiLock = & (Join-Path $PSScriptRoot 'Enter-DesktopQaLock.ps1')
try {
$root=Split-Path -Parent $PSScriptRoot
if(!$ApplicationPath){$ApplicationPath=Join-Path $root 'project_fo_3\bin\Release\project_fo_3.exe'}
$ApplicationPath=(Resolve-Path -LiteralPath $ApplicationPath).Path
$bin=Split-Path -Parent $ApplicationPath
if(!$EvidenceDirectory){$EvidenceDirectory=Join-Path $root ('artifacts\manufacturer-accessibility-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $EvidenceDirectory){throw 'Use a fresh evidence directory to preserve previous results.'}
New-Item -ItemType Directory -Path $EvidenceDirectory|Out-Null
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe'|Select-Object -First 1
$csc=Join-Path (Split-Path -Parent $msbuild) 'Roslyn\csc.exe'
if(!(Test-Path -LiteralPath $csc)){throw 'Roslyn compiler not found.'}
# Each host uses the selected binary's resources and configuration; neither probe opens SQL.
Copy-Item -Path (Join-Path $bin '*') -Destination $EvidenceDirectory -Recurse
$expected=(Get-FileHash -LiteralPath $ApplicationPath).Hash
$results=@()
foreach($probe in @('AccessProbe','SessionAccessProbe')){
 $source=Join-Path $root ('tests\PharmacyManagement.ManufacturerAccessibility\'+$probe+'.cs')
 $runner=Join-Path $EvidenceDirectory ($probe+'.exe')
 Copy-Item -LiteralPath ($ApplicationPath+'.config') -Destination ($runner+'.config')
 $compileLog=Join-Path $EvidenceDirectory ($probe+'.compile.log')
 & $csc /nologo "/out:$runner" "/r:$EvidenceDirectory\AntdUI.dll" "/r:$EvidenceDirectory\PharmacyManagement.Application.dll" /r:System.Data.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Accessibility.dll $source *> $compileLog
 if($LASTEXITCODE){throw "Compile failed: $compileLog"}
 $stdout=Join-Path $EvidenceDirectory ($probe+'.stdout.log');$stderr=Join-Path $EvidenceDirectory ($probe+'.stderr.log')
 $args=@(('"'+$ApplicationPath+'"'),('"'+$EvidenceDirectory+'"'))
 $process=Start-Process -FilePath $runner -ArgumentList $args -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
 $count=if($probe -eq 'AccessProbe'){49}else{20}
 if($process.ExitCode -ne 0 -or (Get-Item -LiteralPath $stderr).Length -ne 0 -or !(Select-String -LiteralPath $stdout -SimpleMatch "CHECKS=$count" -Quiet)){throw "Probe failed: $stdout; $stderr"}
 $results+=@{Probe=$probe;Checks=$count;ExitCode=$process.ExitCode;StandardOutput=$stdout;StandardError=$stderr}
}
if((Get-FileHash -LiteralPath $ApplicationPath).Hash -ne $expected){throw 'Application changed during verification.'}
@{Accepted=$true;MainSHA256=$expected;Results=$results;Scope='Read-only manufacturer provider and actual SessionContext revision guards; no SQL, native input, complete screen-reader journey, or physical devices';FullPlanComplete=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'accepted-audit.json') -Encoding utf8
Write-Output ('PASS: 69 manufacturer accessibility assertions; evidence='+$EvidenceDirectory)

} finally {
    $desktopQaUiLock.ReleaseMutex()
    $desktopQaUiLock.Dispose()
}