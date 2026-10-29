using System;
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
    internal sealed class InventoryAlertPreferences
    {
        internal int LowStock { get; }
        internal int ExpiryDays { get; }
        internal InventoryAlertPreferences(int lowStock,int expiryDays)
        {
            if(lowStock<0||expiryDays<0||expiryDays>3650)throw new ArgumentOutOfRangeException("Invalid inventory alert limits.");
            LowStock=lowStock;ExpiryDays=expiryDays;
        }
    }
    // Desktop-only preference, no database write. Caller supplies a pinned and revocable context.
    internal sealed class InventoryAlertPreferenceStore
    {
        private readonly string directory;
        internal InventoryAlertPreferenceStore(string directory)
        {if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Preference directory required.");this.directory=Path.GetFullPath(directory);}
        private static string Hash(string value)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
        private static string Scope(string user,string server,string database)
        {
            if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(server)||string.IsNullOrWhiteSpace(database))throw new ArgumentException("Signed-in user and pinned database required.");
            // Length framing avoids delimiter collisions; no connection string or credentials persisted.
            var text=new StringBuilder();foreach(string normalized in new[]{user.Trim().ToUpperInvariant(),server.Trim().ToUpperInvariant(),database.Trim()}){text.Append(normalized.Length).Append(':').Append(normalized);}
            return Hash(text.ToString());
        }
        private static void Current(Func<bool> current)
        {if(current==null||!current())throw new OperationCanceledException("Inventory alert preference context changed.");}
        private T Locked<T>(string scope,Func<bool> current,Func<T> operation)
        {
            Current(current);
            using(var mutex=new Mutex(false,"Local\\PharmacyManagement.InventoryAlertPreference."+Hash(directory.ToUpperInvariant()+"|"+scope)))
            {
                bool owned=false;try{try{owned=mutex.WaitOne(3000);}catch(AbandonedMutexException){owned=true;}if(!owned)throw new TimeoutException("Inventory alert preference is busy.");Current(current);T result=operation();Current(current);return result;}finally{if(owned)mutex.ReleaseMutex();}
            }
        }
        internal InventoryAlertPreferences Load(string user,string server,string database,Func<bool> current)
        {
            string scope=Scope(user,server,database);return Locked(scope,current,()=>{
                string path=Path.Combine(directory,scope+".xml");if(!File.Exists(path))return new InventoryAlertPreferences(10,30);
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8192};XDocument document;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){if(stream.Length>8192)throw new InvalidDataException("Inventory alert preference exceeds size limit.");using(var reader=XmlReader.Create(stream,settings))document=XDocument.Load(reader);}
                var root=document.Root;if(root==null||root.Name!="InventoryAlerts"||root.HasElements||!string.IsNullOrWhiteSpace(root.Value)||root.Attributes().Count()!=4||(string)root.Attribute("Version")!="1"||(string)root.Attribute("Scope")!=scope)throw new InvalidDataException("Inventory alert preference owner/schema invalid.");
                int stock,days;if(!int.TryParse((string)root.Attribute("LowStock"),NumberStyles.None,CultureInfo.InvariantCulture,out stock)||!int.TryParse((string)root.Attribute("ExpiryDays"),NumberStyles.None,CultureInfo.InvariantCulture,out days)||stock<0||days<0||days>3650)throw new InvalidDataException("Inventory alert preference limits invalid.");
                return new InventoryAlertPreferences(stock,days);
            });
        }
        internal void Save(string user,string server,string database,InventoryAlertPreferences value,Func<bool> current)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));string scope=Scope(user,server,database);Locked(scope,current,()=>{
                Directory.CreateDirectory(directory);string path=Path.Combine(directory,scope+".xml"),temporary=Path.Combine(directory,scope+"."+Guid.NewGuid().ToString("N")+".tmp");
                try{
                    var document=new XDocument(new XElement("InventoryAlerts",new XAttribute("Version","1"),new XAttribute("Scope",scope),new XAttribute("LowStock",value.LowStock.ToString(CultureInfo.InvariantCulture)),new XAttribute("ExpiryDays",value.ExpiryDays.ToString(CultureInfo.InvariantCulture))));
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false}))document.Save(writer);stream.Flush(true);}
                    Current(current);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);return true;
                }finally{if(File.Exists(temporary))File.Delete(temporary);}
            });
        }
    }
}
