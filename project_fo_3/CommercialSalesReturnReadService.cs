using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    // قراءات المستندات المعتمدة معزولة عن نموذج العرض؛ لا تعدل مخزوناً أو قيوداً.
    internal sealed class CommercialReturnPrintDocument
    {
        internal CommercialReturnPrintHeader Header { get; set; }
        internal List<CommercialReturnPrintLine> Lines { get; set; }
    }

    internal static class CommercialSalesReturnReadService
    {
        internal static DataTable LoadReturnableLots(Guid invoiceId)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT l.LineId AS [LineId], a.AllocationId AS [AllocationId], l.MedName AS [الصنف], lot.BatchNumber AS [الدفعة], a.Quantity-ISNULL((SELECT SUM(x.Quantity) FROM dbo.CommercialReturnLotAllocationsTb x WHERE x.OriginalSaleAllocationId=a.AllocationId),0) AS [المتاح للإرجاع], CAST(0 AS decimal(18,3)) AS [كمية المرتجع], N'Restock' AS [الحالة]
FROM dbo.SalesInvoiceLinesTb l JOIN dbo.LotAllocationsTb a ON a.InvoiceLineId=l.LineId AND a.MovementType=N'Sale' JOIN dbo.InventoryLotsTb lot ON lot.LotId=a.LotId
WHERE l.InvoiceId=@InvoiceId ORDER BY l.LineNumber, lot.ExpiryDate;";
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                connection.Open();
                DataTable table = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                table.Columns.Add("الوحدة الأصلية", typeof(string));
                table.Columns.Add("BaseUnitsPerUnit",typeof(int));
                foreach(DataRow row in table.Rows)
                {
                    var unit=MedicineUnitInvoiceBridge.ReadSnapshot(connection,null,"CommercialSale",((Guid)row["LineId"]).ToString("D"));
                    row["الوحدة الأصلية"]=unit == null ? "—" : unit.Name;
                    row["BaseUnitsPerUnit"]=unit == null ? 1 : unit.BaseUnitsPerUnit;
                }
                return table;
            }
        }
        internal static DataTable LoadHistory(Guid originalInvoiceId)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT h.ReturnId AS [معرف المرتجع], credit.InvoiceNumber AS [رقم الإشعار], original.InvoiceNumber AS [رقم الفاتورة الأصلية],
h.IssuedAtUtc AS [تاريخ الاعتماد UTC], h.RefundMethod AS [طريقة الاسترداد], h.ReturnReason AS [السبب],
credit.GrossTotal AS [الإجمالي], credit.DiscountTotal AS [الخصم], credit.TaxTotal AS [الضريبة], credit.NetTotal AS [الصافي],
credit.CurrencyCode AS [العملة], h.IssuedBy AS [المنفذ]
FROM dbo.CommercialReturnHeadersTb h
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.InvoiceType = N'CreditNote' AND credit.Status = N'Issued'
JOIN dbo.SalesInvoicesTb original ON original.InvoiceId = h.OriginalInvoiceId AND original.InvoiceType = N'Sale'
WHERE h.OriginalInvoiceId = @OriginalInvoiceId
ORDER BY h.IssuedAtUtc DESC, credit.InvoiceNumber DESC;";
                command.Parameters.Add("@OriginalInvoiceId", SqlDbType.UniqueIdentifier).Value = originalInvoiceId;
                connection.Open();
                DataTable table = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                return table;
            }
        }

        internal static CommercialReturnPrintDocument LoadPrintDocument(Guid returnId)
        {
            var lineIds = new List<Guid>();
            var document = new CommercialReturnPrintDocument { Lines = new List<CommercialReturnPrintLine>() };
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT credit.InvoiceNumber, original.InvoiceNumber, h.IssuedAtUtc, h.RefundMethod, h.ReturnReason, h.IssuedBy, credit.CurrencyCode
FROM dbo.CommercialReturnHeadersTb h
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.InvoiceType = N'CreditNote' AND credit.Status = N'Issued'
JOIN dbo.SalesInvoicesTb original ON original.InvoiceId = h.OriginalInvoiceId AND original.InvoiceType = N'Sale'
WHERE h.ReturnId = @ReturnId;
SELECT creditLine.MedName, line.ReturnQuantity, line.GrossTotal, line.DiscountTotal, line.TaxRate, line.TaxTotal, line.NetTotal, creditLine.LineId, creditLine.UnitPrice
FROM dbo.CommercialReturnLinesTb line
JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId = line.ReturnId
JOIN dbo.SalesInvoiceLinesTb creditLine ON creditLine.LineId = line.ReturnInvoiceLineId
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.Status = N'Issued'
WHERE line.ReturnId = @ReturnId
ORDER BY creditLine.LineNumber;";
                command.Parameters.Add("@ReturnId", SqlDbType.UniqueIdentifier).Value = returnId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("إشعار المرتجع غير موجود أو غير معتمد.");
                    document.Header = new CommercialReturnPrintHeader
                    {
                        ReturnInvoiceNumber = reader.GetInt64(0),
                        OriginalInvoiceNumber = reader.GetInt64(1),
                        IssuedAtUtc = reader.GetDateTime(2),
                        RefundMethod = reader.GetString(3),
                        ReturnReason = reader.GetString(4),
                        IssuedBy = reader.GetString(5),
                        CurrencyCode = reader.GetString(6)
                    };

                    if (!reader.NextResult()) throw new InvalidOperationException("تعذر تحميل سطور إشعار المرتجع.");
                    while (reader.Read())
                    {
                        lineIds.Add(reader.GetGuid(7));
                        document.Lines.Add(new CommercialReturnPrintLine
                        {
                            MedicineName = reader.GetString(0),
                            Quantity = reader.GetDecimal(1),
                            GrossTotal = reader.GetDecimal(2),
                            DiscountTotal = reader.GetDecimal(3),
                            TaxRate = reader.GetDecimal(4),
                            TaxTotal = reader.GetDecimal(5),
                            NetTotal = reader.GetDecimal(6),
                            UnitPrice = reader.GetDecimal(8)
                        });
                    }
                }
                for(int i=0;i<document.Lines.Count;i++)
                {
                    var basis=CommercialSalesReturnRepository.ReadBaseSnapshot(connection,null,lineIds[i]);
                    if(basis!=null)
                    {
                        var saved=document.Lines[i];
                        if(saved.Quantity!=basis.BaseQuantity || saved.UnitPrice!=basis.OriginalUnit.UnitPrice || saved.MedicineName!=basis.MedicineName)
                            throw new InvalidOperationException("لقطة المرتجع الأساسي لا تطابق الإشعار المعتمد.");
                        saved.Unit=basis.UnitName;saved.PriceBaseQuantity=basis.PriceBaseQuantity;continue;
                    }
                    var unit=MedicineUnitInvoiceBridge.ReadSnapshot(connection,null,"CommercialReturn",lineIds[i].ToString("D"));
                    document.Lines[i].Unit=unit == null ? string.Empty : unit.Name;
                    if(unit != null && (unit.EnteredQuantity != document.Lines[i].Quantity || unit.UnitPrice != document.Lines[i].UnitPrice)) throw new InvalidOperationException("وحدة إشعار المرتجع لا تطابق السطر المحفوظ.");
                }
            }
            return document;
        }
    }
}