using System;
using System.Collections.Generic;
using PharmacyManagement.Domain;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalEntryPreparationTests
    {
        private static int checks;
        private static void Check(bool value) { if (!value) throw new Exception("Journal preparation contract failed."); checks++; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        private static JournalLineValues Line(decimal debit, decimal credit, string code = "1000")
            => new JournalLineValues(code, debit, credit, "بيان Cash 500 mg", 7);
        internal static void VerifyQaReadOnly(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Journal preparation integration only accepts _qa.");
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 15 };
            var entries = new Dictionary<int, List<JournalLineValues>>();
            using (var connection = new System.Data.SqlClient.SqlConnection(builder.ConnectionString))
            {
                connection.Open(); using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DB_NAME()";
                    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Unexpected QA database.");
                    command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.JournalEntryLinesTb') AND name IN (N'Debit',N'Credit') AND system_type_id IN (106,108) AND precision=18 AND scale=2";
                    if (Convert.ToInt32(command.ExecuteScalar()) != 2)
                        throw new InvalidOperationException("QA journal line precision differs from the validated 18,2 contract.");                    command.CommandText = "SELECT JournalEntryId, AccountCode, Debit, Credit, LineDescription, CostCenterId FROM dbo.JournalEntryLinesTb ORDER BY JournalEntryId, JournalEntryLineId";
                    using (var reader = command.ExecuteReader()) while (reader.Read())
                    {
                        int id = reader.GetInt32(0); List<JournalLineValues> lines;
                        if (!entries.TryGetValue(id, out lines)) entries.Add(id, lines = new List<JournalLineValues>());
                        lines.Add(new JournalLineValues(reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)));
                    }
                }
            }
            int compared = 0;
            foreach (var entry in entries)
            {
                var prepared = JournalEntryPreparation.Prepare(entry.Value);
                var expected = entry.Value.FindAll(line => !string.IsNullOrWhiteSpace(line.AccountCode) && (line.Debit > 0m || line.Credit > 0m));
                if (prepared.Count != expected.Count) throw new Exception("QA prepared journal line count differs.");
                for (int i = 0; i < prepared.Count; i++)
                    if (prepared[i].AccountCode != expected[i].AccountCode.Trim() || prepared[i].Debit != expected[i].Debit ||
                        prepared[i].Credit != expected[i].Credit || prepared[i].Description != expected[i].Description ||
                        prepared[i].CostCenterId != expected[i].CostCenterId)
                        throw new Exception("QA stored journal values differ after domain preparation.");
                compared += prepared.Count;
            }
            Console.WriteLine("PASS: existing QA journal values preserved by domain preparation; Entries=" + entries.Count +
                "; LinesCompared=" + compared + "; ColumnPrecision=18,2; PersistentWrites=0. This is not a posting transaction test.");
        }


        internal static int Run()
        {
            checks = 0;
            Throws<ArgumentNullException>(() => JournalEntryPreparation.Prepare(null));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new JournalLineValues[0]));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(10, 0) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(10, 0), Line(0, 9) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(-5, 10), Line(20, 5) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(10, 5), Line(5, 10) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(0, -5), Line(0, 5) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(-5, 0), Line(0, 5) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(.001m, 0), Line(0, .001m) }));
            Throws<InvalidOperationException>(() => JournalEntryPreparation.Prepare(new[] { Line(5.005m, 0), Line(5.005m, 0), Line(0, 10.01m) }));
            var input = new List<JournalLineValues> { null, Line(0, 0), Line(1, 0, " "), Line(10.005m, 0, " 1000 "), Line(0, 10.005m, "1100") };
            var prepared = JournalEntryPreparation.Prepare(input); input.Clear();
            Check(prepared.Count == 2 && prepared[0].AccountCode == "1000" && prepared[1].AccountCode == "1100");
            Check(prepared[0].Debit == 10.00m && prepared[1].Credit == 10.00m && prepared[0].Credit == 0 && prepared[1].Debit == 0);
            Check(prepared[0].Description == "بيان Cash 500 mg" && prepared[0].CostCenterId == 7);
            Throws<NotSupportedException>(() => ((IList<JournalLineValues>)prepared).Clear());
            prepared = JournalEntryPreparation.Prepare(new[] { Line(10.015m, 0), Line(0, 10.015m) });
            Check(prepared[0].Debit == 10.02m && prepared[1].Credit == 10.02m);
            prepared = JournalEntryPreparation.Prepare(new[] { Line(5.005m, 0), Line(5.015m, 0), Line(0, 10.02m) });
            Check(prepared.Count == 3 && prepared[0].Debit == 5m && prepared[1].Debit == 5.02m && prepared[2].Credit == 10.02m);
            prepared = JournalEntryPreparation.Prepare(new[] { new JournalLineValues("1000", 1m, 0m, null, null), Line(0, 1m) });
            Check(prepared[0].Description == null && prepared[0].CostCenterId == null);
            Throws<JournalAmountException>(() => JournalEntryPreparation.Prepare(new[] { Line(decimal.MaxValue, 0), Line(1, 0), Line(0, decimal.MaxValue) }));
            Console.WriteLine("PASS: " + checks + " journal line validation/rounding contracts; no database connection.");
            return checks;
        }
    }
}
