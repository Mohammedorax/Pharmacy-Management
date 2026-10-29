using System;
using System.Collections.Generic;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Application
{
    // Immutable, validated identity and payload for a manual/opening draft.
    // Persist this request before dispatch; never create another identity for a retry.
    public sealed class JournalPendingRequest
    {
        private JournalPendingRequest(JournalPostRequest request) { Request = request; }
        public JournalPostRequest Request { get; }
        public static JournalPendingRequest Create(DateTime date, string sourceType, string description, IEnumerable<JournalLineValues> lines)
            => Restore(new JournalPostRequest(date, sourceType, Guid.NewGuid().ToString("N"), description, lines));
        public static JournalPendingRequest Create(DateTime date, string sourceType, string description, IEnumerable<JournalLineValues> lines, string expectedSystemCurrencyCode)
            => Restore(new JournalPostRequest(date, sourceType, Guid.NewGuid().ToString("N"), description, lines, expectedSystemCurrencyCode));

        public static JournalPendingRequest Restore(JournalPostRequest request)
        {
            var prepared = JournalPostService.PrepareRequest(request);
            Guid identity;
            if ((prepared.SourceType != "Manual" && prepared.SourceType != "OpeningEntry") ||
                !Guid.TryParseExact(prepared.SourceId, "N", out identity) || identity == Guid.Empty)
                throw new InvalidOperationException("طلب استعادة القيد يجب أن يحمل نوعًا يدويًا أو افتتاحيًا ومعرّفًا ثابتًا صالحًا.");
            return new JournalPendingRequest(prepared);
        }

        public bool HasSameContent(JournalPostRequest request)
        {
            var other = JournalPostService.PrepareRequest(request);
            if (Request.Date != other.Date || Request.SourceType != other.SourceType || Request.SourceId != other.SourceId ||
                Request.Description != other.Description || Request.ExpectedSystemCurrencyCode != other.ExpectedSystemCurrencyCode ||
                Request.Lines.Count != other.Lines.Count) return false;
            for (int i = 0; i < Request.Lines.Count; i++)
            {
                var a = Request.Lines[i]; var b = other.Lines[i];
                if (a.AccountCode != b.AccountCode || a.Debit != b.Debit || a.Credit != b.Credit ||
                    a.Description != b.Description || a.CostCenterId != b.CostCenterId) return false;
            }
            return true;
        }
    }
}
