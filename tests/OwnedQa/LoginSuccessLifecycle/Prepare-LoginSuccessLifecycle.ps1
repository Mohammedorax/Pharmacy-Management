$ErrorActionPreference='Stop'
$root='D:/Comp/Pharmacy-Management-master'
$accepted=Get-Content "$PSScriptRoot/output/authentication-pin-units-delivery-accepted.json" -Raw|ConvertFrom-Json
$backup=Get-Content "$PSScriptRoot/output/login-edited-demo-backup-job.json" -Raw|ConvertFrom-Json
if(!$accepted.Accepted-or(Get-FileHash -LiteralPath $backup.BackupPath).Hash-cne$backup.BackupSHA256){throw 'Verified original backup/source required'}
$owner=[guid]::NewGuid().ToString('N')
$stage="$root/artifacts/company-demo-20261008/login-success-lifecycle-"+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+$owner.Substring(0,8)
[void](New-Item -ItemType Directory $stage)
$database='PharmacyLoginLifecycle_'+$owner+'_qa'
$restore=[IO.File]::ReadAllText("$root/scripts/Restore-CompanyDemoSafely.ps1").Replace("`$target='PharmacyCompanyDemo_20261008_4925d6ed_qa'",("`$target='"+$database+"'")).Replace('4925d6edb1644f348ae7dd9c4594170a',$backup.Owner)
[IO.File]::WriteAllText("$stage/Restore.ps1",$restore,[Text.UTF8Encoding]::new($false))
& "$stage/Restore.ps1" -BackupPath $backup.BackupPath -ExpectedSHA256 $backup.BackupSHA256 -ConfirmIndependentDemoRestore *> "$stage/restore.log"
if(!$?){throw 'Owned restore failed'}
$connection="Data Source=.;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False"
$c=[Data.SqlClient.SqlConnection]::new($connection)
try{
 $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText="EXEC sys.sp_addextendedproperty @name=N'QaLoginLifecycle.Owner',@value=@owner";[void]$cmd.Parameters.AddWithValue('@owner',$owner);[void]$cmd.ExecuteNonQuery();$cmd.Dispose()
 foreach($role in @('Manager','Cashier')){
  $cmd=$c.CreateCommand();$cmd.CommandText="INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpPassword,EmpRole,EmpSal,EmpAge,EmpPhone) VALUES(@id,@name,@password,@role,0,30,N'000')"
  foreach($pair in @(@('@id',('life-'+$owner.Substring(0,8)+'-'+$role)),@('@name',('lifecycle_'+$role.ToLowerInvariant())),@('@password','OwnedLifecycleDummy-2026!'),@('@role',$role))){[void]$cmd.Parameters.AddWithValue($pair[0],$pair[1])}
  if($cmd.ExecuteNonQuery()-ne1){throw 'Exact fixture employee insert required'};$cmd.Dispose()
 }
}finally{$c.Dispose()}
Copy-Item -LiteralPath "$($accepted.Directory)/Source" -Destination "$stage/Source" -Recurse
$folder='PharmacyManagement-QaLifecycle-'+$owner
$remember="$stage/Source/project_fo_3/RememberMeService.cs"
$text=[IO.File]::ReadAllText($remember);$anchor='private const string AppFolderName = "PharmacyManagement";'
if(!$text.Contains($anchor)){throw 'Owned path instrumentation anchor drift'}
[IO.File]::WriteAllText($remember,$text.Replace($anchor,('private const string AppFolderName = "'+$folder+'";')),[Text.UTF8Encoding]::new($false))
$copied=@(Get-Content "$($accepted.Directory)/Verification/source-copy-manifest.json" -Raw|ConvertFrom-Json)
$delta=@($copied|Where-Object {(Get-FileHash -LiteralPath "$stage/Source/$($_.Path)").Hash-cne$_.SHA256})
if($delta.Count-ne1-or$delta[0].Path.Replace('\','/')-cne'project_fo_3/RememberMeService.cs'){throw 'Baseline fixture must differ only by remember folder constant'}
$profileFiles=@("$env:LOCALAPPDATA/PharmacyManagement/remembered-user.dat","$env:LOCALAPPDATA/PharmacyManagement/credentials.txt", "$env:ProgramData/PharmacyManagement/runtime-settings.xml","$env:ProgramData/PharmacyManagement/runtime-settings.previous.xml")
$profile=@($profileFiles|ForEach-Object{[ordered]@{Path=$_;Exists=(Test-Path -LiteralPath $_);SHA256=$(if(Test-Path -LiteralPath $_){(Get-FileHash -LiteralPath $_).Hash}else{$null})}})
$profile|ConvertTo-Json -Depth 5|Set-Content "$stage/real-profile-before.json" -Encoding utf8
[ordered]@{Directory=$stage;Owner=$owner;Database=$database;ConnectionString=$connection;RememberFolder=$folder;ReferenceMainSHA256=$accepted.MainSHA256;BaselineSourceDelta=@($delta.Path);LoginSourceSHA256=(Get-FileHash -LiteralPath "$stage/Source/project_fo_3/Login.cs").Hash;FullPlanComplete=$false}|ConvertTo-Json -Depth 8|Set-Content "$PSScriptRoot/output/login-success-lifecycle-job.json" -Encoding utf8
Write-Output "PASS fresh owned SQL/source lifecycle fixture: $stage"
