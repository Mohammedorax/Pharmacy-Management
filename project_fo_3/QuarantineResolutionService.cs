using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace project_fo_3
{
    internal enum QuarantineResolutionType { ReleaseToStock, Dispose }

    internal sealed class QuarantineResolutionRequest
    {
        public string RequestId { get; set; }
        public Guid ReturnLotAllocationId { get; set; }
        public QuarantineResolutionType ResolutionType { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
        public string DecidedBy { get; set; }
    }

    internal sealed class QuarantineResolutionResult
    {
        public Guid EventId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostAmount { get; set; }
        public int? JournalEntryId { get; set; }
        public bool IsDuplicate { get; set; }
    }

    internal static class QuarantineResolutionService
    {
        // مصدر القراءة الموحد للشاشة؛ يبقى التنفيذ المحاسبي داخل Resolve فقط.
        internal static DataTable LoadOpenAllocations()
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT a.ReturnLotAllocationId AS [معرف التخصيص],i.InvoiceNumber AS [الإشعار],sl.MedName AS [الصنف],lot.BatchNumber AS [الدفعة],lot.ExpiryDate AS [الصلاحية],lot.IsRecalled AS [مستدعاة],a.Quantity AS [المحجور],ISNULL(SUM(e.Quantity),0) AS [المعالج],a.Quantity-ISNULL(SUM(e.Quantity),0) AS [المتبقي],a.UnitCost AS [تكلفة الوحدة]
FROM dbo.CommercialReturnLotAllocationsTb a
JOIN dbo.CommercialReturnLinesTb rl ON rl.ReturnLineId=a.ReturnLineId JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId=rl.ReturnId
JOIN dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId JOIN dbo.SalesInvoiceLinesTb sl ON sl.LineId=rl.ReturnInvoiceLineId
JOIN dbo.InventoryLotsTb lot ON lot.LotId=a.LotId LEFT JOIN dbo.QuarantineResolutionEventsTb e ON e.ReturnLotAllocationId=a.ReturnLotAllocationId
WHERE a.Disposition=N'Quarantine'
GROUP BY a.ReturnLotAllocationId,i.InvoiceNumber,sl.MedName,lot.BatchNumber,lot.ExpiryDate,lot.IsRecalled,a.Quantity,a.UnitCost
HAVING a.Quantity-ISNULL(SUM(e.Quantity),0)>0 ORDER BY lot.ExpiryDate,sl.MedName;";
                connection.Open();
                DataTable table = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                return table;
            }
        }
        internal static QuarantineResolutionResult Resolve(QuarantineResolutionRequest request, DateTime businessDateUtc)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        QuarantineResolutionResult result = Resolve(connection, transaction, request, businessDateUtc);
                        transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        internal static QuarantineResolutionResult Resolve(SqlConnection connection, SqlTransaction transaction, QuarantineResolutionRequest request, DateTime businessDateUtc)
        {
            Validate(request);
            if (!PermissionService.CanExecute(AppOperation.ResolveCommercialReturnQuarantine))
                throw new UnauthorizedAccessException("لا تملك صلاحية اتخاذ قرار في مخزون الحجر.");
            if (!string.Equals(request.DecidedBy, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("يجب أن يطابق منفذ قرار الحجر المستخدم المسجل في الجلسة.");
            CommercialAccountingPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, businessDateUtc);

            string fingerprint = Fingerprint(request);
            QuarantineResolutionResult duplicate = FindDuplicate(connection, transaction, request.RequestId, fingerprint);
            if (duplicate != null) return duplicate;
            Source source = LoadSource(connection, transaction, request.ReturnLotAllocationId);
            if (source.Resolved + request.Quantity > source.Quarantined)
                throw new InvalidOperationException("كمية قرار الحجر تتجاوز الرصيد المحجور المتبقي.");
            if (request.ResolutionType == QuarantineResolutionType.ReleaseToStock && (source.IsRecalled || source.ExpiryDate.Date <= businessDateUtc.Date))
                throw new InvalidOperationException("لا يمكن الإفراج عن دفعة منتهية أو مستدعاة؛ اختر الإتلاف.");

            Guid eventId = Guid.NewGuid();
            decimal cost = InvoiceTotalsCalculator.Round(request.Quantity * source.UnitCost);
            int? journalEntryId = null;
            if (request.ResolutionType == QuarantineResolutionType.ReleaseToStock)
                ReleaseToStock(connection, transaction, source, request.Quantity, eventId, request.DecidedBy, businessDateUtc);
            else if (cost > 0m)
                journalEntryId = AccountingService.CreateJournalEntry(connection, transaction, businessDateUtc,
                    "QuarantineDisposition", eventId.ToString("D"), "إتلاف كمية من الحجر: " + request.Reason.Trim(), request.DecidedBy,
                    new List<JournalLineInput>
                    {
                        new JournalLineInput { AccountCode=AccountingService.PurchasesAccount, Debit=cost, Description="خسارة إتلاف مخزون محجور" },
                        new JournalLineInput { AccountCode=AccountingService.InventoryAccount, Credit=cost, Description="استبعاد قيمة مخزون محجور" }
                    });
            if (request.ResolutionType == QuarantineResolutionType.Dispose && cost > 0m && !journalEntryId.HasValue)
                throw new InvalidOperationException("لا يمكن اعتماد إتلاف حجر دون قيد محاسبي.");
            InsertEvent(connection, transaction, eventId, request, fingerprint, source.UnitCost, cost, journalEntryId, businessDateUtc);
            return new QuarantineResolutionResult { EventId=eventId, Quantity=request.Quantity, CostAmount=cost, JournalEntryId=journalEntryId };
        }

        private static void Validate(QuarantineResolutionRequest request)
        {
            if (request == null || request.ReturnLotAllocationId == Guid.Empty || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Trim().Length > 100 ||
                request.Quantity <= 0m || request.Quantity != decimal.Truncate(request.Quantity) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500 ||
                string.IsNullOrWhiteSpace(request.DecidedBy) || request.DecidedBy.Trim().Length > 200)
                throw new InvalidOperationException("طلب قرار الحجر غير مكتمل أو غير صالح.");
        }

        private static QuarantineResolutionResult FindDuplicate(SqlConnection c, SqlTransaction t, string requestId, string fingerprint)
        {
            using (SqlCommand cmd = new SqlCommand(@"SELECT EventId,Quantity,CostAmount,JournalEntryId,RequestFingerprint FROM dbo.QuarantineResolutionEventsTb WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;", c, t))
            {
                cmd.Parameters.Add("@RequestId",SqlDbType.NVarChar,100).Value=requestId.Trim();
                using (SqlDataReader r=cmd.ExecuteReader())
                {
                    if(!r.Read()) return null;
                    if(!string.Equals(r.GetString(4),fingerprint,StringComparison.Ordinal)) throw new InvalidOperationException("استُخدم معرف قرار الحجر سابقاً بمحتوى مختلف.");
                    return new QuarantineResolutionResult { EventId=r.GetGuid(0),Quantity=r.GetDecimal(1),CostAmount=r.GetDecimal(2),JournalEntryId=r.IsDBNull(3)?(int?)null:r.GetInt32(3),IsDuplicate=true };
                }
            }
        }

        private static Source LoadSource(SqlConnection c, SqlTransaction t, Guid id)
        {
            using(SqlCommand cmd=new SqlCommand(@"SELECT a.ReturnLotAllocationId,a.LotId,a.Quantity,a.UnitCost,l.MedName,l.ExpiryDate,l.IsRecalled,ISNULL(SUM(e.Quantity),0)
FROM dbo.CommercialReturnLotAllocationsTb a WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.InventoryLotsTb l WITH(UPDLOCK,HOLDLOCK) ON l.LotId=a.LotId
LEFT JOIN dbo.QuarantineResolutionEventsTb e WITH(UPDLOCK,HOLDLOCK) ON e.ReturnLotAllocationId=a.ReturnLotAllocationId
WHERE a.ReturnLotAllocationId=@Id AND a.Disposition=N'Quarantine'
GROUP BY a.ReturnLotAllocationId,a.LotId,a.Quantity,a.UnitCost,l.MedName,l.ExpiryDate,l.IsRecalled;",c,t))
            {
                cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id;
                using(SqlDataReader r=cmd.ExecuteReader())
                {
                    if(!r.Read()) throw new InvalidOperationException("تخصيص الحجر غير موجود أو ليس في حالة حجر.");
                    return new Source { AllocationId=r.GetGuid(0),LotId=r.GetGuid(1),Quarantined=r.GetDecimal(2),UnitCost=r.GetDecimal(3),MedicineName=r.GetString(4),ExpiryDate=r.GetDateTime(5),IsRecalled=r.GetBoolean(6),Resolved=r.GetDecimal(7) };
                }
            }
        }

        private static void ReleaseToStock(SqlConnection c, SqlTransaction t, Source source, decimal quantity, Guid eventId, string user, DateTime when)
        {
            int before=InventoryMovementService.GetCurrentQuantity(c,t,source.MedicineName);
            using(SqlCommand cmd=new SqlCommand("UPDATE dbo.InventoryLotsTb SET QuantityRemaining=QuantityRemaining+@Quantity WHERE LotId=@LotId AND IsRecalled=0 AND ExpiryDate>@Today AND QuantityRemaining+@Quantity<=QuantityReceived;",c,t))
            { cmd.Parameters.Add("@LotId",SqlDbType.UniqueIdentifier).Value=source.LotId; AddDecimal(cmd,"@Quantity",quantity); cmd.Parameters.Add("@Today",SqlDbType.Date).Value=when.Date; if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر الإفراج عن الدفعة؛ قد تكون منتهية أو مستدعاة أو تجاوزت الكمية المستلمة."); }
            using(SqlCommand cmd=new SqlCommand("UPDATE dbo.MedicineTb1 SET MedQty=MedQty+@Quantity WHERE MedName=@MedName;",c,t))
            { cmd.Parameters.Add("@Quantity",SqlDbType.Int).Value=Convert.ToInt32(quantity); cmd.Parameters.Add("@MedName",SqlDbType.NVarChar,200).Value=source.MedicineName; if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر تحديث الرصيد المجمع بعد الإفراج من الحجر."); }
            InventoryMovementService.Record(c,t,source.MedicineName,Convert.ToInt32(quantity),before,before+Convert.ToInt32(quantity),"QuarantineRelease",eventId.ToString("D"),source.UnitCost,null,"إفراج معتمد من الحجر",user,when);
        }

        private static void InsertEvent(SqlConnection c, SqlTransaction t, Guid eventId, QuarantineResolutionRequest request, string fingerprint, decimal unitCost, decimal cost, int? journalId, DateTime when)
        {
            using(SqlCommand cmd=new SqlCommand(@"INSERT dbo.QuarantineResolutionEventsTb(EventId,RequestId,RequestFingerprint,ReturnLotAllocationId,ResolutionType,Quantity,UnitCost,CostAmount,Reason,DecidedAtUtc,DecidedBy,JournalEntryId)
VALUES(@EventId,@RequestId,@Fingerprint,@AllocationId,@Type,@Quantity,@UnitCost,@Cost,@Reason,@At,@By,@Journal);",c,t))
            { cmd.Parameters.Add("@EventId",SqlDbType.UniqueIdentifier).Value=eventId; cmd.Parameters.Add("@RequestId",SqlDbType.NVarChar,100).Value=request.RequestId.Trim(); cmd.Parameters.Add("@Fingerprint",SqlDbType.Char,64).Value=fingerprint; cmd.Parameters.Add("@AllocationId",SqlDbType.UniqueIdentifier).Value=request.ReturnLotAllocationId; cmd.Parameters.Add("@Type",SqlDbType.NVarChar,20).Value=request.ResolutionType.ToString(); AddDecimal(cmd,"@Quantity",request.Quantity,3); AddDecimal(cmd,"@UnitCost",unitCost); AddDecimal(cmd,"@Cost",cost); cmd.Parameters.Add("@Reason",SqlDbType.NVarChar,500).Value=request.Reason.Trim(); cmd.Parameters.Add("@At",SqlDbType.DateTime2).Value=when.ToUniversalTime(); cmd.Parameters.Add("@By",SqlDbType.NVarChar,200).Value=request.DecidedBy.Trim(); cmd.Parameters.Add("@Journal",SqlDbType.Int).Value=journalId.HasValue?(object)journalId.Value:DBNull.Value; cmd.ExecuteNonQuery(); }
        }

        private static string Fingerprint(QuarantineResolutionRequest request)
        { using(SHA256 sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(request.ReturnLotAllocationId.ToString("D")+"|"+request.ResolutionType+"|"+request.Quantity.ToString("0.###",CultureInfo.InvariantCulture)+"|"+request.Reason.Trim()+"|"+request.DecidedBy.Trim()))).Replace("-",string.Empty); }
        private static void AddDecimal(SqlCommand cmd,string name,decimal value,byte scale=2) { SqlParameter p=cmd.Parameters.Add(name,SqlDbType.Decimal); p.Precision=18;p.Scale=scale;p.Value=value; }
        private sealed class Source { public Guid AllocationId; public Guid LotId; public decimal Quarantined; public decimal UnitCost; public string MedicineName; public DateTime ExpiryDate; public bool IsRecalled; public decimal Resolved; }
    }
}
