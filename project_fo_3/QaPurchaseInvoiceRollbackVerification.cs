using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class QaPurchaseInvoiceRollbackVerification
    {
        internal static void Run(string serverName, string databaseName)
        {
            EnsureQa(databaseName);
            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);
            string medicine;
            string supplier;
            string employee;
            string currency;
            int stockBefore;
            bool commercial;
            string purchaseId = "QA-PUR-" + Guid.NewGuid().ToString("N").Substring(0, 20).ToUpperInvariant();
            string detailId = DateTime.UtcNow.Ticks.ToString();
            int auditBefore;
            int journalBefore;
            int movementsBefore;
            int purchasesBefore, detailsBefore, sourcesBefore, lotsBefore;
            object productionBefore, expiryBefore;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(@"SELECT TOP(1) m.MedName,m.CompName,m.MedQty
FROM dbo.MedicineTb1 m WHERE m.MedName IS NOT NULL ORDER BY m.MedName;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("لا يوجد دواء في QA لاختبار المشتريات.");
                    medicine = Convert.ToString(reader[0]);
                    supplier = Convert.ToString(reader[1]);
                    stockBefore = Convert.ToInt32(reader[2]);
                }
                employee = ScalarText(connection, "SELECT TOP(1) EmpName FROM dbo.EmployeeTb1 ORDER BY EmpId;");
                currency = ScalarText(connection, "SELECT TOP(1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId;");
                if (string.IsNullOrWhiteSpace(currency)) throw new InvalidOperationException("عملة النظام غير محددة في QA.");
                if (string.IsNullOrWhiteSpace(employee)) throw new InvalidOperationException("لا يوجد موظف في QA لاختبار المشتريات.");
                commercial = CommercialSchemaInstaller.IsFullCommercialMode(connection, null);
                auditBefore = Count(connection, "SELECT COUNT(1) FROM dbo.AuditLogTb;");
                journalBefore = Count(connection, "SELECT COUNT(1) FROM dbo.JournalEntriesTb;");
                movementsBefore = Count(connection, "SELECT COUNT(1) FROM dbo.InventoryMovementsTb;");
                purchasesBefore = Count(connection, "SELECT COUNT(*) FROM dbo.PurchasesTb;");
                detailsBefore = Count(connection, "SELECT COUNT(*) FROM dbo.PurchaseDetailsTb;");
                sourcesBefore = Count(connection, "SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb;");
                lotsBefore = commercial ? Count(connection, "SELECT COUNT(*) FROM dbo.InventoryLotsTb;") : 0;
                productionBefore = Scalar(connection, "SELECT ProdDate FROM dbo.MedicineTb1 WHERE MedName=@Value;", medicine);
                expiryBefore = Scalar(connection, "SELECT ExpDate FROM dbo.MedicineTb1 WHERE MedName=@Value;", medicine);
                using (SqlTransaction guardTransaction = connection.BeginTransaction())
                {
                    decimal effectiveTax = AccountingService.GetEffectiveTaxRate(connection, guardTransaction);
                    ImportedInvoiceApprovalGuard.EnsurePurchaseTax(connection, guardTransaction, effectiveTax);
                    bool rejectedMismatch = false;
                    try
                    {
                        ImportedInvoiceApprovalGuard.EnsurePurchaseTax(connection, guardTransaction,
                            effectiveTax == 0m ? 1m : 0m);
                    }
                    catch (InvalidOperationException) { rejectedMismatch = true; }
                    guardTransaction.Rollback();
                    if (!rejectedMismatch)
                        throw new InvalidOperationException("اختبار QA قبل ضريبة شراء مستورد لا تطابق السياسة الفعالة.");
                }
            }

            InvoiceImportCandidate candidate = InvoiceImportValidation.Validate(
                InvoiceImportMode.ImportAndApprove, "QA-PurchaseRollback", new[] {
                    new InvoiceImportRow {
                        SourceInvoiceId = purchaseId, LineNumber = 1, Kind = ImportedInvoiceKind.Purchase,
                        InvoiceDate = DateTime.Now, PartyName = supplier, CurrencyCode = currency,
                        MedicineName = medicine, Quantity = 1m, UnitPrice = 1m,
                        InvoiceDiscountPercent = 0m, TaxRatePercent = 0m, PriceTaxMode = "Inclusive",
                        PaymentMethod = "Payable", Notes = "QA rollback purchase verification",
                        ProductionDate = DateTime.Today.AddDays(-1), ExpiryDate = DateTime.Today.AddYears(1),
                        BatchNumber = commercial ? "QA-" + purchaseId.Substring(7, 12) : string.Empty
                    }
                })[0];
            PurchaseInvoiceRequest mapped = InvoiceImportApprovalMapping.Purchase(candidate,
                new OperationActor { UserName = employee, UserRole = "QAVerifier" }, commercial);
            mapped.DesiredPurchaseId = purchaseId;
            mapped.Lines[0].DesiredDetailId = detailId;
            var operation = new PurchaseInvoiceOperation(() => new SqlConnection(connectionString));
            PurchaseInvoiceResult result = operation.IssueForVerificationRollback(mapped,
                (connection, transaction, id) => VerifyInTransaction(connection, transaction, id, medicine, stockBefore, commercial, "Payable"));
            if (!string.Equals(result.PurchaseId, purchaseId, StringComparison.Ordinal) || result.TotalAmount != 1m)
                throw new InvalidOperationException("نتيجة عملية المشتريات المتراجعة لم تحفظ هوية الطلب أو الإجمالي المتوقع.");

            InvoiceImportRow cashRow = candidate.Lines[0];
            cashRow.PaymentMethod = "Cash";
            InvoiceImportCandidate cashCandidate = InvoiceImportValidation.Validate(
                InvoiceImportMode.ImportAndApprove, "QA-PurchaseRollback", new[] { cashRow })[0];
            PurchaseInvoiceRequest cashRequest = InvoiceImportApprovalMapping.Purchase(cashCandidate,
                new OperationActor { UserName = employee, UserRole = "QAVerifier" }, commercial);
            cashRequest.DesiredPurchaseId = purchaseId;
            cashRequest.Lines[0].DesiredDetailId = (long.Parse(detailId) + 1).ToString();
            PurchaseInvoiceResult cashResult = operation.IssueForVerificationRollback(cashRequest,
                (connection, transaction, id) => VerifyInTransaction(connection, transaction, id, medicine, stockBefore, commercial, "Cash"));
            if (cashResult.TotalAmount != 1m)
                throw new InvalidOperationException("نتيجة شراء Cash المستورد المتراجع غير صحيحة.");

            bool inducedFailureRejected = false;
            try
            {
                operation.IssueForVerificationRollback(mapped, (connection, transaction, id) =>
                {
                    VerifyInTransaction(connection, transaction, id, medicine, stockBefore, commercial, "Payable");
                    throw new InvalidOperationException("QA induced purchase failure before commit");
                });
            }
            catch (InvalidOperationException error) when (error.Message == "QA induced purchase failure before commit")
            {
                inducedFailureRejected = true;
            }
            if (!inducedFailureRejected) throw new InvalidOperationException("QA induced failure did not reject the purchase.");
            Console.WriteLine("PASS: exception after stock/journal/source writes rejected the entire purchase.");

            bool duplicateSourceRejected = false;
            try
            {
                operation.IssueForVerificationRollback(mapped, (connection, transaction, id) =>
                {
                    VerifyInTransaction(connection, transaction, id, medicine, stockBefore, commercial, "Payable");
                    ImportedInvoiceSourceRegistry.Record(connection, transaction, "Purchase",
                        "QA-PurchaseRollback", purchaseId, id);
                });
            }
            catch (InvalidOperationException error) when (error.Message.Contains("معتمدة سابقًا"))
            {
                duplicateSourceRejected = true;
            }
            if (!duplicateSourceRejected) throw new InvalidOperationException("QA duplicate import source was accepted.");
            Console.WriteLine("PASS: duplicate source identity in SQL rejected and the purchase rolled back.");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (CountByValue(connection, "SELECT COUNT(1) FROM dbo.PurchasesTb WHERE PurchaseId=@Value;", purchaseId) != 0 ||
                    CountByValue(connection, "SELECT COUNT(1) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Value;", purchaseId) != 0)
                    throw new InvalidOperationException("فشل تراجع اختبار فاتورة المشتريات؛ بقي رأس أو بند في QA.");
                if (CountByValue(connection, "SELECT COUNT(1) FROM dbo.ImportedInvoiceSourceTb WHERE SourceInvoiceId=@Value;", purchaseId) != 0)
                    throw new InvalidOperationException("فشل تراجع سجل مصدر فاتورة المشتريات المستوردة.");
                int stockAfter = Convert.ToInt32(Scalar(connection, "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Value;", medicine));
                if (stockAfter != stockBefore) throw new InvalidOperationException("فشل تراجع اختبار المشتريات؛ تغير رصيد الدواء.");
                if (!Equals(productionBefore, Scalar(connection, "SELECT ProdDate FROM dbo.MedicineTb1 WHERE MedName=@Value;", medicine)) ||
                    !Equals(expiryBefore, Scalar(connection, "SELECT ExpDate FROM dbo.MedicineTb1 WHERE MedName=@Value;", medicine)))
                    throw new InvalidOperationException("Purchase rollback changed medicine production/expiry dates.");
                if (Count(connection, "SELECT COUNT(*) FROM dbo.PurchasesTb;") != purchasesBefore ||
                    Count(connection, "SELECT COUNT(*) FROM dbo.PurchaseDetailsTb;") != detailsBefore ||
                    Count(connection, "SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb;") != sourcesBefore ||
                    (commercial && Count(connection, "SELECT COUNT(*) FROM dbo.InventoryLotsTb;") != lotsBefore))
                    throw new InvalidOperationException("Purchase rollback left a header, line, source identity or inventory lot.");
                if (Count(connection, "SELECT COUNT(1) FROM dbo.AuditLogTb;") != auditBefore ||
                    Count(connection, "SELECT COUNT(1) FROM dbo.JournalEntriesTb;") != journalBefore ||
                    Count(connection, "SELECT COUNT(1) FROM dbo.InventoryMovementsTb;") != movementsBefore)
                    throw new InvalidOperationException("فشل تراجع اختبار المشتريات؛ بقي قيد أو حركة أو سجل تدقيق.");
                Console.WriteLine("PASS: stock, medicine dates, headers, lines, sources, lots, journals, movements and audit rows restored.");
            }
        }

        private static void VerifyInTransaction(SqlConnection connection, SqlTransaction transaction,
            string purchaseId, string medicine, int originalStock, bool commercial, string paymentMethod)
        {
            VerifyPurchaseJournal(connection, transaction, purchaseId, 1m, paymentMethod);
            using (var command = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM dbo.PurchasesTb WHERE PurchaseId=@Id AND TotalAmount=1),
(SELECT COUNT(*) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id AND MedName=@Medicine AND Quantity=1 AND BuyingPrice=1 AND TotalAmount=1),
(SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Medicine),
(SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=N'QA-PurchaseRollback' AND InvoiceKind=N'Purchase' AND SourceInvoiceId=@Id AND PostedReference=@Id),
(SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id AND MedName=@Medicine AND QuantityDelta=1 AND QuantityBefore=@Before AND QuantityAfter=@After),
(SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id),
(SELECT COUNT(*) FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id),
(SELECT COALESCE(SUM(l.Debit-l.Credit),0) FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id);", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@Medicine", SqlDbType.NVarChar, 200).Value = medicine;
                command.Parameters.Add("@Before", SqlDbType.Int).Value = originalStock;
                command.Parameters.Add("@After", SqlDbType.Int).Value = checked(originalStock + 1);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (!reader.Read() || Convert.ToInt32(reader[0]) != 1 || Convert.ToInt32(reader[1]) != 1 ||
                        Convert.ToInt32(reader[2]) != originalStock + 1 || Convert.ToInt32(reader[3]) != 1 ||
                        Convert.ToInt32(reader[4]) != 1 || Convert.ToInt32(reader[5]) != 1 ||
                        Convert.ToInt32(reader[6]) < 2 || Convert.ToDecimal(reader[7]) != 0m)
                        throw new InvalidOperationException("Imported purchase did not atomically write its header, line, stock, movement, balanced journal and source identity.");
            }
            if (commercial)
                using (var command = new SqlCommand(@"SELECT COUNT(*) FROM dbo.InventoryLotsTb l
JOIN dbo.PurchaseDetailsTb d ON d.DetailId=l.PurchaseDetailId
WHERE d.PurchaseId=@Id AND l.MedName=@Medicine AND l.QuantityReceived=1 AND l.QuantityRemaining=1 AND l.UnitCost=1;", connection, transaction))
                {
                    command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId;
                    command.Parameters.Add("@Medicine", SqlDbType.NVarChar, 200).Value = medicine;
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                        throw new InvalidOperationException("Imported commercial purchase did not create exactly one matching lot.");
                }
            Console.WriteLine("PASS: " + paymentMethod + " purchase transaction: header, line, stock +1, movement, balanced journal, source and commercial lot=" + commercial);
        }

        private static void VerifyPurchaseJournal(SqlConnection connection, SqlTransaction transaction,
            string purchaseId, decimal expectedAmount, string paymentMethod)
        {
            using (var command = new SqlCommand(@"SELECT
COALESCE(SUM(CASE WHEN l.AccountCode=N'2000' THEN l.Credit ELSE 0 END),0),
COALESCE(SUM(CASE WHEN l.AccountCode=N'1000' THEN l.Credit ELSE 0 END),0),
COALESCE(SUM(CASE WHEN l.AccountCode=N'1200' THEN l.Debit ELSE 0 END),0)
FROM dbo.JournalEntriesTb e
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@SourceId;", connection, transaction))
            {
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = purchaseId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    decimal expectedSupplier = paymentMethod == "Payable" ? expectedAmount : 0m;
                    decimal expectedCash = paymentMethod == "Cash" ? expectedAmount : 0m;
                    if (!reader.Read() || Convert.ToDecimal(reader[0]) != expectedSupplier ||
                        Convert.ToDecimal(reader[1]) != expectedCash || Convert.ToDecimal(reader[2]) != expectedAmount)
                        throw new InvalidOperationException("قيد المشتريات لم يطابق طريقة السداد المستوردة والمخزون.");
                }
            }
        }

        private static int Count(SqlConnection connection, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection)) return Convert.ToInt32(command.ExecuteScalar());
        }

        private static int CountByValue(SqlConnection connection, string sql, string value)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 50).Value = value;
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static object Scalar(SqlConnection connection, string sql, string value)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                return command.ExecuteScalar();
            }
        }

        private static string ScalarText(SqlConnection connection, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection)) return Convert.ToString(command.ExecuteScalar());
        }

        private static void EnsureQa(string databaseName)
        {
            string value = databaseName ?? string.Empty;
            if (!(value.EndsWith("_qa", StringComparison.OrdinalIgnoreCase) ||
                  value.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                  value.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("اختبار المشتريات المتراجع مسموح على QA/Test/Sandbox فقط.");
        }
    }
}
