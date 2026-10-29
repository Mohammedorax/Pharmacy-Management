using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace project_fo_3
{
    internal static class DeviceSettingsStorageSelfTests
    {
        internal static void VerifyFileLock(string token)
        {
            string path=RequireFreshOwnedCopy(token);
            var settings=Properties.Setings.Default;
            string originalPrinter=settings.PreferredPrinterName,originalPaper=settings.ReceiptPaperFormat,originalInvoice=settings.InvoicePrintFormat;
            bool initialized=false;int cases=0,failures=0;
            try
            {
                foreach(string paper in new[]{DeviceSettings.Paper58Mm,DeviceSettings.Paper80Mm})
                foreach(string invoice in new[]{DeviceSettings.InvoiceA4,DeviceSettings.InvoiceReceipt})
                {
                    const string previousPrinter="  QA remembered printer  ";
                    string previousPaper=paper==DeviceSettings.Paper58Mm?DeviceSettings.Paper80Mm:DeviceSettings.Paper58Mm;
                    string previousInvoice=invoice==DeviceSettings.InvoiceA4?DeviceSettings.InvoiceReceipt:DeviceSettings.InvoiceA4;
                    settings.PreferredPrinterName=previousPrinter;settings.ReceiptPaperFormat=previousPaper;settings.InvoicePrintFormat=previousInvoice;
                    settings.Save();initialized=true;
                    Check(File.Exists(path),"Owned settings provider did not create the expected file.");
                    byte[] before=File.ReadAllBytes(path);
                    bool saved,memoryPreserved;
                    using(var fileLock=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                    {
                        saved=DeviceSettings.Save(string.Empty,paper,invoice);
                        memoryPreserved=settings.PreferredPrinterName==previousPrinter&&settings.ReceiptPaperFormat==previousPaper&&settings.InvoicePrintFormat==previousInvoice;
                    }
                    bool bytesPreserved=before.SequenceEqual(File.ReadAllBytes(path));
                    Console.WriteLine("Device save failure: receipt="+paper+"; invoice="+invoice+"; saved="+saved+"; memoryPreserved="+memoryPreserved+"; bytesPreserved="+bytesPreserved);
                    if(saved||!memoryPreserved||!bytesPreserved)failures++;
                    Check(DeviceSettings.Save(string.Empty,paper,invoice),"Device save did not retry after owned lock release.");
                    settings.Reload();
                    Check(settings.PreferredPrinterName==string.Empty&&settings.ReceiptPaperFormat==paper&&settings.InvoicePrintFormat==invoice,
                        "Device retry did not persist the exact pending choices.");
                    cases++;
                }
            }
            finally
            {
                if(initialized)
                {
                    settings.PreferredPrinterName=originalPrinter;settings.ReceiptPaperFormat=originalPaper;settings.InvoicePrintFormat=originalInvoice;
                    settings.Save();settings.Reload();
                    Check(settings.PreferredPrinterName==originalPrinter&&settings.ReceiptPaperFormat==originalPaper&&settings.InvoicePrintFormat==originalInvoice,
                        "Owned device settings defaults were not restored.");
                }
            }
            if(failures>0)throw new InvalidOperationException("Device save failure changed memory or disk in "+failures+" of "+cases+" cases.");
            Console.WriteLine("PASS device settings file-lock failure: "+cases+" paper/invoice cases; all three raw preferences and prior file bytes retained; retry persisted; owned defaults restored; no UI, printer job, SQL or existing user config modified. SettingsPath="+path);
        }

        private static string RequireFreshOwnedCopy(string token)
        {
            Guid parsed;
            if(!Guid.TryParseExact(token,"N",out parsed))throw new UnauthorizedAccessException("Owned device-settings token required.");
            string expected=Path.Combine(@"C:\PharmacyQA\work","device-settings-save-lock-"+token);
            if(!string.Equals(Path.GetFullPath(System.Windows.Forms.Application.StartupPath).TrimEnd(Path.DirectorySeparatorChar),expected,StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Device settings lock verifier requires a fresh owned C copy.");
            if(!RuntimeSettingsStore.IsBuildConnectionProfileForced||!Database.GetConnectionProfile().IsTestEnvironment)
                throw new UnauthorizedAccessException("Device settings lock verifier requires the pinned QA profile.");
            string path=Path.GetFullPath(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
            string local=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(local,StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetPathRoot(path),@"C:\",StringComparison.OrdinalIgnoreCase)||
                path.IndexOf("_Url_",StringComparison.OrdinalIgnoreCase)<0||!string.Equals(Path.GetFileName(path),"user.config",StringComparison.OrdinalIgnoreCase)||File.Exists(path))
                throw new UnauthorizedAccessException("Device settings verification refuses an existing or unowned user.config.");
            return path;
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
