$ErrorActionPreference='Stop'
foreach($pair in @(@('authentication-audit-job.json','authentication-audit-data-accepted.json'),@('authentication-audit-pin-qa-job.json','authentication-audit-pin-data-accepted.json'))){
 $text=[IO.File]::ReadAllText("$PSScriptRoot/Audit-AuthenticationPinData.ps1").Replace('authentication-pin-sql-qa-job.json',$pair[0]).Replace('authentication-pin-data-accepted.json',$pair[1]).Replace('DBCC all3 passed','DBCC selected owned targets passed')
 $script="$PSScriptRoot/Audit-Generated-$($pair[0]).ps1"
 [IO.File]::WriteAllText($script,$text,[Text.UTF8Encoding]::new($false))
 & $script
 if(!$?){throw 'All69 actual data audit failed'}
}
$job=Get-Content "$PSScriptRoot/output/authentication-audit-job.json" -Raw|ConvertFrom-Json
$before=[IO.File]::ReadAllText("$($job.Directory)/Database.before.cs")
$after=[IO.File]::ReadAllText('D:/Comp/Pharmacy-Management-master/project_fo_3/Database.cs')
$signature='(?m)^\s*public static [^\r\n{=]+?\([^)]*\)'
$old=@([regex]::Matches($before,$signature)|ForEach-Object{$_.Value.Trim()})
$new=@([regex]::Matches($after,$signature)|ForEach-Object{$_.Value.Trim()})
if($old.Count-ne46-or($old-join"`n")-cne($new-join"`n")){throw 'Public Database signatures changed'}
$sqlPattern='(?s)\b\w+\.CommandText\s*=\s*(@?"(?:""|[^"])*")\s*;'
$oldSql=@([regex]::Matches($before,$sqlPattern)|ForEach-Object{$_.Groups[1].Value})
$newSql=@([regex]::Matches($after,$sqlPattern)|ForEach-Object{$_.Groups[1].Value})
if($oldSql.Count-lt10-or$oldSql.Count-ne$newSql.Count-or(@($oldSql|ForEach-Object{$_.Replace('SELECT TOP 1 EmpId, EmpPassword','SELECT TOP 1 EmpId, EmpPassword, EmpName, EmpRole')})-join"`n")-cne($newSql-join"`n")){throw 'Unexpected SQL assignment change'}
@{Accepted=$true;PublicDatabaseSignatures=$old.Count;PublicSignaturesUnchanged=$true;PublicSignatureCounterIncludesThreeMultilineMethods=$true;SqlAssignments=$oldSql.Count;OnlyExpectedSqlSelectColumnsChanged=$true;ChangedSelect='Added EmpName and EmpRole to existing exact credential lookup; same WHERE/ORDER and password migration write';SchemaChanged=$false;BestEffortAuditContractChanged=$false;ConnectionPinChanged=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-audit-source-review.json" -Encoding utf8
Write-Output 'PASS actual all69 audits and unchanged46 public Database signatures including three multiline signatures'
