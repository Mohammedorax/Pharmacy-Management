using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    // يسجل الأعطال غير المتوقعة محلياً دون عرض تفاصيل تقنية أو بيانات حساسة للمستخدم.
    internal static class AppDiagnostics
    {
        private const string ProductFolderName = "PharmacyManagement";
        private const long MaxLogFileBytes = 5 * 1024 * 1024;
        private const int LogRetentionDays = 30;
        private const int MaxLogFiles = 20;
        private static readonly object ExceptionHandlerSyncRoot = new object();
        private static bool exceptionHandlingConfigured;

        public static void ConfigureGlobalExceptionHandling()
        {
            lock (ExceptionHandlerSyncRoot)
            {
                if (exceptionHandlingConfigured)
                {
                    return;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnUiThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                exceptionHandlingConfigured = true;
            }
        }

        private static void OnUiThreadException(object sender, System.Threading.ThreadExceptionEventArgs eventArgs)
        {
            string supportCode = Log("UiThread", eventArgs.Exception);
            AppNotifications.Show(
                "حدث خطأ غير متوقع وتم إيقاف العملية الحالية بأمان.\n\n" +
                "أغلق النظام ثم افتحه مجدداً. إذا تكرر الخطأ، أرسل رمز الدعم إلى مسؤول النظام: " + supportCode,
                "خطأ غير متوقع",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Application.Exit();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            Log("AppDomain", eventArgs.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs eventArgs)
        {
            Log("Task", eventArgs.Exception);
            eventArgs.SetObserved();
        }

        internal static string Log(string category, Exception exception)
        {
            return WriteEntry(category, BuildSafeDiagnostic(exception), isFailure: true);
        }

        // يسجل حدثاً تشغيلياً موجزاً لا يتضمن سلسلة اتصال أو بيانات اعتماد.
        // يجب أن يمرر المستدعي قيماً آمنة فقط، مثل اسم قاعدة البيانات ونتيجة فحصها.
        internal static string LogSafeEvent(string category, string details)
        {
            string normalized = (details ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
            if (normalized.Length > 500)
            {
                normalized = normalized.Substring(0, 500);
            }

            return WriteEntry(category, "Event=" + normalized, isFailure: false);
        }

        private static string WriteEntry(string category, string payload, bool isFailure)
        {
            string supportCode = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolderName, "logs");
                Directory.CreateDirectory(directory);
                TrimExpiredLogs(directory);
                string fileName = "application-" + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
                string path = Path.Combine(directory, fileName);
                if (File.Exists(path) && new FileInfo(path).Length >= MaxLogFileBytes)
                    path = Path.Combine(directory, "application-" + supportCode + ".log");
                File.AppendAllText(path,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\t" + supportCode + "\t" + (category ?? "Unknown") + Environment.NewLine + (payload ?? string.Empty) + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // لا ينبغي أن يحجب فشل السجل مسار الإغلاق الآمن.
            }

            if (isFailure)
            {
                Trace.TraceError("Unhandled application failure. SupportCode={0}; Category={1}", supportCode, category ?? "Unknown");
            }
            else
            {
                Trace.TraceInformation("Application diagnostic event. SupportCode={0}; Category={1}", supportCode, category ?? "Unknown");
            }
            return supportCode;
        }

        private static string BuildSafeDiagnostic(Exception exception)
        {
            if (exception == null) return "No exception payload was available.";

            // لا نسجل Message أو ToString لأنهما قد يحتويان SQL أو اسم خادم أو مدخلات مستخدم.
            var lines = new System.Collections.Generic.List<string>();
            Exception current = exception;
            int depth = 0;
            while (current != null && depth < 8)
            {
                string prefix = depth == 0 ? string.Empty : "Inner" + depth.ToString(CultureInfo.InvariantCulture) + ".";
                lines.Add(prefix + "ExceptionType=" + current.GetType().FullName);
                lines.Add(prefix + "HResult=0x" + current.HResult.ToString("X8", CultureInfo.InvariantCulture));

                System.Data.SqlClient.SqlException sqlException = current as System.Data.SqlClient.SqlException;
                if (sqlException != null)
                {
                    string sqlErrorCodes = string.Join(",", sqlException.Errors.Cast<System.Data.SqlClient.SqlError>()
                        .Select(error => error.Number.ToString(CultureInfo.InvariantCulture) + "/" +
                            error.State.ToString(CultureInfo.InvariantCulture) + "/" +
                            error.Class.ToString(CultureInfo.InvariantCulture))
                        .Distinct());
                    lines.Add(prefix + "SqlErrorCodes=Number/State/Class:" + sqlErrorCodes);
                }

                System.ComponentModel.Win32Exception win32Exception = current as System.ComponentModel.Win32Exception;
                if (win32Exception != null)
                {
                    lines.Add(prefix + "NativeErrorCode=" + win32Exception.NativeErrorCode.ToString(CultureInfo.InvariantCulture));
                }

                if (!string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    lines.Add(prefix + "StackTrace=");
                    lines.Add(current.StackTrace);
                }

                current = current.InnerException;
                depth++;
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void TrimExpiredLogs(string directory)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-LogRetentionDays);
            foreach (string path in Directory.GetFiles(directory, "application-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
                }
                catch
                {
                    // أفضل جهد فقط؛ فشل الحذف لا يعطل تسجيل العطل الحالي.
                }
            }

            string[] retained = Directory.GetFiles(directory, "application-*.log")
                .OrderBy(path => File.GetLastWriteTimeUtc(path))
                .ToArray();
            for (int index = 0; index < retained.Length - MaxLogFiles + 1; index++)
            {
                try { File.Delete(retained[index]); }
                catch { }
            }
        }
    }
}
