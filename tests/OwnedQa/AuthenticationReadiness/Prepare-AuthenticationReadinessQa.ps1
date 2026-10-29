$ErrorActionPreference='Stop'
$text=[IO.File]::ReadAllText("$PSScriptRoot/Prepare-AuthenticationPinSqlQa.ps1")
$text=$text.Replace("foreach(`$side in @('A','B'))","foreach(`$side in @('A','B','C'))").Replace('PharmacyAuthPin_','PharmacyAuthReady_').Replace('QaAuthPin.Owner','QaAuthReady.Owner').Replace('artifacts/authentication-pin-','artifacts/authentication-readiness-').Replace('authentication-pin-sql-qa-job.json','authentication-readiness-qa-job.json').Replace('two independent owned authentication QA clones','three independent owned readiness QA clones')
[IO.File]::WriteAllText("$PSScriptRoot/Prepare-AuthenticationReadinessQa-Generated.ps1",$text,[Text.UTF8Encoding]::new($false))
& "$PSScriptRoot/Prepare-AuthenticationReadinessQa-Generated.ps1"
if(!$?){throw 'Owned readiness preparation failed'}
$job=Get-Content "$PSScriptRoot/output/authentication-readiness-qa-job.json" -Raw|ConvertFrom-Json
$c=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog='+$job.Databases[2]+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False')
try{$c.Open();$q=$c.CreateCommand();$q.CommandText='SELECT CurrencyCode,IsSystemCurrency FROM dbo.CurrencySettingsTb ORDER BY CurrencyCode';$dt=[Data.DataTable]::new();$a=[Data.SqlClient.SqlDataAdapter]::new($q);[void]$a.Fill($dt);$dt|Format-Table -AutoSize;$a.Dispose();$q.Dispose()}finally{$c.Dispose()}
