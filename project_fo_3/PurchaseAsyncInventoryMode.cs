using System;
using System.Data.SqlClient;
namespace project_fo_3 { internal static class PurchaseAsyncInventoryMode {
        internal static async System.Threading.Tasks.Task<bool> IsFullCommercialModeAsync(SqlConnection connection, SqlTransaction transaction,System.Threading.CancellationToken cancellationToken)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            const string sql = @"
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
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
            }
        }

        // يختلف عن جاهزية FullCommercial: نحتاج معرفة أن المشغّل طلب الوضع حتى
        internal static async System.Threading.Tasks.Task<bool> EnsureRequestedModeAndReadAsync(SqlConnection connection,SqlTransaction transaction,System.Threading.CancellationToken cancellationToken){
            bool requested;
            using(var command=new SqlCommand(@"IF OBJECT_ID('dbo.CommercialOperationalSettingsTb','U') IS NULL SELECT CAST(0 AS BIT)
ELSE SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.CommercialOperationalSettingsTb WITH(UPDLOCK,HOLDLOCK) WHERE SettingKey=N'Mode' AND SettingValue=N'FullCommercial') THEN 1 ELSE 0 END AS BIT);",connection,transaction))
                requested=Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            bool enabled=await IsFullCommercialModeAsync(connection,transaction,cancellationToken).ConfigureAwait(false);
            if(requested&&!enabled)throw new InvalidOperationException("تم طلب وضع البيع التجاري، لكن تسوية الرصيد المجمع مع الدفعات غير مكتملة. أوقف البيع وصحح الجرد؛ لن يعود النظام تلقائياً إلى مسار البيع القديم.");
            return enabled;
        }
}}
