using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    // Desktop composition root. Constructing services never opens a database connection.
    internal static partial class ApplicationServices
    {
        // Capture profile identity without exposing connection settings to the presentation layer.
        internal static System.Func<bool> CaptureRuntimeConnectionContext()
        {
            string connectionString = Database.ConnectionString;
            return () => string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
        }

        internal static FrequentMedicineDesktopSession CreateFrequentMedicineSession()
        { return FrequentMedicineSessionFactory.Capture(() => Database.ConnectionString); }

        internal static MedicineQuickSearchSession CreateMedicineQuickSearchSession()
        { return CreateMedicineQuickSearchSession(() => Database.ConnectionString); }

        internal static MedicineQuickSearchSession CreateMedicineQuickSearchSession(System.Func<string> connectionStringProvider)
        {
            if (connectionStringProvider == null) throw new System.ArgumentNullException(nameof(connectionStringProvider));
            string connectionString = connectionStringProvider();
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser, role = SessionContext.CurrentRole, employee = SessionContext.CurrentEmployeeId;
            System.Func<bool> current = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(user, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, connectionStringProvider(), System.StringComparison.Ordinal) &&
                (PermissionService.CanOpen(AppPermission.Sales) || PermissionService.CanOpen(AppPermission.Purchases));
            var actor = new OperationActor { UserName = user, UserRole = role };
            var service = new MedicineSearchService(new SqlMedicineSearchRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)),
                canSearch: candidate => current() && string.Equals(candidate.UserName, user, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.UserRole, role, System.StringComparison.OrdinalIgnoreCase));
            return new MedicineQuickSearchSession(service, actor, current);
        }

        internal static VoucherPostSession CreateVoucherPostSession()
        { return CreateVoucherPostSessionForEditor(null); }
        internal static VoucherPostSession CreateVoucherPostSessionForEditor(Func<bool> editorCurrent)
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            Func<bool> current=()=>(editorCurrent==null||editorCurrent())&&SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&string.Equals(connectionString,Database.ConnectionString,StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.PostCashVoucher);
            var service=new VoucherPostService(new SqlVoucherPostRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString),current),actor=>actor!=null&&current()&&
                string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new VoucherPostSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static JournalPostSession CreateJournalPostSession()
        {return CreateJournalPostSessionForEditor(null);}
        internal static JournalPostSession CreateJournalPostSessionForEditor(Func<bool> editorCurrent)
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,System.StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.PostAccountingEntry)&&
                (editorCurrent==null||editorCurrent());
            var service=new JournalPostService(new SqlJournalPostRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString),current),actor=>current()&&actor!=null&&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase));
            return new JournalPostSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static VoucherSubmissionStatusSession CreateVoucherSubmissionStatusSession()
        {return CreateVoucherSubmissionStatusSession(()=>Database.ConnectionString);}
        internal static VoucherSubmissionStatusSession CreateVoucherSubmissionStatusSession(Func<string> connectionStringProvider)
        {
            if(connectionStringProvider==null)throw new ArgumentNullException(nameof(connectionStringProvider));
            string connectionString=connectionStringProvider();long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&string.Equals(connectionString,connectionStringProvider(),StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.PostCashVoucher);
            var service=new VoucherSubmissionStatusService(new SqlVoucherSubmissionStatusRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>current()&&actor!=null&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new VoucherSubmissionStatusSession(service,new OperationActor{UserName=user,UserRole=role},current,
                InventoryPendingStore.CreateContextKey(connectionString,user,role,employee));
        }
        internal static JournalSubmissionStatusSession CreateJournalSubmissionStatusSession()
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.PostAccountingEntry);
            var service=new JournalSubmissionStatusService(new SqlJournalSubmissionStatusRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>current()&&actor!=null&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new JournalSubmissionStatusSession(service,new OperationActor{UserName=user,UserRole=role},current,
                InventoryPendingStore.CreateContextKey(connectionString,user,role,employee));
        }
        internal static AccountWriteSession CreateAccountWriteSession()
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,System.StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.ManageChartOfAccounts);
            var service=new AccountWriteService(new SqlAccountWriteRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),actor=>current()&&actor!=null&&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase));
            return new AccountWriteSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static JournalDocumentReadSession CreateJournalDocumentReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&PermissionService.CanOpen(AppPermission.Accounting)&&
                (PermissionService.CanExecute(AppOperation.PostAccountingEntry)||PermissionService.CanExecute(AppOperation.ViewAccountingReports));
            var service=new JournalDocumentReadService(new SqlJournalDocumentReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new JournalDocumentReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static JournalEditorReadSession CreateJournalEditorReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&PermissionService.CanOpen(AppPermission.Accounting);
            var service=new JournalEditorLookupReadService(new SqlJournalEditorLookupReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new JournalEditorReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static VoucherDocumentReadSession CreateVoucherDocumentReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&
                PermissionService.CanOpen(AppPermission.Accounting)&&(PermissionService.CanExecute(AppOperation.PostCashVoucher)||PermissionService.CanExecute(AppOperation.ViewAccountingReports));
            var service=new VoucherDocumentReadService(new SqlVoucherDocumentReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new VoucherDocumentReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static AccountingReportsReadSession CreateAccountingReportsReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.ViewAccountingReports);
            Func<OperationActor,bool> allowed=actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase);
            return new AccountingReportsReadSession(new AccountingReportsReadService(new SqlAccountingReportsReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString),AccountingService.InventoryAccount,AccountingService.InputTaxAccount,AccountingService.OutputTaxAccount),allowed),
                new AccountingLookupReadService(new SqlAccountingLookupReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),allowed),
                new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static DailyOperationsReadService CreateDailyOperationsReadService(string connection,OperationActor actor,
            Func<OperationActor,string,bool> canRead,Func<bool> isCurrent)
        {
            return new DailyOperationsReadService(new SqlDailyOperationsReadRepository(connection),actor,canRead,isCurrent);
        }
        internal static GeneralLedgerReadSession CreateGeneralLedgerReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.ViewAccountingReports);
            Func<OperationActor,bool> allowed=actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase);
            var ledgerRepository=new SqlGeneralLedgerReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString));
            return new GeneralLedgerReadSession(new GeneralLedgerReadService(ledgerRepository,allowed),
                new AccountingLookupReadService(new SqlAccountingLookupReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),allowed),
                new OperationActor{UserName=user,UserRole=role},current,
                (request,cancellation)=>ledgerRepository.LoadTableAsync(request,cancellation));
        }
        internal static AccountBalanceReadSession CreateAccountBalanceReadSession()
        {
            string connectionString=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&connectionString==Database.ConnectionString&&
                PermissionService.CanOpen(AppPermission.Accounting)&&(PermissionService.CanExecute(AppOperation.PostCashVoucher)||PermissionService.CanExecute(AppOperation.ViewAccountingReports));
            var service=new AccountBalanceReadService(new SqlAccountBalanceReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new AccountBalanceReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static VoucherHistoryReadSession CreateVoucherHistoryReadSession()
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&
                (PermissionService.CanExecute(AppOperation.PostCashVoucher)||PermissionService.CanExecute(AppOperation.ViewAccountingReports));
            var service=new VoucherHistoryReadService(new SqlVoucherHistoryReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new VoucherHistoryReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static AccountingReadSession CreateAccountingReadSession()
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,System.StringComparison.Ordinal);
            System.Func<OperationActor,bool> matches=actor=>current()&&actor!=null&&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase);
            System.Func<bool> lookupsAllowed=()=>current()&&PermissionService.CanOpen(AppPermission.Accounting);
            System.Func<bool> historyAllowed=()=>lookupsAllowed()&&(PermissionService.CanExecute(AppOperation.PostAccountingEntry)||PermissionService.CanExecute(AppOperation.ViewAccountingReports));
            var lookups=new AccountingLookupReadService(new SqlAccountingLookupReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),actor=>matches(actor)&&lookupsAllowed());
            var history=new JournalHistoryReadService(new SqlJournalHistoryReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),actor=>matches(actor)&&historyAllowed());
            return new AccountingReadSession(lookups,history,new OperationActor{UserName=user,UserRole=role},lookupsAllowed,historyAllowed);
        }
        internal static MedicineSession CreateMedicineSession(System.Func<string> barcodeCandidateFactory = null)
        {
            string connectionString = Database.ConnectionString;
            long revision = SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser, role=SessionContext.CurrentRole, employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision==revision &&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString,Database.ConnectionString,System.StringComparison.Ordinal);
            System.Func<OperationActor,bool> matches=actor=>current() && actor!=null &&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase);
            var repository=new SqlMedicineRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString));
            System.Func<OperationActor,bool> canManage=actor=>matches(actor)&&PermissionService.CanOpen(AppPermission.ManageMedicines);
            System.Func<OperationActor,bool> canRead=actor=>matches(actor)&&(PermissionService.CanOpen(AppPermission.ManageMedicines)||PermissionService.CanOpen(AppPermission.Sales));
            var service=barcodeCandidateFactory==null?new MedicineService(repository,canManage,canRead:canRead):
                new MedicineService(repository,canManage,barcodeCandidateFactory,canRead);
            return new MedicineSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static ManufacturerReadSession CreateManufacturerSession()
        {
            string connectionString=Database.ConnectionString;
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,Database.ConnectionString,System.StringComparison.Ordinal)&&
                PermissionService.CanOpen(AppPermission.ManageManufacturers);
            System.Func<OperationActor,bool> authorized=actor=>current()&&actor!=null&&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase);
            var service=new ManufacturerService(new SqlManufacturerRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized);
            return new ManufacturerReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static ManufacturerReadSession CreateManufacturerReadSession()
        {
            string connectionString = Database.ConnectionString;
            long revision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser, role = SessionContext.CurrentRole, employee = SessionContext.CurrentEmployeeId;
            System.Func<bool> current = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == revision &&
                string.Equals(user, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal) &&
                PermissionService.CanOpen(AppPermission.ManageManufacturers);
            var service = new ManufacturerService(new SqlManufacturerRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)),
                actor => false, actor => current() && string.Equals(actor.UserName, user, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, role, System.StringComparison.OrdinalIgnoreCase));
            return new ManufacturerReadSession(service, new OperationActor { UserName=user, UserRole=role }, current);
        }
        internal static CustomerMasterWriteSession CreateCustomerMasterWriteSession(System.Func<string> connectionStringProvider = null)
        {
            connectionStringProvider=connectionStringProvider??(()=>Database.ConnectionString);
            string connectionString=connectionStringProvider();
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,connectionStringProvider(),System.StringComparison.Ordinal)&&
                (PermissionService.CanOpen(AppPermission.Sales)||PermissionService.CanOpen(AppPermission.ManageManufacturers))&&
                PermissionService.CanExecute(AppOperation.ManageSalesCustomers);
            System.Func<OperationActor,bool> authorized=actor=>current()&&actor!=null&&
                string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase);
            var service=new CustomerMasterWriteService(
                new SqlCustomerMasterWriteRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),authorized,authorized);
            string identity;
            using(var hash=System.Security.Cryptography.SHA256.Create())
                identity=System.BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(connectionString))).Replace("-","");
            return new CustomerMasterWriteSession(service,new OperationActor{UserName=user,UserRole=role},identity,current);
        }
        internal static SalesCustomerReferenceReadSession CreateSalesCustomerReferenceReadSession(System.Func<string> connectionStringProvider = null)
        {
            connectionStringProvider=connectionStringProvider??(()=>Database.ConnectionString);
            string connectionString=connectionStringProvider();
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            System.Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,System.StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,connectionStringProvider(),System.StringComparison.Ordinal)&&PermissionService.CanOpen(AppPermission.Sales);
            var service=new CustomerReferenceReadService(new SqlCustomerReferenceReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>current()&&actor!=null&&string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase));
            var credit=new SalesCustomerCreditReadService(new SqlSalesCustomerCreditReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                actor=>current()&&actor!=null&&string.Equals(actor.UserName,user,System.StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(actor.UserRole,role,System.StringComparison.OrdinalIgnoreCase));
            return new SalesCustomerReferenceReadSession(service,credit,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static SalesCustomerReadSession CreateSalesCustomerReadSession()
        {
            string connectionString = Database.ConnectionString;
            long revision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser, role = SessionContext.CurrentRole, employee = SessionContext.CurrentEmployeeId;
            System.Func<bool> current = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == revision &&
                string.Equals(user, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal) &&
                (PermissionService.CanOpen(AppPermission.Sales) || PermissionService.CanOpen(AppPermission.ManageManufacturers));
            var service = new SalesCustomerService(new SqlSalesCustomerRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)),
                actor => false, actor => current() && string.Equals(actor.UserName, user, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, role, System.StringComparison.OrdinalIgnoreCase));
            return new SalesCustomerReadSession(service, new OperationActor { UserName=user, UserRole=role }, current);
        }
        internal static InventoryStockReadSession CreateInventoryStockReadSession()
        {
            string connectionString = Database.ConnectionString;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string userName = SessionContext.CurrentUser, userRole = SessionContext.CurrentRole, employeeId = SessionContext.CurrentEmployeeId;
            System.Func<bool> contextMatches = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(userName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(userRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
            var service = new InventoryStockReadService(
                new SqlInventoryStockReadRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                    contextMatches() && string.Equals(actor.UserName, userName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, userRole, System.StringComparison.OrdinalIgnoreCase) && PermissionService.CanOpen(AppPermission.Inventory));
            return new InventoryStockReadSession(service, new OperationActor { UserName = userName, UserRole = userRole }, contextMatches);
        }
        internal static HomeOperationalAlertReadSession CreateHomeOperationalAlertReadSession()
        { return new HomeOperationalAlertReadSession(CreateInventoryAlertReadSession(() => PermissionService.CanOpen(AppPermission.SystemMonitor))); }

        internal static InventoryAlertReadSession CreateInventoryAlertReadSession()
        { return CreateInventoryAlertReadSession(() => PermissionService.CanOpen(AppPermission.Inventory)); }

        internal static InventoryAlertReadSession CreateInvoiceInventoryAlertReadSession()
        {
            return CreateInventoryAlertReadSession(() =>
                PermissionService.CanExecute(AppOperation.PostSalesInvoice) ||
                PermissionService.CanExecute(AppOperation.PostPurchaseInvoice));
        }

        private static InventoryAlertReadSession CreateInventoryAlertReadSession(System.Func<bool> hasPermission)
        {
            string connectionString = Database.ConnectionString;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string userName = SessionContext.CurrentUser, userRole = SessionContext.CurrentRole, employeeId = SessionContext.CurrentEmployeeId;
            System.Func<bool> contextMatches = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(userName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(userRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
            var service = new InventoryAlertReadService(
                new SqlInventoryAlertRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                    contextMatches() &&
                    string.Equals(actor.UserName, userName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, userRole, System.StringComparison.OrdinalIgnoreCase) &&
                    hasPermission());
            return new InventoryAlertReadSession(service, new OperationActor { UserName = userName, UserRole = userRole }, contextMatches);
        }
        internal static InventoryOperationalReportReadSession CreateInventoryOperationalReportReadSession()
        {
            string connectionString = Database.ConnectionString;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string userName = SessionContext.CurrentUser, userRole = SessionContext.CurrentRole, employeeId = SessionContext.CurrentEmployeeId;
            System.Func<bool> contextMatches = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(userName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(userRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
            var service = new InventoryOperationalReportReadService(
                new SqlInventoryOperationalReportRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                    contextMatches() &&
                    string.Equals(actor.UserName, userName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, userRole, System.StringComparison.OrdinalIgnoreCase) &&
                    PermissionService.CanOpen(AppPermission.Inventory));
            return new InventoryOperationalReportReadSession(service,
                new OperationActor { UserName = userName, UserRole = userRole }, contextMatches);
        }
        internal static InventoryIntegrityReadSession CreateInventoryIntegrityReadSession()
        {
            string connectionString = Database.ConnectionString;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string userName = SessionContext.CurrentUser, userRole = SessionContext.CurrentRole, employeeId = SessionContext.CurrentEmployeeId;
            System.Func<bool> contextMatches = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(userName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(userRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
            var service = new InventoryIntegrityReadService(
                new SqlInventoryIntegrityReadRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                    contextMatches() &&
                    string.Equals(actor.UserName, userName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, userRole, System.StringComparison.OrdinalIgnoreCase) &&
                    PermissionService.CanOpen(AppPermission.Inventory));
            return new InventoryIntegrityReadSession(service,
                new OperationActor { UserName = userName, UserRole = userRole }, contextMatches);
        }
        internal static InventoryPostingSession CreateInventoryPostingSession()
        {
            string connectionString = Database.ConnectionString;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string userName = SessionContext.CurrentUser, userRole = SessionContext.CurrentRole, employeeId = SessionContext.CurrentEmployeeId;
            System.Func<bool> contextMatches = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(userName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(userRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employeeId, SessionContext.CurrentEmployeeId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connectionString, Database.ConnectionString, System.StringComparison.Ordinal);
            var service = new InventoryPostService(
                new SqlInventoryPostRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                    contextMatches() &&
                    string.Equals(actor.UserName, userName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, userRole, System.StringComparison.OrdinalIgnoreCase) &&
                    PermissionService.CanExecute(AppOperation.PostInventoryOperation));
            var reads=new InventoryOperationReadService(new SqlInventoryOperationReadRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), actor =>
                contextMatches() &&
                string.Equals(actor.UserName,userName,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole,userRole,System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Inventory));
            return new InventoryPostingSession(service,
                new OperationActor { UserName = userName, UserRole = userRole }, contextMatches, InventoryPendingStore.CreateContextKey(connectionString,userName,userRole,employeeId), reads);
        }
        internal static readonly IInventoryPostService InventoryPosting =
            new InventoryPostService(new SqlInventoryPostRepository(Database.CreateConnection),actor=>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanExecute(AppOperation.PostInventoryOperation));
        internal static readonly IAccountWriteService AccountWriting =
            new AccountWriteService(new SqlAccountWriteRepository(Database.CreateConnection),actor=>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName,SessionContext.CurrentUser,System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole,SessionContext.CurrentRole,System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Accounting) && PermissionService.CanExecute(AppOperation.ManageChartOfAccounts));
        internal static readonly IJournalPostService JournalPosting =
            new JournalPostService(new SqlJournalPostRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Accounting) && PermissionService.CanExecute(AppOperation.PostAccountingEntry));
        internal static readonly IAccountingLookupReadService AccountingLookups =
            new AccountingLookupReadService(new SqlAccountingLookupReadRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Accounting));
        internal static readonly IJournalHistoryReadService JournalHistory =
            new JournalHistoryReadService(new SqlJournalHistoryReadRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Accounting) &&
                (PermissionService.CanExecute(AppOperation.PostAccountingEntry) || PermissionService.CanExecute(AppOperation.ViewAccountingReports)));
        internal static CommercialReturnReadSession CreateCommercialReturnReadSession()
        {
            string connection=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&revision==SessionContext.CurrentSessionRevision&&connection==Database.ConnectionString&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                PermissionService.CanOpen(AppPermission.Sales)&&PermissionService.CanExecute(AppOperation.IssueCommercialSalesReturn);
            var service=new CommercialReturnReadService(new SqlCommercialReturnReadRepository(()=>new System.Data.SqlClient.SqlConnection(connection)),actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new CommercialReturnReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static OperationalReportReadSession CreateOperationalReportReadSession()
        {
            string connection=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&revision==SessionContext.CurrentSessionRevision&&connection==Database.ConnectionString&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                PermissionService.CanOpen(AppPermission.Accounting)&&PermissionService.CanExecute(AppOperation.ViewAccountingReports);
            var service=new AuthorizedOperationalReportReadService(new SqlOperationalReportRepository(()=>new System.Data.SqlClient.SqlConnection(connection)),actor=>actor!=null&&current()&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new OperationalReportReadSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static IOperationalReportReadService OperationalReports
        {
            get
            {
                string connection = Database.ConnectionString, user = SessionContext.CurrentUser,
                    role = SessionContext.CurrentRole, employee = SessionContext.CurrentEmployeeId;
                long revision = SessionContext.CurrentSessionRevision;
                Func<bool> current = () => SessionContext.IsAuthenticated && revision == SessionContext.CurrentSessionRevision &&
                    connection == Database.ConnectionString && string.Equals(user, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(role, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(employee, SessionContext.CurrentEmployeeId, StringComparison.OrdinalIgnoreCase) &&
                    PermissionService.CanOpen(AppPermission.Accounting) && PermissionService.CanExecute(AppOperation.ViewAccountingReports);
                return new OperationalReportReadService(new SqlOperationalReportRepository(() =>
                    new System.Data.SqlClient.SqlConnection(connection)), current);
            }
        }

        internal static readonly IInventoryAlertReadService InventoryAlerts =
            new InventoryAlertReadService(new SqlInventoryAlertRepository(Database.CreateConnection));
        internal static readonly IInventoryIntegrityReadService InventoryIntegrity =
            new InventoryIntegrityReadService(new SqlInventoryIntegrityReadRepository(Database.CreateConnection));
        internal static IAccountingIntegrityReadService AccountingIntegrity => CreateAccountingIntegrityReadSession().Service;
        internal static AccountingIntegrityReadSession CreateAccountingIntegrityReadSession()
        {
            string connection = Database.ConnectionString, user = SessionContext.CurrentUser,
                role = SessionContext.CurrentRole, employee = SessionContext.CurrentEmployeeId;
            long revision = SessionContext.CurrentSessionRevision;
            Func<bool> current = () => SessionContext.IsAuthenticated && revision == SessionContext.CurrentSessionRevision &&
                connection == Database.ConnectionString && string.Equals(user, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee, SessionContext.CurrentEmployeeId, StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Accounting) && PermissionService.CanExecute(AppOperation.ViewAccountingReports);
            return new AccountingIntegrityReadSession(new AccountingIntegrityReadService(new SqlAccountingIntegrityReadRepository(() =>
                new System.Data.SqlClient.SqlConnection(connection)), current), current);
        }

        internal static ISystemMonitorLogReadService SystemMonitorLogs => CreateSystemMonitorLogSession();
        private static System.Func<bool> CaptureSystemMonitorContext(out string connection, bool requirePermission = true)
        {
            connection = Database.ConnectionString;
            string capturedConnection = connection;
            long revision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser, role = SessionContext.CurrentRole,
                employee = SessionContext.CurrentEmployeeId;
            return () => SessionContext.IsAuthenticated && revision == SessionContext.CurrentSessionRevision &&
                string.Equals(user, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(role, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(employee, SessionContext.CurrentEmployeeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(capturedConnection, Database.ConnectionString, StringComparison.Ordinal) &&
                (!requirePermission || PermissionService.CanOpen(AppPermission.SystemMonitor));
        }
        private static IAsyncSystemMonitorLogReadService CreateSystemMonitorLogSession()
        {
            string connection;
            var current = CaptureSystemMonitorContext(out connection);
            return new SystemMonitorLogReadService(
                new SqlSystemMonitorLogRepository(() => new System.Data.SqlClient.SqlConnection(connection)), current);
        }
        internal static ISystemMonitorReadSession CreateSystemMonitorReadSession()
        {
            string connection;
            var current = CaptureSystemMonitorContext(out connection, false);
            var access = new SystemMonitorAccessReadService(
                new SqlEmployeePermissionRepository(() => new System.Data.SqlClient.SqlConnection(connection)),
                current, PermissionService.IsManagerRole, SessionContext.CurrentUser,
                SessionContext.CurrentRole, SessionContext.CurrentEmployeeId);
            var logs = new SystemMonitorLogReadService(
                new SqlSystemMonitorLogRepository(() => new System.Data.SqlClient.SqlConnection(connection)),
                () => current() && access.HasVerifiedAccess);
            var alerts = new HomeOperationalAlertReadSession(
                CreateInventoryAlertReadSession(() => access.HasVerifiedAccess));
            var session = new SystemMonitorReadSession(alerts, logs, current, access);
            session.EnsureContext();
            return session;
        }
        internal static IOperationalReportReadService CreateQaOperationalReportService(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.Trim().EndsWith("_qa", System.StringComparison.OrdinalIgnoreCase))
                throw new System.UnauthorizedAccessException("فحص مصادر التقارير المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            return new OperationalReportReadService(new SqlOperationalReportRepository(() =>
                new System.Data.SqlClient.SqlConnection(Database.BuildSettingsConnectionString(server, database, true))), () => true);
        }

        internal static readonly ICurrencyWriteService CurrencyWrites =
            new CurrencyWriteService(new SqlCurrencyWriteRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Settings));

        internal static readonly ICurrencyReadService CurrencyRead =
            new CurrencyReadService(new SqlCurrencyReadRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated && actor != null &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase));

        internal static IMedicineSearchService MedicineSearch => CreateMedicineBarcodeSession().Service;

        internal static MedicineQuickSearchSession CreateMedicineBarcodeSession()
        { return CreateMedicineBarcodeSession(() => Database.ConnectionString); }

        internal static MedicineQuickSearchSession CreateMedicineBarcodeSession(Func<string> connectionStringProvider, AppPermission screen = AppPermission.Sales)
        {
            if(connectionStringProvider==null)throw new ArgumentNullException(nameof(connectionStringProvider));
            string connectionString=connectionStringProvider();
            long revision=SessionContext.CurrentSessionRevision;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connectionString,connectionStringProvider(),StringComparison.Ordinal)&&
                (screen==AppPermission.Sales||screen==AppPermission.Purchases)&&PermissionService.CanOpen(screen);
            var service=new MedicineSearchService(new SqlMedicineSearchRepository(()=>new System.Data.SqlClient.SqlConnection(connectionString)),
                canReadBarcode:actor=>current()&&actor!=null&&string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase));
            return new MedicineQuickSearchSession(service,new OperationActor{UserName=user,UserRole=role},current);
        }
        internal static readonly Func<string, IAuthenticationService> AuthenticationFactory =
            connectionString => new AuthenticationService(new LegacyAuthenticationGateway(connectionString));

        internal static IAuthenticationService CreateAuthenticationSession(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Authentication connection is required.", nameof(connectionString));
            return AuthenticationFactory(connectionString);
        }

        private sealed class CurrentConnectionAuthenticationService : IAuthenticationService
        {
            public AuthenticationResult Authenticate(AuthenticationRequest request)
            {
                if (string.IsNullOrWhiteSpace(request?.UserName) || string.IsNullOrEmpty(request?.Password))
                    return AuthenticationResult.Failed(AuthenticationStatus.InvalidInput);
                return CreateAuthenticationSession(Database.ConnectionString).Authenticate(request);
            }
        }

        internal static readonly IAuthenticationService Authentication = new CurrentConnectionAuthenticationService();

        internal static IManufacturerService Manufacturers => CreateManufacturerSession().Service;

        internal static IMedicineService Medicines => CreateMedicineSession().Service;

        internal static readonly IMedicineCatalogImportService MedicineCatalogImport =
            new MedicineCatalogImportService(new SqlMedicineRepository(Database.CreateConnection));

        internal static readonly IActiveIngredientService ActiveIngredients =
            new ActiveIngredientApplicationService(new SqlActiveIngredientRepository(Database.CreateConnection));

        internal static readonly IEmployeeService Employees =
            new EmployeeService(new SqlEmployeeRepository(Database.CreateConnection), new PasswordProtectorAdapter(), CanManageEmployees);

        internal static readonly ICashBoxService CashBoxes =
            new CashBoxService(new SqlCashBoxRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Settings));

        private static bool CanManageEmployees(OperationActor actor)
        {
            return SessionContext.IsAuthenticated && actor != null &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.ManageEmployees);
        }

        internal static readonly IEmployeePermissionService EmployeePermissions =
            new EmployeePermissionService(new SqlEmployeePermissionRepository(Database.CreateConnection), CanManageEmployees);

        internal static readonly ISalesCustomerService SalesCustomers =
            new SalesCustomerService(new SqlSalesCustomerRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanExecute(AppOperation.ManageSalesCustomers), actor =>
                SessionContext.IsAuthenticated && actor != null &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                (PermissionService.CanOpen(AppPermission.Sales) || PermissionService.CanOpen(AppPermission.ManageManufacturers)));

        internal static readonly ISalesHistoryService SalesHistory =
            new SalesHistoryService(new SqlSalesHistoryRepository(Database.CreateConnection));

        internal static readonly IDashboardReadService DashboardRead =
            new DashboardReadService(new SqlDashboardReadRepository(Database.CreateConnection));

        internal static readonly IPurchaseWorkspaceService PurchaseWorkspace =
            new PurchaseWorkspaceService(new SqlPurchaseWorkspaceRepository(Database.CreateConnection));

        internal static readonly IInvoiceImportDraftService InvoiceImportDrafts =
            new InvoiceImportDraftService(new SqlInvoiceImportDraftRepository(Database.CreateConnection), actor =>
                SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                PermissionService.CanOpen(AppPermission.Settings));
        internal static readonly IPurchaseInvoiceOperation PurchaseInvoices = new PurchaseInvoiceOperation();
        internal static readonly IPurchaseInvoiceCancellationOperation PurchaseInvoiceCancellations =
            new PurchaseInvoiceCancellationOperation();

        internal static readonly IPurchaseInventoryModeProvider PurchaseInventoryMode = new PurchaseInventoryModeProvider();

        internal static readonly ISalesInvoiceOperation SalesInvoices = new SalesInvoiceOperation();

        internal static readonly IInvoiceImportApprovalService InvoiceImportApprovals =
            new InvoiceImportApprovalService(SalesInvoices, PurchaseInvoices, PurchaseInventoryMode,
                InvoiceImportDrafts, actor => SessionContext.IsAuthenticated &&
                    string.Equals(actor.UserName, SessionContext.CurrentUser, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actor.UserRole, SessionContext.CurrentRole, System.StringComparison.OrdinalIgnoreCase) &&
                    PermissionService.CanOpen(AppPermission.Settings), new SqlImportedInvoiceLookup());

        internal static readonly ISalesInvoiceCancellationOperation SalesInvoiceCancellations =
            new SalesInvoiceCancellationOperation();
    }
    internal sealed class VoucherPostSession
    {
        internal readonly IVoucherPostService Service;internal readonly OperationActor Actor;private readonly Func<bool> current;
        internal VoucherPostSession(IVoucherPostService service,OperationActor actor,Func<bool> current){Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class VoucherDocumentReadSession
    {
        internal readonly IVoucherDocumentReadService Service;internal readonly OperationActor Actor;private readonly Func<bool> current;
        internal VoucherDocumentReadSession(IVoucherDocumentReadService service,OperationActor actor,Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class AccountBalanceReadSession
    {
        internal readonly IAccountBalanceReadService Service; internal readonly OperationActor Actor; private readonly Func<bool> current;
        internal AccountBalanceReadSession(IAccountBalanceReadService service,OperationActor actor,Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class VoucherHistoryReadSession
    {
        internal readonly IVoucherHistoryReadService Service;internal readonly OperationActor Actor;private readonly Func<bool> current;
        internal VoucherHistoryReadSession(IVoucherHistoryReadService service,OperationActor actor,Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class JournalPostSession
    {
        internal readonly IJournalPostService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> current;
        internal JournalPostSession(IJournalPostService service,OperationActor actor,System.Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class VoucherSubmissionStatusSession
    {
        internal readonly VoucherSubmissionStatusService Service;
        internal readonly OperationActor Actor;
        internal readonly string ContextKey;
        private readonly Func<bool> current;
        internal VoucherSubmissionStatusSession(VoucherSubmissionStatusService service,OperationActor actor,Func<bool> current,string contextKey)
        {Service=service;Actor=actor;this.current=current;ContextKey=contextKey;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class JournalSubmissionStatusSession
    {
        internal readonly JournalSubmissionStatusService Service;
        internal readonly OperationActor Actor;
        internal readonly string ContextKey;
        private readonly Func<bool> current;
        internal JournalSubmissionStatusSession(JournalSubmissionStatusService service,OperationActor actor,Func<bool> current,string contextKey)
        {Service=service;Actor=actor;this.current=current;ContextKey=contextKey;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class AccountWriteSession
    {
        internal readonly IAccountWriteService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> current;
        internal AccountWriteSession(IAccountWriteService service,OperationActor actor,System.Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class JournalDocumentReadSession
    {
        internal readonly IJournalDocumentReadService Service;internal readonly OperationActor Actor;private readonly Func<bool> current;
        internal JournalDocumentReadSession(IJournalDocumentReadService service,OperationActor actor,Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class JournalEditorReadSession
    {
        internal readonly IJournalEditorLookupReadService Service;internal readonly OperationActor Actor;private readonly Func<bool> current;
        internal JournalEditorReadSession(IJournalEditorLookupReadService service,OperationActor actor,Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class AccountingReadSession
    {
        internal readonly IAccountingLookupReadService Lookups;
        internal readonly IJournalHistoryReadService History;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> lookupsCurrent,historyCurrent;
        internal AccountingReadSession(IAccountingLookupReadService lookups,IJournalHistoryReadService history,OperationActor actor,System.Func<bool> lookupsCurrent,System.Func<bool> historyCurrent)
        {Lookups=lookups;History=history;Actor=actor;this.lookupsCurrent=lookupsCurrent;this.historyCurrent=historyCurrent;}
        internal bool IsLookupCurrent=>lookupsCurrent!=null&&lookupsCurrent();
        internal bool IsHistoryCurrent=>historyCurrent!=null&&historyCurrent();
    }
    internal sealed class MedicineSession
    {
        internal readonly IMedicineService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> current;
        internal MedicineSession(IMedicineService service,OperationActor actor,System.Func<bool> current)
        {Service=service;Actor=actor;this.current=current;}
        internal bool IsCurrent=>current!=null&&current();
    }
    internal sealed class ManufacturerReadSession
    {
        internal readonly IManufacturerService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> current;
        internal ManufacturerReadSession(IManufacturerService service, OperationActor actor, System.Func<bool> current)
        { Service=service; Actor=actor; this.current=current; }
        internal bool IsCurrent => current != null && current();
    }
    internal sealed class SalesCustomerReadSession
    {
        internal readonly ISalesCustomerService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> current;
        internal SalesCustomerReadSession(ISalesCustomerService service, OperationActor actor, System.Func<bool> current)
        { Service=service; Actor=actor; this.current=current; }
        internal bool IsCurrent => current != null && current();
    }
    internal sealed class MedicineQuickSearchSession
    {
        internal readonly IMedicineSearchService Service;
        internal readonly OperationActor Actor;
        private readonly System.Func<bool> isCurrent;
        internal MedicineQuickSearchSession(IMedicineSearchService service, OperationActor actor, System.Func<bool> isCurrent)
        { Service = service; Actor = actor; this.isCurrent = isCurrent; }
        internal bool IsCurrent => isCurrent != null && isCurrent();
    }

}












