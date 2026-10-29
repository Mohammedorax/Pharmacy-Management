using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class DailyOperationDetailsSelfTests
    {
        internal static void Run()
        {
            VerifyDeleteProbe();
            string originalTheme=Properties.Setings.Default.ThemeMode;
            int cases=0;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=theme;
                    using(var owner=new DailyOperationsForm(english))
                    using(var table=Rows())
                    using(var view=owner.CreateDetailsView("QA document","QA read-only details",table))
                    {
                        var grid=view.Controls.OfType<DataGridView>().Single();
                        BindHidden(view,grid);
                        bool handled=DeleteSelectedRow(grid);
                        Console.WriteLine("Daily details Delete: theme="+theme+"; english="+english+"; gridRows="+grid.Rows.Count+
                            "; sourceRows="+table.Rows.Count+"; allowDelete="+grid.AllowUserToDeleteRows+"; keyHandled="+handled);
                        if(grid.Rows.Count!=2||table.Rows.Count!=2||table.Rows.Cast<DataRow>().Any(row=>row.RowState==DataRowState.Deleted)||
                            (string)table.Rows[0][0]!="باراسيتامول 500 mg"||(decimal)table.Rows[0][1]!=1.25m||
                            table.Rows.Cast<DataRow>().Sum(row=>(decimal)row[2])!=32.50m)
                            throw new InvalidOperationException("Read-only daily details allowed Delete to change visible rows or the print-source data.");
                        if(!grid.ReadOnly||grid.AllowUserToAddRows||view.Visible||owner.Visible||
                            typeof(DailyOperationsForm).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner)!=null)
                            throw new InvalidOperationException("Daily detail component verifier entered a displayed or database-backed flow.");
                        cases++;
                    }
                }
                Console.WriteLine("PASS daily detail row protection: "+cases+" theme/direction cases; real grid Delete handling preserves both visible rows and print-source values; positive delete control; forms never shown, no desktop input, SQL or authentication.");
            }
            finally
            {
                Properties.Setings.Default.ThemeMode=originalTheme;
                using(var refresh=new Form())UiTheme.Apply(refresh);
            }
        }
        private static DataTable Rows()
        {
            var table=new DataTable("DailyDetails");table.Columns.Add("Item");table.Columns.Add("Quantity",typeof(decimal));table.Columns.Add("Line total",typeof(decimal));
            table.Rows.Add("باراسيتامول 500 mg",1.25m,12.50m);table.Rows.Add("Amoxicillin 250 mg",2m,20m);return table;
        }
        private static void BindHidden(Form form,DataGridView grid)
        {
            form.BindingContext=new BindingContext();grid.BindingContext=form.BindingContext;
            IntPtr handle=grid.Handle;
            if(form.Visible||grid.Rows.Count!=2)throw new InvalidOperationException("Owned hidden grid fixture did not bind exactly two rows.");
        }
        private static bool DeleteSelectedRow(DataGridView grid)
        {
            grid.ClearSelection();grid.CurrentCell=grid.Rows[0].Cells[0];grid.Rows[0].Selected=true;
            return (bool)typeof(DataGridView).GetMethod("ProcessDataGridViewKey",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(grid,new object[]{new KeyEventArgs(Keys.Delete)});
        }
        private static void VerifyDeleteProbe()
        {
            using(var form=new Form())
            using(var table=Rows())
            using(var grid=new DataGridView{ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=true,
                SelectionMode=DataGridViewSelectionMode.FullRowSelect,DataSource=table})
            {
                form.Controls.Add(grid);BindHidden(form,grid);DeleteSelectedRow(grid);
                if(table.Rows.Count!=1)throw new InvalidOperationException("Delete control did not exercise bound-row deletion; regression result would be inconclusive.");
            }
        }
    }
}
