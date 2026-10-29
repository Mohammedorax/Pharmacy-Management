using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class PurchaseInventoryModeProvider : IPurchaseInventoryModeProvider
    {
        public bool IsCommercialInventoryEnabled()
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                try
                {
                    connection.Open();
                    return CommercialSchemaInstaller.IsFullCommercialMode(connection, null);
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (connection.State == ConnectionState.Open) connection.Close();
                }
            }
        }
    }
}
