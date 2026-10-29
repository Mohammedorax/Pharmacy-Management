using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace project_fo_3
{
    internal static class RememberMeService
    {
        private const string FileName = "remembered-user.dat";
        private const string LegacyFileName = "credentials.txt";
        private const string AppFolderName = "PharmacyManagement";

        public sealed class RememberedCredentials
        {
            public string UserName { get; set; }
            public string Password { get; set; }
        }

        public static string LoadRememberedUser()
        {
            RememberedCredentials credentials = LoadRememberedCredentials();
            return credentials.UserName;
        }

        public static RememberedCredentials LoadRememberedCredentials()
        {
            string legacyUserName = ReadLegacyUserName();

            string path = GetDataPath(FileName);
            string legacyProtectedPath = GetBasePath(FileName);
            if (!File.Exists(path) && File.Exists(legacyProtectedPath))
            {
                TryMove(legacyProtectedPath, path);
            }

            if (!File.Exists(path) && !string.IsNullOrWhiteSpace(legacyUserName))
            {
                SaveRememberedUser(legacyUserName);
                return CreateCredentials(legacyUserName.Trim(), string.Empty);
            }

            DeleteLegacyCredentials();

            if (!File.Exists(path))
            {
                return CreateCredentials(string.Empty, string.Empty);
            }

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                string protectedText = TryUnprotectText(encrypted);
                RememberedCredentials protectedCredentials = ParseCredentials(protectedText);
                if (!string.IsNullOrWhiteSpace(protectedCredentials.UserName))
                {
                    // Rewrite old protected v2 payloads so a previously remembered
                    // password is removed after the first successful read. The current
                    // v4 payload intentionally keeps the password (DPAPI-protected).
                    if (protectedText.StartsWith("v2\n", StringComparison.Ordinal))
                    {
                        SaveRememberedUser(protectedCredentials.UserName);
                    }

                    return protectedCredentials;
                }

                TryDelete(path);
                return CreateCredentials(string.Empty, string.Empty);
            }
            catch
            {
                TryDelete(path);
                return CreateCredentials(string.Empty, string.Empty);
            }
        }

        public static void SaveRememberedUser(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                Clear();
                return;
            }

            DeleteLegacyCredentials();

            string payloadText = BuildUserPayload(userName.Trim());
            byte[] plain = Encoding.UTF8.GetBytes(payloadText);
            byte[] payload = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            string path = GetDataPath(FileName);
            EnsureParentDirectory(path);
            File.WriteAllBytes(path, payload);
        }

        public static void SaveRememberedCredentials(string userName, string password)
        {
            // v4 payload: the password is kept only while "remember me" stays checked,
            // encrypted with Windows DPAPI for the current user on this device.
            if (string.IsNullOrWhiteSpace(password))
            {
                SaveRememberedUser(userName);
                return;
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                Clear();
                return;
            }

            DeleteLegacyCredentials();

            string payloadText = "v4\n" + EncodeLine(userName.Trim()) + "\n" + EncodeLine(password);
            byte[] plain = Encoding.UTF8.GetBytes(payloadText);
            byte[] payload = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            string path = GetDataPath(FileName);
            EnsureParentDirectory(path);
            File.WriteAllBytes(path, payload);
        }

        public static void Clear()
        {
            TryDelete(GetDataPath(FileName));
            TryDelete(GetBasePath(FileName));
            DeleteLegacyCredentials();
        }

        private static void DeleteLegacyCredentials()
        {
            TryDelete(GetDataPath(LegacyFileName));
            TryDelete(GetBasePath(LegacyFileName));
        }

        private static string ReadLegacyUserName()
        {
            string userName = ReadFirstLine(GetBasePath(LegacyFileName));
            if (!string.IsNullOrWhiteSpace(userName))
            {
                return userName;
            }

            return ReadFirstLine(GetDataPath(LegacyFileName));
        }

        private static string ReadFirstLine(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return string.Empty;
                }

                using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
                {
                    return reader.ReadLine() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryUnprotectText(byte[] encrypted)
        {
            try
            {
                byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static RememberedCredentials ParseCredentials(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return CreateCredentials(string.Empty, string.Empty);
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length >= 2 && (lines[0] == "v3" || lines[0] == "v2"))
            {
                return CreateCredentials(DecodeLine(lines[1]), string.Empty);
            }

            if (lines.Length >= 3 && lines[0] == "v4")
            {
                return CreateCredentials(DecodeLine(lines[1]), DecodeLine(lines[2]));
            }

            return CreateCredentials(text.Trim(), string.Empty);
        }

        private static string BuildUserPayload(string userName)
        {
            return "v3\n" + EncodeLine(userName);
        }

        private static string EncodeLine(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeLine(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static RememberedCredentials CreateCredentials(string userName, string password)
        {
            return new RememberedCredentials
            {
                UserName = userName ?? string.Empty,
                Password = password ?? string.Empty
            };
        }

        private static string GetDataPath(string fileName)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced)
                return Path.Combine(root, AppFolderName, fileName);

            // Fixed QA bundles must never read, migrate or clear the live user's cache.
            var profile = Database.GetConnectionProfile();
            if (!profile.IsTestEnvironment)
                throw new InvalidOperationException("Pinned remembered-user state requires a test database.");
            using (var identity = new MemoryStream())
            {
                using (var writer = new BinaryWriter(identity, Encoding.UTF8, true))
                {
                    writer.Write(profile.ServerName);
                    writer.Write(profile.DatabaseName);
                }
                using (var hash = SHA256.Create())
                {
                    string key = BitConverter.ToString(hash.ComputeHash(identity.ToArray())).Replace("-", string.Empty);
                    return Path.Combine(root, AppFolderName, "Qa", key, fileName);
                }
            }
        }

        private static string GetBasePath(string fileName)
        {
            if (RuntimeSettingsStore.IsBuildConnectionProfileForced) return GetDataPath(fileName);
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        private static void TryMove(string sourcePath, string targetPath)
        {
            try
            {
                EnsureParentDirectory(targetPath);
                if (File.Exists(targetPath))
                {
                    TryDelete(sourcePath);
                    return;
                }

                File.Move(sourcePath, targetPath);
            }
            catch
            {
                // Remember-me migration must not block login.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Remember-me cleanup must not block login.
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
