using System;
using System.IO;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class VoucherEditorsRecoverySelfTests
    {
        private static int checks;
        private static void Check(bool value){if(!value)throw new InvalidOperationException("Voucher editor recovery assertion failed.");checks++;}
        internal static void Verify()
        {
            checks=0;string token=Guid.NewGuid().ToString("N"),directory=Path.Combine(Path.GetTempPath(),"PharmacyVoucherEditorRecovery-"+token);Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);
            string originalTheme=Properties.Setings.Default.ThemeMode;int ordinal=0;
            try
            {
                AppFonts.Initialize();SessionContext.Start("voucher-editor-recovery-qa","Manager","qa");
                foreach(string theme in new[]{"Light","Dark"})foreach(string type in new[]{"ReceiptExternal","ReceiptInternal","ReceiptMulti","PaymentExternal","PaymentInternal","PaymentMulti"})
                {
                    Properties.Setings.Default.ThemeMode=theme;string key=(++ordinal).ToString("x64");var id=Guid.NewGuid();var pending=VoucherPendingRequest.Restore(new VoucherPostRequest(type,new DateTime(2026,10,6,12,30,45)," طرف عربي Cash 500 mg ","2000",25.015m," بيان محفوظ ","1000",7,"YER",id));int posts=0;
                    Func<VoucherSubmissionStatusRequest,CancellationToken,Task<VoucherSubmissionStatus>> status=(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(71,72,true));
                    using(var store=new VoucherPendingStore(directory,key))store.Save(pending);
                    var recovery=new VoucherEditorRecovery(new VoucherOperationRecovery(new VoucherPendingStore(directory,key),()=>true,request=>{posts++;return Task.FromResult(new VoucherPostResult(81,82));},status));
                    using(var form=new VoucherForm(type,true,theme=="Dark"))
                    {
                        form.AttachVoucherRecovery(recovery);var amount=form.Controls.Find("legacyVoucherAmount",true)[0];Check(!amount.Enabled&&amount.Text=="25.02");Check(recovery.Operation.Pending.HasSameContent(pending.Request));
                        Check(form.RunVoucherWriteAsync(()=>recovery.ExecuteAsync(),()=>Task.FromResult(new DataTable())).GetAwaiter().GetResult());Check(posts==0&&recovery.Operation.Accepted.VoucherId==71&&recovery.Operation.Pending==null);form.ApplyVoucherRecoveryLock();Check(amount.Enabled);
                    }
                    key=(++ordinal).ToString("x64");using(var store=new VoucherPendingStore(directory,key))store.Save(pending);
                    recovery=new VoucherEditorRecovery(new VoucherOperationRecovery(new VoucherPendingStore(directory,key),()=>true,request=>{posts++;return Task.FromResult(new VoucherPostResult(81,82));},status));
                    using(var form=new ReceiptVoucherForm(type,true))
                    {
                        form.AttachReceiptRecovery(recovery);var amount=form.Controls.Find("receiptVoucherAmount",true)[0];Check(!amount.Enabled&&amount.Text=="25.02");Check(!form.Controls.Find("receiptVoucherAction_Print_طباعة كاشير",true)[0].Enabled);
                        typeof(ReceiptVoucherForm).GetMethod("NewVoucher",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(form,null);Check(amount.Text=="25.02"&&recovery.Operation.Pending.HasSameContent(pending.Request));
                        Check(form.RunReceiptWriteAsync(()=>recovery.ExecuteAsync(),"recovered",()=>{}).GetAwaiter().GetResult()==71);Check(posts==0&&form.ResolveVoucherPrintId(null)==71&&recovery.Operation.Pending==null);form.ApplyReceiptRecoveryLock();Check(amount.Enabled);
                    }
                }
                string correctionKey=(++ordinal).ToString("x64");var correction=VoucherPendingRequest.Create(new VoucherPostRequest("ReceiptExternal",DateTime.Today,"طرف","2000",10,"ملاحظة","1000",null,"YER"));
                var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,correctionKey),()=>true,request=>{throw new VoucherPostRejectedException(request.RequestId.Value,new Exception("QA refusal"));},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0)));operation.Prepare(correction);
                using(var form=new ReceiptVoucherForm("ReceiptExternal",true))
                {
                    var recovery=new VoucherEditorRecovery(operation);form.AttachReceiptRecovery(recovery);try{recovery.ExecuteAsync().GetAwaiter().GetResult();throw new Exception("Refusal missing");}catch(VoucherPostRejectedException){}form.ApplyReceiptRecoveryLock();Check(form.Controls.Find("receiptVoucherAmount",true)[0].Enabled&&operation.CanCorrectRejected);
                    recovery.Prepare(new VoucherPostRequest("ReceiptExternal",DateTime.Today,"تصحيح","2000",11,"ملاحظة","1000",null,"YER"));Check(operation.Pending.Request.RequestId==correction.Request.RequestId&&!operation.CanCorrectRejected);form.ApplyReceiptRecoveryLock();Check(!form.Controls.Find("receiptVoucherAmount",true)[0].Enabled);
                }
                foreach(string method in new[]{"Cash","Bank","Transfer","Cheque"})
                {
                    string inputKey=(++ordinal).ToString("x64");var inputs=new VoucherDraftInputs(10.005m,2.5m,"USD",method," ملاحظات أصلية\r\nA&B ","000127","بنك عربي 500 mg",method=="Cheque"?(DateTime?)new DateTime(2026,10,7):null,"قيد التحصيل","مندوب 01","");
                    var request=new VoucherPostRequest("PaymentExternal",new DateTime(2026,10,6),"حساب الطرف","2000",25.0125m,inputs.BuildNotes(),"1000",7,"YER",Guid.NewGuid());var detailed=VoucherPendingRequest.Restore(request,inputs);
                    using(var store=new VoucherPendingStore(directory,inputKey))store.Save(detailed);
                    var restored=new VoucherEditorRecovery(new VoucherOperationRecovery(new VoucherPendingStore(directory,inputKey),()=>true,value=>{throw new Exception("Unexpected post");},(value,cancel)=>Task.FromResult(new VoucherSubmissionStatus(91,92,true))));
                    using(var form=new ReceiptVoucherForm("PaymentExternal",true))
                    {
                        form.AttachReceiptRecovery(restored);Check(form.Controls.Find("receiptVoucherAmount",true)[0].Text==10.005m.ToString());Check(form.Controls.Find("receiptVoucherExchangeRate",true)[0].Text==2.5m.ToString());
                        Check(form.Controls.Find("receiptVoucherNotes",true)[0].Text==inputs.Notes);Check(form.Controls.Find("receiptVoucherChequeNumber",true)[0].Text=="000127"&&form.Controls.Find("receiptVoucherChequeBank",true)[0].Text==inputs.ChequeBank);
                        Check(Convert.ToString(typeof(ReceiptVoucherForm).GetField("paymentMethod",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(form))==method);
                        Check(restored.Operation.Pending.HasSameState(detailed));
                        Check(form.Controls.Find("receiptVoucherCurrency",true)[0].Text=="USD"&&form.Controls.Find("receiptVoucherRepresentative",true)[0].Text==inputs.Representative);
                        Check(form.Controls.Find("receiptVoucherChequeStatus",true)[0].Text==inputs.ChequeStatus&&form.Controls.Find("receiptVoucherPartyName",true)[0].Text=="");
                        Check(form.Controls.Find("receiptVoucherLocalValue",true)[0].Text==25.01m.ToString("0.##"));
                        if(method=="Cheque"){DateTime date;Check(AntdDateInput.TryRead((AntdUI.DatePicker)form.Controls.Find("receiptVoucherChequeDate",true)[0],out date)&&date==inputs.ChequeDate.Value);}
                    }
                }
                Console.WriteLine("PASS: "+checks+" voucher editor recovery presentation/lock, six types/two themes, frozen exact retry without posting, same-GUID correction and accepted print identity; mock services, no SQL/native input.");
            }
            finally{Properties.Setings.Default.ThemeMode=originalTheme;SessionContext.Clear();if(Directory.Exists(directory)&&File.ReadAllText(Path.Combine(directory,"owner"))==token)Directory.Delete(directory,true);}
        }
    }
}