[CmdletBinding()]
param(
    [string]$ServerInstance='.',
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
    [string]$EvidenceRoot=(Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\qa-owned-journal-posting'),
    [switch]$ConfirmOwnedQaJournalPosting,
    [switch]$RequireLedgerPerformanceAcceptance
)
$ErrorActionPreference='Stop'
if(-not $ConfirmOwnedQaJournalPosting){throw 'Explicit -ConfirmOwnedQaJournalPosting is required; this creates a temporary restored QA database and creates one manager and two test journal entries only inside it.'}
if([string]::IsNullOrWhiteSpace($ServerInstance)){throw 'SQL Server instance is required.'}
  $releaseDirectory=[IO.Path]::GetFullPath($ReleaseDirectory)
  if(-not (Test-Path -LiteralPath $releaseDirectory -PathType Container)){throw 'ReleaseDirectory must be an existing build directory.'}
  if(-not (Test-Path -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe') -PathType Leaf) -or
     -not (Test-Path -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe.config') -PathType Leaf)){throw 'The ReleaseDirectory must contain the application and its config.'}
  $repositoryRoot=Split-Path -Parent $PSScriptRoot
  $evidenceRoot=[IO.Path]::GetFullPath($EvidenceRoot)
  # The work moved to the repository drive, so the evidence may live beside the sources or in the
  # legacy work folder. The temporary database, the verified backup and the cleanup stay mandatory.
  $allowedEvidenceRoots=@('C:\PharmacyQA\work',(Join-Path $repositoryRoot 'artifacts'))
  $allowed=$false
  foreach($allowedRoot in $allowedEvidenceRoots){
    $prefix=(Join-Path $allowedRoot '')
    if($evidenceRoot -ieq $allowedRoot -or $evidenceRoot.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){$allowed=$true;break}
  }
  if(-not $allowed){throw 'EvidenceRoot must remain under the repository artifacts folder or the legacy work folder.'}
$token=[Guid]::NewGuid().ToString('N')
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$clone='PharmacyRestore_'+$stamp+'_'+$token+'_qa'
if($clone -notmatch '^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$'){throw 'Temporary database name did not pass its ownership pattern.'}
$run=Join-Path $evidenceRoot ('qa-journal-posting-'+$token)
[void](New-Item -ItemType Directory -Path $run -Force)
$backup=Join-Path $run 'PharmacyManagement_qa_journal_posting.bak'
$master=[Data.SqlClient.SqlConnection]::new("Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=15;Application Name=Pharmacy.QaJournalPosting")
$created=$false;$owned=$false;$targetId=$null;$evidence=[ordered]@{Source='PharmacyManagement_qa';Target=$clone;Token=$token;Backup=$backup;BackupVerified=$false;RestorePassed=$false;CheckDbPassed=$false;RuntimeProfilePinned=$false;JournalPostingPassed=$false;JournalCrashRecoveryPassed=$false;SourceUnchanged=$false;CleanupPassed=$false}
function New-Command([string]$sql,[hashtable]$parameters){$c=$master.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=600;foreach($key in $parameters.Keys){[void]$c.Parameters.AddWithValue($key,$parameters[$key])};return $c}
function Scalar([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
function Table([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;$t=[Data.DataTable]::new();try{$a=[Data.SqlClient.SqlDataAdapter]::new($c);try{[void]$a.Fill($t)}finally{$a.Dispose()}}finally{$c.Dispose()};return ,$t}
function Snapshot([string]$database,[string]$excludeSource='',[object[]]$ownedEditorDocs=@()) {
    if($database -notmatch '^(PharmacyManagement_qa|PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa)$'){throw 'Unexpected snapshot database name.'}
    $all=[Text.StringBuilder]::new(); $finance=[Text.StringBuilder]::new()
    $financialTables=@('AccountingPeriodsTb','MedicineTb1','InventoryLotsTb','InventoryMovementsTb','CustomersBillsTb','CompanyTb1','AccountsTb','CostCentersTb','JournalEntriesTb','JournalEntryLinesTb','CashBoxJournalLinksTb')
    $tables=[ordered]@{AccountingPeriodsTb='PeriodId';MedicineTb1='MedName';InventoryLotsTb='LotId';InventoryMovementsTb='MovementId';CustomersBillsTb='CustomerId';CompanyTb1='CompId';AccountsTb='AccountCode';CostCentersTb='CostCenterId';EmployeeTb1='EmpId';CashBoxesTb='CashBoxId';CashBoxAssignmentsTb='EmpId';JournalEntriesTb='JournalEntryId';JournalEntryLinesTb='JournalEntryLineId';CashBoxJournalLinksTb='JournalEntryLineId';AuditLogTb='AuditId';EmployeeOperationPermissionsTb='EmpId,OperationName'}
    foreach($table in $tables.Keys){
        $xml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] ORDER BY "+$tables[$table]+" FOR XML RAW('r'),ROOT('rows'),TYPE));") @{})
        [void]$all.Append($table).Append($xml)
        if($table -in $financialTables){
            $financialXml=$xml
            if($excludeSource -and $table -in @('JournalEntriesTb','JournalEntryLinesTb')){
                if($excludeSource -notmatch '^[a-f0-9]{32}$'){throw 'Invalid owned journal source token.'}
                $parameters=@{'@Source'=$excludeSource;'@AsyncSource'=$excludeSource+'-A'}
                $ownedFilter="(SourceType=N'QAJournalAcceptance' AND SourceId IN (@Source,@AsyncSource))"
                for($index=0;$index -lt $ownedEditorDocs.Count;$index++){
                    $document=$ownedEditorDocs[$index]
                    if($document.kind -notin @('Manual','OpeningEntry') -or $document.source -notmatch '^[a-f0-9]{32}$' -or [int]$document.id -le 0 -or $document.user -ne ('QA Journal Manager '+$excludeSource.Substring(0,12))){throw 'Owned editor normalization record invalid'}
                    $ownedFilter+=" OR (SourceType=@Kind$index AND SourceId=@Source$index AND JournalEntryId=@Journal$index AND CreatedBy=@User$index)"
                    $parameters['@Kind'+$index]=[string]$document.kind;$parameters['@Source'+$index]=[string]$document.source
                    $parameters['@Journal'+$index]=[int]$document.id;$parameters['@User'+$index]=[string]$document.user
                }
                $financialXml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] WHERE JournalEntryId NOT IN (SELECT JournalEntryId FROM ["+$database+"].dbo.JournalEntriesTb WHERE "+$ownedFilter+") ORDER BY "+$tables[$table]+" FOR XML RAW('r'),ROOT('rows'),TYPE));") $parameters)
            }
            [void]$finance.Append($table).Append($financialXml)
        }
    }
    $triggers=[string](Scalar ("USE ["+$database+"]; SELECT CONVERT(nvarchar(max),(SELECT name,OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name FOR XML RAW('r'),ROOT('rows'),TYPE));") @{})
    [void]$all.Append('Triggers').Append($triggers);$master.ChangeDatabase('master')
    $sha=[Security.Cryptography.SHA256]::Create()
    try{
        $stateHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($all.ToString()))).Replace('-','')
        $financialHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($finance.ToString()))).Replace('-','')
    }finally{$sha.Dispose()}
    return [ordered]@{JournalHeaders=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.JournalEntriesTb;") @{});JournalLines=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.JournalEntryLinesTb;") @{});Permissions=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.EmployeeOperationPermissionsTb;") @{});Boxes=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxesTb;") @{});Assignments=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxAssignmentsTb;") @{});Employees=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.EmployeeTb1;") @{});Audit=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.AuditLogTb;") @{});StateSha256=$stateHash;FinancialSha256=$financialHash}
}
try{
    $master.Open()
    if([string](Scalar 'SELECT DB_NAME();' @{}) -ne 'master'){throw 'Administration connection is not master.'}
    if((Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone}) -isnot [DBNull]){throw 'Refusing to overwrite an existing database.'}
    $sourceBefore=Snapshot 'PharmacyManagement_qa';$evidence.SourceBefore=$sourceBefore
    Execute 'BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM,INIT;' @{'@Path'=$backup}
    Execute 'RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;' @{'@Path'=$backup}
    $evidence.BackupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    $headers=Table 'RESTORE HEADERONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($headers.Rows.Count -ne 1 -or [string]$headers.Rows[0]['DatabaseName'] -ne 'PharmacyManagement_qa' -or [int]$headers.Rows[0]['BackupType'] -ne 1 -or -not [bool]$headers.Rows[0]['HasBackupChecksums'] -or [bool]$headers.Rows[0]['IsDamaged']){throw 'Backup is not one verified, checksummed full backup of PharmacyManagement_qa.'}
    $evidence.BackupVerified=$true
    $dataFile=[string](Scalar "SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id=DB_ID(N'PharmacyManagement_qa') AND type=0 ORDER BY file_id;" @{})
    if([string]::IsNullOrWhiteSpace($dataFile)){throw 'SQL Server data directory could not be resolved.'}
    $dataDirectory=[IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($dataFile))
    $files=Table 'RESTORE FILELISTONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($files.Rows.Count -eq 0){throw 'Backup file list is empty.'}
    $moves=[Collections.Generic.List[string]]::new();$paths=[Collections.Generic.List[string]]::new();$index=0
    foreach($file in $files.Rows){$index++;$kind=[string]$file['Type'];if($kind -notin @('D','L')){throw 'Unsupported backup file type.'};$extension=if($kind -eq 'L'){'.ldf'}elseif($index -eq 1){'.mdf'}else{'.ndf'};$targetPath=[IO.Path]::GetFullPath((Join-Path $dataDirectory ($clone+'_'+$index+$extension)));if(-not $targetPath.StartsWith($dataDirectory.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.File]::Exists($targetPath) -or (Scalar 'SELECT COUNT(*) FROM sys.master_files WHERE physical_name=@Path;' @{'@Path'=$targetPath}) -ne 0){throw 'Restore file path is unsafe or already owned.'};$logical=([string]$file['LogicalName']).Replace("'","''");$moves.Add("MOVE N'$logical' TO N'$($targetPath.Replace("'","''"))'");$paths.Add($targetPath)}
    $evidence.TargetFiles=$paths.ToArray()
    Execute ('RESTORE DATABASE ['+$clone+'] FROM DISK=@Path WITH CHECKSUM,RECOVERY,'+($moves -join ', ')+';') @{'@Path'=$backup}
    $created=$true;$targetId=[int](Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone})
    Execute ('USE ['+$clone+']; EXEC sys.sp_addextendedproperty @name=N''QaTest.Owner'',@value=@Token;') @{'@Token'=$token}
      $owned=$true;$master.ChangeDatabase('master')
      Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
      $evidence.CheckDbPassed=$true
      # The owned clone receives the pending schema migrations, exactly like a deployment would:
      # the new binary reads the ledger through the new indexes.
      $migrationDirectory=Join-Path $repositoryRoot 'database\migrations'
      $master.ChangeDatabase($clone)
      $applied=@()
      foreach($migration in (Get-ChildItem -LiteralPath $migrationDirectory -Filter '*.sql' -File|Sort-Object Name)){
        if([string](Scalar 'SELECT DB_NAME();' @{}) -ne $clone){throw ('The migration session left the owned clone before '+$migration.Name+'.')}
        $script=[IO.File]::ReadAllText($migration.FullName)
        foreach($batch in [Text.RegularExpressions.Regex]::Split($script,'(?im)^\s*GO\s*$')){
          if([string]::IsNullOrWhiteSpace($batch)){continue}
          $command=$master.CreateCommand();$command.CommandText=$batch;$command.CommandTimeout=600;$command.ExecuteNonQuery()|Out-Null;$command.Dispose()
        }
        $applied+=$migration.Name
      }
      $master.ChangeDatabase('master')
      $evidence.MigrationsApplied=$applied
      # Deployment prerequisite: the migrated clone must carry the exact ledger read indexes, so a
      # binary that contains the new read is never accepted against a database without them.
      $schemaGuard=Join-Path $PSScriptRoot 'Test-SchemaPrerequisites.ps1'
      & $schemaGuard -ServerInstance $ServerInstance -Database $clone
      if($LASTEXITCODE -ne 0){throw ('The migrated owned clone '+$clone+' does not satisfy the migration 0032 ledger index contract.')}
      $evidence.LedgerIndexContractPassed=$true
      # The full contract (columns, receipt index, ledger integrity) must hold on the migrated clone too.
      $harmony=Join-Path $PSScriptRoot 'Test-SchemaHarmony.ps1'
      & $harmony -ServerInstance $ServerInstance -Database $clone
      if($LASTEXITCODE -ne 0){throw ('The migrated owned clone failed the schema harmony contract: '+$clone)}
      $evidence.SchemaHarmonyPassed=$true
    $cloneBefore=Snapshot $clone; $evidence.CloneBefore=$cloneBefore
    if($cloneBefore.StateSha256 -ne $sourceBefore.StateSha256){throw 'Restored clone does not match source cashbox/employee/audit/financial snapshots.'}
    $seedRows=[long](Scalar ('SELECT COUNT_BIG(*) FROM ['+$clone+'].dbo.JournalEntryLinesTb WHERE AccountCode=N''1'';') @{})
    if($seedRows -ne 0){throw 'Restored QA cold regression account has preexisting lines; cannot prove this scenario.'}
    $seedXml=[xml]::new();$seedNode=$seedXml.CreateElement('OwnedLedgerPlanSeed');[void]$seedXml.AppendChild($seedNode)
    $seedNode.SetAttribute('token',$token);$seedNode.SetAttribute('database',$clone);$seedNode.SetAttribute('account','1');$seedNode.SetAttribute('rowsBeforeWorkload',[string]$seedRows)
    $seedPath=Join-Path $run 'ledger-plan-seed.xml';$seedXml.Save($seedPath)
    $evidence.LedgerColdSeedRowsBeforeWorkload=$seedRows;$evidence.LedgerColdSeedReport=$seedPath
$appDirectory=Join-Path $run 'journal-app';[void](New-Item -ItemType Directory -Path $appDirectory -Force);Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $appDirectory -Recurse -Force
    $app=Join-Path $appDirectory 'project_fo_3.exe';$configPath=$app+'.config';if(-not (Test-Path -LiteralPath $app)){throw 'Release executable is missing.'}
    [xml]$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
    $force=$config.configuration.appSettings.add|Where-Object{$_.key -eq 'ForceBuildConnectionProfile'}|Select-Object -First 1
    if($null -eq $force){$force=$config.CreateElement('add');[void]$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)};$force.SetAttribute('value','true')
    $connection="Data Source=$ServerInstance;Initial Catalog=$clone;Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaJournalPosting-$token"
    foreach($name in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')){$node=$config.configuration.connectionStrings.add|Where-Object{$_.name -eq $name}|Select-Object -First 1;if($null -eq $node){throw "Missing connection string $name"};$node.SetAttribute('connectionString',$connection)}
    $config.Save($configPath)
    $profileLog=Join-Path $run 'runtime-profile.log';$profileError=Join-Path $run 'runtime-profile.stderr.log';$profile=Start-Process -FilePath $app -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $profileLog -RedirectStandardError $profileError;$profileText=Get-Content -LiteralPath $profileLog -Raw -Encoding UTF8
    if($profile.ExitCode -ne 0 -or $profileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Application connection did not pin to the owned QA restore.'};$evidence.RuntimeProfilePinned=$true;$evidence.RuntimeProfile=$profileText.Trim()
    $importLog=Join-Path $run 'journal-posting.stdout.log';$importError=Join-Path $run 'journal-posting.stderr.log';$journalArguments=@('--verify-qa-journal-posting',$backup,$token,'--confirm-owned-qa-journal-posting');$process=Start-Process -FilePath $app -ArgumentList $journalArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $importLog -RedirectStandardError $importError;if(-not $process.WaitForExit(120000)){Stop-Process -Id $process.Id -Force;throw "Owned QA journal posting verifier timed out."};Get-Content -LiteralPath $importLog -Tail 15 -Encoding UTF8;Get-Content -LiteralPath $importError -Tail 15 -Encoding UTF8 -ErrorAction SilentlyContinue;if($process.ExitCode -ne 0){throw "Journal posting verifier failed with exit code $($process.ExitCode)."};$evidence.JournalExitCode=$process.ExitCode;$evidence.JournalPostingPassed=$true;$evidence.NormalizedFinancialExcludesOwnedSources=@($token,($token+'-A'))
    $ledgerOutput=Get-Content -LiteralPath $importLog -Raw -Encoding UTF8
    foreach($proof in @('PASS owned process kill: actual application child terminated after SQL COMMIT and before recovery settlement; no graceful completion was observed.','PASS owned kill/restart recovery: normal independent app login, passive encrypted restore and actual SaveEntryAsync returned the original id, cleared the pending record, and left exactly one SQL journal.')){
        if(!$ledgerOutput.Contains($proof)){throw ('Owned hard-kill recovery evidence missing: '+$proof)}
    }
    $evidence.JournalCrashRecoveryPassed=$true
    foreach($proof in @('PASS owned ledger captured root SQL parity: 49','PASS owned ledger captured root SQL parity: 75','PASS owned ledger cancellation:','PASS owned ledger stale SQL response denied:','PASS owned ledger fresh recovery and signed-out factory denial;','PASS owned ledger positive cost center:')){
        if(!$ledgerOutput.Contains($proof)){throw ('Owned ledger evidence missing: '+$proof)}
    }
    $waits=[regex]::Matches($ledgerOutput,'Owned ledger SQL wait: session=([0-9]+); blocker=([0-9]+); wait=(LCK[^;]+); ownedBlocker=([0-9]+); observed_ms=([0-9]+)')
    if($waits.Count -ne 2){throw 'Expected exactly two observed owned ledger SQL waits.'}
    foreach($proof in @('PASS owned ledger concurrent snapshot:','PASS owned ledger snapshot restoration:')){if(!$ledgerOutput.Contains($proof)){throw ('Owned snapshot proof missing: '+$proof)}}
    $phase=[regex]::Match($ledgerOutput,'Owned ledger snapshot phase: reader=([0-9]+); rowsBlocker=([0-9]+); phase=rows-select; wait=(LCK[^;]+); observed_ms=([0-9]+)')
    $writerWait=[regex]::Match($ledgerOutput,'Owned ledger snapshot writer wait: writer=([0-9]+); reader=([0-9]+); wait=(LCK[^;]+); observed_ms=([0-9]+)')
    # Budget (B23.2): the read may hold a range lock, but a writer must never wait longer than 250 ms.
    if ($writerWait.Success) {
        $writerMs = [int]$writerWait.Groups[4].Value
        $evidence.LedgerWriterWaitMs = $writerMs
        if ($writerMs -gt 250) { throw ("Ledger snapshot writer waited " + $writerMs + " ms; the budget is 250 ms.") }
    }
    $snapshot=[regex]::Match($ledgerOutput,'PASS owned ledger concurrent snapshot: reader=([0-9]+); writer=([0-9]+); rowsBlocker=([0-9]+); fixture_elapsed_ms=([0-9]+)')
    if(!$phase.Success -or !$writerWait.Success -or !$snapshot.Success -or
       $phase.Groups[1].Value -ne $writerWait.Groups[2].Value -or $phase.Groups[1].Value -ne $snapshot.Groups[1].Value -or
       $phase.Groups[2].Value -ne $snapshot.Groups[3].Value -or $writerWait.Groups[1].Value -ne $snapshot.Groups[2].Value){throw 'Snapshot reader/writer/rows-blocker identities were not linked.'}
    $evidence.LedgerConcurrentSnapshotPassed=$true;$evidence.LedgerSnapshotRestorationPassed=$true
    $evidence.LedgerSnapshotTrace=[ordered]@{Reader=[int]$snapshot.Groups[1].Value;Writer=[int]$snapshot.Groups[2].Value;RowsBlocker=[int]$snapshot.Groups[3].Value;ReaderWait=$phase.Groups[3].Value;WriterWait=$writerWait.Groups[3].Value;FixtureElapsedMs=[int]$snapshot.Groups[4].Value;IsProductPerformanceBenchmark=$false}
    $evidence.LedgerRootParityPassed=$true;$evidence.LedgerPositiveCenterPassed=$true;$evidence.LedgerCancellationPassed=$true;$evidence.LedgerStaleReauthenticationPassed=$true
    $evidence.LedgerSqlWaits=@($waits|ForEach-Object{[ordered]@{Session=[int]$_.Groups[1].Value;ParentBlocker=[int]$_.Groups[2].Value;WaitType=$_.Groups[3].Value;OwnedBlocker=[int]$_.Groups[4].Value;ObservedMs=[int]$_.Groups[5].Value}})
    $performancePath=Join-Path $run 'ledger-read-performance.xml'
    [xml]$performance=Get-Content -LiteralPath $performancePath -Raw -Encoding UTF8
    $measurement=$performance.OwnedLedgerReadPerformance
    $accounts=@($measurement.Account)
    if($measurement.token -ne $token -or $measurement.rounds -ne '10' -or $measurement.warmupPairs -ne '2' -or
       $measurement.rawParity -ne 'true' -or $accounts.Count -ne 3){throw 'Owned ledger performance identity, parity or sample count invalid.'}
    function Milliseconds([string]$value){
        $number=[double]::Parse($value,[Globalization.CultureInfo]::InvariantCulture)
        if([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -le 0){throw 'Invalid ledger timing sample.'}
        return $number
    }
    $allWithin=$true
    foreach($account in $accounts){
        $pairs=@($account.Samples.Pair)
        if($pairs.Count -ne 10 -or [int]$account.rows -le 0){throw 'Ledger timing account has insufficient rows/samples.'}
        $legacy=@();$current=@()
        for($round=0;$round -lt 10;$round++){
            $pair=$pairs[$round];$expectedFirst=if($round%2 -eq 0){'Legacy'}else{'Current'}
            if([int]$pair.round -ne $round+1 -or $pair.first -ne $expectedFirst){throw 'Ledger paired timing order changed.'}
            $legacy+=Milliseconds $pair.legacyMs;$current+=Milliseconds $pair.currentMs
        }
        $legacy=@($legacy|Sort-Object);$current=@($current|Sort-Object)
        $oldMedian=($legacy[4]+$legacy[5])/2;$newMedian=($current[4]+$current[5])/2
        $within=($newMedian -le $oldMedian*1.15 -and $current[9] -le $legacy[9]*1.15)
        if([Math]::Abs((Milliseconds $account.legacyMedianMs)-$oldMedian) -gt 0.000001 -or
           [Math]::Abs((Milliseconds $account.currentMedianMs)-$newMedian) -gt 0.000001 -or
           [Math]::Abs((Milliseconds $account.legacyP95Ms)-$legacy[9]) -gt 0.000001 -or
           [Math]::Abs((Milliseconds $account.currentP95Ms)-$current[9]) -gt 0.000001 -or
           [bool]::Parse($account.within15Percent) -ne $within){throw 'Ledger timing summary does not match its raw pairs.'}
        $allWithin=$allWithin -and $within
    }
    if([bool]::Parse($measurement.within15Percent) -ne $allWithin){throw 'Ledger performance aggregate does not match accounts.'}
    $plansPath=Join-Path $run 'ledger-read-plans.xml'
    [xml]$plans=Get-Content -LiteralPath $plansPath -Raw -Encoding UTF8
    $ownedPlans=$plans.OwnedLedgerCachedPlans
    if($ownedPlans.token -ne $token -or $ownedPlans.orderingKeysUnique -ne 'true' -or
       $ownedPlans.cacheSeedAccountHasNoLines -ne 'true' -or $ownedPlans.productionRowsPlanCaptured -ne 'true' -or
       $ownedPlans.coldEmptyAccountPlanVerified -ne 'true'){throw 'Owned ledger unique keys and actual cold production plan proof missing.'}
    $evidence.LedgerUniqueOrderingKeysPassed=$true
    $evidence.LedgerColdEmptyAccountPlanPassed=$true
    $evidence.LedgerCachedPlansReport=$plansPath
    $evidence.LedgerReadPerformanceMeasured=$true
    $evidence.LedgerReadPerformanceWithin15Percent=$allWithin
    $evidence.LedgerReadPerformanceReport=$performancePath
    $evidence.LedgerReadPerformanceScope=[string]$measurement.scope
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
    $editorManifestPath=Join-Path $run 'owned-editor-journals.xml'
    [xml]$editorManifest=Get-Content -LiteralPath $editorManifestPath -Raw -Encoding UTF8
    $editorDocs=@($editorManifest.OwnedEditorJournals.Document)
    if($editorManifest.OwnedEditorJournals.token -ne $token -or $editorDocs.Count -ne 4 -or
       @($editorDocs.kind|Sort-Object -Unique).Count -ne 2 -or @($editorDocs.source|Sort-Object -Unique).Count -ne 4 -or
       @($editorDocs.id|Sort-Object -Unique).Count -ne 4 -or
       @($editorDocs|Where-Object {$_.kind -eq 'OpeningEntry' -and !$_.editor}).Count -ne 1 -or
       @($editorDocs|Where-Object {$_.kind -eq 'Manual' -and !$_.editor}).Count -ne 1 -or
       @($editorDocs|Where-Object {$_.kind -eq 'Manual' -and $_.editor -eq 'Quick'}).Count -ne 1 -or
       @($editorDocs|Where-Object {$_.kind -eq 'Manual' -and $_.editor -eq 'QuickCorrection'}).Count -ne 1){throw 'Exact full manual/opening, quick manual and corrected quick owned editor documents not recorded'}
    $evidence.OwnedEditorSources=@($editorDocs|ForEach-Object{@{Editor=[string]$_.editor;Kind=[string]$_.kind;Id=[int]$_.id;Source=[string]$_.source;User=[string]$_.user}})
    $cloneAfter=Snapshot $clone $token $editorDocs;$evidence.CloneAfter=$cloneAfter;if($cloneAfter.Boxes -ne $cloneBefore.Boxes -or $cloneAfter.Assignments -ne $cloneBefore.Assignments -or $cloneAfter.Employees -ne $cloneBefore.Employees+1 -or $cloneAfter.Permissions -ne $cloneBefore.Permissions -or $cloneAfter.Audit -ne $cloneBefore.Audit+13 -or $cloneAfter.JournalHeaders -ne $cloneBefore.JournalHeaders+6 -or $cloneAfter.JournalLines -ne $cloneBefore.JournalLines+12 -or $cloneAfter.FinancialSha256 -ne $cloneBefore.FinancialSha256){throw 'Journal postcondition failed: expected six headers/twelve lines, one employee, thirteen audits and unchanged normalized preexisting financial/catalog snapshot.'};$evidence.CloneAfter=$cloneAfter;$evidence.RestorePassed=$true
} finally {
    try{
        if($created -and $owned -and $master.State -eq [Data.ConnectionState]::Open){$master.ChangeDatabase('master');$actualId=[int](Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone});$actualOwner=[string](Scalar ('SELECT CONVERT(nvarchar(128),value) FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{});if($actualId -ne $targetId -or $actualOwner -ne $token){throw 'Temporary QA database ownership changed; refusing cleanup.'};Execute ('ALTER DATABASE ['+$clone+'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ['+$clone+'];') @{};$evidence.CleanupPassed=((Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone}) -is [DBNull]);if(-not $evidence.CleanupPassed){throw 'Temporary QA restore still exists after cleanup.'}}
    }catch{$evidence.CleanupError=$_.Exception.Message;throw}
    finally{
        if($master.State -eq [Data.ConnectionState]::Open){try{$sourceAfter=Snapshot 'PharmacyManagement_qa';$evidence.SourceAfter=$sourceAfter;$evidence.SourceUnchanged=($sourceAfter.StateSha256 -eq $sourceBefore.StateSha256)}catch{$evidence.SourceVerificationError=$_.Exception.Message}}
        $master.Dispose();$evidence|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8;Write-Output ('Evidence: '+$run)
    }
}
if(-not $evidence.SourceUnchanged){throw 'Source PharmacyManagement_qa changed during permission test.'}
if(-not $evidence.CleanupPassed){throw 'Owned temporary QA clone was not cleaned up.'}
Write-Output 'PASS: real SQL journal sync/async commit, retry, rollback and session guards verified in owned restored QA; six journals/twelve lines and thirteen audits, preexisting normalized financial/catalog snapshot and source unchanged, DBCC and cleanup passed.'

Write-Output ('OBSERVED ledger read performance: measured='+$evidence.LedgerReadPerformanceMeasured+'; within15Percent='+$evidence.LedgerReadPerformanceWithin15Percent+'; SQL read/projection only, independent of financial QA success.')
if($RequireLedgerPerformanceAcceptance -and -not $evidence.LedgerReadPerformanceWithin15Percent){throw 'Ledger read performance acceptance failed (>15%); financial QA, source verification and owned cleanup completed independently.'}
