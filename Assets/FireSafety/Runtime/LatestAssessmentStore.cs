using System;
using System.IO;
using UnityEngine;

namespace FireSafety
{
    public static class LatestAssessmentStore
    {
        public static void Save(AssessmentReport report,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(report));
            if(File.Exists(path))File.Replace(temporary,path,null);
            else File.Move(temporary,path);
        }
        public static AssessmentReport Load(string path)
        {
            if(!File.Exists(path))return null;
            var report=JsonUtility.FromJson<AssessmentReport>(File.ReadAllText(path));
            if(report==null||string.IsNullOrEmpty(report.sessionId)||string.IsNullOrEmpty(report.outcome)||report.categories==null||report.categories.Count==0)
                throw new InvalidDataException("Latest assessment has no completed analysis.");
            return report;
        }
    }
}
