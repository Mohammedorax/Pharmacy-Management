using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class ManufacturerTests
    {
        internal static int Run()
        {
            int checks = 0;
            Check(typeof(IManufacturerService).GetMethod("GetAll", Type.EmptyTypes) == null,
                "Supplier service contract exposes a directory read without actor authorization.", ref checks);
            Check(typeof(ManufacturerService).GetMethod("GetAll", Type.EmptyTypes) == null,
                "Supplier concrete service exposes a directory read without actor authorization.", ref checks);
            var repository = new FakeRepository();
            var service = new ManufacturerService(repository, actor => actor.UserName == "admin" && actor.UserRole == "Manager");
            var input = new ManufacturerRecord
            {
                CompId = "  c-1 ", CompName = " شركة الدواء ", CompPhone = " 777 ", CompAddress = " صنعاء "
            };
            RejectUnauthorized(() => service.Create(input, null), "Unsigned supplier creation was accepted.", ref checks);
            service.Create(input, new OperationActor { UserName = " admin ", UserRole = " Manager " });
            Check(repository.Last.CompId == "c-1" && repository.Last.CompName == "شركة الدواء" &&
                repository.Last.CompPhone == "777" && repository.Last.CompAddress == "صنعاء",
                "Manufacturer normalization changed.", ref checks);
            Check(repository.Actor.UserName == "admin" && repository.Actor.UserRole == "Manager",
                "Operation actor normalization changed.", ref checks);

            var allowed = new OperationActor { UserName = "admin", UserRole = "Manager" };
            Reject(() => service.Create(new ManufacturerRecord(), allowed), "Empty manufacturer was accepted.", ref checks);
            Reject(() => service.Delete(new string('x', 51), allowed), "Oversized company id was accepted.", ref checks);
            ManufacturerRecord before = repository.Last;
            foreach (var denied in new[] { null, new OperationActor { UserName = "admin", UserRole = "Cashier" },
                new OperationActor { UserName = "other", UserRole = "Manager" } })
            {
                RejectUnauthorized(() => service.Create(null, denied), "Denied supplier create reached validation.", ref checks);
                RejectUnauthorized(() => service.Update(input, denied), "Denied supplier update was accepted.", ref checks);
                RejectUnauthorized(() => service.Delete("c-1", denied), "Denied supplier delete was accepted.", ref checks);
                Check(ReferenceEquals(before, repository.Last), "Denied supplier operation reached storage.", ref checks);
            }
            var delegated = new ManufacturerService(repository, actor => actor.UserName == "delegated" && actor.UserRole == "Inventory");
            delegated.Create(input, new OperationActor { UserName = "delegated", UserRole = "Inventory" });
            Check(repository.Actor.UserName == "delegated", "Delegated supplier access was lost.", ref checks);
            Check(service.GetAllAsync(allowed, CancellationToken.None).GetAwaiter().GetResult().Count == 0,
                "Async supplier reference rows changed.", ref checks);
            int reads = repository.AsyncReads;
            RejectUnauthorized(() => service.GetAllAsync(null, CancellationToken.None), "Unsigned supplier directory was accepted.", ref checks);
            RejectUnauthorized(() => service.GetAllAsync(new OperationActor { UserName = "other", UserRole = "Manager" }, CancellationToken.None),
                "Spoofed supplier directory was accepted.", ref checks);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); bool stopped = false;
                try { service.GetAllAsync(allowed, cancellation.Token); } catch (OperationCanceledException) { stopped = true; }
                Check(stopped && reads == repository.AsyncReads, "Cancelled or denied supplier read reached storage.", ref checks);
            }
            bool authorized = true;
            var pendingRepository = new FakeRepository();
            var guarded = new ManufacturerService(pendingRepository, actor => authorized && actor.UserName == "admin" && actor.UserRole == "Manager");
            pendingRepository.PendingRead = new TaskCompletionSource<IReadOnlyList<ManufacturerRecord>>();
            var revokedRead = guarded.GetAllAsync(allowed, CancellationToken.None);
            authorized = false;
            pendingRepository.PendingRead.SetResult(new[] { input });
            RejectUnauthorized(() => revokedRead.GetAwaiter().GetResult(),
                "Supplier read disclosed rows after permission revocation.", ref checks);

            authorized = true;
            pendingRepository.PendingRead = new TaskCompletionSource<IReadOnlyList<ManufacturerRecord>>();
            var mutableActor = new OperationActor { UserName = "admin", UserRole = "Manager" };
            var stableRead = guarded.GetAllAsync(mutableActor, CancellationToken.None);
            mutableActor.UserName = "other";
            pendingRepository.PendingRead.SetResult(new[] { input });
            Check(stableRead.GetAwaiter().GetResult()[0].CompName == input.CompName,
                "Supplier read did not preserve its authorized actor snapshot.", ref checks);

            using (var lateCancellation = new CancellationTokenSource())
            {
                pendingRepository.PendingRead = new TaskCompletionSource<IReadOnlyList<ManufacturerRecord>>();
                var cancelledRead = guarded.GetAllAsync(allowed, lateCancellation.Token);
                lateCancellation.Cancel();
                pendingRepository.PendingRead.SetResult(new[] { input });
                bool cancelled = false;
                try { cancelledRead.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "Supplier read ignored cancellation after storage completed.", ref checks);
            }

            pendingRepository.PendingRead = new TaskCompletionSource<IReadOnlyList<ManufacturerRecord>>();
            var missingRead = guarded.GetAllAsync(allowed, CancellationToken.None);
            pendingRepository.PendingRead.SetResult(null);
            Reject(() => missingRead.GetAwaiter().GetResult(), "Missing supplier result was accepted.", ref checks);

            pendingRepository.PendingRead = new TaskCompletionSource<IReadOnlyList<ManufacturerRecord>>();
            var independentRead = guarded.GetAllAsync(allowed, CancellationToken.None);
            var sourceRows = new List<ManufacturerRecord> { input };
            pendingRepository.PendingRead.SetResult(sourceRows);
            var acceptedRows = independentRead.GetAwaiter().GetResult();
            sourceRows.Clear();
            Check(acceptedRows.Count == 1, "Supplier result changed when storage reused its list.", ref checks);
            var readOnly = new ManufacturerService(repository, actor => false, actor => actor.UserName == "admin" && actor.UserRole == "Manager");
            Check(readOnly.GetAllAsync(allowed, CancellationToken.None).GetAwaiter().GetResult().Count == 0,
                "Read-only supplier service lost permitted async reads.", ref checks);
            RejectUnauthorized(() => readOnly.Create(input, allowed), "Read-only supplier service accepted creation.", ref checks);
            RejectUnauthorized(() => readOnly.Update(input, allowed), "Read-only supplier service accepted update.", ref checks);
            RejectUnauthorized(() => readOnly.Delete("c-1", allowed), "Read-only supplier service accepted deletion.", ref checks);
            repository.MutationResult = false;
            Check(!service.Update(input, allowed) && !service.Delete("c-1", allowed),
                "Not-found mutation result was lost.", ref checks);
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
                ApplicationName = "Pharmacy.CoreTests.Manufacturer.ReadOnly"
            };
            var repository = new SqlManufacturerRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<ManufacturerRecord> actual = repository.GetAll();
            IReadOnlyList<ManufacturerRecord> asyncRows = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (asyncRows.Count != actual.Count) throw new InvalidOperationException("Async supplier row count changed.");
            for (int index = 0; index < actual.Count; index++)
            {
                ManufacturerRecord a = actual[index], b = asyncRows[index];
                if (a.CompId != b.CompId || a.CompName != b.CompName || a.CompPhone != b.CompPhone || a.CompAddress != b.CompAddress)
                    throw new InvalidOperationException("Async supplier values or ordering changed.");
            }
            Console.WriteLine("PASS: supplier SQL async parity; Rows=" + actual.Count + "; PersistentWrites=0.");
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM dbo.CompanyTb1;";
                connection.Open();
                int expected = Convert.ToInt32(command.ExecuteScalar());
                if (actual.Count != expected) throw new InvalidOperationException("Manufacturer typed read count changed.");
            }
            Console.WriteLine("PASS: typed manufacturer read. Database=" + database + "; Rows=" + actual.Count + "; PersistentWrites=0.");
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

        private static void RejectUnauthorized(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (UnauthorizedAccessException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeRepository : IManufacturerRepository
        {
            internal int AsyncReads;
            internal TaskCompletionSource<IReadOnlyList<ManufacturerRecord>> PendingRead;
            public Task<IReadOnlyList<ManufacturerRecord>> GetAllAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); AsyncReads++; return PendingRead == null ? Task.FromResult(GetAll()) : PendingRead.Task;
            }
            internal ManufacturerRecord Last;
            internal OperationActor Actor;
            internal bool MutationResult = true;
            public IReadOnlyList<ManufacturerRecord> GetAll() => new ManufacturerRecord[0];
            public void Create(ManufacturerRecord manufacturer, OperationActor actor) { Last = manufacturer; Actor = actor; }
            public bool Update(ManufacturerRecord manufacturer, OperationActor actor) { Last = manufacturer; Actor = actor; return MutationResult; }
            public bool Delete(string companyId, OperationActor actor)
            {
                Last = new ManufacturerRecord { CompId = companyId };
                Actor = actor;
                return MutationResult;
            }
        }
    }
}
