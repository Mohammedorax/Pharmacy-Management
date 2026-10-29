using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryOperationReadRepository : IInventoryOperationReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryOperationReadRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        public async Task<IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using(var connection=connectionFactory())
            using(var command=connection.CreateCommand())
            {
                command.CommandText="SELECT MedName, MedName + N' - ' + CompName AS DisplayName, MedQty FROM dbo.MedicineTb1 ORDER BY MedName;";
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
                var rows=new List<InventoryMedicineLookupRow>();
                using(var reader=await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
                    while(await reader.ReadAsync(cancellation).ConfigureAwait(false))
                        rows.Add(new InventoryMedicineLookupRow(Text(reader,0),Text(reader,1),Convert.ToInt32(reader.GetValue(2),CultureInfo.InvariantCulture)));
                return rows.AsReadOnly();
            }
        }
        public async Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,CancellationToken cancellation)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using(var connection=connectionFactory())
                {
                    await connection.OpenAsync(cancellation).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    // Keep the quantity and movement rows in the same read snapshot.
                    using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        int current;
                        using(var command=connection.CreateCommand())
                        {
                            command.Transaction=transaction;
                            command.CommandTimeout=15;
                            command.CommandText="SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName = @MedName";
                            command.Parameters.Add("@MedName",SqlDbType.NVarChar,200).Value=request.MedicineName;
                            object value=await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false);
                            if(value==null||value==DBNull.Value)throw new InvalidOperationException("الصنف غير موجود في قاعدة البيانات.");
                            current=Convert.ToInt32(value,CultureInfo.InvariantCulture);
                        }
                        var rows=new List<InventoryMovementReadRow>();
                        using(var command=connection.CreateCommand())
                        {
                            command.Transaction=transaction;
                            command.CommandTimeout=15;
                            command.CommandText=@"SELECT CreatedAt, MedName, SourceType, SourceId, QuantityDelta,
QuantityBefore, QuantityAfter, UnitCost, UnitPrice, CreatedBy, Notes
FROM dbo.InventoryMovementsTb
WHERE MedName = @MedName AND CreatedAt >= @FromDate AND CreatedAt < DATEADD(DAY,1,@ToDate)
ORDER BY CreatedAt DESC, MovementId DESC;";
                            command.Parameters.Add("@MedName",SqlDbType.NVarChar,200).Value=request.MedicineName;
                            command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.From;
                            command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.To;
                            using(var reader=await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
                                while(await reader.ReadAsync(cancellation).ConfigureAwait(false))
                                    rows.Add(new InventoryMovementReadRow(reader.GetDateTime(0),Text(reader,1),Text(reader,2),Text(reader,3),
                                        Convert.ToDecimal(reader.GetValue(4),CultureInfo.InvariantCulture),Number(reader,5),Number(reader,6),Number(reader,7),Number(reader,8),Text(reader,9),Text(reader,10)));
                        }
                        cancellation.ThrowIfCancellationRequested();
                        transaction.Commit();
                        cancellation.ThrowIfCancellationRequested();
                        return new InventoryOperationReadSnapshot(request.MedicineName,current,rows.AsReadOnly());
                    }
                }
            }
            catch(SqlException error) when(cancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException("Inventory read was canceled.",error,cancellation);
            }
        }
        private static string Text(SqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
        private static decimal? Number(SqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?(decimal?)null:Convert.ToDecimal(reader.GetValue(ordinal),CultureInfo.InvariantCulture);
    }
}

