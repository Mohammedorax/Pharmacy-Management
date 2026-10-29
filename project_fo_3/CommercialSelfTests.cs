using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace project_fo_3
{
    // اختبارات حتمية بلا قاعدة بيانات؛ تشغّل عند: project_fo_3.exe --verify-commercial-domain
    internal static class CommercialSelfTests
    {
        public static void RunAll()
        {
            BaseUnitReturnConservesOriginalAmounts();
            VerifyCountryTaxPolicy();
            VerifyMedicineBarcodeSelection();
            PopularMedicineRankingIncludesCommercialReturns();
            VerifyReportCsvValues();
            InvoiceCalculationRegressionTests.RunAll();
            InvoiceCalculationKeepsExactTaxAndDiscount();
            InvoiceDiscountRemainderKeepsTaxConsistent();
            InclusiveTaxKeepsScreenAndInvoiceTotalsConsistent();
            LegacyIncludedTaxUsesSharedRounding();
            InvoiceTotalsContractRejectsTamperedInclusiveTotals();
            FefoUsesClosestValidLotAndSkipsExpiredOrRecalled();
            Gs1ParserReadsTraceabilityFields();
            RawGs1ParserReadsTraceabilityFields();
            SandboxEInvoiceProducesHashAndTlv();
            SaudiSandboxRejectsForeignCurrency();
            SaudiFeatureProductionActivationIsBlocked();
            SaudiFeatureBatchValidatesBeforeSql();
            PasswordHashUsesSha256AndUpgradesLegacyHash();
            DynamicDatabaseSettingsKeepEncryptionEnabled();
            RuntimeProfileSeparatesTestDatabases();
            SystemDatabasesAreRejectedForPharmacySettings();
            CommercialTestDatabaseNameUsesStrictSuffix();
            CommercialSchemaSqlUsesExecutableFeatureLiterals();
            EInvoiceChainCannotRewind();
            InsuranceRetryBackoffIsBounded();
            PrescriptionValidationRejectsOverDispense();
            PrescriptionValidationRejectsDuplicateDrugCodes();
            MoneyWithMoreThanTwoDecimalsIsRejected();
            QuantityWithMoreThanThreeDecimalsIsRejected();
            PartialReturnCannotExceedOriginalLotAllocation();
            PartialReturnRejectsInvalidQuantityPrecisionAndNormalizesDuplicates();
            CommercialReturnKeepsOriginalLineValuesAcrossPartialAndFinalCredits();
            CommercialReturnFinalRoundingRemainderProducesExactCreditArtifact();
            CommercialReturnRejectsOverReturnAndFractionalQuantity();
            CommercialReturnUsesActualSoldLotCost();
            CreditNoteRequiresOriginalInvoiceAndPreservesItsLink();
            PurchaseSettlementFollowsExplicitPaymentMethod();
            CommercialReturnPostsToTheSelectedSettlementAccount();
            CommercialReturnFreeItemRestoresCostWithoutRefund();
            CommercialReturnJournalBalancesRefundAndCost();
            CommercialSalesReturnAuthorizationBindsTheSessionUser();
            CommercialReturnDamagedDispositionDoesNotRestoreInventoryValue();
            CommercialReturnCreditRequiresIdentifiedCustomer();
            CommercialReturnRequestValidationRejectsOversizedAuditFields();
            CommercialReturnSubmissionGateRejectsOverlappingSubmissions();
            CommercialReturnPrintDataPreservesCreditNoteReference();
            CommercialReturnPresentationKeepsStorageCodesStable();
            ActiveIngredientEditorValidationRejectsIncompleteDefinitions();
            CommercialReturnQuantityInputRejectsInvalidRows();
            FullCommercialBlocksLegacyAggregateInventoryOperations();
            PrintSnapshotDefensivelyCopiesTotals();
        }

        private static void BaseUnitReturnConservesOriginalAmounts()
        {
            foreach(var mode in new[]{CommercialPriceTaxMode.Exclusive,CommercialPriceTaxMode.Inclusive})
            {
                decimal gross=mode==CommercialPriceTaxMode.Exclusive ? 100m :115m;
                decimal discount=mode==CommercialPriceTaxMode.Exclusive ? 10m :11.50m;
                var original=new PharmacyManagement.Domain.MedicineUnitSelection(Guid.NewGuid(),1,"عبوة ثلاثية",3,1m,gross,gross,"SAR");
                var source=new CommercialReturnSourceLine{OriginalLineId=Guid.NewGuid(),MedicineName="دواء ثلاثي",Unit=original,Quantity=1m,UnitPrice=gross,DiscountTotal=discount,TaxRate=15m,TaxTotal=13.50m,NetTotal=103.50m,CostTotal=100m,PriceTaxMode=mode};
                var prior=new List<CommercialReturnPostedLine>();
                decimal[] costs={33.33m,33.34m,33.33m};
                decimal sumGross=0m,sumDiscount=0m,sumTax=0m,sumNet=0m,sumCost=0m;
                for(int i=0;i<3;i++)
                {
                    var part=CommercialReturnCalculator.CalculateBaseLine(source,prior,1,costs[i]);
                    Require(part.UsesBaseQuantity && part.Quantity==1m && part.BaseQuantity==1 && part.PriceBaseQuantity==3 && part.Unit==null,"يجب تمثيل حبة واحدة دون تقريب ثلث العبوة.");
                    bool electronicRejected=false;try{part.ToCreditNoteLine();}catch(InvalidOperationException){electronicRejected=true;}
                    Require(electronicRejected,"يجب منع مستند إلكتروني لا يمثل أساس السعر الصحيح.");
                    prior.Add(new CommercialReturnPostedLine{BaseQuantity=1,Quantity=1m,GrossTotal=part.GrossTotal,DiscountTotal=part.DiscountTotal,TaxTotal=part.TaxTotal,NetTotal=part.NetTotal,CostTotal=part.CostTotal});
                    sumGross+=part.GrossTotal;sumDiscount+=part.DiscountTotal;sumTax+=part.TaxTotal;sumNet+=part.NetTotal;sumCost+=part.CostTotal;
                }
                Require(sumGross==gross && sumDiscount==discount && sumTax==13.50m && sumNet==103.50m && sumCost==100m,"يجب حفظ كل مبلغ أصلي وتكلفة حتى آخر حبة.");
                bool over=false;try{CommercialReturnCalculator.CalculateBaseLine(source,prior,1,0m);}catch(InvalidOperationException){over=true;}
                Require(over,"يجب رفض المرتجع بعد استنفاد الأصل.");
                Require(original.BaseUnitsPerUnit==3 && original.EnteredQuantity==1m && original.UnitPrice==gross,"لا يغير المرتجع لقطة الفاتورة الأصلية.");
                var oldPrior=new[]{new CommercialReturnPostedLine{Quantity=1m,GrossTotal=gross,DiscountTotal=discount,TaxTotal=13.50m,NetTotal=103.50m,CostTotal=100m}};
                bool oldOver=false;try{CommercialReturnCalculator.CalculateBaseLine(source,oldPrior,1,0m);}catch(InvalidOperationException){oldOver=true;}
                Require(oldOver,"يجب احتساب المرتجع القديم بالعبوة ضمن حدود العدد الأساسي.");
            }
        }

        private static void CommercialReturnSubmissionGateRejectsOverlappingSubmissions()
        {
            var gate = new CommercialReturnSubmissionGate();
            Require(gate.TryEnter(), "يجب أن يقبل حاجز اعتماد المرتجع الإرسال الأول.");
            Require(!gate.TryEnter(), "يجب أن يمنع حاجز اعتماد المرتجع الإرسال المتداخل.");
            gate.Exit();
            Require(gate.TryEnter(), "يجب أن يسمح حاجز اعتماد المرتجع بإعادة المحاولة بعد الخروج.");
            gate.Exit();
            int winners = 0;
            System.Threading.Tasks.Parallel.For(0, 16, i =>
            {
                if (gate.TryEnter()) System.Threading.Interlocked.Increment(ref winners);
            });
            Require(winners == 1, "يجب أن يفوز إرسال واحد فقط عند محاولة الاعتماد المتزامن.");
            gate.Exit();
        }

        private static void CommercialReturnPrintDataPreservesCreditNoteReference()
        {
            var header = new CommercialReturnPrintHeader
            {
                ReturnInvoiceNumber = 4102, OriginalInvoiceNumber = 4019,
                IssuedAtUtc = new DateTime(2026, 9, 16, 12, 30, 0, DateTimeKind.Utc),
                RefundMethod = "Card", ReturnReason = "سبب موثق", IssuedBy = "مدير اختبار", CurrencyCode = "SAR"
            };
            using (var table = CommercialReturnPrintData.BuildTable(header, new[]
            {
                new CommercialReturnPrintLine { MedicineName = "صنف اختبار", Quantity = 1.250m, GrossTotal = 100m, DiscountTotal = 5m, TaxRate = 15m, TaxTotal = 14.25m, NetTotal = 109.25m }
            }))
            {
                Require(table.Rows.Count == 1 && (long)table.Rows[0]["رقم الإشعار"] == 4102 &&
                    (long)table.Rows[0]["رقم الفاتورة الأصلية"] == 4019 && (decimal)table.Rows[0]["الكمية"] == 1.250m &&
                    (decimal)table.Rows[0]["الضريبة"] == 14.25m && (decimal)table.Rows[0]["الصافي"] == 109.25m,
                    "بيانات طباعة الإشعار الدائن لم تحفظ المرجع أو القيم المالية المعتمدة.");
            }
            Require(CommercialReturnPrintData.BuildSubtitle(header).Contains("4102") && CommercialReturnPrintData.BuildSubtitle(header).Contains("4019"),
                "عنوان طباعة الإشعار الدائن لا يربط الفاتورة الأصلية بالإشعار.");
            var lines = new[] { new CommercialReturnPrintLine { MedicineName="باراسيتامول 500 mg",Quantity=1.250m,
                GrossTotal=100m,DiscountTotal=5m,TaxRate=15m,TaxTotal=14.25m,NetTotal=109.25m,Unit="شريط",UnitPrice=80m,PriceBaseQuantity=1 } };
            using(var arabic=CommercialReturnPrintData.BuildTable(header,lines,false))
            using(var english=CommercialReturnPrintData.BuildTable(header,lines,true))
            {
                Require(english.Columns[0].ColumnName=="Credit note number" && english.Columns[1].ColumnName=="Original invoice number" &&
                    english.Columns[2].ColumnName=="Approved at (UTC)" && english.Columns[10].ColumnName=="Refund method",
                    "عناوين طباعة الإشعار الإنجليزي لا تطابق لغة العرض.");
                for(int index=0;index<english.Columns.Count;index++)
                {
                    foreach(char character in english.Columns[index].ColumnName)
                        Require(character<'\u0600' || character>'\u06FF',"عنوان عمود الطباعة الإنجليزي بقي عربيًا.");
                    Require(english.Columns[index].DataType==arabic.Columns[index].DataType &&
                        (index==10 || Equals(english.Rows[0][index],arabic.Rows[0][index])),
                        "ترجمة الطباعة غيرت قيمة مستند معتمد أو نوع عمود.");
                }
                Require((string)english.Rows[0][10]=="Card" && header.RefundMethod=="Card" &&
                    (string)english.Rows[0][3]=="باراسيتامول 500 mg" && (string)english.Rows[0][11]=="سبب موثق" &&
                    arabic.Columns[0].ColumnName=="رقم الإشعار",
                    "ترجمة الطباعة غيرت النص الحر أو رموز المصدر أو النسخة العربية.");
                Require(CommercialReturnPrintData.BuildSubtitle(header,true)=="Credit note 4102 | Original invoice 4019 | SAR" &&
                    CommercialReturnPrintData.BuildSubtitle(header,false)==CommercialReturnPrintData.BuildSubtitle(header),
                    "ترجمة عنوان الإشعار غيرت مراجع المستند أو السلوك العربي السابق.");
                using(var layout=new DataTablePrintPreview.TablePrintLayout("Credit note",CommercialReturnPrintData.BuildSubtitle(header,true),english,false))
                    Require(!layout.IsRightToLeft,"طباعة إشعار إنجليزي اعتمدت اتجاه التطبيق العربي بدل اتجاه النافذة.");
                foreach(bool leftToRight in new[]{false,true})
                using(var items=CommercialReturnPrintData.BuildItemTable(header,lines,leftToRight))
                {
                    int[] mapped={3,15,14,4,5,6,7,8,9};
                    Require(items.Columns.Count==9 && items.Rows.Count==1,"يجب أن يحتفظ جدول المرتجع بتسعة أعمدة كاملة دون فصل الصافي أفقياً.");
                    for(int index=0;index<mapped.Length;index++)
                    {
                        if(index==1)continue;
                        Require(items.Columns[index].DataType==arabic.Columns[mapped[index]].DataType &&
                            Equals(items.Rows[0][index],arabic.Rows[0][mapped[index]]),"القالب المختصر غير قيمة بند أو نوعها.");
                    }
                    Require(items.Columns[1].DataType==typeof(string) && Convert.ToString(items.Rows[0][1])=="80" && Convert.ToString(items.Rows[0][2])=="شريط","قالب المرتجع يجب أن يعرض السعر القديم دون مقام للوحدة الواحدة.");
                    Require(items.Columns[1].ColumnName==(leftToRight ? "Price" : "السعر"),"لغة عنوان السعر لا تطابق لغة المستند.");
                    var baseLines=new[]{new CommercialReturnPrintLine{MedicineName="دواء ثلاثي",Quantity=1m,Unit="وحدة أساسية من عبوة ثلاثية",UnitPrice=115m,PriceBaseQuantity=3,GrossTotal=38.33m,DiscountTotal=3.83m,TaxRate=15m,TaxTotal=4.50m,NetTotal=34.50m}};
                    using(var baseFull=CommercialReturnPrintData.BuildTable(header,baseLines,leftToRight))
                    using(var baseItems=CommercialReturnPrintData.BuildItemTable(header,baseLines,leftToRight))
                    {
                        Require(baseFull.Columns.Count==17 && Convert.ToDecimal(baseFull.Rows[0][15])==115m && Convert.ToInt32(baseFull.Rows[0][16])==3,"يجب أن تظل بيانات السعر وأساسه المحفوظة مستقلة في الجدول الكامل.");
                        Require(baseItems.Columns.Count==9 && Convert.ToString(baseItems.Rows[0][1])==(leftToRight ? "115 / 3" : "115 لكل 3") && Convert.ToDecimal(baseItems.Rows[0][3])==1m,"طباعة الحبة الواحدة يجب أن تعرض أساس سعر العبوة دون تقريب الثلث.");
                        Require(Convert.ToDecimal(baseItems.Rows[0][4])==38.33m && Convert.ToDecimal(baseItems.Rows[0][5])==3.83m && Convert.ToDecimal(baseItems.Rows[0][6])==15m && Convert.ToDecimal(baseItems.Rows[0][7])==4.50m && Convert.ToDecimal(baseItems.Rows[0][8])==34.50m,"دمج أساس السعر يجب ألا يغير إجمالي أو خصم أو ضريبة أو صافي المرتجع.");
                    }
                    string details=CommercialReturnPrintData.BuildDocumentDetails(header,leftToRight);
                    Require(details.Contains("2026-09-16 12:30:00") && details.Contains(header.ReturnReason) && details.Contains(header.IssuedBy) &&
                        details.Contains(leftToRight?"Refund method: Card":"طريقة الاسترداد: بطاقة"),
                        "قالب الإشعار المختصر فقد تاريخًا أو سببًا أو منفذًا أو طريقة استرداد.");
                }
            }
        }

        private static void CommercialReturnPresentationKeepsStorageCodesStable()
        {
            Require(CommercialReturnPresentation.RefundMethodDisplay("OriginalMethod") == "نقداً (المسار الأصلي الحالي)" &&
                CommercialReturnPresentation.RefundMethodDisplay("Cash") == "نقداً" &&
                CommercialReturnPresentation.RefundMethodDisplay("Card") == "بطاقة" &&
                CommercialReturnPresentation.RefundMethodDisplay("Credit") == "رصيد العميل",
                "تسميات طرق الاسترداد العربية لا تطابق القيم المحاسبية المخزنة.");
            Require(CommercialReturnPresentation.DispositionDisplay("Restock") == "إعادة إلى المخزون" &&
                CommercialReturnPresentation.DispositionDisplay("Quarantine") == "حجر" &&
                CommercialReturnPresentation.DispositionDisplay("Damaged") == "تالف",
                "تسميات حالة الدفعة العربية لا تطابق القيم المخزنة.");
            Require(CommercialReturnPresentation.RefundMethodDisplay("Unknown") == "Unknown",
                "لا يجوز أن تخفي الواجهة قيمة محفوظة غير معروفة عند المراجعة.");
        }

        private static void ActiveIngredientEditorValidationRejectsIncompleteDefinitions()
        {
            Require(ActiveIngredientEditorValidation.RequiredArabicName("  باراسيتامول ") == "باراسيتامول" &&
                ActiveIngredientEditorValidation.OptionalLatinName("Paracetamol") == "Paracetamol" &&
                ActiveIngredientEditorValidation.OptionalStrength("500", "mg") == 500m,
                "تحقق تعريف المادة الفعالة لا يحفظ القيم اليدوية الصحيحة.");
            bool rejected = false;
            try { ActiveIngredientEditorValidation.RequiredArabicName(" "); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل تحقق المادة الفعالة اسماً عربياً فارغاً.");
            rejected = false;
            try { ActiveIngredientEditorValidation.OptionalStrength("500", ""); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل تحقق المادة الفعالة تركيزاً بلا وحدة.");
            foreach (decimal invalid in new[] { 0.0001m, 1.2345m, 1000000000000000m })
            {
                rejected = false;
                try { ActiveIngredientEditorValidation.OptionalStrength(invalid.ToString(System.Globalization.CultureInfo.CurrentCulture), "mg"); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "يجب رفض تركيز لا يُحفظ بدقة في decimal(18,3).");
            }
            foreach (decimal valid in new[] { 0.001m, 1.234m, 999999999999999.999m })
                Require(ActiveIngredientEditorValidation.OptionalStrength(valid.ToString(System.Globalization.CultureInfo.CurrentCulture), "mg") == valid,
                    "يجب حفظ التركيز القابل للتمثيل دون تغيير.");
        }

        private static void CommercialReturnQuantityInputRejectsInvalidRows()
        {
            Require(CommercialReturnPresentation.ParseQuantity("0", 5m) == 0m, "الصفر يستبعد الدفعة.");
            Require(CommercialReturnPresentation.ParseQuantity("5", 5m) == 5m, "يجب قبول كامل المتاح.");
            foreach (string value in new[] { "", "invalid", "-1", "6", 0.5m.ToString(System.Globalization.CultureInfo.CurrentCulture) })
            {
                bool rejected = false;
                try { CommercialReturnPresentation.ParseQuantity(value, 5m); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "يجب رفض المدخل غير الصالح بدلاً من تجاهل سطر المرتجع: " + value);
            }
        }

        private static void VerifyReportCsvValues()
        {
            Require(ReportCsvWriter.Cell(-40.25m) == "\"-40.25\"", "يجب أن تبقى الخسارة رقماً سالباً في CSV.");
            Require(ReportCsvWriter.Cell("-40.25") == "\"'-40.25\"", "النص البادئ بإشارة يجب أن يحتفظ بحماية الصيغ.");
            Require(ReportCsvWriter.Cell("=1+2") == "\"'=1+2\"", "حماية صيغ CSV غير مطبقة.");
            Require(ReportCsvWriter.Cell("صنف, \"عربي\"") == "\"صنف, \"\"عربي\"\"\"", "ترميز الاقتباس والفاصلة غير صحيح.");
            Require(ReportCsvWriter.Cell(DBNull.Value) == "\"\"", "الهامش غير المحدد يجب أن يبقى فارغاً.");
            Require(ReportCsvWriter.Cell(new DateTime(2026, 9, 14)) == "\"2026-09-14\"", "تاريخ التقرير يجب أن يكون مستقلاً عن إعدادات المنطقة.");
            var previousCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                Require(ReportCsvWriter.Cell(-40.25m) == "\"-40.25\"", "يجب ألا تغير ثقافة النظام تنسيق الأرقام المصدر.");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previousCulture; }
        }
        private static void VerifyMedicineBarcodeSelection()
        {
            Require(MedicineLookupService.SearchQuery.Contains("MedicineActiveIngredientsTb") &&
                MedicineLookupService.SearchQuery.Contains("ActiveIngredientsTb") &&
                MedicineLookupService.SearchQuery.Contains("ai.IsActive = 1") &&
                MedicineLookupService.SearchQuery.Contains("OBJECT_ID"),
                "يجب أن يبحث الاستعلام بالمادة الفعالة النشطة عند وجود الترحيل مع بقاء توافق القاعدة القديمة.");
            var barcodeService=new PharmacyManagement.Application.MedicineSearchService(new PharmacyManagement.Infrastructure.SqlServer.SqlMedicineSearchRepository(() => { throw new Exception("Unexpected SQL."); }), actor=>true);
            var barcodeActor=new PharmacyManagement.Application.OperationActor { UserName="barcode-test", UserRole="Manager" };
            Require(barcodeService.FindByBarcode("  ", barcodeActor) == null, "الباركود الفارغ لا يجب أن يختار صنفاً.");
            bool longRejected = false;
            try { barcodeService.FindByBarcode(new string('1', 101), barcodeActor); }
            catch (InvalidOperationException) { longRejected = true; }
            Require(longRejected, "لا يجوز اقتطاع باركود طويل واختيار صنف بباركود مختلف.");
            using (var table = new System.Data.DataTable())
            {
                table.Columns.Add("MedName", typeof(string));
                table.Columns.Add("CompName", typeof(string));
                using (var reader = table.CreateDataReader())
                    Require(MedicineLookupService.ReadUniqueBarcodeMatch(reader) == null, "نتيجة البحث الفارغة يجب ألا تختار صنفاً.");
                table.Rows.Add("دواء أول", "شركة أولى");
                using (var reader = table.CreateDataReader())
                {
                    var result = MedicineLookupService.ReadUniqueBarcodeMatch(reader);
                    Require(result.MedicineName == "دواء أول" && result.CompanyName == "شركة أولى", "يجب الحفاظ على اسم الصنف وشركته معاً.");
                }
                table.Rows.Add("دواء آخر", "شركة أخرى");
                bool duplicateRejected = false;
                using (var reader = table.CreateDataReader())
                {
                    try { MedicineLookupService.ReadUniqueBarcodeMatch(reader); }
                    catch (InvalidOperationException) { duplicateRejected = true; }
                }
                Require(duplicateRejected, "الباركود المكرر يجب ألا يختار أول صنف تلقائياً.");
            }
        }

        private static void PopularMedicineRankingIncludesCommercialReturns()
        {
            Require(MedicineLookupService.PopularQuery.Contains("N'CommercialSalesReturn'"),
                "ترتيب الأصناف الأكثر مبيعاً لا يطرح حركات المرتجع التجاري من صافي البيع.");
        }
        private static void VerifyCountryTaxPolicy()
        {
            var recovery = new System.Xml.Linq.XElement("TaxSettingsSave",
                new System.Xml.Linq.XAttribute("previousCountry", "YE"),
                new System.Xml.Linq.XAttribute("country", "SA"));
            Require(TaxSettingsRecovery.ResolveCountry(recovery, false) == "YE", "عملية SQL غير المعتمدة يجب أن تستعيد البلد السابق.");
            Require(TaxSettingsRecovery.ResolveCountry(recovery, true) == "SA", "إثبات اعتماد SQL يجب أن يستعيد البلد الجديد.");
            bool malformedRejected = false;
            try { TaxSettingsRecovery.ResolveCountry(new System.Xml.Linq.XElement("TaxSettingsSave"), true); }
            catch (System.IO.InvalidDataException) { malformedRejected = true; }
            Require(malformedRejected, "لا يجوز تخمين بلد عند تلف سجل الاستعادة.");
            Require(new TaxPolicy(" ye ", true, 15m).EffectiveRate == 0m, "سياسة اليمن يجب أن تعطي صفراً حتى عند تفعيل الضريبة.");
            Require(new TaxPolicy(null, true, 15m).EffectiveRate == 0m, "البلد الافتراضي يجب أن يكون اليمن.");
            Require(new TaxPolicy("SA", true, 15m).EffectiveRate == 15m, "يجب تطبيق النسبة المفعلة خارج اليمن.");
            Require(new TaxPolicy("OTHER", false, 15m).EffectiveRate == 0m, "تعطيل الضريبة يجب أن يعطي صفراً.");
            Require(new TaxPolicy("SA", true, 100m).EffectiveRate == 100m, "الحد الأعلى لنسبة الضريبة غير صحيح.");
            foreach (decimal invalid in new[] { -0.01m, 100.01m, 1.001m })
            {
                bool rejected = false;
                try { new TaxPolicy("SA", true, invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "يجب رفض نسبة الضريبة غير الصالحة.");
            }
        }
        private static void InvoiceCalculationKeepsExactTaxAndDiscount()
        {
            InvoiceTotals totals = InvoiceTotalsCalculator.Calculate(new[]
            {
                new CommercialInvoiceLine { MedicineName = "A", Quantity = 10m, UnitPrice = 100m, TaxRatePercent = 15m },
                new CommercialInvoiceLine { MedicineName = "B", Quantity = 1m, UnitPrice = 250m, LineDiscount = 50m, TaxRatePercent = 15m }
            }, 10m);
            Require(totals.GrossTotal == 1250m && totals.DiscountTotal == 170m && totals.TaxableTotal == 1080m && totals.TaxTotal == 162m && totals.NetTotal == 1242m,
                "إجماليات الفاتورة لا تطابق قاعدة الخصم ثم الضريبة.");
        }

        private static void FefoUsesClosestValidLotAndSkipsExpiredOrRecalled()
        {
            DateTime today = new DateTime(2026, 8, 2);
            Guid selected = Guid.NewGuid();
            IReadOnlyList<LotAllocation> result = FefoAllocator.Allocate("دواء", 3m, new List<InventoryLotSnapshot>
            {
                new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", BatchNumber = "expired", ExpiryDate = today.AddDays(-1), RemainingQuantity = 100m },
                new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", BatchNumber = "recalled", ExpiryDate = today.AddDays(1), RemainingQuantity = 100m, IsRecalled = true },
                new InventoryLotSnapshot { LotId = selected, MedicineName = "دواء", BatchNumber = "near", ExpiryDate = today.AddDays(2), RemainingQuantity = 3m },
                new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", BatchNumber = "later", ExpiryDate = today.AddDays(10), RemainingQuantity = 3m }
            }, today);
            Require(result.Count == 1 && result[0].LotId == selected && result[0].Quantity == 3m, "FEFO لم يخصص الدفعة الأقرب الصالحة.");
        }

        private static void InvoiceDiscountRemainderKeepsTaxConsistent()
        {
            InvoiceTotals totals = InvoiceTotalsCalculator.Calculate(new[]
            {
                new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = .05m, TaxRatePercent = 100m },
                new CommercialInvoiceLine { MedicineName = "B", Quantity = 1m, UnitPrice = .05m, TaxRatePercent = 100m }
            }, 10m);
            Require(totals.TaxableTotal == .09m && totals.TaxTotal == .09m && totals.NetTotal == .18m, "باقي خصم الفاتورة سبب فرقاً في الضريبة.");
        }

        private static void InclusiveTaxKeepsScreenAndInvoiceTotalsConsistent()
        {
            CommercialInvoiceLine line = new CommercialInvoiceLine
            {
                MedicineName = "A",
                Quantity = 4m,
                UnitPrice = 3m,
                TaxRatePercent = 15m,
                PriceTaxMode = CommercialPriceTaxMode.Inclusive
            };
            InvoiceTotals totals = InvoiceTotalsCalculator.Calculate(new[] { line }, 10m);
            IReadOnlyList<decimal> lineTax = InvoiceTotalsCalculator.CalculateLineTaxAmounts(new[] { line }, 10m);
            Require(totals.GrossTotal == 12m && totals.DiscountTotal == 1.20m && totals.TaxableTotal == 9.39m &&
                totals.TaxTotal == 1.41m && totals.NetTotal == 10.80m && lineTax[0] == 1.41m,
                "Inclusive tax calculation lost the displayed invoice total after rounding.");
        }

        private static void InvoiceTotalsContractRejectsTamperedInclusiveTotals()
        {
            bool rejected = false;
            try
            {
                InvoiceTotalsContract.Validate(new InvoiceTotals
                {
                    PriceTaxMode = CommercialPriceTaxMode.Inclusive,
                    GrossTotal = 115m,
                    DiscountTotal = 0m,
                    TaxableTotal = 91m,
                    TaxTotal = 25m,
                    NetTotal = 115m
                });
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل عقد الإجماليات وعاءً ضريبياً لا يطابق Gross وDiscount في سعر شامل.");
        }

        private static void Gs1ParserReadsTraceabilityFields()
        {
            Gs1DataMatrixData data = Gs1DataMatrixParser.ParseHumanReadable("(01)06260001234567(17)270531(10)LOT-A(21)SERIAL-1");
            Require(data.Gtin == "06260001234567" && data.BatchNumber == "LOT-A" && data.SerialNumber == "SERIAL-1" && data.ExpiryDate == new DateTime(2027, 5, 31), "فشل تحليل بيانات GS1.");
        }

        private static void RawGs1ParserReadsTraceabilityFields()
        {
            Gs1DataMatrixData data = Gs1DataMatrixParser.ParseHumanReadable("01062600012345671727053110LOT-A" + (char)29 + "21SERIAL-1");
            Require(data.Gtin == "06260001234567" && data.BatchNumber == "LOT-A" && data.SerialNumber == "SERIAL-1" && data.ExpiryDate == new DateTime(2027, 5, 31), "فشل تحليل GS1 الخام.");
        }

        private static void SandboxEInvoiceProducesHashAndTlv()
        {
            InvoiceTotals totals = new InvoiceTotals { GrossTotal = 100m, DiscountTotal = 0m, TaxableTotal = 100m, TaxTotal = 15m, NetTotal = 115m };
            EInvoiceArtifact artifact = new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
            {
                InvoiceId = Guid.NewGuid(), InvoiceNumber = 1, Kind = EInvoiceDocumentKind.SimplifiedB2C,
                SellerName = "صيدلية اختبار", SellerVatNumber = "310000000000003", IssueDateUtc = new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc),
                Totals = totals, Lines = new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = 100m, TaxRatePercent = 15m } }
            }, 0, string.Empty, 1, string.Empty);
            Require(!string.IsNullOrWhiteSpace(artifact.DocumentHash) && !string.IsNullOrWhiteSpace(artifact.QrTlvBase64) && artifact.Xml.Contains("LOCAL-SANDBOX-NOT-SIGNED") && artifact.Xml.Contains("<LineTotal>115.00</LineTotal>"), "فشل إنشاء أثر الفاتورة الإلكترونية التجريبي أو إجمالي البند الضريبي.");
        }

        private static void InsuranceRetryBackoffIsBounded()
        {
            IntegrationOutboxItem item = new IntegrationOutboxItem();
            for (int i = 0; i < 10; i++) item.RegisterFailure("شبكة", new DateTime(2026, 8, 2));
            Require(item.AttemptCount == 10 && item.NextAttemptAtUtc == new DateTime(2026, 8, 2).AddMinutes(60), "سياسة إعادة المحاولة غير محددة السقف.");
        }

        private static void SaudiSandboxRejectsForeignCurrency()
        {
            bool rejected = false;
            try
            {
                new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
                {
                    InvoiceId = Guid.NewGuid(), InvoiceNumber = 1, Kind = EInvoiceDocumentKind.SimplifiedB2C,
                    SellerName = "صيدلية اختبار", SellerVatNumber = "310000000000003", CurrencyCode = "USD",
                    IssueDateUtc = new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc),
                    Totals = new InvoiceTotals { GrossTotal = 100m, DiscountTotal = 0m, TaxableTotal = 100m, TaxTotal = 15m, NetTotal = 115m },
                    Lines = new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = 100m, TaxRatePercent = 15m } }
                }, 0, string.Empty, 1, string.Empty);
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل مزود الفاتورة السعودي عملة غير الريال السعودي.");
        }

        private static void EInvoiceChainCannotRewind()
        {
            EInvoiceChainValidator.ValidateNext(7, "previous", 8, "previous");
            bool rejected = false;
            try { EInvoiceChainValidator.ValidateNext(7, "previous", 7, "previous"); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "سمح عداد الفاتورة الإلكترونية بالرجوع.");
        }

        private static void SaudiFeatureProductionActivationIsBlocked()
        {
            bool rejected = false;
            try { SaudiCommercialFeatureSettings.ValidateRequestedMode(SaudiCommercialFeatureSettings.EInvoice, SaudiFeatureMode.Production); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "سمحت إعدادات المعايير السعودية بتفعيل الإنتاج الحي دون اعتماد.");
        }

        private static void SaudiFeatureBatchValidatesBeforeSql()
        {
            var valid = new Dictionary<string, SaudiFeatureMode>
            {
                { SaudiCommercialFeatureSettings.EInvoice, SaudiFeatureMode.Simulator },
                { SaudiCommercialFeatureSettings.Rsd, SaudiFeatureMode.Test }
            };
            IReadOnlyDictionary<string, SaudiFeatureMode> validated = SaudiCommercialFeatureSettings.ValidateBatch(valid);
            Require(validated.Count == 2 && validated[SaudiCommercialFeatureSettings.EInvoice] == SaudiFeatureMode.Simulator,
                "لم تُعتمد مجموعة المزايا السعودية الصالحة قبل المعاملة.");

            foreach (var invalid in new[]
            {
                new Dictionary<string, SaudiFeatureMode> { { SaudiCommercialFeatureSettings.EInvoice, SaudiFeatureMode.Production } },
                new Dictionary<string, SaudiFeatureMode> { { SaudiCommercialFeatureSettings.EInvoice, (SaudiFeatureMode)99 } },
                new Dictionary<string, SaudiFeatureMode> { { "UnknownFeature", SaudiFeatureMode.Test } },
                new Dictionary<string, SaudiFeatureMode>(StringComparer.Ordinal)
                {
                    { SaudiCommercialFeatureSettings.EInvoice, SaudiFeatureMode.Test },
                    { "einvoice", SaudiFeatureMode.Simulator }
                }
            })
            {
                bool rejected = false;
                try { SaudiCommercialFeatureSettings.ValidateBatch(invalid); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "قبلت مجموعة المزايا السعودية طلبًا غير صالح قبل بدء معاملة SQL.");
            }
        }

        private static void PasswordHashUsesSha256AndUpgradesLegacyHash()
        {
            const string password = "اختبار-كلمة-مرور-آمنة";
            string currentHash = PasswordSecurity.HashPassword(password);
            bool valid = PasswordSecurity.VerifyPassword(password, currentHash, out bool needsUpgrade);
            Require(currentHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal) && valid && !needsUpgrade,
                "لم تنشئ حماية كلمة المرور تجزئة SHA-256 حديثة قابلة للتحقق.");

            byte[] salt = new byte[16];
            for (int index = 0; index < salt.Length; index++) salt[index] = (byte)(index + 1);
            byte[] legacyHash;
            using (var legacy = new Rfc2898DeriveBytes(password, salt, 100000))
                legacyHash = legacy.GetBytes(32);
            string legacyStoredValue = "PBKDF2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(legacyHash);
            valid = PasswordSecurity.VerifyPassword(password, legacyStoredValue, out needsUpgrade);
            Require(valid && needsUpgrade, "لم تطلب حماية كلمة المرور ترقية تجزئة PBKDF2 القديمة بعد تحققها.");

            bool whitespaceRejected = false;
            try { PasswordSecurity.HashPassword(" كلمة مرور آمنة"); }
            catch (ArgumentException) { whitespaceRejected = true; }
            Require(whitespaceRejected, "قبلت حماية كلمة المرور مسافات طرفية قد تفشل بها تجربة تسجيل الدخول.");

            bool tooShortPasswordRejected = false;
            try { PasswordSecurity.HashPassword("ab"); }
            catch (ArgumentException) { tooShortPasswordRejected = true; }
            Require(tooShortPasswordRejected, "قبلت حماية كلمة المرور الجديدة رمزاً أقصر من ثلاثة أحرف.");

            string shortPasswordHash = PasswordSecurity.HashPassword("admin123");
            valid = PasswordSecurity.VerifyPassword("admin123", shortPasswordHash, out needsUpgrade);
            Require(valid && !needsUpgrade, "لم تقبل حماية كلمة المرور رمزاً صحيحاً من ثلاثة أحرف أو أكثر.");

            string historicalShortPasswordHash = PasswordSecurity.HashLegacyPasswordForMigration("oldpass1");
            valid = PasswordSecurity.VerifyPassword("oldpass1", historicalShortPasswordHash, out needsUpgrade);
            Require(valid && !needsUpgrade, "منعت سياسة كلمة المرور الجديدة دخول حساب قديم تم التحقق منه بالفعل.");

            string migratedWhitespaceHash = PasswordSecurity.HashLegacyPasswordForMigration(" كلمة مرور قديمة ");
            valid = PasswordSecurity.VerifyPassword(" كلمة مرور قديمة ", migratedWhitespaceHash, out needsUpgrade);
            Require(valid && !needsUpgrade, "فشلت ترقية كلمة مرور قديمة صحيحة تحتوي مسافات طرفية.");
        }

        private static void DynamicDatabaseSettingsKeepEncryptionEnabled()
        {
            var remote = new System.Data.SqlClient.SqlConnectionStringBuilder(Database.BuildSettingsConnectionString("sql.example.test", "PharmacyManagement_qa", true));
            var local = new System.Data.SqlClient.SqlConnectionStringBuilder(Database.BuildSettingsConnectionString(".", "PharmacyManagement_qa", true));
            Require(remote.Encrypt && !remote.TrustServerCertificate && local.Encrypt && local.TrustServerCertificate,
                "إعدادات الاتصال الديناميكية يجب أن تفرض التشفير وألا تثق بشهادة خادم بعيد تلقائياً.");
        }

        private static void RuntimeProfileSeparatesTestDatabases()
        {
            var production = new DatabaseConnectionProfile(".", "PharmacyManagementDb", true);
            var qualityAssurance = new DatabaseConnectionProfile(".", "PharmacyManagement_qa", true);
            Require(!production.IsTestEnvironment && production.EnvironmentNameAr == "إنتاج" &&
                qualityAssurance.IsTestEnvironment && qualityAssurance.EnvironmentNameAr == "اختبار",
                "تعذر تمييز قاعدة الإنتاج عن قاعدة الاختبار في إعدادات التشغيل.");
        }

        private static void SystemDatabasesAreRejectedForPharmacySettings()
        {
            bool rejected = false;
            try { Database.BuildSettingsConnectionString(".", "master", true); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "سمح إعداد الاتصال باستخدام قاعدة SQL النظامية master لبيانات الصيدلية.");
        }

        private static void CommercialTestDatabaseNameUsesStrictSuffix()
        {
            Require(CommercialSchemaInstaller.IsClearlyNamedTestDatabase("Pharmacy_qa") &&
                !CommercialSchemaInstaller.IsClearlyNamedTestDatabase("Pharmacy_testimonial"),
                "شرط قاعدة الاختبار التجاري يجب أن يقبل اللاحقة الصريحة فقط.");
        }

        private static void CommercialSchemaSqlUsesExecutableFeatureLiterals()
        {
            Require(CommercialSchemaInstaller.SchemaSql.Contains("N'CommercialSales'") &&
                !CommercialSchemaInstaller.SchemaSql.Contains("N''CommercialSales''"),
                "نص تأسيس المخطط التجاري يحتوي اقتباسات SQL غير صالحة لقيم إعدادات الميزات.");
            Require(CommercialSchemaInstaller.SchemaInstallPermissionSql.Contains("CREATE TABLE") &&
                CommercialSchemaInstaller.SchemaInstallPermissionSql.Contains("ALTER") &&
                CommercialSchemaInstaller.SchemaInstallPermissionSql.Contains("SCHEMA"),
                "أمر صيانة QA يجب أن يتحقق من صلاحيات SQL اللازمة للمخطط قبل التثبيت.");
            CommercialSchemaInstaller.Validate(new CommercialMigrationRequest
            {
                VerifiedBackupPath = @"Z:\sql-service-only\verified.bak",
                IsTestDatabase = true,
                AcknowledgeNoHistoricalLotBackfill = true
            });
            bool invalidExtensionRejected = false;
            try
            {
                CommercialSchemaInstaller.Validate(new CommercialMigrationRequest
                {
                    VerifiedBackupPath = @"Z:\sql-service-only\verified.txt",
                    IsTestDatabase = true,
                    AcknowledgeNoHistoricalLotBackfill = true
                });
            }
            catch (InvalidOperationException) { invalidExtensionRejected = true; }
            Require(invalidExtensionRejected,
                "يجب رفض مسار النسخة غير المنتهي بـ bak قبل طلب التحقق من SQL Server.");
        }

        private static void PrescriptionValidationRejectsOverDispense()
        {
            GatewayResult result = new SimulatedPrescriptionGateway(() => new DateTime(2026, 8, 2)).Validate(new PrescriptionRecord
            {
                PrescriptionId = "RX-1", PatientId = "P-1", PrescriberName = "Dr", IssuedAt = new DateTime(2026, 8, 1), ExpiresAt = new DateTime(2026, 8, 3),
                Items = new[] { new PrescriptionItem { DrugCode = "D", PrescribedQuantity = 1m, DispensedQuantity = 2m } }
            }, new PatientRecord { PatientId = "P-1" });
            Require(!result.IsAccepted, "قُبلت وصفة بها صرف زائد.");
        }

        private static void PrescriptionValidationRejectsDuplicateDrugCodes()
        {
            GatewayResult result = new SimulatedPrescriptionGateway(() => new DateTime(2026, 8, 2)).Validate(new PrescriptionRecord
            {
                PrescriptionId = "RX-DUP", PatientId = "P-1", PrescriberName = "Dr", IssuedAt = new DateTime(2026, 8, 1), ExpiresAt = new DateTime(2026, 8, 3),
                Items = new[]
                {
                    new PrescriptionItem { DrugCode = "DUP", PrescribedQuantity = 1m, DispensedQuantity = 0m },
                    new PrescriptionItem { DrugCode = " dup ", PrescribedQuantity = 1m, DispensedQuantity = 0m }
                }
            }, new PatientRecord { PatientId = "P-1" });
            Require(!result.IsAccepted, "قبلت محاكاة الوصفة رمز دواء مكرراً يمكن أن يخفي الصرف الزائد.");
        }

        private static void MoneyWithMoreThanTwoDecimalsIsRejected()
        {
            bool rejected = false;
            try { InvoiceTotalsCalculator.Calculate(new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = 1m, LineDiscount = .005m } }, 0m); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبلت الحسابات مبلغاً لا يمكن حفظه بدقة العملة.");
        }

        private static void QuantityWithMoreThanThreeDecimalsIsRejected()
        {
            bool rejected = false;
            try { InvoiceTotalsCalculator.Calculate(new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1.0005m, UnitPrice = 1m } }, 0m); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبلت الحسابات كمية لا يمكن حفظها بدقة المخزون.");
        }

        private static void PartialReturnCannotExceedOriginalLotAllocation()
        {
            Guid lotId = Guid.NewGuid();
            IReadOnlyList<LotReturnRequest> accepted = LotReturnValidator.Validate(
                new[] { new LotAllocation { LotId = lotId, Quantity = 2m } },
                new[] { new LotReturnRequest { LotId = lotId, Quantity = 1m } });
            Require(accepted.Count == 1, "رُفض المرتجع الجزئي الصحيح.");
            bool rejected = false;
            try { LotReturnValidator.Validate(new[] { new LotAllocation { LotId = lotId, Quantity = 2m } }, new[] { new LotReturnRequest { LotId = lotId, Quantity = 3m } }); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قُبل مرتجع أكبر من كمية الدفعة المصروفة.");
            rejected = false;
            try { LotReturnValidator.Validate(new[] { new LotAllocation { LotId = lotId, Quantity = 2m } }, new[] { new LotReturnRequest { LotId = lotId, Quantity = 1.5m } }, new[] { new LotReturnRequest { LotId = lotId, Quantity = 1m } }); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قُبل مرتجع تراكمي أكبر من كمية الدفعة المصروفة.");
        }

        private static void PartialReturnRejectsInvalidQuantityPrecisionAndNormalizesDuplicates()
        {
            Guid lotId = Guid.NewGuid();
            IReadOnlyList<LotReturnRequest> normalized = LotReturnValidator.Validate(
                new[] { new LotAllocation { LotId = lotId, Quantity = 2m } },
                new[]
                {
                    new LotReturnRequest { LotId = lotId, Quantity = .25m },
                    new LotReturnRequest { LotId = lotId, Quantity = .75m }
                });
            Require(normalized.Count == 1 && normalized[0].LotId == lotId && normalized[0].Quantity == 1m,
                "لم يوحّد المتحقق بنود المرتجع المكررة لنفس الدفعة.");

            bool rejected = false;
            try
            {
                LotReturnValidator.Validate(
                    new[] { new LotAllocation { LotId = lotId, Quantity = 2m } },
                    new[] { new LotReturnRequest { LotId = lotId, Quantity = 1.0005m } });
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل المتحقق كمية مرتجع أدق من دقة المخزون.");

            rejected = false;
            try
            {
                LotReturnValidator.Validate(
                    new[] { new LotAllocation { LotId = lotId, Quantity = 2m } },
                    new[] { new LotReturnRequest { LotId = lotId, Quantity = -1m } },
                    new[] { new LotReturnRequest { LotId = lotId, Quantity = .5m } });
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "تجاهل المتحقق سجل مرتجع سابقاً غير صالح.");
        }

        private static void CommercialReturnKeepsOriginalLineValuesAcrossPartialAndFinalCredits()
        {
            CommercialReturnSourceLine source = new CommercialReturnSourceLine
            {
                OriginalLineId = Guid.NewGuid(), MedicineName = "A", Quantity = 2m, UnitPrice = 100m,
                DiscountTotal = 10m, TaxRate = 15m, TaxTotal = 28.50m, NetTotal = 218.50m,
                UnitCost = 60m, CostTotal = 120m, PriceTaxMode = CommercialPriceTaxMode.Exclusive
            };
            CommercialReturnCalculatedLine first = CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], 1m);
            Require(first.GrossTotal == 100m && first.DiscountTotal == 5m && first.TaxTotal == 14.25m &&
                first.NetTotal == 109.25m && first.CostTotal == 60m, "لم يوزع المرتجع الجزئي قيم سطر البيع الثابتة بدقة.");
            CommercialReturnCalculatedLine final = CommercialReturnCalculator.CalculateLine(source,
                new[] { new CommercialReturnPostedLine { Quantity = first.Quantity, GrossTotal = first.GrossTotal, DiscountTotal = first.DiscountTotal, TaxTotal = first.TaxTotal, NetTotal = first.NetTotal, CostTotal = first.CostTotal } }, 1m);
            Require(first.GrossTotal + final.GrossTotal == 200m && first.DiscountTotal + final.DiscountTotal == 10m &&
                first.TaxTotal + final.TaxTotal == 28.50m && first.NetTotal + final.NetTotal == 218.50m && first.CostTotal + final.CostTotal == 120m,
                "المرتجع النهائي لم يحفظ بالضبط ما تبقى من لقطة البيع الأصلية.");
            InvoiceTotals totals = CommercialReturnCalculator.CalculateTotals(new[] { first });
            Require(totals.GrossTotal == 100m && totals.DiscountTotal == 5m && totals.TaxTotal == 14.25m && totals.NetTotal == 109.25m,
                "إجماليات الإشعار الدائن لا تطابق السطر المرتجع.");
        }

        private static void CommercialReturnFinalRoundingRemainderProducesExactCreditArtifact()
        {
            CommercialReturnSourceLine source = new CommercialReturnSourceLine
            {
                OriginalLineId = Guid.NewGuid(), MedicineName = "تقريب", Quantity = 3m, UnitPrice = .05m,
                DiscountTotal = 0m, TaxRate = 15m, TaxTotal = .02m, NetTotal = .17m,
                UnitCost = .01m, CostTotal = .03m, PriceTaxMode = CommercialPriceTaxMode.Exclusive
            };
            CommercialReturnCalculatedLine first = CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], 1m);
            CommercialReturnCalculatedLine second = CommercialReturnCalculator.CalculateLine(source,
                new[] { Posted(first) }, 1m);
            CommercialReturnCalculatedLine final = CommercialReturnCalculator.CalculateLine(source,
                new[] { Posted(first), Posted(second) }, 1m);
            Require(first.TaxTotal == .01m && second.TaxTotal == .01m && final.TaxTotal == 0m &&
                first.NetTotal + second.NetTotal + final.NetTotal == source.NetTotal,
                "لم يحتفظ المرتجع الأخير بباقي التقريب المعتمد من الفاتورة الأصلية.");
            InvoiceTotals totals = CommercialReturnCalculator.CalculateTotals(new[] { final });
            EInvoiceArtifact artifact = new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
            {
                InvoiceId = Guid.NewGuid(), InvoiceNumber = 4, Kind = EInvoiceDocumentKind.CreditNote,
                OriginalInvoiceId = Guid.NewGuid(), AdjustmentReason = "باقي تقريب", SellerName = "صيدلية اختبار", SellerVatNumber = "310000000000003",
                IssueDateUtc = new DateTime(2026, 9, 16, 1, 0, 0, DateTimeKind.Utc), Totals = totals,
                Lines = new[] { final.ToCreditNoteLine() },
                ExactLines = new[] { new EInvoiceLineSnapshot
                {
                    MedicineName = final.MedicineName, Quantity = final.Quantity, UnitPrice = final.UnitPrice,
                    GrossAmount = final.GrossTotal, DiscountAmount = final.DiscountTotal,
                    TaxableAmount = final.NetTotal - final.TaxTotal, TaxRatePercent = final.TaxRate,
                    TaxAmount = final.TaxTotal, LineTotal = final.NetTotal, PriceTaxMode = final.PriceTaxMode
                } }
            }, 2, "previous", 3, "previous");
            Require(artifact.Xml.Contains("<TaxAmount>0.00</TaxAmount>") && artifact.Xml.Contains("<LineTotal>0.05</LineTotal>"),
                "مولد الأثر الإلكتروني أعاد تقريب باقي المرتجع بدلاً من حفظ اللقطة المعتمدة.");
        }

        private static CommercialReturnPostedLine Posted(CommercialReturnCalculatedLine line)
        {
            return new CommercialReturnPostedLine
            {
                Quantity = line.Quantity, GrossTotal = line.GrossTotal, DiscountTotal = line.DiscountTotal,
                TaxTotal = line.TaxTotal, NetTotal = line.NetTotal, CostTotal = line.CostTotal
            };
        }

        private static void CommercialReturnRejectsOverReturnAndFractionalQuantity()
        {
            CommercialReturnSourceLine source = new CommercialReturnSourceLine
            {
                OriginalLineId = Guid.NewGuid(), MedicineName = "A", Quantity = 2m, UnitPrice = 115m,
                DiscountTotal = 0m, TaxRate = 15m, TaxTotal = 30m, NetTotal = 230m,
                UnitCost = 50m, CostTotal = 100m, PriceTaxMode = CommercialPriceTaxMode.Inclusive
            };
            bool rejected = false;
            try { CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], 3m); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل الحاسب مرتجعاً يتجاوز كمية البيع التجاري.");
            rejected = false;
            try { CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], .5m); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل الحاسب مرتجعاً كسرياً رغم أن مسار البيع التجاري يبيع عبوات كاملة فقط.");
        }

        private static void CommercialReturnUsesActualSoldLotCost()
        {
            CommercialReturnSourceLine source = new CommercialReturnSourceLine
            {
                OriginalLineId = Guid.NewGuid(), MedicineName = "A", Quantity = 2m, UnitPrice = 100m,
                DiscountTotal = 0m, TaxRate = 15m, TaxTotal = 30m, NetTotal = 230m,
                UnitCost = 45m, CostTotal = 90m, PriceTaxMode = CommercialPriceTaxMode.Exclusive
            };
            CommercialReturnCalculatedLine first = CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], 1m, 40m);
            CommercialReturnCalculatedLine final = CommercialReturnCalculator.CalculateLine(source,
                new[] { new CommercialReturnPostedLine { Quantity = 1m, GrossTotal = first.GrossTotal, DiscountTotal = first.DiscountTotal, TaxTotal = first.TaxTotal, NetTotal = first.NetTotal, CostTotal = first.CostTotal } }, 1m, 50m);
            Require(first.CostTotal == 40m && final.CostTotal == 50m,
                "يجب أن تسترد تكلفة المرتجع من الدفعات المصروفة فعلاً لا من متوسط تكلفة الصنف.");
        }

        private static void CreditNoteRequiresOriginalInvoiceAndPreservesItsLink()
        {
            Guid original = Guid.NewGuid();
            InvoiceTotals totals = new InvoiceTotals { GrossTotal = 100m, DiscountTotal = 0m, TaxableTotal = 100m, TaxTotal = 15m, NetTotal = 115m };
            EInvoiceArtifact artifact = new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
            {
                InvoiceId = Guid.NewGuid(), InvoiceNumber = 2, Kind = EInvoiceDocumentKind.CreditNote,
                OriginalInvoiceId = original, AdjustmentReason = "مرتجع اختبار", SellerName = "صيدلية اختبار", SellerVatNumber = "310000000000003",
                IssueDateUtc = new DateTime(2026, 9, 16, 1, 0, 0, DateTimeKind.Utc), Totals = totals,
                Lines = new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = 100m, TaxRatePercent = 15m } }
            }, 1, "previous", 2, "previous");
            Require(artifact.Xml.Contains(original.ToString("D")) && artifact.Xml.Contains("CreditNote"),
                "لم يحفظ الأثر الإلكتروني رابط الإشعار الدائن بالفاتورة الأصلية.");
            bool rejected = false;
            try { new LocalZatcaSandboxProvider().Create(new EInvoiceRequest { InvoiceId = Guid.NewGuid(), InvoiceNumber = 3, Kind = EInvoiceDocumentKind.CreditNote, SellerName = "صيدلية اختبار", SellerVatNumber = "310000000000003", Totals = totals, Lines = new[] { new CommercialInvoiceLine { MedicineName = "A", Quantity = 1m, UnitPrice = 100m, TaxRatePercent = 15m } } }, 2, artifact.DocumentHash, 3, artifact.DocumentHash); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل مزود الأثر الإلكتروني إشعاراً دائناً بلا فاتورة أصلية وسبب.");
        }

        private static void PurchaseSettlementFollowsExplicitPaymentMethod()
        {
            Require(AccountingService.GetPurchaseSettlementAccount(null) == AccountingService.CashAccount &&
                AccountingService.GetPurchaseSettlementAccount("Cash") == AccountingService.CashAccount &&
                AccountingService.GetPurchaseSettlementAccount("Payable") == AccountingService.SuppliersAccount,
                "طريقة سداد الشراء لا تختار حساب النقد أو ذمم الموردين الصحيح.");
            bool rejected = false;
            try { AccountingService.GetPurchaseSettlementAccount("Unknown"); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل قيد الشراء طريقة سداد غير معروفة.");
        }

        private static void CommercialReturnPostsToTheSelectedSettlementAccount()
        {
            Require(AccountingService.GetCommercialSalesReturnSettlementAccount("Cash") == AccountingService.CashAccount &&
                AccountingService.GetCommercialSalesReturnSettlementAccount("OriginalMethod") == AccountingService.CashAccount &&
                AccountingService.GetCommercialSalesReturnSettlementAccount("Card") == AccountingService.BankAccount &&
                AccountingService.GetCommercialSalesReturnSettlementAccount("Credit") == AccountingService.CustomersAccount,
                "قيد المرتجع لا يختار حساب تسوية طريقة الاسترداد الصحيحة.");
            bool rejected = false;
            try { AccountingService.GetCommercialSalesReturnSettlementAccount("Unknown"); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل القيد طريقة استرداد غير مدعومة.");
        }

        private static void CommercialReturnFreeItemRestoresCostWithoutRefund()
        {
            InvoiceTotals totals = new InvoiceTotals { GrossTotal = 100m, DiscountTotal = 100m, TaxableTotal = 0m, TaxTotal = 0m, NetTotal = 0m };
            var lines = AccountingService.BuildCommercialSalesReturnJournalLines(totals, 60m, "Cash", "اختبار");
            Require(lines.Count == 2 && lines[0].AccountCode == AccountingService.InventoryAccount &&
                lines[0].Debit == 60m && lines[0].Credit == 0m &&
                lines[1].AccountCode == AccountingService.PurchasesAccount && lines[1].Credit == 60m && lines[1].Debit == 0m,
                "يجب أن يعكس المرتجع المجاني تكلفة المخزون دون حركة نقدية.");
            Require(AccountingService.BuildCommercialSalesReturnJournalLines(totals, 0m, "Cash", "اختبار").Count == 0,
                "المرتجع بلا استرداد ولا تكلفة لا يحتاج إلى قيد صفري.");
            bool rejected = false;
            try { AccountingService.BuildCommercialSalesReturnJournalLines(totals, -1m, "Cash", "اختبار"); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل قيد المرتجع تكلفة مخزون سالبة.");
        }

        private static void CommercialReturnJournalBalancesRefundAndCost()
        {
            InvoiceTotals totals = new InvoiceTotals { GrossTotal = 100m, DiscountTotal = 0m, TaxableTotal = 100m, TaxTotal = 15m, NetTotal = 115m };
            var lines = AccountingService.BuildCommercialSalesReturnJournalLines(totals, 60m, "Card", "اختبار");
            decimal debit = 0m, credit = 0m;
            foreach (var line in lines) { debit += line.Debit; credit += line.Credit; }
            Require(lines.Count == 5 && debit == 175m && credit == 175m &&
                lines.Exists(line => line.AccountCode == AccountingService.BankAccount && line.Credit == 115m) &&
                lines.Exists(line => line.AccountCode == AccountingService.OutputTaxAccount && line.Debit == 15m),
                "قيد المرتجع العادي يجب أن يوازن الاسترداد والضريبة والتكلفة.");
        }

        private static void CommercialSalesReturnAuthorizationBindsTheSessionUser()
        {
            CommercialSalesReturnRequest request = new CommercialSalesReturnRequest { IssuedBy = "مدير اختبار" };
            SessionContext.Start("مدير اختبار", "Manager");
            CommercialSalesReturnService.EnsureRequestCanBeIssued(request);
            bool rejected = false;
            request.IssuedBy = "مستخدم آخر";
            try { CommercialSalesReturnService.EnsureRequestCanBeIssued(request); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل مسار الاعتماد انتحال منفذ المرتجع خلاف مستخدم الجلسة.");
            SessionContext.Start("كاشير اختبار", "Cashier");
            request.IssuedBy = "كاشير اختبار";
            rejected = false;
            try { CommercialSalesReturnService.EnsureRequestCanBeIssued(request); }
            catch (UnauthorizedAccessException) { rejected = true; }
            Require(rejected, "قبل مسار الاعتماد مستخدم بيع بلا صلاحية مرتجع مستقلة.");
            SessionContext.Clear();
        }

        private static void CommercialReturnDamagedDispositionDoesNotRestoreInventoryValue()
        {
            Require(CommercialReturnDispositionPolicy.RestoresInventoryValue(CommercialReturnDisposition.Restock) &&
                CommercialReturnDispositionPolicy.RestoresInventoryValue(CommercialReturnDisposition.Quarantine) &&
                !CommercialReturnDispositionPolicy.RestoresInventoryValue(CommercialReturnDisposition.Damaged),
                "لا يجوز إعادة تكلفة الصنف التالف إلى أصل المخزون القابل للبيع أو المحجور.");
        }

        private static void CommercialReturnCreditRequiresIdentifiedCustomer()
        {
            CommercialReturnCreditPolicy.EnsureCustomerIsIdentified("Cash", string.Empty);
            CommercialReturnCreditPolicy.EnsureCustomerIsIdentified("Credit", "C-17");
            bool rejected = false;
            try { CommercialReturnCreditPolicy.EnsureCustomerIsIdentified("Credit", " "); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل المرتجع كرصيد عميل بلا عميل محدد.");
        }

        private static void CommercialReturnRequestValidationRejectsOversizedAuditFields()
        {
            CommercialSalesReturnRequest request = new CommercialSalesReturnRequest
            {
                OriginalInvoiceId = Guid.NewGuid(), RequestId = "request", ReturnReason = "سبب", IssuedBy = new string('م', 201),
                Lines = new[] { new CommercialSalesReturnRequestLine { OriginalLineId = Guid.NewGuid(), Quantity = 1m, Lots = new[] { new CommercialReturnLotRequest { OriginalSaleAllocationId = Guid.NewGuid(), Quantity = 1m, Disposition = CommercialReturnDisposition.Restock } } } }
            };
            bool rejected = false;
            try { CommercialSalesReturnRepository.ValidateRequest(request); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل طلب مرتجع باسم منفذ يتجاوز حد التدقيق.");
            request.IssuedBy = "مستخدم";
            request.Notes = new string('ن', 1001);
            rejected = false;
            try { CommercialSalesReturnRepository.ValidateRequest(request); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "قبل طلب مرتجع بملاحظات تتجاوز حد التدقيق.");
        }

        private static void FullCommercialBlocksLegacyAggregateInventoryOperations()
        {
            CommercialInventoryOperationPolicy.EnsureLegacyAggregateOperationAllowed(false);
            bool rejected = false;
            try { CommercialInventoryOperationPolicy.EnsureLegacyAggregateOperationAllowed(true); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "سمح وضع البيع التجاري بعملية تغير الرصيد المجمع دون حركة دفعة موثقة.");
        }

        private static void PrintSnapshotDefensivelyCopiesTotals()
        {
            InvoiceTotals sourceTotals = new InvoiceTotals { PriceTaxMode = CommercialPriceTaxMode.Inclusive, GrossTotal = 115m, DiscountTotal = 0m, TaxableTotal = 100m, TaxTotal = 15m, NetTotal = 115m };
            CommercialInvoicePrintSnapshot snapshot = new CommercialInvoicePrintSnapshot(
                Guid.NewGuid(), 1, new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc), "SAR", 1m,
                "C-1", "عميل", "777", "عنوان", "cashier", "", sourceTotals,
                new[] { new CommercialInvoicePrintLine("A", 1m, 115m, 0m, 0m, 15m, 15m, 115m) });
            sourceTotals.NetTotal = 999m;
            Require(snapshot.PriceTaxMode == CommercialPriceTaxMode.Inclusive && snapshot.Totals.NetTotal == 115m && snapshot.Lines.Count == 1 && snapshot.Lines[0].LineTotal == 115m,
                "لقطة الطباعة التجارية شاركت مرجعاً قابلاً للتعديل مع بيانات الإدخال.");
        }

        // F7: the legacy tax helper must use the same half-away-from-zero policy as the commercial one.
        private static void LegacyIncludedTaxUsesSharedRounding()
        {
            Require(AccountingService.CalculateIncludedTax(3.03m, 20m) == InvoiceTotalsCalculator.Round(3.03m * 20m / 120m),
                "Legacy included tax differs from the shared rounding policy.");
            Require(AccountingService.CalculateIncludedTax(3.03m, 20m) == 0.51m,
                "Legacy included tax must round half away from zero (3.03 at 20% -> 0.51).");
            Require(AccountingService.CalculateIncludedTax(9.50m, 15m) == 1.24m,
                "Legacy included tax baseline changed unexpectedly.");
        }
        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }
    }
}

