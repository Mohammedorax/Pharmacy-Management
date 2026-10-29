[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$directory=[IO.Path]::GetFullPath($EvidenceDirectory)
$culture=[Globalization.CultureInfo]::InvariantCulture
function Read-Number([string]$Value) {
    $number=[double]::Parse($Value,$culture)
    if([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt 0){throw 'Invalid nonnegative sample'}
    return $number
}
function Quantiles([double[]]$Values) {
    $ordered=@($Values | Sort-Object)
    if($ordered.Count -ne 10){throw 'Each measurement group must have ten samples'}
    [pscustomobject]@{MedianMs=($ordered[4]+$ordered[5])/2;P95Ms=$ordered[[math]::Ceiling(0.95*$ordered.Count)-1];MinimumMs=$ordered[0];MaximumMs=$ordered[9]}
}
$result=Get-Content -LiteralPath (Join-Path $directory 'process-result.json') -Raw | ConvertFrom-Json
if($null -ne $result.ExitCode -and $result.ExitCode -ne 0){throw 'Benchmark process failed'}
if($result.ExitCode -eq $null -and -not $result.TerminalSuccessLog){throw 'Missing authoritative terminal completion evidence'}
$all=@(Import-Csv -LiteralPath (Join-Path $directory 'samples.csv'))
if($all.Count -ne 7056){throw "Expected 7056 samples (two warmups and ten full rounds), got $($all.Count)"}
$allowedStages=@('Construct','ShowAndDispatch','SummaryBindAndDispatch','PresentationAndDispatch','NativeResizeLayoutAndDispatch','NativeClientMeasure','ExplicitLayoutAndDispatch','SummaryBitmapCapture','ThemeAndDispatch')
foreach($sample in $all) {
    if($sample.stage -notin $allowedStages -or [int]$sample.screen_index -notin 0..6 -or $sample.theme -notin @('Light','Dark') -or $sample.direction -notin @('RTL','LTR')){throw 'Unknown measurement case or stage'}
    if(($sample.kind -eq 'warmup') -ne ([int]$sample.round -le 0)){throw 'Incorrect warmup classification'}
    $null=Read-Number $sample.elapsed_ms
    $null=Read-Number $sample.cpu_ms
}
$measured=@($all | Where-Object {$_.kind -eq 'measured'})
if($measured.Count -ne 5880){throw 'Expected 5880 measured samples'}
foreach($round in -1..10) {
    $samples=@($all | Where-Object {[int]$_.round -eq $round})
    if($samples.Count -ne 588){throw "Incomplete round $round"}
    foreach($variant in ($samples | Group-Object screen_index,theme,direction)) {
        if($variant.Count -ne 21){throw 'Incomplete screen/theme/direction matrix'}
        $sized=@($variant.Group | Where-Object {$_.stage -in @('NativeResizeLayoutAndDispatch','NativeClientMeasure','ExplicitLayoutAndDispatch','SummaryBitmapCapture')})
        if($sized.Count -ne 16){throw 'Missing native size stage'}
        foreach($size in @('640,480','960,640','1366,768','1920,1080')) {
            $parts=$size.Split(',');if(@($sized | Where-Object {$_.width -eq $parts[0] -and $_.height -eq $parts[1]}).Count -ne 4){throw "Missing size $size"}
        }
    }
    if(@($samples | Group-Object screen_index,theme,direction).Count -ne 28){throw 'Expected 28 screen variants'}
}
$statistics=@(foreach($group in ($measured | Group-Object screen_index,screen,theme,direction,stage,width,height)) {
    if($group.Count -ne 10 -or @($group.Group.round | Sort-Object -Unique).Count -ne 10){throw 'Repeated or missing measurement round'}
    $values=@($group.Group | ForEach-Object {Read-Number $_.elapsed_ms})
    $stats=Quantiles $values
    $first=$group.Group[0]
    [pscustomobject]@{ScreenIndex=[int]$first.screen_index;Screen=$first.screen;Theme=$first.theme;Direction=$first.direction;Stage=$first.stage;Width=[int]$first.width;Height=[int]$first.height;Samples=10;MedianMs=$stats.MedianMs;P95Ms=$stats.P95Ms;MinimumMs=$stats.MinimumMs;MaximumMs=$stats.MaximumMs;MeanCpuMs=($group.Group | ForEach-Object {Read-Number $_.cpu_ms} | Measure-Object -Average).Average;LayoutEvents=($group.Group.layout_events | Measure-Object -Sum).Sum;PaintEvents=($group.Group.paint_events | Measure-Object -Sum).Sum;GC0=($group.Group.gc0 | Measure-Object -Sum).Sum;GC1=($group.Group.gc1 | Measure-Object -Sum).Sum;GC2=($group.Group.gc2 | Measure-Object -Sum).Sum}
})
if($statistics.Count -ne 588){throw 'Expected 588 independent measurement groups'}
$statistics | Export-Csv -LiteralPath (Join-Path $directory 'statistics.csv') -NoTypeInformation -Encoding utf8
$roundTotals=@(foreach($round in 1..10){$rows=@($measured | Where-Object {[int]$_.round -eq $round});[pscustomobject]@{Round=$round;Order=if($round%2 -eq 0){'Reverse'}else{'Forward'};ElapsedMs=($rows | ForEach-Object {Read-Number $_.elapsed_ms} | Measure-Object -Sum).Sum;CpuMs=($rows | ForEach-Object {Read-Number $_.cpu_ms} | Measure-Object -Sum).Sum}})
$roundTotals | Export-Csv -LiteralPath (Join-Path $directory 'round-totals.csv') -NoTypeInformation -Encoding utf8
$stages=@(foreach($group in ($measured | Group-Object stage)) {[pscustomobject]@{Stage=$group.Name;Samples=$group.Count;TotalMs=($group.Group | ForEach-Object {Read-Number $_.elapsed_ms} | Measure-Object -Sum).Sum;CpuMs=($group.Group | ForEach-Object {Read-Number $_.cpu_ms} | Measure-Object -Sum).Sum;LayoutEvents=($group.Group.layout_events | Measure-Object -Sum).Sum;PaintEvents=($group.Group.paint_events | Measure-Object -Sum).Sum}})
$hashes=@(Get-Content -LiteralPath (Join-Path $directory 'before-hashes.json') -Raw | ConvertFrom-Json)
foreach($hash in $hashes){if((Get-FileHash -LiteralPath $hash.Path -Algorithm SHA256).Hash -ne $hash.Hash){throw 'Application/source hash changed during benchmark'}}
$resources=@(Import-Csv -LiteralPath (Join-Path $directory 'resource-monitor.csv'))
if($resources.Count -lt 2){throw 'Missing resource observation'}
$hostCpu=@($resources | Where-Object {$_.HostCpuPercent -ne ''} | ForEach-Object {Read-Number $_.HostCpuPercent})
$totals=Quantiles @($roundTotals.ElapsedMs)
[ordered]@{Status='MeasurementComplete';ObservedExitCode=$result.ExitCode;ExitCodeObservation=$result.ExitCodeObservation;RawSamples=$all.Count;MeasuredSamples=$measured.Count;WarmupRounds=2;MeasurementRounds=10;ScreenVariants=28;NativeSizes=4;Groups=$statistics.Count;RoundMedianMs=$totals.MedianMs;RoundP95Ms=$totals.P95Ms;HostCpuMeanPercent=($hostCpu | Measure-Object -Average).Average;HostCpuMaxPercent=($hostCpu | Measure-Object -Maximum).Maximum;ResourcesObserved=$resources.Count;SourceHashesStable=$true;StageTotals=$stages;PerformanceAcceptance='Not established: no before/after product baseline or user-workflow budget';PaintLimit='Summary DrawToBitmap CPU capture only; offscreen Paint events do not prove monitor/GPU paint performance';CompositeStageLimit='Resize, theme, presentation and explicit layout include message dispatch and triggered layout/paint';Cases='Preview forms with loadData=false, fixed values, no SQL or native input';OriginalJournalPrintFlake='Unresolved'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'analysis.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $directory 'analysis.json') -Raw