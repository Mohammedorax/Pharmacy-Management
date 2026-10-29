using System;
using System.Globalization;

namespace project_fo_3
{
    internal static class AntdDateInput
    {
        internal static void UseGregorianCalendarForProcess()
        {
            // Keep the locale, number/currency formats, UI culture and keyboard unchanged.
            // Only ar-SA's calendar changes; clone rather than mutate cached cultures.
            CultureInfo current = CultureInfo.CurrentCulture;
            CultureInfo applicationDefault = CultureInfo.DefaultThreadCurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = GregorianSaudiCulture(current);
            if (applicationDefault != null)
                CultureInfo.DefaultThreadCurrentCulture = GregorianSaudiCulture(applicationDefault);
            else if (string.Equals(current.Name, "ar-SA", StringComparison.OrdinalIgnoreCase))
                CultureInfo.DefaultThreadCurrentCulture = GregorianSaudiCulture(current);
        }

        private static CultureInfo GregorianSaudiCulture(CultureInfo culture)
        {
            if (!string.Equals(culture.Name, "ar-SA", StringComparison.OrdinalIgnoreCase) ||
                culture.DateTimeFormat.Calendar is GregorianCalendar) return culture;
            var copy = (CultureInfo)culture.Clone();
            copy.DateTimeFormat.Calendar = new GregorianCalendar();
            return CultureInfo.ReadOnly(copy);
        }

        private static readonly string[] AcceptedFormats = { "yyyy/MM/dd", "yyyy/M/d" };

        internal static bool TryRead(AntdUI.DatePicker picker, out DateTime date)
        {
            date = default(DateTime);
            if (picker == null) return false;

            DateTime parsed;
            if (!DateTime.TryParseExact((picker.Text ?? string.Empty).Trim(), AcceptedFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return false;

            parsed = parsed.Date;
            if ((picker.MinDate.HasValue && parsed < picker.MinDate.Value.Date) ||
                (picker.MaxDate.HasValue && parsed > picker.MaxDate.Value.Date)) return false;

            date = parsed;
            return true;
        }

        internal static void SetDate(AntdUI.DatePicker picker, DateTime date)
        {
            if (picker == null) throw new ArgumentNullException("picker");
            picker.Value = date.Date;
            picker.Text = date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }
    }
}
