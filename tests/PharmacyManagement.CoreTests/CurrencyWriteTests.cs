using System;
using System.Linq;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class CurrencyWriteTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new Fake();
            bool allowed = false;
            var service = new CurrencyWriteService(repository, actor => allowed && actor.UserName == "admin" && actor.UserRole == "Manager");
            var manager = new OperationActor { UserName = "admin", UserRole = "Manager" };
            Reject<UnauthorizedAccessException>(() => service.Save(null, manager), ref checks);
            Reject<UnauthorizedAccessException>(() => service.Delete(-1, manager), ref checks);
            Reject<UnauthorizedAccessException>(() => service.SetSystemCurrency(0, manager), ref checks);
            allowed = true;
            Reject<UnauthorizedAccessException>(() => service.Save(Valid(), null), ref checks);
            Reject<UnauthorizedAccessException>(() => service.Delete(1, new OperationActor { UserName = "admin", UserRole = "Cashier" }), ref checks);
            Reject<UnauthorizedAccessException>(() => service.SetSystemCurrency(1, new OperationActor { UserName = "other", UserRole = "Manager" }), ref checks);
            Check(repository.Calls == 0, ref checks);
            Reject<ArgumentNullException>(() => service.Save(null, manager), ref checks);
            Reject<InvalidOperationException>(() => service.Delete(0, manager), ref checks);
            Reject<InvalidOperationException>(() => service.SetSystemCurrency(-1, manager), ref checks);
            foreach (Action<CurrencyRecord> invalid in new Action<CurrencyRecord>[] {
                c => c.CurrencyId = -1, c => c.CountryNameAr = " ", c => c.CurrencyNameAr = null,
                c => c.CurrencyCode = "", c => c.CountryNameAr = new string('x', 101),
                c => c.CurrencyNameAr = new string('x', 101), c => c.CurrencyCode = new string('x', 11),
                c => c.CurrencySymbol = new string('x', 21) })
            {
                var input = Valid(); invalid(input);
                Reject<InvalidOperationException>(() => service.Save(input, manager), ref checks);
            }
            Check(repository.Calls == 0, ref checks);
            var source = Valid(); source.CurrencyCode = " yer "; source.CurrencySymbol = " ر.ي ";
            service.Save(source, manager);
            Check(repository.Last.CurrencyCode == "YER" && repository.Last.CurrencySymbol == "ر.ي" && repository.Last.CountryNameAr == "اليمن", ref checks);
            Check(source.CurrencyCode == " yer " && source.CountryNameAr == " اليمن " && !ReferenceEquals(source, repository.Last), ref checks);
            Check(!ReferenceEquals(manager, repository.Actor) && repository.Actor.UserName == "admin", ref checks);
            var boundary = Valid(); boundary.CountryNameAr = new string('x',100); boundary.CurrencyNameAr = new string('x',100);
            boundary.CurrencyCode = new string('x',10); boundary.CurrencySymbol = new string('x',20);
            service.Save(boundary, manager); Check(repository.Calls == 2, ref checks);
            service.Delete(7, manager); Check(repository.Id == 7 && repository.Operation == "Delete", ref checks);
            service.SetSystemCurrency(8, manager); Check(repository.Id == 8 && repository.Operation == "SetSystem", ref checks);
            repository.Failure = new InvalidOperationException("transaction failed");
            bool sameFailure = false;
            try { service.Save(Valid(), manager); } catch (InvalidOperationException ex) { sameFailure = ReferenceEquals(ex, repository.Failure); }
            Check(sameFailure, ref checks);
            allowed = false; int calls = repository.Calls;
            Reject<UnauthorizedAccessException>(() => service.Save(Valid(), manager), ref checks);
            Check(repository.Calls == calls, ref checks);
            int connections = 0;
            new SqlCurrencyWriteRepository(() => { connections++; throw new InvalidOperationException(); });
            Check(connections == 0, ref checks);
            Console.WriteLine("PASS: " + checks + " currency write authorization/validation contracts; no database connection.");
            return checks;
        }
        private static CurrencyRecord Valid() => new CurrencyRecord { CountryNameAr=" اليمن ",CurrencyNameAr="ريال يمني",CurrencyCode="YER" };

        internal static void VerifyQaCycle(string server, string database)
        {
            if (database != "PharmacyManagement_qa") throw new InvalidOperationException("Only PharmacyManagement_qa is allowed.");
            var builder = new SqlConnectionStringBuilder { DataSource=server, InitialCatalog=database,
                IntegratedSecurity=true, ConnectTimeout=5, ApplicationName="Pharmacy.CoreTests.Currency.Cycle" };
            Func<SqlConnection> connections = () => new SqlConnection(builder.ConnectionString);
            using(var check=connections()) { check.Open(); using(var command=new SqlCommand("SELECT DB_NAME()",check))
                if(Convert.ToString(command.ExecuteScalar())!=database)throw new InvalidOperationException("Connected database mismatch."); }
            var reads = new SqlCurrencyReadRepository(connections);
            var writes = new SqlCurrencyWriteRepository(connections);
            var service = new CurrencyWriteService(writes, candidate => candidate.UserName=="QA-Currency-Cycle" && candidate.UserRole=="Manager");
            var actor = new OperationActor { UserName="QA-Currency-Cycle", UserRole="Manager" };
            var before = reads.GetAll();
            if (before.Any(c => c.CurrencyId == int.MaxValue)) throw new InvalidOperationException("QA sentinel id is already in use.");
            CurrencyRecord originalSystem = before.Single(c => c.IsSystemCurrency);
            string code="Q"+Guid.NewGuid().ToString("N").Substring(0,9).ToUpperInvariant();
            var input=new CurrencyRecord {CountryNameAr="QA اختبار",CurrencyNameAr="QA عملة مؤقتة",CurrencyCode=code,CurrencySymbol="QA"};
            int created=0;
            long auditBefore=AuditCount(connections);
            try
            {
                service.Save(input, actor);
                created=reads.GetAll().Single(c=>c.CurrencyCode==code).CurrencyId;
                input.CurrencyId=created;input.CurrencySymbol="QA2";service.Save(input, actor);
                if(reads.GetAll().Single(c=>c.CurrencyId==created).CurrencySymbol!="QA2")throw new Exception("QA currency update failed.");
                service.SetSystemCurrency(created,actor);
                if(reads.GetSystemCurrency().CurrencyId!=created)throw new Exception("QA currency selection failed.");
                int checks=0;
                Reject<InvalidOperationException>(()=>service.Delete(created,actor),ref checks);
                Reject<InvalidOperationException>(()=>service.SetSystemCurrency(int.MaxValue,actor),ref checks);
                if(reads.GetSystemCurrency().CurrencyId!=created)throw new Exception("Rejected currency mutation changed system selection.");
                long beforeFailure=AuditCount(connections);
                input.CurrencySymbol="FAIL";input.IsSystemCurrency=true;
                // Fail after the update, during audit insertion, to exercise rollback of the complete transaction.
                Reject<SqlException>(()=>writes.Save(input,new OperationActor{UserName=null,UserRole="Manager"}),ref checks);
                if(reads.GetAll().Single(c=>c.CurrencyId==created).CurrencySymbol!="QA2" ||
                    reads.GetSystemCurrency().CurrencyId!=created || AuditCount(connections)!=beforeFailure)
                    throw new Exception("Failed currency audit did not roll back settings and audit together.");
                service.SetSystemCurrency(originalSystem.CurrencyId,actor);
                service.Delete(created,actor);created=0;
                var after=reads.GetAll();
                if(before.Count!=after.Count || before.Any(a=>!after.Any(b=>Same(a,b))))throw new Exception("Currency state was not restored.");
                long audits=AuditCount(connections)-auditBefore;
                if(audits!=5)throw new Exception("Unexpected QA currency audit count: "+audits);
                Console.WriteLine("PASS: QA currency create/update/select/delete, missing-id/system-delete rejection, audit-failure rollback and full row restoration; RetainedAuditRows="+audits+"; CurrencyRowsAdded=0; IdentityMayAdvance=True.");
            }
            finally
            {
                if(created!=0)
                {
                    if(reads.GetSystemCurrency().CurrencyId!=originalSystem.CurrencyId)service.SetSystemCurrency(originalSystem.CurrencyId,actor);
                    if(reads.GetAll().Any(c=>c.CurrencyId==created))service.Delete(created,actor);
                }
            }
        }
        private static bool Same(CurrencyRecord a,CurrencyRecord b) => a.CurrencyId==b.CurrencyId && a.CountryNameAr==b.CountryNameAr &&
            a.CurrencyNameAr==b.CurrencyNameAr && a.CurrencyCode==b.CurrencyCode && a.CurrencySymbol==b.CurrencySymbol && a.IsSystemCurrency==b.IsSystemCurrency;
        private static long AuditCount(Func<SqlConnection> connections)
        {using(var connection=connections())using(var command=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb WHERE UserName=N'QA-Currency-Cycle' AND EntityName=N'CurrencySettings'",connection)){connection.Open();return Convert.ToInt64(command.ExecuteScalar());}}
        private static void Check(bool condition, ref int count) { count++; if(!condition) throw new InvalidOperationException("Currency write check failed: "+count); }
        private static void Reject<T>(Action action, ref int count) where T:Exception
        { count++; try {action();}catch(T){return;}throw new InvalidOperationException("Currency write did not reject: "+count); }
        private sealed class Fake : ICurrencyWriteRepository
        {
            internal int Calls, Id;
            internal CurrencyRecord Last;
            internal OperationActor Actor;
            internal string Operation;
            internal Exception Failure;
            public void Save(CurrencyRecord currency, OperationActor actor) { Calls++; if(Failure!=null)throw Failure;Last=currency;Actor=actor;Operation="Save"; }
            public void Delete(int id, OperationActor actor) { Calls++;Id=id;Actor=actor;Operation="Delete"; }
            public void SetSystemCurrency(int id, OperationActor actor) { Calls++;Id=id;Actor=actor;Operation="SetSystem"; }
        }
    }
}
