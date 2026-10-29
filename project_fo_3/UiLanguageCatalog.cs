// طبقة اللغة الإنجليزية: لا تُفعّل قبل اكتمال التغطية، لكنها تفصل النص والاتجاه عن طبقة RTL العربية.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class UiLanguageCatalog
    {
        private static readonly Dictionary<string, string> ArabicToEnglish =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "لا توجد بنود فاتورة معتمدة لإلغائها.", "There are no posted invoice lines to cancel." },
                { "مرجع الفاتورة غير صالح للإلغاء.", "The invoice reference is invalid for cancellation." },
                { "تم إلغاء هذه الفاتورة مسبقاً، ولا يمكن تكرار الإلغاء.", "This invoice has already been cancelled and cannot be cancelled again." },
                { "لا يوجد قيد أصلي كامل لهذه الفاتورة، لذلك أُوقف الإلغاء الآلي حفاظاً على سلامة الحسابات.", "There is no complete original journal entry for this invoice. Automatic cancellation was stopped to protect the accounts." },
                { "بعض بنود الفاتورة لم تعد موجودة؛ أُوقف الإلغاء لمنع عكس جزئي.", "Some invoice lines no longer exist. Cancellation was stopped to prevent a partial reversal." },
                { "لا يمكن إلغاء فاتورة بلا عميل مسجل.", "An invoice without a registered customer cannot be cancelled." },
                { "إجمالي الفاتورة غير صالح للإلغاء.", "The invoice total is invalid for cancellation." },
                { "إجمالي الفاتورة المؤرشف لا يطابق القيد الأصلي؛ أُوقف الإلغاء لمنع عكس مالي غير صحيح.", "The archived invoice total does not match the original journal entry. Cancellation was stopped to prevent an incorrect financial reversal." },
                { "القيد الأصلي لا يتضمن مبلغ بيع نقدي صالحاً؛ أُوقف الإلغاء للحفاظ على سلامة الحسابات.", "The original journal entry has no valid cash sale amount. Cancellation was stopped to protect the accounts." },
                { "لم يمكن تحديد قيد واحد للفواتير المحددة. الإلغاء متاح للفواتورة الحالية فقط.", "A single journal entry could not be identified for the selected invoices. Only the current invoice can be cancelled." },
                { "يوجد بند بيع غير صالح للإلغاء.", "A sales line is invalid for cancellation." },
                { "تعذر إعادة المخزون للدواء: {0}", "Could not restore stock for medicine: {0}" },
                { "رصيد العميل لا يسمح بعكس الفاتورة بأمان.", "The customer balance does not allow this invoice to be reversed safely." },
                { "سبب الإلغاء يتجاوز الحد المسموح (500).", "The cancellation reason exceeds the allowed length (500)." },
                { "تم إلغاء فاتورة المشتريات مسبقاً.", "The purchase invoice has already been cancelled." },
                { "لا توجد بنود صالحة وكاملة لعكس فاتورة المشتريات.", "There are no valid, complete lines to reverse this purchase invoice." },
                { "إجمالي الفاتورة لا يطابق تفاصيلها؛ أوقف الإلغاء حفاظاً على سلامة البيانات.", "The invoice total does not match its lines. Cancellation was stopped to protect data integrity." },
                { "لا يوجد قيد شراء كامل ومتوازن لهذه الفاتورة؛ أوقف الإلغاء.", "There is no complete, balanced purchase journal entry for this invoice. Cancellation was stopped." },
                { "فاتورة المشتريات المحددة غير موجودة.", "The selected purchase invoice does not exist." },
                { "تعذر إثبات أن رصيد المورد لم يتأثر بحركات لاحقة؛ أوقف الإلغاء وراجع حساب المورد أولاً.", "It could not be verified that later transactions left the supplier balance unchanged. Cancellation was stopped. Review the supplier account first." },
                { "لا يكفي المخزون الحالي لإلغاء كمية {0}.", "There is not enough stock to cancel the quantity for {0}." },
                { "تغير رصيد المخزون؛ أُوقف الإلغاء.", "The stock balance changed. Cancellation was stopped." },
                { "تعذر التحقق من تواريخ الصنف قبل عكس المشتريات.", "Could not verify the item dates before reversing the purchase." },
                { "لا يمكن إلغاء هذه الفاتورة في وضع المخزون المجمع؛ قد تكون قد غيّرت تواريخ ملخص الصنف ولا توجد لقطة سابقة محفوظة. راجع الصنف يدويًا.", "This invoice cannot be cancelled in aggregate stock mode. It may have changed the item summary dates, and no earlier snapshot is stored. Review the item manually." },
                { "تعذر إعادة اشتقاق تواريخ المخزون بعد الإلغاء.", "Could not recalculate the stock dates after cancellation." },
                { "تعذر الحفاظ على تاريخ انتهاء الدواء بعد الإلغاء.", "Could not preserve the medicine expiry date after cancellation." },
                { "تعذر تحديث تواريخ الدواء بعد إلغاء فاتورة الشراء.", "Could not update the medicine dates after cancelling the purchase invoice." },
                { "التشغيلة المرتبطة ببند الشراء مفقودة؛ أوقف الإلغاء.", "The batch linked to the purchase line is missing. Cancellation was stopped." },
                { "وجدت أكثر من تشغيلة للبند الواحد؛ أوقف الإلغاء للمراجعة.", "More than one batch was found for the same line. Cancellation was stopped for review." },
                { "لا يمكن إلغاء فاتورة استُهلكت تشغيلتها كليًا أو جزئيًا.", "An invoice whose batch has been fully or partly consumed cannot be cancelled." },
                { "تغير رصيد التشغيلة أثناء الإلغاء؛ أعد المحاولة.", "The batch balance changed during cancellation. Try again." },
                { "رقم فاتورة المشتريات مطلوب.", "A purchase invoice number is required." },
                { "رقم فاتورة المشتريات يتجاوز الحد المسموح ({0}).", "The purchase invoice number exceeds the allowed length ({0})." },
                { "منفذ الإلغاء مطلوب.", "A cancellation actor is required." },
                { "منفذ الإلغاء يتجاوز الحد المسموح ({0}).", "The cancellation actor exceeds the allowed length ({0})." },
                { "إلغاء فاتورة المشتريات", "Cancel purchase invoice" },
                { "حفظ الفاتورة", "Save invoice" },
                { "تمت العملية؛ تعذر تحديث العرض", "Operation completed; display could not be updated" },
                { "تمت العملية بنجاح: {0}.\nالمرجع: {1}\n\nتعذر تحديث العرض فقط. لا تكرر العملية؛ أعد فتح السجل لمراجعة النتيجة.\n\nرمز الدعم: {2}", "The operation completed successfully: {0}.\nReference: {1}\n\nOnly the display update failed. Do not repeat the operation; reopen the history to review the result.\n\nSupport code: {2}" },
                { "تحميل سجل الفواتير", "Load invoice history" },
                { "تحميل سجل المشتريات", "Load purchase history" },
                { "تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.", "The history could not be refreshed. Reopen it before taking another action." },
                { "يعرض تفاصيل الفاتورة أو سجل العميل المحدد.", "Shows the selected invoice details or customer history." },
                { "لا توجد فواتير محفوظة للعرض حالياً.", "There are no saved invoices to display." },
                { "ينتقل بين سجل العملاء وسجل الفواتير الحديثة المربوطة.", "Switches between customer history and recently linked invoices." },
                { "لا توجد بعد فواتير حديثة مربوطة؛ الفواتير القديمة لا تُجمع تلقائياً حفاظاً على الدقة.", "There are no recently linked invoices yet. Older invoices are not grouped automatically to preserve accuracy." },
                { "ينشئ عكساً موثقاً للمخزون والقيد لفاتورة حديثة محددة.", "Creates an audited stock and journal reversal for the selected recent invoice." },
                { "ينشئ إشعار مرتجع من فاتورة بيع تجارية محددة.", "Creates a return note from the selected commercial sales invoice." },
                { "تمت العملية بنجاح: {0}.\nالمرجع: {1}\n\nتعذر تحديث العرض فقط. {2}\n\nرمز الدعم: {3}", "The operation completed successfully: {0}.\nReference: {1}\n\nOnly the display update failed. {2}\n\nSupport code: {3}" },
                { "لا تكرر العملية؛ أعد فتح السجل لمراجعة النتيجة.", "Do not repeat the operation; reopen the history to review the result." },
                { "لا تكرر العملية؛ اضغط «تحديث» لمراجعة النتيجة.", "Do not repeat the operation; select Refresh to review the result." },
                { "لا تكرر العملية؛ أغلق النافذة ثم افتحها مجددًا لمراجعة النتيجة.", "Do not repeat the operation; close and reopen this window to review the result." },
                { "تعذر تحديث البيانات؛ أعد فتح النافذة.", "Could not refresh the data. Reopen this window." },
                { "تعذر تحديث البيانات. اضغط «تحديث» للمراجعة.", "Could not refresh the data. Select Refresh to review it." },
                { "تحميل إدارة المواد الفعالة", "Load active ingredient management" },
                { "تحميل تفاصيل المادة", "Load ingredient details" },
                { "تحديث قائمة الفترات المحاسبية", "Refresh accounting periods" },
                { "قائمة الفترات المحاسبية", "Accounting period list" },
                { "بيانات الفترة المحاسبية", "Accounting period details" },
                { "إجراءات الفترات المحاسبية", "Accounting period actions" },
                { "يعيد تحميل الفترات دون حفظ أو تغيير التواريخ والملاحظات.", "Reloads periods without saving or changing the date and note drafts." },
                { "حُدّثت قائمة الفترات المحاسبية.", "The accounting period list was refreshed." },
                { "تعديلات غير محفوظة", "Unsaved changes" },
                { "متابعة التحرير", "Keep editing" },
                { "تجاهل التعديلات", "Discard changes" },
                { "لديك تعديلات غير محفوظة. يمكنك متابعة التحرير أو الإغلاق وتجاهل هذه التعديلات.", "You have unsaved changes. Keep editing, or close and discard these changes." },
                { "الاسم", "Name" },
                { "استيراد بيانات الأصناف والشركات", "Import item and company data" },
                { "اختر ملف Excel أو CSV، ثم راجع الصفوف والأخطاء قبل الاعتماد.", "Choose an Excel or CSV file, then review the rows and errors before approval." },
                { "معاينة بيانات الاستيراد", "Import data preview" },
                { "يعرض تفاصيل فاتورة المشتريات المحددة.", "Shows the details of the selected purchase invoice." },
                { "لا توجد فواتير مشتريات محفوظة للعرض حالياً.", "There are no saved purchase invoices to display." },
                { "اعتماد الاستيراد", "Approve import" },
                { "مسح فاتورة المشتريات", "Clear purchase invoice" },
                { "العودة إلى فاتورة المشتريات", "Return to purchase invoice" },
                { "إلغاء فاتورة المشتريات المحددة", "Cancel selected purchase invoice" },
                { "يعكس القيد والمخزون لفاتورة المشتريات بعد التأكيد.", "Reverses the purchase journal and stock after confirmation." },
                { "الملف فارغ.", "The file is empty." },
                { "ملف الاستيراد يتجاوز 2,000 صف بيانات؛ قسمه إلى دفعات أصغر.", "The import exceeds 2,000 data rows; split it into smaller batches." },
                { "العمود المطلوب مفقود: {0}", "Required column is missing: {0}" },
                { "يوجد باركود مكرر داخل الملف: {0}", "Duplicate barcode in the file: {0}" },
                { "السطر {0}: {1}", "Row {0}: {1}" },
                { "لا توجد صفوف بيانات بعد العنوان.", "There are no data rows after the header." },
                { "معرّف الصف مطلوب.", "A row identifier is required." },
                { "يوجد تكرار داخل الملف: {0}", "Duplicate entry in the file: {0}" },
                { "كل حقول الشركة مطلوبة.", "All company fields are required." },
                { "أحد حقول الشركة يتجاوز الطول المسموح.", "A company field exceeds the allowed length." },
                { "الباركود والشركة مطلوبان.", "Barcode and company are required." },
                { "أحد حقول الدواء يتجاوز الطول المسموح.", "A medicine field exceeds the allowed length." },
                { "سعر الشراء يجب أن يكون موجباً.", "Purchase price must be positive." },
                { "سعر البيع يجب أن يكون موجباً.", "Selling price must be positive." },
                { "الكمية يجب أن تكون صفراً أو أكثر.", "Quantity must be zero or greater." },
                { "تواريخ الإنتاج والانتهاء غير صالحة. استخدم yyyy-MM-dd أو yyyy/MM/dd.", "Production or expiry dates are invalid. Use yyyy-MM-dd or yyyy/MM/dd." },
                { "عينة تحقق محلية — لم يُقرأ ملف أو قاعدة بيانات.", "Local validation sample — no file or database was read." },
                { "اختيار ملف البيانات", "Choose data file" },
                { "معاينة فقط — لم تُحفظ فاتورة", "Preview only — no invoice was saved" },
                { "جارٍ الحفظ…", "Saving…" },
                { "نتيجة الحفظ غير محسومة — أعد الفتح للتحقق", "Save result unconfirmed — reopen to verify" },
                { "محفوظة — {0}", "Saved — {0}" },
                { "مسودة — Enter لإضافة الصنف المحدد", "Draft — Enter adds the selected item" },
                { "لا توجد أصناف مطابقة للاسم أو الباركود.", "No item matches the name or barcode." },
                { "اسم الصنف أو الباركود", "Item name or barcode" },
                { "اكتب الاسم أو امسح الباركود. Enter للإضافة، والأسهم لاختيار اسم.", "Type a name or scan a barcode. Enter adds once; arrows select a name." },
                { "آخر رصيد", "Last stock" },
                { "آخر رصيد تمت قراءته؛ يُتحقق من المخزون الحالي عند الحفظ.", "Last read balance; current stock is verified on saving." },
                { "تاريخ الإنتاج مطلوب.", "Production date is required." },
                { "تاريخ الانتهاء مطلوب.", "Expiry date is required." },
                { "الانتهاء يجب أن يكون بعد الإنتاج.", "Expiry must follow production." },
                { "تنبيه: الصنف منتهي الصلاحية.", "Warning: this item is expired." },
                { "السابق / التغير", "Recorded / change" },
                { "سعر هذه الوحدة المسجل عند إضافتها / التغير في سعرها.", "Recorded cost of this unit when added / change in its price." },
                { "معاينة مرئية؛ لم يُقرأ ملف أو قاعدة بيانات.", "Visual preview; no file or database was read." },
                { "اختر ملفًا يطابق نوع البيانات المحدد.", "Choose a file matching the selected data type." },
                { "اختر ملفًا صحيحًا مطابقًا لنوع البيانات أولاً.", "Choose a valid file matching the data type first." },
                { "اعتماد الاستيراد متاح للمدير فقط.", "Only managers can commit an import." },
                { "أدخل مبلغًا في المدين أو الدائن فقط.", "Enter an amount in either debit or credit only." },
                { "اختر الحساب.", "Select an account." },
                { "أدخل سعر تحويل موجبًا وصحيحًا.", "Enter a valid positive exchange rate." },
                { "أدخل قيمة موجبة وصحيحة في حقل {0}.", "Enter a valid positive value in the {0} field." },
                { "إجمالي المدين: {0:N2}    إجمالي الدائن: {1:N2}    الفرق: {2:N2}", "Total debit: {0:N2}    Total credit: {1:N2}    Difference: {2:N2}" },
                { "أدخل مبلغ السند بشكل صحيح.", "Enter a valid voucher amount." },
                { "لم يتم الحفظ: مبلغ السند غير صحيح.", "Not saved: the voucher amount is invalid." },
                { "اختر الحساب المقابل وحساب الصندوق/الإيداع.", "Select the counter account and the cash/deposit account." },
                { "لم يتم الحفظ: الحسابات المطلوبة غير مكتملة.", "Not saved: required accounts are missing." },
                { "الحساب المقابل لا يمكن أن يكون نفس حساب الصندوق/البنك.", "The counter account cannot be the same as the cash/bank account." },
                { "لم يتم الحفظ: الحسابان متطابقان.", "Not saved: the two accounts are the same." },
                { "أدخل رقم الشيك واسم البنك قبل حفظ سند الشيك.", "Enter the cheque number and bank name before saving the cheque voucher." },
                { "لم يتم الحفظ: بيانات الشيك ناقصة.", "Not saved: cheque details are incomplete." },
                { "تعذر تحميل الصلاحيات: {0}", "Could not load permissions: {0}" },
                { "تعذرت قراءة الملف. رمز الدعم: {0}", "Could not read the file. Support code: {0}" },
                { "سيُستورد {0} صفاً. لن يُحدّث أو يحذف سجلات مكررة. متابعة؟", "Import {0} rows? Duplicate records will not be updated or deleted. Continue?" },
                { "اكتمل الاستيراد: {0} جديد، {1} مكرر تم تجاوزه.", "Import completed: {0} new records; {1} duplicates skipped." },
                { "تعذر الاستيراد. لم تُعتمد عملية جزئية. رمز الدعم: {0}", "Import failed. No partial import was committed. Support code: {0}" },
                { "صفوف المعاينة: {0} — أخطاء: {1}", "Preview rows: {0} — Errors: {1}" },
                { "إجمالي الفاتورة يجب أن يكون أكبر من صفر.", "The invoice total must be greater than zero." },
                { "تاريخ الإنتاج لا يمكن أن يكون في المستقبل.", "The production date cannot be in the future." },
                { "تاريخ الانتهاء يجب أن يكون بعد تاريخ الإنتاج.", "The expiry date must be after the production date." },
                { "تم إلغاء عملية المسح.", "Clearing was cancelled." },
                { "تم حذف الصنف من السلة.", "The item was removed from the cart." },
                { "تم مسح الفاتورة.", "The invoice was cleared." },
                { "فشل في جلب سعر الشراء للدواء المحدد.", "Could not retrieve the purchase price of the selected medicine." },
                { "لا توجد بيانات للحفظ.", "There is no data to save." },
                { "يرجى إدخال كمية صحيحة.", "Enter a valid quantity." },
                { "يرجى إدخال نسبة خصم صحيحة أقل من 100%.", "Enter a valid discount percentage below 100%." },
                { "يرجى اختيار دواء.", "Select a medicine." },
                { "يرجى تعبئة بيانات العميل قبل الاعتماد.", "Complete the customer details before posting." },
                { "تم عرض أصناف المورد: {0}", "Supplier items displayed: {0}" },
                { "فاتورة ملغاة", "Cancelled invoice" },
                { "التحقق من المخزون", "Check stock" },
                { "تجهيز الفاتورة", "Prepare the invoice" },
                { "تحميل أسماء الأدوية", "Load medicine names" },
                { "تحميل الأدوية", "Load medicines" },
                { "تحميل شاشة المشتريات", "Load the purchase screen" },
                { "تعذر تحميل إجمالي المبيعات.", "Could not load total sales." },
                { "تعذر تحميل سجل العملاء.", "Could not load customer history." },
                { "تعذر تحميل عدد الشركات.", "Could not load the company count." },
                { "تعذر تحميل عدد الموظفين.", "Could not load the employee count." },
                { "تعذر تحميل قائمة الكاشيرين.", "Could not load the cashier list." },
                { "تعذر عرض ملخص العميل المحدد.", "Could not display the selected customer's summary." },
                { "حجز رقم فاتورة المشتريات", "Reserve a purchase invoice number" },
                { "قراءة المخزون", "Read stock" },
                { "تم إلغاء الفاتورة الحالية، لذلك لا يمكن {0} كفاتورة بيع معتمدة. افتح فاتورة جديدة.", "The current invoice was cancelled. Action unavailable for a posted sales invoice: {0}. Open a new invoice." },
                { "تم اعتماد الفاتورة {0} بنجاح، لكن تعذر {1} فقط. لا تعِد الحفظ؛ افتح السجل ثم أكمل الإجراء من الفاتورة المعتمدة.\n\nرمز الدعم: {2}", "Invoice {0} was posted successfully, but the following action failed: {1}. Do not save again; open the history and complete the action from the posted invoice.\n\nSupport code: {2}" },
                { "تفاصيل فاتورة المشتريات", "Purchase invoice details" },
                { "{0}  |  سعر: {1:N2}  |  كمية: {2:N3}  |  إجمالي: {3:N2}  |  إنتاج: {4}  |  انتهاء: {5}  |  وحدة: {6}", "{0}  |  Price: {1:N2}  |  Quantity: {2:N3}  |  Total: {3:N2}  |  Manufactured: {4}  |  Expires: {5}  |  Unit: {6}" },
                { "📋 المورد: {0}\n📅 التاريخ: {1}\n💰 الإجمالي: {2} {3}\n👤 الموظف: {4}\n\n--- بنود فاتورة المشتريات ---\n\n", "📋 Supplier: {0}\n📅 Date: {1}\n💰 Total: {2} {3}\n👤 Employee: {4}\n\n--- Purchase invoice items ---\n\n" },
                { "إجمالي بند البيع أكبر من الحد المسموح.", "The sales line total exceeds the allowed limit." },
                { "إجمالي بند الشراء أكبر من الحد المسموح.", "The purchase line total exceeds the allowed limit." },
                { "إشعار مرتجع", "Return credit note" },
                { "احفظ الفاتورة أولاً أو افتح فاتورة معتمدة.", "Save the invoice first or open a posted invoice." },
                { "اختر عميلاً مسجلاً أو أضفه من شاشة إدارة العملاء والموردين أولاً.", "Select a registered customer or add one in Customers and suppliers first." },
                { "اعتماد", "Post" },
                { "الرجاء اختيار دواء.", "Select a medicine." },
                { "الفاتورة التجارية لا تُلغى من هذه الشاشة. استخدم إشعار دائن أو مسار الإلغاء التجاري المعتمد.", "Commercial invoices cannot be cancelled from this screen. Use a credit note or the approved commercial cancellation flow." },
                { "تأكيد الخروج", "Confirm exit" },
                { "تأكيد المسح", "Confirm clearing" },
                { "تأكيد مسح الفاتورة", "Confirm clearing the invoice" },
                { "تعديل الكمية", "Change quantity" },
                { "تعذر التحقق من جميع بنود الفاتورة؛ الإلغاء محجوب حفاظاً على سلامة المخزون والحسابات.", "Could not verify all invoice items. Cancellation is blocked to protect inventory and accounts." },
                { "تعذر تحديد بنود الفاتورة الحالية لإلغائها.", "Could not identify the current invoice items for cancellation." },
                { "تعذر تحميل سجل العميل. راجع الاتصال ثم أعد المحاولة.", "Could not load the customer history. Check the connection and retry." },
                { "تعذر جلب سعر البيع للدواء المحدد.", "Could not retrieve the selling price of the selected medicine." },
                { "تم إلغاء هذه الفاتورة. افتح فاتورة جديدة؛ لا يمكن حفظها أو طباعتها كفاتورة بيع معتمدة.", "This invoice was cancelled. Open a new invoice; it cannot be saved or printed as a posted sales invoice." },
                { "تم اعتماد بيانات العميل بنجاح.", "Customer details were confirmed successfully." },
                { "تم اعتماد فاتورة المشتريات. افتح فاتورة جديدة قبل تعديل البنود.", "The purchase invoice was posted. Open a new invoice before changing items." },
                { "تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة قبل إضافة أصناف أخرى.", "The invoice is already saved. Open a new invoice before adding more items." },
                { "تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة قبل الحفظ مرة أخرى.", "The invoice is already saved. Open a new invoice before saving again." },
                { "تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة لتعديل السلة.", "The invoice is already saved. Open a new invoice to change the cart." },
                { "تم حفظ الفاتورة بالفعل. لا يمكن تعديل الخصم بعد الحفظ.", "The invoice is already saved. Its discount cannot be changed after saving." },
                { "تم فتح فاتورة بيع جديدة.", "A new sales invoice was opened." },
                { "تم فتح فاتورة مشتريات جديدة.", "A new purchase invoice was opened." },
                { "حدد عميلاً من السجل ثم اضغط عرض التفاصيل.", "Select a customer from the history, then choose View details." },
                { "حدد فاتورة بيع تجارية أولاً.", "Select a commercial sales invoice first." },
                { "حدد فاتورة حديثة من السجل أولاً.", "Select a recent invoice from the history first." },
                { "حدد فاتورة من سجل المشتريات ثم اضغط معاينة.", "Select an invoice from the purchase history, then choose Preview." },
                { "سيتم عكس الفاتورة الحالية فقط: إعادة المخزون وإنشاء قيد محاسبي معاكس.\nلن تُحذف السجلات الأصلية.\n\nهل تريد المتابعة؟", "Only the current invoice will be reversed: stock will be restored and a reversing journal entry created.\nOriginal records will be retained.\n\nContinue?" },
                { "هذا الدواء موجود بالفعل. هل تريد تعديل الكمية؟", "This medicine is already in the cart. Change its quantity?" },
                { "هذه الفاتورة ملغاة مسبقاً، ولا يمكن تكرار الإلغاء.", "This invoice is already cancelled and cannot be cancelled again." },
                { "هل أنت متأكد أنك تريد الخروج؟ سيتم إلغاء سلة البيع غير المحفوظة.", "Exit? The unsaved sales cart will be discarded." },
                { "هل تريد حذف هذا الصنف من القائمة؟", "Remove this item from the list?" },
                { "هل تريد مسح الفاتورة الحالية؟ لم يتم تعديل المخزون قبل الحفظ.", "Clear the current invoice? Stock has not been changed before saving." },
                { "هل تريد مسح جميع البنود؟", "Clear all items?" },
                { "📋 العميل: {0}\n📅 آخر تحديث: {1}\n💰 إجمالي الإنفاق: {2}\n👤 الكاشير: {3}\n\n--- جميع بنود العميل المسجلة تاريخياً ---\n\n", "📋 Customer: {0}\n📅 Last updated: {1}\n💰 Total spending: {2}\n👤 Cashier: {3}\n\n--- All recorded customer items ---\n\n" },
                { "📋 فاتورة: {0}\n📅 التاريخ: {1}\n💰 الإجمالي: {2}\n👤 الكاشير: {3}\nالحالة: {4}\n\n--- بنود هذه الفاتورة فقط ---\n\n", "📋 Invoice: {0}\n📅 Date: {1}\n💰 Total: {2}\n👤 Cashier: {3}\nStatus: {4}\n\n--- Items from this invoice only ---\n\n" },
                { "تعذر تحميل بنود الفاتورة المعتمدة.\n\n{0}", "Could not load the posted invoice items.\n\n{0}" },
                { "فاتورة رقم: {0}\nالعميل: {1}\nتاريخ الإصدار: {2}\nالإجمالي: {3}\nالكاشير: {4}\n\n--- بنود الفاتورة ---\n\n", "Invoice no.: {0}\nCustomer: {1}\nIssue date: {2}\nTotal: {3}\nCashier: {4}\n\n--- Invoice items ---\n\n" },
                { "{0}  |  سعر: {1:N2}  |  كمية: {2:N3}  |  إجمالي: {3:N2}  |  وحدة: {4}", "{0}  |  Price: {1:N2}  |  Quantity: {2:N3}  |  Total: {3:N2}  |  Unit: {4}" },
                { "{0}\n\nرمز الدعم: {1}", "{0}\n\nSupport code: {1}" },
                { "تفاصيل فاتورة البيع", "Sales invoice details" },
                { "تفاصيل الفاتورة", "Invoice details" },
                { "سجل العميل", "Customer history" },
                { "لا توجد تفاصيل متاحة.", "No details are available." },
                { "لا توجد بنود متاحة لهذه الفاتورة.", "No items are available for this invoice." },
                { "اختر مفتاحًا مختلفًا لكل إجراء.", "Choose a different key for each action." },
                { "البحث عن صنف", "Search items" },
                { "حقل الصنف (الاسم أو الباركود)", "Item field (name or barcode)" },
                { "الانتقال إلى الكمية", "Focus quantity" },
                { "بيع وطباعة", "Sell and print" },
                { "يبقى Ctrl+K متاحًا للبحث. تطبق الإعدادات على شاشة بيع جديدة لهذا المستخدم.", "Ctrl+K remains available for search. Settings apply to a new sales window for this user." },
                { "الافتراضي", "Defaults" },
                { "حفظ اختصارات البيع", "Save sales shortcuts" },
                { "استعادة الاختصارات الافتراضية", "Restore default shortcuts" },
                { "استعدت القيم الافتراضية. اختر حفظ لتطبيقها.", "Defaults restored. Choose Save to apply." },
                { "تعذر قراءة الإعدادات. راجع الاختصارات ثم احفظ لاستبدال الملف.", "Could not read settings. Review the shortcuts, then save to replace the file." },
                { "معاينة فقط؛ لا تُحفظ إعدادات.", "Preview only; settings are not saved." },
                { "تغيرت الجلسة. أغلق النافذة وافتحها من الحساب الحالي.", "The session changed. Close this window and reopen it from the current account." },
                { "اختر مفتاحًا مختلفًا لكل إجراء؛ الاختصارات المتكررة غير مقبولة.", "Choose a different key for each action; duplicate shortcuts are not allowed." },
                { "تم حفظ الاختصارات. افتح شاشة بيع جديدة لتطبيقها.", "Shortcuts saved. Open a new sales window to apply them." },
                { "تعذر حفظ الاختصارات. بقيت الاختصارات السابقة؛ حاول مجددًا.", "Could not save shortcuts. Previous shortcuts remain; try again." },
                { "رصيد العميل المتاح: {0}", "Available customer credit: {0}" },
                { "سيتم إلغاء الفاتورة رقم {0} فقط، وإعادة مخزونها وإنشاء قيد محاسبي معاكس.\nلن تُحذف السجلات الأصلية.\n\nهل تريد المتابعة؟", "Only invoice {0} will be cancelled. Its stock will be restored and a reversing journal entry created.\nOriginal records will be retained.\n\nContinue?" },
                { "تم إلغاء الفاتورة {0} وإعادة {1} وحدة إلى المخزون.", "Invoice {0} was cancelled and {1} units returned to stock." },
                { "تم الإلغاء بأمان. المبلغ المعكوس: {0}", "Cancelled safely. Reversed amount: {0}" },
                { "تم الحفظ بنجاح.\nمرجع البيع: {0}", "Saved successfully.\nSales reference: {0}" },
                { "تم إلغاء الفاتورة وإعادة {0} وحدة إلى المخزون.", "The invoice was cancelled and {0} units returned to stock." },
                { "تم إلغاء الفاتورة بأمان.\nالمرجع: {0}\nالمبلغ المعكوس: {1}\nالكمية المعادة: {2}", "The invoice was cancelled safely.\nReference: {0}\nReversed amount: {1}\nReturned quantity: {2}" },
                { "تم الإلغاء", "Cancelled" },
                { "تم", "Done" },
                { "تأكيد إلغاء الفاتورة", "Confirm invoice cancellation" },
                { "قيد افتتاحي رقم {0}", "Opening entry no. {0}" },
                { "قيد يومية رقم {0}", "Journal entry no. {0}" },
                { "مدين محلي", "Local debit" },
                { "دائن محلي", "Local credit" },
                { "تعذر حفظ تفضيل اللغة. بقي الإعداد السابق؛ أعد المحاولة.", "Could not save the language preference. The previous setting was retained; retry." },
                { "اسم الملف أو مساره أطول من المسموح. اختر اسمًا أقصر داخل مجلد قريب.", "The file name or path is too long. Choose a shorter name in a nearby folder." },
                { "المجلد أو القرص المختار غير متاح. تحقق من الاتصال أو اختر مجلدًا آخر.", "The selected folder or drive is unavailable. Check the connection or choose another folder." },
                { "تعذر الكتابة في المجلد المختار. اختر مجلدًا يمكنك الحفظ فيه أو اسم ملف آخر.", "Cannot write to the selected folder. Choose a folder you can write to or another file name." },
                { "تعذر حفظ الملف لأنه مستخدم في برنامج آخر. أغلقه في Excel أو قارئ PDF، أو اختر اسمًا آخر، ثم أعد المحاولة.", "The file is in use by another program. Close it in Excel or your PDF reader, or choose another name, then retry." },
                { "المساحة المتاحة لا تكفي لحفظ الملف. وفر مساحة أو اختر قرصًا آخر.", "There is not enough space to save the file. Free some space or choose another drive." },
                { "تعذر تأكيد حفظ الملف. تحقق من الملف والمسار قبل إعادة المحاولة.", "Could not confirm that the file was saved. Please check the file and path before retrying." },
                { "إذا تكرر الأمر، أرسل رمز الدعم إلى مسؤول النظام: {0}", "If the issue persists, send this support code to your administrator: {0}" },
                { "موافق", "OK" },
                { "نعم", "Yes" },
                { "لا", "No" },
                { "إعادة المحاولة", "Retry" },
                { "إيقاف", "Abort" },
                { "تجاهل", "Ignore" },
                { "خطأ", "Error" },
                { "تحذير", "Warning" },
                { "تأكيد", "Confirmation" },
                { "رمز الدعم", "Support code" },
                { "نسخ الرمز", "Copy code" },
                { "لم يتم الحفظ: {0}", "Not saved: {0}" },
                { "راجع بيانات الحركة أو الفترة المحاسبية ثم أعد المحاولة.", "Review the movement details or accounting period, then retry." },
                { "تعذر إكمال عملية المخزون؛ راجع حالة الحركة قبل إعادة المحاولة.", "Could not complete the inventory operation; review the movement status before retrying." },
                { "مرجع الحركة", "Movement reference" },
                { "نوع حركة المخزون غير صالح.", "The inventory movement type is invalid." },
                { "مخزون أول المدة لا يمكن أن يكون أقل من صفر.", "Opening stock cannot be negative." },
                { "الكمية الفعلية لا يمكن أن تكون أقل من صفر.", "The actual quantity cannot be negative." },
                { "كمية التوريد يجب أن تكون أكبر من صفر.", "Receipt quantity must be greater than zero." },
                { "كمية الصرف يجب أن تكون أكبر من صفر.", "Issue quantity must be greater than zero." },
                { "مبلغ الوحدة غير صالح أو يتجاوز حد التخزين.", "The unit amount is invalid or exceeds the storage limit." },
                { "مرجع الحركة مطلوب.", "A movement reference is required." },
                { "مرجع الحركة يتجاوز حد التخزين.", "The movement reference exceeds the storage limit." },
                { "اختر الصنف أولاً.", "Select a medicine first." },
                { "اسم الصنف يتجاوز حد التخزين.", "The medicine name exceeds the storage limit." },
                { "سبب الحركة يجب ألا يتجاوز 500 حرف.", "The movement reason must not exceed 500 characters." },
                { "مرجع حركة المخزون غير صالح.", "The inventory movement reference is invalid." },
                { "لا تملك صلاحية تنفيذ حركة المخزون.", "You do not have permission to post inventory movements." },
                { "تغيرت جلسة الحركة؛ أعد فتحها قبل الاعتماد.", "The movement session changed; reopen it before posting." },
                { "استعادة الحركة غير جاهزة؛ لم ترسل محاولة حفظ.", "Movement recovery is not ready; no save attempt was sent." },
                { "سياق ملف الحركة مختلف.", "The movement file context differs." },
                { "يوجد طلب غير مؤكد؛ لا يمكن استبداله بحركة أخرى.", "An unconfirmed request exists; it cannot be replaced by another movement." },
                { "مرجع الحركة مستخدم لبيانات مختلفة أو سجل غير متسق؛ راجع الحركة المحفوظة.", "The movement reference belongs to different data or inconsistent records; review the saved movement." },
                { "سجل حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.", "The inventory movement record is inconsistent; a retry cannot be confirmed." },
                { "سجل اعتماد حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.", "The inventory posting record is inconsistent; a retry cannot be confirmed." },
                { "تعذر تأكيد حركة المخزون الآن؛ أعد محاولة الطلب نفسه.", "The inventory movement could not be confirmed; retry the same request." },
                { "تعذر تحديث رصيد الصنف.", "Could not update the medicine stock balance." },
                { "وضع البيع التجاري مطلوب؛ لا يمكن إجراء تسوية أو توريد أو صرف يدوي على الرصيد المجمع. استخدم عملية دفعات موثقة حتى لا تنفصل كمية الصنف عن التشغيلة.", "Full commercial sales mode is enabled. Manual adjustments, receipts or issues against the aggregate stock balance are unavailable. Use a documented lot operation so medicine and lot quantities stay consistent." },
                { "حفظ التقرير PDF", "Save report as PDF" },
                { "حفظ الفاتورة PDF", "Save invoice as PDF" },
                { "ملف PDF (*.pdf)|*.pdf", "PDF file (*.pdf)|*.pdf" },
                { "حفظ نسخة PDF من التقرير المعروض", "Save a PDF copy of the displayed report" },
                { "جارٍ حفظ PDF...", "Saving PDF..." },
                { "تم حفظ ملف PDF بنجاح.", "The PDF file was saved successfully." },
                { "اختر ملف PDF بامتداد pdf.", "Choose a PDF file with the pdf extension." },
                { "تعذر تأكيد اكتمال ملف PDF؛ لم يُستبدل الملف السابق.", "The PDF output did not complete. The previous file was retained." },
                { "لا يمكن حفظ PDF لأن طابعة Microsoft Print to PDF غير مفعلة في ويندوز.\nفعّلها من ميزات ويندوز أو اختر طباعة A4 ثم اختر طابعة PDF يدوياً.", "Microsoft Print to PDF is unavailable. Enable it in Windows features, or use A4 printing and select a PDF printer." },
                { "نسخ التفاصيل", "Copy details" },
                { "تم النسخ", "Copied" },
                { "تعذر النسخ", "Copy failed" },
                { "تسهيلات الاستخدام الافتراضية", "Default accessibility" },
                { "فتح الشاشات كامل أو في وسط الصفحة", "Open screens maximized or centered" },
                { "فتح في وسط الصفحة", "Open centered" },
                { "فتح كامل", "Open maximized" },
                { "اختر طريقة فتح الشاشات الجديدة.", "Choose how new screens open." },
                { "يطبق الاختيار عند فتح الشاشات؛ تبقى الحوارات الصغيرة بحجمها المناسب.", "Applies to newly opened screens; small dialogs retain their appropriate size." },
                { "حالة حفظ تسهيلات الاستخدام", "Accessibility settings save status" },
                { "حفظ تسهيلات الاستخدام الافتراضية", "Save default accessibility settings" },
                { "إغلاق دون حفظ", "Close without saving" },
                { "تم الحفظ؛ سيطبق الاختيار عند فتح الشاشة التالية.", "Saved. The next screen will use your choice." },
                { "تعذر حفظ الإعداد. لم يتغير الاختيار المحفوظ؛ حاول مرة أخرى.", "Could not save. Your saved choice was retained; please try again." },
                { "تعذر تأكيد حفظ الإعداد؛ حاول مرة أخرى.", "Could not confirm the setting was saved. Please try again." },
                { "إلغاء فاتورة بيع", "Sales invoice cancellation" },
                { "إلغاء فاتورة شراء", "Purchase invoice cancellation" },
                { "أدخل كمية موجبة صالحة.", "Enter a valid positive quantity." },
                { "أدخل كمية صحيحة للصنف بلا وحدات.", "Enter a whole-number quantity for an item without configured units." },
                { "إجمالي البند أكبر من الحد المسموح.", "The line total exceeds the allowed limit." },
                { "مجموع كميات الصنف يتجاوز المخزون المتاح.", "The item's total quantity exceeds available stock." },
                { "كميات الدفعات بوحدة المخزون الأساسية؛ الوحدة الأصلية للفاتورة موضحة. أدخل أعدادًا صحيحة، وصفر لاستبعاد الدفعة.", "Lot quantities use the base stock unit; the original invoice unit is shown. Enter whole numbers, or zero to exclude a lot." },
                { "معامل الوحدة الأصلية غير صالح.", "The original unit conversion factor is invalid." },
                { "معامل الوحدة الأصلية غير متسق بين الدفعات.", "The original unit conversion factor differs between lots." },
                { "أدخل سعرًا أو كمية موجبة صالحة.", "Enter a valid positive price or quantity." },
                { "الصنف بلا وحدات محفوظة؛ أدخل سعرًا وكمية صحيحين.", "This item has no saved units; enter a whole-number price and quantity." },
                { "إجمالي الفاتورة أكبر من الحد المسموح.", "The invoice total exceeds the allowed limit." },
                { "جاهز", "Built-in" },
                { "القوالب الجاهزة والمحفوظة", "Built-in and saved templates" },
                { "قوالب التقارير الجاهزة والمحفوظة", "Built-in and saved report templates" },
                { "القالب الجاهز لا يُحذف؛ يمكنك حفظ إعداداته باسم جديد.", "Built-in templates cannot be deleted. Save your changes under a new name." },
                { "طريقة العرض", "View mode" },
                { "طريقة عرض التقرير", "Report view mode" },
                { "مختصر مجمّع", "Grouped summary" },
                { "تحليلي — حركة بحركة", "Analytical — each movement" },
                { "المختصر يجمع حسب الحساب أو الصنف؛ التحليلي يعرض كل حركة.", "Summary groups by account or item; analytical shows each movement." },
                { "قائمة الأصناف", "Item list" },
                { "مؤشرات مجمعة أصلًا", "Already aggregated indicators" },
                { "هذا المصدر قائمة أو مؤشرات مجمعة أصلًا؛ اختر مصدر حركات لتغيير طريقة العرض.", "This source is a list or aggregated indicators. Select a movement source to change its view mode." },
                { "المجاميع منفصلة حسب العملة والحالة ونوع الفاتورة؛ ليست صافي المبيعات.", "Totals are separate by currency, status and invoice type; they are not net sales." },
                { "المبالغ حسب العملة المسجلة؛ العملة غير المحددة لا تعني عملة النظام الحالية.", "Amounts use their recorded currency. Unspecified currency does not mean the current system currency." },
                { "الإجمالي مجموع السطور؛ لا يكرر إجمالي رأس الفاتورة.", "Total is the sum of lines; the invoice header total is not repeated." },
                { "تم تطبيق حد العرض الآمن؛ المخرجات تشمل المعروض فقط.", "The safe display limit was applied; outputs include displayed rows only." },
                { "مصدر هذا القالب لا يدعم العرض المجمع.", "This template source does not support a grouped view." },
                { "طريقة عرض التقرير غير مسموحة.", "Report view mode is not allowed." },
                { "هذا المصدر يعرض قائمة أو مؤشرات مجمعة أصلًا؛ اختر مصدر حركات للتقرير المختصر.", "This source shows a list or aggregated indicators. Select a movement source for a summary report." },
                { "كثافة عادية", "Standard density" },
                { "كثافة مدمجة", "Compact density" },
                { "عدد السطور", "Line count" },
                { "إجمالي السطور", "Line total" },
                { "صافي الكمية", "Net quantity" },
                { "نوع الفاتورة", "Invoice type" },
                { "عادي", "Windowed" },
                { "جارٍ حفظ الفاتورة.", "Saving the invoice." },
                { "تحقق من نتيجة الحفظ أولًا.", "Check the save result first." },
                { "ابدأ فاتورة جديدة لاستخدام الرصيد.", "Start a new invoice to use credit." },
                { "جارٍ تحميل رصيد العميل…", "Loading customer credit…" },
                { "اختر عميلًا مسجلًا لاستخدام رصيده.", "Select a saved customer to use credit." },
                { "أعد اختيار العميل للتحقق من الرصيد.", "Select the customer again to check credit." },
                { "لا يوجد رصيد متاح للاستخدام.", "No credit is available." },
                { "يمكن استخدام الرصيد في هذه الفاتورة.", "Credit is available for this invoice." },
                { "حالة استخدام رصيد العميل", "Customer credit availability" },
                { "الكمية الأساسية", "Base quantity" },
                { "أساس السعر (وحدات أساسية)", "Price basis (base units)" },
                { "أدخل الكمية، ثم Enter للإضافة.", "Enter a quantity, then press Enter to add." },
                { "أدخل عدداً صحيحاً موجباً للكمية.", "Enter a positive whole-number quantity." },
                { "سيُتحقق من الرصيد عند الإضافة إلى السلة.", "Stock will be checked when adding to the cart." },
                { "الكمية تتجاوز آخر رصيد معروض: {0}. عدّلها قبل الإضافة.", "Quantity exceeds the last displayed stock: {0}. Adjust it before adding." },
                { "آخر رصيد معروض: {0} وحدة — Enter للإضافة.", "Last displayed stock: {0} units — Enter to add." },
                { "اسم العميل: {0}", "Customer name: {0}" },
                { "هاتف العميل: {0}", "Customer phone: {0}" },
                { "عنوان العميل: {0}", "Customer address: {0}" },
                { "رقم العميل: {0}", "Customer ID: {0}" },
                { "رقم العميل: عام", "Customer ID: General" },
                { "رقم العميل: جديد", "Customer ID: New" },
                { "نوع العميل: {0}", "Customer type: {0}" },
                { "الكمية الحالية", "Current quantity" },
                { "سعر التكلفة", "Cost price" },
                { "سعر البيع التقديري", "Estimated selling price" },
                { "رقم التشغيلة", "Lot number" },
                { "الكمية المستلمة", "Received quantity" },
                { "الكمية القابلة للصرف", "Available quantity" },
                { "حالة التشغيلة", "Lot status" },
                { "معرف التشغيلة", "Lot ID" },
                { "آخر حركة", "Last movement" },
                { "الوارد خلال الفترة", "Received during period" },
                { "الصادر خلال الفترة", "Issued during period" },
                { "معدل الدوران التقريبي", "Estimated turnover" },
                { "آخر صرف", "Last issue" },
                { "التصنيف", "Category" },
                { "نوع التنبيه", "Alert type" },
                { "راكد", "Inactive" },
                { "منخفض", "Low" },
                { "نشط", "Active" },
                { "بدون حركة", "No movement" },
                { "نفد الرصيد", "Out of stock" },
                { "طبيعي", "Normal" },
                { "مسحوبة", "Recalled" },
                { "نفدت", "Depleted" },
                { "منتهية", "Expired" },
                { "قريبة الانتهاء", "Expiring soon" },
                { "متاحة", "Available" },
                { "الرصيد الرئيسي", "Main stock" },
                { "الصنف منتهي الصلاحية ولا يجب صرفه.", "This item has expired and must not be dispensed." },
                { "الصنف يقترب من تاريخ الانتهاء ضمن فترة التنبيه.", "This item expires within the alert period." },
                { "الكمية الحالية عند حد إعادة الطلب أو أقل منه.", "Current quantity is at or below the reorder threshold." },
                { "عميل عام", "General customer" },
                { "عميل مسجل", "Registered customer" },
                { "عميل غير مسجل", "Unregistered customer" },
                { "يعرض نوع العميل فقط. اضغط لاختيار سجل محفوظ من شاشة العملاء والموردين. النوع الحالي: {0}", "Shows the customer type. Click to select a saved record from Customers and suppliers. Current type: {0}" },
                { "هذا الصنف موجود في السلة بكمية {0}. هل تريد استبدالها بكمية {1} بسعر الوحدة الحالي {2}؟", "This item is already in the cart with quantity {0}. Replace it with quantity {1} at the current unit price of {2}?" },
                { "تعديل الصنف المكرر", "Replace existing cart item" },
                { "لا يمكن بيع {0} لأن صلاحيته منتهية بتاريخ {1}.", "Cannot sell {0}; it expired on {1}." },
                { "تنبيه: {0} تنتهي صلاحيته بتاريخ {1}. هل تريد المتابعة؟", "Warning: {0} expires on {1}. Continue?" },
                { "قرب انتهاء الصلاحية", "Expiring soon" },
                { "التاريخ: {0}", "Date: {0}" },
                { "اسم الكاشير: {0}", "Cashier name: {0}" },
                { "رقم الكاشير: {0}", "Cashier ID: {0}" },
                { "رقم الكاشير:", "Cashier ID:" },
                { "الكاشير غير محدد.", "Cashier not selected." },
                { "الأكثر بيعًا", "Most sold" },
                { "اختياراتي المتكررة", "My frequent additions" },
                { "نوع الاقتراحات", "Suggestion source" },
                { "مرات الإضافة", "Times added" },
                { "جارٍ تحميل اختياراتك…", "Loading your frequent additions…" },
                { "لا توجد اختيارات متكررة متاحة. اكتب للبحث عن أي صنف.", "No frequent additions available. Type to search any item." },
                { "اختياراتك المتكررة؛ الرصيد والسعر الحاليان. تحقق الصلاحية عند الإضافة.", "Your frequent additions; current stock and price. Check expiry when adding." },
                { "غير محدد", "Unspecified" },
                { "اختصارات البيع", "Sales shortcuts" },
                { "تخصيص مفاتيح البحث والباركود والكمية ومعاينة الفاتورة", "Customize keys for search, barcode, quantity and invoice preview" },
                { "لا توجد بنود في القائمة.", "There are no items in the list." },
                { "يرجى اختيار مورد.", "Please select a supplier." },
                { "لا توجد بيانات للطباعة.", "There is no data to print." },
                { "لا توجد بيانات لحفظ PDF.", "There is no data to save as PDF." },
                { "تم حفظ هذه المشتريات بالفعل.", "These purchases have already been saved." },
                { "تم حفظ المشتريات بنجاح.\nرقم الفاتورة: {0}", "Purchases saved successfully.\nInvoice number: {0}" },
                { "تم حفظ واعتماد المشتريات بنجاح.\nرقم الفاتورة: {0}", "Purchases saved and approved successfully.\nInvoice number: {0}" },
                { "معاينة طباعة إيصال المشتريات", "Purchase receipt print preview" },
                { "معاينة طباعة فاتورة المشتريات A4", "A4 purchase invoice print preview" },
                { "تم اعتماد فاتورة المشتريات {0} بنجاح، لكن تعذر {1} فقط. لا تعِد الحفظ؛ افتح السجل ثم أعد {1}.\n\nرمز الدعم: {2}", "Purchase invoice {0} was approved successfully, but {1} failed. Do not save again; open history and retry {1}.\n\nSupport code: {2}" },
                { "الفاتورة معتمدة", "Invoice approved" },
                { "الطباعة", "printing" },
                { "تم الحفظ {0} — جارٍ فحص المخزون والصلاحية…", "Saved {0} — Checking stock and expiry…" },
                { "تم الحفظ — لا تنبيهات لأصناف الفاتورة.", "Saved — No alerts for the invoice items." },
                { "تم الحفظ — مخزون منخفض: {0}، قريب الانتهاء: {1}، منتهي: {2}. التفاصيل في تنبيهات المخزون.", "Saved — Low stock: {0}, expiring soon: {1}, expired: {2}. See inventory alerts for details." },
                { "تم حفظ الفاتورة؛ افتح فاتورة جديدة قبل الحفظ مرة أخرى.", "The invoice was saved. Open a new invoice before saving again." },
                { "تم حفظ الفاتورة؛ تعذر تحديث تنبيهات المخزون. رمز الدعم: {0}", "The invoice was saved; inventory alerts could not be updated. Support code: {0}" },
                { "جاري حفظ المشتريات حالياً. الرجاء الانتظار.", "Purchases are being saved. Please wait." },
                { "توجد محاولة حفظ غير محسومة؛ تحقق من نتيجتها قبل مسح الفاتورة.", "A save attempt is unresolved. Check its result before clearing the invoice." },
                { "نتيجة الحفظ غير محسومة؛ أعد فتح الشاشة للتحقق. احتُفظ بالمحاولة.", "The save result is unresolved. Reopen this screen to check it. The attempt was retained." },
                { "فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض.", "This attempt was posted and its invoice was cancelled. Clear the view before starting a new invoice." },
                { "تغيرت مسودة المشتريات أثناء الحفظ؛ احتُفظ بالمحاولة للتحقق.", "The purchase draft changed while saving. The attempt was retained for checking." },
                { "تغير وضع مخزون الدفعات عن محاولة الحفظ المحفوظة؛ احتُفظ بها للتحقق.", "Lot tracking differs from the saved attempt. The attempt was retained for checking." },
                { "لم تثبت نتيجة الحفظ. اضغط حفظ لإعادة المحاولة نفسها.", "The save result is not confirmed. Press Save to retry the same attempt." },
                { "تم تنفيذ المحاولة ثم إلغاء الفاتورة.", "The attempt was posted and its invoice was later cancelled." },
                { "تم التحقق من حفظ الفاتورة واستعادة نتيجتها.", "The saved invoice was verified and its result restored." },
                { "نتيجة الحفظ غير محسومة. ستبقى المحاولة محفوظة للتحقق عند إعادة فتح الشاشة. هل تريد الخروج؟", "The save result is unresolved. The attempt will remain saved for checking when this screen reopens. Do you want to exit?" },
                { "تغيرت جلسة استعادة المشتريات؛ أعد فتح الشاشة.", "The purchase recovery session changed. Reopen this screen." },
                { "توجد محاولة مشتريات معلقة ببيانات مختلفة؛ تحقق من نتيجتها قبل بدء فاتورة جديدة.", "A pending purchase attempt has different details. Check its result before starting a new invoice." },
                { "لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة.", "The displayed attempt result could not be verified. The recovery file was retained." },
                { "تم تنفيذ محاولة المشتريات ثم إلغاء الفاتورة؛ لا يمكن إعادة اعتمادها.", "The purchase attempt was posted and its invoice was cancelled. It cannot be posted again." },
                { "لم تثبت نتيجة اعتماد المشتريات؛ تحقق من المحاولة المحفوظة قبل إعادة الإرسال.", "The purchase posting result is unresolved. Check the saved attempt before resubmitting." },
                { "تغيرت جلسة حفظ المشتريات.", "The purchase save session changed." },
                { "تغير بلد سياسة الضريبة أثناء الحفظ؛ لم تعتمد الفاتورة.", "The tax policy country changed while saving. The invoice was not posted." },
                { "تحقق من الحفظ", "Check saved result" },
                { "إغلاق المحاولة", "Close attempt" },
                { "إعادة نفس الحفظ", "Retry same save" },
                { "تعذر فتح استعادة العميل. أعد التحديث؛ لم يُرسل حفظ جديد.", "Could not open customer recovery. Refresh; no new save was sent." },
                { "لم يظهر إثبات للحفظ. أعد نفس المحاولة أو أغلقها قبل إدخال عميل آخر.", "No save receipt was found. Retry the same attempt or close it before entering another customer." },
                { "إثبات المحاولة مختلف. لم تُحفظ هذه البيانات؛ تحقق قبل إجراء جديد.", "The attempt receipt differs. These details were not saved; check before a new action." },
                { "نتيجة حفظ العميل غير مؤكدة. تحقق من الحفظ قبل إجراء جديد.", "The customer save result is uncertain. Check it before a new action." },
                { "تعذر تجهيز حفظ العميل. راجع البيانات والاستعادة؛ لم يُرسل طلب.", "Could not prepare customer save. Check details and recovery; no request was sent." },
                { "جارٍ حفظ بيانات العميل…", "Saving customer details…" },
                { "لم يُحفظ العميل. راجع البيانات أو تهيئة قاعدة البيانات ثم أعد المحاولة.", "Customer was not saved. Check details or database setup, then retry." },
                { "جارٍ التحقق من محاولة حفظ العميل…", "Checking the customer save attempt…" },
                { "ثبت حفظ المحاولة سابقًا وتغير سجل العميل لاحقًا. راجع بيانات القائمة الحالية.", "The attempt was saved earlier and the customer record changed later. Review current list details." },
                { "أُغلقت المحاولة دون حفظ عميل جديد. يمكنك إدخال بيانات جديدة.", "The attempt was closed without saving a new customer. You can enter new details." },
                { "جارٍ تحميل بيانات العميل…", "Loading customer details…" },
                { "جارٍ قراءة رصيد العميل…", "Reading customer credit…" },
                { "تم تحميل بيانات العميل ورصيده.", "Customer details and credit loaded." },
                { "العميل غير موجود. أعد اختياره أو أضفه من شاشة العملاء.", "Customer not found. Choose it again or add it in Customers." },
                { "تم اختيار العميل العام.", "General customer selected." },
                { "تعذر تحميل العميل. أعد اختياره.", "Could not load the customer. Choose it again." },
                { "تعذر قراءة رصيد العميل. أعد اختيار العميل.", "Could not read customer credit. Choose the customer again." },
                { "تعذر تحميل قائمة العملاء. أعد التحديث.", "Could not load customers. Refresh again." },
                { "انتظر اكتمال بيانات العميل ورصيده أو أعد اختيار العميل.", "Wait for customer details and credit, or choose the customer again." },
                { "تسجيل الخروج", "Sign out" },
                { "هذا الباركود مرتبط بأكثر من صنف. صحح الباركود من إدارة الأدوية أو اختر الصنف بالاسم؛ لم يتم اختيار أي صنف تلقائياً.", "This barcode belongs to multiple medicines. Correct it in Medicine management or choose a medicine by name; no medicine was selected automatically." },
                { "الباركود مكرر. اختر الصنف بالاسم أو صححه في إدارة الأدوية.", "Duplicate barcode. Choose by name or correct it in Medicines." },
                { "تعذر البحث عن الصنف. أعد المحاولة.", "Could not search for the item. Try again." },
                { "تغيرت جلسة المستخدم؛ أعد تحميل التقرير.", "The user session changed; reload the report." },
                { "تغيرت المرشحات؛ أعد عرض التقرير.", "Filters changed; reload the report." },
                { "تعذر تحميل التقرير؛ أعد المحاولة.", "Could not load the report; try again." },
                { "جار تحميل خيارات التقرير...", "Loading report options..." },
                { "تم تحميل التقرير.", "Report loaded." },
                { "أعد تحميل خيارات التقرير قبل متابعة العملية.", "Reload report options before continuing." },
                { "لا توجد بيانات صالحة للطباعة أو التصدير؛ أعد عرض التقرير.", "No valid data to print or export; reload the report." },
                { "تغير سياق العملية؛ أعد المحاولة.", "The operation context changed; try again." },
                { "عملة غير متاحة", "Currency unavailable" },
                { "عملة غير محفوظة", "Currency not recorded" },
                { "جارٍ التحقق من عملة الفاتورة…", "Checking invoice currency…" },
                { "تم التحقق من العملة؛ تبقى ثابتة لهذه المسودة.", "Currency verified; it stays fixed for this draft." },
                { "عملة النظام غير محددة. راجع الإعدادات ثم اضغط تحديث.", "System currency is not configured. Check Settings, then Refresh." },
                { "قراءة عملة الفاتورة", "Reading invoice currency" },
                { "عملة الفاتورة غير متاحة. اضغط تحديث وانتظر التحقق قبل الحفظ.", "Invoice currency is unavailable. Refresh and wait for verification before saving." },
                { "تغيرت عملة النظام منذ إعداد المسودة. افتح فاتورة جديدة بالعملة الصحيحة؛ لا يحدث تحويل تلقائي.", "System currency changed after this draft was prepared. Start a new invoice with the correct currency; no automatic conversion occurs." },
                { "الباركود يتجاوز 100 حرف. راجع القيمة ثم أعد المسح.", "Barcode exceeds 100 characters. Check it and scan again." },
                { "الباركود غير موجود.", "Barcode not found." },
                { "البحث بالباركود", "Barcode search" },
                { "جارٍ البحث بالباركود…", "Searching barcode…" },
                { "تم تغيير الباركود. اضغط Enter للبحث.", "Barcode changed. Press Enter to search." },
                { "الصنف غير موجود في القائمة الحالية. حدّث الأصناف ثم أعد المسح.", "This medicine is missing from the current list. Refresh medicines and scan again." },
                { "تعذر تأكيد الحفظ؛ أعد المحاولة بنفس بيانات الحركة.", "Save could not be confirmed; retry with the same movement details." },
                { "نتيجة الحركة السابقة غير مؤكدة؛ استعد بياناتها وأعد المحاولة قبل اعتماد حركة مختلفة.", "The previous movement is unconfirmed; restore its details and retry before posting a different movement." },
                { "مرجع الحركة:", "Movement reference: " },
                { "تعذر فتح استعادة الحركة؛ الاعتماد غير متاح.", "Movement recovery is unavailable; posting is disabled." },
                { "تم تحميل الرصيد والحركات؛ راجع التفاصيل قبل الاعتماد.", "Balance and movements loaded; review the details before posting." },
                { "الصنف المستعاد غير متاح؛ راجع الحركة قبل الاعتماد.", "The restored medicine is unavailable; review the movement before posting." },
                { "استعيد طلب غير مؤكد؛ راجع التفاصيل ثم اعتمد للتأكيد.", "An unconfirmed request was restored; review its details and post to confirm." },
                { "رفض الحفظ مؤكد؛ تعذر تحديث ملف الاستعادة.", "Posting was rejected; the recovery file could not be updated." },
                { "الكل", "All" },
                { "غير متاح", "Unavailable" },
                { "جار تحميل الرصيد...", "Loading account balance..." },
                { "اختر الحساب لعرض رصيده.", "Select an account to view its balance." },
                { "تاريخ السند غير صحيح؛ الرصيد غير متاح.", "The voucher date is invalid; the balance is unavailable." },
                { "تعذر تحميل الرصيد؛ أعد اختيار الحساب للمحاولة مجددًا.", "Unable to load the balance; select the account again to retry." },
                { "خروج", "Exit" },
                { "الميزانية العمومية", "Balance sheet" },
                { "أرباح وخسائر", "Profit and loss" },
                { "اليومية العامة", "General journal" },
                { "ضريبة", "Tax" },
                { "مراكز التكلفة", "Cost centers" },
                { "مركز التكلفة", "Cost center" },
                { "تقرير الضريبة", "Tax report" },
                { "تقرير نظام الحسابات", "Accounting report" },
                { "تقرير غير معروف", "Unknown report" },
                { "تضمين الرصيد الافتتاحي", "Include opening balance" },
                { "استثناء المخزون من الأرباح والخسائر", "Exclude inventory from profit and loss" },
                { "حتى تاريخ", "As of date" },
                { "اختر تاريخًا صالحًا.", "Choose a valid date." },
                { "قوالب التقارير المحفوظة", "Saved report templates" },
                { "نوع التقرير المحدد", "Selected report type" },
                { "تبديل نوع التقرير", "Switch report type" },
                { "إجراء تقرير الحسابات", "Accounting report action" },
                { "إجراء قالب التقرير", "Report template action" },
                { "مدين", "Debit" },
                { "دائن", "Credit" },
                { "الحساب", "Account" },
                { "اسم الحساب", "Account name" },
                { "رقم الحساب", "Account number" },
                { "المستند", "Document" },
                { "رقم المستند", "Document number" },
                { "نوع المستند", "Document type" },
                { "نوع العملية", "Operation type" },
                { "الرصيد", "Balance" },
                { "الرصيد الصافي", "Net balance" },
                { "الرصيد الافتتاحي", "Opening balance" },
                { "الرصيد الافتتاحي مدين", "Opening debit" },
                { "الرصيد الافتتاحي دائن", "Opening credit" },
                { "الرصيد السابق", "Previous balance" },
                { "الرصيد السابق مدين", "Previous debit" },
                { "الرصيد السابق دائن", "Previous credit" },
                { "حركة الحساب", "Account movement" },
                { "حركة الحساب مدين", "Movement debit" },
                { "حركة الحساب دائن", "Movement credit" },
                { "رصيد الحساب", "Account balance" },
                { "رصيد الحساب مدين", "Closing debit" },
                { "رصيد الحساب دائن", "Closing credit" },
                { "حركة الضريبة", "Tax movement" },
                { "إجمالي (المدين)", "Total debit" },
                { "إجمالي (الدائن)", "Total credit" },
                { "إجمالي الفترة المحددة", "Selected period total" },
                { "عدد الحسابات", "Account count" },
                { "عدد الحسابات المتأثرة", "Affected accounts" },
                { "عدد السجلات", "Record count" },
                { "صافي مركز التكلفة", "Net cost center balance" },
                { "صافي الضريبة", "Net tax" },
                { "صافي الخسارة", "Net loss" },
                { "صافي الربح", "Net profit" },
                { "تحميل التقرير المحاسبي", "Load accounting report" },
                { "لا توجد بيانات", "No data" },
                { "تحديث قائمة الأدوية", "Refresh medicine list" },
                { "جارٍ تحميل الأدوية والشركات…", "Loading medicines and companies…" },
            { "جارٍ حفظ التغيير…", "Saving changes…" },
            { "جارٍ تحميل أسماء الأدوية…", "Loading medicine names…" },
            { "تحديث قائمة الأدوية دون مسح الفاتورة", "Refresh medicines without clearing the invoice" },
            { "تحديث الأصناف", "Refresh medicines" },
            { "تعذر {0}. رمز الدعم: {1}", "Could not {0}. Support code: {1}" },
            { "جارٍ التحقق من الصنف…", "Checking the selected medicine…" },
            { "تعذر العثور على الصنف المحدد. حدّث قائمة الأدوية.", "The selected medicine was not found. Refresh the medicine list." },
                { "تم تحديث الأدوية وخيارات الشركات.", "Medicines and company options refreshed." },
                { "تعذر تحميل الأدوية والشركات. تحقق من الاتصال ثم اضغط تحديث.", "Medicines and companies could not be loaded. Check the connection and choose Refresh." },
                { "لا توجد بيانات للتصدير.", "There is no data to export." },
                { "اعرض نتيجة التقرير أولاً ثم خصص أعمدته.", "View report results before customizing columns." },
                { "تعذر حفظ القالب. تأكد من الاسم والفترة الزمنية ثم أعد المحاولة.", "The template could not be saved. Check its name and date range, then retry." },
                { "تم حفظ قالب التقرير لهذا المستخدم؛ يحفظ الفلاتر والخيارات دون بيانات التقرير.", "The report template was saved for this user. It stores filters and options without report data." },
                { "هذا القالب غير صالح وتم تجاهله بأمان.", "This template is invalid and was safely ignored." },
                { "اختر قالباً محفوظاً لحذفه.", "Choose a saved template to delete." },
                { "هل تريد حذف قالب «{0}»؟ لا يؤثر ذلك على بيانات التقارير.", "Delete template '{0}'? Report data will not be affected." },
                { "نظام إدارة الصيدلية", "Pharmacy Management System" },
                { "إدارة الصيدلية", "Pharmacy management" },
                { "الرئيسية", "Home" },
                { "إعدادات النظام العامة", "General system settings" },
                { "إعدادات اتصال قاعدة البيانات", "Database connection settings" },
                { "إعدادات الاتصال", "Connection settings" },
                { "هذه الحزمة مرتبطة بقاعدة عرض ثابتة. يمكنك اختبار اتصالها، لكن تغيير الخادم أو القاعدة والحفظ معطّل.", "This package uses a fixed demo database. You can test its connection, but changing the server or database and saving are disabled." },
                { "اتصال ثابت لهذه الحزمة؛ الاختبار متاح وتغيير الاتصال معطّل.", "Fixed package connection; testing is available and connection changes are disabled." },
                { "اختر الخادم وقاعدة التشغيل، ثم اختبر الاتصال قبل الحفظ.", "Choose the server and working database, then test the connection before saving." },
                { "خادم SQL", "SQL server" },
                { "مصادقة Windows", "Windows authentication" },
                { "مصادقة Windows (المدعومة حالياً)", "Windows authentication (currently supported)" },
                { "قاعدة البيانات", "Database" },
                { "المظهر", "Appearance" },
                { "فاتح", "Light" },
                { "داكن", "Dark" },
                { "حسب النظام", "System" },
                { "تحميل قواعد البيانات", "Load databases" },
                { "اختبار", "Test" },
                { "خط الواجهة", "Interface font" },
                { "حفظ المظهر", "Save appearance" },
                { "حفظ الخط والمظهر فقط", "Save font and appearance only" },
                { "اختر الخادم وقاعدة البيانات، ثم اختبر الاتصال واحفظ الإعدادات. الحفظ يمنع الرجوع إلى قاعدة افتراضية عند التشغيل التالي.", "Choose the server and database, test the connection, then save. Saving prevents fallback to a default database on the next launch." },
                { "اكتب اسم خادم SQL أو اختره من المقترحات.", "Enter a SQL server name or choose a suggestion." },
                { "اختر قاعدة موجودة أو اكتب اسم قاعدة العمل الصحيحة.", "Choose an existing database or enter the correct working database name." },
                { "هذا الإصدار يستخدم مصادقة Windows للاتصال بقاعدة البيانات.", "This version uses Windows authentication for database connections." },
                { "مظهر التطبيق", "Application appearance" },
                { "اختر المظهر الفاتح أو الداكن أو حسب النظام.", "Choose light, dark, or system appearance." },
                { "البحث عن خوادم SQL", "Discover SQL servers" },
                { "اختبار اتصال قاعدة البيانات", "Test database connection" },
                { "يختبر الاتصال من دون حفظ الإعدادات أو تهيئة الجداول.", "Tests the connection without saving settings or initializing tables." },
                { "حفظ اتصال قاعدة البيانات", "Save database connection" },
                { "يحفظ اتصال التشغيل بعد تأكيد قاعدة البيانات.", "Saves the runtime connection after confirming the database." },
                { "إغلاق إعدادات قاعدة البيانات", "Close database settings" },
                { "حالة إعداد الاتصال", "Connection setup status" },
                { "معاينة الواجهة فقط: الاتصال والحفظ معطّلان.", "Interface preview only: connection and saving are disabled." },
                { "فتح خوادم SQL المقترحة", "Open SQL server suggestions" },
                { "فتح قواعد البيانات", "Open databases" },
                { "فتح خيارات المظهر", "Open appearance options" },
                { "فتح الخطوط المتاحة", "Open available fonts" },
                { "إعدادات المعايير السعودية تعمل وفق الأوضاع المتاحة حتى اكتمال الاعتماد الرسمي", "Saudi compliance settings remain limited to the available modes until official approval is complete." },
                { "مساحة العمل اليومية", "Daily workspace" },
                { "المستخدم والقاعدة المتصلة", "Signed-in user and connected database" },
                { "تبديل وضع الشاشة", "Toggle window mode" },
                { "تبديل تخطيط الصفحة الرئيسية", "Toggle home layout" },
                { "تخصيص مساحة العمل", "Customize workspace" },
                { "تبديل كثافة مساحة العمل", "Toggle workspace density" },
                { "اختصار الأقسام", "Collapse modules" },
                { "توسيع الأقسام", "Expand modules" },
                { "اختصار", "Collapse" },
                { "توسيع", "Expand" },
                { "اختر عرضًا أساسيًا أو كاملًا، أو رتّب الأقسام التي تستخدمها يوميًا.", "Choose a basic or full view, or arrange the modules you use every day." },
                { "في الوضع المخصص: حدّد الأقسام، ثم اسحب لترتيبها أو استخدم زري التحريك.", "In custom mode, select modules and drag to reorder them, or use the move buttons." },
                { "الأقسام الظاهرة وترتيبها", "Visible modules and order" },
                { "أساسي", "Basic" },
                { "كامل", "Full" },
                { "مخصص", "Custom" },
                { "المساحة:", "Workspace: " },
                { "تخطيط", "Layout" },
                { "تخصيص", "Customize" },
                { "تحريك لأعلى", "Move up" },
                { "تحريك لأسفل", "Move down" },
                { "استعادة الافتراضي", "Reset" },
                { "اختر قسمًا واحدًا على الأقل.", "Select at least one module." },
                { "حفظ تخصيص مساحة العمل", "Save workspace customization" },
                { "إرشاد بدء تشغيل النظام", "System setup guide" },
                { "عنوان إرشاد البدء", "Setup guide title" },
                { "وصف خطوات بدء التشغيل", "Setup guide steps" },
                { "تسجيل الدخول", "Sign in" },
                { "دخول آمن وسريع لإدارة المبيعات والمخزون والموظفين.", "Secure, fast access to sales, inventory and employee management." },
                { "إعداد قاعدة البيانات (F1)", "Configure database (F1)" },
                { "الخادم", "Server" },
                { "القاعدة", "Database" },
                { "قاعدة التشغيل", "Active database" },
                { "تعذر قراءة إعداد الاتصال — اضغط F1 للإصلاح", "Connection settings could not be read — press F1 to repair them" },
                { "قاعدة التشغيل الحالية", "Current active database" },
                { "اسم قاعدة البيانات التي سيستخدمها النظام بعد تسجيل الدخول.", "The database the system will use after sign-in." },
                { "إعداد قاعدة البيانات", "Database settings" },
                { "يفتح إعدادات اختيار الخادم وقاعدة البيانات.", "Opens the server and database settings." },
                { "المستخدم", "User" },
                { "اسم المستخدم", "Username" },
                { "كلمة المرور", "Password" },
                { "أدخل اسم مستخدم النظام.", "Enter your system username." },
                { "أدخل كلمة المرور ثم اضغط دخول.", "Enter your password, then select Sign in." },
                { "تذكر اسم المستخدم", "Remember username" },
                { "يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.", "Remembers the user name and password (encrypted) on this device." },
                { "دخول إلى النظام", "Sign in to the system" },
                { "إغلاق التطبيق", "Close application" },
                { "إظهار", "Show" },
                { "إخفاء", "Hide" },
                { "إظهار كلمة المرور", "Show password" },
                { "إخفاء كلمة المرور", "Hide password" },
                { "يعرض كلمة المرور مؤقتاً، واضغط مرة أخرى لإخفائها.", "Shows the password temporarily; select again to hide it." },
                { "يخفي كلمة المرور مرة أخرى.", "Hides the password again." },
                { "معاينة فقط؛ لم تُفتح قاعدة البيانات ولم تُنفذ مصادقة.", "Preview only; no database was opened and no authentication was attempted." },
                { "معاينة فقط؛ لم تُفتح إعدادات قاعدة البيانات.", "Preview only; database settings were not opened." },
                { "تعذر قراءة إعداد اتصال قاعدة البيانات ونسخته الاحتياطية. لا يمكن الدخول إلى قاعدة افتراضية مختلفة. افتح إعدادات الاتصال عبر F1 ليصلحها مسؤول Windows المحلي.", "The database connection settings and their backup could not be read. The application will not fall back to a different database. Ask a local Windows administrator to repair the connection from F1." },
                { "الرجاء إدخال اسم المستخدم وكلمة المرور.", "Enter the username and password." },
                { "تم إيقاف محاولات تسجيل الدخول مؤقتًا. حاول بعد {0} ثانية.", "Sign-in attempts are temporarily paused. Try again in {0} seconds." },
                { "تحقق من إعدادات الخادم وقاعدة البيانات عبر F1 ثم أعد المحاولة.", "Check the server and database settings from F1, then try again." },
                { "فشل الاتصال بقاعدة البيانات. يجب أن يفتح مسؤول Windows المحلي إعدادات الاتصال عبر F1.", "The database connection failed. A local Windows administrator must open the connection settings from F1." },
                { "لا يوجد مدير مسجل للنظام بعد. يجب على مسؤول Windows محلي فتح إعدادات قاعدة البيانات عبر F1 وإنشاء المدير الأول قبل تسجيل الدخول.", "No manager account exists yet. A local Windows administrator must open database settings from F1 and create the first manager before sign-in." },
                { "تهيئة مطلوبة", "Setup required" },
                { "اسم المستخدم أو كلمة المرور غير صحيحة.", "The username or password is incorrect." },
                { "خطأ في تسجيل الدخول", "Sign-in error" },
                { "تغيرت بيانات الدخول أثناء المحاولة. أعد المحاولة.", "The sign-in details changed during the attempt. Try again." },
                { "تغير اتصال قاعدة البيانات أو جلسة المستخدم أثناء المحاولة. أعد المحاولة.", "The database connection or user session changed during the attempt. Try again." },
                { "تم تسجيل الدخول، لكن تعذر حفظ خيار تذكرني.", "Sign-in succeeded, but the Remember me preference could not be saved." },
                { "فتح إعدادات قاعدة البيانات", "Open database settings" },
                { "تعديل اتصال قاعدة البيانات يتطلب صلاحية مسؤول Windows لحماية بيانات الصيدلية. سيتم فتح نافذة إعداد مستقلة بعد موافقتك على طلب Windows.", "Changing the database connection requires Windows administrator access to protect pharmacy data. A separate settings window will open after you approve the Windows prompt." },
                { "فُتحت نافذة إعدادات الاتصال بصلاحية مسؤول. احفظ التغيير فيها ثم أغلق شاشة الدخول وافتح النظام من جديد لاستخدام الاتصال الجديد.", "The connection settings window opened with administrator access. Save the change there, then close the sign-in screen and reopen the application to use the new connection." },
                { "تعذر فتح إعدادات قاعدة البيانات بصلاحية مسؤول. تأكد من السماح بطلب Windows ثم أعد المحاولة.", "Database settings could not be opened with administrator access. Allow the Windows prompt, then try again." },
                { "إعداد المدير الأول", "Initial manager setup" },
                { "إنشاء المدير الأول للنظام", "Create the system's first manager" },
                { "هذه العملية متاحة فقط بعد إعداد قاعدة بيانات لا تحتوي على مدير.", "This step is available only after preparing a database that has no manager account." },
                { "معاينة تخطيطية فقط؛ لم يُنشأ مستخدم ولم تُفتح قاعدة البيانات.", "Layout preview only: no user was created and no database was opened." },
                { "رقم الموظف", "Employee ID" },
                { "العمر", "Age" },
                { "الراتب", "Salary" },
                { "تأكيد كلمة المرور", "Confirm password" },
                { "إنشاء المدير", "Create manager" },
                { "إنشاء المدير الأول", "Create initial manager" },
                { "يتحقق من الحقول ثم ينشئ حساب المدير الأول.", "Validates the fields, then creates the initial manager account." },
                { "إلغاء إعداد المدير الأول", "Cancel initial manager setup" },
                { "يرجى إدخال عمر وراتب صحيحين.", "Enter a valid age and salary." },
                { "كلمتا المرور غير متطابقتين.", "The passwords do not match." },
                { "تم إنشاء المدير الأول بنجاح. سجّل الدخول بالحساب الجديد.", "The initial manager was created. Sign in with the new account." },
                { "تعذر إنشاء المدير الأول. تحقق من الاتصال والبيانات ثم أعد المحاولة.", "The initial manager could not be created. Check the connection and data, then try again." },
                { "تهيئة قاعدة البيانات", "Database setup" },
                { "كلمة المرور مطلوبة.", "Password is required." },
                { "لا يمكن أن تبدأ كلمة المرور أو تنتهي بمسافة.", "The password cannot start or end with a space." },
                { "يجب ألا تقل كلمة المرور عن 3 حرفاً.", "The password must contain at least 3 characters." },
                { "تذكرني", "Remember me" },
                { "دخول", "Sign in" },
                { "حفظ", "Save" },
                { "إلغاء", "Cancel" },
                { "الأصناف منخفضة المخزون", "Low-stock medicines" },
                { "الأصناف قرب الانتهاء", "Medicines expiring soon" },
                { "تفاصيل التنبيه", "Alert details" },
                { "الأصناف المنتهية", "Expired medicines" },
                { "تعذر تحميل التنبيهات؛ أعد فتح مراقبة النظام أو حاول مجددًا.", "Could not load alerts; reopen system monitoring or try again." },
                { "أصناف منتهية", "Expired medicines" },
                { "قرب الانتهاء", "Expiring soon" },
                { "إغلاق", "Close" },
                { "تفاصيل الأخطاء", "All errors" },
                { "تفاصيل أخطاء الملف", "File validation errors" },
                { "عدد الأخطاء: {0}", "{0} errors" },
                { "قائمة أخطاء استيراد الأصناف", "Medicine import errors" },
                { "لا توجد أخطاء في الملف.", "No file errors." },
                { "الصف", "Row" },
                { "المشكلة", "Issue" },
                { "فتح", "Open" },
                { "رجوع", "Back" },
                { "مسح", "Clear" },
                { "إضافة", "Add" },
                { "تعديل", "Edit" },
                { "حذف", "Delete" },
                { "بحث", "Search" },
                { "تحديث", "Refresh" },
                { "طباعة", "Print" },
                { "معاينة", "Preview" },
                { "تصدير CSV", "Export CSV" },
                { "تصدير كشف الحساب إلى CSV", "Export account statement to CSV" },
                { "تم تصدير كشف الحساب إلى CSV.", "Account statement exported to CSV." },
                { "تعذر حفظ CSV؛ بقي كشف الحساب كما هو. تحقق من المسار أو صلاحية الكتابة ثم أعد المحاولة.", "Could not save the CSV; the account statement remains open and unchanged. Check the path or write access, then try again." },
                { "تصدير Excel", "Export Excel" },
                { "تصدير كشف الحساب إلى Excel", "Export account statement to Excel" },
                { "تم تصدير كشف الحساب إلى Excel.", "Account statement exported to Excel." },
                { "تعذر حفظ Excel؛ بقي كشف الحساب كما هو. تحقق من المسار أو صلاحية الكتابة ثم أعد المحاولة.", "Could not save the Excel workbook; the account statement remains open and unchanged. Check the path or write access, then try again." },
                { "تصدير التقرير إلى Excel", "Export the report to Excel" },
                { "الفواتير", "Sales" },
                { "المشتريات", "Purchases" },
                { "إضافة فواتير الموردين وتحديث المخزون", "Add supplier invoices and update stock" },
                { "ملغاة", "Cancelled" },
            { "معتمدة", "Posted" },
            { "هذه الفاتورة ملغاة مسبقاً.", "This invoice has already been cancelled." },
            { "إلغاء الفواتير متاح للمدير فقط.", "Only managers can cancel invoices." },
            { "حدد فاتورة من السجل أولاً.", "Select an invoice from the history first." },
            { "إلغاء الفاتورة", "Cancel invoice" },
            { "سيُعكس القيد وتُخصم الكمية من المخزون. سيُرفض الإلغاء إذا استُهلكت التشغيلة أو لم يكفِ الرصيد. هل تريد المتابعة؟", "The journal entry will be reversed and the quantity deducted from inventory. Cancellation will be rejected if a lot has been consumed or the available balance is insufficient. Continue?" },
            { "تأكيد إلغاء فاتورة مشتريات", "Confirm purchase invoice cancellation" },
            { "تم إلغاء الفاتورة وعكس ", "Invoice cancelled; reversed " },
            { " وحدة بنجاح.", " units successfully." },            { "سجل فواتير المشتريات", "Purchase invoice history" },
                { "انقر مرتين على أي فاتورة لعرض الأصناف والتواريخ", "Double-click an invoice to view its items and dates." },
                { "بنود المشتريات — استخدم زر إزالة لحذف الصنف", "Purchase items — use Remove to delete an item" },
                { "الإدخال السريع", "Quick entry" },
                { "المورد", "Supplier" },
                { "الموظف", "Employee" },
                { "المخزون الحالي", "Current stock" },
                { "المخزون الحالي: {0} وحدة | سعر الشراء: {1}", "Current stock: {0} units | Buying price: {1}" },
                { "المخزون الحالي: {0}", "Current stock: {0}" },
                { "تنبيه: الكمية المطلوبة كبيرة جداً!", "Warning: the requested quantity is very large!" },
                { "لم يعد الدواء المحدد موجودًا. حدّث القائمة ثم أعد المحاولة.", "The selected medicine is no longer available. Refresh the list and try again." },
                { "جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها.", "Reading the medicine. Wait for the result or change the input to cancel it." },
                { "تم تحميل الأصناف. اختر الصنف وأدخل الكمية.", "Items loaded. Select an item and enter the quantity." },
                { "تغيرت جلسة المشتريات. أعد البحث.", "The purchase session changed. Search again." },
                { "جاري البحث بالباركود. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها.", "Barcode lookup is in progress. Wait for the result or change the input to cancel it." },
                { "جاري تحميل الموردين والموظفين. انتظر اكتمال التحميل.", "Loading suppliers and employees. Wait for loading to finish." },
                { "لم يكتمل تحميل بيانات المشتريات. أغلق الشاشة وأعد فتحها.", "Purchase data did not finish loading. Close and reopen this screen." },
                { "مصدر بيانات بداية المشتريات غير متاح.", "The purchase startup data source is unavailable." },
                { "جاري حجز رقم فاتورة المشتريات. انتظر اكتمال الحجز.", "Reserving a purchase invoice number. Wait for the reservation to finish." },
                { "جاري تحميل بيانات المشتريات. انتظر اكتمال التحميل.", "Loading purchase data. Wait for loading to finish." },
                { "لم يتم العثور على باركود لهذا الصنف.", "No medicine was found for this barcode." },
                { "تغير الصنف أثناء القراءة. أعد البحث بالباركود.", "The medicine changed during the lookup. Search the barcode again." },
                { "المخزون الحالي — اختر صنفاً أولاً", "Current stock — select an item first" },
                { "معاينة محلية — لم تُقرأ قاعدة البيانات", "Local preview — no database data was read" },
                { "إضافة للقائمة", "Add item" },
                { "إضافة الصنف إلى فاتورة الشراء", "Add item to purchase invoice" },
                { "اعتماد بيانات المشتريات", "Post purchase invoice" },
                { "اعتماد فاتورة الشراء", "Post purchase invoice" },
                { "حفظ فقط", "Save only" },
                { "حفظ فاتورة المشتريات", "Save purchase invoice" },
                { "طباعة كاشير", "Print receipt" },
                { "حفظ وطباعة إيصال المشتريات", "Save and print purchase receipt" },
                { "حفظ وطباعة فاتورة A4", "Save and print A4 invoice" },
                { "مسح القائمة", "Clear invoice" },
                { "إغلاق شاشة المشتريات", "Close purchases screen" },
                { "حفظ فاتورة المشتريات PDF", "Save purchase invoice as PDF" },
                { "معاينة الفاتورة", "Preview invoice" },
                { "معاينة فاتورة المشتريات المحددة", "Preview selected purchase invoice" },
                { "اختر مورداً مسجلاً أو اكتب اسم مورد جديد.", "Choose a registered supplier or enter a new supplier name." },
                { "الموظف المسؤول", "Responsible employee" },
                { "ملاحظات فاتورة الشراء", "Purchase invoice notes" },
                { "الصافي", "Net total" },
                { "التوقيت", "Time" },
                { "إزالة", "Remove" },
                { "المبيعات", "Sales" },
                { "الأدوية", "Medicines" },
                { "المخزون", "Inventory" },
                { "نظام المخزون", "Inventory workspace" },
                { "المخازن", "Inventory" },
                { "نظام المخازن", "Inventory workspace" },
                { "المتابعة والتقارير", "Monitoring and reports" },
                { "عمليات المخزون", "Inventory operations" },
                { "كشف جرد المخزن", "Stock snapshot" },
                { "جرد التشغيلات والصلاحيات", "Lot stock and expiry" },
                { "إظهار التشغيلات الصفرية", "Show depleted lots" },
                { "حركة صنف محددة", "Item movements" },
                { "جارٍ تحميل بيانات المخزون...", "Loading inventory data..." },
                { "تقرير إجمالي حركة الصنف", "Item movement summary" },
                { "تسوية المخزون", "Stock adjustment" },
                { "مخزون أول المدة", "Opening stock" },
                { "التوريد المخزني", "Stock receipt" },
                { "الصرف المخزني", "Stock issue" },
                { "تنبيهات المخزون", "Inventory alerts" },
                { "معدل الدوران والركود للأصناف", "Item turnover and inactivity" },
                { "تغيرت المرشحات؛ اضغط تحديث لقراءة التقرير مجددًا.", "Filters changed; refresh to load the report again." },
                { "تغيرت حدود التنبيه؛ اضغط تحديث لقراءة النتيجة الجديدة.", "Alert thresholds changed; refresh to load the new results." },
                { "حدد حدود المخزون والصلاحية، ثم اضغط تحديث. صفر يوم يقصر تنبيه قرب الانتهاء على اليوم الحالي.", "Set the stock and expiry thresholds, then refresh. Zero expiry days restricts near-expiry alerts to today." },
                { "حفظ الحدود", "Save limits" },
                { "جارٍ تحميل حدود التنبيه المحفوظة...", "Loading saved alert limits..." },
                { "جارٍ حفظ حدود التنبيه...", "Saving alert limits..." },
                { "تم حفظ حدود التنبيه لهذا المستخدم وقاعدة البيانات.", "Alert limits saved for this user and database." },
                { "تعذر تحميل حدود التنبيه المحفوظة: ", "Could not load saved alert limits: " },
                { "تعذر حفظ حدود التنبيه: ", "Could not save alert limits: " },
                { "تعذر استعادة الحدود المحفوظة؛ تُستخدم الحدود المعروضة. يمكنك تعديلها وحفظها.", "Saved limits could not be restored; displayed limits are in use. You can edit and save them." },
                { "حد المخزون المنخفض", "Low-stock threshold" },
                { "الأيام قبل الانتهاء", "Days until expiry" },
                { "عدد التنبيهات", "Alert count" },
                { "منتهي", "Expired" },
                { "قريب الانتهاء", "Expiring soon" },
                { "مخزون منخفض", "Low stock" },
                { "تم تحميل تنبيهات المخزون.", "Inventory alerts loaded." },
                { "جارٍ تحميل تنبيهات المخزون...", "Loading inventory alerts..." },
                { "لا توجد تنبيهات مطابقة لهذه الحدود.", "No alerts match these thresholds." },
                { "الأيام قبل الانتهاء يجب أن تكون بين 0 و3650 يومًا؛ الصفر يعني اليوم فقط.", "Days until expiry must be between 0 and 3650; zero means today only." },
                { "معاينة تنبيهات المخزون؛ لم تُقرأ قاعدة البيانات.", "Inventory alerts preview; no database data was read." },
                { "تعذر تحميل التنبيهات: ", "Could not load alerts: " },
                { "أيام الركود", "Inactive days" },
                { "بحث باسم الصنف أو الباركود أو الشركة", "Search by medicine, barcode, or company" },
                { "بحث باسم الصنف / الباركود / الشركة", "Medicine / barcode / company" },
                { "إظهار الأرصدة الصفرية", "Show zero balances" },
                { "خيارات العرض", "Display options" },
                { "ابحث أو اضغط تحديث لعرض أرصدة التشغيلات وصلاحياتها.", "Search or refresh to view lot balances and expiry dates." },
                { "ابحث عن صنف أو اضغط تحديث لعرض أرصدة المخزون الحالية.", "Search medicines or refresh to view current stock." },
                { "معاينة جرد التشغيلات؛ لم تُقرأ قاعدة البيانات.", "Lot stock preview; no database data was read." },
                { "معاينة كشف الجرد؛ لم تُقرأ قاعدة البيانات.", "Stock preview; no database data was read." },
                { "لا توجد تشغيلات مطابقة؛ في الوضع القديم استخدم كشف الجرد المجمع.", "No lots match. Use the aggregate stock report for a legacy database." },
                { "لا توجد أصناف مطابقة للبحث وخيار العرض المحددين.", "No medicines match the current search and display option." },
                { "تعذر تحميل جرد التشغيلات: ", "Could not load lot stock: " },
                { "تعذر تحميل كشف الجرد: ", "Could not load stock: " },
                { "اختر الصنف والفترة، ثم اضغط تحديث لعرض حركات المخزون.", "Select a medicine and date range, then refresh to view movements." },
                { "لا توجد أصناف", "No medicines" },
                { "اختر صنفًا لعرض حركته.", "Select a medicine to view its movements." },
                { "اختر تاريخ البداية والنهاية قبل عرض الحركة.", "Choose start and end dates before viewing movements." },
                { "معاينة حركة الصنف؛ لم تُقرأ قاعدة البيانات.", "Item movement preview; no database data was read." },
                { "لا توجد حركات مخزون للصنف والفترة المحددين.", "No movements match the selected medicine and dates." },
                { "تعذر تحميل حركة الصنف: ", "Could not load item movements: " },
                { "بحث عن صنف", "Search medicines" },
                { "اضغط تحديث لعرض ملخص حركة الأصناف خلال الفترة المحددة.", "Refresh to view the item movement summary for the selected dates." },
                { "اختر تاريخ البداية والنهاية قبل عرض التقرير.", "Choose start and end dates before viewing the report." },
                { "معاينة ملخص حركة المخزون؛ لم تُقرأ قاعدة البيانات.", "Movement summary preview; no database data was read." },
                { "لا توجد حركات أصناف مطابقة للفترة والبحث المحددين.", "No item movements match the selected dates and search." },
                { "تعذر تحميل التقرير: ", "Could not load the report: " },
                { "حدد الفترة وحدّ أيام الركود، ثم اضغط تحديث لعرض التقرير.", "Choose the dates and inactive-day threshold, then refresh the report." },
                { "اختر تاريخ البداية والنهاية قبل عرض معدل الدوران.", "Choose start and end dates before viewing turnover." },
                { "أيام الركود يجب أن تكون بين 1 و3650 يومًا.", "Inactive days must be between 1 and 3650." },
                { "معاينة معدل الدوران؛ لم تُقرأ قاعدة البيانات.", "Turnover preview; no database data was read." },
                { "لا توجد أصناف مطابقة لفترة تقرير الدوران.", "No medicines match this turnover period." },
                { "تعذر تحميل معدل الدوران: ", "Could not load turnover: " },
                { "عدد التشغيلات", "Lots" },
                { "عدد الأصناف", "Medicines" },
                { "إجمالي الكمية", "Total quantity" },
                { "قيمة التكلفة", "Cost value" },
                { "كمية قابلة للصرف", "Dispensable quantity" },
                { "قيمة البيع", "Sales value" },
                { "عدد الحركات", "Movements" },
                { "إجمالي الوارد", "Total received" },
                { "إجمالي الصادر", "Total issued" },
                { "صافي الحركة", "Net movement" },
                { "الأصناف الراكدة", "Inactive medicines" },
                { "نتائج التقرير", "Report results" },
                { "المحاسبة", "Accounting" },
                { "نظام الحسابات", "Accounting workspace" },
                { "السندات والحسابات", "Vouchers and accounts" },
                { "القيود والضريبة", "Journals and tax" },
                { "التقارير والتحليل", "Reports and analysis" },
                { "شجرة الحسابات", "Chart of accounts" },
                { "كشف حساب عام", "General ledger" },
                { "سند قبض خارجي", "External receipt voucher" },
                { "سند صرف خارجي", "External payment voucher" },
                { "سند قبض داخلي", "Internal receipt voucher" },
                { "سند صرف داخلي", "Internal payment voucher" },
                { "القيود اليومية", "Journal entries" },
                { "الإقرار الضريبي", "Tax return" },
                { "ميزان المراجعة", "Trial balance" },
                { "القيود الافتتاحية", "Opening entries" },
                { "تقارير نظام الحسابات", "Accounting reports" },
                { "تقرير تحليل مراكز التكلفة", "Cost center analysis" },
                { "تقرير اليومية العامة", "General journal report" },
                { "إدارة الفترات المحاسبية", "Accounting periods" },
                { "أنشئ فترة بلا تداخل. الإقفال يمنع البيع والمشتريات والسندات والمرتجعات وقرارات الحجر الجديدة، ولا يتم إلا بعد نجاح فحوص الجاهزية.", "Create a non-overlapping period. Closing blocks new sales, purchases, vouchers, returns and quarantine decisions after readiness checks pass." },
                { "أنشئ فترة بلا تداخل. لا تُقفلها قبل نجاح فحوص الجاهزية.\nالإقفال يمنع البيع والمشتريات والسندات والمرتجعات وقرارات الحجر الجديدة.", "Create non-overlapping periods. Close only after readiness checks pass.\nClosing blocks new sales, purchases, vouchers, returns and quarantine decisions." },
                { "إنشاء فترة مفتوحة", "Create open period" },
                { "إقفال المحددة", "Close selected" },
                { "فحص الجاهزية", "Check readiness" },
                { "إنشاء فترة محاسبية مفتوحة", "Create an open accounting period" },
                { "إقفال الفترة المحاسبية المحددة", "Close the selected accounting period" },
                { "فحص جاهزية إقفال الفترة", "Check period close readiness" },
                { "إغلاق نافذة الفترات المحاسبية", "Close accounting periods" },
                { "تاريخ بداية الفترة", "Period start date" },
                { "تاريخ نهاية الفترة", "Period end date" },
                { "ملاحظات الفترة", "Period notes" },
                { "من", "From" }, { "إلى", "To" }, { "ملاحظات", "Notes" },
                { "الحالة", "Status" }, { "وقت الإقفال", "Closed at" }, { "أقفلها", "Closed by" },
                { "مفتوحة", "Open" }, { "مقفلة", "Closed" },
                { "معاينة واجهة الفترات؛ لم تُقرأ قاعدة البيانات ولم تُحفظ أي فترة.", "Period preview: no database read and no period saved." },
                { "المعاينة للعرض فقط؛ لم تُقرأ قاعدة البيانات.", "Preview only; the database was not read." },
                { "المعاينة للعرض فقط؛ لم تُنشأ فترة محاسبية.", "Preview only; no accounting period was created." },
                { "المعاينة للعرض فقط؛ لم تُقفل فترة محاسبية.", "Preview only; no accounting period was closed." },
                { "لا توجد فترات محاسبية معرفة بعد", "No accounting periods have been defined yet" },
                { "حدّد فترة أو أنشئ فترة جديدة قبل الاعتماد.", "Select a period or create a new one before confirming." },
                { "لا توجد فترات معرفة بعد.", "No periods have been defined yet." },
                { "راجع الحالة والتواريخ قبل الإقفال.", "Review the status and dates before closing." },
                { "تم إنشاء الفترة المفتوحة.", "The open period was created." },
                { "اختر تاريخ بداية ونهاية صحيحين للفترة.", "Choose valid start and end dates for the period." },
                { "تم إقفال الفترة بعد نجاح فحوص الجاهزية.", "The period was closed after readiness checks passed." },
                { "سيمنع الإقفال البيع والمشتريات والسندات والمرتجعات وقرارات الحجر الجديدة ضمن {0}، ولا توجد إعادة فتح تلقائية. هل تعتمد الإقفال؟", "Closing will block new sales, purchases, vouchers, returns and quarantine decisions within {0}. There is no automatic reopening. Close this period?" },
                { "صلاحية مطلوبة", "Permission required" },
                { "الشركات", "Companies" },
                { "الشركات المصنعة", "Manufacturers" },
                { "قائمة الشركات", "Manufacturer list" },
                { "جدول الشركات المصنعة", "Manufacturer table" },
                { "رقم الشركة", "Company ID" },
                { "اسم الشركة", "Company name" },
                { "بيانات الشركات المصنعة ومحرر الشركة", "Manufacturer data and editor" },
                { "إجراءات الشركات المصنعة", "Manufacturer actions" },
                { "إضافة شركة مصنعة", "Add manufacturer" },
                { "تعديل شركة مصنعة", "Edit manufacturer" },
                { "حذف شركة مصنعة", "Delete manufacturer" },
                { "مسح حقول الشركة المصنعة", "Clear manufacturer fields" },
                { "إغلاق شاشة الشركات المصنعة", "Close manufacturers screen" },
                { "معاينة فقط؛ لا تُحفظ بيانات شركات مصنعة.", "Preview only; manufacturer data is not saved." },
                { "إضافة الشركة المصنعة", "Add manufacturer" },
                { "تعديل الشركة المصنعة", "Edit manufacturer" },
                { "حذف الشركة المصنعة", "Delete manufacturer" },
                { "تحميل بيانات الشركات المصنعة", "Load manufacturer data" },
                { "تمت إضافة الشركة بنجاح.", "The manufacturer was added." },
                { "لم يتم العثور على الشركة.", "The manufacturer was not found." },
                { "تم تعديل بيانات الشركة بنجاح.", "The manufacturer was updated." },
                { "الرجاء إدخال رقم الشركة المراد حذفها.", "Enter the company ID to delete." },
                { "هل تريد حذف هذه الشركة؟", "Delete this manufacturer?" },
                { "تم حذف الشركة بنجاح.", "The manufacturer was deleted." },
                { "قائمة الأدوية", "Medicine list" },
                { "المواد الفعالة", "Active ingredients" },
                { "إضافة دواء", "Add medicine" },
                { "تعديل دواء", "Edit medicine" },
                { "حذف دواء", "Delete medicine" },
                { "مسح حقول الدواء", "Clear medicine fields" },
                { "إغلاق شاشة الأدوية", "Close medicines screen" },
                { "توليد", "Generate" },
                { "توليد باركود", "Generate barcode" },
                { "فتح قائمة الشركات المصنعة", "Open manufacturer list" },
                { "قائمة الأدوية ومحرر بيانات الدواء", "Medicine list and editor" },
                { "إجراءات الأدوية", "Medicine actions" },
                { "معاينة فقط؛ لا تُحفظ بيانات أدوية.", "Preview only; medicine data is not saved." },
                { "يمكن إدخال الباركود يدويًا أو توليده من اسم الدواء.", "Enter the barcode manually or generate it from the medicine name." },
                { "تمت إضافة الدواء بنجاح.", "The medicine was added." },
                { "لم يتم العثور على الدواء، لا يمكن التعديل.", "The medicine was not found and cannot be updated." },
                { "تم تعديل بيانات الدواء بنجاح.", "The medicine was updated." },
                { "الرجاء اختيار الدواء المراد حذفه.", "Select the medicine to delete." },
                { "هل تريد حذف هذا الدواء؟", "Delete this medicine?" },
                { "لم يتم العثور على الدواء.", "The medicine was not found." },
                { "تم حذف الدواء بنجاح.", "The medicine was deleted." },
                { "تحميل بيانات الأدوية", "Load medicine data" },
                { "تحميل تفاصيل الدواء", "Load medicine details" },
                { "البحث السريع عن الأصناف", "Quick medicine search" },
                { "نتائج الأصناف", "Medicine results" },
                { "الاسم أو الباركود أو الشركة", "Name, barcode, or manufacturer" },
                { "نتائج البحث السريع عن الأصناف", "Quick medicine search results" },
                { "استخدم سهم لأسفل ثم Enter لاختيار الصنف المحدد.", "Press Down, then Enter to choose the selected medicine." },
                { "حالة البحث السريع", "Quick search status" },
                { "اختيار الصنف المحدد", "Choose selected medicine" },
                { "يفتح الصنف المحدد في البيع أو الشراء.", "Opens the selected medicine in sales or purchases." },
                { "إغلاق البحث السريع", "Close quick search" },
                { "معاينة البحث دون اتصال بقاعدة البيانات.", "Search preview without a database connection." },
                { "الشركة/المورد", "Manufacturer or supplier" },
                { "الرصيد المسجل", "Recorded stock" },
                { "انتهاء الصنف", "Medicine expiry" },
                { "صافي المباع (30 يومًا)", "Net sold (30 days)" },
                { "جارٍ تحميل الأكثر مبيعًا…", "Loading top sellers…" },
                { "جارٍ البحث…", "Searching…" },
                { "لا توجد اقتراحات من حركة آخر 30 يومًا. اكتب للبحث عن أي صنف.", "No suggestions from the last 30 days. Type to search all medicines." },
                { "الأكثر مبيعًا: صافي حركة آخر 30 يومًا بعد الإلغاءات. تحقق الصلاحية عند الإضافة.", "Top sellers: net movement over the last 30 days after cancellations. Check expiry when adding." },
                { "لا توجد أصناف مطابقة. عدّل عبارة البحث.", "No matching medicines. Refine the search." },
                { "أول 100 نتيجة؛ ضيّق البحث. الرصيد وتاريخ الصنف لا يثبتان صلاحية الدفعات.", "First 100 results. Refine the search. Recorded stock and medicine date do not prove lot validity." },
                { "النتائج: ", "Results: " },
                { ". الرصيد وتاريخ الصنف لا يثبتان صلاحية الدفعات.", ". Recorded stock and medicine date do not prove lot validity." },
                { "الموظفون", "Employees" },
                { "اسم الموظف", "Employee name" },
                { "الهاتف", "Phone" },
                { "حالة كلمة المرور", "Password status" },
                { "قائمة الموظفين", "Employee list" },
                { "بيانات الموظفين ومحرر الموظف", "Employee data and editor" },
                { "إجراءات الموظفين", "Employee actions" },
                { "كلمة مرور جديدة", "New password" },
                { "كلمة مرور جديدة (اختيارية، 3 رموز فأكثر)", "New password (optional, 3 or more characters)" },
                { "محمية", "Protected" },
                { "معاينة فقط؛ لا تُحفظ بيانات موظفين.", "Preview only; employee data is not saved." },
                { "إضافة الموظف", "Add employee" },
                { "تعديل الموظف", "Edit employee" },
                { "حذف الموظف", "Delete employee" },
                { "إضافة موظف", "Add employee" },
                { "تعديل موظف", "Edit employee" },
                { "حذف موظف", "Delete employee" },
                { "مسح حقول الموظف", "Clear employee fields" },
                { "إغلاق شاشة الموظفين", "Close employee screen" },
                { "البحث عن موظف", "Find employee" },
                { "تحميل بيانات الموظفين", "Load employee data" },
                { "تحميل تفاصيل الموظف", "Load employee details" },
                { "لا تملك صلاحية إدارة الصلاحيات التفصيلية.", "You do not have permission to manage detailed access." },
                { "إدارة الموظفين متاحة للمدير المسجل فقط.", "Employee management is available only to the signed-in manager." },
                { "لا تملك صلاحية إدارة بيانات الموردين.", "You do not have permission to manage supplier records." },
                { "اختر موظفاً من الجدول أولاً، ثم افتح صلاحيات الشاشات والعمليات.", "Select an employee from the table before opening screen and operation permissions." },
                { "الرجاء تعبئة جميع الحقول.", "Complete all required fields." },
                { "أدخل عمرًا موجبًا وراتبًا رقميًا غير سالب.", "Enter a positive age and a non-negative numeric salary." },
                { "يجب أن تتكون كلمة المرور من 3 رموز على الأقل.", "The password must contain at least 3 characters." },
                { "تمت إضافة الموظف بنجاح.", "The employee was added." },
                { "رقم الموظف غير موجود، لا يمكن التعديل.", "The employee ID was not found, so the record cannot be updated." },
                { "تم تعديل بيانات الموظف بنجاح.", "The employee was updated." },
                { "الرجاء إدخال رقم الموظف المراد حذفه.", "Enter the employee ID to delete." },
                { "هل تريد حذف هذا الموظف؟", "Delete this employee?" },
                { "تم حذف الموظف بنجاح.", "The employee was deleted." },
                { "لم يتم العثور على الموظف.", "The employee was not found." },
                { "تم إلغاء الحذف.", "Deletion was cancelled." },
                { "تم تغيير حساب المستخدم الحالي أو حذفه، لذلك تم إنهاء الجلسة. سجّل الدخول من جديد.", "The current user account was changed or deleted, so the session ended. Sign in again." },
                { "انتهت الجلسة", "Session ended" },
                { "إدارة صلاحيات الشاشات والعمليات", "Manage screen and operation access" },
                { "صلاحيات الشاشات والعمليات", "Screen and operation permissions" },
                { "صلاحيات الشاشات والعمليات للموظف: {0}", "Screen and operation permissions: {0}" },
                { "صلاحية الدور", "Role default" },
                { "الدور", "Role" },
                { "تجاوز للموظف", "Employee override" },
                { "النتيجة الفعلية", "Effective access" },
                { "افتراضي حسب الدور", "Use role default" },
                { "سماح", "Allow" },
                { "منع", "Deny" },
                { "مسموح", "Allowed" },
                { "غير مسموح", "Denied" },
                { "حفظ الصلاحيات", "Save permissions" },
                { "حفظ صلاحيات الشاشات والعمليات", "Save screen and operation permissions" },
                { "إغلاق صلاحيات الشاشات والعمليات", "Close screen and operation permissions" },
                { "حالة صلاحيات الشاشات والعمليات", "Permission status" },
                { "جدول تجاوزات صلاحيات الشاشات والعمليات", "Screen and operation overrides" },
                { "يلزم ترقية مخطط قاعدة البيانات أولاً لتفعيل الصلاحيات التفصيلية.", "Upgrade the database schema to enable detailed permissions." },
                { "حساب المدير يحتفظ بصلاحيات كاملة ولا يقبل تجاوزات تفصيلية.", "The manager account has full access and cannot receive overrides." },
                { "اختر التجاوزات المطلوبة ثم احفظ. التغيير يطبق عند تسجيل دخول الموظف التالي.", "Choose overrides and save. Changes apply at the next sign-in." },
                { "اختر التجاوزات المطلوبة ثم احفظ. تُراجع عند المحاولة التالية، وقد يتأخر التحديث حتى خمس ثوانٍ.", "Choose overrides and save. Access is checked on the next attempt; updates may take up to five seconds." },
                { "توجد تعديلات غير محفوظة على الصلاحيات.", "Permission changes have not been saved." },
                { "نص البحث يتجاوز 120 حرفًا؛ اختصره دون تغيير الجزء المطلوب البحث عنه.", "Search text exceeds 120 characters. Shorten it while preserving the part you want to find." },
                { "تم حفظ الصلاحيات. تُراجع عند المحاولة التالية، وقد يتأخر التحديث حتى خمس ثوانٍ.", "Permissions saved. Access is checked on the next attempt; updates may take up to five seconds." },
                { "تم حفظ الصلاحيات بنجاح.", "Permissions saved." },
                { "الصلاحيات", "Permissions" },
                { "تعديل شجرة الحسابات", "Edit chart of accounts" },
                { "ترحيل قيد محاسبي", "Post accounting entry" },
                { "حفظ سند قبض أو صرف", "Post receipt or payment voucher" },
                { "عرض التقارير المحاسبية", "View accounting reports" },
                { "اعتماد فاتورة بيع", "Post sales invoice" },
                { "اعتماد مرتجع تجاري", "Approve commercial sales return" },
                { "الإفراج أو الإتلاف من مخزون الحجر", "Release or dispose quarantined stock" },
                { "إدارة المواد الفعالة", "Manage active ingredients" },
                { "إدارة المواد الفعالة ومصادرها", "Active ingredients and evidence" },
                { "كل مادة فعالة يجب أن تبدأ بمرجع موثق. المحتوى الطبي لا يُستنتج آلياً، ويمكن تعطيل المصدر دون حذف أثره.",
                    "Each ingredient needs a documented source. Deactivation keeps its audit trail." },
                { "1. تعريف المادة", "1. Ingredient" },
                { "2. المرجع العلمي أو التنظيمي", "2. Scientific or regulatory source" },
                { "3. ربط المادة بالصنف", "3. Link to medicine" },
                { "الاسم العربي", "Arabic name" },
                { "الاسم اللاتيني", "Latin name" },
                { "عنوان المصدر", "Source title" },
                { "الجهة الناشرة", "Publisher" },
                { "تاريخ الاطلاع", "Retrieved on" },
                { "الرابط", "URL" },
                { "التركيز", "Strength" },
                { "الوحدة", "Unit" },
                { "المادة الأساسية للصنف", "Primary ingredient" },
                { "إضافة المادة ومصدرها", "Add ingredient and source" },
                { "تفعيل/تعطيل المادة", "Activate/deactivate ingredient" },
                { "إضافة مصدر آخر", "Add another source" },
                { "تعطيل المصدر المحدد", "Deactivate selected source" },
                { "حفظ الربط", "Save link" },
                { "اختياري", "Optional" },
                { "معاينة تخطيطية فقط؛ لا اتصال بقاعدة البيانات ولا حفظ.", "Layout preview only; no database access or saving." },
                { "مادة تجريبية", "Sample ingredient" },
                { "مرجع توضيحي", "Sample source" },
                { "جهة تجريبية", "Sample publisher" },
                { "صنف تجريبي", "Sample medicine" },
                { "نشطة", "Active" },
                { "الأصناف المرتبطة", "Linked medicines" },
                { "المصادر النشطة", "Active sources" },
                { "أضيف بواسطة", "Added by" },
                { "أضيف في UTC", "Added at (UTC)" },
                { "عطل بواسطة", "Deactivated by" },
                { "أساسية", "Primary" },
                { "تاريخ الربط UTC", "Linked at (UTC)" },
                { "إلغاء فاتورة بيع معتمدة", "Cancel posted sales invoice" },
                { "إلغاء فاتورة مشتريات معتمدة", "Cancel posted purchase invoice" },
                { "اعتماد فاتورة مشتريات", "Post purchase invoice" },
                { "تسجيل حركة مخزون", "Post inventory movement" },
                { "شاشة:", "Screen:" },
                { "الشاشة أو العملية", "Screen or operation" },
                { "حدّد سماحًا أو منعًا عند الحاجة؛ الإعدادات وإدارة الموظفين للمدير فقط.",
                    "Allow or deny only as needed; settings and employee management remain manager-only." },
                { "إجمالي المبيعات", "Total sales" },
                { "قيمة الفواتير المسجلة", "Recorded invoice value" },
                { "عدد الشركات المصنعة", "Manufacturer count" },
                { "عدد مستخدمي النظام", "System user count" },
                { "سجل الفواتير وتحليل المبيعات", "Invoice history and sales analysis" },
                { "إرشاد الطباعة", "Print guidance" },
                { "مؤشرات الصيدلية", "Pharmacy indicators" },
                { "رقم الفاتورة", "Invoice number" },
                { "مؤشر المبيعات حسب آخر الفواتير", "Sales trend from recent invoices" },
                { "تعذر تحميل بيانات المبيعات", "Sales data could not be loaded" },
                { "لا توجد بيانات مبيعات متاحة", "No sales data is available" },
                { "استخدم الأسهم لاختيار فاتورة وعرض ملخص العميل أدناه. السجل للقراءة فقط.",
                    "Use the arrow keys to select an invoice and view its customer summary below. This history is read-only." },
                { "صلاحية الموظف", "Employee role" },
                { "مدير", "Manager" },
                { "كاشير", "Cashier" },
                { "مسؤول المخزون", "Inventory officer" },
                { "محاسب", "Accountant" },
                { "حركة حساب خلال فترة", "Account activity" },
                { "ترتيب حسب", "Sort by" },
                { "رصيد الفترة المحددة", "Selected period balance" },
                { "جار تحميل خيارات كشف الحساب...", "Loading account statement options..." },
                { "جار تحميل حركات الحساب...", "Loading account transactions..." },
                { "تغيرت جلسة المستخدم؛ أعد تحميل كشف الحساب.", "Your session changed. Reload the account statement." },
                { "تغيرت المرشحات؛ اضغط عرض النتيجة لتحديث كشف الحساب.", "Filters changed. Select View results to refresh the account statement." },
                { "تعذر تحميل كشف الحساب؛ أعد المحاولة. رمز الدعم: {0}", "Unable to load the account statement. Retry. Support code: {0}" },
                { "تم تحميل {0} حركة.", "Loaded {0} transactions." },
                { "لا توجد حركات مطابقة. راجع الحساب والفترة والمرشحات.", "No matching transactions. Check the account, dates and filters." },
                { "لا توجد حسابات متاحة لعرض كشف الحساب.", "No accounts are available for the account statement." },
                { "اختر حسابًا ومركز تكلفة صالحين قبل عرض كشف الحساب.", "Select a valid account and cost center before viewing the account statement." },
                { "لا توجد حركات في الفترة المحددة للطباعة أو التصدير.", "No transactions in the selected period to print or export." },
                { "لا توجد حركات جاهزة للطباعة أو التصدير؛ أعد تحميل كشف الحساب.", "No transactions are ready to print or export. Reload the account statement." },
                { "اختر تاريخ بداية ونهاية صحيحين قبل عرض كشف الحساب.", "Select valid start and end dates before viewing the account statement." },
                { "تاريخ البداية يجب ألا يتجاوز تاريخ النهاية.", "The start date must not be after the end date." },
                { "إجراء دفتر الأستاذ العام", "General ledger action" },
                { "حسب التاريخ ثم رقم المستند", "By date and document number" },
                { "كشف حساب: {0}", "Account statement: {0}" },
                { "الفترة: {0} - {1}", "Period: {0} - {1}" },
                { "تم تصدير كشف الحساب.", "Account statement exported." },
                { "حسب التاريخ", "By date" },
                { "قيد يدوي", "Manual entry" },
                { "سند", "Voucher" },
                { "فاتورة بيع", "Sales invoice" },
                { "فاتورة شراء", "Purchase invoice" },
                { "قيد افتتاحي", "Opening entry" },
                { "بدون مركز تكلفة", "Without cost center" },
                { "لا توجد حركات جاهزة للطباعة.", "No transactions are ready to print." },
                { "لا توجد حركات لهذا الحساب", "No transactions for this account" },
                { "الإعدادات", "Settings" },
                { "لوحة التحكم", "Dashboard" },
                { "العرض الشبكي", "Grid view" },
                { "مركز العمل", "Work center" },
                { "وضع عادي", "Windowed mode" },
                { "ملء الشاشة", "Full screen" },
                { "سجل العمليات اليومية", "Daily operations log" },
                { "مراقبة النظام", "System monitoring" },
                { "تنبيهات المخزون وسجل التدقيق وحركات المخزون", "Inventory alerts, audit log and stock movements" },
                { "سجل التدقيق", "Audit log" },
                { "تحديث بيانات مراقبة النظام", "Refresh system monitoring data" },
                { "إغلاق مراقبة النظام", "Close system monitoring" },
                { "تبويبات مراقبة النظام", "System monitoring tabs" },
                { "معاينة مراقبة النظام دون اتصال بقاعدة البيانات.", "System monitoring preview without a database connection." },
                { "لا تُقرأ سجلات التدقيق في المعاينة.", "Audit entries are not read in preview." },
                { "لا تُقرأ حركات المخزون في المعاينة.", "Stock movements are not read in preview." },
                { "المعاينة للعرض فقط؛ لم تُقرأ بيانات من قاعدة البيانات.", "Preview only; no database data was read." },
                { "لا توجد تنبيهات مخزون أو انتهاء حالياً.", "No stock or expiry alerts at this time." },
                { "{0} تنبيه يحتاج إلى مراجعة.", "{0} alerts need review." },
                { "لا توجد سجلات تدقيق حتى الآن.", "No audit entries yet." },
                { "عرض آخر {0} سجل تدقيق.", "Showing the latest {0} audit entries." },
                { "لا توجد حركات مخزون مسجلة حتى الآن.", "No stock movements recorded yet." },
                { "عرض آخر {0} حركة مخزون.", "Showing the latest {0} stock movements." },
                { "تعذر تحديث التنبيهات؛ قد تكون البيانات المعروضة قديمة.", "Could not refresh alerts; displayed data may be outdated." },
                { "تعذر تحديث سجل التدقيق؛ قد تكون البيانات المعروضة قديمة.", "Could not refresh the audit log; displayed data may be outdated." },
                { "تعذر تحديث حركات المخزون؛ قد تكون البيانات المعروضة قديمة.", "Could not refresh stock movements; displayed data may be outdated." },
                { "تعذر تحميل بيانات المراقبة. تحقق من اتصال قاعدة البيانات ثم أعد المحاولة.", "Could not load monitoring data. Check the database connection and try again." },
                { "الوقت", "Time" },
                { "الصلاحية", "Role" },
                { "الإجراء", "Action" },
                { "التفاصيل", "Details" },
                { "الاتجاه", "Direction" },
                { "التغير", "Change" },
                { "قبل", "Before" },
                { "بعد", "After" },
                { "رقم المصدر", "Source ID" },
                { "تكلفة الوحدة", "Unit cost" },
                { "سعر الوحدة", "Unit price" },
                { "رصيد أول المدة", "Opening balance" },
                { "الكمية الفعلية", "Physical quantity" },
                { "كمية التوريد", "Receipt quantity" },
                { "كمية الصرف", "Issue quantity" },
                { "المعاينة", "Preview" },
                { "سبب الحركة أو مرجعها (إلزامي)", "Movement reason or reference (required)" },
                { "سبب الحركة أو مرجعها", "Movement reason or reference" },
                { "اعتماد الحركة", "Post movement" },
                { "اختر الصنف وأدخل تفاصيل الحركة ثم اعتمدها لعرض أحدث الحركات.", "Select a medicine, enter the movement details, then post it to view recent movements." },
                { "معاينة عملية المخزون؛ لم تُحفظ بيانات ولم تُقرأ قاعدة البيانات.", "Inventory operation preview; no data was saved and the database was not read." },
                { "أدخل كمية صحيحة.", "Enter a valid quantity." },
                { "الرصيد المحدد لا يمكن أن يكون أقل من صفر.", "The specified balance cannot be below zero." },
                { "كمية التوريد أو الصرف يجب أن تكون أكبر من صفر.", "Receipt or issue quantity must be greater than zero." },
                { "بعد العملية: {0:N0} | الفرق: {1:N0}", "After movement: {0:N0} | Difference: {1:N0}" },
                { "لا يمكن اعتماد الصرف لأن الرصيد المتوقع سيكون أقل من صفر.", "The issue cannot be posted because the projected balance would be below zero." },
                { "اختر صنفًا قبل اعتماد حركة المخزون.", "Select a medicine before posting the stock movement." },
                { "أدخل سبب الحركة أو مرجعها قبل الاعتماد.", "Enter a movement reason or reference before posting." },
                { "الرصيد المتاح لا يكفي للصرف. تحقق من الكمية أو حدّث الرصيد.", "Available stock is insufficient. Check the quantity or refresh the balance." },
                { "تم إلغاء العملية قبل تعديل الرصيد.", "The operation was cancelled before changing stock." },
                { "هذه الحركة محفوظة بالفعل؛ أغلق النافذة وافتح حركة جديدة.", "This movement is already saved; close the window and open a new movement." },
                { "تم حفظ حركة المخزون، لكن تعذر تحديث العرض. لا تعِد اعتماد الحركة.", "The inventory movement was saved, but refreshing the view failed. Do not post it again." },
                { "الرصيد الناتج يتجاوز حد تخزين الكمية.", "The resulting balance exceeds the quantity storage limit." },
                { "فرق حركة المخزون يتجاوز حد تخزين الكمية.", "The stock movement difference exceeds the quantity storage limit." },                { "لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.", "The movement cannot be posted because the balance would become negative." },

                { "تم حفظ حركة المخزون.", "The stock movement was saved." },
                { "تم حفظ العملية وتحديث الرصيد.", "The movement was saved and the balance was refreshed." },
                { "لم يتم الحفظ: ", "Save failed: " },
                { "تنفيذ عملية المخزون", "Post stock movement" },
                { "تعذر قراءة الرصيد الحالي: ", "Could not read the current balance: " },
                { "إدارة مخزون الحجر", "Quarantined stock" },
                { "إشعار مرتجع تجاري", "Commercial sales return" },
                { "دفعات الفاتورة المتاحة للإرجاع", "Invoice lots available for return" },
                { "سبب المرتجع", "Return reason" },
                { "طريقة الاسترداد", "Refund method" },
                { "اعتماد الإشعار", "Approve credit note" },
                { "سجل الإشعارات", "Credit note history" },
                { "إدارة الحجر", "Manage quarantine" },
                { "سجل إشعارات المرتجع", "Sales return credit notes" },
                { "المعاينة للعرض فقط؛ لم يُقرأ سجل المرتجعات.", "Preview only; return history was not loaded." },
                { "المعاينة للعرض فقط؛ لم يُفتح مستند للطباعة.", "Preview only; no document was opened for printing." },
                { "لا توجد إشعارات مرتجع معتمدة لهذه الفاتورة", "No approved return credit notes for this invoice" },
                { "لا توجد إشعارات مرتجع معتمدة لهذه الفاتورة.", "No approved return credit notes for this invoice." },
                { "حدد إشعارًا ثم اختر المعاينة أو انقر مرتين.", "Select a credit note, then choose preview or double-click." },
                { "تعذر تحميل سجل الإشعارات. تحقق من الاتصال ثم أعد فتح الشاشة.", "Could not load credit note history. Check the connection, then reopen this screen." },
                { "تحميل سجل إشعارات المرتجع", "Load return credit note history" },
                { "حدد إشعار مرتجع صالحًا أولًا.", "Select a valid return credit note first." },
                { "إشعار دائن", "Credit note" },
                { "تعذر إكمال الفحص. النتائج المعروضة سابقة؛ أعد الفحص بعد التحقق من الاتصال.", "The check could not finish. Displayed results are from an earlier check; check the connection and run it again." },
                { "تعذر إكمال الفحص. تحقق من الاتصال ثم أعد المحاولة.", "The check could not finish. Check the connection, then retry." },
                { "تاريخ الاعتماد UTC: {0}\nطريقة الاسترداد: {1} | المنفذ: {2}\nسبب المرتجع: {3}", "Approved at (UTC): {0}\nRefund method: {1} | Posted by: {2}\nReturn reason: {3}" },
                { "الخصم", "Discount" },
                { "الضريبة", "Tax" },
                { "إشعار دائن رقم {0} | فاتورة أصلية رقم {1} | {2}", "Credit note {0} | Original invoice {1} | {2}" },
                { "معاينة إشعار المرتجع", "Preview return credit note" },
                { "تعذر فتح معاينة الإشعار. يمكنك إعادة المحاولة.", "Could not open the credit note preview. You can retry." },
                { "الإشعارات الدائنة المعتمدة", "Approved credit notes" },
                { "الإشعارات الدائنة المعتمدة المرتبطة بالفاتورة المحددة، مع معاينة وطباعة من بيانات المستند المحفوظ.", "Approved credit notes for the selected invoice. Preview and print using the saved document." },
                { "معاينة وطباعة", "Preview and print" },
                { "الدفعات المحجورة المعلقة", "Pending quarantined lots" },
                { "اختر دفعة محجورة، ثم أفرج عن الكمية الصالحة إلى المخزون أو أتلفها. الإتلاف يستبعد القيمة محاسبياً ولا يزيد الرصيد المتاح.", "Release usable stock or dispose. Disposal removes book value without adding stock." },
                { "دفعات الحجر المعلقة", "Pending quarantine lots" },
                { "قسم الدفعات المحجورة المعلقة", "Pending quarantined lots section" },
                { "إفراج إلى المخزون", "Release to stock" },
                { "إتلاف موثق", "Record disposal" },
                { "القرار", "Decision" },
                { "السبب", "Reason" },
                { "اعتماد القرار", "Confirm decision" },
                { "اعتماد قرار الحجر", "Confirm quarantine decision" },
                { "إغلاق إدارة مخزون الحجر", "Close quarantined stock" },
                { "حالة قرار الحجر", "Quarantine decision status" },
                { "معاينة واجهة الحجر؛ لم تُقرأ قاعدة البيانات ولم يُعتمد قرار.", "Quarantine preview: no database data was read and no decision was posted." },
                { "المعاينة للعرض فقط؛ لم تُقرأ دفعات الحجر.", "Preview only: no quarantine lots were read." },
                { "المعاينة للعرض فقط؛ لم يُعتمد قرار حجر.", "Preview only: no quarantine decision was posted." },
                { "لا توجد دفعات حجر معلقة", "No pending quarantine lots" },
                { "لا توجد كميات حجر معلقة.", "There are no pending quarantine quantities." },
                { "حدد دفعة لمراجعة القرار والكمية.", "Select a lot to review the decision and quantity." },
                { "تحميل مخزون الحجر", "Load quarantined stock" },
                { "حدد دفعة وقراراً أولاً.", "Select a lot and decision first." },
                { "اكتب سبب القرار ليبقى السجل قابلاً للمراجعة.", "Enter a decision reason to keep the audit trail reviewable." },
                { "سيُستبعد مقدار الإتلاف من قيمة المخزون بقيد محاسبي. هل تعتمد القرار؟", "The disposed quantity will be removed from inventory value with an accounting entry. Confirm the decision?" },
                { "ستصبح الكمية متاحة للبيع بعد التحقق من الصلاحية والاستدعاء. هل تعتمد القرار؟", "The quantity will become available for sale after expiry and recall validation. Confirm the decision?" },
                { "جارٍ اعتماد قرار الحجر...", "Posting quarantine decision..." },
                { "تم اعتماد القرار للكمية {0}.", "The decision was posted for quantity {0}." },
                { "تم اعتماد القرار، لكن تعذر تحديث القائمة. أعد فتح الشاشة قبل قرار جديد.", "Decision posted, but the list could not be refreshed. Reopen this screen before another decision." },
                { "تعذر تحديث قائمة الحجر. أعد فتح الشاشة قبل الاعتماد.", "Could not refresh the quarantine list. Reopen this screen before posting." },
                { "تعذر تأكيد القرار. راجع السجل؛ إعادة المحاولة دون تغيير تستخدم الطلب نفسه: {0}", "The decision result could not be confirmed. Review the history; retrying without changes uses the same request: {0}" },
                { "لم يعتمد القرار؛ راجع الرسالة وحاول مجدداً.", "The decision was not posted. Review the message and try again." },
                { "الإشعار", "Credit note" },
                { "مستدعاة", "Recalled" },
                { "المحجور", "Quarantined" },
                { "المعالج", "Resolved" },
                { "المتبقي", "Remaining" },
                { "مراقبة", "Monitor" },
                { "ابدأ بتجهيز بيانات الصيدلية", "Set up your pharmacy data" },
                { "لا توجد أصناف أو عمليات مسجلة بعد. أضف الصنف الأول، ثم سجّل أول فاتورة مشتريات لتحديث المخزون.", "No medicines or transactions have been recorded yet. Add your first medicine, then record the first purchase invoice to update inventory." },
                { "إضافة الصنف الأول", "Add first medicine" },
                { "فتح المشتريات", "Open purchases" },
                { "النوع", "Type" },
                { "الصنف", "Medicine" },
                { "تاريخ الانتهاء", "Expiry date" },
                { "الشركة", "Company" },
                { "التنبيه", "Alert" },
                { "التقارير", "Reports" },
                { "منشئ التقارير التشغيلي", "Operational report builder" },
                { "اختر المصدر والفترة، ثم اعرض النتائج أو استخدم قالبًا محفوظًا.", "Choose a source and period, then view results or apply a saved template." },
                { "مصدر البيانات", "Data source" },
                { "من تاريخ", "From date" },
                { "إلى تاريخ", "To date" },
                { "القوالب المحفوظة", "Saved templates" },
                { "تخصيص الأعمدة", "Customize columns" },
                { "عرض النتيجة", "View results" },
                { "مصدر التقرير", "Report source" },
                { "بحث في نتائج التقرير", "Search report results" },
                { "قوالب التقارير التشغيلية المحفوظة", "Saved operational report templates" },
                { "القوالب المحفوظة لهذا المستخدم", "Templates saved for this user" },
                { "المبيعات (مؤرخة عند توفر المخطط التجاري)", "Sales (dated when the commercial schema is available)" },
                { "فواتير المشتريات", "Purchase invoices" },
                { "سطور اليومية العامة", "General journal lines" },
                { "أداء الصيدلية اليومي (الربح والإلغاءات)", "Daily pharmacy performance (profit and cancellations)" },
                { "الأصناف الأعلى صافي مبيعًا اليوم", "Top net-selling medicines today" },
                { "الأصناف الأعلى صافي مبيعًا خلال آخر 30 يومًا", "Top net-selling medicines over the last 30 days" },
                { "الأصناف الأكثر حركات مخزون اليوم", "Medicines with the most stock movements today" },
                { "صافي الكمية المباعة", "Net quantity sold" },
                { "صافي الكمية اليوم", "Net quantity today" },
                { "صافي الكمية — 30 يومًا", "Net quantity — 30 days" },
                { "حركات المخزون اليوم", "Stock movements today" },
                { "عدد سجلات الحركة، وليس مجموع الكميات. الترتيب تنازلي ثم اسم الصنف.", "Movement records, rather than total quantities. Sorted descending, then by medicine name." },
                { "صافي الكمية بعد الإلغاءات والمرتجعات، بما فيها التالف. الفترة بتوقيت الجهاز وتشمل اليوم؛ الصافي الموجب فقط.", "Net quantity after cancellations and returns, including damaged goods. Device-local period includes today; positive net quantities only." },
                { "لا توجد نتائج مطابقة للفلاتر المحددة.", "No results match the selected filters." },
                { "معاينة ببيانات وهمية؛ لم تُقرأ قاعدة البيانات.", "Preview with sample data; the database was not read." },
                { "حالة التقرير", "Report status" },
                { "تطبيق قالب التقرير المحدد", "Apply the selected report template" },
                { "حذف قالب التقرير المحدد", "Delete the selected report template" },
                { "عرض نتائج التقرير", "View report results" },
                { "تصدير التقرير إلى CSV", "Export the report to CSV" },
                { "إغلاق منشئ التقارير التشغيلي", "Close the operational report builder" },
                { "الفترة", "Period" },
                { "تقرير تشغيلي", "Operational report" },
                { "النتائج", "Results" },
                { "اليوم", "Day" },
                { "فواتير البيع", "Sales invoices" },
                { "فواتير الإلغاء", "Cancellation invoices" },
                { "إشعارات المرتجع التجاري", "Commercial return credit notes" },
                { "المبيعات دون الضريبة", "Sales excluding tax" },
                { "المبيعات المعكوسة دون الضريبة", "Reversed sales excluding tax" },
                { "صافي المبيعات", "Net sales" },
                { "صافي التكلفة المسجلة", "Net recorded cost" },
                { "الربح الإجمالي المسجل", "Recorded gross profit" },
                { "هامش الربح %", "Profit margin %" },
                { "صافي ضريبة المبيعات", "Net sales tax" },
                { "الأداء المالي اليومي حسب العملة", "Daily financial performance by currency" },
                { "المالية حسب العملة", "Finances by currency" },
                { "ملخص حركة المخزون اليومي", "Daily inventory activity summary" },
                { "المخزون اليومي", "Daily inventory" },
                { "الوحدات الأساسية المباعة", "Base stock units sold" },
                { "الوحدات المعادة للمخزون", "Sellable units restored to stock" },
                { "قيود دون عملة محفوظة", "Entries without a saved currency" },
                { "قيود بتعارض العملة", "Entries with conflicting currencies" },
                { "صف مستقل لكل يوم وعملة محفوظة؛ المبالغ الفارغة لقيود بلا عملة أو بعملة متعارضة. الربح قبل مصروفات التشغيل، والإلغاء بتاريخ قيده.", "One row per day and saved currency; blank amounts indicate missing or conflicting currency. Profit is before operating expenses; cancellations use their posting date." },
                { "الكميات بوحدات المخزون الأساسية حسب تاريخ الحركة المحفوظ؛ المعاد للمخزون يشمل الصالح فقط، دون تكرار حسب العملة.", "Quantities use base stock units and saved movement dates; restored stock includes sellable returns only, without duplication by currency." },
                { "اتصال ومعاملة SQL مطلوبان لحفظ عملة المستند.", "A SQL connection and transaction are required to save the document currency." },
                { "مصدر المستند غير مسموح لحفظ عملة البيع.", "This document source cannot save a sales currency." },
                { "مرجع المستند مطلوب.", "A document reference is required." },
                { "تعذر تحديد عملة البيع الجديدة.", "The new sale currency could not be determined." },
                { "تعذر تحديد عملة بيع واحدة صالحة.", "A single valid sales currency could not be determined." },
                { "تغيرت عملة مسودة البيع؛ لم تحفظ الفاتورة. افتح مسودة بالعملة الصحيحة.", "The sales draft currency changed; the invoice was not saved. Open a draft with the correct currency." },
                { "عملة المستند التجاري لا تطابق عملة مسودة البيع.", "The commercial document currency does not match the sales draft currency." },
                { "إشعار المرتجع المحفوظ مطلوب لحفظ عملة قيده.", "A saved credit note is required to record its journal currency." },
                { "تعذر حفظ عملة القيد الجديد؛ يجب التراجع عن المستند.", "The new journal currency could not be saved; the document must be rolled back." },
                { "عملة المستند المحفوظ غير صالحة.", "The saved document currency is invalid." },
                { "تعارضت عملة القيد مع المستند الأصلي؛ أوقف العكس وراجع البيانات.", "The journal currency conflicts with the original document; stop the reversal and review the data." },
                { "نوع المستند المحفوظ لا يطابق مصدر القيد.", "The saved document type does not match the journal source." },
                { "عملة المستند التجاري المحفوظ مفقودة؛ لم يعتمد القيد.", "The saved commercial document currency is missing; the journal was not posted." },
                { "عملة إشعار المرتجع لا تطابق الفاتورة الأصلية.", "The credit note currency does not match the original invoice." },
                { "قيد البيع الأصلي مطلوب قبل الإلغاء.", "The original sales journal is required before cancellation." },
                { "أكثر من قيد أصلي لنفس الفاتورة؛ أوقف الإلغاء وراجع البيانات.", "Multiple original journals exist for this invoice; stop cancellation and review the data." },
                { "فواتير بتكلفة مسجلة صفرية", "Invoices with zero recorded cost" },
                { "الوحدات المباعة", "Units sold" },
                { "الوحدات المعادة", "Units returned" },
                { "(تم تطبيق حد العرض الآمن)", " (safe display limit applied)" },
                { "الربح حسب التكلفة المسجلة وقبل مصروفات التشغيل؛ الإلغاء بتاريخ قيده. الأيام ذات الحركة فقط.", "Profit uses recorded cost before operating expenses; cancellations use their posting date. Only days with activity are shown." },
                { "جار تحميل التقرير...", "Loading report..." },
                { "تعذر تحميل التقرير.", "The report could not be loaded." },
                { "تعذر تحميل التقرير. لم تُعدّل أي بيانات. رمز الدعم: ", "The report could not be loaded. No data was changed. Support code: " },
                { "معاينة فقط؛ لم يُحفظ قالب.", "Preview only; no template was saved." },
                { "معاينة فقط؛ لم يُطبق قالب.", "Preview only; no template was applied." },
                { "معاينة فقط؛ لم يُحذف قالب.", "Preview only; no template was deleted." },
                { "معاينة فقط؛ لم تُعدل الأعمدة.", "Preview only; report columns were not changed." },
                { "معاينة فقط؛ لم يُنشأ ملف.", "Preview only; no file was created." },
                { "معاينة فقط؛ لم تُفتح الطباعة.", "Preview only; printing was not opened." },
                { "خيارات طباعة التقرير", "Report print options" },
                { "اتجاه التقرير", "Report orientation" },
                { "تفصيل التقرير", "Report detail" },
                { "الاتجاه والكثافة وبيانات المنشأة اختيارات مستقلة عن بيانات التقرير.", "Page direction, row density and company identity are independent of report data." },
                { "معاينة الخيارات؛ لن تُفتح طابعة أو قاعدة بيانات.", "Options preview; no printer or database will be opened." },
                { "تعذر تحميل بيانات المنشأة؛ يمكن طباعة التقرير بدون ترويسة.", "Company details could not be loaded. You can print without a company header." },
                { "اختر شكل التقرير والبيانات التي تريد إظهارها في ترويسته.", "Choose the report layout and the company details to show in its header." },
                { "معاينة فقط؛ لم تُفتح طابعة أو قاعدة بيانات.", "Preview only; no printer or database was opened." },
                { "ملف الشعار غير متاح أو كبير جدًا؛ صحح المسار في ملف الصيدلية.", "The logo is unavailable or too large. Update its path in the pharmacy profile." },
                { "اتجاه الصفحة", "Page orientation" },
                { "كثافة الصفوف", "Row density" },
                { "تلقائي حسب الأعمدة", "Automatic by columns" },
                { "طولي", "Portrait" },
                { "عرضي", "Landscape" },
                { "مفصل", "Detailed" },
                { "مختصر", "Compact" },
                { "بيانات المنشأة في الترويسة", "Company details in the header" },
                { "اسم المنشأة", "Company name" },
                { "الشعار", "Logo" },
                { "السجل التجاري", "Commercial registration" },
                { "الجوال", "Mobile" },
                { "الموقع", "Website" },
                { "معاينة الطباعة", "Print preview" },
                { "تخصيص أعمدة التقرير", "Customize report columns" },
                { "حدد الأعمدة الظاهرة ورتبها. هذا لا يغيّر بيانات النظام.", "Choose the visible columns and their order. This does not change system data." },
                { "لا يمكن إخفاء الأعمدة المطلوبة: {0}.", "Required columns cannot be hidden: {0}." },
                { "ترتيب الأعمدة الظاهرة", "Visible column order" },
                { "نقل لأعلى", "Move up" },
                { "نقل العمود المحدد لأعلى", "Move the selected column up" },
                { "نقل لأسفل", "Move down" },
                { "نقل العمود المحدد لأسفل", "Move the selected column down" },
                { "إظهار الكل", "Show all" },
                { "إظهار كل أعمدة التقرير", "Show all report columns" },
                { "تطبيق تخصيص أعمدة التقرير", "Apply report column customization" },
                { "إلغاء تخصيص أعمدة التقرير", "Cancel report column customization" },
                { "يجب إظهار عمود واحد على الأقل.", "At least one column must remain visible." },
                { "حفظ قالب التقرير", "Save report template" },
                { "حفظ قالب", "Save template" },
                { "يحفظ هذا القالب الفلاتر والخيارات فقط، وليس بيانات التقرير.", "This template saves filters and options only, not report data." },
                { "اسم القالب", "Template name" },
                { "اسم قالب التقرير", "Report template name" },
                { "إلغاء حفظ قالب التقرير", "Cancel saving the report template" },
                { "أدخل اسمًا للقالب.", "Enter a template name." },
                { "العملاء", "Customers" },
                { "إدارة العملاء", "Customer management" },
                { "إدارة بيانات العملاء", "Manage customer records" },
                { "عرض بيانات العملاء فقط؛ لا تملك صلاحية تعديلها.", "Customer records are read-only; you do not have permission to edit them." },
                { "إدارة الموردين", "Manage suppliers" },
                { "جديد", "New" },
                { "عميل جديد", "New customer" },
                { "حفظ بيانات العميل", "Save customer details" },
                { "اختيار العميل المحدد", "Choose selected customer" },
                { "حالة حفظ العميل", "Customer status" },
                { "السجل محفوظ للمحافظة على أثر الفواتير.", "Records are retained for invoice history." },
                { "تعذر تحميل العملاء", "Could not load customers" },
                { "جارٍ تحميل العملاء…", "Loading customers…" },
                { "جارٍ تحميل الشركات…", "Loading companies…" },
                { "تحديث قائمة الشركات", "Refresh the company list" },
                { "لا توجد شركات مسجلة.", "No registered companies." },
                { "تم تحديث قائمة الشركات.", "The company list was refreshed." },
                { "تعذر تحميل الشركات. تحقق من الاتصال ثم اضغط تحديث.", "Could not load companies. Check the connection and refresh." },
                { "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث.", "Change saved, but the list could not be refreshed. Choose Refresh." },
                { "تغيرت جلسة المستخدم. أعد تحميل القائمة.", "The user session changed. Reload the list." },
                { "لا تملك صلاحية قراءة قائمة العملاء.", "You do not have permission to read the customer list." },
                { "إعادة تحميل قائمة العملاء", "Reload the customer list" },
                { "تعذر تحميل العملاء. تحقق من الاتصال ثم أعد تحميل القائمة.", "Could not load customers. Check the connection and reload the list." },
                { "تم حفظ العميل، لكن تعذر تحديث القائمة. أعد تحميل القائمة.", "Customer saved, but the list could not be refreshed. Retry loading the list." },
                { "معرّف العميل", "Customer ID" },
                { "إجمالي مشتريات مسجل", "Recorded purchases" },
                { "أدخل بيانات العميل ثم احفظها قبل إصدار الفاتورة.",
                    "Enter and save customer details before issuing an invoice." },
                { "تم حفظ بيانات العميل. أصبح متاحًا الآن للاختيار في فاتورة البيع.",
                    "Customer saved and available for selection in a sales invoice." },
                { "اختر عميلاً من القائمة أولاً.", "Select a customer from the list first." },
                { "يتم تحديث قائمة العملاء أثناء الكتابة.", "The customer list updates as you type." },
                { "أدخل رقم الهاتف بأرقام متصلة.", "Enter the phone number without spaces." },
                { "معاينة تخطيطية فقط؛ لم تُقرأ قاعدة البيانات ولن تُحفظ بيانات.",
                    "Layout preview only; no database data was read or saved." },
                { "العملاء والموردون", "Customers and suppliers" },
                { "لا تملك صلاحية اختيار عميل للمبيعات.", "You do not have permission to select a sales customer." },
                { "لا تملك صلاحية إدارة العملاء والموردين.", "You do not have permission to manage customers and suppliers." },
                { "اختر عميلاً مسجلاً للفاتورة", "Choose a registered customer for the invoice" },
                { "اختيار عميل", "Select a customer" },
                { "اختيار العميل", "Select customer" },
                { "تم تحديد العميل", "Customer selected" },
                { "ابحث عن عميل مسجل ثم اختره للفاتورة", "Search for a registered customer and select them for the invoice" },
                { "تم تحديد العميل. اضغط اختيار العميل للمتابعة.", "Customer selected. Confirm to continue." },
                { "إدارة بيانات العملاء واختيارهم لفواتير البيع", "Manage customer records for sales invoices" },
                { "ابحث بالاسم أو رقم الهاتف", "Search by name or phone number" },
                { "قائمة العملاء", "Customer list" },
                { "بيانات العميل", "Customer details" },
                { "رقم الهاتف", "Phone number" },
                { "العنوان", "Address" },
                { "بيانات الصيدلية", "Pharmacy profile" },
                { "الاسم والعنوان وبيانات المنشأة", "Name, address and business details" },
                { "الهوية، الاتصال، الشعار، ونصوص الفواتير", "Identity, contact, logo and invoice text" },
                { "الهوية والبيانات الرسمية", "Identity and registration" },
                { "الاتصال والشعار والفاتورة", "Contact, logo and invoice" },
                { "الاسم عربي", "Arabic name" },
                { "الاسم انجليزي", "English name" },
                { "النشاط التجاري", "Business activity" },
                { "النشاط انجليزي", "English activity" },
                { "رقم السجل التجاري", "Commercial registration number" },
                { "رقم الرخصة", "License number" },
                { "رقم الجوال", "Mobile number" },
                { "الرقم الضريبي", "Tax number" },
                { "ملاحظات الفاتورة", "Invoice notes" },
                { "رأس وتذيل الصفحة", "Invoice header and footer" },
                { "رقم الفاكس", "Fax number" },
                { "صندوق البريد", "PO box" },
                { "البريد الالكتروني", "Email" },
                { "الموقع الالكتروني", "Website" },
                { "انقر لاختيار الشعار\nPNG · JPG · BMP", "Click to choose a logo\nPNG · JPG · BMP" },
                { "بدء ملف صيدلية فارغ", "Start a blank pharmacy profile" },
                { "حفظ بيانات الصيدلية", "Save pharmacy profile" },
                { "معاينة طباعة بيانات الصيدلية", "Preview pharmacy profile printing" },
                { "فتح مساعدة بيانات الصيدلية", "Open pharmacy profile help" },
                { "اختيار شعار الصيدلية", "Choose pharmacy logo" },
                { "إغلاق بيانات الصيدلية", "Close pharmacy profile" },
                { "اختيار صورة", "Choose image" },
                { "مساعدة", "Help" },
                { "تنبيه", "Notice" },
                { "نجاح", "Success" },
                { "الرجاء إدخال الاسم عربي ورقم الهاتف والعنوان.", "Enter the Arabic name, phone number and address." },
                { "تم حفظ بيانات الصيدلية والتحقق منها في قاعدة البيانات.", "The pharmacy profile was saved and verified in the database." },
                { "تم حفظ بيانات الصيدلية والتحقق منها بنجاح.", "The pharmacy profile was saved and verified successfully." },
                { "أدخل بيانات الشركة ثم اضغط حفظ.\nانقر على مربع الشعار أو زر اختيار صورة.\nاستخدم زر الطباعة أو رابط ذيل الفاتورة للمعاينة.", "Enter the business details, then select Save.\nClick the logo area or Choose image.\nUse Print to preview the profile." },
                { "إجمالي المشتريات", "Total purchases" },
                { "لا توجد نتائج تطابق البحث.", "No customers match this search." },
                { "لا يوجد عملاء مسجلون بعد.", "No customers have been registered yet." },
                { "إجمالي العملاء", "Total customers" },
                { "العملة", "Currency" },
                { "العملات", "Currencies" },
                { "الحسابات والضرائب", "Accounting and tax" },
                { "الدولة وتفعيل الضريبة ونسبتها", "Country, tax status and rate" },
                { "اللغة", "Language" },
                { "لغة عرض النصوص وتسميات الواجهة", "Display language and interface labels" },
                { "الكمية", "Quantity" },
                { "الباركود", "Barcode" },
                { "سعر الشراء", "Purchase price" },
                { "سعر البيع", "Sale price" },
                { "الإجمالي", "Total" },
                { "التاريخ", "Date" },
                { "مركز الأجهزة", "Device center" },
                { "إعداد الطابعة ومقاس إيصالات الكاشير لهذه المحطة.", "Configure the printer and receipt paper for this workstation." },
                { "اضبط الطابعة ومقاس الفاتورة لهذه المحطة.", "Set the printer and invoice size for this workstation." },
                { "فحص الطابعة", "Check printer" },
                { "قارئ الباركود USB/HID: أدخل المسح ثم أرسل Enter؛ المسار مفعل في البيع والمشتريات.", "USB/HID barcode reader: scan, then send Enter. It is enabled in sales and purchases." },
                { "قارئ الباركود USB/HID: امسح الرمز ثم اضغط Enter.", "USB/HID barcode reader: scan, then press Enter." },
                { "فتح القائمة", "Open list" },
                { "المقاس الافتراضي لفاتورة البيع والشراء", "Default sales and purchase invoice size" },
                { "اختيار الطابعة المفضلة", "Select preferred printer" },
                { "حالة الطابعة", "Printer status" },
                { "اختيار مقاس إيصال الكاشير", "Select cashier receipt size" },
                { "اختيار مقاس فاتورة البيع والشراء", "Select sales and purchase invoice size" },
                { "معلومة قارئ الباركود", "Barcode reader information" },
                { "حفظ إعدادات الأجهزة", "Save device settings" },
                { "فحص توفر الطابعة المختارة", "Check selected printer" },
                { "يتحقق من توفر الطابعة محلياً دون إرسال مهمة طباعة.", "Checks local printer availability without sending a print job." },
                { "إغلاق مركز الأجهزة", "Close device center" },
                { "استخدام طابعة Windows الافتراضية", "Use the Windows default printer" },
                { "80mm — المقاس الافتراضي", "80mm — default size" },
                { "58mm — للطابعات الصغيرة", "58mm — compact printers" },
                { "A4 — فاتورة تفصيلية", "A4 — detailed invoice" },
                { "إيصال حراري — حسب مقاس الإيصال", "Thermal receipt — uses receipt size" },
                { "الطابعة المحفوظة غير متصلة الآن؛ سيستخدم النظام طابعة Windows الافتراضية.", "The saved printer is offline; the Windows default printer will be used." },
                { "سيُستخدم اختيار Windows الافتراضي.", "The Windows default printer will be used." },
                { "الطابعة المحفوظة متاحة لهذه المحطة.", "The saved printer is available on this workstation." },
                { "تعذر حفظ إعدادات الأجهزة. تأكد أن الطابعة المختارة لا تزال متصلة.", "Device settings could not be saved. Make sure the selected printer is still connected." },
                { "تعذر حفظ إعدادات الأجهزة. تحقق من توفر الطابعة وإمكانية حفظ إعدادات المستخدم، ثم أعد المحاولة.", "Could not save device settings. Check printer availability and access to user settings, then retry." },
                { "تم حفظ إعدادات هذه المحطة. ستستخدمها معاينات وطباعة الفواتير التالية.", "Workstation settings were saved and will apply to future invoice previews and prints." },
                { "اختر طابعة أولاً ثم أعد الفحص.", "Select a printer, then check again." },
                { "اختيار Windows الافتراضي لا يمكن التحقق منه مسبقاً؛ سيختاره Windows عند الطباعة.", "The Windows default printer cannot be checked in advance; Windows will select it when printing." },
                { "الطابعة المختارة متاحة وجاهزة للاستخدام من هذه المحطة. لم تُرسل أي طباعة تجريبية.", "The selected printer is available on this workstation. No test page was sent." },
                { "تعذر التحقق من الطابعة المختارة. تأكد من اتصالها ومن تعريف Windows ثم حدّث القائمة.", "The selected printer could not be verified. Check its connection and Windows driver, then refresh the list." },
                { "الحفظ معطل في المعاينة؛ لا تتغير تفضيلات هذه المحطة.", "Saving is disabled in preview; workstation preferences are unchanged." },
                { "معاينة فقط: الحفظ معطّل.", "Preview only: saving is disabled." },
                { "إعدادات العملات", "Currency settings" },
                { "العملات والدول", "Currencies and countries" },
                { "إدارة العملات المتاحة وتحديد عملة النظام الأساسية", "Manage available currencies and choose the system currency." },
                { "بيانات العملة", "Currency details" },
                { "الدولة", "Country" },
                { "اسم العملة", "Currency name" },
                { "رمز العملة", "Currency code" },
                { "الرمز", "Code" },
                { "اختصار/علامة العملة", "Currency symbol" },
                { "الاختصار", "Symbol" },
                { "اجعلها عملة النظام", "Make this the system currency" },
                { "عملة النظام", "System currency" },
                { "معاينة فقط", "Preview only" },
                { "معاينة فقط: لن تُحفظ تغييرات العملات.", "Preview only: currency changes will not be saved." },
                { "غير محددة", "Not set" },
                { "لا توجد عملات معرفة بعد", "No currencies have been defined yet" },
                { "حفظ العملة", "Save currency" },
                { "تعيين العملة كعملة للنظام", "Set currency as system currency" },
                { "حذف العملة المحددة", "Delete selected currency" },
                { "إضافة عملة جديدة", "Add new currency" },
                { "تحميل العملات", "Load currencies" },
                { "تم حفظ العملة بنجاح.", "Currency saved successfully." },
                { "حدد العملة التي تريد حذفها.", "Select the currency to delete." },
                { "لا يمكن حذف عملة النظام الحالية. عيّن عملة أخرى كعملة النظام أولاً.", "The current system currency cannot be deleted. Set another system currency first." },
                { "هل تريد حذف هذه العملة؟", "Do you want to delete this currency?" },
                { "تأكيد الحذف", "Confirm deletion" },
                { "تم حذف العملة.", "Currency deleted." },
                { "حذف العملة", "Delete currency" },
                { "حدد العملة التي تريد جعلها عملة النظام.", "Select the currency to use as the system currency." },
                { "تم تعيين عملة النظام.", "System currency updated." },
                { "تعيين عملة النظام", "Set system currency" },
                { "الدولة واسم العملة ورمز العملة حقول مطلوبة.", "Country, currency name, and currency code are required." },
                { "لغة الواجهة", "Interface language" },
                { "إعدادات الحسابات والضرائب", "Accounting and tax settings" },
                { "البلد وسياسة الضريبة", "Country and tax policy" },
                { "حقول إعدادات الضريبة", "Tax settings fields" },
                { "إجراءات إعدادات الضريبة", "Tax settings actions" },
                { "تؤثر السياسة في الفواتير الجديدة فقط.", "The policy applies to new invoices only." },
                { "بلد الضريبة", "Tax country" },
                { "تفعيل الضريبة", "Enable tax" },
                { "نسبة الضريبة (%)", "Tax rate (%)" },
                { "اليمن (YE)", "Yemen (YE)" },
                { "السعودية (SA)", "Saudi Arabia (SA)" },
                { "بلد آخر", "Other country" },
                { "اليمن: الضريبة غير مطبقة؛ النسبة الفعلية 0%.", "Yemen: tax is not applied; the effective rate is 0%." },
                { "تطبّق الضريبة على الفواتير الجديدة فقط؛ المستندات السابقة تحتفظ بقيمها.", "Tax applies to new invoices only; existing documents keep their values." },
                { "فتح قائمة بلد الضريبة", "Open tax country list" },
                { "حالة إعدادات الضريبة", "Tax settings status" },
                { "معاينة فقط: لا حفظ ولا اتصال بقاعدة البيانات.", "Preview only: no save or database connection." },
                { "جار تحميل سياسة الضريبة...", "Loading tax policy..." },
                { "تحميل إعدادات الضريبة", "Load tax settings" },
                { "تعذر التحميل. أغلق النافذة وأعد المحاولة بعد التحقق من الاتصال.", "Loading failed. Close the window and try again after checking the connection." },
                { "جار حفظ سياسة الضريبة...", "Saving tax policy..." },
                { "حفظ إعدادات الضريبة", "Save tax settings" },
                { "فتح إعدادات الضريبة", "Open tax settings" },
                { "فتح إعدادات النظام", "Open system settings" },
                { "تعذر تحميل بيانات الصيدلية. أغلق النافذة وأعد فتحها للمراجعة.", "Pharmacy details could not be loaded. Close and reopen this window to review them." },
                { "اكتمل طلب الحفظ؛ يلزم التحقق", "Save request completed; verification required" },
                { "تعذر عرض الشعار؛ اختر صورة أخرى.", "Cannot display the logo; choose another image." },
                { "اكتمل طلب حفظ بيانات الصيدلية، لكن تعذر تأكيد تطابق القراءة.\nالمرجع: {0}\n\nلا تكرر الحفظ؛ أغلق النافذة وأعد فتحها للمراجعة.\n\nرمز الدعم: {1}", "The pharmacy save request completed, but the read-back could not be verified.\nReference: {0}\n\nDo not save again; close and reopen this window to review the result.\n\nSupport code: {1}" },
                { "تم حفظ سياسة الضريبة. البلد: {0}؛ تطبق على الفواتير الجديدة.", "Tax policy saved. Country: {0}; applies to new invoices." },
                { "تم حفظ إعدادات الضريبة. أغلق النافذة وأعد فتحها للمراجعة.", "Tax settings were saved. Close and reopen this window to review the result." },
                { "جار تأكيد حفظ سياسة الضريبة؛ انتظر اكتمال العملية قبل الإغلاق.", "Tax policy save is being confirmed; wait for it to finish before closing." },
                { "إعدادات المعايير السعودية", "Saudi compliance settings" },
                { "المزايا المنظمة — السعودية", "Regulated features — Saudi Arabia" },
                { "الإلغاء يوقف الاستخدام الجديد فقط ولا يحذف فواتير أو دفعات. الإنتاج الحي محجوب حتى الاعتماد الرسمي.", "Disabling stops new use without deleting invoices or payments. Live production remains blocked until formal approval." },
                { "المزايا المنظمة السعودية", "Saudi regulated features" },
                { "الميزة", "Feature" },
                { "الوصف", "Description" },
                { "الحالة الحالية", "Current mode" },
                { "التغيير", "Requested change" },
                { "سبب التغيير للتدقيق", "Audit reason for change" },
                { "سبب الإضافة أو الإلغاء", "Reason for enabling or disabling" },
                { "حالة إعدادات المعايير السعودية", "Saudi compliance settings status" },
                { "حفظ التغييرات المدققة", "Save audited changes" },
                { "إغلاق إعدادات المعايير السعودية", "Close Saudi compliance settings" },
                { "معاينة تخطيط فقط؛ الحفظ غير متاح.", "Layout preview only; saving is unavailable." },
                { "الحالات: معطل، محاكاة، اختبار. وضع الإنتاج محجوب في هذه النسخة.", "Modes: disabled, simulator, and test. Production is blocked in this version." },
                { "لا تتوفر إعدادات المعايير السعودية. رمز الدعم: ", "Saudi compliance settings are unavailable. Support code: " },
                { "لا توجد تغييرات للحفظ.", "There are no changes to save." },
                { "تم حفظ الإعدادات مع سجل تدقيق. لم تُحذف أي بيانات.", "Settings were saved with an audit record. No data was deleted." },
                { "تم الحفظ", "Saved" },
                { "حفظ إعدادات المعايير السعودية", "Save Saudi compliance settings" },
                { "فواتير تجريبية", "Simulation invoices" },
                { "إعدادات المحاكاة والتحقق المحلي", "Simulation and local validation settings" },
                { "الربط الضريبي", "Tax integration" },
                { "اختبار التكامل دون إنتاج حي", "Integration testing without live production" },
                { "البيع التجاري السعودي", "Saudi commercial sales" },
                { "فواتير ودفعات تجارية وفق الإعداد المعتمد.", "Commercial invoices and payments under the approved configuration." },
                { "فاتورة السعودية", "Saudi e-invoicing" },
                { "محاكاة ثم اختبار اعتماد؛ لا يوجد اتصال حي في هذا الإصدار.", "Simulation followed by certification testing; this version has no live connection." },
                { "رصد الدوائي", "RSD medicine tracking" },
                { "تجهيز محاكاة لتتبع الدواء، ولا يُربط حيًا قبل اعتماد SFDA.", "Medicine-tracking simulation; live connection requires SFDA approval." },
                { "نفيس للتأمين", "NPHIES insurance" },
                { "محاكاة الأهلية والمطالبات، ولا يُربط حيًا قبل onboarding وشهادات الربط.", "Eligibility and claims simulation; live connection requires onboarding and integration certificates." },
                { "مركز اللغة", "Language center" },
                { "اختر لغة مكتملة ومراجعة لتظهر بها كل شاشات النظام.", "Choose a complete, reviewed language for every application screen." },
                { "اختر لغة الواجهة المتاحة.", "Choose an available interface language." },
                { "العربية", "Arabic" },
                { "الإنجليزية", "English" },
                { "العربية — مكتملة (الافتراضية)", "Arabic — complete (default)" },
                { "العربية (الافتراضية)", "Arabic (default)" },
                { "العربية هي اللغة الافتراضية للتطبيق وتدعم الاتجاه من اليمين إلى اليسار بالكامل.", "Arabic is the default language and supports complete right-to-left layout." },
                { "العربية هي لغة الواجهة الافتراضية.", "Arabic is the default interface language." },
                { "English — قيد الإعداد", "English — in progress" },
                { "وصف لغة الواجهة", "Interface language description" },
                { "اختيار اللغة العربية", "Select Arabic" },
                { "اختيار اللغة الإنجليزية", "Select English" },
                { "لا يمكن تفعيل الإنجليزية قبل اكتمال ترجمة الشاشات والتحقق منها.", "English cannot be enabled until every screen is translated and verified." },
                { "الإنجليزية قيد الإعداد", "English is in progress" },
                { "حالة توفر اللغة", "Language availability" },
                { "حفظ إعداد لغة الواجهة", "Save interface language" },
                { "إغلاق مركز اللغة", "Close language center" },
                { "التغيير يطبّق بعد إعادة تشغيل النظام.", "The change applies after restarting the application." },
                { "الإنجليزية قيد الإعداد؛ العربية هي اللغة المتاحة حاليًا.", "English is in progress; Arabic is currently available." },
                { "الحفظ معطل في المعاينة؛ لا تتغير لغة المستخدم المحفوظة.", "Saving is disabled in preview; the saved language is unchanged." },
                { "معاينة فقط: لن تتغير اللغة المحفوظة.", "Preview only: the saved language will not change." },
                { "تعذر حفظ تفضيل اللغة. لم يُغيّر النظام أي بيانات تشغيلية.", "The language preference could not be saved. No operational data was changed." },
                { "تم حفظ العربية كلغة افتراضية للواجهة.", "Arabic was saved as the default interface language." },
                { "حفظ الإعدادات", "Save settings" },
                { "الطابعة المفضلة", "Preferred printer" },
                { "مقاس إيصال الكاشير", "Receipt paper size" },
                { "استيراد البيانات", "Data import" },
                { "معاينة CSV واستيراد آمن", "Safe CSV preview and import" },
                { "العربية الافتراضية وحالة توفر اللغات", "Arabic is the default; view language availability" },
                { "الطابعة ومقاس الإيصال وقارئ الباركود", "Printer, receipt paper and barcode reader" },
                { "المعايير السعودية", "Saudi standards" },
                { "خيارات الامتثال والمحاكاة", "Compliance and simulation options" },
                { "تصدير إكسل", "Export to Excel" },
                { "مركز Excel", "Excel center" },
                { "تعذر تأكيد تصدير الملف؛ راجع الملف والمسار قبل إعادة المحاولة.", "Export could not be confirmed; check the file and path before retrying." },
                { "تصدير البيانات أو فتح استيراد الأصناف والفواتير والصناديق", "Export data or open medicine, invoice, and cashbox import" },
                { "صناديق النقد", "Cashboxes" },
                { "تعريف الصناديق وإسناد الكاشير واستيرادها وتصديرها", "Define and assign cashboxes; import or export definitions" },
                { "الأدوية والفواتير والمخزون والقيود؛ البحث والفترة حيث تتوفر", "Medicines, invoices, inventory and journals; filter before export" },
                { "تصدير البيانات إلى إكسل", "Export data to Excel" },
                { "اختر البيانات للتصدير أو افتح مسار استيرادها؛ قوالب الاستيراد مستقلة عن كشوف التصدير.", "Choose data to export or open its import workflow; import templates are separate from export reports." },
                { "فتح الاستيراد", "Open import" },
                { "كشوف التصدير للتحليل؛ افتح الاستيراد لتنزيل قالب مستقل ومراجعة البيانات قبل الاعتماد.", "Export reports are for analysis. Open import to download its separate template and review data before approval." },
                { "يفتح استيراد الفواتير شاشة واحدة للبيع والشراء؛ استخدم قالبها المستقل وراجع البيانات قبل الاعتماد.", "Invoice import opens one workflow for sales and purchases. Use its separate template and review data before approval." },
                { "استيراد الإعدادات متاح للمدير فقط.", "Only managers can import from settings." },
                { "هذا النوع للتصدير فقط؛ لا يوجد له استيراد مباشر.", "This data type is export-only; direct import is unavailable." },
                { "اختر البيانات؛ استخدم البحث أو الفترة لتحديد النتائج قبل التصدير.", "Choose the data, then narrow the results by search or date before exporting." },
                { "نوع البيانات", "Data type" },
                { "بحث اختياري", "Optional search" },
                { "الأدوية والأرصدة الحالية", "Medicines and current stock" },
                { "سطور فواتير البيع", "Sales invoice lines" },
                { "سطور البيع القديمة (دون تاريخ محفوظ)", "Legacy sales lines (no stored date)" },
                { "البيع القديم (بلا تاريخ)", "Legacy sales (undated)" },
                { "الأداء اليومي", "Daily performance" },
                { "القالب المحدد", "Selected template" },
                { "مصدر القالب", "Template source" },
                { "تفاصيل القالب المحدد", "Selected template details" },
                { "تعذر حفظ القالب. تحقق من الاسم والفترة.", "Could not save the template. Check its name and date range." },
                { "تم حفظ القالب محلياً لهذا المستخدم. لا يحتوي على بيانات أو SQL.", "The template was saved locally for this user. It contains no report data or SQL." },
                { "تم حفظ القالب لهذا المستخدم؛ يشمل الفلاتر وخيارات العرض فقط.", "The template was saved for this user; it contains filters and display options only." },
                { "اختر قالباً محفوظاً أولاً.", "Select a saved template first." },
                { "مصدر هذا القالب لم يعد متاحاً.", "This template's data source is no longer available." },
                { "حذف القالب «{0}»؟ لن يؤثر ذلك على بيانات قاعدة البيانات.", "Delete template «{0}»? Database records will not be affected." },
                { "تعذر حذف القالب.", "Could not delete the template." },
                { "تم حذف القالب المحلي؛ لم تتغير بيانات النظام.", "The local template was deleted; system records were unchanged." },
                { "اعرض نتيجة التقرير أولاً.", "View the report results first." },
                { "لا توجد نتائج لتصديرها.", "There are no results to export." },
                { "تعذر تأكيد تصدير التقرير؛ تحقق من الملف والمسار قبل إعادة المحاولة.", "Report export could not be confirmed; check the file and path before retrying." },
                { "تعذر فتح معاينة الطباعة.", "Could not open the print preview." },
                { "سطور فواتير المشتريات", "Purchase invoice lines" },
                { "حركات المخزون", "Inventory movements" },
                { "سطور القيود المحاسبية", "Journal entry lines" },
                { "حركات النقد المشترك", "Shared cash movements" },
                { "تعريفات صناديق النقد", "Cashbox definitions" },
                { "تُصدّر تعريفات الصناديق وإسناد الكاشير فقط دون أرصدة مالية، ويمكن استيراد الملف من شاشة صناديق النقد.", "Exports cashbox definitions and cashier assignments only, without balances. Import this file from the Cashboxes screen." },
                { "تعريفات الصناديق تحتاج ترحيل قاعدة البيانات قبل تصديرها.", "Cashbox definitions require a database migration before export." },
                { "هذا كشف حساب النقد المشترك، ولا يحدد أرصدة كل كاشير. يُرفض التصدير عند 2,000 صف.", "This is the shared cash ledger, not individual cashier balances. Export stops at 2,000 rows." },
                { "حالة تصدير Excel", "Excel export status" },
                { "معاينة التصدير؛ لم تُقرأ بيانات ولم يُنشأ ملف.", "Export preview; no data was read and no file was created." },
                { "يُرفض التصدير عند 2,000 صف؛ ضيّق الفترة أو استخدم البحث.", "Export stops at 2,000 rows; narrow the date range or search." },
                { "يُرفض التصدير عند 2,000 صف؛ استخدم البحث لتضييق النتائج.", "Export stops at 2,000 rows; use search to narrow the results." },
                { "وصل المصدر إلى 2,000 صف؛ لم يُحفظ ملف جديد وبقي الملف السابق دون تغيير. ضيّق الفترة أو البحث.", "The source reached 2,000 rows. No new file was saved; any existing file is unchanged. Narrow the date range or search." },
                { "تصدير الإعدادات متاح للمدير فقط.", "Only managers can export from settings." },
                { "اختر نوع البيانات أولاً.", "Choose a data type first." },
                { "نوع البيانات خارج نطاق التصدير المعتمد.", "This data type is outside the approved export scope." },
                { "فترة التصدير غير صحيحة.", "The export date range is invalid." },
                { "تصدير بيانات Excel", "Export Excel data" },
                { "جارٍ تجهيز البيانات وحفظ ملف Excel...", "Preparing data and saving the Excel file..." },
                { "يجري حفظ ملف Excel؛ انتظر اكتمال العملية.", "The Excel file is being saved. Wait for completion." },
                { "تم تصدير {0} صفًا إلى Excel.", "Exported {0} rows to Excel." },
                { "استيراد أصناف Excel", "Import medicines from Excel" },
                { "استيراد ملف إكسل لتعريفات الأصناف", "Import medicine definitions from Excel" },
                { "حمّل القالب، أدخل الأصناف والشركة المسجلة، ثم راجع الصفوف قبل الاعتماد. أدخل الكميات لاحقًا من المشتريات.", "Download the template, add medicines and registered companies, then review rows before importing. Enter quantities through purchases later." },
                { "اختيار ملف", "Choose file" },
                { "تنزيل قالب", "Download template" },
                { "اعتماد الأصناف", "Import medicines" },
                { "معاينة أصناف Excel", "Excel medicine preview" },
                { "اختر ملف Excel لعرض الأصناف.", "Choose an Excel file to preview medicines." },
                { "لم يتم اختيار ملف.", "No file selected." },
                { "الملف: {0}", "File: {0}" },
                { "صفوف الملف: {0} — أخطاء: {1}", "File rows: {0} — errors: {1}" },
                { "معاينة مرئية؛ لم يُقرأ ملف أو تُفتح قاعدة بيانات.", "Visual preview; no file or database was opened." },
                { "استورد تعريفات جديدة برصيد صفر؛ أدخل الكميات وتكلفتها عبر شاشة المشتريات المعتمدة.", "Import new medicine definitions with zero stock. Enter quantities and cost through approved purchases." },
                { "العملية جارية؛ انتظر اكتمال المعاينة أو الاستيراد.", "Operation in progress. Wait for preview or import to finish." },
                { "اختيار ملف أصناف Excel", "Choose medicine Excel file" },
                { "جارٍ قراءة ملف Excel والتحقق من صفوفه...", "Reading and validating Excel rows..." },
                { "تعذرت معاينة الملف المحدد؛ لم تُعتمد أي أصناف.", "The selected file could not be previewed; no medicines were imported." },
                { "معاينة ملف الأصناف", "Preview medicine file" },
                { "حفظ قالب استيراد الأصناف", "Save medicine import template" },
                { "حُفظ قالب الأصناف؛ املأ الأعمدة ثم اختر الملف للمعاينة.", "Template saved. Fill the columns, then choose the file for preview." },
                { "حفظ قالب الأصناف", "Save medicine template" },
                { "استيراد تعريفات الأصناف متاح للمدير فقط.", "Only managers can import medicine definitions." },
                { "سيُضاف {0} تعريف صنف برصيد صفر. الأسماء الموجودة ستُتجاوز دون تعديل. متابعة؟", "Import {0} medicine definitions with zero stock? Existing names will be skipped without changes." },
                { "جارٍ اعتماد تعريفات الأصناف؛ انتظر اكتمال العملية.", "Importing medicine definitions. Wait for completion." },
                { "اكتمل الاستيراد: {0} صنف جديد، {1} اسم موجود دون تعديل.", "Import complete: {0} new medicines, {1} existing names unchanged." },
                { "تعذر تأكيد نتيجة الاستيراد؛ راجع قائمة الأصناف قبل إعادة المحاولة.", "Import outcome could not be confirmed. Review the medicine list before retrying." },
                { "اعتماد أصناف Excel", "Import Excel medicines" },
                { "اسم الدواء", "Medicine name" },
                { "تاريخ الإنتاج", "Production date" },
                { "الشركة المصنعة", "Manufacturer" },                { "تعريف أصناف جديدة برصيد صفر مع معاينة قبل الاعتماد", "Preview new medicines with zero stock before import" },
                { "معاينة: ", "Preview: " },
                { " — لم يُفتح اتصال أو تُحفظ بيانات.", " — no connection or save was performed." },
                { "حالة المعاينة: ", "Preview status: " },
                { "معاينة مرئية؛ الأزرار لا تفتح خدمات أو قاعدة بيانات.", "Visual preview; actions do not open services or a database." },
                { "إضافة أو تعطيل المزايا المدققة", "Enable or disable reviewed features" },
                { "الدول والعملات وعملة النظام", "Countries, currencies and system currency" },
                { "اختيار CSV", "Choose CSV" },
                { "استيراد إلى QA", "Import to QA" },
                { "منشئ تقارير تشغيلي", "Operational report builder" },
                { "جاهزية إقفال الفترة", "Period-close readiness" },
                { "فحص قرار قراءة فقط قبل الإقفال. لا ينشئ قيود إقفال ولا يعالج الفروقات تلقائياً.", "Read-only checks before closing. No closing entries are posted or discrepancies corrected automatically." },
                { "تاريخ بداية فحص الجاهزية", "Readiness start date" },
                { "تاريخ نهاية فحص الجاهزية", "Readiness end date" },
                { "نتائج فحص جاهزية الإقفال", "Period-close readiness results" },
                { "نطاق تاريخ فحص الجاهزية", "Readiness date range" },
                { "إجراءات فحص الجاهزية", "Readiness check actions" },
                { "إجراءات التقرير", "Report actions" },
                { "تحديث قائمة العملات", "Refresh currencies" },
                { "قائمة العملات", "Currency list" },
                { "إجراءات العملات", "Currency actions" },
                { "اختصار أو علامة العملة", "Currency symbol" },
                { "يحدث القائمة دون حفظ أو تغيير المدخلات غير المحفوظة.", "Refreshes the list without saving or changing unsaved input." },
                { "تعذر تحديث العملات. اضغط «تحديث» للمراجعة.", "Currencies could not be refreshed. Select Refresh to review them." },
                { "اختر عملة أو أضف جديدة.", "Select a currency or add a new one." },
                { "لم تعد العملة المحددة موجودة. بقيت المدخلات؛ اختر عملة أو اضغط إضافة جديدة قبل الحفظ.", "The selected currency no longer exists. Your input was kept; select a currency or choose Add new before saving." },
                { "تعذر تحديث العملات. أغلق النافذة وأعد فتحها للمراجعة.", "Currencies could not be refreshed. Close and reopen the window to review them." },
                { "اتجاه الصفحة وكثافة الصفوف", "Page orientation and row density" },
                { "نتيجة جاهزية الإقفال", "Period-close readiness status" },
                { "إغلاق فحص جاهزية الفترة", "Close readiness checks" },
                { "الفحص", "Check" }, { "عدد الملاحظات", "Issues" }, { "التفصيل", "Details" },
                { "سليم", "Passed" }, { "يحتاج مراجعة", "Needs review" },
                { "سلامة القيود", "Journal integrity" },
                { "مطابقة المشتريات", "Purchase reconciliation" },
                { "أرصدة مخزون سالبة", "Negative stock balances" },
                { "تسلسل حركات المخزون", "Inventory movement sequence" },
                { "قيود بلا أسطر أو غير متوازنة ضمن الفترة.", "Entries with no lines or an imbalance in the selected period." },
                { "إجمالي فاتورة شراء لا يطابق مجموع تفاصيلها.", "A purchase invoice total differs from its line totals." },
                { "رصيد حالي أقل من الصفر؛ الفحص يشمل المخزون الحالي.", "A current balance is below zero; this checks current stock." },
                { "تطابق قبل/بعد كل حركة مع الرصيد الحالي؛ الفحص يشمل كل الحركات.", "Each movement's before and after balance matches current stock; all movements are checked." },
                { "معاينة واجهة القراءة فقط؛ لم يُنفذ فحص على قاعدة البيانات.", "Read-only preview; no database check was run." },
                { "المعاينة للعرض فقط؛ لم يُنفذ فحص على قاعدة البيانات.", "Preview only; no database check was run." },
                { "تاريخ النهاية يجب أن يكون بعد تاريخ البداية أو مساوياً له.", "The end date must be on or after the start date." },
                { "لا توجد بيانات مطابقة لخيارات البحث الحالية.", "No data matches the current filters." },
                { "لا توجد بيانات جاهزة للطباعة.", "There is no data ready to print." },
                { "عدد السجلات: {0} | تاريخ الطباعة: {1}", "Records: {0} | Printed: {1}" },
                { "تم فتح معاينة الطباعة.", "Print preview opened." },
                { "تعذر فتح معاينة الطباعة: ", "Could not open print preview: " },
                { "فتح معاينة الطباعة", "Open print preview" },
                { "لا توجد بيانات جاهزة للتصدير.", "There is no data ready to export." },
                { "تم تصدير التقرير بنجاح.", "Report exported successfully." },
                { "تعذر تصدير التقرير: ", "Could not export report: " },
                { "تصدير التقرير", "Export report" },
                { "أدخل قيمة صحيحة في حقل {0}.", "Enter a valid value in {0}." },
                { "يجب ألا تكون قيمة {0} سالبة.", "{0} cannot be negative." },
                { "تاريخ البداية يجب أن يسبق تاريخ النهاية أو يساويه.", "The start date must be on or before the end date." },
                { "أدخل قيمة مالية صحيحة في حقل {0}.", "Enter a valid monetary value in {0}." },
                { "اختر تاريخ بداية ونهاية صحيحين للفحص.", "Choose valid start and end dates for the check." },
                { "جاهز للمراجعة النهائية: لا توجد ملاحظات في الفحوص المعروضة. لم يُنفذ إقفال تلقائي.", "Ready for final review: the displayed checks found no issues. No automatic close was performed." },
                { "غير جاهز للإقفال: عالج الملاحظات الظاهرة ثم أعد الفحص. لم تُعدّل أي بيانات.", "Not ready to close: resolve the displayed issues and check again. No data was changed." },
                { "فحص سلامة القيود", "Journal integrity check" },
                { "جارٍ فحص سلامة القيود...", "Checking journal integrity..." },
                { "لا تملك صلاحية فحص سلامة القيود أو تغيرت جلسة المستخدم؛ أعد فتح الفحص.", "You do not have permission to check journal integrity, or the user session changed. Reopen the check." },
                { "تغير المستخدم أو الاتصال أو الصلاحية؛ أعد تحميل فحص سلامة القيود.", "The user, connection, or permissions changed. Reload the journal integrity check." },
                { "مصدر فحص سلامة القيود لا يدعم القراءة غير المتزامنة.", "The journal integrity source does not support asynchronous reads." },
                { "لم يرجع فحص سلامة القيود نتيجة.", "The journal integrity check returned no result." },
                { "لا تملك صلاحية قراءة التقارير أو تغيرت جلسة المستخدم؛ أعد تحميل التقرير.", "You do not have permission to read reports, or the user session changed. Reload the report." },
                { "فحص مطابقة المخزون", "Inventory integrity check" },
                { "فحص قراءة فقط: يعرض القيود غير المتوازنة أو التي لا تحتوي أسطراً، ولا يصلحها تلقائياً.", "Read-only check of unbalanced journal entries and entries without lines; no automatic correction." },
                { "فحص قراءة فقط: يراجع تسلسل حركات الصنف ومطابقة الرصيد الحالي مع آخر حركة مسجلة.", "Read-only check of movement sequence and current stock against the latest movement." },
                { "إعادة الفحص", "Check again" },
                { "إعادة فحص سلامة القيود", "Check journal integrity again" },
                { "إغلاق فحص سلامة القيود", "Close journal integrity check" },
                { "نتيجة فحص سلامة القيود", "Journal integrity status" },
                { "تاريخ بداية فحص سلامة القيود", "Journal check start date" },
                { "تاريخ نهاية فحص سلامة القيود", "Journal check end date" },
                { "القيود التي تحتاج مراجعة", "Journal entries needing review" },
                { "إعادة فحص مطابقة المخزون", "Check inventory integrity again" },
                { "إغلاق فحص مطابقة المخزون", "Close inventory integrity check" },
                { "نتيجة فحص مطابقة المخزون", "Inventory integrity status" },
                { "حركات المخزون التي تحتاج مراجعة", "Inventory movements needing review" },
                { "رقم القيد", "Entry number" }, { "المصدر", "Source" }, { "رقم المرجع", "Reference" },
                { "البيان", "Description" }, { "عدد الأسطر", "Lines" }, { "إجمالي المدين", "Total debit" },
                { "إجمالي الدائن", "Total credit" }, { "الفرق", "Difference" },
                { "قيد بلا أسطر", "Entry without lines" }, { "القيد غير متوازن", "Unbalanced entry" },
                { "رقم الحركة", "Movement number" }, { "تاريخ الحركة", "Movement date" },
                { "الرصيد قبل", "Balance before" }, { "الرصيد بعد", "Balance after" },
                { "الرصيد الحالي", "Current balance" },
                { "الرصيد الحالي لا يطابق آخر حركة", "Current balance differs from the latest movement" },
                { "تسلسل الحركات غير متصل", "Movement sequence has a gap" },
                { "فحص سليم: لا توجد قيود غير متوازنة أو بلا أسطر في الفترة المحددة.", "Check passed: no unbalanced or empty journal entries in the selected period." },
                { "توجد {0} قيود تحتاج مراجعة محاسبية. لم يُنفذ أي إصلاح تلقائي.", "{0} journal entries need accounting review. No automatic correction was made." },
                { "فحص سليم: لا توجد فروقات في الحركات المسجلة أو الرصيد الحالي.", "Check passed: no differences in recorded movements or current stock." },
                { "توجد {0} حركات تحتاج مراجعة. لم تُعدّل أي كمية تلقائياً.", "{0} movements need review. No quantities were changed automatically." },
                { "نقطة البيع", "Point of sale" },
                { "ملخص الفاتورة", "Invoice summary" },
                { "الدفع عند اعتماد الفاتورة", "Payment is posted when the invoice is finalized" },
                { "ملاحظة داخلية", "Internal note" },
                { "نوع العميل: عميل عام", "Customer type: General customer" },
                { "نوع العميل: عميل مسجل", "Customer type: Registered customer" },
                { "نوع العميل: عميل غير مسجل", "Customer type: Unregistered customer" },
                { "يعرض نوع العميل فقط. اضغط لاختيار سجل محفوظ من شاشة العملاء والموردين.", "Shows the customer type only. Select a saved record from customer and supplier management." },
                { "إضافة الصنف إلى الفاتورة", "Add item to invoice" },
                { "تطبيق الخصم", "Apply discount" },
                { "حفظ الفاتورة دون طباعة", "Save invoice without printing" },
                { "إتمام البيع وطباعة الفاتورة", "Complete sale and print invoice" },
                { "طباعة الفاتورة A4", "Print A4 invoice" },
                { "مسح الفاتورة الحالية", "Clear current invoice" },
                { "إغلاق نقطة البيع", "Close point of sale" },
                { "العودة إلى الفاتورة", "Return to invoice" },
                { "ملاحظة داخلية للفاتورة", "Internal invoice note" },
                { "امسح الباركود أو أدخله يدويًا ثم اضغط Enter.", "Scan or enter the barcode, then press Enter." },
                { "أدخل كمية صحيحة ثم اضغط Enter لإضافة الصنف.", "Enter a valid quantity, then press Enter to add the item." },
                { "نسبة الخصم", "Discount rate" },
                { "أدخل نسبة بين صفر وأقل من مئة ثم اضغط تطبيق.", "Enter a rate from zero to less than one hundred, then apply." },
                { "المخزون المتاح — اختر صنفاً أولاً", "Available stock — select an item first" },
                { "مسح سريع للباركود، فاتورة واضحة، واعتماد آمن للطباعة", "Scan barcodes quickly, review a clear invoice, and finalize printing safely." },
                { "الفاتورة الحالية", "Current invoice" },
                { "سجل الفواتير", "Invoice history" },
        { "الرجوع إلى الأقسام", "Back to sections" },
        { "بحث في سجل الفواتير", "Search invoice history" },
        { "تم اعتماد الفاتورة. تعذر إكمال الطباعة الآن — يمكنك إعادة الطباعة من السجل.", "The invoice was approved. Printing could not finish now — you can reprint from the history." },
        { "تم اعتماد فاتورة المشتريات {0}. تعذر {1} الآن — يمكنك إعادة الطباعة من السجل.", "Purchase invoice {0} was approved. {1} could not finish now — you can reprint from the history." },
        { "عرض المزيد", "Show more" },
        { "عرض المزيد من السجل", "Show more of the history" },
        { "تصدير السجل إلى ملف CSV", "Export the history to a CSV file" },
        { "لا توجد بيانات لتصديرها.", "There is no data to export." },
        { "تعذر حفظ ملف التصدير. تحقق من صلاحية المجلد.", "The export file could not be saved. Check the folder permissions." },
        { "تم تصدير السجل إلى Excel بنجاح.", "The history was exported to Excel successfully." },
                { "سجل العملاء", "Customer history" },
                { "بنود الفاتورة", "Invoice items" },
                { "سجل فواتير البيع", "Sales invoice history" },
                { "انقر مرتين على أي فاتورة لعرض تفاصيلها", "Double-click an invoice to view its details." },
                { "امسح الباركود أو ابحث عن صنف (F2)", "Scan a barcode or search for an item (F2)" },
                { "بحث الأصناف (F3)", "Search items (F3)" },
                { "اختيار الصنف", "Select item" },
                { "الكمية (F4)", "Quantity (F4)" },
                { "نسبة الخصم %", "Discount rate %" },
                { "الكاشير", "Cashier" },
                { "جميع الكاشيرين", "All cashiers" },
                { "اسم العميل", "Customer name" },
                { "هاتف العميل", "Customer phone" },
                { "عنوان العميل", "Customer address" },
                { "إضافة للسلة", "Add to cart" },
                { "تطبيق", "Apply" },
                { "اعتماد بيانات العميل", "Confirm customer details" },
                { "حفظ بدون طباعة", "Save without printing" },
                { "إتمام البيع وطباعة  F9", "Complete sale and print  F9" },
                { "طباعة A4", "Print A4" },
                { "حفظ PDF", "Save PDF" },
                { "مسح الفاتورة", "Clear invoice" },
                { "العودة", "Return to invoice" },
                { "رصيد العميل المتاح: اختر عميلاً مسجلاً", "Available customer credit: Choose a registered customer" },
                { "استخدام رصيد العميل في هذه الفاتورة", "Use customer credit for this invoice" },
                { "سجل فواتير البيع المعتمدة", "Approved sales invoices" },
                { "الفواتير الجديدة القابلة للاستئناف", "Resumable new invoices" },
                { "عرض الفاتورة", "View invoice" },
                { "عرض سجل العميل", "View customer history" },
                { "أدخل كمية كل دفعة وحدد طريقة الاسترداد، ثم راجع الإشعار قبل الاعتماد.", "Enter the quantity for each lot, choose the refund method, then review the credit note before posting." },
                { "دفعات المرتجع المتاحة", "Available return lots" },
                { "اعتماد إشعار المرتجع", "Post return credit note" },
                { "إلغاء إشعار المرتجع", "Cancel return credit note" },
                { "حالة إشعار المرتجع", "Return credit note status" },
                { "قسم دفعات الفاتورة المتاحة للإرجاع", "Returnable invoice lots section" },
                { "الدفعة", "Lot" },
                { "المتاح", "Available" },
                { "كمية المرتجع", "Return quantity" },
                { "إعادة للمخزون", "Return to stock" },
                { "حجر مؤقت", "Temporary quarantine" },
                { "نقداً (المسار الأصلي الحالي)", "Cash (current original method)" },
                { "نقداً", "Cash" },
                { "بطاقة", "Card" },
                { "رصيد العميل", "Customer credit" },
                { "إعادة إلى المخزون", "Return to stock" },
                { "حجر", "Quarantine" },
                { "تالف", "Damaged" },
                { "معاينة واجهة المرتجع؛ لم تُقرأ فاتورة ولم يُعتمد إشعار.", "Return preview: no invoice was read and no credit note was posted." },
                { "سجل إشعارات المرتجع المعتمدة", "Approved return credit notes" },
                { "حالة سجل إشعارات المرتجع", "Return credit note history status" },
                { "معاينة وطباعة إشعار المرتجع", "Preview and print return credit note" },
                { "إغلاق سجل إشعارات المرتجع", "Close return credit note history" },
                { "قسم الإشعارات الدائنة المعتمدة", "Approved credit notes section" },
                { "معاينة السجل دون اتصال بقاعدة البيانات أو طباعة.", "History preview without database access or printing." },
                { "رقم الإشعار", "Credit note number" },
                { "رقم الفاتورة الأصلية", "Original invoice number" },
                { "تاريخ الاعتماد UTC", "Approved at (UTC)" },
                { "المنفذ", "Posted by" },
                { "المتاح للإرجاع", "Available to return" },
                { "تحميل الفترات المحاسبية", "Load accounting periods" },
                { "إنشاء فترة محاسبية", "Create accounting period" },
                { "إقفال الفترة المحاسبية", "Close accounting period" },
                { "عملة النظام الحالية", "Current system currency" },
                { "إغلاق إعدادات العملات", "Close currency settings" },
                { "العربية هي اللغة الافتراضية", "Arabic is the default language" },
                { "إعدادات قاعدة البيانات", "Database settings" },
                { "إضافة الدواء", "Add medicine" },
                { "تعديل الدواء", "Edit medicine" },
                { "حذف الدواء", "Delete medicine" },
                { "توليد الباركود", "Generate barcode" },
                { "ابحث بالاسم أو الباركود أو الشركة. Enter للاختيار، Esc للإغلاق.", "Search by name, barcode, or manufacturer. Press Enter to select or Esc to close." },
                { "طباعة التقرير", "Print report" },
                { "مصدر غير معروف", "Unknown source" },
                { "نسبة الضريبة", "Tax rate" },
                { "الاسم العربي للمادة الفعالة", "Arabic name of active ingredient" },
                { "الاسم اللاتيني للمادة الفعالة", "Latin name of active ingredient" },
                { "عنوان المرجع العلمي", "Scientific reference title" },
                { "رابط المرجع العلمي", "Scientific reference URL" },
                { "تاريخ الاطلاع الاختياري", "Optional access date" },
                { "الصنف المرتبط بالمادة الفعالة", "Medicine linked to active ingredient" },
                { "تركيز المادة الفعالة", "Active ingredient strength" },
                { "وحدة تركيز المادة الفعالة", "Active ingredient strength unit" },
                { "أدخل المرجع مع المادة الجديدة، أو اختر مادة لإضافة مصدر وربطها بصنف.", "Enter a source with the new ingredient, or select an ingredient to add a source and link a medicine." },
                { "لا يمكن تحميل إدارة المواد الفعالة:", "Cannot load active ingredient management: " },
                { "لا يمكن تحميل تفاصيل المادة:", "Cannot load ingredient details: " },
                { "حُفظت المادة ومصدرها معاً بنجاح.", "Ingredient and source saved successfully." },
                { "إضافة مادة فعالة", "Add active ingredient" },
                { "أضيف المصدر وحُفظ أثر المستخدم.", "Source added and user action recorded." },
                { "إضافة مصدر للمادة", "Add ingredient source" },
                { "سيبقى المصدر محفوظاً للتدقيق، وسيُعلّم كمعطل. هل تريد المتابعة؟", "The source will remain in the audit record and be marked inactive. Continue?" },
                { "عُطل المصدر مع الاحتفاظ بالسجل.", "Source deactivated; its record was retained." },
                { "تعطيل مصدر المادة", "Deactivate ingredient source" },
                { "تغيير حالة المادة", "Change ingredient status" },
                { "حُفظ ربط المادة بالصنف.", "Ingredient-to-medicine link saved." },
                { "حفظ ربط المادة الفعالة", "Save active ingredient link" },
                { "تاريخ الاطلاع غير صحيح؛ صححه أو امسح الحقل لأنه اختياري.", "Invalid access date; correct or clear this optional field." },
                { "تأكيد إنشاء قاعدة جديدة", "Confirm new database creation" },
                { "سيتم إنشاء قاعدة تشغيل فارغة", "An empty operational database will be created" },
                { "الخادم:", "Server: " },
                { "قاعدة جديدة:", "New database: " },
                { "لن تُنقل بيانات أي قاعدة سابقة تلقائياً.", "Data from an existing database will not be moved automatically." },
                { "للتأكيد، اكتب اسم القاعدة كما هو:", "To confirm, type the database name exactly:" },
                { "اسم قاعدة البيانات للتأكيد", "Database name confirmation" },
                { "إنشاء القاعدة", "Create database" },
                { "تم حفظ المظهر. الخط المستخدم: {0}. إذا لم يتوفر الخط المختار، يُستخدم البديل تلقائياً.", "Appearance saved. Font in use: {0}. An unavailable font is replaced automatically." },
                { "تعذر حفظ المظهر. احتُفظ بالإعداد السابق.", "Could not save appearance. The previous setting was retained." },
                { "إعدادات قاعدة البيانات محمية. شغّل التطبيق بحساب Windows محلي مسؤول للوصول إليها.", "Database settings are protected. Run the application as a local Windows administrator to access them." },
                { "الاتصال النشط: {0} على {1}. اختبره ثم احفظه قبل الاستخدام.", "Active connection: {0} on {1}. Test and save it before use." },
                { "جار البحث عن خوادم SQL Server...", "Searching for SQL Server instances..." },
                { "تم تحديث قائمة الخوادم. يمكنك تجربة '.', أو '.\\SQLEXPRESS'، أو اختيار خادم مكتشف.", "Server list refreshed. Try '.', '.\\SQLEXPRESS', or a discovered server." },
                { "تعذر البحث عن خوادم SQL Server.", "Could not search for SQL Server instances." },
                { "جار تحميل قواعد البيانات...", "Loading databases..." },
                { "لم يتم العثور على قواعد بيانات للمستخدم. يمكنك كتابة اسم قاعدة جديدة.", "No databases were found for this user. You can enter a new database name." },
                { "تم تحميل قواعد البيانات. اختر قاعدة موجودة أو اكتب اسماً جديداً.", "Databases loaded. Select an existing database or enter a new name." },
                { "تعذر تحميل قواعد البيانات.", "Could not load databases." },
                { "جار اختبار الاتصال...", "Testing connection..." },
                { "نجح الاتصال. لم يتم حفظ الإعدادات أو تهيئة الجداول بعد.", "Connection succeeded. Settings have not been saved and tables have not been initialized." },
                { "فشل اختبار الاتصال.", "Connection test failed." },
                { "جار التحقق من قاعدة التشغيل دون تعديلها...", "Inspecting the operational database without modifying it..." },
                { "لم يتم تغيير اتصال التشغيل.", "The operational connection was not changed." },
                { "جار حفظ الإعدادات...", "Saving settings..." },
                { "تم حفظ الاتصال، لكن يجب إنشاء المدير الأول قبل استخدام النظام.", "Connection saved. Create the first administrator before using the system." },
                { "تم حفظ إعدادات الاتصال. أنشئ المدير الأول قبل تسجيل الدخول.", "Connection settings saved. Create the first administrator before signing in." },
                { "تم حفظ إعدادات قاعدة البيانات في ملف التشغيل الدائم.", "Database settings saved in the persistent runtime file." },
                { "تم حفظ إعدادات قاعدة البيانات بنجاح.", "Database settings saved successfully." },
                { "تم حفظ اتصال قاعدة البيانات، لكن تعذر حفظ إعداد المظهر.", "Database connection saved, but appearance settings could not be saved." },
                { "تم حفظ اتصال قاعدة البيانات بنجاح، لكن تعذر حفظ إعداد المظهر.", "Database connection saved successfully, but appearance settings could not be saved." },
                { "تعذر حفظ الإعدادات.", "Could not save settings." },
                { "رمز الدعم:", "Support code: " },
                { "الرجاء إدخال اسم خادم SQL.", "Enter the SQL Server name." },
                { "الرجاء اختيار أو كتابة اسم قاعدة البيانات.", "Select or enter the database name." },
                { "هذه النسخة تدعم مصادقة Windows فقط. فعّل الخيار ثم أعد المحاولة.", "This version supports Windows authentication only. Enable it and try again." },
                { "تأكيد تغيير قاعدة التشغيل", "Confirm operational database change" },
                { "سيصبح الاتصال التالي هو اتصال التشغيل:\n\nالخادم: {0}\nقاعدة البيانات: {1}\nحالة الفحص: قاعدة موجودة، والجداول الأساسية المكتشفة: {2}/4\n\nلا يؤدي تغيير الاتصال إلى حذف بيانات القاعدة السابقة. ستبقى البيانات هناك، لكن التطبيق سيعرض بيانات القاعدة الجديدة فقط.", "The following connection will become active:\n\nServer: {0}\nDatabase: {1}\nInspection: existing database; core tables found: {2}/4\n\nChanging the connection does not delete data from the previous database. That data remains there; the application will display data from the new database only." },
                { "انتظر حتى تكتمل العملية الحالية ثم أغلق النافذة.", "Wait for the current operation to finish before closing this window." },
                { "تعذر إكمال الاتصال بقاعدة البيانات.", "Could not complete the database connection." },
                { "خطوات الحل:", "Troubleshooting steps:" },
                { "1) شغّل التطبيق بحساب Windows محلي مسؤول، ثم افتح شاشة الدخول واضغط F1.", "1) Run the application as a local Windows administrator, then press F1 on the sign-in screen." },
                { "2) جرّب السيرفرات: . ثم .\\SQLEXPRESS ثم localhost\\SQLEXPRESS.", "2) Try these servers: . then .\\SQLEXPRESS then localhost\\SQLEXPRESS." },
                { "3) تأكد من تشغيل خدمة SQL Server من Services.", "3) Make sure the SQL Server service is running in Services." },
                { "4) إذا كنت تستخدم اسم مثيل مثل SQLEXPRESS فتأكد من تشغيل SQL Server Browser.", "4) For a named instance such as SQLEXPRESS, make sure SQL Server Browser is running." },
                { "5) بعد اختيار الخادم اضغط تحميل قواعد البيانات أو اختبار الاتصال.", "5) After selecting the server, choose Load databases or Test connection." },
                { "تعذر إكمال العملية", "Could not complete the operation" },
                { "إكمال العملية", "complete the operation" },
                { "تعذر {0} الآن. لا تُعرض التفاصيل التقنية هنا.\n\nإذا كانت العملية تتضمن حفظاً، راجع السجل قبل تكرارها. وإذا تكرر الأمر، أرسل رمز الدعم إلى مسؤول النظام: {1}", "Could not {0} now. Technical details are not displayed here.\n\nIf this operation saves data, review the record before retrying. If the issue persists, send this support code to your administrator: {1}" },
                { "المعاينة للعرض فقط؛ لم تُقرأ دفعات الفاتورة.", "Preview only; invoice lots were not loaded." },
                { "أدخل كميات صحيحة لكل دفعة؛ صفر لاستبعادها.", "Enter whole quantities for each lot; use zero to exclude it." },
                { "تعذر تحميل دفعات الفاتورة. تحقق من الاتصال ثم أعد فتح الشاشة.", "Could not load invoice lots. Check the connection and reopen this screen." },
                { "تحميل دفعات المرتجع", "Load return lots" },
                { "المعاينة للعرض فقط؛ لم يُعتمد إشعار مرتجع.", "Preview only; no return credit note was posted." },
                { "سيُنشأ إشعار دائن وقيد ومتابعة دفعات. هل تريد الاعتماد؟", "A credit note, journal entry and lot tracking will be created. Post this return?" },
                { "استُرجع إشعار المرتجع المعتمد مسبقاً رقم {0}.", "Previously posted return credit note {0} was retrieved." },
                { "تم اعتماد إشعار المرتجع رقم {0}.", "Return credit note {0} was posted." },
                { "لم يبدأ اعتماد المرتجع.", "Return posting has not started." },
                { "لم يبدأ اعتماد المرتجع. تعذر إكمال العملية؛ تحقق من الإدخال ثم أعد المحاولة.", "Return posting has not started. The operation could not be completed; check the input and try again." },
                { "تعذر تأكيد نتيجة الاعتماد. راجع سجل الإشعارات؛ إعادة المحاولة هنا تستخدم الطلب نفسه. لا تنشئ طلباً جديداً قبل التحقق.\nرقم الطلب: {0}\n\nرمز الدعم: {1}", "The posting result could not be confirmed. Review the credit note history; retrying here uses the same request. Verify the result before creating another request.\nRequest: {0}\n\nSupport code: {1}" },
                { "إعادة نفس الطلب", "Retry same request" },
                { "جارٍ اعتماد إشعار المرتجع...", "Posting return credit note..." },
                { "راجع السجل أو أعد نفس الطلب دون تعديل.", "Review the history or retry the same request without changes." },
                { "طلب مرتجع معلق: {0}. راجع السجل أو أعد الطلب نفسه دون تعديل.", "Pending return request: {0}. Review the history or retry the same request without changes." },
                { "لم يُعتمد المرتجع. صحح البيانات أو الفترة ثم أعد المحاولة.", "The return was not posted. Correct the data or accounting period and retry." },
                { "تغيرت جلسة المستخدم أو قاعدة التشغيل. أغلق الشاشة وافتحها مجددًا.", "The user session or database changed. Close and reopen this screen." },
                { "تغير سياق المرتجع؛ احتُفظ بأي طلب معلق في سياقه الأصلي.", "The return context changed; any pending request remains saved in its original context." },
                { "تعذر فتح استعادة المرتجع. قد تكون الشاشة مفتوحة في نافذة أخرى أو ملف الاستعادة غير متاح. أغلقها ثم أعد المحاولة.", "Return recovery is unavailable. Another window may be open or the recovery file may be inaccessible. Close that window and retry." },
                { "اعتمد المرتجع، لكن تعذر تحديث ملف الاستعادة. عند فتحه مجددًا استخدم الطلب نفسه.", "The return was posted, but its recovery file could not be updated. Retry the same request when reopening it." },
                { "تغير سياق المرتجع أو تعذر فتح الاستعادة؛ أغلق الشاشة وافتحها مجددًا.", "The return context changed or recovery is unavailable. Close and reopen the screen." },
                { "صحح قيمة الخلية الحالية قبل الاعتماد.", "Correct the current cell value before posting." },
                { "سبب المرتجع مطلوب.", "A return reason is required." },
                { "اختر طريقة استرداد صالحة.", "Choose a valid refund method." },
                { "أدخل كمية مرتجع واحدة على الأقل.", "Enter at least one return quantity." },
                { "كمية المرتجع يجب أن تكون عدداً صحيحاً غير سالب. أدخل صفراً لاستبعاد الدفعة.", "Return quantity must be a non-negative whole number. Enter zero to exclude the lot." },
                { "كمية المرتجع تتجاوز المتاح للدفعة.", "Return quantity exceeds the available quantity for this lot." },
                { "اختر حسابا من الشجرة أو أنشئ حسابا جديدا.", "Select an account from the tree or create a new one." },
                { "بيانات توضيحية للعرض فقط.", "Sample data for display only." },
                { "اختر تاريخ قيد صحيحًا قبل الحفظ.", "Choose a valid entry date before saving." },
                { "تعذر حساب الإجمالي؛ راجع مبالغ السطور.", "Could not compute the total; review the line amounts." },
                { "تغيرت جلسة السند؛ تم إخفاء المسودة القديمة. أعد تحميل السند قبل المتابعة.", "The voucher session changed; the old draft was hidden. Reload the voucher before continuing." },
                { "جار تحميل بيانات السند...", "Loading voucher data..." },
                { "انتظر اكتمال حفظ السند قبل إغلاق النافذة.", "Wait for the voucher save to finish before closing the window." },
                { "وضع معاينة — البيانات محلية ولا يتم حفظها.", "Preview mode - the data is local and nothing is saved." },
                { "تم تجهيز سند جديد.", "A new voucher is ready." },
                { "استخدم سجل آخر السندات للمراجعة والتنقل.", "Use the voucher history to review and navigate." },
                { "لا توجد سندات للتنقل.", "No vouchers to navigate." },
                { "المعاينة للعرض فقط؛ لا يمكن طباعة سند تجريبي.", "Preview only; a sample voucher cannot be printed." },
                { "أعد فتح السند لتحميل بيانات الجلسة الحالية.", "Reopen the voucher to load the current session data." },
                { "لم يتم اختيار سند للطباعة.", "No voucher was selected for printing." },
                { "جار قراءة السند المحفوظ للطباعة...", "Reading the saved voucher for printing..." },
                { "تغير السند المحدد؛ اطلب طباعة السند المطلوب مجددًا.", "The selected voucher changed; request the print again." },
                { "تم فتح معاينة طباعة السند رقم ", "Opened the print preview for voucher " },
                { "أُلغيت قراءة السند للطباعة.", "The voucher print read was cancelled." },
                { "تغيرت جلسة السند؛ أعد فتح الشاشة.", "The voucher session changed; reopen the screen." },
                { "تعذر قراءة السند رقم ", "Could not read voucher " },
                { "؛ لم تُنشأ معاينة طباعة.", "; no print preview was created." },
                { "تعذر تحميل بيانات السند. تحقق من الاتصال ثم أعد فتح الشاشة.", "Could not load the voucher data. Check the connection and reopen the screen." },
                { "أعد فتح الشاشة لتحميل بيانات السند الحالية.", "Reopen the screen to load the current voucher data." },
                { "عملة النظام غير متاحة؛ أعد تحميل السند قبل الحفظ.", "The system currency is unavailable; reload the voucher before saving." },
                { "المعاينة للعرض فقط؛ لا يمكن حفظ سند تجريبي.", "Preview only; a sample voucher cannot be saved." },
                { "لم يتم الحفظ: تاريخ السند غير صحيح.", "Not saved: the voucher date is invalid." },
                { "لم يتم الحفظ: أدخل مبلغًا صالحًا وسعر تحويل أكبر من صفر ضمن النطاق المسموح.", "Not saved: enter a valid amount and a conversion rate greater than zero within the allowed range." },
                { "لم يتم الحفظ: تاريخ استحقاق الشيك غير صحيح.", "Not saved: the cheque due date is invalid." },
                { "جار حفظ السند؛ انتظر اكتمال العملية.", "Saving the voucher; wait for the operation to complete." },
                { "تم حفظ السند رقم ", "Voucher " },
                { " وتوليد القيد المحاسبي بنجاح.", " was saved and the accounting entry was created." },
                { "؛ تعذر تحديث السجل. لا تعِد الحفظ.", " was saved; the history could not be updated. Do not save again." },
                { "تم تحديد السند رقم ", "Voucher " },
                { " من السجل.", " was selected from the history." },
                { "عام", "General" },
                { "اسم العميل:", "Customer name:" },
                { "هاتف العميل:", "Customer phone:" },
                { "عنوان العميل:", "Customer address:" },
                { "اسم الكاشير:", "Cashier name:" },
                { "رقم العميل:", "Customer ID:" },
                { "التوقيت:", "Time:" },
                { "اختر الدواء", "Select the medicine" },
                { "المخزون الحالي — اختر مورداً أو أدخل باركود", "Current stock - select a supplier or enter a barcode" },
                { "تعذر فتح سجل استعادة السند؛ لم يرسل حفظ. أغلق الشاشة الأخرى للسند ثم أعد المحاولة.", "Could not open the voucher recovery record; no save was sent. Close the other voucher screen and try again." },
                { "حفظ قالب: ", "Save template: " },
                { "أدخل بيانات السند ثم احفظه.", "Enter the voucher details, then save." },
                { "تغيرت جلسة السند أو صلاحيتها. حدّث الخيارات للبدء بمسودة فارغة.", "The voucher session or permissions changed. Refresh the options to start an empty draft." },
                { "المبلغ بعملة النظام: ", "Amount in the system currency: " },
                { "تغيرت عملة النظام؛ بقيت المسودة بعملتها السابقة. افتح سندًا جديدًا وراجع المبلغ قبل الحفظ.", "The system currency changed; the draft keeps its previous currency. Open a new voucher and review the amount before saving." },
                { "تم تحميل حسابات السند وعملته.", "The voucher accounts and currency are loaded." },
                { "تعذر تحميل حسابات السند. أعد التحديث.", "Could not load the voucher accounts. Refresh." },
                { "أدخل تاريخًا صحيحًا للسند بصيغة yyyy/MM/dd.", "Enter a valid voucher date as yyyy/MM/dd." },
                { "أدخل مبلغًا موجبًا وصحيحًا.", "Enter a positive, valid amount." },
                { "اختر الحساب المقابل قبل حفظ السند.", "Select the offset account before saving the voucher." },
                { "عملة السند غير متاحة؛ حدّث الخيارات قبل الحفظ.", "The voucher currency is unavailable; refresh the options before saving." },
                { "تعذر حفظ السند؛ راجع رسالة الخطأ قبل إعادة المحاولة.", "Could not save the voucher; review the error message before retrying." },
                { ". تم تحديث السجل.", ". The history was updated." },
                { "تم تحديث سجل السندات.", "The voucher history was updated." },
                { "رمز الدعم: ", "Support code: " },
                { "تاريخ السند", "Voucher date" },
                { "الطرف", "Party" },
                { "الحساب المقابل", "Offset account" },
                { "الحساب الأب", "Parent account" },
                { "حساب الصندوق أو الإيداع", "Cash or deposit account" },
                { "معرف الفترة", "Period ID" },
                { "نوع السند", "Voucher type" },
                { "السند الأول", "First voucher" },
                { "السند السابق", "Previous voucher" },
                { "السند التالي", "Next voucher" },
                { "السند الأخير", "Last voucher" },
                { "وحدة العبوة", "Package unit" },
                { "المبلغ", "Amount" },
                { "حفظ السند", "Save voucher" },
                { "إلغاء المحددة", "Cancel the selected one" },
                { "الفواتير الحديثة", "Recent invoices" },
                { "معاينة طباعة الفاتورة A4", "A4 invoice print preview" },
                { "سند قبض", "Receipt voucher" },
                { "سند صرف", "Payment voucher" },
                { "خارجي", " (external)" },
                { "داخلي", " (internal)" },
                { "متعدد", " (multi)" },
                { "لا توجد بيانات حالياً", "No data available." },
                { "آخر السندات", "Recent vouchers" },
                { "رصيد الحساب حتى تاريخ السند", "Account balance as of the voucher date" },
                { "بيانات الشيك", "Cheque details" },
                { "تلقائي عند الحفظ", "Automatic on save" },
                { "الفرع الرئيسي", "Main branch" },
                { "إجراء سند القبض أو الصرف", "Receipt or payment action" },
                { "اختيار طريقة دفع السند", "Choose the voucher payment method" },
                { "الفرع", "Branch" },
                { "اسم الطرف", "Party" },
                { "سعر التحويل", "Conversion rate" },
                { "القيمة محلي", "Local amount" },
                { "حساب الصندوق", "Cashbox account" },
                { "المندوب", "Representative" },
                { "رقم الشيك", "Cheque number" },
                { "تاريخ استحقاق الشيك", "Cheque due date" },
                { "حالة الشيك", "Cheque status" },
                { "البنك المسحوب عليه", "Bank" },
                { "رقم السند", "Voucher number" },
                { "بدون", "None" },
                { "مستخدم المعاينة", "Preview user" },
                { "تحت التحصيل", "Under collection" },
                { "محصل", "Collected" },
                { "مرتجع", "Returned" },
                { "حفظ وطباعة", "Save and print" },
                { "يصرف من حساب", "Debited from an account" },
                { "يودع في حساب", "Credited to an account" },
                { "استلمت من حساب", "Received from an account" },
                { "استلمت من حساب/موظف", "Received from an account/employee" },
                { "صرف إلى حساب/موظف", "Paid to an account/employee" },
                { "صرف إلى السيد", "Paid to the customer" },
                { "استلمت من السيد", "Received from the customer" },
                { "طريقة الدفع شيك: رقم الشيك والبنك مطلوبان قبل الحفظ.", "Cheque payment: the cheque number and bank are required before saving." },
                { "طريقة الدفع المختارة", "Selected payment method" },
                { "المورد:", "Supplier:" },
                { "الموظف:", "Employee:" },
                { "الشيك مسحوب على بنك", "Cheque drawn on bank" },
                { "سجل السندات", "Voucher history" },
                { "السجل المحفوظ لسندات القبض والصرف", "The saved receipt and payment history" },
                { "حوالة", "Transfer" },
                { "شيك", "Cheque" },
                { "كاش", "Cash" },
                { "الشيك مسحوب عل بنك", "Cheque drawn on bank" },
                { "سجل السند", "Voucher history" },
                { "السجل المحف لسندا القبي والص", "The saved receipt and payment history" },
                { "رئيسي", "Main" },
                { "فرعي", "Subaccount" },
                { "تكرار البيان", "Repeat the description" },
                { "مدين: ", "Debit: " },
                { "دائن: ", "Credit: " },
                { "صلاحيات الشاشات والعمليات للموظف: ", "Screen and operation permissions for the employee: " },
                { "عمليات اليوم", "Today's operations" },
                { "استيراد ومراجعة فواتير Excel", "Import and review Excel invoices" },
                { "دليل المكونات — AntdUI وOutlined", "Component guide - AntdUI and Outlined" },
                { "تصميم موحد • بيانات تجريبية", "Unified design - sample data" },
                { "الحقول الأصلية محفوظة لحين اجتياز العربية. الأزرار تجريبية فقط.", "The native fields are kept until Arabic passes. The buttons are samples only." },
                { "السلة فارغة — امسح الباركود لإضافة أول صنف", "The cart is empty - scan a barcode to add the first item" },
                { "أُغلقت المحاولة غير المعتمدة دون فاتورة؛ يمكنك بدء فاتورة جديدة.", "The unposted attempt was closed without an invoice; you can start a new purchase." },
                { "تعذر إغلاق المحاولة غير المعتمدة؛ تحقق من الاتصال ثم أعد المحاولة.", "Could not close the unposted attempt; check the connection and try again." },
                { "لم تثبت نتيجة الحفظ. يمكنك إغلاق المحاولة غير المعتمدة أو إعادة المحاولة نفسها.", "The save result is not confirmed. You can close the unposted attempt or retry the same attempt." },
};

        internal static bool IsEnglishActive
        {
            get
            {
                return UiLanguageSettings.IsEnglishAvailable &&
                    string.Equals(UiLanguageSettings.Load(), UiLanguageSettings.English, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static string GetDisplayText(string arabicText)
        {
            return IsEnglishActive ? Translate(arabicText) : arabicText;
        }

        // Translate the application template before inserting raw business values.
        // Never translate the completed message: names, references and braces are data.
        internal static string Format(bool english, string template, params object[] values)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture,
                english ? Translate(template) : template, values);
        }

        internal static string Translate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string translated;
            string normalized = text.Trim();
            if (ArabicToEnglish.TryGetValue(normalized, out translated)) return translated;

            // نصوص حالة تولّد عند التشغيل؛ لا تعتمد على اسم قاعدة أو مستخدم ثابت.
            const string activeDatabasePrefix = "قاعدة التشغيل: ";
            if (normalized.StartsWith(activeDatabasePrefix, StringComparison.Ordinal))
            {
                return "Active database: " + normalized.Substring(activeDatabasePrefix.Length);
            }

            const string customerCreditPrefix = "رصيد العميل المتاح: ";
            if (normalized.StartsWith(customerCreditPrefix, StringComparison.Ordinal))
            {
                return "Available customer credit: " + normalized.Substring(customerCreditPrefix.Length);
            }
            return text;
        }

        // يُستدعى فقط عندما تصبح الإنجليزية متاحة ومراجعة بالكامل.
        internal static void Apply(Form form)
        {
            if (form == null) return;
            form.RightToLeft = RightToLeft.No;
            form.RightToLeftLayout = false;
            TranslateControl(form);
            foreach (Control control in form.Controls) Apply(control);
            // The status strip is not a Control, so the translation pass does not reach it. Rebuild its
            // default text after the title is translated, otherwise an English screen keeps an Arabic strip.
            UiTheme.ApplyStatusStripPresentation(form, true);
        }

        internal static void Apply(Control control)
        {
            if (control == null) return;
            control.RightToLeft = RightToLeft.No;
            TranslateControl(control);

            Label label = control as Label;
            if (label != null) label.TextAlign = ContentAlignment.MiddleLeft;

            TextBox textBox = control as TextBox;
            if (textBox != null) textBox.TextAlign = HorizontalAlignment.Left;

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.TextAlign = ContentAlignment.MiddleLeft;
                checkBox.CheckAlign = ContentAlignment.MiddleLeft;
            }

            RadioButton radioButton = control as RadioButton;
            if (radioButton != null)
            {
                radioButton.TextAlign = ContentAlignment.MiddleLeft;
                radioButton.CheckAlign = ContentAlignment.MiddleLeft;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null) comboBox.RightToLeft = RightToLeft.No;

            FlowLayoutPanel flow = control as FlowLayoutPanel;
            if (flow != null && flow.FlowDirection == FlowDirection.RightToLeft) flow.FlowDirection = FlowDirection.LeftToRight;

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.RightToLeft = RightToLeft.No;
                foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Translate(column.HeaderText);
            }

            foreach (Control child in control.Controls) Apply(child);
        }

        private static void TranslateControl(Control control)
        {
            if (control == null) return;
            // Field values belong to business data. Their captions/placeholders are translated by the owning screen.
            bool excluded = control is BusinessValueLabel || control is HomeSessionHeader || control is TextBoxBase || control is ComboBox || control is AntdUI.Input ||
                control is AntdUI.Select || control is AntdUI.DatePicker || control is DateTimePicker || control is NumericUpDown;
            // The accessible name is what a screen reader announces; an English workspace must not
            // leave Arabic there even when the visible field itself carries a raw business value.
            if (!string.IsNullOrWhiteSpace(control.AccessibleName)) control.AccessibleName = Translate(control.AccessibleName);
            if (excluded || string.IsNullOrWhiteSpace(control.Text)) return;
            control.Text = Translate(control.Text);
        }
    }

    internal static class UiLanguageCatalogSelfTests
    {
        internal static void RunAll()
        {
            VerifyLibraryPreviewLanguage();
            VerifyPurchasePresentationBounds();
            VerifyImportValidationPresentation();
            VerifyCommerceStatePresentation();
            VerifyFormattedMessages();
            VerifyTemplateContracts();
            VerifyCentralizedShortcuts();
            VerifySalesHistoryPresentation();
            VerifyPurchaseHistoryPresentation();
            VerifyDynamicPermissionAndImportStatus();
            VerifyJournalTotalsPresentation();
            VerifyBusinessValueBoundary();
            VerifyArabicBusinessValueBoundary();
            VerifySessionHeaderRawValueBoundary();
            VerifyRuntimeTextFollowsLanguage();
            Require(UiLanguageCatalog.Translate("الإعدادات") == "Settings", "ترجمة الإعدادات غير صحيحة.");
            Require(UiLanguageCatalog.Translate("إعدادات اتصال قاعدة البيانات") == "Database connection settings" &&
                UiLanguageCatalog.Translate("تحميل قواعد البيانات") == "Load databases" &&
                UiLanguageCatalog.Translate("خط الواجهة") == "Interface font",
                "ترجمة إعدادات اتصال قاعدة البيانات غير مكتملة.");
            Require(UiLanguageCatalog.Translate("مركز الأجهزة") == "Device center" &&
                UiLanguageCatalog.Translate("المقاس الافتراضي لفاتورة البيع والشراء") == "Default sales and purchase invoice size" &&
                UiLanguageCatalog.Translate("حفظ إعدادات الأجهزة") == "Save device settings",
                "ترجمة إعدادات الأجهزة غير مكتملة.");
            Require(UiLanguageCatalog.Translate("مركز اللغة") == "Language center" &&
                UiLanguageCatalog.Translate("حالة توفر اللغة") == "Language availability" &&
                UiLanguageCatalog.Translate("إغلاق مركز اللغة") == "Close language center",
                "ترجمة مركز اللغة غير مكتملة.");
            foreach (string loginText in new[]
            {
                "تسجيل الدخول", "إدارة الصيدلية", "دخول آمن وسريع لإدارة المبيعات والمخزون والموظفين.",
                "إعداد قاعدة البيانات (F1)", "الخادم", "القاعدة", "قاعدة التشغيل",
                "قاعدة التشغيل الحالية", "اسم قاعدة البيانات التي سيستخدمها النظام بعد تسجيل الدخول.",
                "إعداد قاعدة البيانات", "يفتح إعدادات اختيار الخادم وقاعدة البيانات.", "المستخدم",
                "اسم المستخدم", "كلمة المرور", "أدخل اسم مستخدم النظام.",
                "أدخل كلمة المرور ثم اضغط دخول.", "تذكر اسم المستخدم", "يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.",
                "دخول إلى النظام", "إغلاق التطبيق", "إظهار", "إخفاء", "إظهار كلمة المرور", "إخفاء كلمة المرور",
                "معاينة فقط؛ لم تُفتح قاعدة البيانات ولم تُنفذ مصادقة.",
                "معاينة فقط؛ لم تُفتح إعدادات قاعدة البيانات.",
                "تعذر قراءة إعداد اتصال قاعدة البيانات ونسخته الاحتياطية. لا يمكن الدخول إلى قاعدة افتراضية مختلفة. افتح إعدادات الاتصال عبر F1 ليصلحها مسؤول Windows المحلي.",
                "الرجاء إدخال اسم المستخدم وكلمة المرور.", "تم إيقاف محاولات تسجيل الدخول مؤقتًا. حاول بعد {0} ثانية.",
                "تحقق من إعدادات الخادم وقاعدة البيانات عبر F1 ثم أعد المحاولة.",
                "فشل الاتصال بقاعدة البيانات. يجب أن يفتح مسؤول Windows المحلي إعدادات الاتصال عبر F1.",
                "لا يوجد مدير مسجل للنظام بعد. يجب على مسؤول Windows محلي فتح إعدادات قاعدة البيانات عبر F1 وإنشاء المدير الأول قبل تسجيل الدخول.",
                "تهيئة مطلوبة", "اسم المستخدم أو كلمة المرور غير صحيحة.", "خطأ في تسجيل الدخول",
                "تم تسجيل الدخول، لكن تعذر حفظ خيار تذكرني.", "فتح إعدادات قاعدة البيانات",
                "تعديل اتصال قاعدة البيانات يتطلب صلاحية مسؤول Windows لحماية بيانات الصيدلية. سيتم فتح نافذة إعداد مستقلة بعد موافقتك على طلب Windows.",
                "فُتحت نافذة إعدادات الاتصال بصلاحية مسؤول. احفظ التغيير فيها ثم أغلق شاشة الدخول وافتح النظام من جديد لاستخدام الاتصال الجديد.",
                "تعذر فتح إعدادات قاعدة البيانات بصلاحية مسؤول. تأكد من السماح بطلب Windows ثم أعد المحاولة."
            })
                Require(UiLanguageCatalog.Translate(loginText) != loginText,
                    "نص شاشة الدخول بلا ترجمة: " + loginText);
            foreach (string currencyText in new[]
            {
                "إعدادات العملات", "العملات والدول", "إدارة العملات المتاحة وتحديد عملة النظام الأساسية",
                "بيانات العملة", "الدولة", "اسم العملة", "رمز العملة", "اختصار/علامة العملة",
                "اجعلها عملة النظام", "عملة النظام", "حفظ العملة", "تعيين العملة كعملة للنظام"
            })
                Require(UiLanguageCatalog.Translate(currencyText) != currencyText,
                    "نص شاشة العملات بلا ترجمة: " + currencyText);
            foreach (string dashboardText in new[]
            {
                "إجمالي المبيعات", "قيمة الفواتير المسجلة", "الشركات", "عدد الشركات المصنعة",
                "الموظفون", "عدد مستخدمي النظام", "سجل الفواتير وتحليل المبيعات",
                "اسم العميل", "رقم الهاتف", "العنوان", "الإجمالي", "رجوع", "إرشاد الطباعة"
            })
                Require(UiLanguageCatalog.Translate(dashboardText) != dashboardText,
                    "نص لوحة التحكم بلا ترجمة: " + dashboardText);
            foreach (string employeeText in new[]
            {
                "الموظفون", "اسم الموظف", "قائمة الموظفين", "بيانات الموظفين ومحرر الموظف",
                "إجراءات الموظفين", "كلمة مرور جديدة", "كلمة مرور جديدة (اختيارية، 3 رموز فأكثر)",
                "إضافة موظف", "تعديل موظف", "حذف موظف", "مسح حقول الموظف", "إغلاق شاشة الموظفين",
                "إدارة صلاحيات الشاشات والعمليات", "معاينة فقط؛ لا تُحفظ بيانات موظفين.",
                "أدخل عمرًا موجبًا وراتبًا رقميًا غير سالب.", "تمت إضافة الموظف بنجاح.",
                "تم تعديل بيانات الموظف بنجاح.", "تم حذف الموظف بنجاح."
            })
                Require(UiLanguageCatalog.Translate(employeeText) != employeeText,
                    "نص شاشة الموظفين بلا ترجمة: " + employeeText);
            foreach (string manufacturerText in new[]
            {
                "الشركات المصنعة", "قائمة الشركات", "جدول الشركات المصنعة", "رقم الشركة", "اسم الشركة",
                "بيانات الشركات المصنعة ومحرر الشركة", "إجراءات الشركات المصنعة", "إضافة شركة مصنعة",
                "تعديل شركة مصنعة", "حذف شركة مصنعة", "مسح حقول الشركة المصنعة",
                "إغلاق شاشة الشركات المصنعة", "معاينة فقط؛ لا تُحفظ بيانات شركات مصنعة.",
                "تمت إضافة الشركة بنجاح.", "تم تعديل بيانات الشركة بنجاح.", "تم حذف الشركة بنجاح."
            })
                Require(UiLanguageCatalog.Translate(manufacturerText) != manufacturerText,
                    "نص شاشة الشركات المصنعة بلا ترجمة: " + manufacturerText);
            foreach (string cardText in new[]
            {
                "بيانات الصيدلية", "الاسم والعنوان وبيانات المنشأة", "الحسابات والضرائب",
                "الدولة وتفعيل الضريبة ونسبتها", "العملات", "الدول والعملات وعملة النظام",
                "اللغة", "لغة عرض النصوص وتسميات الواجهة", "مركز الأجهزة",
                "الطابعة ومقاس الإيصال وقارئ الباركود", "المعايير السعودية",
                "خيارات الامتثال والمحاكاة", "مركز Excel",
                "تصدير البيانات أو فتح استيراد الأصناف والفواتير والصناديق",
                "الأدوية والفواتير والمخزون والقيود؛ البحث والفترة حيث تتوفر",
                "استيراد أصناف Excel", "تعريف أصناف جديدة برصيد صفر مع معاينة قبل الاعتماد",
                "صناديق النقد", "تعريف الصناديق وإسناد الكاشير واستيرادها وتصديرها", "فتح"
            })
                Require(UiLanguageCatalog.Translate(cardText) != cardText, "نص بطاقة الإعدادات بلا ترجمة: " + cardText);
            foreach (string exportText in new[]
            {
                "مركز Excel", "اختر البيانات للتصدير أو افتح مسار استيرادها؛ قوالب الاستيراد مستقلة عن كشوف التصدير.",
                "فتح الاستيراد", "كشوف التصدير للتحليل؛ افتح الاستيراد لتنزيل قالب مستقل ومراجعة البيانات قبل الاعتماد.",
                "يفتح استيراد الفواتير شاشة واحدة للبيع والشراء؛ استخدم قالبها المستقل وراجع البيانات قبل الاعتماد.",
                "استيراد الإعدادات متاح للمدير فقط.", "هذا النوع للتصدير فقط؛ لا يوجد له استيراد مباشر.",
                "نوع البيانات", "بحث اختياري", "الأدوية والأرصدة الحالية", "سطور فواتير البيع",
                "سطور البيع القديمة (دون تاريخ محفوظ)",
                "سطور فواتير المشتريات", "حركات المخزون", "سطور القيود المحاسبية",
                "حركات النقد المشترك", "تعريفات صناديق النقد",
                "تُصدّر تعريفات الصناديق وإسناد الكاشير فقط دون أرصدة مالية، ويمكن استيراد الملف من شاشة صناديق النقد.",
                "هذا كشف حساب النقد المشترك، ولا يحدد أرصدة كل كاشير. يُرفض التصدير عند 2,000 صف.",
                "معاينة التصدير؛ لم تُقرأ بيانات ولم يُنشأ ملف.",
                "جارٍ تجهيز البيانات وحفظ ملف Excel...", "تم تصدير {0} صفًا إلى Excel."
            })
                Require(UiLanguageCatalog.Translate(exportText) != exportText,
                    "نص شاشة تصدير Excel بلا ترجمة: " + exportText);
            foreach (string readinessText in new[]
            {
                "جاهزية إقفال الفترة", "فحص قرار قراءة فقط قبل الإقفال. لا ينشئ قيود إقفال ولا يعالج الفروقات تلقائياً.",
                "الفحص", "عدد الملاحظات", "الحالة", "التفصيل", "سليم", "يحتاج مراجعة",
                "سلامة القيود", "مطابقة المشتريات", "أرصدة مخزون سالبة", "تسلسل حركات المخزون",
                "معاينة واجهة القراءة فقط؛ لم يُنفذ فحص على قاعدة البيانات.",
                "المعاينة للعرض فقط؛ لم يُنفذ فحص على قاعدة البيانات."
            })
                Require(UiLanguageCatalog.Translate(readinessText) != readinessText,
                    "نص شاشة جاهزية الإقفال بلا ترجمة: " + readinessText);
            foreach (string integrityText in new[]
            {
                "فحص سلامة القيود", "فحص مطابقة المخزون", "إعادة الفحص",
                "جارٍ فحص سلامة القيود...", "لا تملك صلاحية فحص سلامة القيود أو تغيرت جلسة المستخدم؛ أعد فتح الفحص.",
                "تغير المستخدم أو الاتصال أو الصلاحية؛ أعد تحميل فحص سلامة القيود.", "مصدر فحص سلامة القيود لا يدعم القراءة غير المتزامنة.",
                "لم يرجع فحص سلامة القيود نتيجة.", "لا تملك صلاحية قراءة التقارير أو تغيرت جلسة المستخدم؛ أعد تحميل التقرير.",
                "فحص قراءة فقط: يعرض القيود غير المتوازنة أو التي لا تحتوي أسطراً، ولا يصلحها تلقائياً.",
                "فحص قراءة فقط: يراجع تسلسل حركات الصنف ومطابقة الرصيد الحالي مع آخر حركة مسجلة.",
                "رقم القيد", "عدد الأسطر", "إجمالي المدين", "إجمالي الدائن", "الفرق",
                "قيد بلا أسطر", "القيد غير متوازن", "رقم الحركة", "الرصيد قبل", "الرصيد بعد",
                "الرصيد الحالي لا يطابق آخر حركة", "تسلسل الحركات غير متصل",
                "توجد {0} قيود تحتاج مراجعة محاسبية. لم يُنفذ أي إصلاح تلقائي.",
                "توجد {0} حركات تحتاج مراجعة. لم تُعدّل أي كمية تلقائياً."
            })
                Require(UiLanguageCatalog.Translate(integrityText) != integrityText,
                    "نص شاشة فحص السلامة بلا ترجمة: " + integrityText);
            foreach (string operationalReportText in new[]
            {
                "منشئ التقارير التشغيلي", "اختر المصدر والفترة، ثم اعرض النتائج أو استخدم قالبًا محفوظًا.",
                "مصدر البيانات", "مصدر التقرير", "بحث في نتائج التقرير", "قوالب التقارير التشغيلية المحفوظة",
                "القوالب المحفوظة لهذا المستخدم", "الأدوية والأرصدة الحالية",
                "المبيعات (مؤرخة عند توفر المخطط التجاري)", "فواتير المشتريات", "سطور فواتير المشتريات",
                "حركات المخزون", "سطور اليومية العامة", "حركات النقد المشترك", "أداء الصيدلية اليومي (الربح والإلغاءات)",
                "لا توجد نتائج مطابقة للفلاتر المحددة.", "معاينة ببيانات وهمية؛ لم تُقرأ قاعدة البيانات.",
                "حالة التقرير", "تطبيق قالب التقرير المحدد", "حذف قالب التقرير المحدد",
                "عرض نتائج التقرير", "تصدير التقرير إلى Excel", "إغلاق منشئ التقارير التشغيلي",
                "الفترة", "تقرير تشغيلي", "النتائج", " (تم تطبيق حد العرض الآمن)",
                "تاريخ البداية يجب أن يسبق تاريخ النهاية أو يساويه.", "جار تحميل التقرير...",
                "تعذر تحميل التقرير.", "معاينة فقط؛ لم يُحفظ قالب.", "معاينة فقط؛ لم يُطبق قالب.",
                "معاينة فقط؛ لم يُحذف قالب.", "معاينة فقط؛ لم تُعدل الأعمدة.",
                "معاينة فقط؛ لم يُنشأ ملف.", "تم تصدير التقرير بنجاح.", "معاينة فقط؛ لم تُفتح الطباعة."
            })
                Require(UiLanguageCatalog.Translate(operationalReportText) != operationalReportText,
                    "نص منشئ التقارير التشغيلي بلا ترجمة: " + operationalReportText);
            foreach (string printText in new[]
            {
                "خيارات طباعة التقرير", "الاتجاه والكثافة وبيانات المنشأة اختيارات مستقلة عن بيانات التقرير.",
                "اتجاه التقرير", "تفصيل التقرير", "اتجاه الصفحة", "كثافة الصفوف",
                "تلقائي حسب الأعمدة", "طولي", "عرضي", "مفصل", "مختصر",
                "بيانات المنشأة في الترويسة", "اسم المنشأة", "الشعار", "الرقم الضريبي",
                "السجل التجاري", "الجوال", "العنوان", "الموقع", "معاينة الطباعة", "إلغاء",
                "معاينة الخيارات؛ لن تُفتح طابعة أو قاعدة بيانات.",
                "تعذر تحميل بيانات المنشأة؛ يمكن طباعة التقرير بدون ترويسة.",
                "اختر شكل التقرير والبيانات التي تريد إظهارها في ترويسته.",
                "معاينة فقط؛ لم تُفتح طابعة أو قاعدة بيانات.",
                "ملف الشعار غير متاح أو كبير جدًا؛ صحح المسار في ملف الصيدلية."
            })
                Require(UiLanguageCatalog.Translate(printText) != printText,
                    "نص خيارات طباعة التقرير بلا ترجمة: " + printText);
            foreach (string reportDialogText in new[]
            {
                "تخصيص أعمدة التقرير", "حدد الأعمدة الظاهرة ورتبها. هذا لا يغيّر بيانات النظام.",
                "ترتيب الأعمدة الظاهرة", "نقل لأعلى", "نقل العمود المحدد لأعلى", "نقل لأسفل",
                "نقل العمود المحدد لأسفل", "إظهار الكل", "إظهار كل أعمدة التقرير",
                "تطبيق تخصيص أعمدة التقرير", "إلغاء تخصيص أعمدة التقرير", "يجب إظهار عمود واحد على الأقل.",
                "حفظ قالب التقرير", "حفظ قالب", "يحفظ هذا القالب الفلاتر والخيارات فقط، وليس بيانات التقرير.",
                "اسم القالب", "اسم قالب التقرير", "إلغاء حفظ قالب التقرير", "أدخل اسمًا للقالب."
            })
                Require(UiLanguageCatalog.Translate(reportDialogText) != reportDialogText,
                    "نص نافذة تقرير مساندة بلا ترجمة: " + reportDialogText);
            foreach (string importText in new[]
            {
                "استيراد ملف إكسل لتعريفات الأصناف", "حمّل القالب، أدخل الأصناف والشركة المسجلة، ثم راجع الصفوف قبل الاعتماد. أدخل الكميات لاحقًا من المشتريات.",
                "اختيار ملف", "تنزيل قالب", "اعتماد الأصناف", "لم يتم اختيار ملف.",
                "الملف: {0}", "صفوف الملف: {0} — أخطاء: {1}",
                "معاينة مرئية؛ لم يُقرأ ملف أو تُفتح قاعدة بيانات.",
                "جارٍ قراءة ملف Excel والتحقق من صفوفه...",
                "جارٍ اعتماد تعريفات الأصناف؛ انتظر اكتمال العملية.",
                "اكتمل الاستيراد: {0} صنف جديد، {1} اسم موجود دون تعديل.",
                "اسم الدواء", "تاريخ الإنتاج", "الشركة المصنعة"
            })
                Require(UiLanguageCatalog.Translate(importText) != importText,
                    "نص شاشة استيراد Excel بلا ترجمة: " + importText);
            Require(UiLanguageCatalog.Translate("سعر البيع") == "Sale price", "ترجمة سعر البيع غير صحيحة.");
            Require(UiLanguageCatalog.Translate("إضافة الصنف الأول") == "Add first medicine", "ترجمة إجراء البداية غير صحيحة.");
            Require(UiLanguageCatalog.Translate("المبيعات") == "Sales" &&
                UiLanguageCatalog.Translate("العرض الشبكي") == "Grid view" &&
                UiLanguageCatalog.Translate("مركز العمل") == "Work center" &&
                UiLanguageCatalog.Translate("ملء الشاشة") == "Full screen" &&
                UiLanguageCatalog.Translate("وضع عادي") == "Windowed mode",
                "ترجمة بطاقات الرئيسية وأوضاع عرضها غير صحيحة.");
            Require(UiLanguageCatalog.Translate("قاعدة التشغيل: PharmacyManagement_qa") == "Active database: PharmacyManagement_qa", "ترجمة حالة قاعدة التشغيل غير صحيحة.");
            Require(UiLanguageCatalog.Translate("نقطة البيع") == "Point of sale", "ترجمة عنوان نقطة البيع غير صحيحة.");
            Require(UiLanguageCatalog.Translate("البلد وسياسة الضريبة") == "Country and tax policy" &&
                UiLanguageCatalog.Translate("نسبة الضريبة (%)") == "Tax rate (%)" &&
                UiLanguageCatalog.Translate("اليمن (YE)") == "Yemen (YE)",
                "ترجمة عناصر سياسة الضريبة غير صحيحة.");
            Require(UiLanguageCatalog.Translate("إشعار مرتجع تجاري") == "Commercial sales return" &&
                UiLanguageCatalog.Translate("نقداً (المسار الأصلي الحالي)") == "Cash (current original method)" &&
                UiLanguageCatalog.Translate("كمية المرتجع") == "Return quantity" &&
                UiLanguageCatalog.Translate("حجر مؤقت") == "Temporary quarantine",
                "ترجمة شاشة المرتجع وخياراتها غير مكتملة.");
            Require(UiLanguageCatalog.Translate("إدارة مخزون الحجر") == "Quarantined stock" &&
                UiLanguageCatalog.Translate("الدفعات المحجورة المعلقة") == "Pending quarantined lots" &&
                UiLanguageCatalog.Translate("إفراج إلى المخزون") == "Release to stock" &&
                UiLanguageCatalog.Translate("إتلاف موثق") == "Record disposal" &&
                UiLanguageCatalog.Translate("المتبقي") == "Remaining",
                "ترجمة شاشة قرار الحجر غير مكتملة.");
            Require(UiLanguageCatalog.Translate("إتمام البيع وطباعة  F9") == "Complete sale and print  F9", "ترجمة إجراء إتمام البيع غير صحيحة.");
            Require(UiLanguageCatalog.Translate("اسم العميل") == "Customer name", "ترجمة بيانات العميل غير صحيحة.");
            Require(UiLanguageCatalog.Translate("جميع الكاشيرين") == "All cashiers", "ترجمة مرشح لوحة التحكم غير صحيحة.");
            Require(UiLanguageCatalog.Translate("مسؤول المخزون") == "Inventory officer", "ترجمة دور موظف المخزون غير صحيحة.");
            Require(UiLanguageCatalog.Translate("تنبيهات المخزون") == "Inventory alerts" &&
                UiLanguageCatalog.Translate("معدل الدوران والركود للأصناف") == "Item turnover and inactivity" &&
                UiLanguageCatalog.Translate("أيام الركود") == "Inactive days",
                "ترجمة عناوين ومرشحات تقارير المخزون غير صحيحة.");
            Require(UiLanguageCatalog.Translate("المتابعة والتقارير") == "Monitoring and reports" &&
                UiLanguageCatalog.Translate("جرد التشغيلات والصلاحيات") == "Lot stock and expiry" &&
                UiLanguageCatalog.Translate("عمليات المخزون") == "Inventory operations",
                "ترجمة تبويبات ومهام مركز المخزون غير صحيحة.");
            Require(UiLanguageCatalog.Translate("السندات والحسابات") == "Vouchers and accounts" &&
                UiLanguageCatalog.Translate("القيود والضريبة") == "Journals and tax" &&
                UiLanguageCatalog.Translate("التقارير والتحليل") == "Reports and analysis",
                "ترجمة تبويبات مركز المحاسبة غير صحيحة.");
            Require(UiLanguageCatalog.Translate("رصيد العميل المتاح: 1,900.00 ر.ي") == "Available customer credit: 1,900.00 ر.ي", "ترجمة رصيد العميل الديناميكي غير صحيحة.");
            Require(UiLanguageCatalog.GetDisplayText("نقطة البيع") == "نقطة البيع", "يجب إبقاء العربية افتراضية ما دامت الإنجليزية غير متاحة.");
            Require(!UiLanguageCatalog.IsEnglishActive, "لا يجوز تفعيل الإنجليزية قبل اكتمال التغطية ومراجعتها.");
        }

        private static void VerifyLibraryPreviewLanguage()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var uiCulture = System.Globalization.CultureInfo.CurrentUICulture;
            AntdControlLocalization.Apply();
            string original = AntdUI.Localization.Provider.GetLocalizedString("ID");
            using (AntdControlLocalization.BeginPreviewLanguage(true))
            {
                AntdThemeAdapter.SynchronizePalette();
                Require(AntdUI.Localization.Provider.GetLocalizedString("ID") == "en-US" &&
                    AntdUI.Localization.Provider.GetLocalizedString("ToDay") == "Today" &&
                    AntdUI.Localization.Provider.GetLocalizedString("Mon") == "Mon", "English preview calendar captions must follow its isolated presentation.");
                using (AntdControlLocalization.BeginPreviewLanguage(false))
                    Require(AntdUI.Localization.Provider.GetLocalizedString("ID") == "ar-EG", "Arabic preview must use the Gregorian calendar locale.");
                Require(AntdUI.Localization.Provider.GetLocalizedString("ToDay") == "Today", "Nested preview language must restore its parent.");
            }
            Require(AntdUI.Localization.Provider.GetLocalizedString("ID") == original &&
                ReferenceEquals(culture, System.Globalization.CultureInfo.CurrentCulture) && ReferenceEquals(uiCulture, System.Globalization.CultureInfo.CurrentUICulture) &&
                !UiLanguageSettings.IsEnglishAvailable && !UiLanguageCatalog.IsEnglishActive,
                "A preview must restore library language without changing culture or the availability gate.");
            Console.WriteLine("PASS library preview language: English/Gregorian Arabic, nested restore and palette refresh; culture/UI culture and global English gate unchanged.");
        }

        private static void VerifyPurchasePresentationBounds()
        {
            foreach (bool english in new[] { false, true })
            using (var form = new PurchasesUiAutomationPreview(english))
            {
                foreach (Size size in new[] { new Size(960, 640), new Size(1366, 768), new Size(1920, 1080), new Size(960, 640) })
                {
                    form.ClientSize = size;
                    var history = (Panel)form.Controls.Find("pnlPurchaseHistory", true)[0];
                    history.Visible = true;
                    form.Controls.Find("pnlPurchaseCartHost", true)[0].Visible = false;
                    CommerceWorkspaceTheme.RefreshLayout(form);
                    var grid = (DataGridView)history.Controls["PurchaseHistoryGV"];
                    var source = new System.Data.DataTable();
                    foreach (string name in new[] { "رقم الفاتورة", "المورد", "الإجمالي", "الموظف", "ملاحظات", "الحالة", "العملة" }) source.Columns.Add(name);
                    source.Columns.Add("التاريخ", typeof(DateTime));
                    source.Rows.Add("QA-RAW", "مورد", "551.00", "اختبار", "", english ? "Posted" : "معتمدة", english ? "Unspecified" : "غير محدد", new DateTime(2026, 10, 17, 12, 34, 0));
                    grid.DataSource = source;
                    UiTheme.ApplyDataGridQuality(grid);
                    form.RefreshContentDirections();
                    Require(grid.Rows[0].Cells["التاريخ"].FormattedValue.ToString() == "2026-10-17 12:34" &&
                        grid.Columns["التاريخ"].MinimumWidth >= TextRenderer.MeasureText("2026-10-17 12:34", grid.Columns["التاريخ"].InheritedStyle.Font).Width &&
                        (DateTime)source.Rows[0]["التاريخ"] == new DateTime(2026, 10, 17, 12, 34, 0),
                        "History date formatting must remain Gregorian and fit without altering its stored value.");
                    var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
                    try
                    {
                        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                        Require(grid.Rows[0].Cells["التاريخ"].FormattedValue.ToString() == "2026-10-17 12:34",
                            "History dates must remain Gregorian under ar-SA without changing process culture.");
                    }
                    finally { System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture; }
                    Require(grid.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes),
                        "Purchase history theme refresh must preserve the screen presentation direction.");
                    var header = new System.Collections.Generic.List<Control>();
                    foreach (string name in new[] { "lblHistoryTitle", "lblHistorySubtitle", "btnPreviewPurchaseHistory", "btnCancelPurchaseHistory", "btnBackToPurchase" })
                    {
                        Control control = history.Controls[name];
                        Require(control.Left >= 0 && control.Right <= history.ClientSize.Width && control.Bottom <= grid.Top - 8,
                            "Purchase history header clipped or overlaps its grid: " + english + "/" + size + "/" + name);
                        foreach (Control other in header)
                            Require(!control.Bounds.IntersectsWith(other.Bounds), "Purchase history actions or labels overlap: " + name);
                        header.Add(control);
                        if (control is AntdUI.Button)
                            Require(control.Width >= TextRenderer.MeasureText(control.Text, control.Font).Width + 36,
                                "Purchase history action caption does not fit: " + name);
                    }
                    foreach (string name in new[] { "dtpProdDate", "dtpExpDate" })
                    {
                        Control date = form.Controls.Find(name, true)[0];
                        Require(date.Width >= TextRenderer.MeasureText("2099/12/31", date.Font).Width + 48,
                            "Purchase date text and calendar icon need a complete viewport: " + size + "/" + name);
                    }
                    foreach (string name in new[] { "btnClearPurchase", "btnBackToPurchase", "btnCancelPurchaseHistory" })
                    {
                        Control button = form.Controls.Find(name, true)[0];
                        Require(!english || !System.Text.RegularExpressions.Regex.IsMatch(button.AccessibleName ?? "", "[\u0600-\u06ff]"),
                            "Purchase action accessible name remains Arabic: " + name);
                    }
                }
            }
            Console.WriteLine("PASS purchase presentation bounds: AR/EN compact-wide-compact; full date text, wrapped history actions, names; construction only, no SQL/native input.");
        }

        private static void VerifyImportValidationPresentation()
        {
            using (var form = new QaCsvImportForm(true, true))
            {
                form.ShowValidationPreview();
                var previewGrid = (DataGridView)form.Controls.Find("qaCsvPreviewGrid", true)[0];
                UiTheme.ApplyDataGridQuality(previewGrid);
                Require(previewGrid.RightToLeft == RightToLeft.No && previewGrid.Columns["id"].HeaderText == "Company ID" &&
                    form.Controls.Find("csvImportTitle", true)[0].Text == "Import item and company data" &&
                    !System.Text.RegularExpressions.Regex.IsMatch(form.Controls.Find("csvImportHint", true)[0].Text, "[\u0600-\u06ff]"),
                    "English import header, grid captions and direction must survive styling.");
                Label status = (Label)form.Controls.Find("csvImportStatus", true)[0];
                Require(status.Text.Contains("Row 3: Duplicate entry in the file: QA-{0}-شركة") &&
                    status.Text.Contains("Row 4: A row identifier is required.") && status.AccessibleName == status.Text,
                    "English validation preview must display full structured errors and preserve raw identifiers.");
                Require(!form.Controls.Find("executeCsvImport", true)[0].Enabled && !form.Controls.Find("chooseCsvImport", true)[0].Enabled,
                    "Validation preview must not read files or permit import writes.");
                Require(form.Controls["csvImportInfo"].Height > 76, "Validation errors need room beyond the original one-line status area.");
                foreach (DataGridViewColumn column in previewGrid.Columns)
                    Require(!System.Text.RegularExpressions.Regex.IsMatch(column.HeaderText, "[\u0600-\u06ff]"),
                        "An English import column caption remains Arabic: " + column.Name);
                foreach (string caption in new[] { "رقم الشركة", "الاسم", "الهاتف", "العنوان", "الباركود", "سعر الشراء", "سعر البيع", "الكمية", "تاريخ الإنتاج", "تاريخ الانتهاء", "الشركة" })
                    Require(!System.Text.RegularExpressions.Regex.IsMatch(UiLanguageCatalog.Translate(caption), "[\u0600-\u06ff]"),
                        "An import schema field caption has no English translation: " + caption);
            }
            Console.WriteLine("PASS import validation UI: translated row errors, raw identifier braces, expanded status and disabled input/approval; no SQL/files.");
        }

        private static void VerifyCommerceStatePresentation()
        {
            Require(CommerceInvoiceState.Describe(true, true, false, false, false, true, "غير معتمد") == "Preview only — no invoice was saved", "A preview must not claim that its synthetic invoice was saved.");
            Require(CommerceInvoiceState.Describe(false, true, false, false, false, true, "غير معتمد") == "معاينة فقط — لم تُحفظ فاتورة", "Arabic preview must identify its unsaved sample.");
            Require(CommerceInvoiceState.Describe(true, false, false, false, false, true, "C-123{عميل}") == "Saved — C-123{عميل}", "Saved status must retain the exact raw reference.");
            Require(CommerceInvoiceState.Describe(true, false, true, false, false, false, null) == "Saving…", "Saving state must remain explicit.");
            Require(CommerceInvoiceState.Describe(true, false, false, true, false, true, "123") == "Cancelled", "Cancelled must take precedence over saved.");
            Require(CommerceInvoiceState.Describe(true, false, false, false, true, true, "123") == "Save result unconfirmed — reopen to verify", "An unresolved result must never claim success.");
            Require(CommerceInvoiceState.Describe(true, false, false, false, false, false, null) == "Draft — Enter adds the selected item", "Draft status must remain distinct from saved.");
            Console.WriteLine("PASS commerce state: AR/EN preview, saving, draft, cancelled, unresolved, saved and raw reference; no persistence changes.");
        }

        private static void VerifyJournalTotalsPresentation()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                using (var form = new JournalEntriesForm("قيد تجريبي", "Manual", true))
                {
                    var method = typeof(JournalEntriesForm).GetMethod("JournalFormat",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    form.RightToLeft = RightToLeft.No;
                    string text = (string)method.Invoke(form, new object[] {
                        "إجمالي المدين: {0:N2}    إجمالي الدائن: {1:N2}    الفرق: {2:N2}", new object[] { 12.50m, 10m, 2.50m } });
                    Require(text == "Total debit: 12.50    Total credit: 10.00    Difference: 2.50",
                        "Journal totals translation changed decimal values or labels.");
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS journal totals English presentation: debit/credit/difference and two decimals; preview only, no posting or SQL.");
        }

        private static void VerifyDynamicPermissionAndImportStatus()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            const string rawError = "سند {0} / Save & A";
            using (var form = new EmployeeOperationPermissionsForm("preview", "موظف / Save", true))
            {
                var method = typeof(EmployeeOperationPermissionsForm).GetMethod("SetStatusFormat", flags);
                var label = (Label)typeof(EmployeeOperationPermissionsForm).GetField("status", flags).GetValue(form);
                method.Invoke(form, new object[] { "تعذر تحميل الصلاحيات: {0}", false, new object[] { rawError } });
                foreach (bool english in new[] { true, false, true })
                {
                    form.ApplyPresentation(english);
                    Require(label.Text == (english ? "Could not load permissions: " : "تعذر تحميل الصلاحيات: ") + rawError,
                        "Permission status lost its template or raw error when changing presentation.");
                }
                typeof(EmployeeOperationPermissionsForm).GetMethod("SetStatus", flags).Invoke(form, new object[] { "موافق", true });
                form.ApplyPresentation(false); form.ApplyPresentation(true);
                Require(label.Text == "OK", "Static permission status retained stale format arguments.");
            }
            using (var form = new QaCsvImportForm(true))
            {
                form.RightToLeft = RightToLeft.No;
                typeof(QaCsvImportForm).GetMethod("SetStatusFormat", flags).Invoke(form,
                    new object[] { "اكتمل الاستيراد: {0} جديد، {1} مكرر تم تجاوزه.", true, new object[] { 3, 2 } });
                var label = (Label)typeof(QaCsvImportForm).GetField("status", flags).GetValue(form);
                var import = (Control)typeof(QaCsvImportForm).GetField("import", flags).GetValue(form);
                Require(label.Text == "Import completed: 3 new records; 2 duplicates skipped." &&
                    label.AccessibleName == label.Text && !import.Enabled,
                    "Import status translation changed counts/accessibility or enabled a preview write.");
            }
            Console.WriteLine("PASS dynamic permissions/import: repeated Arabic-English rendering, raw error braces, static reset, counts and disabled preview writes; no SQL/files.");
        }

        private static void VerifyPurchaseHistoryPresentation()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                foreach (bool english in new[] { false, true })
                using (var form = new Purchases(true, english))
                {
                    var line = new PharmacyManagement.Application.PurchaseHistoryLine
                    { MedicineName = "صنف / Save {0}", BuyingPrice = 10m, Quantity = 1.25m, TotalAmount = 12.50m,
                        Unit = "حفظ", ProductionDate = new DateTime(2026, 10, 18), ExpiryDate = new DateTime(2027, 10, 18) };
                    string text = (string)typeof(Purchases).GetMethod("FormatPurchaseHistoryLines",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, new object[] { new[] { line } });
                    Require(text.Contains(english ? "Manufactured: 2026-10-18" : "إنتاج: 2026-10-18") &&
                        text.Contains(english ? "Expires: 2027-10-18" : "انتهاء: 2027-10-18") && text.StartsWith("صنف / Save {0}  |  ") &&
                        text.EndsWith(english ? "Unit: حفظ" : "وحدة: حفظ") && line.Quantity == 1.25m && line.TotalAmount == 12.50m,
                        "Purchase history lost Gregorian dates, translated labels or raw item/unit values under ar-SA.");
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS purchase history formatter: Arabic/English production/expiry stay Gregorian under ar-SA; raw business values preserved; no SQL.");
        }

        private static void VerifySalesHistoryPresentation()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                foreach (bool english in new[] { false, true })
                using (var form = new Billing(true, english))
                {
                    var line = new PharmacyManagement.Application.SalesHistoryLine
                    { MedicineName = "سند / Save {0}", UnitPrice = 10m, Quantity = 1.25m, TotalAmount = 12.50m, Unit = "حفظ" };
                    var lines = new[] { line };
                    string text = (string)typeof(Billing).GetMethod("FormatSalesHistoryLines",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, new object[] { lines });
                    string expected = english ? "سند / Save {0}  |  Price: 10.00  |  Quantity: 1.250  |  Total: 12.50  |  Unit: حفظ" :
                        "سند / Save {0}  |  سعر: 10.00  |  كمية: 1.250  |  إجمالي: 12.50  |  وحدة: حفظ";
                    Require(text == expected && line.MedicineName == "سند / Save {0}" && line.Unit == "حفظ" &&
                        line.Quantity == 1.25m && line.UnitPrice == 10m && line.TotalAmount == 12.50m,
                        "Sales history presentation changed raw medicine/unit/numbers or left labels untranslated.");
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS sales history formatter: Arabic/English labels, fractional quantity, raw medicine/unit and values preserved; preview only, no SQL.");
        }

        private static void VerifyFormattedMessages()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            const string rawReference = "قيد افتتاحي رقم {0} / Save & A&B";
            try
            {
                foreach (string culture in new[] { "ar-SA", "en-US" })
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(culture);
                    Require(UiLanguageCatalog.Format(true, "تم الحفظ بنجاح.\nمرجع البيع: {0}", rawReference) ==
                        "Saved successfully.\nSales reference: " + rawReference, "Saved reference translation changed raw business data.");
                    Require(UiLanguageCatalog.Format(false, "تم الحفظ بنجاح.\nمرجع البيع: {0}", rawReference) ==
                        "تم الحفظ بنجاح.\nمرجع البيع: " + rawReference, "Arabic formatted message changed.");
                    Require(UiLanguageCatalog.Format(true, "تم إلغاء الفاتورة {0} وإعادة {1} وحدة إلى المخزون.", rawReference, 3) ==
                        "Invoice " + rawReference + " was cancelled and 3 units returned to stock.", "Cancellation reference or quantity changed.");
                    Require(UiLanguageCatalog.Format(true, "تم إلغاء الفاتورة بأمان.\nالمرجع: {0}\nالمبلغ المعكوس: {1}\nالكمية المعادة: {2}", rawReference, "11.50 SAR", 1) ==
                        "The invoice was cancelled safely.\nReference: " + rawReference + "\nReversed amount: 11.50 SAR\nReturned quantity: 1", "Cancellation receipt fields changed.");
                    Require(UiLanguageCatalog.Format(true, "تعذر {0}. رمز الدعم: {1}", UiLanguageCatalog.Translate("حفظ PDF"), "QA-{0}-2026") ==
                        "Could not Save PDF. Support code: QA-{0}-2026", "Operation failure translation changed the support code.");
                    string followUp = UiLanguageCatalog.Format(true,
                        "تم اعتماد الفاتورة {0} بنجاح، لكن تعذر {1} فقط. لا تعِد الحفظ؛ افتح السجل ثم أكمل الإجراء من الفاتورة المعتمدة.\n\nرمز الدعم: {2}",
                        rawReference, UiLanguageCatalog.Translate("حفظ PDF"), "QA-{0}-2026");
                    Require(followUp.Contains(rawReference) && followUp.Contains("Do not save again;") &&
                        followUp.Contains("Support code: QA-{0}-2026") && followUp.Contains("Save PDF"),
                        "Post-save failure message lost the no-repeat warning, reference, action or support code.");
                }
                string arabicHelp = ConnectionSettingsService.BuildConnectionHelp(new InvalidOperationException());
                string englishHelp = ConnectionSettingsService.BuildConnectionHelp(new InvalidOperationException(), true);
                Require(ConnectionSettingsService.BuildConnectionHelp(new InvalidOperationException(), false) == arabicHelp &&
                    englishHelp.Contains("Troubleshooting steps:") && englishHelp.Contains("SQL Server Browser") &&
                    !System.Text.RegularExpressions.Regex.IsMatch(englishHelp, @"[\u0600-\u06ff]"),
                    "Connection help did not preserve Arabic or translate all fixed troubleshooting lines.");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS formatted messages: Arabic/English under ar-SA/en-US; references, braces, amounts and support codes preserved; no SQL.");
        }

        private static void VerifyTemplateContracts()
        {
            var entries = (Dictionary<string, string>)typeof(UiLanguageCatalog).GetField("ArabicToEnglish",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var placeholder = new System.Text.RegularExpressions.Regex(@"(?<!\{)\{(?<index>\d+)(?:,[^{}:]*)?(?::[^{}]*)?\}");
            int templates = 0;
            foreach (var entry in entries)
            {
                Require(!string.IsNullOrWhiteSpace(entry.Value) && !System.Text.RegularExpressions.Regex.IsMatch(entry.Value, @"[\u0600-\u06ff]"),
                    "English catalog value is blank or still contains Arabic: " + entry.Key);
                string[] source = placeholder.Matches(entry.Key).Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => match.Groups["index"].Value).OrderBy(value => value).ToArray();
                string[] target = placeholder.Matches(entry.Value).Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => match.Groups["index"].Value).OrderBy(value => value).ToArray();
                Require(source.SequenceEqual(target), "Translation placeholder contract differs: " + entry.Key);
                if (source.Length == 0) continue;
                templates++;
                object[] values = Enumerable.Range(0, source.Max(value => int.Parse(value)) + 1).Select(index => (object)("QA-" + index)).ToArray();
                foreach (string template in new[] { entry.Key, entry.Value })
                {
                    string result = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, values);
                    foreach (string index in source.Distinct()) Require(result.Contains("QA-" + index), "Formatted template lost a value: " + entry.Key);
                }
            }
            Console.WriteLine("PASS catalog contracts: {0} entries, {1} formatted templates; placeholder identities/counts and renderability.", entries.Count, templates);
        }

        private static void VerifyCentralizedShortcuts()
        {
            using (var form = new SalesShortcutSettingsForm(new SalesShortcutProfileStore(@"D:\PharmacyQA\work\translation-preview-unused"), true, true))
            {
                Require(form.Text == "Sales shortcuts" && form.Controls.Find("shortcutSave", true).Single().Text == "Save" &&
                    form.Controls.Find("shortcutReset", true).Single().Text == "Defaults" &&
                    form.Controls.Find("shortcutStatus", true).Single().Text == "Preview only; settings are not saved.",
                    "Shortcut preview does not use the central English catalog.");
                Require(!form.Controls.Find("shortcutSave", true).Single().Enabled, "Translation preview must not save user settings.");
            }
            Console.WriteLine("PASS shortcut English preview: centralized captions/status; saving disabled; no SQL or stored preference change.");
        }

        private static void VerifySessionHeaderRawValueBoundary()
        {
            foreach(bool english in new[]{false,true})
            using(var form=new Form())
            {
                var header=new HomeSessionHeader();form.Controls.Add(header);
                header.SetDetails("Remember Me Labs A&B عربي","Manager","Save",english);
                string expected=header.Text;string accessible=header.AccessibleDescription;
                var lines=header.Controls.OfType<BusinessValueLabel>().ToArray();var raw=lines.Select(line=>line.Text).ToArray();
                int changes=0;header.TextChanged+=(s,e)=>changes++;foreach(var line in lines)line.TextChanged+=(s,e)=>changes++;
                for(int repeat=0;repeat<2;repeat++)
                {
                    ArabicUi.Apply(form);UiLanguageCatalog.Apply(form);
                    Require(header.Text==expected&&header.AccessibleDescription==accessible&&lines.Select(line=>line.Text).SequenceEqual(raw)&&changes==0,"Session header aggregate/accessible/raw line data changed during language reapplication.");
                }
            }
            Console.WriteLine("PASS session header literal aggregate, accessible description and three raw lines: Arabic/English repeated application; zero changes; no SQL/native input.");
        }
        private static void VerifyArabicBusinessValueBoundary()
        {
            using(var form=new Form())
            {
                var host=new Panel();form.Controls.Add(host);
                var text=new TextBox{Text="Save"};
                var password=new TextBox{Text="Remember Me",UseSystemPasswordChar=true};
                var readOnly=new TextBox{Text="Stock",ReadOnly=true};
                var mixed=new TextBox{Text="Remember Me Labs A&B عربي 500 mg"};
                var input=new AntdUI.Input{Text="Quantity"};
                var choice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDown};choice.Items.Add("Cashier");choice.SelectedIndex=0;
                var business=new BusinessValueLabel{Text="Medicine"};
                var caption=new Label{Text="Save"};var action=new Button{Text="Close"};
                var numeric=new NumericUpDown{Value=12.5m,DecimalPlaces=2};var date=new DateTimePicker{Value=new DateTime(2026,10,6)};
                var values=new Control[]{text,password,readOnly,mixed,input,choice,business,numeric,date};
                var before=values.Select(value=>value.Text).ToArray();int changes=0;
                foreach(var value in values){host.Controls.Add(value);value.TextChanged+=(s,e)=>changes++;}
                host.Controls.Add(caption);host.Controls.Add(action);
                for(int repeat=0;repeat<2;repeat++)
                {
                    ArabicUi.Apply(form);
                    Require(values.Select(value=>value.Text).SequenceEqual(before)&&changes==0&&choice.SelectedIndex==0&&Convert.ToString(choice.SelectedItem)=="Cashier"&&numeric.Value==12.5m&&date.Value==new DateTime(2026,10,6),"Arabic translation changed a raw editor/password/selection/business label value or emitted a change event.");
                    Require(caption.Text=="حفظ"&&action.Text=="إغلاق","Arabic static captions/actions must remain translated.");
                }
            }
            Console.WriteLine("PASS Arabic business values: dictionary collisions and mixed Remember Me text preserved; no value events, captions/actions translated; no SQL/native input.");
        }
        private static void VerifyBusinessValueBoundary()
        {
            using (var form = new Form())
            {
                var host = new Panel(); form.Controls.Add(host);
                var text = new TextBox { Text = "سند" };
                var password = new TextBox { Text = "سند", UseSystemPasswordChar = true };
                var readOnly = new TextBox { Text = "الكل", ReadOnly = true };
                var input = new AntdUI.Input { Text = "قيد يدوي" };
                var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
                choice.Items.Add("سند"); choice.SelectedIndex = 0;
                var valueLabel = new BusinessValueLabel { Text = "سند" };
                var caption = new Label { Text = "سند" };
                host.Controls.AddRange(new Control[] { text, password, readOnly, input, choice, valueLabel, caption });
                int valueChanges = 0;
                foreach (Control editor in new Control[] { text, password, readOnly, input, choice, valueLabel })
                    editor.TextChanged += (sender, args) => valueChanges++;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    UiLanguageCatalog.Apply(form);
                    Require(text.Text == "سند" && password.Text == "سند" && readOnly.Text == "الكل" &&
                        input.Text == "قيد يدوي" && choice.Text == "سند" && choice.SelectedIndex == 0 &&
                        Convert.ToString(choice.SelectedItem) == "سند" && valueLabel.Text == "سند" && valueChanges == 0,
                        "UI translation must preserve entered/read-only/password/AntdUI/selected business values without change events.");
                    Require(caption.Text == "Voucher", "Static captions must remain translatable without touching data values.");
                }
            }
            Console.WriteLine("PASS catalog business values: known-key text/password/read-only/AntdUI/choice stay raw, selection and zero change events retained; captions translated; no SQL/native input.");
        }
        private static void Require(bool condition, string message)
        {
            if (condition) return;

            Console.Error.WriteLine("UI_LANGUAGE_ASSERT: " + message);
            throw new InvalidOperationException(message);
        }

        // Preview fixtures and domain values are business-like values and must stay exactly as they are.
        private static readonly string[] AllowedRuntimeFixtures =
        {
            "عميل تجريبي",
            "بيانات توضيحية للمعاينة فقط",
            "2,500.00 ر.ي",
            "مدير Admin",
            "باراسيتامول 500 mg",
            "توريد تجريبي",
            "أحمد Pharma 500 mg",
            "ليلى",
            "ر.ي",
            "الفرع الرئيسي",
            "بدون",
            "مستخدم المعاينة",
            "تحت التحصيل",
            "محصل",
            "مرتجع",
            "صرف إلى السيد",
            "استلمت من السيد",
            "1100 - العملاء",
            "1000 - الصندوق",
            "1000 - الأصول",
            "تقرير تجريبي"
        };

        private static void VerifyRuntimeTextFollowsLanguage()
        {
            var forms = new List<Form>();
            try
            {
                forms.Add(new ChartOfAccountsForm(true, true));
                forms.Add(new VoucherForm("نقد", true, true));
                forms.Add(new ReportColumnLayoutDialog(new System.Windows.Forms.DataGridViewColumn[0], true));
                forms.Add(new Login(false, true, true));
                forms.Add(new Billing(true, true));
                forms.Add(new Purchases(true, true));
                forms.Add(new Medicine(true, true));
                forms.Add(new Manufacturer(true, true));
                forms.Add(new ReceiptVoucherForm("نقد", true));
                (forms[forms.Count - 1] as ReceiptVoucherForm)?.ApplyReceiptCurrencyPresentation(true);
                forms.Add(new AccountingPeriodManagementForm(true));
                (forms[forms.Count - 1] as AccountingPeriodManagementForm)?.ApplyPresentation(true);
                forms.Add(new CustomerSupplierManagementForm(false, true, true));
                forms.Add(new ReportTemplateNameDialog("تقرير تجريبي", true));
                foreach (Form form in forms)
                {
                    var arabic = new List<string>();
                    CollectArabicTexts(form, arabic);
                    foreach (string text in arabic)
                    {
                        bool allowed = false;
                        foreach (string fixture in AllowedRuntimeFixtures)
                        {
                            if (text.Contains(fixture)) { allowed = true; break; }
                        }
                        Require(allowed, "The screen kept an Arabic caption in English mode: " + form.GetType().Name + " -> " + text);
                    }
                }
            }
            finally
            {
                foreach (Form form in forms) { try { form.Dispose(); } catch { } }
            }
            Console.WriteLine("PASS runtime language: twelve business screens show no Arabic caption in English mode; preview fixtures stay raw.");
        }

        private static void CollectArabicTexts(Control control, List<string> found)
        {
            if (control == null) return;
            string text = control.Text;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (char character in text)
                {
                    if (character >= '\u0600' && character <= '\u06FF') { found.Add(text); break; }
                }
            }
            foreach (Control child in control.Controls) CollectArabicTexts(child, found);
        }
    }
}





















