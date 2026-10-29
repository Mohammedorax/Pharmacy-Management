using System;
using System.Globalization;
using System.IO;

namespace project_fo_3
{
    // Free, local, no-login performance trace. It appends one line per slow operation to
    // %TEMP%\pharmacy-perf.log so screen-open and binding costs can be compared before/after.
    // Disable with PHARMACY_PERF=0.
    internal static class PerfTrace
    {
        private static readonly object Gate = new object();

        internal static string FilePath
        {
            get { return Path.Combine(Path.GetTempPath(), "pharmacy-perf.log"); }
        }

        internal static bool Enabled
        {
            get { return !string.Equals(Environment.GetEnvironmentVariable("PHARMACY_PERF"), "0", StringComparison.Ordinal); }
        }

        internal static void Log(string area, string detail, double milliseconds)
        {
            if (!Enabled || milliseconds < 15.0) return;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(FilePath, string.Format(CultureInfo.InvariantCulture,
                        "{0:yyyy-MM-dd HH:mm:ss.fff}\t{1}\t{2}\t{3:F1} ms\n", DateTime.Now, area, detail, milliseconds));
                }
            }
            catch
            {
                // Instrumentation must never break the pharmacy workflow.
            }
        }
    }
}
