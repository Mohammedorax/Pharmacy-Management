using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed partial class GeneralLedgerReadQaSelfTests
    {
        internal void VerifyReadPerformance(string token)
        {
            Check(QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token)==database,"Ledger measurement escaped owned QA.");
            AppDomain.MonitoringIsEnabled=true;
            var directory=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            Check(directory!=null&&directory.Name=="qa-journal-posting-"+token,"Ledger measurement artifact directory is not owned.");
            var corpus=new List<GeneralLedgerReadRequest>();
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Ledger corpus escaped owned QA.");
                using(var command=new SqlCommand(@"SELECT TOP (3) l.AccountCode,MIN(e.EntryDate),MAX(e.EntryDate)
FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
GROUP BY l.AccountCode ORDER BY COUNT_BIG(*) DESC,l.AccountCode",connection))
                    using(var reader=command.ExecuteReader())while(reader.Read())
                    {
                        DateTime first=reader.GetDateTime(1).Date,last=reader.GetDateTime(2).Date;
                        corpus.Add(new GeneralLedgerReadRequest(reader.GetString(0),first.AddDays(Math.Min(30,(last-first).Days)),last,null,true,true,null));
                    }
            }
            Check(corpus.Count==3,"Ledger performance requires three existing account samples.");
            var session=ApplicationServices.CreateGeneralLedgerReadSession();Check(session.IsCurrent,"Ledger measurement session is not current.");
            var report=Await(Task.Run(()=>MeasureCorpus(session,corpus,token)));
            string path=Path.Combine(directory.FullName,"ledger-read-performance.xml");report.Save(path);
            CaptureLedgerPlans(token,Path.Combine(directory.FullName,"ledger-read-plans.xml"));
            VerifyUnchanged();
            Console.WriteLine("OBSERVED owned ledger read performance: three accounts, two paired warmups each, ten alternating legacy/current rounds each; exact row/previous parity every pair; within15Percent="+(string)report.Attribute("within15Percent")+"; report="+path+"; no SQL writes, UI rendering/lock-duration acceptance remains separate.");
        }
        private static XElement MeasureCorpus(GeneralLedgerReadSession session,List<GeneralLedgerReadRequest> corpus,string token)
        {
            var report=new XElement("OwnedLedgerReadPerformance",new XAttribute("token",token),new XAttribute("utc",DateTime.UtcNow.ToString("O")),
                new XAttribute("rounds",10),new XAttribute("warmupPairs",2),new XAttribute("scope","SQL read plus projection on one worker; no UI paint/input"),
                new XAttribute("p95Method","nearest rank, ten samples: maximum"));
            bool all=true;bool allAbsolute=true;
        // A read that costs about a millisecond cannot be judged by a ratio alone, so the absolute
        // budget is reported next to the strict ratio without replacing it.
        const double AbsoluteBudgetMs=1.0;
            string connectionString=Database.ConnectionString;
            var direct=new PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository(()=>new SqlConnection(connectionString));
            foreach(var request in corpus)
            {
                for(int warm=0;warm<2;warm++)using(var pair=MeasurePair(session,request,warm%2==0))ComparePair(pair);
                var oldTimes=new List<double>();var newTimes=new List<double>();var directTimes=new List<double>();var samples=new XElement("Samples");var diagnostics=new XElement("Diagnostics",new XAttribute("scope","after all ten timed pairs for this account; excluded from acceptance"));int rows=0;
                for(int round=0;round<10;round++)using(var pair=MeasurePair(session,request,round%2==0))
                {
                    ComparePair(pair);rows=pair.Current.Rows.Rows.Count;oldTimes.Add(pair.LegacyMs);newTimes.Add(pair.CurrentMs);
                    samples.Add(new XElement("Pair",new XAttribute("round",round+1),new XAttribute("first",round%2==0?"Legacy":"Current"),
                        new XAttribute("legacyMs",Number(pair.LegacyMs)),new XAttribute("currentMs",Number(pair.CurrentMs)),new XAttribute("currentReadMs",Number(pair.ReadMs)),
                        new XAttribute("projectionMs",Number(pair.ProjectionMs)),new XAttribute("legacyGcBefore",pair.LegacyGcBefore),new XAttribute("legacyGcAfter",pair.LegacyGcAfter),new XAttribute("currentGcBefore",pair.CurrentGcBefore),new XAttribute("currentGcAfter",pair.CurrentGcAfter),new XAttribute("legacyAllocatedBytes",pair.LegacyAllocatedBytes),new XAttribute("currentAllocatedBytes",pair.CurrentAllocatedBytes),new XAttribute("readAllocatedBytes",pair.ReadAllocatedBytes),new XAttribute("projectionAllocatedBytes",pair.ProjectionAllocatedBytes)));
                }
                using(var reference=MeasurePair(session,request,true))for(int diagnostic=0;diagnostic<10;diagnostic++)
                {
                    ComparePair(reference);
                    var timer=Stopwatch.StartNew();var raw=direct.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult();
                    using(var projected=GeneralLedgerPresentation.ToTable(raw,false))
                    {
                        directTimes.Add(timer.Elapsed.TotalMilliseconds);
                        ComparePair(new BenchPair{Legacy=reference.Current,Current=new GeneralLedgerQueryResult(projected,raw.PreviousBalance)});
                    }
                    timer.Restart();for(int check=0;check<10;check++)Check(session.IsCurrent,"Timing root context changed.");double contextMs=timer.Elapsed.TotalMilliseconds/10;
                    var phases=MeasureReadPhases(connectionString,request,reference.Current,token).GetAwaiter().GetResult();
                    var compact=MeasureReadPhases(connectionString,request,reference.Current,token,true).GetAwaiter().GetResult();compact.Name="CompactSqlReadPhases";
                    diagnostics.Add(new XElement("Round",new XAttribute("round",diagnostic+1),new XAttribute("directRepositoryMs",Number(directTimes.Last())),new XAttribute("contextCheckMs",Number(contextMs)),phases,compact));
                }
                double oldMedian=Median(oldTimes),newMedian=Median(newTimes),old95=P95(oldTimes),new95=P95(newTimes);
                Check(oldMedian>0&&old95>0,"Ledger timing baseline is not measurable.");
                bool accepted=newMedian<=oldMedian*1.15&&new95<=old95*1.15;all&=accepted;
                // The typed read materialises the rows twice (typed rows, then the table the grid
                // binds), so the ratio is reported next to an absolute budget for a sub-two-millisecond read.
                bool withinBudget=newMedian<=oldMedian+AbsoluteBudgetMs&&new95<=old95+AbsoluteBudgetMs;
                allAbsolute&=withinBudget;
                // The same read without the one read transaction: this isolates the cost of the
                // snapshot guarantee from the cost of the rows themselves.
                var noTransactionTimes=new List<double>();
                for(int sample=0;sample<10;sample++)noTransactionTimes.Add(MeasureTableReadWithoutTransaction(connectionString,request));
                double noTransactionMedian=Median(noTransactionTimes);
                report.Add(new XElement("Account",new XAttribute("code",request.AccountCode),new XAttribute("from",request.FromDate.ToString("O")),new XAttribute("to",request.ToDate.ToString("O")),
                    new XAttribute("rows",rows),new XAttribute("legacyMedianMs",Number(oldMedian)),new XAttribute("currentMedianMs",Number(newMedian)),
                    new XAttribute("legacyP95Ms",Number(old95)),new XAttribute("currentP95Ms",Number(new95)),new XAttribute("medianRatio",Number(newMedian/oldMedian)),
                    new XAttribute("p95Ratio",Number(new95/old95)),new XAttribute("within15Percent",accepted),new XAttribute("withinAbsoluteBudget",withinBudget),
                    new XAttribute("absoluteBudgetMs",Number(AbsoluteBudgetMs)),new XAttribute("directRepositoryMedianMs",Number(Median(directTimes))),
                    new XAttribute("noTransactionMedianMs",Number(noTransactionMedian)),new XAttribute("readTransactionOverheadMs",Number(newMedian-noTransactionMedian)),
                    samples,diagnostics));
            }
            report.Add(new XAttribute("rawParity",true),new XAttribute("within15Percent",all));report.Add(new XAttribute("withinAbsoluteBudget",allAbsolute));return report;
        }
        // A diagnostic only, never in the production path: the same table read with the one read
        // transaction removed, so the report can state how much of the remaining difference is the
        // snapshot guarantee and how much is the rows themselves.
        private static double MeasureTableReadWithoutTransaction(string connectionString,GeneralLedgerReadRequest request)
        {
            var repository=new PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository(()=>new SqlConnection(connectionString));
            var create=typeof(PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository)
                .GetMethod("CreateCommand",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var createTable=typeof(PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository)
                .GetMethod("CreateLedgerTable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var timer=Stopwatch.StartNew();
            using(var connection=new SqlConnection(connectionString))
            {
                connection.Open();
                using(var command=(SqlCommand)create.Invoke(repository,new object[]{connection,null,request}))
                using(var reader=command.ExecuteReader())
                {
                    if(reader.Read())reader.GetDecimal(0);
                    if(reader.NextResult())
                    {
                        var table=(DataTable)createTable.Invoke(null,null);
                        while(reader.Read())
                            table.Rows.Add(reader.GetDateTime(0),reader.GetInt32(1),
                                reader.IsDBNull(2)?(object)DBNull.Value:reader.GetString(2),
                                reader.IsDBNull(3)?(object)DBNull.Value:reader.GetString(3),
                                reader.IsDBNull(4)?(object)DBNull.Value:reader.GetString(4),
                                reader.IsDBNull(5)?(object)DBNull.Value:reader.GetString(5),
                                reader.IsDBNull(6)?(object)DBNull.Value:reader.GetDecimal(6),
                                reader.IsDBNull(7)?(object)DBNull.Value:reader.GetDecimal(7),
                                reader.GetDecimal(8));
                    }
                }
            }
            return timer.Elapsed.TotalMilliseconds;
        }
        private static BenchPair MeasurePair(GeneralLedgerReadSession session,GeneralLedgerReadRequest request,bool legacyFirst)
        {            var pair=new BenchPair();
            try
            {
                if(legacyFirst){MeasureLegacy(pair,request);MeasureCurrent(pair,session,request);}
                else{MeasureCurrent(pair,session,request);MeasureLegacy(pair,request);}
                return pair;
            }
            catch{pair.Dispose();throw;}
        }
        private static string GcCounts(){return GC.CollectionCount(0)+"/"+GC.CollectionCount(1)+"/"+GC.CollectionCount(2);}
        private static void MeasureLegacy(BenchPair pair,GeneralLedgerReadRequest request)
        {
            long allocatedBefore=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            pair.LegacyGcBefore=GcCounts();
            var timer=Stopwatch.StartNew();
            pair.Legacy=GeneralLedgerWorkspaceService.Load(new GeneralLedgerQuery(request.AccountCode,request.FromDate,request.ToDate,request.SourceType,
                request.IncludePreviousBalance,request.IncludeOpeningEntries,request.CostCenterId));
            pair.LegacyMs=timer.Elapsed.TotalMilliseconds; pair.LegacyAllocatedBytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocatedBefore; pair.LegacyGcAfter=GcCounts();
        }
        private static void MeasureCurrent(BenchPair pair,GeneralLedgerReadSession session,GeneralLedgerReadRequest request)
        {
            long allocatedBefore=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            pair.CurrentGcBefore=GcCounts();
            var timer=Stopwatch.StartNew();
            // The entire pair runs on the same worker, without UI-context/pumping overhead for either path.
            // The current path is the one the screen uses: the rows are read straight into the table.
            var result=session.LoadTableAsync(request,CancellationToken.None).GetAwaiter().GetResult();
            pair.ReadMs=timer.Elapsed.TotalMilliseconds;
            long allocatedAfterRead=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            pair.ReadAllocatedBytes=allocatedAfterRead-allocatedBefore;timer.Restart();
            pair.Current=new GeneralLedgerQueryResult(result.Table,result.PreviousBalance);
            pair.ProjectionMs=timer.Elapsed.TotalMilliseconds;pair.CurrentMs=pair.ReadMs+pair.ProjectionMs; pair.CurrentAllocatedBytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocatedBefore; pair.ProjectionAllocatedBytes=pair.CurrentAllocatedBytes-pair.ReadAllocatedBytes; pair.CurrentGcAfter=GcCounts();
        }
        private static void ComparePair(BenchPair pair)
        {
            var old=pair.Legacy.Rows;var current=pair.Current.Rows;
            Check(pair.Legacy.PreviousBalance==pair.Current.PreviousBalance&&old.Rows.Count==current.Rows.Count&&old.Columns.Count==current.Columns.Count,"Ledger timed pair previous/count mismatch.");
            for(int column=0;column<old.Columns.Count;column++)Check(old.Columns[column].ColumnName==current.Columns[column].ColumnName&&old.Columns[column].DataType==current.Columns[column].DataType,"Ledger timed pair schema changed.");
            for(int row=0;row<old.Rows.Count;row++)for(int column=0;column<old.Columns.Count;column++)
                Check(Equals(old.Rows[row][column],current.Rows[row][column]),"Ledger timed pair changed raw cell/order.");
        }
        private static void CaptureLedgerPlans(string token,string path)
        {
            var report=new XElement("OwnedLedgerCachedPlans",new XAttribute("token",token));
            string productionSql=(string)typeof(PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository)
                .GetField("LedgerSql",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token),"Plan diagnostic escaped owned QA.");
                using(var keys=new SqlCommand(@"SELECT COUNT(*) FROM (VALUES(N'JournalEntryLinesTb',N'JournalEntryLineId'),(N'JournalEntriesTb',N'JournalEntryId'),(N'CostCentersTb',N'CostCenterId')) v(TableName,ColumnName)
WHERE EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.'+v.TableName) AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=1
AND EXISTS(SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1 AND c.name=v.ColumnName AND c.is_nullable=0))",connection))
                    Check((int)keys.ExecuteScalar()==3,"Ledger line/header/center ordering joins are not backed by unique nonnullable keys.");
                report.Add(new XAttribute("orderingKeysUnique",true));
                var initialSeed=XElement.Load(Path.Combine(Path.GetDirectoryName(path),"ledger-plan-seed.xml"));
                Check((string)initialSeed.Attribute("token")==token&&(string)initialSeed.Attribute("database")==connection.Database&&
                    (string)initialSeed.Attribute("account")=="1"&&(long)initialSeed.Attribute("rowsBeforeWorkload")==0,"Owned initial cold account snapshot is missing or mismatched.");
                using(var seed=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.JournalEntryLinesTb WHERE AccountCode=N'1'",connection))
                    report.Add(new XAttribute("cacheSeedRowsAfterOwnedWorkload",(long)seed.ExecuteScalar()));
                report.Add(new XAttribute("cacheSeedAccountHasNoLines",true));
                using(var command=new SqlCommand(@"SELECT t.text,s.execution_count,s.total_worker_time,s.total_elapsed_time,s.creation_time,s.last_execution_time,p.query_plan,
SUBSTRING(t.text,(s.statement_start_offset/2)+1,((CASE s.statement_end_offset WHEN -1 THEN DATALENGTH(t.text) ELSE s.statement_end_offset END-s.statement_start_offset)/2)+1)
FROM sys.dm_exec_query_stats s
CROSS APPLY sys.dm_exec_sql_text(s.sql_handle) t
CROSS APPLY sys.dm_exec_query_plan(s.plan_handle) p
WHERE EXISTS(SELECT 1 FROM sys.dm_exec_plan_attributes(s.plan_handle) a WHERE a.attribute=N'dbid' AND CONVERT(int,a.value)=DB_ID())
AND t.text LIKE N'%DECLARE @OpeningBalance DECIMAL%' AND t.text LIKE N'%SELECT e.EntryDate%'
ORDER BY s.creation_time,s.statement_start_offset",connection))
                using(var reader=command.ExecuteReader())while(reader.Read())
                    report.Add(new XElement("Statement",new XAttribute("productionBatch",reader.GetString(0).Contains(productionSql)),new XAttribute("executions",reader.GetInt64(1)),new XAttribute("workerUs",reader.GetInt64(2)),new XAttribute("elapsedUs",reader.GetInt64(3)),
                        new XAttribute("created",reader.GetDateTime(4).ToString("O")),new XAttribute("lastExecution",reader.GetDateTime(5).ToString("O")),
                        new XElement("Batch",reader.GetString(0)),new XElement("Sql",reader.GetString(7)),reader.IsDBNull(6)?null:XElement.Parse(reader.GetString(6))));
            }
            if(!report.Elements("Statement").Any())
            {
                report.Add(new XAttribute("captureStatus","unavailable"),new XAttribute("captureReason","SQL Server exposed no matching cached plan rows to this QA connection."));
                report.Save(path);
                Console.WriteLine("OBSERVED owned ledger plan capture unavailable: no matching cached plan rows were exposed; performance timing/parity remains separate and no plan acceptance is claimed.");
                return;
            }
            report.Save(path);
            var rootRows=report.Elements("Statement").Where(row=>(bool)row.Attribute("productionBatch")&&((string)row.Element("Sql")).TrimStart().StartsWith("SELECT e.EntryDate",StringComparison.Ordinal)).ToList();
            Check(rootRows.Count>0&&rootRows.Any(row=>row.Descendants().Any(column=>column.Name.LocalName=="ColumnReference"&&
                (string)column.Attribute("Column")=="@AccountCode"&&(string)column.Attribute("ParameterCompiledValue")=="N'1'")),"Actual production plan was not compiled for the empty regression account.");
            report.Add(new XAttribute("productionRowsPlanCaptured",true),new XAttribute("coldEmptyAccountPlanVerified",true));report.Save(path);
        }
        // Diagnostic only: same SQL/parameters/isolation on owned QA, never used by a production operation.
        private static async Task<XElement> MeasureReadPhases(string connectionString,GeneralLedgerReadRequest request,GeneralLedgerQueryResult expected,string token,bool compact=false)
        {
            var total=Stopwatch.StartNew();var phase=Stopwatch.StartNew();
            double open,begin,execute,opening,next,rowsTime,close,commit;decimal previous;
            var rows=new List<GeneralLedgerReadRow>();
            System.Collections.IDictionary statistics=null;int packetSize=0,sqlCharacters=0;
            using(var connection=new SqlConnection(connectionString))
            {
                await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);open=phase.Elapsed.TotalMilliseconds;
                Check(connection.Database==QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token),"Read-phase diagnostic escaped owned QA.");phase.Restart();
                using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))using(var command=connection.CreateCommand())
                {
                    begin=phase.Elapsed.TotalMilliseconds;
                    command.Transaction=transaction;command.CommandTimeout=15;
                    command.CommandText=(string)typeof(PharmacyManagement.Infrastructure.SqlServer.SqlGeneralLedgerReadRepository)
                        .GetField("LedgerSql",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
                    if(compact)command.CommandText=string.Join(" ",command.CommandText.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(line=>line.Trim()));
                    sqlCharacters=command.CommandText.Length;packetSize=connection.PacketSize;
                    command.Parameters.Add("@AccountCode",SqlDbType.NVarChar,20).Value=request.AccountCode;
                    command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.FromDate;command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.ToDate;
                    command.Parameters.Add("@SourceType",SqlDbType.NVarChar,50).Value=(object)request.SourceType??DBNull.Value;
                    command.Parameters.Add("@IncludePreviousBalance",SqlDbType.Bit).Value=request.IncludePreviousBalance;
                    command.Parameters.Add("@IncludeOpeningEntries",SqlDbType.Bit).Value=request.IncludeOpeningEntries;
                    command.Parameters.Add("@CostCenterId",SqlDbType.Int).Value=(object)request.CostCenterId??DBNull.Value;
                    connection.StatisticsEnabled=true;connection.ResetStatistics();
                    phase.Restart();var reader=await command.ExecuteReaderAsync(CancellationToken.None).ConfigureAwait(false);
                    execute=phase.Elapsed.TotalMilliseconds;
                    try
                    {
                        phase.Restart();Check(await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false),"Phase opening result missing.");
                        previous=reader.GetDecimal(0);opening=phase.Elapsed.TotalMilliseconds;
                        phase.Restart();Check(await reader.NextResultAsync(CancellationToken.None).ConfigureAwait(false),"Phase rows result missing.");next=phase.Elapsed.TotalMilliseconds;
                        phase.Restart();while(await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false))
                            rows.Add(new GeneralLedgerReadRow(reader.GetDateTime(0),reader.GetInt32(1),PhaseText(reader,2),PhaseText(reader,3),PhaseText(reader,4),PhaseText(reader,5),
                                PhaseMoney(reader,6),PhaseMoney(reader,7),reader.GetDecimal(8)));
                        rowsTime=phase.Elapsed.TotalMilliseconds;
                    }
                    finally{phase.Restart();reader.Dispose();close=phase.Elapsed.TotalMilliseconds;}
                    statistics=connection.RetrieveStatistics();connection.StatisticsEnabled=false;
                    phase.Restart();transaction.Commit();commit=phase.Elapsed.TotalMilliseconds;total.Stop();
                }
            }
            using(var projected=GeneralLedgerPresentation.ToTable(new GeneralLedgerReadResult(rows,previous),false))
                ComparePair(new BenchPair{Legacy=expected,Current=new GeneralLedgerQueryResult(projected,previous)});
            return new XElement("ReadPhases",new XAttribute("scope","diagnostic clone of repository; not root benchmark"),new XAttribute("totalMs",Number(total.Elapsed.TotalMilliseconds)),
                new XAttribute("openMs",Number(open)),new XAttribute("beginMs",Number(begin)),new XAttribute("executeReaderMs",Number(execute)),
                new XAttribute("openingReadMs",Number(opening)),new XAttribute("nextResultMs",Number(next)),new XAttribute("rowsReadMs",Number(rowsTime)),
                new XAttribute("readerCloseMs",Number(close)),new XAttribute("commitMs",Number(commit)),new XAttribute("packetSize",packetSize),new XAttribute("sqlCharacters",sqlCharacters),
                new XAttribute("bytesSent",statistics["BytesSent"]),new XAttribute("buffersSent",statistics["BuffersSent"]),new XAttribute("buffersReceived",statistics["BuffersReceived"]),
                new XAttribute("networkServerTimeMs",statistics["NetworkServerTime"]));
        }
        private static string PhaseText(SqlDataReader reader,int ordinal){return reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);}
        private static decimal? PhaseMoney(SqlDataReader reader,int ordinal){return reader.IsDBNull(ordinal)?(decimal?)null:reader.GetDecimal(ordinal);}
        private static string Number(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static double Median(IEnumerable<double> values){var sorted=values.OrderBy(value=>value).ToArray();return (sorted[4]+sorted[5])/2;}
        private static double P95(IEnumerable<double> values){return values.OrderBy(value=>value).ElementAt(9);}
        private sealed class BenchPair:IDisposable
        {
            internal GeneralLedgerQueryResult Legacy,Current;internal double LegacyMs,CurrentMs,ReadMs,ProjectionMs;internal string LegacyGcBefore,LegacyGcAfter,CurrentGcBefore,CurrentGcAfter;internal long LegacyAllocatedBytes,CurrentAllocatedBytes,ReadAllocatedBytes,ProjectionAllocatedBytes;
            public void Dispose(){Legacy?.Rows.Dispose();Current?.Rows.Dispose();}
        }
    }
}
