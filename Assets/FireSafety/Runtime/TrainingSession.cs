using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireSafety
{
    public class TrainingSession : MonoBehaviour
    {
        public ScenarioDefinition[] scenarios=ScenarioDefinition.Defaults();
        public TrainingXRPlayer player;
        public TrainingRouteGraph graph;
        public TrainingPresentation presentation;
        public TrainingLift lift;
        public TrainingHazard incident;
        public TrainingHazard[] routeFires;
        public TrainingFireDevelopment fireDevelopment;
        public TrainingNPC[] occupants;
        public TrainingExtinguisher[] extinguishers;
        public TrainingBucket[] buckets;
        public TrainingWaterSource[] waterSources;
        public TrainingDoor[] doors;
        public Transform[] spawnPoints;
        public TrainingDoor[] refugeDoors;
        public TrainingDoor[] incidentDoors;
        public string participantCode="Participant";
        public SessionPhase phase=SessionPhase.Ready;
        public AssessmentReport report;
        public int scenarioIndex;
        public bool PrimaryBlocked { get; private set; }
        public bool SecondaryBlocked { get; private set; }
        public bool ExitBlocked {get;private set;}
        public bool HasSecondaryGroundExit {get{return !Scenario.secondaryExitBlocked&&graph.nodes.Exists(n=>n.kind==RouteKind.Outside&&n.secondaryExit);}}
        public bool HasSafeEgress {get{return CurrentRoute==RouteKind.Outside||CurrentRoute==RouteKind.Assembly||((!ExitBlocked||HasSecondaryGroundExit)&&(CurrentFloor==0||!PrimaryBlocked));}}
        public bool AlarmRaised { get; private set; }
        public bool AssistanceRequested { get; private set; }
        public float Elapsed { get; private set; }
        public ScenarioDefinition Scenario { get { return scenarios[scenarioIndex]; } }
        public RouteKind CurrentRoute { get; private set; } = RouteKind.Corridor;
        public int CurrentFloor { get { return Mathf.Clamp(Mathf.RoundToInt(player.transform.position.y/3.5f),0,2); } }
        public string LastSavedDirectory { get; private set; }
        public AudioSource alarmAudio;
        public TrainingAudio audioDirector;
        public bool exportReports;
        public bool persistLatestReport=true;
        public string LatestReportSaveError {get;private set;}
        string LatestReportPath {get{return Path.Combine(Application.persistentDataPath,"LastAssessment.json");}}
        public int PendingScenarioIndex {get;private set;}
        public RenderPipelineAsset desktopPipeline;
        public RenderPipelineAsset vrPipeline;
        public List<AssessmentReport> history=new List<AssessmentReport>();
        readonly Dictionary<string,float> eventCooldowns=new Dictionary<string,float>();
        Vector3 previousPosition;
        float sampleTimer;
        float refugeTimer;
        float alarmDelay;
        bool escalated;
        bool batch;
        int completedInBatch;
        int[] order;
        int frameCount,slowFrames;
        double frameDuration;
        Vector3 initialPosition;
        bool movementRecorded;
        RenderPipelineAsset previousPipeline;
        bool renderingConfigured;
        readonly List<int> scenarioBag=new List<int>();
        public TrainingGameSettings gameSettings=new TrainingGameSettings();
        readonly System.Random gameRandom=new System.Random();
        int gameNumber;
        public void PrepareGame(bool random)
        {
            if(phase==SessionPhase.Assessing)return;
            if(random)gameSettings=TrainingGameSettings.Draw(gameRandom);
            var generated=gameSettings.CreateScenario((random?"RANDOM-":"CUSTOM-")+(++gameNumber));
            // Reuse one custom slot so repeated games do not grow the scenario array.
            if(scenarios.Length==7)Array.Resize(ref scenarios,8);
            scenarios[7]=generated;PrepareAssessment(7);
        }
        public void PrepareVariedAssessment()
        {
            if(phase==SessionPhase.Assessing)return;
            if(scenarioBag.Count==0)
            {
                for(int i=0;i<scenarios.Length;i++)scenarioBag.Add(i);
                for(int i=scenarioBag.Count-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);int item=scenarioBag[i];scenarioBag[i]=scenarioBag[j];scenarioBag[j]=item;}
                if(history.Count>0&&scenarioBag.Count>1&&scenarios[scenarioBag[scenarioBag.Count-1]].id==history[history.Count-1].scenarioId)
                {int item=scenarioBag[0];scenarioBag[0]=scenarioBag[scenarioBag.Count-1];scenarioBag[scenarioBag.Count-1]=item;}
            }
            int selected=scenarioBag[scenarioBag.Count-1];scenarioBag.RemoveAt(scenarioBag.Count-1);PrepareAssessment(selected);
        }

        void Start()
        {
            Application.targetFrameRate=90;
            QualitySettings.vSyncCount=0;
            previousPipeline=QualitySettings.renderPipeline;
            renderingConfigured=true;
            ConfigureRendering(UnityEngine.XR.XRSettings.isDeviceActive);
            // Occupants collide with architecture while remaining unobtrusive in narrow VR passages.
            for(int i=0;i<occupants.Length;i++)
            {
                var a=occupants[i].GetComponent<CharacterController>();
                Physics.IgnoreCollision(a,player.Body);
                for(int j=0;j<i;j++)Physics.IgnoreCollision(a,occupants[j].GetComponent<CharacterController>());
            }
            if(alarmAudio!=null)
            {
                if(alarmAudio.clip==null)
                {
                    int rate=22050;var samples=new float[rate*2];
                    for(int i=0;i<samples.Length;i++){float t=(float)i/rate;samples[i]=0.45f*Mathf.Sin(2*Mathf.PI*(750*t-120*Mathf.Cos(2*Mathf.PI*t)/(2*Mathf.PI)));}
                    var clip=AudioClip.Create("Training alarm",samples.Length,1,rate,false);clip.SetData(samples,0);alarmAudio.clip=clip;
                }
                alarmAudio.loop=true;alarmAudio.volume=0.38f;
            }
            if(persistLatestReport)
            {
                try{var saved=LatestAssessmentStore.Load(LatestReportPath);if(saved!=null){report=saved;history.Add(saved);}}
                catch(Exception ex){Debug.LogWarning("Latest report could not be restored: "+ex.Message);}
            }
            presentation.ShowMenu();
        }
        public void ConfigureRendering(bool vr)
        {var asset=vr?vrPipeline:desktopPipeline;if(asset!=null&&QualitySettings.renderPipeline!=asset)QualitySettings.renderPipeline=asset;}
        void OnDestroy(){if(renderingConfigured)QualitySettings.renderPipeline=previousPipeline;}
        public void StartAssessment(int index, bool battery=false)
        {
            if(phase==SessionPhase.Assessing)return;
            if(index<0 || index>=scenarios.Length)return;
            if(player.IsVR && (player.TrackingUnavailable || !player.ControllersReady))
            {presentation.ShowMenu();return;}
            index=ConfigureSequence(index,battery);
            Begin(index);
        }
        int ConfigureSequence(int index,bool battery)
        {
            batch=battery;
            if(battery)
            {
                completedInBatch=0; order=new int[scenarios.Length];for(int i=0;i<order.Length;i++)order[i]=i;
                for(int i=order.Length-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);int n=order[i];order[i]=order[j];order[j]=n;}
                index=order[0];
            }
            return index;
        }
        public void PrepareAssessment(int index,bool battery=false)
        {
            if(phase==SessionPhase.Assessing||index<0||index>=scenarios.Length)return;
            PendingScenarioIndex=ConfigureSequence(index,battery);phase=SessionPhase.Briefing;
            ClearIncidentEffects();
            presentation.ShowBriefing();
        }
        public void BeginPreparedAssessment()
        {
            if(phase!=SessionPhase.Briefing)return;
            if(player.IsVR&&(player.TrackingUnavailable||!player.ControllersReady)){presentation.ShowBriefing();return;}
            Begin(PendingScenarioIndex);
        }
        void Begin(int index)
        {
            scenarioIndex=index; Elapsed=0; AlarmRaised=false;AssistanceRequested=false;escalated=false;
            LastSavedDirectory=null;if(audioDirector!=null)audioDirector.BeginAssessment();
            eventCooldowns.Clear(); sampleTimer=0;refugeTimer=0;alarmDelay=0;CurrentRoute=RouteKind.Corridor;frameCount=slowFrames=0;frameDuration=0;
            PrimaryBlocked=Scenario.primaryBlocked;SecondaryBlocked=true;ExitBlocked=Scenario.exitBlocked;
            if(alarmAudio!=null)alarmAudio.Stop();
            foreach(var d in doors)d.ResetDoor();
            foreach(var e in extinguishers)e.ResetTool();
            if(buckets!=null)foreach(var b in buckets)b.ResetBucket();
            if(waterSources!=null)foreach(var source in waterSources)source.ResetSource();
            if(lift!=null)lift.ResetLift();
            player.DropTool();player.Teleport(spawnPoints[Scenario.startFloor].position,spawnPoints[Scenario.startFloor].rotation);
            if(ExitBlocked)player.Teleport(new Vector3(.15f,.06f,13.4f),Quaternion.Euler(0,-100,0));
            previousPosition=player.Feet;
            initialPosition=previousPosition;movementRecorded=false;
            report=new AssessmentReport { sessionId=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6),
                participantCode=participantCode,startedUtc=DateTime.UtcNow.ToString("o"),scenarioId=Scenario.id,
                scenario=Scenario.title,fireType=Scenario.fireKind.ToString(),npcCount=occupants.Length };
            phase=SessionPhase.Assessing;
            bool legacyEntrance=ExitBlocked&&Scenario.incidentRoom!=7;
            incident.transform.position=IncidentSites.Position(legacyEntrance?7:Scenario.incidentRoom,Scenario.startFloor);
            incident.ResetHazard(Scenario.fireKind,true,legacyEntrance?30:Scenario.initialStrength,legacyEntrance?2.2f:Scenario.initialFootprint);
            TrainingDoor sourceDoor=null,targetDoor=null;float nearestSource=float.MaxValue,nearestTarget=float.MaxValue;
            foreach(var door in doors)
            {
                if(Mathf.Abs(door.transform.position.y-Scenario.startFloor*3.5f)>.5f)continue;
                float distance=Vector3.Distance(door.transform.position,incident.transform.position);
                if(IncidentSites.IsEnclosed(Scenario.incidentRoom)&&distance<nearestSource){nearestSource=distance;sourceDoor=door;}
                var target=new Vector3(-3.2f,Scenario.startFloor*3.5f,Scenario.incidentRoom==0?4:17.5f);
                distance=Vector3.Distance(door.transform.position,target);if(distance<nearestTarget){nearestTarget=distance;targetDoor=door;}
            }
            if(!ExitBlocked&&sourceDoor!=null)sourceDoor.SetForNPC(true);
            if(Scenario.developingFire&&targetDoor!=null)targetDoor.SetForNPC(true);
            if(!legacyEntrance)
            {
                Vector3 observer=IncidentSites.ObserverPosition(Scenario.incidentRoom,Scenario.startFloor);
                Vector3 look=Vector3.ProjectOnPlane(incident.transform.position-observer,Vector3.up);
                player.Teleport(observer,Quaternion.LookRotation(look));
            }
            previousPosition=initialPosition=player.Feet;
            if(fireDevelopment!=null)fireDevelopment.Configure(Scenario.incidentRoom,Scenario.startFloor,sourceDoor,targetDoor);
            report.peakFireFootprint=incident.footprintScale;
            report.fireDevelopmentRecorded=true;report.fireWasInitiallyDangerous=!incident.PortableSuppressionSuitable;
            UpdateRouteFires();
            foreach(var n in occupants)n.ResetOccupant();
            Record("Scenario started",Scenario.id+"; "+Scenario.title,"observation");
            foreach(var npc in occupants)Record("Occupant initial activity",npc.RoleLabel+": "+npc.activity+" / "+IncidentSites.FloorName(Mathf.RoundToInt(npc.transform.position.y/3.5f)),"environment");
            Record("Initial routes","One main staircase: "+(!PrimaryBlocked?"available":"blocked")+"; main entrance: "+(!ExitBlocked?"available":"blocked"),"observation");
            Record("Second ground exit",HasSecondaryGroundExit?"Available beside the lift":"Blocked / unavailable","observation");
            presentation.Hide();
        }
        void Update()
        {
            if(phase!=SessionPhase.Assessing)return;
            // A disconnected headset suspends the assessment rather than accumulating exposure/time.
            if(player.TrackingUnavailable)return;
            float dt=Time.deltaTime;Elapsed+=dt;
            frameCount++;frameDuration+=Time.unscaledDeltaTime;if(Time.unscaledDeltaTime>1f/72)slowFrames++;
            Vector3 p=player.Feet;
            if(!movementRecorded && Vector3.ProjectOnPlane(p-initialPosition,Vector3.up).magnitude>.35f)
            {movementRecorded=true;Respond();Record("First movement","Moved more than 0.35 m from the start","observed");}
            float movement=Vector3.Distance(p,previousPosition);
            if(movement<2)report.distanceMeters+=movement;
            previousPosition=p; sampleTimer+=dt;
            if(sampleTimer>=0.5f){sampleTimer=0;report.path.Add(new RouteSample { seconds=Elapsed,position=p });}
            if(fireDevelopment!=null)fireDevelopment.Tick(Elapsed,dt);
            if(Scenario.developingFire && !escalated && Elapsed>=Scenario.escalationSeconds&&(fireDevelopment==null?!incident.Extinguished:fireDevelopment.CanReachStairs))
            {
                escalated=true;
                // Only an unsuppressed incident develops into a blocked primary stair.
                PrimaryBlocked=true;UpdateRouteFires();Record("Route conditions changed","Developing fire blocked primary stair","observation");foreach(var n in occupants)n.Replan();
            }
            if(AlarmRaised) { alarmDelay+=dt; foreach(var n in occupants)if(!n.Emergency && alarmDelay>=n.responseDelay)n.BeginEmergency(); }
            RouteKind route=Locate(p);
            if(route!=CurrentRoute)
            {
                if(CurrentRoute==RouteKind.PrimaryStairs&&route==RouteKind.Corridor&&PrimaryBlocked)
                {report.routeChanges++;Record("Retreated from blocked stair","Returned to corridor to reconsider response","observed");}
                CurrentRoute=route;
                if(route==RouteKind.PrimaryStairs || route==RouteKind.SecondaryStairs)Respond();
                Record("Route entered",(route==RouteKind.Refuge?"Room for assistance":route.ToString())+"; floor "+CurrentFloor,"observation");
            }
            if(!AssessmentRules.RouteAvailable(route,PrimaryBlocked,SecondaryBlocked))
                Unsafe("Blocked route entry",route.ToString(),"blocked-"+route,12);
            float heat=incident.HeatAt(p);float smoke=incident.SmokeAt(player.Head.position);
            foreach(var h in routeFires){heat=Mathf.Max(heat,h.HeatAt(p));smoke=Mathf.Max(smoke,h.SmokeAt(player.Head.position));}
            if(fireDevelopment!=null)foreach(var h in fireDevelopment.zones){heat=Mathf.Max(heat,h.HeatAt(p));smoke=Mathf.Max(smoke,h.SmokeAt(player.Head.position));}
            if(heat>0){report.heatExposureSeconds+=dt;Unsafe("Fire proximity","Entered heat zone","heat",15);}
            if(smoke>0)report.smokeExposureSeconds+=dt;
            if(route==RouteKind.Assembly) Finish("Reached the assembly point",true,false);
            else if(route==RouteKind.Refuge)
            {
                bool safe=AssessmentRules.RefugeSuccess(!HasSafeEgress,AlarmRaised,AssistanceRequested,true,!refugeDoors[CurrentFloor].IsOpen);
                refugeTimer=safe?refugeTimer+dt:0;
                if(refugeTimer>=5)Finish("Waited in a closed room and requested assistance",false,true);
            }
            else refugeTimer=0;
            if(phase==SessionPhase.Assessing && Elapsed>=Scenario.durationLimit)Finish("Assessment time limit reached",false,false);
            if(phase==SessionPhase.Assessing && report.heatExposureSeconds>=12)Finish("Assessment stopped: prolonged heat exposure",false,false);
        }
        public static RouteKind Locate(Vector3 p)
        {
            if(p.x<-13 && p.x>-20 && p.z>9 && p.z<17 && p.y<1.2f)return RouteKind.Assembly;
            if(p.x<-7.3f && p.y<1.2f)return RouteKind.Outside;
            if(p.x>1.65f && p.z>11.8f && p.z<15.9f)return RouteKind.PrimaryStairs;
            if(p.x>1.7f && p.x<6.8f && p.z>25.05f && p.z<29.0f)return RouteKind.Washroom;
            if(Mathf.Abs(p.x)<1.6f && p.z>30 && p.z<34.6f)return RouteKind.Washroom;
            if(p.x<-2.1f && p.x>-6.8f && p.z>20.6f && p.z<24.7f)return RouteKind.Refuge;
            return RouteKind.Corridor;
        }
        void UpdateRouteFires()
        {
            for(int i=0;i<routeFires.Length;i++){bool active=i<3?PrimaryBlocked&&Scenario.incidentRoom!=6:SecondaryBlocked;routeFires[i].ResetHazard(Scenario.fireKind,active,30,active?1.15f:1);}
        }
        public void RaiseAlarm()
        {
            if(phase!=SessionPhase.Assessing || AlarmRaised)return;
            AlarmRaised=true;report.alarmSeconds=Elapsed;Respond();
            Record("Alarm activated","Manual call point","observed");
            if(alarmAudio!=null)alarmAudio.Play();
            if(audioDirector!=null)audioDirector.Play(audioDirector.alarmAcknowledged);
            if(lift!=null)lift.SetEmergency(true);
        }
        public void CallAssistance()
        {
            if(phase!=SessionPhase.Assessing || AssistanceRequested)return;
            AssistanceRequested=true;report.assistanceSeconds=Elapsed;Respond();
            Record("Emergency assistance requested","Simulated call; floor "+CurrentFloor+"; route "+CurrentRoute,"observed");
            if(audioDirector!=null)audioDirector.Play(audioDirector.callAcknowledged);
        }
        public void Respond() { if(report!=null && report.firstResponseSeconds<0)report.firstResponseSeconds=Elapsed; }
        public void Record(string action,string detail,string severity="observation")
        {
            if(phase!=SessionPhase.Assessing || report==null)return;
            report.events.Add(new AssessmentEvent {seconds=Elapsed,action=action,detail=detail,severity=severity,position=player.Feet});
        }
        public void Unsafe(string action,string detail,string key,float cooldown=6)
        {
            if(phase!=SessionPhase.Assessing)return;
            if(eventCooldowns.TryGetValue(key,out float last) && Elapsed-last<cooldown)return;
            eventCooldowns[key]=Elapsed;report.unsafeDecisions++;Respond();Record(action,detail,"unsafe");
        }
        public void Finish(string outcome,bool assembly=false,bool refuge=false)
        {
            if(phase!=SessionPhase.Assessing)return;
            Record("Assessment ended",outcome,"observation");
            report.outcome=outcome;report.durationSeconds=Elapsed;
            report.meanFramesPerSecond=frameDuration>0?(float)(frameCount/frameDuration):0;
            report.slowFramePercent=frameCount>0?100f*slowFrames/frameCount:0;
            foreach(var n in occupants){if(n.AtAssembly)report.npcsEvacuated++;if(n.AtRefuge)report.npcsAtRefuge++;if(n.ExitUsed=="Main entrance")report.npcsMainExit++;if(n.ExitUsed=="Second exit beside lift")report.npcsSecondExit++;}
            report.Evaluate(assembly,refuge,AlarmRaised,AssistanceRequested);
            phase=SessionPhase.Complete;
            if(alarmAudio!=null)alarmAudio.Stop();
            if(audioDirector!=null)audioDirector.Stop();
            player.DropTool();
            if(exportReports)
            {
                try { LastSavedDirectory=AssessmentExport.Save(report,Path.Combine(Application.persistentDataPath,"Assessments")); }
                catch(Exception ex){report.saveError=ex.Message;Debug.LogError("Assessment save failed: "+ex);}
            }
            history.Add(report);
            LatestReportSaveError=null;
            if(persistLatestReport)
            {
                try{LatestAssessmentStore.Save(report,LatestReportPath);}
                catch(Exception ex){LatestReportSaveError=ex.Message;Debug.LogWarning("Latest report could not be saved: "+ex.Message);}
            }
            if(exportReports&&string.IsNullOrEmpty(report.saveError))try{AssessmentExport.SaveIndex(history,Path.Combine(Application.persistentDataPath,"Assessments"));}catch(Exception ex){Debug.LogWarning("Comparison index save failed: "+ex.Message);}
            presentation.ShowReport(0);
        }
        public void Next()
        {
            if(phase==SessionPhase.Assessing)return;
            ClearIncidentEffects();
            if(batch && ++completedInBatch<order.Length){PendingScenarioIndex=order[completedInBatch];phase=SessionPhase.Briefing;presentation.ShowBriefing();}
            else { batch=false;phase=SessionPhase.Ready;presentation.ShowMenu(); }
        }
        public void ShowLatestReport()
        {
            if(phase==SessionPhase.Assessing||history.Count==0)return;
            report=history[history.Count-1];phase=SessionPhase.Complete;presentation.ShowReport(0);
        }
        public void ReturnToMenu()
        {
            if(phase==SessionPhase.Assessing)Finish("Assessment ended by participant",false,false);
            batch=false;phase=SessionPhase.Ready;presentation.ShowMenu();
            ClearIncidentEffects();
            if(audioDirector!=null)audioDirector.Stop();
        }
        void ClearIncidentEffects()
        {
            if(fireDevelopment!=null)fireDevelopment.Reset();
            incident.ResetHazard(Scenario.fireKind,false);foreach(var fire in routeFires)fire.ResetHazard(Scenario.fireKind,false);
            if(alarmAudio!=null)alarmAudio.Stop();
            foreach(var occupant in occupants)if(occupant.voice!=null)occupant.voice.Stop();
        }
        void OnApplicationQuit()
        {
            if(phase==SessionPhase.Assessing)Finish("Application closed before completion",false,false);
        }
    }
}
