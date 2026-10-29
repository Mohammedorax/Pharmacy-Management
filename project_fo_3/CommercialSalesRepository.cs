using System;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace project_fo_3
{
    internal sealed class CommercialSaleDraft
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public string PatientId { get; set; }
        public string CurrencyCode { get; set; }
        public decimal ExchangeRate { get; set; }
        public decimal InvoiceDiscountPercent { get; set; }
        public EInvoiceDocumentKind EInvoiceKind { get; set; }
        public string IssuedBy { get; set; }
        public string Notes { get; set; }
        public decimal CustomerCreditApplied { get; set; }
        public IReadOnlyList<CommercialInvoiceLine> Lines { get; set; }

        public CommercialSaleDraft()
        {
            CustomerId = string.Empty;
            CustomerName = string.Empty;
            CustomerPhone = string.Empty;
            CustomerAddress = string.Empty;
            PatientId = string.Empty;
            CurrencyCode = "SAR";
            ExchangeRate = 1m;
            EInvoiceKind = EInvoiceDocumentKind.SimplifiedB2C;
            IssuedBy = string.Empty;
            Notes = string.Empty;
            Lines = new List<CommercialInvoiceLine>();
        }
    }

    internal sealed class CommercialSaleResult
    {
        public Guid InvoiceId { get; set; }
        public long InvoiceNumber { get; set; }
        public InvoiceTotals Totals { get; set; }
        public decimal CostOfGoods { get; set; }
        public decimal CustomerCreditApplied { get; set; }
        public decimal CashCollected { get; set; }
        public CommercialInvoicePrintSnapshot PrintSnapshot { get; set; }
    }

    // مسار البيع التجاري لا يعتمد الرصيد المجمع وحده: يقفل الدفعات ويخصصها FEFO قبل اعتماد الفاتورة.
    internal static class CommercialSalesRepository
    {
        public static CommercialSaleResult Issue(SqlConnection connection, SqlTransaction transaction, CommercialSaleDraft draft, DateTime businessDateUtc)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            ValidateDraft(draft);
            EnsureCommercialSchemaInstalled(connection, transaction);
            if (!CommercialSchemaInstaller.IsFullCommercialMode(connection, transaction))
                throw new InvalidOperationException("مسار البيع التجاري مقفل حتى تكتمل تسوية الدفعات والمخزون في وضع FullCommercial.");

            InvoiceTotals totals = InvoiceTotalsCalculator.Calculate(draft.Lines, draft.InvoiceDiscountPercent);
            decimal customerCreditApplied = InvoiceTotalsCalculator.Round(draft.CustomerCreditApplied);
            if (customerCreditApplied < 0m || customerCreditApplied > totals.NetTotal)
                throw new InvalidOperationException("رصيد العميل المستخدم يجب أن يكون بين صفر وإجمالي الفاتورة.");
            if (customerCreditApplied > 0m && string.IsNullOrWhiteSpace(draft.CustomerId))
                throw new InvalidOperationException("لا يمكن استخدام رصيد عميل دون عميل محدد.");
            IReadOnlyList<decimal> allocatedDiscounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(draft.Lines, draft.InvoiceDiscountPercent);
            IReadOnlyList<decimal> lineTaxes = InvoiceTotalsCalculator.CalculateLineTaxAmounts(draft.Lines, draft.InvoiceDiscountPercent);
            Guid invoiceId = Guid.NewGuid();
            long invoiceNumber = GetNextInvoiceNumber(connection, transaction);

            InsertInvoiceHeader(connection, transaction, invoiceId, invoiceNumber, draft, totals, businessDateUtc);
            InsertPaymentAllocations(connection, transaction, invoiceId, totals.NetTotal, customerCreditApplied, draft.IssuedBy, businessDateUtc);
            decimal costOfGoods = 0m;
            List<CommercialInvoicePrintLine> printLines = new List<CommercialInvoicePrintLine>();
            for (int index = 0; index < draft.Lines.Count; index++)
            {
                CommercialInvoiceLine line = draft.Lines[index];
                Guid lineId = Guid.NewGuid();
                int baseQuantity = line.Unit == null ? checked((int)line.Quantity) : line.Unit.BaseQuantity;
                if (line.Unit != null)
                {
                    if (line.Quantity != line.Unit.EnteredQuantity || line.UnitPrice != line.Unit.UnitPrice) throw new InvalidOperationException("بند البيع لا يطابق لقطة الوحدة المختارة.");
                    MedicineUnitInvoiceBridge.ValidateQuote(connection, transaction, line.MedicineName, line.Unit, true, baseQuantity, draft.CurrencyCode);
                }
                // Lock the legacy aggregate row before moving either balance.  The
                // aggregate value remains a projection for existing screens, while
                // the lot allocation remains the authoritative commercial trace.
                int aggregateBefore = InventoryMovementService.GetCurrentQuantity(connection, transaction, line.MedicineName);
                decimal beforeInvoiceDiscount = InvoiceTotalsCalculator.Round(line.Quantity * line.UnitPrice) - line.LineDiscount;
                decimal lineAmountAfterDiscount = InvoiceTotalsCalculator.Round(beforeInvoiceDiscount - allocatedDiscounts[index]);
                decimal lineTotal = line.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? lineAmountAfterDiscount
                    : InvoiceTotalsCalculator.Round(lineAmountAfterDiscount + lineTaxes[index]);
                IReadOnlyList<LotAllocation> allocations = AllocateAndDecrementLots(connection, transaction, line.MedicineName, baseQuantity, businessDateUtc);
                decimal lineCost = allocations.Sum(x => x.CostTotal ?? InvoiceTotalsCalculator.Round(x.Quantity * x.UnitCost));
                InsertInvoiceLine(connection, transaction, lineId, invoiceId, index + 1, line, allocatedDiscounts[index], lineTaxes[index], lineTotal, lineCost);
                InsertLotAllocations(connection, transaction, lineId, allocations);
                if (line.Unit != null) MedicineUnitInvoiceBridge.SaveSnapshot(connection, transaction, "CommercialSale", lineId.ToString("D"), line.MedicineName, line.Unit);
                DecrementAggregateStock(connection, transaction, line.MedicineName, baseQuantity);
                InventoryMovementService.Record(
                    connection,
                    transaction,
                    line.MedicineName,
                    -baseQuantity,
                    aggregateBefore,
                    aggregateBefore - baseQuantity,
                    "CommercialSalesInvoice",
                    invoiceId.ToString("D"),
                    InvoiceTotalsCalculator.Round(lineCost / baseQuantity),
                    line.Unit == null ? line.UnitPrice : InvoiceTotalsCalculator.Round(line.UnitPrice / line.Unit.BaseUnitsPerUnit),
                    "صرف FEFO - فاتورة تجارية " + invoiceNumber + (line.Unit==null ? "" : "؛ السعر المعروض تقديري للأساس؛ سعر العبوة="+line.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)+" لكل "+line.Unit.BaseUnitsPerUnit+"؛ كمية العبوات="+line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    draft.IssuedBy,
                    businessDateUtc);
                printLines.Add(new CommercialInvoicePrintLine(
                    line.MedicineName,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineDiscount,
                    allocatedDiscounts[index],
                    line.TaxRatePercent,
                    lineTaxes[index],
                    lineTotal, line.Unit == null ? string.Empty : line.Unit.Name));
                costOfGoods += lineCost;
            }

            CommercialEInvoiceOutboxRepository.QueueLocalSandboxArtifact(connection, transaction, invoiceId, invoiceNumber, draft, totals, businessDateUtc);
            MarkIssued(connection, transaction, invoiceId);
            return new CommercialSaleResult
            {
                InvoiceId = invoiceId,
                InvoiceNumber = invoiceNumber,
                Totals = totals,
                CostOfGoods = InvoiceTotalsCalculator.Round(costOfGoods),
                CustomerCreditApplied = customerCreditApplied,
                CashCollected = InvoiceTotalsCalculator.Round(totals.NetTotal - customerCreditApplied),
                PrintSnapshot = new CommercialInvoicePrintSnapshot(
                    invoiceId, invoiceNumber, businessDateUtc, draft.CurrencyCode, draft.ExchangeRate,
                    draft.CustomerId, draft.CustomerName, draft.CustomerPhone, draft.CustomerAddress,
                    draft.IssuedBy, draft.Notes, totals, printLines)
            };
        }

        private static void ValidateDraft(CommercialSaleDraft draft)
        {
            if (draft == null || draft.Lines == null || draft.Lines.Count == 0 || string.IsNullOrWhiteSpace(draft.IssuedBy) ||
                string.IsNullOrWhiteSpace(draft.CurrencyCode) || draft.CurrencyCode.Length != 3 || draft.ExchangeRate <= 0m)
                throw new InvalidOperationException("مسودة البيع التجاري غير مكتملة.");
            if (!string.Equals(draft.CurrencyCode.Trim(), "SAR", StringComparison.OrdinalIgnoreCase) || draft.ExchangeRate != 1m)
                throw new InvalidOperationException("المسار التجاري الحالي مخصص للسعودية فقط: العملة يجب أن تكون SAR وسعر الصرف 1.");
            if (draft.EInvoiceKind != EInvoiceDocumentKind.SimplifiedB2C)
                throw new InvalidOperationException("واجهة البيع الحالية لا تجمع بيانات المشتري اللازمة للفاتورة الضريبية B2B؛ استخدم مسار B2B المخصص بعد تفعيله.");
            if (draft.Lines.Any(x => x == null || (x.Unit == null && (x.Quantity != decimal.Truncate(x.Quantity) || x.Quantity > int.MaxValue))))
                throw new InvalidOperationException("المخزون المجمع الحالي يدعم عبوات كاملة فقط؛ لا يمكن صرف كمية كسرية قبل ترقية رصيد الصنف.");
        }

        private static void EnsureCommercialSchemaInstalled(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.SalesInvoicesTb', 'U') IS NULL OR OBJECT_ID('dbo.InventoryLotsTb', 'U') IS NULL OR OBJECT_ID('dbo.SalesInvoicePaymentsTb', 'U') IS NULL THEN 0 ELSE 1 END", connection, transaction))
            {
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("مخطط البيع التجاري غير مثبت على قاعدة الاختبار.");
            }
        }

        // على خلاف SQL SEQUENCE، يعود هذا العداد مع المعاملة عند فشل الاعتماد؛ لذلك
        // لا تنشأ فجوة بلا مستند في تسلسل الفواتير التجارية المحلي.
        // المرتجع يأخذ القفل نفسه قبل قراءة المصدر أو قفل الدفعات. لا نزيد
        // العداد هنا حتى لا يستهلك الطلب المكرر رقماً بلا مستند جديد.
        internal static void LockInvoiceNumberSequence(SqlConnection connection, SqlTransaction transaction)
        {
            const string sql = @"
SELECT NextValue FROM dbo.CommercialCountersTb WITH (UPDLOCK, HOLDLOCK)
WHERE CounterKey = N'SalesInvoice';";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("عداد الفواتير التجارية غير مهيأ.");
            }
        }

        internal static long GetNextInvoiceNumber(SqlConnection connection, SqlTransaction transaction)
        {
            const string sql = @"
UPDATE dbo.CommercialCountersTb WITH (UPDLOCK, HOLDLOCK)
SET NextValue = NextValue + 1
OUTPUT deleted.NextValue
WHERE CounterKey = N'SalesInvoice';";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("عداد الفواتير التجارية غير مهيأ.");
                return Convert.ToInt64(value);
            }
        }

        private static void InsertInvoiceHeader(SqlConnection connection, SqlTransaction transaction, Guid invoiceId, long invoiceNumber, CommercialSaleDraft draft, InvoiceTotals totals, DateTime issuedAtUtc)
        {
            const string sql = @"
INSERT INTO dbo.SalesInvoicesTb
(InvoiceId, InvoiceNumber, InvoiceType, Status, CustomerId, CustomerNameSnapshot, CustomerPhoneSnapshot, CustomerAddressSnapshot, PatientId, CurrencyCode, ExchangeRate, PriceTaxMode, GrossTotal, DiscountTotal, TaxTotal, NetTotal, IssuedAtUtc, IssuedBy, Notes)
VALUES (@InvoiceId, @InvoiceNumber, N'Sale', N'Draft', @CustomerId, @CustomerNameSnapshot, @CustomerPhoneSnapshot, @CustomerAddressSnapshot, @PatientId, @CurrencyCode, @ExchangeRate, @PriceTaxMode, @GrossTotal, @DiscountTotal, @TaxTotal, @NetTotal, @IssuedAtUtc, @IssuedBy, @Notes);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                command.Parameters.Add("@InvoiceNumber", SqlDbType.BigInt).Value = invoiceNumber;
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = DbValue(draft.CustomerId);
                command.Parameters.Add("@CustomerNameSnapshot", SqlDbType.NVarChar, 200).Value = DbValue(draft.CustomerName);
                command.Parameters.Add("@CustomerPhoneSnapshot", SqlDbType.NVarChar, 50).Value = DbValue(draft.CustomerPhone);
                command.Parameters.Add("@CustomerAddressSnapshot", SqlDbType.NVarChar, 300).Value = DbValue(draft.CustomerAddress);
                command.Parameters.Add("@PatientId", SqlDbType.NVarChar, 50).Value = DbValue(draft.PatientId);
                command.Parameters.Add("@CurrencyCode", SqlDbType.NVarChar, 10).Value = draft.CurrencyCode.Trim().ToUpperInvariant();
                AddDecimal(command, "@ExchangeRate", 18, 6, draft.ExchangeRate);
                command.Parameters.Add("@PriceTaxMode", SqlDbType.NVarChar, 20).Value = draft.Lines[0].PriceTaxMode.ToString();
                AddDecimal(command, "@GrossTotal", 18, 2, totals.GrossTotal);
                AddDecimal(command, "@DiscountTotal", 18, 2, totals.DiscountTotal);
                AddDecimal(command, "@TaxTotal", 18, 2, totals.TaxTotal);
                AddDecimal(command, "@NetTotal", 18, 2, totals.NetTotal);
                command.Parameters.Add("@IssuedAtUtc", SqlDbType.DateTime2).Value = issuedAtUtc.ToUniversalTime();
                command.Parameters.Add("@IssuedBy", SqlDbType.NVarChar, 200).Value = draft.IssuedBy.Trim();
                command.Parameters.Add("@Notes", SqlDbType.NVarChar, 1000).Value = DbValue(draft.Notes);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertPaymentAllocations(SqlConnection connection, SqlTransaction transaction, Guid invoiceId, decimal netTotal, decimal customerCreditApplied, string createdBy, DateTime createdAtUtc)
        {
            decimal cashAmount = InvoiceTotalsCalculator.Round(netTotal - customerCreditApplied);
            const string sql = @"INSERT dbo.SalesInvoicePaymentsTb(PaymentId,InvoiceId,PaymentMethod,Amount,CreatedAtUtc,CreatedBy) VALUES(NEWID(),@InvoiceId,@PaymentMethod,@Amount,@CreatedAtUtc,@CreatedBy);";
            Action<string, decimal> insert = (method, amount) =>
            {
                if (amount <= 0m) return;
                using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                {
                    command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                    command.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 30).Value = method;
                    AddDecimal(command, "@Amount", 18, 2, amount);
                    command.Parameters.Add("@CreatedAtUtc", SqlDbType.DateTime2).Value = createdAtUtc.ToUniversalTime();
                    command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = createdBy.Trim();
                    command.ExecuteNonQuery();
                }
            };
            insert("Cash", cashAmount);
            insert("CustomerCredit", customerCreditApplied);
        }

        private static IReadOnlyList<LotAllocation> AllocateAndDecrementLots(SqlConnection connection, SqlTransaction transaction, string medicineName, decimal quantity, DateTime businessDateUtc)
        {
            List<InventoryLotSnapshot> lots = new List<InventoryLotSnapshot>();
            const string selectSql = @"
SELECT LotId, MedName, BatchNumber, ExpiryDate, CreatedAtUtc, QuantityRemaining, UnitCost, IsRecalled
FROM dbo.InventoryLotsTb WITH (UPDLOCK, HOLDLOCK)
WHERE MedName = @MedName";
            using (SqlCommand command = new SqlCommand(selectSql, connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lots.Add(new InventoryLotSnapshot
                        {
                            LotId = reader.GetGuid(0), MedicineName = reader.GetString(1), BatchNumber = reader.GetString(2),
                            ExpiryDate = reader.GetDateTime(3), ReceivedAt = reader.GetDateTime(4), RemainingQuantity = reader.GetDecimal(5),
                            UnitCost = reader.GetDecimal(6), IsRecalled = reader.GetBoolean(7)
                        });
                    }
                }
            }

            foreach (InventoryLotSnapshot lot in lots)
                lot.RemainingCost = UnitLotCostBridge.GetRemainingCost(connection, transaction, lot.LotId, checked((int)lot.RemainingQuantity));
            IReadOnlyList<LotAllocation> allocations = FefoAllocator.Allocate(medicineName, quantity, lots, businessDateUtc);
            const string decrementSql = @"
UPDATE dbo.InventoryLotsTb SET QuantityRemaining = QuantityRemaining - @Quantity
WHERE LotId = @LotId AND QuantityRemaining >= @Quantity AND IsRecalled = 0 AND ExpiryDate >= @BusinessDate;";
            foreach (LotAllocation allocation in allocations)
            {
                if (allocation.CostTotal.HasValue) UnitLotCostBridge.Consume(connection, transaction, allocation.LotId, checked((int)allocation.Quantity), allocation.CostTotal.Value);
                using (SqlCommand command = new SqlCommand(decrementSql, connection, transaction))
                {
                    AddDecimal(command, "@Quantity", 18, 3, allocation.Quantity);
                    command.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = allocation.LotId;
                    command.Parameters.Add("@BusinessDate", SqlDbType.Date).Value = businessDateUtc.Date;
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("تغير رصيد الدفعة أثناء البيع؛ أعد المحاولة.");
                }
            }
            return allocations;
        }

        private static void InsertInvoiceLine(SqlConnection connection, SqlTransaction transaction, Guid lineId, Guid invoiceId, int lineNumber, CommercialInvoiceLine line, decimal allocatedInvoiceDiscount, decimal taxAmount, decimal lineTotal, decimal unitCost)
        {
            const string sql = @"
INSERT INTO dbo.SalesInvoiceLinesTb
(LineId, InvoiceId, LineNumber, MedName, Quantity, UnitPrice, DiscountAmount, InvoiceDiscountAllocation, TaxRate, TaxAmount, LineTotal, UnitCost, CostTotal)
VALUES (@LineId, @InvoiceId, @LineNumber, @MedName, @Quantity, @UnitPrice, @DiscountAmount, @InvoiceDiscountAllocation, @TaxRate, @TaxAmount, @LineTotal, @UnitCost, @CostTotal);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@LineId", SqlDbType.UniqueIdentifier).Value = lineId;
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                command.Parameters.Add("@LineNumber", SqlDbType.Int).Value = lineNumber;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                AddDecimal(command, "@Quantity", 18, 3, line.Quantity); AddDecimal(command, "@UnitPrice", 18, 2, line.UnitPrice);
                AddDecimal(command, "@DiscountAmount", 18, 2, line.LineDiscount); AddDecimal(command, "@InvoiceDiscountAllocation", 18, 2, allocatedInvoiceDiscount); AddDecimal(command, "@TaxRate", 9, 4, line.TaxRatePercent);
                AddDecimal(command, "@TaxAmount", 18, 2, taxAmount); AddDecimal(command, "@LineTotal", 18, 2, lineTotal);
                AddDecimal(command, "@UnitCost", 18, 2, unitCost / line.Quantity);
                AddDecimal(command, "@CostTotal", 18, 2, unitCost);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertLotAllocations(SqlConnection connection, SqlTransaction transaction, Guid lineId, IReadOnlyList<LotAllocation> allocations)
        {
            const string legacySql = "INSERT INTO dbo.LotAllocationsTb (AllocationId, InvoiceLineId, LotId, MovementType, Quantity, UnitCost) VALUES (@AllocationId, @InvoiceLineId, @LotId, N'Sale', @Quantity, @UnitCost);";
            foreach (LotAllocation allocation in allocations)
            {
                using (SqlCommand command = new SqlCommand(allocation.CostTotal.HasValue ? legacySql.Replace("Quantity, UnitCost)", "Quantity, UnitCost, CostTotal)").Replace("@Quantity, @UnitCost);", "@Quantity, @UnitCost, @CostTotal);") : legacySql, connection, transaction))
                {
                    if (allocation.CostTotal.HasValue) AddDecimal(command, "@CostTotal", 18, 2, allocation.CostTotal.Value);
                    command.Parameters.Add("@AllocationId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                    command.Parameters.Add("@InvoiceLineId", SqlDbType.UniqueIdentifier).Value = lineId;
                    command.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = allocation.LotId;
                    AddDecimal(command, "@Quantity", 18, 3, allocation.Quantity); AddDecimal(command, "@UnitCost", 18, 2, allocation.UnitCost);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void DecrementAggregateStock(SqlConnection connection, SqlTransaction transaction, string medicineName, decimal quantity)
        {
            const string sql = "UPDATE dbo.MedicineTb1 SET MedQty = MedQty - @Quantity WHERE MedName = @MedName AND MedQty >= @Quantity;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = Convert.ToInt32(quantity);
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("الرصيد المجمع لا يطابق الدفعات؛ أوقف البيع وصحح الجرد.");
            }
        }

        private static void MarkIssued(SqlConnection connection, SqlTransaction transaction, Guid invoiceId)
        {
            using (SqlCommand command = new SqlCommand("UPDATE dbo.SalesInvoicesTb SET Status = N'Issued' WHERE InvoiceId = @InvoiceId AND Status = N'Draft';", connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("تعذر اعتماد الفاتورة التجارية.");
            }
        }

        private static void AddDecimal(SqlCommand command, string name, byte precision, byte scale, decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = precision; parameter.Scale = scale; parameter.Value = value;
        }

        private static object DbValue(string value) { return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim(); }
    }
}
