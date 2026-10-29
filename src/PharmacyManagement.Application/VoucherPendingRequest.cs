using System;
namespace PharmacyManagement.Application
{
    public sealed class VoucherPendingRequest
    {
        private VoucherPendingRequest(VoucherPostRequest request,VoucherDraftInputs draftInputs){Request=request;DraftInputs=draftInputs;}
        public VoucherDraftInputs DraftInputs {get;}
        public VoucherPostRequest Request { get; }
        public static VoucherPendingRequest Create(VoucherPostRequest draft) => Create(draft,null);
        public static VoucherPendingRequest Create(VoucherPostRequest draft,VoucherDraftInputs draftInputs)
        {
            if(draft==null)throw new ArgumentNullException(nameof(draft));
            return Restore(new VoucherPostRequest(draft.Type,draft.Date,draft.PartyName,draft.AccountCode,draft.Amount,draft.Notes,draft.CashAccountCode,draft.CostCenterId,draft.ExpectedSystemCurrencyCode,Guid.NewGuid()),draftInputs);
        }
        public static VoucherPendingRequest Restore(VoucherPostRequest request) => Restore(request,null);
        public static VoucherPendingRequest Restore(VoucherPostRequest request,VoucherDraftInputs draftInputs)
        {
            VoucherSubmissionStatusService.PrepareRequest(new VoucherSubmissionStatusRequest(request));
            var prepared=VoucherPostService.PrepareRequest(request);
            var canonical=new VoucherPostRequest(prepared.Type,request.Date,prepared.PartyName,prepared.AccountCode,prepared.Amount,prepared.Notes,prepared.CashAccountCode,prepared.CostCenterId,prepared.ExpectedSystemCurrencyCode,request.RequestId);
            draftInputs?.EnsureMatches(canonical);return new VoucherPendingRequest(canonical,draftInputs);
        }
        public bool HasSameState(VoucherPendingRequest other)=>other!=null&&HasSameContent(other.Request)&&Equals(DraftInputs,other.DraftInputs);
        public bool HasSameContent(VoucherPostRequest request)
        {
            var other=Restore(request).Request;var a=Request;
            return a.RequestId==other.RequestId&&a.Date==other.Date&&a.Type==other.Type&&a.PartyName==other.PartyName&&a.AccountCode==other.AccountCode&&a.Amount==other.Amount&&a.Notes==other.Notes&&a.CashAccountCode==other.CashAccountCode&&a.CostCenterId==other.CostCenterId&&a.ExpectedSystemCurrencyCode==other.ExpectedSystemCurrencyCode;
        }
    }
}