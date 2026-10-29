using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class AccountWritingDesktopService
    {
        internal static AccountWriteResult Save(AccountSaveRequest request)
        {var session=ApplicationServices.CreateAccountWriteSession();return session.Service.Save(request,session.Actor);}
        internal static AccountWriteResult Delete(string code)
        {var session=ApplicationServices.CreateAccountWriteSession();return session.Service.Delete(code,session.Actor);}
        internal static Task<AccountWriteResult> SaveAsync(AccountSaveRequest request)
        {var session=ApplicationServices.CreateAccountWriteSession();return Task.Run(()=>session.Service.Save(request,session.Actor));}
        internal static Task<AccountWriteResult> DeleteAsync(string code)
        {var session=ApplicationServices.CreateAccountWriteSession();return Task.Run(()=>session.Service.Delete(code,session.Actor));}
    }
}
