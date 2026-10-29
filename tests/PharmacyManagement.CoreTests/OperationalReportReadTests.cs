using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class OperationalReportReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var request = new OperationalReportRequest(OperationalReportKind.Medicines,
                new DateTime(2026, 9, 1, 13, 15, 0), new DateTime(2026, 9, 30, 8, 0, 0), " باراسيتامول 500 mg ");
            Check(request.FromDate == new DateTime(2026, 9, 1), ref checks);
            Check(request.ToDateExclusive == new DateTime(2026, 10, 1), ref checks);
            Check(request.SearchText == "باراسيتامول 500 mg", ref checks);
            foreach (OperationalReportKind kind in Enum.GetValues(typeof(OperationalReportKind)))
                Check(SqlOperationalReportQueries.Get(kind).Contains("@Take"), ref checks);
            var legacyRequest = new OperationalReportRequest(OperationalReportKind.LegacySalesLines,
                DateTime.Today, DateTime.Today, "000123");
            Check(legacyRequest.Kind == OperationalReportKind.LegacySalesLines && legacyRequest.SearchText == "000123", ref checks);
            string legacyQuery = SqlOperationalReportQueries.Get(legacyRequest.Kind);
            Check(legacyQuery.Contains("FROM dbo.BillsTb1") && !legacyQuery.Contains("SalesInvoicesTb"), ref checks);
            Check(!legacyQuery.Contains("@FromDate") && !legacyQuery.Contains("@ToDate"), ref checks);
            Reject<InvalidOperationException>(() => new OperationalReportRequest((OperationalReportKind)999, DateTime.Today, DateTime.Today, ""), ref checks);
            Reject<InvalidOperationException>(() => new OperationalReportRequest(OperationalReportKind.Medicines, DateTime.Today, DateTime.Today.AddDays(-1), ""), ref checks);
            Reject<InvalidOperationException>(() => new OperationalReportRequest(OperationalReportKind.Medicines, DateTime.MinValue, DateTime.Today, ""), ref checks);
            Reject<InvalidOperationException>(() => new OperationalReportRequest(OperationalReportKind.Medicines, DateTime.Today, DateTime.MaxValue, ""), ref checks);
            Reject<InvalidOperationException>(() => new OperationalReportRequest(OperationalReportKind.Medicines, DateTime.Today, DateTime.Today, new string('س',121)), ref checks);
            var columns = new[] { new OperationalReportColumn("الصنف",typeof(string)), new OperationalReportColumn("السعر",typeof(decimal)) };
            var row = new object[] { "دواء 500 mg", null };
            var result = new OperationalReportResult(columns, new[] { row });
            row[0] = "changed"; columns[0] = new OperationalReportColumn("changed",typeof(string));
            Check(result.Columns[0].Name == "الصنف" && (string)result.Rows[0][0] == "دواء 500 mg" && result.Rows[0][1] == null, ref checks);
            Reject<ArgumentException>(() => new OperationalReportResult(result.Columns,new[]{new object[0]}), ref checks);
            Reject<ArgumentException>(() => new OperationalReportResult(new[]{result.Columns[0],result.Columns[0]},new object[0][]), ref checks);
            var repository = new Fake { Result = result };
            var service = new OperationalReportReadService(repository, () => true);
            Check(ReferenceEquals(service.Load(request),result) && ReferenceEquals(repository.Request,request), ref checks);
            Reject<ArgumentNullException>(() => service.Load(null), ref checks);
            repository.Fail = true;
            Reject<InvalidOperationException>(() => service.Load(request), ref checks);
            var deniedRepository = new Fake { Result = result };
            Reject<UnauthorizedAccessException>(() => new OperationalReportReadService(deniedRepository).Load(request), ref checks);
            Check(deniedRepository.Calls == 0, ref checks);
            Reject<UnauthorizedAccessException>(() => new OperationalReportReadService(deniedRepository, () => false).Load(request), ref checks);
            Check(deniedRepository.Calls == 0, ref checks);
            bool current = true;
            deniedRepository.OnLoad = () => current = false;
            Reject<UnauthorizedAccessException>(() => new OperationalReportReadService(deniedRepository, () => current).Load(request), ref checks);
            Check(deniedRepository.Calls == 1, ref checks);
            current = true; deniedRepository.OnLoad = null;
            Check(ReferenceEquals(new OperationalReportReadService(deniedRepository, () => current).Load(request), result), ref checks);
            int connections=0;
            var sql = new SqlOperationalReportRepository(() => { connections++; throw new InvalidOperationException(); });
            Reject<ArgumentNullException>(() => sql.Load(null), ref checks);
            Check(connections == 0, ref checks);
            Console.WriteLine("PASS: "+checks+" typed report request/result contracts; no database connection.");
            return checks;
        }
        private static void Check(bool value, ref int checks)
        { if(!value)throw new InvalidOperationException("Report contract failed.");checks++; }
        private static void Reject<T>(Action action, ref int checks) where T:Exception
        { try{action();}catch(T){checks++;return;}throw new InvalidOperationException("Expected report rejection: "+typeof(T).Name); }
        private sealed class Fake : IOperationalReportRepository
        {
            internal OperationalReportResult Result;
            internal OperationalReportRequest Request;
            internal bool Fail;
            internal int Calls; internal Action OnLoad;
            public OperationalReportResult Load(OperationalReportRequest request)
            { Calls++; Request=request; OnLoad?.Invoke();if(Fail)throw new InvalidOperationException("test");return Result; }
        }
    }
}
