[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselineCsv,
    [Parameter(Mandatory=$true)][string]$CandidateCsv,
    [Parameter(Mandatory=$true)][string]$OutputJson
)
$ErrorActionPreference='Stop'
function Read-Samples([string]$path){
    $rows=@(Import-Csv -LiteralPath $path)
    if($rows.Count -eq 0){throw 'Empty performance samples.'}
    foreach($row in $rows){
        foreach($field in @('Scenario','DeviceId','DatasetSha256','Round','ElapsedMs')){
            if([string]::IsNullOrWhiteSpace([string]$row.$field)){throw "Missing sample field: $field"}
        }
        if($row.DatasetSha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Invalid dataset fingerprint.'}
        $round=0; $elapsed=0.0
        if(-not [int]::TryParse($row.Round,[ref]$round) -or $round -lt 1 -or $round -gt 10){throw 'Measured rounds must be 1 through 10; omit warmup rows.'}
        if(-not [double]::TryParse($row.ElapsedMs,[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$elapsed) -or [double]::IsNaN($elapsed) -or [double]::IsInfinity($elapsed) -or $elapsed -le 0){throw 'ElapsedMs must be finite and positive.'}
        $row.Round=$round; $row.ElapsedMs=$elapsed
    }
    return $rows
}
function Metrics($rows){
    if(@($rows).Count -ne 10 -or @($rows.Round|Sort-Object -Unique).Count -ne 10){throw 'Each scenario requires exactly ten unique measured rounds.'}
    $sorted=@($rows.ElapsedMs|Sort-Object)
    return @{Median=($sorted[4]+$sorted[5])/2;P95=$sorted[9]}
}
$baseline=@(Read-Samples $BaselineCsv);$candidate=@(Read-Samples $CandidateCsv)
$names=@($baseline.Scenario|Sort-Object -Unique);$other=@($candidate.Scenario|Sort-Object -Unique)
if([string]::Join("`n",$names) -cne [string]::Join("`n",$other)){throw 'Scenario sets differ.'}
$results=@(foreach($name in $names){
    $before=@($baseline|Where-Object Scenario -CEQ $name);$after=@($candidate|Where-Object Scenario -CEQ $name)
    if(@(($before+$after).DeviceId|Sort-Object -Unique).Count -ne 1 -or @(($before+$after).DatasetSha256|ForEach-Object {$_.ToUpperInvariant()}|Sort-Object -Unique).Count -ne 1){throw 'Device or dataset differs.'}
    $b=Metrics $before;$c=Metrics $after
    $median=100*($c.Median/$b.Median-1);$p95=100*($c.P95/$b.P95-1)
    [ordered]@{Scenario=$name;BaselineMedianMs=$b.Median;CandidateMedianMs=$c.Median;BaselineP95Ms=$b.P95;CandidateP95Ms=$c.P95;MedianRegressionPercent=$median;P95RegressionPercent=$p95;Passed=($median -le 15 -and $p95 -le 15)}
})
$passed=@($results|Where-Object { -not $_.Passed }).Count -eq 0
[ordered]@{Passed=$passed;P95Method='nearest rank';MeasuredRounds=10;ThresholdPercent=15;WarmupAndMeasurementAuthenticityRequireExternalEvidence=$true;Results=$results}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $OutputJson -Encoding utf8
if(-not $passed){throw 'Performance regression exceeds 15 percent.'}
Write-Output 'PASS performance sample comparison; measurement provenance and warmup require independent evidence.'