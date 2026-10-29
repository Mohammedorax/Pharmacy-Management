using System.Threading;

namespace project_fo_3
{
    // يحمي واجهة الاعتماد من إرسالين متداخلين لنفس الطلب. لا يحل محل قيد
    // التكرار في SQL، لكنه يمنع إعادة النقر أثناء انتظار العملية المتزامنة.
    internal sealed class CommercialReturnSubmissionGate
    {
        private int isEntered;

        internal bool TryEnter()
        {
            return Interlocked.CompareExchange(ref isEntered, 1, 0) == 0;
        }

        internal void Exit()
        {
            Volatile.Write(ref isEntered, 0);
        }
    }
}
