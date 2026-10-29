using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer {
 public sealed class SqlPurchaseInventoryModeReadRepository:IPurchaseInventoryModeReadRepository {
  readonly Func<SqlConnection> factory;
  public SqlPurchaseInventoryModeReadRepository(Func<SqlConnection> factory){this.factory=factory??throw new ArgumentNullException(nameof(factory));}
  public async Task<bool> IsCommercialInventoryEnabledAsync(CancellationToken token){
   const string sql=@"
IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') RETURN;
IF OBJECT_ID('dbo.SaudiFeatureSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'CommercialSales' AND FeatureMode = N'Test') RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'EInvoice' AND FeatureMode IN (N'Simulator', N'Test')) RETURN;
IF OBJECT_ID('dbo.CommercialEInvoiceSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialEInvoiceSettingsTb WHERE SettingsId = 1 AND LocalSandboxEnabled = 1 AND LEN(LTRIM(RTRIM(SellerName))) > 0 AND LEN(LTRIM(RTRIM(SellerVatNumber))) > 0) RETURN;
IF EXISTS (
    SELECT 1 FROM dbo.MedicineTb1 m
    OUTER APPLY (SELECT ISNULL(SUM(l.QuantityRemaining), 0) AS LotQuantity FROM dbo.InventoryLotsTb l WHERE l.MedName = m.MedName) lots
    WHERE CONVERT(DECIMAL(18,3), m.MedQty) <> lots.LotQuantity
) RETURN;
SELECT CAST(1 AS INT);";
   using(var connection=factory())using(var command=new SqlCommand(sql,connection)){
    await connection.OpenAsync(token).ConfigureAwait(false);
    var result=await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    return result!=null&&result!=DBNull.Value&&Convert.ToInt32(result)==1;
   }
  }
 }
}
