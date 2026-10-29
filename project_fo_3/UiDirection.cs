using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class UiDirection
    {
        internal static bool IsLeftToRight => UiLanguageCatalog.IsEnglishActive;

        internal static void Apply(Form form)
        {
            Apply(form, IsLeftToRight);
        }

        internal static void Apply(Form form, bool leftToRight)
        {
            if (form == null) return;
            form.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            form.RightToLeftLayout = !leftToRight;
            Apply((Control)form, leftToRight);
        }

        internal static void Apply(Control control)
        {
            Apply(control, IsLeftToRight);
        }

        internal static void Apply(Control control, bool leftToRight)
        {
            if (control == null) return;
            var nativeEditor = control as TextBox;
            if (nativeEditor != null && nativeEditor.Parent is AntdUI.Panel &&
                ReferenceEquals(nativeEditor.Parent.Tag, DesktopComponents.NativeTextFrameMarker))
            {
                ApplyContentDirection(nativeEditor, leftToRight);
                return;
            }
            bool structuredLeftToRight = !leftToRight && IsStructuredInput(control);
            control.RightToLeft = leftToRight || structuredLeftToRight ? RightToLeft.No : RightToLeft.Yes;

            Label label = control as Label;
            if (label != null)
            {
                // WinForms mirrors TextAlign again when a Label itself is RTL. Keep the label's
                // text surface physical, then choose the visible edge explicitly.
                label.RightToLeft = RightToLeft.No;
                label.TextAlign = DirectionalAlignment(label.TextAlign, leftToRight);
            }

            TextBox textBox = control as TextBox;
            if (textBox != null)
                textBox.TextAlign = leftToRight || structuredLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;

            AntdUI.DatePicker antdDate = control as AntdUI.DatePicker;
            AntdUI.InputNumber antdNumber = control as AntdUI.InputNumber;
            AntdUI.Input antdInput = control as AntdUI.Input;
            AntdUI.Select antdSelect = control as AntdUI.Select;
            AntdUI.Checkbox antdCheckbox = control as AntdUI.Checkbox;
            if (antdCheckbox != null)
                antdCheckbox.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;

            if (antdInput != null && antdDate == null && antdNumber == null)
                antdInput.TextAlign = leftToRight || structuredLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (antdSelect != null)
                antdSelect.TextAlign = leftToRight || structuredLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (antdDate != null)
            {
                antdDate.RightToLeft = RightToLeft.No;
                antdDate.TextAlign = HorizontalAlignment.Left;
            }
            if (antdNumber != null)
            {
                antdNumber.RightToLeft = RightToLeft.No;
                antdNumber.TextAlign = HorizontalAlignment.Left;
            }
            DateTimePicker picker = control as DateTimePicker;
            if (picker != null)
            {
                picker.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                picker.RightToLeftLayout = !leftToRight;
            }

            FlowLayoutPanel flow = control as FlowLayoutPanel;
            if (flow != null && (flow.FlowDirection == FlowDirection.LeftToRight || flow.FlowDirection == FlowDirection.RightToLeft))
                flow.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                DataGridViewContentAlignment alignment = leftToRight
                    ? DataGridViewContentAlignment.MiddleLeft
                    : DataGridViewContentAlignment.MiddleRight;
                grid.ColumnHeadersDefaultCellStyle.Alignment = alignment;
                grid.DefaultCellStyle.Alignment = alignment;
                // The empty-state caption follows the grid direction, so it is refreshed here.
                UiTheme.RefreshEmptyStateText(grid);
            }

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.TextAlign = DirectionalAlignment(checkBox.TextAlign, leftToRight);
                // WinForms mirrors MiddleLeft تلقائيًا عند RightToLeft.Yes؛ قلبه يدويًا
                // يسبب انعكاسًا مزدوجًا ويضع العلامة في الجهة الخاطئة.
                checkBox.CheckAlign = ContentAlignment.MiddleLeft;
            }

            RadioButton radio = control as RadioButton;
            if (radio != null)
            {
                radio.TextAlign = DirectionalAlignment(radio.TextAlign, leftToRight);
                radio.CheckAlign = ContentAlignment.MiddleLeft;
            }

            foreach (Control child in control.Controls) Apply(child, leftToRight);
        }

        internal static void ApplyContentDirection(TextBox input, bool interfaceLeftToRight)
        {
            if (input == null) return;
            if (input.PasswordChar != '\0' || input.UseSystemPasswordChar)
            {
                input.RightToLeft = RightToLeft.No;
                input.TextAlign = HorizontalAlignment.Left;
                return;
            }
            bool contentIsArabic = interfaceLeftToRight && StartsWithArabicText(input.Text);
            RightToLeft direction = !interfaceLeftToRight || contentIsArabic ? RightToLeft.Yes : RightToLeft.No;
            // Native WinForms TextBox mirrors alignment when RightToLeft is Yes.
            // Left is the physical right edge for Arabic content in this control.
            HorizontalAlignment alignment = HorizontalAlignment.Left;
            // A native TextBox recreates its HWND when its bidi direction changes.
            // During an undoable edit this destroys Windows' undo record and can
            // move the caret. Keep the existing editing direction until the next
            // programmatic value load/reset clears that record. Never alter text
            // or insert direction markers to compensate for the display.
            if (input.IsHandleCreated && input.CanUndo &&
                (input.RightToLeft != direction || input.TextAlign != alignment)) return;
            if (input.RightToLeft != direction) input.RightToLeft = direction;
            if (input.TextAlign != alignment) input.TextAlign = alignment;
        }

        internal static void ApplyContentDirection(AntdUI.Input input, bool interfaceLeftToRight)
        {
            if (input == null) return;
            bool contentIsArabic = interfaceLeftToRight && StartsWithArabicText(input.Text);
            input.RightToLeft = !interfaceLeftToRight || contentIsArabic ? RightToLeft.Yes : RightToLeft.No;
            input.TextAlign = !interfaceLeftToRight || contentIsArabic
                ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        internal static void ApplyContentDirection(ComboBox comboBox, bool interfaceLeftToRight)
        {
            if (comboBox == null) return;
            bool contentIsArabic = interfaceLeftToRight && StartsWithArabicText(comboBox.Text);
            comboBox.RightToLeft = !interfaceLeftToRight || contentIsArabic ? RightToLeft.Yes : RightToLeft.No;
        }

        internal static void ApplyContentDirection(AntdUI.Select select, bool interfaceLeftToRight)
        {
            if (select == null) return;
            bool contentIsArabic = interfaceLeftToRight && StartsWithArabicText(select.Text);
            select.RightToLeft = !interfaceLeftToRight || contentIsArabic ? RightToLeft.Yes : RightToLeft.No;
            select.TextAlign = !interfaceLeftToRight || contentIsArabic
                ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private static bool StartsWithArabicText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (char character in value)
            {
                if (character >= '\u0600' && character <= '\u08FF') return true;
                if (char.IsLetter(character)) return false;
            }
            return false;
        }

        private static ContentAlignment DirectionalAlignment(ContentAlignment current, bool leftToRight)
        {
            bool top = current == ContentAlignment.TopLeft || current == ContentAlignment.TopCenter || current == ContentAlignment.TopRight;
            bool bottom = current == ContentAlignment.BottomLeft || current == ContentAlignment.BottomCenter || current == ContentAlignment.BottomRight;
            if (top) return leftToRight ? ContentAlignment.TopLeft : ContentAlignment.TopRight;
            if (bottom) return leftToRight ? ContentAlignment.BottomLeft : ContentAlignment.BottomRight;
            return leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private static bool IsStructuredInput(Control control)
        {
            return ArabicUi.ShouldUseLeftToRightInput(control);
        }
    }

    internal static class UiDirectionSelfTests
    {
        internal static void RunAll()
        {
            VerifyCheckboxDirection();
            using (var form = BuildFixture())
            {
                UiDirection.Apply(form, false);
                Require(form.RightToLeft == RightToLeft.Yes && form.RightToLeftLayout, "Arabic form direction must be RTL.");
                Require(((Label)form.Controls["caption"]).TextAlign == ContentAlignment.MiddleRight &&
                    ((Label)form.Controls["caption"]).RightToLeft == RightToLeft.No,
                    "Arabic labels must align physically right without a second RTL mirror.");
                Require(((TextBox)form.Controls["description"]).RightToLeft == RightToLeft.Yes &&
                    ((TextBox)form.Controls["description"]).TextAlign == HorizontalAlignment.Right,
                    "Arabic text input must align right.");
                Require(((TextBox)form.Controls["barcodeInput"]).RightToLeft == RightToLeft.No &&
                    ((TextBox)form.Controls["barcodeInput"]).TextAlign == HorizontalAlignment.Left,
                    "Barcode input must remain LTR inside Arabic UI.");
                Require(((AntdUI.DatePicker)form.Controls["dateInput"]).RightToLeft == RightToLeft.No &&
                    ((AntdUI.DatePicker)form.Controls["dateInput"]).TextAlign == HorizontalAlignment.Left,
                    "Structured date input must remain LTR inside Arabic UI.");
                AntdUI.Input arabicAntdInput = (AntdUI.Input)form.Controls["arabicAntdInput"];
                AntdUI.Input phoneAntdInput = (AntdUI.Input)form.Controls["phoneAntdInput"];
                AntdUI.Select arabicSelect = (AntdUI.Select)form.Controls["arabicSelect"];
                AntdUI.InputNumber quantityInput = (AntdUI.InputNumber)form.Controls["quantityInput"];
                Require(arabicAntdInput.RightToLeft == RightToLeft.Yes && arabicAntdInput.TextAlign == HorizontalAlignment.Right,
                    "Arabic AntdUI text must align right.");
                Require(phoneAntdInput.RightToLeft == RightToLeft.No && phoneAntdInput.TextAlign == HorizontalAlignment.Left,
                    "Phone input must stay LTR and align left inside Arabic UI.");
                Require(arabicSelect.RightToLeft == RightToLeft.Yes && arabicSelect.TextAlign == HorizontalAlignment.Right,
                    "Arabic AntdUI selection must align right.");
                Require(quantityInput.RightToLeft == RightToLeft.No && quantityInput.TextAlign == HorizontalAlignment.Left,
                    "AntdUI quantity must remain LTR and align left inside Arabic UI.");
                Require(((FlowLayoutPanel)form.Controls["actions"]).FlowDirection == FlowDirection.RightToLeft,
                    "Arabic action order must start at the right.");
                Require(((DataGridView)form.Controls["records"]).RightToLeft == RightToLeft.Yes,
                    "Arabic grid direction must be RTL.");

                UiDirection.Apply(form, true);
                Require(form.RightToLeft == RightToLeft.No && !form.RightToLeftLayout, "English form direction must be LTR.");
                Require(((Label)form.Controls["caption"]).TextAlign == ContentAlignment.MiddleLeft, "English labels must align left.");
                Require(((TextBox)form.Controls["description"]).RightToLeft == RightToLeft.No &&
                    ((TextBox)form.Controls["description"]).TextAlign == HorizontalAlignment.Left,
                    "English text input must align left.");
                TextBox nativeDescription = (TextBox)form.Controls["description"];
                nativeDescription.Text = "باراسيتامول 500 mg";
                UiDirection.ApplyContentDirection(nativeDescription, true);
                Require(nativeDescription.RightToLeft == RightToLeft.Yes &&
                    nativeDescription.TextAlign == HorizontalAlignment.Left &&
                    nativeDescription.Text == "باراسيتامول 500 mg",
                    "Arabic-first native text must use RTL without changing its stored value in English UI.");
                nativeDescription.Text = "Paracetamol 500 mg";
                UiDirection.ApplyContentDirection(nativeDescription, true);
                Require(nativeDescription.RightToLeft == RightToLeft.No &&
                    nativeDescription.TextAlign == HorizontalAlignment.Left,
                    "Latin-first native text must restore LTR in English UI.");
                Require(((AntdUI.DatePicker)form.Controls["dateInput"]).RightToLeft == RightToLeft.No &&
                    ((AntdUI.DatePicker)form.Controls["dateInput"]).TextAlign == HorizontalAlignment.Left,
                    "Structured date input must remain LTR in the English interface.");
                Require(arabicAntdInput.RightToLeft == RightToLeft.No && arabicAntdInput.TextAlign == HorizontalAlignment.Left,
                    "AntdUI text must align left in English UI.");
                Require(phoneAntdInput.RightToLeft == RightToLeft.No && phoneAntdInput.TextAlign == HorizontalAlignment.Left,
                    "Phone input must remain LTR and align left in English UI.");
                Require(arabicSelect.RightToLeft == RightToLeft.No && arabicSelect.TextAlign == HorizontalAlignment.Left,
                    "AntdUI selection must align left in English UI.");
                arabicAntdInput.Text = "باراسيتامول 500 mg";
                UiDirection.ApplyContentDirection(arabicAntdInput, true);
                Require(arabicAntdInput.RightToLeft == RightToLeft.Yes && arabicAntdInput.TextAlign == HorizontalAlignment.Right,
                    "Arabic content must keep its natural direction inside an English AntdUI field.");
                arabicAntdInput.Text = "Paracetamol 500 mg";
                UiDirection.ApplyContentDirection(arabicAntdInput, true);
                Require(arabicAntdInput.RightToLeft == RightToLeft.No && arabicAntdInput.TextAlign == HorizontalAlignment.Left,
                    "English content must restore LTR direction inside an English AntdUI field.");
                Require(quantityInput.RightToLeft == RightToLeft.No && quantityInput.TextAlign == HorizontalAlignment.Left,
                    "AntdUI quantity must remain LTR and align left in English UI.");
                Require(((FlowLayoutPanel)form.Controls["actions"]).FlowDirection == FlowDirection.LeftToRight,
                    "English action order must start at the left.");
                Require(((DataGridView)form.Controls["records"]).RightToLeft == RightToLeft.No,
                    "English grid direction must be LTR.");
            }
            VerifyNativeUndoDirection();
        }

        // Send only to edit controls owned by this in-process fixture. No desktop
        // keyboard, clipboard, foreground window, database, or saved settings.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr parameter, string text);

        private static void VerifyNativeUndoDirection()
        {
            foreach (bool english in new[] { false, true })
            foreach (string original in new[] { "صيدلية المُهندس 500 mg", "Pharmacy 500 mg", "" })
            using (var host = new Form())
            using (var input = new TextBox())
            {
                host.Controls.Add(input);
                input.Text = original;
                UiDirection.ApplyContentDirection(input, english);
                IntPtr originalHandle = input.Handle;
                RightToLeft originalDirection = input.RightToLeft;
                input.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(input, english);
                string replacement = original.StartsWith("صيدلية", StringComparison.Ordinal)
                    ? "TEST-123" : "صيدلية المُهندس 500 mg";
                input.SelectAll();
                SendMessage(input.Handle, 0x00C2, new IntPtr(1), replacement); // EM_REPLACESEL, undoable.
                Require(input.Text == replacement && input.CanUndo && input.Handle == originalHandle,
                    "Mixed-script replacement must preserve native undo and the edit handle.");
                Require(input.RightToLeft == originalDirection &&
                    input.SelectionStart == replacement.Length && input.SelectionLength == 0,
                    "Undoable edits must retain writing direction and the insertion position.");
                // Reapplying presentation (for example after a layout pass) must not clear history.
                UiDirection.ApplyContentDirection(input, english);
                input.Undo();
                Require(input.Text == original && input.Handle == originalHandle,
                    "Native Undo must restore the exact mixed text, including diacritics.");
                input.Undo(); // Standard native edit undo toggles the last operation.
                Require(input.Text == replacement, "Native undo toggle must restore the replacement.");

                input.ClearUndo();
                input.Select(input.Text.Length - 1, 1);
                SendMessage(input.Handle, 0x00C2, new IntPtr(1), string.Empty);
                Require(input.Text == replacement.Substring(0, replacement.Length - 1) && input.CanUndo,
                    "Deleting the final character must remain undoable.");
                input.Undo();
                Require(input.Text == replacement, "Undo must restore the deleted character.");

                input.Text = "Loaded English value";
                UiDirection.ApplyContentDirection(input, english);
                Require(!input.CanUndo && input.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes),
                    "A programmatic value load must adopt the current interface direction.");
                input.Text = "قيمة عربية جديدة";
                UiDirection.ApplyContentDirection(input, english);
                Require(!input.CanUndo && input.RightToLeft == RightToLeft.Yes,
                    "A loaded Arabic value must use RTL after the previous edit history is reset.");
            }
        }

        private static void VerifyCheckboxDirection()
        {
            using (var form = new Form())
            {
                var host = new Panel();
                var checkbox = DesktopComponents.Checkbox("directionCheckbox", "الرصيد الافتتاحي", true);
                form.Controls.Add(host); host.Controls.Add(checkbox);
                int changes = 0;
                checkbox.CheckedChanged += (sender, args) => changes++;
                foreach (bool english in new[] { false, true, false, true })
                {
                    UiDirection.Apply(form, english);
                    Require(checkbox.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) &&
                        checkbox.TextAlign == (english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight),
                        "Nested AntdUI checkbox caption must follow the interface direction.");
                    Require(checkbox.Checked && changes == 0 && checkbox.Text == "الرصيد الافتتاحي",
                        "Direction changes must preserve the checkbox value and stored caption without filter events.");
                }
            }
        }
        private static Form BuildFixture()
        {
            var form = new Form();
            form.Controls.Add(new Label { Name = "caption", Text = "العنوان", TextAlign = ContentAlignment.MiddleCenter });
            form.Controls.Add(new TextBox { Name = "description", AccessibleName = "الوصف" });
            form.Controls.Add(new TextBox { Name = "barcodeInput", AccessibleName = "الباركود" });
            form.Controls.Add(DesktopComponents.DatePicker("dateInput", "التاريخ", DateTime.Today));
            form.Controls.Add(DesktopComponents.Input("arabicAntdInput", "اسم الدواء"));
            form.Controls.Add(DesktopComponents.Input("phoneAntdInput", "رقم الهاتف"));
            form.Controls.Add(DesktopComponents.Select("arabicSelect", "اختيار الدواء", "باراسيتامول 500 mg"));
            form.Controls.Add(DesktopComponents.Quantity("quantityInput", "الكمية", 1.25m));
            form.Controls.Add(new FlowLayoutPanel { Name = "actions", FlowDirection = FlowDirection.LeftToRight });
            form.Controls.Add(new DataGridView { Name = "records" });
            return form;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}


