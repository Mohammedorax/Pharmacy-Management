using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
namespace project_fo_3
{
    internal sealed class FrequentMedicineChoice
    {
        internal string Name { get; }
        internal int Count { get; }
        internal DateTime LastChosenUtc { get; }
        internal FrequentMedicineChoice(string name,int count,DateTime lastChosenUtc)
        { Name=name; Count=count; LastChosenUtc=lastChosenUtc; }
    }
    internal sealed class FrequentMedicinePreferenceStore
    {
        private const int MaxEntries=50, MaxBytes=65536, MaxCount=1000000;
        private readonly string directory;
        private readonly Func<DateTime> utcNow;
        internal FrequentMedicinePreferenceStore(string directory,Func<DateTime> utcNow=null)
        {
            if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("A preference directory is required.");
            this.directory=Path.GetFullPath(directory);
            this.utcNow=utcNow??(()=>DateTime.UtcNow);
        }
        private static string Hash(string value)
        {
            using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
        private static string UserKey(string user)
        {
            if(string.IsNullOrWhiteSpace(user))throw new ArgumentException("A signed-in user is required.");
            return Hash(user.Trim().ToUpperInvariant());
        }
        private string PathFor(string user)=>Path.Combine(directory,UserKey(user)+".xml");
        private static void RequireCurrent(Func<bool> current)
        {
            if(current==null||!current())throw new OperationCanceledException("Preference session changed.");
        }
        private static void ValidateName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)||name.Length>200)throw new ArgumentException("Invalid canonical medicine name.");
            XmlConvert.VerifyXmlChars(name);
        }
        private static IReadOnlyList<FrequentMedicineChoice> Rank(IEnumerable<FrequentMedicineChoice> entries)
        {
            return entries.OrderByDescending(x=>x.Count).ThenByDescending(x=>x.LastChosenUtc)
                .ThenBy(x=>x.Name,StringComparer.Ordinal).Take(MaxEntries).ToList().AsReadOnly();
        }
        private IReadOnlyList<FrequentMedicineChoice> Read(string user)
        {
            string path=PathFor(user);
            if(!File.Exists(path))return new List<FrequentMedicineChoice>().AsReadOnly();

            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxBytes};
            XDocument document;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
            {
                if(stream.Length>MaxBytes)throw new InvalidDataException("Preference file is too large.");
                using(var reader=XmlReader.Create(stream,settings))document=XDocument.Load(reader);
            }
            var root=document.Root;
            if(root==null||root.Name!="FrequentChoices"||root.Attributes().Count()!=2||
               (string)root.Attribute("Version")!="1"||(string)root.Attribute("User")!=UserKey(user))
                throw new InvalidDataException("Invalid preference owner/schema.");
            var entries=new List<FrequentMedicineChoice>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in root.Elements())
            {
                if(row.Name!="Choice"||row.Attributes().Count()!=3||row.HasElements||!string.IsNullOrWhiteSpace(row.Value))
                    throw new InvalidDataException("Invalid preference entry.");
                string name=(string)row.Attribute("Name");ValidateName(name);
                int count;long ticks;
                if(!int.TryParse((string)row.Attribute("Count"),NumberStyles.None,CultureInfo.InvariantCulture,out count)||
                   count<1||count>MaxCount||
                   !long.TryParse((string)row.Attribute("UtcTicks"),NumberStyles.None,CultureInfo.InvariantCulture,out ticks)||
                   ticks<DateTime.MinValue.Ticks||ticks>DateTime.MaxValue.Ticks||!names.Add(name))
                    throw new InvalidDataException("Invalid preference count/time/identity.");
                entries.Add(new FrequentMedicineChoice(name,count,new DateTime(ticks,DateTimeKind.Utc)));
                if(entries.Count>MaxEntries)throw new InvalidDataException("Too many preference entries.");
            }
            return Rank(entries);
        }
        internal IReadOnlyList<FrequentMedicineChoice> Load(string user,Func<bool> isCurrent)
        {
            RequireCurrent(isCurrent);
            string lockKey=Hash(directory.ToUpperInvariant()+"|"+UserKey(user));
            using(var mutex=new Mutex(false,"Local\\PharmacyManagement.FrequentChoice."+lockKey))
            {
                bool owned=false;
                try
                {
                    try { owned=mutex.WaitOne(3000); }
                    catch(AbandonedMutexException) { owned=true; }
                    if(!owned)throw new IOException("Preference store is busy.");
                    RequireCurrent(isCurrent);
                    var entries=Read(user);
                    RequireCurrent(isCurrent);
                    return entries;
                }
                finally { if(owned)mutex.ReleaseMutex(); }
            }
        }
        internal void RecordCartAddition(string user,string canonicalMedicineName,Func<bool> isCurrent)
        {
            RequireCurrent(isCurrent);ValidateName(canonicalMedicineName);
            string key=UserKey(user);
            string lockKey=Hash(directory.ToUpperInvariant()+"|"+key);
            using(var mutex=new Mutex(false,"Local\\PharmacyManagement.FrequentChoice."+lockKey))
            {
                bool owned=false;
                try
                {
                    try { owned=mutex.WaitOne(3000); }
                    catch(AbandonedMutexException) { owned=true; }
                    if(!owned)throw new IOException("Preference store is busy.");
                    RequireCurrent(isCurrent);
                    var entries=Read(user).ToList();
                    var old=entries.SingleOrDefault(x=>x.Name==canonicalMedicineName);
                    DateTime now=utcNow();
                    if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Preference clock must be UTC.");
                    if(old!=null)entries.Remove(old);
                    entries.Add(new FrequentMedicineChoice(canonicalMedicineName,
                        old==null?1:Math.Min(MaxCount,old.Count+1),
                        old!=null&&old.LastChosenUtc>now?old.LastChosenUtc:now));
                    var ranked=Rank(entries);
                    var root=new XElement("FrequentChoices",new XAttribute("Version","1"),new XAttribute("User",key),
                        ranked.Select(x=>new XElement("Choice",new XAttribute("Name",x.Name),
                        new XAttribute("Count",x.Count),new XAttribute("UtcTicks",x.LastChosenUtc.Ticks))));
                    byte[] bytes=Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
                    if(bytes.Length>MaxBytes)throw new InvalidDataException("Preference serialization is too large.");
                    RequireCurrent(isCurrent);
                    Directory.CreateDirectory(directory);
                    string path=PathFor(user),temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
                    try
                    {
                        File.WriteAllBytes(temporary,bytes);
                        RequireCurrent(isCurrent);
                        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                    }
                    finally { if(File.Exists(temporary))File.Delete(temporary); }
                }
                finally { if(owned)mutex.ReleaseMutex(); }
            }
        }
    }
}


