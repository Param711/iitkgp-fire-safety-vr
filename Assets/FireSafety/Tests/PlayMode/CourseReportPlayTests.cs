using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireSafety.Tests
{
    public class CourseReportPlayTests
    {
        [UnityTest] public IEnumerator AddedPagesKeepNavigationReadableAndPreserveReport()
        {
            yield return SceneManager.LoadSceneAsync("FireSafetyTraining");yield return null;
            var session=Object.FindObjectOfType<TrainingSession>();session.persistLatestReport=false;
            var r=new AssessmentReport{sessionId="course-test",outcome="Test fixture",durationSeconds=70};
            r.events.Add(new AssessmentEvent{action="PASS: pull",detail="Pin removed",severity="observed",seconds=10});
            r.events.Add(new AssessmentEvent{action="PASS: aim",detail="Base aim established",severity="observed",seconds=12});
            r.Evaluate(true,false,true,true);session.report=r;
            int score=r.score,count=r.events.Count;bool time=false,method=false,timeline=false;
            session.presentation.ShowReport(0);
            for(int page=0;page<20;page++)
            {
                var ui=session.presentation;var body=ui.body.text;
                time|=body.Contains("TIME STUDY / OBSERVED DURATIONS");
                method|=body.Contains("METHOD ANALYSIS / PASS");timeline|=body.Contains("OBSERVED DECISIONS");
                Assert.GreaterOrEqual(ui.body.characterSize,.013f);
                Assert.LessOrEqual(ui.buttons.Count(b=>b.gameObject.activeSelf),3);
                var next=ui.buttons[1];if(!next.gameObject.activeSelf)break;
                ui.ShowReport(next.value);
            }
            Assert.IsTrue(time&&method&&timeline,"Course pages or the original timeline are unreachable.");
            Assert.AreEqual(score,r.score);Assert.AreEqual(count,r.events.Count);
            session.presentation.ShowReport(3);StringAssert.Contains("RECOMMENDATIONS",session.presentation.body.text);
        }
    }
}
