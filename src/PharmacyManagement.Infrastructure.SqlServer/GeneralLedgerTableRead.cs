using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    // The presentation read of the general ledger. The rows are read straight into the table so
    // they are materialised once; the typed contract stays free of the table type.
    public sealed class GeneralLedgerTableRead
    {
        public GeneralLedgerTableRead(DataTable table, decimal previousBalance, decimal periodDebit,
            decimal periodCredit, decimal endingBalance)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            PreviousBalance = previousBalance; PeriodDebit = periodDebit; PeriodCredit = periodCredit;
            EndingBalance = endingBalance;
        }
        public DataTable Table { get; }
        public decimal PreviousBalance { get; }
        public decimal PeriodDebit { get; }
        public decimal PeriodCredit { get; }
        public decimal EndingBalance { get; }
    }
}
