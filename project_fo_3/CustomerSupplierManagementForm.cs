using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Master-data screen for customers; supplier maintenance remains on the existing Manufacturer workflow.
    internal sealed partial class CustomerSupplierManagementForm : Form
    {
        private const int WideLayoutBreakpoint = 1220;
        private const int CompactEditorHeight = 245;

        private CancellationTokenSource readCancellation;
        private long readVersion;
        private bool loadingCustomers, savingCustomer;
        private readonly ISalesCustomerService customerService;
        private readonly bool useDefaultCustomerRead;
        private Func<bool> customerListContextIsCurrent;
        private bool invalidatingCustomerList;
        private readonly bool selectionMode;
        private readonly bool previewMode;
        private bool leftToRight;
        private readonly AntdUI.Table customers = DesktopComponents.Table("customerMasterTable", "قائمة العملاء");
        private readonly TextBox search = new TextBox { Name = "customerSearch", BorderStyle = BorderStyle.None };
        private AntdUI.Panel searchFrame;
        private Label searchHint;
        // AntdUI 2.4.11 reverses Latin fragments in mixed Arabic customer names while editing.
        private readonly TextBox name = new TextBox { Name = "customerName", MaxLength = 200, AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle };
        private readonly AntdUI.Input phone = DesktopComponents.Input("customerPhone", "رقم الهاتف");
        private readonly TextBox address = new TextBox { Name = "customerAddress", MaxLength = 300, AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle };
        private readonly Label status = new Label { Name = "customerManagementStatus", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly List<SalesCustomer> allCustomers = new List<SalesCustomer>();
        private readonly List<SalesCustomer> visibleCustomers = new List<SalesCustomer>();
        private string editingCustomerId = string.Empty;

        private TableLayoutPanel rootLayout;
        private TableLayoutPanel workspace;
        private AntdUI.Panel editorSurface;
        private TableLayoutPanel editorLayout;
        private TableLayoutPanel editorFields;
        private Control nameField;
        private Control phoneField;
        private Control addressField;
        private AntdUI.Panel nameFrame;
        private AntdUI.Panel addressFrame;
        private FlowLayoutPanel editorActions;
        private Label editorTitle;
        private AntdUI.Button suppliersButton;
        private AntdUI.Button reloadButton;
        private AntdUI.Button addButton;
        private AntdUI.Button saveButton;
        private AntdUI.Button chooseButton;

        internal SalesCustomer SelectedCustomer { get; private set; }

        internal CustomerSupplierManagementForm(bool selectionMode = false, bool previewMode = false, bool? presentationLeftToRight = null, ISalesCustomerService customerService = null)
        {
            this.customerService = customerService ?? ApplicationServices.SalesCustomers;
            useDefaultCustomerRead = customerService == null;
            this.selectionMode = selectionMode;
            this.previewMode = previewMode;
            leftToRight = presentationLeftToRight ?? UiDirection.IsLeftToRight;
            Text = UiLanguageCatalog.GetDisplayText(selectionMode ? "اختيار عميل" : "إدارة العملاء");
            Name = "CustomerSupplierManagementForm";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 590);
            Size = new Size(1120, 760);
            AutoScaleMode = AutoScaleMode.Dpi;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BuildLayout();
            UiTheme.Apply(this);
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            AntdThemeAdapter.SynchronizePalette();
            ApplyCustomerTableDirection(leftToRight);
            ApplyFormTheme();
            RefreshResponsiveLayout();
            ApplyCustomerInputDirections();
            ApplyPresentation(leftToRight);
            SizeChanged += (sender, args) => RefreshResponsiveLayout();
            Load += async (sender, args) => { if (previewMode) PopulatePreviewData(); else { InitializeCustomerRecovery(); await LoadCustomers(); RestoreCustomerRecoveryEditor(); } };
            FormClosed += (sender, args) => CancelCustomerRead();
            Disposed += (sender, args) => CancelCustomerRead();
            ConfigureCustomerRecovery();
            UiSessionSubscription.Attach(this, () =>
            {
                if (previewMode) return;
                CancelCustomerRead();
                loadingCustomers = false;
                customerListContextIsCurrent = null;
                EnsureCurrentCustomerList();
                search.Enabled = true;
                chooseButton.Enabled = false;
                if (!selectionMode)
                {
                    name.ReadOnly = true;
                    phone.Enabled = false;
                    address.ReadOnly = true;
                    addButton.Enabled = false;
                    saveButton.Enabled = false;
                }
            });
        }

        private string Present(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        // Allows direction testing without enabling English across unfinished screens.
        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Present(selectionMode ? "اختيار عميل" : "إدارة العملاء");
            Controls.Find("customerManagementTitle", true)[0].Text = Text;
            Controls.Find("customerManagementSubtitle", true)[0].Text = Present(selectionMode
                ? "ابحث عن عميل مسجل ثم اختره للفاتورة" : "إدارة بيانات العملاء واختيارهم لفواتير البيع");
            editorTitle.Text = Present(selectionMode && SelectedCustomer != null ? "تم تحديد العميل"
                : selectionMode ? "اختيار العميل" : "بيانات العميل");
            searchHint.Text = Present("ابحث بالاسم أو رقم الهاتف");
            reloadButton.Text = Present("تحديث");
            reloadButton.AccessibleName = Present("إعادة تحميل قائمة العملاء");
            search.AccessibleName = Present("ابحث بالاسم أو رقم الهاتف");
            search.AccessibleDescription = Present("يتم تحديث قائمة العملاء أثناء الكتابة.");
            customers.AccessibleName = Present("قائمة العملاء");
            phone.AccessibleDescription = Present("أدخل رقم الهاتف بأرقام متصلة.");
            status.AccessibleName = null;
            status.AccessibleDescription = Present("حالة حفظ العميل");
            foreach (var field in new[] { new { Name = "customerNameFieldLabel", Text = "اسم العميل", Input = (Control)name },
                new { Name = "customerPhoneFieldLabel", Text = "رقم الهاتف", Input = (Control)phone },
                new { Name = "customerAddressFieldLabel", Text = "العنوان", Input = (Control)address } })
            {
                Control[] labels = Controls.Find(field.Name, true);
                if (labels.Length > 0) labels[0].Text = Present(field.Text);
                field.Input.AccessibleName = Present(field.Text);
            }
            if (suppliersButton != null)
            {
                suppliersButton.Text = Present("إدارة الموردين");
                suppliersButton.AccessibleName = suppliersButton.Text;
            }
            chooseButton.Text = Present("اختيار العميل");
            chooseButton.AccessibleName = Present("اختيار العميل المحدد");
            if (addButton != null)
            {
                addButton.Text = Present("جديد");
                addButton.AccessibleName = Present("عميل جديد");
                saveButton.Text = Present("حفظ");
                saveButton.AccessibleName = Present("حفظ بيانات العميل");
            }
            UiDirection.Apply(this, leftToRight);
            ApplyCustomerTableDirection(leftToRight);
            RefreshResponsiveLayout();
            ApplyFormTheme();
            ApplyCustomerRecoveryButtons();
        }

        internal static AppPermission RequiredPermission(bool selectionMode)
        {
            return selectionMode ? AppPermission.Sales : AppPermission.ManageManufacturers;
        }

        private bool EnsureCanOpen()
        {
            if (PermissionService.CanOpen(RequiredPermission(selectionMode))) return true;
            AppNotifications.Show(Present(selectionMode ? "لا تملك صلاحية اختيار عميل للمبيعات." :
                "لا تملك صلاحية إدارة العملاء والموردين."),
                Present("الصلاحيات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void BuildLayout()
        {
            rootLayout = new TableLayoutPanel
            {
                Name = "customerManagementLayout", Dock = DockStyle.Fill, Padding = new Padding(16),
                ColumnCount = 1, RowCount = 3
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(rootLayout);

            BuildHeader();
            BuildSearch();
            BuildWorkspace();
            BuildEditor();
        }

        private void BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                Name = "customerManagementHeader", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                RightToLeft = RightToLeft.Yes, Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));

            var titleBlock = new Panel { Name = "customerManagementTitleBlock", Dock = DockStyle.Fill, Margin = Padding.Empty };
            var title = new Label
            {
                Name = "customerManagementTitle", Text = UiLanguageCatalog.GetDisplayText("إدارة العملاء"),
                Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(14F, FontStyle.Bold)
            };
            var subtitle = new Label
            {
                Name = "customerManagementSubtitle",
                Text = UiLanguageCatalog.GetDisplayText(selectionMode ? "ابحث عن عميل مسجل ثم اختره للفاتورة" : "إدارة بيانات العملاء واختيارهم لفواتير البيع"),
                Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.MutedText, Font = AppFonts.Create(9F)
            };
            titleBlock.Controls.Add(subtitle);
            titleBlock.Controls.Add(title);
            header.Controls.Add(titleBlock, 0, 0);

            if (!selectionMode)
            {
                suppliersButton = DesktopComponents.Action("openSupplierManagement", "إدارة الموردين", AppIcon.User, false);
                suppliersButton.Dock = DockStyle.Fill;
                suppliersButton.Click += async (s, e) => await OpenSuppliers();
                header.Controls.Add(suppliersButton, 1, 0);
            }
            rootLayout.Controls.Add(header, 0, 0);
        }

        private void BuildSearch()
        {
            var host = new TableLayoutPanel { Name = "customerSearchHost", Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 0, 0, 6) };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            searchHint = new Label { Name = "customerSearchHint", Dock = DockStyle.Fill,
                Text = Present("ابحث بالاسم أو رقم الهاتف"), TextAlign = ContentAlignment.MiddleRight };
            searchFrame = DesktopComponents.NativeTextFrame(search, "customerSearchFrame");
            searchFrame.Dock = DockStyle.Fill;
            searchFrame.Margin = Padding.Empty;
            searchFrame.BorderWidth = 1;
            searchFrame.BorderColor = UiTheme.Border;
            search.Font = AppFonts.Create(10F);
            search.AccessibleName = Present("ابحث بالاسم أو رقم الهاتف");
            search.AccessibleDescription = Present("يتم تحديث قائمة العملاء أثناء الكتابة.");
            search.TextChanged += (s, e) => BindCustomers();
            host.Controls.Add(searchHint, 0, 0);
            host.SetColumnSpan(searchHint, 2);
            host.Controls.Add(searchFrame, 0, 1);
            reloadButton = DesktopComponents.Action("reloadCustomers", "تحديث", AppIcon.Refresh, false);
            reloadButton.Size = new Size(104, 44);
            reloadButton.Margin = new Padding(4, 0, 4, 0);
            reloadButton.Anchor = AnchorStyles.None;
            reloadButton.AccessibleName = Present("إعادة تحميل قائمة العملاء");
            reloadButton.Enabled = !previewMode;
            reloadButton.Click += async (sender, args) => await ReloadCustomerList();
            host.Controls.Add(reloadButton, 1, 1);
            rootLayout.Controls.Add(host, 0, 1);
        }

        private void BuildWorkspace()
        {
            workspace = new TableLayoutPanel
            {
                Name = "customerManagementWorkspace", Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = Padding.Empty, ColumnCount = 1, RowCount = 1
            };
            customers.Dock = DockStyle.Fill;
            customers.Margin = new Padding(0);
            customers.AccessibleName = UiLanguageCatalog.GetDisplayText("قائمة العملاء");
            customers.Columns = CreateCustomerColumns(UiDirection.IsLeftToRight);
            customers.SelectIndexChanged += (s, e) => LoadSelectedCustomer();
            workspace.Controls.Add(customers, 0, 0);
            rootLayout.Controls.Add(workspace, 0, 2);
        }

        private AntdUI.ColumnCollection CreateCustomerColumns(bool leftToRight)
        {
            AntdUI.ColumnAlign textAlignment = leftToRight ? AntdUI.ColumnAlign.Left : AntdUI.ColumnAlign.Right;
            if (selectionMode)
                return new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("CustomerName", Present("اسم العميل"), textAlignment) { Width = "fill" }
                };

            var columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("CustomerName", Present("اسم العميل"), textAlignment) { Width = "fill" },
                new AntdUI.Column("PhoneNumber", Present("رقم الهاتف"), AntdUI.ColumnAlign.Left) { Width = "150" },
                new AntdUI.Column("Address", Present("العنوان"), textAlignment) { Width = "180" },
                new AntdUI.Column("TotalSpent", Present("إجمالي المشتريات"), AntdUI.ColumnAlign.Left) { Width = "150" }
            };
            // AntdUI 2.4.11 aligns RTL text but keeps the physical column order.
            if (!leftToRight)
            {
                var reversed = new AntdUI.ColumnCollection();
                for (int index = columns.Count - 1; index >= 0; index--) reversed.Add(columns[index]);
                return reversed;
            }
            return columns;
        }

        private void BuildEditor()
        {
            editorSurface = DesktopComponents.Card("customerEditorSurface");
            editorSurface.Dock = DockStyle.Fill;
            editorSurface.Margin = new Padding(0);

            bool chooser = selectionMode;
            editorLayout = new TableLayoutPanel
            {
                Name = "customerEditor", Dock = DockStyle.Fill, ColumnCount = 1,
                RowCount = chooser ? 2 : 4, Padding = chooser ? new Padding(8) : new Padding(12), Margin = Padding.Empty,
                BackColor = UiTheme.Surface
            };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, chooser ? 24 : 28));
            if (chooser)
            {
                editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            }
            else
            {
                editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            }
            editorSurface.Controls.Add(editorLayout);

            editorTitle = new Label
            {
                Name = "customerEditorTitle",
                Text = UiLanguageCatalog.GetDisplayText(chooser ? "اختيار العميل" : "بيانات العميل"),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(10F, FontStyle.Bold), ForeColor = UiTheme.Text
            };
            editorLayout.Controls.Add(editorTitle, 0, 0);

            editorActions = new FlowLayoutPanel
            {
                Name = "customerEditorActions", Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.No,
                FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                WrapContents = false, AutoScroll = false, Margin = Padding.Empty, Padding = Padding.Empty
            };
            chooseButton = DesktopComponents.Action("chooseCustomer", "اختيار العميل", AppIcon.Confirm, true);
            chooseButton.Size = new Size(132, 44);
            chooseButton.Visible = chooser;
            chooseButton.Margin = Padding.Empty;
            chooseButton.AccessibleName = UiLanguageCatalog.GetDisplayText("اختيار العميل المحدد");
            if (chooser) chooseButton.Enabled = false;
            chooseButton.Click += (s, e) => ChooseSelectedCustomer();
            editorActions.Controls.Add(chooseButton);

            if (chooser)
            {
                editorLayout.Controls.Add(editorActions, 0, 1);
                return;
            }

            editorFields = new TableLayoutPanel
            {
                Name = "customerEditorFields", Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            nameField = CreateEditorField("customerNameField", "اسم العميل", name);
            phoneField = CreateEditorField("customerPhoneField", "رقم الهاتف", phone);
            addressField = CreateEditorField("customerAddressField", "العنوان", address);
            editorLayout.Controls.Add(editorFields, 0, 1);

            status.Dock = DockStyle.Fill;
            status.Margin = new Padding(0, 2, 0, 0);
            status.ForeColor = UiTheme.MutedText;
            status.Font = AppFonts.Create(8.5F);
            status.TextAlign = ContentAlignment.MiddleRight;
            status.AccessibleName = UiLanguageCatalog.GetDisplayText("حالة حفظ العميل");
            editorLayout.Controls.Add(status, 0, 2);

            addButton = DesktopComponents.Action("newCustomer", "جديد", AppIcon.Add, false);
            saveButton = DesktopComponents.Action("saveCustomer", "حفظ", AppIcon.Save, true);
            addButton.Size = new Size(96, 44);
            saveButton.Size = new Size(96, 44);
            addButton.Margin = saveButton.Margin = new Padding(4, 0, 4, 0);
            addButton.AccessibleName = UiLanguageCatalog.GetDisplayText("عميل جديد");
            saveButton.AccessibleName = UiLanguageCatalog.GetDisplayText("حفظ بيانات العميل");
            addButton.Click += (s, e) => ClearEditor();
            saveButton.Click += async (s, e) => await SaveCustomer();
            editorActions.Controls.Add(addButton);
            editorActions.Controls.Add(saveButton);
            editorLayout.Controls.Add(editorActions, 0, 3);
        }

        private Control CreateEditorField(string fieldName, string labelText, Control input)
        {
            var field = new TableLayoutPanel
            {
                Name = fieldName, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill,
                Margin = new Padding(3, 0, 3, 0), Padding = Padding.Empty,
                BackColor = UiTheme.Surface
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            field.Controls.Add(new Label
            {
                Name = fieldName + "Label", Text = UiLanguageCatalog.GetDisplayText(labelText),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(9F), ForeColor = UiTheme.MutedText
            }, 0, 0);
            input.Dock = DockStyle.Fill;
            input.AccessibleName = UiLanguageCatalog.GetDisplayText(labelText);
            input.Margin = Padding.Empty;
            if (fieldName == "customerPhoneField") input.AccessibleDescription = UiLanguageCatalog.GetDisplayText("أدخل رقم الهاتف بأرقام متصلة.");
            TextBox nativeText = input as TextBox;
            if (nativeText != null)
            {
                nativeText.BorderStyle = BorderStyle.None;
                nativeText.AutoSize = true;
                nativeText.Dock = DockStyle.None;
                var frame = DesktopComponents.NativeTextFrame(nativeText,
                    input == name ? "customerNameFrame" : "customerAddressFrame");
                frame.Dock = DockStyle.Fill;
                frame.Margin = Padding.Empty;
                if (input == name) nameFrame = frame;
                else addressFrame = frame;
                field.Controls.Add(frame, 0, 1);
            }
            else field.Controls.Add(input, 0, 1);
            return field;
        }

        private void RefreshResponsiveLayout()
        {
            if (workspace == null || editorSurface == null) return;
            LayoutHeader();
            editorActions.RightToLeft = RightToLeft.No;
            editorActions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            bool wide = ClientSize.Width >= WideLayoutBreakpoint;
            workspace.SuspendLayout();
            workspace.Controls.Clear();
            workspace.ColumnStyles.Clear();
            workspace.RowStyles.Clear();
            workspace.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            customers.RightToLeft = workspace.RightToLeft;

            if (selectionMode)
            {
                workspace.ColumnCount = 1;
                workspace.RowCount = 2;
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
                workspace.Controls.Add(customers, 0, 0);
                workspace.Controls.Add(editorSurface, 0, 1);
                workspace.ResumeLayout(true);
                ApplyCustomerInputDirections();
                return;
            }

            if (wide)
            {
                workspace.ColumnCount = 2;
                workspace.RowCount = 1;
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
                workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                workspace.Controls.Add(editorSurface, 0, 0);
                workspace.Controls.Add(customers, 1, 0);
            }
            else
            {
                workspace.ColumnCount = 1;
                workspace.RowCount = 2;
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, CompactEditorHeight));
                workspace.Controls.Add(customers, 0, 0);
                workspace.Controls.Add(editorSurface, 0, 1);
            }

            workspace.ResumeLayout(true);
            LayoutEditorFields();
            ApplyCustomerInputDirections();
        }

        private void LayoutHeader()
        {
            var header = Controls.Find("customerManagementHeader", true).FirstOrDefault() as TableLayoutPanel;
            if (header == null || rootLayout == null) return;
            int textWidth = Math.Max(100, rootLayout.ClientSize.Width - rootLayout.Padding.Horizontal -
                (selectionMode ? 0 : 190));
            var title = (Label)Controls.Find("customerManagementTitle", true)[0];
            var subtitle = (Label)Controls.Find("customerManagementSubtitle", true)[0];
            title.Height = Math.Max(40, TextRenderer.MeasureText(title.Text, title.Font,
                new Size(textWidth, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            subtitle.Height = Math.Max(28, TextRenderer.MeasureText(subtitle.Text, subtitle.Font,
                new Size(textWidth, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            title.AccessibleName = title.Text;
            subtitle.AccessibleName = subtitle.Text;
            rootLayout.RowStyles[0].Height = title.Height + subtitle.Height + 8;
            if (suppliersButton != null) { suppliersButton.Dock = DockStyle.None;
                suppliersButton.Size = new Size(180, 44);
                suppliersButton.Anchor = AnchorStyles.Top | (leftToRight ? AnchorStyles.Left : AnchorStyles.Right); }
        }

        private void LayoutEditorFields()
        {
            if (editorFields == null) return;
            bool wide = ClientSize.Width >= WideLayoutBreakpoint;
            editorFields.SuspendLayout();
            editorFields.Controls.Clear();
            editorFields.ColumnStyles.Clear();
            editorFields.RowStyles.Clear();
            editorFields.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            editorFields.ColumnCount = wide ? 1 : 3;
            editorFields.RowCount = wide ? 3 : 1;
            foreach (Control field in new[] { nameField, phoneField, addressField })
            {
                field.Dock = wide ? DockStyle.Top : DockStyle.Fill;
                field.Height = 69;
            }
            if (wide)
            {
                editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int row = 0; row < 3; row++) editorFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
                editorFields.Controls.Add(nameField, 0, 0);
                editorFields.Controls.Add(phoneField, 0, 1);
                editorFields.Controls.Add(addressField, 0, 2);
            }
            else
            {
                for (int column = 0; column < 3; column++) editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
                editorFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                editorFields.Controls.Add(nameField, 0, 0);
                editorFields.Controls.Add(phoneField, 1, 0);
                editorFields.Controls.Add(addressField, 2, 0);
            }
            editorFields.ResumeLayout(true);
        }

        private void ApplyCustomerInputDirections()
        {
            bool english = leftToRight;
            search.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            search.TextAlign = HorizontalAlignment.Left; // The mirrored RTL parent supplies physical right alignment.
            name.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            name.TextAlign = HorizontalAlignment.Left; // The mirrored RTL parent supplies physical right alignment.
            phone.RightToLeft = RightToLeft.No;
            phone.TextAlign = HorizontalAlignment.Left;
            address.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            address.TextAlign = HorizontalAlignment.Left; // The mirrored RTL parent supplies physical right alignment.
        }

        private void ApplyCustomerTableDirection(bool leftToRight)
        {
            customers.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            customers.Columns = CreateCustomerColumns(leftToRight);
        }

        private void ApplyFormTheme()
        {
            AntdThemeAdapter.Apply(editorSurface);
            AntdThemeAdapter.Apply(phone);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            searchHint.ForeColor = UiTheme.MutedText;
            editorTitle.ForeColor = UiTheme.Text;
            editorTitle.BackColor = UiTheme.Surface;
            editorActions.BackColor = UiTheme.Surface;
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            rootLayout.BackColor = UiTheme.Background;
            Control header = Controls.Find("customerManagementHeader", true).FirstOrDefault();
            if (header != null)
            {
                header.BackColor = UiTheme.Background;
                foreach (Control child in header.Controls)
                {
                    child.BackColor = UiTheme.Background;
                    foreach (Control label in child.Controls)
                    {
                        label.BackColor = UiTheme.Background;
                        label.ForeColor = label.Name == "customerManagementTitle" ? UiTheme.Text : UiTheme.MutedText;
                    }
                }
            }
            workspace.BackColor = UiTheme.Background;
            editorLayout.BackColor = UiTheme.Surface;
            status.ForeColor = UiTheme.MutedText;
            customers.BackColor = UiTheme.Surface;
            customers.ForeColor = UiTheme.Text;
            foreach (var pair in new[] { new { Frame = nameFrame, Input = name }, new { Frame = addressFrame, Input = address } })
            {
                pair.Input.BackColor = UiTheme.Surface;
                pair.Input.ForeColor = UiTheme.Text;
                if (pair.Frame == null) continue;
                DesktopComponents.StyleNativeTextFrame(pair.Frame, pair.Input);
            }
            if (editorFields != null)
            foreach (Control control in editorFields.Controls)
            {
                control.BackColor = UiTheme.Surface;
                foreach (Control child in control.Controls)
                    if (child is Label) { child.BackColor = Color.Transparent; child.ForeColor = UiTheme.MutedText; }
            }
            foreach (AntdUI.Button button in new[] { addButton, saveButton, chooseButton, suppliersButton, reloadButton })
            {
                if (button == null) continue;
                DesktopComponents.StyleOutlinedFooterAction(button);
                button.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                button.IconSize = new Size(18, 18);
            }
        }

        private void PopulatePreviewData()
        {
            allCustomers.Clear();
            allCustomers.Add(new SalesCustomer { CustomerId = "preview-1", CustomerName = "أحمد Pharma 500 mg",
                PhoneNumber = "777123456", Address = "صنعاء", TotalSpent = 1250.50m });
            allCustomers.Add(new SalesCustomer { CustomerId = "preview-2", CustomerName = "ليلى",
                PhoneNumber = "733456789", Address = "تعز", TotalSpent = 340m });
            BindCustomers();
            if (!selectionMode) customers.SelectedIndex = 1;
            if (addButton != null) addButton.Enabled = false;
            if (saveButton != null) saveButton.Enabled = false;
            if (suppliersButton != null) suppliersButton.Enabled = false;
            status.Text = Present("معاينة تخطيطية فقط؛ لم تُقرأ قاعدة البيانات ولن تُحفظ بيانات.");
        }

        private async Task ReloadCustomerList()
        {
            if (savingCustomer) return;
            InitializeCustomerRecovery();
            if (customerListContextIsCurrent != null && !EnsureCurrentCustomerList())
            {
                await LoadCustomers();
                return;
            }
            var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
            string employeeId = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureCustomerReadContext(actor, employeeId);
            string currentId = editingCustomerId;
            string currentName = name.Text, currentPhone = phone.Text, currentAddress = address.Text;
            await LoadCustomers(currentId);
            if (selectionMode || !readContextIsCurrent()) return;
            editingCustomerId = currentId;
            name.Text = currentName;
            phone.Text = currentPhone;
            address.Text = currentAddress;
            RestoreCustomerRecoveryEditor();
        }

        private async Task<bool> LoadCustomers(string selectedId = null)
        {
            if (!EnsureCanOpen()) { Close(); return false; }
            CancelCustomerRead();
            var cancellation = new CancellationTokenSource();
            readCancellation = cancellation;
            long version = readVersion;
            var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
            string employeeId = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureCustomerReadContext(actor, employeeId);
            var scopedRead = useDefaultCustomerRead ? ApplicationServices.CreateSalesCustomerReadSession() : null;
            loadingCustomers = true;
            customerListContextIsCurrent = null;
            allCustomers.Clear(); visibleCustomers.Clear();
            customers.DataSource = new DataTable();
            SelectedCustomer = null; chooseButton.Enabled = false;
            search.Enabled = false;
            ApplyWritePermissionState();
            status.Text = Present("جارٍ تحميل العملاء…");
            try
            {
                var readService = scopedRead == null ? customerService : scopedRead.Service;
                IReadOnlyList<SalesCustomer> rows = await readService.GetAllAsync(actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != readVersion || !readContextIsCurrent() ||
                    !PermissionService.CanOpen(RequiredPermission(selectionMode))) return false;
                customerListContextIsCurrent = readContextIsCurrent;
                allCustomers.AddRange(rows);
                BindCustomers();
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    int index = visibleCustomers.FindIndex(c => c.CustomerId == selectedId);
                    if (index >= 0) customers.SelectedIndex = index + 1;
                }
                if (allCustomers.Count == 0)
                    status.Text = Present("لا يوجد عملاء مسجلون بعد.");
                else
                    status.Text = string.Format("{0}: {1} — {2}", Present("إجمالي العملاء"),
                        allCustomers.Count, Present("السجل محفوظ للمحافظة على أثر الفواتير."));
                ApplyWritePermissionState();
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception)
            {
                if (version != readVersion || !readContextIsCurrent()) return false;
                AppDiagnostics.Log("CustomerListRead", exception);
                allCustomers.Clear();
                customers.DataSource = new DataTable();
                visibleCustomers.Clear();
                if (string.IsNullOrWhiteSpace(selectedId)) ResetEditorValues();
                else
                {
                    SelectedCustomer = null;
                    chooseButton.Enabled = false;
                }
                status.Text = Present("تعذر تحميل العملاء. تحقق من الاتصال ثم أعد تحميل القائمة.");
                return false;
            }
            finally
            {
                if (version == readVersion && !IsDisposed && !Disposing)
                {
                    readCancellation = null;
                    loadingCustomers = false;
                    search.Enabled = true;
                    if (!readContextIsCurrent())
                    {
                        ResetEditorValues();
                        status.Text = Present("تغيرت جلسة المستخدم. أعد تحميل القائمة.");
                    }
                    ApplyWritePermissionState();
                    RestoreCustomerRecoveryEditor();
                }
                cancellation.Dispose();
            }
        }

        private bool IsSessionCurrent(OperationActor actor, string employeeId)
        {
            return !IsDisposed && !Disposing && SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, StringComparison.Ordinal);
        }

        private Func<bool> CaptureCustomerReadContext(OperationActor actor, string employeeId)
        {
            long revision = SessionContext.CurrentSessionRevision;
            var connectionIsCurrent = ApplicationServices.CaptureRuntimeConnectionContext();
            return () => SessionContext.CurrentSessionRevision == revision && connectionIsCurrent() && IsSessionCurrent(actor, employeeId);
        }
        private bool EnsureCurrentCustomerList()
        {
            if (previewMode) return true;
            if (invalidatingCustomerList) return false;
            if (customerListContextIsCurrent != null && customerListContextIsCurrent() && PermissionService.CanOpen(RequiredPermission(selectionMode))) return true;
            invalidatingCustomerList = true;
            try
            {
                customerListContextIsCurrent = null;
                allCustomers.Clear(); visibleCustomers.Clear(); SelectedCustomer = null;
                customers.DataSource = new DataTable();
                ResetEditorValues();
                status.Text = Present("تغيرت جلسة المستخدم. أعد تحميل القائمة.");
            }
            finally { invalidatingCustomerList = false; }
            return false;
        }
        private void CancelCustomerRead()
        {
            readVersion++;
            var pending = readCancellation;
            readCancellation = null;
            pending?.Cancel();
        }

        private void ApplyWritePermissionState()
        {
            if (selectionMode || previewMode) return;
            EnsureCustomerRecoveryVisibility();
            bool canWrite = !loadingCustomers && !savingCustomer && PermissionService.CanExecute(AppOperation.ManageSalesCustomers) && !CustomerRecoveryLocksEditor;
            name.ReadOnly = !canWrite;
            phone.Enabled = canWrite;
            address.ReadOnly = !canWrite;
            addButton.Enabled = canWrite;
            saveButton.Enabled = canWrite;
            ApplyCustomerRecoveryButtons();
            if (!canWrite && !loadingCustomers && !savingCustomer && !CustomerRecoveryEnabled)
                status.Text = Present("عرض بيانات العملاء فقط؛ لا تملك صلاحية تعديلها.");
        }

        private void BindCustomers()
        {
            if (!EnsureCurrentCustomerList()) return;
            string query = (search.Text ?? string.Empty).Trim();
            List<SalesCustomer> rows = allCustomers.Where(c => query.Length == 0 ||
                (c.CustomerName ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                (c.PhoneNumber ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var table = new DataTable();
            table.Columns.Add("CustomerName", typeof(string));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Columns.Add("Address", typeof(string));
            table.Columns.Add("TotalSpent", typeof(string));
            foreach (SalesCustomer customer in rows)
                table.Rows.Add(customer.CustomerName, customer.PhoneNumber, customer.Address, customer.TotalSpent.ToString("N2"));
            visibleCustomers.Clear();
            visibleCustomers.AddRange(rows);
            customers.DataSource = table;

            if (rows.Count == 0)
            {
                customers.SelectedIndex = 0;
                ResetEditorValues();
                status.Text = query.Length == 0
                    ? Present("لا يوجد عملاء مسجلون بعد.")
                    : Present("لا توجد نتائج تطابق البحث.");
                return;
            }

            customers.SelectedIndex = 0;
            ResetEditorValues();
            status.Text = Present("إجمالي العملاء") + ": " + rows.Count + " — " + Present("اختر عميلاً من القائمة أولاً.");
        }

        private void LoadSelectedCustomer()
        {
            if (CustomerRecoveryLocksEditor) return;
            if (!EnsureCurrentCustomerList()) return;
            int index = customers.SelectedIndex - 1;
            if (index < 0 || index >= visibleCustomers.Count) return;
            SalesCustomer selected = visibleCustomers[index];
            if (selectionMode)
            {
                SelectedCustomer = selected;
                chooseButton.Enabled = true;
                editorTitle.Text = Present("تم تحديد العميل");
                return;
            }

            editingCustomerId = selected.CustomerId;
            name.Text = selected.CustomerName ?? string.Empty;
            phone.Text = selected.PhoneNumber ?? string.Empty;
            address.Text = selected.Address ?? string.Empty;
            status.Text = string.Format("{0}: {1}  •  {2}: {3:N2}",
                Present("معرّف العميل"), selected.CustomerId,
                Present("إجمالي مشتريات مسجل"), selected.TotalSpent);
        }

        private void ClearEditor()
        {
            if (CustomerRecoveryLocksEditor || savingCustomer) return;
            ResetEditorValues();
            status.Text = Present("أدخل بيانات العميل ثم احفظها قبل إصدار الفاتورة.");
            name.Focus();
        }

        private void ResetEditorValues()
        {
            if (CanRevealCustomerAttempt) { RestoreCustomerRecoveryEditor(); return; }
            editingCustomerId = string.Empty;
            SelectedCustomer = null;
            if (selectionMode)
            {
                if (chooseButton != null) chooseButton.Enabled = false;
                if (editorTitle != null) editorTitle.Text = Present("اختيار العميل");
            }
            if (name != null) name.Clear();
            if (phone != null) phone.Clear();
            if (address != null) address.Clear();
        }

        private async Task SaveCustomer()
        {
            if (CustomerRecoveryEnabled) { await SaveCustomerRecoverably(); return; }
            if (previewMode || savingCustomer || loadingCustomers) return;
            if (customerListContextIsCurrent != null && !EnsureCurrentCustomerList()) return;
            if (!EnsureCanOpen()) return;
            if (!PermissionService.EnsureAllowed(AppOperation.ManageSalesCustomers, this)) return;
            savingCustomer = true;
            ApplyWritePermissionState();
            reloadButton.Enabled = false;
            try
            {
                var customer = new SalesCustomer { CustomerId = editingCustomerId, CustomerName = name.Text, PhoneNumber = phone.Text, Address = address.Text };
                OperationActor actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
                string savedId;
                if (string.IsNullOrWhiteSpace(editingCustomerId)) savedId = customerService.Create(customer, actor);
                else
                {
                    if (!customerService.Update(customer, actor))
                        throw new InvalidOperationException("لم يعد سجل العميل موجودًا. حدّث القائمة ثم حاول مجددًا.");
                    savedId = editingCustomerId;
                }
                editingCustomerId = savedId;
                string employeeId = SessionContext.CurrentEmployeeId;
                var readContextIsCurrent = CaptureCustomerReadContext(actor, employeeId);
                bool refreshed = await LoadCustomers(savedId);
                if (!readContextIsCurrent()) return;
                status.Text = Present(refreshed
                    ? "تم حفظ بيانات العميل. أصبح متاحًا الآن للاختيار في فاتورة البيع."
                    : "تم حفظ العميل، لكن تعذر تحديث القائمة. أعد تحميل القائمة.");
            }
            catch (Exception exception) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationFailure(this, Present("حفظ بيانات العميل"), exception); }
            finally
            {
                savingCustomer = false;
                if (!IsDisposed && !Disposing) { reloadButton.Enabled = true; ApplyWritePermissionState(); }
            }
        }

        private void ChooseSelectedCustomer()
        {
            if (!EnsureCurrentCustomerList()) return;
            int index = customers.SelectedIndex - 1;
            if (!selectionMode || index < 0 || index >= visibleCustomers.Count)
            {
                status.Text = Present("اختر عميلاً من القائمة أولاً.");
                return;
            }
            SelectedCustomer = visibleCustomers[index];
            DialogResult = DialogResult.OK;
            Close();
        }

        private async Task OpenSuppliers()
        {
            if (previewMode) return;
            if (!EnsureCanOpen()) return;
            try { using (var suppliers = new Manufacturer()) suppliers.ShowDialog(this); }
            catch (Exception exception) { UiTheme.ShowOperationIssue(this, Present("إدارة الموردين"), exception); }
            await LoadCustomers(editingCustomerId);
        }
    }
}



