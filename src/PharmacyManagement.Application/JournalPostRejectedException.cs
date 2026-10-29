using System;
namespace PharmacyManagement.Application
{
    public sealed class JournalPostRejectedException : InvalidOperationException
    {
        public string SourceType { get; }
        public string SourceId { get; }
        public JournalPostRejectedException(string sourceType, string sourceId, Exception cause)
            : base("رُفض ترحيل هذه المحاولة وأُكد التراجع؛ صحح المسودة وأعدها بالمعرّف نفسه.", cause)
        {
            Guid id;
            if ((sourceType != "Manual" && sourceType != "OpeningEntry") || !Guid.TryParseExact(sourceId, "N", out id) || id == Guid.Empty)
                throw new ArgumentException("هوية رفض القيد غير صالحة.");
            if (cause == null) throw new ArgumentNullException(nameof(cause));
            SourceType = sourceType; SourceId = sourceId;
        }
    }
}
