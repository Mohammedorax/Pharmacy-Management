using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Domain;
namespace project_fo_3 {
 internal static class PurchaseAsyncAccountingService {
  internal static async Task<decimal> GetEffectiveTaxRateAsync(SqlConnection connection,SqlTransaction transaction,CancellationToken cancellationToken){
   string country;
   using(TaxSettingsRecovery.Enter()){TaxSettingsRecovery.EnsureSettled();country=TaxPolicyService.GetConfiguredCountryCode();}
   bool enabled=false;decimal rate=0;
   using(var command=new SqlCommand("SELECT TOP(1) IsEnabled,TaxRate FROM dbo.TaxSettingsTb WITH(HOLDLOCK) ORDER BY TaxSettingId;",connection,transaction))
   using(var reader=await command.ExecuteReaderAsync(CommandBehavior.SingleRow,cancellationToken).ConfigureAwait(false)){
    if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){
     enabled=!reader.IsDBNull(0)&&Convert.ToBoolean(reader[0]);
     if(!reader.IsDBNull(1))rate=Convert.ToDecimal(reader[1]);
    }
   }
   using(TaxSettingsRecovery.Enter()){
    TaxSettingsRecovery.EnsureSettled();
    if(!string.Equals(country,TaxPolicyService.GetConfiguredCountryCode(),StringComparison.Ordinal))
     throw new InvalidOperationException("تغير بلد سياسة الضريبة أثناء الحفظ؛ لم تعتمد الفاتورة.");
    return new TaxPolicy(country,enabled,rate).EffectiveRate;
   }
  }
  internal static Task PostPurchaseInvoiceAsync(SqlConnection connection,SqlTransaction transaction,string purchaseId,decimal totalAmount,string description,DateTime entryDate,string actorName,string paymentMethod,CancellationToken cancellationToken){return PostPurchaseInvoiceWithCurrencyAsync(connection,transaction,purchaseId,totalAmount,description,entryDate,actorName,paymentMethod,cancellationToken,null);}
  internal static async Task PostPurchaseInvoiceWithCurrencyAsync(SqlConnection connection,SqlTransaction transaction,string purchaseId,decimal totalAmount,string description,DateTime entryDate,string actorName,string paymentMethod,CancellationToken cancellationToken,string expectedSystemCurrencyCode){
   string settlement=AccountingService.GetPurchaseSettlementAccount(paymentMethod);
   if(totalAmount<=0||await PurchaseAsyncJournalWriter.GetJournalEntryIdBySourceAsync(connection,transaction,"PurchaseInvoice",purchaseId,cancellationToken).ConfigureAwait(false)>0)return;
   decimal rate=await GetEffectiveTaxRateAsync(connection,transaction,cancellationToken).ConfigureAwait(false);
   decimal tax=rate<=0?0:InvoiceTotalsCalculator.Round(totalAmount*rate/(100+rate));
   var lines=new List<JournalLineValues>{
    new JournalLineValues(AccountingService.InventoryAccount,totalAmount-tax,0,description,null),
    new JournalLineValues(settlement,0,totalAmount,description,null)
   };
   if(tax>0)lines.Add(new JournalLineValues(AccountingService.InputTaxAccount,tax,0,description,null));
   await PurchaseAsyncJournalWriter.CreateAsync(connection,transaction,entryDate,"PurchaseInvoice",purchaseId,description,string.IsNullOrWhiteSpace(actorName)?"Unknown":actorName.Trim(),lines,cancellationToken,expectedSystemCurrencyCode).ConfigureAwait(false);
  }
  internal static async Task EnsurePurchaseTaxAsync(SqlConnection connection,SqlTransaction transaction,decimal? expected,CancellationToken cancellationToken){
   if(!expected.HasValue)return;
   decimal actual=await GetEffectiveTaxRateAsync(connection,transaction,cancellationToken).ConfigureAwait(false);
   if(expected.Value!=actual)throw new InvalidOperationException("ضريبة الشراء المستورد تختلف عن السياسة الفعالة؛ لم تتغير قيم الفاتورة أو القيد.");
  }
 }
}
