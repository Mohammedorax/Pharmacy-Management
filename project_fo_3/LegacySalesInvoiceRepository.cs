using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal sealed class LegacyMappedSalesInvoice
    {
        public string InvoiceReference { get; private set; }
        public decimal TotalAmount { get; private set; }
        public List<string> BillLineIds { get; private set; }

        public LegacyMappedSalesInvoice(string invoiceReference, decimal totalAmount, List<string> billLineIds)
        {
            InvoiceReference = invoiceReference;
            TotalAmount = totalAmount;
            BillLineIds = billLineIds ?? new List<string>();
        }
    }

    // يضيف رابطاً صريحاً بين بنود البيع القديمة ورأس فاتورتها، من دون تخمين أو إعادة تجميع البيانات التاريخية.
    internal static class LegacySalesInvoiceRepository
    {
        public static bool HasSchema(SqlConnection connection, SqlTransaction transaction)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            using (SqlCommand command = new SqlCommand(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb', N'U') IS NOT NULL
                  AND OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb', N'U') IS NOT NULL
            THEN 1 ELSE 0 END;", connection, transaction))
            {
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        // يستدعى داخل معاملة حفظ البيع نفسها؛ لذلك لا يبقى رأس أو رابط يتيم إذا فشل الحفظ لاحقاً.
        public static void RecordIssuedInvoice(
            SqlConnection connection,
            SqlTransaction transaction,
            string invoiceReference,
            IEnumerable<string> billLineIds,
            string customerId,
            string customerName,
            string phoneNumber,
            decimal totalAmount,
            string employeeName,
            DateTime invoiceDate,
            string invoiceNotes)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (string.IsNullOrWhiteSpace(invoiceReference)) throw new InvalidOperationException("مرجع الفاتورة مطلوب لأرشفة البيع.");
            if (totalAmount <= 0m) throw new InvalidOperationException("إجمالي الفاتورة غير صالح للأرشفة.");

            List<string> ids = NormalizeBillIds(billLineIds);
            if (ids.Count == 0) throw new InvalidOperationException("لا توجد بنود صالحة لأرشفة الفاتورة.");

            EnsureSchema(connection, transaction);

            using (SqlCommand header = new SqlCommand(@"
INSERT INTO dbo.LegacySalesInvoiceHeadersTb
    (InvoiceReference, CustomerId, CustomerNameSnapshot, CustomerPhoneSnapshot, TotalAmount, InvoiceDate, EmployeeName, InvoiceNotes)
VALUES
    (@InvoiceReference, @CustomerId, @CustomerName, @CustomerPhone, @TotalAmount, @InvoiceDate, @EmployeeName, @InvoiceNotes);", connection, transaction))
            {
                header.Parameters.Add("@InvoiceReference", SqlDbType.NVarChar, 50).Value = invoiceReference.Trim();
                header.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = DbValue(customerId);
                header.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 200).Value = DbValue(customerName);
                header.Parameters.Add("@CustomerPhone", SqlDbType.NVarChar, 50).Value = DbValue(phoneNumber);
                AddMoney(header, "@TotalAmount", totalAmount);
                header.Parameters.Add("@InvoiceDate", SqlDbType.DateTime).Value = invoiceDate;
                header.Parameters.Add("@EmployeeName", SqlDbType.NVarChar, 200).Value = DbValue(employeeName);
                header.Parameters.Add("@InvoiceNotes", SqlDbType.NVarChar, 1000).Value = DbValue(invoiceNotes);
                header.ExecuteNonQuery();
            }

            for (int index = 0; index < ids.Count; index++)
            {
                using (SqlCommand line = new SqlCommand(@"
INSERT INTO dbo.LegacySalesInvoiceLinesTb (InvoiceReference, LineNumber, BillId)
VALUES (@InvoiceReference, @LineNumber, @BillId);", connection, transaction))
                {
                    line.Parameters.Add("@InvoiceReference", SqlDbType.NVarChar, 50).Value = invoiceReference.Trim();
                    line.Parameters.Add("@LineNumber", SqlDbType.Int).Value = index + 1;
                    line.Parameters.Add("@BillId", SqlDbType.NVarChar, 50).Value = ids[index];
                    line.ExecuteNonQuery();
                }
            }
        }

        // لا يرجع هذا الأسلوب أي بنود تاريخية غير مربوطة صراحة برأس فاتورة جديد.
        public static LegacyMappedSalesInvoice GetMappedInvoice(SqlConnection connection, SqlTransaction transaction, string invoiceReference)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(invoiceReference)) throw new InvalidOperationException("مرجع الفاتورة غير صالح.");
            if (!HasSchema(connection, transaction))
            {
                throw new InvalidOperationException("هذه الفاتورة من السجل القديم غير المربوط؛ لا يمكن إلغاؤها آلياً دون خطر تجميع بنود غير صحيحة.");
            }

            decimal totalAmount;
            using (SqlCommand headerCommand = new SqlCommand(@"
SELECT TotalAmount
FROM dbo.LegacySalesInvoiceHeadersTb WITH (UPDLOCK, HOLDLOCK)
WHERE InvoiceReference = @InvoiceReference;", connection, transaction))
            {
                headerCommand.Parameters.Add("@InvoiceReference", SqlDbType.NVarChar, 50).Value = invoiceReference.Trim();
                object value = headerCommand.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    throw new InvalidOperationException("لا يوجد رأس موثوق لهذه الفاتورة؛ أُوقف الإلغاء حفاظاً على سلامة البيانات.");
                }
                totalAmount = Convert.ToDecimal(value);
            }

            if (totalAmount <= 0m)
            {
                throw new InvalidOperationException("إجمالي الفاتورة المؤرشف غير صالح للإلغاء.");
            }

            List<string> ids = new List<string>();
            using (SqlCommand command = new SqlCommand(@"
SELECT BillId
FROM dbo.LegacySalesInvoiceLinesTb WITH (UPDLOCK, HOLDLOCK)
WHERE InvoiceReference = @InvoiceReference
ORDER BY LineNumber;", connection, transaction))
            {
                command.Parameters.Add("@InvoiceReference", SqlDbType.NVarChar, 50).Value = invoiceReference.Trim();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) ids.Add(Convert.ToString(reader["BillId"]));
                }
            }

            if (ids.Count == 0)
            {
                throw new InvalidOperationException("لا يوجد ربط موثوق لبنود هذه الفاتورة؛ أُوقف الإلغاء حفاظاً على المخزون والحسابات.");
            }

            return new LegacyMappedSalesInvoice(invoiceReference.Trim(), totalAmount, ids);
        }

        private static void EnsureSchema(SqlConnection connection, SqlTransaction transaction)
        {
            const string schemaSql = @"
DECLARE @LockResult INT;
EXEC @LockResult = sp_getapplock
    @Resource = N'PharmacyLegacySalesInvoiceSchema-v1',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 10000;
IF @LockResult < 0
BEGIN
    RAISERROR(N'تعذر تأمين ترحيل أرشفة فواتير البيع. أعد المحاولة.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegacySalesInvoiceHeadersTb (
        InvoiceReference NVARCHAR(50) NOT NULL CONSTRAINT PK_LegacySalesInvoiceHeadersTb PRIMARY KEY,
        CustomerId NVARCHAR(50) NULL,
        CustomerNameSnapshot NVARCHAR(200) NULL,
        CustomerPhoneSnapshot NVARCHAR(50) NULL,
        TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT CK_LegacySalesInvoiceHeadersTb_Total CHECK (TotalAmount > 0),
        InvoiceDate DATETIME NOT NULL,
        EmployeeName NVARCHAR(200) NULL,
        InvoiceNotes NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegacySalesInvoiceHeadersTb_Created DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegacySalesInvoiceLinesTb (
        InvoiceReference NVARCHAR(50) NOT NULL,
        LineNumber INT NOT NULL,
        BillId NVARCHAR(50) NOT NULL,
        CONSTRAINT PK_LegacySalesInvoiceLinesTb PRIMARY KEY (InvoiceReference, LineNumber),
        CONSTRAINT UX_LegacySalesInvoiceLinesTb_BillId UNIQUE (BillId),
        CONSTRAINT FK_LegacySalesInvoiceLinesTb_Header FOREIGN KEY (InvoiceReference)
            REFERENCES dbo.LegacySalesInvoiceHeadersTb (InvoiceReference),
        CONSTRAINT CK_LegacySalesInvoiceLinesTb_LineNumber CHECK (LineNumber > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LegacySalesInvoiceHeadersTb_InvoiceDate' AND object_id = OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb'))
    CREATE INDEX IX_LegacySalesInvoiceHeadersTb_InvoiceDate ON dbo.LegacySalesInvoiceHeadersTb(InvoiceDate DESC);";

            using (SqlCommand command = new SqlCommand(schemaSql, connection, transaction))
            {
                command.CommandTimeout = 30;
                command.ExecuteNonQuery();
            }
        }

        private static List<string> NormalizeBillIds(IEnumerable<string> billLineIds)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ids = new List<string>();
            foreach (string value in billLineIds ?? new string[0])
            {
                string id = value == null ? string.Empty : value.Trim();
                if (id.Length == 0 || !seen.Add(id)) continue;
                ids.Add(id);
            }
            return ids;
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static void AddMoney(SqlCommand command, string name, decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 18;
            parameter.Scale = 2;
            parameter.Value = value;
        }
    }
}
