using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;
namespace project_fo_3
{
    public partial class Purchases
    {
        private PurchaseA4PrintLayout purchaseA4PrintJob;
        private void BeginPurchaseA4Print(object sender, PrintEventArgs args)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(args))return;
            EndPurchaseA4Print(sender,args);
            purchaseA4PrintJob = CapturePurchaseA4PrintJob();
        }
        private void EndPurchaseA4Print(object sender,PrintEventArgs args){purchaseA4PrintJob?.Dispose();purchaseA4PrintJob=null;}

        private PurchaseA4PrintLayout CapturePurchaseA4PrintJob()
        {
            GuardFrozenUnitPresentation();
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            var rows = new List<PurchaseA4PrintRow>();
            decimal total = 0m;
            foreach(DataGridViewRow row in PurchasesGV.Rows)
            {
                if(row.IsNewRow)continue;
                var unit=PrintUnit(row);
                rows.Add(new PurchaseA4PrintRow(Convert.ToString(row.Cells["DetailId"].Value,culture),
                    Convert.ToString(row.Cells["MedName"].Value,culture),
                    unit==null?Convert.ToString(row.Cells["BuyingPrice"].Value,culture):unit.UnitPrice.ToString("N2",culture),
                    unit==null?Convert.ToString(row.Cells["Quantity"].Value,culture):unit.EnteredQuantity.ToString("0.###",culture),
                    Convert.ToString(row.Cells["ProdDate"].Value,culture),Convert.ToString(row.Cells["ExpDate"].Value,culture),
                    Convert.ToString(row.Cells["TotalAmount"].Value,culture),unit?.Name));
                total+=Convert.ToDecimal(row.Cells["TotalAmount"].Value??0,culture);
            }
            
            var printHeader = CaptureInvoicePrintHeader();
            return new PurchaseA4PrintLayout(rows, printHeader, currentPurchaseId, CurrencyText,
                txtPurchaseNotesInput.Text, total, culture, invoicePrintSelection?.Identity,presentationLeftToRight);
        }
    }
}