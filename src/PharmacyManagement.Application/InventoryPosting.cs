using System;

namespace PharmacyManagement.Application
{
    public enum InventoryPostKind { OpeningStock, Adjustment, Receipt, Issue }
    public sealed class InventoryPostRequest
    {
        public InventoryPostRequest(InventoryPostKind kind, string sourceId, string medicineName, int quantity, decimal? unitAmount, string notes)
        { Kind=kind; SourceId=sourceId; MedicineName=medicineName; Quantity=quantity; UnitAmount=unitAmount; Notes=notes; }
        public InventoryPostKind Kind { get; }
        public string SourceId { get; }
        public string MedicineName { get; }
        public int Quantity { get; }
        public decimal? UnitAmount { get; }
        public string Notes { get; }
        public bool HasSameContent(InventoryPostRequest other)
            => other!=null && Kind==other.Kind && Quantity==other.Quantity && UnitAmount==other.UnitAmount &&
               string.Equals(MedicineName?.Trim(),other.MedicineName?.Trim(),StringComparison.Ordinal) &&
               string.Equals(Notes?.Trim(),other.Notes?.Trim(),StringComparison.Ordinal);
        public string SourceType => Kind==InventoryPostKind.Adjustment ? "StockAdjustment" : Kind==InventoryPostKind.Receipt ? "StockReceipt" : Kind==InventoryPostKind.Issue ? "StockIssue" : "OpeningStock";
        public string Caption => Kind==InventoryPostKind.Adjustment ? "تسوية مخزون" : Kind==InventoryPostKind.Receipt ? "توريد مخزني يدوي" : Kind==InventoryPostKind.Issue ? "صرف مخزني يدوي" : "رصيد افتتاحي للمخزون";
        public string AuditAction => Kind==InventoryPostKind.Adjustment ? "StockAdjustmentSaved" : Kind==InventoryPostKind.Receipt ? "StockReceiptSaved" : Kind==InventoryPostKind.Issue ? "StockIssueSaved" : "OpeningStockSaved";
    }
    public sealed class InventoryPostResult
    {
        public InventoryPostResult(string sourceId, int before, int after, int delta)
            : this(sourceId,before,after,delta,false) { }
        public InventoryPostResult(string sourceId,int before,int after,int delta,bool alreadyPosted)
        { SourceId=sourceId; Before=before; After=after; Delta=delta; AlreadyPosted=alreadyPosted; }
        public bool AlreadyPosted { get; }
        public string SourceId { get; }
        public int Before { get; }
        public int After { get; }
        public int Delta { get; }
    }
    // Only an owned transaction rollback with no previous source record may produce this outcome.
    public sealed class InventoryPostNotCommittedException : InvalidOperationException
    {
        public InventoryPostNotCommittedException(string sourceId,string message,Exception innerException)
            : base(message,innerException){SourceId=sourceId;}
        public string SourceId {get;}
    }
    public interface IInventoryPostRepository { InventoryPostResult Post(InventoryPostRequest request, OperationActor actor); }
    public interface IInventoryPostService { InventoryPostResult Post(InventoryPostRequest request, OperationActor actor); }
    public sealed class InventoryPostService : IInventoryPostService
    {
        private readonly IInventoryPostRepository repository;
        private readonly Func<OperationActor,bool> canPost;
        public InventoryPostService(IInventoryPostRepository repository, Func<OperationActor,bool> canPost)
        { this.repository=repository??throw new ArgumentNullException(nameof(repository)); this.canPost=canPost??throw new ArgumentNullException(nameof(canPost)); }
        private void Authorize(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canPost(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية تنفيذ حركة المخزون.");
        }
        private static string Required(string value,int maximum,string empty,string tooLong)
        {
            string text=string.IsNullOrWhiteSpace(value)?string.Empty:value.Trim();
            if(text.Length==0)throw new InvalidOperationException(empty);
            if(text.Length>maximum)throw new InvalidOperationException(tooLong);
            return text;
        }
        public static InventoryPostRequest PrepareRequest(InventoryPostRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(!Enum.IsDefined(typeof(InventoryPostKind),request.Kind))throw new InvalidOperationException("نوع حركة المخزون غير صالح.");
            bool setBalance=request.Kind==InventoryPostKind.OpeningStock||request.Kind==InventoryPostKind.Adjustment;
            if(setBalance?request.Quantity<0:request.Quantity<=0)
                throw new InvalidOperationException(request.Kind==InventoryPostKind.OpeningStock?"مخزون أول المدة لا يمكن أن يكون أقل من صفر.":request.Kind==InventoryPostKind.Adjustment?"الكمية الفعلية لا يمكن أن تكون أقل من صفر.":request.Kind==InventoryPostKind.Receipt?"كمية التوريد يجب أن تكون أكبر من صفر.":"كمية الصرف يجب أن تكون أكبر من صفر.");
            if(request.UnitAmount.HasValue&&(setBalance||request.UnitAmount.Value<0||request.UnitAmount.Value>9999999999999999.99m))
                throw new InvalidOperationException("مبلغ الوحدة غير صالح أو يتجاوز حد التخزين.");
            var normalized=new InventoryPostRequest(request.Kind,
                Required(request.SourceId,50,"مرجع الحركة مطلوب.","مرجع الحركة يتجاوز حد التخزين."),
                Required(request.MedicineName,200,"اختر الصنف أولاً.","اسم الصنف يتجاوز حد التخزين."),request.Quantity,request.UnitAmount,
                Required(request.Notes,500,"أدخل سبب الحركة أو مرجعها قبل الاعتماد.","سبب الحركة يجب ألا يتجاوز 500 حرف."));
            if(!System.Text.RegularExpressions.Regex.IsMatch(normalized.SourceId,"^[A-Za-z0-9_-]{1,50}$")) throw new InvalidOperationException("مرجع حركة المخزون غير صالح.");
            return normalized;
        }
        public InventoryPostResult Post(InventoryPostRequest request,OperationActor actor)
        {
            Authorize(actor);
            var capturedActor=new OperationActor {
                UserName=Required(actor.UserName,200,"المستخدم مطلوب.","اسم المستخدم يتجاوز حد التخزين."),
                UserRole=Required(actor.UserRole,50,"الدور مطلوب.","دور المستخدم يتجاوز حد التخزين.") };
            var normalized=PrepareRequest(request);
            Authorize(capturedActor);
            // Do not reauthorize after a known commit and turn acceptance into a retry prompt.
            var result=repository.Post(normalized,capturedActor);
            if(result==null||result.SourceId!=normalized.SourceId)throw new InvalidOperationException("لم تُعد حركة المخزون مرجعًا صالحًا.");
            return result;
        }
    }
}
