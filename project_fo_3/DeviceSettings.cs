// تفضيلات الأجهزة محفوظة للمستخدم والمحطة ولا تتصل بقاعدة البيانات.
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;

namespace project_fo_3
{
    internal static class DeviceSettings
    {
        internal const string Paper80Mm = "80mm";
        internal const string Paper58Mm = "58mm";
        internal const string InvoiceA4 = "A4";
        internal const string InvoiceReceipt = "Receipt";

        internal static string LoadPreferredPrinter()
        {
            try { return (Properties.Setings.Default.PreferredPrinterName ?? string.Empty).Trim(); }
            catch { return string.Empty; }
        }

        internal static string LoadReceiptPaperFormat()
        {
            try
            {
                return string.Equals(Properties.Setings.Default.ReceiptPaperFormat, Paper58Mm, StringComparison.OrdinalIgnoreCase)
                    ? Paper58Mm
                    : Paper80Mm;
            }
            catch { return Paper80Mm; }
        }

        internal static string LoadInvoicePrintFormat()
        {
            try
            {
                return string.Equals(Properties.Setings.Default.InvoicePrintFormat, InvoiceReceipt, StringComparison.OrdinalIgnoreCase)
                    ? InvoiceReceipt
                    : InvoiceA4;
            }
            catch { return InvoiceA4; }
        }

        internal static bool IsReceiptInvoiceFormat()
        {
            return string.Equals(LoadInvoicePrintFormat(), InvoiceReceipt, StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<string> GetInstalledPrinters()
        {
            try
            {
                return PrinterSettings.InstalledPrinters.Cast<string>()
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        internal static bool IsInstalled(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return false;
            }

            return GetInstalledPrinters().Any(name => string.Equals(name, printerName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // تحقق محلي فقط: لا يرسل مهمة طباعة ولا يغير التفضيلات المحفوظة.
        internal static bool IsReadyForPrinting(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return true; // سيُترك الاختيار لـ Windows عند الطباعة الفعلية.
            }

            if (!IsInstalled(printerName))
            {
                return false;
            }

            try
            {
                PrinterSettings settings = new PrinterSettings();
                settings.PrinterName = printerName.Trim();
                return settings.IsValid;
            }
            catch
            {
                return false;
            }
        }

        internal static bool Save(string printerName, string receiptPaperFormat)
        {
            return Save(printerName, receiptPaperFormat, InvoiceA4);
        }

        internal static bool Save(string printerName, string receiptPaperFormat, string invoicePrintFormat)
        {
            string normalizedPrinter = string.IsNullOrWhiteSpace(printerName) ? string.Empty : printerName.Trim();
            if (!string.IsNullOrEmpty(normalizedPrinter) && !IsInstalled(normalizedPrinter))
            {
                return false;
            }

            try
            {
                var settings = Properties.Setings.Default;
                string previousPrinter = settings.PreferredPrinterName;
                string previousPaper = settings.ReceiptPaperFormat;
                string previousInvoice = settings.InvoicePrintFormat;
                try
                {
                    settings.PreferredPrinterName = normalizedPrinter;
                    settings.ReceiptPaperFormat = NormalizeReceiptPaperFormat(receiptPaperFormat);
                    settings.InvoicePrintFormat = NormalizeInvoicePrintFormat(invoicePrintFormat);
                    settings.Save();
                    return true;
                }
                catch (Exception error)
                {
                    // A failed file save must not silently change the next print job.
                    // Restore exact stored values, including an unavailable remembered printer.
                    try { settings.PreferredPrinterName = previousPrinter; } catch { }
                    try { settings.ReceiptPaperFormat = previousPaper; } catch { }
                    try { settings.InvoicePrintFormat = previousInvoice; } catch { }
                    AppDiagnostics.Log("DeviceSettingsSave", error);
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static void ApplyPreferredPrinter(PrintDocument document)
        {
            if (document == null)
            {
                return;
            }

            string preferredPrinter = LoadPreferredPrinter();
            if (!string.IsNullOrEmpty(preferredPrinter) && IsInstalled(preferredPrinter))
            {
                document.PrinterSettings.PrinterName = preferredPrinter;
            }
        }

        internal static PaperSize CreateCashierPaperSize()
        {
            return new PaperSize(
                LoadReceiptPaperFormat() == Paper58Mm ? "Cashier Receipt 58mm" : "Cashier Receipt 80mm",
                GetReceiptPaperWidth(LoadReceiptPaperFormat()),
                1200);
        }

        internal static PaperSize CreateA4PaperSize()
        {
            return new PaperSize("A4", 827, 1169);
        }

        internal static PaperSize CreateInvoicePaperSize()
        {
            return IsReceiptInvoiceFormat() ? CreateCashierPaperSize() : CreateA4PaperSize();
        }

        internal static int GetReceiptPaperWidth(string receiptPaperFormat)
        {
            return string.Equals(receiptPaperFormat, Paper58Mm, StringComparison.OrdinalIgnoreCase) ? 228 : 300;
        }

        private static string NormalizeReceiptPaperFormat(string value)
        {
            return string.Equals(value, Paper58Mm, StringComparison.OrdinalIgnoreCase) ? Paper58Mm : Paper80Mm;
        }

        internal static string NormalizeInvoicePrintFormat(string value)
        {
            return string.Equals(value, InvoiceReceipt, StringComparison.OrdinalIgnoreCase) ? InvoiceReceipt : InvoiceA4;
        }
    }

    internal static class DeviceSettingsSelfTests
    {
        internal static void RunAll()
        {
            Require(DeviceSettings.GetReceiptPaperWidth(DeviceSettings.Paper80Mm) == 300, "مقاس الإيصال 80mm غير صحيح.");
            Require(DeviceSettings.GetReceiptPaperWidth(DeviceSettings.Paper58Mm) == 228, "مقاس الإيصال 58mm غير صحيح.");
            Require(DeviceSettings.GetReceiptPaperWidth("unknown") == 300, "القيمة غير المعروفة يجب أن تعود إلى 80mm الآمن.");
            Require(DeviceSettings.CreateA4PaperSize().Width == 827 && DeviceSettings.CreateA4PaperSize().Height == 1169, "مقاس A4 غير صحيح.");
            Require(DeviceSettings.NormalizeInvoicePrintFormat(DeviceSettings.InvoiceReceipt) == DeviceSettings.InvoiceReceipt, "اختيار الإيصال لا يحفظ بصورة صحيحة.");
            Require(DeviceSettings.NormalizeInvoicePrintFormat("unknown") == DeviceSettings.InvoiceA4, "القيمة غير المعروفة لمقاس الفاتورة يجب أن تعود إلى A4 الآمن.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
