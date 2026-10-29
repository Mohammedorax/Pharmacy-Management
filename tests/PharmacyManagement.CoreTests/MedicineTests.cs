using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class MedicineTests
    {
        internal static int Run()
        {
            int checks = 0;
            int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            using (var begun = new System.Threading.ManualResetEventSlim())
            using (var release = new System.Threading.ManualResetEventSlim())
            {
                int boundaryThread = ownerThread;
                var transactionWait = SqlTransactionBoundaryDispatcher.DispatchAsync(() =>
                {
                    boundaryThread = System.Threading.Thread.CurrentThread.ManagedThreadId; begun.Set();
                    release.Wait(TimeSpan.FromSeconds(2)); return 37;
                });
                try
                {
                    Check(!transactionWait.IsCompleted, "Transaction boundary blocked the caller before returning its task.", ref checks);
                    Check(begun.Wait(TimeSpan.FromSeconds(2)) && boundaryThread != ownerThread, "Transaction boundary ran on the UI/caller thread.", ref checks);
                }
                finally { release.Set(); transactionWait.GetAwaiter().GetResult(); }
                Check(transactionWait.Result == 37, "Transaction boundary changed result.", ref checks);
            }
            using (var begun = new System.Threading.ManualResetEventSlim())
            using (var release = new System.Threading.ManualResetEventSlim())
            {
                int boundaryThread = ownerThread;
                var transactionWait = SqlTransactionBoundaryDispatcher.DispatchAsync((Action)(() =>
                {
                    boundaryThread = System.Threading.Thread.CurrentThread.ManagedThreadId; begun.Set();
                    release.Wait(TimeSpan.FromSeconds(2));
                }));
                try
                {
                    Check(!transactionWait.IsCompleted, "Void commit/rollback boundary blocked its caller.", ref checks);
                    Check(begun.Wait(TimeSpan.FromSeconds(2)) && boundaryThread != ownerThread, "Commit/rollback boundary used caller thread.", ref checks);
                }
                finally { release.Set(); transactionWait.GetAwaiter().GetResult(); }
            }
            bool executed = false;
            SqlTransactionBoundaryDispatcher.DispatchAsync((Action)(() => executed = true)).GetAwaiter().GetResult();
            Check(executed, "Void transaction boundary did not finish.", ref checks);
            bool originalFault = false;
            try { SqlTransactionBoundaryDispatcher.DispatchAsync((Action)(() => { throw new InvalidOperationException("boundary-fault"); })).GetAwaiter().GetResult(); }
            catch (InvalidOperationException ex) { originalFault = ex.Message == "boundary-fault"; }
            Check(originalFault, "Transaction boundary lost operation failure.", ref checks);
            var repository = new FakeRepository();
            var managerActor = new OperationActor { UserName = "admin", UserRole = "Manager" };
            var service = new MedicineService(repository, actor => actor.UserName == "admin" && actor.UserRole == "Manager", () => "6281100000999",
                actor => actor.UserName == "reader" && actor.UserRole == "Sales" || actor.UserName == "admin" && actor.UserRole == "Manager");
            var readerActor = new OperationActor { UserName = "reader", UserRole = "Sales" };
            repository.NameRead = token => System.Threading.Tasks.Task.FromResult<IReadOnlyList<string>>(new[] { "باراسيتامول 500 mg", "دواء آخر" });
            using (var namesCancellation = new System.Threading.CancellationTokenSource())
            {
                var names = service.GetNamesAsync(readerActor, namesCancellation.Token).GetAwaiter().GetResult();
                Check(names.Count == 2 && names[0] == "باراسيتامول 500 mg" && names[1] == "دواء آخر" && repository.LastToken == namesCancellation.Token,
                    "Authorized medicine name read changed values or cancellation token.", ref checks);
                namesCancellation.Cancel();
                int beforeCanceledNames = repository.Accesses;
                bool canceled = false;
                try { service.GetNamesAsync(readerActor, namesCancellation.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled && repository.Accesses == beforeCanceledNames, "Canceled medicine name read reached the repository.", ref checks);
            }
            MedicineRecord created = service.Create(Valid(), new OperationActor { UserName = " admin ", UserRole = " Manager " });
            Check(created.MedName == "باراسيتامول 500 mg" && created.Barcode == "6281100000999" &&
                repository.Last.CompanyName == "شركة الدواء" && repository.Actor.UserName == "admin",
                "Medicine normalization or generated barcode changed.", ref checks);

            MedicineRecord update = Valid();
            update.Barcode = " ";
            Check(service.Update(update, managerActor) && repository.Last.Barcode == string.Empty,
                "Optional update barcode changed.", ref checks);
            Check(service.Delete(" باراسيتامول 500 mg ", managerActor) && repository.DeletedName == "باراسيتامول 500 mg",
                "Medicine delete normalization changed.", ref checks);

            MedicineRecord invalid = Valid(); invalid.BuyingPrice = 0;
            Reject(() => service.Create(invalid, managerActor), "Zero buying price was accepted.", ref checks);
            invalid = Valid(); invalid.Quantity = -1;
            Reject(() => service.Update(invalid, managerActor), "Negative quantity was accepted.", ref checks);
            invalid = Valid(); invalid.ProductionDate = invalid.ExpiryDate.AddDays(1);
            Reject(() => service.Create(invalid, managerActor), "Production date after expiry was accepted.", ref checks);
            invalid = Valid(); invalid.CompanyName = new string('x', 201);
            Reject(() => service.Create(invalid, managerActor), "Oversized company name was accepted.", ref checks);

            repository.BarcodeAvailable = false;
            Reject(() => service.GenerateAvailableBarcode("دواء", managerActor), "Exhausted barcode candidates were accepted.", ref checks);

            repository.BarcodeAvailable = true;
            Check(service.GenerateAvailableBarcodeAsync("دواء", managerActor, System.Threading.CancellationToken.None).GetAwaiter().GetResult() == "6281100000999", "Async barcode changed candidate.", ref checks);
            repository.BarcodeAvailable = false;
            int beforeExhausted = repository.Accesses;
            Reject(() => service.GenerateAvailableBarcodeAsync("دواء", managerActor, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), "Async barcode exhaustion accepted.", ref checks);
            Check(repository.Accesses == beforeExhausted + 20, "Async barcode retry bound changed.", ref checks);
            repository.BarcodeAvailable = true;
            using (var canceledBarcode = new System.Threading.CancellationTokenSource())
            {
                canceledBarcode.Cancel(); int before = repository.Accesses;
                try { service.GenerateAvailableBarcodeAsync("دواء", managerActor, canceledBarcode.Token).GetAwaiter().GetResult(); throw new Exception("Canceled barcode accepted."); }
                catch (OperationCanceledException) { checks++; }
                Check(repository.Accesses == before, "Canceled barcode reached SQL.", ref checks);
            }
            bool stillAllowed = true;
            var guarded = new MedicineService(repository, actor => stillAllowed, () => "6281100000999");
            var pendingBarcode = new System.Threading.Tasks.TaskCompletionSource<bool>();
            repository.BarcodeRead = token => pendingBarcode.Task;
            var pendingResult = guarded.GenerateAvailableBarcodeAsync("دواء", managerActor, System.Threading.CancellationToken.None);
            Check(!pendingResult.IsCompleted, "Barcode did not await repository.", ref checks);
            stillAllowed = false; pendingBarcode.SetResult(true);
            Deny(() => pendingResult.GetAwaiter().GetResult(), "Permission revoked during barcode wait was ignored.", ref checks);
            using (var during = new System.Threading.CancellationTokenSource())
            {
                pendingBarcode = new System.Threading.Tasks.TaskCompletionSource<bool>();
                var canceledDuring = service.GenerateAvailableBarcodeAsync("دواء", managerActor, during.Token);
                during.Cancel(); pendingBarcode.SetResult(true);
                try { canceledDuring.GetAwaiter().GetResult(); throw new Exception("Late barcode ignored cancellation."); }
                catch (OperationCanceledException) { checks++; }
            }
            repository.BarcodeRead = null;
            int beforeDenied = repository.Accesses;
            foreach (var actor in new OperationActor[] { null, new OperationActor { UserName = " ", UserRole = "Manager" },
                new OperationActor { UserName = "admin", UserRole = " " }, new OperationActor { UserName = "forged", UserRole = "Manager" },
                new OperationActor { UserName = "reader", UserRole = "Sales" } })
            {
                Deny(() => service.CreateAsync(Valid(), actor).GetAwaiter().GetResult(), "Unauthorized async create.", ref checks);
                Deny(() => service.UpdateAsync(null, actor).GetAwaiter().GetResult(), "Unauthorized async update.", ref checks);
                Deny(() => service.DeleteAsync(null, actor).GetAwaiter().GetResult(), "Unauthorized async delete.", ref checks);
                Deny(() => service.Create(Valid(), actor), "Unauthorized medicine create was accepted.", ref checks);
                Deny(() => service.Update(null, actor), "Authorization must precede medicine validation.", ref checks);
                Deny(() => service.Delete(null, actor), "Unauthorized medicine delete was accepted.", ref checks);
                Deny(() => service.GetCompanyNames(actor), "Unauthorized supplier options were read.", ref checks);
                Deny(() => service.GetCompanyNamesAsync(actor, System.Threading.CancellationToken.None), "Unauthorized async company options were read.", ref checks);
                Deny(() => service.GenerateAvailableBarcodeAsync(null, actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), "Unauthorized async barcode accepted.", ref checks);
                Deny(() => service.GenerateAvailableBarcode(null, actor), "Unauthorized barcode generation was accepted.", ref checks);
                if (actor == null || actor.UserName != "reader")
                {
                    Deny(() => service.GetAll(actor), "Unauthorized medicine directory was read.", ref checks);
                    Deny(() => service.FindByNameAsync("دواء", actor, System.Threading.CancellationToken.None), "Unauthorized medicine lookup.", ref checks);
                    Deny(() => service.GetAllAsync(actor, System.Threading.CancellationToken.None), "Unauthorized async medicine directory was read.", ref checks);
                }
            }
            Check(repository.Accesses == beforeDenied, "Denied medicine request reached repository or barcode storage.", ref checks);
            var reader = new OperationActor { UserName = "reader", UserRole = "Sales" };
            Check(service.GetAll(reader).Count == 0, "Authorized sales reader lost directory access.", ref checks);
            Check(service.GetCompanyNames(managerActor).Count == 1, "Authorized manager lost supplier options.", ref checks);
            using (var cancellation = new System.Threading.CancellationTokenSource())
            {
                Check(service.FindByNameAsync(" دواء 500 mg ", reader, cancellation.Token).GetAwaiter().GetResult() == repository.Last && repository.LastLookupName == " دواء 500 mg " && repository.LastToken == cancellation.Token,
                    "Medicine lookup changed token, normalization or result.", ref checks);
                Reject(() => service.FindByNameAsync("", reader, cancellation.Token), "Empty lookup reached storage.", ref checks);
                Reject(() => service.FindByNameAsync(new string('x', 201), reader, cancellation.Token), "Oversized lookup reached storage.", ref checks);
                Check(service.GetAllAsync(reader, cancellation.Token).GetAwaiter().GetResult().Count == 0 && repository.LastToken == cancellation.Token,
                    "Authorized async medicine read lost token or data.", ref checks);
                Check(service.GetCompanyNamesAsync(managerActor, cancellation.Token).GetAwaiter().GetResult().Count == 1 && repository.LastToken == cancellation.Token,
                    "Authorized async company options lost token or data.", ref checks);
                int beforeCancel = repository.Accesses; cancellation.Cancel();
                foreach (Action action in new Action[] { () => service.GetAllAsync(managerActor, cancellation.Token), () => service.GetCompanyNamesAsync(managerActor, cancellation.Token), () => service.FindByNameAsync("دواء", managerActor, cancellation.Token) })
                {
                    bool canceled = false; try { action(); } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled, "Canceled medicine read reached storage.", ref checks);
                }
                Check(repository.Accesses == beforeCancel, "Canceled medicine directory/options accessed repository.", ref checks);
            }
            try { new MedicineService(repository, (Func<OperationActor, bool>)null); throw new InvalidOperationException("Missing medicine policy accepted."); }
            catch (ArgumentNullException) { checks++; }

            var asyncRow = Valid();
            var capturedActor = new OperationActor { UserName = " admin ", UserRole = " Manager " };
            var delayedWrite = new System.Threading.Tasks.TaskCompletionSource<bool>();
            repository.WriteWait = () => delayedWrite.Task;
            var creating = service.CreateAsync(asyncRow, capturedActor);
            Check(!creating.IsCompleted && repository.Last.Barcode == "6281100000999", "Async create did not await write or generate barcode.", ref checks);
            asyncRow.MedName = "changed draft"; capturedActor.UserName = "forged";
            delayedWrite.SetResult(true);
            var asyncCreated = creating.GetAwaiter().GetResult();
            Check(asyncCreated.MedName == "باراسيتامول 500 mg" && repository.Actor.UserName == "admin", "Async write retained mutable draft or actor.", ref checks);
            repository.WriteWait = null;
            Check(service.UpdateAsync(Valid(), managerActor).GetAwaiter().GetResult(), "Async update result changed.", ref checks);
            Check(service.DeleteAsync(" دواء ", managerActor).GetAwaiter().GetResult() && repository.DeletedName == "دواء", "Async delete normalization changed.", ref checks);
            invalid = Valid(); invalid.Quantity = -1;
            Reject(() => service.CreateAsync(invalid, managerActor).GetAwaiter().GetResult(), "Invalid async create accepted.", ref checks);
            Reject(() => service.UpdateAsync(invalid, managerActor).GetAwaiter().GetResult(), "Invalid async update accepted.", ref checks);
            Reject(() => service.DeleteAsync("", managerActor).GetAwaiter().GetResult(), "Empty async delete accepted.", ref checks);
            var catalogRepository = new FakeCatalogImportRepository();
            var catalog = new MedicineCatalogImportService(catalogRepository);
            var catalogRow = Valid(); catalogRow.Quantity = 0; catalogRow.Barcode = "6281100000111";
            var manager = new OperationActor { UserName = " admin ", UserRole = " Manager " };
            MedicineCatalogImportResult catalogResult = catalog.ImportNew(new[] { catalogRow }, manager);
            Check(catalogResult.Created == 1 && catalogRepository.Rows[0].Quantity == 0 &&
                catalogRepository.Actor.UserName == "admin", "Zero-stock catalog import contract failed.", ref checks);
            catalogRow.Quantity = 2;
            Reject(() => catalog.ImportNew(new[] { catalogRow }, manager), "Catalog import accepted direct stock.", ref checks);
            catalogRow.Quantity = 0;
            Reject(() => catalog.ImportNew(new[] { catalogRow, catalogRow }, manager), "Catalog import accepted duplicate rows.", ref checks);
            try { catalog.ImportNew(new[] { catalogRow }, new OperationActor { UserName = "cashier", UserRole = "Cashier" });
                throw new InvalidOperationException("Cashier imported a catalog."); }
            catch (UnauthorizedAccessException) { checks++; }
            checks += VerifyPendingMedicineReadAuthorization();
            return checks;
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? string.Empty).ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = true,
                ConnectTimeout = 5,
                ApplicationName = "Pharmacy.CoreTests.Medicine.ReadOnly"
            };
            var repository = new SqlMedicineRepository(() => new SqlConnection(builder.ConnectionString));
            var actor = new OperationActor { UserName = "qa-medicine-reader", UserRole = "Manager" };
            var service = new MedicineService(repository,
                request => request.UserName == actor.UserName && request.UserRole == actor.UserRole);
            IReadOnlyList<MedicineRecord> medicines = service.GetAll(actor);
            IReadOnlyList<string> companies = service.GetCompanyNames(actor);
            var asyncMedicines = service.GetAllAsync(actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            var asyncCompanies = service.GetCompanyNamesAsync(actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            if (asyncMedicines.Count != medicines.Count || asyncCompanies.Count != companies.Count) throw new InvalidOperationException("Async medicine/options count differs from synchronous read.");
            for (int i = 0; i < medicines.Count; i++)
            {
                var a = medicines[i]; var b = asyncMedicines[i];
                var selected = service.FindByNameAsync(a.MedName, actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (selected == null || selected.MedName != a.MedName || selected.Barcode != a.Barcode || selected.BuyingPrice != a.BuyingPrice || selected.SellingPrice != a.SellingPrice || selected.Quantity != a.Quantity || selected.ProductionDate != a.ProductionDate || selected.ExpiryDate != a.ExpiryDate || selected.CompanyName != a.CompanyName)
                    throw new InvalidOperationException("Targeted medicine lookup changed any field.");
                if (a.MedName != b.MedName || a.Barcode != b.Barcode || a.BuyingPrice != b.BuyingPrice || a.SellingPrice != b.SellingPrice ||
                    a.Quantity != b.Quantity || a.ProductionDate != b.ProductionDate || a.ExpiryDate != b.ExpiryDate || a.CompanyName != b.CompanyName)
                    throw new InvalidOperationException("Async medicine read changed field or ordering.");
            }
            for (int i = 0; i < companies.Count; i++) if (asyncCompanies[i] != companies[i]) throw new InvalidOperationException("Async company options changed text or order.");
            if (service.FindByNameAsync("QA-MISSING-MEDICINE-20261003", actor, System.Threading.CancellationToken.None).GetAwaiter().GetResult() != null)
                throw new InvalidOperationException("Missing medicine lookup returned another record.");
            var probes = new List<string> { "QA-BARCODE-NOT-PRESENT-20261003", "" };
            foreach (var medicine in medicines) if (!string.IsNullOrWhiteSpace(medicine.Barcode)) probes.Add(medicine.Barcode);
            foreach (string probe in probes)
            foreach (string excluded in new[] { "", medicines.Count == 0 ? "" : medicines[0].MedName })
            {
                bool original = repository.IsBarcodeAvailable(probe, excluded);
                bool asynchronous = repository.IsBarcodeAvailableAsync(probe, excluded, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (original != asynchronous) throw new InvalidOperationException("Async barcode availability differs from previous SQL predicate.");
            }
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM dbo.MedicineTb1;";
                connection.Open();
                int expected = Convert.ToInt32(command.ExecuteScalar());
                if (medicines.Count != expected) throw new InvalidOperationException("Medicine typed read count changed.");
            }
            if (companies.Count == 0) throw new InvalidOperationException("No manufacturer reference data was returned.");
            Console.WriteLine("PASS: authorized sync/async medicine fields/options and barcode availability probes match. Database=" + database +
                "; Medicines=" + medicines.Count + "; Companies=" + companies.Count + "; PersistentWrites=0.");
        }

        internal static void VerifyQaCatalogImportRollback(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Explicit _qa database suffix required for rollback import test.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, ConnectTimeout = 5, ApplicationName = "Pharmacy.CoreTests.CatalogImport.Rollback" };
            var repository = new SqlMedicineRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<string> companies = repository.GetCompanyNames();
            if (companies.Count == 0) throw new InvalidOperationException("QA needs a registered company for catalog import.");
            string unique = Guid.NewGuid().ToString("N");
            var first = new MedicineRecord { MedName = "QA-CATALOG-" + unique,
                Barcode = "QA-CAT-" + unique, BuyingPrice = 10, SellingPrice = 15, Quantity = 0,
                ProductionDate = new DateTime(2026, 1, 1), ExpiryDate = new DateTime(2027, 1, 1),
                CompanyName = companies[0] };
            var actor = new OperationActor { UserName = "QA-catalog-check", UserRole = "Manager" };
            int medicineBefore = CountQaRows(builder, "dbo.MedicineTb1");
            int auditBefore = CountQaRows(builder, "dbo.AuditLogTb");
            int movementBefore = CountQaRows(builder, "dbo.InventoryMovementsTb");
            var service = new MedicineCatalogImportService(new RollbackCatalogRepository(repository));
            MedicineCatalogImportResult result = service.ImportNew(new[] { first }, actor);
            if (result.Created != 1 || result.SkippedExisting != 0)
                throw new InvalidOperationException("QA catalog import rollback did not execute insertion SQL.");
            AssertQaUnchanged(builder, first.MedName, medicineBefore, auditBefore, movementBefore);

            var missingCompany = new MedicineRecord { MedName = "QA-CATALOG-FAIL-" + unique,
                Barcode = "QA-CAT-FAIL-" + unique, BuyingPrice = 10, SellingPrice = 15, Quantity = 0,
                ProductionDate = first.ProductionDate, ExpiryDate = first.ExpiryDate,
                CompanyName = "QA-MISSING-COMPANY-" + unique };
            try { service.ImportNew(new[] { first, missingCompany }, actor);
                throw new InvalidOperationException("QA catalog import accepted an unknown company."); }
            catch (InvalidOperationException exception)
            {
                if (!exception.Message.Contains("الشركة المصنعة غير مسجلة")) throw;
            }
            AssertQaUnchanged(builder, first.MedName, medicineBefore, auditBefore, movementBefore);
            Console.WriteLine("PASS: QA catalog import insert and failure both rolled back. Database=" + database + "; PersistentWrites=0.");
        }

        private static int CountQaRows(SqlConnectionStringBuilder builder, string table)
        {
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            { command.CommandText = "SELECT COUNT(1) FROM " + table + ";";
                connection.Open(); return Convert.ToInt32(command.ExecuteScalar()); }
        }

        private static void AssertQaUnchanged(SqlConnectionStringBuilder builder, string medicineName,
            int medicineBefore, int auditBefore, int movementBefore)
        {
            if (CountQaRows(builder, "dbo.MedicineTb1") != medicineBefore ||
                CountQaRows(builder, "dbo.AuditLogTb") != auditBefore ||
                CountQaRows(builder, "dbo.InventoryMovementsTb") != movementBefore)
                throw new InvalidOperationException("QA catalog import rollback left persistent rows.");
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM dbo.MedicineTb1 WHERE MedName=@Name;";
                command.Parameters.AddWithValue("@Name", medicineName);
                connection.Open();
                if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                    throw new InvalidOperationException("QA catalog import rollback left its medicine row.");
            }
        }

        private static int VerifyPendingMedicineReadAuthorization()
        {
            int checks=0;
            for(int kind=0;kind<3;kind++)
            for(int scenario=0;scenario<3;scenario++)
            {
                bool authorized=true;
                var repository=new FakeRepository();
                var reply=new System.Threading.Tasks.TaskCompletionSource<object>();
                repository.ListRead=token=>ConvertMedicineRead<IReadOnlyList<MedicineRecord>>(reply.Task);
                repository.CompanyRead=token=>ConvertMedicineRead<IReadOnlyList<string>>(reply.Task);
                repository.SelectedRead=token=>ConvertMedicineRead<MedicineRecord>(reply.Task);
                var service=new MedicineService(repository, candidate=>authorized&&candidate.UserName=="admin"&&candidate.UserRole=="Manager");
                var actor=new OperationActor{UserName="admin",UserRole="Manager"};
                using(var cancellation=new System.Threading.CancellationTokenSource())
                {
                    var pending=kind==0?BoxMedicineRead(service.GetAllAsync(actor,cancellation.Token)):
                        kind==1?BoxMedicineRead(service.GetCompanyNamesAsync(actor,cancellation.Token)):
                        BoxMedicineRead(service.FindByNameAsync("  دواء 500 mg  ",actor,cancellation.Token));
                    if(scenario==0)authorized=false;
                    if(scenario==1)actor.UserName="forged";
                    if(scenario==2)cancellation.Cancel();
                    object value=kind==0?(object)new[]{Valid()}:kind==1?(object)new[]{"شركة الدواء"}:Valid();
                    reply.SetResult(value);
                    if(scenario==0)Deny(()=>pending.GetAwaiter().GetResult(),"Medicine pending read leaked data after authorization revocation; kind="+kind,ref checks);
                    if(scenario==1)Check(pending.GetAwaiter().GetResult()!=null,"Medicine pending read lost captured actor; kind="+kind,ref checks);
                    if(scenario==2)
                    {
                        bool stopped=false;try{pending.GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}
                        Check(stopped,"Medicine pending read ignored late cancellation; kind="+kind,ref checks);
                    }
                }
            }
            var authorizedActor=new OperationActor{UserName="admin",UserRole="Manager"};
            for(int kind=0;kind<2;kind++)
            {
                var repository=new FakeRepository();
                repository.ListRead=token=>System.Threading.Tasks.Task.FromResult<IReadOnlyList<MedicineRecord>>(null);
                repository.CompanyRead=token=>System.Threading.Tasks.Task.FromResult<IReadOnlyList<string>>(null);
                var service=new MedicineService(repository,actor=>true);
                Reject(()=>{if(kind==0)service.GetAllAsync(authorizedActor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    else service.GetCompanyNamesAsync(authorizedActor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();},
                    "Missing medicine list result was accepted; kind="+kind,ref checks);
            }
            var sourceMedicines=new List<MedicineRecord>{Valid()};
            var sourceCompanies=new List<string>{"شركة الدواء"};
            var reusedRepository=new FakeRepository{
                ListRead=token=>System.Threading.Tasks.Task.FromResult<IReadOnlyList<MedicineRecord>>(sourceMedicines),
                CompanyRead=token=>System.Threading.Tasks.Task.FromResult<IReadOnlyList<string>>(sourceCompanies),
                SelectedRead=token=>System.Threading.Tasks.Task.FromResult<MedicineRecord>(null)};
            var reusedService=new MedicineService(reusedRepository,actor=>true);
            var acceptedMedicines=reusedService.GetAllAsync(authorizedActor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            var acceptedCompanies=reusedService.GetCompanyNamesAsync(authorizedActor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            sourceMedicines.Clear();sourceCompanies.Clear();
            Check(acceptedMedicines.Count==1,"Medicine result changed when storage reused its list.",ref checks);
            Check(acceptedCompanies.Count==1,"Company result changed when storage reused its list.",ref checks);
            Check(reusedService.FindByNameAsync("missing",authorizedActor,System.Threading.CancellationToken.None).GetAwaiter().GetResult()==null,
                "Targeted medicine not-found null result was lost.",ref checks);
            return checks;
        }
        private static async System.Threading.Tasks.Task<T> ConvertMedicineRead<T>(System.Threading.Tasks.Task<object> task)
        {return (T)await task.ConfigureAwait(false);}
        private static async System.Threading.Tasks.Task<object> BoxMedicineRead<T>(System.Threading.Tasks.Task<T> task)
        {return await task.ConfigureAwait(false);}

        private static void Deny(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (UnauthorizedAccessException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private static MedicineRecord Valid()
        {
            return new MedicineRecord
            {
                MedName = " باراسيتامول 500 mg ",
                Barcode = string.Empty,
                BuyingPrice = 10,
                SellingPrice = 15,
                Quantity = 4,
                ProductionDate = new DateTime(2026, 1, 1),
                ExpiryDate = new DateTime(2027, 1, 1),
                CompanyName = " شركة الدواء "
            };
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Reject(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeRepository : IMedicineRepository
        {
            internal int Accesses;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<MedicineRecord>>> ListRead;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<string>>> NameRead;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<string>>> CompanyRead;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<MedicineRecord>> SelectedRead;
            internal System.Threading.CancellationToken LastToken;
            internal MedicineRecord Last;
            internal OperationActor Actor;
            internal string DeletedName;
            internal bool BarcodeAvailable = true;
            public IReadOnlyList<MedicineRecord> GetAll() { Accesses++; return new MedicineRecord[0]; }
            public System.Threading.Tasks.Task<IReadOnlyList<MedicineRecord>> GetAllAsync(System.Threading.CancellationToken token)
            { LastToken = token; return ListRead==null?System.Threading.Tasks.Task.FromResult(GetAll()):ListRead(token); }
            public System.Threading.Tasks.Task<IReadOnlyList<string>> GetNamesAsync(System.Threading.CancellationToken token)
            { Accesses++; LastToken = token; return NameRead==null?System.Threading.Tasks.Task.FromResult<IReadOnlyList<string>>(new string[0]):NameRead(token); }
            public System.Threading.Tasks.Task<IReadOnlyList<string>> GetCompanyNamesAsync(System.Threading.CancellationToken token)
            { LastToken = token; return CompanyRead==null?System.Threading.Tasks.Task.FromResult(GetCompanyNames()):CompanyRead(token); }
            internal string LastLookupName;
            public System.Threading.Tasks.Task<MedicineRecord> FindByNameAsync(string medicineName, System.Threading.CancellationToken token)
            { Accesses++; LastLookupName = medicineName; LastToken = token; return SelectedRead==null?System.Threading.Tasks.Task.FromResult(Last):SelectedRead(token); }
            public IReadOnlyList<string> GetCompanyNames() { Accesses++; return new[] { "شركة الدواء" }; }
            internal Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<bool>> BarcodeRead;
            public System.Threading.Tasks.Task<bool> IsBarcodeAvailableAsync(string barcode, string excludedMedicineName, System.Threading.CancellationToken token)
            { Accesses++; LastToken = token; return BarcodeRead == null ? System.Threading.Tasks.Task.FromResult(BarcodeAvailable) : BarcodeRead(token); }
            public bool IsBarcodeAvailable(string barcode, string excludedMedicineName) { Accesses++; return BarcodeAvailable; }
            internal Func<System.Threading.Tasks.Task> WriteWait;
            public async System.Threading.Tasks.Task CreateAsync(MedicineRecord medicine, OperationActor actor)
            { Create(medicine, actor); if (WriteWait != null) await WriteWait(); }
            public System.Threading.Tasks.Task<bool> UpdateAsync(MedicineRecord medicine, OperationActor actor)
            { return System.Threading.Tasks.Task.FromResult(Update(medicine, actor)); }
            public System.Threading.Tasks.Task<bool> DeleteAsync(string medicineName, OperationActor actor)
            { return System.Threading.Tasks.Task.FromResult(Delete(medicineName, actor)); }
            public void Create(MedicineRecord medicine, OperationActor actor) { Accesses++; Last = medicine; Actor = actor; }
            public bool Update(MedicineRecord medicine, OperationActor actor) { Accesses++; Last = medicine; Actor = actor; return true; }
            public bool Delete(string medicineName, OperationActor actor) { Accesses++; DeletedName = medicineName; Actor = actor; return true; }
        }

        private sealed class FakeCatalogImportRepository : IMedicineCatalogImportRepository
        {
            internal IReadOnlyList<MedicineRecord> Rows;
            internal OperationActor Actor;
            public MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor)
            { Rows = medicines; Actor = actor; return new MedicineCatalogImportResult { Created = medicines.Count }; }
        }

        private sealed class RollbackCatalogRepository : IMedicineCatalogImportRepository
        {
            private readonly SqlMedicineRepository repository;
            internal RollbackCatalogRepository(SqlMedicineRepository repository) { this.repository = repository; }
            public MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor)
            { return repository.VerifyQaImportRollback(medicines, actor); }
        }
    }
}
