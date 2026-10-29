using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class CustomerAccountLedgerService
    {

        internal static void RecordCreditGrant(
            SqlConnection connection, SqlTransaction transaction,
            string customerId, int journalEntryId, string sourceId,
            decimal amount, DateTime entryDateUtc, string createdBy)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(sourceId) ||
                journalEntryId <= 0 || amount <= 0m || string.IsNullOrWhiteSpace(createdBy))
                throw new InvalidOperationException("بيانات حركة ذمة العميل غير مكتملة.");
            const string sql = @"
IF OBJECT_ID(N'dbo.CustomerAccountLedgerTb',N'U') IS NULL
    THROW 51000,N'ترحيل دفتر ذمم العملاء غير مثبت؛ لا يمكن منح رصيد عميل.',1;
IF EXISTS(SELECT 1 FROM dbo.CustomerAccountLedgerTb WITH(UPDLOCK,HOLDLOCK) WHERE SourceType=N'CommercialSalesReturn' AND SourceId=@SourceId AND EntryKind=N'CreditGranted')
    RETURN;
IF NOT EXISTS
(
    SELECT 1 FROM dbo.JournalEntriesTb e
    JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
    WHERE e.JournalEntryId=@JournalEntryId AND e.SourceType=N'CommercialSalesReturn' AND e.SourceId=@SourceId AND l.AccountCode=@CustomersAccount
    GROUP BY e.JournalEntryId HAVING SUM(l.Credit-l.Debit)=@Amount
)
    THROW 51000,N'حركة ذمة العميل لا تطابق قيد حساب العملاء العام.',1;
INSERT dbo.CustomerAccountLedgerTb
(LedgerEntryId,CustomerId,JournalEntryId,SourceType,SourceId,EntryKind,Debit,Credit,EntryDateUtc,CreatedBy)
VALUES(NEWID(),@CustomerId,@JournalEntryId,N'CommercialSalesReturn',@SourceId,N'CreditGranted',0,@Amount,@EntryDateUtc,@CreatedBy);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId.Trim();
                command.Parameters.Add("@JournalEntryId", SqlDbType.Int).Value = journalEntryId;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = sourceId.Trim();
                command.Parameters.Add("@CustomersAccount", SqlDbType.NVarChar, 20).Value = AccountingService.CustomersAccount;
                SqlParameter amountParameter = command.Parameters.Add("@Amount", SqlDbType.Decimal);
                amountParameter.Precision = 18; amountParameter.Scale = 2; amountParameter.Value = InvoiceTotalsCalculator.Round(amount);
                command.Parameters.Add("@EntryDateUtc", SqlDbType.DateTime2).Value = entryDateUtc.ToUniversalTime();
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = createdBy.Trim();
                command.ExecuteNonQuery();
            }
        }

        internal static void RecordCreditUse(
            SqlConnection connection, SqlTransaction transaction,
            string customerId, int journalEntryId, string invoiceId,
            decimal amount, DateTime entryDateUtc, string createdBy)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            amount = InvoiceTotalsCalculator.Round(amount);
            if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(invoiceId) || journalEntryId <= 0 || amount <= 0m || string.IsNullOrWhiteSpace(createdBy))
                throw new InvalidOperationException("بيانات استخدام رصيد العميل غير مكتملة.");
            using (SqlCommand existing = new SqlCommand(@"SELECT COUNT(*) FROM dbo.CustomerAccountLedgerTb WITH(UPDLOCK,HOLDLOCK) WHERE SourceType=N'CommercialSalesInvoice' AND SourceId=@SourceId AND EntryKind=N'CreditUsed';", connection, transaction))
            {
                existing.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = invoiceId.Trim();
                if (Convert.ToInt32(existing.ExecuteScalar()) > 0) return;
            }
            decimal available = GetBalanceForUpdate(connection, transaction, customerId);
            if (available < amount) throw new InvalidOperationException("رصيد العميل المتاح تغير ولا يكفي لإتمام الفاتورة. حدّث بيانات العميل ثم أعد المحاولة.");
            const string sql = @"
IF EXISTS(SELECT 1 FROM dbo.CustomerAccountLedgerTb WITH(UPDLOCK,HOLDLOCK) WHERE SourceType=N'CommercialSalesInvoice' AND SourceId=@SourceId AND EntryKind=N'CreditUsed')
    RETURN;
IF NOT EXISTS
(
    SELECT 1 FROM dbo.SalesInvoicesTb h JOIN dbo.SalesInvoicePaymentsTb p ON p.InvoiceId=h.InvoiceId
    WHERE h.InvoiceId=TRY_CONVERT(uniqueidentifier,@SourceId) AND h.CustomerId=@CustomerId AND h.Status=N'Issued'
      AND p.PaymentMethod=N'CustomerCredit' AND p.Amount=@Amount
)
    THROW 51000,N'استخدام رصيد العميل لا يطابق دفعة الفاتورة المعتمدة.',1;
IF NOT EXISTS
(
    SELECT 1 FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
    WHERE e.JournalEntryId=@JournalEntryId AND e.SourceType=N'SalesInvoice' AND e.SourceId=@SourceId AND l.AccountCode=@CustomersAccount
    GROUP BY e.JournalEntryId HAVING SUM(l.Debit-l.Credit)=@Amount
)
    THROW 51000,N'استخدام رصيد العميل لا يطابق قيد حساب العملاء العام.',1;
INSERT dbo.CustomerAccountLedgerTb
(LedgerEntryId,CustomerId,JournalEntryId,SourceType,SourceId,EntryKind,Debit,Credit,EntryDateUtc,CreatedBy)
VALUES(NEWID(),@CustomerId,@JournalEntryId,N'CommercialSalesInvoice',@SourceId,N'CreditUsed',@Amount,0,@EntryDateUtc,@CreatedBy);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId.Trim();
                command.Parameters.Add("@JournalEntryId", SqlDbType.Int).Value = journalEntryId;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = invoiceId.Trim();
                command.Parameters.Add("@CustomersAccount", SqlDbType.NVarChar, 20).Value = AccountingService.CustomersAccount;
                SqlParameter amountParameter = command.Parameters.Add("@Amount", SqlDbType.Decimal);
                amountParameter.Precision = 18; amountParameter.Scale = 2; amountParameter.Value = amount;
                command.Parameters.Add("@EntryDateUtc", SqlDbType.DateTime2).Value = entryDateUtc.ToUniversalTime();
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = createdBy.Trim();
                command.ExecuteNonQuery();
            }
        }

        internal static decimal GetBalanceForUpdate(SqlConnection connection, SqlTransaction transaction, string customerId)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            using (SqlCommand command = new SqlCommand(@"
IF OBJECT_ID(N'dbo.CustomerAccountLedgerTb',N'U') IS NULL THROW 51000,N'ترحيل دفتر ذمم العملاء غير مثبت.',1;
SELECT ISNULL(SUM(Credit-Debit),0) FROM dbo.CustomerAccountLedgerTb WITH(UPDLOCK,HOLDLOCK) WHERE CustomerId=@CustomerId;", connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = (customerId ?? string.Empty).Trim();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        internal static decimal GetBalance(SqlConnection connection, SqlTransaction transaction, string customerId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            using (SqlCommand command = new SqlCommand(@"
IF OBJECT_ID(N'dbo.CustomerAccountLedgerTb',N'U') IS NULL SELECT CAST(0 AS DECIMAL(18,2));
ELSE SELECT ISNULL(SUM(Credit-Debit),0) FROM dbo.CustomerAccountLedgerTb WHERE CustomerId=@CustomerId;", connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = (customerId ?? string.Empty).Trim();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }
    }
}
