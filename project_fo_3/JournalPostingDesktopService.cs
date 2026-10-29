using System;
using System.Collections.Generic;
using System.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class JournalPostingDesktopService
    {
        internal static System.Threading.Tasks.Task<int> PostAsync(JournalPostRequest request)
        {return PostAsync(request,System.Threading.CancellationToken.None);}
        internal static async System.Threading.Tasks.Task<int> PostAsync(JournalPostRequest request,System.Threading.CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var session=ApplicationServices.CreateJournalPostSession();
            var native=session.Service as IAsyncJournalPostService;
            if(native==null)throw new InvalidOperationException("لا تتوفر كتابة القيود الأصلية غير المتزامنة؛ لم يرسل الحفظ.");
            return (await native.PostAsync(request,session.Actor,token).ConfigureAwait(false)).Id;
        }
        internal static int Post(DateTime date, string sourceType, string sourceId, string description, IEnumerable<JournalLineInput> lines)
        {
            var session=ApplicationServices.CreateJournalPostSession();
            var request = new JournalPostRequest(date, sourceType, sourceId, description,
                lines?.Select(line => line == null ? null : new JournalLineValues(line.AccountCode, line.Debit, line.Credit, line.Description, line.CostCenterId)));
            return session.Service.Post(request,session.Actor).Id;
        }
    }
}
