using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace project_fo_3
{
    // Shared application-owned notifications; callers retain their result and operation contracts.
    internal static class AppNotifications
    {
        [ThreadStatic] private static bool presentingNotification;
        [ThreadStatic] private static bool presentingEmergency;
        internal static DialogResult Show(string text)
        {
            Form owner = Form.ActiveForm;
            return Show(owner, text, DefaultCaption(owner));
        }

        internal static DialogResult Show(string text, string caption)
        {
            return Show(text, caption, MessageBoxButtons.OK);
        }

        internal static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return Show(text, caption, buttons, MessageBoxIcon.None);
        }

        internal static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(Form.ActiveForm, text, caption, buttons, icon);
        }

        internal static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            return Show(Form.ActiveForm, text, caption, buttons, icon, defaultButton);
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption)
        {
            return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
        {
            return Show(owner, text, caption, buttons, MessageBoxIcon.None);
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(owner, text, caption, buttons, icon, DefaultButton(buttons));
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return Show(owner, text, caption, buttons, icon, defaultButton, ResolvePresentationDirection(owner));
        }

        internal static string DefaultCaption(IWin32Window owner) => ResolvePresentationDirection(owner)
            ? "Pharmacy management" : "نظام إدارة الصيدليات";

        internal static bool ResolvePresentationDirection(IWin32Window owner)
        {
            if (IsOwnerUnavailable(owner)) return UiLanguageCatalog.IsEnglishActive;
            Form form = owner as Form ?? (owner as Control)?.FindForm();
            // These screens expose an explicit presentation contract for previews
            // and normal sessions. Generic LTR controls do not select a language.
            return (form as Billing)?.PresentationLeftToRight ??
                (form as Purchases)?.PresentationLeftToRight ??
                (form as AccountingPeriodManagementForm)?.PresentationLeftToRight ??
                (form as PeriodCloseReadinessForm)?.PresentationLeftToRight ??
                (form as ReportTemplateNameDialog)?.PresentationLeftToRight ??
                (form as ReportColumnLayoutDialog)?.PresentationLeftToRight ??
                (form as CurrencySettingsForm)?.PresentationLeftToRight ??
                (form as SettingsForm)?.PresentationLeftToRight ??
                (form as PharmacyCompanyForm)?.PresentationLeftToRight ??
                (form as LanguageSettingsForm)?.PresentationLeftToRight ??
                (form as DeviceSettingsForm)?.PresentationLeftToRight ??
                (form as SalesShortcutSettingsForm)?.PresentationLeftToRight ??
                (form as AccessibilitySettingsForm)?.PresentationLeftToRight ??
                (form as TaxSettingsForm)?.PresentationLeftToRight ??
                (form as SaudiCommercialSettingsForm)?.PresentationLeftToRight ??
                (form as DatabaseSetingsForm)?.PresentationLeftToRight ??
                (form as ActiveIngredientManagementForm)?.PresentationLeftToRight ??
                (form as ReceiptVoucherForm)?.ReceiptPresentationDirection ??
                UiLanguageCatalog.IsEnglishActive;
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, bool leftToRight)
        {
            if (IsOwnerUnavailable(owner)) return DismissResult(buttons);
            IWin32Window dialogOwner = (owner as Control)?.FindForm() ?? owner;
            if (IsOwnerUnavailable(dialogOwner)) return DismissResult(buttons);
            // A UI-thread exception raised while rendering a dialog must not recursively
            // construct another copy of the same failing themed surface.
            if (presentingNotification) return GuardOwnerResult(
                ShowEmergency(dialogOwner, text, caption, buttons, icon, defaultButton), buttons, owner, dialogOwner);
            presentingNotification = true;
            try
            {
                DialogResult result;
                using (var dialog = new AppNotificationForm(text, caption, buttons, icon, defaultButton, leftToRight))
                    result = dialogOwner == null ? dialog.ShowDialog() : dialog.ShowDialog(dialogOwner);
                // Disposal callbacks can close the owner too; validate after the
                // dialog's complete lifetime, not inside its using scope.
                return GuardOwnerResult(result, buttons, owner, dialogOwner);
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException ||
                error is Win32Exception || error is ConfigurationErrorsException || error is IOException ||
                error is UnauthorizedAccessException || error is TypeInitializationException)
            {
                // Keep startup/recovery failures visible if the themed UI itself cannot initialize.
                // This is the only native notification path, and never implies user confirmation.
                Trace.TraceError("Themed notification unavailable: " + error.GetType().FullName);
                if (IsOwnerUnavailable(owner) || IsOwnerUnavailable(dialogOwner)) return DismissResult(buttons);
                return GuardOwnerResult(ShowEmergency(dialogOwner, text, caption, buttons, icon, defaultButton), buttons, owner, dialogOwner);
            }
            finally { presentingNotification = false; }
        }

        internal static bool IsOwnerUnavailable(IWin32Window owner)
        {
            // Inspect managed lifetime only: reading Handle can recreate a window
            // or throw after disposal. Include enclosing controls that are closing.
            for (Control current = owner as Control; current != null; current = current.Parent)
                if (current.IsDisposed || current.Disposing) return true;
            return false;
        }

        private static DialogResult GuardOwnerResult(DialogResult result, MessageBoxButtons buttons,
            IWin32Window originalOwner, IWin32Window dialogOwner)
        {
            // A result cannot approve a workflow whose original screen closed
            // while the dialog was completing, even if a child was detached.
            return IsOwnerUnavailable(originalOwner) || IsOwnerUnavailable(dialogOwner)
                ? DismissResult(buttons) : result;
        }

        private static DialogResult ShowEmergency(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            if (presentingEmergency || IsOwnerUnavailable(owner)) return DismissResult(buttons);
            presentingEmergency = true;
            try { return MessageBox.Show(owner, text, caption, buttons, icon, defaultButton); }
            catch (Exception error) when ((error is InvalidOperationException || error is Win32Exception) && IsOwnerUnavailable(owner))
            { return DismissResult(buttons); }
            finally { presentingEmergency = false; }
        }

        // File output has no financial posting side effect. Offer a useful next step without
        // displaying exception messages, paths or credentials; financial retry warnings stay separate.
        internal static void ShowFileOutputIssue(IWin32Window owner, string operation, Exception error, bool leftToRight)
        {
            string supportCode = AppDiagnostics.Log("FileOutputIssue:" + (operation ?? "Unknown"), error);
            string caption = operation ?? "تعذر إكمال العملية";
            Show(owner, BuildFileOutputIssueMessage(error, supportCode, leftToRight),
                leftToRight ? UiLanguageCatalog.Translate(caption) : caption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, leftToRight);
        }

        internal static string BuildFileOutputIssueMessage(Exception error, string supportCode, bool leftToRight)
        {
            string hint = FileOutputHint(error);
            const string support = "إذا تكرر الأمر، أرسل رمز الدعم إلى مسؤول النظام: {0}";
            return (leftToRight ? UiLanguageCatalog.Translate(hint) : hint) + "\n\n" +
                string.Format(leftToRight ? UiLanguageCatalog.Translate(support) : support, supportCode);
        }

        private static string FileOutputHint(Exception error)
        {
            for (int depth = 0; error != null && depth < 8; depth++, error = error.InnerException)
            {
                if (error is PathTooLongException)
                    return "اسم الملف أو مساره أطول من المسموح. اختر اسمًا أقصر داخل مجلد قريب.";
                if (error is DirectoryNotFoundException || error is DriveNotFoundException)
                    return "المجلد أو القرص المختار غير متاح. تحقق من الاتصال أو اختر مجلدًا آخر.";
                if (error is UnauthorizedAccessException)
                    return "تعذر الكتابة في المجلد المختار. اختر مجلدًا يمكنك الحفظ فيه أو اسم ملف آخر.";
                if (error is IOException)
                {
                    int code = error.HResult & 0xffff;
                    if (code == 32 || code == 33)
                        return "تعذر حفظ الملف لأنه مستخدم في برنامج آخر. أغلقه في Excel أو قارئ PDF، أو اختر اسمًا آخر، ثم أعد المحاولة.";
                    if (code == 112 || code == 39)
                        return "المساحة المتاحة لا تكفي لحفظ الملف. وفر مساحة أو اختر قرصًا آخر.";
                }
            }
            return "تعذر تأكيد حفظ الملف. تحقق من الملف والمسار قبل إعادة المحاولة.";
        }

        internal static bool ConfirmDelete(IWin32Window owner, string text, string caption)
        {
            return Show(owner, text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        internal static DialogResult DismissResult(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.YesNo: return DialogResult.No;
                case MessageBoxButtons.YesNoCancel:
                case MessageBoxButtons.OKCancel:
                case MessageBoxButtons.RetryCancel: return DialogResult.Cancel;
                case MessageBoxButtons.AbortRetryIgnore: return DialogResult.Abort;
                default: return DialogResult.OK;
            }
        }

        private static MessageBoxDefaultButton DefaultButton(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                case MessageBoxButtons.OKCancel:
                case MessageBoxButtons.RetryCancel: return MessageBoxDefaultButton.Button2;
                case MessageBoxButtons.YesNoCancel: return MessageBoxDefaultButton.Button3;
                default: return MessageBoxDefaultButton.Button1;
            }
        }
    }
}
