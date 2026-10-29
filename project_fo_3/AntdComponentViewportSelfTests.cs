using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // Owned component evidence only. Navigation never uses this viewport adapter.
    internal static class AntdComponentViewportSelfTests
    {
        internal static void Render(Form form, Size size, string path)
        {
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            if (!form.Visible) form.Show();
            OwnedTestWindowViewport.Resize(form, size);
            Console.WriteLine("PASS component native viewport: " + form.GetType().Name +
                "; requested=" + size + "; client=" + form.ClientSize + "; frame=" + form.Size +
                "; minimum=" + form.MinimumSize);
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
