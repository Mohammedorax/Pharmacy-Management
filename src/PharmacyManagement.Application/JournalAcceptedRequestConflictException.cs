using System;

namespace PharmacyManagement.Application
{
    // Known existing acceptance; this must never be presented as a newly saved edit.
    public sealed class JournalAcceptedRequestConflictException : InvalidOperationException
    {
        public JournalAcceptedRequestConflictException(int journalId, string sourceType, string sourceId)
            : base("معرّف القيد معتمد سابقًا ببيانات مختلفة؛ لم تعتمد التعديلات الحالية.")
        {
            Guid identity;
            if (journalId <= 0 || (sourceType != "Manual" && sourceType != "OpeningEntry") ||
                !Guid.TryParseExact(sourceId, "N", out identity) || identity == Guid.Empty)
                throw new ArgumentException("هوية نتيجة القيد المعتمد غير صالحة.");
            JournalId = journalId; SourceType = sourceType; SourceId = sourceId;
        }
        public int JournalId { get; }
        public string SourceType { get; }
        public string SourceId { get; }
    }
}
