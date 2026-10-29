namespace project_fo_3
{
    // يحفظ ملف الصيدلية ثم يعيده للتحقق؛ واجهة الإعدادات لا تتعامل مع Database مباشرة.
    internal static class PharmacyProfileService
    {
        internal static PharmacyProfile Load()
        {
            return Database.GetPharmacyProfile();
        }

        internal static void Save(PharmacyProfile profile)
        {
            Database.SavePharmacyProfile(profile);
        }

        internal static PharmacyProfile SaveAndReload(PharmacyProfile profile)
        {
            Save(profile);
            return Load();
        }

        internal static void RecordSavedAudit(PharmacyProfile profile)
        {
            string name = profile == null ? string.Empty : (profile.NameAr ?? string.Empty).Trim();
            string phone = profile == null ? string.Empty : (profile.Phone ?? string.Empty).Trim();
            Database.WriteAudit("PharmacyProfileSaved", "Settings", "تم حفظ بيانات الصيدلية: " + name + "، الهاتف: " + phone + ".");
        }
    }
}