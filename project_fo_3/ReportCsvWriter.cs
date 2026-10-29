using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace project_fo_3
{
    internal static class ReportCsvWriter
    {
        internal static bool TryWriteText(string fileName, string content, out Exception error)
        {
            string temporaryPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A CSV file path is required.", nameof(fileName));
                string destination = System.IO.Path.GetFullPath(fileName);
                string directory = System.IO.Path.GetDirectoryName(destination);
                if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("The CSV destination directory is invalid.", nameof(fileName));
                temporaryPath = System.IO.Path.Combine(directory,
                    "." + System.IO.Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new System.IO.FileStream(temporaryPath, System.IO.FileMode.CreateNew,
                    System.IO.FileAccess.Write, System.IO.FileShare.None))
                using (var writer = new System.IO.StreamWriter(stream, new UTF8Encoding(true)))
                {
                    writer.Write(content ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (System.IO.File.Exists(destination)) System.IO.File.Replace(temporaryPath, destination, null);
                else System.IO.File.Move(temporaryPath, destination);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception;
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); }
                    catch { }
                }
            }
        }

        internal static string DateText(DateTime value)
        {
            return value.ToString(value.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        internal static string Serialize(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(column => Cell(column.ColumnName))));
            foreach (DataRow row in table.Rows)
                builder.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(column => Cell(row[column]))));
            return builder.ToString();
        }

        internal static string Cell(object value)
        {
            if (value == null || value == DBNull.Value) return "\"\"";
            string text;
            bool numeric = value is decimal || value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong || value is float || value is double;
            if (numeric) text = Convert.ToString(value, CultureInfo.InvariantCulture);
            else if (value is DateTime)
                text = DateText((DateTime)value);
            else
            {
                text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
                string trimmed = text.TrimStart();
                // Text may contain spreadsheet formulas; typed numbers retain numeric minus signs.
                if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) text = "'" + text;
            }
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
