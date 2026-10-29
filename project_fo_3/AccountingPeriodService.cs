using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class AccountingPeriodService
    {
        internal static DataTable GetPeriods()
        {
            using(SqlConnection c=Database.CreateConnection()) using(SqlCommand cmd=c.CreateCommand())
            {
                cmd.CommandText=@"SELECT PeriodId AS [معرف الفترة],StartDate AS [من],EndDate AS [إلى],CASE Status WHEN N'Open' THEN N'مفتوحة' ELSE N'مقفلة' END AS [الحالة],ClosedAtUtc AS [وقت الإقفال],ISNULL(ClosedBy,N'') AS [أقفلها],ISNULL(Notes,N'') AS [ملاحظات] FROM dbo.AccountingPeriodsTb ORDER BY StartDate DESC;";
                DataTable table=new DataTable(); new SqlDataAdapter(cmd).Fill(table); return table;
            }
        }

        internal static Guid CreateOpen(DateTime startDate, DateTime endDate, string notes)
        {
            using(SqlConnection c=Database.CreateConnection())
            {
                c.Open(); using(SqlTransaction t=c.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        Guid id=CreateOpen(c,t,startDate,endDate,notes);
                        t.Commit(); return id;
                    }
                    catch { try { t.Rollback(); } catch { } throw; }
                }
            }
        }

        internal static Guid CreateOpen(SqlConnection c, SqlTransaction t, DateTime startDate, DateTime endDate, string notes)
        {
            EnsureManager();
            if(endDate.Date<startDate.Date) throw new InvalidOperationException("تاريخ نهاية الفترة يجب ألا يسبق بدايتها.");
            if((notes??string.Empty).Trim().Length>500) throw new InvalidOperationException("ملاحظات الفترة أطول من الحد المسموح.");
            Guid id=Guid.NewGuid();
            using(SqlCommand cmd=new SqlCommand(@"IF OBJECT_ID(N'dbo.AccountingPeriodsTb',N'U') IS NULL THROW 51000,N'ترحيل الفترات المحاسبية غير مثبت.',1;
INSERT dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@Id,@Start,@End,N'Open',NULL,NULL,@Notes);",c,t))
            { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=id; cmd.Parameters.Add("@Start",SqlDbType.Date).Value=startDate.Date; cmd.Parameters.Add("@End",SqlDbType.Date).Value=endDate.Date; cmd.Parameters.Add("@Notes",SqlDbType.NVarChar,500).Value=string.IsNullOrWhiteSpace(notes)?(object)DBNull.Value:notes.Trim(); cmd.ExecuteNonQuery(); }
            return id;
        }

        internal static void Close(Guid periodId, string notes)
        {
            using(SqlConnection c=Database.CreateConnection())
            {
                c.Open(); using(SqlTransaction t=c.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        Close(c,t,periodId,notes);
                        t.Commit();
                    }
                    catch { try { t.Rollback(); } catch { } throw; }
                }
            }
        }

        internal static void Close(SqlConnection c, SqlTransaction t, Guid periodId, string notes)
        {
            EnsureManager();
            if(periodId==Guid.Empty) throw new InvalidOperationException("حدد فترة مفتوحة أولاً.");
            if((notes??string.Empty).Trim().Length>500) throw new InvalidOperationException("ملاحظات الإقفال أطول من الحد المسموح.");
            DateTime from,to;
            using(SqlCommand cmd=new SqlCommand("SELECT StartDate,EndDate,Status FROM dbo.AccountingPeriodsTb WITH(UPDLOCK,HOLDLOCK) WHERE PeriodId=@Id;",c,t))
            { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId; using(SqlDataReader r=cmd.ExecuteReader()){ if(!r.Read()) throw new InvalidOperationException("الفترة المحاسبية غير موجودة."); if(!string.Equals(r.GetString(2),"Open",StringComparison.Ordinal)) throw new InvalidOperationException("الفترة مقفلة مسبقاً."); from=r.GetDateTime(0);to=r.GetDateTime(1); } }
            DataTable readiness=AccountingService.GetPeriodCloseReadiness(c,t,from,to);
            int observations=0; foreach(DataRow row in readiness.Rows) observations+=Convert.ToInt32(row["عدد الملاحظات"]);
            if(observations!=0) throw new InvalidOperationException("لا يمكن إقفال الفترة قبل معالجة ملاحظات فحص الجاهزية: "+observations+".");
            using(SqlCommand cmd=new SqlCommand("UPDATE dbo.AccountingPeriodsTb SET Status=N'Closed',ClosedAtUtc=SYSUTCDATETIME(),ClosedBy=@By,Notes=COALESCE(NULLIF(@Notes,N''),Notes) WHERE PeriodId=@Id AND Status=N'Open';",c,t))
            { cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId; cmd.Parameters.Add("@By",SqlDbType.NVarChar,200).Value=SessionContext.CurrentUser; cmd.Parameters.Add("@Notes",SqlDbType.NVarChar,500).Value=(notes??string.Empty).Trim(); if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("تعذر إقفال الفترة بسبب تغيير متزامن."); }
        }

        private static void EnsureManager()
        {
            if(!PermissionService.CanExecute(AppOperation.ManageAccountingPeriods)) throw new UnauthorizedAccessException("إدارة الفترات المحاسبية تتطلب صلاحية مستقلة.");
            if(!SessionContext.IsAuthenticated || string.IsNullOrWhiteSpace(SessionContext.CurrentUser)) throw new InvalidOperationException("يجب تسجيل الدخول لإدارة الفترات المحاسبية.");
        }
    }
}
