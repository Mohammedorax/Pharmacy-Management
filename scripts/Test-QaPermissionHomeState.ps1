[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$QaServer = '.',
    [string]$QaDatabase = 'PharmacyManagement_qa',
    [string]$ArtifactsDirectory = (Join-Path $PSScriptRoot '..\artifacts\qa-permission-navigation'),
    [switch]$ConfirmQaWrites
)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmQaWrites -or $QaDatabase -cne 'PharmacyManagement_qa') {
    throw 'QA permission navigation requires -ConfirmQaWrites and PharmacyManagement_qa exactly.'
}
$executable = Join-Path $PackageDirectory 'project_fo_3.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'QA application executable is missing.' }
$profileCheck = Start-Process -FilePath $executable -ArgumentList @('--verify-qa-package-profile') -PassThru -Wait -NoNewWindow
if ($profileCheck.ExitCode -ne 0) { throw 'QA package does not enforce its test database profile.' }

$suffix = [Guid]::NewGuid().ToString('N')
$employeeId = 'QA-UI-' + $suffix
$userName = 'qa-ui-' + $suffix
$password = [Guid]::NewGuid().ToString('N')
$salt = New-Object byte[] 16
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
$derivation = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
    $password, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try { $passwordHashBytes = $derivation.GetBytes(32) } finally { $derivation.Dispose() }
$passwordHash = 'PBKDF2-SHA256$100000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($passwordHashBytes)

$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source'] = $QaServer
$builder['Initial Catalog'] = $QaDatabase
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $true
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 5
$builder['Application Name'] = 'Pharmacy.QaPermissionNavigation'
$created = $false
try {
    $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    try {
        $connection.Open()
        $transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        try {
            $command = $connection.CreateCommand()
            $command.Transaction = $transaction
            $command.CommandText = @'
INSERT dbo.EmployeeTb1 (EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES (@Id,@Name,0,30,N'',@Password,N'Cashier');
INSERT dbo.EmployeeOperationPermissionsTb (EmpId,OperationName,IsAllowed,UpdatedBy)
VALUES (@Id,N'Screen.Sales',0,N'QA-UI-Test'),
       (@Id,N'Screen.Accounting',1,N'QA-UI-Test'),
       (@Id,N'Screen.Inventory',1,N'QA-UI-Test');
'@
            [void]$command.Parameters.Add('@Id', [System.Data.SqlDbType]::NVarChar, 50)
            [void]$command.Parameters.Add('@Name', [System.Data.SqlDbType]::NVarChar, 200)
            [void]$command.Parameters.Add('@Password', [System.Data.SqlDbType]::NVarChar, 200)
            $command.Parameters['@Id'].Value = $employeeId
            $command.Parameters['@Name'].Value = $userName
            $command.Parameters['@Password'].Value = $passwordHash
            [void]$command.ExecuteNonQuery()
            $transaction.Commit()
            $created = $true
        } catch { $transaction.Rollback(); throw } finally { $transaction.Dispose() }
    } finally { $connection.Dispose() }

    $env:PHARMACY_QA_UI_USER = $userName
    $env:PHARMACY_QA_UI_PASSWORD = $password
    $runner = Join-Path $PSScriptRoot '..\tests\PharmacyManagement.UiAutomation\PharmacyManagement.UiAutomation.csproj'
    dotnet run --project $runner --configuration Release --no-build -- --exe $executable --artifacts $ArtifactsDirectory --scenario qa-permission-home-state
    if ($LASTEXITCODE -ne 0) { throw "FlaUI QA permission home-state check failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:PHARMACY_QA_UI_USER -ErrorAction SilentlyContinue
    Remove-Item Env:PHARMACY_QA_UI_PASSWORD -ErrorAction SilentlyContinue
    if ($created) {
        $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = 'DELETE dbo.EmployeeTb1 WHERE EmpId=@Id AND EmpName=@Name;'
            [void]$command.Parameters.Add('@Id', [System.Data.SqlDbType]::NVarChar, 50)
            [void]$command.Parameters.Add('@Name', [System.Data.SqlDbType]::NVarChar, 200)
            $command.Parameters['@Id'].Value = $employeeId
            $command.Parameters['@Name'].Value = $userName
            if ($command.ExecuteNonQuery() -ne 1) { throw 'QA UI fixture cleanup failed.' }
        } finally { $connection.Dispose() }
    }
}
Write-Output 'PASS: QA login and permission home state tested; employee and permission fixture removed.'
