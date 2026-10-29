using System;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public enum CustomerMasterWriteKind { Create=1, Update=2 }
    public sealed class CustomerMasterWriteRequest
    {
        public Guid RequestId { get; }
        public CustomerMasterWriteKind Kind { get; }
        public string TargetCustomerId { get; }
        public string CustomerName { get; }
        public string PhoneNumber { get; }
        public string Address { get; }
        public string OwnerUser { get; }
        public string OwnerRole { get; }
        private static string Normalize(string text,int maximum,string name,bool required)
        {
            string value=(text??string.Empty).Trim();
            if(value.Length>maximum||required&&value.Length==0)throw new ArgumentException(name+" غير صالح.");
            return value;
        }
        public CustomerMasterWriteRequest(Guid requestId,SalesCustomer customer,OperationActor owner)
        {
            if(requestId==Guid.Empty)throw new ArgumentException("معرف محاولة حفظ العميل مطلوب.");
            if(customer==null)throw new ArgumentNullException(nameof(customer));
            RequestId=requestId;TargetCustomerId=Normalize(customer.CustomerId,50,"رقم العميل",false);
            if(TargetCustomerId=="0")throw new ArgumentException("لا يمكن تعديل سجل العميل العام.");
            Kind=TargetCustomerId.Length==0?CustomerMasterWriteKind.Create:CustomerMasterWriteKind.Update;
            CustomerName=Normalize(customer.CustomerName,200,"اسم العميل",true);
            PhoneNumber=Normalize(customer.PhoneNumber,50,"هاتف العميل",true);
            if(PhoneNumber=="000000000")throw new ArgumentException("الهاتف محجوز للعميل العام.");
            Address=Normalize(customer.Address,300,"عنوان العميل",false);
            OwnerUser=Normalize(owner?.UserName,200,"مستخدم المحاولة",true);
            OwnerRole=Normalize(owner?.UserRole,50,"دور مستخدم المحاولة",true);
        }
        public bool MatchesEditor(SalesCustomer customer)
        {
            if(customer==null)return false;
            return TargetCustomerId==(customer.CustomerId??string.Empty).Trim()&&
                CustomerName==(customer.CustomerName??string.Empty).Trim()&&
                PhoneNumber==(customer.PhoneNumber??string.Empty).Trim()&&Address==(customer.Address??string.Empty).Trim();
        }
        public bool MatchesOwner(OperationActor actor,bool requireOriginalRole=true)=>actor!=null&&
            string.Equals(OwnerUser,(actor.UserName??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase)&&
            (!requireOriginalRole||string.Equals(OwnerRole,(actor.UserRole??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase));
        public SalesCustomer CopyCustomer()=>new SalesCustomer{CustomerId=TargetCustomerId,CustomerName=CustomerName,PhoneNumber=PhoneNumber,Address=Address};
    }
    public enum CustomerMasterWriteOutcome { Committed=1, Canceled=2 }
    public sealed class CustomerMasterWriteReceipt
    {
        public Guid RequestId { get; }
        public string CustomerId { get; }
        public int AuditId { get; }
        public DateTime CommittedAtUtc { get; }
        public bool ExpectedContentMatches { get; }
        public bool CurrentCustomerMatches { get; }
        public CustomerMasterWriteOutcome Outcome { get; }
        public CustomerMasterWriteReceipt(Guid requestId,string customerId,int auditId,DateTime committedAtUtc,bool expectedContentMatches,bool currentCustomerMatches,CustomerMasterWriteOutcome outcome=CustomerMasterWriteOutcome.Committed)
        {
            if(requestId==Guid.Empty||auditId<=0||!Enum.IsDefined(typeof(CustomerMasterWriteOutcome),outcome)||
                outcome==CustomerMasterWriteOutcome.Committed&&(string.IsNullOrWhiteSpace(customerId)||customerId.Length>50||customerId.Trim()=="0")||
                outcome==CustomerMasterWriteOutcome.Canceled&&(!string.IsNullOrEmpty(customerId)||currentCustomerMatches))
                throw new ArgumentException("إثبات حفظ العميل غير صالح.");
            Outcome=outcome;RequestId=requestId;CustomerId=customerId??string.Empty;AuditId=auditId;CommittedAtUtc=DateTime.SpecifyKind(committedAtUtc,DateTimeKind.Utc);
            ExpectedContentMatches=expectedContentMatches;CurrentCustomerMatches=currentCustomerMatches;
        }
    }
    public sealed class CustomerMasterWriteStatus
    {
        public CustomerMasterWriteReceipt Receipt { get; }
        public bool IsFound=>Receipt!=null;
        public bool IsCommitted=>Receipt!=null&&Receipt.Outcome==CustomerMasterWriteOutcome.Committed;
        public bool IsCanceled=>Receipt!=null&&Receipt.Outcome==CustomerMasterWriteOutcome.Canceled;
        public CustomerMasterWriteStatus(CustomerMasterWriteReceipt receipt){Receipt=receipt;}
    }
    public sealed class CustomerMasterWriteOutcomeUnknownException:Exception
    {
        public Guid RequestId { get; }
        public CustomerMasterWriteOutcomeUnknownException(Guid requestId,Exception error)
            :base("تعذر إثبات نتيجة حفظ العميل. تحقق من نفس المحاولة قبل إعادة الحفظ.",error){RequestId=requestId;}
    }
    public sealed class CustomerMasterWriteRejectedException:Exception
    {
        public CustomerMasterWriteRejectedException(string message,Exception error=null):base(message,error){}
    }
    public sealed class CustomerMasterWriteContentConflictException:Exception
    {
        public CustomerMasterWriteContentConflictException():base("رقم هذه المحاولة محفوظ بمحتوى مختلف. راجع النتيجة؛ لم تحفظ التعديلات الجديدة."){}
    }
    public interface ICustomerMasterWriteRepository
    {
        Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest request,OperationActor actor,Action authorizeBeforeSend,CancellationToken token);
        Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token);
        Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest request,OperationActor actor,Action authorizeBeforeSend,CancellationToken token);
    }
    public interface ICustomerMasterWriteService
    {
        Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token);
        Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token);
        Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token);
    }
    public sealed class CustomerMasterWriteService:ICustomerMasterWriteService
    {
        private readonly ICustomerMasterWriteRepository repository;
        private readonly Func<OperationActor,bool> canSubmit,canResolve;
        public CustomerMasterWriteService(ICustomerMasterWriteRepository repository,Func<OperationActor,bool> canSubmit=null,Func<OperationActor,bool> canResolve=null)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canSubmit=canSubmit??(a=>false);this.canResolve=canResolve??(a=>false);}
        private static OperationActor Capture(OperationActor actor)=>new OperationActor{UserName=(actor?.UserName??string.Empty).Trim(),UserRole=(actor?.UserRole??string.Empty).Trim()};
        private void Authorize(CustomerMasterWriteRequest request,OperationActor actor,Func<OperationActor,bool> policy,bool originalRole=true)
        {
            if(actor.UserName.Length==0||actor.UserName.Length>200||actor.UserRole.Length==0||actor.UserRole.Length>50||!policy(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية حفظ العميل أو التحقق من محاولته.");
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(!request.MatchesOwner(actor,originalRole))throw new UnauthorizedAccessException("المحاولة لا تخص مستخدم العملية ودوره.");
        }
        public Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
        {
            var captured=Capture(actor);Authorize(request,captured,canSubmit);token.ThrowIfCancellationRequested();
            return SubmitAuthorizedAsync(request,captured,token);
        }
        public Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
        {
            var captured=Capture(actor);Authorize(request,captured,canSubmit,false);token.ThrowIfCancellationRequested();
            return FinalizeAuthorizedAsync(request,captured,token,true);
        }
        private Task<CustomerMasterWriteReceipt> SubmitAuthorizedAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
            =>FinalizeAuthorizedAsync(request,actor,token,false);
        private async Task<CustomerMasterWriteReceipt> FinalizeAuthorizedAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token,bool cancel)
        {
            CustomerMasterWriteReceipt receipt;
            try
            {
                Action authorize=()=>Authorize(request,actor,canSubmit,!cancel);
                receipt=await (cancel?repository.CancelAsync(request,actor,authorize,token):repository.SubmitAsync(request,actor,authorize,token)).ConfigureAwait(false);
            }
            catch(CustomerMasterWriteRejectedException){throw;}
            catch(CustomerMasterWriteOutcomeUnknownException){throw;}
            catch(Exception error){throw new CustomerMasterWriteOutcomeUnknownException(request.RequestId,error);}
            if(receipt==null||receipt.RequestId!=request.RequestId)
                throw new CustomerMasterWriteOutcomeUnknownException(request.RequestId,new InvalidOperationException("المصدر لم يرجع إثباتًا مطابقًا للمحاولة."));
            try{Authorize(request,actor,canSubmit,!cancel);}
            catch(Exception error){throw new CustomerMasterWriteOutcomeUnknownException(request.RequestId,error);}
            if(!receipt.ExpectedContentMatches)throw new CustomerMasterWriteContentConflictException();
            // Once SQL reports a finalized request, late cancellation cannot mean rollback.
            return receipt;
        }
        public Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
        {
            var captured=Capture(actor);Authorize(request,captured,canResolve,false);token.ThrowIfCancellationRequested();
            return ResolveAuthorizedAsync(request,captured,token);
        }
        private async Task<CustomerMasterWriteStatus> ResolveAuthorizedAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
        {
            var status=await repository.ResolveAsync(request,actor,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Authorize(request,actor,canResolve,false);
            if(status==null||status.IsFound&&status.Receipt.RequestId!=request.RequestId)
                throw new InvalidOperationException("المصدر لم يرجع حالة موثوقة للمحاولة.");
            return status;
        }
    }
    public enum CustomerMasterWritePhase { Ready, Submitting, Unknown, Resolving, Accepted, Canceled, Conflict }
    public sealed class CustomerMasterWriteAttempt
    {
        public CustomerMasterWriteRequest Request { get; }
        public string SourceIdentity { get; }
        public CustomerMasterWritePhase Phase { get; private set; }
        public CustomerMasterWriteReceipt Receipt { get; private set; }
        private CustomerMasterWritePhase resolvingFrom;
        public bool HasUnknownHistory { get; private set; }
        public bool CanDiscard=>Phase==CustomerMasterWritePhase.Ready&&!HasUnknownHistory||Phase==CustomerMasterWritePhase.Accepted||Phase==CustomerMasterWritePhase.Canceled;
        public CustomerMasterWriteAttempt(CustomerMasterWriteRequest request,string sourceIdentity)
        {
            Request=request??throw new ArgumentNullException(nameof(request));
            if(string.IsNullOrWhiteSpace(sourceIdentity))throw new ArgumentException("مصدر المحاولة مطلوب.");
            SourceIdentity=sourceIdentity;Phase=CustomerMasterWritePhase.Ready;
        }
        private void Context(string source,OperationActor actor,bool originalRole=true)
        {
            if(!string.Equals(SourceIdentity,source,StringComparison.Ordinal)||!Request.MatchesOwner(actor,originalRole))
                throw new UnauthorizedAccessException("لا يمكن استخدام المحاولة في مصدر أو مستخدم آخر.");
        }
        private void Require(CustomerMasterWritePhase phase){if(Phase!=phase)throw new InvalidOperationException("حالة محاولة حفظ العميل لا تسمح بالإجراء.");}
        public void BeginSubmit(string source,OperationActor actor,SalesCustomer editor)
        {
            Require(CustomerMasterWritePhase.Ready);Context(source,actor);
            if(!Request.MatchesEditor(editor))throw new CustomerMasterWriteContentConflictException();
            Phase=CustomerMasterWritePhase.Submitting;
        }
        public void BeginCancel(string source,OperationActor actor)
        {
            if(Phase==CustomerMasterWritePhase.Submitting||Phase==CustomerMasterWritePhase.Resolving)
                throw new InvalidOperationException("انتظر انتهاء إجراء المحاولة.");
            Context(source,actor,false);Phase=CustomerMasterWritePhase.Submitting;
        }
        public void ObserveRejected(){Require(CustomerMasterWritePhase.Submitting);Phase=HasUnknownHistory?CustomerMasterWritePhase.Unknown:CustomerMasterWritePhase.Ready;}
        public void ObserveUnknown(){Require(CustomerMasterWritePhase.Submitting);HasUnknownHistory=true;Phase=CustomerMasterWritePhase.Unknown;}
        public void ObserveReceipt(CustomerMasterWriteReceipt receipt)
        {
            Require(CustomerMasterWritePhase.Submitting);
            if(receipt==null||receipt.RequestId!=Request.RequestId){HasUnknownHistory=true;Phase=CustomerMasterWritePhase.Unknown;throw new InvalidOperationException("إثبات المحاولة مفقود أو مختلف.");}
            Receipt=receipt;Phase=!receipt.ExpectedContentMatches?CustomerMasterWritePhase.Conflict:receipt.Outcome==CustomerMasterWriteOutcome.Committed?CustomerMasterWritePhase.Accepted:CustomerMasterWritePhase.Canceled;
        }
        public void BeginResolve(string source,OperationActor actor)
        {
            if(Phase!=CustomerMasterWritePhase.Unknown&&Phase!=CustomerMasterWritePhase.Conflict&&!(Phase==CustomerMasterWritePhase.Ready&&HasUnknownHistory))throw new InvalidOperationException("لا توجد نتيجة مجهولة للتحقق منها.");
            Context(source,actor,false);resolvingFrom=Phase;Phase=CustomerMasterWritePhase.Resolving;
        }
        public void ResolutionFailed(){Require(CustomerMasterWritePhase.Resolving);Phase=resolvingFrom;}
        public void ObserveStatus(CustomerMasterWriteStatus status)
        {
            Require(CustomerMasterWritePhase.Resolving);
            if(status==null||status.IsFound&&status.Receipt.RequestId!=Request.RequestId){Phase=resolvingFrom;throw new InvalidOperationException("حالة المحاولة غير موثوقة.");}
            Receipt=status.Receipt;
            Phase=!status.IsFound?CustomerMasterWritePhase.Ready:!status.Receipt.ExpectedContentMatches?CustomerMasterWritePhase.Conflict:status.IsCommitted?CustomerMasterWritePhase.Accepted:CustomerMasterWritePhase.Canceled;
        }
    }
}