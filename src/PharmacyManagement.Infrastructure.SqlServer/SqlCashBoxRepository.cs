using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCashBoxRepository : ICashBoxRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlCashBoxRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<CashBoxRecord> GetAll()
        {
            var result = new List<CashBoxRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT b.CashBoxId,b.BoxCode,b.BoxName,b.IsActive,
       a.EmpId,e.EmpName
FROM dbo.CashBoxesTb b
LEFT JOIN dbo.CashBoxAssignmentsTb a ON a.CashBoxId=b.CashBoxId
LEFT JOIN dbo.EmployeeTb1 e ON e.EmpId=a.EmpId
ORDER BY b.BoxCode;";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new CashBoxRecord
                        {
                            CashBoxId = reader.GetGuid(0),
                            Code = reader.GetString(1),
                            Name = reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            EmployeeId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            EmployeeName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                        });
            }
            return result;
        }

        public void SaveBatch(IReadOnlyList<CashBoxRecord> boxes, OperationActor actor)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand lockCommand = new SqlCommand(
                            "DECLARE @Result int; EXEC @Result=sys.sp_getapplock @Resource=N'PharmacyManagement.CashBoxDefinitions', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000; SELECT @Result;",
                            connection, transaction))
                        {
                            if (Convert.ToInt32(lockCommand.ExecuteScalar()) < 0)
                                throw new InvalidOperationException("تعذر قفل تعريف صناديق النقد.");
                        }

                        foreach (CashBoxRecord box in boxes) UpsertBox(connection, transaction, box);
                        // Delete all touched assignments first so a deliberate swap in this batch is valid.
                        foreach (CashBoxRecord box in boxes)
                            using (SqlCommand remove = new SqlCommand(
                                "DELETE dbo.CashBoxAssignmentsTb WHERE CashBoxId=@BoxId;", connection, transaction))
                            {
                                remove.Parameters.Add("@BoxId", SqlDbType.UniqueIdentifier).Value = box.CashBoxId;
                                remove.ExecuteNonQuery();
                            }
                        foreach (CashBoxRecord box in boxes)
                        {
                            if (!string.IsNullOrEmpty(box.EmployeeId))
                                InsertAssignment(connection, transaction, box, actor);
                            InsertAudit(connection, transaction, box, actor);
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch (InvalidOperationException) { }
                        throw;
                    }
                }
            }
        }

        private static void UpsertBox(SqlConnection connection, SqlTransaction transaction, CashBoxRecord box)
        {
            using (SqlCommand command = new SqlCommand(@"IF EXISTS
    (SELECT 1 FROM dbo.CashBoxesTb WITH(UPDLOCK,HOLDLOCK) WHERE CashBoxId=@Id)
    UPDATE dbo.CashBoxesTb SET BoxCode=@Code,BoxName=@Name,IsActive=@Active,
        UpdatedAt=SYSUTCDATETIME() WHERE CashBoxId=@Id;
ELSE
    INSERT dbo.CashBoxesTb(CashBoxId,BoxCode,BoxName,IsActive)
    VALUES(@Id,@Code,@Name,@Active);", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = box.CashBoxId;
                command.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = box.Code;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = box.Name;
                command.Parameters.Add("@Active", SqlDbType.Bit).Value = box.IsActive;
                command.ExecuteNonQuery();
            }
        }

        private static void InsertAssignment(SqlConnection connection, SqlTransaction transaction,
            CashBoxRecord box, OperationActor actor)
        {
            using (SqlCommand command = new SqlCommand(@"IF EXISTS
    (SELECT 1 FROM dbo.CashBoxAssignmentsTb WITH(UPDLOCK,HOLDLOCK) WHERE EmpId=@EmployeeId)
    THROW 51001,N'الكاشير مسند بالفعل إلى صندوق آخر. أزل الإسناد القديم أولاً.',1;
IF NOT EXISTS (SELECT 1 FROM dbo.EmployeeTb1 WHERE EmpId=@EmployeeId)
    THROW 51002,N'رقم الكاشير غير موجود.',1;
INSERT dbo.CashBoxAssignmentsTb(EmpId,CashBoxId,AssignedBy)
VALUES(@EmployeeId,@BoxId,@Actor);", connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.NVarChar, 50).Value = box.EmployeeId;
                command.Parameters.Add("@BoxId", SqlDbType.UniqueIdentifier).Value = box.CashBoxId;
                command.Parameters.Add("@Actor", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.ExecuteNonQuery();
            }
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction,
            CashBoxRecord box, OperationActor actor)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@User,@Role,N'CashBoxSaved',N'CashBox',@Details);", connection, transaction))
            {
                command.Parameters.Add("@User", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    "الصندوق " + box.Code + " (" + box.CashBoxId + ")، الكاشير: " +
                    (string.IsNullOrEmpty(box.EmployeeId) ? "غير مسند" : box.EmployeeId) +
                    "، نشط: " + box.IsActive + ".";
                command.ExecuteNonQuery();
            }
        }
    }
}
