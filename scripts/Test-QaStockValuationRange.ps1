[CmdletBinding()]
param([switch]$ConfirmQaRestore,[ValidateSet('baseline','fixed')][string]$Mode='fixed')
$ErrorActionPreference='Stop'
if(!$ConfirmQaRestore){throw 'Pass -ConfirmQaRestore to create and remove an owned temporary QA clone.'}
$r=Split-Path -Parent $PSScriptRoot
$release=Join-Path $r 'project_fo_3/bin/Release'
$main=(Get-FileHash -LiteralPath (Join-Path $release 'project_fo_3.exe')).Hash
$infra=(Get-FileHash -LiteralPath (Join-Path $release 'PharmacyManagement.Infrastructure.SqlServer.dll')).Hash
$directory=Join-Path $r ('artifacts/company-demo-20261008/stock-valuation-'+$Mode+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory -Path $directory)
$metaPath=Join-Path $directory 'run.json'
Get-ChildItem -LiteralPath $release -File -Filter '*.dll'|Copy-Item -Destination $directory
Copy-Item -LiteralPath (Join-Path $release 'project_fo_3.exe') -Destination $directory
Copy-Item -LiteralPath (Join-Path $r 'tests/PharmacyManagement.InventoryStockValuation/StockValuationRangeProbe.cs') -Destination (Join-Path $directory 'Probe.cs')
$probeExecutable=Join-Path $directory 'PharmacyManagement.MonitorDesktopTests.exe'
$references=@('System.Data.dll','System.Data.DataSetExtensions.dll','System.Configuration.dll')+@('project_fo_3.exe','AntdUI.dll','PharmacyManagement.Application.dll','PharmacyManagement.Domain.dll','PharmacyManagement.Infrastructure.SqlServer.dll'|ForEach-Object {Join-Path $directory $_})
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe' @('/nologo','/target:exe','/langversion:latest',('/out:'+$probeExecutable)) @($references|ForEach-Object {'/reference:'+$_}) (Join-Path $directory 'Probe.cs') *> (Join-Path $directory 'compile.log')
if($LASTEXITCODE-ne0){Get-Content (Join-Path $directory 'compile.log') -Tail 8;throw 'Stock valuation probe compilation failed'}
$original=Join-Path $r 'scripts/Test-QaBackupRestore.ps1'
$source=[IO.File]::ReadAllText($original)
$source=$source.Replace('$taskRoot=Split-Path -Parent $PSScriptRoot',('$taskRoot='''+$r+''''))
$anchor='    if($IncludeVoucherRequestIdentity) {'
$position=$source.IndexOf($anchor,[StringComparison]::Ordinal)
if($position-lt0){throw 'Owned restore insertion boundary absent'}
$hook=@'
    $valuationOutput=Join-Path $run 'stock-valuation'
    [void](New-Item -ItemType Directory -Path $valuationOutput)
    $valuationExecutable='__EXE__'
    $configuration=[xml]'<configuration><connectionStrings><add name="PharmacyConnection" connectionString="" providerName="System.Data.SqlClient" /></connectionStrings><appSettings><add key="ForceBuildConnectionProfile" value="true" /></appSettings></configuration>'
    $configuration.configuration.connectionStrings.add.connectionString='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10'
    $configuration.Save($valuationExecutable+'.config')
    $valuation=Start-Process -FilePath $valuationExecutable -ArgumentList @($clone,$token,$valuationOutput,'__MODE__','--confirm-owned-temporary-qa-writes') -WorkingDirectory (Split-Path -Parent $valuationExecutable) -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $valuationOutput 'stdout.log') -RedirectStandardError (Join-Path $valuationOutput 'stderr.log')
    $evidence.StockValuationExitCode=$valuation.ExitCode
    $evidence.StockValuationOutput=$valuationOutput
    $evidence.StockValuationMode='__MODE__'
    Get-Content -LiteralPath (Join-Path $valuationOutput 'stdout.log') -Tail 8
    if($valuation.ExitCode-ne0-or(Get-Item -LiteralPath (Join-Path $valuationOutput 'stderr.log')).Length-ne0){Get-Content -LiteralPath (Join-Path $valuationOutput 'stderr.log') -Tail 12;throw 'Owned actual stock valuation probe failed'}
    $valuationText=Get-Content -LiteralPath (Join-Path $valuationOutput 'stdout.log') -Raw
    if(!$valuationText.Contains('PASS STOCK_VALUATION mode=__MODE__')){throw 'Actual valuation outcome summary absent'}
    $evidence.StockValuationExpectedOutcomeVerified=$true
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
    $evidence.StockValuationPostCheckDbPassed=$true
'@
$expectedMode=if($Mode-eq'baseline'){'--expect-overflow'}else{'--expect-valuations'}
$hook=$hook.Replace('__EXE__',$probeExecutable.Replace("'","''")).Replace('__MODE__',$expectedMode)
$source=$source.Insert($position,$hook+[Environment]::NewLine)
$derived=Join-Path $directory 'Test-OwnedStockValuation.ps1'
[IO.File]::WriteAllText($derived,$source,[Text.UTF8Encoding]::new($true))
$tokens=$null;$errors=$null
[void][Management.Automation.Language.Parser]::ParseFile($derived,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Derived owned restore harness parse failed'}
$meta=[ordered]@{State='running';Mode=$Mode;MainSHA256=$main;InfrastructureSHA256=$infra;Directory=$directory;ProcessId=$PID;StartUtcTicks=(Get-Process -Id $PID).StartTime.ToUniversalTime().Ticks;SourceDatabase='PharmacyManagement_qa';RetainedDemoTouched=$false;OutcomeMatched=$false;ProductFixed=$false;OriginalHarnessSHA256=(Get-FileHash -LiteralPath $original).Hash;DerivedHarnessSHA256=(Get-FileHash -LiteralPath $derived).Hash;FullPlanComplete=$false}
$meta|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $metaPath -Encoding utf8
$mutex=$null
try{
 $mutex=& "$r/scripts/Enter-DesktopQaLock.ps1"
 & $derived -ConfirmQaRestore *> (Join-Path $directory 'harness.log')
 if(!$?){throw 'Owned stock valuation restore/check failed'}
 if((Get-FileHash -LiteralPath (Join-Path $release 'project_fo_3.exe')).Hash-ne$main-or(Get-FileHash -LiteralPath (Join-Path $release 'PharmacyManagement.Infrastructure.SqlServer.dll')).Hash-ne$infra){throw 'Valuation runtime changed during run'}
 $meta.State='completed';$meta.OutcomeMatched=$true;$meta.ProductFixed=($Mode-eq'fixed')
 Get-Content -LiteralPath (Join-Path $directory 'harness.log') -Tail 12
}catch{
 $meta.State='failed';$meta.Error=$_.Exception.Message
 if(Test-Path -LiteralPath (Join-Path $directory 'harness.log')){Get-Content -LiteralPath (Join-Path $directory 'harness.log') -Tail 18}
 throw
}finally{
 $meta|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $metaPath -Encoding utf8
 if($mutex){$mutex.ReleaseMutex();$mutex.Dispose()}
}
