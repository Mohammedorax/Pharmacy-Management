using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal sealed class MedicineUnitCatalogEditor : Form
    {
        readonly string medicine, connectionString, user, role, employee; readonly long sessionRevision;
        readonly bool leftToRight, preview;
        readonly MedicineUnitCatalogService service;
        readonly DataGridView grid=new MedicineDataGridView { Name="medicineUnitsGrid",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false };
        readonly Label header=new Label { Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(12),TextAlign=ContentAlignment.MiddleRight };
        readonly Label pricingHint=new Label { Name="medicineUnitEditorPricingHint",Dock=DockStyle.Bottom,Height=38,AutoSize=false,Padding=new Padding(12,3,12,3) };
        readonly Label status=new Label { Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(12) };
        readonly CheckBox confirm=new CheckBox { Name="confirmExistingBaseStock",Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(12,0,12,0) };
        readonly AntdUI.Button add=DesktopComponents.Action("addMedicineUnit","إضافة وحدة",AppIcon.Add);
        readonly AntdUI.Button remove=DesktopComponents.Action("removeMedicineUnit","حذف الوحدة",AppIcon.Delete);
        readonly AntdUI.Button save=DesktopComponents.Action("saveMedicineUnits","حفظ الوحدات",AppIcon.Confirm);
        readonly AntdUI.Button reload=DesktopComponents.Action("reloadMedicineUnits","إعادة تحميل",AppIcon.Refresh);
        readonly AntdUI.Button close=DesktopComponents.Action("closeMedicineUnits","إغلاق",AppIcon.Close);
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        MedicineUnitCatalogSnapshot snapshot; MedicineUnitCatalogRequest pending;
        int currentStock; string currency; bool busy,saving,ready,mustReload,recoveryBlocked; int lifetimeReleased; MedicineUnitPendingStore pendingStore;
        string T(string arabic,string english)=>leftToRight?english:arabic;
        internal MedicineUnitCatalogEditor(string medicine,bool leftToRight,bool preview=false) : this(medicine,leftToRight,preview,null) { }
        internal MedicineUnitCatalogEditor(string medicine,bool leftToRight,bool preview,string recoveryDirectory)
        {
            if(string.IsNullOrWhiteSpace(medicine))throw new ArgumentException("اسم الصنف مطلوب.",nameof(medicine));
            this.medicine=medicine;this.leftToRight=leftToRight;this.preview=preview;
            user=SessionContext.CurrentUser;role=SessionContext.CurrentRole;employee=SessionContext.CurrentEmployeeId;sessionRevision=SessionContext.CurrentSessionRevision;
            using(var c=Database.CreateConnection())connectionString=c.ConnectionString;
            service=new MedicineUnitCatalogService(new SqlMedicineUnitCatalogRepository(()=>new SqlConnection(connectionString)),CanManage);
            Name="MedicineUnitCatalogEditor";Text=T("وحدات الصنف","Medicine units");Size=new Size(820,580);MinimumSize=new Size(700,560);
            StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,112));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,FlowDirection=leftToRight?FlowDirection.LeftToRight:FlowDirection.RightToLeft };
            foreach(var button in new[]{save,add,remove,reload,close}) { button.Size=new Size(118,42);button.Margin=new Padding(4);actions.Controls.Add(button); }
            pricingHint.Text=T("أسعار العبوات تُدار هنا؛ تعديلها لا يغيّر الفواتير السابقة.","Manage package prices here; changes preserve previous invoices.");
            var headerPanel=new Panel {Dock=DockStyle.Fill};headerPanel.Controls.Add(header);headerPanel.Controls.Add(pricingHint);
            layout.Controls.Add(headerPanel,0,0);layout.Controls.Add(confirm,0,1);layout.Controls.Add(grid,0,2);layout.Controls.Add(status,0,3);layout.Controls.Add(actions,0,4);Controls.Add(layout);
            grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="Base",HeaderText=T("أساسية","Base"),FillWeight=50});
            grid.Columns.Add("Name",T("اسم الوحدة","Unit name"));grid.Columns.Add("Factor",T("عدد الوحدات الأساسية","Base units per package"));
            grid.Columns.Add("Selling",T("سعر البيع","Sale price"));grid.Columns.Add("Buying",T("سعر الشراء","Purchase price"));
            foreach(string name in new[]{"Selling","Buying"})grid.Columns[name].DefaultCellStyle.Format="N2";
            grid.Columns["Name"].FillWeight=150;grid.Columns["Factor"].FillWeight=110;
            confirm.Text=T("الرصيد الحالي محسوب بالحبة/الوحدة الأساسية المختارة","The existing stock is counted in the selected pill/base unit");
            save.Text=T("حفظ الوحدات","Save units");add.Text=T("إضافة وحدة","Add unit");remove.Text=T("حذف الوحدة","Remove unit");reload.Text=T("إعادة تحميل","Reload");close.Text=T("إغلاق","Close");
            UiTheme.Apply(this); BackColor=layout.BackColor=actions.BackColor=headerPanel.BackColor=confirm.BackColor=status.BackColor=UiTheme.Surface; confirm.ForeColor=status.ForeColor=UiTheme.Text; UiDirection.Apply(this,leftToRight);header.Font=AppFonts.Create(11,FontStyle.Bold);status.Font=AppFonts.Create(9);pricingHint.Font=AppFonts.Create(9);pricingHint.ForeColor=UiTheme.MutedText;
            add.Click+=(s,e)=>AddRow(Guid.NewGuid(),"",1,0m,0m,false);
            remove.Click+=(s,e)=> { var row=grid.CurrentRow;if(row==null)return;if(snapshot!=null&&(Guid)row.Tag==snapshot.BaseUnitId){SetStatus(T("لا يمكن حذف الوحدة الأساسية المحفوظة.","The saved base unit cannot be removed."));return;}grid.Rows.Remove(row); };
            grid.CurrentCellDirtyStateChanged+=(s,e)=> { if(grid.IsCurrentCellDirty && grid.CurrentCell?.OwningColumn.Name=="Base")grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged+=(s,e)=> { if(e.RowIndex<0||grid.Columns[e.ColumnIndex].Name!="Base"||snapshot!=null)return;if(Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Base"].Value??false))foreach(DataGridViewRow row in grid.Rows)if(row.Index!=e.RowIndex)row.Cells["Base"].Value=false; };
            grid.DataError+=(s,e)=> { e.ThrowException=false;SetStatus(T("راجع قيمة الوحدة المدخلة.","Review the entered unit value.")); };
            save.Click+=async(s,e)=>await SaveAsync();reload.Click+=async(s,e)=>await LoadAsync();close.Click+=(s,e)=>Close();
            FormClosing+=(s,e)=> { if(busy && saving){e.Cancel=true;SetStatus(T("انتظر انتهاء العملية الحالية.","Wait for the current operation to finish."));} else if(!e.Cancel)lifetime.Cancel(); };
            Disposed+=(s,e)=> { if(Interlocked.Exchange(ref lifetimeReleased,1)==0){try{lifetime.Cancel();}finally{lifetime.Dispose();pendingStore?.Dispose();}} };
            UiSessionSubscription.Attach(this,()=> { if(IsDisposed||Disposing)return;InvalidateUnitEditorContext(); });
            Activated+=(s,e)=> { if(!preview&&!IsDisposed&&!Disposing&&!CanManage(Actor()))InvalidateUnitEditorContext(); };
            if(!preview){InitializeRecovery(recoveryDirectory);Shown+=async(s,e)=>await LoadAsync();}else { currency="YER";currentStock=40;ready=true;AddRow(Guid.NewGuid(),"حبة",1,2.50m,1.25m,true);ApplyHeader(); }
            ApplyState();
        }
        bool ContextCurrent()
        {
            if(sessionRevision!=SessionContext.CurrentSessionRevision || !SessionContext.IsAuthenticated || user!=SessionContext.CurrentUser || role!=SessionContext.CurrentRole || employee!=SessionContext.CurrentEmployeeId)return false;
            using(var c=Database.CreateConnection())return string.Equals(connectionString,c.ConnectionString,StringComparison.Ordinal);
        }
        bool CanManage(OperationActor actor)=>ContextCurrent() && actor!=null && actor.UserName==user && actor.UserRole==role && PermissionService.CanOpen(AppPermission.ManageMedicines);
        OperationActor Actor()=>new OperationActor { UserName=user,UserRole=role };
        void SetStatus(string text) { if(!IsDisposed&&!Disposing)status.Text=text; }
        void ApplyState()
        {
            bool editable=!busy && ready && !mustReload && !recoveryBlocked && pending==null;
            grid.Enabled=editable;confirm.Enabled=editable && snapshot==null;add.Enabled=remove.Enabled=editable;
            save.Enabled=!preview && !busy && ready && !mustReload && !recoveryBlocked;reload.Enabled=!preview && !busy && pending==null && !recoveryBlocked;close.Enabled=!saving;
            save.Text=pending==null?T("حفظ الوحدات","Save units"):T("تحقق","Verify");save.AccessibleName=save.Text;
        }
        void InvalidateUnitEditorContext()
        {
            if(IsDisposed||Disposing)return;
            lifetime.Cancel();ready=false;recoveryBlocked=true;
            grid.Visible=false;grid.Rows.Clear();header.Text=string.Empty;confirm.Checked=false;confirm.Visible=false;
            SetStatus(T("تغيرت الجلسة أو الصلاحية أو الاتصال. أغلق النافذة وأعد فتحها.","The session, permission or connection changed. Close and reopen this window."));
            ApplyState();
        }
        void InitializeRecovery(string directory)
        {
            try
            {
                if(!CanManage(Actor()))throw new UnauthorizedAccessException(T("صلاحية إدارة الأدوية مطلوبة لاستعادة الطلب.","Medicine management permission is required to restore this request."));
                var key=MedicineUnitPendingStore.CreateContextKey(connectionString,user,role,employee,medicine);
                pendingStore=new MedicineUnitPendingStore(directory??MedicineUnitPendingStore.DefaultDirectory,key,medicine);
                pending=pendingStore.Load();
                if(pending==null)return;
                currency=pending.CurrencyCode;ready=true;
                grid.Rows.Clear();
                foreach(var unit in pending.Units)AddRow(unit.Id,unit.Name,unit.BaseUnitsPerUnit,unit.SellingPrice,unit.BuyingPrice,unit.Id==pending.BaseUnitId);
                confirm.Checked=pending.ConfirmInitialStockUnit;ApplyHeader();
                SetStatus(T("استُعيد طلب وحدات غير مؤكد. راجعه ثم اختر تحقق.","An uncertain unit request was restored. Review it, then choose Verify.")+Environment.NewLine+pending.RequestId);
            }
            catch(Exception error){recoveryBlocked=true;ready=false;SetStatus(T("تعذر فتح سجل استعادة الوحدات؛ أغلق النافذة وراجع سبب الخطأ: ","Cannot open unit recovery; close this window and review the error: ")+error.Message);}
        }
        bool ResolvePending(bool accepted)
        {
            try{pendingStore.Resolve(pending);pending=null;return true;}
            catch(Exception error){recoveryBlocked=true;ready=false;SetStatus((accepted?T("تم حفظ الوحدات، لكن تعذر تنظيف سجل الاستعادة. أعد فتح النافذة للتحقق من الطلب نفسه.","Units were saved, but recovery cleanup failed. Reopen to verify the same request."):T("رفض الحفظ مؤكد، لكن تعذر تنظيف سجل الاستعادة. أغلق النافذة وراجع الخطأ.","The write was rejected, but recovery cleanup failed. Close this window and review the error."))+Environment.NewLine+error.Message);return false;}
        }
        void ApplyHeader()
        {
            if(pending!=null)
            {
                header.Text=medicine+Environment.NewLine+T("عملة الطلب: ","Request currency: ")+pending.CurrencyCode+" | "+T("إصدار المسودة: ","Draft revision: ")+pending.ExpectedRevision;
                confirm.Visible=pending.ExpectedRevision==0;return;
            }
            header.Text=medicine+Environment.NewLine+T("العملة: ","Currency: ")+currency+" | "+T("الرصيد بالوحدة الأساسية: ","Stock in base units: ")+currentStock+" | "+T("الإصدار: ","Revision: ")+(snapshot?.Revision??0);
            confirm.Visible=snapshot==null;
        }
        void AddRow(Guid id,string name,int factor,decimal selling,decimal buying,bool isBase)
        { int index=grid.Rows.Add(isBase,name,factor,selling,buying);grid.Rows[index].Tag=id;if(snapshot!=null && id==snapshot.BaseUnitId) { grid.Rows[index].Cells["Base"].ReadOnly=true;grid.Rows[index].Cells["Factor"].ReadOnly=true; } }
        async Task<Tuple<int,decimal,decimal,string>> ReadEditorInfoAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!CanManage(Actor()))throw new UnauthorizedAccessException("صلاحية إدارة الأدوية مطلوبة.");
            using(var c=new SqlConnection(connectionString))
            {
                await c.OpenAsync(token);
                await MedicineUnitInvoiceBridge.RequireSchemaAsync(c,null,token);
                using(var cmd=new SqlCommand("SELECT MedQty,Bprice,Sprice FROM dbo.MedicineTb1 WHERE MedName=@m; SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1;",c))
                {
                    cmd.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;
                    using(var r=await cmd.ExecuteReaderAsync(token))
                    {
                        if(!await r.ReadAsync(token))throw new InvalidOperationException("الصنف المحدد لم يعد موجودًا.");
                        int stock=Convert.ToInt32(r[0]);decimal buying=Convert.ToDecimal(r[1]),selling=Convert.ToDecimal(r[2]);
                        if(await r.ReadAsync(token)||!await r.NextResultAsync(token)||!await r.ReadAsync(token))throw new InvalidOperationException("تعذر تحديد العملة أو الصنف بصورة متسقة.");
                        string code=Convert.ToString(r[0]).Trim();if(string.IsNullOrWhiteSpace(code)||await r.ReadAsync(token))throw new InvalidOperationException("حدد عملة نظام واحدة قبل إعداد الوحدات.");
                        return Tuple.Create(stock,buying,selling,code);
                    }
                }
            }
        }
        async Task LoadAsync()
        {
            if(busy||pending!=null||preview||recoveryBlocked||IsDisposed||Disposing)return;
            if(!CanManage(Actor())){InvalidateUnitEditorContext();return;}
            var token=lifetime.Token;busy=true;ready=false;ApplyState();SetStatus(T("جارٍ التحقق من الوحدات والمخطط…","Checking unit configuration and database schema…"));
            try
            {
                var info=await ReadEditorInfoAsync(token);
                var loaded=await service.GetAsync(medicine,Actor(),token);
                token.ThrowIfCancellationRequested();
                if(!ContextCurrent()||IsDisposed||Disposing)return;
                if(loaded!=null && !string.Equals(loaded.CurrencyCode,info.Item4,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("عملة الوحدات المحفوظة تختلف عن عملة النظام؛ لا يمكن إعادة تسعيرها ضمنيًا.");
                snapshot=loaded;currentStock=loaded?.CurrentBaseStockQuantity??info.Item1;currency=info.Item4;
                grid.Rows.Clear();confirm.Checked=false;
                if(loaded==null)AddRow(Guid.NewGuid(),T("حبة","Pill"),1,info.Item3,info.Item2,true);
                else foreach(var unit in loaded.Units)AddRow(unit.Id,unit.Name,unit.BaseUnitsPerUnit,unit.SellingPrice,unit.BuyingPrice,unit.Id==loaded.BaseUnitId);
                grid.Columns["Base"].ReadOnly=loaded!=null;ready=true;mustReload=false;ApplyHeader();
                SetStatus(T("اختر الوحدة الأساسية ذات المعامل 1. الأسعار ومعاملات التحويل الجديدة لا تغير الفواتير السابقة.","Choose the base unit with factor1. New prices and conversions preserve previous invoices."));
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { }
            catch(Exception error) { if(ContextCurrent())SetStatus(T("تعذر التحميل: ","Cannot load: ")+error.Message); }
            finally { busy=false;if(!IsDisposed&&!Disposing)ApplyState(); }
        }
        internal MedicineUnitCatalogRequest BuildRequest(Guid requestId)
        {
            if(!grid.EndEdit())throw new InvalidOperationException(T("صحح الخلية الحالية.","Correct the current cell."));
            var units=new List<MedicineUnitInput>();Guid baseId=Guid.Empty;
            foreach(DataGridViewRow row in grid.Rows)
            {
                if(row.IsNewRow)continue;int factor;decimal selling,buying;
                if(!int.TryParse(Convert.ToString(row.Cells["Factor"].Value),out factor)||factor<=0)throw new InvalidOperationException(T("معامل التحويل يجب أن يكون عددًا صحيحًا موجبًا.","Conversion factor must be a positive whole number."));
                if(!decimal.TryParse(Convert.ToString(row.Cells["Selling"].Value),NumberStyles.Number,CultureInfo.CurrentCulture,out selling)||!decimal.TryParse(Convert.ToString(row.Cells["Buying"].Value),NumberStyles.Number,CultureInfo.CurrentCulture,out buying))throw new InvalidOperationException(T("أدخل أسعارًا رقمية صحيحة.","Enter valid numeric prices."));
                Guid id=(Guid)row.Tag;units.Add(new MedicineUnitInput {Id=id,Name=Convert.ToString(row.Cells["Name"].Value),BaseUnitsPerUnit=factor,SellingPrice=selling,BuyingPrice=buying});
                if(Convert.ToBoolean(row.Cells["Base"].Value??false)){if(baseId!=Guid.Empty)throw new InvalidOperationException("اختر وحدة أساسية واحدة.");baseId=id;}
            }
            if(snapshot!=null && baseId!=snapshot.BaseUnitId)throw new InvalidOperationException("لا يمكن تغيير الوحدة الأساسية للمخزون بعد تهيئتها.");
            var request=new MedicineUnitCatalogRequest {RequestId=requestId,MedicineName=medicine,ExpectedRevision=snapshot?.Revision??0,BaseUnitId=baseId,CurrencyCode=currency,
                ConfirmInitialStockUnit=snapshot==null && confirm.Checked,ExpectedInitialStockQuantity=snapshot==null?(int?)currentStock:null,Units=units.AsReadOnly()};
            MedicineUnitCatalogValidation.Prepare(request);return request;
        }
        async Task SaveAsync()
        {
            if(preview||busy||!ready||mustReload||recoveryBlocked||IsDisposed||Disposing)return;
            if(!CanManage(Actor())){InvalidateUnitEditorContext();return;}
            var token=lifetime.Token;MedicineUnitCatalogRequest request;
            try{request=pending??BuildRequest(Guid.NewGuid());}
            catch(Exception error){SetStatus(error.Message);return;}
            if(IsDisposed||Disposing||token.IsCancellationRequested||!ContextCurrent())return;
            try{pendingStore.Save(request);pending=pendingStore.Load();}
            catch(Exception error){recoveryBlocked=true;ready=false;SetStatus(T("تعذر تأمين طلب الوحدات محليًا؛ لم يُرسل الحفظ. أغلق النافذة وراجع الخطأ.","Could not preserve the unit request locally; no write was sent. Close this window and review the error.")+Environment.NewLine+error.Message);ApplyState();return;}
            Guid attemptId=pending.RequestId;bool committed=false;
            saving=true;busy=true;ApplyState();SetStatus(T("جارٍ التحقق وحفظ الوحدات. رقم الطلب: ","Verifying / saving units. Request: ")+attemptId);
            try
            {
                var result=await service.SaveAsync(pending,Actor(),token);
                committed=true;
                if(!ResolvePending(true))return;
                if(!ContextCurrent()){ready=false;SetStatus(T("تأكد حفظ الطلب، وتغيرت الجلسة. أغلق النافذة.","The request was saved; the session changed. Close this window."));return;}
                busy=false;DialogResult=DialogResult.OK;Close();
            }
            catch(MedicineUnitCatalogConflictException error){if(ResolvePending(false)){mustReload=true;SetStatus(error.Message+T(" اضغط إعادة تحميل ثم راجع تعديلاتك."," Reload and review your changes."));}}
            catch(Exception error){if(committed){ready=false;SetStatus(T("تم حفظ الطلب، لكن تعذر تحديث النافذة. أغلقها وأعد فتحها.","The request was saved, but the window could not refresh. Close and reopen it."));}else SetStatus(T("نتيجة الحفظ غير مؤكدة. رقم الطلب: ","Save outcome is unconfirmed. Request: ")+attemptId+Environment.NewLine+T("حُفظ الطلب محليًا. يمكن إعادة نفس الطلب هنا أو استعادته عند فتح وحدات هذا الصنف مجددًا.","The request was preserved locally. Retry it here or restore it when reopening this medicine's units.")+Environment.NewLine+error.Message);}
            finally{saving=false;busy=false;if(!IsDisposed&&!Disposing)ApplyState();}
        }    }
}
