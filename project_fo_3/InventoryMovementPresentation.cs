using System;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class InventoryMovementPresentation
    {
        internal static System.Data.DataTable ToTable(InventoryOperationReadSnapshot snapshot)
        {
            var table=new System.Data.DataTable();
            string[] names={"التاريخ","الصنف","نوع الحركة","رقم المستند","الوارد","الصادر","صافي الحركة","الرصيد قبل","الرصيد بعد","تكلفة الوحدة","سعر الوحدة","المستخدم","ملاحظات"};
            for(int i=0;i<names.Length;i++)table.Columns.Add(names[i],i==0?typeof(DateTime):i>=4&&i<=10?typeof(decimal):typeof(string));
            foreach(var row in snapshot.Movements)
            {
                string caption=row.SourceType;
                switch(caption){case "SalesInvoice":caption="فاتورة بيع";break;case "PurchaseInvoice":caption="فاتورة شراء";break;case "OpeningStock":caption="مخزون أول المدة";break;case "StockAdjustment":caption="تسوية مخزون";break;case "StockReceipt":caption="توريد مخزني";break;case "StockIssue":caption="صرف مخزني";break;}
                table.Rows.Add(row.CreatedAt,DbValue(row.MedicineName),DbValue(caption),DbValue(row.SourceId),row.Delta>0?row.Delta:0,row.Delta<0?-row.Delta:0,row.Delta,DbValue(row.Before),DbValue(row.After),DbValue(row.UnitCost),DbValue(row.UnitPrice),DbValue(row.UserName),DbValue(row.Notes));
            }
            return table;
        }
        private static object DbValue(object value)=>value??DBNull.Value;

    }
}
