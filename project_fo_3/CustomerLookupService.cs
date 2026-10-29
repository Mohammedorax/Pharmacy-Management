using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace project_fo_3
{
    // يحصر البحث عن العميل في خدمة واحدة حتى يبقى قفل عدم التكرار متسقاً مع حفظ الفاتورة.
    internal static class CustomerLookupService
    {
        internal static string FindCustomerIdByPhone(SqlConnection connection, SqlTransaction transaction, string phoneNumber)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(phoneNumber)) return string.Empty;

            // HOLDLOCK يحافظ على قفل نطاق رقم الهاتف حتى نهاية معاملة الفاتورة.
            // بذلك لا تستطيع فاتورتان متزامنتان إنشاء عميلين لنفس الهاتف بعد أن تجدا النتيجة فارغة.
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 CustomerId
FROM dbo.CustomersBillsTb WITH (UPDLOCK, HOLDLOCK)
WHERE PhoneNumber = @PhoneNumber
ORDER BY InvoiceDate DESC;", connection, transaction))
            {
                command.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 50).Value = phoneNumber.Trim();
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
            }
        }

        // فحص تكاملي غير متلف للـ QA: لا يضيف بيانات، بل يثبت أن قفل غياب رقم الهاتف يمنع السباق.
        internal static void VerifyQaMissingPhoneLookupSerializes(string serverName, string databaseName)
        {
            EnsureQaDatabase(databaseName);
            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);
            string probePhone = "__qa_customer_lock_" + Guid.NewGuid().ToString("N").Substring(0, 20);

            using (SqlConnection firstConnection = new SqlConnection(connectionString))
            {
                firstConnection.Open();
                SqlTransaction firstTransaction = firstConnection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    if (!string.IsNullOrEmpty(FindCustomerIdByPhone(firstConnection, firstTransaction, probePhone)))
                    {
                        throw new InvalidOperationException("رقم هاتف فحص التزامن موجود مسبقاً بصورة غير متوقعة.");
                    }

                    using (ManualResetEventSlim secondProbeStarted = new ManualResetEventSlim(false))
                    {
                        Task<string> secondProbe = Task.Factory.StartNew(
                            () => RunSecondProbe(connectionString, probePhone, secondProbeStarted),
                            CancellationToken.None,
                            TaskCreationOptions.LongRunning,
                            TaskScheduler.Default);

                        if (!secondProbeStarted.Wait(TimeSpan.FromSeconds(5)))
                        {
                            throw new InvalidOperationException("لم يبدأ فحص التزامن الثاني ضمن المهلة.");
                        }

                        // قبل إنهاء المعاملة الأولى يجب أن يبقى البحث الثاني منتظراً على قفل النطاق نفسه.
                        if (secondProbe.Wait(TimeSpan.FromMilliseconds(900)))
                        {
                            throw new InvalidOperationException("بحث العميل المتزامن لم ينتظر القفل؛ قد يؤدي ذلك إلى تكرار العميل.");
                        }

                        firstTransaction.Commit();
                        firstTransaction.Dispose();
                        firstTransaction = null;

                        if (!secondProbe.Wait(TimeSpan.FromSeconds(10)))
                        {
                            throw new InvalidOperationException("لم يكتمل بحث العميل الثاني بعد تحرير القفل.");
                        }

                        if (!string.IsNullOrEmpty(secondProbe.GetAwaiter().GetResult()))
                        {
                            throw new InvalidOperationException("أعاد فحص العميل المتزامن نتيجة غير متوقعة لرقم هاتف الفحص.");
                        }
                    }
                }
                catch
                {
                    if (firstTransaction != null)
                    {
                        try { firstTransaction.Rollback(); }
                        catch { }
                    }
                    throw;
                }
                finally
                {
                    if (firstTransaction != null) firstTransaction.Dispose();
                }
            }
        }

        private static string RunSecondProbe(string connectionString, string probePhone, ManualResetEventSlim started)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        started.Set();
                        string customerId = FindCustomerIdByPhone(connection, transaction, probePhone);
                        transaction.Rollback();
                        return customerId;
                    }
                    catch
                    {
                        try { transaction.Rollback(); }
                        catch { }
                        throw;
                    }
                }
            }
        }

        private static void EnsureQaDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص تزامن العملاء مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }
        }
    }
}
