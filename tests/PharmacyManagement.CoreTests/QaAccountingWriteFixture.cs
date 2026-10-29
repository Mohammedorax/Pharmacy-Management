using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace PharmacyManagement.CoreTests
{
    internal static class QaAccountingWriteFixture
    {
        internal static void ValidateBackupHeader(IDataReader reader,string database,DateTime now)
        {
            if(!reader.Read()||!string.Equals(Convert.ToString(reader["DatabaseName"]),database,StringComparison.OrdinalIgnoreCase)||
                Convert.ToDateTime(reader["BackupFinishDate"])<now.AddHours(-2)||
                Convert.ToDateTime(reader["BackupFinishDate"])>now.AddMinutes(5)||
                !Convert.ToBoolean(reader["HasBackupChecksums"])||Convert.ToInt32(reader["BackupType"])!=1||
                Convert.ToBoolean(reader["IsDamaged"])||reader.Read())
                throw new UnauthorizedAccessException("A single recent undamaged full QA backup with checksums is required.");
        }
        internal static int VerifyBackupHeaderContracts()
        {
            int checks=0; DateTime now=new DateTime(2026,10,2,12,0,0);
            Action<Action<DataTable>,bool> verify=(change,reject)=>
            {
                using(var table=new DataTable())
                {
                    table.Columns.Add("DatabaseName",typeof(string)); table.Columns.Add("BackupFinishDate",typeof(DateTime));
                    table.Columns.Add("HasBackupChecksums",typeof(bool)); table.Columns.Add("BackupType",typeof(int));
                    table.Columns.Add("IsDamaged",typeof(bool));
                    table.Rows.Add("PharmacyManagement_qa",now.AddMinutes(-5),true,1,false);
                    change(table); bool refused=false;
                    using(var reader=table.CreateDataReader())
                        try {ValidateBackupHeader(reader,"PharmacyManagement_qa",now);} catch(UnauthorizedAccessException){refused=true;}
                    if(refused!=reject) throw new Exception("QA backup header acceptance differs from the full standalone backup requirement.");
                    checks++;
                }
            };
            verify(table=>{},false);
            verify(table=>table.Rows[0]["DatabaseName"]="Production",true);
            verify(table=>table.Rows[0]["BackupFinishDate"]=now.AddHours(-3),true);
            verify(table=>table.Rows[0]["HasBackupChecksums"]=false,true);
            verify(table=>table.Rows[0]["BackupType"]=5,true);
            verify(table=>table.Rows[0]["BackupType"]=2,true);
            verify(table=>table.Rows[0]["IsDamaged"]=true,true);
            verify(table=>table.Rows.Add(table.Rows[0].ItemArray),true);
            verify(table=>table.Clear(),true);
            verify(table=>table.Rows[0]["BackupFinishDate"]=now.AddMinutes(10),true);
            Console.WriteLine("PASS: QA backup header full/standalone/checksum/fresh/undamaged contracts; no SQL connection.");
            return checks;
        }
        internal static SqlConnection Open(string server,string database,string backupPath)
        {
            if(!string.Equals(database,"PharmacyManagement_qa",StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Accounting SQL writes only accept PharmacyManagement_qa.");
            if(string.IsNullOrWhiteSpace(backupPath)||!File.Exists(backupPath)||!string.Equals(Path.GetExtension(backupPath),".bak",StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("A verified QA backup file is required before accounting writes.");
            var builder=new SqlConnectionStringBuilder {DataSource=server,InitialCatalog=database,IntegratedSecurity=true,
                Encrypt=false,TrustServerCertificate=true,ConnectTimeout=15,Pooling=false};
            var connection=new SqlConnection(builder.ConnectionString);
            try
            {
                connection.Open();
                using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT DB_NAME()";
                    if(!string.Equals(Convert.ToString(command.ExecuteScalar()),database,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Unexpected QA target.");
                    command.CommandTimeout=120;command.Parameters.Add("@BackupPath",SqlDbType.NVarChar,260).Value=Path.GetFullPath(backupPath);
                    command.CommandText="RESTORE VERIFYONLY FROM DISK=@BackupPath WITH CHECKSUM";command.ExecuteNonQuery();
                    command.CommandText="RESTORE HEADERONLY FROM DISK=@BackupPath";
                    using(var reader=command.ExecuteReader()) ValidateBackupHeader(reader,database,DateTime.Now);
                    command.Parameters.Clear();command.CommandText="SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID() AND spid<>@@SPID AND spid>50";
                    if(Convert.ToInt32(command.ExecuteScalar())!=0)throw new UnauthorizedAccessException("Other QA sessions are present; do not run accounting writes.");
                }
                return connection;
            }
            catch {connection.Dispose();throw;}
        }
    }
}
