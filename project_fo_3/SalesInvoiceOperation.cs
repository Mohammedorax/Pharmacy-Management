using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    // حد العملية بين شاشة البيع والحفظ المالي/المخزني. لا يعتمد على أي عنصر WinForms.
    internal sealed partial class SalesInvoiceOperation : ISalesInvoiceOperation
    {
        internal Action BeforeCommitGateForVerification {get;set;}
        internal Action<SqlConnection,SqlTransaction> DisconnectConnectionBeforeCommitForVerification {get;set;}
        internal Action InCommitGateForVerification {get;set;}
        public SalesInvoiceIssueResult Issue(SalesInvoiceIssueRequest request) => IssueCore(request,null,null,null);
        private SalesInvoiceIssueResult IssueCore(SalesInvoiceIssueRequest request,SalesWriteIntent identity,Func<bool> current,string expectedSource)
        {
            if (!PermissionService.CanExecute(AppOperation.PostSalesInvoice))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد فاتورة بيع.");
            Validate(request);
            using (SqlConnection connection = expectedSource==null?Database.CreateConnection():new SqlConnection(expectedSource))
            {
                connection.Open();
                if(current!=null&&!current())throw new UnauthorizedAccessException("تغيرت جلسة محاولة البيع.");
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if(identity!=null){
                            SalesCommitAuthority.Acquire(connection,transaction,identity,true);
                            var saved=SalesWriteReceiptStore.AcquireAndRead(connection,transaction,identity);
                            if(!current())throw new UnauthorizedAccessException("تغيرت جلسة محاولة البيع.");
                            if(saved!=null){SalesWriteReceiptStore.VerifyPersisted(connection,transaction,identity,saved);transaction.Rollback();return saved;}
                        }
                        if(identity!=null)SalesWriteReceiptStore.AcquireBusinessSettings(connection,transaction);
                        CommercialAccountingPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, request.InvoiceDate);
                        ImportedInvoiceApprovalGuard.EnsureDraftCurrency(connection, transaction, request.ExpectedCurrencyCode);
                        ImportedInvoiceApprovalGuard.EnsureCurrency(connection, transaction, request.ImportCurrencyCode);
                        ImportedInvoiceApprovalGuard.EnsureSalesTax(connection, transaction,
                            request.ImportTaxRatePercent, request.ImportPriceTaxMode);
                        string customerId = ResolveCustomerId(connection, transaction, request.CustomerId, request.PhoneNumber);
                        CommercialSchemaInstaller.EnsureRequestedCommercialModeIsReady(connection, transaction);
                        bool commercial = CommercialSchemaInstaller.IsFullCommercialMode(connection, transaction); if(identity!=null&&commercial!=identity.CommercialExpected)throw new InvalidOperationException("تغير نمط المخزون منذ تجهيز محاولة البيع؛ تحقق قبل المتابعة.");
                        if (!commercial && request.ImportTaxRatePercent.GetValueOrDefault() != 0m)
                            throw new InvalidOperationException("البيع المستورد الخاضع للضريبة يحتاج مسار الفواتير التجارية قبل الاعتماد.");
                        SalesInvoiceIssueResult result = commercial
                            ? IssueCommercial(connection, transaction, request, customerId)
                            : IssueLegacy(connection, transaction, request, customerId);
                        ImportedInvoiceSourceRegistry.Record(connection, transaction, "Sale", request.ImportSourceSystem,
                            request.ImportSourceInvoiceId, result.AccountingSourceId);
                        ImportedInvoiceApprovalGuard.MarkDraftApproved(connection, transaction, request.ImportDraftId,
                            "Sale", request.ImportSourceSystem, request.ImportSourceInvoiceId, result.AccountingSourceId);
                        SqlCashBoxJournalRepository.LinkSaleCash(connection, transaction,
                            result.AccountingSourceId, request.CashierEmployeeId, request.EmployeeName, result.CashCollected);
                        if(identity!=null){if(!current())throw new UnauthorizedAccessException("تغيرت جلسة اعتماد البيع.");SalesWriteReceiptStore.Record(connection,transaction,identity,result);if(!current())throw new UnauthorizedAccessException("تغيرت جلسة اعتماد البيع.");}
            WriteAudit(connection, transaction, request, result, identity?.Owner);
                        BeforeCommitGateForVerification?.Invoke();
                        DisconnectConnectionBeforeCommitForVerification?.Invoke(connection, transaction);
                        if(identity!=null){using(SessionCommitGate.Commit(current)){InCommitGateForVerification?.Invoke();transaction.Commit();}}
                        else transaction.Commit();
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

        private static SalesInvoiceIssueResult IssueCommercial(SqlConnection connection, SqlTransaction transaction, SalesInvoiceIssueRequest request, string customerId)
        {
            UpsertCustomerSummary(connection, transaction, customerId, request, request.DisplayedNetTotal);
            decimal credit = 0m;
            if (request.UseCustomerCredit)
            {
                if (!string.Equals(request.CreditCustomerId, customerId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("تغير العميل بعد اختيار استخدام الرصيد. أعد اختيار العميل ثم حاول مرة أخرى.");
                decimal available = CustomerAccountLedgerService.GetBalanceForUpdate(connection, transaction, customerId);
                if (available <= 0m)
                    throw new InvalidOperationException("لم يعد للعميل رصيد متاح. حدّث بيانات العميل ثم أعد المحاولة.");
                credit = InvoiceTotalsCalculator.Round(Math.Min(available, request.DisplayedNetTotal));
            }

            decimal taxRate = AccountingService.GetEffectiveTaxRate(connection, transaction);
            var lines = new List<CommercialInvoiceLine>();
            foreach (SalesInvoiceLineRequest requestedLine in request.Lines)
            {
                decimal currentPrice = requestedLine.Unit?.UnitPrice ?? GetCurrentSellingPrice(connection, transaction, requestedLine.MedicineName);
                if (currentPrice <= 0 || (requestedLine.Unit == null && currentPrice != requestedLine.DisplayedSellingPrice) || requestedLine.Quantity <= 0)
                    throw new InvalidOperationException("تغير سعر أو كمية بند منذ إضافته إلى السلة. حدّث السلة ثم أعد الحفظ.");
                lines.Add(new CommercialInvoiceLine
                {
                    MedicineName = requestedLine.MedicineName,
                    Quantity = requestedLine.Unit?.EnteredQuantity ?? requestedLine.Quantity,
                    Unit = requestedLine.Unit,
                    UnitPrice = currentPrice,
                    LineDiscount = 0m,
                    TaxRatePercent = taxRate,
                    PriceTaxMode = CommercialPriceTaxMode.Inclusive
                });
            }

            CurrencyInfo currency = Database.GetSystemCurrency();
            var draft = new CommercialSaleDraft
            {
                CustomerId = customerId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.PhoneNumber,
                CustomerAddress = request.CustomerAddress,
                CurrencyCode = string.IsNullOrWhiteSpace(currency.CurrencyCode) ? "SAR" : currency.CurrencyCode,
                ExchangeRate = 1m,
                InvoiceDiscountPercent = request.DiscountPercent,
                IssuedBy = request.EmployeeName,
                Notes = request.Notes,
                CustomerCreditApplied = credit,
                Lines = lines
            };
            CommercialSaleResult issued = CommercialSalesRepository.Issue(connection, transaction, draft, request.InvoiceDate.ToUniversalTime());
            decimal displayedGross = InvoiceTotalsCalculator.Round(request.DisplayedGrossTotal);
            decimal displayedDiscount = InvoiceTotalsCalculator.Round(displayedGross - request.DisplayedNetTotal);
            if (issued.Totals.GrossTotal != displayedGross || issued.Totals.DiscountTotal != displayedDiscount ||
                issued.Totals.NetTotal != request.DisplayedNetTotal)
                throw new InvalidOperationException("إجمالي البيع التجاري لا يطابق إجمالي الواجهة. راجع إعداد الضريبة قبل الاعتماد.");

            string sourceId = issued.InvoiceId.ToString("D");
            int journalEntryId = AccountingService.PostSalesInvoice(connection, transaction, sourceId, issued.Totals,
                "فاتورة تجارية رقم " + issued.InvoiceNumber, issued.CostOfGoods, request.InvoiceDate, issued.CustomerCreditApplied);
            if (issued.CustomerCreditApplied > 0m)
                CustomerAccountLedgerService.RecordCreditUse(connection, transaction, customerId, journalEntryId, sourceId,
                    issued.CustomerCreditApplied, request.InvoiceDate, request.EmployeeName);
            return new SalesInvoiceIssueResult
            {
                IsCommercial = true,
                CustomerId = customerId,
                CommercialInvoiceId = issued.InvoiceId,
                CommercialInvoiceNumber = issued.InvoiceNumber,
                AccountingSourceId = sourceId,
                BillLineIds = new string[0],
                GrossTotal = issued.Totals.GrossTotal,
                DiscountTotal = issued.Totals.DiscountTotal,
                TaxTotal = issued.Totals.TaxTotal,
                NetTotal = issued.Totals.NetTotal,
                CostOfGoods = issued.CostOfGoods,
                CustomerCreditApplied = issued.CustomerCreditApplied,
                CashCollected = issued.CashCollected
            };
        }

        private static SalesInvoiceIssueResult IssueLegacy(SqlConnection connection, SqlTransaction transaction, SalesInvoiceIssueRequest request, string customerId)
        {
            var reservedIds = new HashSet<long>();
            var billIds = new List<string>();
            decimal grossTotal = 0m;
            decimal costOfGoods = 0m;
            foreach (SalesInvoiceLineRequest line in request.Lines)
            {
                int buyingPrice=0;
                decimal sellingPrice;
                decimal lineTotal;
                decimal lineCost;
                if(line.Unit!=null)
                {
                    MedicineUnitInvoiceBridge.ValidateQuote(connection,transaction,line.MedicineName,line.Unit,true,line.Quantity,request.ExpectedCurrencyCode ?? Database.GetSystemCurrency().CurrencyCode);
                    sellingPrice=line.Unit.UnitPrice;lineTotal=line.Unit.GrossTotal;
                    using(var cmd=new SqlCommand("SELECT U.BuyingPrice FROM dbo.MedicineUnitsTb U WITH(UPDLOCK,HOLDLOCK) WHERE U.UnitId=@id",connection,transaction))
                    {cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=line.Unit.UnitId;lineCost=InvoiceTotalsCalculator.Round(Convert.ToDecimal(cmd.ExecuteScalar())*line.Unit.EnteredQuantity);}
                }
                else
                {
                    sellingPrice=GetCurrentPrices(connection,transaction,line.MedicineName,out buyingPrice);
                    if(line.DisplayedSellingPrice!=sellingPrice)throw new InvalidOperationException("تغير سعر الدواء منذ إضافته؛ حدّث البند.");
                    lineTotal=checked((int)sellingPrice*line.Quantity);lineCost=checked((decimal)buyingPrice*line.Quantity);
                }
                if(sellingPrice<=0||line.Quantity<=0||lineTotal<=0)throw new InvalidOperationException("يوجد بند بسعر أو كمية غير صحيحة.");
                grossTotal=checked(grossTotal+lineTotal);costOfGoods=checked(costOfGoods+lineCost);

                int before = InventoryMovementService.GetCurrentQuantity(connection, transaction, line.MedicineName);
                if (before < line.Quantity)
                    throw new InvalidOperationException("المخزون غير كاف للدواء " + line.MedicineName + ". المتاح " + before + " والمطلوب " + line.Quantity + ".");
                string billId = NextBillId(connection, transaction, reservedIds);
                DecrementLegacyStock(connection, transaction, line.MedicineName, line.Quantity);
                InsertLegacyLine(connection, transaction, billId, line.MedicineName, sellingPrice, line.Quantity, lineTotal, customerId);
                MedicineUnitInvoiceBridge.SaveSnapshot(connection,transaction,"Sale",billId,line.MedicineName,line.Unit);
                InventoryMovementService.Record(connection, transaction, line.MedicineName, -line.Quantity, before,
                    before - line.Quantity, "SalesInvoice", billId, null, line.Unit == null ? sellingPrice : decimal.Round(lineTotal / line.Quantity, 2, MidpointRounding.AwayFromZero), line.Unit == null ? "خصم مخزون من فاتورة بيع" : "خصم مخزون بالوحدة الأساسية؛ " + line.Unit.EnteredQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + line.Unit.Name + "؛ إجمالي البند " + lineTotal.ToString(System.Globalization.CultureInfo.InvariantCulture), request.EmployeeName, request.InvoiceDate);
                billIds.Add(billId);
            }

            decimal expectedNet = InvoiceTotalsCalculator.Round(grossTotal - InvoiceTotalsCalculator.Round(grossTotal * request.DiscountPercent / 100m));
            if (grossTotal != request.DisplayedGrossTotal || expectedNet != request.DisplayedNetTotal)
                throw new InvalidOperationException("صافي الفاتورة بعد الخصم غير صحيح.");
            UpsertCustomerSummary(connection, transaction, customerId, request, request.DisplayedNetTotal);
            string sourceId = billIds[0];
            LegacySalesInvoiceRepository.RecordIssuedInvoice(connection, transaction, sourceId, billIds, customerId,
                request.CustomerName, request.PhoneNumber, request.DisplayedNetTotal, request.EmployeeName, request.InvoiceDate, request.Notes);
            string description = "فاتورة بيع رقم " + sourceId;
            if (costOfGoods > 0m) description += " (تكلفة تقديرية حتى تفعيل الدفعات)";
            decimal taxTotal = AccountingService.CalculateIncludedTax(connection, transaction, request.DisplayedNetTotal);
            AccountingService.PostSalesInvoice(connection, transaction, sourceId, request.DisplayedNetTotal, description, costOfGoods, request.ExpectedCurrencyCode, request.InvoiceDate, taxTotal);
            return new SalesInvoiceIssueResult
            {
                IsCommercial = false,
                CustomerId = customerId,
                AccountingSourceId = sourceId,
                BillLineIds = billIds.AsReadOnly(),
                GrossTotal = grossTotal,
                DiscountTotal = grossTotal - request.DisplayedNetTotal,
                TaxTotal = taxTotal,
                NetTotal = request.DisplayedNetTotal,
                CostOfGoods = costOfGoods,
                CashCollected = request.DisplayedNetTotal
            };
        }

        private static void Validate(SalesInvoiceIssueRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Lines == null || request.Lines.Count == 0) throw new InvalidOperationException("لا توجد بنود في الفاتورة.");
            if (request.InvoiceDate == default(DateTime)) throw new InvalidOperationException("تاريخ الفاتورة غير صالح.");
            if (string.IsNullOrWhiteSpace(request.CustomerName)) throw new InvalidOperationException("اسم العميل مطلوب.");
            if (string.IsNullOrWhiteSpace(request.EmployeeName)) throw new InvalidOperationException("اسم الموظف مطلوب.");
            if (request.DiscountPercent < 0m || request.DiscountPercent >= 100m) throw new InvalidOperationException("نسبة الخصم غير صالحة.");
            if (request.DisplayedGrossTotal <= 0m || request.DisplayedNetTotal <= 0m) throw new InvalidOperationException("إجمالي الفاتورة يجب أن يكون أكبر من صفر.");
            EnsureLength(request.CustomerId, 50, "رقم العميل");
            EnsureLength(request.CustomerName, 200, "اسم العميل");
            EnsureLength(request.PhoneNumber, 50, "رقم الهاتف");
            EnsureLength(request.CustomerAddress, 300, "عنوان العميل");
            EnsureLength(request.EmployeeName, 200, "اسم الموظف");
            EnsureLength(request.CashierEmployeeId, 50, "رقم الكاشير");
            EnsureCashierSelection(request.CashierEmployeeId);
            EnsureLength(request.Notes, 1000, "ملاحظات الفاتورة");
            foreach (SalesInvoiceLineRequest line in request.Lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.MedicineName) || line.MedicineName.Trim().Length > 200)
                    throw new InvalidOperationException("يوجد بند بدون اسم دواء صالح.");
                if(line.Unit!=null && line.Unit.BaseQuantity!=line.Quantity)throw new InvalidOperationException("كمية المخزون لا تطابق الوحدة المختارة.");
                line.MedicineName = line.MedicineName.Trim();
            }
        }

        internal static void EnsureCashierSelection(string cashierEmployeeId)
        {
            if (!string.IsNullOrWhiteSpace(cashierEmployeeId) &&
                !PermissionService.IsManagerRole(SessionContext.CurrentRole) &&
                !string.Equals(cashierEmployeeId.Trim(), SessionContext.CurrentEmployeeId,
                    StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("لا يمكن للكاشير نسبة فاتورة البيع إلى موظف آخر.");
        }

        private static string ResolveCustomerId(SqlConnection connection, SqlTransaction transaction, string requestedId, string phoneNumber)
        {
            string customerId = (requestedId ?? string.Empty).Trim();
            string phone = (phoneNumber ?? string.Empty).Trim();
            if (customerId.Length == 0) customerId = CustomerLookupService.FindCustomerIdByPhone(connection, transaction, phone);
            if (customerId.Length == 0 && string.Equals(phone, "000000000", StringComparison.Ordinal)) return "0";
            if (customerId.Length == 0)
                throw new InvalidOperationException("العميل غير مسجل. أضفه من شاشة إدارة العملاء والموردين ثم اختره في الفاتورة.");

            using (SqlCommand command = new SqlCommand(@"SELECT PhoneNumber FROM dbo.CustomersBillsTb WITH (UPDLOCK, HOLDLOCK) WHERE CustomerId=@CustomerId;", connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId;
                object registeredPhone = command.ExecuteScalar();
                if (registeredPhone == null || registeredPhone == DBNull.Value)
                {
                    if (customerId == "0" && phone == "000000000") return "0";
                    throw new InvalidOperationException("معرّف العميل غير موجود. حدّث قائمة العملاء ثم أعد اختيار العميل.");
                }
                if (!string.Equals(Convert.ToString(registeredPhone).Trim(), phone, StringComparison.Ordinal))
                    throw new InvalidOperationException("بيانات العميل المختار تغيرت. أعد اختياره من قائمة العملاء قبل إصدار الفاتورة.");
            }
            return customerId;
        }

        private static int GetCurrentSellingPrice(SqlConnection connection, SqlTransaction transaction, string medicineName)
        {
            int ignored;
            return GetCurrentPrices(connection, transaction, medicineName, out ignored);
        }

        private static int GetCurrentPrices(SqlConnection connection, SqlTransaction transaction, string medicineName, out int buyingPrice)
        {
            using (SqlCommand command = new SqlCommand("SELECT TOP (1) Sprice, ISNULL(Bprice, 0) AS Bprice FROM dbo.MedicineTb1 WHERE MedName = @MedName", connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read()) { buyingPrice = 0; return 0; }
                    buyingPrice = Convert.ToInt32(reader["Bprice"]);
                    return Convert.ToInt32(reader["Sprice"]);
                }
            }
        }

        private static string NextBillId(SqlConnection connection, SqlTransaction transaction, ISet<long> reserved)
        {
            long value = Database.GetNextSequenceValue(connection, transaction, "Seq_BillId");
            while (reserved.Contains(value)) value = Database.GetNextSequenceValue(connection, transaction, "Seq_BillId");
            reserved.Add(value);
            return value.ToString();
        }

        private static void DecrementLegacyStock(SqlConnection connection, SqlTransaction transaction, string medicineName, int quantity)
        {
            using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1 SET MedQty = MedQty - @Quantity
WHERE MedName = @MedName AND MedQty >= @Quantity AND ExpDate >= CAST(GETDATE() AS DATE);", connection, transaction))
            {
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("تعذر خصم المخزون للدواء " + medicineName + ".");
            }
        }

        private static void InsertLegacyLine(SqlConnection connection, SqlTransaction transaction, string billId, string medicineName, decimal price, int quantity, decimal total, string customerId)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.BillsTb1 (BillId, MedName, Sprice, Quantity, TotalAmount, CustomerId)
VALUES (@BillId, @MedName, @Sprice, @Quantity, @TotalAmount, @CustomerId);", connection, transaction))
            {
                command.Parameters.Add("@BillId", SqlDbType.NVarChar, 50).Value = billId;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                MedicineUnitInvoiceBridge.Money(command,"@Sprice",price);
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;
                MedicineUnitInvoiceBridge.Money(command,"@TotalAmount",total);
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId;
                command.ExecuteNonQuery();
            }
        }

        private static void UpsertCustomerSummary(SqlConnection connection, SqlTransaction transaction, string customerId, SalesInvoiceIssueRequest request, decimal total)
        {
            const string sql = @"IF EXISTS (SELECT 1 FROM dbo.CustomersBillsTb WHERE CustomerId = @CustomerId)
UPDATE dbo.CustomersBillsTb SET CustomerName = @CustomerName, TotalSpent = TotalSpent + @TotalAmount, EmployeeName = @EmployeeName, InvoiceDate = @InvoiceDate, PhoneNumber = @PhoneNumber, CustomerAddress = @CustomerAddress, InvoiceNotes = @InvoiceNotes WHERE CustomerId = @CustomerId;
ELSE INSERT INTO dbo.CustomersBillsTb (CustomerId, CustomerName, PhoneNumber, TotalSpent, EmployeeName, InvoiceDate, CustomerAddress, InvoiceNotes) VALUES (@CustomerId, @CustomerName, @PhoneNumber, @TotalAmount, @EmployeeName, @InvoiceDate, @CustomerAddress, @InvoiceNotes);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId;
                command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 200).Value = request.CustomerName.Trim();
                command.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 50).Value = (request.PhoneNumber ?? string.Empty).Trim();
                SqlParameter amount = command.Parameters.Add("@TotalAmount", SqlDbType.Decimal); amount.Precision = 18; amount.Scale = 2; amount.Value = total;
                command.Parameters.Add("@EmployeeName", SqlDbType.NVarChar, 200).Value = request.EmployeeName.Trim();
                command.Parameters.Add("@InvoiceDate", SqlDbType.DateTime).Value = request.InvoiceDate;
                command.Parameters.Add("@CustomerAddress", SqlDbType.NVarChar, 300).Value = (request.CustomerAddress ?? string.Empty).Trim();
                command.Parameters.Add("@InvoiceNotes", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(request.Notes) ? (object)DBNull.Value : request.Notes.Trim();
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureLength(string value, int maximum, string fieldName)
        {
            if ((value ?? string.Empty).Trim().Length > maximum)
                throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximum + ").");
        }

        // Atomic audit: the row is inserted inside the sales transaction, so a failed audit rolls the
        // sale back instead of committing a financial document with no audit trail.
        private static void WriteAudit(SqlConnection connection, SqlTransaction transaction,
            SalesInvoiceIssueRequest request, SalesInvoiceIssueResult result, OperationActor actor)
        {
            string action = result.IsCommercial ? "CommercialSalesInvoiceIssued" : "SalesInvoiceSaved";
            string details = "فاتورة بيع معتمدة. المرجع: " + result.AccountingSourceId +
                "، العميل: " + request.CustomerName + "، الإجمالي: " + result.NetTotal.ToString("N2") +
                "، التكلفة: " + result.CostOfGoods.ToString("N2") + ".";
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, @EntityName, @Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor?.UserName ?? SessionContext.CurrentUser ?? string.Empty;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor?.UserRole ?? SessionContext.CurrentRole ?? string.Empty;
                command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = "Billing";
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details.Trim();
                command.ExecuteNonQuery();
            }
        }
    }
}
