using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class CashBoxTests
    {
        private sealed class FakeRepository : ICashBoxRepository
        {
            internal int Writes;
            internal IReadOnlyList<CashBoxRecord> Saved;
            public IReadOnlyList<CashBoxRecord> GetAll() => Saved ?? new CashBoxRecord[0];
            public void SaveBatch(IReadOnlyList<CashBoxRecord> boxes, OperationActor actor)
            {
                Writes++;
                Saved = boxes;
            }
        }

        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var service = new CashBoxService(repository, actor => true);
            var manager = new OperationActor { UserName = "مدير", UserRole = "Manager" };
            var cashier = new OperationActor { UserName = "كاشير", UserRole = "Cashier" };
            var input = new CashBoxInput { Code = " main-1 ", Name = " الصندوق الرئيسي ", IsActive = true, EmployeeId = " E001 " };

            Reject<UnauthorizedAccessException>(() => service.GetAll(cashier)); checks++;
            Reject<UnauthorizedAccessException>(() => service.SaveBatch(new[] { input }, cashier)); checks++;
            Reject<UnauthorizedAccessException>(() =>
                new CashBoxService(repository, actor => false).SaveBatch(new[] { input }, manager)); checks++;
            if (repository.Writes != 0) throw new InvalidOperationException("Unauthorized cashbox write.");
            checks++;

            IReadOnlyList<Guid> ids = service.SaveBatch(new[] { input }, manager);
            if (ids.Count != 1 || ids[0] == Guid.Empty || repository.Writes != 1 ||
                repository.Saved[0].Code != "MAIN-1" || repository.Saved[0].Name != "الصندوق الرئيسي" ||
                repository.Saved[0].EmployeeId != "E001")
                throw new InvalidOperationException("Cashbox normalization or identity failed.");
            checks++;
            if (service.GetAll(manager).Count != 1) throw new InvalidOperationException("Cashbox read failed.");
            checks++;

            Reject<InvalidOperationException>(() => service.SaveBatch(new[]
            {
                input, new CashBoxInput { Code = "MAIN-1", Name = "آخر", IsActive = true }
            }, manager)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new[]
            {
                input, new CashBoxInput { Code = "SECOND", Name = "ثان", IsActive = true, EmployeeId = "e001" }
            }, manager)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new[]
            {
                new CashBoxInput { Code = "A B", Name = "ثالث", IsActive = true }
            }, manager)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new[]
            {
                new CashBoxInput { Code = "OFF", Name = "معطل", IsActive = false, EmployeeId = "E002" }
            }, manager)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new CashBoxInput[0], manager)); checks++;
            if (repository.Writes != 1) throw new InvalidOperationException("Invalid cashbox batch reached repository.");
            checks++;
            return checks;
        }

        internal static void VerifyQaRollback(string serverName, string databaseName)
        {
            Func<SqlConnection> factory = QaFactory(serverName, databaseName);
            using (SqlConnection connection = factory())
            {
                connection.Open();
                using (SqlCommand identity = new SqlCommand("SELECT DB_NAME()", connection))
                    if (!string.Equals(Convert.ToString(identity.ExecuteScalar()), databaseName,
                        StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Cashbox QA database identity changed.");
            }

            var service = new CashBoxService(new SqlCashBoxRepository(factory), actor => true);
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            string code = "QA-" + nonce;
            var manager = new OperationActor { UserName = "cashbox-qa", UserRole = "Manager" };
            bool rejected = false;
            try
            {
                service.SaveBatch(new[]
                {
                    new CashBoxInput { Code = code, Name = "اختبار رجوع المعاملة", IsActive = true },
                    new CashBoxInput { Code = code + "-2", Name = "صندوق ثاني", IsActive = true,
                        EmployeeId = "MISSING-" + nonce }
                }, manager);
            }
            catch (SqlException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Missing cashier did not reject the cashbox batch.");
            using (SqlConnection connection = factory())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(@"SELECT
    (SELECT COUNT(*) FROM dbo.CashBoxesTb WHERE BoxCode LIKE @Code + N'%') +
    (SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'CashBoxSaved' AND Details LIKE N'%' + @Code + N'%');", connection))
                {
                    command.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = code;
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("Failed cashbox batch left persistent definitions or audit records.");
                }
            }
            Console.WriteLine("PASS: QA cashbox batch rolled back definitions, assignments and audit.");
        }

        internal static void VerifyQaRoundtrip(string serverName, string databaseName)
        {
            Func<SqlConnection> factory = QaFactory(serverName, databaseName);
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            string employeeId = "QA-CASH-" + nonce;
            string code = "QA-BOX-" + nonce;
            Guid cashBoxId = Guid.NewGuid();
            bool fixtureInserted = false;
            var manager = new OperationActor { UserName = "cashbox-qa", UserRole = "Manager" };
            try
            {
                using (SqlConnection connection = factory())
                {
                    connection.Open();
                    using (SqlCommand identity = new SqlCommand("SELECT DB_NAME()", connection))
                        if (!string.Equals(Convert.ToString(identity.ExecuteScalar()), databaseName,
                            StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Cashbox QA database identity changed.");
                    using (SqlCommand fixture = new SqlCommand(@"INSERT dbo.EmployeeTb1
(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Id,@Name,0,25,N'000',N'QA-ONLY-NO-LOGIN',N'Cashier');", connection))
                    {
                        fixture.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                        fixture.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = "Cashbox QA " + nonce;
                        fixture.ExecuteNonQuery();
                        fixtureInserted = true;
                    }
                }
                var service = new CashBoxService(new SqlCashBoxRepository(factory), actor => true);
                cashBoxId = service.SaveBatch(new[]
                {
                    new CashBoxInput { CashBoxId = cashBoxId, Code = code, Name = "صندوق اختبار",
                        IsActive = true, EmployeeId = employeeId }
                }, manager)[0];
                CashBoxRecord created = Find(service.GetAll(manager), cashBoxId);
                if (created == null || created.EmployeeId != employeeId || created.Code != code ||
                    !created.IsActive || created.EmployeeName != "Cashbox QA " + nonce)
                    throw new InvalidOperationException("Cashbox assignment was not loaded by stable EmpId.");
                service.SaveBatch(new[]
                {
                    new CashBoxInput { CashBoxId = cashBoxId, Code = code, Name = "صندوق بعد التعديل",
                        IsActive = false, EmployeeId = string.Empty }
                }, manager);
                CashBoxRecord updated = Find(service.GetAll(manager), cashBoxId);
                if (updated == null || updated.Name != "صندوق بعد التعديل" ||
                    updated.IsActive || !string.IsNullOrEmpty(updated.EmployeeId))
                    throw new InvalidOperationException("Cashbox update or unassignment failed.");
                Console.WriteLine("PASS: QA cashbox definition, stable cashier assignment and unassignment.");
            }
            finally
            {
                if (fixtureInserted)
                {
                using (SqlConnection connection = factory())
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (SqlCommand clean = new SqlCommand(@"DELETE dbo.CashBoxAssignmentsTb WHERE EmpId=@EmpId;
DELETE dbo.CashBoxesTb WHERE CashBoxId=@BoxId;
DELETE dbo.AuditLogTb WHERE UserName=N'cashbox-qa' AND ActionName=N'CashBoxSaved'
    AND Details LIKE N'%' + @Code + N'%';
DELETE dbo.EmployeeTb1 WHERE EmpId=@EmpId;", connection, transaction))
                            {
                                clean.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId;
                                clean.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = code;
                                clean.Parameters.Add("@BoxId", SqlDbType.UniqueIdentifier).Value = cashBoxId;
                                clean.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                        catch { transaction.Rollback(); throw; }
                    }
                }
                }
            }
        }

        internal static void VerifyQaJournalLinkRollback(string serverName, string databaseName)
        {
            Func<SqlConnection> factory = QaFactory(serverName, databaseName);
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            string cashier = "QA-LINK-" + nonce;
            string source = "QA-LINK-" + nonce;
            string code = "QA-LINK-" + nonce;
            Guid box = Guid.NewGuid();
            using (SqlConnection connection = factory())
            {
                connection.Open();
                using (SqlCommand identity = new SqlCommand("SELECT DB_NAME(),COL_LENGTH(N'dbo.CashBoxJournalLinksTb',N'ReversesJournalEntryLineId')", connection))
                using (SqlDataReader reader = identity.ExecuteReader())
                {
                    if (!reader.Read() || !string.Equals(reader.GetString(0), databaseName, StringComparison.OrdinalIgnoreCase) ||
                        reader.IsDBNull(1))
                        throw new InvalidOperationException("Cashbox journal test requires migration 0022 on the named QA database.");
                }
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand fixture = new SqlCommand(@"INSERT dbo.EmployeeTb1
(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Cashier,@Name,0,25,N'000',N'QA-ONLY-NO-LOGIN',N'Cashier');
INSERT dbo.CashBoxesTb(CashBoxId,BoxCode,BoxName,IsActive) VALUES(@Box,@Code,N'صندوق QA',1);
INSERT dbo.CashBoxAssignmentsTb(EmpId,CashBoxId,AssignedBy) VALUES(@Cashier,@Box,N'cashbox-qa');", connection, transaction))
                        {
                            fixture.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                            fixture.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = "Cashier QA " + nonce;
                            fixture.Parameters.Add("@Box", SqlDbType.UniqueIdentifier).Value = box;
                            fixture.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = code;
                            fixture.ExecuteNonQuery();
                        }
                        int cashLine;
                        using (SqlCommand invoice = new SqlCommand(@"DECLARE @JournalId INT;
INSERT dbo.JournalEntriesTb(EntryDate,SourceType,SourceId,Description,CreatedBy)
VALUES(GETDATE(),N'SalesInvoice',@Source,N'QA cashbox link',N'cashbox-qa');
SET @JournalId=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@JournalId,N'1000',12.34,0,N'QA cash debit');
SELECT CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@JournalId,N'4000',0,12.34,N'QA sale credit');", connection, transaction))
                        {
                            invoice.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = source;
                            cashLine = Convert.ToInt32(invoice.ExecuteScalar());
                        }
                        string cashierName = "Cashier QA " + nonce;
                        SqlCashBoxJournalRepository.LinkSaleCash(connection, transaction, source, cashier, cashierName, 12.34m);
                        SqlCashBoxJournalRepository.LinkSaleCash(connection, transaction, source, cashier, cashierName, 12.34m);
                        using (SqlCommand verify = new SqlCommand(@"SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb
WHERE JournalEntryLineId=@Line AND CashBoxId=@Box AND CashierEmployeeId=@Cashier;", connection, transaction))
                        {
                            verify.Parameters.Add("@Line", SqlDbType.Int).Value = cashLine;
                            verify.Parameters.Add("@Box", SqlDbType.UniqueIdentifier).Value = box;
                            verify.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                            if (Convert.ToInt32(verify.ExecuteScalar()) != 1)
                                throw new InvalidOperationException("Cashbox journal link was missing or duplicated.");
                        }
                        bool mismatchRejected = false;
                        try { SqlCashBoxJournalRepository.LinkSaleCash(connection, transaction, source, cashier, cashierName, 12.35m); }
                        catch (InvalidOperationException) { mismatchRejected = true; }
                        if (!mismatchRejected) throw new InvalidOperationException("Mismatched cash amount was attributed to a box.");
                        bool identityRejected = false;
                        try { SqlCashBoxJournalRepository.LinkSaleCash(connection, transaction, source, cashier, "Another cashier", 12.34m); }
                        catch (InvalidOperationException) { identityRejected = true; }
                        if (!identityRejected) throw new InvalidOperationException("Mismatched cashier name was attributed to a box.");
                        // A later assignment must never rewrite a posted sale's cashbox.
                        Guid laterBox = Guid.NewGuid();
                        using (SqlCommand reassignment = new SqlCommand(@"DELETE dbo.CashBoxAssignmentsTb WHERE EmpId=@Cashier;
UPDATE dbo.CashBoxesTb SET IsActive=0 WHERE CashBoxId=@OriginalBox;
INSERT dbo.CashBoxesTb(CashBoxId,BoxCode,BoxName,IsActive) VALUES(@LaterBox,@Code,N'صندوق لاحق',1);
INSERT dbo.CashBoxAssignmentsTb(EmpId,CashBoxId,AssignedBy) VALUES(@Cashier,@LaterBox,N'cashbox-qa');", connection, transaction))
                        {
                            reassignment.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                            reassignment.Parameters.Add("@OriginalBox", SqlDbType.UniqueIdentifier).Value = box;
                            reassignment.Parameters.Add("@LaterBox", SqlDbType.UniqueIdentifier).Value = laterBox;
                            reassignment.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = code + "-NEW";
                            reassignment.ExecuteNonQuery();
                        }
                        int cancellationLine;
                        using (SqlCommand cancellation = new SqlCommand(@"DECLARE @JournalId INT;
INSERT dbo.JournalEntriesTb(EntryDate,SourceType,SourceId,Description,CreatedBy)
VALUES(GETDATE(),N'SalesInvoiceCancellation',@Source,N'QA cashbox cancellation',N'cashbox-qa');
SET @JournalId=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@JournalId,N'1000',0,12.35,N'QA cash reversal');
SELECT CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@JournalId,N'4000',12.35,0,N'QA sale reversal');", connection, transaction))
                        {
                            cancellation.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = source;
                            cancellationLine = Convert.ToInt32(cancellation.ExecuteScalar());
                        }
                        bool reversalMismatchRejected = false;
                        try { SqlCashBoxJournalRepository.LinkSaleCancellationCash(connection, transaction, source); }
                        catch (InvalidOperationException) { reversalMismatchRejected = true; }
                        if (!reversalMismatchRejected) throw new InvalidOperationException("Incorrect cancellation amount was assigned to a cashbox.");
                        using (SqlCommand correct = new SqlCommand(@"UPDATE l SET Debit=CASE WHEN l.AccountCode=N'4000' THEN 12.34 ELSE 0 END,
Credit=CASE WHEN l.AccountCode=N'1000' THEN 12.34 ELSE 0 END
FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE e.SourceType=N'SalesInvoiceCancellation' AND e.SourceId=@Source;", connection, transaction))
                        {
                            correct.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = source;
                            correct.ExecuteNonQuery();
                        }
                        SqlCashBoxJournalRepository.LinkSaleCancellationCash(connection, transaction, source);
                        SqlCashBoxJournalRepository.LinkSaleCancellationCash(connection, transaction, source);
                        using (SqlCommand verify = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb WHERE JournalEntryLineId=@Cancellation AND CashBoxId=@Box
 AND CashierEmployeeId=@Cashier AND ReversesJournalEntryLineId=@Original),
(SELECT SUM(l.Debit-l.Credit) FROM dbo.CashBoxJournalLinksTb k JOIN dbo.JournalEntryLinesTb l
 ON l.JournalEntryLineId=k.JournalEntryLineId WHERE k.CashBoxId=@Box),
(SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb WHERE CashBoxId=@LaterBox);", connection, transaction))
                        {
                            verify.Parameters.Add("@Cancellation", SqlDbType.Int).Value = cancellationLine;
                            verify.Parameters.Add("@Original", SqlDbType.Int).Value = cashLine;
                            verify.Parameters.Add("@Box", SqlDbType.UniqueIdentifier).Value = box;
                            verify.Parameters.Add("@LaterBox", SqlDbType.UniqueIdentifier).Value = laterBox;
                            verify.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                            using (SqlDataReader reader = verify.ExecuteReader())
                                if (!reader.Read() || reader.GetInt32(0) != 1 || reader.GetDecimal(1) != 0m || reader.GetInt32(2) != 0)
                                    throw new InvalidOperationException("Cancellation did not exactly clear the original cashbox, or used the later assignment.");
                        }


                    }
                    finally { transaction.Rollback(); }
                }
                using (SqlCommand remaining = new SqlCommand(@"SELECT
 (SELECT COUNT(*) FROM dbo.EmployeeTb1 WHERE EmpId=@Cashier) +
 (SELECT COUNT(*) FROM dbo.CashBoxesTb WHERE BoxCode LIKE @Source + N'%') +
 (SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType IN(N'SalesInvoice',N'SalesInvoiceCancellation') AND SourceId=@Source);", connection))
                {
                    remaining.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                    remaining.Parameters.Add("@Box", SqlDbType.UniqueIdentifier).Value = box;
                    remaining.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = source;
                    if (Convert.ToInt32(remaining.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("QA cashbox journal test left persisted records.");
                }
            }
            Console.WriteLine("PASS: QA sale and cancellation cash linked once to original box after reassignment/deactivation; mismatches rejected; box net zero; transaction rolled back.");
        }

        internal static void VerifyQaCancellationTriggerRollback(string serverName, string databaseName)
        {
            Func<SqlConnection> factory = QaFactory(serverName, databaseName);
            foreach (string fault in new[] { "amount", "box", "source" })
            {
                string nonce = "QA-REV-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                using (SqlConnection connection = factory())
                {
                    connection.Open();
                    using (var identity = new SqlCommand("SELECT DB_NAME(),COL_LENGTH(N'dbo.CashBoxJournalLinksTb',N'ReversesJournalEntryLineId');", connection))
                    using (SqlDataReader reader = identity.ExecuteReader())
                        if (!reader.Read() || reader.GetString(0) != databaseName || reader.IsDBNull(1))
                            throw new InvalidOperationException("Cancellation integrity test requires migration 0022 on the named QA database.");
                    bool rejected = false;
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            using (var command = new SqlCommand(@"DECLARE @Box UNIQUEIDENTIFIER=NEWID(),@Other UNIQUEIDENTIFIER=NEWID();
DECLARE @Journal INT,@OriginalLine INT,@CancellationLine INT;
INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Source,N'Cashbox reverse QA',0,25,N'000',N'QA-ONLY-NO-LOGIN',N'Cashier');
INSERT dbo.CashBoxesTb(CashBoxId,BoxCode,BoxName,IsActive)
VALUES(@Box,@Source,N'Original QA box',1),(@Other,@Source+N'-OTHER',N'Other QA box',1);
INSERT dbo.CashBoxAssignmentsTb(EmpId,CashBoxId,AssignedBy) VALUES(@Source,@Box,N'cashbox-qa');
INSERT dbo.JournalEntriesTb(EntryDate,SourceType,SourceId,Description,CreatedBy)
VALUES(GETDATE(),N'SalesInvoice',@Source,N'QA original',N'cashbox-qa');
SET @Journal=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@Journal,N'1000',12.34,0,N'QA cash');
SET @OriginalLine=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@Journal,N'4000',0,12.34,N'QA sale');
INSERT dbo.CashBoxJournalLinksTb(JournalEntryLineId,CashBoxId,CashierEmployeeId)
VALUES(@OriginalLine,@Box,@Source);
INSERT dbo.JournalEntriesTb(EntryDate,SourceType,SourceId,Description,CreatedBy)
VALUES(GETDATE(),N'SalesInvoiceCancellation',CASE WHEN @Fault=N'source' THEN @Source+N'-WRONG' ELSE @Source END,N'QA reversal',N'cashbox-qa');
SET @Journal=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.JournalEntryLinesTb(JournalEntryId,AccountCode,Debit,Credit,LineDescription)
VALUES(@Journal,N'1000',0,CASE WHEN @Fault=N'amount' THEN 12.35 ELSE 12.34 END,N'QA reversal cash');
SET @CancellationLine=CONVERT(INT,SCOPE_IDENTITY());
INSERT dbo.CashBoxJournalLinksTb(JournalEntryLineId,CashBoxId,CashierEmployeeId,ReversesJournalEntryLineId)
VALUES(@CancellationLine,CASE WHEN @Fault=N'box' THEN @Other ELSE @Box END,@Source,@OriginalLine);", connection, transaction))
                            {
                                command.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = nonce;
                                command.Parameters.Add("@Fault", SqlDbType.NVarChar, 10).Value = fault;
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (SqlException error) when (error.Number == 51014) { rejected = true; }
                        finally { if (transaction.Connection != null) transaction.Rollback(); }
                    }
                    if (!rejected) throw new InvalidOperationException("SQL accepted a fabricated cashbox cancellation: " + fault);
                    using (var remaining = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM dbo.EmployeeTb1 WHERE EmpId=@Source)+
(SELECT COUNT(*) FROM dbo.CashBoxesTb WHERE BoxCode LIKE @Source+N'%')+
(SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId LIKE @Source+N'%');", connection))
                    {
                        remaining.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = nonce;
                        if (Convert.ToInt32(remaining.ExecuteScalar()) != 0)
                            throw new InvalidOperationException("Rejected cancellation left QA fixtures: " + fault);
                    }
                }
            }
            Console.WriteLine("PASS: QA SQL rejected cancellation with wrong amount, box and source; all fixtures rolled back.");
        }

        private static CashBoxRecord Find(IReadOnlyList<CashBoxRecord> boxes, Guid id)
        {
            foreach (CashBoxRecord box in boxes)
                if (box.CashBoxId == id) return box;
            return null;
        }

        private static Func<SqlConnection> QaFactory(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Cashbox SQL verification is QA-only.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = serverName, InitialCatalog = databaseName,
                IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true
            };
            return () => new SqlConnection(builder.ConnectionString);
        }

        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Invalid cashbox operation accepted: " + typeof(T).Name);
        }
    }
}
