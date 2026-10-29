using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class DailyOperation
    {
        internal string Kind, Reference, DisplayReference, Party, Employee, Status;
        internal DateTime Date;
        internal decimal Amount;
    }

    internal sealed class DailyOperationsReadSession
    {
        private readonly string connection = Database.ConnectionString;
        private readonly DailyOperationsReadService service;
        internal DailyOperationsReadSession()
        {
            service=ApplicationServices.CreateDailyOperationsReadService(connection,new OperationActor{UserName=user,UserRole=role},
                (actor,kind)=>actor.UserName==user&&actor.UserRole==role&&CanRead(kind),()=>IsCurrent);
        }
        private readonly long revision = SessionContext.CurrentSessionRevision;
        private readonly string user = SessionContext.CurrentUser, employee = SessionContext.CurrentEmployeeId, role = SessionContext.CurrentRole;
        internal bool IsCurrent => SessionContext.IsAuthenticated && revision == SessionContext.CurrentSessionRevision &&
            user == SessionContext.CurrentUser && employee == SessionContext.CurrentEmployeeId && role == SessionContext.CurrentRole && connection == Database.ConnectionString;
        internal bool CanRead(string kind) => IsCurrent && (kind == "Purchase" ? PermissionService.CanOpen(AppPermission.Purchases)
            : kind == "Voucher" ? PermissionService.CanOpen(AppPermission.Accounting) && (PermissionService.CanExecute(AppOperation.PostCashVoucher) || PermissionService.CanExecute(AppOperation.ViewAccountingReports))
            : (kind == "Sale" || kind == "CommercialSale") && PermissionService.CanOpen(AppPermission.Sales));
        internal static string KindText(string kind, bool english) => kind == "Purchase" ? (english ? "Purchase" : "شراء") : kind == "Voucher" ? (english ? "Voucher" : "سند") : (english ? "Sale" : "بيع");
        private void Require(string kind) { if (!CanRead(kind)) throw new UnauthorizedAccessException("تغيرت الجلسة أو صلاحية قراءة المستند."); }

        internal async Task<List<DailyOperation>> LoadAsync(DateTime day,CancellationToken token)
        {
            var loaded=await service.LoadAsync(day,token);
            return loaded.Select(item=>new DailyOperation{Kind=item.Kind,Reference=item.Reference,DisplayReference=item.DisplayReference,
                Date=item.Date,Party=item.Party,Amount=item.Amount,Employee=item.Employee,Status=item.Status}).ToList();
        }
        internal async Task<DataTable> ReadLinesAsync(DailyOperation item,bool english,CancellationToken token)
        {
            if(item==null)throw new ArgumentNullException(nameof(item));
            var document=await service.ReadLinesAsync(item.Kind,item.Reference,token);
            token.ThrowIfCancellationRequested();Require(item.Kind);
            var table=new DataTable();
            try
            {
                foreach(var column in document.Columns)table.Columns.Add(ColumnText(column.Name,english),column.ValueType==typeof(DateTime)?typeof(string):column.ValueType);
                foreach(var source in document.Rows)
                {
                    var values=source.Select(value=>value is DateTime date?(object)date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture):value).ToArray();
                    table.Rows.Add(values);
                }
                token.ThrowIfCancellationRequested();Require(item.Kind);return table;
            }
            catch{table.Dispose();throw;}
        }
        private static string ColumnText(string value,bool english)
        {
            var names=new Dictionary<string,string[]> { {"MedName",new[]{"الصنف","Item"}},{"BuyingPrice",new[]{"سعر الشراء","Buying price"}},
                {"Sprice",new[]{"سعر الوحدة","Unit price"}},{"UnitPrice",new[]{"سعر الوحدة","Unit price"}},{"Quantity",new[]{"الكمية","Quantity"}},
                {"TotalAmount",new[]{"إجمالي البند","Line total"}},{"LineTotal",new[]{"إجمالي البند","Line total"}},{"ProdDate",new[]{"الإنتاج","Production"}},
                {"ExpDate",new[]{"الانتهاء","Expiry"}},{"VoucherType",new[]{"نوع السند","Voucher type"}},{"AccountCode",new[]{"الحساب","Account"}},
                {"Amount",new[]{"المبلغ","Amount"}},{"Notes",new[]{"ملاحظات","Notes"}},{"JournalEntryId",new[]{"رقم القيد","Journal entry"}} };
            return names.TryGetValue(value,out var pair) ? pair[english?1:0] : value;
        }
    }

}
