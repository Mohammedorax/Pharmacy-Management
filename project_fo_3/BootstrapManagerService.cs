namespace project_fo_3
{
    // نقطة تشغيل واحدة لإنشاء المدير الأول مع الحفاظ على تحقق كلمة المرور داخل النموذج.
    internal static class BootstrapManagerService
    {
        internal static void CreateInitialManager(string employeeId, string employeeName, int age, int salary, string phone, string password)
        {
            Database.CreateBootstrapManager(employeeId, employeeName, age, salary, phone, password);
        }
    }
}