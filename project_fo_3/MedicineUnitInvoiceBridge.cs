using PharmacyManagement.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class MedicineUnitInvoiceBridge
    {
        internal static string ConnectionIdentity() { using(var connection=Database.CreateConnection())return connection.ConnectionString; }
        internal static Dictionary<string,MedicineUnitSelection> ReadSavedPresentation(string kind,string[] keys,MedicineUnitPresentationContext origin)
        {
            if(!origin.IsCurrent)throw new InvalidOperationException("تغير أصل الفاتورة المحفوظة؛ أعد فتحها من السجل.");
            var frozen=new Dictionary<string,MedicineUnitSelection>(StringComparer.Ordinal);
            using(var connection=new SqlConnection(origin.ConnectionString))
            {
                connection.Open();foreach(var key in keys)frozen.Add(key,ReadSnapshot(connection,null,kind,key));
            }
            if(!origin.IsCurrent)throw new InvalidOperationException("تغير أصل الفاتورة أثناء تحميل لقطتها؛ الطباعة محظورة.");
            return frozen;
        }
        internal static IReadOnlyList<MedicineUnitSelection> ReadUnits(string medicine, bool selling)
        {
            if (!SessionContext.IsAuthenticated || !(PermissionService.CanOpen(AppPermission.Sales) || PermissionService.CanOpen(AppPermission.Purchases)))
                throw new UnauthorizedAccessException("صلاحية قراءة وحدات الصنف مطلوبة.");
            var units=new List<MedicineUnitSelection>();
            using(var c=Database.CreateConnection())
            {
                c.Open();
                using(var t=c.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    if(!Exists(c,t,"MedicineUnitCatalogTb")){t.Commit();return units.AsReadOnly();}
                    bool configured;
                    using(var exists=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineUnitCatalogTb WHERE MedName=@m",c,t))
                    {exists.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;configured=Convert.ToInt32(exists.ExecuteScalar())==1;}
                    if(configured)RequireSchema(c,t);
                    using(var cmd=new SqlCommand(@"SELECT U.UnitId,C.Revision,U.UnitName,U.BaseUnitsPerUnit,U.SellingPrice,U.BuyingPrice,C.CurrencyCode
FROM dbo.MedicineUnitCatalogTb C JOIN dbo.MedicineUnitsTb U ON U.CatalogId=C.CatalogId
WHERE C.MedName=@m AND U.IsActive=1 ORDER BY CASE WHEN U.UnitId=C.BaseUnitId THEN 0 ELSE 1 END,U.DisplayOrder;",c,t))
                    {
                        cmd.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;
                        using(var rd=cmd.ExecuteReader())while(rd.Read())
                        {
                            decimal p=rd.GetDecimal(selling?4:5);
                            // Zero-priced catalogue packages cannot create a positive invoice line.
                            if(p>0)units.Add(new MedicineUnitSelection(rd.GetGuid(0),rd.GetInt64(1),rd.GetString(2),rd.GetInt32(3),1m,p,p,rd.GetString(6)));
                        }
                    }
                    if(configured && units.Count==0)throw new InvalidOperationException("لا توجد وحدة بسعر صالح لهذا الصنف؛ راجع إعداد وحداته.");
                    t.Commit();
                }
            }
            return units.AsReadOnly();
        }
        internal static bool Exists(SqlConnection c,SqlTransaction t,string table)
        {using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@n,N'U') IS NULL THEN 0 ELSE 1 END",c,t)){cmd.Parameters.Add("@n",SqlDbType.NVarChar,256).Value="dbo."+table;return Convert.ToInt32(cmd.ExecuteScalar())==1;}}
        private const string UnitInvoiceSchemaSql=@"SELECT CASE WHEN OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.BillsTb1',N'UnitSnapshotRequired') IS NOT NULL AND COL_LENGTH(N'dbo.PurchaseDetailsTb',N'UnitSnapshotRequired') IS NOT NULL AND COL_LENGTH(N'dbo.SalesInvoiceLinesTb',N'UnitSnapshotRequired') IS NOT NULL
AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.UnitInvoiceVersion' AND CONVERT(nvarchar(20),value)=N'1')
AND (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.BillsTb1'),OBJECT_ID(N'dbo.PurchaseDetailsTb')) AND name IN(N'Sprice',N'BuyingPrice',N'TotalAmount') AND TYPE_NAME(system_type_id)=N'decimal' AND precision=18 AND scale=2)=4
THEN 1 ELSE 0 END;";
        internal static void RequireSchema(SqlConnection c,SqlTransaction t)
        {
            using(var cmd=new SqlCommand(UnitInvoiceSchemaSql,c,t))if(Convert.ToInt32(cmd.ExecuteScalar())!=1)
                throw new InvalidOperationException("مخطط فواتير الوحدات غير جاهز؛ لم تحفظ أي حركة. يلزم تطبيق ترحيل الوحدات المعتمد أولًا.");
        }
        internal static async System.Threading.Tasks.Task RequireSchemaAsync(SqlConnection c,SqlTransaction t,System.Threading.CancellationToken token)
        {
            using(var cmd=new SqlCommand(UnitInvoiceSchemaSql,c,t))if(Convert.ToInt32(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))!=1)
                throw new InvalidOperationException("مخطط فواتير الوحدات غير جاهز؛ لم تحفظ أي حركة. يلزم تطبيق ترحيل الوحدات المعتمد أولًا.");
        }
        internal static void ValidateQuote(SqlConnection c,SqlTransaction t,string medicine,MedicineUnitSelection unit,bool selling,int baseQuantity,string currency)
        {
            if(unit==null)return;
            RequireSchema(c,t);
            if(unit.BaseQuantity!=baseQuantity||!string.Equals(unit.CurrencyCode,currency,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("كمية أو عملة وحدة الفاتورة غير مطابقة؛ راجع البند.");
            using(var cmd=new SqlCommand(@"SELECT C.Revision,U.UnitName,U.BaseUnitsPerUnit,U.SellingPrice,U.BuyingPrice,C.CurrencyCode
FROM dbo.MedicineUnitCatalogTb C WITH(UPDLOCK,HOLDLOCK) JOIN dbo.MedicineUnitsTb U WITH(UPDLOCK,HOLDLOCK) ON U.CatalogId=C.CatalogId
WHERE C.MedName=@m AND U.UnitId=@id AND U.IsActive=1;",c,t))
            {
                cmd.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;
                cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=unit.UnitId;
                using(var rd=cmd.ExecuteReader())
                    if(!rd.Read()||rd.GetInt64(0)!=unit.CatalogRevision||rd.GetString(1)!=unit.Name||rd.GetInt32(2)!=unit.BaseUnitsPerUnit||rd.GetDecimal(selling?3:4)!=unit.CatalogPrice||!string.Equals(rd.GetString(5),unit.CurrencyCode,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("تغيرت وحدة الصنف أو سعرها منذ إضافته؛ حدّث البند قبل الاعتماد. لم تحفظ الفاتورة.");
            }
        }
        internal static void SaveSnapshot(SqlConnection c,SqlTransaction t,string kind,string key,string medicine,MedicineUnitSelection unit)
        {
            if(unit==null)return;RequireSchema(c,t);
            using(var cmd=new SqlCommand(@"INSERT INTO dbo.InvoiceUnitSnapshotsTb
(DocumentKind,LineKey,MedName,UnitId,CatalogRevision,UnitName,BaseUnitsPerUnit,EnteredQuantity,UnitPrice,CatalogPrice,CurrencyCode,BaseQuantity,GrossTotal)
VALUES(@k,@key,@m,@id,@rev,@n,@f,@q,@p,@cp,@c,@b,@g);",c,t))
            {
                cmd.Parameters.Add("@k",SqlDbType.NVarChar,30).Value=kind;cmd.Parameters.Add("@key",SqlDbType.NVarChar,50).Value=key;
                cmd.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=unit.UnitId;
                cmd.Parameters.Add("@rev",SqlDbType.BigInt).Value=unit.CatalogRevision;cmd.Parameters.Add("@n",SqlDbType.NVarChar,50).Value=unit.Name;
                cmd.Parameters.Add("@f",SqlDbType.Int).Value=unit.BaseUnitsPerUnit;Money(cmd,"@q",unit.EnteredQuantity,3);
                Money(cmd,"@p",unit.UnitPrice);Money(cmd,"@cp",unit.CatalogPrice);cmd.Parameters.Add("@c",SqlDbType.NVarChar,10).Value=unit.CurrencyCode;
                cmd.Parameters.Add("@b",SqlDbType.Int).Value=unit.BaseQuantity;Money(cmd,"@g",unit.GrossTotal);cmd.ExecuteNonQuery();
                string table,column;Source(kind,out table,out column);
                using(var mark=new SqlCommand("UPDATE dbo."+table+" SET UnitSnapshotRequired=1 WHERE "+column+"=@key",c,t))
                {mark.Parameters.Add("@key",SqlDbType.NVarChar,50).Value=key;if(mark.ExecuteNonQuery()!=1)throw new InvalidOperationException("بند الفاتورة غير موجود لربط الوحدة.");}
            }
        }
        static void Source(string kind,out string table,out string column)
        {
            switch(kind){case "Sale":table="BillsTb1";column="BillId";break;case "Purchase":table="PurchaseDetailsTb";column="DetailId";break;
            case "CommercialSale":case "CommercialReturn":table="SalesInvoiceLinesTb";column="LineId";break;default:throw new InvalidOperationException("نوع لقطة الوحدة غير صالح.");}
        }
        internal static MedicineUnitSelection ReadSnapshot(SqlConnection c,SqlTransaction t,string kind,string key)
        {
            MedicineUnitSelection unit=null; string snapshotMedicine=null;
            if(Exists(c,t,"InvoiceUnitSnapshotsTb"))
            using(var cmd=new SqlCommand(@"SELECT UnitId,CatalogRevision,UnitName,BaseUnitsPerUnit,EnteredQuantity,UnitPrice,CatalogPrice,CurrencyCode,BaseQuantity,GrossTotal,MedName
FROM dbo.InvoiceUnitSnapshotsTb WHERE DocumentKind=@k AND LineKey=@key;",c,t))
            {
                cmd.Parameters.Add("@k",SqlDbType.NVarChar,30).Value=kind;cmd.Parameters.Add("@key",SqlDbType.NVarChar,50).Value=key;
                using(var rd=cmd.ExecuteReader())if(rd.Read())
                {
                    snapshotMedicine=rd.GetString(10); unit=new MedicineUnitSelection(rd.GetGuid(0),rd.GetInt64(1),rd.GetString(2),rd.GetInt32(3),rd.GetDecimal(4),rd.GetDecimal(5),rd.GetDecimal(6),rd.GetString(7));
                    if(unit.BaseQuantity!=rd.GetInt32(8)||unit.GrossTotal!=rd.GetDecimal(9))throw new InvalidOperationException("لقطة وحدة الفاتورة لا تطابق كميتها أو قيمتها المحفوظة.");
                }
            }
            string table,column;Source(kind,out table,out column);
            bool flag;
            using(var check=new SqlCommand("SELECT CASE WHEN COL_LENGTH(@t,N'UnitSnapshotRequired') IS NULL THEN 0 ELSE 1 END",c,t))
            {check.Parameters.Add("@t",SqlDbType.NVarChar,256).Value="dbo."+table;flag=Convert.ToInt32(check.ExecuteScalar())==1;}
            if(!flag && unit==null)return null;
            string price=kind=="Sale"?"Sprice":kind=="Purchase"?"BuyingPrice":"UnitPrice";
            using(var source=new SqlCommand("SELECT MedName,"+price+",Quantity,"+(flag?"UnitSnapshotRequired":"CAST(0 AS bit)")+" FROM dbo."+table+" WHERE "+column+"=@key",c,t))
            {
                source.Parameters.Add("@key",SqlDbType.NVarChar,50).Value=key;
                using(var rd=source.ExecuteReader())
                {
                    if(!rd.Read()){if(unit!=null)throw new InvalidOperationException("بند مصدر لقطة الوحدة غير موجود.");return null;}
                    if(rd.GetBoolean(3)&&unit==null)throw new InvalidOperationException("لقطة وحدة الفاتورة مفقودة؛ أوقفت القراءة لمنع تفسيرها كوحدة قديمة.");
                    if(unit!=null && (!string.Equals(rd.GetString(0),snapshotMedicine,StringComparison.Ordinal) || Convert.ToDecimal(rd[1])!=unit.UnitPrice || Convert.ToDecimal(rd[2])!=((kind=="Sale"||kind=="Purchase")?unit.BaseQuantity:unit.EnteredQuantity)))
                        throw new InvalidOperationException("لقطة الوحدة لا تطابق كمية أو سعر بند الفاتورة المحفوظ.");
                }
            }
            return unit;
        }
        internal static void Money(SqlCommand cmd,string name,decimal value,byte scale=2)
        {var p=cmd.Parameters.Add(name,SqlDbType.Decimal);p.Precision=18;p.Scale=scale;p.Value=value;}
    }
    internal sealed class MedicineUnitPresentationContext
    {
        internal readonly string ConnectionString,Reference,Rows;
        readonly long revision;readonly string user,role;
        internal MedicineUnitPresentationContext(string reference,string rows)
        {Reference=reference;Rows=rows;revision=SessionContext.CurrentSessionRevision;user=SessionContext.CurrentUser;role=SessionContext.CurrentRole;ConnectionString=MedicineUnitInvoiceBridge.ConnectionIdentity();}
        private MedicineUnitPresentationContext(MedicineUnitPresentationContext origin,string reference,string rows)
        {Reference=reference;Rows=rows;revision=origin.revision;user=origin.user;role=origin.role;ConnectionString=origin.ConnectionString;}
        internal MedicineUnitPresentationContext WithDocument(string reference,string rows)=>new MedicineUnitPresentationContext(this,reference,rows);
        internal bool IsCurrent=>SessionContext.IsAuthenticated && revision==SessionContext.CurrentSessionRevision && user==SessionContext.CurrentUser && role==SessionContext.CurrentRole && string.Equals(ConnectionString,MedicineUnitInvoiceBridge.ConnectionIdentity(),StringComparison.Ordinal);
        internal bool Matches(string reference,string rows)=>IsCurrent && string.Equals(Reference,reference,StringComparison.Ordinal) && string.Equals(Rows,rows,StringComparison.Ordinal);
    }
    internal static class UnitLotCostBridge
    {
        internal static decimal? GetRemainingCost(SqlConnection c,SqlTransaction t,Guid id,int remainingQuantity)
        {
            if(!MedicineUnitInvoiceBridge.Exists(c,t,"UnitLotCostTb"))return null;
            using(var cmd=new SqlCommand("SELECT RemainingQuantity,RemainingCost FROM dbo.UnitLotCostTb WITH(UPDLOCK,HOLDLOCK) WHERE LotId=@id",c,t))
            {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;using(var rd=cmd.ExecuteReader()){if(!rd.Read())return null;if(rd.GetInt32(0)!=remainingQuantity)throw new InvalidOperationException("كمية تكلفة الدفعة لا تطابق رصيدها؛ أوقفت العملية.");return rd.GetDecimal(1);}}
        }
        internal static void Consume(SqlConnection c,SqlTransaction t,Guid id,int quantity,decimal cost)=>Move(c,t,id,-quantity,-cost);
        internal static void Restore(SqlConnection c,SqlTransaction t,Guid id,int quantity,decimal cost)=>Move(c,t,id,quantity,cost);
        static void Move(SqlConnection c,SqlTransaction t,Guid id,int quantity,decimal cost)
        {
            if(!MedicineUnitInvoiceBridge.Exists(c,t,"UnitLotCostTb"))return;
            using(var cmd=new SqlCommand(@"UPDATE dbo.UnitLotCostTb SET RemainingQuantity=RemainingQuantity+@q,RemainingCost=RemainingCost+@c WHERE LotId=@id;",c,t))
            {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.Parameters.Add("@q",SqlDbType.Int).Value=quantity;MedicineUnitInvoiceBridge.Money(cmd,"@c",cost);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("سجل تكلفة الدفعة غير موجود؛ أوقفت العملية دون حركة.");}
        }
        internal static void Register(SqlConnection c,SqlTransaction t,string detail,int quantity,decimal cost)
        {
            using(var cmd=new SqlCommand(@"INSERT INTO dbo.UnitLotCostTb(LotId,InitialQuantity,InitialCost,RemainingQuantity,RemainingCost)
SELECT LotId,@q,@c,@q,@c FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@d;",c,t))
            {cmd.Parameters.Add("@d",SqlDbType.NVarChar,50).Value=detail;cmd.Parameters.Add("@q",SqlDbType.Int).Value=quantity;MedicineUnitInvoiceBridge.Money(cmd,"@c",cost);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("تعذر ربط تكلفة العبوة بدفعتها.");}
        }
    }
}
