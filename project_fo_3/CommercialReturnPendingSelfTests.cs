using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class CommercialReturnPendingSelfTests
    {
        private const string Owner="Return Recovery Verifier", Role="Manager";
        private static readonly Guid Invoice=new Guid("43daa5a7-7345-46c2-8244-35dacda36961");
        private static readonly Guid Line=new Guid("53daa5a7-7345-46c2-8244-35dacda36961");
        private static readonly Guid Allocation=new Guid("63daa5a7-7345-46c2-8244-35dacda36961");
        private static readonly DateTime Date=new DateTime(2026,10,18,12,34,56,DateTimeKind.Utc).AddTicks(7890);
        private static int checks;
        private static string lastNotificationMessage;
        private static void Check(bool value,string message) { checks++; if(!value)throw new Exception(message); }
        private static void Throws<T>(Action action) where T:Exception
        {
            try { action(); } catch(T) { checks++; return; }
            throw new Exception("Expected "+typeof(T).Name);
        }
        private static CommercialReturnPendingAttempt Fixture(string source,Guid invoice,string notes="=literal\nعربي")
        {
            return new CommercialReturnPendingAttempt(source,Owner,Role,Date,new CommercialSalesReturnRequest {
                OriginalInvoiceId=invoice, RequestId="9d4de33447bf43c18e3bbfeaf36cfb58", IssuedBy=Owner,
                RefundMethod="Cash",ReturnReason="دليل الاستعادة — Recovery proof",Notes=notes,
                Lines=new[] { new CommercialSalesReturnRequestLine { OriginalLineId=Line,Quantity=2m,
                    Lots=new[] { new CommercialReturnLotRequest { OriginalSaleAllocationId=Allocation,Quantity=2m,Disposition=CommercialReturnDisposition.Restock } } } }
            });
        }
        private static CommercialSalesReturnResult Receipt() => new CommercialSalesReturnResult {
            ReturnId=new Guid("73daa5a7-7345-46c2-8244-35dacda36961"),
            ReturnInvoiceId=new Guid("83daa5a7-7345-46c2-8244-35dacda36961"),ReturnInvoiceNumber=123,IsDuplicate=true
        };
        internal static void Run(string phase,string directory)
        {
            string root=Path.GetFullPath(directory);
            if(!string.Equals(Path.GetDirectoryName(root),@"C:\PharmacyQA\work",StringComparison.OrdinalIgnoreCase) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(root),@"\Areturn-pending-test-[a-f0-9]{32}\z"))
                throw new InvalidOperationException("Pending return tests require an exact owned C: test directory.");
            string token=Path.GetFileName(root).Substring("return-pending-test-".Length);
            string marker=Path.Combine(root,"owner");
            if(phase=="write")
            {
                if(Directory.Exists(root))throw new InvalidOperationException("Refusing an existing test directory.");
                Directory.CreateDirectory(root);File.WriteAllText(marker,token);
                using(var store=new CommercialReturnPendingStore(root,new string('A',64),Owner,Role,Invoice))
                {
                    var attempt=Fixture(new string('A',64),Invoice);
                    Check(store.Load()==null,"Fresh store was populated.");
                    store.Save(attempt);
                    Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(store.FilePath)).Contains("Recovery proof"),"Recovery request was stored in plaintext.");
                    var mutated=attempt.Snapshot();mutated.RequestId="changed";
                    Check(attempt.Snapshot().RequestId!="changed","Snapshot was mutable.");
                    Throws<IOException>(()=> { using(var held=new CommercialReturnPendingStore(root,new string('A',64),Owner,Role,Invoice)){} });
                }
                File.WriteAllText(Path.Combine(root,"write-process"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
                Console.WriteLine("PASS return pending write: encrypted exact intent, immutable snapshot and exclusive lease; checks="+checks+".");return;
            }
            if(!File.Exists(marker)||File.ReadAllText(marker)!=token)throw new InvalidOperationException("Test ownership marker missing.");
            if(phase=="read")VerifyStore(root);
            else if(phase=="ui")VerifyUi(root);
            else throw new ArgumentException("Unknown pending-return test phase.");
        }
        private static void VerifyStore(string root)
        {
            Check(File.ReadAllText(Path.Combine(root,"write-process"))!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString(),"Store verification did not use a fresh process.");
            var attempt=Fixture(new string('A',64),Invoice);
            using(var store=new CommercialReturnPendingStore(root,new string('A',64),Owner,Role,Invoice))
            {
                var loaded=store.Load();
                Check(loaded.BusinessDateUtc==Date&&loaded.BusinessDateUtc.Kind==DateTimeKind.Utc&&loaded.Snapshot().RequestId==attempt.Snapshot().RequestId,"Restart lost date or identity.");
                Check(loaded.Snapshot().Notes=="=literal\nعربي"&&loaded.Snapshot().Lines[0].Lots[0].Quantity==2m,"Restart changed content.");
                byte[] original=File.ReadAllBytes(store.FilePath);
                store.Save(attempt);Check(File.ReadAllBytes(store.FilePath).SequenceEqual(original),"Idempotent file save rewrote the record.");
                Throws<InvalidOperationException>(()=>store.Save(Fixture(new string('A',64),Invoice,"changed")));
                Throws<InvalidOperationException>(()=>store.RemoveVerified(loaded,null));
                Throws<InvalidOperationException>(()=>store.RemoveConfirmedNotCommitted(loaded,null));
                Throws<InvalidOperationException>(()=>store.RemoveConfirmedNotCommitted(loaded,
                    new CommercialReturnNotCommittedException(new string('B',64),Invoice,loaded.Snapshot().RequestId,new InvalidOperationException("wrong source"))));
                byte[] corrupt=(byte[])original.Clone();corrupt[corrupt.Length/2]^=0x80;File.WriteAllBytes(store.FilePath,corrupt);
                Throws<CryptographicException>(()=>store.Load());
                Throws<CryptographicException>(()=>store.Save(attempt));
                Check(File.ReadAllBytes(store.FilePath).SequenceEqual(corrupt),"Corrupt record was overwritten.");
                File.WriteAllBytes(store.FilePath,original);
            }
            foreach(var value in new[] { new { Source=new string('B',64),User=Owner,Invoice=Invoice },
                new { Source=new string('A',64),User="Other",Invoice=Invoice },
                new { Source=new string('A',64),User=Owner,Invoice=Guid.NewGuid() } })
                using(var other=new CommercialReturnPendingStore(root,value.Source,value.User,Role,value.Invoice))Check(other.Load()==null,"Foreign context exposed a pending request.");
            using(var changedRole=new CommercialReturnPendingStore(root,new string('A',64),Owner,"Cashier",Invoice))
                Throws<InvalidDataException>(()=>changedRole.Load());
            using(var store=new CommercialReturnPendingStore(root,new string('A',64),Owner,Role,Invoice))
            {
                store.RemoveVerified(store.Load(),Receipt());Check(store.Load()==null,"Verified result did not resolve the file.");
                var request=attempt.Snapshot();request.Lines[0].Quantity=0m;request.Lines[0].BaseQuantity=7;
                request.Lines[0].Lots[0].Quantity=7m;
                var baseAttempt=new CommercialReturnPendingAttempt(new string('A',64),Owner,Role,Date,request);
                store.Save(baseAttempt);
                Check(store.Load().Snapshot().Lines[0].BaseQuantity==7&&store.Load().Snapshot().Lines[0].Quantity==0m,"Base-quantity fallback lost precision.");
                store.RemoveVerified(baseAttempt,Receipt());
            }
            var disposed=new CommercialReturnPendingStore(root,new string('A',64),Owner,Role,Invoice);disposed.Dispose();
            Throws<ObjectDisposedException>(()=>disposed.Load());
            Check(!Directory.GetFiles(root,"*.tmp").Any(),"Temporary recovery output remained.");
            Console.WriteLine("PASS return pending restart: exact date/content, source/user/invoice isolation, role guard, corrupt fail closed, overwrite guards, verified cleanup and base quantity; checks="+checks+".");
        }
        private static DataTable Lots(decimal available)
        {
            var table=new DataTable();
            table.Columns.Add("LineId",typeof(Guid));table.Columns.Add("AllocationId",typeof(Guid));
            table.Columns.Add("الصنف");table.Columns.Add("الدفعة");table.Columns.Add("المتاح للإرجاع",typeof(decimal));
            table.Columns.Add("كمية المرتجع",typeof(decimal));table.Columns.Add("الحالة");
            table.Columns.Add("الوحدة الأصلية");table.Columns.Add("BaseUnitsPerUnit",typeof(int));
            table.Rows.Add(Line,Allocation,"QA medicine","QA lot",available,0m,"Restock","unit",1);
            return table;
        }
        private static T Find<T>(Control parent,string name) where T:Control => (T)parent.Controls.Find(name,true).Single();
        private static void Invoke(CommercialSalesReturnForm form,string method)
        {
            try { typeof(CommercialSalesReturnForm).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null); }
            catch(TargetInvocationException error) { throw error.InnerException; }
        }
        private static void WithNotifications(Action action,Action<string> inspect=null)
        {
            Exception failure=null;
            using(var timer=new Timer {Interval=10})
            {
                timer.Tick+=(s,e)=>{
                    var dialog=Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if(dialog==null)return;
                    lastNotificationMessage=(string)typeof(AppNotificationForm).GetField("originalMessage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog);
                    try {inspect?.Invoke(lastNotificationMessage);}catch(Exception error){failure=error;}
                    var yes=dialog.Controls.Find("notificationYes",true).OfType<AntdUI.Button>().FirstOrDefault();
                    if(yes!=null)yes.PerformClick();else dialog.CancelButton?.PerformClick();
                };
                timer.Start();try {action();}finally{timer.Stop();}
            }
            if(failure!=null)throw failure;
        }
        private static CommercialSalesReturnForm Open(string directory,Guid invoice,bool english,decimal available)
        {
            var form=new CommercialSalesReturnForm(invoice,false,english) {
                ReturnPendingDirectoryOverride=directory, ReturnLotsLoader=id=>Lots(available)
            };
            Check(form.InitializeReturnRecovery(),"UI recovery initialization failed.");
            WithNotifications(()=>Invoke(form,"LoadLots"));return form;
        }
        private static void VerifyUi(string root)
        {
            string previousTheme=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string theme in new[] {"Light","Dark"})foreach(bool english in new[] {false,true})
                {
                    Properties.Setings.Default.ThemeMode=theme;SessionContext.Start(Owner,Role,"fixture");
                    string directory=Path.Combine(root,"ui-"+theme+"-"+english);Guid invoice=Guid.NewGuid();
                    string source=SalesWriteSourceFingerprint.Compute(Database.ConnectionString), pendingPath=null, requestId=null;
                    int calls=0;
                    // Exercise the actual Issue path: confirmation, durable save, then an uncertain response.
                    using(var form=Open(directory,invoice,english,3m))
                    {
                        Find<TextBox>(form,"commercialReturnReason").Text="دليل الاستعادة — Recovery proof";
                        Find<AntdUI.Select>(form,"commercialReturnRefund").SelectedIndex=1;
                        Find<DataGridView>(form,"commercialReturnLotsGrid").Rows[0].Cells["كمية المرتجع"].Value=2m;
                        form.ReturnIssuerOverride=(request,date)=>{
                            calls++;pendingPath=Directory.GetFiles(directory,"*.pending").Single();
                            Check(File.Exists(pendingPath),"SQL port ran before durable save.");
                            requestId=request.RequestId;Check(date.Kind==DateTimeKind.Utc,"Submission date was not UTC.");
                            throw new TimeoutException("Simulated lost response");
                        };
                        WithNotifications(()=>Invoke(form,"Issue"));
                        Check(calls==1&&File.Exists(pendingPath),"Uncertain return request was discarded.");
                        Check(Find<DataGridView>(form,"commercialReturnLotsGrid").Enabled&&Find<DataGridView>(form,"commercialReturnLotsGrid").ReadOnly,
                            "Uncertain request must allow review/scroll/copy without editing.");
                    }
                    byte[] preserved=File.ReadAllBytes(pendingPath);
                    // A reopened form must remain passive and show the saved request even if availability is now zero.
                    using(var form=Open(directory,invoice,english,0m))
                    {
                        Check(calls==1,"Loading recovery submitted automatically.");
                        Check(Find<TextBox>(form,"commercialReturnReason").Enabled&&Find<TextBox>(form,"commercialReturnReason").ReadOnly&&Find<AntdUI.Button>(form,"issueCommercialReturn").Text==(english?"Retry same request":"إعادة نفس الطلب"),"Reopened request editor was not read-only/localized.");
                        Check((decimal)Find<DataGridView>(form,"commercialReturnLotsGrid").Rows[0].Cells["كمية المرتجع"].Value==2m,"Saved quantity was not restored.");
                        using(var blocked=new CommercialSalesReturnForm(invoice,false,english) { ReturnPendingDirectoryOverride=directory })
                            Check(!blocked.InitializeReturnRecovery()&&!Find<AntdUI.Button>(blocked,"issueCommercialReturn").Enabled,"A second editor acquired the pending request.");
                        form.ReturnIssuerOverride=(request,date)=>{
                            calls++;Check(request.RequestId==requestId,"Retry changed the original request.");
                            return Receipt();
                        };
                        using(new FileStream(pendingPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                            WithNotifications(()=>Invoke(form,"Issue"));
                        Check(calls==2&&File.Exists(pendingPath)&&File.ReadAllBytes(pendingPath).SequenceEqual(preserved),"Cleanup failure lost the known committed attempt.");
                        Check(lastNotificationMessage.Contains(english?"recovery file could not be updated":"تعذر تحديث ملف الاستعادة"),"Known commit with failed file cleanup was reported as unknown.");
                    }
                    using(var form=Open(directory,invoice,english,0m))
                    {
                        form.ReturnIssuerOverride=(request,date)=>{calls++;Check(request.RequestId==requestId,"Cleanup retry replaced the request identity.");return Receipt();};
                        WithNotifications(()=>Invoke(form,"Issue"),message=>Check(File.Exists(pendingPath),"Pending request was removed before success acknowledgment."));
                        Check(calls==3&&!File.Exists(pendingPath),"Verified retry did not resolve the saved request.");
                    }
                    // A new session must not see or submit the old user's restored details.
                    using(var store=new CommercialReturnPendingStore(directory,source,Owner,Role,invoice))store.Save(Fixture(source,invoice));
                    using(var form=Open(directory,invoice,english,0m))
                    {
                        form.ReturnIssuerOverride=(request,date)=>{calls++;return Receipt();};
                        SessionContext.Start("Other recovery user",Role,"other");
                        Check(Find<TextBox>(form,"commercialReturnReason").Text==""&&!Find<AntdUI.Button>(form,"issueCommercialReturn").Enabled,"Session change retained private details or enabled submission.");
                        WithNotifications(()=>Invoke(form,"Issue"));Check(calls==3,"Old request was sent by the new session.");
                    }
                    SessionContext.Start(Owner,Role,"fixture");
                    string failedDirectory=Path.Combine(root,"write-failure-"+theme+"-"+english);
                    using(var form=Open(failedDirectory,Guid.NewGuid(),english,3m))
                    {
                        Find<TextBox>(form,"commercialReturnReason").Text="Write failure proof";
                        Find<DataGridView>(form,"commercialReturnLotsGrid").Rows[0].Cells["كمية المرتجع"].Value=1m;
                        int sent=0;form.ReturnIssuerOverride=(request,date)=>{sent++;return Receipt();};
                        string blockedPath=Directory.GetFiles(failedDirectory,"*.pending.lock").Single();
                        blockedPath=blockedPath.Substring(0,blockedPath.Length-5);
                        using(new FileStream(blockedPath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                            WithNotifications(()=>Invoke(form,"Issue"));
                        Check(sent==0&&Find<DataGridView>(form,"commercialReturnLotsGrid").Enabled&&Find<AntdUI.Button>(form,"issueCommercialReturn").Enabled,
                            "Failed durable save sent SQL or locked an unsent editor.");
                        File.Delete(blockedPath);
                    }
                    string rejectedDirectory=Path.Combine(root,"confirmed-rejection-"+theme+"-"+english);Guid rejectedInvoice=Guid.NewGuid();
                    using(var form=Open(rejectedDirectory,rejectedInvoice,english,3m))
                    {
                        Find<TextBox>(form,"commercialReturnReason").Text="Confirmed rollback proof";
                        Find<DataGridView>(form,"commercialReturnLotsGrid").Rows[0].Cells["كمية المرتجع"].Value=1m;
                        form.ReturnIssuerOverride=(request,date)=>{throw new CommercialReturnNotCommittedException(source,request.OriginalInvoiceId,request.RequestId,new InvalidOperationException("Confirmed rollback"));};
                        WithNotifications(()=>Invoke(form,"Issue"));
                        Check(!Directory.GetFiles(rejectedDirectory,"*.pending").Any()&&Find<DataGridView>(form,"commercialReturnLotsGrid").Enabled&&!Find<DataGridView>(form,"commercialReturnLotsGrid").ReadOnly,
                            "A fresh confirmed rollback left an unresolved locked request.");
                        Check(Find<DataGridView>(form,"commercialReturnLotsGrid").Columns["الصنف"].ReadOnly&&
                            !Find<DataGridView>(form,"commercialReturnLotsGrid").Columns["كمية المرتجع"].ReadOnly,
                            "Fresh rollback unlocked source columns or left the editable quantity locked.");
                        form.ReturnIssuerOverride=(request,date)=>{throw new TimeoutException("Prior outcome unknown");};
                        WithNotifications(()=>Invoke(form,"Issue"));
                        Check(Directory.GetFiles(rejectedDirectory,"*.pending").Length==1,"An uncertain request was discarded.");
                    }
                    using(var form=Open(rejectedDirectory,rejectedInvoice,english,3m))
                    {
                        form.ReturnIssuerOverride=(request,date)=>{throw new CommercialReturnNotCommittedException(source,request.OriginalInvoiceId,request.RequestId,new InvalidOperationException("Current retry rolled back"));};
                        WithNotifications(()=>Invoke(form,"Issue"));
                        Check(Directory.GetFiles(rejectedDirectory,"*.pending").Length==1&&Find<DataGridView>(form,"commercialReturnLotsGrid").ReadOnly,
                            "Rollback of a later retry incorrectly discarded an earlier unknown outcome.");
                    }
                }
                Console.WriteLine("PASS return recovery UI: 4 theme/direction cases; durable save before send, lost response, passive reopen, exact retry, exclusive editor, success acknowledgment, cleanup failure/retry, unsent write failure, fresh rollback versus prior unknown and session isolation; no SQL.");
            }
            finally {SessionContext.Clear();Properties.Setings.Default.ThemeMode=previousTheme;}
        }
    }
}
