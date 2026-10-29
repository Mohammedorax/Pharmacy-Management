using System;

namespace PharmacyManagement.Application
{
    public sealed class AccountSaveRequest
    {
        public AccountSaveRequest(string code,string name,string type,string parentCode,bool active)
        {Code=code;Name=name;Type=type;ParentCode=parentCode;IsActive=active;}
        public string Code {get;}
        public string Name {get;}
        public string Type {get;}
        public string ParentCode {get;}
        public bool IsActive {get;}
    }
    public sealed class AccountWriteResult
    {
        public AccountWriteResult(string code,bool changed){Code=code;Changed=changed;}
        public string Code {get;}
        public bool Changed {get;}
    }
    public interface IAccountWriteRepository
    {
        AccountWriteResult Save(AccountSaveRequest request,OperationActor actor);
        AccountWriteResult Delete(string code,OperationActor actor);
    }
    public interface IAccountWriteService
    {
        AccountWriteResult Save(AccountSaveRequest request,OperationActor actor);
        AccountWriteResult Delete(string code,OperationActor actor);
    }
    public sealed class AccountWriteService : IAccountWriteService
    {
        private readonly IAccountWriteRepository repository;
        private readonly Func<OperationActor,bool> canManage;
        public AccountWriteService(IAccountWriteRepository repository,Func<OperationActor,bool> canManage)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canManage=canManage??throw new ArgumentNullException(nameof(canManage));}
        private void Authorize(OperationActor actor)
        {if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canManage(actor))throw new UnauthorizedAccessException("لا تملك صلاحية إدارة شجرة الحسابات.");}
        private static string Text(string value,int max,string field,bool required=false)
        {
            string result=string.IsNullOrWhiteSpace(value)?null:value.Trim();
            if(required&&result==null)throw new InvalidOperationException(field+" مطلوب.");
            if(result!=null&&result.Length>max)throw new InvalidOperationException(field+" يتجاوز الحد المسموح.");
            return result;
        }
        private OperationActor Snapshot(OperationActor actor)
        {Authorize(actor);return new OperationActor {UserName=Text(actor.UserName,200,"المستخدم",true),UserRole=Text(actor.UserRole,50,"الدور",true)};}
        private static AccountWriteResult Accepted(AccountWriteResult result,string code)
        {if(result==null||!string.Equals(result.Code,code,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("لم تُعد عملية الحساب نتيجة صحيحة.");return result;}
        public AccountWriteResult Save(AccountSaveRequest request,OperationActor actor)
        {
            var captured=Snapshot(actor);if(request==null)throw new ArgumentNullException(nameof(request));
            string code=Text(request.Code,20,"رقم الحساب",true),name=Text(request.Name,200,"اسم الحساب",true),parent=Text(request.ParentCode,20,"الحساب الأب");
            if(string.Equals(code,parent,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("لا يمكن ربط الحساب بنفسه.");
            string type=Text(request.Type,50,"نوع الحساب")??"Asset",canonical=null;
            foreach(var value in new[]{"Asset","Liability","Equity","Revenue","Expense"})if(string.Equals(type,value,StringComparison.OrdinalIgnoreCase)){canonical=value;break;}
            if(canonical==null)throw new InvalidOperationException("نوع الحساب غير مدعوم.");
            var normalized=new AccountSaveRequest(code,name,canonical,parent,request.IsActive);
            Authorize(captured);var result=Accepted(repository.Save(normalized,captured),code);
            if(!result.Changed)throw new InvalidOperationException("لم يُعتمد حفظ الحساب.");return result;
        }
        public AccountWriteResult Delete(string code,OperationActor actor)
        {
            var captured=Snapshot(actor);code=Text(code,20,"رقم الحساب",true);Authorize(captured);
            return Accepted(repository.Delete(code,captured),code);
        }
    }
}
