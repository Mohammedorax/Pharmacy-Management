using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // Acceptance of the window geometry contract: opening and closing a modal dialog must not
    // change the size or the position of the screen that owns it. The check uses preview screens
    // so it runs without SQL, and it repeats the measurement after a theme and language pass.
    internal static class WindowStabilitySelfTests
    {
        internal static void RunAll()
        {
            try
            {
                RunChecks();
            }
            catch (Exception error)
            {
                Console.Out.WriteLine("Window stability failure: " + error.GetType().Name + ": " + error.Message);
                Console.Out.WriteLine(error.StackTrace);
                throw;
            }
        }

        private static void RunChecks()
        {
            var forms = new List<Form>();
            try
            {
                forms.Add(new Billing(true, true));
                forms.Add(new Purchases(true, true));
                forms.Add(new Medicine(true, true));
                forms.Add(new Manufacturer(true, true));
                forms.Add(new ChartOfAccountsForm(true, true));
                forms.Add(new VoucherForm("نقد", true, true));
                forms.Add(new ReceiptVoucherForm("نقد", true));
                forms.Add(new AccountingPeriodManagementForm(true));
                forms.Add(new CustomerSupplierManagementForm(false, true, true));
                forms.Add(new Login(false, true, true));
                forms.Add(new ReportColumnLayoutDialog(new DataGridViewColumn[0], true));
                forms.Add(new ReportTemplateNameDialog("تقرير تجريبي", true));

                int checkedForms = 0;
                foreach (Form form in forms)
                {
                    Prepare(form);
                    Application.DoEvents();
                    Size before = form.ClientSize;
                    Rectangle boundsBefore = form.Bounds;
                    FormWindowState stateBefore = form.WindowState;

                    ShowDialogAndClose(form);
                    Application.DoEvents();

                    Require(form.ClientSize == before, form.GetType().Name + " changed its client size after a modal dialog: " + before + " -> " + form.ClientSize);
                    Require(form.Bounds == boundsBefore, form.GetType().Name + " moved after a modal dialog: " + boundsBefore + " -> " + form.Bounds);
                    Require(form.WindowState == stateBefore, form.GetType().Name + " changed its window state after a modal dialog.");

                    // A second dialog and a layout pass must keep the same geometry.
                    ShowDialogAndClose(form);
                    form.PerformLayout();
                    Application.DoEvents();
                    Require(form.ClientSize == before, form.GetType().Name + " changed its client size after the layout pass: " + before + " -> " + form.ClientSize);
                    Require(form.Bounds == boundsBefore, form.GetType().Name + " moved after the layout pass: " + boundsBefore + " -> " + form.Bounds);
                    Require(form.WindowState == stateBefore, form.GetType().Name + " changed its window state after the layout pass.");
                    checkedForms++;
                }
                Console.WriteLine("PASS window stability: " + checkedForms +
                    " screens kept their client size, bounds and window state across modal dialogs and a layout pass; no SQL, no printing.");
            }
            finally
            {
                foreach (Form form in forms) { try { form.Dispose(); } catch { } }
            }
        }

        private static void Prepare(Form form)
        {
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
        }

        // The dialog closes itself through a timer so the modal loop ends without user input.
        private static void ShowDialogAndClose(Form owner)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "اختبار";
                dialog.ClientSize = new Size(240, 160);
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-20000, -20000);
                using (var timer = new Timer { Interval = 120 })
                {
                    timer.Tick += (sender, args) => { timer.Stop(); dialog.Close(); };
                    timer.Start();
                    // A hard deadline closes the dialog even when the timer never ticks.
                    using (var deadline = new System.Threading.Timer(
                        state =>
                        {
                            try
                            {
                                if (((Form)state).IsDisposed || !((Form)state).Visible) return;
                                ((Form)state).BeginInvoke(new Action(() =>
                                {
                                    if (!((Form)state).IsDisposed && ((Form)state).Visible) ((Form)state).Close();
                                }));
                            }
                            catch { }
                        },
                        dialog, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250)))
                    {
                        dialog.ShowDialog(owner);
                    }
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (condition) return;
            Console.Error.WriteLine("WINDOW_STABILITY_ASSERT: " + message);
            throw new InvalidOperationException(message);
        }
    }
}
