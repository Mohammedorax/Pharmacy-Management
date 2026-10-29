using System;
using System.Data;
using System.Threading.Tasks;
using System.Threading;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class JournalDocumentDesktopService
    {
        internal static Task<JournalDocumentSnapshot> ReadAsync(int id)=>ReadAsync(id,CancellationToken.None);
        internal static async Task<JournalDocumentSnapshot> ReadAsync(int id,CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateJournalDocumentReadSession();
            var document=await session.Service.LoadAsync(new JournalDocumentReadRequest(id),session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();if(!session.IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة قراءة القيد المحفوظ.");
            return document;
        }
        internal static DataTable PrintLines(JournalDocumentSnapshot document)
            => PrintLines(document, false);
        internal static DataTable PrintLines(JournalDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            var table=new DataTable();
            table.Columns.Add("رقم الحساب",typeof(string));table.Columns.Add("اسم الحساب",typeof(string));
            table.Columns.Add("مدين محلي",typeof(decimal));table.Columns.Add("دائن محلي",typeof(decimal));
            table.Columns.Add("مركز التكلفة",typeof(string));table.Columns.Add("البيان",typeof(string));
            foreach(var line in document.Lines)table.Rows.Add(Db(line.AccountCode),Db(line.AccountName),line.Debit,line.Credit,Db(line.CostCenterName),Db(line.Description));
            if(english)foreach(DataColumn column in table.Columns)column.Caption=UiLanguageCatalog.Translate(column.ColumnName);
            return table;
        }
        internal static string CurrencyCaption(JournalDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            string currency=document.SystemCurrencyCode;
            return (english?"Journal currency: ":"عملة القيد: ")+(currency??(english?"Unverified (historical record)":"غير موثقة (سجل قديم)"));
        }
        internal static string PrintDate(DateTime date) => date.ToString("yyyy'/'MM'/'dd", System.Globalization.CultureInfo.InvariantCulture);

        internal static string PrintTitle(JournalDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            string pattern=document.SourceType=="OpeningEntry"
                ? (english?UiLanguageCatalog.Translate("قيد افتتاحي رقم {0}"):"قيد افتتاحي رقم {0}")
                : (english?UiLanguageCatalog.Translate("قيد يومية رقم {0}"):"قيد يومية رقم {0}");
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,pattern,document.Id);
        }
        internal static string PrintSubtitle(JournalDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            return (english?UiLanguageCatalog.Translate("التاريخ"):"التاريخ")+": "+PrintDate(document.Date)+" | "+document.Description+" | "+CurrencyCaption(document,english);
        }

        internal static void VerifyPrintPresentation()
        {
            var previous=System.Threading.Thread.CurrentThread.CurrentCulture;
            int cases=0;
            try
            {
                foreach(string culture in new[]{"ar-SA","ar-EG","en-US","th-TH"})
                foreach(string kind in new[]{"Manual","OpeningEntry"})
                foreach(bool english in new[]{false,true})
                foreach(string currency in new[]{"YER",null})
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture);
                    var line=new JournalDocumentLine(1,"1000","الصندوق 500 mg",12.50m,0m,"=SUM(A1:A2)",null,null);
                    var document=new JournalDocumentSnapshot(92,new DateTime(2026,10,18),kind,null,"بيع صنف 500 mg =X","qa",new[]{line},currency);
                    string title=(english?(kind=="OpeningEntry"?"Opening entry no. ":"Journal entry no. "):(kind=="OpeningEntry"?"قيد افتتاحي رقم ":"قيد يومية رقم "))+"92";
                    string subtitle=(english?"Date: ":"التاريخ: ")+"2026/10/18 | بيع صنف 500 mg =X | "+
                        (english?"Journal currency: ":"عملة القيد: ")+(currency??(english?"Unverified (historical record)":"غير موثقة (سجل قديم)"));
                    if(PrintTitle(document,english)!=title||PrintSubtitle(document,english)!=subtitle)
                        throw new InvalidOperationException("Journal print heading language/date mismatch: "+culture+"; "+kind+"; english="+english);
                    using(var rows=PrintLines(document,english))
                    {
                        string[] names={"رقم الحساب","اسم الحساب","مدين محلي","دائن محلي","مركز التكلفة","البيان"};
                        string[] captions=english?new[]{"Account number","Account name","Local debit","Local credit","Cost center","Description"}:names;
                        for(int index=0;index<names.Length;index++)
                            if(rows.Columns[index].ColumnName!=names[index]||rows.Columns[index].Caption!=captions[index])
                                throw new InvalidOperationException("Journal print caption/key mismatch: "+names[index]);
                        if(rows.Rows.Count!=1||(string)rows.Rows[0][0]!="1000"||(string)rows.Rows[0][1]!="الصندوق 500 mg"||
                            (decimal)rows.Rows[0][2]!=12.50m||(decimal)rows.Rows[0][3]!=0m||!rows.Rows[0].IsNull(4)||
                            (string)rows.Rows[0][5]!="=SUM(A1:A2)"||document.Lines[0].Description!="=SUM(A1:A2)"||document.Description!="بيع صنف 500 mg =X")
                            throw new InvalidOperationException("Journal print localization changed business values.");
                    }
                    cases++;
                }
            }
            finally{System.Threading.Thread.CurrentThread.CurrentCulture=previous;}
            Console.WriteLine("PASS journal print presentation: "+cases+" culture/source/language/currency cases; localized headings/captions, Gregorian dates and unchanged business values; no UI/SQL/printer job.");
        }

        internal static async Task VerifyPrintPdfAsync(string token)
        {
            Guid parsed;
            if(!Guid.TryParseExact(token,"N",out parsed))throw new UnauthorizedAccessException("Owned PDF fixture token required.");
            string directory=System.IO.Path.Combine(@"C:\PharmacyQA\work","journal-print-pdf-"+token);
            if(System.IO.Directory.Exists(directory))throw new System.IO.IOException("PDF fixture directory must be new.");
            bool printerFound=false;
            foreach(string name in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                if(string.Equals(name,"Microsoft Print to PDF",StringComparison.OrdinalIgnoreCase))printerFound=true;
            if(!printerFound)throw new InvalidOperationException("Microsoft Print to PDF is unavailable.");
            System.IO.Directory.CreateDirectory(directory);
            var lines=new[]{
                new JournalDocumentLine(1,"1000","الصندوق Cash",12.50m,0m,"اختبار Test 500 mg",null,null),
                new JournalDocumentLine(2,"2000","المورد Supplier",0m,12.50m,"اختبار Test 500 mg",null,null)};
            var snapshot=new JournalDocumentSnapshot(92,new DateTime(2026,10,18),"Manual",null,"اختبار طباعة Test 500 mg","qa",lines,"YER");
            foreach(bool english in new[]{false,true})
            using(var rows=PrintLines(snapshot,english))
            using(var layout=new DataTablePrintPreview.TablePrintLayout(PrintTitle(snapshot,english),PrintSubtitle(snapshot,english),rows,!english))
            using(var document=new System.Drawing.Printing.PrintDocument())
            {
                document.DefaultPageSettings.Landscape=DataTablePrintPreview.ShouldUseLandscape(null,rows.Columns.Count);
                document.PrintPage+=layout.PrintPage;
                string target=System.IO.Path.Combine(directory,english?"journal-EN.pdf":"journal-AR.pdf");
                if(!await PdfExportHelper.WritePdfCompleteFileAsync(target,path=>PdfExportHelper.PrintReportPdf(document,path,"Journal QA 92"),null,30000,english))
                    throw new System.IO.IOException("Journal PDF fixture was not committed.");
                Console.WriteLine("Journal PDF fixture: "+target);
            }
            Console.WriteLine("PASS journal PDF producer: two languages, shared title/subtitle/columns and real Windows PDF printer; fixture data only, no UI/SQL.");
        }

        internal static void VerifyPrintDate()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            int cases = 0;
            try
            {
                foreach (string name in new[] { "ar-SA", "ar-EG", "en-US", "th-TH" })
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(name);
                    foreach (var sample in new[] {
                        new { Date = new DateTime(2026, 10, 18), Expected = "2026/10/18" },
                        new { Date = new DateTime(2024, 2, 29), Expected = "2024/02/29" } })
                    {
                        string actual = PrintDate(sample.Date);
                        if (actual != sample.Expected)
                            throw new InvalidOperationException("Journal print date changed calendar or separator under " + name + ": " + actual);
                        cases++;
                    }
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS journal print Gregorian date: " + cases + " culture/date cases; thread culture restored; no UI/SQL/printer job.");
        }
        private static object Db(string text)=>(object)text??DBNull.Value;
    }
}
