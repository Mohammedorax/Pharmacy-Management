using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // اختبارات حتمية بلا قاعدة بيانات؛ تشغّل عند: project_fo_3.exe --verify-permission-policy
    internal static class PermissionSelfTests
    {
        public static void RunAll()
        {
            VerifyCustomerDirectoryAuthorization();
            VerifyManufacturerAuthorization();
            VerifyMedicineAuthorization();
            VerifyRetainedMedicineRootSession();
            VerifyBarcodeAuthorization();
            VerifyRetainedBarcodeRootSession();
            VerifyPurchaseBarcodeSourceRevocation();
            VerifyEmployeeAdministrationAuthorization();
            VerifyCurrencyOperationAuthorization();
            VerifyCurrencySnapshotAuthorization();
            VerifyQuickSearchSessionPolicy();
            VerifyInventorySessionRevisions();
            VerifyPurchaseOperationAuthorization();
            VerifySalesOperationAuthorization();
            VerifySalesCashierSelection();
            VerifyAccountingOperationAuthorization();
            VerifyInventoryOperationAuthorization();
            VerifyPermissionCacheRefresh();
            VerifyEmployeeIdPermissionCacheKey();
            AssertAllowed("Cashier sales", PermissionService.GetRoleDefault("Cashier", AppOperation.PostSalesInvoice));
            AssertDenied("Cashier sales cancellation", PermissionService.GetRoleDefault("Cashier", AppOperation.CancelSalesInvoice));
            AssertDenied("Cashier commercial return", PermissionService.GetRoleDefault("Cashier", AppOperation.IssueCommercialSalesReturn));
            AssertDenied("Cashier quarantine resolution", PermissionService.GetRoleDefault("Cashier", AppOperation.ResolveCommercialReturnQuarantine));
            AssertDenied("Cashier accounting periods", PermissionService.GetRoleDefault("Cashier", AppOperation.ManageAccountingPeriods));
            AssertDenied("Cashier active ingredients", PermissionService.GetRoleDefault("Cashier", AppOperation.ManageActiveIngredients));
            AssertDenied("Cashier inventory", PermissionService.GetRoleDefault("Cashier", AppOperation.PostInventoryOperation));
            AssertAllowed("Inventory purchase", PermissionService.GetRoleDefault("Inventory", AppOperation.PostPurchaseInvoice));
            AssertAllowed("Inventory operation", PermissionService.GetRoleDefault("Inventory", AppOperation.PostInventoryOperation));
            AssertAllowed("Cashier customer maintenance", PermissionService.GetRoleDefault("Cashier", AppOperation.ManageSalesCustomers));
            AssertAllowed("Inventory customer maintenance", PermissionService.GetRoleDefault("Inventory", AppOperation.ManageSalesCustomers));
            AssertDenied("Accountant customer maintenance", PermissionService.GetRoleDefault("Accountant", AppOperation.ManageSalesCustomers));
            AssertDenied("Inventory active ingredients", PermissionService.GetRoleDefault("Inventory", AppOperation.ManageActiveIngredients));
            AssertDenied("Inventory accounting", PermissionService.GetRoleDefault("Inventory", AppOperation.PostAccountingEntry));
            AssertAllowed("Accountant journal", PermissionService.GetRoleDefault("Accountant", AppOperation.PostAccountingEntry));
            AssertAllowed("Accountant reports", PermissionService.GetRoleDefault("Accountant", AppOperation.ViewAccountingReports));
            AssertDenied("Accountant sales", PermissionService.GetRoleDefault("Accountant", AppOperation.PostSalesInvoice));
            AssertAllowed("Cashier sales screen", PermissionService.GetRoleDefault("Cashier", AppPermission.Sales));
            if (CustomerSupplierManagementForm.RequiredPermission(true) != AppPermission.Sales ||
                CustomerSupplierManagementForm.RequiredPermission(false) != AppPermission.ManageManufacturers)
                throw new InvalidOperationException("Customer selection and customer management must use separate screen permissions.");
            AssertDenied("Cashier customer management can be hidden",
                PermissionService.ResolveScreenAccess("Cashier", AppPermission.ManageManufacturers, false));
            AssertAllowed("Customer selection remains available within permitted sales",
                PermissionService.ResolveScreenAccess("Cashier", CustomerSupplierManagementForm.RequiredPermission(true), null));
            AssertDenied("Cashier accounting screen", PermissionService.GetRoleDefault("Cashier", AppPermission.Accounting));
            AssertDenied("Explicit sales screen deny", PermissionService.ResolveScreenAccess("Cashier", AppPermission.Sales, false));
            AssertAllowed("Explicit accounting screen grant", PermissionService.ResolveScreenAccess("Cashier", AppPermission.Accounting, true));
            AssertDenied("Employee administration cannot be delegated", PermissionService.ResolveScreenAccess("Cashier", AppPermission.ManageEmployees, true));
            AssertDenied("Settings cannot be delegated", PermissionService.ResolveScreenAccess("Cashier", AppPermission.Settings, true));
            AssertAllowed("Manager retains screen access", PermissionService.ResolveScreenAccess("Manager", AppPermission.Sales, false));
            if (EmployeeScreenPermissionKeys.For("Sales") != "Screen.Sales" ||
                EmployeeScreenPermissionKeys.IsDelegableKey("Screen.Settings") ||
                EmployeeScreenPermissionKeys.IsDelegableKey("Screen.ManageEmployees"))
                throw new InvalidOperationException("Screen override allow-list is inconsistent.");

            SessionContext.Start("qa-permission-lookup", "Cashier");
            try
            {
                bool selected;
                AssertAllowed("Failed permission lookup must return an explicit decision",
                    PermissionService.TryGetCurrentUserOverride("Screen.Sales", out selected,
                        user => { throw new InvalidOperationException("simulated read failure"); }));
                AssertDenied("Failed permission lookup must deny access", selected);
                AssertAllowed("Permission lookup retries after a failure",
                    PermissionService.TryGetCurrentUserOverride("Screen.Sales", out selected,
                        user => new EmployeePermissionSnapshot
                        {
                            IsAvailable = true,
                            Role = "Cashier",
                            Overrides = new Dictionary<string, bool> { { "Screen.Sales", true } }
                        }));
                AssertAllowed("Successful retry restores the saved grant", selected);
            }
            finally { SessionContext.Clear(); }

            foreach (AppOperation operation in (AppOperation[])Enum.GetValues(typeof(AppOperation)))
            {
                AssertAllowed("Manager " + operation, PermissionService.GetRoleDefault("Manager", operation));
            }
        }

        private static void VerifyCurrencyOperationAuthorization()
        {
            int writes = 0;
            Action write = () => writes++;
            try
            {
                SessionContext.Clear();
                VerifyCurrencyDenied(write, "Unauthenticated", ref writes);
                foreach (string role in new[] { "Cashier", "Accountant", "Inventory", "Unknown" })
                {
                    SessionContext.Start("permission-preview", role);
                    VerifyCurrencyDenied(write, role, ref writes);
                }
                foreach (string role in new[] { "Manager", "Admin", "مدير" })
                {
                    SessionContext.Start("permission-preview", role);
                    AssertAllowed("Currency " + role, CurrencyWorkspaceService.CanManageCurrencies);
                    CurrencyWorkspaceService.ExecuteAuthorizedMutation(write);
                    try { CurrencyWorkspaceService.SaveCurrency(null); }
                    catch (ArgumentNullException) { continue; }
                    throw new InvalidOperationException("Manager currency save must reach existing input validation.");
                }
                if (writes != 3) throw new InvalidOperationException("Currency writer was executed incorrectly.");
                SessionContext.Clear();
                VerifyCurrencyDenied(write, "Logged out", ref writes, 3);
            }
            finally { SessionContext.Clear(); }
        }

        private static void VerifyCurrencyDenied(Action write, string scenario, ref int writes, int expectedWrites = 0)
        {
            AssertDenied("Currency " + scenario, CurrencyWorkspaceService.CanManageCurrencies);
            bool blocked = false;
            try { CurrencyWorkspaceService.ExecuteAuthorizedMutation(write); }
            catch (UnauthorizedAccessException) { blocked = true; }
            if (!blocked || writes != expectedWrites)
                throw new InvalidOperationException("Unauthorized currency writer reached: " + scenario);
            blocked = false;
            try { CurrencyWorkspaceService.SaveCurrency(null); }
            catch (UnauthorizedAccessException) { blocked = true; }
            if (!blocked) throw new InvalidOperationException("Currency save authorization must precede validation: " + scenario);
        }

        private static void VerifyCustomerDirectoryAuthorization()
        {
            try
            {
                SessionContext.Clear();
                foreach (bool signedIn in new[] { false, true })
                {
                    if (signedIn) SessionContext.Start("customer-read-test", "Manager");
                    foreach (var actor in new[] { null,
                        new OperationActor { UserName = "other", UserRole = "Manager" },
                        new OperationActor { UserName = "customer-read-test", UserRole = "Cashier" } })
                    {
                        try { ApplicationServices.SalesCustomers.GetAllAsync(actor, System.Threading.CancellationToken.None); }
                        catch (UnauthorizedAccessException) { continue; }
                        throw new InvalidOperationException("Customer directory accepted an unauthorized session identity.");
                    }
                }
            }
            finally { SessionContext.Clear(); }
        }

        public static void VerifyCurrencyGuardQaReadOnly(string server,string database)
        {
            string normalized=(database??string.Empty).ToLowerInvariant();
            if(!(normalized.EndsWith("_qa")||normalized.EndsWith("_test")||normalized.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA suffix required before reading currency guard.");
            ImportedInvoiceApprovalGuard.EnsureDraftCurrency(null,null,null);
            foreach(string invalid in new[]{" ",new string('X',11)})
            {
                bool rejected=false;try{ImportedInvoiceApprovalGuard.EnsureDraftCurrency(null,null,invalid);}catch(InvalidOperationException){rejected=true;}
                if(!rejected)throw new Exception("Invalid expected currency reached SQL.");
            }
            var builder=new System.Data.SqlClient.SqlConnectionStringBuilder{DataSource=server,InitialCatalog=database,IntegratedSecurity=true,ConnectTimeout=5,ApplicationName="Pharmacy.CurrencyGuard.ReadOnly"};
            using(var connection=new System.Data.SqlClient.SqlConnection(builder.ConnectionString))
            {
                connection.Open();using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        string code;
                        using(var command=new System.Data.SqlClient.SqlCommand("SELECT TOP (1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId;",connection,transaction))
                            code=Convert.ToString(command.ExecuteScalar())?.Trim()??string.Empty;
                        if(code.Length==0 || code.Length>10)throw new Exception("QA has no valid system code for currency guard comparison.");
                        ImportedInvoiceApprovalGuard.EnsureDraftCurrency(connection,transaction,code);
                        ImportedInvoiceApprovalGuard.EnsureDraftCurrency(connection,transaction," "+code.ToLowerInvariant()+" ");
                        string wrong=string.Equals(code,"QAF",StringComparison.OrdinalIgnoreCase)?"QAQ":"QAF";
                        bool rejected=false;try{ImportedInvoiceApprovalGuard.EnsureDraftCurrency(connection,transaction,wrong);}
                        catch(InvalidOperationException error){rejected=error.Message=="تغيرت عملة النظام منذ إعداد المسودة. افتح فاتورة جديدة بالعملة الصحيحة؛ لا يحدث تحويل تلقائي.";}
                        if(!rejected)throw new Exception("Stale draft currency not rejected by actual SQL guard.");
                        rejected=false;try{ImportedInvoiceApprovalGuard.EnsureCurrency(connection,transaction,wrong);}
                        catch(InvalidOperationException error){rejected=error.Message=="عملة الفاتورة المستوردة لا تطابق عملة النظام الحالية؛ لا يحدث تحويل تلقائي.";}
                        if(!rejected)throw new Exception("Imported currency guard compatibility lost.");
                        using(var command=new System.Data.SqlClient.SqlCommand("SELECT XACT_STATE();",connection,transaction))
                            if(Convert.ToInt32(command.ExecuteScalar())!=1)throw new Exception("Currency rejection damaged read-only transaction.");
                    }
                    finally{transaction.Rollback();}
                }
            }
            Console.WriteLine("PASS QA currency guard: actual HOLDLOCK read, matching code/trim/case accepted, stale expected code rejected, import compatibility, invalid/null contract and healthy transaction; SELECT-only with rollback; PersistentWrites=0. This does not issue or cancel a financial invoice.");
        }

        private static void VerifyInventorySessionRevisions()
        {
            var factories=new Func<Tuple<Action,Action>>[] {
                ()=>{var session=ApplicationServices.CreateInventoryStockReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadAsync(null,System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateHomeOperationalAlertReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateInventoryAlertReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadAsync(5,90,System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateInvoiceInventoryAlertReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadAsync(5,90,System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateInventoryOperationalReportReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadMovementSummaryAsync(null,System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateInventoryIntegrityReadSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult());},
                ()=>{var session=ApplicationServices.CreateInventoryPostingSession();return Tuple.Create<Action,Action>(session.EnsureContext,()=>session.LoadMedicinesAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult());}
            };
            try
            {
                SessionContext.Start("inventory-revision","Manager","INVENTORY-E1");
                var postingOriginal=ApplicationServices.CreateInventoryPostingSession();
                SessionContext.Clear();SessionContext.Start("inventory-revision","Manager","INVENTORY-E1");
                var postingFresh=ApplicationServices.CreateInventoryPostingSession();
                AssertAllowed("Pending recovery context key remains stable across valid reauthentication",postingOriginal.ContextKey==postingFresh.ContextKey);
                bool writeRejected=false;
                try{postingOriginal.PostAsync(new InventoryPostRequest(InventoryPostKind.Adjustment,"REVISION-NO-SQL","REVISION-NO-SQL",1,1m,"revision guard verification")).GetAwaiter().GetResult();}catch(InvalidOperationException){writeRejected=true;}
                AssertAllowed("Old posting session cannot send a write after relogin",writeRejected);
                foreach(var factory in factories)
                {
                    SessionContext.Start("inventory-revision","Manager","INVENTORY-E1");
                    var original=factory(); original.Item1();
                    SessionContext.Clear();SessionContext.Start("inventory-revision","Manager","INVENTORY-E1");
                    bool rejected=false;try{original.Item1();}catch(InvalidOperationException){rejected=true;}
                    AssertAllowed("Inventory session cannot revive after same-identity reauthentication",rejected);
                    rejected=false;try{original.Item2();}catch(InvalidOperationException){rejected=true;}
                    AssertAllowed("Stale inventory read rejected before validation or SQL",rejected);
                    var fresh=factory();fresh.Item1();
                    SessionContext.Clear();rejected=false;try{fresh.Item1();}catch(InvalidOperationException){rejected=true;}
                    AssertAllowed("Logout invalidates fresh inventory session",rejected);
                }
            }
            finally{SessionContext.Clear();}
            Console.WriteLine("PASS inventory root session revision: stock, alerts, home SystemMonitor alerts, invoice alerts, movement summary/turnover, integrity and posting reads; same-identity relogin/logout rejected before SQL, new session usable; no writes.");
        }
        private static void VerifyQuickSearchSessionPolicy()
        {
            const string profileA="Data Source=.;Initial Catalog=PharmacyManagement_qa;Integrated Security=True";
            string profile=profileA;
            try
            {
                SessionContext.Clear();
                var loggedOut=ApplicationServices.CreateMedicineQuickSearchSession(()=>profile);
                AssertDenied("Search session requires login",loggedOut.IsCurrent);
                RequireQuickSearchDenied(loggedOut);
                foreach(string role in new[]{"Cashier","Pharmacist","Inventory","Accountant","Unknown"})
                foreach(var grants in new[]{new[]{false,false},new[]{true,false},new[]{false,true}})
                {
                    SessionContext.Start("search-policy",role,"SEARCH-E1");
                    SeedQuickSearchGrants(role,grants[0],grants[1]);
                    var session=ApplicationServices.CreateMedicineQuickSearchSession(()=>profile);
                    bool allowed=grants[0]||grants[1];
                    if(session.IsCurrent!=allowed)throw new Exception("Quick search ignored explicit screen grants: "+role);
                    if(allowed)RequireQuickSearchCancelled(session);else RequireQuickSearchDenied(session);
                    RequireQuickSearchDenied(session,new OperationActor{UserName="spoof",UserRole=role});
                    RequireQuickSearchDenied(session,new OperationActor{UserName="search-policy",UserRole="Manager"});
                    RequireQuickSearchDenied(session,null,true);
                    PermissionService.ClearCachedOverrides(); SeedQuickSearchGrants(role,false,false);
                    AssertDenied("Revoked quick search grants",session.IsCurrent); RequireQuickSearchDenied(session);
                }
                foreach(string role in new[]{"Manager","Admin","مدير"})
                {
                    SessionContext.Start("search-policy",role,"SEARCH-E1");
                    var session=ApplicationServices.CreateMedicineQuickSearchSession(()=>profile);
                    AssertAllowed("Manager quick search alias",session.IsCurrent); RequireQuickSearchCancelled(session);
                    SessionContext.Clear(); SessionContext.Start("search-policy",role,"SEARCH-E1");
                    AssertDenied("Same-user relogin cannot revive old quick search session",session.IsCurrent); RequireQuickSearchDenied(session);
                    session=ApplicationServices.CreateMedicineQuickSearchSession(()=>profile);
                    AssertAllowed("Fresh login creates usable quick search session",session.IsCurrent); RequireQuickSearchCancelled(session);
                    profile=profileA+";Application Name=ChangedProfile";
                    AssertDenied("Changed connection profile invalidates quick search",session.IsCurrent); RequireQuickSearchDenied(session);
                    profile=profileA;
                    SessionContext.Start("search-policy",role,"SEARCH-E2");
                    AssertDenied("Changed employee invalidates quick search",session.IsCurrent); RequireQuickSearchDenied(session);
                    SessionContext.Start("other-user",role,"SEARCH-E1");
                    AssertDenied("Changed user invalidates quick search",session.IsCurrent); RequireQuickSearchDenied(session);
                    SessionContext.Start("search-policy","Cashier","SEARCH-E1");
                    AssertDenied("Changed role invalidates quick search",session.IsCurrent); RequireQuickSearchDenied(session);
                    SessionContext.Clear(); AssertDenied("Logout invalidates quick search",session.IsCurrent); RequireQuickSearchDenied(session);
                }
            }
            finally{SessionContext.Clear();}
            Console.WriteLine("PASS quick search root policy: 15 role/grant combinations, manager aliases, forged/null actor, revoked grants, employee/user/role/logout/profile changes. Real root/PermissionService, synthetic permission cache/profile provider, precancelled reads; no SQL/config writes or interactive login.");
        }
        private static void SeedQuickSearchGrants(string role,bool sales,bool purchases)
        {
            bool ignored;
            PermissionService.TryGetCurrentUserOverride("Screen.Sales",out ignored,_=>new EmployeePermissionSnapshot{
                IsAvailable=true,Role=role,Overrides=new Dictionary<string,bool>{{"Screen.Sales",sales},{"Screen.Purchases",purchases}}
            });
        }
        private static void RequireQuickSearchCancelled(MedicineQuickSearchSession session)
        {
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                cancellation.Cancel();
                try{session.Service.SearchAsync("",session.Actor,cancellation.Token).GetAwaiter().GetResult();}
                catch(OperationCanceledException){return;}
                throw new Exception("Authorized precancelled quick search reached storage.");
            }
        }
        private static void RequireQuickSearchDenied(MedicineQuickSearchSession session,OperationActor actor=null,bool forceNull=false)
        {
            try{session.Service.SearchAsync("",forceNull?null:actor??session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
            catch(UnauthorizedAccessException){return;}
            throw new Exception("Denied quick search root session reached storage.");
        }
        private static void VerifyCurrencySnapshotAuthorization()
        {
            try
            {
                SessionContext.Clear();VerifyCurrencySnapshotDenied(new OperationActor{UserName="currency-reader",UserRole="Manager"});
                SessionContext.Start("currency-reader","Cashier");
                VerifyCurrencySnapshotDenied(null);VerifyCurrencySnapshotDenied(new OperationActor{UserName="other",UserRole="Cashier"});
                VerifyCurrencySnapshotDenied(new OperationActor{UserName="currency-reader",UserRole="Manager"});
                using(var canceled=new System.Threading.CancellationTokenSource())
                {
                    canceled.Cancel();
                    try{ApplicationServices.CurrencyRead.GetSystemCurrencySnapshotAsync(new OperationActor{UserName="currency-reader",UserRole="Cashier"},canceled.Token).GetAwaiter().GetResult();}
                    catch(OperationCanceledException){return;}
                    throw new Exception("Authorized canceled currency read reached storage.");
                }
            }
            finally{SessionContext.Clear();}
        }
        private static void VerifyCurrencySnapshotDenied(OperationActor actor)
        {
            try{ApplicationServices.CurrencyRead.GetSystemCurrencySnapshotAsync(actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
            catch(UnauthorizedAccessException){return;}
            throw new Exception("Unauthenticated or forged currency snapshot request reached storage.");
        }

        private static void VerifyBarcodeAuthorization()
        {
            try
            {
                SessionContext.Clear(); VerifyBarcodeDenied(new OperationActor { UserName="barcode-test",UserRole="Manager" });
                SessionContext.Start("barcode-test","Accountant"); SeedBarcodePermission("Accountant",false);
                VerifyBarcodeDenied(new OperationActor { UserName="barcode-test",UserRole="Accountant" });
                SessionContext.Start("barcode-test","Manager"); VerifyBarcodeDenied(new OperationActor { UserName="other",UserRole="Manager" });
                VerifyBarcodeDenied(null); VerifyBarcodeDenied(new OperationActor { UserName="barcode-test",UserRole="Admin" });
                foreach(string role in new[]{"Cashier","Manager","Admin","مدير"})
                {
                    SessionContext.Start("barcode-test",role); SeedBarcodePermission(role,true);
                    if(ApplicationServices.MedicineSearch.FindByBarcodeAsync(" ",new OperationActor{UserName="barcode-test",UserRole=role},System.Threading.CancellationToken.None).GetAwaiter().GetResult()!=null)
                        throw new Exception("Authorized empty barcode invented data.");
                }
                SessionContext.Start("barcode-test","Cashier"); SeedBarcodePermission("Cashier",false);
                VerifyBarcodeDenied(new OperationActor { UserName="barcode-test",UserRole="Cashier" });
            }
            finally { SessionContext.Clear(); }
            Console.WriteLine("PASS barcode root policy: logout, denied screen, forged user/role/null denied; cashier with synthetic cached screen grant/manager aliases reach blank validation, no SQL.");
        }
        private static void VerifyRetainedBarcodeRootSession()
        {
            try
            {
                SessionContext.Start("barcode-retained","Manager");
                SeedBarcodePermission("Manager",true);
                var actor=new OperationActor{UserName="barcode-retained",UserRole="Manager"};
                var retained=ApplicationServices.MedicineSearch;
                if(retained.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult()!=null)
                    throw new Exception("Blank barcode invented data.");
                SessionContext.Start("barcode-retained","Manager");
                SeedBarcodePermission("Manager",true);
                bool denied=false;
                try{retained.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied)throw new Exception("Retained barcode root survived same-account relogin.");
                if(ApplicationServices.MedicineSearch.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult()!=null)
                    throw new Exception("Fresh barcode root invented data.");
                string source="Server=.;Database=barcode_a_sandbox;Integrated Security=true;";
                var pinned=ApplicationServices.CreateMedicineBarcodeSession(()=>source);
                if(!pinned.IsCurrent)throw new Exception("Fresh barcode connection context rejected.");
                source="Server=.;Database=barcode_b_sandbox;Integrated Security=true;";
                denied=false;
                try{pinned.Service.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied||pinned.IsCurrent)throw new Exception("Old barcode connection context survived source change.");
                SessionContext.Clear();
                denied=false;
                try{retained.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied)throw new Exception("Retained barcode root survived logout.");
            }
            finally{SessionContext.Clear();}
            Console.WriteLine("PASS retained barcode root: same-account relogin/logout rejected; fresh root valid; blank validation only, no SQL.");
        }
        private static void VerifyPurchaseBarcodeSourceRevocation()
        {
            try
            {
                SessionContext.Start("owned-purchase-source","Manager");
                bool ignored;
                PermissionService.TryGetCurrentUserOverride("Screen.Purchases",out ignored,_=>new EmployeePermissionSnapshot{
                    IsAvailable=true,Role="Manager",Overrides=new Dictionary<string,bool>{{"Screen.Purchases",true}}
                });
                string original="Server=purchase.invalid;Database=source_a_sandbox;Integrated Security=true;Connect Timeout=1;";
                string source=original;
                var old=ApplicationServices.CreatePurchaseBarcodeReadSession(()=>source);
                AssertAllowed("Fresh purchase barcode root",old.IsCurrent);
                source="Server=purchase.invalid;Database=source_b_sandbox;Integrated Security=true;Connect Timeout=1;";
                AssertDenied("Changed purchase barcode source",old.IsCurrent);
                source=original;
                AssertDenied("Old purchase barcode root must remain invalid",old.IsCurrent);
                foreach(Action action in new Action[]{
                    ()=>old.Medicines.GetNamesAsync("",old.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),
                    ()=>old.Medicines.FindAsync("عربي",old.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),
                    ()=>old.Barcode.FindByBarcodeAsync("001",old.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult()
                }){
                    bool denied=false;try{action();}catch(UnauthorizedAccessException){denied=true;}
                    AssertAllowed("Invalid purchase barcode root denied before SQL",denied);
                }
                var fresh=ApplicationServices.CreatePurchaseBarcodeReadSession(()=>source);
                AssertAllowed("Renewed purchase barcode root",fresh.IsCurrent);
                using(var cancel=new System.Threading.CancellationTokenSource()){
                    cancel.Cancel();bool canceled=false;
                    try{fresh.Medicines.GetNamesAsync("",fresh.Actor,cancel.Token).GetAwaiter().GetResult();}
                    catch(OperationCanceledException){canceled=true;}
                    AssertAllowed("Fresh purchase read honors cancellation before SQL",canceled);
                }
            }
            finally{SessionContext.Clear();}
            Console.WriteLine("PASS purchase barcode source lifetime: observed source change stays revoked after restore; all three old reads denied before SQL; fresh root usable; no SQL/config writes.");
        }
        private static void SeedBarcodePermission(string role,bool granted)
        {
            bool ignored;
            PermissionService.TryGetCurrentUserOverride("Screen.Sales",out ignored,_ => new EmployeePermissionSnapshot {
                IsAvailable=true,Role=role,Overrides=new Dictionary<string,bool>{{"Screen.Sales",granted}}
            });
        }
        private static void VerifyBarcodeDenied(OperationActor actor)
        {
            try { ApplicationServices.MedicineSearch.FindByBarcodeAsync(" ",actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult(); }
            catch(UnauthorizedAccessException){return;}
            throw new Exception("Unauthorized barcode reached validation/storage.");
        }

        private static void VerifyMedicineAuthorization()
        {
            try
            {
                SessionContext.Clear();
                VerifyMedicineDenied(new OperationActor { UserName = "medicine-test", UserRole = "Manager" });
                SessionContext.Start("medicine-test", "Accountant");
                VerifyMedicineDenied(new OperationActor { UserName = "medicine-test", UserRole = "Accountant" });
                VerifyMedicineDenied(new OperationActor { UserName = "medicine-test", UserRole = "Manager" });
                foreach (string role in new[] { "Manager", "Admin", "مدير" })
                {
                    SessionContext.Start("medicine-test", role);
                    VerifyMedicineDenied(new OperationActor { UserName = "other", UserRole = role });
                    VerifyMedicineDenied(null);
                    var actor = new OperationActor { UserName = "medicine-test", UserRole = role };
                    bool validated = false;
                    try { ApplicationServices.Medicines.Create(null, actor); }
                    catch (ArgumentNullException) { validated = true; }
                    if (!validated) throw new InvalidOperationException("Authorized medicine create did not reach input validation.");
                }
            }
            finally { SessionContext.Clear(); }
            Console.WriteLine("PASS medicine session identity, forged role/user, logout, read/write/barcode denial and authorized manager aliases; no SQL.");
        }

        private static void VerifyRetainedMedicineRootSession()
        {
            try
            {
                SessionContext.Start("medicine-retained", "Manager");
                var actor = new OperationActor { UserName="medicine-retained", UserRole="Manager" };
                var retained = ApplicationServices.Medicines;
                try { retained.Create(null, actor); }
                catch (ArgumentNullException) { }
                SessionContext.Start("medicine-retained", "Manager");
                foreach (Action action in new Action[] {
                    () => retained.Create(null, actor),
                    () => retained.Update(null, actor),
                    () => retained.Delete(null, actor),
                    () => retained.CreateAsync(null, actor).GetAwaiter().GetResult(),
                    () => retained.UpdateAsync(null, actor).GetAwaiter().GetResult(),
                    () => retained.DeleteAsync(null, actor).GetAwaiter().GetResult()
                })
                {
                    bool denied=false;
                    try { action(); }
                    catch (UnauthorizedAccessException) { denied=true; }
                    if(!denied) throw new InvalidOperationException("Retained medicine root survived same-account relogin.");
                }
                bool fresh=false;
                try { ApplicationServices.Medicines.Create(null, actor); }
                catch (ArgumentNullException) { fresh=true; }
                if(!fresh) throw new InvalidOperationException("Fresh medicine root did not reach validation.");
                SessionContext.Clear();
                bool logoutDenied=false;
                try { retained.Create(null, actor); }
                catch(UnauthorizedAccessException){logoutDenied=true;}
                if(!logoutDenied)throw new InvalidOperationException("Retained medicine root survived logout.");
            }
            finally { SessionContext.Clear(); }
            Console.WriteLine("PASS retained medicine root: six writes denied after same-account relogin; fresh root reaches validation; logout rejected; no SQL.");
        }
        private static void VerifyMedicineDenied(OperationActor actor)
        {
            foreach (Action action in new Action[] {
                () => ApplicationServices.Medicines.FindByNameAsync("دواء", actor, System.Threading.CancellationToken.None),
                () => ApplicationServices.Medicines.GetAll(actor),
                () => ApplicationServices.Medicines.GetCompanyNames(actor),
                () => ApplicationServices.Medicines.GetAllAsync(actor, System.Threading.CancellationToken.None),
                () => ApplicationServices.Medicines.GetCompanyNamesAsync(actor, System.Threading.CancellationToken.None),
                () => ApplicationServices.Medicines.GenerateAvailableBarcode(null, actor),
                () => ApplicationServices.Medicines.GenerateAvailableBarcodeAsync(null, actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult(),
                () => ApplicationServices.Medicines.CreateAsync(null, actor).GetAwaiter().GetResult(),
                () => ApplicationServices.Medicines.UpdateAsync(null, actor).GetAwaiter().GetResult(),
                () => ApplicationServices.Medicines.DeleteAsync(null, actor).GetAwaiter().GetResult(),
                () => ApplicationServices.Medicines.Create(null, actor),
                () => ApplicationServices.Medicines.Update(null, actor),
                () => ApplicationServices.Medicines.Delete(null, actor) })
            {
                try { action(); }
                catch (UnauthorizedAccessException) { continue; }
                throw new InvalidOperationException("Unauthorized medicine request reached validation/storage.");
            }
        }

        private static void VerifyManufacturerAuthorization()
        {
            try
            {
                SessionContext.Clear();
                VerifyManufacturerDenied(new OperationActor { UserName = "test", UserRole = "Manager" });
                SessionContext.Start("test", "Accountant");
                VerifyManufacturerDenied(new OperationActor { UserName = "test", UserRole = "Accountant" });
                VerifyManufacturerDenied(new OperationActor { UserName = "test", UserRole = "Manager" });
                SessionContext.Start("test", "Manager");
                VerifyManufacturerDenied(new OperationActor { UserName = "other", UserRole = "Manager" });
                try { ApplicationServices.Manufacturers.Create(null, new OperationActor { UserName = "test", UserRole = "Manager" }); }
                catch (ArgumentNullException) { return; }
                throw new InvalidOperationException("Manager supplier access did not reach validation.");
            }
            finally { SessionContext.Clear(); }
        }

        private static void VerifyManufacturerDenied(OperationActor actor)
        {
            foreach (Action operation in new Action[] {
                () => ApplicationServices.Manufacturers.GetAllAsync(actor, System.Threading.CancellationToken.None),
                () => ApplicationServices.Manufacturers.Create(null, actor),
                () => ApplicationServices.Manufacturers.Update(null, actor),
                () => ApplicationServices.Manufacturers.Delete(null, actor) })
            {
                try { operation(); }
                catch (UnauthorizedAccessException) { continue; }
                throw new InvalidOperationException("Unauthorized supplier operation was accepted.");
            }
        }

        private static void VerifyEmployeeIdPermissionCacheKey()
        {
            try
            {
                string loadedId = string.Empty;
                Func<string, EmployeePermissionSnapshot> load = id =>
                {
                    loadedId = id;
                    return new EmployeePermissionSnapshot {
                        IsAvailable = true, Role = "Cashier",
                        Overrides = new Dictionary<string, bool> { { "Screen.Sales", true } }
                    };
                };
                bool allowed;
                SessionContext.Start("same-name", "Cashier", "EMP-7");
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 100);
                AssertAllowed("Permission lookup used the verified EmpId", allowed && loadedId == "EMP-7");
                SessionContext.Start("same-name", "Cashier", "EMP-8");
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 101);
                AssertAllowed("A different EmpId cannot reuse the same-name cache", allowed && loadedId == "EMP-8");
                SessionContext.Clear();
                AssertAllowed("Logout clears the stable identity", SessionContext.CurrentEmployeeId.Length == 0);
            }
            finally { SessionContext.Clear(); }
        }

        private static void VerifyPermissionCacheRefresh()
        {
            try
            {
                SessionContext.Start("permission-refresh-test", "Cashier");
                int reads = 0;
                bool savedGrant = true;
                Func<string, EmployeePermissionSnapshot> load = user =>
                {
                    reads++;
                    return new EmployeePermissionSnapshot
                    {
                        IsAvailable = true, Role = "Cashier",
                        Overrides = new Dictionary<string, bool> { { "Screen.Sales", savedGrant } }
                    };
                };
                bool allowed;
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 100);
                AssertAllowed("Initial saved grant", allowed);
                savedGrant = false;
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 5099);
                AssertAllowed("One permission read within cache lifetime", reads == 1);
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 5100);
                AssertDenied("Expired permission grant refreshed to deny", allowed);
                AssertAllowed("Expired permission snapshot reloaded", reads == 2);
                savedGrant = true;
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 10100);
                AssertAllowed("Saved grant restored before failure simulation", allowed && reads == 3);
                bool hasDecision = PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed,
                    user => { throw new InvalidOperationException("simulated refresh failure"); }, 15100);
                AssertAllowed("Failed refresh returns an explicit decision rather than role fallback", hasDecision);
                AssertDenied("Failed refresh discards a previous grant", allowed);
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, 15101);
                AssertAllowed("Failed refresh retries immediately", allowed && reads == 4);
                SessionContext.Clear();
                SessionContext.Start("permission-wrap-test", "Cashier");
                int startTick = int.MaxValue - 2000;
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, startTick);
                int readsBeforeWrap = reads;
                savedGrant = false;
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, unchecked(startTick + 4999));
                AssertAllowed("Tick wrap preserves unexpired cache", allowed && reads == readsBeforeWrap);
                PermissionService.TryGetCurrentUserOverride("Screen.Sales", out allowed, load, unchecked(startTick + 5000));
                AssertDenied("Tick wrap does not extend an expired grant", allowed);
                AssertAllowed("Tick wrap reloads exactly once at expiry", reads == readsBeforeWrap + 1);
            }
            finally { SessionContext.Clear(); }
        }
        private static void VerifyInventoryOperationAuthorization()
        {
            Action[] operations =
            {
                () => InventoryService.ApplyOpeningStock("permission-test", 1, "Permission verification"),
                () => InventoryService.ApplyAdjustment("permission-test", 1, "Permission verification"),
                () => InventoryService.ApplyManualReceipt("permission-test", 1, 1m, "Permission verification"),
                () => InventoryService.ApplyManualIssue("permission-test", 1, 1m, "Permission verification")
            };
            try
            {
                SessionContext.Clear();
                foreach (Action operation in operations) ExpectOperationDenied(operation, "Unauthenticated inventory operation");
                SessionContext.Start("permission-inventory-test", "Inventory");
                bool granted;
                PermissionService.TryGetCurrentUserOverride(AppOperation.PostInventoryOperation.ToString(), out granted,
                    user => new EmployeePermissionSnapshot
                    {
                        IsAvailable = true, Role = "Inventory",
                        Overrides = new Dictionary<string, bool> { { AppOperation.PostInventoryOperation.ToString(), false } }
                    });
                foreach (Action operation in operations) ExpectOperationDenied(operation, "Explicitly denied inventory operation");
            }
            finally { SessionContext.Clear(); }
        }
        private static void VerifyAccountingOperationAuthorization()
        {
            Action[] operations =
            {
                () => AccountingService.CreateJournalEntry(DateTime.Today, "Manual", null, "Permission test", null),
                () => AccountingService.CreateVoucher("ReceiptExternal", DateTime.Today, "Permission test", "1000", 0m, null),
                () => AccountingService.SaveAccount(null, null, null, null, true),
                () => AccountingService.DeleteAccount(null)
            };
            try
            {
                SessionContext.Clear();
                foreach (Action operation in operations) ExpectOperationDenied(operation, "Unauthenticated accounting operation");
                SessionContext.Start("permission-accounting-test", "Accountant");
                bool granted;
                PermissionService.TryGetCurrentUserOverride(AppOperation.PostAccountingEntry.ToString(), out granted,
                    user => new EmployeePermissionSnapshot
                    {
                        IsAvailable = true, Role = "Accountant",
                        Overrides = new Dictionary<string, bool>
                        {
                            { AppOperation.PostAccountingEntry.ToString(), false },
                            { AppOperation.PostCashVoucher.ToString(), false },
                            { AppOperation.ManageChartOfAccounts.ToString(), false }
                        }
                    });
                foreach (Action operation in operations) ExpectOperationDenied(operation, "Explicitly denied accounting operation");
            }
            finally { SessionContext.Clear(); }
        }
        private static void VerifySalesOperationAuthorization()
        {
            Action issue = () => new SalesInvoiceOperation().Issue(null);
            Action cancel = () => new SalesInvoiceCancellationOperation().Cancel(null);
            try
            {
                SessionContext.Clear();
                ExpectOperationDenied(issue, "Unauthenticated sale");
                ExpectOperationDenied(cancel, "Unauthenticated cancellation");
                SessionContext.Start("permission-sales-test", "Cashier");
                bool granted;
                PermissionService.TryGetCurrentUserOverride(AppOperation.PostSalesInvoice.ToString(), out granted,
                    user => new EmployeePermissionSnapshot
                    {
                        IsAvailable = true, Role = "Cashier",
                        Overrides = new Dictionary<string, bool>
                        {
                            { AppOperation.PostSalesInvoice.ToString(), false },
                            { AppOperation.CancelSalesInvoice.ToString(), false }
                        }
                    });
                ExpectOperationDenied(issue, "Explicitly denied sale");
                ExpectOperationDenied(cancel, "Explicitly denied cancellation");
                SessionContext.Start("permission-sales-manager", "Manager");
                foreach (Action operation in new[] { issue, cancel })
                {
                    bool validated = false;
                    try { operation(); }
                    catch (ArgumentNullException) { validated = true; }
                    AssertAllowed("Manager reaches sales operation input validation", validated);
                }
            }
            finally { SessionContext.Clear(); }
        }

        private static void VerifySalesCashierSelection()
        {
            try
            {
                SessionContext.Start("cashier-selection-check", "Cashier", "EMP-7");
                SalesInvoiceOperation.EnsureCashierSelection("EMP-7");
                ExpectOperationDenied(() => SalesInvoiceOperation.EnsureCashierSelection("EMP-8"),
                    "Cashier cannot allocate another employee's cashbox");
                SessionContext.Start("manager-selection-check", "Manager", "EMP-M");
                SalesInvoiceOperation.EnsureCashierSelection("EMP-8");
            }
            finally { SessionContext.Clear(); }
        }

        private static void ExpectOperationDenied(Action operation, string scenario)
        {
            bool denied = false;
            try { operation(); }
            catch (UnauthorizedAccessException) { denied = true; }
            AssertAllowed(scenario, denied);
        }
        private static void VerifyPurchaseOperationAuthorization()
        {
            bool connectionRequested = false;
            var operation = new PurchaseInvoiceOperation(() =>
            {
                connectionRequested = true;
                throw new InvalidOperationException("Authorization test must not open SQL.");
            });
            try
            {
                SessionContext.Clear();
                ExpectPurchaseDenied(operation, "Unauthenticated purchase");
                SessionContext.Start("permission-purchase-test", "Cashier");
                bool granted;
                PermissionService.TryGetCurrentUserOverride(AppOperation.PostPurchaseInvoice.ToString(), out granted,
                    user => new EmployeePermissionSnapshot
                    {
                        IsAvailable = true,
                        Role = "Cashier",
                        Overrides = new Dictionary<string, bool> { { AppOperation.PostPurchaseInvoice.ToString(), false } }
                    });
                ExpectPurchaseDenied(operation, "Explicitly denied purchase");
                SessionContext.Start("permission-manager-test", "Manager");
                bool reachedValidation = false;
                try { operation.Issue(null); }
                catch (ArgumentNullException) { reachedValidation = true; }
                AssertAllowed("Authorized purchase reaches input validation", reachedValidation);
                AssertDenied("Denied purchase never requests SQL", connectionRequested);
            }
            finally { SessionContext.Clear(); }
        }

        private static void ExpectPurchaseDenied(PurchaseInvoiceOperation operation, string scenario)
        {
            bool denied = false;
            try { operation.Issue(null); }
            catch (UnauthorizedAccessException) { denied = true; }
            AssertAllowed(scenario, denied);
        }
        internal static void RunOwnedQaJournalPosting(string backupPath,string ownerToken)
        {
            string database=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using(var connection=Database.CreateConnection())
            {
                connection.Open();AssertAllowed("Journal actual owned database",connection.Database==database);
                QaTaxPolicySelfTests.VerifyBackup(connection,backupPath);
            }
            string employeeId="QAJ-"+ownerToken.Substring(0,12), user="QA Journal Manager "+ownerToken.Substring(0,12);
            string password=Guid.NewGuid().ToString("N");
            string baseline=PermissionOwnedSnapshot(true);
            try
            {
                SessionContext.Start("QA journal bootstrap","Manager");
                ApplicationServices.Employees.Create(new EmployeeInput{EmployeeId=employeeId,UserName=user,Age=30,Salary=0,Phone="000",Role="Manager",NewPassword=password},
                    new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole});
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                var accounts=AccountingLookupDesktopService.LoadAccounts(true).Rows.Cast<System.Data.DataRow>().Select(r=>Convert.ToString(r["AccountCode"])).ToArray();
                AssertAllowed("Journal fixture two active accounts",accounts.Length>=2);
                string debit=accounts[0],credit=accounts[1]; DateTime date=DateTime.Today;
                Func<string,string,JournalPostRequest> request=(id,account)=>new JournalPostRequest(date,"QAJournalAcceptance",id,"قيد مُتوازن 500 mg",
                    new[]{new PharmacyManagement.Domain.JournalLineValues(debit,1250.50m,0m,"مدين",null),new PharmacyManagement.Domain.JournalLineValues(account,0m,1250.50m,"دائن",null)});
                string beforeFailedAdministration=PermissionOwnedSnapshot(false);
                var bad=request(ownerToken+"-FAIL","QA-MISSING-"+ownerToken.Substring(0,8));
                bool rolledBack=false;
                try{JournalPostingDesktopService.PostAsync(bad).GetAwaiter().GetResult();}
                catch(InvalidOperationException ex){rolledBack=ex.Message.Contains(bad.Lines[1].AccountCode);}
                AssertAllowed("Missing second account rejects after header and first line",rolledBack);
                AssertAllowed("Failed SQL transaction leaves no financial/catalog rows",baseline==PermissionOwnedSnapshot(true));
                AssertAllowed("Failed SQL transaction leaves no audit/admin effects",beforeFailedAdministration==PermissionOwnedSnapshot(false));
                long auditBefore=JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
                int syncId=JournalPostingDesktopService.Post(date,"QAJournalAcceptance",ownerToken,"قيد مُتوازن 500 mg",new[]{
                    new JournalLineInput{AccountCode=debit,Debit=1250.50m,Description="مدين"},
                    new JournalLineInput{AccountCode=credit,Credit=1250.50m,Description="دائن"}});
                VerifyJournalQaPersisted(syncId,date,ownerToken,user,debit,credit);
                AssertAllowed("Sync posting exactly one audit",JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;")==auditBefore+1);
                string afterSync=PermissionOwnedSnapshot(true),adminAfterSync=PermissionOwnedSnapshot(false);
                int retryId=JournalPostingDesktopService.PostAsync(request(ownerToken,credit)).GetAwaiter().GetResult();
                AssertAllowed("Repeated source returns original accepted id",retryId==syncId);
                AssertAllowed("Repeated source has no journal or audit duplication",afterSync==PermissionOwnedSnapshot(true)&&adminAfterSync==PermissionOwnedSnapshot(false));
                int asyncId=JournalPostingDesktopService.PostAsync(request(ownerToken+"-A",credit)).GetAwaiter().GetResult();
                AssertAllowed("Distinct async posting has distinct id",asyncId>0&&asyncId!=syncId);
                VerifyJournalQaPersisted(asyncId,date,ownerToken+"-A",user,debit,credit);
                AssertAllowed("Two positive postings exactly two audit entries",JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;")==auditBefore+2);
                AssertAllowed("All preexisting financial/catalog rows unchanged",baseline==PermissionOwnedSnapshot(true,new[]{syncId,asyncId}));
                VerifyOwnedJournalPeriodAndAudit(ownerToken,user,date,request,credit,syncId);
                long editorAuditBefore=JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
                int[] editorIds;
                try { editorIds=JournalEntriesRecoveryQaSelfTests.Verify(backupPath,ownerToken,user,password,employeeId,debit,credit); }
                catch(Exception error)
                {
                    Console.Error.WriteLine("Owned journal editor verification failed: " + error);
                    throw;
                }
                try {
                    AssertAllowed("Actual full/quick editors, correction and independent restarts exactly four journals plus three Login audits",editorIds.Length==4&&editorIds.All(id=>id>0)&&editorIds.Distinct().Count()==4&&JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;")==editorAuditBefore+7);
                    AssertAllowed("Actual editor/restart leaves all preexisting finance/catalog unchanged",baseline==PermissionOwnedSnapshot(true,new[]{syncId,asyncId}.Concat(editorIds).ToArray()));
                } catch(Exception error) { Console.Error.WriteLine("Owned journal editor aggregate verification failed: " + error); throw; }
                string recoveryFinancialBefore=PermissionOwnedSnapshot(true),recoveryAdminBefore=PermissionOwnedSnapshot(false);
                int recoveryLogins=0;
                try { JournalSubmissionStatusQaSelfTests.Verify(ownerToken,syncId,user,()=>
                {
                    AssertAllowed("Recovery reads before each reauthentication leave admin snapshots unchanged",recoveryAdminBefore==PermissionOwnedSnapshot(false));
                    long auditsBeforeLogin=JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
                    AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                    AssertAllowed("Actual reauthentication creates exactly one security login audit",JournalQaScalar("SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;")==auditsBeforeLogin+1&&
                        JournalQaScalar("SELECT COUNT_BIG(*) FROM (SELECT TOP (1) ActionName,EntityName FROM dbo.AuditLogTb ORDER BY AuditId DESC) q WHERE ActionName=N'Login' AND EntityName=N'Security';")==1);
                    recoveryAdminBefore=PermissionOwnedSnapshot(false);++recoveryLogins;
                }); }
                catch(Exception error) { Console.Error.WriteLine("Owned journal submission recovery verification failed: " + error); throw; }
                AssertAllowed("Recovery fixture performed two real login cycles",recoveryLogins==2);
                AssertAllowed("Recovery status leaves financial/admin snapshots unchanged",recoveryFinancialBefore==PermissionOwnedSnapshot(true)&&recoveryAdminBefore==PermissionOwnedSnapshot(false));
                var old=ApplicationServices.CreateJournalPostSession();string accepted=PermissionOwnedSnapshot(true),auditAccepted=PermissionOwnedSnapshot(false);
                SessionContext.Start(user,"Manager",employeeId);
                bool oldDenied=false;try{old.Service.Post(request(ownerToken+"-STALE",credit),old.Actor);}catch(UnauthorizedAccessException){oldDenied=true;}
                SessionContext.Clear();bool signedOutDenied=false;
                try{JournalPostingDesktopService.PostAsync(request(ownerToken+"-OUT",credit)).GetAwaiter().GetResult();}catch(UnauthorizedAccessException){signedOutDenied=true;}
                AssertAllowed("Old session and signed out positive requests denied",oldDenied&&signedOutDenied);
                AssertAllowed("Denied positive requests no SQL/audit effects",accepted==PermissionOwnedSnapshot(true)&&auditAccepted==PermissionOwnedSnapshot(false));
                Console.WriteLine("PASS owned SQL journal sync/async commit: exact persisted Arabic/date/source/actor/account/1250.50 values, two lines per id, balanced entries, one audit per new entry; same-source retry returns same id without rows/audit; missing second account rolls back inserted header/first line; old session/signed out denied; preexisting financial/catalog snapshot unchanged. No native input.");
            }
            finally{SessionContext.Clear();}
        }
        private static void VerifyOwnedJournalPeriodAndAudit(string ownerToken,string user,DateTime date,Func<string,string,JournalPostRequest> request,string credit,int acceptedId)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string periodsBefore=JournalQaXml("SELECT * FROM dbo.AccountingPeriodsTb ORDER BY PeriodId FOR XML RAW('r'),ROOT('rows'),TYPE");
            string triggersBefore=JournalQaXml("SELECT name,OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name FOR XML RAW('r'),ROOT('rows'),TYPE");
            Guid periodId=Guid.NewGuid();bool inserted=false;
            try
            {
                JournalQaExecuteOwned(ownerToken,"INSERT INTO dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@Id,@Date,@Date,N'Closed',SYSUTCDATETIME(),@User,N'Owned journal acceptance closed period')",
                    new Dictionary<string,object>{{"@Id",periodId},{"@Date",date},{"@User",user}});inserted=true;
                string financial=PermissionOwnedSnapshot(true),administration=PermissionOwnedSnapshot(false);
                bool closedDenied=false;
                try{JournalPostingDesktopService.PostAsync(request(ownerToken+"-CLOSED",credit)).GetAwaiter().GetResult();}
                catch(InvalidOperationException ex){closedDenied=ex.Message.Contains("مقفلة");}
                AssertAllowed("New valid journal blocked by actual closed period",closedDenied);
                AssertAllowed("Closed period rejects before any persisted journal/audit",financial==PermissionOwnedSnapshot(true)&&administration==PermissionOwnedSnapshot(false));
                int retry=JournalPostingDesktopService.PostAsync(request(ownerToken,credit)).GetAwaiter().GetResult();
                AssertAllowed("Accepted source retry through now closed period returns original",retry==acceptedId&&financial==PermissionOwnedSnapshot(true)&&administration==PermissionOwnedSnapshot(false));
            }
            finally
            {
                if(inserted)JournalQaExecuteOwned(ownerToken,"DELETE FROM dbo.AccountingPeriodsTb WHERE PeriodId=@Id",new Dictionary<string,object>{{"@Id",periodId}});
            }
            AssertAllowed("Temporary closed period removed without changing existing periods",periodsBefore==JournalQaXml("SELECT * FROM dbo.AccountingPeriodsTb ORDER BY PeriodId FOR XML RAW('r'),ROOT('rows'),TYPE"));
            string trigger="QA_JournalAudit_"+ownerToken;bool created=false;
            AssertAllowed("Unique owned audit failure trigger absent",JournalQaScalar("SELECT COUNT_BIG(*) FROM sys.triggers WHERE name=N'"+trigger+"'")==0);
            try
            {
                // The token is validated by RequireOwnedTemporaryQa and the username is escaped.
                // Only this fixture's journal audit is rejected; existing triggers are untouched.
                string sql="CREATE TRIGGER dbo.["+trigger+"] ON dbo.AuditLogTb AFTER INSERT AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted WHERE UserName=N'"+user.Replace("'","''")+"' AND ActionName=N'JournalEntryCreated') THROW 51091,N'Owned QA journal audit rejection',1; END";
                JournalQaExecuteOwned(ownerToken,sql,null);created=true;
                string financial=PermissionOwnedSnapshot(true),administration=PermissionOwnedSnapshot(false);bool auditRejected=false;
                try{JournalPostingDesktopService.PostAsync(request(ownerToken+"-AUDIT",credit)).GetAwaiter().GetResult();}
                catch(System.Data.SqlClient.SqlException ex){auditRejected=ex.Number==51091;}
                AssertAllowed("Actual AFTER INSERT audit failure reached",auditRejected);
                AssertAllowed("Audit failure rolls back header/both lines/audit",financial==PermissionOwnedSnapshot(true)&&administration==PermissionOwnedSnapshot(false));
            }
            finally
            {
                if(created)JournalQaExecuteOwned(ownerToken,"DROP TRIGGER dbo.["+trigger+"]",null);
            }
            AssertAllowed("All previous trigger definitions preserved",triggersBefore==JournalQaXml("SELECT name,OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name FOR XML RAW('r'),ROOT('rows'),TYPE"));
            Console.WriteLine("PASS owned SQL journal closed period rejects new entry, permits accepted source retry without duplication, restores period rows; actual AFTER INSERT audit rejection 51091 rolls back header/two lines/audit, restores all trigger definitions. No source/production schema change.");
        }
        private static string JournalQaXml(string select)
        {
            using(var connection=Database.CreateConnection())
            {connection.Open();using(var command=new System.Data.SqlClient.SqlCommand("SELECT CONVERT(nvarchar(max),("+select+"));",connection))return Convert.ToString(command.ExecuteScalar());}
        }
        private static void JournalQaExecuteOwned(string ownerToken,string sql,Dictionary<string,object> parameters)
        {
            string owned=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using(var connection=Database.CreateConnection())
            {
                connection.Open();AssertAllowed("Failure fixture writes only owned temporary database",connection.Database==owned);
                using(var command=new System.Data.SqlClient.SqlCommand(sql,connection))
                {if(parameters!=null)foreach(var pair in parameters)command.Parameters.AddWithValue(pair.Key,pair.Value);command.ExecuteNonQuery();}
            }
        }
        private static long JournalQaScalar(string sql)
        {
            using(var connection=Database.CreateConnection())
            {connection.Open();using(var command=new System.Data.SqlClient.SqlCommand(sql,connection))return Convert.ToInt64(command.ExecuteScalar());}
        }
        private static void VerifyJournalQaPersisted(int id,DateTime date,string sourceId,string user,string debit,string credit)
        {
            AssertAllowed("Positive journal valid id",id>0);
            using(var connection=Database.CreateConnection())
            {
                connection.Open();
                using(var command=new System.Data.SqlClient.SqlCommand("SELECT EntryDate,SourceType,SourceId,Description,CreatedBy FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id",connection))
                {
                    command.Parameters.AddWithValue("@Id",id);using(var reader=command.ExecuteReader())
                    {AssertAllowed("Exact persisted journal header",reader.Read()&&reader.GetDateTime(0)==date&&reader.GetString(1)=="QAJournalAcceptance"&&reader.GetString(2)==sourceId&&reader.GetString(3)=="قيد مُتوازن 500 mg"&&reader.GetString(4)==user&&!reader.Read());}
                }
                using(var command=new System.Data.SqlClient.SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId",connection))
                {
                    command.Parameters.AddWithValue("@Id",id);using(var reader=command.ExecuteReader())
                    {
                        AssertAllowed("Exact persisted debit",reader.Read()&&reader.GetString(0)==debit&&reader.GetDecimal(1)==1250.50m&&reader.GetDecimal(2)==0m&&reader.GetString(3)=="مدين"&&reader.IsDBNull(4));
                        AssertAllowed("Exact persisted credit",reader.Read()&&reader.GetString(0)==credit&&reader.GetDecimal(1)==0m&&reader.GetDecimal(2)==1250.50m&&reader.GetString(3)=="دائن"&&reader.IsDBNull(4)&&!reader.Read());
                    }
                }
            }
        }

        internal static void RunOwnedQaEmployeePermissions(string backupPath, string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using (var connection = Database.CreateConnection())
            {
                connection.Open();
                AssertAllowed("Permission test actual owned database", connection.Database == expectedDatabase);
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
            }
            string suffix = ownerToken.Substring(0, 12);
            string managerId = "QAPM-" + suffix, cashierId = "QAPC-" + suffix;
            string managerName = "QA Permission Manager " + suffix, cashierName = "QA Permission Cashier " + suffix;
            string managerPassword = Guid.NewGuid().ToString("N"), cashierPassword = Guid.NewGuid().ToString("N");
            string financeBefore = PermissionOwnedSnapshot(true);
            try
            {
                SessionContext.Start("QA permission fixture bootstrap", "Manager");
                var bootstrap = new OperationActor { UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole };
                ApplicationServices.Employees.Create(new EmployeeInput {
                    EmployeeId=managerId,UserName=managerName,Age=30,Salary=0,Phone="000",Role="Manager",NewPassword=managerPassword }, bootstrap);
                ApplicationServices.Employees.Create(new EmployeeInput {
                    EmployeeId=cashierId,UserName=cashierName,Age=25,Salary=0,Phone="000",Role="Cashier",NewPassword=cashierPassword }, bootstrap);
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                AuthenticatePermissionFixture(managerName,managerPassword,managerId,"Manager");
                VerifyOwnedQaInventoryBlockedRelogin(managerName,managerPassword,managerId,ownerToken);
                VerifyAdditionalOwnedQaInventoryBlockedReads(managerName,managerPassword,managerId,ownerToken);
                VerifyOwnedQaCustomerReadSession(managerName,managerPassword,managerId,ownerToken);
                VerifyOwnedQaSupplierReadSession(managerName,managerPassword,managerId,ownerToken);
                VerifyOwnedQaMedicineSession(managerName,managerPassword,managerId,ownerToken);
                VerifyOwnedQaAccountingReadSession(managerName,managerPassword,managerId,ownerToken);
                SavePermissionEditorFixture(cashierId,cashierName,false);
                var deniedSnapshot = ApplicationServices.EmployeePermissions.GetByEmployeeId(cashierId);
                AssertAllowed("Persisted screen and operation overrides", deniedSnapshot.IsAvailable && deniedSnapshot.Overrides.Count==3 &&
                    deniedSnapshot.Overrides["Screen.Sales"] && !deniedSnapshot.Overrides["Screen.Purchases"] && !deniedSnapshot.Overrides["PostSalesInvoice"]);

                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                AssertAllowed("Cashier permitted sales screen after real authentication", PermissionService.CanOpen(AppPermission.Sales));
                AssertDenied("Cashier denied purchases screen after real authentication", PermissionService.CanOpen(AppPermission.Purchases));
                AssertDenied("Cashier explicit sales posting deny", PermissionService.CanExecute(AppOperation.PostSalesInvoice));
                VerifyAuthenticatedCashierPosReference(cashierName,cashierId);
                VerifyPersistedQuickSearchAccess(true);
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                string stateBeforeDeniedCalls = PermissionOwnedSnapshot(false);
                var cashierActor = new OperationActor { UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole };
                var forgedManager = new OperationActor { UserName=managerName,UserRole="Manager" };
                foreach (OperationActor actor in new[] { cashierActor, forgedManager })
                {
                    ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.GetAll(actor), "QA cashier/forged manager administrative read");
                    ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Create(null, actor), "QA cashier/forged manager create");
                    ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Update(null, actor), "QA cashier/forged manager update");
                    ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Delete(cashierId, actor), "QA cashier/forged manager delete");
                    ExpectEmployeeAdministrationDenied(() => ApplicationServices.EmployeePermissions.Replace(cashierId, null, actor), "QA cashier/forged manager permissions");
                }
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.SalesInvoices.Issue(null), "QA denied sales service call");
                var synchronousReportRequest = new OperationalReportRequest(OperationalReportKind.Medicines, DateTime.Today, DateTime.Today, string.Empty);
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.OperationalReports.Load(synchronousReportRequest), "Authenticated cashier synchronous operational report denied");
                DateTime integrityFrom = DateTime.Today.AddYears(-20), integrityTo = DateTime.Today;
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.AccountingIntegrity.Load(integrityFrom, integrityTo), "Authenticated cashier synchronous accounting integrity denied");
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.CreateAccountingIntegrityReadSession().Service.LoadAsync(integrityFrom, integrityTo, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), "Authenticated cashier async accounting integrity denied");
                AssertAllowed("Denied service calls had no SQL side effects", stateBeforeDeniedCalls==PermissionOwnedSnapshot(false));

                AuthenticatePermissionFixture(managerName,managerPassword,managerId,"Manager");
                var authenticatedManagerIntegrity = ApplicationServices.CreateAccountingIntegrityReadSession();
                var managerIntegritySync = authenticatedManagerIntegrity.Service.Load(integrityFrom, integrityTo);
                var managerIntegrityAsync = authenticatedManagerIntegrity.Service.LoadAsync(integrityFrom, integrityTo, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                authenticatedManagerIntegrity.EnsureContext();
                AssertAllowed("Authenticated manager accounting integrity sync/async SQL column parity", managerIntegritySync.Columns.Count == managerIntegrityAsync.Columns.Count && managerIntegritySync.Columns.Count == 10);
                for (int integrityColumn = 0; integrityColumn < managerIntegritySync.Columns.Count; integrityColumn++)
                    AssertAllowed("Accounting integrity SQL column name/type " + integrityColumn, managerIntegritySync.Columns[integrityColumn].Name == managerIntegrityAsync.Columns[integrityColumn].Name && managerIntegritySync.Columns[integrityColumn].DataType == managerIntegrityAsync.Columns[integrityColumn].DataType);
                AssertAllowed("Accounting integrity SQL row count parity", managerIntegritySync.Rows.Count == managerIntegrityAsync.Rows.Count);
                for (int integrityRow = 0; integrityRow < managerIntegritySync.Rows.Count; integrityRow++)
                    for (int integrityColumn = 0; integrityColumn < managerIntegritySync.Columns.Count; integrityColumn++)
                        AssertAllowed("Accounting integrity raw SQL cell parity", Equals(managerIntegritySync.Rows[integrityRow][integrityColumn], managerIntegrityAsync.Rows[integrityRow][integrityColumn]));
                Console.WriteLine("PASS owned authenticated accounting integrity sync/async SQL parity; rows=" + managerIntegrityAsync.Rows.Count + "; no financial repair.");
                VerifyOwnedAccountingIntegrityCancellation(ownerToken);
                var authenticatedManagerReport = ApplicationServices.OperationalReports;
                var managerReportResult = authenticatedManagerReport.Load(synchronousReportRequest);
                using (var reportCountConnection = Database.CreateConnection())
                using (var reportCount = reportCountConnection.CreateCommand())
                {
                    reportCountConnection.Open();
                    reportCount.CommandText = "SELECT COUNT_BIG(*) FROM dbo.MedicineTb1";
                    long expectedReportRows = Math.Min(OperationalReportRequest.MaximumRows, Convert.ToInt64(reportCount.ExecuteScalar()));
                    AssertAllowed("Authenticated manager synchronous report reaches owned SQL with expected medicine row count",
                        managerReportResult.Columns.Count > 0 && managerReportResult.Rows.Count == expectedReportRows);
                }
                SavePermissionEditorFixture(cashierId,cashierName,false,false,false);
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                ExpectEmployeeAdministrationDenied(() => authenticatedManagerReport.Load(synchronousReportRequest), "Captured manager synchronous report denied after real cashier authentication");
                ExpectEmployeeAdministrationDenied(() => authenticatedManagerIntegrity.Service.LoadAsync(integrityFrom, integrityTo, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), "Captured manager accounting integrity denied after real cashier authentication");
                ExpectEmployeeAdministrationDenied(() => authenticatedManagerIntegrity.EnsureContext(), "Captured manager accounting integrity UI continuation denied after real cashier authentication");
                AssertDenied("Persisted sales deny",PermissionService.CanOpen(AppPermission.Sales));
                AssertDenied("Persisted purchases deny",PermissionService.CanOpen(AppPermission.Purchases));
                VerifyPersistedQuickSearchAccess(false);
                AuthenticatePermissionFixture(managerName,managerPassword,managerId,"Manager");
                SavePermissionEditorFixture(cashierId,cashierName,false,false,true);
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                AssertDenied("Purchase-only persisted sales deny",PermissionService.CanOpen(AppPermission.Sales));
                AssertAllowed("Purchase-only persisted purchases grant",PermissionService.CanOpen(AppPermission.Purchases));
                VerifyPersistedQuickSearchAccess(true);
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                var previousSearchSession=ApplicationServices.CreateMedicineQuickSearchSession();
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                AssertDenied("Real SQL reauthentication does not revive old search session",previousSearchSession.IsCurrent);
                RequireQuickSearchDenied(previousSearchSession);
                VerifyPersistedQuickSearchAccess(true);

                AuthenticatePermissionFixture(managerName,managerPassword,managerId,"Manager");
                SavePermissionEditorFixture(cashierId,cashierName,true);
                AuthenticatePermissionFixture(cashierName,cashierPassword,cashierId,"Cashier");
                AssertAllowed("Saved sales posting grant after reauthentication", PermissionService.CanExecute(AppOperation.PostSalesInvoice));
                AssertDenied("Unchanged purchases screen deny after reauthentication", PermissionService.CanOpen(AppPermission.Purchases));
                bool reachedValidation=false;
                try { ApplicationServices.SalesInvoices.Issue(null); }
                catch (ArgumentNullException) { reachedValidation=true; }
                AssertAllowed("Allowed sales service reaches validation", reachedValidation);
                SessionContext.Clear();
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.EmployeePermissions.Replace(cashierId,null,forgedManager), "QA signed-out permission administration");
                AssertAllowed("Permission changes do not alter financial or stock snapshots", financeBefore==PermissionOwnedSnapshot(true));
                Console.WriteLine("PASS: actual permission editor saved screen/operation denies and grants; manager/cashier authentication and reauthentication loaded persisted overrides.");
                Console.WriteLine("PASS: cashier, forged manager and signed-out administration rejected inside actual employee/permission services; denied sales service had no SQL effect; granted sales reached validation; finances/stock unchanged.");
                Console.WriteLine("PASS: administrative employee reads denied to cashier/forged manager; actual POS displayed only authenticated cashier identity without fetching full employee directory.");
                Console.WriteLine("UI scope: permission form and save button exercised; login used the same authentication service as Login, without native keyboard/file dialogs.");
            }
            finally { SessionContext.Clear(); }
        }

        private static void VerifyOwnedQaInventoryBlockedRelogin(string user,string password,string employeeId,string ownerToken)
        {
            string owned=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken),before=PermissionOwnedSnapshot(true);
            var request=new InventoryStockReadRequest(InventoryStockReadMode.Basic,"",true);
            var original=ApplicationServices.CreateInventoryStockReadSession();
            var initial=original.LoadAsync(request,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            using(var blocker=Database.CreateConnection()) using(var observer=Database.CreateConnection())
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                blocker.Open();observer.Open();
                AssertAllowed("Owned QA blocker/observer databases",blocker.Database==owned&&observer.Database==owned);
                using(var command=observer.CreateCommand())
                {command.CommandText="SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id=DB_ID()";AssertDenied("Lock test requires locking read committed",Convert.ToBoolean(command.ExecuteScalar()));}
                int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                System.Data.SqlClient.SqlTransaction transaction=null;
                System.Threading.Tasks.Task<OperationalReportResult> pending=null;
                try
                {
                    transaction=blocker.BeginTransaction();
                    using(var command=blocker.CreateCommand())
                    {command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)";command.CommandTimeout=10;command.ExecuteScalar();}
                    pending=original.LoadAsync(request,cancellation.Token);
                    bool observed=false;DateTime deadline=DateTime.UtcNow.AddSeconds(8);
                    while(DateTime.UtcNow<deadline)
                    {
                        if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){observed=true;break;}
                        if(pending.IsCompleted)break;System.Threading.Thread.Sleep(20);
                    }
                    AssertAllowed("Actual QA SQL stock read observed waiting",observed&&!pending.IsCompleted);
                    AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                    transaction.Rollback();transaction.Dispose();transaction=null;
                    deadline=DateTime.UtcNow.AddSeconds(10);
                    while(!pending.IsCompleted&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(10);
                    AssertAllowed("Released stock SQL read completed",pending.IsCompleted);
                    bool rejected=false;
                    try{pending.GetAwaiter().GetResult();}
                    catch(UnauthorizedAccessException exception){rejected=exception.Message=="تغيرت صلاحية قراءة المخزون أثناء الطلب.";if(!rejected)throw;}
                    AssertAllowed("Actual in-flight SQL result denied after same-user login",rejected);
                    var fresh=ApplicationServices.CreateInventoryStockReadSession();
                    var result=fresh.LoadAsync(request,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    AssertAllowed("New authenticated session reads unchanged stock shape",result.Rows.Count==initial.Rows.Count&&result.Columns.Count==initial.Columns.Count);
                    for(int row=0;row<initial.Rows.Count;row++)
                    {
                        AssertAllowed("Fresh stock row has same field count",result.Rows[row].Count==initial.Rows[row].Count);
                        for(int column=0;column<initial.Rows[row].Count;column++)
                            AssertAllowed("Fresh stock cell preserves actual read value",object.Equals(result.Rows[row][column],initial.Rows[row][column]));
                    }
                    AssertAllowed("No blocked SQL requests remain",CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                    Console.WriteLine("PASS live QA inventory revision: LCK_M wait observed, real same-user SQL reauthentication, released result rejected at post-read authorization, fresh Rows="+result.Rows.Count+", no blocked requests, no stock/financial writes or schema/options changes.");
                }
                finally
                {
                    cancellation.Cancel();
                    if(transaction!=null){try{transaction.Rollback();}finally{transaction.Dispose();}}
                    if(pending!=null&&!pending.IsCompleted){var deadline=DateTime.UtcNow.AddSeconds(5);while(!pending.IsCompleted&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(10);}
                }
            }
            AssertAllowed("Blocked read/relogin preserves financial and medicine rows",before==PermissionOwnedSnapshot(true));
        }

        private static void VerifyAdditionalOwnedQaInventoryBlockedReads(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string medicine;
            using(var connection=Database.CreateConnection())
            using(var command=new System.Data.SqlClient.SqlCommand("SELECT TOP(1) MedName FROM dbo.MedicineTb1 ORDER BY MedName",connection))
            {connection.Open();medicine=Convert.ToString(command.ExecuteScalar());}
            AssertAllowed("Inventory read fixture has medicine",!string.IsNullOrWhiteSpace(medicine));
            DateTime from=new DateTime(2000,1,1),to=DateTime.Today;
            VerifyOwnedQaBlockedRead("CommercialLots","InventoryLotsTb",()=>{
                var session=ApplicationServices.CreateInventoryStockReadSession();
                return token=>BoxInventoryQaRead(session.LoadAsync(new InventoryStockReadRequest(InventoryStockReadMode.CommercialLots,"",true),token));
            },"تغيرت صلاحية قراءة المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("InventoryAlerts","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryAlertReadSession();
                return token=>BoxInventoryQaRead(session.LoadAsync(10,30,token));
            },"تغيرت صلاحية قراءة تنبيهات المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("InvoiceAlerts","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInvoiceInventoryAlertReadSession();
                return token=>BoxInventoryQaRead(session.LoadAsync(10,30,new[]{medicine},token));
            },"تغيرت صلاحية قراءة تنبيهات المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("MovementSummary","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryOperationalReportReadSession();
                return token=>BoxInventoryQaRead(session.LoadMovementSummaryAsync(new InventoryMovementSummaryRequest(from,to,""),token));
            },"تغيرت صلاحية قراءة تقارير المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("Turnover","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryOperationalReportReadSession();
                return token=>BoxInventoryQaRead(session.LoadTurnoverAsync(new InventoryTurnoverRequest(from,to,30),token));
            },"تغيرت صلاحية قراءة تقارير المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("Integrity","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryIntegrityReadSession();
                return token=>BoxInventoryQaRead(session.LoadAsync(token));
            },"تغيرت صلاحية فحص المخزون أثناء الطلب.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("PostingMedicineLookup","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryPostingSession();
                return token=>BoxInventoryQaRead(session.LoadMedicinesAsync(token));
            },"لا تملك صلاحية قراءة حركة المخزون.",user,password,employeeId,ownerToken);
            VerifyOwnedQaBlockedRead("PostingSnapshot","MedicineTb1",()=>{
                var session=ApplicationServices.CreateInventoryPostingSession();
                return token=>BoxInventoryQaRead(session.LoadSnapshotAsync(new InventoryOperationReadRequest(medicine,from,to),token));
            },"لا تملك صلاحية قراءة حركة المخزون.",user,password,employeeId,ownerToken);
        }
        private static async System.Threading.Tasks.Task<object> BoxInventoryQaRead<T>(System.Threading.Tasks.Task<T> task)
        {return await task.ConfigureAwait(false);}

        // Compare the complete public data contract, including column metadata and every cell.
        // Only application DTOs, collections and value types are accepted; no session/service reflection.
        private static string InventoryQaDataSnapshot(object value)
        {
            if(value==null)return "null";
            if(value==DBNull.Value)return "DBNull";
            if(value is System.Data.DataTable table)
                return "DataTable:"+InventoryQaDataSnapshot(table.Columns.Cast<System.Data.DataColumn>().Select(column=>new object[]{column.ColumnName,column.DataType,column.AllowDBNull,column.MaxLength}))+InventoryQaDataSnapshot(table.Rows.Cast<System.Data.DataRow>().Select(row=>row.ItemArray));
            if(value is Type type)return "Type:"+type.FullName;
            Type actual=value.GetType();
            if(value is string text)return "String:"+text.Length+":"+text;
            if(value is DateTime date)return "DateTime:"+date.ToString("O",System.Globalization.CultureInfo.InvariantCulture);
            if(actual.IsPrimitive||actual.IsEnum||value is decimal||value is Guid)
                return actual.FullName+":"+Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
            if(value is System.Collections.IEnumerable sequence)
            {
                var builder=new System.Text.StringBuilder("[");
                foreach(object item in sequence){string entry=InventoryQaDataSnapshot(item);builder.Append(entry.Length).Append(':').Append(entry);}
                return builder.Append(']').ToString();
            }
            if(actual.Assembly!=typeof(InventoryOperationReadSnapshot).Assembly)
                throw new InvalidOperationException("Unexpected inventory QA data contract: "+actual.FullName);
            var result=new System.Text.StringBuilder(actual.FullName+"{");
            foreach(var property in actual.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance).OrderBy(property=>property.Name,StringComparer.Ordinal))
            {
                if(property.GetIndexParameters().Length!=0)throw new InvalidOperationException("Unexpected indexed QA DTO.");
                string entry=InventoryQaDataSnapshot(property.GetValue(value,null));
                result.Append(property.Name).Append('=').Append(entry.Length).Append(':').Append(entry);
            }
            return result.Append('}').ToString();
        }
        private static void VerifyOwnedAccountingIntegrityCancellation(string ownerToken)
        {
            string owned = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken), before = PermissionOwnedSnapshot(true);
            var session = ApplicationServices.CreateAccountingIntegrityReadSession();
            using (var blocker = Database.CreateConnection())
            using (var observer = Database.CreateConnection())
            using (var cancellation = new System.Threading.CancellationTokenSource())
            {
                blocker.Open(); observer.Open();
                AssertAllowed("Accounting integrity owned blocker/observer", blocker.Database == owned && observer.Database == owned);
                int blockerId;
                using (var command = blocker.CreateCommand()) { command.CommandText = "SELECT @@SPID"; blockerId = Convert.ToInt32(command.ExecuteScalar()); }
                using (var transaction = blocker.BeginTransaction())
                {
                    using (var command = blocker.CreateCommand())
                    { command.Transaction = transaction; command.CommandText = "SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WITH(TABLOCKX,HOLDLOCK)"; command.CommandTimeout = 10; command.ExecuteScalar(); }
                    var pending = session.Service.LoadAsync(DateTime.Today.AddYears(-20), DateTime.Today, cancellation.Token);
                    bool observed = false; DateTime deadline = DateTime.UtcNow.AddSeconds(8);
                    while (!pending.IsCompleted && DateTime.UtcNow < deadline)
                    { if (CountOwnedQaBlockedRequests(observer, blockerId, true) > 0) { observed = true; break; } System.Threading.Thread.Sleep(20); }
                    AssertAllowed("Accounting integrity actual SQL lock wait", observed && !pending.IsCompleted);
                    cancellation.Cancel(); deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!pending.IsCompleted && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(10);
                    AssertAllowed("Accounting integrity cancellation completed before blocker rollback", pending.IsCompleted);
                    bool canceled = false; try { pending.GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
                    AssertAllowed("Accounting integrity SQL canceled result", canceled);
                    AssertAllowed("Accounting integrity no remaining blocked SQL", CountOwnedQaBlockedRequests(observer, blockerId, false) == 0);
                    transaction.Rollback();
                }
            }
            AssertAllowed("Accounting integrity cancellation leaves financial rows unchanged", before == PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned accounting integrity live SQL cancellation: observed LCK_M wait, canceled before lock release, no remaining blocked requests, financial rows unchanged.");
        }
        private static void VerifyOwnedQaBlockedRead(string name,string table,
            Func<Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<object>>> capture,string expectedError,
            string user,string password,string employeeId,string ownerToken)
        {
            string owned=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken),before=PermissionOwnedSnapshot(true);
            if(table!="MedicineTb1"&&table!="InventoryLotsTb"&&table!="CustomersBillsTb"&&table!="CompanyTb1"&&table!="AccountsTb"&&table!="CostCentersTb"&&table!="JournalEntriesTb"&&table!="CurrencySettingsTb"&&table!="JournalEntryLinesTb"&&table!="AccountingVouchersTb")throw new InvalidOperationException("QA lock table is not permitted.");
            var original=capture();
            string initial=InventoryQaDataSnapshot(original(System.Threading.CancellationToken.None).GetAwaiter().GetResult());
            using(var blocker=Database.CreateConnection()) using(var observer=Database.CreateConnection())
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                blocker.Open();observer.Open();
                AssertAllowed(name+" owned blocker/observer",blocker.Database==owned&&observer.Database==owned);
                using(var command=observer.CreateCommand())
                {command.CommandText="SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id=DB_ID()";AssertDenied(name+" locking read committed",Convert.ToBoolean(command.ExecuteScalar()));}
                int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                System.Data.SqlClient.SqlTransaction transaction=null;
                System.Threading.Tasks.Task<object> pending=null;
                try
                {
                    transaction=blocker.BeginTransaction();
                    using(var command=blocker.CreateCommand())
                    {command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";command.CommandTimeout=10;command.ExecuteScalar();}
                    pending=original(cancellation.Token);
                    bool observed=false;DateTime deadline=DateTime.UtcNow.AddSeconds(8);
                    while(DateTime.UtcNow<deadline)
                    {if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){observed=true;break;}if(pending.IsCompleted)break;System.Threading.Thread.Sleep(20);}
                    AssertAllowed(name+" actual SQL lock wait",observed&&!pending.IsCompleted);
                    AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                    transaction.Rollback();transaction.Dispose();transaction=null;
                    deadline=DateTime.UtcNow.AddSeconds(10);
                    while(!pending.IsCompleted&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(10);
                    AssertAllowed(name+" released read completed",pending.IsCompleted);
                    bool rejected=false;
                    try{pending.GetAwaiter().GetResult();}
                    catch(UnauthorizedAccessException error){rejected=error.Message==expectedError;if(!rejected)throw;}
                    AssertAllowed(name+" old result denied at post-read authorization",rejected);
                    string fresh=InventoryQaDataSnapshot(capture()(System.Threading.CancellationToken.None).GetAwaiter().GetResult());
                    AssertAllowed(name+" fresh full data parity",fresh==initial);
                    AssertAllowed(name+" no blocked SQL requests",CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                    Console.WriteLine("PASS live QA inventory revision "+name+": LCK_M wait, SQL same-user reauthentication, old result rejected, fresh full DTO parity, no blocked requests.");
                }
                finally
                {
                    cancellation.Cancel();
                    if(transaction!=null){try{transaction.Rollback();}finally{transaction.Dispose();}}
                    if(pending!=null&&!pending.IsCompleted){var deadline=DateTime.UtcNow.AddSeconds(5);while(!pending.IsCompleted&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(10);}
                }
            }
            AssertAllowed(name+" medicine/financial state unchanged",before==PermissionOwnedSnapshot(true));
        }


        private sealed class OwnedQaQueuedContext:System.Threading.SynchronizationContext
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<Tuple<System.Threading.SendOrPostCallback,object>> queue=new System.Collections.Concurrent.ConcurrentQueue<Tuple<System.Threading.SendOrPostCallback,object>>();
            internal readonly System.Threading.ManualResetEventSlim Posted=new System.Threading.ManualResetEventSlim();
            public override void Post(System.Threading.SendOrPostCallback action,object state){queue.Enqueue(Tuple.Create(action,state));Posted.Set();}
            internal void Drain(){Tuple<System.Threading.SendOrPostCallback,object> item;while(queue.TryDequeue(out item))item.Item1(item.Item2);}
        }
        private static void VerifyOwnedQaAccountingQueuedContinuation(string user,string password,string employeeId,string ownerToken)
        {
            string owned=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken),before=PermissionOwnedSnapshot(true);
            var originalContext=System.Threading.SynchronizationContext.Current;
            var queued=new OwnedQaQueuedContext();
            System.Threading.Tasks.Task<System.Data.DataTable> pending=null;
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                System.Data.SqlClient.SqlTransaction transaction=null;
                try
                {
                    blocker.Open();observer.Open();AssertAllowed("Queued accounting read uses owned database",blocker.Database==owned&&observer.Database==owned);
                    int blockerId;using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                    transaction=blocker.BeginTransaction();
                    using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.AccountsTb WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                    System.Threading.SynchronizationContext.SetSynchronizationContext(queued);
                    pending=AccountingLookupDesktopService.LoadAccountsAsync(false,cancellation.Token);
                    var deadline=DateTime.UtcNow.AddSeconds(10);bool observed=false;
                    while(!observed){observed=CountOwnedQaBlockedRequests(observer,blockerId,true)>0;if(observed)break;if(pending.IsCompleted||DateTime.UtcNow>deadline)throw new InvalidOperationException("Queued accounting read did not reach SQL lock wait.");System.Threading.Thread.Sleep(10);}
                    transaction.Rollback();transaction.Dispose();transaction=null;
                    AssertAllowed("Accounting SQL reply posted without pumping UI continuation",queued.Posted.Wait(TimeSpan.FromSeconds(10))&&!pending.IsCompleted);
                    AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                    queued.Drain();
                    bool denied=false;
                    try{var rows=pending.GetAwaiter().GetResult();rows.Dispose();}catch(UnauthorizedAccessException error){denied=error.Message=="لا تملك صلاحية قراءة خيارات المحاسبة.";if(!denied)throw;}
                    AssertAllowed("Accounting facade rejects queued response after real reauthentication",denied);
                    AssertAllowed("Queued accounting read leaves no blocked requests",CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                }
                finally
                {
                    cancellation.Cancel();
                    if(transaction!=null){try{transaction.Rollback();}finally{transaction.Dispose();}}
                    var deadline=DateTime.UtcNow.AddSeconds(5);
                    while(pending!=null&&!pending.IsCompleted&&DateTime.UtcNow<deadline){queued.Drain();System.Threading.Thread.Sleep(5);}
                    queued.Drain();
                    System.Threading.SynchronizationContext.SetSynchronizationContext(originalContext);queued.Posted.Dispose();
                }
            }
            AssertAllowed("Queued accounting continuation keeps financial/catalog state",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS live QA queued accounting reply: SQL lock observed/released, worker response queued before SQL same-user authentication, stale UI continuation denied, no blocked requests or financial writes; no native input.");
        }

        private static void VerifyOwnedQaChartSession(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Action<System.Threading.Tasks.Task> finish=task=>{
                var deadline=DateTime.UtcNow.AddSeconds(20);
                while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA chart read timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                task.GetAwaiter().GetResult();
            };
            var expected=AccountingLookupDesktopService.LoadAccounts(false);
            using(var form=new ChartOfAccountsForm(false))
            {
                IntPtr handle=form.Handle;finish(form.RefreshChartAsync());
                var tree=(TreeView)form.Controls.Find("accountsTree",true)[0];
                var code=(AntdUI.Input)form.Controls.Find("chartAccountCode",true)[0];
                var name=(TextBox)form.Controls.Find("chartAccountName",true)[0];
                AssertAllowed("Chart full SQL tree record count",tree.GetNodeCount(true)==expected.Rows.Count&&expected.Rows.Count>0);
                TreeNode retained=tree.Nodes[0];typeof(ChartOfAccountsForm).GetMethod("SelectAccountFromNode",flags).Invoke(form,new object[]{retained});
                AssertAllowed("Chart real SQL row selects editor",code.Text==Convert.ToString(((System.Data.DataRow)retained.Tag)["AccountCode"]));
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                typeof(ChartOfAccountsForm).GetMethod("SelectAccountFromNode",flags).Invoke(form,new object[]{retained});
                AssertAllowed("Chart real SQL relogin clears retained tree/editor",tree.GetNodeCount(true)==0&&code.Text.Length==0&&name.Text.Length==0);
                typeof(ChartOfAccountsForm).GetMethod("SaveAccount",flags).Invoke(form,null);
                typeof(ChartOfAccountsForm).GetMethod("DeleteAccount",flags).Invoke(form,null);
                finish(form.RefreshChartAsync());
                AssertAllowed("Chart real SQL fresh recovery",tree.GetNodeCount(true)==expected.Rows.Count&&code.Text.Length==0&&name.Text.Length==0);
                tree.SelectedNode=tree.Nodes[0];
                typeof(ChartOfAccountsForm).GetMethod("SelectAccountFromNode",flags).Invoke(form,new object[]{tree.Nodes[0]});
                AssertAllowed("Chart confirmation has a selected SQL account",code.Text.Length>0);
                var oldWriter=ApplicationServices.CreateAccountWriteSession();
                AssertAllowed("Owned chart manager initially allowed",oldWriter.IsCurrent);
                bool confirmed=false;
                form.ChartDeleteConfirmation=()=>{confirmed=true;AuthenticatePermissionFixture(user,password,employeeId,"Manager");return true;};
                typeof(ChartOfAccountsForm).GetMethod("DeleteAccount",flags).Invoke(form,null);
                AssertAllowed("Chart confirmation relogin blocks delete before SQL",confirmed&&tree.GetNodeCount(true)==0&&code.Text.Length==0);
                ExpectEmployeeAdministrationDenied(()=>oldWriter.Service.Save(new AccountSaveRequest("FORBIDDEN-QA","ممنوع","Asset",null,true),oldWriter.Actor),"Old chart writer save session rejected");
                ExpectEmployeeAdministrationDenied(()=>oldWriter.Service.Delete("FORBIDDEN-QA",oldWriter.Actor),"Old chart writer delete session rejected");
                finish(form.RefreshChartAsync());
                AssertAllowed("Chart recovery after confirmation rejection",tree.GetNodeCount(true)==expected.Rows.Count&&code.Text.Length==0);
            }
            expected.Dispose();AssertAllowed("Chart relogin/confirmation test does not change accounts/catalog/finances",before==PermissionOwnedSnapshot(true));
            string accountCode="QAC"+ownerToken.Substring(0,12);
            var saved=AccountWritingDesktopService.SaveAsync(new AccountSaveRequest(accountCode,"صندوق QA Cash 500 mg","Asset",null,true));finish(saved);
            AssertAllowed("Pinned account writer async create accepted",saved.Result.Changed&&saved.Result.Code==accountCode);
            var changed=AccountWritingDesktopService.Save(new AccountSaveRequest(accountCode,"صندوق QA معدل Cash 500 mg","Asset",null,false));
            AssertAllowed("Pinned account writer sync update accepted",changed.Changed&&changed.Code==accountCode);
            using(var actual=AccountingLookupDesktopService.LoadAccounts(false))
            {
                var row=actual.Rows.Cast<System.Data.DataRow>().SingleOrDefault(x=>Convert.ToString(x["AccountCode"])==accountCode);
                AssertAllowed("Pinned account writer raw mixed name/status persisted",row!=null&&Convert.ToString(row["AccountName"])=="صندوق QA معدل Cash 500 mg"&&!Convert.ToBoolean(row["IsActive"]));
            }
            var deleted=AccountWritingDesktopService.DeleteAsync(accountCode);finish(deleted);
            AssertAllowed("Pinned account writer async delete accepted",deleted.Result.Changed&&deleted.Result.Code==accountCode);
            AssertAllowed("Pinned account writer create/update/delete restores financial/catalog snapshot",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS live QA pinned account writer: async create, sync update, SQL mixed Arabic/name and inactive status parity, async delete; 3 expected audits, financial/catalog snapshot restored.");
            Console.WriteLine("PASS live QA chart: full SQL tree binding, retained row/editor rejection after SQL relogin, fresh empty-editor recovery, confirmation SQL relogin blocks delete, pinned old writer save/delete denied; no account writes/native input.");
        }

        private static void VerifyOwnedQaVoucherDocumentRows(string ownerToken,string marker)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            using(var raw=new System.Data.DataTable())
            {
                using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT VoucherId,VoucherDate,VoucherType,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy FROM dbo.AccountingVouchersTb WHERE Notes=@Marker ORDER BY VoucherId";
                    command.Parameters.Add("@Marker",System.Data.SqlDbType.NVarChar,500).Value=marker;
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command))adapter.Fill(raw);
                }
                AssertAllowed("Owned saved voucher nonempty reference",raw.Rows.Count==2);
                Func<VoucherDocumentSnapshot,System.Data.DataRow,bool> matches=(doc,row)=>doc!=null&&doc.VoucherId==Convert.ToInt32(row[0])&&doc.Date==Convert.ToDateTime(row[1])&&doc.Type==Convert.ToString(row[2])&&
                    doc.PartyName==Convert.ToString(row[3])&&doc.AccountCode==Convert.ToString(row[4])&&doc.Amount==Convert.ToDecimal(row[5])&&doc.Notes==Convert.ToString(row[6])&&
                    doc.JournalEntryId==(row.IsNull(7)?(int?)null:Convert.ToInt32(row[7]))&&doc.CreatedBy==Convert.ToString(row[8]);
                var session=ApplicationServices.CreateVoucherDocumentReadSession();
                foreach(System.Data.DataRow row in raw.Rows)
                {
                    int id=Convert.ToInt32(row[0]);
                    var snapshot=session.Service.LoadAsync(new VoucherDocumentReadRequest(id),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    AssertAllowed("Owned saved document nine raw field parity "+id,matches(snapshot,row));
                    using(var form=new ReceiptVoucherForm("ReceiptExternal",true))
                    {
                        form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                        form.Controls.Find("receiptVoucherPartyName",true).OfType<System.Windows.Forms.TextBox>().Single().Text="مسودة مختلفة";
                        form.Controls.Find("receiptVoucherAmount",true).OfType<AntdUI.Input>().Single().Text="999.99";
                        int presentations=0;var reading=form.OpenVoucherPrintAsync(id,doc=>{presentations++;AssertAllowed("Owned actual preview-opening saved DTO parity",matches(doc,row));return true;});
                        var watch=System.Diagnostics.Stopwatch.StartNew();while(!reading.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned saved voucher UI did not finish.");}
                        AssertAllowed("Owned actual saved voucher preview callback once",reading.GetAwaiter().GetResult()&&presentations==1);form.Close();
                    }
                }
                var request=new VoucherDocumentReadRequest(Convert.ToInt32(raw.Rows[0][0]));
                ExpectEmployeeAdministrationDenied(()=>session.Service.LoadAsync(request,new OperationActor{UserName="forged",UserRole="Manager"},System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"Owned saved voucher forged actor");
                using(var cancellation=new System.Threading.CancellationTokenSource())
                {
                    cancellation.Cancel();bool rejected=false;try{session.Service.LoadAsync(request,session.Actor,cancellation.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}
                    AssertAllowed("Owned saved voucher precancelled request",rejected);
                }
                bool missing=false;try{session.Service.LoadAsync(new VoucherDocumentReadRequest(int.MaxValue),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){missing=true;}
                AssertAllowed("Owned saved voucher missing row no fallback",missing);
            }
            AssertAllowed("Owned saved voucher nonempty reads no financial effect",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned saved voucher nonempty SQL/root/UI: two existing temporary rows, nine fields independently matched including mixed Arabic/NULL/timestamp/decimal; callback uses saved DTO despite different draft; forged/pre-cancelled/missing rejected. No native preview or new fixture rows.");
        }

        private static void VerifyOwnedQaVoucherCurrencyGuard(string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            bool forwarded=false;
            try{AccountingService.CreateVoucher("ReceiptExternal",DateTime.Today,"QA no write","1100",0m,null,AccountingService.CashAccount,null,"Y3R");}
            catch(InvalidOperationException error){forwarded=error.Message.Contains("رمز عملة المسودة");}
            AssertAllowed("Owned compatibility wrapper forwards explicit currency before amount validation without SQL write",forwarded);
            Console.WriteLine("PASS owned voucher compatibility wrapper: explicit invalid currency reaches domain validation before zero-amount rejection; no SQL posting.");
            using(var connection=Database.CreateConnection())
            {
                connection.Open();var codes=new List<string>();string currencyCode;
                using(var command=connection.CreateCommand())
                {command.CommandText="SELECT TOP 2 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode";using(var reader=command.ExecuteReader())while(reader.Read())codes.Add(reader.GetString(0));}
                AssertAllowed("Owned voucher currency active account fixture",codes.Count==2);
                using(var command=connection.CreateCommand())
                {command.CommandText="SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1";using(var reader=command.ExecuteReader()){AssertAllowed("Owned currency unique system fixture",reader.Read());currencyCode=reader.GetString(0).Trim();AssertAllowed("Owned currency unique system fixture count",!reader.Read());}}
                using(var form=new VoucherForm("ReceiptExternal",true))
                {
                    IntPtr handle=form.Handle;var loading=form.LoadVoucherOptionsAsync();var watch=System.Diagnostics.Stopwatch.StartNew();
                    while(!loading.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned short currency read did not finish.");}
                    AssertAllowed("Owned short currency actual root/UI read",loading.GetAwaiter().GetResult()&&form.CreateVoucherRequest(DateTime.Today,10m,codes[1]).ExpectedSystemCurrencyCode==currencyCode.ToUpperInvariant());
                }
                foreach(string scenario in new[]{"matching","mismatched","changed","missing"})
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        if(scenario=="changed"||scenario=="missing")
                        using(var command=connection.CreateCommand())
                        {command.Transaction=transaction;command.CommandText=scenario=="missing"?"UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=0 WHERE IsSystemCurrency=1":"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE IsSystemCurrency=1";command.Parameters.Add("@Code",System.Data.SqlDbType.NVarChar,10).Value=currencyCode=="ZZZ"?"YYY":"ZZZ";command.ExecuteNonQuery();}
                        var prepared=PharmacyManagement.Domain.VoucherPreparation.Prepare("ReceiptExternal","QA currency Arabic",codes[1],codes[0],10.005m,"QA rollback only",null,scenario=="mismatched"?(currencyCode=="ZZZ"?"YYY":"ZZZ"):currencyCode);
                        bool rejected=false;VoucherPostResult result=null;
                        try{result=PharmacyManagement.Infrastructure.SqlServer.SqlVoucherPostRepository.PostInTransaction(connection,transaction,new DateTime(1900,1,1),prepared,new OperationActor{UserName="QA-Currency",UserRole="Manager"});}catch(InvalidOperationException){rejected=true;}
                        AssertAllowed("Owned currency transaction guard "+scenario,scenario=="matching"?!rejected&&result!=null&&result.VoucherId>0&&result.JournalEntryId>0:rejected&&result==null);
                        if(scenario=="matching")using(var command=connection.CreateCommand())
                        {command.Transaction=transaction;command.CommandText="SELECT Amount FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id";command.Parameters.Add("@Id",System.Data.SqlDbType.Int).Value=result.VoucherId;AssertAllowed("Owned currency precision unchanged",Convert.ToDecimal(command.ExecuteScalar())==10m);}
                    }
                    finally{transaction.Rollback();}
                }
            }
            AssertAllowed("Owned voucher currency all writes rolled back",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned voucher draft currency SQL transaction guard: short form root/UI currency matched; matching rounded amount, mismatched draft, changed system code, missing system currency; all writes rollback, financial/currency snapshot unchanged. No concurrent writer or durable commit acceptance.");
        }

        private static void VerifyOwnedQaVoucherDocumentLifetime(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            Action<System.Threading.Tasks.Task> finish=task=>{
                var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned saved voucher lifecycle did not finish.");}task.GetAwaiter().GetResult();
            };
            foreach(bool close in new[]{false,true})
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())using(var form=new ReceiptVoucherForm("ReceiptExternal",true))
            {
                blocker.Open();observer.Open();int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                using(var transaction=blocker.BeginTransaction())
                {
                    bool released=false;
                    try
                    {
                        using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.AccountingVouchersTb WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                        form.Controls.Find("receiptVoucherPartyName",true).OfType<System.Windows.Forms.TextBox>().Single().Text="جلسة سابقة Pharmacy";
                        form.Controls.Find("receiptVoucherAmount",true).OfType<AntdUI.Input>().Single().Text="1250.50";
                        form.Controls.Find("receiptVoucherNotes",true).OfType<System.Windows.Forms.TextBox>().Single().Text="مسودة قديمة";
                        int presentations=0;var reading=form.OpenVoucherPrintAsync(int.MaxValue,doc=>{presentations++;return true;});var historyReading=form.RefreshVoucherHistoryAsync();var watch=System.Diagnostics.Stopwatch.StartNew();bool observed=false;
                        while(watch.ElapsedMilliseconds<8000){System.Windows.Forms.Application.DoEvents();if(CountOwnedQaBlockedRequests(observer,blockerId,true)>=1){observed=true;break;}if(reading.IsCompleted)break;System.Threading.Thread.Sleep(5);}
                        AssertAllowed("Owned saved voucher actual lock close="+close,observed&&!reading.IsCompleted&&!historyReading.IsCompleted);
                        if(close)
                        {
                            form.Close();watch.Restart();while((!reading.IsCompleted||!historyReading.IsCompleted)&&watch.ElapsedMilliseconds<5000){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                            AssertAllowed("Owned saved voucher close finishes under held lock",form.IsDisposed&&reading.IsCompleted&&historyReading.IsCompleted);finish(reading);finish(historyReading);
                            AssertAllowed("Owned saved voucher close no delivery/wait",!reading.Result&&presentations==0&&CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                        }
                        else
                        {
                            AuthenticatePermissionFixture(user,password,employeeId,"Manager");transaction.Rollback();released=true;finish(reading);finish(historyReading);
                            AssertAllowed("Owned history rejects old login rows and selection",form.Controls.Find("receiptVoucherHistory",true).OfType<System.Windows.Forms.DataGridView>().Single().DataSource==null&&form.SelectedReceiptHistoryText()==null);
                            AssertAllowed("Owned detailed draft cleared after actual SQL relogin",form.Controls.Find("receiptVoucherPartyName",true).OfType<System.Windows.Forms.TextBox>().Single().Text.Length==0&&form.Controls.Find("receiptVoucherAmount",true).OfType<AntdUI.Input>().Single().Text.Length==0&&form.Controls.Find("receiptVoucherNotes",true).OfType<System.Windows.Forms.TextBox>().Single().Text.Length==0);
                            AssertAllowed("Owned saved voucher same-user relogin no preview",!reading.Result&&presentations==0);
                            var session=ApplicationServices.CreateVoucherDocumentReadSession();bool missing=false;
                            try{session.Service.LoadAsync(new VoucherDocumentReadRequest(int.MaxValue),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){missing=true;}
                            AssertAllowed("Owned fresh saved voucher root authorized missing record",session.IsCurrent&&missing);
                            finish(form.LoadReceiptInitialAsync());
                            AssertAllowed("Owned saved voucher print action recovery",form.Controls.Find("receiptVoucherAction_Print_طباعة كاشير",true).OfType<AntdUI.Button>().Single().Enabled);form.Close();
                        }
                    }
                    finally{if(!released)transaction.Rollback();}
                }
                Console.WriteLine("PASS owned saved voucher AND detailed history pending under observed SQL lock: "+(close?"Form.Close cancels under held lock, no presentation or waiting request":"same-user SQL reauthentication rejects old document, fresh root and print action recover"));
            }
            AssertAllowed("Owned saved voucher lifecycle leaves financial snapshot unchanged",before==PermissionOwnedSnapshot(true));
        }

        private static void VerifyOwnedQaVoucherBalance(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            Action<System.Threading.Tasks.Task> finish=task=>{
                var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned voucher balance did not finish.");}task.GetAwaiter().GetResult();
            };
            using(var raw=new System.Data.DataTable())
            {
                var codes=new System.Collections.Generic.List<string>();
                using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT l.AccountCode,e.EntryDate,l.Debit,l.Credit FROM dbo.JournalEntryLinesTb l INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId";
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command))adapter.Fill(raw);
                    connection.Open();command.CommandText="SELECT AccountCode FROM dbo.AccountsTb ORDER BY AccountCode";
                    using(var reader=command.ExecuteReader())while(reader.Read())codes.Add(reader.GetString(0));
                }
                codes.Add("QA-no-account");
                Func<string,DateTime,decimal> expected=(account,date)=>{
                    decimal sum=0m;foreach(System.Data.DataRow row in raw.Rows)
                        if(Convert.ToString(row[0])==account&&Convert.ToDateTime(row[1]).Date<=date.Date)sum+=Convert.ToDecimal(row[2])-Convert.ToDecimal(row[3]);
                    return sum;
                };
                var session=ApplicationServices.CreateAccountBalanceReadSession();int cases=0;
                foreach(string account in codes)foreach(DateTime date in new[]{new DateTime(1753,1,1),DateTime.Today,DateTime.MaxValue.Date})
                {
                    decimal actual=session.Service.LoadAsync(new AccountBalanceReadRequest(account,date),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    AssertAllowed("Owned balance independent decimal line sum "+account+" "+date.Year,actual==expected(account,date));cases++;
                }
                ExpectEmployeeAdministrationDenied(()=>session.Service.LoadAsync(new AccountBalanceReadRequest(codes[0],DateTime.Today),new OperationActor{UserName="forged",UserRole="Manager"},System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"Owned balance forged actor");
                using(var cancellation=new System.Threading.CancellationTokenSource())
                {
                    cancellation.Cancel();bool cancelled=false;try{session.Service.LoadAsync(new AccountBalanceReadRequest(codes[0],DateTime.Today),session.Actor,cancellation.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                    AssertAllowed("Owned balance precancelled request",cancelled);
                }
                using(var form=new ReceiptVoucherForm("ReceiptExternal",true,null,false))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                    finish(form.LoadReceiptInitialAsync());finish(form.RefreshBalanceAsync());
                    var account=form.Controls.Find("receiptVoucherPartyAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                    var label=form.Controls.Find("receiptVoucherBalance",true).OfType<System.Windows.Forms.Label>().Single();
                    string code=account.Text.Split(new[]{" - "},StringSplitOptions.None)[0];
                    AssertAllowed("Owned actual voucher displayed balance parity",label.Text==expected(code,DateTime.Today).ToString("N2"));form.Close();
                }
                Console.WriteLine("PASS owned voucher balance independent decimal raw-line sum: cases="+cases+", raw lines="+raw.Rows.Count+"; actual UI parity, forged actor and precancelled denial; no SUM used in reference.");
            }
            foreach(string table in new[]{"JournalEntryLinesTb","JournalEntriesTb"})foreach(bool close in new[]{false,true})
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())using(var form=new ReceiptVoucherForm("ReceiptExternal",true,null,false))
            {
                Console.WriteLine("BEGIN owned voucher balance SQL probe "+table+" close="+close);
                blocker.Open();observer.Open();int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);finish(form.RefreshBalanceAsync());
                var label=form.Controls.Find("receiptVoucherBalance",true).OfType<System.Windows.Forms.Label>().Single();
                var account=form.Controls.Find("receiptVoucherPartyAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                using(var transaction=blocker.BeginTransaction())
                {
                    bool released=false;
                    try
                    {
                        using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                        var reading=form.RefreshBalanceAsync();var watch=System.Diagnostics.Stopwatch.StartNew();bool observed=false;
                        while(watch.ElapsedMilliseconds<8000){System.Windows.Forms.Application.DoEvents();if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){observed=true;break;}if(reading.IsCompleted)break;System.Threading.Thread.Sleep(5);}
                        AssertAllowed("Voucher balance actual SQL lock "+table+" close="+close,observed&&!reading.IsCompleted&&label.Text=="...");
                        if(close)
                        {
                            form.Close();watch.Restart();while(!reading.IsCompleted&&watch.ElapsedMilliseconds<5000){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                            AssertAllowed("Voucher balance close task finished under held lock "+table,form.IsDisposed&&reading.IsCompleted);finish(reading);
                            AssertAllowed("Voucher balance close removes SQL wait before lock release "+table,CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                        }
                        else
                        {
                            AuthenticatePermissionFixture(user,password,employeeId,"Manager");transaction.Rollback();released=true;finish(reading);
                            AssertAllowed("Voucher balance old login result rejected "+table,label.Text=="—"&&!account.Enabled);
                            finish(form.LoadReceiptInitialAsync());finish(form.RefreshBalanceAsync());
                            AssertAllowed("Voucher balance current login recovery "+table,account.Enabled&&label.Text!="..."&&label.Text!="غير متاح"&&label.Text!="—");form.Close();
                        }
                    }
                    finally{if(!released)transaction.Rollback();}
                }
                Console.WriteLine("PASS owned voucher balance "+table+": "+(close?"actual close cancellation under held lock, no waiting SQL":"same-user SQL relogin rejects old balance, current session recovers"));
            }
            AssertAllowed("Owned balance financial state unchanged",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned voucher balance pinned SQL/service/UI read parity and lifetime probes; no financial posting/native input.");
        }

        private static void VerifyOwnedQaReceiptInitialRows(string ownerToken,string marker)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            foreach(string type in new[]{"ReceiptExternal","PaymentInternal","ReceiptMulti"})
            using(var expected=new System.Data.DataTable())using(var form=new ReceiptVoucherForm(type,true))
            {
                using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
                {
                    command.CommandText=@"SELECT VoucherId,VoucherDate,CASE VoucherType WHEN N'ReceiptExternal' THEN N'قبض خارجي' WHEN N'PaymentInternal' THEN N'صرف داخلي' ELSE VoucherType END,
ISNULL(PartyName,N''),ISNULL(AccountCode,N''),Amount,ISNULL(Notes,N''),JournalEntryId,ISNULL(CreatedBy,N'') FROM dbo.AccountingVouchersTb
WHERE Notes=@Marker AND VoucherType=@Type AND VoucherDate>=@From AND VoucherDate<@End ORDER BY VoucherDate DESC,VoucherId DESC";
                    command.Parameters.Add("@Marker",System.Data.SqlDbType.NVarChar,500).Value=marker;command.Parameters.Add("@Type",System.Data.SqlDbType.NVarChar,50).Value=type;
                    command.Parameters.Add("@From",System.Data.SqlDbType.DateTime2).Value=DateTime.Today.AddMonths(-1);command.Parameters.Add("@End",System.Data.SqlDbType.DateTime2).Value=DateTime.Today.AddDays(1);
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command))adapter.Fill(expected);
                }
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                var loading=form.LoadReceiptInitialAsync();var watch=System.Diagnostics.Stopwatch.StartNew();
                while(!loading.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Detailed voucher nonempty row probe timed out.");}
                AssertAllowed("Detailed voucher nonempty initial read "+type,loading.GetAwaiter().GetResult());
                var grid=form.Controls.Find("receiptVoucherHistory",true).OfType<System.Windows.Forms.DataGridView>().Single();
                var table=(System.Data.DataTable)grid.DataSource;
                var rows=table.Rows.Cast<System.Data.DataRow>().Where(row=>Convert.ToString(row[6])==marker).ToArray();
                AssertAllowed("Detailed voucher nonempty type filter "+type,rows.Length==expected.Rows.Count&&rows.Length==(type=="ReceiptMulti"?0:1));
                for(int row=0;row<rows.Length;row++)for(int col=0;col<9;col++)AssertAllowed("Detailed voucher independent nonempty cell "+type+" "+col,object.Equals(rows[row][col],expected.Rows[row][col]));
                form.Close();
            }
            Console.WriteLine("PASS owned detailed voucher nonempty initial UI: two existing temporary history fixtures, full independent nine-cell Arabic/mixed/NULL/date/amount/id parity and three type filters; no extra fixture rows.");
        }

        private static void VerifyOwnedQaReceiptInitial(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string before=PermissionOwnedSnapshot(true);
            Action<System.Threading.Tasks.Task> finish=task=>{
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned detailed voucher options did not finish.");}
                task.GetAwaiter().GetResult();
            };
            var accounts=new System.Collections.Generic.List<string>();var centers=new System.Collections.Generic.List<string>();var currencies=new System.Collections.Generic.List<string>();string systemCode=null;
            using(var connection=Database.CreateConnection())
            {
                connection.Open();
                using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT AccountCode,AccountName FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode";
                    using(var reader=command.ExecuteReader())while(reader.Read())accounts.Add(reader.GetString(0)+" - "+reader.GetString(1));
                    command.CommandText="SELECT CostCenterId,CostCenterName FROM dbo.CostCentersTb WHERE IsActive=1 ORDER BY CostCenterName";
                    using(var reader=command.ExecuteReader())while(reader.Read())centers.Add(reader.GetInt32(0)+" - "+reader.GetString(1));
                    command.CommandText="SELECT CurrencyCode,IsSystemCurrency FROM dbo.CurrencySettingsTb ORDER BY IsSystemCurrency DESC,CountryNameAr";
                    int systems=0;
                    using(var reader=command.ExecuteReader())while(reader.Read())
                    {string code=reader.GetString(0).Trim();if(!currencies.Contains(code))currencies.Add(code);if(reader.GetBoolean(1)){systemCode=code;systems++;}}
                    AssertAllowed("Detailed voucher independent source contains one system currency",systems==1);
                }
            }
            using(var expected=new System.Data.DataTable())
            {
                using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
                {
                    command.CommandText=@"SELECT VoucherId,VoucherDate,N'قبض خارجي',ISNULL(PartyName,N''),ISNULL(AccountCode,N''),Amount,ISNULL(Notes,N''),JournalEntryId,ISNULL(CreatedBy,N'')
FROM dbo.AccountingVouchersTb WHERE VoucherType=N'ReceiptExternal' AND VoucherDate>=@From AND VoucherDate<@End ORDER BY VoucherDate DESC,VoucherId DESC";
                    command.Parameters.Add("@From",System.Data.SqlDbType.DateTime2).Value=DateTime.Today.AddMonths(-1);command.Parameters.Add("@End",System.Data.SqlDbType.DateTime2).Value=DateTime.Today.AddDays(1);
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command))adapter.Fill(expected);
                }
                using(var form=new ReceiptVoucherForm("ReceiptExternal",true))
                {
                    form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                    var loading=form.LoadReceiptInitialAsync();finish(loading);
                    var account=form.Controls.Find("receiptVoucherPartyAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                    var settlement=form.Controls.Find("receiptVoucherSettlementAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                    var center=form.Controls.Find("receiptVoucherCostCenter",true).OfType<System.Windows.Forms.ComboBox>().Single();
                    var currency=form.Controls.Find("receiptVoucherCurrency",true).OfType<System.Windows.Forms.ComboBox>().Single();
                    var grid=form.Controls.Find("receiptVoucherHistory",true).OfType<System.Windows.Forms.DataGridView>().Single();
                    AssertAllowed("Detailed voucher full independent accounts/settlement parity",loading.Result&&account.Items.Cast<string>().SequenceEqual(accounts)&&settlement.Items.Cast<string>().SequenceEqual(accounts));
                    AssertAllowed("Detailed voucher full independent active centers parity",center.Items.Cast<string>().SequenceEqual(new[]{"بدون"}.Concat(centers)));
                    AssertAllowed("Detailed voucher full independent currencies/system parity",currency.Items.Cast<string>().SequenceEqual(currencies)&&currency.Text==systemCode);
                    var actual=grid.DataSource as System.Data.DataTable;
                    AssertAllowed("Detailed voucher history independent shape",actual!=null&&actual.Columns.Count==9&&actual.Rows.Count==expected.Rows.Count);
                    for(int row=0;row<expected.Rows.Count;row++)for(int col=0;col<9;col++)AssertAllowed("Detailed voucher history raw cell "+row+":"+col,object.Equals(actual.Rows[row][col],expected.Rows[row][col]));
                    Console.WriteLine("PASS owned detailed voucher independent initial parity: accounts="+accounts.Count+", active centers="+centers.Count+", currencies="+currencies.Count+", history rows="+expected.Rows.Count+" (nine fields, date/type filtering and order).");
                    form.Close();
                }
            }
            foreach(string table in new[]{"AccountsTb","CostCentersTb","CurrencySettingsTb","AccountingVouchersTb"})
            foreach(bool close in new[]{false,true})
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())using(var form=new ReceiptVoucherForm("ReceiptExternal",true))
            {
                Console.WriteLine("BEGIN owned detailed voucher SQL probe "+table+" close="+close);
                blocker.Open();observer.Open();int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                var amount=form.Controls.Find("receiptVoucherAmount",true).OfType<AntdUI.Input>().Single();amount.Text="123.45";
                var account=form.Controls.Find("receiptVoucherPartyAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                using(var transaction=blocker.BeginTransaction())
                {
                    bool released=false;
                    try
                    {
                        using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                        var reading=form.LoadReceiptInitialAsync();var watch=System.Diagnostics.Stopwatch.StartNew();bool observed=false;
                        while(watch.ElapsedMilliseconds<8000){System.Windows.Forms.Application.DoEvents();if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){observed=true;break;}if(reading.IsCompleted)break;System.Threading.Thread.Sleep(5);}
                        AssertAllowed("Detailed voucher actual pending SQL lock "+table+" close="+close,observed&&!reading.IsCompleted&&!account.Enabled);
                        if(close)
                        {
                            form.Close();watch.Restart();
                            while(!reading.IsCompleted&&watch.ElapsedMilliseconds<5000){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                            AssertAllowed("Detailed voucher actual close completes under held lock "+table,form.IsDisposed&&reading.IsCompleted);
                            finish(reading);
                            AssertAllowed("Detailed voucher close rejected delivery "+table,!reading.Result&&CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                        }
                        else
                        {
                            AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                            transaction.Rollback();released=true;finish(reading);
                            AssertAllowed("Detailed voucher rejects old SQL result after same-user login "+table,!reading.Result&&!account.Enabled&&account.Items.Count==0);
                            var recovery=form.LoadReceiptInitialAsync();finish(recovery);
                            AssertAllowed("Detailed voucher fresh options recover empty draft "+table,recovery.Result&&account.Enabled&&amount.Text.Length==0&&account.Items.Cast<string>().SequenceEqual(accounts));
                            form.Close();
                        }
                    }
                    finally{if(!released)transaction.Rollback();}
                }
                Console.WriteLine("PASS owned detailed voucher actual "+table+" SQL wait: "+(close?"Form.Close cancels before lock release; no waiting requests":"same-user SQL reauthentication rejects old options; empty draft recovery"));
            }
            AssertAllowed("Detailed voucher initial live probes leave financial snapshot unchanged",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned detailed voucher initial integrated SQL read/cancellation/session probes; financial snapshot unchanged; no native input or voucher posting.");
        }

        private static void VerifyOwnedQaLegacyVoucherClose(string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            foreach(string table in new[]{"AccountsTb","AccountingVouchersTb"})
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())
            using(var form=new VoucherForm("ReceiptExternal",true))
            {
                blocker.Open();observer.Open();int blockerId;
                using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                using(var transaction=blocker.BeginTransaction())
                {
                    try
                    {
                        using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                        System.Threading.Tasks.Task reading=table=="AccountsTb"?(System.Threading.Tasks.Task)form.LoadVoucherOptionsAsync():form.RefreshVoucherHistoryAsync();
                        var watch=System.Diagnostics.Stopwatch.StartNew();bool observed=false;
                        while(watch.ElapsedMilliseconds<8000){System.Windows.Forms.Application.DoEvents();if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){observed=true;break;}if(reading.IsCompleted)break;System.Threading.Thread.Sleep(5);}
                        AssertAllowed("Live legacy voucher actual pending lock "+table,observed&&!reading.IsCompleted);
                        form.Close();watch.Restart();
                        while(!reading.IsCompleted&&watch.ElapsedMilliseconds<5000){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                        AssertAllowed("Live legacy voucher close completes cancelled read "+table,form.IsDisposed&&reading.IsCompleted);reading.GetAwaiter().GetResult();
                        AssertAllowed("Live legacy voucher has no SQL wait while lock remains "+table,CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                    }
                    finally{transaction.Rollback();}
                }
            }
            AssertAllowed("Live legacy voucher close probes have no financial effect",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned actual legacy voucher close: account and history LCK_M waits cancelled by Form.Close while locks remained held; read tasks finished, no blocked requests; financial snapshot unchanged. No native input.");
        }
        private static void VerifyOwnedQaLegacyVoucherEditor(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            var writer=ApplicationServices.CreateVoucherPostSession();
            var expected=new System.Collections.Generic.List<string>();
            using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
            {
                connection.Open();command.CommandText="SELECT AccountCode,AccountName FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode";
                using(var reader=command.ExecuteReader())while(reader.Read())expected.Add(reader.GetString(0)+" - "+reader.GetString(1));
            }
            Action<System.Threading.Tasks.Task> finish=task=>{
                var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned legacy voucher UI read timed out.");}task.GetAwaiter().GetResult();
            };
            using(var form=new VoucherForm("ReceiptExternal",true))
            {
                form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Show();form.Location=new System.Drawing.Point(-20000,-20000);
                var loading=form.LoadVoucherOptionsAsync();finish(loading);
                var account=form.Controls.Find("legacyVoucherAccount",true).OfType<System.Windows.Forms.ComboBox>().Single();
                var amount=form.Controls.Find("legacyVoucherAmount",true).OfType<AntdUI.Input>().Single();
                var party=form.Controls.Find("legacyVoucherParty",true).OfType<System.Windows.Forms.TextBox>().Single();
                var date=form.Controls.Find("legacyVoucherDate",true).OfType<AntdUI.DatePicker>().Single();
                var editor=form.Controls.Find("accountingDataEditor",true).Single();var grid=form.Controls.Find("accountingDataGrid",true).OfType<System.Windows.Forms.DataGridView>().Single();
                AssertAllowed("Live voucher active account full independent parity",loading.Result&&account.Items.Cast<string>().SequenceEqual(expected)&&editor.Enabled);
                amount.Text="1250.37";party.Text="مسودة Pharmacy";date.Value=DateTime.Today.AddDays(-5);
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                AssertAllowed("Old pinned voucher writer denies without SQL",!writer.IsCurrent);
                ExpectEmployeeAdministrationDenied(()=>writer.Service.Post(new VoucherPostRequest("ReceiptExternal",DateTime.Today,"QA",AccountingService.CashAccount,1m,"",AccountingService.CashAccount,null),writer.Actor),"Old voucher writer session");
                AssertAllowed("Live voucher retained editor/history is invalidated",form.SelectedVoucherHistoryText()==null&&!editor.Enabled&&grid.DataSource==null&&amount.Text.Length==0&&party.Text.Length==0&&date.Value==DateTime.Today);
                loading=form.LoadVoucherOptionsAsync();finish(loading);
                AssertAllowed("Live voucher empty draft fresh options recovery",loading.Result&&editor.Enabled&&amount.Text.Length==0&&party.Text.Length==0&&account.Items.Cast<string>().SequenceEqual(expected));
                form.Close();
            }
            AssertAllowed("Live voucher UI and denied old writer no financial effect",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned legacy voucher UI: active accounts independently matched, actual SQL same-user relogin clears retained fields/date/history, fresh empty recovery, old pinned writer denied; no voucher posting/native input.");
        }
        private static void VerifyOwnedQaVoucherHistoryRead(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            var request=new VoucherHistoryReadRequest(new DateTime(1753,1,1),DateTime.MaxValue,"");
            VerifyOwnedQaBlockedRead("VoucherHistory","AccountingVouchersTb",()=>{
                var session=ApplicationServices.CreateVoucherHistoryReadSession();return token=>BoxInventoryQaRead(session.Service.LoadAsync(request,session.Actor,token));
            },"لا تملك صلاحية قراءة سجل السندات.",user,password,employeeId,ownerToken);
            var current=ApplicationServices.CreateVoucherHistoryReadSession();
            VerifyOwnedSqlReadCancellation(ownerToken,"AccountingVouchersTb",token=>BoxInventoryQaRead(current.Service.LoadAsync(request,current.Actor,token)));
            var reading=ReceiptVoucherWorkspaceService.LoadHistoryAsync("ReceiptExternal",System.Threading.CancellationToken.None);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!reading.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Voucher history desktop read timed out.");}
            using(var table=reading.GetAwaiter().GetResult())AssertAllowed("Voucher history pinned desktop projection exists",table.Columns.Count>0);
            VerifyOwnedQaVoucherHistoryRows(ownerToken);
            Console.WriteLine("PASS owned voucher history: SQL lock/relogin rejection, actual SQL cancellation, desktop projection and two temporary raw-row mapping/filter/order fixtures; no financial posting/native input.");
        }
        private static void VerifyOwnedQaVoucherHistoryRows(string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string before=PermissionOwnedSnapshot(true),marker="QA-History-"+ownerToken;
            var ids=new System.Collections.Generic.List<int>();var journalIds=new System.Collections.Generic.List<int>();var links=new System.Collections.Generic.Dictionary<int,int>();int journalId;
            using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
            {connection.Open();command.CommandText="SELECT TOP(2) e.JournalEntryId FROM dbo.JournalEntriesTb e WHERE NOT EXISTS(SELECT 1 FROM dbo.AccountingVouchersTb v WHERE v.JournalEntryId=e.JournalEntryId) ORDER BY e.JournalEntryId";using(var reader=command.ExecuteReader())while(reader.Read())journalIds.Add(reader.GetInt32(0));}
            AssertAllowed("Two unlinked journal identities for read fixture",journalIds.Count==2);
            try
            {
                using(var connection=Database.CreateConnection())
                {
                    connection.Open();
                    foreach(string type in new[]{"ReceiptExternal","PaymentInternal"})
                    using(var command=connection.CreateCommand())
                    {
                        command.CommandText="INSERT dbo.AccountingVouchersTb(VoucherDate,VoucherType,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy) OUTPUT INSERTED.VoucherId VALUES(@Date,@Type,@Party,@Account,@Amount,@Notes,@Journal,@User)";
                        command.Parameters.Add("@Date",System.Data.SqlDbType.DateTime).Value=DateTime.Today.AddHours(type=="ReceiptExternal"?12:13).AddMinutes(34);
                        command.Parameters.Add("@Type",System.Data.SqlDbType.NVarChar,50).Value=type;
                        command.Parameters.Add("@Party",System.Data.SqlDbType.NVarChar,200).Value=type=="ReceiptExternal"?(object)"عميل Pharmacy 500 mg":DBNull.Value;
                        command.Parameters.Add("@Account",System.Data.SqlDbType.NVarChar,20).Value=AccountingService.CashAccount;
                        var amountParameter=command.Parameters.Add("@Amount",System.Data.SqlDbType.Decimal);amountParameter.Precision=18;amountParameter.Scale=2;amountParameter.Value=1250.37m;
                        command.Parameters.Add("@Notes",System.Data.SqlDbType.NVarChar,500).Value=marker;
                        command.Parameters.Add("@User",System.Data.SqlDbType.NVarChar,200).Value="qa-history-reader";
                        journalId=journalIds[ids.Count];command.Parameters.Add("@Journal",System.Data.SqlDbType.Int).Value=journalId;
                        int newId=Convert.ToInt32(command.ExecuteScalar());ids.Add(newId);links.Add(newId,journalId);
                    }
                }
                var source=new System.Data.DataTable();
                using(var connection=Database.CreateConnection())using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT VoucherId,VoucherDate,VoucherType,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy FROM dbo.AccountingVouchersTb WHERE Notes=@Marker ORDER BY VoucherDate DESC,VoucherId DESC";
                    command.Parameters.Add("@Marker",System.Data.SqlDbType.NVarChar,500).Value=marker;
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command))adapter.Fill(source);
                }
                AssertAllowed("Voucher raw row fixture count",source.Rows.Count==2);
                var session=ApplicationServices.CreateVoucherHistoryReadSession();
                var rows=session.Service.LoadAsync(new VoucherHistoryReadRequest(DateTime.Today,DateTime.Today,""),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                var own=rows.Where(row=>row.Notes==marker).ToArray();
                AssertAllowed("Voucher SQL row fixture order/count",own.Length==2);
                for(int index=0;index<own.Length;index++)
                {
                    var row=own[index];var raw=source.Rows[index];
                    AssertAllowed("Voucher full raw row parity "+index,row.VoucherId==Convert.ToInt32(raw["VoucherId"])&&row.Date==Convert.ToDateTime(raw["VoucherDate"])&&
                        row.Type==Convert.ToString(raw["VoucherType"])&&row.PartyName==Convert.ToString(raw["PartyName"])&&row.AccountCode==Convert.ToString(raw["AccountCode"])&&
                        row.Amount==Convert.ToDecimal(raw["Amount"])&&row.Notes==Convert.ToString(raw["Notes"])&&row.CreatedBy==Convert.ToString(raw["CreatedBy"])&&row.JournalEntryId==Convert.ToInt32(raw["JournalEntryId"]));
                }
                foreach(string type in new[]{"ReceiptExternal","PaymentInternal","ReceiptMulti"})
                {
                    var filtered=session.Service.LoadAsync(new VoucherHistoryReadRequest(DateTime.Today,DateTime.Today,type),session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    AssertAllowed("Voucher fixture type filter "+type,filtered.Count(row=>row.Notes==marker)==(type=="ReceiptMulti"?0:1)&&filtered.All(row=>row.Type==type));
                }
                VerifyOwnedQaReceiptInitialRows(ownerToken,marker);
                VerifyOwnedQaVoucherDocumentRows(ownerToken,marker);
                source.Dispose();
            }
            finally
            {
                using(var connection=Database.CreateConnection())
                {
                    connection.Open();foreach(int id in ids)using(var command=connection.CreateCommand())
                    {
                        command.CommandText="DELETE dbo.AccountingVouchersTb WHERE VoucherId=@Id AND Notes=@Marker AND CreatedBy=N'qa-history-reader' AND JournalEntryId=@Journal";
                        command.Parameters.Add("@Id",System.Data.SqlDbType.Int).Value=id;command.Parameters.Add("@Marker",System.Data.SqlDbType.NVarChar,500).Value=marker;
                        command.Parameters.Add("@Journal",System.Data.SqlDbType.Int).Value=links[id];
                        AssertAllowed("Owned voucher raw fixture exact cleanup",command.ExecuteNonQuery()==1);
                    }
                }
            }
            AssertAllowed("Voucher row mapping fixture financial snapshot restored",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned voucher history rows: 2 temporary raw rows, 9 fields including Arabic/mixed text and NULL, order/type filters; exact cleanup and financial snapshot restored. This is a read fixture, not voucher posting acceptance.");
        }
        private static void VerifyOwnedQaSavedJournalRead(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            int id;
            using(var connection=Database.CreateConnection())
            {connection.Open();using(var command=connection.CreateCommand())
            {command.CommandText="SELECT TOP(1) e.JournalEntryId FROM dbo.JournalEntriesTb e INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId GROUP BY e.JournalEntryId HAVING COUNT(*)>=2 AND SUM(l.Debit)=SUM(l.Credit) AND SUM(l.Debit)>0 ORDER BY e.JournalEntryId";id=Convert.ToInt32(command.ExecuteScalar());}}
            AssertAllowed("Saved journal fixture actual accepted id",id>0);
            var request=new JournalDocumentReadRequest(id);
            foreach(string table in new[]{"JournalEntriesTb","JournalEntryLinesTb","AccountsTb","CostCentersTb"})
                VerifyOwnedQaBlockedRead("SavedJournal"+table,table,()=>{
                    var session=ApplicationServices.CreateJournalDocumentReadSession();return token=>BoxInventoryQaRead(session.Service.LoadAsync(request,session.Actor,token));
                },"لا تملك صلاحية قراءة القيد المحفوظ.",user,password,employeeId,ownerToken);
            var current=ApplicationServices.CreateJournalDocumentReadSession();
            VerifyOwnedSqlReadCancellation(ownerToken,"JournalEntriesTb",token=>BoxInventoryQaRead(current.Service.LoadAsync(request,current.Actor,token)));
            var reading=JournalDocumentDesktopService.ReadAsync(id);var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!reading.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Saved journal desktop read timed out.");}
            var actual=reading.GetAwaiter().GetResult();
            AssertAllowed("Saved journal desktop root delivered correct complete document",actual.Id==id&&actual.Lines.Count>=2);
            Console.WriteLine("PASS owned saved journal: four SQL join-source lock/relogin rejections and fresh full DTO parity; actual SQL cancellation; desktop pinned read returns accepted id/lines. No write/native print.");
        }

        private static void VerifyOwnedJournalEditorSqlCancellation(string ownerToken)
        {
            var session=ApplicationServices.CreateJournalEditorReadSession();
            VerifyOwnedSqlReadCancellation(ownerToken,"CurrencySettingsTb",token=>BoxInventoryQaRead(session.Service.LoadAsync(session.Actor,token)));
        }
        private static void VerifyOwnedSqlReadCancellation(string ownerToken,string table,Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<object>> read)
        {
            if(table!="CurrencySettingsTb"&&table!="JournalEntriesTb"&&table!="AccountingVouchersTb")throw new InvalidOperationException("Cancellation fixture table not permitted.");
            string owned=QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken),before=PermissionOwnedSnapshot(true);
            using(var blocker=Database.CreateConnection())using(var observer=Database.CreateConnection())using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                blocker.Open();observer.Open();AssertAllowed("Journal cancel connections owned",blocker.Database==owned&&observer.Database==owned);
                int blockerId;using(var command=blocker.CreateCommand()){command.CommandText="SELECT @@SPID";blockerId=Convert.ToInt32(command.ExecuteScalar());}
                using(var transaction=blocker.BeginTransaction())
                {
                    System.Threading.Tasks.Task<object> pending=null;
                    try
                    {
                        using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";command.ExecuteScalar();}
                        pending=read(cancellation.Token);
                        var watch=System.Diagnostics.Stopwatch.StartNew();bool waiting=false;
                        while(watch.ElapsedMilliseconds<8000&&!pending.IsCompleted){if(CountOwnedQaBlockedRequests(observer,blockerId,true)>0){waiting=true;break;}System.Threading.Thread.Sleep(10);}
                        AssertAllowed("Journal currency real SQL cancellation lock wait",waiting&&!pending.IsCompleted);
                        cancellation.Cancel();watch.Restart();while(!pending.IsCompleted&&watch.ElapsedMilliseconds<5000)System.Threading.Thread.Sleep(10);
                        AssertAllowed("Journal SQL cancellation completes while blocker still held",pending.IsCompleted);
                        bool canceled=false;try{pending.GetAwaiter().GetResult();}catch(OperationCanceledException){canceled=true;}
                        AssertAllowed("Journal SQL cancellation observed, no result delivered",canceled);
                        AssertAllowed("Journal SQL cancellation leaves no blocked request",CountOwnedQaBlockedRequests(observer,blockerId,false)==0);
                    }
                    finally{cancellation.Cancel();transaction.Rollback();if(pending!=null&&!pending.IsCompleted){var watch=System.Diagnostics.Stopwatch.StartNew();while(!pending.IsCompleted&&watch.ElapsedMilliseconds<5000)System.Threading.Thread.Sleep(10);}}
                }
            }
            AssertAllowed("Journal SQL cancellation financial/currency rows unchanged",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned actual SQL cancellation "+table+": LCK_M wait, token cancels while lock remains held, no delivered result/blocked request, financial/currency snapshot unchanged.");
        }

        private static void VerifyOwnedQaFullJournalEditor(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            Action<System.Threading.Tasks.Task> finish=task=>{
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Owned journal editor read timed out.");}
                task.GetAwaiter().GetResult();System.Windows.Forms.Application.DoEvents();
            };
            foreach(string table in new[]{"AccountsTb","CostCentersTb","CurrencySettingsTb"})
                VerifyOwnedQaBlockedRead("JournalEditor"+table,table,()=>{
                    var session=ApplicationServices.CreateJournalEditorReadSession();return token=>BoxInventoryQaRead(session.Service.LoadAsync(session.Actor,token));
                },"لا تملك صلاحية قراءة خيارات القيد.",user,password,employeeId,ownerToken);
            VerifyOwnedJournalEditorSqlCancellation(ownerToken);
            foreach(string kind in new[]{"Manual","OpeningEntry"})
            {
                var expected=JournalEntryLookupService.Load();
                var centerRows=AccountingLookupDesktopService.LoadCostCenters();
                string[] expectedCenters=new[]{"بدون"}.Concat(centerRows.Rows.Cast<System.Data.DataRow>().Select(r=>Convert.ToString(r["CostCenterId"])+" - "+Convert.ToString(r["CostCenterName"]))).ToArray();

                using(var form=new JournalEntriesForm("QA journal",kind,false))
                {
                    form.CreateControl();var handle=form.Handle;
                    var loading=form.LoadInitialLookupsAsync();finish(loading);
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    Func<string,object> field=name=>typeof(JournalEntriesForm).GetField(name,flags).GetValue(form);
                    var account=(ComboBox)field("account");var currency=(AntdUI.Select)field("currency");
                    var desc=(TextBox)field("headerDescription");var rows=(System.Data.DataTable)field("lines");var center=(ComboBox)field("costCenter");
                    AssertAllowed("Owned full/opening actual SQL option parity",loading.Result&&account.Items.Count>0&&account.Items.Cast<string>().SequenceEqual(expected.Accounts)&&currency.Items.Cast<string>().SequenceEqual(expected.Currencies)&&currency.Text==expected.SystemCurrencyCode&&center.Items.Cast<string>().SequenceEqual(expectedCenters));
                    desc.Text="مسودة قديمة 500 mg";
                    AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                    int calls=0;var denied=form.RunJournalWriteAsync(()=>{calls++;return System.Threading.Tasks.Task.FromResult(700);},false);finish(denied);
                    AssertAllowed("Actual relogin full/opening cached editor denied before dispatch",denied.Result==0&&calls==0&&rows.Rows.Count==0&&desc.Text.Length==0&&account.Items.Count==0&&currency.Items.Count==0);
                    loading=form.LoadInitialLookupsAsync();finish(loading);
                    AssertAllowed("Actual SQL full/opening recovery full options with blank draft",loading.Result&&rows.Rows.Count==0&&desc.Text.Length==0&&account.Items.Cast<string>().SequenceEqual(expected.Accounts)&&currency.Items.Cast<string>().SequenceEqual(expected.Currencies)&&currency.Text==expected.SystemCurrencyCode&&center.Items.Cast<string>().SequenceEqual(expectedCenters));
                }
            }
            AssertAllowed("Owned full/opening retained editor/recovery no finance/catalog changes",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned SQL full/opening journal: actual accounts/currency full option parity, actual same-user reauthentication denies stale editor before dispatch, fresh recovery empty/full; no journal writes/native input. Pinned lookup SQL uses actual async accounts/currencies/active cost centers with cancellation.");
        }

        private static void VerifyOwnedQaQuickJournalSession(string user,string password,string employeeId,string ownerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);string before=PermissionOwnedSnapshot(true);
            Action<System.Threading.Tasks.Task> finish=task=>{
                var deadline=DateTime.UtcNow.AddSeconds(20);
                while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA quick journal read timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                task.GetAwaiter().GetResult();
            };
            DateTime from=new DateTime(2020,1,1),to=DateTime.Today;
            using(var expected=JournalEntriesQuickReadService.Load(from,to,""))using(var accounts=AccountingLookupDesktopService.LoadAccounts(true))
            using(var form=new JournalEntriesQuickForm(false,null,text=>{}))
            {
                IntPtr handle=form.Handle;
                AntdDateInput.SetDate((AntdUI.DatePicker)form.Controls.Find("quickJournalFromDate",true)[0],from);
                AntdDateInput.SetDate((AntdUI.DatePicker)form.Controls.Find("quickJournalToDate",true)[0],to);
                finish(form.LoadQuickJournalAccountsAsync());finish(form.RefreshQuickJournalHistoryAsync());
                var grid=(DataGridView)form.Controls.Find("accountingDataGrid",true)[0];
                var debit=(ComboBox)form.Controls.Find("quickJournalDebitAccount",true)[0];var credit=(ComboBox)form.Controls.Find("quickJournalCreditAccount",true)[0];
                var amount=(AntdUI.Input)form.Controls.Find("quickJournalAmount",true)[0];var description=(TextBox)form.Controls.Find("quickJournalDescription",true)[0];
                string[] expectedAccounts=accounts.Rows.Cast<System.Data.DataRow>().Select(row=>Convert.ToString(row["AccountCode"])+" - "+Convert.ToString(row["AccountName"])).ToArray();
                AssertAllowed("Quick journal actual SQL full history and account parity",expected.Rows.Count>0&&InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected)&&debit.Items.Cast<string>().SequenceEqual(expectedAccounts)&&credit.Items.Cast<string>().SequenceEqual(expectedAccounts));
                grid.CurrentCell=grid.Rows[0].Cells[0];AssertAllowed("Quick actual current row copy available",form.SelectedQuickHistoryText()!=null);
                amount.Text="17.50";description.Text="old draft Cash 500 mg";
                var oldWriter=ApplicationServices.CreateJournalPostSession();AssertAllowed("Current journal posting policy initially allowed",oldWriter.IsCurrent);
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                AssertAllowed("Quick actual SQL relogin rejects old copy and clears editor/history/options",form.SelectedQuickHistoryText()==null&&grid.Rows.Count==0&&debit.Items.Count==0&&credit.Items.Count==0&&amount.Text.Length==0&&description.Text.Length==0);
                decimal value;string debitCode,creditCode;
                AssertAllowed("Quick actual old editor input denied",!form.TryReadQuickManualInput(out value,out debitCode,out creditCode));
                var unbalanced=new JournalPostRequest(DateTime.Now,"Manual",null,"QA no SQL validation",new[]{new PharmacyManagement.Domain.JournalLineValues("1000",1m,0m,null,null)});
                ExpectEmployeeAdministrationDenied(()=>oldWriter.Service.Post(unbalanced,oldWriter.Actor),"Old pinned journal posting session rejected before validation/SQL");
                var current=ApplicationServices.CreateJournalPostSession();bool reachesValidation=false;
                try{current.Service.Post(unbalanced,current.Actor);}catch(InvalidOperationException){reachesValidation=true;}
                AssertAllowed("Fresh pinned journal posting reaches domain validation without SQL write",current.IsCurrent&&reachesValidation);
                finish(form.LoadQuickJournalAccountsAsync());finish(form.RefreshQuickJournalHistoryAsync());
                AssertAllowed("Quick fresh SQL recovery has empty editor and full history",amount.Text.Length==0&&description.Text.Length==0&&InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected));
                amount.Text="18";description.Text="old options draft";
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                finish(form.LoadQuickJournalAccountsAsync());finish(form.RefreshQuickJournalHistoryAsync());
                AssertAllowed("Quick direct reload after SQL relogin clears draft and restores full data",amount.Text.Length==0&&description.Text.Length==0&&debit.Items.Cast<string>().SequenceEqual(expectedAccounts)&&InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected));
            }
            AssertAllowed("Quick journal SQL/relogin/denied post and granted validation do not change financial/catalog data",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS live QA quick journal: complete SQL accounts/history binding, real SQL same-user relogin denies retained copy/editor and old posting service, fresh recovery clears draft, granted posting reaches unbalanced validation; no journal writes/native input.");
        }

        private static void VerifyOwnedQaAccountingReadSession(string user,string password,string employeeId,string ownerToken)
        {
            foreach(string mode in new[]{"Accounts","Centers","History"})
            {
                string path=mode;
                VerifyOwnedQaBlockedRead("Accounting"+path,path=="Accounts"?"AccountsTb":path=="Centers"?"CostCentersTb":"JournalEntriesTb",()=>{
                    var session=ApplicationServices.CreateAccountingReadSession();
                    return token=>path=="Accounts"?BoxInventoryQaRead(session.Lookups.LoadAccountsAsync(false,session.Actor,token)):
                        path=="Centers"?BoxInventoryQaRead(session.Lookups.LoadCostCentersAsync(session.Actor,token)):
                        BoxInventoryQaRead(session.History.LoadAsync(new JournalHistoryReadRequest(new DateTime(1753,1,1),DateTime.MaxValue,""),session.Actor,token));
                },path=="History"?"لا تملك صلاحية قراءة سجل القيود.":"لا تملك صلاحية قراءة خيارات المحاسبة.",user,password,employeeId,ownerToken);
            }
            VerifyOwnedQaAccountingQueuedContinuation(user,password,employeeId,ownerToken);
            VerifyOwnedQaChartSession(user,password,employeeId,ownerToken);
            VerifyOwnedQaQuickJournalSession(user,password,employeeId,ownerToken);
            VerifyOwnedQaFullJournalEditor(user,password,employeeId,ownerToken);
            VerifyOwnedQaSavedJournalRead(user,password,employeeId,ownerToken);
            VerifyOwnedQaVoucherHistoryRead(user,password,employeeId,ownerToken);
            VerifyOwnedQaLegacyVoucherEditor(user,password,employeeId,ownerToken);
            VerifyOwnedQaLegacyVoucherClose(ownerToken);
            VerifyOwnedQaReceiptInitial(user,password,employeeId,ownerToken);
            VerifyOwnedQaVoucherBalance(user,password,employeeId,ownerToken);
            VerifyOwnedQaVoucherDocumentLifetime(user,password,employeeId,ownerToken);
            VerifyOwnedQaVoucherCurrencyGuard(ownerToken);
            var current=ApplicationServices.CreateAccountingReadSession();
            ExpectEmployeeAdministrationDenied(()=>current.Lookups.LoadAccounts(false,new OperationActor{UserName="forged",UserRole="Manager"}),"Accounting root forged actor");
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                cancellation.Cancel();bool cancelled=false;
                try{current.Lookups.LoadAccountsAsync(false,current.Actor,cancellation.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                AssertAllowed("Accounting root pre-cancelled SQL read rejected",cancelled);
            }
            Console.WriteLine("PASS live QA accounting pinned accounts/centers/history lock/relogin rejection and fresh complete DTO parity; forged/pre-cancelled request rejection; no accounting writes.");
        }

        private static void VerifyOwnedQaMedicineSession(string user,string password,string employeeId,string ownerToken)
        {
            string medicine;
            using(var connection=Database.CreateConnection())using(var command=new System.Data.SqlClient.SqlCommand("SELECT TOP(1) MedName FROM dbo.MedicineTb1 ORDER BY MedName",connection))
            {connection.Open();medicine=Convert.ToString(command.ExecuteScalar());}
            foreach(string mode in new[]{"Directory","Companies","Selected","Barcode"})
            {
                string path=mode;
                VerifyOwnedQaBlockedRead("Medicine"+path,path=="Companies"?"CompanyTb1":"MedicineTb1",()=>{
                    var session=ApplicationServices.CreateMedicineSession(()=>"0099912345000");
                    return token=>path=="Directory"?BoxInventoryQaRead(session.Service.GetAllAsync(session.Actor,token)):
                        path=="Companies"?BoxInventoryQaRead(session.Service.GetCompanyNamesAsync(session.Actor,token)):
                        path=="Selected"?BoxInventoryQaRead(session.Service.FindByNameAsync(medicine,session.Actor,token)):
                        BoxInventoryQaRead(session.Service.GenerateAvailableBarcodeAsync(medicine,session.Actor,token));
                },"لا تملك صلاحية الوصول إلى بيانات الأدوية.",user,password,employeeId,ownerToken);
            }
            string before=PermissionOwnedSnapshot(true);
            var current=ApplicationServices.CreateMedicineSession();
            var expected=current.Service.GetAllAsync(current.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            var companies=current.Service.GetCompanyNamesAsync(current.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            ExpectEmployeeAdministrationDenied(()=>current.Service.GetAllAsync(new OperationActor{UserName="forged",UserRole="Manager"},System.Threading.CancellationToken.None),"Medicine root forged actor");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using(var form=new Medicine(false,false,null,()=>true,text=>{}))
            {
                IntPtr handle=form.Handle;
                Action<System.Threading.Tasks.Task> finish=task=>{
                    var deadline=DateTime.UtcNow.AddSeconds(20);
                    while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA medicine UI read timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                    task.GetAwaiter().GetResult();
                };
                finish(form.ReloadDirectoryAsync());
                var grid=(DataGridView)typeof(Medicine).GetField("MedicineGV",flags).GetValue(form);
                var selector=(ComboBox)typeof(Medicine).GetField("SelectCompany",flags).GetValue(form);
                AssertAllowed("Actual medicine form full SQL data parity",InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected));
                AssertAllowed("Actual medicine form full company option parity",selector.Items.Cast<string>().SequenceEqual(companies));
                typeof(Medicine).GetMethod("PopulateFieldsFromRow",flags).Invoke(form,new object[]{0});
                var editor=(TextBox)typeof(Medicine).GetField("MedicineTb",flags).GetValue(form);
                AssertAllowed("Actual medicine form selects real SQL record",editor.Text==expected[0].MedName);
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                AssertAllowed("Actual SQL relogin rejects retained medicine row copy",form.SelectedMedicineText()==null);
                typeof(Medicine).GetMethod("button3_Click_1",flags).Invoke(form,new object[]{null,EventArgs.Empty});
                AssertAllowed("Actual SQL relogin clears medicine editor/list/options",editor.Text.Length==0&&grid.Rows.Count==0&&selector.Items.Count==0);
                finish(form.ReloadDirectoryAsync());
                AssertAllowed("Actual medicine form fresh SQL directory recovery",InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected));
                typeof(Medicine).GetMethod("PopulateFieldsFromRow",flags).Invoke(form,new object[]{0});editor.Text="old draft";
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                finish(form.ReloadDirectoryAsync());
                AssertAllowed("Actual medicine relogin reload clears previous editor/company",editor.Text.Length==0&&selector.Text.Length==0&&InventoryQaDataSnapshot(grid.DataSource)==InventoryQaDataSnapshot(expected));
            }
            AssertAllowed("Medicine session/UI tests do not change catalog/finance/inventory",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS live QA medicine pinned directory/company/targeted/barcode lock-relogin denial and fresh parity; actual form binding/old editor/copy/reload; no medicine writes.");
        }

        private static void VerifyOwnedQaSupplierReadSession(string user,string password,string employeeId,string ownerToken)
        {
            VerifyOwnedQaBlockedRead("SupplierDirectory","CompanyTb1",()=>{
                var session=ApplicationServices.CreateManufacturerReadSession();
                return token=>BoxInventoryQaRead(session.Service.GetAllAsync(session.Actor,token));
            },"تغيرت صلاحية قراءة الموردين أثناء الطلب.",user,password,employeeId,ownerToken);
            string before=PermissionOwnedSnapshot(true);
            var sessionNow=ApplicationServices.CreateManufacturerReadSession();
            var expected=sessionNow.Service.GetAllAsync(sessionNow.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            AssertAllowed("Owned QA supplier records available",expected.Count>0);
            ExpectEmployeeAdministrationDenied(()=>sessionNow.Service.GetAllAsync(new OperationActor{UserName="forged",UserRole="Manager"},System.Threading.CancellationToken.None),"Supplier read root forged actor");
            var value=new ManufacturerRecord{CompId="readonly-check",CompName="مورد ممنوع",CompPhone="778000222",CompAddress="عدن"};
            ExpectEmployeeAdministrationDenied(()=>sessionNow.Service.Create(value,sessionNow.Actor),"Supplier read-only create denied");
            ExpectEmployeeAdministrationDenied(()=>sessionNow.Service.Update(value,sessionNow.Actor),"Supplier read-only update denied");
            ExpectEmployeeAdministrationDenied(()=>sessionNow.Service.Delete(value.CompId,sessionNow.Actor),"Supplier read-only delete denied");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(bool modern in new[]{false,true})
            using(var form=new Manufacturer(false,false,null,modern,text=>{},()=>true))
            {
                IntPtr handle=form.Handle;
                Action<System.Threading.Tasks.Task> finish=task=>{
                    var deadline=DateTime.UtcNow.AddSeconds(20);
                    while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA supplier UI read timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                    task.GetAwaiter().GetResult();
                };
                finish(form.ReloadGridAsync());
                var rows=(List<ManufacturerRecord>)typeof(Manufacturer).GetField("companyRows",flags).GetValue(form);
                AssertAllowed("Actual supplier form full SQL parity",InventoryQaDataSnapshot(rows)==InventoryQaDataSnapshot(expected));
                if(modern)((AntdUI.Table)typeof(Manufacturer).GetField("companyTable",flags).GetValue(form)).SelectedIndex=1;
                else typeof(Manufacturer).GetMethod("CompanyGV_CellContentClick",flags).Invoke(form,new object[]{null,new DataGridViewCellEventArgs(0,0)});
                var editor=(TextBox)typeof(Manufacturer).GetField("tb2",flags).GetValue(form);
                AssertAllowed("Actual supplier form selects SQL record",editor.Text==expected[0].CompName);
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                AssertAllowed("Actual SQL relogin rejects supplier row copy",form.SelectedCompanyText()==null);
                typeof(Manufacturer).GetMethod("button3_Click",flags).Invoke(form,new object[]{null,EventArgs.Empty});
                AssertAllowed("Actual SQL relogin clears supplier editor/list",editor.Text.Length==0&&rows.Count==0);
                finish(form.ReloadGridAsync());
                AssertAllowed("Actual supplier form reload recovers full SQL data",InventoryQaDataSnapshot(rows)==InventoryQaDataSnapshot(expected));
            }
            AssertAllowed("Supplier UI/session guards do not alter supplier/financial state",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS live QA supplier pinned async root, locked read/relogin, readonly policy, both default table variants and stale editor recovery; no supplier writes.");
        }

        private static void VerifyOwnedQaCustomerReadSession(string user,string password,string employeeId,string ownerToken)
        {
            VerifyOwnedQaBlockedRead("CustomerDirectory","CustomersBillsTb",()=>{
                var session=ApplicationServices.CreateSalesCustomerReadSession();
                return token=>BoxInventoryQaRead(session.Service.GetAllAsync(session.Actor,token));
            },"تغيرت صلاحية قراءة العملاء أثناء الطلب.",user,password,employeeId,ownerToken);
            string before=PermissionOwnedSnapshot(true);
            var current=ApplicationServices.CreateSalesCustomerReadSession();
            var expected=current.Service.GetAllAsync(current.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            AssertAllowed("Owned QA customer fixture has records",expected.Count>0);
            ExpectEmployeeAdministrationDenied(()=>current.Service.GetAllAsync(new OperationActor{UserName="forged",UserRole="Manager"},System.Threading.CancellationToken.None),"Customer read root forged actor");
            ExpectEmployeeAdministrationDenied(()=>current.Service.Create(new SalesCustomer{CustomerName="عميل ممنوع",PhoneNumber="778000222"},current.Actor),"Customer read-only session cannot create");
            ExpectEmployeeAdministrationDenied(()=>current.Service.Update(new SalesCustomer{CustomerId="readonly-check",CustomerName="عميل ممنوع",PhoneNumber="778000222"},current.Actor),"Customer read-only session cannot update");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using(var form=new CustomerSupplierManagementForm(true))
            {
                IntPtr handle=form.Handle;
                Func<System.Threading.Tasks.Task> load=()=>(System.Threading.Tasks.Task)typeof(CustomerSupplierManagementForm).GetMethod("LoadCustomers",flags).Invoke(form,new object[]{null});
                Action<System.Threading.Tasks.Task> finish=task=>{
                    var deadline=DateTime.UtcNow.AddSeconds(20);
                    while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA customer UI read timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                    task.GetAwaiter().GetResult();
                };
                finish(load());
                var visible=(List<SalesCustomer>)typeof(CustomerSupplierManagementForm).GetField("visibleCustomers",flags).GetValue(form);
                AssertAllowed("Actual customer form binds complete SQL data",InventoryQaDataSnapshot(visible)==InventoryQaDataSnapshot(expected));
                var table=(AntdUI.Table)typeof(CustomerSupplierManagementForm).GetField("customers",flags).GetValue(form);table.SelectedIndex=1;
                AssertAllowed("Actual customer form selected a real customer",form.SelectedCustomer!=null);
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                typeof(CustomerSupplierManagementForm).GetMethod("ChooseSelectedCustomer",flags).Invoke(form,null);
                AssertAllowed("Actual SQL relogin rejects retained customer selection",form.DialogResult!=DialogResult.OK&&form.SelectedCustomer==null&&visible.Count==0);
                finish(load());
                AssertAllowed("Actual customer form reload recovers full data",InventoryQaDataSnapshot(visible)==InventoryQaDataSnapshot(expected));
            }

            using(var form=new CustomerSupplierManagementForm(false))
            {
                IntPtr handle=form.Handle;
                Action<System.Threading.Tasks.Task> finish=task=>{
                    var deadline=DateTime.UtcNow.AddSeconds(20);
                    while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Owned QA customer editor reload timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                    task.GetAwaiter().GetResult();
                };
                finish((System.Threading.Tasks.Task)typeof(CustomerSupplierManagementForm).GetMethod("LoadCustomers",flags).Invoke(form,new object[]{null}));
                var table=(AntdUI.Table)typeof(CustomerSupplierManagementForm).GetField("customers",flags).GetValue(form);table.SelectedIndex=1;
                var editor=(TextBox)typeof(CustomerSupplierManagementForm).GetField("name",flags).GetValue(form);editor.Text="مسودة سابقة Pharma 500 mg";
                AuthenticatePermissionFixture(user,password,employeeId,"Manager");
                finish((System.Threading.Tasks.Task)typeof(CustomerSupplierManagementForm).GetMethod("ReloadCustomerList",flags).Invoke(form,null));
                var visible=(List<SalesCustomer>)typeof(CustomerSupplierManagementForm).GetField("visibleCustomers",flags).GetValue(form);
                AssertAllowed("Real SQL relogin cannot restore previous customer editor",editor.Text.Length==0&&
                    Convert.ToString(typeof(CustomerSupplierManagementForm).GetField("editingCustomerId",flags).GetValue(form)).Length==0&&
                    InventoryQaDataSnapshot(visible)==InventoryQaDataSnapshot(expected));
            }

            AssertAllowed("Customer scoped read/selection leaves stored data unchanged",before==PermissionOwnedSnapshot(true));
            Console.WriteLine("PASS owned QA customer root: observed SQL lock/relogin/post-read denial, fresh full data parity, forged/write denial, actual AntdUI table binding, old selection rejected after real reauthentication, reload recovered; no customer/financial writes or native keys.");
        }

        private static int CountOwnedQaBlockedRequests(System.Data.SqlClient.SqlConnection connection,int blockerId,bool lockWaitOnly)
        {
            using(var command=connection.CreateCommand())
            {
                command.CommandText="SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@Blocker"+(lockWaitOnly?" AND wait_type LIKE N'LCK_M_%'":"");
                command.Parameters.Add("@Blocker",System.Data.SqlDbType.Int).Value=blockerId;command.CommandTimeout=2;
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void VerifyPersistedQuickSearchAccess(bool expectedAllowed)
        {
            string medicine;
            using(var connection=Database.CreateConnection())
            using(var command=new System.Data.SqlClient.SqlCommand("SELECT TOP(1) MedName FROM dbo.MedicineTb1 ORDER BY MedName",connection))
            {connection.Open();medicine=Convert.ToString(command.ExecuteScalar());}
            AssertAllowed("Search fixture contains medicine",!string.IsNullOrWhiteSpace(medicine));
            string financial=PermissionOwnedSnapshot(true), administration=PermissionOwnedSnapshot(false);

            var customerSession=ApplicationServices.CreateSalesCustomerReadSession();
            bool customersAllowed=PermissionService.CanOpen(AppPermission.Sales)||PermissionService.CanOpen(AppPermission.ManageManufacturers);
            AssertAllowed("Persisted customer read session follows actual screen grants",customerSession.IsCurrent==customersAllowed);
            if(customersAllowed)
                AssertAllowed("Persisted permitted customer root returns SQL rows",customerSession.Service.GetAllAsync(customerSession.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult().Count>0);
            else
                ExpectEmployeeAdministrationDenied(()=>customerSession.Service.GetAllAsync(customerSession.Actor,System.Threading.CancellationToken.None),"Persisted customer read denial");

            var accountingSession=ApplicationServices.CreateAccountingReadSession();
            if(!PermissionService.CanOpen(AppPermission.Accounting))
            {
                ExpectEmployeeAdministrationDenied(()=>accountingSession.Lookups.LoadAccounts(false,accountingSession.Actor),"Persisted accounting lookup screen denial");
                ExpectEmployeeAdministrationDenied(()=>accountingSession.History.Load(new JournalHistoryReadRequest(DateTime.Today,DateTime.Today,""),accountingSession.Actor),"Persisted accounting history denial");
                AssertDenied("Persisted accounting read scope is inactive",accountingSession.IsLookupCurrent||accountingSession.IsHistoryCurrent);
            }

            var medicineSession=ApplicationServices.CreateMedicineSession();
            bool medicinesAllowed=PermissionService.CanOpen(AppPermission.ManageMedicines)||PermissionService.CanOpen(AppPermission.Sales);
            if(medicinesAllowed)
                AssertAllowed("Persisted allowed medicine root SQL read",medicineSession.Service.GetAllAsync(medicineSession.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult().Count>0);
            else
                ExpectEmployeeAdministrationDenied(()=>medicineSession.Service.GetAllAsync(medicineSession.Actor,System.Threading.CancellationToken.None),"Persisted medicine root read denial; purchase alone is insufficient");
            if(!PermissionService.CanOpen(AppPermission.ManageMedicines))
            {
                ExpectEmployeeAdministrationDenied(()=>medicineSession.Service.GetCompanyNamesAsync(medicineSession.Actor,System.Threading.CancellationToken.None),"Persisted medicine company-management denial");
                ExpectEmployeeAdministrationDenied(()=>medicineSession.Service.GenerateAvailableBarcodeAsync("",medicineSession.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"Persisted medicine barcode-management denial");
                ExpectEmployeeAdministrationDenied(()=>medicineSession.Service.DeleteAsync("must-not-write",medicineSession.Actor),"Persisted medicine mutation-management denial");
            }

            var supplierSession=ApplicationServices.CreateManufacturerReadSession();
            bool suppliersAllowed=PermissionService.CanOpen(AppPermission.ManageManufacturers);
            AssertAllowed("Persisted supplier root follows actual management screen permission",supplierSession.IsCurrent==suppliersAllowed);
            if(suppliersAllowed)
                AssertAllowed("Persisted permitted supplier root returns SQL rows",supplierSession.Service.GetAllAsync(supplierSession.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult().Count>0);
            else
                ExpectEmployeeAdministrationDenied(()=>supplierSession.Service.GetAllAsync(supplierSession.Actor,System.Threading.CancellationToken.None),"Persisted supplier read denial; sales/purchase alone is insufficient");

            var session=ApplicationServices.CreateMedicineQuickSearchSession();
            if(expectedAllowed)
            {
                var records=session.Service.SearchAsync(medicine,session.Actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                AssertAllowed("Persisted permitted root search returns real medicine",records.Any(row=>row.MedName==medicine));
            }
            else {AssertDenied("Persisted denial invalidates root search",session.IsCurrent);RequireQuickSearchDenied(session);}
            using(var form=new MedicineQuickSearchForm(medicine,false,UiLanguageCatalog.IsEnglishActive))
            {
                var task=form.RunSearchAsync(); var deadline=DateTime.UtcNow.AddSeconds(20);
                while(!task.IsCompleted)
                {if(DateTime.UtcNow>deadline)throw new Exception("Actual QA quick search timed out.");System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(1);}
                task.GetAwaiter().GetResult();
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var grid=(DataGridView)typeof(MedicineQuickSearchForm).GetField("resultsGrid",flags).GetValue(form);
                var select=(AntdUI.Button)typeof(MedicineQuickSearchForm).GetField("selectButton",flags).GetValue(form);
                AssertAllowed("Actual QA quick search button follows stored screen permissions",select.Enabled==expectedAllowed);
                if(expectedAllowed)
                {
                    var rows=grid.DataSource as System.Collections.IEnumerable;
                    AssertAllowed("Actual QA quick search binds real medicine",rows!=null&&rows.Cast<object>().Any(row=>Convert.ToString(System.ComponentModel.TypeDescriptor.GetProperties(row)["MedName"]?.GetValue(row))==medicine));
                    SessionContext.Clear();
                    typeof(MedicineQuickSearchForm).GetMethod("SelectResult",flags).Invoke(form,null);
                    AssertAllowed("Logout clears actual QA search selection",!select.Enabled&&grid.DataSource==null&&form.SelectedMedicine==null&&form.DialogResult!=DialogResult.OK);
                }
                else AssertAllowed("Denied QA search exposes no rows",grid.DataSource==null&&form.SelectedMedicine==null);
            }
            AssertAllowed("QA search has no financial/inventory side effects",financial==PermissionOwnedSnapshot(true));
            AssertAllowed("QA search does not alter employees/permissions/audit",administration==PermissionOwnedSnapshot(false));
            Console.WriteLine("PASS persisted QA quick search: "+(expectedAllowed?"root SQL read, actual form binding and logout selection rejection":"stored screen denial blocks root read and actual form rows")+"; financial/inventory/permission/audit state unchanged; no native keys.");
        }

        private static void VerifyAuthenticatedCashierPosReference(string userName, string employeeId)
        {
            using(var form=new Billing())
            {
                form.Show(); System.Windows.Forms.Application.DoEvents();
                var flags=System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var selector=(AntdUI.Select)typeof(Billing).GetField("SelectEmployee",flags).GetValue(form);
                var idLabel=(Label)typeof(Billing).GetField("EmployeeIdLabel",flags).GetValue(form);
                AssertAllowed("Actual POS contains only its authenticated cashier identity", selector.Items.Count==1 &&
                    selector.SelectedIndex==0 && selector.Text==userName && !selector.Enabled && idLabel.Text.Contains(employeeId));
                form.Close();
            }
        }

        private static void AuthenticatePermissionFixture(string user, string password, string employeeId, string role)
        {
            SessionContext.Clear();
            AuthenticationResult result = ApplicationServices.Authentication.Authenticate(new AuthenticationRequest { UserName=user,Password=password });
            AssertAllowed("QA credentials verified from actual SQL store", result.Status==AuthenticationStatus.Success && result.EmployeeId==employeeId && result.Role==role);
            SessionContext.Start(result.UserName,result.Role,result.EmployeeId);
        }

        private static void SavePermissionEditorFixture(string employeeId, string userName, bool allowPosting, bool allowSales=true, bool allowPurchases=false)
        {
            using (var form = new EmployeeOperationPermissionsForm(employeeId,userName))
            {
                form.Show(); System.Windows.Forms.Application.DoEvents();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var grid = (DataGridView)typeof(EmployeeOperationPermissionsForm).GetField("grid",flags).GetValue(form);
                var save = (AntdUI.Button)typeof(EmployeeOperationPermissionsForm).GetField("saveButton",flags).GetValue(form);
                var desired = new Dictionary<string,bool> { {"Screen.Sales",allowSales}, {"Screen.Purchases",allowPurchases}, {"PostSalesInvoice",allowPosting} };
                foreach (DataGridViewRow row in grid.Rows)
                {
                    bool value;
                    if (row.Tag is string key && desired.TryGetValue(key,out value))
                        row.Cells["Override"].Value=value ? "سماح" : "منع";
                }
                AssertAllowed("Permission editor has changes ready to save", form.HasPendingChanges && save.Enabled);
                save.PerformClick(); System.Windows.Forms.Application.DoEvents();
                AssertAllowed("Permission editor confirms saved state without a blocking success dialog", !form.HasPendingChanges && !save.Enabled &&
                    Convert.ToBoolean(typeof(EmployeeOperationPermissionsForm).GetField("statusSuccess",flags).GetValue(form)));
                var stored=ApplicationServices.EmployeePermissions.GetByEmployeeId(employeeId);
                AssertAllowed("Fresh SQL permission read matches complete UI selection", stored.Overrides.Count==3 &&
                    desired.All(pair=>stored.Overrides.ContainsKey(pair.Key) && stored.Overrides[pair.Key]==pair.Value));
                form.Close();
            }
        }

        private static string PermissionOwnedSnapshot(bool financialOnly,int[] excludedJournalIds=null)
        {
            string[] tables=financialOnly
                ? new[] { "AccountingVouchersTb:VoucherId", "CurrencySettingsTb:CurrencyId", "JournalEntriesTb:JournalEntryId", "JournalEntryLinesTb:JournalEntryLineId", "CashBoxJournalLinksTb:JournalEntryLineId", "MedicineTb1:MedName", "InventoryLotsTb:LotId", "InventoryMovementsTb:MovementId", "CustomersBillsTb:CustomerId", "CompanyTb1:CompId", "AccountsTb:AccountCode", "CostCentersTb:CostCenterId" }
                : new[] { "EmployeeTb1:EmpId", "EmployeeOperationPermissionsTb:EmpId,OperationName", "AuditLogTb:AuditId" };
            var text=new System.Text.StringBuilder();
            using(var connection=Database.CreateConnection())
            {
                connection.Open();
                foreach(string table in tables)
                {
                    string[] pair=table.Split(':');
                    string filter=string.Empty;
                    if(excludedJournalIds!=null && excludedJournalIds.Length>0 && (pair[0]=="JournalEntriesTb" || pair[0]=="JournalEntryLinesTb"))
                    {
                        if(excludedJournalIds.Any(id=>id<=0))throw new ArgumentException("Only positive accepted journal ids can be excluded.");
                        filter=" WHERE JournalEntryId NOT IN ("+string.Join(",",excludedJournalIds)+")";
                    }
                    using(var command=new System.Data.SqlClient.SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+pair[0]+"]"+filter+" ORDER BY "+pair[1]+" FOR XML RAW('r'),ROOT('rows'),TYPE));",connection))
                        text.Append(pair[0]).Append(Convert.ToString(command.ExecuteScalar()));
                }
            }
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }

        private static void VerifyEmployeeAdministrationAuthorization()
        {
            Action<OperationActor> deny = actor =>
            {
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.GetAll(actor), "Employee administrative read identity");
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Create(null, actor), "Employee create identity");
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Update(null, actor), "Employee update identity");
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.Employees.Delete("QA-NO-SQL", actor), "Employee delete identity");
                ExpectEmployeeAdministrationDenied(() => ApplicationServices.EmployeePermissions.Replace("QA-NO-SQL", null, actor), "Permission replace identity");
            };
            try
            {
                var requestedManager = new OperationActor { UserName="permission-admin-test", UserRole="Manager" };
                SessionContext.Clear(); deny(requestedManager);
                SessionContext.Start(requestedManager.UserName, "Cashier"); deny(requestedManager);
                SessionContext.Start("different-manager", "Manager"); deny(requestedManager);
                SessionContext.Start(requestedManager.UserName, requestedManager.UserRole);
                bool employeeValidated = false, permissionsValidated = false;
                try { ApplicationServices.Employees.Create(null, requestedManager); }
                catch (ArgumentNullException) { employeeValidated = true; }
                try { ApplicationServices.EmployeePermissions.Replace(string.Empty, null, requestedManager); }
                catch (InvalidOperationException) { permissionsValidated = true; }
                AssertAllowed("Authenticated manager reaches validation before SQL", employeeValidated && permissionsValidated);
            }
            finally { SessionContext.Clear(); }
        }

        private static void ExpectEmployeeAdministrationDenied(Action operation, string scenario)
        {
            bool denied = false;
            try { operation(); }
            catch (UnauthorizedAccessException) { denied = true; }
            AssertAllowed(scenario, denied);
        }

        private static void AssertAllowed(string scenario, bool value)
        {
            if (!value)
            {
                throw new InvalidOperationException("Permission policy should allow: " + scenario);
            }
        }

        private static void AssertDenied(string scenario, bool value)
        {
            if (value)
            {
                throw new InvalidOperationException("Permission policy should deny: " + scenario);
            }
        }
    }
}






