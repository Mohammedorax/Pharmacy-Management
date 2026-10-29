using System;
using System.Globalization;
using PharmacyManagement.Domain;
namespace PharmacyManagement.Application
{
    // Original financial inputs; separate from the immutable local posting payload.
    public sealed class VoucherDraftInputs
    {
        public VoucherDraftInputs(decimal originalAmount,decimal exchangeRate,string currencyCode,string paymentMethod,string notes,string chequeNumber,string chequeBank,DateTime? chequeDate,string chequeStatus,string representative,string partyName)
        {
            decimal local;if(!VoucherAmountCalculator.TryCalculate(originalAmount,exchangeRate,out local)||local<=0)throw new InvalidOperationException("مدخلات تحويل السند غير صالحة.");
            if(string.IsNullOrWhiteSpace(currencyCode)||currencyCode.Length>10)throw new InvalidOperationException("عملة المدخلات الأصلية غير صالحة.");
            if(paymentMethod!="Cash"&&paymentMethod!="Bank"&&paymentMethod!="Transfer"&&paymentMethod!="Cheque")throw new InvalidOperationException("طريقة دفع السند غير صالحة.");
            foreach(string value in new[]{notes,chequeNumber,chequeBank,chequeStatus,representative,partyName})if(value!=null&&value.Length>500)throw new InvalidOperationException("نص مدخلات السند يتجاوز الحد.");
            if(paymentMethod=="Cheque"&&(string.IsNullOrWhiteSpace(chequeNumber)||string.IsNullOrWhiteSpace(chequeBank)||!chequeDate.HasValue))throw new InvalidOperationException("مدخلات الشيك الأصلية ناقصة.");
            if(chequeDate.HasValue&&(chequeDate.Value<new DateTime(1753,1,1)||chequeDate.Value>new DateTime(9999,12,31,23,59,59,997)))throw new InvalidOperationException("تاريخ الشيك خارج نطاق التخزين.");
            OriginalAmount=originalAmount;ExchangeRate=exchangeRate;CurrencyCode=currencyCode;PaymentMethod=paymentMethod;Notes=notes;ChequeNumber=chequeNumber;ChequeBank=chequeBank;ChequeDate=chequeDate;ChequeStatus=chequeStatus;Representative=representative;PartyName=partyName;
        }
        public decimal OriginalAmount {get;} public decimal ExchangeRate {get;} public string CurrencyCode {get;} public string PaymentMethod {get;} public string Notes {get;} public string ChequeNumber {get;} public string ChequeBank {get;} public DateTime? ChequeDate {get;} public string ChequeStatus {get;} public string Representative {get;} public string PartyName {get;}
        public string BuildNotes()
        {
            string text=(Notes??"").Trim();
            return PaymentMethod=="Cheque"?text+" | شيك رقم: "+ChequeNumber+" | البنك: "+ChequeBank+" | الاستحقاق: "+ChequeDate.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+" | الحالة: "+ChequeStatus:
                text+" | طريقة الدفع: "+(PaymentMethod=="Cash"?"نقدي":"تحويل");
        }
        internal void EnsureMatches(VoucherPostRequest request)
        {
            decimal local;if(!VoucherAmountCalculator.TryCalculate(OriginalAmount,ExchangeRate,out local)||local!=request.Amount||BuildNotes()!=request.Notes)
                throw new InvalidOperationException("المدخلات الأصلية لا تطابق مبلغ السند وبيانه الثابتين.");
        }
        public override bool Equals(object obj)
        {
            var b=obj as VoucherDraftInputs;return b!=null&&OriginalAmount==b.OriginalAmount&&ExchangeRate==b.ExchangeRate&&CurrencyCode==b.CurrencyCode&&PaymentMethod==b.PaymentMethod&&Notes==b.Notes&&ChequeNumber==b.ChequeNumber&&ChequeBank==b.ChequeBank&&ChequeDate==b.ChequeDate&&ChequeStatus==b.ChequeStatus&&Representative==b.Representative&&PartyName==b.PartyName;
        }
        public override int GetHashCode()=>OriginalAmount.GetHashCode();
    }
}