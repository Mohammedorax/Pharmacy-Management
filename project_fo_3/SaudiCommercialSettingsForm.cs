using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // شاشة ضبط آمنة للمزايا السعودية. لا تتيح الإنتاج الحي، ولا تحذف أي بيانات عند التعطيل.
    internal sealed class SaudiCommercialSettingsForm : Form
    {
        private readonly DataGridView grid;
        private readonly TextBox reasonText;
        private readonly Label statusLabel;
        private readonly AntdUI.Button saveButton;
        private readonly AntdUI.Button closeButton;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        internal bool PresentationLeftToRight => presentationLeftToRight;
        private IReadOnlyList<SaudiCommercialFeature> features;
        private bool requiresReload;
        private bool saving;

        public SaudiCommercialSettingsForm() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal SaudiCommercialSettingsForm(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        private readonly Func<IReadOnlyList<SaudiCommercialFeature>> readFeatures;
        private readonly Action<IReadOnlyDictionary<string, SaudiFeatureMode>, string> changeModes;

        internal SaudiCommercialSettingsForm(bool previewMode, bool presentationLeftToRight)
            : this(previewMode, presentationLeftToRight, SaudiCommercialFeatureSettings.GetAll,
                SaudiCommercialFeatureSettings.ChangeModes) { }

        internal SaudiCommercialSettingsForm(bool previewMode, bool presentationLeftToRight,
            Func<IReadOnlyList<SaudiCommercialFeature>> readFeatures,
            Action<IReadOnlyDictionary<string, SaudiFeatureMode>, string> changeModes)
        {
            this.readFeatures = readFeatures ?? throw new ArgumentNullException(nameof(readFeatures));
            this.changeModes = changeModes ?? throw new ArgumentNullException(nameof(changeModes));
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            Text = Present("إعدادات المعايير السعودية");
            Name = "SaudiCommercialSettingsForm";
            RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !presentationLeftToRight;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(880, 570);
            MinimumSize = new Size(620, SizeFromClientSize(new Size(620, 470)).Height);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            Font = AppFonts.Create(9.5F);
            BackColor = UiTheme.Background;

            ContentAlignment readingAlignment = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            Panel header = new Panel { Name = "saudiCommercialHeader", Dock = DockStyle.Top, Height = 104, Padding = new Padding(22, 12, 22, 10) };
            Label title = new Label { Name = "saudiCommercialTitle", Text = Present("المزايا المنظمة — السعودية"), Dock = DockStyle.Top, Height = 36, TextAlign = readingAlignment, AutoEllipsis = true, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label hint = new Label { Name = "saudiCommercialHint", Text = Present("الإلغاء يوقف الاستخدام الجديد فقط ولا يحذف فواتير أو دفعات. الإنتاج الحي محجوب حتى الاعتماد الرسمي."), Dock = DockStyle.Fill, TextAlign = readingAlignment, AutoEllipsis = false };
            header.Controls.Add(hint);
            header.Controls.Add(title);

            Panel body = new Panel { Name = "saudiCommercialBody", Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 18), BackColor = UiTheme.Background };
            AntdUI.Panel card = DesktopComponents.Card("saudiCommercialCard");
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18);

            grid = new DataGridView
            {
                Name = "saudiFeaturesGrid",
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = Present("المزايا المنظمة السعودية")
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FeatureKey", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Feature", HeaderText = Present("الميزة"), ReadOnly = true, Width = 185 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = Present("الوصف"), ReadOnly = true, MinimumWidth = 220, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentMode", HeaderText = Present("الحالة الحالية"), ReadOnly = true, Width = 120 });
            DataGridViewComboBoxColumn modeColumn = new DataGridViewComboBoxColumn { Name = "RequestedMode", HeaderText = Present("التغيير"), Width = 120, FlatStyle = FlatStyle.Flat };
            modeColumn.Items.Add(DisplayMode(SaudiFeatureMode.Disabled, presentationLeftToRight));
            modeColumn.Items.Add(DisplayMode(SaudiFeatureMode.Simulator, presentationLeftToRight));
            modeColumn.Items.Add(DisplayMode(SaudiFeatureMode.Test, presentationLeftToRight));
            grid.Columns.Add(modeColumn);
            grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;

            Panel reasonPanel = new Panel { Name = "saudiCommercialReasonPanel", Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(0, 10, 0, 0) };
            Label reasonLabel = new Label { Name = "saudiCommercialReasonLabel", Text = Present("سبب التغيير للتدقيق"), Dock = DockStyle.Top, Height = 24, TextAlign = readingAlignment };
            reasonText = new TextBox { Name = "saudiCommercialReasonText", AutoSize = false, MaxLength = 500, AccessibleName = Present("سبب الإضافة أو الإلغاء") };
            AntdUI.Panel reasonFrame = DesktopComponents.NativeTextFrame(reasonText, "saudiCommercialReasonFrame");
            reasonFrame.Dock = DockStyle.Bottom;
            reasonFrame.Height = 44;
            reasonPanel.Controls.Add(reasonFrame);
            reasonPanel.Controls.Add(reasonLabel);

            statusLabel = new Label { Name = "saudiCommercialStatus", Dock = DockStyle.Bottom, Height = 46, TextAlign = readingAlignment, AutoEllipsis = true, Padding = new Padding(8, 0, 8, 0), ForeColor = UiTheme.MutedText, AccessibleName = Present("حالة إعدادات المعايير السعودية") };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Name = "saudiCommercialActions",
                Dock = DockStyle.Bottom,
                Height = 70,
                FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(24, 13, 24, 0),
                BackColor = UiTheme.Background
            };
            saveButton = new AccessibleAntdButton { Name = "saveSaudiCommercialSettings", Text = Present("حفظ التغييرات المدققة"), Width = 200, Height = 44, AccessibleName = Present("حفظ التغييرات المدققة"), IconSvg = OutlinedIcons.Svg(AppIcon.Save, true), IconSize = new Size(18, 18), IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right };
            closeButton = new AccessibleAntdButton { Name = "closeSaudiCommercialSettings", Text = Present("إغلاق"), Width = 120, Height = 44, AccessibleName = Present("إغلاق إعدادات المعايير السعودية"), IconSvg = OutlinedIcons.Svg(AppIcon.Close, true), IconSize = new Size(18, 18), IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right };
            saveButton.Click += SaveChanges;
            closeButton.Click += (sender, args) => Close();
            actions.Controls.Add(saveButton);
            actions.Controls.Add(closeButton);

            card.Controls.Add(grid);
            card.Controls.Add(reasonPanel);
            card.Controls.Add(statusLabel);
            body.Controls.Add(card);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);

            UiTheme.Apply(this);
            UiDirection.Apply(this, presentationLeftToRight);
            if (previewMode)
            {
                Control runtimeStatus = Controls.Find("__UiStatusStrip", true).Length == 0 ? null : Controls.Find("__UiStatusStrip", true)[0];
                if (runtimeStatus != null) runtimeStatus.Dispose();
            }
            UiTheme.ApplyDialogHeader(header, title, hint, null);
            UiTheme.ApplyDataGridQuality(grid);
            UiTheme.ApplyFieldLabels(reasonLabel);
            UiTheme.ApplyInfoLabels(statusLabel);
            statusLabel.BorderStyle = BorderStyle.None;
            statusLabel.TextAlign = readingAlignment;
            DesktopComponents.StyleNativeTextFrame(reasonFrame, reasonText);
            StartPosition = FormStartPosition.CenterParent;
            header.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            hint.ForeColor = UiTheme.MutedText;
            title.RightToLeft = hint.RightToLeft = RightToLeft.No;
            title.TextAlign = hint.TextAlign = readingAlignment;
            actions.BackColor = UiTheme.Background;
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            reasonPanel.BackColor = UiTheme.Surface;
            reasonLabel.BackColor = UiTheme.Surface;
            reasonLabel.ForeColor = UiTheme.Text;
            reasonText.BackColor = UiTheme.Surface;
            reasonText.ForeColor = UiTheme.Text;
            reasonText.Leave += (sender, args) => ApplyReasonDirection();
            reasonText.TextChanged += (sender, args) => ApplyReasonDirection();
            ApplyReasonDirection();
            DesktopComponents.StyleOutlinedFooterAction(saveButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            saveButton.Text = saveButton.AccessibleName = Present("حفظ التغييرات المدققة");
            closeButton.Text = Present("إغلاق");
            closeButton.AccessibleName = Present("إغلاق إعدادات المعايير السعودية");
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 60;
            grid.ScrollBars = ScrollBars.Both;
            AntdThemeAdapter.Apply(card);
            header.AccessibleName = title.Text;
            body.AccessibleName = Present("إعدادات المعايير السعودية");
            card.AccessibleName = grid.AccessibleName;
            reasonPanel.AccessibleName = reasonLabel.Text;
            reasonFrame.AccessibleName = reasonText.AccessibleName;
            actions.AccessibleName = saveButton.Text;
            grid.Columns["Description"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            Action layoutHeader = delegate
            {
                int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
                title.Height = TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                int hintHeight = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                header.Height = header.Padding.Vertical + title.Height + hintHeight;
                title.AutoEllipsis = false;
                header.AccessibleName = title.AccessibleName = title.Text;
                hint.AccessibleName = hint.Text;
            };
            header.Resize += (sender, args) => layoutHeader();
            layoutHeader();
            Action layoutBody = delegate
            {
                bool shortWindow = ClientSize.Height < 560;
                body.Padding = shortWindow ? new Padding(24, 12, 24, 12) : new Padding(24, 20, 24, 18);
                card.Padding = new Padding(shortWindow ? 12 : 18);
                actions.Height = shortWindow ? 64 : 70;
                actions.Padding = new Padding(24, shortWindow ? 10 : 13, 24, 0);
            };
            Resize += (sender, args) => layoutBody();
            layoutBody();
            if (previewMode)
            {
                saveButton.Enabled = false;
                AddFeatureRow("simulation", "فواتير تجريبية", "إعدادات المحاكاة والتحقق المحلي", SaudiFeatureMode.Simulator);
                AddFeatureRow("testing", "الربط الضريبي", "اختبار التكامل دون إنتاج حي", SaudiFeatureMode.Test);
                UiTheme.SetStatus(statusLabel, Present("معاينة تخطيط فقط؛ الحفظ غير متاح."), null);
            }
            else Load += (sender, args) => ReloadFeatures();
        }

        private bool HasSettingsAccess()
        {
            if (IsDisposed || Disposing) return false;
            if (PermissionService.CanOpen(AppPermission.Settings)) return true;
            Close();
            return false;
        }

        private void UpdateSaveAvailability()
        {
            if (!IsDisposed && !Disposing)
                saveButton.Enabled = !previewMode && !saving && !requiresReload && features != null
                    && PermissionService.CanOpen(AppPermission.Settings);
        }

        private void MarkFeaturesStale()
        {
            requiresReload = true;
            features = null;
            grid.Enabled = false;
            saveButton.Enabled = false;
        }

        private bool ReloadFeaturesCore()
        {
            if (!HasSettingsAccess()) return false;
            MarkFeaturesStale();
            var loaded = readFeatures();
            if (!HasSettingsAccess()) return false;
            grid.Rows.Clear();
            foreach (SaudiCommercialFeature feature in loaded)
                AddFeatureRow(feature.FeatureKey, feature.DisplayName, feature.Description, feature.Mode);
            UiTheme.SetStatus(statusLabel, Present("الحالات: معطل، محاكاة، اختبار. وضع الإنتاج محجوب في هذه النسخة."), null);
            statusLabel.AccessibleName = statusLabel.Text;
            features = loaded;
            requiresReload = false;
            grid.Enabled = true;
            UpdateSaveAvailability();
            return true;
        }

        private void ReloadFeatures()
        {
            try { ReloadFeaturesCore(); }
            catch (Exception ex)
            {
                MarkFeaturesStale();
                string supportCode = AppDiagnostics.Log("SaudiCommercialSettings:Load", ex);
                UiTheme.SetStatus(statusLabel, Present("لا تتوفر إعدادات المعايير السعودية. رمز الدعم: ") + supportCode, false);
                statusLabel.AccessibleName = statusLabel.Text;
            }
        }

        private void AddFeatureRow(string key, string name, string description, SaudiFeatureMode mode)
        {
            string visibleName = Present(name);
            string visibleDescription = Present(description);
            int index = grid.Rows.Add(key, visibleName, visibleDescription, DisplayMode(mode, presentationLeftToRight), DisplayMode(mode, presentationLeftToRight));
            foreach (DataGridViewCell cell in grid.Rows[index].Cells)
                cell.ToolTipText = Convert.ToString(cell.Value);
        }

        private void SaveChanges(object sender, EventArgs e)
        {
            if (previewMode || saving || requiresReload || features == null) return;
            if (!HasSettingsAccess()) return;
            bool completed = false;
            var changes = new Dictionary<string, SaudiFeatureMode>(StringComparer.OrdinalIgnoreCase);
            saving = true;
            UpdateSaveAvailability();
            try
            {
                if (grid.IsCurrentCellDirty && !grid.CommitEdit(DataGridViewDataErrorContexts.Commit))
                    throw new InvalidOperationException("تعذر اعتماد حالة الميزة المحددة.");
                if (!grid.EndEdit())
                    throw new InvalidOperationException("أكمل تعديل الحالة قبل الحفظ.");
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string key = Convert.ToString(row.Cells["FeatureKey"].Value);
                    SaudiFeatureMode requested;
                    if (!TryReadMode(Convert.ToString(row.Cells["RequestedMode"].Value), out requested))
                        throw new InvalidOperationException("اختر حالة صالحة لكل ميزة.");
                    SaudiCommercialFeature current = FindFeature(key);
                    if (current.Mode == requested) continue;
                    changes.Add(key, requested);
                }
                if (changes.Count == 0)
                {
                    UiTheme.SetStatus(statusLabel, Present("لا توجد تغييرات للحفظ."), null);
                    statusLabel.AccessibleName = statusLabel.Text;
                    return;
                }

                changeModes(changes, reasonText.Text);
                completed = true;
                if (!HasSettingsAccess()) return;
                requiresReload = true;
                reasonText.Clear();
                if (!ReloadFeaturesCore()) return;
                AppNotifications.Show(this, Present("تم حفظ الإعدادات مع سجل تدقيق. لم تُحذف أي بيانات."), Present("تم الحفظ"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (completed)
                {
                    MarkFeaturesStale();
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "حفظ إعدادات المعايير السعودية",
                        string.Join(", ", changes.Keys), ex);
                }
                else UiTheme.ShowOperationIssue(this, Present("حفظ إعدادات المعايير السعودية"), ex);
            }
            finally
            {
                saving = false;
                UpdateSaveAvailability();
            }
        }

        private SaudiCommercialFeature FindFeature(string key)
        {
            foreach (SaudiCommercialFeature feature in features)
                if (string.Equals(feature.FeatureKey, key, StringComparison.OrdinalIgnoreCase)) return feature;
            throw new InvalidOperationException("الميزة غير موجودة.");
        }

        private static string ArabicMode(SaudiFeatureMode mode)
        {
            switch (mode)
            {
                case SaudiFeatureMode.Disabled: return "معطلة";
                case SaudiFeatureMode.Simulator: return "محاكاة";
                case SaudiFeatureMode.Test: return "اختبار";
                default: return "إنتاج";
            }
        }

        private static string DisplayMode(SaudiFeatureMode mode, bool leftToRight)
        {
            return leftToRight ? mode.ToString() : ArabicMode(mode);
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void ApplyReasonDirection()
        {
            UiDirection.ApplyContentDirection(reasonText, presentationLeftToRight);
        }

        internal static bool TryReadMode(string value, out SaudiFeatureMode mode)
        {
            foreach (SaudiFeatureMode candidate in new[] { SaudiFeatureMode.Disabled, SaudiFeatureMode.Simulator, SaudiFeatureMode.Test })
            {
                if (string.Equals(value, ArabicMode(candidate), StringComparison.Ordinal) ||
                    string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    mode = candidate;
                    return true;
                }
            }
            mode = SaudiFeatureMode.Disabled;
            return false;
        }
    }
}
