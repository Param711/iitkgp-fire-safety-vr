using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FireSafety
{
    public enum FireKind { OrdinaryCombustibles, EnergizedElectrical, FlammableLiquid }
    public enum AgentKind { Water, DryChemicalABC, CarbonDioxide }
    public enum RouteKind { Corridor, PrimaryStairs, SecondaryStairs, Refuge, Outside, Assembly, Washroom }
    public enum SessionPhase { Ready, Assessing, Complete, Briefing }

    [Serializable]
    public class ScenarioDefinition
    {
        public string id;
        public string title;
        public FireKind fireKind;
        public bool primaryBlocked;
        public bool secondaryBlocked;
        public bool developingFire;
        public bool exitBlocked;
        public bool secondaryExitBlocked;
        public float escalationSeconds = 55;
        public int startFloor = 2;
        public float durationLimit = 420;
        public float initialFootprint=1,initialStrength=8;
        public int incidentRoom;
        public static ScenarioDefinition[] Defaults()
        {
            return new[] {
                Make("A01", "Office wastebasket", FireKind.OrdinaryCombustibles, false, false),
                Make("A02", "Energized electrical equipment", FireKind.EnergizedElectrical, false, false),
                new ScenarioDefinition { id="A03",title="Developing laboratory liquid fire",fireKind=FireKind.FlammableLiquid,developingFire=true,escalationSeconds=90,startFloor=1,incidentRoom=2 },
                Make("A04", "Main staircase obstructed by fire", FireKind.EnergizedElectrical, true, false),
                new ScenarioDefinition { id="A05",title="Fire blocks the main entrance",fireKind=FireKind.OrdinaryCombustibles,exitBlocked=true,startFloor=0 },
                Make("A06", "Main staircase unavailable / liquid fire", FireKind.FlammableLiquid, true, false),
                new ScenarioDefinition { id="A07", title="Developing fire / changing route", fireKind=FireKind.FlammableLiquid,
                    developingFire=true, escalationSeconds=55, startFloor=2,incidentRoom=1 }
            };
        }
        static ScenarioDefinition Make(string id, string title, FireKind kind, bool primary, bool secondary)
        { return new ScenarioDefinition { id=id, title=title, fireKind=kind, primaryBlocked=primary, secondaryBlocked=secondary,
            initialFootprint=primary?1.6f:1,initialStrength=primary?24:8,incidentRoom=primary&&kind==FireKind.FlammableLiquid?2:0 }; }
    }

    public static class AssessmentRules
    {
        // Agent suitability is explicit; electrical is a hazard, not a regional class label.
        public static bool Compatible(AgentKind agent, FireKind fire)
        {
            if (agent == AgentKind.DryChemicalABC) return true;
            if (agent == AgentKind.Water) return fire == FireKind.OrdinaryCombustibles;
            return fire != FireKind.OrdinaryCombustibles;
        }
        public static bool RouteAvailable(RouteKind route, bool primaryBlocked, bool secondaryBlocked)
        {
            return !(route == RouteKind.PrimaryStairs && primaryBlocked) &&
                   !(route == RouteKind.SecondaryStairs && secondaryBlocked);
        }
        public static bool RefugeSuccess(bool escapeUnavailable, bool alarm, bool assistance, bool inside, bool doorClosed)
        { return escapeUnavailable && alarm && assistance && inside && doorClosed; }
    }

    [Serializable] public class AssessmentEvent
    {
        public float seconds;
        public string action;
        public string detail;
        public string severity;
        public Vector3 position;
    }
    [Serializable] public class RouteSample { public float seconds; public Vector3 position; }
    [Serializable] public class CategoryResult { public string name; public int earned; public int maximum; public string evidence; }
    [Serializable] public class AssessmentReport
    {
        public string reportVersion="1.0";
        public string sessionId;
        public string participantCode;
        public string startedUtc;
        public string scenarioId;
        public string scenario;
        public string fireType;
        public string outcome;
        public bool assessmentWithoutHints=true;
        public float durationSeconds;
        public float firstResponseSeconds=-1;
        public float alarmSeconds=-1;
        public float assistanceSeconds=-1;
        public float extinguishedSeconds=-1;
        public float distanceMeters;
        public float smokeExposureSeconds;
        public float heatExposureSeconds;
        public float spraySeconds;
        public float effectiveSpraySeconds;
        public int spreadZonesActivated;
        public float peakFireFootprint;
        public float externalSpreadDelaySeconds;
        public bool fireDevelopmentRecorded,fireWasInitiallyDangerous;
        public bool extinguisherAttempted;
        public bool pinPulled;
        public bool aimedAtBase;
        public bool squeezed;
        public bool swept;
        public bool correctAgent;
        public bool passInOrder=true;
        public string extinguisherProfile;
        public bool extinguisherProfileVerified;
        public bool bucketUsed;
        public float waterCollectedLiters,waterPouredLiters,waterEffectiveLiters;
        public int unsafeWaterUses;
        public int unsafeDecisions;
        public int routeChanges;
        public int npcsEvacuated;
        public int npcsAtRefuge;
        public int npcsMainExit,npcsSecondExit;
        public int npcCount;
        public int score;
        public float meanFramesPerSecond;
        public float slowFramePercent;
        public string classification;
        public string exportDirectory;
        public string saveError;
        public List<CategoryResult> categories=new List<CategoryResult>();
        public List<string> recommendations=new List<string>();
        public List<AssessmentEvent> events=new List<AssessmentEvent>();
        public List<RouteSample> path=new List<RouteSample>();

        public void Evaluate(bool reachedAssembly, bool refugeSafe, bool alarm, bool assistance)
        {
            categories.Clear(); recommendations.Clear();
            bool completed = reachedAssembly || refugeSafe;
            Add("Evacuation / shelter objective", completed ? 35 : 0, 35, outcome);
            Add("Raised alarm", alarm ? 20 : 0, 20, alarm ? "Alarm at " + F(alarmSeconds) + " s" : "No alarm activation recorded");
            Add("Requested emergency assistance", assistance ? 10 : 0, 10,
                assistance ? "Simulated call at " + F(assistanceSeconds) + " s" : "No emergency call recorded");
            int safety = Mathf.Max(0, 25 - unsafeDecisions * 5 - Mathf.FloorToInt(heatExposureSeconds * 2) - Mathf.FloorToInt(smokeExposureSeconds / 4));
            Add("Route and hazard decisions", safety, 25, unsafeDecisions + " unsafe decisions; " + F(smokeExposureSeconds) + " s smoke; " + F(heatExposureSeconds) + " s heat");
            int technique = !extinguisherAttempted ? 10 : (pinPulled ? 2 : 0) + (aimedAtBase ? 2 : 0) +
                (squeezed ? 2 : 0) + (swept ? 2 : 0) + (correctAgent ? 2 : 0);
            if(extinguisherAttempted&&!passInOrder)technique=Mathf.Min(technique,6);
            Add("Extinguisher judgment / PASS", technique, 10, !extinguisherAttempted ?
                "Not attempted. Safe evacuation is a valid decision; PASS skill was not assessed." :
                "Pull " + pinPulled + "; Aim " + aimedAtBase + "; Squeeze " + squeezed + "; Sweep " + swept + "; Suitable agent " + correctAgent+"; Complete in order "+(passInOrder&&pinPulled&&aimedAtBase&&squeezed&&swept));
            score=0; foreach (var c in categories) score += c.earned;
            // Outcome and critical safety failures take precedence over an aggregate mark.
            classification = !completed ? "Incomplete / unsuccessful" : unsafeDecisions > 0 || heatExposureSeconds > 1 ?
                "Completed with safety concerns" : refugeSafe&&!reachedAssembly ? "Assistance requested; evacuation pending" : score >= 85 ? "Safe completion" : "Completed; review required";
            if (!alarm) recommendations.Add("Raise the alarm promptly so other occupants can respond.");
            if (!assistance) recommendations.Add("Use a simulated emergency call point from a safe location.");
            if (unsafeDecisions > 0) recommendations.Add("Review each flagged timeline event, especially blocked-route entry, elevator attempts and equipment handling.");
            if (smokeExposureSeconds > 0) recommendations.Add("Avoid smoke-affected routes; reassess rather than continuing into smoke.");
            if (extinguisherAttempted && technique < 10) recommendations.Add("Review the recorded PASS steps and suitable extinguishing agent. Maintain a clear escape route.");
            if (extinguisherAttempted && !extinguisherProfileVerified) recommendations.Add("Extinguisher suitability uses a provisional simulation profile. Confirm the installed cylinder's agent and rating before a real training study.");
            if (!extinguisherAttempted) recommendations.Add("Extinguisher handling was not demonstrated in this session; assess that skill separately.");
            if (!completed) recommendations.Add("Repeat this scenario and reach an assembly point or complete the shelter-and-assistance procedure.");
            if(refugeSafe&&!reachedAssembly)recommendations.Add("Assistance was requested; this session did not simulate a completed rescue or evacuation.");
            bool liftAttempt=false,blockedEntry=false;
            foreach(var action in events){liftAttempt|=action.action=="Elevator attempted during fire";blockedEntry|=action.action=="Blocked route entry";}
            if(liftAttempt)recommendations.Add("Use a safe stair route instead of the lift during fire. If the stair is unsafe, withdraw and request assistance.");
            if(blockedEntry)recommendations.Add("Turn back from the blocked route before entering its heat or smoke. Reassess a safe exit or the closed-room assistance response.");
            if(unsafeWaterUses>0)recommendations.Add("Do not pour water onto energized electrical equipment or flammable liquid. Check the hazard and installed equipment rating before acting.");
            if(fireWasInitiallyDangerous&&extinguisherAttempted)recommendations.Add("This incident was already beyond the portable-equipment training limit. Prioritize safe evacuation or requesting assistance instead of continued spraying.");
            if(spreadZonesActivated>0)recommendations.Add("Review the fire-spread events and reassess routes as conditions change. Closing the source door before corridor spread can delay the modelled outward development.");
            if(externalSpreadDelaySeconds>1)recommendations.Add("Door closure delayed external spread in this trial. Keep using safe door-closing behaviour; existing fire outside that room still requires reassessment.");
            if(completed&&unsafeDecisions==0)recommendations.Add("Maintain the safe-route decisions shown here. Practise with varied incidents and compare the decision evidence, not only completion time.");
            recommendations.Add("Timing and path length are descriptive measures, not a reason to rush or take an unsafe route.");
        }
        void Add(string name, int value, int max, string evidence)
        { categories.Add(new CategoryResult {name=name,earned=value,maximum=max,evidence=evidence}); }
        public static string F(float n) { return n.ToString("0.0", CultureInfo.InvariantCulture); }
    }

    public static class AssessmentExport
    {
        public static string Save(AssessmentReport report, string root)
        {
            string folder=Path.Combine(root, report.sessionId);
            Directory.CreateDirectory(folder);
            report.exportDirectory=folder;
            File.WriteAllText(Path.Combine(folder,"report.json"), JsonUtility.ToJson(report,true), Encoding.UTF8);
            var csv=new StringBuilder("seconds,action,detail,severity,x,y,z\r\n");
            foreach (var e in report.events) csv.AppendLine(AssessmentReport.F(e.seconds)+","+Cell(e.action)+","+Cell(e.detail)+","+Cell(e.severity)+","+
                e.position.x.ToString(CultureInfo.InvariantCulture)+","+e.position.y.ToString(CultureInfo.InvariantCulture)+","+e.position.z.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(folder,"timeline.csv"),csv.ToString(),Encoding.UTF8);
            var path=new StringBuilder("seconds,x,y,z\r\n");
            foreach(var p in report.path) path.AppendLine(AssessmentReport.F(p.seconds)+","+p.position.x.ToString(CultureInfo.InvariantCulture)+","+p.position.y.ToString(CultureInfo.InvariantCulture)+","+p.position.z.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(folder,"path.csv"),path.ToString(),Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder,"analysis.html"), Html(report),Encoding.UTF8);
            return folder;
        }
        static string Cell(string value)
        {
            value=value??"";
            // Spreadsheet consumers must not interpret report text as formulas.
            if(value.Length>0 && "=+-@\t\r".IndexOf(value[0])>=0) value="'"+value;
            return "\""+value.Replace("\"","\"\"")+"\"";
        }
        static string E(string s) { return System.Net.WebUtility.HtmlEncode(s??""); }
        public static string Html(AssessmentReport r)
        {
            var b=new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Fire safety assessment</title><style>body{font:16px/1.6 system-ui;background:#eff3f5;color:#152735;max-width:1100px;margin:auto;padding:36px}article{background:white;padding:32px;border-radius:16px}h1,h2{line-height:1.2}small{color:#456}table{border-collapse:collapse;width:100%;margin:20px 0}td,th{text-align:left;padding:10px;border-bottom:1px solid #dce4e9}th{background:#eaf0f4}.score{font-size:48px;color:#126966}.unsafe{color:#b42318}svg{background:#f5f8fa;width:100%;height:240px}li{margin:8px 0}</style><article>");
            b.Append("<small>ASSESSMENT / NO LIVE HINTS / "+E(r.startedUtc)+"</small><h1>Fire safety session analysis</h1><p>Participant "+E(r.participantCode)+" · "+E(r.scenarioId)+" · "+E(r.scenario)+"</p><div class='score'>"+r.score+" / 100</div><h2>"+E(r.classification)+"</h2><p>"+E(r.outcome)+"</p>");
            b.Append("<p>Duration: "+AssessmentReport.F(r.durationSeconds)+" s · Distance: "+AssessmentReport.F(r.distanceMeters)+" m · First response: "+(r.firstResponseSeconds<0?"not observed":AssessmentReport.F(r.firstResponseSeconds)+" s")+" · Route changes: "+r.routeChanges+"</p>");
            b.Append("<p>NPCs: "+r.npcsEvacuated+" at assembly, "+r.npcsAtRefuge+" awaiting assistance in rooms, out of "+r.npcCount+". NPC status is context and does not score the trainee.</p><h2>Criteria and evidence</h2><table><tr><th>Criterion</th><th>Mark</th><th>Evidence</th></tr>");
            foreach(var c in r.categories) b.Append("<tr><td>"+E(c.name)+"</td><td>"+c.earned+" / "+c.maximum+"</td><td>"+E(c.evidence)+"</td></tr>");
            b.Append("</table><h2>Extinguisher observations</h2><p>Spray: "+AssessmentReport.F(r.spraySeconds)+" s; effective: "+AssessmentReport.F(r.effectiveSpraySeconds)+" s. Fire suppressed: "+(r.extinguishedSeconds<0?"not recorded":AssessmentReport.F(r.extinguishedSeconds)+" s")+".</p><h2>Water-bucket observations</h2><p>Collected: "+AssessmentReport.F(r.waterCollectedLiters)+" L; poured: "+AssessmentReport.F(r.waterPouredLiters)+" L; effective: "+AssessmentReport.F(r.waterEffectiveLiters)+" L. Water use is evaluated separately from PASS.</p><h2>Movement trace</h2><p>Top-down view; color indicates floor (blue: ground, teal: first, orange: second). Vertical transitions overlap in this projection. Full 3D positions are in path.csv.</p><svg viewBox='-24 -5 38 45' role='img' aria-label='Recorded movement path'>");
            for(int i=1;i<r.path.Count;i++) { var a=r.path[i-1].position; var p=r.path[i].position; string col=p.y>5.5f?"#c66a19":p.y>2?"#09877e":"#3068b2"; b.AppendFormat(CultureInfo.InvariantCulture,"<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' stroke='{4}' stroke-width='.15'/>",a.x,35-a.z,p.x,35-p.z,col); }
            b.Append("</svg><h2>Decision timeline</h2><table><tr><th>Time</th><th>Action</th><th>Detail</th><th>Assessment</th></tr>");
            foreach(var e in r.events) b.Append("<tr><td>"+AssessmentReport.F(e.seconds)+" s</td><td>"+E(e.action)+"</td><td>"+E(e.detail)+"</td><td class='"+(e.severity=="unsafe"?"unsafe":"")+"'>"+E(e.severity)+"</td></tr>");
            b.Append("</table><h2>Runtime performance</h2><p>Mean frame rate: "+AssessmentReport.F(r.meanFramesPerSecond)+" fps. Frames slower than 13.9 ms (72 Hz): "+AssessmentReport.F(r.slowFramePercent)+"%. These application frame timings do not measure headset compositor timing or prove VR comfort.</p><h2>Review points</h2><ul>"); foreach(var t in r.recommendations)b.Append("<li>"+E(t)+"</li>");
            b.Append("</ul><p><small>Prototype rubric v1.0. Route availability, agent suitability and exposure are simplified simulation measures. Architectural additions await photographic verification. No real emergency call is placed. This assessment is not a professional certification.</small></p></article></html>");
            return b.ToString();
        }
        public static void SaveIndex(List<AssessmentReport> history,string root)
        {
            var b=new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><title>Assessment sessions</title><style>body{font:16px/1.6 system-ui;max-width:1000px;margin:40px auto;padding:20px}table{width:100%;border-collapse:collapse}td,th{padding:12px;text-align:left;border-bottom:1px solid #ddd}</style><h1>Assessment session comparison</h1><p>Only completed report exports from this application run are shown. Compare equivalent scenarios and participant codes. Scores use the prototype rubric, not a certification standard.</p><table><tr><th>Participant</th><th>Scenario</th><th>Score</th><th>Time</th><th>Unsafe decisions</th><th>Outcome</th></tr>");
            foreach(var r in history)b.Append("<tr><td>"+E(r.participantCode)+"</td><td><a href='"+E(r.sessionId)+"/analysis.html'>"+E(r.scenarioId)+"</a></td><td>"+r.score+"</td><td>"+AssessmentReport.F(r.durationSeconds)+" s</td><td>"+r.unsafeDecisions+"</td><td>"+E(r.outcome)+"</td></tr>");
            b.Append("</table></html>");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"session-comparison.html"),b.ToString(),Encoding.UTF8);
        }
    }
}
