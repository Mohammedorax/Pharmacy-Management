[CmdletBinding()]
param([switch]$ConfirmQaRestore,[switch]$IncludeCommittedPosting,[switch]$IncludeInventoryRollback,[switch]$IncludeCommittedInventory,[switch]$IncludeCommittedSalesInvoice,[switch]$IncludeCommittedPurchaseCancellation,[switch]$IncludePurchaseCancellationCommercial,[switch]$IncludeCommittedCashBoxSale,[switch]$IncludeCommercialReturnRollback,[switch]$IncludeCommercialReturnConcurrency,[switch]$IncludeCommercialReturnDurableRecovery,[switch]$IncludeCommercialReturnProcessInterruption,[switch]$IncludeLotStockParity,[switch]$IncludeLotStockUiSmoke,[switch]$IncludeInventorySqlCancellation,[switch]$IncludeLiveSalesUi,[switch]$IncludeVoucherRequestIdentity,[switch]$IncludeVoucherEditorSql,[string]$OutputRoot="C:\PharmacyQA\work",[string]$ExecutablePath)
$ErrorActionPreference='Stop'
if(-not $ConfirmQaRestore){throw 'Explicit -ConfirmQaRestore is required.'}
if($IncludeCommercialReturnProcessInterruption){$IncludeCommercialReturnDurableRecovery=$true}
if($IncludeCommercialReturnDurableRecovery){$IncludeCommercialReturnConcurrency=$true}
if($IncludeVoucherEditorSql){$IncludeVoucherRequestIdentity=$true}
if($IncludeLotStockUiSmoke){$IncludeLotStockParity=$true}
if($IncludePurchaseCancellationCommercial -and -not $IncludeCommittedPurchaseCancellation){throw '-IncludePurchaseCancellationCommercial requires -IncludeCommittedPurchaseCancellation.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
# Keep backup, restored SQL files and acceptance evidence on the requested C drive.
if($OutputRoot -notmatch '^[cC]:[\\/]'){throw 'QA OutputRoot must be an absolute C: path.'}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if($ExecutablePath) {
    if($ExecutablePath -notmatch '^[cC]:[\\/]' -or [IO.Path]::GetExtension($ExecutablePath) -ine '.exe' -or -not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw 'Explicit QA executable must be an existing .exe on C:.'
    }
    if(-not ($IncludeCommercialReturnRollback -or $IncludeCommercialReturnConcurrency)){throw '-ExecutablePath requires a commercial-return QA check.'}
    $ExecutablePath=[IO.Path]::GetFullPath($ExecutablePath)
}
$token=[Guid]::NewGuid().ToString('N')
if($IncludeCommercialReturnConcurrency) {
    $otherChecks=@($PSBoundParameters.Keys | Where-Object {$_ -like 'Include*' -and $_ -notin @('IncludeCommercialReturnConcurrency','IncludeCommercialReturnRollback','IncludeCommercialReturnDurableRecovery','IncludeCommercialReturnProcessInterruption')})
    if($otherChecks.Count){throw 'Committed return concurrency must run independently of unrelated write checks.'}
    if(-not $ExecutablePath){throw 'Committed return concurrency requires an explicit C: executable.'}
    $clone='PharmacyConcurrency_'+(Get-Date -Format 'yyyyMMddHHmmss')+'_'+$token.Substring(0,8)+'_sandbox'
} else {
    $clone='PharmacyRestore_'+(Get-Date -Format 'yyyyMMddHHmmss')+'_'+$token+'_qa'
}
if($clone -notmatch '^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$' -and $clone -notmatch '^PharmacyConcurrency_[0-9]{14}_[a-f0-9]{8}_sandbox$'){throw 'Invalid temporary QA name.'}
$run=Join-Path $OutputRoot ('qa-restore-'+$token)
[void](New-Item -ItemType Directory -Path $run)
$backup=Join-Path $run 'PharmacyManagement_qa_restore_test.bak'
$evidence=[ordered]@{Source='PharmacyManagement_qa';Target=$clone;Token=$token;Backup=$backup;FullRestoreTested=$false;CheckDbPassed=$false;CountsMatched=$false;SourceCountsUnchanged=$false;CleanupPassed=$false;LotStockParityTested=$false;LotRollbackTested=$false;LiveUiStockExitCode=$null;LiveUiLotsExitCode=$null;LiveUiSummaryExitCode=$null;LiveUiTurnoverExitCode=$null;LiveUiAlertsExitCode=$null;LiveUiMovementExitCode=$null;LiveUiIntegrityExitCode=$null;SqlCancellationTested=$false;SqlCancellationLockWaitObserved=$false;SqlCancellationRequestCleared=$false;LiveUiCancelLoadingExitCode=$null;SalesInvoiceExitCode=$null;SalesInvoiceVerified=$false;PurchaseCancellationExitCode=$null;PurchaseCancellationVerified=$false;PurchaseCancellationLiveUiExitCode=$null;PurchaseCancellationLiveUiVerified=$false;PurchaseCancellationLiveUiInventoryRestored=$false;PurchaseCancellationLiveUiFinanceRestored=$false;PurchaseCancellationLiveUiMovementChanged=$false;PurchaseCancellationCommercialSchemaExitCode=$null;PurchaseCancellationCommercialSchemaInstalled=$false;PurchaseCancellationCommercialModeReady=$false;CashBoxSaleExitCode=$null;CashBoxSaleVerified=$false;CommercialReturnExitCode=$null;CommercialReturnVerified=$false;LiveSalesUiExitCode=$null;LiveSalesUiVerified=$false;LiveSalesUiInventoryRestored=$false;LiveSalesUiMovementLedgerChanged=$false;LiveSalesUiFinanceRestored=$false;VoucherRequestMigrationApplied=$false;VoucherRequestMissingSchemaPassed=$false;VoucherRequestIdentityPassed=$false;VoucherRequestSourceDataUnchanged=$false}
$master=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy.QaRestore')
$created=$false; $owned=$false; $targetId=$null
function Assert-OwnedReturnWorker([Diagnostics.Process]$Process,[string]$Executable,[long]$StartedUtcTicks) {
    if($Process.HasExited){throw 'The owned return worker exited before controlled termination.'}
    if($Process.StartTime.ToUniversalTime().Ticks -ne $StartedUtcTicks -or
       -not [string]::Equals([IO.Path]::GetFullPath($Process.MainModule.FileName),[IO.Path]::GetFullPath($Executable),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Return worker identity changed; termination refused.'
    }
}
function Invoke-InterruptedReturnWrite([string]$Executable,[string[]]$Arguments,[string]$Output,[string]$Errors,[string]$ReadyPath,[string]$Owner,[string]$Target) {
    $worker=$null; $startedUtcTicks=0
    try {
        $worker=Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -WorkingDirectory (Split-Path -Parent $Executable) -PassThru -RedirectStandardOutput $Output -RedirectStandardError $Errors
        $null=$worker.Handle
        $startedUtcTicks=$worker.StartTime.ToUniversalTime().Ticks
        $watch=[Diagnostics.Stopwatch]::StartNew()
        while(-not (Test-Path -LiteralPath $ReadyPath -PathType Leaf)) {
            if($worker.HasExited){throw 'Owned return worker exited without a committed-ready checkpoint.'}
            if($watch.Elapsed.TotalSeconds -ge 45){throw 'Owned return worker did not reach its committed checkpoint in time.'}
            Start-Sleep -Milliseconds 100
        }
        $ready=Get-Content -LiteralPath $ReadyPath -Raw | ConvertFrom-Json
        if($ready.Owner -cne $Owner -or $ready.Database -cne $Target -or $ready.ProcessId -ne $worker.Id -or
           $ready.StartedUtcTicks -ne $startedUtcTicks -or $ready.Phase -cne 'AfterCommitBeforeStoreDispose' -or
           $ready.RequestId -notmatch '^[a-f0-9]{32}$' -or $ready.ReturnId -notmatch '^[a-fA-F0-9-]{36}$') {
            throw 'Return interruption checkpoint does not identify this owned process and sandbox.'
        }
        $pending=@(Get-ChildItem -LiteralPath (Split-Path -Parent $ReadyPath) -Filter '*-CommercialReturn.pending' -File)
        if($pending.Count -ne 1 -or $pending[0].Length -le 0){throw 'Committed-ready return has no single durable pending record.'}
        Assert-OwnedReturnWorker $worker $Executable $startedUtcTicks
        $worker.Kill()
        if(-not $worker.WaitForExit(10000)){throw 'Owned return process did not terminate.'}
        if($worker.ExitCode -eq 0){throw 'Return process completed normally instead of being interrupted.'}
        return [pscustomobject]@{Id=$worker.Id;ExitCode=$worker.ExitCode;StartedUtcTicks=$startedUtcTicks;WasKilled=$true;Checkpoint=$ReadyPath;ReadyWaitMilliseconds=$watch.ElapsedMilliseconds;RequestId=$ready.RequestId;ReturnId=$ready.ReturnId}
    } finally {
        if($null -ne $worker) {
            try {
                if(-not $worker.HasExited) {
                    Assert-OwnedReturnWorker $worker $Executable $startedUtcTicks
                    $worker.Kill()
                    if(-not $worker.WaitForExit(10000)){throw 'Owned failed QA worker remained active.'}
                }
            } finally {$worker.Dispose()}
        }
    }
}
function New-Command([string]$sql,[hashtable]$parameters) {
    $command=$master.CreateCommand(); $command.CommandText=$sql; $command.CommandTimeout=180
    foreach($key in $parameters.Keys){[void]$command.Parameters.AddWithValue($key,$parameters[$key])}
    return $command
}
function Scalar([string]$sql,[hashtable]$parameters) {
    $command=New-Command $sql $parameters
    try{return $command.ExecuteScalar()}finally{$command.Dispose()}
}
function Execute([string]$sql,[hashtable]$parameters) {
    $command=New-Command $sql $parameters
    try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
}
function Counts([string]$database) {
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected database in count comparison.'}
    $result=[ordered]@{}
    foreach($table in @('JournalEntriesTb','JournalEntryLinesTb','AccountingVouchersTb','AuditLogTb','AccountingPeriodsTb')) {
        $result[$table]=[long](Scalar ('SELECT COUNT_BIG(*) FROM ['+$database+'].dbo.['+$table+'];') @{})
    }
    return $result
}
function Inventory-Snapshot([string]$database) {
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected inventory snapshot database.'}
    $result=[ordered]@{}
    foreach($entry in @(@('Stock',('SELECT MedName,MedQty FROM ['+$database+'].dbo.MedicineTb1 ORDER BY MedName')),@('Movements',('SELECT * FROM ['+$database+'].dbo.InventoryMovementsTb ORDER BY MovementId')),@('Lots',('SELECT LotId,MedName,BatchNumber,ExpiryDate,UnitCost,QuantityReceived,QuantityRemaining,IsRecalled FROM ['+$database+'].dbo.InventoryLotsTb ORDER BY LotId')),@('ActiveLots',('SELECT LotId,MedName,BatchNumber,ExpiryDate,UnitCost,QuantityReceived,QuantityRemaining,IsRecalled FROM ['+$database+'].dbo.InventoryLotsTb WHERE QuantityRemaining>0 ORDER BY LotId')))) {
        $xml=[string](Scalar ('SELECT CONVERT(nvarchar(max),('+$entry[1]+' FOR XML RAW,ROOT(''rows''),TYPE));') @{})
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$result[$entry[0]+'SHA256']=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($xml))).Replace('-','')}finally{$sha.Dispose()}
    }
    return $result
}
function Voucher-DataSnapshot([string]$database) {
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected voucher snapshot database.'}
    $result=[ordered]@{}
    $tables=[ordered]@{AccountingVouchersTb='VoucherId';JournalEntriesTb='JournalEntryId';JournalEntryLinesTb='JournalEntryLineId';AuditLogTb='AuditId';AccountingPeriodsTb='PeriodId';CurrencySettingsTb='CurrencyCode';CostCentersTb='CostCenterId';AccountsTb='AccountCode'}
    foreach($table in $tables.Keys){
        $xml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] ORDER BY "+$tables[$table]+" FOR XML RAW('r'),ROOT('rows'),BINARY BASE64,TYPE));") @{})
        $sha=[Security.Cryptography.SHA256]::Create();try{$result[$table]=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($xml))).Replace('-','')}finally{$sha.Dispose()}
    }
    return $result
}
function Accounting-ValueSnapshot([string]$database) {
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected snapshot database.'}
    # Enumerate source tables, excluding only SQL system tables. Clone ownership metadata is not source data.
    [xml]$tables=[string](Scalar 'SELECT CONVERT(nvarchar(max),(SELECT s.name SchemaName,t.name TableName FROM [PharmacyManagement_qa].sys.tables t JOIN [PharmacyManagement_qa].sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR XML RAW,ROOT(''tables'')));' @{})
    $result=[ordered]@{}
    foreach($table in $tables.tables.row){
        $schema=[string]$table.SchemaName;$name=[string]$table.TableName
        $quotedSchema='['+$schema.Replace(']',']]')+']';$quotedTable='['+$name.Replace(']',']]')+']'
        [xml]$xml=[string](Scalar ('SELECT CONVERT(nvarchar(max),(SELECT * FROM ['+$database+'].'+$quotedSchema+'.'+$quotedTable+' FOR XML RAW,ROOT(''rows''),BINARY BASE64));') @{})
        $rows=[Collections.Generic.List[string]]::new()
        foreach($row in $xml.rows.ChildNodes){$rows.Add($row.OuterXml)}
        $rows.Sort([StringComparer]::Ordinal)
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$result[$schema+'.'+$name]=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]::Join("`n",$rows)))).Replace('-','')}finally{$sha.Dispose()}
    }
    if($result.Count -eq 0){throw 'Source table inventory is empty.'}
    return $result
}
function Column-Snapshot([string]$database){
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected column snapshot database.'}
    $xml=[string](Scalar ('SELECT CONVERT(nvarchar(max),(SELECT s.name SchemaName,t.name TableName,c.column_id ColumnOrdinal,c.name ColumnName,ts.name TypeSchema,ty.name TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.collation_name,c.is_identity,c.is_computed,dc.definition DefaultDefinition,cc.definition ComputedDefinition,cc.is_persisted,CONVERT(nvarchar(100),ic.seed_value) IdentitySeed,CONVERT(nvarchar(100),ic.increment_value) IdentityIncrement FROM ['+$database+'].sys.tables t JOIN ['+$database+'].sys.schemas s ON s.schema_id=t.schema_id JOIN ['+$database+'].sys.columns c ON c.object_id=t.object_id JOIN ['+$database+'].sys.types ty ON ty.user_type_id=c.user_type_id JOIN ['+$database+'].sys.schemas ts ON ts.schema_id=ty.schema_id LEFT JOIN ['+$database+'].sys.default_constraints dc ON dc.object_id=c.default_object_id LEFT JOIN ['+$database+'].sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN ['+$database+'].sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE EXISTS(SELECT 1 FROM [PharmacyManagement_qa].sys.tables st JOIN [PharmacyManagement_qa].sys.schemas ss ON ss.schema_id=st.schema_id WHERE st.is_ms_shipped=0 AND st.name=t.name AND ss.name=s.name) ORDER BY s.name,t.name,c.column_id FOR XML RAW,ROOT(''columns'')));') @{})
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($xml))).Replace('-','')}finally{$sha.Dispose()}
}
function Finance-Snapshot([string]$database) {
    if($database -ne 'PharmacyManagement_qa' -and $database -ne $clone){throw 'Unexpected finance snapshot database.'}
    $result=[ordered]@{}
    foreach($entry in @(
        @('AccountBalances',('SELECT AccountCode,CONVERT(decimal(18,2),SUM(Debit-Credit)) NetBalance FROM ['+$database+'].dbo.JournalEntryLinesTb GROUP BY AccountCode ORDER BY AccountCode FOR XML RAW,ROOT(''rows''),TYPE')),
        @('CashBoxBalances',('SELECT k.CashBoxId,CONVERT(decimal(18,2),SUM(l.Debit-l.Credit)) NetBalance FROM ['+$database+'].dbo.CashBoxJournalLinksTb k JOIN ['+$database+'].dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=k.JournalEntryLineId GROUP BY k.CashBoxId ORDER BY k.CashBoxId FOR XML RAW,ROOT(''rows''),TYPE')))) {
        $xml=[string](Scalar ('SELECT CONVERT(nvarchar(max),('+ $entry[1] +'));') @{})
        if($null -eq $xml){$xml=''}
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$result[$entry[0]+'SHA256']=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($xml))).Replace('-','')}finally{$sha.Dispose()}
    }
    return $result
}function Equal-Counts($before,$after) {
    if($before.Count -ne $after.Count){throw 'Restore/source snapshot key count changed.'}
    foreach($key in $before.Keys){if(-not $after.Contains($key)){throw 'Restore/source snapshot key missing.'}}
    foreach($key in $before.Keys){if($before[$key] -ne $after[$key]){throw ('Restore/source row count changed: '+$key)}}
}
try {
    $master.Open()
    if([string](Scalar 'SELECT DB_NAME();' @{}) -ne 'master'){throw 'Unexpected administration connection.'}
    if((Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$clone}) -isnot [DBNull]){throw 'Target already exists; never overwrite a database.'}
    if([int](Scalar 'SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID(N''PharmacyManagement_qa'') AND spid>50;' @{}) -ne 0){throw 'Other QA sessions are active.'}
    $before=Counts 'PharmacyManagement_qa'; $evidence.SourceCountsBefore=$before
    $accountingValuesBefore=Accounting-ValueSnapshot 'PharmacyManagement_qa'; $evidence.AccountingValuesBefore=$accountingValuesBefore
    $columnSchemaBefore=Column-Snapshot 'PharmacyManagement_qa'; $evidence.ColumnSchemaBefore=$columnSchemaBefore
    if($IncludeVoucherRequestIdentity){$voucherSourceBefore=Voucher-DataSnapshot 'PharmacyManagement_qa';$evidence.VoucherRequestSourceDataBefore=$voucherSourceBefore}
    $inventoryBefore=Inventory-Snapshot 'PharmacyManagement_qa'; $evidence.SourceInventoryBefore=$inventoryBefore
    Execute 'BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;' @{'@Path'=$backup}
    Execute 'RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;' @{'@Path'=$backup}
    $evidence.BackupSHA256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    $command=New-Command 'RESTORE HEADERONLY FROM DISK=@Path;' @{'@Path'=$backup}
    try {
        $reader=$command.ExecuteReader()
        try {
            if(-not $reader.Read() -or [string]$reader['DatabaseName'] -ne 'PharmacyManagement_qa' -or
                [int]$reader['BackupType'] -ne 1 -or -not [bool]$reader['HasBackupChecksums'] -or [bool]$reader['IsDamaged'] -or $reader.Read()) {
                throw 'Expected one undamaged full QA backup with checksum.'
            }
        } finally {$reader.Dispose()}
    } finally {$command.Dispose()}
    $dataDirectory=[IO.Path]::GetFullPath((Join-Path $run 'sql-data'))
    [void](New-Item -ItemType Directory -Path $dataDirectory)
    $evidence.RestoreDataDirectory=$dataDirectory
    $command=New-Command 'RESTORE FILELISTONLY FROM DISK=@Path;' @{'@Path'=$backup}
    $files=[Data.DataTable]::new()
    try{$adapter=[Data.SqlClient.SqlDataAdapter]::new($command);try{[void]$adapter.Fill($files)}finally{$adapter.Dispose()}}finally{$command.Dispose()}
    if($files.Rows.Count -eq 0){throw 'Backup file list is empty.'}
    $moves=[Collections.Generic.List[string]]::new(); $paths=[Collections.Generic.List[string]]::new(); $index=0
    foreach($file in $files.Rows) {
        $index++; $kind=[string]$file['Type']
        if($kind -ne 'D' -and $kind -ne 'L'){throw 'Unsupported backup file type.'}
        $extension=if($kind -eq 'L'){'.ldf'}else{'.mdf'}
        $path=[IO.Path]::GetFullPath((Join-Path $dataDirectory ($clone+'_'+$index+$extension)))
        if(-not $path.StartsWith($dataDirectory.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.File]::Exists($path)) {throw 'Unsafe or existing restore file path.'}
        if([int](Scalar 'SELECT COUNT(*) FROM sys.master_files WHERE physical_name=@Path;' @{'@Path'=$path}) -ne 0){throw 'Restore path is owned by another SQL database.'}
        $moves.Add("MOVE N'$(([string]$file['LogicalName']).Replace("'","''"))' TO N'$($path.Replace("'","''"))'")
        $paths.Add($path)
    }
    $evidence.TargetFiles=$paths.ToArray()
    Execute ('RESTORE DATABASE ['+$clone+'] FROM DISK=@Path WITH CHECKSUM,RECOVERY, '+($moves -join ', ')+';') @{'@Path'=$backup}
    $created=$true; $targetId=[int](Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$clone})
    Execute ('USE ['+$clone+']; EXEC sys.sp_addextendedproperty @name=N''QaTest.Owner'',@value=@Token;') @{'@Token'=$token}
    $owned=$true; $master.ChangeDatabase('master'); $evidence.FullRestoreTested=$true
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
    $evidence.CheckDbPassed=$true
    $restored=Counts $clone; Equal-Counts $before $restored
    $evidence.RestoredCounts=$restored; $evidence.CountsMatched=$true
    $restoredColumns=Column-Snapshot $clone; if($restoredColumns -ne $columnSchemaBefore){throw 'Restored column schema mismatch.'}; $evidence.RestoredColumnSchema=$restoredColumns; $evidence.ColumnSchemaMatched=$true
    Execute 'BEGIN TRANSACTION;' @{}
    try{
        Execute ('ALTER TABLE ['+$clone+'].dbo.MedicineTb1 ADD [OwnedRestoreSchemaProbe] nvarchar(17) NULL;') @{}
        if((Column-Snapshot $clone) -eq $restoredColumns){throw 'Restored column addition was not detected.'}
        $evidence.RestoredColumnMutationDetected=$true
    }finally{Execute 'IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;' @{}}
    if((Column-Snapshot $clone) -ne $restoredColumns){throw 'Restored column mutation rollback mismatch.'}
    $evidence.RestoredColumnMutationRolledBack=$true
    $restoredValues=Accounting-ValueSnapshot $clone; Equal-Counts $accountingValuesBefore $restoredValues; $evidence.RestoredAccountingValues=$restoredValues; $evidence.AccountingValuesMatched=$true
    if([long](Scalar ('SELECT COUNT_BIG(*) FROM ['+$clone+'].dbo.MedicineTb1;') @{}) -gt 0){
        Execute 'BEGIN TRANSACTION;' @{}
        try{
            Execute ('UPDATE ['+$clone+'].dbo.MedicineTb1 SET MedQty=CASE WHEN MedQty=0 THEN 1 ELSE 0 END WHERE MedName=(SELECT TOP(1) MedName FROM ['+$clone+'].dbo.MedicineTb1 ORDER BY MedName);') @{}
            $changedValues=Accounting-ValueSnapshot $clone
            $mutationDetected=$false
            try{Equal-Counts $restoredValues $changedValues}catch{$mutationDetected=$true}
            if(-not $mutationDetected){throw 'Restored row value change was not detected.'}
            $evidence.RestoredValueMutationDetected=$true
        }finally{Execute 'IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;' @{}}
        Equal-Counts $restoredValues (Accounting-ValueSnapshot $clone)
        $evidence.RestoredValueMutationRolledBack=$true
    }else{$evidence.RestoredValueMutationDetected=$false;$evidence.RestoredValueMutationSkipped='No medicine rows.'}
    $restoredInventory=Inventory-Snapshot $clone; Equal-Counts $inventoryBefore $restoredInventory; $evidence.RestoredInventory=$restoredInventory
    if($IncludeVoucherRequestIdentity) {
        $core=Join-Path $taskRoot 'tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe'
        foreach($flag in @('--owned-voucher-request-missing-schema','--owned-voucher-request-identity')) {
            if($flag -eq '--owned-voucher-request-identity') {
                $migrationDirectory=Join-Path $run 'voucher-request-migrations'
                New-Item -ItemType Directory -Path $migrationDirectory -Force | Out-Null
                Copy-Item -LiteralPath (Join-Path $taskRoot 'database\migrations\0023_voucher_request_identity.sql') -Destination $migrationDirectory
                & (Join-Path $taskRoot 'scripts\Apply-CommercialMigrations.ps1') -ServerInstance '.' -DatabaseName $clone -Environment Qa -VerifiedBackupPath $backup -MigrationDirectory $migrationDirectory -Force > (Join-Path $run 'voucher-migration.log')
                if(-not $?){throw 'Owned voucher migration failed.'}
                [Data.SqlClient.SqlConnection]::ClearAllPools() # Release only this process's idle migration connections before the strict session guard.
                $evidence.VoucherRequestMigrationApplied=$true
            }
            $name=$flag.TrimStart('-')
            $process=Start-Process -FilePath $core -ArgumentList @($flag,'.',$clone,$token,'--confirm-owned-temporary-qa-writes') -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput (Join-Path $run ($name+'.stdout.log')) -RedirectStandardError (Join-Path $run ($name+'.stderr.log'))
            $evidence[$name+'ExitCode']=$process.ExitCode
            if($process.ExitCode -ne 0){Get-Content (Join-Path $run ($name+'.stderr.log')) -Tail 10;throw "Owned voucher request test failed: $flag"}
            if($flag -eq '--owned-voucher-request-missing-schema'){$evidence.VoucherRequestMissingSchemaPassed=$true}else{$evidence.VoucherRequestIdentityPassed=$true}
        }
        if($IncludeVoucherEditorSql){
            $editorDirectory=Join-Path $run 'voucher-editor-app'
            [void](New-Item -ItemType Directory -Path $editorDirectory -Force)
            Copy-Item -Path (Join-Path $taskRoot 'project_fo_3\bin\Release\*') -Destination $editorDirectory -Recurse -Force
            $editorApp=Join-Path $editorDirectory 'project_fo_3.exe'
            $editorConfigPath=$editorApp+'.config'
            [xml]$editorConfig=Get-Content -LiteralPath $editorConfigPath -Raw -Encoding UTF8
            $forceNode=$editorConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
            if($null -eq $forceNode){$forceNode=$editorConfig.CreateElement('add');$forceNode.SetAttribute('key','ForceBuildConnectionProfile');[void]$editorConfig.configuration.appSettings.AppendChild($forceNode)}
            $forceNode.SetAttribute('value','true')
            foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')){
                $node=$editorConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
                if($null -eq $node){throw 'Missing voucher editor isolated connection.'}
                $node.SetAttribute('connectionString','Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True')
            }
            $editorConfig.Save($editorConfigPath)
            $profile=Start-Process -FilePath $editorApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'voucher-editor-profile.stdout.log') -RedirectStandardError (Join-Path $run 'voucher-editor-profile.stderr.log')
            if($profile.ExitCode -ne 0 -or (Get-Content (Join-Path $run 'voucher-editor-profile.stdout.log') -Raw).IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Voucher editor profile was not pinned to owned clone.'}
            $editor=Start-Process -FilePath $editorApp -ArgumentList @('--owned-voucher-editors-qa',$clone,$token,'--confirm-owned-voucher-editor-writes') -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'voucher-editor-sql.stdout.log') -RedirectStandardError (Join-Path $run 'voucher-editor-sql.stderr.log')
            $evidence.VoucherEditorSqlExitCode=$editor.ExitCode
            Get-Content (Join-Path $run 'voucher-editor-sql.stdout.log') -Tail 12
            if($editor.ExitCode -ne 0){Get-Content (Join-Path $run 'voucher-editor-sql.stderr.log') -Tail 15;throw 'Owned actual voucher editor SQL save/recovery failed.'}
            $evidence.VoucherEditorSqlPassed=$true
        }
        Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
        $evidence.VoucherRequestPostCheckDbPassed=$true
        $voucherSourceAfter=Voucher-DataSnapshot 'PharmacyManagement_qa';Equal-Counts $voucherSourceBefore $voucherSourceAfter
        $evidence.VoucherRequestSourceDataAfter=$voucherSourceAfter;$evidence.VoucherRequestSourceDataUnchanged=$true
        Write-Output 'PASS owned voucher request migration, SQL cases and unchanged source data.'
    }
    if($IncludeCommittedSalesInvoice) {
        $releaseDirectory=Join-Path $taskRoot 'project_fo_3\bin\Release'
        $salesAppDirectory=Join-Path $run 'sales-app'
        [void](New-Item -ItemType Directory -Path $salesAppDirectory -Force)
        Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $salesAppDirectory -Recurse -Force
        $salesApp=Join-Path $salesAppDirectory 'project_fo_3.exe'
        $salesConfigPath=$salesApp+'.config'
        [xml]$salesConfig=Get-Content -LiteralPath $salesConfigPath -Raw -Encoding UTF8
        $salesForceSetting=$salesConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
        if($null -eq $salesForceSetting) { $salesForceSetting=$salesConfig.CreateElement('add'); $salesForceSetting.SetAttribute('key','ForceBuildConnectionProfile'); [void]$salesConfig.configuration.appSettings.AppendChild($salesForceSetting) }
        $salesForceSetting.SetAttribute('value','true')
        $salesConnection='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True'
        foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
            $connectionNode=$salesConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
            if($null -eq $connectionNode){throw ('Missing isolated sales connection: '+$connectionName)}
            $connectionNode.SetAttribute('connectionString',$salesConnection)
        }
        $salesConfig.Save($salesConfigPath)
        $salesProfilePath=Join-Path $run 'sales-profile.log'
        $salesProfileErrorPath=Join-Path $run 'sales-profile-error.log'
        $salesProfile=Start-Process -FilePath $salesApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $salesProfilePath -RedirectStandardError $salesProfileErrorPath
        $salesProfileText=Get-Content -LiteralPath $salesProfilePath -Raw -Encoding UTF8
        if($salesProfile.ExitCode -ne 0 -or $salesProfileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Sales test did not pin its connection to the owned temporary QA clone.'}
        $salesLog=Join-Path $run 'sales-invoice-roundtrip.log'
        $salesError=Join-Path $run 'sales-invoice-roundtrip-error.log'
        $salesArgs=@('--verify-qa-sales-roundtrip',$backup,$token,'--confirm-qa-invoice-writes')
        $salesProcess=Start-Process -FilePath $salesApp -ArgumentList $salesArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $salesLog -RedirectStandardError $salesError
        $evidence.SalesInvoiceExitCode=$salesProcess.ExitCode
        Get-Content -LiteralPath $salesLog -Tail 40
        Get-Content -LiteralPath $salesError -Tail 20 -ErrorAction SilentlyContinue
        if($evidence.SalesInvoiceExitCode -ne 0){throw 'Owned temporary QA sales invoice issue/cancellation roundtrip failed.'}
        $evidence.SalesInvoiceVerified=$true
    }
    if($IncludeCommittedPurchaseCancellation) {
        $releaseDirectory=Join-Path $taskRoot 'project_fo_3\bin\Release'
        $purchaseAppDirectory=Join-Path $run 'purchase-cancellation-app'
        [void](New-Item -ItemType Directory -Path $purchaseAppDirectory -Force)
        Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $purchaseAppDirectory -Recurse -Force
        $purchaseApp=Join-Path $purchaseAppDirectory 'project_fo_3.exe'
        $purchaseConfigPath=$purchaseApp+'.config'
        [xml]$purchaseConfig=Get-Content -LiteralPath $purchaseConfigPath -Raw -Encoding UTF8
        $purchaseForce=$purchaseConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
        if($null -eq $purchaseForce) { $purchaseForce=$purchaseConfig.CreateElement('add'); $purchaseForce.SetAttribute('key','ForceBuildConnectionProfile'); [void]$purchaseConfig.configuration.appSettings.AppendChild($purchaseForce) }
        $purchaseForce.SetAttribute('value','true')
        $purchaseConnection='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True'
        foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
            $connectionNode=$purchaseConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
            if($null -eq $connectionNode){throw ('Missing isolated purchase connection: '+$connectionName)}
            $connectionNode.SetAttribute('connectionString',$purchaseConnection)
        }
        $purchaseConfig.Save($purchaseConfigPath)
        $purchaseProfilePath=Join-Path $run 'purchase-cancellation-profile.log'
        $purchaseProfileErrorPath=Join-Path $run 'purchase-cancellation-profile-error.log'
        $purchaseProfile=Start-Process -FilePath $purchaseApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $purchaseProfilePath -RedirectStandardError $purchaseProfileErrorPath
        $purchaseProfileText=Get-Content -LiteralPath $purchaseProfilePath -Raw -Encoding UTF8
        if($purchaseProfile.ExitCode -ne 0 -or $purchaseProfileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Purchase cancellation test did not pin its connection to the owned temporary QA clone.'}
        if($IncludePurchaseCancellationCommercial) {
            $schemaLog=Join-Path $run 'purchase-cancellation-commercial-schema.log'
            $schemaError=Join-Path $run 'purchase-cancellation-commercial-schema-error.log'
            $schemaArgs=@('--install-commercial-schema-qa','.',$clone,$backup)
            $schemaProcess=Start-Process -FilePath $purchaseApp -ArgumentList $schemaArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $schemaLog -RedirectStandardError $schemaError
            $evidence.PurchaseCancellationCommercialSchemaExitCode=$schemaProcess.ExitCode
            Get-Content -LiteralPath $schemaLog -Tail 20
            Get-Content -LiteralPath $schemaError -Tail 20 -ErrorAction SilentlyContinue
            $schemaText=Get-Content -LiteralPath $schemaLog -Raw -Encoding UTF8
            if($schemaProcess.ExitCode -ne 0 -or $schemaText.IndexOf('Commercial schema installed on explicit QA database.',[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Guarded commercial schema installation failed on the owned temporary QA clone.'}
            $evidence.PurchaseCancellationCommercialSchemaInstalled=$true
            $cloneTable='['+$clone+']'
            $fixtureSql=@"
IF EXISTS (SELECT 1 FROM $cloneTable.dbo.MedicineTb1 WHERE MedQty<0) THROW 51000,N'QA fixture contains negative aggregate stock.',1;
INSERT INTO $cloneTable.dbo.InventoryLotsTb (LotId,MedName,BatchNumber,ProductionDate,ExpiryDate,SupplierName,UnitCost,QuantityReceived,QuantityRemaining,IsRecalled)
SELECT NEWID(),m.MedName,N'QA-BASE-'+CONVERT(nvarchar(36),NEWID()),
       CASE WHEN m.ProdDate IS NOT NULL AND m.ExpDate>m.ProdDate THEN CONVERT(date,m.ProdDate) ELSE NULL END,
       CASE WHEN m.ExpDate>ISNULL(m.ProdDate,CONVERT(datetime,'19000101',112)) THEN CONVERT(date,m.ExpDate) ELSE DATEADD(day,90,CONVERT(date,GETDATE())) END,
       N'QA synthetic quantity-parity fixture',0.00,CONVERT(decimal(18,3),m.MedQty),CONVERT(decimal(18,3),m.MedQty),0
FROM $cloneTable.dbo.MedicineTb1 m
WHERE m.MedQty>0 AND NOT EXISTS(SELECT 1 FROM $cloneTable.dbo.InventoryLotsTb l WHERE l.MedName=m.MedName);
UPDATE $cloneTable.dbo.CommercialOperationalSettingsTb SET SettingValue=N'FullCommercial',UpdatedAtUtc=SYSUTCDATETIME() WHERE SettingKey=N'Mode';
UPDATE $cloneTable.dbo.SaudiFeatureSettingsTb SET FeatureMode=N'Test',UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=N'QA verifier' WHERE FeatureKey=N'CommercialSales';
UPDATE $cloneTable.dbo.SaudiFeatureSettingsTb SET FeatureMode=N'Simulator',UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=N'QA verifier' WHERE FeatureKey=N'EInvoice';
UPDATE $cloneTable.dbo.CommercialEInvoiceSettingsTb SET SellerName=N'QA Test Pharmacy',SellerVatNumber=N'123456789012345',LocalSandboxEnabled=1,UpdatedAtUtc=SYSUTCDATETIME() WHERE SettingsId=1;
IF EXISTS(SELECT 1 FROM $cloneTable.dbo.MedicineTb1 m OUTER APPLY(SELECT ISNULL(SUM(l.QuantityRemaining),0) q FROM $cloneTable.dbo.InventoryLotsTb l WHERE l.MedName=m.MedName) x WHERE CONVERT(decimal(18,3),m.MedQty)<>x.q)
 THROW 51001,N'QA commercial parity fixture did not reconcile.',1;
"@
            [void](Execute $fixtureSql @{})
            $evidence.PurchaseCancellationCommercialModeReady=$true
        }
        $purchaseLog=Join-Path $run 'purchase-cancellation-roundtrip.log'
        $purchaseError=Join-Path $run 'purchase-cancellation-roundtrip-error.log'
        $purchaseArgs=@('--verify-qa-purchase-cancellation-roundtrip',$backup,$token,'--confirm-qa-purchase-cancellation')
        $purchaseProcess=Start-Process -FilePath $purchaseApp -ArgumentList $purchaseArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $purchaseLog -RedirectStandardError $purchaseError
        $evidence.PurchaseCancellationExitCode=$purchaseProcess.ExitCode
        Get-Content -LiteralPath $purchaseLog -Tail 40
        Get-Content -LiteralPath $purchaseError -Tail 20 -ErrorAction SilentlyContinue
        if($evidence.PurchaseCancellationExitCode -ne 0){throw 'Owned temporary QA purchase issue/cancellation roundtrip failed.'}
        $evidence.PurchaseCancellationVerified=$true
        $purchaseUiInventoryBefore=Inventory-Snapshot $clone
        $purchaseUiFinanceBefore=Finance-Snapshot $clone
        $fixtureLog=Join-Path $run 'purchase-cancellation-ui-fixture.log'
        $fixtureError=Join-Path $run 'purchase-cancellation-ui-fixture-error.log'
        $fixtureArgs=@('--prepare-qa-purchase-cancellation-ui',$backup,$token,'--confirm-qa-purchase-cancellation')
        $fixtureProcess=Start-Process -FilePath $purchaseApp -ArgumentList $fixtureArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $fixtureLog -RedirectStandardError $fixtureError
        $fixtureText=Get-Content -LiteralPath $fixtureLog -Raw -Encoding UTF8
        Get-Content -LiteralPath $fixtureLog -Tail 20
        Get-Content -LiteralPath $fixtureError -Tail 20 -ErrorAction SilentlyContinue
        if($fixtureProcess.ExitCode -ne 0 -or $fixtureText -notmatch 'PurchaseId=(QA-CXL-UI-[A-F0-9]{16})'){throw 'QA live UI purchase fixture creation failed or returned an invalid id.'}
        $uiPurchaseId=$Matches[1]
        $suffix='QA-PURCHASE-UI-'+$token.Substring(0,12)
        $employeeId=$suffix; $userName='qa-purchase-ui-'+$token.Substring(0,12); $password=[Guid]::NewGuid().ToString('N')
        $salt=New-Object byte[] 16; $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
        $derivation=[Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,100000,[Security.Cryptography.HashAlgorithmName]::SHA256)
        try { $hashBytes=$derivation.GetBytes(32) } finally { $derivation.Dispose() }
        $passwordHash='PBKDF2-SHA256$100000$'+[Convert]::ToBase64String($salt)+'$'+[Convert]::ToBase64String($hashBytes)
        $fixtureConnection=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaPurchaseCancellationUiFixture')
        try {
            $fixtureConnection.Open(); $fixtureCommand=$fixtureConnection.CreateCommand()
            $fixtureCommand.CommandText='INSERT dbo.EmployeeTb1 (EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole) VALUES (@Id,@Name,0,30,N'''',@Password,N''Manager'');'
            [void]$fixtureCommand.Parameters.Add('@Id',[Data.SqlDbType]::NVarChar,50); [void]$fixtureCommand.Parameters.Add('@Name',[Data.SqlDbType]::NVarChar,200); [void]$fixtureCommand.Parameters.Add('@Password',[Data.SqlDbType]::NVarChar,200)
            $fixtureCommand.Parameters['@Id'].Value=$employeeId; $fixtureCommand.Parameters['@Name'].Value=$userName; $fixtureCommand.Parameters['@Password'].Value=$passwordHash
            if($fixtureCommand.ExecuteNonQuery() -ne 1){throw 'Could not add the temporary manager for purchase cancellation UI.'}
        } finally { if($fixtureCommand){$fixtureCommand.Dispose()}; $fixtureConnection.Dispose() }
        $env:PHARMACY_QA_UI_USER=$userName; $env:PHARMACY_QA_UI_PASSWORD=$password; $env:PHARMACY_QA_UI_PURCHASE_ID=$uiPurchaseId
        $uiRunner=Join-Path $taskRoot 'tests\PharmacyManagement.UiAutomation\bin\Release\net10.0-windows\PharmacyManagement.UiAutomation.dll'
        if(-not (Test-Path -LiteralPath $uiRunner)){throw 'Build the FlaUI runner before the purchase cancellation live UI test.'}
        $liveUiArtifacts=Join-Path $run 'purchase-cancellation-live-ui-results'; $liveUiLog=Join-Path $run 'purchase-cancellation-live-ui.log'; $liveUiError=Join-Path $run 'purchase-cancellation-live-ui-error.log'
        try {
            $liveUiProcess=Start-Process -FilePath 'dotnet' -ArgumentList @($uiRunner,'--exe',$purchaseApp,'--artifacts',$liveUiArtifacts,'--scenario','purchase-cancellation-live-qa') -WorkingDirectory $purchaseAppDirectory -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $liveUiLog -RedirectStandardError $liveUiError
            $evidence.PurchaseCancellationLiveUiExitCode=$liveUiProcess.ExitCode
            Get-Content -LiteralPath $liveUiLog -Tail 60 -ErrorAction SilentlyContinue; Get-Content -LiteralPath $liveUiError -Tail 25 -ErrorAction SilentlyContinue
            if($evidence.PurchaseCancellationLiveUiExitCode -ne 0){throw 'Live QA purchase cancellation UI scenario failed.'}
            $evidence.PurchaseCancellationLiveUiVerified=$true
            $purchaseUiInventoryAfter=Inventory-Snapshot $clone
            foreach($key in @('StockSHA256','ActiveLotsSHA256')) {if($purchaseUiInventoryBefore[$key] -ne $purchaseUiInventoryAfter[$key]){throw ('Purchase cancellation UI did not restore inventory: '+$key)}}
            $cancelledLots=[int](Scalar ('SELECT COUNT(*) FROM ['+$clone+'].dbo.InventoryLotsTb l JOIN ['+$clone+'].dbo.PurchaseDetailsTb d ON d.DetailId=l.PurchaseDetailId WHERE d.PurchaseId=@Id AND l.QuantityRemaining=0;') @{'@Id'=$uiPurchaseId})
            if($cancelledLots -ne 1){throw 'The cancelled UI fixture lot was not preserved at zero quantity for audit.'}
            if($purchaseUiInventoryBefore['MovementsSHA256'] -eq $purchaseUiInventoryAfter['MovementsSHA256']){throw 'Purchase cancellation UI did not append the reversal movement.'}
            $evidence.PurchaseCancellationLiveUiInventoryRestored=$true; $evidence.PurchaseCancellationLiveUiMovementChanged=$true
            $purchaseUiFinanceAfter=Finance-Snapshot $clone; Equal-Counts $purchaseUiFinanceBefore $purchaseUiFinanceAfter
            $evidence.PurchaseCancellationLiveUiFinanceRestored=$true
            $cancellationCount=[int](Scalar ('SELECT COUNT(*) FROM ['+$clone+'].dbo.JournalEntriesTb WHERE SourceType=N''PurchaseInvoiceCancellation'' AND SourceId=@Id;') @{'@Id'=$uiPurchaseId})
            if($cancellationCount -ne 1){throw 'Expected exactly one cancellation journal for the live UI purchase fixture.'}
            $evidence.PurchaseCancellationLiveUiPurchaseId=$uiPurchaseId
            $evidence.PurchaseCancellationLiveUiInventoryBefore=$purchaseUiInventoryBefore; $evidence.PurchaseCancellationLiveUiInventoryAfter=$purchaseUiInventoryAfter
            $evidence.PurchaseCancellationLiveUiFinanceBefore=$purchaseUiFinanceBefore; $evidence.PurchaseCancellationLiveUiFinanceAfter=$purchaseUiFinanceAfter
        } finally { Remove-Item Env:PHARMACY_QA_UI_USER,Env:PHARMACY_QA_UI_PASSWORD,Env:PHARMACY_QA_UI_PURCHASE_ID -ErrorAction SilentlyContinue }
    }
    if($IncludeLiveSalesUi) {
        $liveAppDirectory=Join-Path $run 'live-sales-ui-app'
        [void](New-Item -ItemType Directory -Path $liveAppDirectory -Force)
        Copy-Item -Path (Join-Path $taskRoot 'project_fo_3\bin\Release\*') -Destination $liveAppDirectory -Recurse -Force
        $liveApp=Join-Path $liveAppDirectory 'project_fo_3.exe'
        $liveConfigPath=$liveApp+'.config'
        [xml]$liveConfig=Get-Content -LiteralPath $liveConfigPath -Raw -Encoding UTF8
        $forceProfile=$liveConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
        if($null -eq $forceProfile) { $forceProfile=$liveConfig.CreateElement('add'); $forceProfile.SetAttribute('key','ForceBuildConnectionProfile'); [void]$liveConfig.configuration.appSettings.AppendChild($forceProfile) }
        $forceProfile.SetAttribute('value','true')
        $liveConnection='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True'
        foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
            $connectionNode=$liveConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
            if($null -eq $connectionNode){throw ('Missing isolated live UI connection: '+$connectionName)}
            $connectionNode.SetAttribute('connectionString',$liveConnection)
        }
        $liveConfig.Save($liveConfigPath)
        $liveProfilePath=Join-Path $run 'live-sales-ui-profile.log'
        $liveProfileErrorPath=Join-Path $run 'live-sales-ui-profile-error.log'
        $liveProfile=Start-Process -FilePath $liveApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $liveProfilePath -RedirectStandardError $liveProfileErrorPath
        $liveProfileText=Get-Content -LiteralPath $liveProfilePath -Raw -Encoding UTF8
        if($liveProfile.ExitCode -ne 0 -or $liveProfileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Live sales UI did not pin its connection to the owned temporary QA clone.'}

        $liveInventoryBefore=Inventory-Snapshot $clone
        $liveFinanceBefore=Finance-Snapshot $clone
        $evidence.LiveSalesUiInventoryBefore=$liveInventoryBefore
        $evidence.LiveSalesUiFinanceBefore=$liveFinanceBefore
        $suffix='QA-LIVE-'+$token.Substring(0,12)
        $employeeId=$suffix
        $userName='qa-live-'+$token.Substring(0,12)
        $password=[Guid]::NewGuid().ToString('N')
        $salt=New-Object byte[] 16
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
        $derivation=[Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,100000,[Security.Cryptography.HashAlgorithmName]::SHA256)
        try { $hashBytes=$derivation.GetBytes(32) } finally { $derivation.Dispose() }
        $passwordHash='PBKDF2-SHA256$100000$'+[Convert]::ToBase64String($salt)+'$'+[Convert]::ToBase64String($hashBytes)
        $fixtureConnection=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaLiveSalesFixture')
        try {
            $fixtureConnection.Open()
            $fixtureCommand=$fixtureConnection.CreateCommand()
            $fixtureCommand.CommandText='INSERT dbo.EmployeeTb1 (EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole) VALUES (@Id,@Name,0,30,N'''',@Password,N''Manager'');'
            [void]$fixtureCommand.Parameters.Add('@Id',[Data.SqlDbType]::NVarChar,50)
            [void]$fixtureCommand.Parameters.Add('@Name',[Data.SqlDbType]::NVarChar,200)
            [void]$fixtureCommand.Parameters.Add('@Password',[Data.SqlDbType]::NVarChar,200)
            $fixtureCommand.Parameters['@Id'].Value=$employeeId
            $fixtureCommand.Parameters['@Name'].Value=$userName
            $fixtureCommand.Parameters['@Password'].Value=$passwordHash
            if($fixtureCommand.ExecuteNonQuery() -ne 1){throw 'Could not add the temporary manager to the owned QA clone.'}
        } finally { if($fixtureCommand){$fixtureCommand.Dispose()}; $fixtureConnection.Dispose() }

        $env:PHARMACY_QA_UI_USER=$userName
        $env:PHARMACY_QA_UI_PASSWORD=$password
        $uiRunner=Join-Path $taskRoot 'tests\PharmacyManagement.UiAutomation\bin\Release\net10.0-windows\PharmacyManagement.UiAutomation.dll'
        if(-not (Test-Path -LiteralPath $uiRunner)){throw 'Build the FlaUI runner before the live sales UI test.'}
        $liveUiArtifacts=Join-Path $run 'live-sales-ui-results'
        $liveUiLog=Join-Path $run 'live-sales-ui.log'
        $liveUiError=Join-Path $run 'live-sales-ui-error.log'
        try {
            $liveUiProcess=Start-Process -FilePath 'dotnet' -ArgumentList @($uiRunner,'--exe',$liveApp,'--artifacts',$liveUiArtifacts,'--scenario','billing-live-qa') -WorkingDirectory $liveAppDirectory -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $liveUiLog -RedirectStandardError $liveUiError
            $evidence.LiveSalesUiExitCode=$liveUiProcess.ExitCode
            Get-Content -LiteralPath $liveUiLog -Tail 50 -ErrorAction SilentlyContinue
            Get-Content -LiteralPath $liveUiError -Tail 20 -ErrorAction SilentlyContinue
            if($evidence.LiveSalesUiExitCode -ne 0){throw 'Live QA point-of-sale save/cancel UI scenario failed.'}
            $evidence.LiveSalesUiVerified=$true
            $liveInventoryAfter=Inventory-Snapshot $clone
            foreach($key in @('StockSHA256','LotsSHA256')) { if($liveInventoryBefore[$key] -ne $liveInventoryAfter[$key]){throw ('Live sale cancellation did not restore inventory snapshot: '+$key)} }
            if($liveInventoryBefore['MovementsSHA256'] -eq $liveInventoryAfter['MovementsSHA256']){throw 'Live sale cancellation did not append a compensating inventory movement.'}
            $evidence.LiveSalesUiMovementLedgerChanged=$true
            $liveFinanceAfter=Finance-Snapshot $clone; Equal-Counts $liveFinanceBefore $liveFinanceAfter
            $evidence.LiveSalesUiInventoryAfter=$liveInventoryAfter; $evidence.LiveSalesUiInventoryRestored=$true
            $evidence.LiveSalesUiFinanceAfter=$liveFinanceAfter; $evidence.LiveSalesUiFinanceRestored=$true
        } finally {
            Remove-Item Env:PHARMACY_QA_UI_USER -ErrorAction SilentlyContinue
            Remove-Item Env:PHARMACY_QA_UI_PASSWORD -ErrorAction SilentlyContinue
        }
    }    if($IncludeCommittedCashBoxSale) {
        $releaseDirectory=Join-Path $taskRoot 'project_fo_3\bin\Release'
        $cashboxAppDirectory=Join-Path $run 'cashbox-sale-app'
        [void](New-Item -ItemType Directory -Path $cashboxAppDirectory -Force)
        Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $cashboxAppDirectory -Recurse -Force
        $cashboxApp=Join-Path $cashboxAppDirectory 'project_fo_3.exe'
        $cashboxConfigPath=$cashboxApp+'.config'
        [xml]$cashboxConfig=Get-Content -LiteralPath $cashboxConfigPath -Raw -Encoding UTF8
        $cashboxForceSetting=$cashboxConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
        if($null -eq $cashboxForceSetting) { $cashboxForceSetting=$cashboxConfig.CreateElement('add'); $cashboxForceSetting.SetAttribute('key','ForceBuildConnectionProfile'); [void]$cashboxConfig.configuration.appSettings.AppendChild($cashboxForceSetting) }
        $cashboxForceSetting.SetAttribute('value','true')
        $cashboxConnection='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True'
        foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
            $connectionNode=$cashboxConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
            if($null -eq $connectionNode){throw ('Missing isolated cashbox connection: '+$connectionName)}
            $connectionNode.SetAttribute('connectionString',$cashboxConnection)
        }
        $cashboxConfig.Save($cashboxConfigPath)
        $cashboxProfilePath=Join-Path $run 'cashbox-sale-profile.log'
        $cashboxProfileErrorPath=Join-Path $run 'cashbox-sale-profile-error.log'
        $cashboxProfile=Start-Process -FilePath $cashboxApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $cashboxProfilePath -RedirectStandardError $cashboxProfileErrorPath
        $cashboxProfileText=Get-Content -LiteralPath $cashboxProfilePath -Raw -Encoding UTF8
        if($cashboxProfile.ExitCode -ne 0 -or $cashboxProfileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Cashbox sale test did not pin its connection to the owned temporary QA clone.'}
        $cashboxLog=Join-Path $run 'cashbox-sale-roundtrip.log'
        $cashboxError=Join-Path $run 'cashbox-sale-roundtrip-error.log'
        $cashboxArgs=@('--verify-qa-cashbox-sale-roundtrip',$backup,$token,'--confirm-qa-cashbox-sale')
        $cashboxProcess=Start-Process -FilePath $cashboxApp -ArgumentList $cashboxArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $cashboxLog -RedirectStandardError $cashboxError
        $evidence.CashBoxSaleExitCode=$cashboxProcess.ExitCode
        Get-Content -LiteralPath $cashboxLog -Tail 40
        Get-Content -LiteralPath $cashboxError -Tail 20 -ErrorAction SilentlyContinue
        if($evidence.CashBoxSaleExitCode -ne 0){throw 'Owned temporary QA cashier cashbox sale/cancellation roundtrip failed.'}
        $evidence.CashBoxSaleVerified=$true
    }
    if($IncludeCommercialReturnRollback) {
        $application=if($ExecutablePath){$ExecutablePath}else{Join-Path $taskRoot 'project_fo_3\bin\Release\project_fo_3.exe'}
        $evidence.CommercialReturnApplication=$application
        if(-not (Test-Path -LiteralPath $application)){throw 'Build the Release application before the QA commercial-return verification.'}
        $ownerCheck=[string](Scalar ('SELECT CONVERT(nvarchar(64),value) FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{})
        if($ownerCheck -ne $token){throw 'Commercial return test target is not the owned temporary QA clone.'}
        $evidence.CommercialReturnApplicationSHA256=(Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash
        $returnLog=Join-Path $run 'commercial-return-roundtrip.log'
        $returnError=Join-Path $run 'commercial-return-roundtrip-error.log'
        $returnArgs=@('--verify-qa-commercial-return-roundtrip','.',$clone,'--confirm-qa-commercial-return-writes')
        $returnProcess=Start-Process -FilePath $application -ArgumentList $returnArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $returnLog -RedirectStandardError $returnError
        $evidence.CommercialReturnExitCode=$returnProcess.ExitCode
        Get-Content -LiteralPath $returnLog -Tail 40
        Get-Content -LiteralPath $returnError -Tail 20 -ErrorAction SilentlyContinue
        if($evidence.CommercialReturnExitCode -ne 0){throw 'Owned temporary QA commercial return rollback verification failed.'}
        $evidence.CommercialReturnVerified=$true
    }
    if($IncludeCommercialReturnConcurrency) {
        $actualOwner=[string](Scalar ('SELECT CONVERT(nvarchar(64),value) FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{})
        if($actualOwner -ne $token){throw 'Committed return target is not the owned sandbox.'}
        $evidence.CommercialReturnConcurrencyApplicationSHA256=(Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
        foreach($check in @('concurrency','replay')) {
            $output=Join-Path $run ('commercial-return-'+$check+'.stdout.log')
            $errors=Join-Path $run ('commercial-return-'+$check+'.stderr.log')
            $arguments=@("--verify-qa-commercial-return-$check",'.',$clone,'--confirm-temporary-concurrency-database-writes')
            $process=Start-Process -FilePath $ExecutablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $output -RedirectStandardError $errors
            $evidence['CommercialReturn'+$check+'ExitCode']=$process.ExitCode
            $evidence['CommercialReturn'+$check+'ProcessId']=$process.Id
            Get-Content -LiteralPath $output -Tail 8
            Get-Content -LiteralPath $errors -Tail 8
            if($process.ExitCode -ne 0){throw ('Owned committed return '+$check+' failed.')}
        }
        if($IncludeCommercialReturnDurableRecovery) {
            $runtime=Join-Path $run 'return-runtime'
            [void](New-Item -ItemType Directory -Path $runtime)
            $runtimeExecutable=Join-Path $runtime 'project_fo_3.exe'
            Copy-Item -LiteralPath $ExecutablePath -Destination $runtimeExecutable
            Copy-Item -LiteralPath ($ExecutablePath+'.config') -Destination ($runtimeExecutable+'.config')
            Get-ChildItem -LiteralPath (Split-Path -Parent $ExecutablePath) -File -Filter '*.dll' | Copy-Item -Destination $runtime
            [xml]$configuration=Get-Content -LiteralPath ($runtimeExecutable+'.config') -Raw
            $forced=$configuration.configuration.appSettings.SelectSingleNode('add[@key="ForceBuildConnectionProfile"]')
            if(-not $forced){$forced=$configuration.CreateElement('add');$forced.SetAttribute('key','ForceBuildConnectionProfile');[void]$configuration.configuration.appSettings.AppendChild($forced)}
            $forced.SetAttribute('value','true')
            $connection=[Data.SqlClient.SqlConnectionStringBuilder]::new('Data Source=.;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
            $connection.set_InitialCatalog($clone)
            foreach($entry in $configuration.configuration.connectionStrings.add){$entry.SetAttribute('connectionString',$connection.ConnectionString)}
            $configuration.Save($runtimeExecutable+'.config')
            $evidence.CommercialReturnDurableExecutableSHA256=(Get-FileHash -LiteralPath $runtimeExecutable -Algorithm SHA256).Hash
            foreach($phase in @('write','replay')) {
                $output=Join-Path $run ('durable-'+$phase+'.stdout.log'); $errors=Join-Path $run ('durable-'+$phase+'.stderr.log')
                $interruptedWrite=$IncludeCommercialReturnProcessInterruption -and $phase -eq 'write'
                $command=if($interruptedWrite){'--verify-qa-commercial-return-durable-await-stop'}else{"--verify-qa-commercial-return-durable-$phase"}
                $arguments=@($command,'.',$clone,(Join-Path $run 'return-pending'),'--confirm-temporary-concurrency-database-writes')
                if($interruptedWrite) {
                    $process=Invoke-InterruptedReturnWrite -Executable $runtimeExecutable -Arguments $arguments -Output $output -Errors $errors -ReadyPath (Join-Path $run 'return-pending\process-stop-ready.json') -Owner $token -Target $clone
                    $evidence.CommercialReturnProcessInterruption=$process
                } else {
                    $process=Start-Process -FilePath $runtimeExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $output -RedirectStandardError $errors
                }
                $evidence['CommercialReturnDurable'+$phase+'ExitCode']=$process.ExitCode
                $evidence['CommercialReturnDurable'+$phase+'ProcessId']=$process.Id
                Get-Content -LiteralPath $output -Tail 8; Get-Content -LiteralPath $errors -Tail 8
                if(-not $interruptedWrite -and $process.ExitCode -ne 0){throw ('Owned durable return '+$phase+' failed.')}
            }
            $evidence.CommercialReturnDurableVerified=$true
        }
        Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
        $evidence.CommercialReturnPostCommitCheckDbPassed=$true
        $evidence.CommercialReturnConcurrencyVerified=$true
        $evidence.CommercialReturnReplayVerified=$true
    }
    if($IncludeCommittedPosting) {
        $runner=Join-Path $taskRoot 'tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe'
        $evidence.RunnerSHA256=(Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash
        & $runner --temporary-qa-accounting-commit . $clone $token --confirm-owned-temporary-qa-writes *> (Join-Path $run 'commit.log')
        $evidence.CommitExitCode=$LASTEXITCODE
        Get-Content -LiteralPath (Join-Path $run 'commit.log')
        if($evidence.CommitExitCode -ne 0){throw 'Owned temporary QA committed posting test failed.'}
    }
    if($IncludeCommittedInventory) {
        $runner=Join-Path $taskRoot 'tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe'
        $evidence.InventoryCommitRunnerSHA256=(Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash
        & $runner --temporary-qa-inventory-commit . $clone $token --confirm-owned-temporary-qa-writes *> (Join-Path $run 'inventory-commit.log')
        $evidence.InventoryCommitExitCode=$LASTEXITCODE
        Get-Content -LiteralPath (Join-Path $run 'inventory-commit.log')
        if($evidence.InventoryCommitExitCode -ne 0){throw 'Owned temporary QA inventory Commit test failed.'}
    }
    if($IncludeInventoryRollback) {
        $application=Join-Path $taskRoot 'project_fo_3\bin\Release\project_fo_3.exe'
        $evidence.InventoryApplicationSHA256=(Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash
        $inventoryProcess=Start-Process -FilePath $application -ArgumentList @('--verify-qa-inventory-rollback','.',$clone,'--confirm-qa-inventory-rollback') -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'inventory.log') -RedirectStandardError (Join-Path $run 'inventory-error.log')
        $evidence.InventoryExitCode=$inventoryProcess.ExitCode
        Get-Content -LiteralPath (Join-Path $run 'inventory.log')
        if($evidence.InventoryExitCode -ne 0){throw 'Owned temporary QA inventory rollback test failed.'}
    }
    if($IncludeLotStockParity -or $IncludeLotStockUiSmoke -or $IncludeInventorySqlCancellation) {
      if($IncludeLotStockParity) {
        $application=Join-Path $taskRoot 'project_fo_3\bin\Release\project_fo_3.exe'
        $evidence.LotApplicationSHA256=(Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash
        $lotId=[guid]::NewGuid()
        $batch='QA-LOT-COMPARE-'+$token.Substring(0,12)
        $medicine=[string](Scalar ('SELECT TOP (1) MedName FROM ['+$clone+'].dbo.MedicineTb1 ORDER BY MedName;') @{})
        if([string]::IsNullOrWhiteSpace($medicine)){throw 'Temporary QA has no medicine for lot parity.'}
        Execute ('INSERT INTO ['+$clone+'].dbo.InventoryLotsTb (LotId,MedName,BatchNumber,ExpiryDate,UnitCost,QuantityReceived,QuantityRemaining,IsRecalled) VALUES (@LotId,@Medicine,@Batch,@Expiry,@Cost,@Received,@Remaining,0);') @{
            '@LotId'=$lotId;'@Medicine'=$medicine;'@Batch'=$batch;'@Expiry'=[datetime]::Today.AddDays(90);'@Cost'=[decimal]1.50;'@Received'=[decimal]3.000;'@Remaining'=[decimal]2.250
        }
        $evidence.LotFixtureId=$lotId.ToString();$evidence.LotFixtureBatch=$batch;$evidence.LotFixtureMedicine=$medicine
        $parityProcess=Start-Process -FilePath $application -ArgumentList @('--verify-qa-stock-readonly','.',$clone) -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'lot-stock-parity.log') -RedirectStandardError (Join-Path $run 'lot-stock-parity-error.log')
        $evidence.LotStockParityExitCode=$parityProcess.ExitCode
        Get-Content -LiteralPath (Join-Path $run 'lot-stock-parity.log')
        if($evidence.LotStockParityExitCode -ne 0){throw 'Application/legacy lot stock parity failed on the owned temporary QA fixture.'}
        $evidence.LotStockParityTested=$true

        $rollbackProcess=Start-Process -FilePath $application -ArgumentList @('--verify-qa-lot-stock-rollback','.',$clone,'--confirm-qa-lot-stock-writes') -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'lot-stock-rollback.log') -RedirectStandardError (Join-Path $run 'lot-stock-rollback-error.log')
        $evidence.LotRollbackExitCode=$rollbackProcess.ExitCode
        Get-Content -LiteralPath (Join-Path $run 'lot-stock-rollback.log')
        if($evidence.LotRollbackExitCode -ne 0){throw 'Owned temporary QA lot stock rollback test failed.'}
        $evidence.LotRollbackTested=$true
        if([int](Scalar ('SELECT COUNT(*) FROM ['+$clone+'].dbo.InventoryLotsTb WHERE LotId=@LotId;') @{'@LotId'=$lotId}) -ne 1){throw 'Lot parity fixture changed during rollback verification.'}
        $evidence.LotFixtureRetainedUntilCleanup=$true
      }

        if($IncludeLotStockUiSmoke -or $IncludeInventorySqlCancellation) {
            $releaseDirectory=Join-Path $taskRoot 'project_fo_3\bin\Release'
            $isolatedAppDirectory=Join-Path $run 'live-ui-app'
            [void](New-Item -ItemType Directory -Path $isolatedAppDirectory -Force)
            Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $isolatedAppDirectory -Recurse -Force
            $isolatedApp=Join-Path $isolatedAppDirectory 'project_fo_3.exe'
            $isolatedConfigPath=$isolatedApp+'.config'
            [xml]$isolatedConfig=Get-Content -LiteralPath $isolatedConfigPath -Raw -Encoding UTF8
            $forceSetting=$isolatedConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'ForceBuildConnectionProfile' } | Select-Object -First 1
            if($null -eq $forceSetting) {
                $forceSetting=$isolatedConfig.CreateElement('add')
                $forceSetting.SetAttribute('key','ForceBuildConnectionProfile')
                [void]$isolatedConfig.configuration.appSettings.AppendChild($forceSetting)
            }
            $forceSetting.SetAttribute('value','true')
            $isolatedConnection='Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaCancelProbe-'+$token
            foreach($connectionName in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')) {
                $connectionNode=$isolatedConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq $connectionName } | Select-Object -First 1
                if($null -eq $connectionNode){throw ('Missing isolated app connection: '+$connectionName)}
                $connectionNode.SetAttribute('connectionString',$isolatedConnection)
            }
            $isolatedConfig.Save($isolatedConfigPath)
            $profilePath=Join-Path $run 'live-ui-profile.log'
            $profileErrorPath=Join-Path $run 'live-ui-profile-error.log'
            $profileProcess=Start-Process -FilePath $isolatedApp -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $profilePath -RedirectStandardError $profileErrorPath
            $profileOutput=Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8
            if($profileProcess.ExitCode -ne 0 -or $profileOutput.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0) {throw 'Isolated UI app did not pin its connection to the temporary QA clone.'}
            $evidence.LiveUiProfileVerified=$true
            $evidence.LiveUiProfile=$profileOutput.Trim()

            $uiRunner=Join-Path $taskRoot 'tests\PharmacyManagement.UiAutomation\bin\Release\net10.0-windows\PharmacyManagement.UiAutomation.dll'
            if(-not (Test-Path -LiteralPath $uiRunner)){throw 'Build the FlaUI runner before the live QA UI smoke.'}
            $uiArtifacts=Join-Path $run 'live-ui-results'
            [void](New-Item -ItemType Directory -Path $uiArtifacts -Force)
            if($IncludeVoucherEditorSql){$IncludeVoucherRequestIdentity=$true}
if($IncludeLotStockUiSmoke) {
            foreach($scenario in @('inventory-stock-liveqa','inventory-lots-liveqa','inventory-summary-liveqa','inventory-turnover-liveqa','inventory-alerts-liveqa','inventory-movement-liveqa','inventory-integrity-liveqa')) {
                $uiLog=Join-Path $run ($scenario+'.log')
                & dotnet $uiRunner --exe $isolatedApp --artifacts $uiArtifacts --scenario $scenario *> $uiLog
                $scenarioExit=$LASTEXITCODE
                Get-Content -LiteralPath $uiLog -Tail 40
                switch($scenario){
                    'inventory-stock-liveqa' {$evidence.LiveUiStockExitCode=$scenarioExit}
                    'inventory-lots-liveqa' {$evidence.LiveUiLotsExitCode=$scenarioExit}
                    'inventory-summary-liveqa' {$evidence.LiveUiSummaryExitCode=$scenarioExit}
                    'inventory-turnover-liveqa' {$evidence.LiveUiTurnoverExitCode=$scenarioExit}
                    'inventory-alerts-liveqa' {$evidence.LiveUiAlertsExitCode=$scenarioExit}
                    'inventory-movement-liveqa' {$evidence.LiveUiMovementExitCode=$scenarioExit}
                    'inventory-integrity-liveqa' {$evidence.LiveUiIntegrityExitCode=$scenarioExit}
                }
                if($scenarioExit -ne 0){throw ('Live QA inventory UI smoke failed: '+$scenario)}
            }
            $evidence.LiveUiSmokePassed=$true
            }
            if($IncludeInventorySqlCancellation) {
                $uiRunner=Join-Path $taskRoot 'tests\PharmacyManagement.UiAutomation\bin\Release\net10.0-windows\PharmacyManagement.UiAutomation.dll'
                if(-not (Test-Path -LiteralPath $uiRunner)){throw 'Build the FlaUI runner before the live SQL cancellation smoke.'}
                $blocker=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog='+$clone+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy.QaCancelBlocker-'+$token)
                $transaction=$null; $lockCommand=$null; $cancelProcess=$null
                try {
                    $blocker.Open()
                    $transaction=$blocker.BeginTransaction([Data.IsolationLevel]::ReadCommitted)
                    $lockCommand=$blocker.CreateCommand(); $lockCommand.Transaction=$transaction; $lockCommand.CommandTimeout=10
                    $probeConstraint='CK_QaCancelProbe_'+$token
                    $blockerSessionCommand=$blocker.CreateCommand(); $blockerSessionCommand.Transaction=$transaction; $blockerSessionCommand.CommandText='SELECT @@SPID;'; $blockerSessionId=[int]$blockerSessionCommand.ExecuteScalar(); $blockerSessionCommand.Dispose()
                    $lockCommand.CommandText='ALTER TABLE dbo.MedicineTb1 WITH NOCHECK ADD CONSTRAINT ['+$probeConstraint+'] CHECK (1=1);'
                    [void]$lockCommand.ExecuteNonQuery()
                    $cancelSignal=Join-Path $run 'inventory-sql-cancellation.close'
                    $cancelOutput=Join-Path $run 'inventory-sql-cancellation-ui.log'
                    $cancelError=Join-Path $run 'inventory-sql-cancellation-ui-error.log'
                    $cancelArgs=@($uiRunner,'--exe',$isolatedApp,'--artifacts',$uiArtifacts,'--scenario','inventory-movement-cancel-loading','--cancel-signal',$cancelSignal)
                    $cancelProcess=Start-Process -FilePath 'dotnet' -ArgumentList $cancelArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $cancelOutput -RedirectStandardError $cancelError
                    $deadline=[DateTime]::UtcNow.AddSeconds(20)
                    while([DateTime]::UtcNow -lt $deadline) {
                        $waitCount=[int](Scalar 'SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID(@Db) AND blocking_session_id=@Blocker AND wait_type LIKE N''LCK_M_%'';' @{'@Db'=$clone;'@Blocker'=$blockerSessionId})
                        if($waitCount -gt 0){$evidence.SqlCancellationLockWaitObserved=$true;break}
                        if($cancelProcess.HasExited){break}
                        Start-Sleep -Milliseconds 100
                    }
                    if(-not $evidence.SqlCancellationLockWaitObserved){throw 'Could not observe the QA app waiting on the schema-modification lock.'}
                    [void](New-Item -ItemType File -Path $cancelSignal -Force)
                    if(-not $cancelProcess.WaitForExit(20000)){throw 'The UI did not close the report promptly while SQL was blocked.'}
                    $evidence.LiveUiCancelLoadingExitCode=$cancelProcess.ExitCode
                    Get-Content -LiteralPath $cancelOutput -Tail 30 -ErrorAction SilentlyContinue
                    Get-Content -LiteralPath $cancelError -Tail 15 -ErrorAction SilentlyContinue
                    if($evidence.LiveUiCancelLoadingExitCode -ne 0){throw 'The live UI did not close cleanly during the blocked SQL read.'}
                    $clearDeadline=[DateTime]::UtcNow.AddSeconds(10)
                    do {
                        $activeCount=[int](Scalar 'SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID(@Db) AND blocking_session_id=@Blocker;' @{'@Db'=$clone;'@Blocker'=$blockerSessionId})
                        if($activeCount -eq 0){$evidence.SqlCancellationRequestCleared=$true;break}
                        Start-Sleep -Milliseconds 100
                    } while([DateTime]::UtcNow -lt $clearDeadline)
                    if(-not $evidence.SqlCancellationRequestCleared){throw 'The SQL request remained active after closing the report.'}
                    $evidence.SqlCancellationTested=$true
                    Write-Output 'PASS: the live QA report waited on a controlled table lock, closed, and its SQL request cleared before the blocker was released.'
                } finally {
                    if($lockCommand){$lockCommand.Dispose()}
                    if($transaction){try{$transaction.Rollback()}catch{}; $transaction.Dispose()}
                    if($blocker){$blocker.Dispose()}
                }
            }
        }
    }
    $after=Counts 'PharmacyManagement_qa'; Equal-Counts $before $after
    $evidence.SourceCountsAfter=$after; $evidence.SourceCountsUnchanged=$true
    $inventoryAfter=Inventory-Snapshot 'PharmacyManagement_qa'; Equal-Counts $inventoryBefore $inventoryAfter; $evidence.SourceInventoryAfter=$inventoryAfter; $evidence.SourceInventoryUnchanged=$true
    Write-Output 'PASS: full QA backup restored to a distinct temporary database; DBCC CHECKDB and accounting row counts matched. Source QA counts unchanged.'
    if($IncludeLotStockParity){Write-Output 'PASS: live lot fixture matched legacy and Application readers; lot state/rollback checks passed on the owned temporary QA copy.'}
    if($IncludeVoucherEditorSql){$IncludeVoucherRequestIdentity=$true}
if($IncludeLotStockUiSmoke){Write-Output 'PASS: live stock, lot, movement summary, item movement, turnover, alerts, and integrity reports loaded on the owned temporary QA copy.'}
} finally {
    try {
        if($created -and $owned -and $master.State -eq [Data.ConnectionState]::Open) {
            $master.ChangeDatabase('master')
            $actualId=[int](Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$clone})
            $actualOwner=[string](Scalar ('SELECT value FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{})
            if($actualId -ne $targetId -or $actualOwner -ne $token){throw 'Temporary restore ownership changed; cleanup refused.'}
            Execute ('ALTER DATABASE ['+$clone+'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ['+$clone+'];') @{}
            $evidence.CleanupPassed=((Scalar 'SELECT DB_ID(@Name);' @{'@Name'=$clone}) -is [DBNull])
            if(-not $evidence.CleanupPassed){throw 'Temporary QA restore cleanup failed.'}
        }
    } catch {
        $evidence.CleanupError=$_.Exception.Message
        throw
    } finally {
    $sourceVerificationFailure=$null
    if($master.State -eq [Data.ConnectionState]::Open -and $null -ne $before) {
        try {
            if($IncludeVoucherRequestIdentity -and $null -ne $voucherSourceBefore){$voucherFinal=Voucher-DataSnapshot 'PharmacyManagement_qa';Equal-Counts $voucherSourceBefore $voucherFinal;$evidence.VoucherRequestSourceDataAfter=$voucherFinal;$evidence.VoucherRequestSourceDataUnchanged=$true}
            $sourceCountsAtEnd=Counts 'PharmacyManagement_qa'; Equal-Counts $before $sourceCountsAtEnd
            $evidence.SourceCountsAfter=$sourceCountsAtEnd; $evidence.SourceCountsUnchanged=$true
            $finalColumns=Column-Snapshot 'PharmacyManagement_qa'; if($finalColumns -ne $columnSchemaBefore){throw 'Source column schema changed.'}; $evidence.ColumnSchemaAfter=$finalColumns; $evidence.SourceColumnSchemaUnchanged=$true
            $finalValues=Accounting-ValueSnapshot 'PharmacyManagement_qa'; Equal-Counts $accountingValuesBefore $finalValues; $evidence.AccountingValuesAfter=$finalValues; $evidence.SourceAccountingValuesUnchanged=$true
            $sourceInventoryAtEnd=Inventory-Snapshot 'PharmacyManagement_qa'; Equal-Counts $inventoryBefore $sourceInventoryAtEnd
            $evidence.SourceInventoryAfter=$sourceInventoryAtEnd; $evidence.SourceInventoryUnchanged=$true
        } catch { $evidence.SourceVerificationError=$_.Exception.Message; $sourceVerificationFailure=$_ }
    }
        $master.Dispose()
        $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8
        Write-Output ('Evidence: '+$run)
        if($sourceVerificationFailure){throw $sourceVerificationFailure}
    }
}
