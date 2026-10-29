using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace project_fo_3
{
    internal static class ReportTemplateFileCommit
    {
        internal static FileStream AcquireWriter(string path)
        {
            string fullPath=Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            // The empty coordination file may persist. Exclusivity belongs to the OS handle,
            // so process exit releases it without deleting a path another writer could reuse.
            return new FileStream(fullPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }

        internal static bool IsUnavailableRead(Exception error)
        {
            for(int depth=0;error!=null&&depth<8;depth++,error=error.InnerException)
                if(error is UnauthorizedAccessException||(error is IOException&&!(error is InvalidDataException)))return true;
            return false;
        }

        internal static void WriteAll<T>(string path,List<T> templates,bool preserveBackup)
        {
            path=Path.GetFullPath(path);
            string directory=Path.GetDirectoryName(path);Directory.CreateDirectory(directory);
            string temporary=Path.Combine(directory,"."+Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
            bool created=false;
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    created=true;new XmlSerializer(typeof(List<T>)).Serialize(stream,templates);stream.Flush(true);
                }
                if(File.Exists(path))File.Replace(temporary,path,preserveBackup?null:path+".bak",true);
                else File.Move(temporary,path);
            }
            finally
            {
                try{if(created&&File.Exists(temporary))File.Delete(temporary);}
                catch(IOException error){AppDiagnostics.Log("ReportTemplateTemporaryCleanup",error);}
                catch(UnauthorizedAccessException error){AppDiagnostics.Log("ReportTemplateTemporaryCleanup",error);}
            }
        }
    }
}
