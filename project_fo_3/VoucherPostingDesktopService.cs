using System;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class VoucherPostingDesktopService
    {
        internal static System.Threading.Tasks.Task<int> PostAsync(VoucherPostRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var session=ApplicationServices.CreateVoucherPostSession();
            return System.Threading.Tasks.Task.Run(() => session.Service.Post(request,session.Actor).VoucherId);
        }
        internal static int Post(string type, DateTime date, string party, string account, decimal amount, string notes,
            string cash=AccountingService.CashAccount, int? center=null, string expectedSystemCurrencyCode=null)
        {
            var session=ApplicationServices.CreateVoucherPostSession();
            var request=new VoucherPostRequest(type,date,party,account,amount,notes,cash,center,expectedSystemCurrencyCode);
            return session.Service.Post(request,session.Actor).VoucherId;
        }
    }
}
