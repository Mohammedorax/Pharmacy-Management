using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlMedicineUnitCatalogRepository : IMedicineUnitCatalogRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlMedicineUnitCatalogRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        private static SqlCommand Command(SqlConnection connection,SqlTransaction transaction,string text)
        {return new SqlCommand(text,connection,transaction){CommandTimeout=30};}
        private static void Text(SqlCommand command,string name,int size,string value)
        {command.Parameters.Add(name,SqlDbType.NVarChar,size).Value=value;}
        private static void GuidParameter(SqlCommand command,string name,Guid value)
        {command.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=value;}
        private static void Money(SqlCommand command,string name,decimal value)
        {var p=command.Parameters.Add(name,SqlDbType.Decimal);p.Precision=18;p.Scale=2;p.Value=value;}

        private static readonly string SchemaContract=LoadSchemaContract();
        private static string LoadSchemaContract()
        {
            using(var stream=typeof(SqlMedicineUnitCatalogRepository).Assembly.GetManifestResourceStream("PharmacyManagement.MedicineUnitCatalog.v1.sql"))
            {
                if(stream==null)throw new InvalidOperationException("تعذر تحميل عقد مخطط الوحدات.");
                using(var reader=new System.IO.StreamReader(stream))return reader.ReadToEnd();
            }
        }
        private static async Task EnsureSchemaAsync(SqlConnection connection,CancellationToken token)
        {
            const string ready=@"IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.MedicineUnitCatalogVersion' AND CONVERT(nvarchar(20),value)=N'1')
 OR OBJECT_ID(N'dbo.MedicineUnitCatalogTb',N'U') IS NULL OR OBJECT_ID(N'dbo.MedicineUnitsTb',N'U') IS NULL OR OBJECT_ID(N'dbo.MedicineUnitWriteReceiptsTb',N'U') IS NULL
 THROW 51101,N'مخطط وحدات الأصناف غير مثبت؛ لم تتغير البيانات.',1;
";
            // Only metadata validation. Reads and saves never install or repair schema implicitly.
            using(var command=Command(connection,null,ready+SchemaContract))
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        private static async Task<int> ReadMedicineAsync(SqlConnection connection,SqlTransaction transaction,string name,bool write,CancellationToken token)
        {
            using(var command=Command(connection,transaction,"SELECT MedName,MedQty FROM dbo.MedicineTb1 "+(write?"WITH(UPDLOCK,HOLDLOCK) ":"")+"WHERE MedName=@Name;"))
            {
                Text(command,"@Name",200,name);
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if(!await reader.ReadAsync(token).ConfigureAwait(false)||!string.Equals(reader.GetString(0),name,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("الصنف غير موجود؛ حدّث قائمة الأصناف.");
                    return reader.GetInt32(1);
                }
            }
        }
        public async Task<MedicineUnitCatalogSnapshot> GetAsync(string medicineName,CancellationToken cancellationToken)
        {
            if(string.IsNullOrWhiteSpace(medicineName)||medicineName.Length>200)throw new InvalidOperationException("اسم الصنف غير صالح.");
            using(var connection=connectionFactory())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await EnsureSchemaAsync(connection,cancellationToken).ConfigureAwait(false);
                using(var transaction=await SqlTransactionBoundaryDispatcher.BeginAsync(connection,IsolationLevel.RepeatableRead).ConfigureAwait(false))
                {
                    try
                    {
                        int stock=await ReadMedicineAsync(connection,transaction,medicineName,false,cancellationToken).ConfigureAwait(false);
                        Guid id=Guid.Empty,baseId=Guid.Empty;long revision=0;int confirmed=0;string currency=null;
                        using(var command=Command(connection,transaction,"SELECT CatalogId,BaseUnitId,Revision,ConfirmedBaseStockQuantity,CurrencyCode FROM dbo.MedicineUnitCatalogTb WHERE MedName=@Name;"))
                        {
                            Text(command,"@Name",200,medicineName);
                            using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                                if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {id=reader.GetGuid(0);baseId=reader.GetGuid(1);revision=reader.GetInt64(2);confirmed=reader.GetInt32(3);currency=reader.GetString(4);}
                        }
                        MedicineUnitCatalogSnapshot result=null;
                        if(id!=Guid.Empty)
                        {
                            var units=new List<MedicineUnitDefinition>();
                            using(var command=Command(connection,transaction,"SELECT UnitId,UnitName,BaseUnitsPerUnit,SellingPrice,BuyingPrice FROM dbo.MedicineUnitsTb WHERE CatalogId=@Id AND IsActive=1 ORDER BY DisplayOrder;"))
                            {
                                GuidParameter(command,"@Id",id);
                                using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                                    while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                        units.Add(new MedicineUnitDefinition(reader.GetGuid(0),revision,reader.GetString(1),reader.GetInt32(2),reader.GetDecimal(3),reader.GetDecimal(4),currency));
                            }
                            result=new MedicineUnitCatalogSnapshot(id,medicineName,baseId,revision,stock,confirmed,currency,units);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);return result;
                    }
                    catch{try{await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false);}catch{}throw;}
                }
            }
        }
        public async Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogWrite request,OperationActor actor,Action authorizeCommit,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(authorizeCommit==null)throw new ArgumentNullException(nameof(authorizeCommit));
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||actor.UserName.Length>200||string.IsNullOrWhiteSpace(actor.UserRole)||actor.UserRole.Length>50)
                throw new UnauthorizedAccessException("هوية حفظ الوحدات غير صالحة.");
            string user=actor.UserName,role=actor.UserRole;authorizeCommit();cancellationToken.ThrowIfCancellationRequested();
            using(var connection=connectionFactory())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await EnsureSchemaAsync(connection,cancellationToken).ConfigureAwait(false);
                using(var transaction=await SqlTransactionBoundaryDispatcher.BeginAsync(connection,IsolationLevel.Serializable).ConfigureAwait(false))
                {
                    try
                    {
                        using(var command=Command(connection,transaction,@"DECLARE @Result int;EXEC @Result=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;SELECT @Result;"))
                        {
                            Text(command,"@Resource",255,"Pharmacy.MedicineUnits."+request.RequestId.ToString("D"));
                            if(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))<0)
                                throw new InvalidOperationException("تعذر تأكيد حفظ الوحدات؛ أعد محاولة الطلب نفسه.");
                        }
                        using(var command=Command(connection,transaction,"SELECT RequestFingerprint,OwnerUser,OwnerRole,ResultRevision,MedicineName FROM dbo.MedicineUnitWriteReceiptsTb WHERE RequestId=@Request;"))
                        {
                            GuidParameter(command,"@Request",request.RequestId);
                            using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    if(!((byte[])reader[0]).SequenceEqual(request.Fingerprint)||reader.GetString(1)!=user||reader.GetString(2)!=role||reader.GetString(4)!=request.MedicineName)
                                        throw new MedicineUnitRequestConflictException();
                                    long saved=reader.GetInt64(3);reader.Close();authorizeCommit();cancellationToken.ThrowIfCancellationRequested();
                                    await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false);
                                    return new MedicineUnitCatalogSaveResult(request.RequestId,saved,true);
                                }
                            }
                        }
                        int stock=await ReadMedicineAsync(connection,transaction,request.MedicineName,true,cancellationToken).ConfigureAwait(false);
                        Guid catalog=Guid.Empty,baseId=Guid.Empty;long revision=0;
                        using(var command=Command(connection,transaction,"SELECT CatalogId,BaseUnitId,Revision FROM dbo.MedicineUnitCatalogTb WITH(UPDLOCK,HOLDLOCK) WHERE MedName=@Name;"))
                        {
                            Text(command,"@Name",200,request.MedicineName);
                            using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                                if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){catalog=reader.GetGuid(0);baseId=reader.GetGuid(1);revision=reader.GetInt64(2);}
                        }
                        if(revision!=request.ExpectedRevision)throw new MedicineUnitCatalogConflictException();
                        bool create=catalog==Guid.Empty;
                        if(create)
                        {
                            if(request.ExpectedInitialStockQuantity!=stock)throw new MedicineUnitCatalogConflictException("تغير رصيد الصنف منذ مراجعة الوحدة الأساسية؛ حدّث الرصيد وأعد التأكيد.");
                            catalog=request.RequestId;
                        }
                        else if(baseId!=request.BaseUnitId)throw new MedicineUnitCatalogConflictException("لا يمكن تبديل الوحدة الأساسية بعد التهيئة؛ يلزم تحويل مخزون موثق مستقل.");
                        using(var command=Command(connection,transaction,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WITH(UPDLOCK,HOLDLOCK) WHERE IsSystemCurrency=1;"))
                        using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)||!string.Equals(reader.GetString(0).Trim(),request.CurrencyCode,StringComparison.OrdinalIgnoreCase)||await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                throw new MedicineUnitCatalogConflictException("عملة أسعار الوحدات تختلف عن عملة النظام الحالية؛ راجع الأسعار قبل الحفظ.");
                        }
                        foreach(var unit in request.Units.OrderBy(u=>u.Id))
                        using(var command=Command(connection,transaction,"SELECT CatalogId FROM dbo.MedicineUnitsTb WITH(UPDLOCK,HOLDLOCK) WHERE UnitId=@Unit;"))
                        {
                            GuidParameter(command,"@Unit",unit.Id);
                            object owner=await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                            if(owner!=null&&(Guid)owner!=catalog)throw new InvalidOperationException("هوية الوحدة مرتبطة بصنف آخر.");
                        }
                        using(var command=Command(connection,transaction,create?@"INSERT dbo.MedicineUnitCatalogTb(CatalogId,MedName,BaseUnitId,Revision,CurrencyCode,ConfirmedBaseStockQuantity) VALUES(@Id,@Name,@Base,@Revision,@Currency,@Stock);":
                            @"UPDATE dbo.MedicineUnitCatalogTb SET Revision=@Revision,CurrencyCode=@Currency,UpdatedAtUtc=SYSUTCDATETIME() WHERE CatalogId=@Id AND Revision=@Expected;"))
                        {
                            GuidParameter(command,"@Id",catalog);Text(command,"@Name",200,request.MedicineName);GuidParameter(command,"@Base",request.BaseUnitId);
                            command.Parameters.Add("@Revision",SqlDbType.BigInt).Value=request.ExpectedRevision+1;command.Parameters.Add("@Expected",SqlDbType.BigInt).Value=request.ExpectedRevision;
                            Text(command,"@Currency",10,request.CurrencyCode);command.Parameters.Add("@Stock",SqlDbType.Int).Value=stock;
                            if(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)!=1)throw new MedicineUnitCatalogConflictException();
                        }
                        using(var command=Command(connection,transaction,"UPDATE dbo.MedicineUnitsTb SET IsActive=0 WHERE CatalogId=@Id;"))
                        {GuidParameter(command,"@Id",catalog);await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);}
                        for(int index=0;index<request.Units.Count;index++)
                        {
                            var unit=request.Units[index];
                            using(var command=Command(connection,transaction,@"IF EXISTS(SELECT 1 FROM dbo.MedicineUnitsTb WHERE UnitId=@Unit)
 UPDATE dbo.MedicineUnitsTb SET UnitName=@Name,BaseUnitsPerUnit=@Factor,SellingPrice=@Selling,BuyingPrice=@Buying,IsActive=1,DisplayOrder=@Order WHERE UnitId=@Unit AND CatalogId=@Id;
ELSE INSERT dbo.MedicineUnitsTb(UnitId,CatalogId,UnitName,BaseUnitsPerUnit,SellingPrice,BuyingPrice,IsActive,DisplayOrder) VALUES(@Unit,@Id,@Name,@Factor,@Selling,@Buying,1,@Order);"))
                            {
                                GuidParameter(command,"@Unit",unit.Id);GuidParameter(command,"@Id",catalog);Text(command,"@Name",50,unit.Name);
                                command.Parameters.Add("@Factor",SqlDbType.Int).Value=unit.BaseUnitsPerUnit;Money(command,"@Selling",unit.SellingPrice);Money(command,"@Buying",unit.BuyingPrice);
                                command.Parameters.Add("@Order",SqlDbType.Int).Value=index;
                                if(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)!=1)throw new InvalidOperationException("تعذر حفظ تعريف الوحدة.");
                            }
                        }
                        using(var command=Command(connection,transaction,@"INSERT dbo.MedicineUnitWriteReceiptsTb(RequestId,CatalogId,MedicineName,ExpectedRevision,ResultRevision,RequestFingerprint,Payload,OwnerUser,OwnerRole)
VALUES(@Request,@Catalog,@Name,@Expected,@Revision,@Fingerprint,@Payload,@User,@Role);
INSERT dbo.AuditLogTb(UserName,UserRole,ActionName,EntityName,Details) VALUES(@User,@Role,N'MedicineUnitsSaved',N'Medicine',@Details);"))
                        {
                            GuidParameter(command,"@Request",request.RequestId);GuidParameter(command,"@Catalog",catalog);Text(command,"@Name",200,request.MedicineName);
                            command.Parameters.Add("@Expected",SqlDbType.BigInt).Value=request.ExpectedRevision;command.Parameters.Add("@Revision",SqlDbType.BigInt).Value=request.ExpectedRevision+1;
                            command.Parameters.Add("@Fingerprint",SqlDbType.Binary,32).Value=request.Fingerprint;command.Parameters.Add("@Payload",SqlDbType.VarBinary,-1).Value=request.Payload;
                            Text(command,"@User",200,user);Text(command,"@Role",50,role);Text(command,"@Details",1000,"حفظ وحدات الصنف: "+request.MedicineName+"؛ الإصدار "+(request.ExpectedRevision+1)+"؛ عدد الوحدات "+request.Units.Count);
                            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                        authorizeCommit();cancellationToken.ThrowIfCancellationRequested();
                        await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);
                        return new MedicineUnitCatalogSaveResult(request.RequestId,request.ExpectedRevision+1,false);
                    }
                    catch(Exception operationFailure)
                    {
                        try{await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false);}
                        catch(Exception rollbackFailure){throw new AggregateException("تعذر تأكيد تراجع حفظ الوحدات؛ احتفظ برقم المحاولة وافحص نتيجتها.",operationFailure,rollbackFailure);}
                        var sql=operationFailure as SqlException;
                        if(sql!=null&&(sql.Number==2601||sql.Number==2627))throw new InvalidOperationException("اسم أو ترتيب الوحدة مكرر وفق قواعد قاعدة البيانات.",sql);
                        throw;
                    }
                }
            }
        }
    }
}
