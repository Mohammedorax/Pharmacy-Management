using System;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryIntegrityReadTests
    {
        internal static int Run()
        {
            int checks=0;
            var result=new OperationalReportResult(new[]{new OperationalReportColumn("رقم الحركة",typeof(int)),
                new OperationalReportColumn("الصنف",typeof(string))},new[]{new object[]{15,"باراسيتامول 500 mg"},new object[]{14,null}});
            var repository=new Fake { Result=result };
            var service=new InventoryIntegrityReadService(repository);
            Check(ReferenceEquals(result,service.Load()) && repository.Calls==1,ref checks);
            Check((int)result.Rows[0][0]==15 && (int)result.Rows[1][0]==14 && result.Rows[1][1]==null,ref checks);
            var failure=new InvalidOperationException("read failure"); repository.Failure=failure;
            bool propagated=false;
            try { service.Load(); } catch(InvalidOperationException ex) { propagated=ReferenceEquals(failure,ex); }
            Check(propagated && repository.Calls==2,ref checks);
            bool rejected=false; try { new InventoryIntegrityReadService(null); } catch(ArgumentNullException) { rejected=true; }
            Check(rejected,ref checks);
            rejected=false; try { new SqlInventoryIntegrityReadRepository(null); } catch(ArgumentNullException) { rejected=true; }
            Check(rejected,ref checks);
            bool opened=false;
            var sql=new SqlInventoryIntegrityReadRepository(()=>{opened=true;throw failure;});
            Check(sql!=null && !opened,ref checks);
            rejected=false; try { SqlInventoryIntegrityReadRepository.Load(null,null); } catch(ArgumentNullException) { rejected=true; }
            Check(rejected && !opened,ref checks);
            Console.WriteLine("PASS: "+checks+" inventory integrity read contracts; no database connection.");
            return checks;
        }
        private static void Check(bool condition,ref int checks)
        { if(!condition) throw new InvalidOperationException("Inventory integrity read contract changed.");checks++; }
        private sealed class Fake : IInventoryIntegrityReadRepository
        {
            internal OperationalReportResult Result;
            internal Exception Failure;
            internal int Calls;
            public OperationalReportResult Load() { Calls++; if(Failure!=null) throw Failure; return Result; }
        }
    }
}
