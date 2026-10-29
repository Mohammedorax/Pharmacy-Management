using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace PharmacyManagement.CoreTests
{
    internal static class JournalDocumentReadTests
    {
        internal static int Run()
        {
            int count=0;Action<bool> check=value=>{if(!value)throw new InvalidOperationException("Journal document contract failed.");count++;};
            Action<Type,Action> reject=(type,action)=>{try{action();}catch(Exception error){if(type.IsInstanceOfType(error)){count++;return;}throw;}throw new InvalidOperationException("Expected "+type.Name);};
            reject(typeof(ArgumentOutOfRangeException),()=>new JournalDocumentReadRequest(0));
            reject(typeof(ArgumentOutOfRangeException),()=>new JournalDocumentReadRequest(-1));
            var repo=new Fake();bool allowed=false;var actor=new OperationActor {UserName="reader",UserRole="Manager"};
            var service=new JournalDocumentReadService(repo,a=>allowed&&a.UserName=="reader"&&a.UserRole=="Manager");
            var request=new JournalDocumentReadRequest(42);
            Func<JournalDocumentSnapshot> read=()=>service.LoadAsync(request,actor).GetAwaiter().GetResult();
            reject(typeof(UnauthorizedAccessException),()=>read());check(repo.Calls==0);allowed=true;
            reject(typeof(UnauthorizedAccessException),()=>service.LoadAsync(request,null).GetAwaiter().GetResult());
            reject(typeof(ArgumentNullException),()=>service.LoadAsync(null,actor).GetAwaiter().GetResult());check(repo.Calls==0);
            var lines=new List<JournalDocumentLine>{Line(1,1250.50m,0m),Line(2,0m,1250.50m)};
            var snapshot=new JournalDocumentSnapshot(42,new DateTime(2026,9,24,13,14,15),"Manual",null,"  قيد Pharmacy 500 mg  ","reader",lines);
            lines.Clear();check(snapshot.Lines.Count==2);repo.Value=snapshot;
            check(ReferenceEquals(read(),snapshot));check(snapshot.Description=="  قيد Pharmacy 500 mg  "&&snapshot.Date.Hour==13&&snapshot.SourceId==null);
            check(typeof(JournalDocumentSnapshot).GetProperties().All(p=>!p.CanWrite)&&typeof(JournalDocumentLine).GetProperties().All(p=>!p.CanWrite));
            repo.After=()=>{actor.UserName="other";actor.UserRole="Cashier";};check(ReferenceEquals(read(),snapshot));
            actor.UserName="reader";actor.UserRole="Manager";repo.After=()=>allowed=false;
            reject(typeof(UnauthorizedAccessException),()=>read());allowed=true;repo.After=null;
            repo.Value=null;reject(typeof(InvalidOperationException),()=>read());
            repo.Value=new JournalDocumentSnapshot(7,DateTime.Today,null,null,null,null,snapshot.Lines);reject(typeof(InvalidOperationException),()=>read());
            foreach(var invalid in new[]{new JournalDocumentLine[0],new[]{Line(1,1m,0m)},new[]{Line(1,1m,0m),Line(1,0m,1m)},
                new[]{Line(0,1m,0m),Line(2,0m,1m)},new[]{Line(1,-1m,0m),Line(2,0m,-1m)},
                new[]{Line(1,1.005m,0m),Line(2,0m,1.005m)},new[]{Line(1,1m,1m)},new[]{Line(1,0m,0m)},new JournalDocumentLine[]{null}})
            {repo.Value=new JournalDocumentSnapshot(42,DateTime.Today,null,null,null,null,invalid);reject(typeof(InvalidOperationException),()=>read());}
            repo.Value=snapshot;repo.Error=new InvalidOperationException("Read failure");bool exact=false;
            try{read();}catch(Exception error){exact=ReferenceEquals(error,repo.Error);}check(exact);
            repo.Error=null;repo.After=null;
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {cancellation.Cancel();int before=repo.Calls;reject(typeof(OperationCanceledException),()=>service.LoadAsync(request,actor,cancellation.Token).GetAwaiter().GetResult());check(repo.Calls==before);}
            var pending=new System.Threading.Tasks.TaskCompletionSource<JournalDocumentSnapshot>();repo.Async=token=>pending.Task;
            var delayed=service.LoadAsync(request,actor,System.Threading.CancellationToken.None);allowed=false;pending.SetResult(snapshot);
            reject(typeof(UnauthorizedAccessException),()=>delayed.GetAwaiter().GetResult());allowed=true;
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                pending=new System.Threading.Tasks.TaskCompletionSource<JournalDocumentSnapshot>();repo.Async=token=>pending.Task;
                delayed=service.LoadAsync(request,actor,cancellation.Token);check(repo.LastToken==cancellation.Token);cancellation.Cancel();pending.SetResult(snapshot);
                reject(typeof(OperationCanceledException),()=>delayed.GetAwaiter().GetResult());
            }
            pending=new System.Threading.Tasks.TaskCompletionSource<JournalDocumentSnapshot>();repo.Async=token=>pending.Task;
            delayed=service.LoadAsync(request,actor,System.Threading.CancellationToken.None);actor.UserName="changed";actor.UserRole="Cashier";pending.SetResult(snapshot);check(ReferenceEquals(delayed.GetAwaiter().GetResult(),snapshot));
            actor.UserName="reader";actor.UserRole="Manager";repo.Async=token=>System.Threading.Tasks.Task.FromException<JournalDocumentSnapshot>(new TimeoutException());
            reject(typeof(TimeoutException),()=>service.LoadAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());
            Console.WriteLine("PASS: "+count+" saved journal read, authorization, identity, raw values and integrity contracts; no database connection.");return count;
        }
        private static JournalDocumentLine Line(int id,decimal debit,decimal credit)=>new JournalDocumentLine(id,"1000","الصندوق",debit,credit,"  بيان مختلط 500 mg  ",null,null);
        private sealed class Fake:IJournalDocumentReadRepository
        {
            internal int Calls;internal JournalDocumentSnapshot Value;internal Action After;internal Exception Error;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<JournalDocumentSnapshot>> Async;
            internal System.Threading.CancellationToken LastToken;
            public System.Threading.Tasks.Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,System.Threading.CancellationToken cancellationToken)
            {LastToken=cancellationToken;if(Async!=null){Calls++;return Async(cancellationToken);}return System.Threading.Tasks.Task.FromResult(Load(request));}
            public JournalDocumentSnapshot Load(JournalDocumentReadRequest request){Calls++;if(Error!=null)throw Error;After?.Invoke();return Value;}
        }
        private static string Text(DataRow row,string field)=>row.IsNull(field)?null:Convert.ToString(row[field]);
        internal static void VerifyQaReadOnly(string server,string database)
        {
            if(string.IsNullOrWhiteSpace(database)||!database.EndsWith("_qa",StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Explicit QA target required before connecting.");
            var builder=new SqlConnectionStringBuilder {DataSource=server,InitialCatalog=database,IntegratedSecurity=true,Pooling=false,ConnectTimeout=10,ApplicationName="Pharmacy.CoreTests.JournalDocument.ReadOnly"};
            var data=new DataSet();
            using(var connection=new SqlConnection(builder.ConnectionString))
            using(var command=connection.CreateCommand())
            {
                connection.Open();command.CommandText="SELECT DB_NAME()";
                if(!string.Equals(Convert.ToString(command.ExecuteScalar()),database,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("QA identity mismatch.");
                command.CommandText="SELECT * FROM dbo.JournalEntriesTb; SELECT * FROM dbo.JournalEntryLinesTb; SELECT AccountCode,AccountName FROM dbo.AccountsTb; SELECT CostCenterId,CostCenterName FROM dbo.CostCentersTb;";
                using(var adapter=new SqlDataAdapter(command))adapter.Fill(data);
            }
            var accounts=data.Tables[2].Rows.Cast<DataRow>().ToDictionary(r=>Text(r,"AccountCode"),r=>Text(r,"AccountName"),StringComparer.OrdinalIgnoreCase);
            var centers=data.Tables[3].Rows.Cast<DataRow>().ToDictionary(r=>(int)r["CostCenterId"],r=>Text(r,"CostCenterName"));
            var service=new JournalDocumentReadService(new SqlJournalDocumentReadRepository(()=>new SqlConnection(builder.ConnectionString)),a=>a.UserName=="qa-reader"&&a.UserRole=="Manager");
            var actor=new OperationActor {UserName="qa-reader",UserRole="Manager"};int compared=0;
            foreach(DataRow header in data.Tables[0].Rows)
            {
                int id=(int)header["JournalEntryId"];var actual=service.LoadAsync(new JournalDocumentReadRequest(id),actor).GetAwaiter().GetResult();
                if(actual.Id!=id||actual.Date!=(DateTime)header["EntryDate"]||actual.SourceType!=Text(header,"SourceType")||actual.SourceId!=Text(header,"SourceId")||actual.Description!=Text(header,"Description")||actual.CreatedBy!=Text(header,"CreatedBy"))throw new InvalidOperationException("Saved journal header differs.");
                var rows=data.Tables[1].Rows.Cast<DataRow>().Where(r=>(int)r["JournalEntryId"]==id).OrderBy(r=>(int)r["JournalEntryLineId"]).ToArray();
                if(rows.Length!=actual.Lines.Count)throw new InvalidOperationException("Saved journal line count differs.");
                for(int i=0;i<rows.Length;i++)
                {
                    var row=rows[i];var line=actual.Lines[i];string code=Text(row,"AccountCode"),name;accounts.TryGetValue(code,out name);
                    int? center=row.IsNull("CostCenterId")?(int?)null:(int)row["CostCenterId"];string centerName=null;if(center.HasValue)centers.TryGetValue(center.Value,out centerName);
                    if(line.Id!=(int)row["JournalEntryLineId"]||line.AccountCode!=code||line.AccountName!=name||line.Debit!=(decimal)row["Debit"]||line.Credit!=(decimal)row["Credit"]||line.Description!=Text(row,"LineDescription")||line.CostCenterId!=center||line.CostCenterName!=centerName)throw new InvalidOperationException("Saved journal line values/order differ.");
                    compared++;
                }
            }
            Console.WriteLine("PASS: QA saved journal snapshots; Headers="+data.Tables[0].Rows.Count+"; Lines="+compared+"; PersistentWrites=0. Names are current lookup names, not historical name snapshots.");
        }
    }
}
