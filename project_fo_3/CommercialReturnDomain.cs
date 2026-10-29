using System;
using PharmacyManagement.Domain;
using System.Collections.Generic;
using System.Linq;

namespace project_fo_3
{
    internal enum CommercialReturnDisposition { Restock, Quarantine, Damaged }

    // لقطة بند البيع المعتمد؛ لا تعيد هذه الطبقة قراءة سعر الصنف أو الضريبة الحالية.
    internal sealed class CommercialReturnSourceLine
    {
        public Guid OriginalLineId { get; set; }
        public string MedicineName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public MedicineUnitSelection Unit { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal UnitCost { get; set; }
        public decimal CostTotal { get; set; }
        public CommercialPriceTaxMode PriceTaxMode { get; set; }

        public CommercialReturnSourceLine()
        {
            MedicineName = string.Empty;
            PriceTaxMode = CommercialPriceTaxMode.Exclusive;
        }
    }

    // يسجل القيم المطلقة التي اعتمدتها مرتجعات سابقة للسطر نفسه.
    internal sealed class CommercialReturnPostedLine
    {
        public int? BaseQuantity { get; set; }
        public decimal Quantity { get; set; }
        public decimal GrossTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal CostTotal { get; set; }
    }

    internal sealed class CommercialReturnCalculatedLine
    {
        public Guid OriginalLineId { get; private set; }
        public string MedicineName { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public MedicineUnitSelection Unit { get; private set; }
        public MedicineUnitSelection OriginalUnit { get; private set; }
        public bool UsesBaseQuantity { get; private set; }
        public int BaseQuantity { get; private set; }
        public int PriceBaseQuantity { get { return UsesBaseQuantity ? OriginalUnit.BaseUnitsPerUnit : 1; } }
        public decimal GrossTotal { get; private set; }
        public decimal DiscountTotal { get; private set; }
        public decimal TaxRate { get; private set; }
        public decimal TaxTotal { get; private set; }
        public decimal NetTotal { get; private set; }
        public decimal UnitCost { get; private set; }
        public decimal CostTotal { get; private set; }
        public CommercialPriceTaxMode PriceTaxMode { get; private set; }

        internal CommercialReturnCalculatedLine(CommercialReturnSourceLine source, decimal quantity, decimal grossTotal,
            decimal discountTotal, decimal taxTotal, decimal netTotal, decimal costTotal, bool usesBaseQuantity = false)
        {
            OriginalLineId = source.OriginalLineId;
            MedicineName = source.MedicineName;
            Quantity = quantity;
            UnitPrice = source.UnitPrice;
            UsesBaseQuantity=usesBaseQuantity;OriginalUnit=source.Unit;
            Unit = usesBaseQuantity || source.Unit == null ? null : source.Unit.WithQuantity(quantity);
            BaseQuantity=usesBaseQuantity ? checked((int)quantity) : Unit == null ? checked((int)quantity) : Unit.BaseQuantity;
            GrossTotal = grossTotal;
            DiscountTotal = discountTotal;
            TaxRate = source.TaxRate;
            TaxTotal = taxTotal;
            NetTotal = netTotal;
            UnitCost = source.UnitCost;
            CostTotal = costTotal;
            PriceTaxMode = source.PriceTaxMode;
        }

        internal CommercialInvoiceLine ToCreditNoteLine()
        {
            if(UsesBaseQuantity) throw new InvalidOperationException("إرسال هذا المرتجع إلكترونياً موقوف حتى يدعم المزود أساس سعر العبوة؛ المستند المحلي محفوظ.");
            return new CommercialInvoiceLine
            {
                MedicineName = MedicineName,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                Unit = Unit,
                // تعطيل خصم المستند في الإشعار؛ هذا الحقل يحمل كل خصم السطر الأصلي
                // وحصته الثابتة من خصم الفاتورة، حتى لا يعاد توزيعها من جديد.
                LineDiscount = DiscountTotal,
                TaxRatePercent = TaxRate,
                PriceTaxMode = PriceTaxMode
            };
        }
    }

    internal sealed class CommercialReturnBaseSnapshot
    {
        internal Guid OriginalLineId { get; private set; }
        internal string MedicineName { get; private set; }
        internal MedicineUnitSelection OriginalUnit { get; private set; }
        internal int BaseQuantity { get; private set; }
        internal string UnitName { get { return "وحدة أساسية من " + OriginalUnit.Name; } }
        internal int PriceBaseQuantity { get { return OriginalUnit.BaseUnitsPerUnit; } }
        internal CommercialReturnBaseSnapshot(Guid sourceLine,string medicine,MedicineUnitSelection source,int quantity)
        {
            if(sourceLine==Guid.Empty || string.IsNullOrWhiteSpace(medicine) || source==null || quantity<=0 || quantity>source.BaseQuantity)throw new InvalidOperationException("لقطة المرتجع الأساسي غير صالحة.");
            OriginalLineId=sourceLine;MedicineName=medicine;OriginalUnit=source;BaseQuantity=quantity;
        }
    }

    internal static class CommercialReturnCalculator
    {
        internal static CommercialReturnCalculatedLine CalculateLine(
            CommercialReturnSourceLine source,
            IEnumerable<CommercialReturnPostedLine> priorReturns,
            decimal requestedQuantity)
        {
            return CalculateLine(source, priorReturns, requestedQuantity, null);
        }

        // تكلفة المرتجع تأتي من مخصصات الدفعات الأصلية التي اختارها المتصل؛
        // لا يصح افتراض متوسط تكلفة عندما تكون الفاتورة صُرفت من أكثر من دفعة.
        internal static CommercialReturnCalculatedLine CalculateLine(
            CommercialReturnSourceLine source,
            IEnumerable<CommercialReturnPostedLine> priorReturns,
            decimal requestedQuantity,
            decimal? returnedLotCost)
        {
            ValidateSource(source);
            if (priorReturns == null) throw new ArgumentNullException(nameof(priorReturns));
            if (!IsValidQuantity(source, requestedQuantity))
                throw new InvalidOperationException("مسار البيع التجاري الحالي يدعم إرجاع عبوات كاملة فقط.");

            List<CommercialReturnPostedLine> previous = priorReturns.ToList();
            if (previous.Any(x => x == null || !IsValidQuantity(source, x.Quantity) || x.GrossTotal < 0m ||
                                  x.DiscountTotal < 0m || x.TaxTotal < 0m || x.NetTotal < 0m || x.CostTotal < 0m))
            {
                throw new InvalidOperationException("سجل المرتجعات السابق للسطر غير صالح.");
            }

            decimal sourceGross = Gross(source);
            decimal returnedQuantity=0m;
            foreach(var prior in previous)
            {
                if(prior.BaseQuantity.HasValue)throw new InvalidOperationException("يوجد مرتجع سابق بالعدد الأساسي؛ استخدم العدد الأساسي لبقية مرتجعات هذا السطر.");
                returnedQuantity+=prior.Quantity;
            }
            decimal remainingQuantity = source.Quantity - returnedQuantity;
            if (remainingQuantity <= 0m || requestedQuantity > remainingQuantity)
                throw new InvalidOperationException("كمية المرتجع تتجاوز الكمية المتبقية من سطر البيع الأصلي.");

            decimal previousGross = previous.Sum(x => x.GrossTotal);
            decimal previousDiscount = previous.Sum(x => x.DiscountTotal);
            decimal previousTax = previous.Sum(x => x.TaxTotal);
            decimal previousNet = previous.Sum(x => x.NetTotal);
            decimal previousCost = previous.Sum(x => x.CostTotal);
            if (previousGross > sourceGross || previousDiscount > source.DiscountTotal || previousTax > source.TaxTotal ||
                previousNet > source.NetTotal || previousCost > source.CostTotal)
            {
                throw new InvalidOperationException("القيم التراكمية للمرتجعات تتجاوز لقطة سطر البيع الأصلي.");
            }

            bool finalReturn = requestedQuantity == remainingQuantity;
            decimal gross = finalReturn ? sourceGross - previousGross : ProRate(sourceGross, requestedQuantity, source.Quantity);
            decimal discount = finalReturn ? source.DiscountTotal - previousDiscount : ProRate(source.DiscountTotal, requestedQuantity, source.Quantity);
            decimal tax = finalReturn ? source.TaxTotal - previousTax : ProRate(source.TaxTotal, requestedQuantity, source.Quantity);
            decimal net = finalReturn ? source.NetTotal - previousNet : ProRate(source.NetTotal, requestedQuantity, source.Quantity);
            decimal cost = returnedLotCost.HasValue
                ? InvoiceTotalsCalculator.Round(returnedLotCost.Value)
                : (finalReturn ? source.CostTotal - previousCost : ProRate(source.CostTotal, requestedQuantity, source.Quantity));
            if (cost < 0m || cost > source.CostTotal - previousCost || (finalReturn && cost != source.CostTotal - previousCost))
                throw new InvalidOperationException("تكلفة دفعات المرتجع لا تطابق الجزء المتبقي من سطر البيع الأصلي.");

            // في الجزء الأخير تكون القيم المتبقية من اللقطة الأصلية هي المرجع.
            // لا يعاد رفضها إذا حوّل تقريب الأجزاء السابقة ضريبة السطر المستقل.
            ValidateReturnEquation(source, requestedQuantity, gross, discount, tax, net, cost, finalReturn);
            return new CommercialReturnCalculatedLine(source, requestedQuantity, gross, discount, tax, net, cost);
        }

        internal static CommercialReturnCalculatedLine CalculateBaseLine(CommercialReturnSourceLine source,
            IEnumerable<CommercialReturnPostedLine> priorReturns,int requestedBaseQuantity,decimal returnedLotCost)
        {
            ValidateSource(source);
            if(source.Unit==null || requestedBaseQuantity<=0 || priorReturns==null)throw new InvalidOperationException("المرتجع الأساسي يتطلب لقطة وحدة أصلية وعدداً صحيحاً موجباً.");
            var previous=priorReturns.ToList();
            if(previous.Any(x=>x==null || x.GrossTotal<0m || x.DiscountTotal<0m || x.TaxTotal<0m || x.NetTotal<0m || x.CostTotal<0m))throw new InvalidOperationException("سجل المرتجع غير صالح.");
            int returnedBase=0;
            foreach(var item in previous)
            {
                int quantity=item.BaseQuantity ?? MedicineUnitCalculator.ToBaseQuantity(item.Quantity,source.Unit.BaseUnitsPerUnit);
                if(quantity<=0)throw new InvalidOperationException("كمية مرتجع سابقة غير صالحة.");
                returnedBase=checked(returnedBase+quantity);
            }
            int remaining=checked(source.Unit.BaseQuantity-returnedBase);
            if(remaining<=0 || requestedBaseQuantity>remaining)throw new InvalidOperationException("العدد الأساسي يتجاوز المتبقي من البيع الأصلي.");
            decimal grossRemaining=Gross(source)-previous.Sum(x=>x.GrossTotal),discountRemaining=source.DiscountTotal-previous.Sum(x=>x.DiscountTotal),
                taxRemaining=source.TaxTotal-previous.Sum(x=>x.TaxTotal),netRemaining=source.NetTotal-previous.Sum(x=>x.NetTotal),costRemaining=source.CostTotal-previous.Sum(x=>x.CostTotal);
            if(grossRemaining<0m || discountRemaining<0m || taxRemaining<0m || netRemaining<0m || costRemaining<0m || discountRemaining>grossRemaining)throw new InvalidOperationException("مبالغ المرتجع السابق تتجاوز أصل الفاتورة.");
            bool final=requestedBaseQuantity==remaining;
            decimal gross=MedicineUnitCostCalculator.AllocateRemaining(grossRemaining,remaining,requestedBaseQuantity);
            decimal discount=MedicineUnitCostCalculator.AllocateRemaining(discountRemaining,remaining,requestedBaseQuantity);
            decimal tax=MedicineUnitCostCalculator.AllocateRemaining(taxRemaining,remaining,requestedBaseQuantity);
            decimal net=source.PriceTaxMode==CommercialPriceTaxMode.Inclusive ? InvoiceTotalsCalculator.Round(gross-discount) : InvoiceTotalsCalculator.Round(gross-discount+tax);
            if(final){gross=grossRemaining;discount=discountRemaining;tax=taxRemaining;net=netRemaining;}
            if(discount>gross || tax<0m || net<0m || net>netRemaining || returnedLotCost<0m || returnedLotCost>costRemaining || (final && returnedLotCost!=costRemaining) ||
                (source.PriceTaxMode==CommercialPriceTaxMode.Inclusive ? gross-discount!=net || tax>net : gross-discount+tax!=net))
                throw new InvalidOperationException("توزيع مبالغ المرتجع الأساسي لا يحفظ معادلة وأصل الفاتورة.");
            return new CommercialReturnCalculatedLine(source,requestedBaseQuantity,gross,discount,tax,net,returnedLotCost,true);
        }

        internal static InvoiceTotals CalculateTotals(IEnumerable<CommercialReturnCalculatedLine> lines)
        {
            List<CommercialReturnCalculatedLine> materialized = lines == null ? null : lines.ToList();
            if (materialized == null || materialized.Count == 0 || materialized.Any(x => x == null))
                throw new InvalidOperationException("لا يمكن اعتماد إشعار دائن بلا بنود مرتجع.");
            CommercialPriceTaxMode mode = materialized[0].PriceTaxMode;
            if (materialized.Any(x => x.PriceTaxMode != mode))
                throw new InvalidOperationException("لا يمكن خلط سياسات الضريبة في إشعار دائن واحد.");

            InvoiceTotals totals = new InvoiceTotals
            {
                PriceTaxMode = mode,
                GrossTotal = InvoiceTotalsCalculator.Round(materialized.Sum(x => x.GrossTotal)),
                DiscountTotal = InvoiceTotalsCalculator.Round(materialized.Sum(x => x.DiscountTotal)),
                TaxTotal = InvoiceTotalsCalculator.Round(materialized.Sum(x => x.TaxTotal)),
                NetTotal = InvoiceTotalsCalculator.Round(materialized.Sum(x => x.NetTotal))
            };
            totals.TaxableTotal = InvoiceTotalsCalculator.Round(totals.NetTotal - totals.TaxTotal);
            InvoiceTotalsContract.Validate(totals);
            return totals;
        }

        private static void ValidateSource(CommercialReturnSourceLine source)
        {
            if (source == null || source.OriginalLineId == Guid.Empty || string.IsNullOrWhiteSpace(source.MedicineName) ||
                !IsValidQuantity(source, source.Quantity) || (source.Unit != null && (source.Unit.EnteredQuantity != source.Quantity || source.Unit.UnitPrice != source.UnitPrice)) || source.UnitPrice < 0m || source.DiscountTotal < 0m ||
                source.TaxRate < 0m || source.TaxRate > 100m || source.TaxTotal < 0m || source.NetTotal < 0m ||
                source.CostTotal < 0m)
            {
                throw new InvalidOperationException("لقطة سطر البيع التجاري غير صالحة للمرتجع.");
            }
            decimal gross = Gross(source);
            if (source.DiscountTotal > gross)
                throw new InvalidOperationException("خصم سطر البيع الأصلي يتجاوز قيمته.");
            ValidateReturnEquation(source, source.Quantity, gross, source.DiscountTotal, source.TaxTotal, source.NetTotal, source.CostTotal);
        }

        private static void ValidateReturnEquation(CommercialReturnSourceLine source, decimal quantity, decimal gross,
            decimal discount, decimal tax, decimal net, decimal cost, bool allowSnapshotRemainder = false)
        {
            if (gross < 0m || discount < 0m || tax < 0m || net < 0m || cost < 0m || discount > gross)
                throw new InvalidOperationException("قيم المرتجع السطرية غير صالحة.");
            decimal taxable = InvoiceTotalsCalculator.Round(gross - discount);
            decimal expectedTax = source.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                ? InvoiceTotalsCalculator.Round(taxable * source.TaxRate / (100m + source.TaxRate))
                : InvoiceTotalsCalculator.Round(taxable * source.TaxRate / 100m);
            decimal expectedNet = source.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                ? taxable
                : InvoiceTotalsCalculator.Round(taxable + expectedTax);
            if ((!allowSnapshotRemainder && (tax != expectedTax || net != expectedNet)) ||
                (allowSnapshotRemainder && (source.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                    ? InvoiceTotalsCalculator.Round(gross - discount) != net
                    : InvoiceTotalsCalculator.Round(gross - discount) != InvoiceTotalsCalculator.Round(net - tax))))
                throw new InvalidOperationException("تقسيم المرتجع لا يطابق ضريبة وقيمة سطر الفاتورة الأصلية؛ اعتمد كمية مختلفة أو أعد كامل المتبقي في إشعار واحد.");
            if (!IsValidQuantity(source, quantity))
                throw new InvalidOperationException("كمية المرتجع غير صالحة.");
        }

        private static decimal Gross(CommercialReturnSourceLine source)
        {
            return InvoiceTotalsCalculator.Round(source.Quantity * source.UnitPrice);
        }

        private static decimal ProRate(decimal value, decimal quantity, decimal totalQuantity)
        {
            return InvoiceTotalsCalculator.Round(value * quantity / totalQuantity);
        }

        private static bool IsValidQuantity(CommercialReturnSourceLine source, decimal value)
        {
            if (source.Unit == null) return IsWholeUnitQuantity(value);
            try { return source.Unit.WithQuantity(value).BaseQuantity > 0; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (OverflowException) { return false; }
        }

        private static bool IsWholeUnitQuantity(decimal value)
        {
            return value > 0m && value == decimal.Truncate(value);
        }
    }

    // يظل التالف تكلفة محققة ولا يعود إلى أصل المخزون؛ أما الحجر فهو أصل
    // موجود لكن خارج الرصيد القابل للبيع حتى يقرر المسؤول مصيره.
    internal static class CommercialReturnDispositionPolicy
    {
        internal static bool RestoresInventoryValue(CommercialReturnDisposition disposition)
        {
            return disposition == CommercialReturnDisposition.Restock ||
                disposition == CommercialReturnDisposition.Quarantine;
        }
    }

    internal static class CommercialReturnCreditPolicy
    {
        internal static void EnsureCustomerIsIdentified(string refundMethod, string customerId)
        {
            if (string.Equals((refundMethod ?? string.Empty).Trim(), "Credit", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(customerId))
                throw new InvalidOperationException("استرداد المرتجع كرصيد عميل يتطلب فاتورة أصلية مرتبطة بعميل محدد.");
        }
    }
}
