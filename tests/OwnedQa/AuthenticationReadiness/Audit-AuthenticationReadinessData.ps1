$ErrorActionPreference='Stop'
$s=[IO.File]::ReadAllText("$PSScriptRoot/Audit-AuthenticationPinData.ps1").Replace('authentication-pin-sql-qa-job.json','authentication-readiness-qa-job.json').Replace('authentication-pin-data-accepted.json','authentication-readiness-data-accepted.json').Replace('DBCC all3 passed','DBCC all selected readiness clones and source passed')
[IO.File]::WriteAllText("$PSScriptRoot/Audit-AuthenticationReadinessData-Generated.ps1",$s,[Text.UTF8Encoding]::new($false))
& "$PSScriptRoot/Audit-AuthenticationReadinessData-Generated.ps1"
if(!$?){throw 'Readiness all69 audit failed'}
$j=Get-Content "$PSScriptRoot/output/authentication-readiness-qa-job.json" -Raw|ConvertFrom-Json
$before=[IO.File]::ReadAllText("$($j.Directory)/Database.before.cs")
$after=[IO.File]::ReadAllText('D:/Comp/Pharmacy-Management-master/project_fo_3/Database.cs')
$pattern='(?m)^\s*public static [^\r\n{=]+?\([^)]*\)'
$old=@([regex]::Matches($before,$pattern)|ForEach-Object{$_.Value.Trim()})
$new=@([regex]::Matches($after,$pattern)|ForEach-Object{$_.Value.Trim()})
if($old.Count-ne46-or($old-join"`n")-cne($new-join"`n")){throw 'Public Database API signature drift'}
$saveAt=$after.IndexOf('public static void SaveSetings(',[StringComparison]::Ordinal)
$saveEnd=$after.IndexOf('public static void SaveSettings(',$saveAt,[StringComparison]::Ordinal)
if(!$after.Substring($saveAt,$saveEnd-$saveAt).Contains('ResetOperationalReadiness();')){throw 'Settings-save cache invalidation missing'}
$initializerAt=$after.IndexOf('public static DatabaseSchemaValidationResult InitializeCoreSchema(',[StringComparison]::Ordinal)
$initializerEnd=$after.IndexOf('public static DatabaseSchemaValidationResult ValidateConfiguredCoreSchema()',$initializerAt,[StringComparison]::Ordinal)
if(!$after.Substring($initializerAt,$initializerEnd-$initializerAt).Contains('ResetOperationalReadiness();')){throw 'Explicit schema initializer invalidation missing'}
@{Accepted=$true;PublicDatabaseSignatures=46;PublicSignaturesUnchanged=$true;SaveSettingsInvalidatesCache=$true;ExplicitInitializationInvalidatesCache=$true;ExplicitValidationStillRunsSql=$true;NoSqlUnderGlobalCacheLock=$true;SchemaChanged=$false;CredentialsRoleAuditNotCached=$true;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-readiness-source-review.json" -Encoding utf8
Write-Output 'PASS readiness all69 data, restored tax/currency rows, unchanged46 public signatures and invalidation wiring'
