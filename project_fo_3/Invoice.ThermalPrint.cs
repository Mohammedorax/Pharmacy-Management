using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class Billing
    {
        private ThermalInvoicePrintLayout thermalPrintJob;

        private void BeginThermalPrint(object sender, PrintEventArgs args)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(args))return;
            EndThermalPrint(sender, args);
            thermalPrintJob = CaptureThermalPrintJob();
        }

        private void EndThermalPrint(object sender, PrintEventArgs args)
        {
            thermalPrintJob?.Dispose();
            thermalPrintJob = null;
        }

        private ThermalInvoicePrintLayout CaptureThermalPrintJob()
        {
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            using(var captured = CaptureA4PrintJob()){
            string notes = _currentCommercialInvoiceSnapshot == null ? txtInvoiceNotes.Text : _currentCommercialInvoiceSnapshot.Notes;
            var rows = new List<ThermalInvoicePrintRow>();
            foreach (var row in captured.Rows)
                rows.Add(new ThermalInvoicePrintRow(row.Name, row.Quantity, row.Price, row.Total,null,row.Unit));
                return new ThermalInvoicePrintLayout(rows, captured.Header, captured.Reference, captured.Currency,
                    notes, captured.Gross, captured.Discount, captured.Tax, captured.Net,
                    false, captured.IsCommercial, captured.TaxInclusive, _isInvoiceSaved, culture, captured.Identity);
            }
        }
    }

    public partial class Purchases
    {
        private ThermalInvoicePrintLayout purchaseThermalPrintJob;

        private void BeginPurchaseThermalPrint(object sender, PrintEventArgs args)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(args))return;
            EndPurchaseThermalPrint(sender, args);
            purchaseThermalPrintJob = CapturePurchaseThermalPrintJob();
        }

        private void EndPurchaseThermalPrint(object sender, PrintEventArgs args)
        {
            purchaseThermalPrintJob?.Dispose();
            purchaseThermalPrintJob = null;
        }

        private ThermalInvoicePrintLayout CapturePurchaseThermalPrintJob()
        {
            GuardFrozenUnitPresentation();
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            var rows = new List<ThermalInvoicePrintRow>();
            decimal total = 0m;
            foreach(DataGridViewRow row in PurchasesGV.Rows)
            {
                if(row.IsNewRow)continue;
                var unit=PrintUnit(row);
                rows.Add(new ThermalInvoicePrintRow(Convert.ToString(row.Cells["MedName"].Value,culture),
                    unit==null?Convert.ToString(row.Cells["Quantity"].Value,culture):unit.EnteredQuantity.ToString("0.###",culture),
                    unit==null?Convert.ToString(row.Cells["BuyingPrice"].Value,culture):unit.UnitPrice.ToString("N2",culture),
                    Convert.ToString(row.Cells["TotalAmount"].Value,culture),Convert.ToString(row.Cells["ExpDate"].Value,culture),unit?.Name));
                total+=Convert.ToDecimal(row.Cells["TotalAmount"].Value??0,culture);
            }
            
                return new ThermalInvoicePrintLayout(rows, CaptureInvoicePrintHeader(), currentPurchaseId,
                    CurrencyText, txtPurchaseNotesInput.Text, total, 0m, 0m, total,
                    true, false, false, isPurchaseSaved, culture, invoicePrintSelection?.Identity);
        }
    }
}
