using System;

namespace PharmacyManagement.Application
{
    // Shared by report reads and Excel exports. Never silently broaden a query
    // by dropping the end of the user's search text.
    public static class ReportSearchCriteria
    {
        public const int MaximumLength = 120;
        public const string TooLongMessage = "نص البحث يتجاوز 120 حرفًا؛ اختصره دون تغيير الجزء المطلوب البحث عنه.";

        public static string Normalize(string value)
        {
            string search = (value ?? string.Empty).Trim();
            if (search.Length > MaximumLength) throw new InvalidOperationException(TooLongMessage);
            return search;
        }
    }
}
