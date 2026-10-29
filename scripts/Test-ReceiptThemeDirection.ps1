[CmdletBinding()]
param([string]$ApplicationPath,[string]$DependencyDirectory,[string]$EvidenceDirectory,[switch]$IncludeNativeMatrix)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $ApplicationPath){$ApplicationPath=Join-Path $root 'project_fo_3\bin\Release\project_fo_3.exe'}
if(-not $DependencyDirectory){$DependencyDirectory=Join-Path $root 'project_fo_3\bin\Release'}
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $root ('artifacts\receipt-theme-direction-test-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$csc='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$exe=Join-Path $EvidenceDirectory 'ReceiptThemeDirectionTests.exe'
& $csc /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:'+$exe) (Join-Path $root 'tests\ReceiptThemeDirectionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Receipt theme test compilation failed'}
$config='<configuration><connectionStrings><add name="PharmacyConnection" connectionString="Data Source=report-preview.invalid;Initial Catalog=report_preview_test;Integrated Security=True;Connect Timeout=1" providerName="System.Data.SqlClient" /></connectionStrings><appSettings><add key="ForceBuildConnectionProfile" value="true" /></appSettings></configuration>'
[IO.File]::WriteAllText($exe+'.config',$config,[Text.UTF8Encoding]::new($false))
$arguments=@(('"'+$ApplicationPath+'"'),('"'+$DependencyDirectory+'"'))
if($IncludeNativeMatrix){$arguments+=@('full')}
$process=Start-Process -FilePath $exe -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $EvidenceDirectory 'stdout.log') -RedirectStandardError (Join-Path $EvidenceDirectory 'stderr.log')
[pscustomobject]@{ExitCode=$process.ExitCode;ApplicationPath=$ApplicationPath;ApplicationSHA256=(Get-FileHash -LiteralPath $ApplicationPath).Hash;NativeInput=$false;SqlUsed=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'result.json') -Encoding UTF8
if($process.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'stderr.log') -Tail 12;throw 'Receipt theme direction test failed'}
Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'stdout.log') -Tail 1