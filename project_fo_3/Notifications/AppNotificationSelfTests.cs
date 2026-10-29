using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class AppNotificationSelfTests
    {
        private static int assertions;

        internal static void Run()
        {
            assertions = 0;
            string originalTheme = Properties.Setings.Default.ThemeMode;
            bool originalOpeningPreference = Properties.Setings.Default.OpenScreensMaximized;
            try
            {
                Console.WriteLine("BEGIN notification button results");
                VerifyButtonResultsAndDismissal();
                Console.WriteLine("BEGIN notification owner language");
                VerifyReportContainerAccessibility();
                VerifyReportValidationOwnerLanguage();
                VerifyReadinessAccessibility();
                ResponsiveLayoutSelfTests.VerifyPeriodReadinessPresentation();
                Console.WriteLine("PASS focused readiness field/header/footer/detail layout checks; no SQL.");
                VerifyReadinessOwnerLanguage();
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    VerifyDefaultOwnerLanguage();
                }
                Console.WriteLine("BEGIN notification delete default");
                VerifyDeleteConfirmationDefaultsToNo();
                Console.WriteLine("BEGIN notification theme, direction and long text");
                VerifyThemeDirectionAndLongMessage();
                VerifySupportCodeRecognition();
                Console.WriteLine("BEGIN notification copy");
                VerifyCopyDetailsWithoutClipboardSideEffects();
                VerifyNotificationActionSizing();
                VerifyCancellationValidationLanguage();
                Console.WriteLine("BEGIN notification disposed owner");
                VerifyDisposedOwnerDismissal();
                VerifyDisposedChildOwnerDismissal();
                VerifyLiveChildOwnerAndClosingResult();
                VerifyOperationFailureGuidance();
                Console.WriteLine("BEGIN unsaved changes owner lifetime and choices");
                VerifyUnsavedChangesOwnerLifetime();
                VerifyUnsavedChangesChoices();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("App notification verification failed: " + error);
                throw;
            }
            finally
            {
                Properties.Setings.Default.ThemeMode = originalTheme;
                Properties.Setings.Default.OpenScreensMaximized = originalOpeningPreference;
            }
            Console.WriteLine("PASS app notifications: result buttons, safe dismissal, delete default, theme/direction, long text, copy, owner lifetime and unsaved changes; assertions=" + assertions + ".");
        }

        private static void VerifyButtonResultsAndDismissal()
        {
            foreach (var fixture in new[]
            {
                new { Buttons = MessageBoxButtons.OK, Results = new[] { DialogResult.OK } },
                new { Buttons = MessageBoxButtons.OKCancel, Results = new[] { DialogResult.OK, DialogResult.Cancel } },
                new { Buttons = MessageBoxButtons.YesNo, Results = new[] { DialogResult.Yes, DialogResult.No } },
                new { Buttons = MessageBoxButtons.YesNoCancel, Results = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel } },
                new { Buttons = MessageBoxButtons.RetryCancel, Results = new[] { DialogResult.Retry, DialogResult.Cancel } },
                new { Buttons = MessageBoxButtons.AbortRetryIgnore, Results = new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore } }
            })
            {
                DialogResult dismiss = AppNotifications.DismissResult(fixture.Buttons);
                foreach (DialogResult expected in fixture.Results)
                {
                    var dialog = CreateDialog("رسالة اختبار", "اختبار الإشعار", fixture.Buttons,
                        MessageBoxIcon.Information, DefaultFor(fixture.Results, expected), false);
                    DialogResult actual = Show(dialog, shown => Find<AntdUI.Button>(shown, "notification" + expected).PerformClick());
                    Assert(actual == expected, fixture.Buttons + " action " + expected + " returned " + actual + ".");
                }

                var closeDialog = CreateDialog("رسالة اختبار", "اختبار الإغلاق", fixture.Buttons,
                    MessageBoxIcon.Warning, DefaultFor(fixture.Results, fixture.Results[0]), false);
                DialogResult closeResult = Show(closeDialog, shown => shown.Close());
                Assert(closeResult == dismiss, fixture.Buttons + " window close must dismiss as " + dismiss + ", got " + closeResult + ".");
            }

            var cancelDialog = CreateDialog("رسالة اختبار", "اختبار Escape", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, false);
            DialogResult escapeResult = Show(cancelDialog, shown =>
            {
                Assert(shown.CancelButton != null && ((Control)shown.CancelButton).Name == "notificationNo",
                    "Escape/cancel must map to the safe No action for a Yes/No confirmation.");
                shown.CancelButton.PerformClick();
            });
            Assert(escapeResult == DialogResult.No, "Escape/cancel selected Yes in a delete-style confirmation.");
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child)) yield return nested;
            }
        }
        private static void VerifyReportContainerAccessibility()
        {
            foreach (string theme in new[] { "Light", "Dark" })
            {
                Properties.Setings.Default.ThemeMode = theme;
                using (var template = new ReportTemplateNameDialog("QA {0} حفظ", false))
                using (var columns = new ReportColumnLayoutDialog(new[] {
                    new DataGridViewTextBoxColumn { Name = "qa", HeaderText = "QA {0}" } }, false))
                using (var print = new ReportPrintOptionsDialog(new PharmacyProfile(), true, null, false))
                {
                    var name = template.Controls.Find("reportTemplateName", true).OfType<TextBox>().Single();
                    name.Text = "QA {0} حفظ";
                    var choices = columns.Controls.Find("reportColumns", true).OfType<CheckedListBox>().Single();
                    choices.SetItemChecked(0, false);
                    var orientation = print.Controls.Find("reportPrintOrientation", true).OfType<AntdUI.Select>().Single();
                    orientation.SelectedIndex = 2;
                    foreach (bool english in new[] { false, true, false, true })
                    {
                        template.ApplyPresentation(english); columns.ApplyPresentation(english); print.ApplyPresentation(english);
                        foreach (Form form in new Form[] { template, columns, print })
                        foreach (Control control in Descendants(form).Where(c => new[] {
                            "reportTemplateHeader", "reportTemplateBody", "reportTemplateNameFrame", "reportTemplateActions",
                            "reportColumnsHeader", "reportColumnsBody", "reportColumnMoveActions", "reportColumnActions",
                            "reportPrintHeader", "reportPrintBody", "reportPrintSelectors", "reportPrintIdentity", "reportPrintFooter" }.Contains(c.Name)))
                        {
                            string arabic = control.Name.StartsWith("reportTemplate") ?
                                (control.Name == "reportTemplateHeader" ? "حفظ قالب التقرير" : control.Name == "reportTemplateActions" ? "إجراءات التقرير" : "اسم قالب التقرير") :
                                control.Name.StartsWith("reportColumn") ?
                                (control.Name == "reportColumnsHeader" ? "تخصيص أعمدة التقرير" : control.Name == "reportColumnActions" ? "إجراءات التقرير" : "ترتيب الأعمدة الظاهرة") :
                                control.Name == "reportPrintIdentity" ? "بيانات المنشأة في الترويسة" :
                                control.Name == "reportPrintSelectors" ? "اتجاه الصفحة وكثافة الصفوف" :
                                control.Name == "reportPrintFooter" ? "إجراءات التقرير" : "خيارات طباعة التقرير";
                            Assert(control.AccessibleName == (english ? UiLanguageCatalog.Translate(arabic) : arabic),
                                "Report container accessibility must describe its purpose in the current language: " + control.Name);
                        }
                        Assert(name.Text == "QA {0} حفظ" && choices.CheckedIndices.Count == 0 && choices.Items[0].ToString() == "QA {0}",
                            "Relabeling containers must retain raw input and column choice.");
                        Assert(orientation.SelectedIndex == 2, "Relabeling print containers must retain orientation choice.");
                    }
                }
            }
        }

        private static void VerifyReportValidationOwnerLanguage()
        {
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (bool english in new[] { false, true })
            foreach (bool template in new[] { false, true })
            {
                Properties.Setings.Default.ThemeMode = theme;
                using (Form owner = template ? (Form)new ReportTemplateNameDialog("QA {0} حفظ", english) :
                    new ReportColumnLayoutDialog(new[] { new DataGridViewTextBoxColumn { Name = "qa", HeaderText = "QA {0}" } }, english))
                using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
                {
                    owner.ShowInTaskbar = false; owner.Opacity = 0;
                    owner.StartPosition = FormStartPosition.Manual; owner.Location = new Point(-20000, -20000);
                    owner.Show(); Application.DoEvents();
                    if (!template)
                    {
                        var columns = owner.Controls.Find("reportColumns", true).OfType<CheckedListBox>().Single();
                        for (int i = 0; i < columns.Items.Count; ++i) columns.SetItemChecked(i, false);
                    }
                    bool found = false; string failure = null; int ticks = 0;
                    timer.Tick += (sender, args) =>
                    {
                        var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                        if (dialog != null)
                        {
                            timer.Stop(); found = true;
                            var ok = dialog.Controls.Find("notificationOK", true).OfType<AntdUI.Button>().SingleOrDefault();
                            if (dialog.RightToLeft != (english ? RightToLeft.No : RightToLeft.Yes) ||
                                ok == null || ok.Text != (english ? "OK" : "موافق"))
                                failure = "Report validation notification buttons/direction must match owner language.";
                            string expected = template ? (english ? "Enter a template name." : "أدخل اسمًا للقالب.") :
                                (english ? "At least one column must remain visible." : "يجب إظهار عمود واحد على الأقل.");
                            if (dialog.Controls.Find("notificationMessage", true).Single().Text != expected || dialog.Text != owner.Text)
                                failure = "Report validation message or title changed.";
                            if (ok != null) ok.PerformClick(); else dialog.Close();
                        }
                        else if (++ticks > 200) { timer.Stop(); failure = "Report validation notification not observed."; Form.ActiveForm?.Close(); }
                    };
                    timer.Start();
                    owner.Controls.Find(template ? "saveReportTemplate" : "applyReportColumns", true).OfType<AntdUI.Button>().Single().PerformClick();
                    timer.Stop();
                    Assert(found && failure == null, failure ?? "Report validation not observed.");
                    Assert(!owner.IsDisposed && owner.DialogResult != DialogResult.OK, "Invalid report input must keep its editor open.");
                    Assert(!UiLanguageSettings.IsEnglishAvailable, "Report validation must not enable global English.");
                    owner.Close();
                }
            }
        }

        private static void VerifyReadinessAccessibility()
        {
            foreach (string mode in new[] { "Light", "Dark" })
            {
                Properties.Setings.Default.ThemeMode = mode;
                using (var form = new PeriodCloseReadinessForm(true))
                {
                    foreach (bool english in new[] { false, true, false, true })
                    {
                        form.ApplyPresentation(english);
                        Action verify = () =>
                        {
                            foreach (var item in new[] {
                                new[] { "periodReadinessHeader", "Period-close readiness", "جاهزية إقفال الفترة" },
                                new[] { "periodReadinessFilters", "Readiness date range", "نطاق تاريخ فحص الجاهزية" },
                                new[] { "periodReadinessActions", "Readiness check actions", "إجراءات فحص الجاهزية" },
                                new[] { "periodReadinessGridCard", "Period-close readiness results", "نتائج فحص جاهزية الإقفال" } })
                                Assert(form.Controls.Find(item[0], true).Single().AccessibleName == item[english ? 1 : 2],
                                    "Readiness container must keep a meaningful localized name independent of status: " + item[0]);
                            var status = form.Controls.Find("periodReadinessStatus", true).Single();
                            Assert(status.AccessibleName == status.Text && !string.IsNullOrEmpty(status.Text),
                                "Readiness status must expose its actual current message.");
                        };
                        verify();
                        form.Controls.Find("refreshPeriodReadiness", true).OfType<AntdUI.Button>().Single().PerformClick();
                        verify();
                        Assert(!UiLanguageSettings.IsEnglishAvailable, "Accessibility must not enable incomplete English.");
                    }
                }
            }
        }

        private static void VerifyReadinessOwnerLanguage()
        {
            foreach (string mode in new[] { "Light", "Dark" })
            foreach (bool english in new[] { false, true })
            {
                Properties.Setings.Default.ThemeMode = mode;
                using (var owner = new AccountingPeriodManagementForm(true))
                using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
                {
                    owner.ApplyPresentation(english);
                    owner.StartPosition = FormStartPosition.Manual; owner.Location = new Point(-20000, -20000);
                    owner.ShowInTaskbar = false; owner.Opacity = 0; owner.MaximizeBox = false;
                    owner.Show(); Application.DoEvents();
                    bool observed = false; string failure = null; int ticks = 0;
                    timer.Tick += (sender, args) =>
                    {
                        var child = Application.OpenForms.OfType<PeriodCloseReadinessForm>().FirstOrDefault();
                        if (child != null)
                        {
                            timer.Stop(); observed = true;
                            if (child.Text != (english ? "Period-close readiness" : "جاهزية إقفال الفترة"))
                                failure = "Readiness child must inherit its accounting period owner's language.";
                            if (AppNotifications.ResolvePresentationDirection(child) != english)
                                failure = "Readiness notification must inherit the explicit child language.";
                            var nested = new Label(); child.Controls.Add(nested);
                            if (AppNotifications.ResolvePresentationDirection(nested) != english)
                                failure = "Readiness child controls must preserve notification language.";
                            child.Close();
                        }
                        else if (++ticks > 200) { timer.Stop(); failure = "Readiness dialog did not open."; owner.Close(); }
                    };
                    timer.Start();
                    owner.Controls.Find("checkPeriodReadiness", true).OfType<AntdUI.Button>().Single().PerformClick();
                    timer.Stop();
                    Assert(observed && failure == null, failure ?? "Readiness dialog not observed.");
                    Assert(!UiLanguageSettings.IsEnglishAvailable, "Readiness presentation must not enable global English.");
                    owner.Close();
                }
            }
        }

        private static void VerifyDefaultOwnerLanguage()
        {
            foreach (bool english in new[] { false, true })
            foreach (Form owner in new Form[] { new BillingUiAutomationPreview(english), new PurchasesUiAutomationPreview(english),
                new CurrencySettingsForm(true, english), new TaxSettingsForm(true, english),
                new SaudiCommercialSettingsForm(true, english), new DatabaseSetingsForm(true, english) })
            using (owner)
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                bool found = false;
                string failure = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    ++ticks;
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop(); found = true;
                        if (dialog.RightToLeft != (english ? RightToLeft.No : RightToLeft.Yes))
                            failure = "Default notification language did not follow its explicit commerce owner.";
                        var no = dialog.Controls.Find("notificationNo", true).OfType<AntdUI.Button>().FirstOrDefault();
                        if (no == null || no.Text != (english ? "No" : "لا"))
                            failure = "Default confirmation action language differs from its owner: " + owner.GetType().Name + "/english=" + english + "/actual=" + no?.Text;
                        if (dialog.AcceptButton == null || ((Control)dialog.AcceptButton).Name != "notificationNo")
                            failure = "Owner language changed the safe delete default.";
                        if (no != null) no.PerformClick(); else dialog.Close();
                    }
                    else if (ticks >= 200)
                    {
                        timer.Stop(); failure = "No owned notification was created.";
                        Form.ActiveForm?.Close();
                    }
                };
                timer.Start();
                Assert(AppNotifications.ResolvePresentationDirection(owner) == english, "Explicit settings/commerce owner language must be resolved: " + owner.GetType().Name);
                bool approved = AppNotifications.ConfirmDelete(owner, "QA raw {0} / حفظ", "QA owner language");
                timer.Stop();
                Assert(found && failure == null, failure ?? "Owned notification was not observed.");
                Assert(!approved, "Language selection must not approve deletion.");
            }
            using (var genericOwner = new Form { RightToLeft = RightToLeft.No })
            {
                Assert(!AppNotifications.ResolvePresentationDirection(genericOwner), "A generic LTR form must not bypass the global language setting.");
                Assert(AppNotifications.DefaultCaption(genericOwner) == "نظام إدارة الصيدليات", "Generic notifications must keep the current default caption language.");
            }
            using (var englishOwner = new BillingUiAutomationPreview(true))
            {
                var child = new Label(); englishOwner.Controls.Add(child);
                Assert(AppNotifications.ResolvePresentationDirection(child), "A child owner must resolve its screen presentation.");
                Assert(AppNotifications.DefaultCaption(englishOwner) == "Pharmacy management", "Default title must follow the explicit owner language.");
            }
            using (var receiptOwner = new ReceiptVoucherForm("Receipt", true))
            {
                receiptOwner.ApplyReceiptCurrencyPresentation(true);
                Assert(AppNotifications.ResolvePresentationDirection(receiptOwner), "Receipt notification must follow explicit English presentation.");
                receiptOwner.ApplyReceiptCurrencyPresentation(false);
                Assert(!AppNotifications.ResolvePresentationDirection(receiptOwner), "Receipt notification must follow explicit Arabic presentation.");
            }
            Assert(!UiLanguageSettings.IsEnglishAvailable && !UiLanguageCatalog.IsEnglishActive,
                "Owner presentation must not enable the incomplete global English option.");
        }

        private static void VerifyDeleteConfirmationDefaultsToNo()
        {
            using (var owner = CreateOwner())
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                owner.Show();
                Application.DoEvents();
                bool found = false;
                string failure = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    ++ticks;
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop();
                        found = true;
                        if (dialog.AcceptButton == null || ((Control)dialog.AcceptButton).Name != "notificationNo")
                            failure = "Delete confirmation Enter/default action must be No.";
                        if (dialog.CancelButton == null || ((Control)dialog.CancelButton).Name != "notificationNo")
                            failure = "Delete confirmation Escape action must be No.";
                        var no = dialog.Controls.Find("notificationNo", true).OfType<AntdUI.Button>().FirstOrDefault();
                        if (no != null) no.PerformClick(); else dialog.Close();
                        return;
                    }

                    if (ticks >= 200)
                    {
                        timer.Stop();
                        failure = "The shared delete confirmation did not create its themed notification form.";
                        Form.ActiveForm?.Close();
                    }
                };
                timer.Start();
                bool confirmed = AppNotifications.ConfirmDelete(owner, "هل تريد حذف هذا السجل؟", "تأكيد الحذف");
                timer.Stop();
                Assert(found, failure ?? "The shared delete confirmation was not observed.");
                Assert(failure == null, failure ?? string.Empty);
                Assert(!confirmed, "The delete confirmation default was interpreted as approval.");
                owner.Close();
            }
        }

        private static void VerifyThemeDirectionAndLongMessage()
        {
            string longMessage = string.Join(Environment.NewLine,
                Enumerable.Range(1, 120).Select(index => "تفصيل تشخيصي طويل رقم " + index + " مع وصف واضح للحالة والإجراء المقترح."));
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (bool leftToRight in new[] { false, true })
            {
                Properties.Setings.Default.ThemeMode = theme;
                var dialog = CreateDialog(longMessage, "تنبيه", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2, leftToRight);
                bool inspected = false;
                DialogResult result = Show(dialog, shown =>
                {
                    var actions = Find<FlowLayoutPanel>(shown, "notificationActions");
                    var severity = Find<Label>(shown, "notificationSeverity");
                    var message = Find<Label>(shown, "notificationMessage");
                    var content = message.Parent as Panel;
                    Assert(shown.RightToLeft == (leftToRight ? RightToLeft.No : RightToLeft.Yes), "Notification form direction is incorrect.");
                    Assert(shown.RightToLeftLayout == !leftToRight, "Notification form layout mirroring is incorrect.");
                    Assert(actions.FlowDirection == (leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft), "Notification action flow is incorrect.");
                    Assert(severity.BackColor == UiTheme.WarningSurface, "Warning severity does not use the shared warning surface.");
                    Assert(shown.BackColor == UiTheme.Background, "Notification form does not use the application surface.");
                    Assert(content != null && content.AutoScroll, "Long notification content must remain scrollable.");
                    Assert(message.Text == longMessage && !message.AutoEllipsis, "Long notification text was truncated.");
                    Assert(shown.ClientSize.Height <= 480, "Long notification exceeds its designed compact height.");
                    Assert(shown.AcceptButton != null && ((Control)shown.AcceptButton).Name == "notificationCancel", "Explicit default Cancel action was not preserved.");
                    inspected = true;
                    Find<AntdUI.Button>(shown, "notificationCancel").PerformClick();
                });
                Assert(inspected, "The themed notification was not shown for inspection.");
                Assert(result == DialogResult.Cancel, "Long warning notification did not return its selected Cancel result.");
            }
        }

        private static void VerifyCopyDetailsWithoutClipboardSideEffects()
        {
            const string original = "تعذر إكمال العملية.\n\nرمز الدعم: 20261017-123456-a1b2c3d4";
            var dialog = CreateDialog(original, "تعذر الإكمال", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, false);
            string copied = null;
            typeof(AppNotificationForm).GetField("copyTextWriter", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(dialog, new Action<string>(value => copied = value));
            DialogResult result = Show(dialog, shown =>
            {
                var body = Find<Label>(shown, "notificationMessage");
                var code = Find<Label>(shown, "notificationSupportCode");
                Assert(body.Text == "تعذر إكمال العملية.", "Support code was not separated from the readable message body; body=" + body.Text.Replace(Environment.NewLine, "<NL>") + "; code=" + code.Text + ".");
                Assert(code.Text == "20261017-123456-a1b2c3d4", "Support code display was altered.");
                var copyCode = Find<AntdUI.Button>(shown, "notificationCopySupportCode");
                typeof(AppNotificationForm).GetMethod("CopyToClipboard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(shown, new object[] { copyCode, code.Text });
                Assert(copied == code.Text, "Copy-support-code button copied unrelated content.");
                Assert(copyCode.AccessibleName == "نسخ الرمز" && copyCode.AccessibleDescription == "تم النسخ", "Copy code must retain its purpose after success.");
                copied = null;
                var copyDetails = Find<AntdUI.Button>(shown, "notificationCopy");
                typeof(AppNotificationForm).GetMethod("CopyToClipboard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(shown, new object[] { copyDetails, shown.BuildCopyText() });
                Assert(copied == "تعذر الإكمال" + Environment.NewLine + Environment.NewLine + original,
                    "Copy-details did not preserve the full original message.");
                Assert(!shown.IsDisposed, "Copying details closed the notification.");
                Assert(copyDetails.DialogResult == DialogResult.None, "Copy-details can be interpreted as a confirmation action.");
                typeof(AppNotificationForm).GetField("copyTextWriter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(shown, new Action<string>(value => { throw new System.Threading.ThreadStateException(); }));
                typeof(AppNotificationForm).GetMethod("CopyToClipboard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(shown, new object[] { copyDetails, shown.BuildCopyText() });
                Assert(copyDetails.AccessibleName == "نسخ التفاصيل" && copyDetails.AccessibleDescription == "تعذر النسخ", "Copy details must retain its purpose and expose failure separately.");
                Assert(copyDetails.Text == "تعذر النسخ", "Clipboard failure was not shown on the existing action.");
                Assert(!shown.IsDisposed && copyDetails.DialogResult == DialogResult.None,
                    "Clipboard failure closed or confirmed the notification.");
                Find<AntdUI.Button>(shown, "notificationOK").PerformClick();
            });
            Assert(result == DialogResult.OK, "Copying details changed the action result.");
        }

        private static void VerifyNotificationActionSizing()
        {
            foreach (bool english in new[] { false, true })
            using (var largerFont = AppFonts.Create(14F))
            {
                var dialog = CreateDialog("QA\n\nSupport code: 20261018-123456-a1b2c3d4", "QA",
                    MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, english);
                Show(dialog, shown =>
                {
                    var actions = Find<FlowLayoutPanel>(shown, "notificationActions");
                    foreach (Control button in actions.Controls) button.Font = largerFont;
                    foreach (int width in new[] { 360, 680, 360 })
                    {
                        shown.ClientSize = new Size(width, 400);
                        shown.PerformLayout(); actions.PerformLayout();
                        foreach (Control button in actions.Controls)
                        {
                            int textWidth = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                            Assert(button.Width >= textWidth + 32, "Notification action clips enlarged text: " + button.Name);
                            Assert(actions.ClientRectangle.Contains(button.Bounds), "Notification footer action is outside the viewport: " + button.Name);
                        }
                    }
                    Find<AntdUI.Button>(shown, "notificationAbort").PerformClick();
                });
            }
        }

        private static void VerifyCancellationValidationLanguage()
        {
            const string rawName = "QA-{0}-شركة Save";
            var dynamicError = new UiValidationException("تعذر إعادة المخزون للدواء: {0}", rawName);
            Assert(dynamicError.Message == "تعذر إعادة المخزون للدواء: " + rawName &&
                dynamicError.DisplayMessage(true) == "Could not restore stock for medicine: " + rawName,
                "Localized validation must preserve raw names and braces in both languages.");
            bool databaseTouched = false;
            var purchase = new PurchaseInvoiceCancellationOperation(() => { databaseTouched = true; throw new InvalidOperationException("Unexpected database access"); }, () => true);
            Exception purchaseError = null;
            try { purchase.Cancel(new PharmacyManagement.Application.PurchaseInvoiceCancellationRequest {
                PurchaseId = "QA", ActorName = "QA", Reason = new string('x', 501) }); }
            catch (UiValidationException error) { purchaseError = error; }
            Assert(!databaseTouched && purchaseError != null && ((UiValidationException)purchaseError).DisplayMessage(true) ==
                "The cancellation reason exceeds the allowed length (500).", "Purchase validation must localize before any database access.");
            foreach (var field in new[] {
                new { Id = "", Actor = "QA", Expected = "A purchase invoice number is required." },
                new { Id = new string('x', 51), Actor = "QA", Expected = "The purchase invoice number exceeds the allowed length (50)." },
                new { Id = "QA", Actor = "", Expected = "A cancellation actor is required." },
                new { Id = "QA", Actor = new string('x', 201), Expected = "The cancellation actor exceeds the allowed length (200)." } })
            {
                UiValidationException error = null;
                try { purchase.Cancel(new PharmacyManagement.Application.PurchaseInvoiceCancellationRequest {
                    PurchaseId = field.Id, ActorName = field.Actor }); }
                catch (UiValidationException caught) { error = caught; }
                Assert(!databaseTouched && error != null && error.DisplayMessage(true) == field.Expected,
                    "Required field validation must retain its boundary and translate before database access.");
            }
            Exception validation = null;
            try { SalesInvoiceCancellationService.CancelCurrentLegacySale(new string[0], "QA", "QA"); }
            catch (InvalidOperationException error) { validation = error; }
            Assert(validation != null && validation.Message == "لا توجد بنود فاتورة معتمدة لإلغائها.",
                "Empty invoice validation must preserve the Arabic service contract before any database access.");
            foreach (bool english in new[] { false, true })
            using (var owner = new BillingUiAutomationPreview(english))
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                string body = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop(); body = Find<Label>(dialog, "notificationMessage").Text;
                        Find<AntdUI.Button>(dialog, "notificationOK").PerformClick();
                    }
                    else if (++ticks >= 200) { timer.Stop(); Form.ActiveForm?.Close(); }
                };
                timer.Start();
                UiTheme.ShowOperationFailure(owner, "إلغاء الفاتورة", validation, preserveValidationMessage: true);
                timer.Stop();
                Assert(body == (english ? "There are no posted invoice lines to cancel." : validation.Message),
                    "Cancellation validation must follow the owner's presentation language.");
            }
        }

        private static void VerifySupportCodeRecognition()
        {
            foreach (string message in new[]
            {
                "رفضت العملية.\n\nرمز الدعم: 20260230-123456-a1b2c3d4",
                "رفضت العملية.\n\nرمز الدعم: 20261017-123456-a1b2c3d4\nمرجع العملية: SALE-2026-17",
                "Operation failed.\n\nSupport code: 20261017-123456-a1b2c3d4"
            })
            {
                bool english = message.StartsWith("Operation", StringComparison.Ordinal);
                using (var form = new AppNotificationForm(message, english ? "Error" : "خطأ",
                    MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, english))
                {
                    var body = Find<Label>(form, "notificationMessage");
                    var codeControls = form.Controls.Find("notificationSupportCode", true);
                    if (message.Contains("Support code: 20261017"))
                    {
                        Assert(codeControls.Length == 1, "Valid English support code was not detected.");
                        Assert(body.Text == "Operation failed.", "English support label remained in the message body.");
                    }
                    else
                    {
                        Assert(codeControls.Length == 0, "Malformed or non-terminal support text was split from the business message.");
                        Assert(body.Text == message, "Malformed or trailing support text was altered.");
                    }
                }
            }
        }

        private static void VerifyDisposedOwnerDismissal()
        {
            var owner = new Form();
            owner.Dispose();
            DialogResult result = AppNotifications.Show(owner, "رسالة اختبار", "اختبار المالك المتخلص منه",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            Assert(result == DialogResult.No, "A disposed owner must return a safe dismiss result without showing a dialog.");
        }

        private static void VerifyDisposedChildOwnerDismissal()
        {
            foreach (bool disposeParent in new[] { false, true })
            foreach (var buttons in new[] { MessageBoxButtons.OK, MessageBoxButtons.OKCancel, MessageBoxButtons.YesNo,
                MessageBoxButtons.YesNoCancel, MessageBoxButtons.RetryCancel, MessageBoxButtons.AbortRetryIgnore })
            using (var owner = CreateOwner())
            using (var child = new Button())
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                owner.Controls.Add(child);
                if (disposeParent) owner.Dispose(); else child.Dispose();
                bool shown = false;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog == null) return;
                    shown = true;
                    dialog.Close();
                };
                DialogResult result = DialogResult.None;
                Exception failure = null;
                try
                {
                    timer.Start();
                    result = AppNotifications.Show(child, "QA disposed child", "QA owner lifetime", buttons, MessageBoxIcon.Warning);
                }
                catch (Exception error) { failure = error; }
                finally { timer.Stop(); }
                Assert(failure == null, "Disposed child owner must dismiss without a dialog or exception: parent=" + disposeParent + "/" + failure?.GetType().Name);
                Assert(!shown && result == AppNotifications.DismissResult(buttons), "A disposed child owner must dismiss before constructing a dialog: parent=" + disposeParent + "/" + buttons);
            }
        }

        private static void VerifyLiveChildOwnerAndClosingResult()
        {
            foreach (int closeStage in new[] { 0, 1, 2 })
            using (var owner = CreateOwner())
            using (var child = new Button())
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                owner.Controls.Add(child);
                bool found = false, correctOwner = false;
                Exception failure = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop(); found = true; correctOwner = dialog.Owner == owner;
                        try
                        {
                            if (closeStage > 0)
                            {
                                if (closeStage == 1) dialog.FormClosing += (s, e) => owner.Dispose();
                                else dialog.Disposed += (s, e) => owner.Dispose();
                                Find<AntdUI.Button>(dialog, "notificationYes").PerformClick();
                            }
                            else Find<AntdUI.Button>(dialog, "notificationNo").PerformClick();
                        }
                        catch (Exception error) { failure = error; if (!dialog.IsDisposed) dialog.Close(); }
                    }
                    else if (++ticks >= 200)
                    {
                        timer.Stop(); failure = new TimeoutException("No child-owned notification appeared.");
                        Form.ActiveForm?.Close();
                    }
                };
                timer.Start();
                bool approved = AppNotifications.ConfirmDelete(child, "QA child owner result", "QA owner lifetime");
                timer.Stop();
                Assert(found && correctOwner && failure == null, "Child notification must belong to its form: closing=" + closeStage + "/" + failure?.GetType().Name);
                Assert(!approved, "A closed owner or the selected No action must never become approval: stage=" + closeStage);
            }
        }

        private static void VerifyOperationFailureGuidance()
        {
            foreach (bool english in new[] { false, true })
            foreach (bool validation in new[] { false, true })
            using (var owner = new BillingUiAutomationPreview(english))
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                const string rawValidation = "QA_USER_VALUE {0} اسم";
                const string technical = "QA_TECHNICAL_DETAIL_ONLY_IN_LOG";
                string operation = english ? "save QA item {0}" : "حفظ سجل QA {0}";
                string body = null, copied = null, action = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop();
                        body = Find<Label>(dialog, "notificationMessage").Text;
                        copied = dialog.BuildCopyText();
                        var ok = Find<AntdUI.Button>(dialog, "notificationOK");
                        action = ok.Text;
                        ok.PerformClick();
                    }
                    else if (++ticks >= 200) { timer.Stop(); Form.ActiveForm?.Close(); }
                };
                timer.Start();
                Exception error = validation ? (Exception)new InvalidOperationException(rawValidation) : new System.IO.IOException(technical);
                UiTheme.ShowOperationFailure(owner, operation, error, preserveValidationMessage: validation);
                timer.Stop();
                Assert(body != null && action == (english ? "OK" : "موافق"), "Failure guidance and action must follow the explicit owner language.");
                if (validation)
                    Assert(body == rawValidation, "Explicit non-SQL validation text must remain verbatim, including braces and names.");
                else
                {
                    Assert(body.Contains(english ? "review the record before retrying" : "راجع السجل قبل تكرارها") &&
                        !body.Contains("لم يتم اعتماد أي تغيير غير مكتمل"), "A generic failure must not infer rollback from an exception.");
                    Assert(body.Contains(operation) && copied.Contains(english ? "support code" : "رمز الدعم") && !copied.Contains(technical),
                        "Failure guidance lost its operation value, support code or technical-detail boundary.");
                }
            }
        }

        private static void VerifyUnsavedChangesOwnerLifetime()
        {
            foreach (bool english in new[] { false, true })
            foreach (bool missingOwner in new[] { false, true })
            using (var owner = CreateOwner())
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                owner.Dispose();
                bool shown = false, discard = false;
                Exception failure = null;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<UnsavedChangesDialog>().FirstOrDefault();
                    if (dialog == null) return;
                    shown = true; dialog.Close();
                };
                try
                {
                    timer.Start();
                    discard = UnsavedChangesDialog.ConfirmDiscard(missingOwner ? null : owner, english);
                }
                catch (Exception error) { failure = error; }
                finally { timer.Stop(); }
                Assert(failure == null && !shown && !discard,
                    "An absent/disposed unsaved-changes owner must keep editing without showing a dialog: missing=" + missingOwner + "/" + failure?.GetType().Name);
            }
        }

        private static void VerifyUnsavedChangesChoices()
        {
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (bool english in new[] { false, true })
            foreach (string choice in new[] { "keep", "discard", "close", "enter", "escape", "ownerClosing", "ownerDisposed" })
            using (var owner = CreateOwner())
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                Properties.Setings.Default.ThemeMode = theme;
                bool found = false, discard = false;
                Exception failure = null;
                int ticks = 0;
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<UnsavedChangesDialog>().FirstOrDefault();
                    if (dialog != null)
                    {
                        timer.Stop(); found = true;
                        try
                        {
                            var keep = Find<AntdUI.Button>(dialog, "keepEditing");
                            var discardButton = Find<AntdUI.Button>(dialog, "discardChanges");
                            var actions = Find<FlowLayoutPanel>(dialog, "unsavedChangesActions");
                            var message = Find<Label>(dialog, "unsavedChangesMessage");
                            Assert(dialog.Owner == owner && ReferenceEquals(dialog.AcceptButton, keep) && ReferenceEquals(dialog.CancelButton, keep),
                                "Unsaved changes must keep its owner and safe Enter/Escape actions.");
                            Assert(dialog.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) &&
                                keep.Text == (english ? "Keep editing" : "متابعة التحرير") &&
                                discardButton.Text == (english ? "Discard changes" : "تجاهل التعديلات") &&
                                dialog.Text == (english ? "Unsaved changes" : "تعديلات غير محفوظة"),
                                "Unsaved changes captions or direction differ from the selected language.");
                            Assert(message.AccessibleName == message.Text && !message.AutoEllipsis && actions.BackColor == UiTheme.Background &&
                                !dialog.MaximizeBox && !dialog.MinimizeBox,
                                "Unsaved changes must retain full accessible text and compact themed chrome.");
                            foreach (Size size in new[] { new Size(430, 280), new Size(640, 360), new Size(430, 280) })
                            {
                                dialog.ClientSize = size; dialog.PerformLayout(); actions.PerformLayout();
                                Assert(actions.ClientRectangle.Contains(keep.Bounds) && actions.ClientRectangle.Contains(discardButton.Bounds) &&
                                    !keep.Bounds.IntersectsWith(discardButton.Bounds), "Unsaved changes actions overlap or leave the actual viewport.");
                            }
                            switch (choice)
                            {
                                case "keep": keep.PerformClick(); break;
                                case "discard": discardButton.PerformClick(); break;
                                case "enter": dialog.AcceptButton.PerformClick(); break;
                                case "escape": dialog.CancelButton.PerformClick(); break;
                                case "ownerClosing":
                                    dialog.FormClosing += (s, e) => owner.Dispose(); discardButton.PerformClick(); break;
                                case "ownerDisposed":
                                    dialog.Disposed += (s, e) => owner.Dispose(); discardButton.PerformClick(); break;
                                default: dialog.Close(); break;
                            }
                        }
                        catch (Exception error) { failure = error; if (!dialog.IsDisposed) dialog.Close(); }
                    }
                    else if (++ticks >= 200)
                    {
                        timer.Stop(); failure = new TimeoutException("No unsaved-changes dialog appeared.");
                        Form.ActiveForm?.Close();
                    }
                };
                try
                {
                    timer.Start(); discard = UnsavedChangesDialog.ConfirmDiscard(owner, english);
                }
                catch (Exception error) { failure = error; }
                finally { timer.Stop(); }
                Assert(found && failure == null, "Unsaved changes callback failed: " + theme + "/" + english + "/" + choice + "/" + failure?.Message);
                Assert(discard == (choice == "discard"), "Only explicit discard in a live editor may approve: " + choice);
            }
            Assert(!UiLanguageSettings.IsEnglishAvailable, "Unsaved changes translation must not enable the global English option.");
        }

        private static AppNotificationForm CreateDialog(string message, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, bool leftToRight)
        {
            return new AppNotificationForm(message, caption, buttons, icon, defaultButton, leftToRight)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000),
                ShowInTaskbar = false,
                Opacity = 0
            };
        }

        private static DialogResult Show(AppNotificationForm dialog, Action<AppNotificationForm> onShown)
        {
            using (dialog)
            using (var owner = CreateOwner())
            {
                owner.Show();
                Application.DoEvents();
                bool shown = false;
                Exception callbackFailure = null;
                dialog.Shown += (sender, args) =>
                {
                    shown = true;
                    try { onShown(dialog); }
                    catch (Exception error)
                    {
                        callbackFailure = error;
                        if (!dialog.IsDisposed) dialog.Close();
                    }
                };
                dialog.Show(owner);
                Application.DoEvents();
                if (!shown) throw new InvalidOperationException("Notification test form did not reach Shown.");
                DialogResult result = dialog.DialogResult;
                if (!dialog.IsDisposed) dialog.Close();
                Application.DoEvents();
                owner.Close();
                if (callbackFailure != null)
                    throw new InvalidOperationException("Notification UI assertion failed: " + callbackFailure.Message, callbackFailure);
                return result;
            }
        }

        private static Form CreateOwner()
        {
            return new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000),
                Size = new Size(320, 220),
                ShowInTaskbar = false,
                Opacity = 0
            };
        }

        private static T Find<T>(Control root, string name) where T : Control
        {
            var matches = root.Controls.Find(name, true).OfType<T>().ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one notification control " + name + ", found " + matches.Length + ".");
            return matches[0];
        }

        private static MessageBoxDefaultButton DefaultFor(DialogResult[] results, DialogResult result)
        {
            int index = Array.IndexOf(results, result);
            return (MessageBoxDefaultButton)(index * 256);
        }

        private static void Assert(bool condition, string message)
        {
            ++assertions;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
