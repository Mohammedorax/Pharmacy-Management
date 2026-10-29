using System;
using PharmacyManagement.Domain;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace project_fo_3
{
    internal enum CommercialPriceTaxMode
    {
        Exclusive,
        Inclusive
    }

    internal sealed class CommercialInvoiceLine
    {
        public string MedicineName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public MedicineUnitSelection Unit { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public CommercialPriceTaxMode PriceTaxMode { get; set; }

        public CommercialInvoiceLine()
        {
            MedicineName = string.Empty;
            PriceTaxMode = CommercialPriceTaxMode.Exclusive;
        }
    }

    internal sealed class InvoiceTotals
    {
        public CommercialPriceTaxMode PriceTaxMode { get; set; }
        public decimal GrossTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxableTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal NetTotal { get; set; }

        public InvoiceTotals()
        {
            PriceTaxMode = CommercialPriceTaxMode.Exclusive;
        }
    }

    internal static class InvoiceTotalsContract
    {
        public static void Validate(InvoiceTotals totals)
        {
            if (totals == null || totals.GrossTotal < 0m || totals.DiscountTotal < 0m || totals.TaxableTotal < 0m || totals.TaxTotal < 0m || totals.NetTotal < 0m)
                throw new InvalidOperationException("إجماليات الفاتورة التجارية غير صالحة.");
            if (InvoiceTotalsCalculator.Round(totals.TaxableTotal + totals.TaxTotal) != totals.NetTotal)
                throw new InvalidOperationException("الصافي لا يطابق الوعاء الضريبي مع الضريبة.");

            decimal expectedNet = totals.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                ? InvoiceTotalsCalculator.Round(totals.GrossTotal - totals.DiscountTotal)
                : InvoiceTotalsCalculator.Round(totals.GrossTotal - totals.DiscountTotal + totals.TaxTotal);
            if (expectedNet != totals.NetTotal)
                throw new InvalidOperationException("Gross وDiscount وTax لا تطابق Net وفق سياسة تسعير الفاتورة.");
        }
    }

    // الحسابات هنا مستقلة عن الواجهة وقاعدة البيانات حتى يبقى مبلغ الفاتورة نفسه في الشاشة والطباعة والقيد.
    internal static class InvoiceTotalsCalculator
    {
        public static InvoiceTotals Calculate(IEnumerable<CommercialInvoiceLine> lines, decimal invoiceDiscountPercent)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (invoiceDiscountPercent < 0m || invoiceDiscountPercent > 100m)
            {
                throw new InvalidOperationException("نسبة الخصم يجب أن تكون بين 0 و100.");
            }

            List<CommercialInvoiceLine> materialized = lines.ToList();
            if (materialized.Count == 0)
            {
                throw new InvalidOperationException("لا يمكن اعتماد فاتورة بلا بنود.");
            }

            decimal gross = 0m;
            decimal lineDiscount = 0m;
            CommercialPriceTaxMode priceTaxMode = materialized[0] == null ? CommercialPriceTaxMode.Exclusive : materialized[0].PriceTaxMode;
            foreach (CommercialInvoiceLine line in materialized)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.MedicineName) || line.Quantity <= 0m || line.UnitPrice < 0m ||
                    line.LineDiscount < 0m || line.TaxRatePercent < 0m || line.TaxRatePercent > 100m || line.PriceTaxMode != priceTaxMode)
                {
                    throw new InvalidOperationException("يوجد بند فاتورة غير صالح أو يتبع سياسة ضريبة مختلفة عن بقية الفاتورة.");
                }

                EnsureMoneyScale(line.UnitPrice, "سعر البند");
                EnsureMoneyScale(line.LineDiscount, "خصم البند");
                EnsureScale(line.Quantity, 3, "كمية البند");
                EnsureScale(line.TaxRatePercent, 4, "نسبة الضريبة");

                decimal lineGross = Round(line.Quantity * line.UnitPrice);
                if (line.LineDiscount > lineGross)
                {
                    throw new InvalidOperationException("خصم البند لا يمكن أن يتجاوز قيمته.");
                }

                gross += lineGross;
                lineDiscount += line.LineDiscount;
            }

            decimal afterLineDiscount = Round(gross - lineDiscount);
            decimal invoiceDiscount = Round(afterLineDiscount * invoiceDiscountPercent / 100m);
            decimal taxable = priceTaxMode == CommercialPriceTaxMode.Exclusive ? Round(afterLineDiscount - invoiceDiscount) : 0m;
            decimal tax = 0m;

            // يوزع خصم الفاتورة نسبياً قبل حساب ضريبة كل بند لمنع اختلاف التقريب بين الرأس والبنود.
            IReadOnlyList<decimal> allocatedDiscounts = AllocateInvoiceDiscounts(materialized, invoiceDiscount);
            for (int index = 0; index < materialized.Count; index++)
            {
                CommercialInvoiceLine line = materialized[index];
                decimal lineGross = Round(line.Quantity * line.UnitPrice);
                decimal lineTaxableBeforeInvoiceDiscount = Round(lineGross - line.LineDiscount);
                decimal proportionalDiscount = allocatedDiscounts[index];
                decimal lineAmountAfterDiscount = Round(lineTaxableBeforeInvoiceDiscount - proportionalDiscount);
                decimal lineTax = priceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? Round(lineAmountAfterDiscount * line.TaxRatePercent / (100m + line.TaxRatePercent))
                    : Round(lineAmountAfterDiscount * line.TaxRatePercent / 100m);
                tax += lineTax;
                if (priceTaxMode == CommercialPriceTaxMode.Inclusive)
                    taxable += Round(lineAmountAfterDiscount - lineTax);
            }

            return new InvoiceTotals
            {
                PriceTaxMode = priceTaxMode,
                GrossTotal = Round(gross),
                DiscountTotal = Round(lineDiscount + invoiceDiscount),
                TaxableTotal = taxable,
                TaxTotal = Round(tax),
                NetTotal = priceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? Round(gross - lineDiscount - invoiceDiscount)
                    : Round(taxable + tax)
            };
        }

        public static decimal Round(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        public static IReadOnlyList<decimal> CalculateAllocatedInvoiceDiscounts(IEnumerable<CommercialInvoiceLine> lines, decimal invoiceDiscountPercent)
        {
            List<CommercialInvoiceLine> materialized = lines == null ? null : lines.ToList();
            Calculate(materialized, invoiceDiscountPercent);
            decimal afterLineDiscount = materialized.Sum(x => Round(x.Quantity * x.UnitPrice) - x.LineDiscount);
            decimal invoiceDiscount = Round(afterLineDiscount * invoiceDiscountPercent / 100m);
            return AllocateInvoiceDiscounts(materialized, invoiceDiscount);
        }

        // Allocate whole minor units, then distribute the remaining units by largest
        // remainder. A free line cannot absorb a rounding difference. Stable content
        // tie-breakers keep tax totals independent of the display order of lines.
        private static IReadOnlyList<decimal> AllocateInvoiceDiscounts(
            IList<CommercialInvoiceLine> lines, decimal invoiceDiscount)
        {
            decimal[] bases = lines.Select(x => Round(x.Quantity * x.UnitPrice) - x.LineDiscount).ToArray();
            decimal total = bases.Sum();
            decimal[] result = new decimal[lines.Count];
            if (total == 0m || invoiceDiscount == 0m) return result;
            if (invoiceDiscount == total) return bases;

            decimal[] remainders = new decimal[lines.Count];
            for (int index = 0; index < lines.Count; index++)
            {
                decimal exact = invoiceDiscount * (bases[index] / total);
                result[index] = decimal.Floor(exact * 100m) / 100m;
                remainders[index] = exact - result[index];
            }

            int unitsRemaining = decimal.ToInt32(Round(invoiceDiscount - result.Sum()) * 100m);
            int[] order = Enumerable.Range(0, lines.Count)
                .Where(index => result[index] < bases[index])
                .OrderByDescending(index => remainders[index])
                .ThenBy(index => lines[index].MedicineName, StringComparer.Ordinal)
                .ThenBy(index => lines[index].TaxRatePercent)
                .ThenBy(index => lines[index].PriceTaxMode)
                .ThenBy(index => lines[index].Quantity)
                .ThenBy(index => lines[index].UnitPrice)
                .ThenBy(index => lines[index].LineDiscount)
                .ThenBy(index => index)
                .ToArray();
            if (unitsRemaining < 0 || unitsRemaining > order.Length)
                throw new InvalidOperationException("تعذر توزيع خصم الفاتورة ضمن دقة العملة.");
            for (int index = 0; index < unitsRemaining; index++) result[order[index]] += .01m;

            if (result.Sum() != invoiceDiscount ||
                Enumerable.Range(0, lines.Count).Any(index => result[index] < 0m || result[index] > bases[index]))
                throw new InvalidOperationException("توزيع الخصم لا يطابق حدود بنود الفاتورة.");
            return result;
        }

        public static IReadOnlyList<decimal> CalculateLineTaxAmounts(IEnumerable<CommercialInvoiceLine> lines, decimal invoiceDiscountPercent)
        {
            List<CommercialInvoiceLine> materialized = lines == null ? null : lines.ToList();
            InvoiceTotals totals = Calculate(materialized, invoiceDiscountPercent);
            IReadOnlyList<decimal> discounts = CalculateAllocatedInvoiceDiscounts(materialized, invoiceDiscountPercent);
            List<decimal> result = new List<decimal>();
            for (int index = 0; index < materialized.Count; index++)
            {
                CommercialInvoiceLine line = materialized[index];
                decimal before = Round(line.Quantity * line.UnitPrice) - line.LineDiscount;
                decimal allocated = discounts[index];
                decimal amountAfterDiscount = Round(before - allocated);
                result.Add(line.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? Round(amountAfterDiscount * line.TaxRatePercent / (100m + line.TaxRatePercent))
                    : Round(amountAfterDiscount * line.TaxRatePercent / 100m));
            }
            if (Round(result.Sum()) != totals.TaxTotal) throw new InvalidOperationException("ضريبة بنود الفاتورة لا تطابق ضريبة الرأس.");
            return result;
        }

        private static void EnsureMoneyScale(decimal value, string fieldName)
        {
            EnsureScale(value, 2, fieldName);
        }

        private static void EnsureScale(decimal value, int scale, string fieldName)
        {
            if (value != decimal.Round(value, scale, MidpointRounding.AwayFromZero)) throw new InvalidOperationException(fieldName + " يتجاوز الدقة المسموح بها.");
        }
    }

    internal sealed class InventoryLotSnapshot
    {
        public Guid LotId { get; set; }
        public string MedicineName { get; set; }
        public string BatchNumber { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime ReceivedAt { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal? RemainingCost { get; set; }
        public decimal UnitCost { get; set; }
        public bool IsRecalled { get; set; }

        public InventoryLotSnapshot()
        {
            MedicineName = string.Empty;
            BatchNumber = string.Empty;
        }
    }

    internal sealed class LotAllocation
    {
        public decimal? CostTotal { get; set; }
        public Guid LotId { get; set; }
        public string BatchNumber { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    internal sealed class LotReturnRequest
    {
        public Guid LotId { get; set; }
        public decimal Quantity { get; set; }
    }

    internal static class LotReturnValidator
    {
        // المرتجع لا يعيد كمية إلى دفعة عشوائية؛ يتحقق من تخصيصات البيع الأصلية أولاً.
        public static IReadOnlyList<LotReturnRequest> Validate(IEnumerable<LotAllocation> originalAllocations, IEnumerable<LotReturnRequest> returnRequests)
        {
            return Validate(originalAllocations, Enumerable.Empty<LotReturnRequest>(), returnRequests);
        }

        // عند التنفيذ الفعلي يجب تمرير المرتجعات السابقة المقروءة بقفل من قاعدة البيانات في المعاملة نفسها.
        public static IReadOnlyList<LotReturnRequest> Validate(IEnumerable<LotAllocation> originalAllocations, IEnumerable<LotReturnRequest> previousReturns, IEnumerable<LotReturnRequest> returnRequests)
        {
            if (originalAllocations == null || previousReturns == null || returnRequests == null) throw new ArgumentNullException("تخصيصات الدفعة مطلوبة.");
            List<LotAllocation> original = originalAllocations.ToList();
            List<LotReturnRequest> prior = previousReturns.ToList();
            List<LotReturnRequest> requested = returnRequests.ToList();
            if (original.Count == 0 || original.Any(x => x == null || x.LotId == Guid.Empty || !IsValidQuantity(x.Quantity)))
                throw new InvalidOperationException("تخصيصات البيع الأصلية غير صالحة للمرتجع.");
            if (prior.Any(x => x == null || x.LotId == Guid.Empty || !IsValidQuantity(x.Quantity)))
                throw new InvalidOperationException("سجل المرتجعات السابق غير صالح.");
            if (requested.Count == 0 || requested.Any(x => x == null || x.LotId == Guid.Empty || !IsValidQuantity(x.Quantity)))
                throw new InvalidOperationException("بنود المرتجع غير صالحة.");

            Dictionary<Guid, decimal> soldByLot = original
                .GroupBy(x => x.LotId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            Dictionary<Guid, decimal> returnedBefore = prior
                .GroupBy(x => x.LotId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            List<LotReturnRequest> normalized = requested
                .GroupBy(x => x.LotId)
                .Select(x => new LotReturnRequest { LotId = x.Key, Quantity = x.Sum(y => y.Quantity) })
                .OrderBy(x => x.LotId)
                .ToList();

            foreach (LotReturnRequest returnedLot in normalized)
            {
                if (!IsValidQuantity(returnedLot.Quantity))
                    throw new InvalidOperationException("تجميع بند المرتجع يتجاوز دقة كمية المخزون.");
                decimal previouslyReturned = returnedBefore.TryGetValue(returnedLot.LotId, out decimal previousQuantity) ? previousQuantity : 0m;
                if (!soldByLot.TryGetValue(returnedLot.LotId, out decimal soldQuantity) || previouslyReturned + returnedLot.Quantity > soldQuantity)
                    throw new InvalidOperationException("كمية المرتجع تتجاوز ما تم صرفه من الدفعة الأصلية.");
            }
            return normalized;
        }

        private static bool IsValidQuantity(decimal quantity)
        {
            return quantity > 0m && quantity == decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        }
    }

    // بعد طلب FullCommercial تصبح الدفعات المصدر الرسمي للمخزون؛ لا تسمح هذه
    // القاعدة بتعديل الرصيد المجمع وحده لأن ذلك يفك تسوية الدفعات ويكسر التتبع.
    public static class CommercialInventoryOperationPolicy
    {
        public static void EnsureLegacyAggregateOperationAllowed(bool fullCommercialRequested)
        {
            if (fullCommercialRequested)
            {
                throw new InvalidOperationException("وضع البيع التجاري مطلوب؛ لا يمكن إجراء تسوية أو توريد أو صرف يدوي على الرصيد المجمع. استخدم عملية دفعات موثقة حتى لا تنفصل كمية الصنف عن التشغيلة.");
            }
        }
    }

    // FEFO لا يستنتج دفعات من الرصيد المجمع: يستقبل فقط دفعات صريحة ومسجلة.
    internal static class FefoAllocator
    {
        public static IReadOnlyList<LotAllocation> Allocate(string medicineName, decimal requiredQuantity, IEnumerable<InventoryLotSnapshot> lots, DateTime businessDate)
        {
            if (string.IsNullOrWhiteSpace(medicineName)) throw new ArgumentException("اسم الدواء مطلوب.", nameof(medicineName));
            if (requiredQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
            if (lots == null) throw new ArgumentNullException(nameof(lots));

            decimal remaining = requiredQuantity;
            List<LotAllocation> allocations = new List<LotAllocation>();
            List<InventoryLotSnapshot> candidates = lots.ToList();
            if (candidates.Any(x => x == null)) throw new InvalidOperationException("توجد دفعة غير صالحة.");
            if (candidates.Where(x => x.LotId != Guid.Empty).GroupBy(x => x.LotId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("تكرار معرف الدفعة يمنع التخصيص الآمن.");

            foreach (InventoryLotSnapshot lot in candidates
                .Where(x => string.Equals(x.MedicineName, medicineName, StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.IsRecalled && x.ExpiryDate.Date >= businessDate.Date && x.RemainingQuantity > 0m)
                .OrderBy(x => x.ExpiryDate.Date)
                .ThenBy(x => x.ReceivedAt)
                .ThenBy(x => x.LotId))
            {
                decimal quantity = Math.Min(remaining, lot.RemainingQuantity);
                allocations.Add(new LotAllocation
                {
                    LotId = lot.LotId,
                    BatchNumber = lot.BatchNumber,
                    Quantity = quantity,
                    UnitCost = lot.UnitCost,
                    CostTotal = lot.RemainingCost.HasValue ? (decimal?)MedicineUnitCostCalculator.AllocateRemaining(lot.RemainingCost.Value, checked((int)lot.RemainingQuantity), checked((int)quantity)) : null
                });
                remaining -= quantity;
                if (remaining == 0m) break;
            }

            if (remaining != 0m)
            {
                throw new InvalidOperationException("لا توجد كمية صالحة وغير مستدعاة كافية في الدفعات المسجلة.");
            }

            return allocations;
        }
    }

    internal sealed class Gs1DataMatrixData
    {
        public string Gtin { get; set; }
        public string BatchNumber { get; set; }
        public string SerialNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    // قارئ بيانات GS1 محافظ: يقبل صيغة الأقواس الشائعة ويعيد خطأ واضحاً بدلاً من تخمين بيانات دوائية.
    internal static class Gs1DataMatrixParser
    {
        public static Gs1DataMatrixData ParseHumanReadable(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("رمز GS1 مطلوب.", nameof(input));
            string value = input.Trim();
            if (!value.StartsWith("(", StringComparison.Ordinal))
            {
                return ParseRaw(value);
            }
            Gs1DataMatrixData result = new Gs1DataMatrixData();
            int position = 0;
            while (position < value.Length)
            {
                if (value[position] != '(')
                {
                    throw new FormatException("صيغة GS1 غير مدعومة؛ استخدم معرفات التطبيق بين أقواس مثل (01).");
                }
                int closing = value.IndexOf(')', position + 1);
                if (closing < 0) throw new FormatException("معرف تطبيق GS1 غير مكتمل.");
                string ai = value.Substring(position + 1, closing - position - 1);
                int nextAi = value.IndexOf('(', closing + 1);
                string data = value.Substring(closing + 1, (nextAi < 0 ? value.Length : nextAi) - closing - 1);
                if (string.IsNullOrWhiteSpace(data)) throw new FormatException("قيمة معرف GS1 " + ai + " فارغة.");

                if ((ai == "01" && !string.IsNullOrWhiteSpace(result.Gtin)) || (ai == "10" && !string.IsNullOrWhiteSpace(result.BatchNumber)) ||
                    (ai == "21" && !string.IsNullOrWhiteSpace(result.SerialNumber)) || (ai == "17" && result.ExpiryDate.HasValue))
                    throw new FormatException("تكرار معرف التطبيق GS1 " + ai + " غير مسموح.");
                if (ai == "01") result.Gtin = ValidateGtin(data);
                else if (ai == "10") result.BatchNumber = data;
                else if (ai == "21") result.SerialNumber = data;
                else if (ai == "17") result.ExpiryDate = ParseGs1Expiry(data);
                position = nextAi < 0 ? value.Length : nextAi;
            }

            if (string.IsNullOrWhiteSpace(result.Gtin)) throw new FormatException("رمز GS1 يجب أن يحتوي على GTIN (01).");
            return result;
        }

        private static Gs1DataMatrixData ParseRaw(string value)
        {
            const char groupSeparator = (char)29;
            int position = 0;
            Gs1DataMatrixData result = new Gs1DataMatrixData();
            while (position < value.Length)
            {
                if (value[position] == groupSeparator) { position++; continue; }
                if (position + 2 > value.Length) throw new FormatException("صيغة GS1 الخام غير مكتملة.");
                string ai = value.Substring(position, 2);
                position += 2;
                if (ai == "01")
                {
                    if (position + 14 > value.Length) throw new FormatException("GTIN (01) غير مكتمل.");
                    result.Gtin = ValidateGtin(value.Substring(position, 14)); position += 14;
                }
                else if (ai == "17")
                {
                    if (position + 6 > value.Length) throw new FormatException("تاريخ الانتهاء (17) غير مكتمل.");
                    result.ExpiryDate = ParseGs1Expiry(value.Substring(position, 6)); position += 6;
                }
                else if (ai == "10" || ai == "21")
                {
                    int end = value.IndexOf(groupSeparator, position);
                    if (end < 0) end = value.Length;
                    string variableData = value.Substring(position, end - position);
                    if (string.IsNullOrWhiteSpace(variableData)) throw new FormatException("قيمة GS1 " + ai + " فارغة.");
                    if (ai == "10") result.BatchNumber = variableData; else result.SerialNumber = variableData;
                    position = end;
                }
                else
                {
                    throw new FormatException("معرف التطبيق GS1 " + ai + " غير مدعوم في قارئ الدواء.");
                }
            }
            if (string.IsNullOrWhiteSpace(result.Gtin)) throw new FormatException("رمز GS1 يجب أن يحتوي على GTIN (01).");
            return result;
        }

        private static string ValidateGtin(string value)
        {
            if (value == null || value.Length != 14 || value.Any(x => x < '0' || x > '9')) throw new FormatException("GTIN يجب أن يحتوي على 14 رقماً.");
            int sum = 0;
            for (int index = 0; index < 13; index++) sum += (value[12 - index] - '0') * (index % 2 == 0 ? 3 : 1);
            if ((10 - sum % 10) % 10 != value[13] - '0') throw new FormatException("رقم تحقق GTIN غير صالح.");
            return value;
        }

        private static DateTime ParseGs1Expiry(string yyMMdd)
        {
            if (yyMMdd == null || yyMMdd.Length != 6 ||
                !int.TryParse(yyMMdd.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int year) ||
                !int.TryParse(yyMMdd.Substring(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int month) ||
                !int.TryParse(yyMMdd.Substring(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int day) ||
                month < 1 || month > 12 || day < 0 || day > 31)
            {
                throw new FormatException("تاريخ انتهاء GS1 (17) غير صالح.");
            }
            int fullYear = 2000 + year;
            // معيار GS1 يستخدم آخر يوم من الشهر عندما يكون يوم الانتهاء 00.
            if (day == 0)
            {
                return new DateTime(fullYear, month, DateTime.DaysInMonth(fullYear, month));
            }
            try { return new DateTime(fullYear, month, day); }
            catch (ArgumentOutOfRangeException) { throw new FormatException("تاريخ انتهاء GS1 (17) غير صالح."); }
        }
    }
}
