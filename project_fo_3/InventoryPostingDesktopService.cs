using System;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Owns the original service/actor for an unresolved submission; never recaptures its target.
    internal sealed class InventoryPostingSession
    {
        private readonly IInventoryPostService service;
        private readonly IInventoryOperationReadService reads;
        private readonly OperationActor actor;
        private readonly Func<bool> contextMatches;

        internal InventoryPostingSession(IInventoryPostService service, OperationActor actor, Func<bool> contextMatches, string contextKey = null)
            : this(service,actor,contextMatches,contextKey,null) { }
        internal InventoryPostingSession(IInventoryPostService service, OperationActor actor, Func<bool> contextMatches, string contextKey, IInventoryOperationReadService reads)
        {
            ContextKey = contextKey; this.reads = reads;
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            this.actor = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            this.contextMatches = contextMatches ?? throw new ArgumentNullException(nameof(contextMatches));
        }

        internal string ContextKey { get; }
        internal void EnsureContext()
        {
            if (!contextMatches())
                throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد السياق الأصلي لتأكيد نتيجة الحركة السابقة. لم تُرسل محاولة جديدة.");
        }

        private void EnsureReadContext()
        {
            if(!contextMatches())throw new InvalidOperationException("تغير سياق قراءة المخزون؛ أعد فتح النافذة لعرض بيانات القاعدة الحالية.");
        }
        internal async Task<System.Collections.Generic.IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();EnsureReadContext();
            if(reads==null)throw new InvalidOperationException("قراءات نافذة المخزون غير مهيأة.");
            var rows=await reads.LoadMedicinesAsync(new OperationActor {UserName=actor.UserName,UserRole=actor.UserRole},cancellation).ConfigureAwait(false);
            EnsureReadContext();return rows;
        }
        internal async Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();EnsureReadContext();
            if(reads==null)throw new InvalidOperationException("قراءات نافذة المخزون غير مهيأة.");
            var result=await reads.LoadSnapshotAsync(request,new OperationActor {UserName=actor.UserName,UserRole=actor.UserRole},cancellation).ConfigureAwait(false);
            EnsureReadContext();return result;
        }
        internal Task<InventoryPostResult> PostAsync(InventoryPostRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            EnsureContext();
            return Task.Run(() =>
            {
                EnsureContext();
                return service.Post(request, new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole });
            });
        }
    }

    internal static class InventoryPostingDesktopService
    {
        internal static InventoryPostingSession CaptureSession() => ApplicationServices.CreateInventoryPostingSession();
        internal static Task<InventoryPostResult> PostAsync(InventoryPostRequest request) => CaptureSession().PostAsync(request);
    }
}
