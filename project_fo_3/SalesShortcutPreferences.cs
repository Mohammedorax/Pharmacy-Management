using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace project_fo_3
{
    internal enum SalesShortcutAction { Search, Barcode, Quantity, Preview }

    internal sealed class SalesShortcutProfile
    {
        private readonly Dictionary<SalesShortcutAction, Keys> bindings;
        private static readonly Keys[] availableKeys =
        {
            Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6,
            Keys.F7, Keys.F8, Keys.F9, Keys.F11, Keys.F12
        };

        internal static IReadOnlyList<Keys> AvailableKeys { get { return Array.AsReadOnly(availableKeys); } }

        internal SalesShortcutProfile(IDictionary<SalesShortcutAction, Keys> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var actions = (SalesShortcutAction[])Enum.GetValues(typeof(SalesShortcutAction));
            if (values.Count != actions.Length || actions.Any(action => !values.ContainsKey(action)))
                throw new ArgumentException("Each sales action needs one shortcut.", nameof(values));
            if (values.Values.Any(key => !AvailableKeys.Contains(key)))
                throw new ArgumentException("Use an available function key without modifiers.", nameof(values));
            if (values.Values.Distinct().Count() != actions.Length)
                throw new ArgumentException("Two sales actions cannot use the same shortcut.", nameof(values));
            bindings = new Dictionary<SalesShortcutAction, Keys>(values);
        }

        internal Keys this[SalesShortcutAction action] { get { return bindings[action]; } }
        internal static SalesShortcutProfile Defaults
        {
            get
            {
                return new SalesShortcutProfile(new Dictionary<SalesShortcutAction, Keys>
                {
                    { SalesShortcutAction.Search, Keys.F3 },
                    { SalesShortcutAction.Barcode, Keys.F2 },
                    { SalesShortcutAction.Quantity, Keys.F4 },
                    { SalesShortcutAction.Preview, Keys.F9 }
                });
            }
        }

        internal SalesShortcutAction? Resolve(Keys keyData)
        {
            // Preserve the existing search alias; ordinary editing keys remain untouched.
            if (keyData == (Keys.Control | Keys.K)) return SalesShortcutAction.Search;
            foreach (var binding in bindings)
                if (binding.Value == keyData) return binding.Key;
            return null;
        }

        internal IDictionary<SalesShortcutAction, Keys> CopyBindings()
        {
            return new Dictionary<SalesShortcutAction, Keys>(bindings);
        }
    }

    internal sealed class SalesShortcutProfileStore
    {
        private readonly string directory;
        internal SalesShortcutProfileStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A settings directory is required.");
            this.directory = Path.GetFullPath(directory);
        }

        internal static string DefaultDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PharmacyManagement", "sales-shortcuts");
            }
        }

        private static string UserKey(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("A signed-in user is required.");
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(userName.Trim().ToUpperInvariant())))
                    .Replace("-", string.Empty);
        }

        private string PathFor(string userName) { return Path.Combine(directory, UserKey(userName) + ".xml"); }

        internal SalesShortcutProfile Load(string userName)
        {
            string path = PathFor(userName);
            if (!File.Exists(path)) return SalesShortcutProfile.Defaults;
            if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Shortcut settings are too large.");
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 16384
            };
            XDocument document;
            using (var reader = XmlReader.Create(path, settings)) document = XDocument.Load(reader);
            var root = document.Root;
            if (root == null || root.Name != "SalesShortcuts" || (string)root.Attribute("version") != "1"
                || (string)root.Attribute("user") != UserKey(userName))
                throw new InvalidDataException("Unsupported shortcut settings.");
            var entries = root.Elements().ToArray();
            if (entries.Length != 4 || entries.Any(entry => entry.Name != "Binding"))
                throw new InvalidDataException("Invalid shortcut bindings.");
            var bindings = new Dictionary<SalesShortcutAction, Keys>();
            foreach (var entry in entries)
            {
                SalesShortcutAction action;
                Keys key;
                if (!Enum.TryParse((string)entry.Attribute("action"), false, out action)
                    || !Enum.IsDefined(typeof(SalesShortcutAction), action)
                    || !Enum.TryParse((string)entry.Attribute("key"), false, out key)
                    || bindings.ContainsKey(action))
                    throw new InvalidDataException("Invalid shortcut entry.");
                bindings.Add(action, key);
            }
            try { return new SalesShortcutProfile(bindings); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid shortcut bindings.", error); }
        }

        internal void Save(string userName, SalesShortcutProfile profile, Func<bool> isCurrent = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (isCurrent != null && !isCurrent()) throw new OperationCanceledException("Shortcut settings context changed.");
            string path = PathFor(userName);
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var document = new XDocument(new XElement("SalesShortcuts",
                new XAttribute("version", "1"), new XAttribute("user", UserKey(userName)),
                profile.CopyBindings().OrderBy(binding => binding.Key).Select(binding =>
                    new XElement("Binding", new XAttribute("action", binding.Key.ToString()),
                        new XAttribute("key", binding.Value.ToString())))));
            try
            {
                document.Save(temporary);
                if (isCurrent != null && !isCurrent()) throw new OperationCanceledException("Shortcut settings context changed.");
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
