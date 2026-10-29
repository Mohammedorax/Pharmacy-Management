using System;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace project_fo_3
{
    internal sealed class CommercialReturnLotRequest
    {
        public Guid OriginalSaleAllocationId { get; set; }
        public decimal Quantity { get; set; }
        public CommercialReturnDisposition Disposition { get; set; }
    }

    internal sealed class CommercialSalesReturnRequestLine
    {
        public int? BaseQuantity { get; set; }
        public Guid OriginalLineId { get; set; }
        public decimal Quantity { get; set; }
        public IReadOnlyList<CommercialReturnLotRequest> Lots { get; set; }
        public CommercialSalesReturnRequestLine() { Lots = new List<CommercialReturnLotRequest>(); }
    }

    internal sealed class CommercialSalesReturnRequest
    {
        public Guid OriginalInvoiceId { get; set; }
        public string RequestId { get; set; }
        public string RefundMethod { get; set; }
        public string ReturnReason { get; set; }
        public string IssuedBy { get; set; }
        public string Notes { get; set; }
        public IReadOnlyList<CommercialSalesReturnRequestLine> Lines { get; set; }
        public CommercialSalesReturnRequest()
        {
            RequestId = string.Empty; RefundMethod = "OriginalMethod"; ReturnReason = string.Empty;
            IssuedBy = string.Empty; Notes = string.Empty; Lines = new List<CommercialSalesReturnRequestLine>();
        }
    }

    internal sealed class CommercialSalesReturnResult
    {
        public Guid ReturnId { get; set; }
        public Guid ReturnInvoiceId { get; set; }
        public long ReturnInvoiceNumber { get; set; }
        public InvoiceTotals Totals { get; set; }
        public decimal ReturnedCost { get; set; }
        public bool IsDuplicate { get; set; }
        public bool ElectronicDocumentBlocked { get; set; }
        public string ElectronicDocumentBlockReason { get; set; }
    }

    // يكتب المستند الدائن والدفعات والمخزون والقيد في معاملة واحدة. لا توجد عملية
    // "تعديل فاتورة": الإرجاع دائماً مستند جديد قابل للتدقيق.
    internal static class CommercialSalesReturnRepository
    {
        public static CommercialSalesReturnResult Issue(
            SqlConnection connection, SqlTransaction transaction, CommercialSalesReturnRequest request, DateTime businessDateUtc)
        {
            return Issue(connection, transaction, request, businessDateUtc, null);
        }

        internal static CommercialSalesReturnResult Issue(
            SqlConnection connection, SqlTransaction transaction, CommercialSalesReturnRequest request,
            DateTime businessDateUtc, Action afterInventoryBeforeAccounting)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            ValidateRequest(request);
            EnsureReady(connection, transaction);
            string fingerprint = Fingerprint(request);
            // ترتيب مشترك مع البيع: عداد الفواتير قبل مستندات المصدر والدفعات.
            // القفل يبقى إلى نهاية المعاملة ولا يستهلك رقماً للطلب المكرر.
            CommercialSalesRepository.LockInvoiceNumberSequence(connection, transaction);
            CommercialSalesReturnResult duplicate = FindDuplicate(connection, transaction, request.RequestId, fingerprint);
            if (duplicate != null) return duplicate;

            SourceInvoice invoice = LoadOriginalInvoice(connection, transaction, request.OriginalInvoiceId);
            CommercialReturnCreditPolicy.EnsureCustomerIsIdentified(request.RefundMethod, invoice.CustomerId);
            List<PreparedLine> prepared = new List<PreparedLine>();
            foreach (CommercialSalesReturnRequestLine requestedLine in request.Lines)
            {
                CommercialReturnSourceLine source = LoadSourceLine(connection, transaction, invoice.InvoiceId, requestedLine.OriginalLineId, invoice.PriceTaxMode);
                List<CommercialReturnPostedLine> previous = LoadPreviousReturns(connection, transaction, source.OriginalLineId);
                List<PreparedLot> lots = PrepareLots(connection, transaction, source, requestedLine, businessDateUtc);
                decimal lotCost = InvoiceTotalsCalculator.Round(lots.Sum(x => x.CostTotal));
                bool useBase=requestedLine.BaseQuantity.HasValue || previous.Any(x=>x.BaseQuantity.HasValue);
                if(useBase)EnsureBaseReturnSchema(connection,transaction);
                int baseQuantity=requestedLine.BaseQuantity ?? (source.Unit==null ? checked((int)requestedLine.Quantity) : source.Unit.WithQuantity(requestedLine.Quantity).BaseQuantity);
                CommercialReturnCalculatedLine calculated = useBase ? CommercialReturnCalculator.CalculateBaseLine(source,previous,baseQuantity,lotCost) : CommercialReturnCalculator.CalculateLine(source, previous, requestedLine.Quantity, lotCost);
                prepared.Add(new PreparedLine { Calculated = calculated, Lots = lots });
            }

            InvoiceTotals totals = CommercialReturnCalculator.CalculateTotals(prepared.Select(x => x.Calculated));
            Guid returnInvoiceId = Guid.NewGuid();
            Guid returnId = Guid.NewGuid();
            long invoiceNumber = CommercialSalesRepository.GetNextInvoiceNumber(connection, transaction);
            InsertCreditInvoiceHeader(connection, transaction, returnInvoiceId, invoiceNumber, invoice, request, totals, businessDateUtc);
            // رأس المرتجع يجب أن يسبق سطوره بسبب مفتاحها الخارجي؛ يبقى كل شيء
            // داخل المعاملة نفسها حتى اعتماد الإشعار الدائن في آخر العملية.
            InsertReturnHeader(connection, transaction, returnId, returnInvoiceId, request, fingerprint, businessDateUtc);

            int lineNumber = 1;
            foreach (PreparedLine item in prepared)
            {
                Guid returnInvoiceLineId = Guid.NewGuid();
                Guid returnLineId = Guid.NewGuid();
                InsertCreditInvoiceLine(connection, transaction, returnInvoiceLineId, returnInvoiceId, lineNumber++, item.Calculated);
                InsertReturnLine(connection, transaction, returnLineId, returnId, returnInvoiceLineId, item.Calculated);
                if(item.Calculated.UsesBaseQuantity)SaveBaseSnapshot(connection,transaction,returnInvoiceLineId,item.Calculated);
                if (item.Calculated.Unit != null) MedicineUnitInvoiceBridge.SaveSnapshot(connection, transaction, "CommercialReturn", returnInvoiceLineId.ToString("D"), item.Calculated.MedicineName, item.Calculated.Unit);
                InsertReturnLotsAndRestock(connection, transaction, returnLineId, returnInvoiceId, item, request.IssuedBy, invoiceNumber, businessDateUtc);
            }
            if (afterInventoryBeforeAccounting != null) afterInventoryBeforeAccounting();
            bool electronicBlocked=prepared.Any(x=>x.Calculated.UsesBaseQuantity);
            if(electronicBlocked)RecordElectronicBlock(connection,transaction,returnInvoiceId);
            else CommercialEInvoiceOutboxRepository.QueueLocalSandboxArtifact(
                connection, transaction, returnInvoiceId, invoiceNumber, EInvoiceDocumentKind.CreditNote,
                invoice.CurrencyCode, 0m, prepared.Select(x => x.Calculated.ToCreditNoteLine()).ToList(), totals,
                businessDateUtc, invoice.InvoiceId, request.ReturnReason,
                prepared.Select(x => new EInvoiceLineSnapshot
                {
                    MedicineName = x.Calculated.MedicineName, Quantity = x.Calculated.Quantity,
                    UnitPrice = x.Calculated.UnitPrice, GrossAmount = x.Calculated.GrossTotal,
                    DiscountAmount = x.Calculated.DiscountTotal,
                    TaxableAmount = InvoiceTotalsCalculator.Round(x.Calculated.NetTotal - x.Calculated.TaxTotal),
                    TaxRatePercent = x.Calculated.TaxRate, TaxAmount = x.Calculated.TaxTotal,
                    LineTotal = x.Calculated.NetTotal, PriceTaxMode = x.Calculated.PriceTaxMode
                }).ToList());
            decimal restoredInventoryCost = InvoiceTotalsCalculator.Round(prepared.Sum(line =>
                line.Lots.Where(lot => CommercialReturnDispositionPolicy.RestoresInventoryValue(lot.Disposition))
                    .Sum(lot => lot.CostTotal)));
            int journalEntryId = AccountingService.PostSalesReturnCreditNote(connection, transaction, returnInvoiceId.ToString("D"), totals,
                restoredInventoryCost, request.RefundMethod,
                "إشعار دائن للفاتورة التجارية " + invoice.InvoiceNumber, businessDateUtc);
            if (string.Equals(request.RefundMethod, "Credit", StringComparison.Ordinal) && totals.NetTotal > 0m)
            {
                CustomerAccountLedgerService.RecordCreditGrant(
                    connection, transaction, invoice.CustomerId, journalEntryId,
                    returnInvoiceId.ToString("D"), totals.NetTotal, businessDateUtc, request.IssuedBy);
            }
            MarkIssued(connection, transaction, returnInvoiceId);
            return new CommercialSalesReturnResult
            {
                ReturnId = returnId, ReturnInvoiceId = returnInvoiceId, ReturnInvoiceNumber = invoiceNumber,
                Totals = totals, ReturnedCost = InvoiceTotalsCalculator.Round(prepared.Sum(x => x.Calculated.CostTotal)),
                ElectronicDocumentBlocked=electronicBlocked,ElectronicDocumentBlockReason=electronicBlocked?BaseReturnElectronicReason:null
            };
        }

        internal static void ValidateRequest(CommercialSalesReturnRequest request)
        {
            string[] methods = { "Cash", "Card", "Credit", "OriginalMethod" };
            if (request == null || request.OriginalInvoiceId == Guid.Empty || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(request.ReturnReason) || request.ReturnReason.Trim().Length > 500 || string.IsNullOrWhiteSpace(request.IssuedBy) || request.IssuedBy.Trim().Length > 200 ||
                (request.Notes ?? string.Empty).Trim().Length > 1000 ||
                request.Lines == null || request.Lines.Count == 0 || request.Lines.Any(x => x == null || x.OriginalLineId == Guid.Empty || (x.BaseQuantity.HasValue ? x.BaseQuantity.Value<=0 : !(x.Quantity > 0m && x.Quantity == decimal.Round(x.Quantity, 3))) || x.Lots == null || x.Lots.Count == 0) ||
                !methods.Contains(request.RefundMethod ?? string.Empty, StringComparer.Ordinal))
                throw new InvalidOperationException("طلب المرتجع التجاري غير مكتمل أو غير صالح.");
            if (request.Lines.GroupBy(x => x.OriginalLineId).Any(x => x.Count() != 1))
                throw new InvalidOperationException("لا يجوز تكرار سطر البيع الأصلي في طلب مرتجع واحد.");
            foreach (CommercialSalesReturnRequestLine line in request.Lines)
                if (line.Lots.Any(x => x == null || x.OriginalSaleAllocationId == Guid.Empty || !Whole(x.Quantity)) ||
                    line.Lots.GroupBy(x => x.OriginalSaleAllocationId).Any(x => x.Count() != 1) || (line.BaseQuantity.HasValue && line.Lots.Sum(x=>x.Quantity)!=line.BaseQuantity.Value))
                    throw new InvalidOperationException("توزيع دفعات المرتجع يجب أن يساوي كمية السطر كاملة وبعبوات صحيحة.");
        }

        private static void EnsureReady(SqlConnection c, SqlTransaction t)
        {
            using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb', N'U') IS NULL THEN 0 ELSE 1 END", c, t))
                if (Convert.ToInt32(command.ExecuteScalar()) != 1) throw new InvalidOperationException("ترحيل المرتجعات التجارية الجزئية غير مثبت على قاعدة الاختبار.");
            if (!CommercialSchemaInstaller.IsFullCommercialMode(c, t)) throw new InvalidOperationException("المرتجعات التجارية مقفلة حتى تكتمل تسوية الدفعات في وضع FullCommercial.");
        }

        // Used outside the write transaction so recovery can observe an immutable issued receipt
        // after its period closes. New requests still follow the original period/sequence lock order.
        internal static CommercialSalesReturnResult ReadCommittedResult(SqlConnection connection, CommercialSalesReturnRequest request)
        {
            ValidateRequest(request);
            return FindDuplicate(connection,null,request.RequestId,Fingerprint(request),true);
        }

        private static CommercialSalesReturnResult FindDuplicate(SqlConnection c, SqlTransaction t, string requestId, string fingerprint, bool readOnly=false)
        {
            const string sql = @"SELECT h.ReturnId, h.ReturnInvoiceId, i.InvoiceNumber, i.GrossTotal, i.DiscountTotal, i.TaxTotal, i.NetTotal, i.PriceTaxMode,
 ISNULL(SUM(l.CostTotal), 0), h.RequestFingerprint
FROM dbo.CommercialReturnHeadersTb h WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId
LEFT JOIN dbo.CommercialReturnLinesTb l ON l.ReturnId=h.ReturnId
WHERE h.RequestId=@RequestId AND i.Status=N'Issued'
GROUP BY h.ReturnId, h.ReturnInvoiceId, i.InvoiceNumber, i.GrossTotal, i.DiscountTotal, i.TaxTotal, i.NetTotal, i.PriceTaxMode, h.RequestFingerprint;";
            using (SqlCommand command = new SqlCommand(sql, c, t))
            {
                if(readOnly)command.CommandText=sql.Replace(" WITH (UPDLOCK, HOLDLOCK)",string.Empty);
                command.Parameters.Add("@RequestId", SqlDbType.NVarChar, 100).Value = requestId.Trim();
                using (SqlDataReader r = command.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    if (!string.Equals(r.GetString(9), fingerprint, StringComparison.Ordinal)) throw new InvalidOperationException("تم استخدام معرّف طلب المرتجع سابقاً بمحتوى مختلف.");
                    InvoiceTotals totals = Totals(r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), (CommercialPriceTaxMode)Enum.Parse(typeof(CommercialPriceTaxMode), r.GetString(7)));
                    var result=new CommercialSalesReturnResult { ReturnId=r.GetGuid(0), ReturnInvoiceId=r.GetGuid(1), ReturnInvoiceNumber=r.GetInt64(2), Totals=totals, ReturnedCost=r.GetDecimal(8), IsDuplicate=true };
                    r.Close();
                    result.ElectronicDocumentBlockReason=ReadElectronicBlock(c,t,result.ReturnInvoiceId);
                    result.ElectronicDocumentBlocked=!string.IsNullOrEmpty(result.ElectronicDocumentBlockReason);
                    return result;
                }
            }
        }

        private static SourceInvoice LoadOriginalInvoice(SqlConnection c, SqlTransaction t, Guid id)
        {
            const string sql = @"SELECT InvoiceId, InvoiceNumber, CurrencyCode, ExchangeRate, PriceTaxMode, CustomerId, CustomerNameSnapshot, CustomerPhoneSnapshot, CustomerAddressSnapshot, PatientId
FROM dbo.SalesInvoicesTb WITH (UPDLOCK, HOLDLOCK) WHERE InvoiceId=@Id AND InvoiceType=N'Sale' AND Status=N'Issued';";
            using (SqlCommand command = new SqlCommand(sql, c, t))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value=id;
                using (SqlDataReader r=command.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("الفاتورة الأصلية ليست فاتورة بيع تجارية معتمدة.");
                    return new SourceInvoice { InvoiceId=r.GetGuid(0), InvoiceNumber=r.GetInt64(1), CurrencyCode=r.GetString(2), ExchangeRate=r.GetDecimal(3), PriceTaxMode=(CommercialPriceTaxMode)Enum.Parse(typeof(CommercialPriceTaxMode),r.GetString(4)), CustomerId=Text(r,5), CustomerName=Text(r,6), CustomerPhone=Text(r,7), CustomerAddress=Text(r,8), PatientId=Text(r,9) };
                }
            }
        }

        private static CommercialReturnSourceLine LoadSourceLine(SqlConnection c, SqlTransaction t, Guid invoiceId, Guid lineId, CommercialPriceTaxMode mode)
        {
            const string sql=@"SELECT LineId, MedName, Quantity, UnitPrice, DiscountAmount + InvoiceDiscountAllocation, TaxRate, TaxAmount, LineTotal, UnitCost, CostTotal
FROM dbo.SalesInvoiceLinesTb WITH (UPDLOCK, HOLDLOCK) WHERE LineId=@LineId AND InvoiceId=@InvoiceId;";
            CommercialReturnSourceLine result;
            using (SqlCommand command=new SqlCommand(sql,c,t))
            {
                command.Parameters.Add("@LineId",SqlDbType.UniqueIdentifier).Value=lineId; command.Parameters.Add("@InvoiceId",SqlDbType.UniqueIdentifier).Value=invoiceId;
                using(SqlDataReader r=command.ExecuteReader())
                {
                    if(!r.Read()) throw new InvalidOperationException("سطر المرتجع لا ينتمي إلى الفاتورة الأصلية.");
                    result = new CommercialReturnSourceLine { OriginalLineId=r.GetGuid(0), MedicineName=r.GetString(1), Quantity=r.GetDecimal(2), UnitPrice=r.GetDecimal(3), DiscountTotal=r.GetDecimal(4), TaxRate=r.GetDecimal(5), TaxTotal=r.GetDecimal(6), NetTotal=r.GetDecimal(7), UnitCost=r.IsDBNull(8)?0m:r.GetDecimal(8), CostTotal=r.GetDecimal(9), PriceTaxMode=mode };
                }
            }
            result.Unit = MedicineUnitInvoiceBridge.ReadSnapshot(c,t,"CommercialSale",lineId.ToString("D"));
            return result;
        }

        private static List<CommercialReturnPostedLine> LoadPreviousReturns(SqlConnection c, SqlTransaction t, Guid originalLineId)
        {
            string sql=@"SELECT ReturnQuantity, GrossTotal, DiscountTotal, TaxTotal, NetTotal, CostTotal
FROM dbo.CommercialReturnLinesTb WITH (UPDLOCK, HOLDLOCK) WHERE OriginalLineId=@OriginalLineId;";
            using(var schema=new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.CommercialReturnLinesTb',N'ReturnBaseQuantity') IS NULL THEN 0 ELSE 1 END",c,t))
                if(Convert.ToInt32(schema.ExecuteScalar())==1)sql=sql.Replace("NetTotal, CostTotal","NetTotal, CostTotal, ReturnBaseQuantity");
            List<CommercialReturnPostedLine> result=new List<CommercialReturnPostedLine>();
            using(SqlCommand command=new SqlCommand(sql,c,t)) { command.Parameters.Add("@OriginalLineId",SqlDbType.UniqueIdentifier).Value=originalLineId; using(SqlDataReader r=command.ExecuteReader()) while(r.Read()) result.Add(new CommercialReturnPostedLine { Quantity=r.GetDecimal(0), GrossTotal=r.GetDecimal(1), DiscountTotal=r.GetDecimal(2), TaxTotal=r.GetDecimal(3), NetTotal=r.GetDecimal(4), CostTotal=r.GetDecimal(5), BaseQuantity=r.FieldCount>6&&!r.IsDBNull(6)?(int?)r.GetInt32(6):null }); }
            return result;
        }

        private static List<PreparedLot> PrepareLots(SqlConnection c, SqlTransaction t, CommercialReturnSourceLine source, CommercialSalesReturnRequestLine request, DateTime businessDateUtc)
        {
            bool exactCost;
            using (SqlCommand check = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.LotAllocationsTb',N'CostTotal') IS NULL THEN 0 ELSE 1 END", c, t)) exactCost = Convert.ToInt32(check.ExecuteScalar()) == 1;
            string sql=@"SELECT a.AllocationId, a.LotId, a.Quantity, a.UnitCost, l.ExpiryDate, l.IsRecalled,
 ISNULL((SELECT SUM(x.Quantity) FROM dbo.CommercialReturnLotAllocationsTb x WITH (UPDLOCK, HOLDLOCK) WHERE x.OriginalSaleAllocationId=a.AllocationId),0)
FROM dbo.LotAllocationsTb a WITH (UPDLOCK, HOLDLOCK) JOIN dbo.InventoryLotsTb l WITH (UPDLOCK, HOLDLOCK) ON l.LotId=a.LotId
WHERE a.InvoiceLineId=@LineId AND a.MovementType=N'Sale';";
            if (exactCost) sql = sql.Replace("l.IsRecalled,", "l.IsRecalled, a.CostTotal, ISNULL((SELECT SUM(x.CostTotal) FROM dbo.CommercialReturnLotAllocationsTb x WITH (UPDLOCK,HOLDLOCK) WHERE x.OriginalSaleAllocationId=a.AllocationId),0),");
            Dictionary<Guid, SaleLot> salesLots=new Dictionary<Guid, SaleLot>();
            using(SqlCommand command=new SqlCommand(sql,c,t)) { command.Parameters.Add("@LineId",SqlDbType.UniqueIdentifier).Value=source.OriginalLineId; using(SqlDataReader r=command.ExecuteReader()) while(r.Read()) salesLots.Add(r.GetGuid(0),new SaleLot { AllocationId=r.GetGuid(0), LotId=r.GetGuid(1), Quantity=r.GetDecimal(2), UnitCost=r.GetDecimal(3), Expiry=r.GetDateTime(4), Recalled=r.GetBoolean(5), Returned=r.GetDecimal(exactCost ? 8 : 6), CostTotal=exactCost && !r.IsDBNull(6) ? (decimal?)r.GetDecimal(6) : null, ReturnedCost=exactCost ? r.GetDecimal(7) : 0m }); }
            List<PreparedLot> result=new List<PreparedLot>();
            foreach(CommercialReturnLotRequest item in request.Lots)
            {
                SaleLot sold; if(!salesLots.TryGetValue(item.OriginalSaleAllocationId,out sold) || sold.Returned+item.Quantity>sold.Quantity) throw new InvalidOperationException("كمية دفعة المرتجع تتجاوز ما صُرف فعلاً أو سبق إرجاعه.");
                if(item.Disposition==CommercialReturnDisposition.Restock && (sold.Recalled || sold.Expiry.Date<=businessDateUtc.Date)) throw new InvalidOperationException("لا يمكن إعادة دفعة مستدعاة أو منتهية إلى الرصيد المتاح؛ اختر حجر أو تالف.");
                result.Add(new PreparedLot { OriginalAllocationId=sold.AllocationId, LotId=sold.LotId, Quantity=item.Quantity, UnitCost=sold.UnitCost, CostTotal=sold.CostTotal.HasValue ? MedicineUnitCostCalculator.AllocateRemaining(sold.CostTotal.Value-sold.ReturnedCost, checked((int)(sold.Quantity-sold.Returned)), checked((int)item.Quantity)) : InvoiceTotalsCalculator.Round(item.Quantity*sold.UnitCost), HasExactCost=sold.CostTotal.HasValue, Disposition=item.Disposition });
            }
            if(result.Sum(x=>x.Quantity)!=(request.BaseQuantity.HasValue ? request.BaseQuantity.Value : source.Unit == null ? request.Quantity : source.Unit.WithQuantity(request.Quantity).BaseQuantity)) throw new InvalidOperationException("توزيع دفعات المرتجع لا يطابق كمية السطر.");
            return result;
        }

        private static void InsertCreditInvoiceHeader(SqlConnection c, SqlTransaction t, Guid id, long number, SourceInvoice source, CommercialSalesReturnRequest request, InvoiceTotals totals, DateTime when)
        {
            const string sql=@"INSERT INTO dbo.SalesInvoicesTb (InvoiceId,InvoiceNumber,InvoiceType,Status,OriginalInvoiceId,CustomerId,CustomerNameSnapshot,CustomerPhoneSnapshot,CustomerAddressSnapshot,PatientId,CurrencyCode,ExchangeRate,PriceTaxMode,GrossTotal,DiscountTotal,TaxTotal,NetTotal,IssuedAtUtc,IssuedBy,Notes)
VALUES (@Id,@Number,N'CreditNote',N'Draft',@Original,@CustomerId,@CustomerName,@CustomerPhone,@CustomerAddress,@PatientId,@Currency,@Rate,@Mode,@Gross,@Discount,@Tax,@Net,@At,@By,@Notes);";
            using(SqlCommand cmd=new SqlCommand(sql,c,t)) { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; cmd.Parameters.Add("@Number",SqlDbType.BigInt).Value=number; cmd.Parameters.Add("@Original",SqlDbType.UniqueIdentifier).Value=source.InvoiceId; AddText(cmd,"@CustomerId",50,source.CustomerId); AddText(cmd,"@CustomerName",200,source.CustomerName); AddText(cmd,"@CustomerPhone",50,source.CustomerPhone); AddText(cmd,"@CustomerAddress",300,source.CustomerAddress); AddText(cmd,"@PatientId",50,source.PatientId); AddText(cmd,"@Currency",10,source.CurrencyCode); Decimal(cmd,"@Rate",18,6,source.ExchangeRate); AddText(cmd,"@Mode",20,source.PriceTaxMode.ToString()); Decimal(cmd,"@Gross",18,2,totals.GrossTotal); Decimal(cmd,"@Discount",18,2,totals.DiscountTotal); Decimal(cmd,"@Tax",18,2,totals.TaxTotal); Decimal(cmd,"@Net",18,2,totals.NetTotal); cmd.Parameters.Add("@At",SqlDbType.DateTime2).Value=when.ToUniversalTime(); AddText(cmd,"@By",200,request.IssuedBy.Trim()); AddText(cmd,"@Notes",1000,request.Notes); cmd.ExecuteNonQuery(); }
        }

        private static void InsertCreditInvoiceLine(SqlConnection c, SqlTransaction t, Guid id, Guid invoiceId, int number, CommercialReturnCalculatedLine line)
        {
            const string sql=@"INSERT INTO dbo.SalesInvoiceLinesTb (LineId,InvoiceId,LineNumber,MedName,Quantity,UnitPrice,DiscountAmount,InvoiceDiscountAllocation,TaxRate,TaxAmount,LineTotal,UnitCost,CostTotal)
VALUES (@Id,@InvoiceId,@Number,@Name,@Quantity,@UnitPrice,@Discount,0,@TaxRate,@Tax,@Total,@UnitCost,@Cost);";
            using(SqlCommand cmd=new SqlCommand(sql,c,t)) { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; cmd.Parameters.Add("@InvoiceId",SqlDbType.UniqueIdentifier).Value=invoiceId; cmd.Parameters.Add("@Number",SqlDbType.Int).Value=number; AddText(cmd,"@Name",200,line.MedicineName); Decimal(cmd,"@Quantity",18,3,line.Quantity); Decimal(cmd,"@UnitPrice",18,2,line.UnitPrice); Decimal(cmd,"@Discount",18,2,line.DiscountTotal); Decimal(cmd,"@TaxRate",9,4,line.TaxRate); Decimal(cmd,"@Tax",18,2,line.TaxTotal); Decimal(cmd,"@Total",18,2,line.NetTotal); Decimal(cmd,"@UnitCost",18,2,line.Quantity==0?0m:InvoiceTotalsCalculator.Round(line.CostTotal/line.Quantity)); Decimal(cmd,"@Cost",18,2,line.CostTotal); cmd.ExecuteNonQuery(); }
        }

        private static void InsertReturnHeader(SqlConnection c, SqlTransaction t, Guid id, Guid invoiceId, CommercialSalesReturnRequest request, string fingerprint, DateTime when)
        {
            const string sql="INSERT INTO dbo.CommercialReturnHeadersTb (ReturnId,ReturnInvoiceId,OriginalInvoiceId,RequestId,RequestFingerprint,RefundMethod,ReturnReason,IssuedAtUtc,IssuedBy,Notes) VALUES (@Id,@Invoice,@Original,@Request,@Fingerprint,@Method,@Reason,@At,@By,@Notes);";
            using(SqlCommand cmd=new SqlCommand(sql,c,t)) { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; cmd.Parameters.Add("@Invoice",SqlDbType.UniqueIdentifier).Value=invoiceId; cmd.Parameters.Add("@Original",SqlDbType.UniqueIdentifier).Value=request.OriginalInvoiceId; AddText(cmd,"@Request",100,request.RequestId.Trim()); AddText(cmd,"@Fingerprint",64,fingerprint); AddText(cmd,"@Method",30,request.RefundMethod); AddText(cmd,"@Reason",500,request.ReturnReason.Trim()); cmd.Parameters.Add("@At",SqlDbType.DateTime2).Value=when.ToUniversalTime(); AddText(cmd,"@By",200,request.IssuedBy.Trim()); AddText(cmd,"@Notes",1000,request.Notes); cmd.ExecuteNonQuery(); }
        }

        private static void InsertReturnLine(SqlConnection c, SqlTransaction t, Guid id, Guid returnId, Guid invoiceLineId, CommercialReturnCalculatedLine line)
        {
            string sql="INSERT INTO dbo.CommercialReturnLinesTb (ReturnLineId,ReturnId,ReturnInvoiceLineId,OriginalLineId,ReturnQuantity,GrossTotal,DiscountTotal,TaxRate,TaxTotal,NetTotal,UnitCost,CostTotal) VALUES (@Id,@Return,@InvoiceLine,@Original,@Quantity,@Gross,@Discount,@Rate,@Tax,@Net,@UnitCost,@Cost);";
            if(line.UsesBaseQuantity)sql=sql.Replace("UnitCost,CostTotal)","UnitCost,CostTotal,ReturnBaseQuantity,ReturnQuantityBasis)").Replace("@UnitCost,@Cost);","@UnitCost,@Cost,@Base,N'Base');");
            using(SqlCommand cmd=new SqlCommand(sql,c,t)) { if(line.UsesBaseQuantity)cmd.Parameters.Add("@Base",SqlDbType.Int).Value=line.BaseQuantity;cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; cmd.Parameters.Add("@Return",SqlDbType.UniqueIdentifier).Value=returnId; cmd.Parameters.Add("@InvoiceLine",SqlDbType.UniqueIdentifier).Value=invoiceLineId; cmd.Parameters.Add("@Original",SqlDbType.UniqueIdentifier).Value=line.OriginalLineId; Decimal(cmd,"@Quantity",18,3,line.Quantity); Decimal(cmd,"@Gross",18,2,line.GrossTotal); Decimal(cmd,"@Discount",18,2,line.DiscountTotal); Decimal(cmd,"@Rate",9,4,line.TaxRate); Decimal(cmd,"@Tax",18,2,line.TaxTotal); Decimal(cmd,"@Net",18,2,line.NetTotal); Decimal(cmd,"@UnitCost",18,2,line.Quantity==0?0m:InvoiceTotalsCalculator.Round(line.CostTotal/line.Quantity)); Decimal(cmd,"@Cost",18,2,line.CostTotal); cmd.ExecuteNonQuery(); }
        }

        private static void InsertReturnLotsAndRestock(SqlConnection c, SqlTransaction t, Guid returnLineId, Guid returnInvoiceId, PreparedLine item, string issuedBy, long invoiceNumber, DateTime when)
        {
            foreach(PreparedLot lot in item.Lots)
            {
                string insert="INSERT INTO dbo.CommercialReturnLotAllocationsTb (ReturnLotAllocationId,ReturnLineId,OriginalSaleAllocationId,LotId,Quantity,UnitCost,Disposition) VALUES (@Id,@ReturnLine,@Sale,@Lot,@Quantity,@Cost,@Disposition);";
                if (lot.HasExactCost) insert=insert.Replace("UnitCost,Disposition)","UnitCost,Disposition,CostTotal)").Replace("@Cost,@Disposition);","@Cost,@Disposition,@CostTotal);");
                using(SqlCommand cmd=new SqlCommand(insert,c,t)) { if(lot.HasExactCost) Decimal(cmd,"@CostTotal",18,2,lot.CostTotal); cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=Guid.NewGuid(); cmd.Parameters.Add("@ReturnLine",SqlDbType.UniqueIdentifier).Value=returnLineId; cmd.Parameters.Add("@Sale",SqlDbType.UniqueIdentifier).Value=lot.OriginalAllocationId; cmd.Parameters.Add("@Lot",SqlDbType.UniqueIdentifier).Value=lot.LotId; Decimal(cmd,"@Quantity",18,3,lot.Quantity); Decimal(cmd,"@Cost",18,2,lot.UnitCost); AddText(cmd,"@Disposition",20,lot.Disposition.ToString()); cmd.ExecuteNonQuery(); }
                if(lot.Disposition!=CommercialReturnDisposition.Restock) continue;
                int before=InventoryMovementService.GetCurrentQuantity(c,t,item.Calculated.MedicineName);
                using(SqlCommand cmd=new SqlCommand("UPDATE dbo.InventoryLotsTb SET QuantityRemaining=QuantityRemaining+@Quantity WHERE LotId=@Lot AND IsRecalled=0 AND ExpiryDate>@Today AND QuantityRemaining+@Quantity<=QuantityReceived;",c,t)) { cmd.Parameters.Add("@Lot",SqlDbType.UniqueIdentifier).Value=lot.LotId; Decimal(cmd,"@Quantity",18,3,lot.Quantity); cmd.Parameters.Add("@Today",SqlDbType.Date).Value=when.Date; if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر إعادة الدفعة إلى الرصيد المتاح؛ قد تكون منتهية أو مستدعاة."); }
                if(lot.HasExactCost) UnitLotCostBridge.Restore(c,t,lot.LotId,checked((int)lot.Quantity),lot.CostTotal);
                using(SqlCommand cmd=new SqlCommand("UPDATE dbo.MedicineTb1 SET MedQty=MedQty+@Quantity WHERE MedName=@Name;",c,t)) { cmd.Parameters.Add("@Quantity",SqlDbType.Int).Value=Convert.ToInt32(lot.Quantity); AddText(cmd,"@Name",200,item.Calculated.MedicineName); if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر تحديث الرصيد المجمع للمرتجع."); }
                InventoryMovementService.Record(c,t,item.Calculated.MedicineName,Convert.ToInt32(lot.Quantity),before,before+Convert.ToInt32(lot.Quantity),"CommercialSalesReturn",returnInvoiceId.ToString("D"),InvoiceTotalsCalculator.Round(lot.CostTotal/lot.Quantity),item.Calculated.UsesBaseQuantity ? InvoiceTotalsCalculator.Round(item.Calculated.UnitPrice/item.Calculated.PriceBaseQuantity) : item.Calculated.Unit == null ? item.Calculated.UnitPrice : InvoiceTotalsCalculator.Round(item.Calculated.UnitPrice/item.Calculated.Unit.BaseUnitsPerUnit),"إعادة للرصيد من إشعار دائن " + invoiceNumber + (item.Calculated.UsesBaseQuantity ? "؛ السعر المعروض تقديري للأساس؛ سعر العبوة="+item.Calculated.UnitPrice+" لكل "+item.Calculated.PriceBaseQuantity : string.Empty),issuedBy,when);
            }
        }

        internal const string BaseReturnElectronicReason="إرسال هذا المرتجع إلكترونياً موقوف: العدد الأساسي يستخدم أساس سعر العبوة الأصلي، ويجب أن يدعم مزود الفوترة هذا الأساس وفروق التقريب أولاً. الإشعار المحلي والمخزون والقيد محفوظة.";
        private static void EnsureBaseReturnSchema(SqlConnection c,SqlTransaction t)
        {
            using(var cmd=new SqlCommand(@"SELECT CASE WHEN OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.CommercialReturnEInvoiceBlocksTb',N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CommercialReturnLinesTb',N'ReturnBaseQuantity') IS NOT NULL
AND COL_LENGTH(N'dbo.CommercialReturnLinesTb',N'ReturnQuantityBasis') IS NOT NULL
AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.BaseReturnVersion' AND CONVERT(nvarchar(20),value)=N'1') THEN 1 ELSE 0 END",c,t))
                if(Convert.ToInt32(cmd.ExecuteScalar())!=1)throw new InvalidOperationException("ترحيل المرتجع الأساسي 0030 غير جاهز؛ لم يعتمد المرتجع.");
        }
        private static void SaveBaseSnapshot(SqlConnection c,SqlTransaction t,Guid creditLine,CommercialReturnCalculatedLine line)
        {
            EnsureBaseReturnSchema(c,t);var u=line.OriginalUnit;
            using(var cmd=new SqlCommand(@"INSERT dbo.CommercialReturnUnitSnapshotsTb
(ReturnInvoiceLineId,OriginalLineId,MedName,OriginalUnitId,OriginalCatalogRevision,OriginalUnitName,OriginalFactor,OriginalEnteredQuantity,OriginalBaseQuantity,OriginalUnitPrice,OriginalCatalogPrice,CurrencyCode,ReturnBaseQuantity)
VALUES(@line,@source,@m,@unit,@rev,@name,@factor,@entered,@originalBase,@price,@catalogPrice,@currency,@returned);
UPDATE dbo.SalesInvoiceLinesTb SET UnitSnapshotRequired=1 WHERE LineId=@line;IF @@ROWCOUNT<>1 THROW 51000,N'سطر الإشعار غير موجود لحفظ الوحدة.',1;",c,t))
            {
                cmd.Parameters.Add("@line",SqlDbType.UniqueIdentifier).Value=creditLine;cmd.Parameters.Add("@source",SqlDbType.UniqueIdentifier).Value=line.OriginalLineId;
                AddText(cmd,"@m",200,line.MedicineName);cmd.Parameters.Add("@unit",SqlDbType.UniqueIdentifier).Value=u.UnitId;cmd.Parameters.Add("@rev",SqlDbType.BigInt).Value=u.CatalogRevision;
                AddText(cmd,"@name",50,u.Name);cmd.Parameters.Add("@factor",SqlDbType.Int).Value=u.BaseUnitsPerUnit;Decimal(cmd,"@entered",18,3,u.EnteredQuantity);
                cmd.Parameters.Add("@originalBase",SqlDbType.Int).Value=u.BaseQuantity;Decimal(cmd,"@price",18,2,u.UnitPrice);Decimal(cmd,"@catalogPrice",18,2,u.CatalogPrice);
                AddText(cmd,"@currency",10,u.CurrencyCode);cmd.Parameters.Add("@returned",SqlDbType.Int).Value=line.BaseQuantity;cmd.ExecuteNonQuery();
            }
        }
        internal static CommercialReturnBaseSnapshot ReadBaseSnapshot(SqlConnection c,SqlTransaction t,Guid creditLine)
        {
            if(!MedicineUnitInvoiceBridge.Exists(c,t,"CommercialReturnUnitSnapshotsTb"))return null;
            using(var cmd=new SqlCommand(@"SELECT u.OriginalLineId,u.MedName,u.OriginalUnitId,u.OriginalCatalogRevision,u.OriginalUnitName,u.OriginalFactor,u.OriginalEnteredQuantity,u.OriginalBaseQuantity,u.OriginalUnitPrice,u.OriginalCatalogPrice,u.CurrencyCode,u.ReturnBaseQuantity,
r.OriginalLineId,r.ReturnBaseQuantity,r.ReturnQuantityBasis,original.MedName
FROM dbo.CommercialReturnUnitSnapshotsTb u JOIN dbo.CommercialReturnLinesTb r ON r.ReturnInvoiceLineId=u.ReturnInvoiceLineId
JOIN dbo.SalesInvoiceLinesTb credit ON credit.LineId=u.ReturnInvoiceLineId
JOIN dbo.SalesInvoicesTb header ON header.InvoiceId=credit.InvoiceId AND header.InvoiceType=N'CreditNote'
JOIN dbo.SalesInvoiceLinesTb original ON original.LineId=u.OriginalLineId AND original.InvoiceId=header.OriginalInvoiceId
WHERE u.ReturnInvoiceLineId=@line",c,t))
            {
                cmd.Parameters.Add("@line",SqlDbType.UniqueIdentifier).Value=creditLine;
                using(var r=cmd.ExecuteReader())
                {
                    if(!r.Read())return null;
                    var source=new MedicineUnitSelection(r.GetGuid(2),r.GetInt64(3),r.GetString(4),r.GetInt32(5),r.GetDecimal(6),r.GetDecimal(8),r.GetDecimal(9),r.GetString(10));
                    if(source.BaseQuantity!=r.GetInt32(7)||r.GetGuid(0)!=r.GetGuid(12)||r.IsDBNull(13)||r.GetInt32(11)!=r.GetInt32(13)||r.GetString(14)!="Base")throw new InvalidOperationException("لقطة المرتجع الأساسي لا تطابق مستنده.");
                    var result=new CommercialReturnBaseSnapshot(r.GetGuid(0),r.GetString(1),source,r.GetInt32(11));
                    if(result.MedicineName!=r.GetString(15))throw new InvalidOperationException("اسم المرتجع الأساسي لا يطابق البيع الأصلي.");
                    r.Close();
                    var original=MedicineUnitInvoiceBridge.ReadSnapshot(c,t,"CommercialSale",result.OriginalLineId.ToString("D"));
                    if(original==null || original.UnitId!=source.UnitId || original.CatalogRevision!=source.CatalogRevision || original.Name!=source.Name || original.BaseUnitsPerUnit!=source.BaseUnitsPerUnit || original.EnteredQuantity!=source.EnteredQuantity || original.UnitPrice!=source.UnitPrice || original.CatalogPrice!=source.CatalogPrice || original.CurrencyCode!=source.CurrencyCode)
                        throw new InvalidOperationException("أساس المرتجع لا يطابق لقطة وحدة البيع الأصلية.");
                    return result;
                }
            }
        }
        private static void RecordElectronicBlock(SqlConnection c,SqlTransaction t,Guid invoice)
        {
            using(var cmd=new SqlCommand("INSERT dbo.CommercialReturnEInvoiceBlocksTb(InvoiceId,BlockReason) VALUES(@id,@reason);",c,t))
            {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=invoice;AddText(cmd,"@reason",500,BaseReturnElectronicReason);cmd.ExecuteNonQuery();}
        }
        internal static string ReadElectronicBlock(SqlConnection c,SqlTransaction t,Guid invoice)
        {
            bool baseDocument=false;
            using(var schema=new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.CommercialReturnLinesTb',N'ReturnQuantityBasis') IS NULL THEN 0 ELSE 1 END",c,t))
            {
                if(Convert.ToInt32(schema.ExecuteScalar())==1)
                using(var basis=new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.CommercialReturnLinesTb r JOIN dbo.SalesInvoiceLinesTb l ON l.LineId=r.ReturnInvoiceLineId WHERE l.InvoiceId=@id AND r.ReturnQuantityBasis=N'Base') THEN 1 ELSE 0 END",c,t))
                {basis.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=invoice;baseDocument=Convert.ToInt32(basis.ExecuteScalar())==1;}
            }
            string reason=null;
            if(MedicineUnitInvoiceBridge.Exists(c,t,"CommercialReturnEInvoiceBlocksTb"))
            using(var cmd=new SqlCommand("SELECT BlockReason FROM dbo.CommercialReturnEInvoiceBlocksTb WHERE InvoiceId=@id;",c,t))
            {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=invoice;reason=cmd.ExecuteScalar() as string;}
            if(baseDocument && string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("سجل منع الإرسال الإلكتروني للمرتجع الأساسي مفقود؛ أوقف الإرسال والمراجعة حتى استعادة السجل الصحيح.");
            return reason;
        }


        private static void MarkIssued(SqlConnection c, SqlTransaction t, Guid id) { using(SqlCommand cmd=new SqlCommand("UPDATE dbo.SalesInvoicesTb SET Status=N'Issued' WHERE InvoiceId=@Id AND Status=N'Draft';",c,t)) { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر اعتماد إشعار المرتجع التجاري."); } }
        private static InvoiceTotals Totals(decimal gross, decimal discount, decimal tax, decimal net, CommercialPriceTaxMode mode) { InvoiceTotals x=new InvoiceTotals { PriceTaxMode=mode,GrossTotal=gross,DiscountTotal=discount,TaxTotal=tax,NetTotal=net,TaxableTotal=InvoiceTotalsCalculator.Round(net-tax) }; InvoiceTotalsContract.Validate(x); return x; }
        private static string Text(SqlDataReader r,int index) { return r.IsDBNull(index)?string.Empty:r.GetString(index); }
        private static void Decimal(SqlCommand c,string name,byte p,byte s,decimal value) { SqlParameter x=c.Parameters.Add(name,SqlDbType.Decimal); x.Precision=p; x.Scale=s; x.Value=value; }
        private static void AddText(SqlCommand c,string name,int len,string value) { c.Parameters.Add(name,SqlDbType.NVarChar,len).Value=string.IsNullOrWhiteSpace(value)?(object)DBNull.Value:value.Trim(); }
        private static bool Whole(decimal value) { return value>0m && value==decimal.Truncate(value); }
        private static string Fingerprint(CommercialSalesReturnRequest request)
        {
            StringBuilder b=new StringBuilder(request.OriginalInvoiceId.ToString("D")).Append('|').Append(request.RefundMethod).Append('|').Append(request.ReturnReason.Trim()).Append('|').Append(request.IssuedBy.Trim()).Append('|').Append((request.Notes ?? string.Empty).Trim());
            foreach(CommercialSalesReturnRequestLine l in request.Lines.OrderBy(x=>x.OriginalLineId)) { b.Append('|').Append(l.OriginalLineId.ToString("D")).Append('|').Append(l.Quantity.ToString("0.###",CultureInfo.InvariantCulture)); if(l.BaseQuantity.HasValue)b.Append("|Base:").Append(l.BaseQuantity.Value.ToString(CultureInfo.InvariantCulture)); foreach(CommercialReturnLotRequest lot in l.Lots.OrderBy(x=>x.OriginalSaleAllocationId)) b.Append('|').Append(lot.OriginalSaleAllocationId.ToString("D")).Append('|').Append(lot.Quantity.ToString("0.###",CultureInfo.InvariantCulture)).Append('|').Append(lot.Disposition); }
            using(SHA256 sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-",string.Empty);
        }
        private sealed class SourceInvoice { public Guid InvoiceId; public long InvoiceNumber; public string CurrencyCode; public decimal ExchangeRate; public CommercialPriceTaxMode PriceTaxMode; public string CustomerId; public string CustomerName; public string CustomerPhone; public string CustomerAddress; public string PatientId; }
        private sealed class SaleLot { public Guid AllocationId; public Guid LotId; public decimal Quantity; public decimal UnitCost; public decimal Returned; public decimal? CostTotal; public decimal ReturnedCost; public DateTime Expiry; public bool Recalled; }
        private sealed class PreparedLot { public Guid OriginalAllocationId; public Guid LotId; public decimal Quantity; public decimal UnitCost; public decimal CostTotal; public bool HasExactCost; public CommercialReturnDisposition Disposition; }
        private sealed class PreparedLine { public CommercialReturnCalculatedLine Calculated; public List<PreparedLot> Lots; }
    }
}
