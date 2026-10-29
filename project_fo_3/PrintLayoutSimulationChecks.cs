using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;

namespace project_fo_3
{
    // محاكاة بصرية داخلية لمخرج الطباعة: لا تفتح حوار طابعة ولا تنشئ مستنداً أو اتصال قاعدة بيانات.
    internal static class PrintLayoutSimulationChecks
    {
        internal static void Run(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("مسار مخرجات محاكاة الطباعة مطلوب.", "outputDirectory");
            }

            Directory.CreateDirectory(outputDirectory);
            AppFonts.Initialize();
            VerifyIdentityMatrix(outputDirectory);
            VerifyIdentityDialog(outputDirectory);
            VerifyCommerceRawSummaryValues();
            VerifyInvoiceHeaderValues();
            if (DataTablePrintPreview.ShouldUseLandscape(new ReportPrintOptions { Orientation = ReportPageOrientation.Portrait }, 14) ||
                !DataTablePrintPreview.ShouldUseLandscape(new ReportPrintOptions { Orientation = ReportPageOrientation.Landscape }, 2) ||
                !DataTablePrintPreview.ShouldUseLandscape(null, 6) || DataTablePrintPreview.ShouldUseLandscape(null, 5))
                throw new InvalidOperationException("خيار اتجاه التقرير لا يتحكم في صفحة الطباعة كما هو متوقع.");

            Render(outputDirectory, "rtl-a4", new Size(1240, 1754), new Margins(60, 60, 60, 60), true);
            Render(outputDirectory, "ltr-a4", new Size(1240, 1754), new Margins(60, 60, 60, 60), false);
            RenderReturnCreditNote(outputDirectory, false);
            RenderReturnCreditNote(outputDirectory, true);
            RenderReturnCreditNote(outputDirectory, false, true);
            RenderReturnCreditNote(outputDirectory, true, true);
            Render(outputDirectory, "rtl-thermal-80", new Size(630, 1400), new Margins(20, 20, 20, 20), true);
            Render(outputDirectory, "rtl-thermal-58", new Size(456, 1400), new Margins(16, 16, 16, 16), true);
            string logoPath = Path.Combine(outputDirectory, "qa-report-logo.png");
            using (var logo = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(logo))
            {
                graphics.Clear(Color.FromArgb(25, 112, 85));
                using (var font = new Font(FontFamily.GenericSansSerif, 20F, FontStyle.Bold))
                    graphics.DrawString("QA", font, Brushes.White, new PointF(4, 14));
                logo.Save(logoPath, ImageFormat.Png);
            }
            RenderReturnCreditNote(outputDirectory, false, true, true);
            RenderReturnCreditNote(outputDirectory, true, true, true);
            RenderReportVariant(outputDirectory, "report-branded-portrait-compact", new Size(1240, 1754),
                ReportPageOrientation.Portrait, ReportDensity.Compact, logoPath);
            RenderReportVariant(outputDirectory, "report-branded-landscape-detailed", new Size(1754, 1240),
                ReportPageOrientation.Landscape, ReportDensity.Standard, logoPath);
            RenderReportVariant(outputDirectory, "report-branded-long-address", new Size(840, 1188),
                ReportPageOrientation.Portrait, ReportDensity.Standard, logoPath, true);
        }

        private static void VerifyIdentityMatrix(string outputDirectory)
        {
            int checks = 0;
            foreach (bool rtl in new[] { true, false })
            foreach (bool wide in new[] { true, false })
            {
                string name = "identity-" + (rtl ? "rtl" : "ltr") + (wide ? "-wide" : "-tall");
                string logoPath = Path.Combine(outputDirectory, name + "-logo.png");
                using (var logo = new Bitmap(wide ? 280 : 40, wide ? 40 : 280))
                using (Graphics g = Graphics.FromImage(logo))
                { g.Clear(Color.Magenta); logo.Save(logoPath, ImageFormat.Png); }
                var options = new ReportPrintOptions {
                    CompanyName = string.Join(" ", Enumerable.Repeat(rtl ? "صيدلية الرعاية الصحية المركزية" : "Central Healthcare Pharmacy", 6)),
                    Address = "Aden / عدن", Mobile = "700000000", TaxNumber = "000123456789",
                    CommercialRegistration = "CR-001", Website = "pharmacy.example", LogoPath = logoPath,
                    ShowCompanyName = true, ShowAddress = true, ShowMobile = true, ShowTaxNumber = true,
                    ShowCommercialRegistration = true, ShowWebsite = true, ShowLogo = true
                };
                using (DataTable sample = CreateSampleTable())
                using (var layout = new DataTablePrintPreview.TablePrintLayout("Identity QA", "", sample, rtl, options))
                using (var bitmap = new Bitmap(840, 1188))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    var args = new PrintPageEventArgs(g, new Rectangle(70,70,700,1048), new Rectangle(0,0,840,1188), new PageSettings());
                    layout.PrintPage(null, args);
                    if (layout.LastIdentityHeaderHeight <= 100 || layout.RenderedRowCount != sample.Rows.Count || args.HasMorePages)
                        throw new InvalidOperationException("Identity matrix lost text or rows: " + name);
                    int minX=840,minY=1188,maxX=-1,maxY=-1;
                    for(int y=70;y<140;y++) for(int x=70;x<770;x++)
                        if(bitmap.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb()) { minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y); }
                    int width=maxX-minX+1,height=maxY-minY+1;
                    if(width<=0 || height<=0 || (wide ? width<height*6 : height<width*6))
                        throw new InvalidOperationException("Logo aspect ratio or placement failed: " + name);
                    bitmap.Save(Path.Combine(outputDirectory,name+".png"),ImageFormat.Png); checks++;
                }
                foreach(string property in new[]{"CompanyName","Address","Mobile","TaxNumber","CommercialRegistration","Website","LogoPath"})
                {
                    var hidden=options.Copy();string flag=property=="LogoPath"?"ShowLogo":"Show"+property;
                    typeof(ReportPrintOptions).GetProperty(flag,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(hidden,false,null);
                    var empty=hidden.Copy();typeof(ReportPrintOptions).GetProperty(property,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(empty,"",null);
                    using(var a=RenderIdentityForComparison(hidden,rtl)) using(var b=RenderIdentityForComparison(empty,rtl))
                    {
                        for(int y=0;y<a.Height;y++) for(int x=0;x<a.Width;x++)
                            if(a.GetPixel(x,y)!=b.GetPixel(x,y)) throw new InvalidOperationException("Hidden identity field leaked: "+property);
                    }
                    checks++;
                }
            }
            foreach(bool rtl in new[]{true,false})
            {
                var hidden=new ReportPrintOptions {CompanyName="Hidden pharmacy",Address="Hidden address",Mobile="777",TaxNumber="123",CommercialRegistration="CR",Website="hidden.example",LogoPath=Path.Combine(outputDirectory,"identity-rtl-wide-logo.png")};
                using(var actual=RenderIdentityForComparison(hidden,rtl))
                using(var baseline=RenderIdentityForComparison(new ReportPrintOptions(),rtl))
                {
                    for(int y=0;y<actual.Height;y++) for(int x=0;x<actual.Width;x++)
                        if(actual.GetPixel(x,y)!=baseline.GetPixel(x,y)) throw new InvalidOperationException("Hidden identity retained text, logo or space.");
                    actual.Save(Path.Combine(outputDirectory,rtl?"identity-hidden-rtl.png":"identity-hidden-ltr.png"),ImageFormat.Png);
                }
                checks++;
            }
            using(DataTable sample=CreateSampleTable())
            using(var layout=new DataTablePrintPreview.TablePrintLayout("QA","",sample,true,new ReportPrintOptions {CompanyName=string.Join(" ",Enumerable.Repeat("اسم المنشأة الطويل",500)),ShowCompanyName=true}))
            using(var bitmap=new Bitmap(300,300)) using(Graphics g=Graphics.FromImage(bitmap))
            {
                bool rejected=false;
                try { layout.PrintPage(null,new PrintPageEventArgs(g,new Rectangle(20,20,260,260),new Rectangle(0,0,300,300),new PageSettings())); }
                catch(InvalidOperationException ex) { rejected=ex.Message.Contains("ترويسة التقرير أطول"); }
                if(!rejected || layout.RenderedRowCount!=0) throw new InvalidOperationException("Oversized identity must reject before rows.");checks++;
            }
            Console.WriteLine("Identity matrix passed. Cases={0}; RTL/LTR; long name; wide/tall logo; seven hidden fields; entirely hidden identity; oversized rejection; PersistentWrites=0.",checks);
        }

        private static Bitmap RenderIdentityForComparison(ReportPrintOptions options,bool rtl)
        {
            var bitmap=new Bitmap(840,1188);
            try {
                using(DataTable sample=CreateSampleTable()) using(var layout=new DataTablePrintPreview.TablePrintLayout("Identity QA","",sample,rtl,options)) using(Graphics g=Graphics.FromImage(bitmap))
                {g.Clear(Color.White);layout.PrintPage(null,new PrintPageEventArgs(g,new Rectangle(70,70,700,1048),new Rectangle(0,0,840,1188),new PageSettings()));}
                return bitmap;
            } catch {bitmap.Dispose();throw;}
        }

        private static void VerifyIdentityDialog(string outputDirectory)
        {
            int cases=0;
            foreach(bool english in new[]{false,true}) foreach(bool show in new[]{false,true})
            {
                var profile=new PharmacyProfile {NameAr="صيدلية الرعاية — 500 mg",NameEn="Care Pharmacy — 500 mg",TaxNo="000123",CommercialRegNo="CR-001",Mobile="777000000",Address="عدن / Aden",Website="pharmacy.example",LogoPath=Path.Combine(outputDirectory,"identity-rtl-wide-logo.png")};
                using(var form=new ReportPrintOptionsDialog(profile,false,null,english))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();
                    foreach(string field in new[]{"CompanyName","Logo","TaxNumber","Registration","Mobile","Address","Website"})
                    {
                        var box=(System.Windows.Forms.CheckBox)form.Controls.Find("reportPrint"+field,true).Single();
                        if(!box.Enabled) throw new InvalidOperationException("Available identity field disabled: "+field);box.Checked=show;
                    }
                    ((AntdUI.Select)form.Controls.Find("reportPrintOrientation",true).Single()).SelectedIndex=2;
                    ((AntdUI.Select)form.Controls.Find("reportPrintDensity",true).Single()).SelectedIndex=1;
                    ((AntdUI.Button)form.Controls.Find("reportPrintPreview",true).Single()).PerformClick();
                    var selected=form.SelectedOptions;
                    if(form.DialogResult!=System.Windows.Forms.DialogResult.OK || selected==null || selected.CompanyName!=(english?profile.NameEn:profile.NameAr) || selected.TaxNumber!=profile.TaxNo || selected.CommercialRegistration!=profile.CommercialRegNo || selected.Mobile!=profile.Mobile || selected.Address!=profile.Address || selected.Website!=profile.Website || selected.LogoPath!=profile.LogoPath || selected.Orientation!=ReportPageOrientation.Landscape || selected.Density!=ReportDensity.Compact)
                        throw new InvalidOperationException("Dialog identity values changed.");
                    if(new[]{selected.ShowCompanyName,selected.ShowLogo,selected.ShowTaxNumber,selected.ShowCommercialRegistration,selected.ShowMobile,selected.ShowAddress,selected.ShowWebsite}.Any(value=>value!=show)) throw new InvalidOperationException("Dialog choices did not map.");
                    using(var bitmap=RenderIdentityForComparison(selected,!english)) bitmap.Save(Path.Combine(outputDirectory,"identity-dialog-"+(english?"ltr":"rtl")+(show?"-shown":"-hidden")+".png"),ImageFormat.Png);
                    cases++;
                }
            }
            foreach(bool english in new[]{false,true})
            {
                foreach(bool unavailable in new[]{false,true})
                using(var form=new ReportPrintOptionsDialog(unavailable?null:new PharmacyProfile(),false,new ReportPrintOptions {ShowCompanyName=true,ShowLogo=true,ShowTaxNumber=true,ShowCommercialRegistration=true,ShowMobile=true,ShowAddress=true,ShowWebsite=true},english))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();
                    foreach(string field in new[]{"CompanyName","Logo","TaxNumber","Registration","Mobile","Address","Website"})
                        if(form.Controls.Find("reportPrint"+field+"Unavailable",true).Length!=1 || form.Controls.Find("reportPrint"+field,true).Length!=0)
                            throw new InvalidOperationException("Unavailable field exposed a selectable control: "+field);
                    ((AntdUI.Button)form.Controls.Find("reportPrintPreview",true).Single()).PerformClick();
                    if(form.DialogResult!=System.Windows.Forms.DialogResult.OK || form.SelectedOptions==null || form.SelectedOptions.HasVisibleIdentity || new[]{form.SelectedOptions.ShowCompanyName,form.SelectedOptions.ShowLogo,form.SelectedOptions.ShowTaxNumber,form.SelectedOptions.ShowCommercialRegistration,form.SelectedOptions.ShowMobile,form.SelectedOptions.ShowAddress,form.SelectedOptions.ShowWebsite}.Any(value=>value))
                        throw new InvalidOperationException("Missing identity prevented unbranded report or retained defaults.");
                    cases++;
                }
                using(var form=new ReportPrintOptionsDialog(new PharmacyProfile {NameAr="صيدلية",NameEn="Pharmacy"},false,null,english))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();
                    ((AntdUI.Button)form.Controls.Find("reportPrintCancel",true).Single()).PerformClick();
                    if(form.SelectedOptions!=null || form.DialogResult==System.Windows.Forms.DialogResult.OK || form.Visible)
                        throw new InvalidOperationException("Cancel accepted report options or left dialog visible.");
                    cases++;
                }
            }
            foreach(bool english in new[]{false,true})
            {
                string path=Path.Combine(outputDirectory,english?"invalid-ltr.png":"invalid-rtl.png");
                File.Copy(Path.Combine(outputDirectory,"identity-rtl-wide-logo.png"),path,true);
                using(var form=new ReportPrintOptionsDialog(new PharmacyProfile {NameAr="صيدلية",NameEn="Pharmacy",LogoPath=path},false,null,english))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();
                    var logo=(System.Windows.Forms.CheckBox)form.Controls.Find("reportPrintLogo",true).Single();
                    if(!logo.Enabled || !logo.Checked) throw new InvalidOperationException("Valid logo disabled.");
                    File.WriteAllText(path,"Not an image");
                    var button=(AntdUI.Button)form.Controls.Find("reportPrintPreview",true).Single();button.PerformClick();
                    if(form.SelectedOptions!=null || !form.Visible) throw new InvalidOperationException("Corrupt logo accepted.");
                    logo.Checked=false;button.PerformClick();
                    if(form.SelectedOptions==null || form.SelectedOptions.ShowLogo || form.DialogResult!=System.Windows.Forms.DialogResult.OK) throw new InvalidOperationException("Invalid logo recovery failed.");cases++;
                }
                using(var form=new ReportPrintOptionsDialog(new PharmacyProfile {LogoPath=path},false,null,english))
                {if(form.Controls.Find("reportPrintLogoUnavailable",true).Length!=1) throw new InvalidOperationException("Invalid logo selectable.");cases++;}
            }
            Console.WriteLine("Identity dialog passed. Cases={0}; actual controls and preview button; exact profile values and seven choices; RTL/LTR; PersistentWrites=0.",cases);
        }

        private static void VerifyCommerceRawSummaryValues()
        {
            string original=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                using(var sales=new Billing(true,english))
                using(var purchases=new Purchases(true,english))
                {
                    Properties.Setings.Default.ThemeMode=theme;
                    var salesNames=new[]{"CustomerNameLabel","CustomerPhoneLabel","CustomerAddressLabel","CustomerIdLabel","EmployeeLabel","EmployeeIdLabel","TimingLabel"};
                    var purchaseNames=new[]{"lblPurchaseSupplier","lblPurchaseEmployee","lblPurchaseTiming"};
                    foreach(var pair in new[]{new{Form=(System.Windows.Forms.Form)sales,Names=salesNames},new{Form=(System.Windows.Forms.Form)purchases,Names=purchaseNames}})
                    {
                        var labels=pair.Names.Select(name=>(System.Windows.Forms.Label)GetHeaderLabel(pair.Form,name)).ToArray();
                        const string raw="Remember Me Labs A&B عربي 500 mg: Save";
                        foreach(var label in labels)label.Text=raw;
                        int changes=0;foreach(var label in labels)label.TextChanged+=(s,e)=>changes++;
                        for(int repeat=0;repeat<2;repeat++)
                        {
                            ArabicUi.Apply(pair.Form);UiLanguageCatalog.Apply(pair.Form);UiTheme.Apply(pair.Form);
                            if(labels.Any(label=>label.Text!=raw)||changes!=0)throw new InvalidOperationException("Commerce raw summary changed during language/theme reapplication.");
                            if(labels.Any(label=>label.UseMnemonic))throw new InvalidOperationException("Commerce raw summary interprets literal ampersands as mnemonics.");
                        }
                    }
                }
            }
            finally{Properties.Setings.Default.ThemeMode=original;}
            Console.WriteLine("PASS commerce raw summary: ten business labels, two themes/directions, exact mixed data and literal ampersands, zero translation change events; no SQL/native input.");
        }
        private static void VerifyInvoiceHeaderValues()
        {
            string theme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string name in new[] { "شركة A:B", "Customer Name: Labs", "اسم العميل: مؤسسة 500 mg" })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    const string id = "C:123";
                    const string phone = "+967:777777777";
                    const string address = "شارع A:B - Customer Address: 500 mg";
                    const string employee = "Cashier: أحمد A:B";
                    const string employeeId = "E:12";
                    const string timing = "2026-10-05 13:45:06";
                    using (var sales = new Billing(true, english))
                    using (var purchase = new Purchases(true, english))
                    {
                        SetHeaderLabel(sales, "CustomerIdLabel", (english ? "Customer Id: " : "رقم العميل: ") + id);
                        SetHeaderLabel(sales, "CustomerNameLabel", (english ? "Customer Name: " : "اسم العميل: ") + name);
                        SetHeaderLabel(sales, "CustomerPhoneLabel", (english ? "Customer Phone: " : "هاتف العميل: ") + phone);
                        SetHeaderLabel(sales, "CustomerAddressLabel", (english ? "Customer Address: " : "عنوان العميل: ") + address);
                        SetHeaderLabel(sales, "EmployeeIdLabel", (english ? "Employee Id: " : "رقم الكاشير: ") + employeeId);
                        SetHeaderLabel(sales, "EmployeeLabel", (english ? "Cashier: " : "الكاشير: ") + employee);
                        SetHeaderLabel(sales, "TimingLabel", (english ? UiLanguageCatalog.Translate("التوقيت") : "التوقيت") + ": " + timing);
                        var header = sales.CaptureInvoicePrintHeader();
                        RequireHeaderText(header.PartyId, id, "sales id");
                        RequireHeaderText(header.PartyName, name, "sales name");
                        RequireHeaderText(header.PartyPhone, phone, "sales phone");
                        RequireHeaderText(header.PartyAddress, address, "sales address");
                        RequireHeaderText(header.EmployeeName, employee, "sales cashier");
                        RequireHeaderText(header.EmployeeId, employeeId, "sales cashier id");
                        RequireHeaderText(header.Timing, timing, "sales timing");
                        const string supplier = "المورد: Supplier: شركة A:B";
                        SetHeaderLabel(purchase, "lblPurchaseSupplier", (english ? "Supplier: " : "المورد: ") + supplier);
                        SetHeaderLabel(purchase, "lblPurchaseEmployee", (english ? "Employee: " : "الموظف: ") + employee);
                        SetHeaderLabel(purchase, "lblPurchaseTiming", (english ? "Date: " : "التاريخ: ") + timing);
                        var purchaseHeader = purchase.CaptureInvoicePrintHeader();
                        RequireHeaderText(purchaseHeader.PartyName, supplier, "purchase supplier");
                        RequireHeaderText(purchaseHeader.EmployeeName, employee, "purchase employee");
                        RequireHeaderText(purchaseHeader.Timing, timing, "purchase timing");
                        RequireHeaderText(GetHeaderLabel(sales, "CustomerNameLabel").Text,
                            (english ? "Customer Name: " : "اسم العميل: ") + name, "source label unchanged");
                        SetHeaderLabel(sales, "CustomerNameLabel", "Customer Name Labs A:B");
                        RequireHeaderText(sales.CaptureInvoicePrintHeader().PartyName, "Customer Name Labs A:B", "unrecognized caption retained");
                        foreach (string caption in new[] { UiLanguageCatalog.Translate("التوقيت"), "Timing", "Date", "التاريخ", "التوقيت" })
                        {
                            string display = caption + ": " + timing;
                            SetHeaderLabel(sales, "TimingLabel", display);
                            SetHeaderLabel(purchase, "lblPurchaseTiming", display);
                            RequireHeaderText(sales.CaptureInvoicePrintHeader().Timing, timing, "sales actual/legacy time caption");
                            RequireHeaderText(purchase.CaptureInvoicePrintHeader().Timing, timing, "purchase actual/legacy time caption");
                            RequireHeaderText(GetHeaderLabel(sales, "TimingLabel").Text, display, "sales timing label unchanged");
                            RequireHeaderText(GetHeaderLabel(purchase, "lblPurchaseTiming").Text, display, "purchase timing label unchanged");
                        }
                        var issued = new DateTime(2026, 10, 5, 10, 45, 0, DateTimeKind.Utc);
                        var saved = new CommercialInvoicePrintSnapshot(Guid.NewGuid(), 123, issued, "YER", 1m,
                            id, name, phone, address, employee, "", new InvoiceTotals(), new CommercialInvoicePrintLine[0]);
                        typeof(Billing).GetField("_currentCommercialInvoiceSnapshot",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(sales, saved);
                        var commercial = sales.CaptureInvoicePrintHeader();
                        RequireHeaderText(commercial.PartyId, id, "saved id");
                        RequireHeaderText(commercial.PartyName, name, "saved name");
                        RequireHeaderText(commercial.PartyPhone, phone, "saved phone");
                        RequireHeaderText(commercial.PartyAddress, address, "saved address");
                        RequireHeaderText(commercial.EmployeeName, employee, "saved cashier");
                        RequireHeaderText(commercial.EmployeeId, string.Empty, "saved cashier has no recorded ID");
                        SetHeaderLabel(sales, "EmployeeIdLabel", "Employee Id: OTHER-USER");
                        SetHeaderLabel(sales, "EmployeeLabel", "Employee Name: Different user");
                        var changedCashier = sales.CaptureInvoicePrintHeader();
                        RequireHeaderText(changedCashier.EmployeeId, string.Empty, "current user ID must not enter saved invoice");
                        RequireHeaderText(changedCashier.EmployeeName, employee, "original cashier after current user changed");
                        RequireHeaderText(commercial.Timing, issued.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), "saved issue time");
                    }
                }
            }
            finally { Properties.Setings.Default.ThemeMode = theme; }
            Console.WriteLine("PASS invoice header values: sales/purchase production capture, repeated captions, mixed Arabic, colons, IDs, timestamps, immutable saved commercial source, source labels retained; both themes/directions; no SQL or physical printer.");
        }

        private static System.Windows.Forms.Control GetHeaderLabel(System.Windows.Forms.Control form, string field)
        {
            return (System.Windows.Forms.Control)form.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
        }

        private static void SetHeaderLabel(System.Windows.Forms.Control form, string field, string value)
        {
            GetHeaderLabel(form, field).Text = value;
        }

        private static void RequireHeaderText(string actual, string expected, string field)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Invoice print header changed " + field + ": [" + actual + "] expected [" + expected + "].");
        }
        private static void RenderReportVariant(string outputDirectory, string name, Size pageSize,
            ReportPageOrientation orientation, ReportDensity density, string logoPath, bool longIdentity = false)
        {
            const string addressPart = "عدن، شارع الصيدليات، بجوار المستشفى المركزي، مبنى الرعاية الصحية، الطابق الأول، مدخل العيادات الخارجية، قسم صرف الوصفات. ";
            using (DataTable sample = CreateSampleTable())
            using (var layout = new DataTablePrintPreview.TablePrintLayout("تقرير الأصناف", "فترة تجريبية — بيانات وهمية", sample,
                true, new ReportPrintOptions
                {
                    Orientation = orientation, Density = density,
                    CompanyName = "صيدلية المهندس", TaxNumber = "123456789", CommercialRegistration = "QA-100",
                    Mobile = "700000000", Address = longIdentity
                        ? addressPart + addressPart + addressPart
                        : "عدن", Website = longIdentity
                        ? "pharmacy.example/branches/aden/central-hospital/outpatient-prescriptions/customer-service"
                        : "pharmacy.example", LogoPath = logoPath,
                    ShowCompanyName = true, ShowTaxNumber = true, ShowCommercialRegistration = true,
                    ShowMobile = true, ShowAddress = true, ShowWebsite = true, ShowLogo = true
                }))
            {
                bool more;
                int page = 0;
                do
                {
                    if (++page > 24) throw new InvalidOperationException("تجاوز التقرير المطبوع الحد الآمن للصفحات.");
                    using (var bitmap = new Bitmap(pageSize.Width, pageSize.Height))
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        Rectangle margins = new Rectangle(70, 70, pageSize.Width - 140, pageSize.Height - 140);
                        var args = new PrintPageEventArgs(graphics, margins,
                            new Rectangle(0, 0, pageSize.Width, pageSize.Height), new PageSettings());
                        layout.PrintPage(null, args);
                        if (longIdentity) Console.WriteLine("Long identity header height={0}.", layout.LastIdentityHeaderHeight);
                        if (longIdentity && layout.LastIdentityHeaderHeight <= 120)
                            throw new InvalidOperationException("ترويسة التقرير الطويلة لم تتكيف مع التفاف النص. Height=" + layout.LastIdentityHeaderHeight);
                        if (!HasVisibleOutput(bitmap)) throw new InvalidOperationException("تقرير الهوية فارغ: " + name);
                        bitmap.Save(Path.Combine(outputDirectory, name + "-page-" + page + ".png"), ImageFormat.Png);
                        more = args.HasMorePages;
                    }
                } while (more);
                Console.WriteLine("Report print variant passed. Profile={0}; Pages={1}; Direction=RTL; PersistentWrites=0.", name, page);
            }
        }

        private static void RenderReturnCreditNote(string outputDirectory, bool english, bool longReason = false, bool brandedManyLines = false)
        {
            var header=new CommercialReturnPrintHeader { ReturnInvoiceNumber=4102,OriginalInvoiceNumber=4019,
                IssuedAtUtc=new DateTime(2026,10,1,12,30,0,DateTimeKind.Utc),RefundMethod="OriginalMethod",
                ReturnReason="عبوة سليمة — Batch 500 mg",IssuedBy="كاشير",CurrencyCode="YER" };
            if(longReason) header.ReturnReason=string.Join(" ",System.Linq.Enumerable.Repeat("عبوة سليمة — Batch 500 mg",18));
            var lines=new[]{new CommercialReturnPrintLine { MedicineName="باراسيتامول 500 mg",Quantity=3m,
                GrossTotal=1500m,DiscountTotal=249.50m,TaxRate=0m,TaxTotal=0m,NetTotal=1250.50m }};
            if(brandedManyLines) lines=System.Linq.Enumerable.Range(1,150).Select(index=>new CommercialReturnPrintLine {
                MedicineName="صنف "+index+" — 500 mg",Quantity=1m,GrossTotal=1500m,DiscountTotal=249.50m,
                TaxRate=0m,TaxTotal=0m,NetTotal=1250.50m }).ToArray();
            string logoPath=Path.Combine(outputDirectory,"qa-report-logo.png");
            var options=brandedManyLines?new ReportPrintOptions { CompanyName=english?"Engineer Pharmacy":"صيدلية المهندس",
                TaxNumber="000123",CommercialRegistration="CR-001",Mobile="777000000",Address="عدن",Website="pharmacy.example",
                LogoPath=logoPath,ShowCompanyName=true,ShowTaxNumber=true,ShowCommercialRegistration=true,
                ShowMobile=true,ShowAddress=true,ShowWebsite=true,ShowLogo=true }:null;
            string profile=(english?"return-credit-note-ltr":"return-credit-note-rtl")+(brandedManyLines?"-branded-many-lines":longReason?"-long-reason":"-compact");
            using(var data=CommercialReturnPrintData.BuildItemTable(header,lines,english))
            using(var layout=new DataTablePrintPreview.TablePrintLayout(english?"Credit note":"إشعار دائن",
                CommercialReturnPrintData.BuildSubtitle(header,english),data,!english,options,
                CommercialReturnPrintData.BuildDocumentDetails(header,english)))
            {
                int page=0; bool more;
                do
                {
                    if(++page>12) throw new InvalidOperationException("Credit note simulation exceeded page limit.");
                    using(var bitmap=new Bitmap(1240,1754))
                    using(var graphics=Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        var args=new PrintPageEventArgs(graphics,new Rectangle(60,60,1120,1634),
                            new Rectangle(0,0,1240,1754),new PageSettings());
                        layout.PrintPage(null,args);
                        if(layout.LastDocumentDetailsHeight<40 || (longReason && layout.LastDocumentDetailsHeight<80))
                            throw new InvalidOperationException("Credit note metadata did not reserve its wrapped height.");
                        if(brandedManyLines && layout.LastIdentityHeaderHeight<=0)
                            throw new InvalidOperationException("Credit note continuation page lost company identity.");
                        if(!HasVisibleOutput(bitmap)) throw new InvalidOperationException("Empty credit note simulation.");
                        bitmap.Save(Path.Combine(outputDirectory,profile+"-page-"+page+".png"),ImageFormat.Png);
                        more=args.HasMorePages;
                    }
                } while(more);
                if(brandedManyLines ? page<2 : page!=1) throw new InvalidOperationException("Credit note pagination did not match the input size.");
                if(layout.RenderedRowCount!=lines.Length)
                    throw new InvalidOperationException("Credit note pagination skipped or repeated item rows.");
                layout.Reset();
                if(layout.RenderedRowCount!=0 || layout.LastIdentityHeaderHeight!=0 || layout.LastDocumentDetailsHeight!=0)
                    throw new InvalidOperationException("Credit note print retry did not reset page counters.");
                Console.WriteLine("Credit note simulation passed. Profile={0}; Pages={1}; Direction={2}; PersistentWrites=0.",profile,page,english?"LTR":"RTL");
            }
        }

        private static void Render(string outputDirectory, string profile, Size pageSize, Margins margins, bool rightToLeft)
        {
            using (DataTable sample = CreateSampleTable())
            using (DataTablePrintPreview.TablePrintLayout layout = new DataTablePrintPreview.TablePrintLayout(
                rightToLeft ? "محاكاة فاتورة صيدلية" : "Pharmacy invoice simulation",
                rightToLeft ? "صيدلية المهندس | الإجمالي 1,250.50 ر.ي | بيانات وهمية" : "Pharmacy | Total 1,250.50 YER | Synthetic data",
                sample,
                rightToLeft))
            {
                layout.Reset();
                bool hasMorePages = true;
                int page = 0;

                while (hasMorePages)
                {
                    page++;
                    if (page > 24)
                    {
                        throw new InvalidOperationException("تجاوزت محاكاة الطباعة الحد الآمن للصفحات.");
                    }

                    string outputPath = Path.Combine(outputDirectory, string.Format("print-layout-{0}-page-{1}.png", profile, page));
                    using (Bitmap image = new Bitmap(pageSize.Width, pageSize.Height))
                    using (Graphics graphics = Graphics.FromImage(image))
                    {
                        graphics.Clear(Color.White);

                        Rectangle pageBounds = new Rectangle(0, 0, pageSize.Width, pageSize.Height);
                        Rectangle marginBounds = new Rectangle(
                            margins.Left,
                            margins.Top,
                            pageSize.Width - margins.Left - margins.Right,
                            pageSize.Height - margins.Top - margins.Bottom);
                        PageSettings pageSettings = new PageSettings();
                        pageSettings.Margins = margins;
                        PrintPageEventArgs printArgs = new PrintPageEventArgs(graphics, marginBounds, pageBounds, pageSettings);

                        layout.PrintPage(null, printArgs);
                        if (!HasVisibleOutput(image))
                        {
                            throw new InvalidOperationException("لم تنتج محاكاة الطباعة محتوى مرئياً: " + profile);
                        }

                        image.Save(outputPath, ImageFormat.Png);
                        hasMorePages = printArgs.HasMorePages;
                    }
                }

                Console.WriteLine(
                    "Print layout simulation passed. Profile={0}; Pages={1}; Direction={2}; PersistentWrites=0.",
                    profile,
                    page,
                    rightToLeft ? "RTL" : "LTR");
            }
        }

        private static DataTable CreateSampleTable()
        {
            DataTable table = new DataTable("PrintLayoutSimulation");
            table.Columns.Add("الصنف");
            table.Columns.Add("الباركود");
            table.Columns.Add("الكمية");
            table.Columns.Add("سعر الوحدة");
            table.Columns.Add("الإجمالي");
            table.Columns.Add("ملاحظات");
            table.Columns.Add("CostCenterId");

            string[] medicines =
            {
                "باراسيتامول 500 mg",
                "أموكسيسيلين 250 mg",
                "فيتامين C 1000 mg",
                "شراب سعال للأطفال",
                "ضماد طبي معقم",
                "محلول ملحي 0.9%"
            };

            for (int index = 0; index < 18; index++)
            {
                decimal quantity = 1 + (index % 4);
                decimal unitPrice = 25.50m + (index * 3.75m);
                DataRow row = table.NewRow();
                row["الصنف"] = medicines[index % medicines.Length];
                row["الباركود"] = "6281000" + index.ToString("000000");
                row["الكمية"] = quantity.ToString("0.###");
                row["سعر الوحدة"] = unitPrice.ToString("N2");
                row["الإجمالي"] = (quantity * unitPrice).ToString("N2");
                row["ملاحظات"] = index % 3 == 0 ? "نص عربي وEnglish mixed" : "—";
                row["CostCenterId"] = "internal-" + index;
                table.Rows.Add(row);
            }

            return table;
        }

        private static bool HasVisibleOutput(Bitmap image)
        {
            const int stride = 8;
            int visiblePixels = 0;
            for (int y = 0; y < image.Height; y += stride)
            {
                for (int x = 0; x < image.Width; x += stride)
                {
                    Color pixel = image.GetPixel(x, y);
                    if (pixel.R < 242 || pixel.G < 242 || pixel.B < 242)
                    {
                        visiblePixels++;
                        if (visiblePixels >= 12)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}


