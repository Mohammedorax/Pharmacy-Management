using System;

namespace PharmacyManagement.Application
{
    public enum AuthenticationStatus
    {
        Success,
        InvalidInput,
        ManagerSetupRequired,
        InvalidCredentials
    }

    public sealed class AuthenticationRequest
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public sealed class AuthenticationResult
    {
        public AuthenticationStatus Status { get; }
        public string UserName { get; }
        public string Role { get; }
        public string EmployeeId { get; }

        private AuthenticationResult(AuthenticationStatus status, string userName, string role, string employeeId)
        {
            Status = status;
            UserName = userName ?? string.Empty;
            Role = role ?? string.Empty;
            EmployeeId = employeeId ?? string.Empty;
        }

        public static AuthenticationResult Failed(AuthenticationStatus status)
        {
            if (status == AuthenticationStatus.Success) throw new ArgumentException("Use Succeeded for a successful result.", nameof(status));
            return new AuthenticationResult(status, string.Empty, string.Empty, string.Empty);
        }

        public static AuthenticationResult Succeeded(string userName, string role, string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                throw new ArgumentException("A successful login needs the verified employee ID.", nameof(employeeId));
            return new AuthenticationResult(AuthenticationStatus.Success, userName, role, employeeId.Trim());
        }
    }

    public interface IAuthenticationGateway
    {
        void EnsureOperationalReady();
        bool HasManager();
        string AuthenticateEmployeeId(string userName, string password);
        string GetEmployeeRoleById(string employeeId);
        void WriteAudit(string action, string entity, string details, OperationActor actor);
    }

    public interface IAuthenticationService
    {
        AuthenticationResult Authenticate(AuthenticationRequest request);
    }

    public sealed class AuthenticationService : IAuthenticationService
    {
        private readonly IAuthenticationGateway gateway;

        public AuthenticationService(IAuthenticationGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public AuthenticationResult Authenticate(AuthenticationRequest request)
        {
            string userName = (request?.UserName ?? string.Empty).Trim();
            string password = request?.Password ?? string.Empty;
            if (userName.Length == 0 || password.Length == 0)
                return AuthenticationResult.Failed(AuthenticationStatus.InvalidInput);

            gateway.EnsureOperationalReady();
            if (!gateway.HasManager())
                return AuthenticationResult.Failed(AuthenticationStatus.ManagerSetupRequired);
            string employeeId = gateway.AuthenticateEmployeeId(userName, password);
            if (string.IsNullOrWhiteSpace(employeeId))
                return AuthenticationResult.Failed(AuthenticationStatus.InvalidCredentials);

            string role = gateway.GetEmployeeRoleById(employeeId) ?? string.Empty;
            gateway.WriteAudit("Login", "Security", "User signed in.", new OperationActor { UserName = userName, UserRole = role });
            return AuthenticationResult.Succeeded(userName, role, employeeId);
        }
    }
}
