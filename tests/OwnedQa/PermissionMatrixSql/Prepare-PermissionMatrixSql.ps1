$ErrorActionPreference='Stop'
$prep=[IO.File]::ReadAllText("$PSScriptRoot/Prepare-AuthenticationAuditActor.ps1").Replace('PharmacyAuthAudit_','PharmacyPermissionMatrix_').Replace('QaAuthenticationAudit.Owner','QaPermissionMatrix.Owner').Replace('authentication-audit-','permissions-matrix-')
[IO.File]::WriteAllText("$PSScriptRoot/Prepare-PermissionMatrixFixture.ps1",$prep,[Text.UTF8Encoding]::new($false))
$run=[IO.File]::ReadAllText("$PSScriptRoot/Run-AuthenticationAuditActor.ps1").Replace('AuthenticationAuditActorProbe.cs','PermissionMatrixSqlProbe.cs').Replace('authentication-audit-','permissions-matrix-')
[IO.File]::WriteAllText("$PSScriptRoot/Run-PermissionMatrixSql.ps1",$run,[Text.UTF8Encoding]::new($false))
& "$PSScriptRoot/Prepare-PermissionMatrixFixture.ps1"
if(!$?){throw 'Owned permission fixture preparation failed'}
& "$PSScriptRoot/Run-PermissionMatrixSql.ps1" -Mode fixed
if(!$?){throw 'Original permission matrix failed; preserve evidence'}
