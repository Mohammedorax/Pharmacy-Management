using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class PdfExportHelper
    {
        private const string PdfPrinterName = "Microsoft Print to PDF";

        public static Task<bool> SaveAsPdfAsync(PrintDocument document, string defaultFileName, IWin32Window owner, Func<bool> isCurrent=null)
        { return SaveAsPdfAsync(document,defaultFileName,owner,UiLanguageCatalog.IsEnglishActive,isCurrent); }

        internal static async Task<bool> SaveAsPdfAsync(PrintDocument document, string defaultFileName,
            IWin32Window owner, bool leftToRight, Func<bool> isCurrent=null)
        {
            if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, PdfPrinterName, StringComparison.OrdinalIgnoreCase)))
            {
                ShowPdfNotice(owner, PdfPrinterUnavailableMessage(leftToRight), leftToRight);
                return false;
            }

            using (SaveFileDialog dialog = CreatePdfSaveDialog(defaultFileName, true, leftToRight))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return false;
                }

                if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
                bool saved = await WritePdfCompleteFileAsync(dialog.FileName,
                    temporary => PrintReportPdf(document, temporary, Path.GetFileNameWithoutExtension(dialog.FileName)),
                    isCurrent, 30000, leftToRight);
                if (!saved) return false;

                if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
                ShowPdfNotice(owner, PdfSavedMessage(leftToRight), leftToRight);
                return true;
            }
        }

        internal static async Task<bool> SaveReportAsPdfAsync(PrintDocument document,
            string defaultFileName, IWin32Window owner, bool leftToRight, Func<bool> isCurrent)
        {
            if (!ReportPreviewLifetime.ContextCurrent(isCurrent)) return false;
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!PrinterSettings.InstalledPrinters.Cast<string>().Any(p =>
                string.Equals(p, PdfPrinterName, StringComparison.OrdinalIgnoreCase)))
            {
                ShowPdfNotice(owner, PdfPrinterUnavailableMessage(leftToRight), leftToRight);
                return false;
            }
            using (var dialog = CreatePdfSaveDialog(defaultFileName, false, leftToRight))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK ||
                    !ReportPreviewLifetime.ContextCurrent(isCurrent)) return false;
                bool saved = await WritePdfCompleteFileAsync(dialog.FileName,
                    temporary => PrintReportPdf(document, temporary, Path.GetFileNameWithoutExtension(dialog.FileName)),
                    isCurrent, 30000, leftToRight);
                if (!saved || !ReportPreviewLifetime.ContextCurrent(isCurrent)) return false;
                ShowPdfNotice(owner, PdfSavedMessage(leftToRight), leftToRight);
                return true;
            }
        }

        internal static SaveFileDialog CreatePdfSaveDialog(string defaultFileName, bool invoice, bool leftToRight)
        {
            return new SaveFileDialog
            {
                Title = PresentPdf(invoice ? "حفظ الفاتورة PDF" : "حفظ التقرير PDF", leftToRight),
                Filter = PresentPdf("ملف PDF (*.pdf)|*.pdf", leftToRight), DefaultExt = "pdf", AddExtension = true,
                FileName = SanitizeFileName(!invoice && string.IsNullOrWhiteSpace(defaultFileName) ? "report.pdf" : defaultFileName),
                OverwritePrompt = true
            };
        }

        internal static string PdfPrinterUnavailableMessage(bool leftToRight) => PresentPdf(
            "لا يمكن حفظ PDF لأن طابعة Microsoft Print to PDF غير مفعلة في ويندوز.\nفعّلها من ميزات ويندوز أو اختر طباعة A4 ثم اختر طابعة PDF يدوياً.", leftToRight);

        internal static string PdfSavedMessage(bool leftToRight) => PresentPdf("تم حفظ ملف PDF بنجاح.", leftToRight);

        private static void ShowPdfNotice(IWin32Window owner, string message, bool leftToRight)
        {
            AppNotifications.Show(owner, message, PresentPdf("حفظ PDF", leftToRight), MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, leftToRight);
        }

        internal static void PrintReportPdf(PrintDocument document, string output, string documentName)
        {
            // This document belongs to the PDF job, never to the live preview.
            var page = (PageSettings)document.DefaultPageSettings.Clone();
            document.PrinterSettings = new PrinterSettings
            { PrinterName = PdfPrinterName, PrintToFile = true, PrintFileName = output };
            page.PrinterSettings = document.PrinterSettings;
            document.DefaultPageSettings = page;
            document.PrintController = new StandardPrintController();
            document.DocumentName = documentName;
            document.Print();
        }

        // The printer may finish its file after Print returns. Await without pumping nested UI
        // messages, and commit only completed PDF header/EOF output using atomic replacement.
        // A completed candidate must also have no active writable handles. Keep a read/delete
        // lease through a new-file move so it cannot publish a still-running producer.
        // This is a
        // completion check, not a substitute for validating the rendered PDF with a reader.
        internal static async Task<bool> WritePdfCompleteFileAsync(string target, Action<string> writePdf,
            Func<bool> isCurrent = null, int completionTimeoutMilliseconds = 30000, bool leftToRight = false)
        {
            if (!ReportPreviewLifetime.ContextCurrent(isCurrent)) return false;
            if (writePdf == null) throw new ArgumentNullException(nameof(writePdf));
            if (completionTimeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(completionTimeoutMilliseconds));
            if (string.IsNullOrWhiteSpace(target) || !string.Equals(Path.GetExtension(target), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(PresentPdf("اختر ملف PDF بامتداد pdf.", leftToRight), nameof(target));
            string fullTarget = Path.GetFullPath(target);
            string temporary = Path.Combine(Path.GetDirectoryName(fullTarget), ".pharmacy-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
            bool generationStarted = false;
            try
            {
                if (File.Exists(temporary)) throw new IOException("Temporary PDF path already exists.");
                if (!ReportPreviewLifetime.ContextCurrent(isCurrent)) return false;
                generationStarted = true;
                writePdf(temporary);
                var wait = Stopwatch.StartNew();
                bool revoked = false;
                IOException sharingError = null;
                while (true)
                {
                    // Revocation forbids commit, but a queued printer can still finish its file.
                    // Await that bounded completion so cleanup cannot precede a late creation.
                    revoked = revoked || !ReportPreviewLifetime.ContextCurrent(isCurrent);
                    using (var completed = TryOpenCompletePdf(temporary))
                    {
                        if (completed != null)
                        {
                            revoked = revoked || !ReportPreviewLifetime.ContextCurrent(isCurrent);
                            try
                            {
                                if (revoked) { File.Delete(temporary); return false; }
                                if (File.Exists(fullTarget))
                                {
                                    // Windows ReplaceFile opens the replacement itself and rejects
                                    // an active writable source. Release our read lease for that open.
                                    completed.Dispose();
                                    File.Replace(temporary, fullTarget, null);
                                }
                                else File.Move(temporary, fullTarget);
                                return true;
                            }
                            catch (IOException error) when (IsSharingConflict(error)) { sharingError = error; }
                        }
                    }
                    if (wait.ElapsedMilliseconds >= completionTimeoutMilliseconds)
                    {
                        if (revoked) return false;
                        if (sharingError != null) throw sharingError;
                        throw new IOException(PresentPdf("تعذر تأكيد اكتمال ملف PDF؛ لم يُستبدل الملف السابق.", leftToRight));
                    }
                    await Task.Delay(Math.Min(100, Math.Max(1, completionTimeoutMilliseconds - (int)wait.ElapsedMilliseconds)));
                }
            }
            finally
            {
                try { if (generationStarted && File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException error) { AppDiagnostics.Log("ReportPdfTemporaryCleanup", error); }
                catch (UnauthorizedAccessException error) { AppDiagnostics.Log("ReportPdfTemporaryCleanup", error); }
            }
        }

        private static FileStream TryOpenCompletePdf(string path)
        {
            FileStream stream = null;
            try
            {
                // Readers and rename/delete remain allowed; a producer with write access
                // prevents this lease from opening until it closes its writable handle.
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    if (stream.Length < 11 || Encoding.ASCII.GetString(reader.ReadBytes(5)) != "%PDF-") return null;
                    int length = (int)Math.Min(1024, stream.Length);
                    stream.Seek(-length, SeekOrigin.End);
                    if (!Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\r', '\n', ' ', '\t', '\0')
                        .EndsWith("%%EOF", StringComparison.Ordinal)) return null;
                    var completed = stream;
                    stream = null; // Ownership transfers to the commit scope.
                    return completed;
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (IOException) { return null; }
            finally { stream?.Dispose(); }
        }

        private static string PresentPdf(string value, bool leftToRight)
        { return leftToRight ? UiLanguageCatalog.Translate(value) : value; }

        private static bool IsSharingConflict(IOException error)
        { int code = error.HResult & 0xffff; return code == 32 || code == 33; }

        private static string SanitizeFileName(string fileName)
        {
            string cleanName = string.IsNullOrWhiteSpace(fileName) ? "invoice.pdf" : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                cleanName = cleanName.Replace(invalidChar, '-');
            }

            return cleanName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? cleanName : cleanName + ".pdf";
        }
    }
}
