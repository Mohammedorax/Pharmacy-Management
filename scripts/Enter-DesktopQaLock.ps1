# One Windows desktop cannot independently own focus for simultaneous UI test jobs.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$mutex=[Threading.Mutex]::new($false,'Local\PharmacyManagement.DesktopQaUi')
try {
    if(!$mutex.WaitOne(0)){throw 'Another Pharmacy desktop QA job owns the UI. Wait for its terminal result before starting this job.'}
    return $mutex
} catch [Threading.AbandonedMutexException] {
    $mutex.ReleaseMutex(); $mutex.Dispose()
    throw 'Previous desktop QA owner exited unexpectedly. Inspect its process and windows before retrying.'
} catch {
    $mutex.Dispose()
    throw
}
