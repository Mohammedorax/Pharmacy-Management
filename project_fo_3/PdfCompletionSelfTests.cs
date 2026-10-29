using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace project_fo_3
{
    internal static class PdfCompletionSelfTests
    {
        private static readonly byte[] First=Encoding.ASCII.GetBytes("%PDF-1.4\nfirst segment\n%%EOF\n");
        private static readonly byte[] Last=Encoding.ASCII.GetBytes("last segment\n%%EOF\n");
        private static readonly byte[] Previous=Encoding.ASCII.GetBytes("previous user report");

        internal static void VerifyPresentation()
        {
            int failures=0;
            foreach(bool invoice in new[]{true,false})
            foreach(bool english in new[]{false,true})
            using(var dialog=PdfExportHelper.CreatePdfSaveDialog("Sales:1001",invoice,english))
            {
                string expectedTitle=invoice?(english?"Save invoice as PDF":"حفظ الفاتورة PDF"):(english?"Save report as PDF":"حفظ التقرير PDF");
                string expectedFilter=english?"PDF file (*.pdf)|*.pdf":"ملف PDF (*.pdf)|*.pdf";
                bool valid=dialog.Title==expectedTitle&&dialog.Filter==expectedFilter&&dialog.FileName=="Sales-1001.pdf"&&
                    dialog.DefaultExt=="pdf"&&dialog.AddExtension&&dialog.OverwritePrompt;
                Console.WriteLine("PDF save presentation: invoice="+invoice+"; english="+english+"; pass="+valid);
                if(!valid)failures++;
            }
            if(failures>0)throw new InvalidOperationException("PDF save presentation failed in "+failures+" of four language/document cases.");
            Check(PdfExportHelper.PdfSavedMessage(true)=="The PDF file was saved successfully."&&
                PdfExportHelper.PdfSavedMessage(false)=="تم حفظ ملف PDF بنجاح.","PDF success feedback lost its explicit language.");
            Check(PdfExportHelper.PdfPrinterUnavailableMessage(true)=="Microsoft Print to PDF is unavailable. Enable it in Windows features, or use A4 printing and select a PDF printer."&&
                PdfExportHelper.PdfPrinterUnavailableMessage(false).StartsWith("لا يمكن حفظ PDF لأن طابعة Microsoft Print to PDF"),"PDF unavailable-printer feedback lost its explicit language.");
            using(var invoice=PdfExportHelper.CreatePdfSaveDialog(null,true,false))
            using(var report=PdfExportHelper.CreatePdfSaveDialog(null,false,true))
                Check(invoice.FileName=="invoice.pdf"&&report.FileName=="report.pdf","Default invoice/report filenames changed.");
            Console.WriteLine("PASS PDF save presentation: four invoice/report language cases; title/filter, explicit-language feedback, extension and filename protection; no dialogs shown, no files, SQL or desktop input.");
        }

        internal static async Task RunPrinterAsync()
        {
            if(!PrinterSettings.InstalledPrinters.Cast<string>().Any(name=>string.Equals(name,"Microsoft Print to PDF",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Microsoft Print to PDF is unavailable; real printer verification cannot pass.");
            string directory=Path.Combine(@"C:\PharmacyQA\work","pdf-printer-completion-"+Guid.NewGuid().ToString("N"));
            if(Directory.Exists(directory))throw new IOException("Fresh PDF printer fixture already exists.");
            Directory.CreateDirectory(directory);
            Console.WriteLine("PDF_PRINTER_OUTPUT="+directory);
            string first=Path.Combine(directory,"new.pdf"),replacement=Path.Combine(directory,"replaced.pdf");
            await Print(first,"QA NEW PDF COMPLETION");
            File.Copy(first,replacement);
            byte[] before=File.ReadAllBytes(replacement);
            await Print(replacement,"QA REPLACEMENT PDF COMPLETION");
            Check(!File.ReadAllBytes(replacement).SequenceEqual(before),"Real printer replacement retained the first document.");
            Check(!Directory.GetFiles(directory,".pharmacy-pdf-*.pdf").Any(),"Real printer verification left temporary output.");
            Console.WriteLine("PASS real Windows PDF completion: new and existing target; no SQL, authentication or UI input; reader/render validation separate.");
        }

        private static async Task Print(string target,string caption)
        {
            using(var document=new PrintDocument())
            using(var font=new Font("Arial",14F))
            {
                document.PrintPage+=(sender,args)=>
                {
                    args.Graphics.DrawString(caption,font,Brushes.Black,args.MarginBounds.Left,args.MarginBounds.Top);
                    args.HasMorePages=false;
                };
                Check(await PdfExportHelper.WritePdfCompleteFileAsync(target,path=>PdfExportHelper.PrintReportPdf(document,path,caption)),"Real PDF printer output was not committed.");
            }
        }

        internal static async Task RunAsync()
        {
            string directory=Path.Combine(@"C:\PharmacyQA\work","pdf-completion-"+Guid.NewGuid().ToString("N"));
            if(Directory.Exists(directory))throw new IOException("Fresh PDF fixture already exists.");
            Directory.CreateDirectory(directory);
            Console.WriteLine("Owned PDF completion fixtures: "+directory);
            await VerifyActiveProducer(directory,true);
            await VerifyActiveProducer(directory,false);
            string target=Path.Combine(directory,"replace.pdf");File.WriteAllBytes(target,Previous);
            Check(await PdfExportHelper.WritePdfCompleteFileAsync(target,path=>File.WriteAllBytes(path,First)),"Complete PDF did not replace prior file.");
            Check(File.ReadAllBytes(target).SequenceEqual(First),"Replacement changed complete bytes.");
            string fresh=Path.Combine(directory,"new.pdf");
            Check(await PdfExportHelper.WritePdfCompleteFileAsync(fresh,path=>File.WriteAllBytes(path,First)),"New completed PDF was not committed.");
            await VerifyLockedDestination(directory);
            await VerifyRevokedDestination(directory);
            target=Path.Combine(directory,"timeout.pdf");File.WriteAllBytes(target,Previous);
            bool timedOut=false;
            try{await PdfExportHelper.WritePdfCompleteFileAsync(target,path=>File.WriteAllText(path,"%PDF-1.4\nincomplete"),completionTimeoutMilliseconds:80);}
            catch(IOException){timedOut=true;}
            Check(timedOut&&File.ReadAllBytes(target).SequenceEqual(Previous),"Incomplete PDF timeout replaced previous report.");
            bool wrote=false;
            Check(!await PdfExportHelper.WritePdfCompleteFileAsync(target,path=>wrote=true,()=>false)&&!wrote,"Initial revocation invoked producer.");
            Check(!Directory.GetFiles(directory,".pharmacy-pdf-*.pdf").Any(),"Completed fixture left temporary PDF files.");
            Console.WriteLine("PASS PDF completion: active writable producer, exact replacement/new bytes, locked destination retry, revocation, incomplete timeout and cleanup; marker-only fixtures, no printer/UI/SQL.");
        }

        private static async Task VerifyActiveProducer(string directory,bool existing)
        {
            string target=Path.Combine(directory,existing?"active-producer-replace.pdf":"active-producer-new.pdf");
            if(existing)File.WriteAllBytes(target,Previous);
            FileStream producer=null;
            try
            {
                Task<bool> pending=PdfExportHelper.WritePdfCompleteFileAsync(target,path=>
                {
                    producer=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);
                    producer.Write(First,0,First.Length);producer.Flush(true);
                },completionTimeoutMilliseconds:10000);
                bool finishedWhileWriting=pending.IsCompleted;
                bool previousPreserved=existing?File.ReadAllBytes(target).SequenceEqual(Previous):!File.Exists(target);
                producer.Write(Last,0,Last.Length);producer.Flush(true);producer.Dispose();producer=null;
                bool saved=await pending;
                Console.WriteLine("Active producer: existing="+existing+"; finishedBeforeClose="+finishedWhileWriting+"; previousPreserved="+previousPreserved+"; savedAfterClose="+saved);
                Check(!finishedWhileWriting&&previousPreserved,"PDF was committed while a writable producer handle remained open.");
                Check(saved&&File.ReadAllBytes(target).SequenceEqual(First.Concat(Last)),"PDF did not retain the producer's final bytes after close.");
            }
            finally{producer?.Dispose();}
        }

        private static async Task VerifyLockedDestination(string directory)
        {
            string target=Path.Combine(directory,"locked.pdf");File.WriteAllBytes(target,Previous);
            Task<bool> pending;
            using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                pending=PdfExportHelper.WritePdfCompleteFileAsync(target,path=>File.WriteAllBytes(path,First),completionTimeoutMilliseconds:10000);
                Check(!pending.IsCompleted&&File.ReadAllBytes(target).SequenceEqual(Previous),"Locked destination was overwritten or completed early.");
            }
            Check(await pending&&File.ReadAllBytes(target).SequenceEqual(First),"PDF did not recover after destination unlock.");
        }

        private static async Task VerifyRevokedDestination(string directory)
        {
            string target=Path.Combine(directory,"revoked.pdf");File.WriteAllBytes(target,Previous);
            bool current=true;Task<bool> pending;
            using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                pending=PdfExportHelper.WritePdfCompleteFileAsync(target,path=>File.WriteAllBytes(path,First),()=>current,10000);
                Check(!pending.IsCompleted,"Revocation fixture did not enter destination retry.");current=false;
            }
            Check(!await pending&&File.ReadAllBytes(target).SequenceEqual(Previous),"Revoked PDF export replaced the previous report.");
        }

        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
