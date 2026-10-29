namespace project_fo_3
{
    // أرقام المسودة تُحجز من مصدر واحد؛ شاشة المشتريات لا تتصل بالقاعدة مباشرة.
    internal static class PurchaseDraftSequenceService
    {
        internal static long GetNextPurchaseId()
        {
            return Database.GetNextSequenceValue("Seq_PurchaseId");
        }

        internal static long GetNextPurchaseDetailId()
        {
            return Database.GetNextSequenceValue("Seq_PurchaseDetailId");
        }
    }
}