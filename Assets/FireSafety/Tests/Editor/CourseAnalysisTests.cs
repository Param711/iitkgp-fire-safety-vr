using NUnit.Framework;

namespace FireSafety.Tests
{
    public class CourseAnalysisTests
    {
        static AssessmentReport Report()
        {
            var r=new AssessmentReport {durationSeconds=75,firstResponseSeconds=2,alarmSeconds=5,assistanceSeconds=8};
            r.events.Add(new AssessmentEvent{action="Extinguisher picked up",seconds=10});
            r.events.Add(new AssessmentEvent{action="PASS: pull",seconds=12});
            r.events.Add(new AssessmentEvent{action="PASS: aim",seconds=15});
            r.events.Add(new AssessmentEvent{action="PASS: squeeze",seconds=16});
            r.events.Add(new AssessmentEvent{action="PASS: sweep",seconds=18});
            return r;
        }
        [Test] public void MilestoneIntervalsUseFirstEventsAndChronology()
        {
            var r=Report();r.events.Reverse();r.events.Add(new AssessmentEvent{action="PASS: squeeze",seconds=40});
            Assert.AreEqual("2.0 s",TrainingCourseAnalysis.Interval(r,"Extinguisher picked up","PASS: pull"));
            Assert.AreEqual("1.0 s",TrainingCourseAnalysis.Interval(r,"PASS: aim","PASS: squeeze"));
            Assert.AreEqual("Out of order",TrainingCourseAnalysis.Interval(r,"PASS: sweep","PASS: pull"));
        }
        [Test] public void MissingAndLegacyDataDoNotBecomeZeroTimes()
        {
            var r=new AssessmentReport{events=null};
            StringAssert.Contains("First recorded response: Not recorded",TrainingCourseAnalysis.Pages(r)[0]);
            Assert.AreEqual("Not recorded",TrainingCourseAnalysis.Interval(r,"PASS: pull","PASS: aim"));
            Assert.IsEmpty(TrainingCourseAnalysis.Pages(null));
            r.events=new System.Collections.Generic.List<AssessmentEvent>{null,new AssessmentEvent{action="PASS: pull",seconds=float.NaN}};
            Assert.DoesNotThrow(()=>TrainingCourseAnalysis.Pages(r));
        }
        [Test] public void ReleaseBetweenMilestonesIsNotContinuousMotionTime()
        {
            var r=Report();r.events.Add(new AssessmentEvent{action="Extinguisher released",seconds=13});
            Assert.AreEqual("Interrupted / released",TrainingCourseAnalysis.Interval(r,"PASS: pull","PASS: aim"));
        }
        [Test] public void CoursePagesDoNotChangeScoringOrRawRecords()
        {
            var r=Report();r.Evaluate(true,false,true,true);int score=r.score,count=r.events.Count,categories=r.categories.Count;
            string classification=r.classification;int recommendations=r.recommendations.Count;
            var pages=TrainingCourseAnalysis.Pages(r);
            Assert.AreEqual(2,pages.Count);Assert.AreEqual(score,r.score);Assert.AreEqual(count,r.events.Count);
            Assert.AreEqual(categories,r.categories.Count);Assert.AreEqual(classification,r.classification);
            Assert.AreEqual(recommendations,r.recommendations.Count);
            StringAssert.Contains("No PMTS total assigned",pages[1]);
            foreach(var page in pages)Assert.LessOrEqual(page.Split('\n').Length,18);
        }
    }
}
