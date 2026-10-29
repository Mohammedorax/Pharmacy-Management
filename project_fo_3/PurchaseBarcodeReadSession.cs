using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
 internal static partial class ApplicationServices
 {
  internal static PurchaseBarcodeReadSession CreatePurchaseBarcodeReadSession()
  { return CreatePurchaseBarcodeReadSession(() => Database.ConnectionString); }
  internal static PurchaseBarcodeReadSession CreatePurchaseBarcodeReadSession(Func<string> connectionStringProvider)
  {
   if(connectionStringProvider==null)throw new ArgumentNullException(nameof(connectionStringProvider));
   string connectionString=connectionStringProvider();
   long revision=SessionContext.CurrentSessionRevision;
   string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
   int invalidated=0;
   Func<bool> contextCurrent=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
    string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
    string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
    string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
    string.Equals(connectionString,connectionStringProvider(),StringComparison.Ordinal)&&PermissionService.CanOpen(AppPermission.Purchases);
   Func<bool> current=()=>{
    if(System.Threading.Volatile.Read(ref invalidated)!=0)return false;
    bool valid=contextCurrent();
    if(!valid)System.Threading.Interlocked.Exchange(ref invalidated,1);
    return valid;
   };
   Func<OperationActor,bool> authorized=actor=>current()&&actor!=null&&
    string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase);
   var search=new MedicineSearchService(new SqlMedicineSearchRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),canReadBarcode:authorized);
   var medicines=new PurchaseMedicineReadService(new SqlPurchaseMedicineReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
   var lookups=new PurchaseLookupReadService(new SqlPurchaseLookupReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
   var mode=new PurchaseInventoryModeReadService(new SqlPurchaseInventoryModeReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
   var numbers=new PurchaseDraftNumberService(new SqlPurchaseDraftNumberRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
   var currency=new CurrencyReadService(new SqlCurrencyReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
   return new PurchaseBarcodeReadSession(search,medicines,new OperationActor{UserName=user,UserRole=role},current,lookups,mode,numbers,currency);
  }
 }
 internal sealed class PurchaseBarcodeReadSession
 {
  internal readonly IMedicineSearchService Barcode;
  internal readonly PurchaseMedicineReadService Medicines;
  internal readonly OperationActor Actor;
  internal readonly PurchaseLookupReadService Lookups;
  internal readonly PurchaseInventoryModeReadService InventoryMode;
  internal readonly PurchaseDraftNumberService Numbers;
  internal readonly ICurrencyReadService Currency;
  private readonly Func<bool> current;
  internal PurchaseBarcodeReadSession(IMedicineSearchService barcode,PurchaseMedicineReadService medicines,OperationActor actor,Func<bool> current)
  :this(barcode,medicines,actor,current,null){}
  internal PurchaseBarcodeReadSession(IMedicineSearchService barcode,PurchaseMedicineReadService medicines,OperationActor actor,Func<bool> current,PurchaseLookupReadService lookups)
  :this(barcode,medicines,actor,current,lookups,null){}
  internal PurchaseBarcodeReadSession(IMedicineSearchService barcode,PurchaseMedicineReadService medicines,OperationActor actor,Func<bool> current,PurchaseLookupReadService lookups,PurchaseInventoryModeReadService inventoryMode)
  :this(barcode,medicines,actor,current,lookups,inventoryMode,null){}
  internal PurchaseBarcodeReadSession(IMedicineSearchService barcode,PurchaseMedicineReadService medicines,OperationActor actor,Func<bool> current,PurchaseLookupReadService lookups,PurchaseInventoryModeReadService inventoryMode,PurchaseDraftNumberService numbers,ICurrencyReadService currency=null)
  {Barcode=barcode;Medicines=medicines;Actor=actor;this.current=current;Lookups=lookups;InventoryMode=inventoryMode;Numbers=numbers;Currency=currency;}
  internal bool IsCurrent=>current!=null&&current();
 }
}





