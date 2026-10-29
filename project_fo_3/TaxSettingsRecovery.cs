using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Xml.Linq;

namespace project_fo_3
{
    // A receipt committed with SQL is the authority after an interrupted cross-store save.
    internal static class TaxSettingsRecovery
    {
        private static readonly string PendingPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PharmacyManagement", "tax-settings-pending.xml");
        private static readonly Mutex Gate = new Mutex(false,
            @"Local\PharmacyTaxSettings-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value);

        internal static IDisposable Enter()
        {
            bool acquired;
            try { acquired = Gate.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new InvalidOperationException("يجري تحديث سياسة الضريبة. أعد المحاولة بعد اكتمال الحفظ.");
            return new GateLease();
        }

        private sealed class GateLease : IDisposable
        {
            public void Dispose() { Gate.ReleaseMutex(); }
        }

        private static bool HasPendingRecord()
        {
            try
            {
                File.GetAttributes(PendingPath);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            // Access and I/O failures must propagate: absence was not established.
        }
        internal static void EnsureSettled()
        {
            if (HasPendingRecord())
                throw new InvalidOperationException("يوجد حفظ ضريبة غير مكتمل. افتح إعدادات الضرائب لاستعادة السياسة قبل إصدار فاتورة جديدة.");
        }

        internal static string Begin(SqlConnection connection, string previousCountry, string country)
        {
            EnsureSettled();
            string operation = Guid.NewGuid().ToString("D");
            var document = new XDocument(new XElement("TaxSettingsSave",
                new XAttribute("operation", operation),
                new XAttribute("server", connection.DataSource),
                new XAttribute("database", connection.Database),
                new XAttribute("previousCountry", previousCountry),
                new XAttribute("country", country)));
            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath));
            using (var stream = new FileStream(PendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                document.Save(stream);
                stream.Flush(true);
            }
            return operation;
        }

        internal static void WriteReceipt(SqlConnection connection, SqlTransaction transaction, string operation)
        {
            using (var command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName, UserRole, ActionName, EntityName, Details)
VALUES (@User, @Role, N'TaxPolicyCommitted', N'AccountingSettings', @Operation)", connection, transaction))
            {
                command.Parameters.Add("@User", SqlDbType.NVarChar, 200).Value = SessionContext.CurrentUser;
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = SessionContext.CurrentRole;
                command.Parameters.Add("@Operation", SqlDbType.NVarChar, 1000).Value = operation;
                command.ExecuteNonQuery();
            }
        }

        internal static void Complete(string country)
        {
            Properties.Setings.Default.TaxCountryCode = country;
            Properties.Setings.Default.Save();
            File.Delete(PendingPath);
        }

        public static void Recover()
        {
            using (Enter())
            {
                if (!HasPendingRecord()) return;
                XElement record = XDocument.Load(PendingPath).Root;
                Guid operation;
                if (record == null || !Guid.TryParse((string)record.Attribute("operation"), out operation))
                    throw new InvalidDataException("سجل استعادة الضريبة غير صالح؛ يلزم فحصه بواسطة الدعم.");
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    if (!string.Equals(connection.DataSource, (string)record.Attribute("server"), StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(connection.Database, (string)record.Attribute("database"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("أعد الاتصال بقاعدة البيانات التي بدأ عليها حفظ الضريبة لاستكمال الاستعادة.");
                    using (var command = new SqlCommand(@"SELECT COUNT(*) FROM dbo.AuditLogTb
WHERE ActionName = N'TaxPolicyCommitted' AND EntityName = N'AccountingSettings' AND Details = @Operation", connection))
                    {
                        command.Parameters.Add("@Operation", SqlDbType.NVarChar, 1000).Value = operation.ToString("D");
                        bool committed = Convert.ToInt32(command.ExecuteScalar()) > 0;
                        Complete(ResolveCountry(record, committed));
                    }
                }
            }
        }

        internal static string ResolveCountry(XElement record, bool committed)
        {
            string country = (string)record.Attribute(committed ? "country" : "previousCountry");
            if (string.IsNullOrWhiteSpace(country)) throw new InvalidDataException("بلد الاستعادة مفقود.");
            return country;
        }
    }
}