using System;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class AuthenticationTests
    {
        internal static int Run()
        {
            int checks = 0;
            var gateway = new FakeGateway();
            var service = new AuthenticationService(gateway);

            Check(service.Authenticate(new AuthenticationRequest()).Status == AuthenticationStatus.InvalidInput,
                "Empty credentials must be rejected before database access.", ref checks);
            Check(gateway.CallCount == 0, "Invalid input touched the gateway.", ref checks);

            gateway.HasManagerValue = false;
            Check(service.Authenticate(Request()).Status == AuthenticationStatus.ManagerSetupRequired,
                "Missing manager state changed.", ref checks);

            gateway.HasManagerValue = true;
            gateway.CredentialsValid = false;
            Check(service.Authenticate(Request()).Status == AuthenticationStatus.InvalidCredentials,
                "Invalid credentials state changed.", ref checks);

            Check(gateway.AuditCount == 0, "Rejected authentication must not create a successful Login audit.", ref checks);
            gateway.CredentialsValid = true;
            gateway.Role = "Manager";
            AuthenticationResult result = service.Authenticate(new AuthenticationRequest
                { UserName = "  admin  ", Password = "123" });
            Check(result.Status == AuthenticationStatus.Success && result.UserName == "admin" &&
                result.Role == "Manager" && result.EmployeeId == "EMP-2" &&
                gateway.LastRoleLookupId == "EMP-2",
                "Successful authentication did not preserve the verified employee ID and its role.", ref checks);
            Check(gateway.LastAuditAction == "Login" && gateway.LastAuditEntity == "Security",
                "Successful authentication was not audited.", ref checks);
            Check(gateway.LastAuditUserName == result.UserName && gateway.LastAuditRole == result.Role,
                "Login audit must receive the verified username and role explicitly.", ref checks);
            gateway.Role = "Cashier";
            AuthenticationResult next = service.Authenticate(new AuthenticationRequest { UserName = "  next-user  ", Password = "123" });
            Check(gateway.LastAuditUserName == "next-user" && gateway.LastAuditRole == "Cashier" &&
                next.UserName == "next-user" && gateway.AuditCount == 2,
                "A later login must audit its own actor, not the prior authenticated actor.", ref checks);
            gateway.EmployeeId = string.Empty;
            Check(service.Authenticate(Request()).Status == AuthenticationStatus.InvalidCredentials,
                "A missing verified employee ID was accepted.", ref checks);
            Check(gateway.AuditCount == 2, "Rejected employee identity created a successful Login audit.", ref checks);
            return checks;
        }

        private static AuthenticationRequest Request() =>
            new AuthenticationRequest { UserName = "admin", Password = "123" };

        private static void Check(bool value, string message, ref int checks)
        {
            checks++;
            if (!value) throw new InvalidOperationException(message);
        }

        private sealed class FakeGateway : IAuthenticationGateway
        {
            internal int CallCount;
            internal bool HasManagerValue = true;
            internal bool CredentialsValid = true;
            internal string Role = "User";
            internal string EmployeeId = "EMP-2";
            internal string LastRoleLookupId;
            internal string LastAuditAction;
            internal string LastAuditEntity;
            internal string LastAuditUserName;
            internal string LastAuditRole;
            internal int AuditCount;

            public void EnsureOperationalReady() { CallCount++; }
            public bool HasManager() { CallCount++; return HasManagerValue; }
            public string AuthenticateEmployeeId(string userName, string password)
            { CallCount++; return CredentialsValid ? EmployeeId : string.Empty; }
            public string GetEmployeeRoleById(string employeeId)
            { CallCount++; LastRoleLookupId = employeeId; return Role; }
            public void WriteAudit(string action, string entity, string details, OperationActor actor)
            {
                CallCount++;
                AuditCount++;
                LastAuditUserName = actor.UserName;
                LastAuditRole = actor.UserRole;
                LastAuditAction = action;
                LastAuditEntity = entity;
            }
        }
    }
}
