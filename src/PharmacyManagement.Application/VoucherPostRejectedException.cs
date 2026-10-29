using System;
namespace PharmacyManagement.Application
{
    public sealed class VoucherPostRejectedException : InvalidOperationException
    {
        public VoucherPostRejectedException(Guid requestId,Exception cause)
            :base("رُفضت محاولة السند قبل الاعتماد وأُكد التراجع؛ صحح المسودة بالمعرّف نفسه.",cause)
        {if(requestId==Guid.Empty)throw new ArgumentException("هوية رفض السند غير صالحة.");if(cause==null)throw new ArgumentNullException(nameof(cause));RequestId=requestId;}
        public Guid RequestId {get;}
    }
}