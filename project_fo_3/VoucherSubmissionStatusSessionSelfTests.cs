using System;
using System.Threading;
namespace project_fo_3
{
    internal static class VoucherSubmissionStatusSessionSelfTests
    {
        private static int checks;
        private static void Check(bool value){if(!value)throw new InvalidOperationException("Voucher status session test failed.");checks++;}
        private static void Denied(VoucherSubmissionStatusSession session)
        {try{session.Service.FindAsync(null,session.Actor,CancellationToken.None).GetAwaiter().GetResult();}catch(UnauthorizedAccessException){checks++;return;}throw new InvalidOperationException("Stale session was not refused before SQL.");}
        internal static void Verify()
        {
            checks=0;string connection="Server=unreachable-voucher-test;Database=context_a_qa;Integrated Security=true;Connect Timeout=1";
            try
            {
                SessionContext.Clear();var missing=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(!missing.IsCurrent);Denied(missing);
                SessionContext.Start("voucher-context-user","Manager","employee-a");var first=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(first.IsCurrent);Check(first.Actor.UserName=="voucher-context-user"&&first.Actor.UserRole=="Manager"&&first.ContextKey.Length==64);
                connection=connection.Replace("context_a_qa","context_b_qa");Check(!first.IsCurrent);Denied(first);var other=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(other.IsCurrent&&other.ContextKey!=first.ContextKey);
                connection=connection.Replace("context_b_qa","context_a_qa");Check(first.IsCurrent);SessionContext.Start("voucher-context-user","Manager","employee-a");Check(!first.IsCurrent);Denied(first);var reopened=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(reopened.IsCurrent&&reopened.ContextKey==first.ContextKey);
                reopened.Actor.UserName="forged";Denied(reopened);
                SessionContext.Start("voucher-context-user","Manager","employee-b");Check(!reopened.IsCurrent);Denied(reopened);var employee=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(employee.ContextKey!=first.ContextKey);
                SessionContext.Start("different-user","Manager","employee-a");Check(!employee.IsCurrent);Denied(employee);var user=ApplicationServices.CreateVoucherSubmissionStatusSession(()=>connection);Check(user.ContextKey!=first.ContextKey);
                SessionContext.Clear();Check(!user.IsCurrent);Denied(user);
                Console.WriteLine("PASS: "+checks+" voucher status session/context checks; stale actor/profile/relogin/employee/logout refusal before SQL; no connection.");
            }
            finally{SessionContext.Clear();}
        }
    }
}