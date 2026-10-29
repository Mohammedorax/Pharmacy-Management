using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace project_fo_3
{
    // Candidate shared renderer. Values and logo pixels belong to one print job.
    internal sealed class InvoicePrintIdentity : IDisposable
    {
        private readonly string companyName;
        private readonly string[] details;
        private readonly Bitmap logo;
        private bool disposed;
        internal string CompanyName { get { return companyName; } }
        internal IReadOnlyList<string> Details { get { return Array.AsReadOnly(details); } }
        internal bool HasLogo { get { return logo != null; } }
        internal bool LeftToRight { get; private set; }

        private InvoicePrintIdentity(InvoicePrintIdentity source)
        {
            if (source.disposed) throw new ObjectDisposedException(nameof(InvoicePrintIdentity));
            companyName = source.companyName; LeftToRight = source.LeftToRight;
            details = source.details.ToArray();
            logo = source.logo == null ? null : new Bitmap(source.logo);
        }

        internal InvoicePrintIdentity Copy() { return new InvoicePrintIdentity(this); }

        internal InvoicePrintIdentity(ReportPrintOptions options, bool? leftToRight=null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var flow = new InvoicePrintFlow(leftToRight ?? UiDirection.IsLeftToRight); LeftToRight = flow.LeftToRight;
            companyName = options.ShowCompanyName ? options.CompanyName ?? string.Empty : string.Empty;
            var captured = new List<string>();
            if (options.ShowTaxNumber && !string.IsNullOrWhiteSpace(options.TaxNumber)) captured.Add(flow.Text("الرقم الضريبي: ", "Tax number: ") + options.TaxNumber);
            if (options.ShowCommercialRegistration && !string.IsNullOrWhiteSpace(options.CommercialRegistration)) captured.Add(flow.Text("السجل التجاري: ", "Commercial registration: ") + options.CommercialRegistration);
            if (options.ShowMobile && !string.IsNullOrWhiteSpace(options.Mobile)) captured.Add(flow.Text("الجوال: ", "Mobile: ") + options.Mobile);
            if (options.ShowAddress && !string.IsNullOrWhiteSpace(options.Address)) captured.Add(options.Address);
            if (options.ShowWebsite && !string.IsNullOrWhiteSpace(options.Website)) captured.Add(flow.Text("الموقع: ", "Website: ") + options.Website);
            details = captured.ToArray();
            if (options.ShowLogo && !string.IsNullOrWhiteSpace(options.LogoPath))
            {
                var file = new FileInfo(options.LogoPath);
                if (!file.Exists || file.Length > 10L * 1024 * 1024)
                    throw new InvalidOperationException("تعذر تحميل شعار المنشأة؛ اختر صورة صالحة لا تتجاوز 10 ميجابايت.");
                try
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(file.FullName)))
                    using (var image = Image.FromStream(stream))
                    {
                        if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 16000000)
                            throw new InvalidOperationException("أبعاد شعار المنشأة كبيرة جدًا؛ اختر صورة أصغر.");
                        logo = new Bitmap(image);
                    }
                }
                catch (Exception error) when (error is ArgumentException || error is OutOfMemoryException || error is IOException || error is UnauthorizedAccessException)
                { throw new InvalidOperationException("تعذر تحميل شعار المنشأة؛ راجع الصورة في الإعدادات.", error); }
            }
        }

        internal float Draw(Graphics graphics, RectangleF bounds, bool rightToLeft)
        {
            if (disposed) throw new ObjectDisposedException(nameof(InvoicePrintIdentity));
            if (string.IsNullOrWhiteSpace(companyName) && details.Length == 0 && logo == null) return 0;
            float logoSize = logo == null ? 0 : Math.Min(64, Math.Max(32, bounds.Width * .16f));
            float textWidth = bounds.Width - (logo == null ? 0 : logoSize + 12);
            if (textWidth < 80 || bounds.Height <= 0)
                throw new InvalidOperationException("مساحة الصفحة لا تكفي لبيانات المنشأة؛ عدّل الهوامش أو خيارات الإظهار.");
            using (var nameFont = AppFonts.Create(bounds.Width <= 320 ? 10 : 14, FontStyle.Bold))
            using (var detailFont = AppFonts.Create(8))
            using (var format = new StringFormat {
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.None,
                FormatFlags = rightToLeft ? StringFormatFlags.DirectionRightToLeft : 0
            })
            {
                float nameHeight = string.IsNullOrWhiteSpace(companyName) ? 0 :
                    (float)Math.Ceiling(graphics.MeasureString(companyName, nameFont, new SizeF(textWidth, 100000), format).Height);
                var heights = details.Select(line => (float)Math.Ceiling(graphics.MeasureString(line, detailFont, new SizeF(textWidth, 100000), format).Height)).ToArray();
                float height = Math.Max(nameHeight + heights.Sum(), logoSize) + 8;
                if (height > bounds.Height)
                    throw new InvalidOperationException("بيانات المنشأة أطول من مساحة الصفحة؛ قلّل البيانات المعروضة أو اختر ورقًا أكبر.");
                float x = bounds.Left + (rightToLeft || logo == null ? 0 : logoSize + 12);
                float y = bounds.Top;
                if (nameHeight > 0)
                {
                    graphics.DrawString(companyName, nameFont, Brushes.DarkSlateGray, new RectangleF(x, y, textWidth, nameHeight), format);
                    y += nameHeight;
                }
                for (int i = 0; i < details.Length; i++)
                {
                    graphics.DrawString(details[i], detailFont, Brushes.DimGray, new RectangleF(x, y, textWidth, heights[i]), format);
                    y += heights[i];
                }
                if (logo != null)
                {
                    float scale = Math.Min(logoSize / logo.Width, logoSize / logo.Height);
                    float width = logo.Width * scale, logoHeight = logo.Height * scale;
                    float logoX = rightToLeft ? bounds.Right - logoSize : bounds.Left;
                    graphics.DrawImage(logo, new RectangleF(logoX + (logoSize - width) / 2, bounds.Top + (logoSize - logoHeight) / 2, width, logoHeight));
                }
                return height;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (logo != null) logo.Dispose();
        }
    }
}
