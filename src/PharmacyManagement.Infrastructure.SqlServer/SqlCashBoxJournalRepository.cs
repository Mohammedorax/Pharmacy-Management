using System;
using System.Data;
using System.Data.SqlClient;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    // The invoice owns the transaction. A missing link means unassigned cash,
    // never an inferred or fabricated cashbox balance.
    public static class SqlCashBoxJournalRepository
    {
        public static void LinkSaleCancellationCash(SqlConnection connection, SqlTransaction transaction,
            string originalInvoiceSourceId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (string.IsNullOrWhiteSpace(originalInvoiceSourceId) || originalInvoiceSourceId.Length > 50)
                throw new InvalidOperationException("مرجع البيع الأصلي غير صالح لربط إلغاء النقد.");
            using (var schema = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.CashBoxJournalLinksTb',N'U') IS NULL THEN 0 ELSE 1 END;",
                connection, transaction))
                if (Convert.ToInt32(schema.ExecuteScalar()) == 0) return;

            int originalLine;
            Guid box;
            string cashier;
            decimal debit, credit;
            using (var original = new SqlCommand(@"SELECT l.JournalEntryLineId,k.CashBoxId,k.CashierEmployeeId,l.Debit,l.Credit
FROM dbo.JournalEntriesTb e WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.JournalEntryLinesTb l WITH(UPDLOCK,HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
LEFT JOIN dbo.CashBoxJournalLinksTb k WITH(UPDLOCK,HOLDLOCK) ON k.JournalEntryLineId=l.JournalEntryLineId
WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Source AND l.AccountCode=N'1000';", connection, transaction))
            {
                original.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = originalInvoiceSourceId;
                using (SqlDataReader reader = original.ExecuteReader())
                {
                    if (!reader.Read()) return;
                    originalLine = reader.GetInt32(0);
                    bool unassigned = reader.IsDBNull(1);
                    box = unassigned ? Guid.Empty : reader.GetGuid(1);
                    cashier = unassigned ? null : reader.GetString(2);
                    debit = reader.GetDecimal(3); credit = reader.GetDecimal(4);
                    if (reader.Read()) throw new InvalidOperationException("قيد البيع يحوي أكثر من سطر نقد؛ أُوقف إسناد الإلغاء للمراجعة.");
                    if (unassigned) return;
                }
            }
            using (var schema = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.CashBoxJournalLinksTb',N'ReversesJournalEntryLineId') IS NULL THEN 0 ELSE 1 END;",
                connection, transaction))
                if (Convert.ToInt32(schema.ExecuteScalar()) == 0)
                    throw new InvalidOperationException("يتطلب إلغاء بيع منسوب لصندوق ترحيل 0022؛ لم تُعتمد عملية الإلغاء.");

            int reversalLine;
            using (var reversal = new SqlCommand(@"SELECT l.JournalEntryLineId,l.Debit,l.Credit
FROM dbo.JournalEntriesTb e WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.JournalEntryLinesTb l WITH(UPDLOCK,HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceType=N'SalesInvoiceCancellation' AND e.SourceId=@Source AND l.AccountCode=N'1000';", connection, transaction))
            {
                reversal.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = originalInvoiceSourceId;
                using (SqlDataReader reader = reversal.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("لا يوجد سطر إلغاء نقد مطابق لقيد البيع الأصلي.");
                    reversalLine = reader.GetInt32(0);
                    if (reader.GetDecimal(1) != credit || reader.GetDecimal(2) != debit || reader.Read())
                        throw new InvalidOperationException("سطر إلغاء النقد لا يعكس قيمة البيع الأصلية؛ لم يُعتمد الإلغاء.");
                }
            }
            using (var insert = new SqlCommand(@"IF EXISTS
    (SELECT 1 FROM dbo.CashBoxJournalLinksTb WITH(UPDLOCK,HOLDLOCK) WHERE JournalEntryLineId=@Line)
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo.CashBoxJournalLinksTb WHERE JournalEntryLineId=@Line AND CashBoxId=@Box
        AND CashierEmployeeId=@Cashier AND ReversesJournalEntryLineId=@Original)
        THROW 51014,N'إلغاء النقد منسوب إلى صندوق أو قيد مختلف.',1;
END
ELSE
    INSERT dbo.CashBoxJournalLinksTb(JournalEntryLineId,CashBoxId,CashierEmployeeId,ReversesJournalEntryLineId)
    VALUES(@Line,@Box,@Cashier,@Original);", connection, transaction))
            {
                insert.Parameters.Add("@Line", SqlDbType.Int).Value = reversalLine;
                insert.Parameters.Add("@Box", SqlDbType.UniqueIdentifier).Value = box;
                insert.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                insert.Parameters.Add("@Original", SqlDbType.Int).Value = originalLine;
                insert.ExecuteNonQuery();
            }
        }

        public static void LinkSaleCash(SqlConnection connection, SqlTransaction transaction,
            string invoiceSourceId, string cashierEmployeeId, string cashierName, decimal expectedCash)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (expectedCash < 0m) throw new InvalidOperationException("تحصيل البيع لا يمكن أن يكون سالبًا.");
            string cashier = (cashierEmployeeId ?? string.Empty).Trim();
            if (cashier.Length == 0 || expectedCash == 0m) return;
            if (cashier.Length > 50 || string.IsNullOrWhiteSpace(invoiceSourceId))
                throw new InvalidOperationException("هوية الكاشير أو مرجع البيع غير صالح لربط الصندوق.");

            using (var schema = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.CashBoxJournalLinksTb',N'U') IS NULL THEN 0 ELSE 1 END;",
                connection, transaction))
                if (Convert.ToInt32(schema.ExecuteScalar()) == 0) return;

            Guid boxId;
            using (var assignment = new SqlCommand(@"SELECT b.CashBoxId,e.EmpName
FROM dbo.CashBoxAssignmentsTb a WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.CashBoxesTb b WITH(UPDLOCK,HOLDLOCK) ON b.CashBoxId=a.CashBoxId
JOIN dbo.EmployeeTb1 e WITH(UPDLOCK,HOLDLOCK) ON e.EmpId=a.EmpId
WHERE a.EmpId=@Cashier AND b.IsActive=1;", connection, transaction))
            {
                assignment.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                using (SqlDataReader reader = assignment.ExecuteReader())
                {
                    if (!reader.Read()) return;
                    boxId = reader.GetGuid(0);
                    if (!string.Equals(reader.GetString(1), (cashierName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("اسم الكاشير لا يطابق رقمه عند ربط الصندوق؛ حدّث اختيار الكاشير.");
                }
            }

            int lineId = 0;
            decimal cashDebit = 0m;
            using (var cashLine = new SqlCommand(@"SELECT l.JournalEntryLineId,l.Debit,l.Credit
FROM dbo.JournalEntriesTb e WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.JournalEntryLinesTb l WITH(UPDLOCK,HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Source AND l.AccountCode=N'1000';", connection, transaction))
            {
                cashLine.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = invoiceSourceId;
                using (SqlDataReader reader = cashLine.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("قيد البيع لا يحوي سطر التحصيل المتوقع للصندوق.");
                    lineId = reader.GetInt32(0);
                    cashDebit = reader.GetDecimal(1);
                    decimal credit = reader.GetDecimal(2);
                    if (reader.Read() || credit != 0m || cashDebit != expectedCash)
                        throw new InvalidOperationException("سطر نقد البيع لا يطابق التحصيل المعتمد؛ أُلغي ربط الصندوق والفاتورة.");
                }
            }

            using (var insert = new SqlCommand(@"IF EXISTS
    (SELECT 1 FROM dbo.CashBoxJournalLinksTb WITH(UPDLOCK,HOLDLOCK) WHERE JournalEntryLineId=@LineId)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.CashBoxJournalLinksTb WHERE JournalEntryLineId=@LineId AND CashBoxId=@BoxId AND CashierEmployeeId=@Cashier)
        THROW 51011,N'سطر النقد مربوط بصندوق آخر؛ أُوقف تكرار الاعتماد.',1;
END
ELSE
    INSERT dbo.CashBoxJournalLinksTb(JournalEntryLineId,CashBoxId,CashierEmployeeId)
    VALUES(@LineId,@BoxId,@Cashier);", connection, transaction))
            {
                insert.Parameters.Add("@LineId", SqlDbType.Int).Value = lineId;
                insert.Parameters.Add("@BoxId", SqlDbType.UniqueIdentifier).Value = boxId;
                insert.Parameters.Add("@Cashier", SqlDbType.NVarChar, 50).Value = cashier;
                insert.ExecuteNonQuery();
            }
        }
    }
}
