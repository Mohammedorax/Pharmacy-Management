using System;
using System.Collections.Generic;

namespace PharmacyManagement.Domain
{
    public static class AccountHierarchy
    {
        public static void ValidateParent(string code, string parent, IDictionary<string,string> parents)
        {
            if(parents==null)throw new ArgumentNullException(nameof(parents));
            if(string.IsNullOrWhiteSpace(code))throw new InvalidOperationException("رقم الحساب مطلوب.");
            if(string.IsNullOrWhiteSpace(parent))return;
            var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase){code.Trim()};
            string current=parent.Trim();
            while(!string.IsNullOrWhiteSpace(current))
            {
                if(!visited.Add(current))throw new InvalidOperationException("لا يمكن ربط الحساب بنفسه أو بأحد أحفاده؛ الربط يكوّن حلقة في شجرة الحسابات.");
                string next;
                if(!parents.TryGetValue(current,out next))throw new InvalidOperationException("الحساب الأب أو أحد أصوله غير موجود.");
                current=string.IsNullOrWhiteSpace(next)?null:next.Trim();
            }
        }
    }
}
