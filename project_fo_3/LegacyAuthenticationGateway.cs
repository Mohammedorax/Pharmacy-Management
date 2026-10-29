using PharmacyManagement.Application;

namespace project_fo_3
{
    // Transitional adapter around the established database facade. The form depends only
    // on IAuthenticationService; the adapter can move to Infrastructure.SqlServer once the
    // remaining startup/schema responsibilities have been extracted from Database.
    internal sealed class LegacyAuthenticationGateway : IAuthenticationGateway
    {
        private readonly string connectionString;
        internal LegacyAuthenticationGateway() : this(Database.ConnectionString) { }
        internal LegacyAuthenticationGateway(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new System.ArgumentException("Authentication connection is required.", nameof(connectionString));
            this.connectionString = connectionString;
        }
        public void EnsureOperationalReady() => Database.EnsureOperationalReady(connectionString);
        public bool HasManager() => Database.HasManager(connectionString);
        public string AuthenticateEmployeeId(string userName, string password) =>
            Database.AuthenticateEmployeeId(userName, password, connectionString);
        public string GetEmployeeRoleById(string employeeId) => Database.GetEmployeeRoleById(employeeId, connectionString);
        public void WriteAudit(string action, string entity, string details, OperationActor actor) =>
            Database.WriteAudit(action, entity, details, actor.UserName, actor.UserRole, connectionString);
    }
}
