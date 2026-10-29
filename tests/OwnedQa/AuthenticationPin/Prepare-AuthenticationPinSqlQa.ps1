param([string]$VerifiedBackupJobPath = "$PSScriptRoot/output/login-edited-demo-backup-job.json")
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path ($PSScriptRoot+'/output') -Force | Out-Null
$root='D:/Comp/Pharmacy-Management-master'
$prior=Get-Content $VerifiedBackupJobPath -Raw|ConvertFrom-Json
if(!$prior.Accepted-or(Get-FileHash -LiteralPath $prior.BackupPath).Hash-cne$prior.BackupSHA256){throw 'Verified immutable owned backup required'}
$owner=[guid]::NewGuid().ToString('N')
$stage="$root/artifacts/authentication-pin-"+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+$owner.Substring(0,8)
New-Item -ItemType Directory $stage|Out-Null
$databases=@()
foreach($side in @('A','B')){
 $database='PharmacyAuthPin_'+$owner+'_'+$side+'_qa'
 $s=[IO.File]::ReadAllText("$root/scripts/Restore-CompanyDemoSafely.ps1")
 $s=$s.Replace("`$target='PharmacyCompanyDemo_20261008_4925d6ed_qa'",("`$target='"+$database+"'")).Replace('4925d6edb1644f348ae7dd9c4594170a',$prior.Owner)
 $script="$stage/Restore-$side.ps1";[IO.File]::WriteAllText($script,$s,[Text.UTF8Encoding]::new($false))
 & $script -BackupPath $prior.BackupPath -ExpectedSHA256 $prior.BackupSHA256 -ConfirmIndependentDemoRestore *> "$stage/restore-$side.log"
 if(!$?){throw 'Independent auth database restore failed'}
 $c=[Data.SqlClient.SqlConnection]::new("Data Source=.;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False")
 try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText="EXEC sys.sp_addextendedproperty @name=N'QaAuthPin.Owner',@value=@owner";[void]$cmd.Parameters.AddWithValue('@owner',$owner);[void]$cmd.ExecuteNonQuery();$cmd.Dispose()}finally{$c.Dispose()}
 $databases+=$database
}
@{Directory=$stage;Owner=$owner;Databases=$databases;BackupPath=$prior.BackupPath;BackupSHA256=$prior.BackupSHA256;OriginalSource=$prior.Source;ProductionTouched=$false;FullPlanComplete=$false}|ConvertTo-Json|Set-Content "$PSScriptRoot/output/authentication-pin-sql-qa-job.json" -Encoding utf8
Write-Output "PASS two independent owned authentication QA clones: $stage"
