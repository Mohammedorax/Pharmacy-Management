using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    // A new session is needed for recovery after reauthentication. The attempt retains its source and owner.
    internal sealed class CustomerMasterWriteSession
    {
        private readonly ICustomerMasterWriteService service;
        private readonly Func<bool> current;
        private readonly string user, role;
        private int revoked;
        internal string SourceIdentity { get; }
        internal OperationActor Actor => new OperationActor { UserName=user, UserRole=role };
        internal CustomerMasterWriteSession(ICustomerMasterWriteService service, OperationActor actor,
            string sourceIdentity, Func<bool> current)
        {
            this.service=service??throw new ArgumentNullException(nameof(service));
            if(actor==null)
                throw new ArgumentException("هوية جلسة حفظ العميل مطلوبة.");
            if(string.IsNullOrWhiteSpace(sourceIdentity))throw new ArgumentException("مصدر حفظ العميل مطلوب.");
            user=actor.UserName;role=actor.UserRole;SourceIdentity=sourceIdentity;
            this.current=current??throw new ArgumentNullException(nameof(current));
        }
        internal bool IsCurrent
        {
            get
            {
                if(Volatile.Read(ref revoked)!=0)return false;
                if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(role)||!current()){Interlocked.Exchange(ref revoked,1);return false;}
                return Volatile.Read(ref revoked)==0;
            }
        }
        private void Authorize(CustomerMasterWriteAttempt attempt, bool originalRole)
        {
            if(!IsCurrent||attempt==null||!string.Equals(attempt.SourceIdentity,SourceIdentity,StringComparison.Ordinal)||
                !attempt.Request.MatchesOwner(Actor,originalRole))
                throw new UnauthorizedAccessException("تغير سياق حفظ العميل. افتح جلسة مفوضة للمستخدم وقاعدة المحاولة.");
        }
        internal async Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteAttempt attempt,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Authorize(attempt,true);
            var receipt=await service.SubmitAsync(attempt.Request,Actor,token).ConfigureAwait(false);
            try { Authorize(attempt,true); }
            catch(Exception error){throw new CustomerMasterWriteOutcomeUnknownException(attempt.Request.RequestId,error);}
            return receipt;
        }
        internal async Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteAttempt attempt,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Authorize(attempt,false);
            var receipt=await service.CancelAsync(attempt.Request,Actor,token).ConfigureAwait(false);
            try { Authorize(attempt,false); }
            catch(Exception error){throw new CustomerMasterWriteOutcomeUnknownException(attempt.Request.RequestId,error);}
            return receipt;
        }
        internal async Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteAttempt attempt,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Authorize(attempt,false);
            var result=await service.ResolveAsync(attempt.Request,Actor,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Authorize(attempt,false);
            return result;
        }
    }
}

