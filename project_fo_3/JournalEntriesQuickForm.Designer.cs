using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class JournalEntriesQuickForm
    {
        private void BuildEditor()
        {
            AddQuickField("من تاريخ", fromDate);
            AddQuickField("إلى تاريخ", toDate);
            AddQuickField("المصدر", source);
            AddAntdQuickAction("quickJournalRefresh", "تحديث", AppIcon.Refresh, ReloadQuickWorkspace);

            AddSeparator("إضافة قيد يدوي سريع");
            AddQuickField("حساب مدين", debitAccount);
            AddQuickField("حساب دائن", creditAccount);
            AddQuickField("المبلغ", amount);
            AddQuickField("البيان", description);
            AddAntdQuickAction("quickJournalSave", "حفظ القيد", AppIcon.Save, SaveManualEntry);
            AddAntdQuickAction("quickJournalNew", "جديد", AppIcon.Add, NewQuickDraft);
        }

        private void AddQuickField(string caption, Control input)
        {
            Control surface = input;
            if (input is TextBox text)
            {
                text.Dock = DockStyle.None; text.MinimumSize = Size.Empty;
                surface = DesktopComponents.NativeTextFrame(text, input.Name + "Frame");
            }
            else if (input is ComboBox selector)
            {
                selector.Dock = DockStyle.None; selector.MinimumSize = Size.Empty;
                surface = DesktopComponents.NativeSelectFrame(selector, input.Name + "Frame", caption);
            }
            AddField(caption, surface);
        }
    }
}
