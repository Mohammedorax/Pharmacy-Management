using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class JournalPendingStoreSelfTests
    {
        private static int checks;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); ++checks; }
        private static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { ++checks; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        internal static void Verify()
        {
            checks = 0;
            string directory = Path.Combine(Path.GetTempPath(), "PharmacyJournalStore-" + Guid.NewGuid().ToString("N"));
            string context = new string('a', 64), otherContext = new string('b', 64);
            string file = Path.Combine(directory, context + "-Manual.pending");
            var lines = new[] { new JournalLineValues("1000", 1250.50m, 0, " باراسيتامول 500 mg\r\nA&B ", null),
                new JournalLineValues("2000", 0, 1250.50m, null, 7) };
            var pending = JournalPendingRequest.Create(new DateTime(2026, 10, 5, 14, 30, 1), "Manual", "صيدلية المهندس", lines);
            try
            {
                Reject<ArgumentException>(() => new JournalPendingStore(directory, "../bad", "Manual"));
                Reject<ArgumentException>(() => new JournalPendingStore(directory, context, "SalesInvoice"));
                using (var store = new JournalPendingStore(directory, context, "Manual"))
                {
                    Check(store.Load() == null, "Missing recovery must be passive and empty.");
                    Reject<IOException>(() => { using (var duplicate = new JournalPendingStore(directory, context, "Manual")) { } });
                    Reject<ArgumentNullException>(() => store.Save(null));
                    store.Save(pending);
                    Check(store.Load().HasSameContent(pending.Request), "Payload failed roundtrip.");
                    byte[] original = File.ReadAllBytes(file);
                    Check(!Encoding.UTF8.GetString(original).Contains("JournalPending") &&
                        !Encoding.UTF8.GetString(original).Contains("صيدلية المهندس"), "Clear business data leaked into the record.");
                    store.Save(pending);
                    Check(original.SequenceEqual(File.ReadAllBytes(file)), "Retry rewrote immutable pending record.");
                    Reject<InvalidOperationException>(() => store.Save(JournalPendingRequest.Create(pending.Request.Date, "Manual", pending.Request.Description, lines)));
                    Reject<InvalidOperationException>(() => store.Save(JournalPendingRequest.Restore(new JournalPostRequest(pending.Request.Date,
                        "Manual", pending.Request.SourceId, "changed", lines))));
                    Reject<InvalidOperationException>(() => store.Resolve(Guid.NewGuid().ToString("N")));
                    Check(store.Load().HasSameContent(pending.Request), "Rejected replace/resolve damaged pending request.");
                    byte[] corrupted = (byte[])original.Clone(); corrupted[corrupted.Length / 2] ^= 0x40;
                    File.WriteAllBytes(file, corrupted);
                    Reject<CryptographicException>(() => store.Load());
                    Reject<CryptographicException>(() => store.Save(pending));
                    Check(corrupted.SequenceEqual(File.ReadAllBytes(file)), "Corruption was silently overwritten.");
                    File.WriteAllBytes(file, original);
                    using (var separate = new JournalPendingStore(directory, otherContext, "Manual"))
                    {
                        string otherFile = Path.Combine(directory, otherContext + "-Manual.pending");
                        Check(separate.Load() == null, "Context leaked pending request.");
                        File.WriteAllBytes(otherFile, original);
                        Reject<CryptographicException>(() => separate.Load());
                        File.Delete(otherFile);
                    }
                    using (var opening = new JournalPendingStore(directory, context, "OpeningEntry"))
                    {
                        Check(opening.Load() == null, "Manual and opening stores collided.");
                        Reject<InvalidOperationException>(() => opening.Save(pending));
                        var openingPending = JournalPendingRequest.Create(pending.Request.Date, "OpeningEntry", null, lines);
                        opening.Save(openingPending);
                        Check(opening.Load().HasSameContent(openingPending.Request), "Null descriptions lost on restore.");
                        opening.Resolve(openingPending.Request.SourceId);
                    }
                    string xml = "<!DOCTYPE JournalPending [<!ENTITY x SYSTEM 'file:///nonexistent'>]><JournalPending>&x;</JournalPending>";
                    var malformed = ProtectedData.Protect(Encoding.UTF8.GetBytes(xml),
                        Encoding.UTF8.GetBytes("PharmacyJournalPending/v1/" + context + "/Manual"), DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(file, malformed);
                    Reject<System.Xml.XmlException>(() => store.Load());
                    File.WriteAllBytes(file, new byte[1048576 + 65537]);
                    Reject<InvalidDataException>(() => store.Load());
                    File.WriteAllBytes(file, original);
                    Check(!Directory.GetFiles(directory, "*.tmp").Any(), "Temporary records leaked.");
                }
                using (var restored = new JournalPendingStore(directory, context, "Manual"))
                {
                    Check(restored.Load().HasSameContent(pending.Request), "Reopened store changed identity or payload.");
                    restored.Resolve(pending.Request.SourceId);
                    Check(restored.Load() == null, "Accepted pending request was not resolved.");
                    restored.Resolve(pending.Request.SourceId);
                }
                using (var currencyStore = new JournalPendingStore(directory, context, "Manual"))
                {
                    currencyStore.Save(pending);
                    var legacyLoaded = currencyStore.Load();
                    Check(legacyLoaded.Request.ExpectedSystemCurrencyCode == null, "Legacy file invented a base currency.");
                    byte[] legacyBytes = File.ReadAllBytes(file);
                    var captured = JournalPendingRequest.Restore(new JournalPostRequest(pending.Request.Date, "Manual",
                        pending.Request.SourceId, pending.Request.Description, lines, " yer "));
                    Reject<InvalidOperationException>(() => currencyStore.Save(captured));
                    Check(legacyBytes.SequenceEqual(File.ReadAllBytes(file)), "Legacy request was silently assigned a currency.");
                    currencyStore.ReplaceRejected(legacyLoaded, captured);
                    Check(currencyStore.Load().HasSameContent(captured.Request) &&
                        currencyStore.Load().Request.ExpectedSystemCurrencyCode == "YER", "Explicit rejected-request correction lost currency or payload.");
                    byte[] version2Bytes = File.ReadAllBytes(file);
                    byte[] recordEntropy = Encoding.UTF8.GetBytes("PharmacyJournalPending/v1/" + context + "/Manual");
                    var document = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(
                        ProtectedData.Unprotect(version2Bytes, recordEntropy, DataProtectionScope.CurrentUser)));
                    Check((string)document.Root.Attribute("version") == "2" &&
                        (string)document.Root.Element("ExpectedSystemCurrencyCode") == "YER", "Currency record format is not version2.");
                    Check(!Encoding.UTF8.GetString(version2Bytes).Contains("ExpectedSystemCurrencyCode"), "Currency metadata leaked in clear text.");
                    currencyStore.Save(captured);
                    Check(version2Bytes.SequenceEqual(File.ReadAllBytes(file)), "Currency retry rewrote its encrypted record.");
                    Reject<InvalidOperationException>(() => currencyStore.Save(JournalPendingRequest.Restore(new JournalPostRequest(
                        pending.Request.Date, "Manual", pending.Request.SourceId, pending.Request.Description, lines, "USD"))));
                    Action writeDocument = () => File.WriteAllBytes(file, ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)), recordEntropy, DataProtectionScope.CurrentUser));
                    document.Root.SetAttributeValue("version", "3"); writeDocument();
                    Reject<InvalidDataException>(() => currencyStore.Load());
                    document.Root.SetAttributeValue("version", "2");
                    document.Root.Element("ExpectedSystemCurrencyCode").Remove(); writeDocument();
                    Reject<InvalidDataException>(() => currencyStore.Load());
                    document.Root.Add(new System.Xml.Linq.XElement("ExpectedSystemCurrencyCode", new System.Xml.Linq.XAttribute("null", "1")));
                    writeDocument(); Reject<InvalidDataException>(() => currencyStore.Load());
                    File.WriteAllBytes(file, version2Bytes);
                    Check(currencyStore.Load().HasSameContent(captured.Request), "Rejected malformed record damaged valid recovery.");
                    currencyStore.Resolve(captured.Request.SourceId);
                    var freshCurrency = JournalPendingRequest.Create(pending.Request.Date, "Manual", pending.Request.Description, lines, "USD");
                    currencyStore.Save(freshCurrency);
                    Check(currencyStore.Load().HasSameContent(freshCurrency.Request), "Fresh currency snapshot failed encrypted roundtrip.");
                    currencyStore.Resolve(freshCurrency.Request.SourceId);
                }
                var disposed = new JournalPendingStore(directory, context, "Manual"); disposed.Dispose(); disposed.Dispose();
                Reject<ObjectDisposedException>(() => disposed.Load());
                Reject<ObjectDisposedException>(() => disposed.Save(pending));
                Reject<ObjectDisposedException>(() => disposed.Resolve(pending.Request.SourceId));
                Console.WriteLine("PASS: " + checks + " encrypted journal store checks: exact mixed/null payload, exclusive lease, immutable retry, corruption/context/DTD/size rejection, reopened recovery and resolution; no SQL or UI posting.");
            }
            finally
            {
                string resolved = Path.GetFullPath(directory), root = Path.GetFullPath(Path.GetTempPath());
                if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("PharmacyJournalStore-"))
                    throw new InvalidOperationException("Owned test cleanup escaped temporary root.");
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
