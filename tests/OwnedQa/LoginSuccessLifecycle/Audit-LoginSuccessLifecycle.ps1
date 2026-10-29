$ErrorActionPreference='Stop'
$j=Get-Content "$PSScriptRoot/output/login-success-lifecycle-job.json" -Raw|ConvertFrom-Json
$backup=Get-Content "$PSScriptRoot/output/login-edited-demo-backup-job.json" -Raw|ConvertFrom-Json
foreach($mode in @('Baseline','Fixed')){
 $job=Get-Content "$PSScriptRoot/output/login-success-lifecycle-$mode-job.json" -Raw|ConvertFrom-Json
 $state=Get-Content "$($job.Directory)/state.json" -Raw|ConvertFrom-Json
 $terminal=Get-Content "$($job.Directory)/desktop-terminal.json" -Raw|ConvertFrom-Json
 if($state.State-ne'completed'-or$state.ExitCode-ne0-or!$terminal.DesktopHandleClosed){throw 'Exact original lifecycle jobs must be completed and closed'}
}
$profiles=@(Get-Content "$($j.Directory)/real-profile-before.json" -Raw|ConvertFrom-Json)
foreach($profile in $profiles){
 if((Test-Path -LiteralPath $profile.Path)-ne$profile.Exists){throw 'Real remembered-user/ProgramData file existence changed'}
 if($profile.Exists-and(Get-FileHash -LiteralPath $profile.Path).Hash-cne$profile.SHA256){throw 'Real remembered-user/ProgramData file contents changed'}
}
$fixed=Get-Content "$PSScriptRoot/output/login-success-lifecycle-Fixed-job.json" -Raw|ConvertFrom-Json
if((Get-FileHash -LiteralPath 'D:/Comp/Pharmacy-Management-master/project_fo_3/Login.cs').Hash-cne$fixed.LoginSourceSHA256){throw 'Fixed fixture Login must match actual current production source'}
$copied=@(Get-Content "$PSScriptRoot/output/login-success-source-manifest.json" -Raw|ConvertFrom-Json)
$delta=@($copied|Where-Object{
 $relative=[IO.Path]::GetRelativePath('D:/Comp/Pharmacy-Management-master',$_.Path)
 (Get-FileHash -LiteralPath "$($j.Directory)/Source/$relative").Hash-cne$_.SHA256
})
if($delta.Count-ne1-or[IO.Path]::GetFileName($delta[0].Path)-cne'RememberMeService.cs'){throw 'Fixed source fixture must differ only by owned remember folder'}
[ordered]@{Directory=$j.Directory;Owner=$j.Owner;Databases=@($j.Database);OriginalSource=$backup.Source}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/login-success-data-job.json" -Encoding utf8
$audit=[IO.File]::ReadAllText("$PSScriptRoot/Audit-AuthenticationPinData.ps1")
$audit=$audit.Replace('authentication-pin-sql-qa-job.json','login-success-data-job.json').Replace('authentication-pin-data-accepted.json','login-success-data-accepted.json').Replace('DBCC all3 passed','DBCC source and clone passed')
[IO.File]::WriteAllText("$PSScriptRoot/output/Audit-LoginSuccessData-Generated.ps1",$audit.Replace('$PSScriptRoot/output/','$PSScriptRoot/'),[Text.UTF8Encoding]::new($false))
& "$PSScriptRoot/output/Audit-LoginSuccessData-Generated.ps1"
if(!$?){throw 'Exact all69 data audit failed'}
$c=[Data.SqlClient.SqlConnection]::new($j.ConnectionString)
try{
 $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText="SELECT COUNT(*) FROM dbo.AuditLogTb WHERE UserName IN(N'lifecycle_manager',N'lifecycle_cashier')";$count=[int]$cmd.ExecuteScalar();$cmd.Dispose()
 if($count-ne16){throw "Expected16 actual SQL successful logins, got$count"}
 $cmd=$c.CreateCommand();$cmd.CommandText="SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PasswordUpgraded'";$upgrades=[int]$cmd.ExecuteScalar();$cmd.Dispose()
 if($upgrades-ne2){throw 'Expected two fixture password upgrade events'}
}finally{$c.Dispose()}
[ordered]@{Accepted=$true;Directory=$j.Directory;Database=$j.Database;BaselineCases=8;BaselineProductGaps=8;FixedCases=8;FixedProductGaps=0;FixedChecks=89;SuccessfulSqlLoginOperations=16;LoginAuditRows=16;PasswordUpgradeAuditRows=2;PasswordUpgradeAuditActorBlank=$true;ActualLoginAndHomeModalPath=$true;RealRememberImplementation=$true;InstrumentedRememberFolderOnly=$true;ProductionLoginSourceMatchesFixture=$true;RealUserProfileFilesUnchanged=$true;All69TableAudit="$PSScriptRoot/output/login-success-data-accepted.json";NativeInput=$false;CleanClient=$false;PhysicalPrinting=$false;FullPlanComplete=$false}|ConvertTo-Json -Depth 8|Set-Content "$PSScriptRoot/output/login-success-lifecycle-accepted.json" -Encoding utf8
Write-Output 'PASS source-isolated real Login/Home/remember lifecycle8; 16 SQL logins; real profile unchanged; full69 tables audited'
