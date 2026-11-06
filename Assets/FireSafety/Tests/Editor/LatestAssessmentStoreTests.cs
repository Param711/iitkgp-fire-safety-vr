using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class LatestAssessmentStoreTests
    {
        string path;
        [SetUp] public void Prepare(){path=Path.Combine(Application.temporaryCachePath,"ReportCheck-"+Guid.NewGuid().ToString("N")+".json");}
        [TearDown] public void Clean(){if(File.Exists(path))File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}
        [Test] public void LatestResultRoundTripsAndCanBeReplaced()
        {
            var report=new AssessmentReport{sessionId="first",outcome="Reached assembly",scenarioId="A01",durationSeconds=35};report.Evaluate(true,false,true,true);
            LatestAssessmentStore.Save(report,path);var loaded=LatestAssessmentStore.Load(path);
            Assert.AreEqual("first",loaded.sessionId);Assert.AreEqual(report.score,loaded.score);Assert.AreEqual(35,loaded.durationSeconds);
            report.sessionId="second";report.outcome="Assistance pending";report.Evaluate(false,true,true,true);
            LatestAssessmentStore.Save(report,path);loaded=LatestAssessmentStore.Load(path);
            Assert.AreEqual("second",loaded.sessionId);Assert.AreEqual("Assistance pending",loaded.outcome);Assert.IsFalse(File.Exists(path+".tmp"));
        }
        [Test] public void MissingOrIncompleteResultCannotBecomeALastReport()
        {Assert.IsNull(LatestAssessmentStore.Load(path));File.WriteAllText(path,"{}");Assert.Throws<InvalidDataException>(()=>LatestAssessmentStore.Load(path));}
    }
}
