using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace project_fo_3
{
    // مصدر إعادة الطباعة هو الجداول المعتمدة فقط، وليس عناصر الشاشة أو سجل العميل المجمع.
    internal static class CommercialInvoiceSnapshotRepository
    {
        public static CommercialInvoicePrintSnapshot LoadIssued(Guid invoiceId)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                return LoadIssued(connection, null, invoiceId);
            }
        }

        public static CommercialInvoicePrintSnapshot LoadIssued(SqlConnection connection, SqlTransaction transaction, Guid invoiceId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            long invoiceNumber;
            DateTime issuedAtUtc;
            string customerId;
            string customerName;
            string customerPhone;
            string customerAddress;
            string currencyCode;
            decimal exchangeRate;
            string issuedBy;
            string notes;
            string invoiceType;
            CommercialPriceTaxMode priceTaxMode;
            InvoiceTotals totals;

            using (SqlCommand command = new SqlCommand(@"
SELECT InvoiceNumber, IssuedAtUtc, CustomerId, CustomerNameSnapshot, CustomerPhoneSnapshot, CustomerAddressSnapshot,
        CurrencyCode, ExchangeRate, PriceTaxMode, GrossTotal, DiscountTotal, TaxTotal, NetTotal, IssuedBy, Notes, InvoiceType
FROM dbo.SalesInvoicesTb
WHERE InvoiceId = @InvoiceId AND Status = N'Issued';", connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("لم يتم العثور على فاتورة تجارية معتمدة لإعادة الطباعة.");
                    invoiceNumber = reader.GetInt64(0);
                    issuedAtUtc = reader.GetDateTime(1);
                    customerId = ReadString(reader, 2);
                    customerName = ReadString(reader, 3);
                    customerPhone = ReadString(reader, 4);
                    customerAddress = ReadString(reader, 5);
                    currencyCode = ReadString(reader, 6);
                    exchangeRate = reader.GetDecimal(7);
                    if (!Enum.TryParse(ReadString(reader, 8), true, out priceTaxMode))
                        throw new InvalidOperationException("سياسة ضريبة الفاتورة التجارية غير صالحة.");
                    decimal gross = reader.GetDecimal(9);
                    decimal discount = reader.GetDecimal(10);
                    decimal tax = reader.GetDecimal(11);
                    decimal net = reader.GetDecimal(12);
                    totals = new InvoiceTotals { PriceTaxMode = priceTaxMode, GrossTotal = gross, DiscountTotal = discount, TaxTotal = tax, NetTotal = net, TaxableTotal = InvoiceTotalsCalculator.Round(net - tax) };
                    issuedBy = ReadString(reader, 13);
                    notes = ReadString(reader, 14);
                    invoiceType = ReadString(reader, 15);
                }
            }

            List<CommercialInvoicePrintLine> lines = new List<CommercialInvoicePrintLine>();
            List<Guid> lineIds = new List<Guid>();
            string lineSql=@"
SELECT MedName, Quantity, UnitPrice, DiscountAmount, InvoiceDiscountAllocation, TaxRate, TaxAmount, LineTotal, LineId
FROM dbo.SalesInvoiceLinesTb
WHERE InvoiceId = @InvoiceId ORDER BY LineNumber;";
            bool credit=string.Equals(invoiceType,"CreditNote",StringComparison.Ordinal);
            if(credit) lineSql=@"SELECT l.MedName,l.Quantity,l.UnitPrice,l.DiscountAmount,l.InvoiceDiscountAllocation,l.TaxRate,l.TaxAmount,l.LineTotal,l.LineId,
r.GrossTotal,r.DiscountTotal,r.TaxTotal,r.NetTotal,r.ReturnQuantity
FROM dbo.SalesInvoiceLinesTb l LEFT JOIN dbo.CommercialReturnLinesTb r ON r.ReturnInvoiceLineId=l.LineId
WHERE l.InvoiceId=@InvoiceId ORDER BY l.LineNumber;";
            using (SqlCommand command = new SqlCommand(lineSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if(credit && (reader.IsDBNull(9) || reader.GetDecimal(10)!=reader.GetDecimal(3)+reader.GetDecimal(4) ||
                            reader.GetDecimal(11)!=reader.GetDecimal(6) || reader.GetDecimal(12)!=reader.GetDecimal(7) || reader.GetDecimal(13)!=reader.GetDecimal(1)))
                            throw new InvalidOperationException("لقطة مبالغ المرتجع لا تطابق سطر الإشعار الدائن المعتمد.");
                        lineIds.Add(reader.GetGuid(8));
                        lines.Add(new CommercialInvoicePrintLine(
                            ReadString(reader, 0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3),
                            reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), null, credit ? (decimal?)reader.GetDecimal(9) : null));
                    }
                }
            }

            for(int i=0;i<lines.Count;i++)
            {
                var basis=invoiceType=="Sale" ? null : CommercialSalesReturnRepository.ReadBaseSnapshot(connection,transaction,lineIds[i]);
                if(basis!=null)
                {
                    var saved=lines[i];
                    if(saved.Quantity!=basis.BaseQuantity || saved.UnitPrice!=basis.OriginalUnit.UnitPrice || saved.MedicineName!=basis.MedicineName)
                        throw new InvalidOperationException("لقطة المرتجع الأساسي لا تطابق الإشعار المعتمد.");
                    lines[i]=new CommercialInvoicePrintLine(saved.MedicineName,saved.Quantity,saved.UnitPrice,saved.LineDiscount,saved.AllocatedInvoiceDiscount,saved.TaxRatePercent,saved.TaxAmount,saved.LineTotal,basis.UnitName,saved.GrossTotal,basis.PriceBaseQuantity);
                    continue;
                }
                var unit=MedicineUnitInvoiceBridge.ReadSnapshot(connection,transaction, invoiceType == "Sale" ? "CommercialSale" : "CommercialReturn",lineIds[i].ToString("D"));
                if(unit == null) continue;
                var line=lines[i];
                if(unit.EnteredQuantity != line.Quantity || unit.UnitPrice != line.UnitPrice) throw new InvalidOperationException("لقطة وحدة الفاتورة لا تطابق السطر المعتمد.");
                lines[i]=new CommercialInvoicePrintLine(line.MedicineName,line.Quantity,line.UnitPrice,line.LineDiscount,line.AllocatedInvoiceDiscount,line.TaxRatePercent,line.TaxAmount,line.LineTotal,unit.Name,line.GrossTotal);
            }
            decimal grossFromLines = 0m;
            decimal discountsFromLines = 0m;
            decimal taxFromLines = 0m;
            decimal netFromLines = 0m;
            foreach (CommercialInvoicePrintLine line in lines)
            {
                decimal lineGross = line.GrossTotal;
                decimal lineAmountAfterDiscount = InvoiceTotalsCalculator.Round(lineGross - line.LineDiscount - line.AllocatedInvoiceDiscount);
                decimal expectedTax = priceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? InvoiceTotalsCalculator.Round(lineAmountAfterDiscount * line.TaxRatePercent / (100m + line.TaxRatePercent))
                    : InvoiceTotalsCalculator.Round(lineAmountAfterDiscount * line.TaxRatePercent / 100m);
                decimal expectedLineTotal = priceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? lineAmountAfterDiscount
                    : InvoiceTotalsCalculator.Round(lineAmountAfterDiscount + expectedTax);
                bool creditEquation=priceTaxMode==CommercialPriceTaxMode.Inclusive
                    ? lineAmountAfterDiscount==line.LineTotal
                    : lineAmountAfterDiscount==InvoiceTotalsCalculator.Round(line.LineTotal-line.TaxAmount);
                if (line.Quantity<=0m || line.UnitPrice<0m || lineGross<0m || line.TaxRatePercent<0m || line.TaxRatePercent>100m || line.TaxAmount<0m || line.LineTotal<0m ||
                    line.LineDiscount < 0m || line.AllocatedInvoiceDiscount < 0m || lineAmountAfterDiscount < 0m ||
                    (credit ? !creditEquation : expectedTax != line.TaxAmount || expectedLineTotal != line.LineTotal))
                    throw new InvalidOperationException("تفاصيل بند الفاتورة التجارية لا تتطابق مع قاعدته المحاسبية.");
                grossFromLines += lineGross;
                discountsFromLines += line.LineDiscount + line.AllocatedInvoiceDiscount;
                taxFromLines += line.TaxAmount;
                netFromLines += line.LineTotal;
            }

            try { InvoiceTotalsContract.Validate(totals); }
            catch (InvalidOperationException exception) { throw new InvalidOperationException("رأس الفاتورة التجارية غير متسق: " + exception.Message, exception); }
            if (lines.Count == 0 || InvoiceTotalsCalculator.Round(grossFromLines) != totals.GrossTotal ||
                InvoiceTotalsCalculator.Round(discountsFromLines) != totals.DiscountTotal ||
                InvoiceTotalsCalculator.Round(taxFromLines) != totals.TaxTotal ||
                InvoiceTotalsCalculator.Round(netFromLines) != totals.NetTotal)
            {
                throw new InvalidOperationException("بيانات طباعة الفاتورة التجارية غير متسقة مع رأسها المعتمد.");
            }

            return new CommercialInvoicePrintSnapshot(invoiceId, invoiceNumber, issuedAtUtc, currencyCode, exchangeRate,
                customerId, customerName, customerPhone, customerAddress, issuedBy, notes, totals, lines);
        }

        private static string ReadString(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
    }
}
