using System;
using System.Security.Cryptography;

namespace project_fo_3
{
    internal static class PasswordSecurity
    {
        // تحافظ البادئة القديمة على قابلية الدخول للحسابات التي حُفظت قبل ترقية SHA-256.
        private const string LegacyPrefix = "PBKDF2";
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;
        internal const int MinimumPasswordLength = 3;

        public static string HashPassword(string password)
        {
            ValidatePasswordInput(password);
            return HashPasswordCore(password);
        }

        // يُستخدم فقط لترقية كلمات مرور تاريخية تحققت بالفعل. لا نسمح به لمسار الإنشاء أو التعديل الجديد.
        internal static string HashLegacyPasswordForMigration(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            return HashPasswordCore(password);
        }

        private static string HashPasswordCore(string password)
        {

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = DeriveHash(password, salt, Iterations, HashAlgorithmName.SHA256);
            return Prefix + "$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string storedPassword, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (password == null || string.IsNullOrWhiteSpace(storedPassword))
            {
                return false;
            }

            if (!IsHashed(storedPassword))
            {
                needsUpgrade = true;
                return string.Equals(password, storedPassword, StringComparison.Ordinal);
            }

            needsUpgrade = storedPassword.StartsWith(LegacyPrefix + "$", StringComparison.Ordinal);

            string[] parts = storedPassword.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations < Iterations || iterations > 1000000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                HashAlgorithmName algorithm = storedPassword.StartsWith(Prefix + "$", StringComparison.Ordinal)
                    ? HashAlgorithmName.SHA256
                    : HashAlgorithmName.SHA1;
                byte[] actual = DeriveHash(password, salt, iterations, algorithm);
                return FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsHashed(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.StartsWith(Prefix + "$", StringComparison.Ordinal) || value.StartsWith(LegacyPrefix + "$", StringComparison.Ordinal));
        }

        internal static void ValidatePasswordInput(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (password.Length == 0) throw new ArgumentException("كلمة المرور مطلوبة.", nameof(password));
            if (!string.Equals(password, password.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("لا يمكن أن تبدأ كلمة المرور أو تنتهي بمسافة.", nameof(password));
            if (password.Length < MinimumPasswordLength)
                throw new ArgumentException("يجب ألا تقل كلمة المرور عن " + MinimumPasswordLength + " حرفاً.", nameof(password));
        }

        private static byte[] DeriveHash(string password, byte[] salt, int iterations, HashAlgorithmName algorithm)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, algorithm))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
