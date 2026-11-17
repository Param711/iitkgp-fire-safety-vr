using System;
using System.Collections.Generic;
using System.Text;

namespace FireSafety
{
    // Read-only projections of an existing report. No scene, input or scoring dependencies.
    public static class TrainingCourseAnalysis
    {
        public static List<string> Pages(AssessmentReport report)
        {
            var pages=new List<string>();
            if(report==null)return pages;
            var time=new StringBuilder("TIME STUDY / OBSERVED DURATIONS\n\n");
            time.AppendLine("Trial duration: "+Seconds(report.durationSeconds));
            time.AppendLine("First recorded response: "+Seconds(report.firstResponseSeconds));
            time.AppendLine("Alarm after start: "+Seconds(report.alarmSeconds));
            time.AppendLine("Assistance after start: "+Seconds(report.assistanceSeconds));
            time.AppendLine("Source suppressed after start: "+Seconds(report.extinguishedSeconds));
            time.AppendLine("\nSpray duration: "+Seconds(report.spraySeconds));
            time.AppendLine("Effective spray: "+Seconds(report.effectiveSpraySeconds));
            time.AppendLine("\nTimes include decisions and pauses.");
            time.AppendLine("First response is an observed action or movement.");
            time.AppendLine("No performance rating or allowances applied.");
            time.Append("These are observed times, not standard times.");
            pages.Add(time.ToString());

            var method=new StringBuilder("METHOD ANALYSIS / PASS\n\n");
            method.AppendLine("Pickup to pull: "+Interval(report,"Extinguisher picked up","PASS: pull"));
            method.AppendLine("Pull to established aim: "+Interval(report,"PASS: pull","PASS: aim"));
            method.AppendLine("Aim to first squeeze: "+Interval(report,"PASS: aim","PASS: squeeze"));
            method.AppendLine("Squeeze to first sweep: "+Interval(report,"PASS: squeeze","PASS: sweep"));
            method.AppendLine("\nSequence: "+(!report.extinguisherAttempted?"Not assessed":!report.passInOrder?"Sequence error":
                report.pinPulled&&report.aimedAtBase&&report.squeezed&&report.swept?"Complete in order":"Incomplete"));
            method.AppendLine("\nIntervals use the first recorded milestone.");
            method.AppendLine("Releases/regrabs make intervals discontinuous.");
            method.AppendLine("Aim includes the 0.4 s detection requirement.");
            method.AppendLine("\nPMTS needs an approved motion code system");
            method.AppendLine("and predetermined values. Neither is inferred");
            method.Append("from PASS timestamps. No PMTS total assigned.");
            pages.Add(method.ToString());
            return pages;
        }
        static string Seconds(float value)
        { return ValidTime(value)?AssessmentReport.F(value)+" s":"Not recorded"; }
        static bool ValidTime(float value)
        { return value>=0&&!float.IsNaN(value)&&!float.IsInfinity(value); }
        static AssessmentEvent First(AssessmentReport report,string action)
        {
            AssessmentEvent earliest=null;
            if(report.events==null)return null;
            foreach(var e in report.events)
                if(e!=null&&e.action==action&&ValidTime(e.seconds)&&(earliest==null||e.seconds<earliest.seconds))earliest=e;
            return earliest;
        }
        public static string Interval(AssessmentReport report,string startAction,string endAction)
        {
            if(report==null)return "Not recorded";
            var start=First(report,startAction);var end=First(report,endAction);
            if(start==null||end==null)return "Not recorded";
            if(end.seconds<start.seconds)return "Out of order";
            // Event timestamps cannot reconstruct continuous handling across regrabs.
            if(report.events!=null)foreach(var e in report.events)
                if(e!=null&&e.action=="Extinguisher released"&&ValidTime(e.seconds)&&e.seconds>=start.seconds&&e.seconds<=end.seconds)
                    return "Interrupted / released";
            return Seconds(end.seconds-start.seconds);
        }
    }
}
