using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class PasswordProtectorAdapter : IPasswordProtector
    {
        public string Protect(string password)
        {
            return PasswordSecurity.HashPassword(password);
        }
    }
}
