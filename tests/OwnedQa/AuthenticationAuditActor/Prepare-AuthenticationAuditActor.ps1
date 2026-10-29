$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$backup=Get-Content "$PSScriptRoot/output/login-edited-demo-backup-job.json" -Raw|ConvertFrom-Json
if(!$backup.Accepted-or(Get-FileHash -LiteralPath $backup.BackupPath).Hash-cne$backup.BackupSHA256){throw 'Verified original backup required'}
$owner=[guid]::NewGuid().ToString('N');$database='PharmacyAuthAudit_'+$owner+'_qa'
$stage="$root/artifacts/authentication-audit-"+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+$owner.Substring(0,8)
[void](New-Item -ItemType Directory $stage)
$restore=[IO.File]::ReadAllText("$root/scripts/Restore-CompanyDemoSafely.ps1").Replace("`$target='PharmacyCompanyDemo_20261008_4925d6ed_qa'",("`$target='"+$database+"'")).Replace('4925d6edb1644f348ae7dd9c4594170a',$backup.Owner)
[IO.File]::WriteAllText("$stage/Restore.ps1",$restore,[Text.UTF8Encoding]::new($false))
& "$stage/Restore.ps1" -BackupPath $backup.BackupPath -ExpectedSHA256 $backup.BackupSHA256 -ConfirmIndependentDemoRestore *> "$stage/restore.log"
if(!$?){throw 'Fresh owned QA restore failed'}
$c=[Data.SqlClient.SqlConnection]::new("Data Source=.;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False")
try{$c.Open();$q=$c.CreateCommand();$q.CommandText="EXEC sys.sp_addextendedproperty @name=N'QaAuthenticationAudit.Owner',@value=@owner";[void]$q.Parameters.AddWithValue('@owner',$owner);[void]$q.ExecuteNonQuery();$q.Dispose()}finally{$c.Dispose()}
@{Directory=$stage;Owner=$owner;Database=$database;Databases=@($database);OriginalSource=$backup.Source;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-audit-job.json" -Encoding utf8
Write-Output "PASS fresh owned audit QA: $stage"
