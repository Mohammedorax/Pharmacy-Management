using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class SalesCustomerTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            bool canWrite = true;
            var service = new SalesCustomerService(repository, actor => canWrite &&
                actor.UserName == "cashier" && actor.UserRole == "Sales");

            var reference=new CustomerReferenceReadService(new ReferenceRepository(repository),a=>a.UserName=="cashier"&&a.UserRole=="Sales");
            var referenceActor=new OperationActor{UserName="cashier",UserRole="Sales"};
            var byName=reference.ReadAsync(new CustomerReferenceReadRequest(CustomerReferenceKind.ByName,"  عميل نقدي  "),referenceActor,CancellationToken.None).GetAwaiter().GetResult().Customer;
            Check(repository.LastName=="عميل نقدي"&&byName.CustomerId=="C-1","Customer name lookup input changed",ref checks);
            var byPhone=reference.ReadAsync(new CustomerReferenceReadRequest(CustomerReferenceKind.ByPhone,"  777  "),referenceActor,CancellationToken.None).GetAwaiter().GetResult().Customer;
            Check(repository.LastPhone=="777"&&byPhone.PhoneNumber=="777","Customer phone input changed",ref checks);
            Check(reference.ReadAsync(new CustomerReferenceReadRequest(CustomerReferenceKind.ByPhone," "),referenceActor,CancellationToken.None).GetAwaiter().GetResult().Customer==null&&repository.PhoneCalls==1,"Empty lookup reached repository",ref checks);
            try{new CustomerReferenceReadRequest(CustomerReferenceKind.ByPhone,new string('7',51));throw new InvalidOperationException("Oversized lookup accepted");}catch(ArgumentException){checks++;}
            foreach(var type in new[]{typeof(ISalesCustomerService),typeof(SalesCustomerService)})
            foreach(string name in new[]{"GetAll","GetCustomerNames","GetPhoneNumbers","FindById","FindByName","FindByPhone"})
                Check(type.GetMethods().All(m=>m.Name!=name),"Actorless customer API remains: "+type.Name+"."+name,ref checks);
            string createdId = service.Create(new SalesCustomer { CustomerName = "  عميل جديد  ", PhoneNumber = " 778 ", Address = " صنعاء " }, new OperationActor { UserName = " cashier ", UserRole = " Sales " });
            Check(createdId == "C-2" && repository.CreatedCustomer.CustomerName == "عميل جديد" && repository.CreatedCustomer.PhoneNumber == "778" && repository.CreatedCustomer.Address == "صنعاء" && repository.LastActor.UserName == "cashier",
                "Customer create did not normalize master data and audit actor.", ref checks);
            canWrite = false;
            int writesBeforeDenial = repository.WriteCalls;
            RejectUnauthorized(() => service.Create(new SalesCustomer { CustomerName = "عميل آخر", PhoneNumber = "779" },
                new OperationActor { UserName = "cashier", UserRole = "Sales" }),
                "Denied customer create reached the repository.", ref checks);
            RejectUnauthorized(() => service.Update(new SalesCustomer { CustomerId = "C-1", CustomerName = "عميل آخر", PhoneNumber = "779" },
                new OperationActor { UserName = "cashier", UserRole = "Sales" }),
                "Denied customer update reached the repository.", ref checks);
            Check(repository.WriteCalls == writesBeforeDenial,
                "Customer write continued after the operation grant was revoked.", ref checks);
            canWrite = true;
            RejectUnauthorized(() => service.Update(new SalesCustomer { CustomerId = "C-1", CustomerName = "عميل آخر", PhoneNumber = "779" },
                new OperationActor { UserName = "other-user", UserRole = "Sales" }),
                "Customer update accepted another actor identity.", ref checks);
            Reject(() => service.Create(new SalesCustomer { CustomerName = "عميل", PhoneNumber = "000000000" }, new OperationActor { UserName = "admin", UserRole = "Manager" }),
                "Reserved general-customer phone was accepted for a normal customer.", ref checks);
            Reject(() => service.Update(new SalesCustomer { CustomerId = "0", CustomerName = "عميل", PhoneNumber = "778" }, new OperationActor { UserName = "admin", UserRole = "Manager" }),
                "General customer record was accepted for editing.", ref checks);
            var readActor = new OperationActor { UserName = " cashier ", UserRole = " Sales " };
            var asyncRows = service.GetAllAsync(readActor, CancellationToken.None).GetAwaiter().GetResult();
            Check(asyncRows.Count == 1 && asyncRows[0].CustomerId == "C-1", "Async customer directory changed rows.", ref checks);
            int reads = repository.AsyncReads;
            RejectUnauthorized(() => service.GetAllAsync(new OperationActor { UserName = "other", UserRole = "Sales" }, CancellationToken.None),
                "Async customer directory accepted a stale identity.", ref checks);
            RejectUnauthorized(() => service.GetAllAsync(null, CancellationToken.None), "Async directory accepted no actor.", ref checks);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                bool stopped = false;
                try { service.GetAllAsync(readActor, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
                Check(stopped && repository.AsyncReads == reads, "Cancelled or denied customer read reached SQL boundary.", ref checks);
            }

            bool readAllowed=true;
            var pendingRepository=new FakeRepository{Pending=new TaskCompletionSource<IReadOnlyList<SalesCustomer>>()};
            var guarded=new SalesCustomerService(pendingRepository,_=>false,actor=>readAllowed&&actor.UserName=="reader"&&actor.UserRole=="Cashier");
            var caller=new OperationActor{UserName="reader",UserRole="Cashier"};
            var pendingRead=guarded.GetAllAsync(caller,CancellationToken.None);readAllowed=false;
            pendingRepository.Pending.SetResult(new[]{new SalesCustomer{CustomerId="PRIVATE"}});
            RejectUnauthorized(()=>pendingRead.GetAwaiter().GetResult(),"Customer result escaped after authorization was revoked.",ref checks);
            readAllowed=true;pendingRepository.Pending=new TaskCompletionSource<IReadOnlyList<SalesCustomer>>();
            pendingRead=guarded.GetAllAsync(caller,CancellationToken.None);caller.UserName="changed";
            pendingRepository.Pending.SetResult(new[]{new SalesCustomer{CustomerId="CAPTURED"}});
            Check(pendingRead.GetAwaiter().GetResult()[0].CustomerId=="CAPTURED","Customer read did not preserve its captured actor.",ref checks);
            caller.UserName="reader";pendingRepository.Pending=new TaskCompletionSource<IReadOnlyList<SalesCustomer>>();
            using(var cancel=new CancellationTokenSource())
            {
                pendingRead=guarded.GetAllAsync(caller,cancel.Token);cancel.Cancel();pendingRepository.Pending.SetResult(new SalesCustomer[0]);
                bool rejected=false;try{pendingRead.GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}
                Check(rejected,"Customer read ignored cancellation after provider completed.",ref checks);
            }

            return checks;
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? string.Empty).ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server, InitialCatalog = database, IntegratedSecurity = true, ConnectTimeout = 5,
                ApplicationName = "Pharmacy.CoreTests.SalesCustomer.ReadOnly"
            };
            var repository = new SqlSalesCustomerRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<SalesCustomer> expected = repository.GetAll();
            IReadOnlyList<SalesCustomer> actual = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (actual.Count != expected.Count) throw new InvalidOperationException("Async customer SQL row count changed.");
            for (int index = 0; index < expected.Count; index++)
            {
                SalesCustomer a = actual[index], b = expected[index];
                if (a.CustomerId != b.CustomerId || a.CustomerName != b.CustomerName || a.PhoneNumber != b.PhoneNumber ||
                    a.Address != b.Address || a.TotalSpent != b.TotalSpent)
                    throw new InvalidOperationException("Async customer SQL values/order changed.");
            }
            Console.WriteLine("PASS: async customer directory SQL parity; Rows=" + actual.Count + "; PersistentWrites=0.");
            IReadOnlyList<string> names = repository.GetCustomerNames();
            IReadOnlyList<string> phones = repository.GetPhoneNumbers();
            if (names.Count > 0)
            {
                SalesCustomer customer = repository.FindByName(names[0]);
                if (customer == null || string.IsNullOrWhiteSpace(customer.CustomerId))
                    throw new InvalidOperationException("Typed sales customer lookup lost the customer identity.");
            }
            if (phones.Count > 0)
            {
                SalesCustomer customer = repository.FindByPhone(phones[0]);
                if (customer == null || !string.Equals(customer.PhoneNumber, phones[0], StringComparison.Ordinal))
                    throw new InvalidOperationException("Typed sales customer phone lookup did not round-trip.");
            }
            Console.WriteLine("PASS: typed sales customer reads. Database=" + database +
                "; Names=" + names.Count + "; Phones=" + phones.Count + "; PersistentWrites=0.");
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            checks++; if (!condition) throw new InvalidOperationException(message);
        }

        private static void Reject(Action action, string message, ref int checks)
        {
            try { action(); } catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private static void RejectUnauthorized(Action action, string message, ref int checks)
        {
            try { action(); } catch (UnauthorizedAccessException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class ReferenceRepository:ICustomerReferenceReadRepository
        {
            private readonly FakeRepository repository;
            internal ReferenceRepository(FakeRepository repository){this.repository=repository;}
            public Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                SalesCustomer customer=request.Kind==CustomerReferenceKind.ByName?repository.FindByName(request.Value):repository.FindByPhone(request.Value);
                return Task.FromResult(new CustomerReferenceReadResult(request.Kind,new string[0],customer==null?null:new CustomerReferenceSnapshot(customer.CustomerId,customer.CustomerName,customer.PhoneNumber,customer.Address,customer.TotalSpent)));
            }
        }
        private sealed class FakeRepository : ISalesCustomerRepository
        {
            internal int AsyncReads;
            internal TaskCompletionSource<IReadOnlyList<SalesCustomer>> Pending;
            public Task<IReadOnlyList<SalesCustomer>> GetAllAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                AsyncReads++;
                return Pending==null?Task.FromResult(GetAll()):Pending.Task;
            }
            internal string LastName;
            internal string LastPhone;
            internal int PhoneCalls;
            internal SalesCustomer CreatedCustomer;
            internal OperationActor LastActor;
            internal int WriteCalls;
            public IReadOnlyList<SalesCustomer> GetAll() => new[] { new SalesCustomer { CustomerId = "C-1", CustomerName = "عميل نقدي", PhoneNumber = "777" } };
            public IReadOnlyList<string> GetCustomerNames() => new[] { "عميل نقدي" };
            public SalesCustomer FindById(string customerId) => customerId == "C-1" ? FindByPhone("777") : null;
            public string Create(SalesCustomer customer, OperationActor actor) { WriteCalls++; CreatedCustomer = customer; LastActor = actor; return "C-2"; }
            public bool Update(SalesCustomer customer, OperationActor actor) { WriteCalls++; return customer.CustomerId == "C-1"; }
            public IReadOnlyList<string> GetPhoneNumbers() => new[] { "777" };
            public SalesCustomer FindByName(string customerName)
            {
                LastName = customerName;
                return new SalesCustomer { CustomerId = "C-1", CustomerName = customerName, PhoneNumber = "777" };
            }
            public SalesCustomer FindByPhone(string phoneNumber)
            {
                PhoneCalls++;
                LastPhone = phoneNumber;
                return new SalesCustomer { CustomerId = "C-1", CustomerName = "عميل نقدي", PhoneNumber = phoneNumber };
            }
        }
    }
}
