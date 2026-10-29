using System;
using System.Threading;
namespace project_fo_3
{
 // Synchronous Commit stays on the lease-owning thread. No await may cross this lease.
 // This serializes application session publication only; SQL permission/profile mutations
 // need their own separately verified integration before claiming broader context safety.
 internal static class SessionCommitGate
 {
  static readonly ReaderWriterLockSlim gate=new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
  internal static int WaitingSessionChanges=>gate.WaitingWriteCount;
  internal static IDisposable Commit(Func<bool> current){
   if(current==null)throw new ArgumentNullException(nameof(current));gate.EnterReadLock();
   try{if(!current())throw new UnauthorizedAccessException("تغيرت جلسة البيع قبل الاعتماد؛ تحقق من المحاولة الأصلية.");return new Lease(false);}catch{gate.ExitReadLock();throw;}
  }
  internal static IDisposable ChangeSession(){
   if(gate.IsReadLockHeld&&!gate.IsWriteLockHeld)throw new InvalidOperationException("لا يمكن نشر جلسة جديدة داخل حد اعتماد مالي على الخيط نفسه.");
   gate.EnterWriteLock();return new Lease(true);
  }
  sealed class Lease:IDisposable{
   readonly bool writer;bool disposed;internal Lease(bool writer){this.writer=writer;}
   public void Dispose(){if(disposed)return;disposed=true;if(writer)gate.ExitWriteLock();else gate.ExitReadLock();}
  }
 }
}
