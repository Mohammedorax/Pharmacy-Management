<#
.SYNOPSIS
    Runs the standard quality gate on a schedule and keeps a bounded, dated evidence trail.

.DESCRIPTION
    The repository quality gate (scripts\Verify-UiQuality.ps1) failed silently for two days
    because nothing ran it on a schedule. This watcher runs it, stores the exit code and the
    captured output under a dated folder, keeps only the newest runs, and can register or
    remove a scheduled task for the current user.

    It never touches production data and never deletes sources, rules, acceptance evidence or
    packages: only the folders this watcher itself created are pruned, and only above the
    retained count.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Invoke-QualityGateWatch.ps1
    powershell -ExecutionPolicy Bypass -File scripts\Invoke-QualityGateWatch.ps1 -RegisterTask
#>
[CmdletBinding()]
param(
    [string]$EvidenceRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\quality-gate-watch'),
    [int]$RetainedRuns = 3,
    [switch]$InteractiveSmoke,
    [switch]$RegisterTask,
    [switch]$UnregisterTask,
    [switch]$PruneOnly,
    [string]$TaskName = 'PharmacyManagement-QualityGate'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$gateScript = Join-Path $PSScriptRoot 'Verify-UiQuality.ps1'

if ($UnregisterTask) {
    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Output "REMOVED $TaskName"
    }
    else {
        Write-Output "ABSENT $TaskName"
    }
    return
}

if ($RegisterTask) {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $PSCommandPath)
    $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 9am
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Hours 4)
    $principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -RunLevel Limited
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
        -Description 'Runs the pharmacy management quality gate and keeps the last three results.' -Force | Out-Null
    Write-Output "REGISTERED $TaskName (weekly, Sunday 09:00, current user, no elevation)"
    return
}

if (-not (Test-Path -LiteralPath $gateScript)) { throw "لم يوجد مشغل البوابة: $gateScript" }
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null

function Remove-OldRuns {
    # Prune only the dated folders this watcher created, and only above the retained count.
    $runs = @(Get-ChildItem -LiteralPath $EvidenceRoot -Directory | Where-Object { $_.Name -match '^\d{8}-\d{6}$' } | Sort-Object Name -Descending)
    $retained = @($runs | Select-Object -First $RetainedRuns)
    foreach ($folder in $runs) {
        if ($retained.Name -contains $folder.Name) { continue }
        Remove-Item -LiteralPath $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    return $retained.Count
}
if ($PruneOnly) {
    Write-Output ("PRUNED kept={0} retained={1}" -f (Remove-OldRuns), $RetainedRuns)
    return
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDirectory = Join-Path $EvidenceRoot $stamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$stdoutPath = Join-Path $runDirectory 'gate.stdout.log'
$stderrPath = Join-Path $runDirectory 'gate.stderr.log'
$started = Get-Date
$timer = [Diagnostics.Stopwatch]::StartNew()

$arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $gateScript)
if (-not $InteractiveSmoke) { $arguments += '-SkipInteractiveSmoke' }
# The scheduled profile also proves the owned live SQL contract.
$arguments += @('-IncludeQaOwnedSalesIdempotency')
# Windows PowerShell 5.1 reads a UTF-8 script without a BOM as ANSI, which mangles the Arabic
# messages inside the gate. PowerShell 7 reads UTF-8 by default, so it is preferred here.
$shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }
$process = Start-Process -FilePath $shell -ArgumentList $arguments -WorkingDirectory $repositoryRoot `
    -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
$exited = $process.WaitForExit(4 * 60 * 60 * 1000)
if (-not $exited) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    $process.WaitForExit()
}
$timer.Stop()
$exitCode = if ($exited) { $process.ExitCode } else { 'TIMEOUT' }
$finished = Get-Date

$result = [pscustomobject]@{
    StartedAt = $started.ToString('o')
    FinishedAt = $finished.ToString('o')
    ElapsedMilliseconds = $timer.ElapsedMilliseconds
    ExitCode = $exitCode
    InteractiveSmoke = [bool]$InteractiveSmoke
    Switches = @('-SkipInteractiveSmoke', '-IncludeQaOwnedSalesIdempotency')
    Repository = $repositoryRoot
    GateScript = $gateScript
    Stdout = $stdoutPath
    Stderr = $stderrPath
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDirectory 'gate.result.json') -Encoding UTF8
[void](Remove-OldRuns)

Write-Output ("GATE exit={0} elapsed_ms={1} run={2}" -f $exitCode, $timer.ElapsedMilliseconds, $runDirectory)
if ($exitCode -ne 0) {
    Get-Content -LiteralPath $stderrPath -Tail 20 -ErrorAction SilentlyContinue
    exit 1
}
exit 0
