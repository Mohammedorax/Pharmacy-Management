using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class AccountingHistoryReadOnlySelfTests
    {
        internal static void Run()
        {
            string originalTheme=Properties.Setings.Default.ThemeMode;
            int cases=0,failures=0;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=theme;
                    using(var form=new JournalEntriesQuickForm(true))
                    {
                        form.ApplyQuickPresentation(english);
                        if(!TryVerify(form,theme,english,"QuickJournal"))failures++;cases++;
                    }
                    foreach(string type in new[]{"ReceiptInternal","ReceiptExternal","ReceiptMulti","PaymentInternal","PaymentExternal","PaymentMulti"})
                    using(var form=new VoucherForm(type,true,english))
                    { if(!TryVerify(form,theme,english,type))failures++;cases++; }
                }
                if(failures>0)throw new InvalidOperationException("Accounting history row protection failed in "+failures+" of "+cases+" cases.");
                Console.WriteLine("PASS accounting history row protection: "+cases+" preview cases; Delete preserves every displayed source value; hidden forms, no SQL or desktop input.");
            }
            finally
            {
                Properties.Setings.Default.ThemeMode=originalTheme;
                using(var refresh=new Form())UiTheme.Apply(refresh);
            }
        }

        private static bool TryVerify(Form form,string theme,bool english,string kind)
        {
            try { Verify(form,theme,english,kind);return true; }
            catch(InvalidOperationException error){Console.WriteLine("FAIL "+kind+": "+error.Message);return false;}
        }

        private static void Verify(Form form,string theme,bool english,string kind)
        {
            var grid=form.Controls.Find("accountingDataGrid",true).OfType<DataGridView>().Single();
            var table=grid.DataSource as DataTable;
            if(table==null)
            {
                // The voucher preview constructs its production grid without reading SQL.
                // Supply the same history shape with synthetic document identities/amounts.
                table=new DataTable();
                table.Columns.Add("رقم السند",typeof(int));table.Columns.Add("التاريخ",typeof(DateTime));
                table.Columns.Add("نوع السند");table.Columns.Add("الطرف");table.Columns.Add("الحساب");
                table.Columns.Add("المبلغ",typeof(decimal));table.Columns.Add("ملاحظات");
                table.Columns.Add("رقم القيد",typeof(int));table.Columns.Add("المستخدم");
                table.Rows.Add(71,new DateTime(2026,10,18),kind,"طرف عربي","2000",25.02m,"QA first",81,"QA");
                table.Rows.Add(72,new DateTime(2026,10,18),kind,"QA party","2000",10.01m,"QA second",82,"QA");
                grid.DataSource=table;
                form.Disposed+=(sender,args)=>table.Dispose();
            }
            if(table==null||table.Rows.Count==0)throw new InvalidOperationException("Accounting preview history is empty; Delete regression would be inconclusive.");
            object[][] expected=table.Rows.Cast<DataRow>().Select(row=>row.ItemArray).ToArray();
            form.BindingContext=new BindingContext();grid.BindingContext=form.BindingContext;
            IntPtr handle=grid.Handle;
            if(form.Visible||grid.Rows.Count!=expected.Length)throw new InvalidOperationException("Hidden accounting history fixture did not bind.");
            grid.ClearSelection();grid.CurrentCell=grid.Rows[0].Cells[0];grid.Rows[0].Selected=true;
            bool handled=(bool)typeof(DataGridView).GetMethod("ProcessDataGridViewKey",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(grid,new object[]{new KeyEventArgs(Keys.Delete)});
            Console.WriteLine("Accounting history Delete: "+kind+"; theme="+theme+"; english="+english+"; before="+expected.Length+
                "; gridRows="+grid.Rows.Count+"; sourceRows="+table.Rows.Count+"; allowDelete="+grid.AllowUserToDeleteRows+"; handled="+handled);
            if(table.Rows.Count!=expected.Length||grid.Rows.Count!=expected.Length||table.Rows.Cast<DataRow>().Any(row=>row.RowState==DataRowState.Deleted))
                throw new InvalidOperationException("Read-only accounting history allowed Delete to remove a source row.");
            for(int row=0;row<expected.Length;row++)
                if(!table.Rows[row].ItemArray.SequenceEqual(expected[row]))throw new InvalidOperationException("Delete changed an accounting history value.");
            if(!grid.ReadOnly||grid.AllowUserToAddRows||grid.AllowUserToDeleteRows||form.Visible)
                throw new InvalidOperationException("Accounting history lost its read-only component contract.");
        }
    }
}
