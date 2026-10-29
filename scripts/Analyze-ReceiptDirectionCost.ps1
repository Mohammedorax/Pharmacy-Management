[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$directory=[IO.Path]::GetFullPath($EvidenceDirectory)
$result=Get-Content -LiteralPath (Join-Path $directory 'process-result.json') -Raw | ConvertFrom-Json
if($result.ExitCode -ne 0){throw 'Receipt direction benchmark did not exit successfully'}
$rows=@(Import-Csv -LiteralPath (Join-Path $directory 'direction-samples.csv'))
if($rows.Count -ne 144){throw 'Expected 144 rows: two warmups and ten rounds for four variants and three stages'}
$columns=@('initial_theme','direction','round','kind','stage','elapsed_ms','handle_created','handle_destroyed','layout_events')
$actualColumns=@($rows[0].PSObject.Properties.Name)
if($actualColumns.Count -ne $columns.Count -or @($columns | Where-Object {$_ -cnotin $actualColumns}).Count -ne 0){throw 'Benchmark CSV schema changed'}
$stages=@('RepeatedPresentation','RoundTripPresentation','ThemeAndDispatch')
function Number([string]$value){$n=[double]::Parse($value,[Globalization.CultureInfo]::InvariantCulture);if([double]::IsNaN($n)-or [double]::IsInfinity($n)-or $n -lt 0){throw 'Invalid sample number'};return $n}
foreach($row in $rows){
 if($row.round -notmatch '^-?\d+$' -or $row.kind -notin @('warmup','measured')){throw 'Invalid round or sample kind'}
 if($row.initial_theme -notin @('Light','Dark') -or $row.direction -notin @('RTL','LTR') -or $row.stage -notin $stages -or [int]$row.round -notin -1..10){throw 'Unknown benchmark variant'}
 if(($row.kind -eq 'warmup') -ne ([int]$row.round -le 0)){throw 'Invalid warmup classification'}
 $null=Number $row.elapsed_ms
 foreach($counter in @('handle_created','handle_destroyed','layout_events')){if($row.$counter -notmatch '^\d+$' -or [int]$row.$counter -lt 0){throw 'Invalid event count'}}
}
foreach($group in ($rows | Group-Object initial_theme,direction,stage)){
 if($group.Count -ne 12 -or @($group.Group | ForEach-Object {[int]$_.round} | Sort-Object -Unique).Count -ne 12){throw 'Missing or duplicated rounds'}
}
$measured=@($rows | Where-Object kind -EQ 'measured')
if($measured.Count -ne 120){throw 'Expected 120 measured rows'}
$statistics=@(foreach($group in ($measured | Group-Object initial_theme,direction,stage)){
 if($group.Count -ne 10){throw 'Expected ten measured samples'}
 $values=@($group.Group | ForEach-Object {Number $_.elapsed_ms} | Sort-Object)
 $first=$group.Group[0]
 [pscustomobject]@{Theme=$first.initial_theme;Direction=$first.direction;Stage=$first.stage;Samples=10;MedianMs=($values[4]+$values[5])/2;P95Ms=$values[9];MinimumMs=$values[0];MaximumMs=$values[9];TotalHandleCreated=($group.Group.handle_created | Measure-Object -Sum).Sum;TotalHandleDestroyed=($group.Group.handle_destroyed | Measure-Object -Sum).Sum;TotalLayoutEvents=($group.Group.layout_events | Measure-Object -Sum).Sum}
})
if($statistics.Count -ne 12){throw 'Missing measurement groups'}
foreach($hash in (Get-Content -LiteralPath (Join-Path $directory 'before-hashes.json') -Raw | ConvertFrom-Json)){if((Get-FileHash -LiteralPath $hash.Path).Hash -cne $hash.Hash){throw 'Product/source changed while measuring'}}
$statistics | Export-Csv -LiteralPath (Join-Path $directory 'statistics.csv') -NoTypeInformation -Encoding UTF8
[pscustomobject]@{Status='MeasurementComplete';ExitCode=0;RawRows=144;MeasuredRows=120;WarmupRounds=2;MeasuredRounds=10;Groups=12;SourceHashesStable=$true;Scope='ReceiptExternal cheque sidebar, 960x600, two themes/directions; no SQL/native input';GeneralPerformanceAccepted=$false;Statistics=$statistics} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'analysis.json') -Encoding UTF8
$statistics