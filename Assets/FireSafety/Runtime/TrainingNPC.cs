using System.Collections.Generic;
using UnityEngine;

namespace FireSafety
{
    public enum OccupantRole { Student, Teacher, Staff }
    [RequireComponent(typeof(CharacterController))]
    public class TrainingNPC : MonoBehaviour
    {
        public TrainingSession session;
        public TrainingRouteGraph graph;
        public int identity;
        public float responseDelay=3;
        public float speed=1.15f;
        public Transform leftArm,rightArm,leftLeg,rightLeg;
        public AudioSource voice;
        public OccupantRole role;
        public bool seated, corridorDuty;
        public TrainingDoor workplaceDoor;
        public string RoleLabel {get{return role+" "+(identity+1);}}
        public bool Emergency {get;private set;}
        public bool AtAssembly {get;private set;}
        public bool AtRefuge {get;private set;}
        public string ExitUsed {get;private set;}
        public string activity="Walking";
        CharacterController body;
        Vector3 home;
        Quaternion homeRotation;
        List<int> route=new List<int>();
        int step;
        float gravity;
        float idle;
        float gait;
        float stuck;
        Vector3 previous;
        int replans;
        bool arrivalLogged;
        readonly List<Vector3> roomExit=new List<Vector3>();
        int exitStep;
        bool homeCaptured,poseApplied,posedEmergency;
        float localAlertAt=-1,routeCheck;
        void Awake(){CaptureHome();}
        void CaptureHome(){body=GetComponent<CharacterController>();home=transform.position;homeRotation=transform.rotation;previous=home;homeCaptured=true;}
        public void ResetOccupant()
        {
            if(!homeCaptured)CaptureHome();
            if(body==null)body=GetComponent<CharacterController>();
            body.enabled=false;transform.SetPositionAndRotation(home,homeRotation);body.enabled=true;
            Emergency=false;AtAssembly=false;AtRefuge=false;ExitUsed="";route.Clear();step=0;idle=identity*0.3f;gravity=0;replans=0;stuck=0;previous=home;arrivalLogged=false;
            roomExit.Clear();exitStep=0;localAlertAt=-1;routeCheck=0;poseApplied=false;activity=role==OccupantRole.Teacher?"Teaching a class":role==OccupantRole.Student?"Attending class":corridorDuty?"Corridor duty":"Working in the staff office";
            RestPose();if(voice!=null)voice.Stop();
        }
        public void BeginEmergency()
        {
            if(Emergency)return;
            Emergency=true;RestPose();
            if(workplaceDoor!=null&&Mathf.Abs(transform.position.x)>1.8f)
            {
                float side=Mathf.Sign(transform.position.x),doorZ=workplaceDoor.transform.position.z+.56f;
                roomExit.Add(new Vector3(side*3.0f,home.y,transform.position.z));
                roomExit.Add(new Vector3(side*3.0f,home.y,doorZ+.3f));
                roomExit.Add(new Vector3(0,home.y,doorZ+.3f));
                workplaceDoor.SetForNPC(true);
            }
            Replan();session.Record("Occupant responding",RoleLabel+(role==OccupantRole.Teacher?" stopped teaching and directed the class to respond":role==OccupantRole.Staff?" stopped work and checked the route":" stood up and left class"),"environment");
            if(role==OccupantRole.Teacher)
                foreach(var peer in session.occupants)if(peer!=this&&peer.workplaceDoor==workplaceDoor&&workplaceDoor!=null&&!peer.Emergency)peer.localAlertAt=session.Elapsed+1+peer.identity%2*.6f;
            if(session.audioDirector!=null)session.audioDirector.Occupant(this);
        }
        public void Replan()
        {
            if(!Emergency)return;
            var current=graph.Nearest(exitStep<roomExit.Count?roomExit[roomExit.Count-1]:transform.position,session.PrimaryBlocked,session.SecondaryBlocked);
            bool trapped=(session.ExitBlocked&&!session.HasSecondaryGroundExit)||(session.PrimaryBlocked&&transform.position.y>2);
            int floor=Mathf.Clamp(Mathf.RoundToInt(transform.position.y/3.5f),0,2);
            route=trapped?graph.FindPath(current,n=>n.kind==RouteKind.Refuge&&Mathf.Abs(n.position.y-floor*3.5f)<0.5f,
                session.PrimaryBlocked,session.SecondaryBlocked,session.ExitBlocked,SafeEdge,session.Scenario.secondaryExitBlocked):EvacuationPath(current);
            if(route.Count==0&&!trapped)
            {
                trapped=true;
                route=graph.FindPath(current,n=>n.kind==RouteKind.Refuge&&Mathf.Abs(n.position.y-floor*3.5f)<.5f,session.PrimaryBlocked,session.SecondaryBlocked,session.ExitBlocked,SafeEdge,session.Scenario.secondaryExitBlocked);
            }
            if(route.Count==0&&workplaceDoor!=null&&Mathf.Abs(transform.position.x)>1.8f)
            {
                roomExit.Clear();exitStep=0;AtRefuge=true;workplaceDoor.SetForNPC(false);activity="Waiting for assistance in workplace";
                session.Record("Occupant sheltering",RoleLabel+" could not find a clear route and closed the workplace door","environment");return;
            }
            step=exitStep<roomExit.Count?0:route.Count>1?1:0;AtAssembly=false;AtRefuge=false;stuck=0;activity=trapped?"Moving to a room for assistance":"Evacuating";
        }
        List<int> EvacuationPath(int current)
        {
            if(TrainingSession.Locate(transform.position)==RouteKind.Outside||TrainingSession.Locate(transform.position)==RouteKind.Assembly)
                return graph.FindPath(current,n=>n.kind==RouteKind.Assembly,session.PrimaryBlocked,session.SecondaryBlocked,session.ExitBlocked,SafeEdge,session.Scenario.secondaryExitBlocked);
            // Choose the nearest safe EXIT first; the distance across the courtyard must
            // not make an occupant ignore a much closer building exit.
            var path=graph.FindPath(current,n=>n.kind==RouteKind.Outside,session.PrimaryBlocked,session.SecondaryBlocked,session.ExitBlocked,SafeEdge,session.Scenario.secondaryExitBlocked);
            if(path.Count==0)return path;
            var outside=graph.FindPath(path[path.Count-1],n=>n.kind==RouteKind.Assembly,session.PrimaryBlocked,session.SecondaryBlocked,session.ExitBlocked,SafeEdge,session.Scenario.secondaryExitBlocked);
            if(outside.Count==0)return new List<int>();
            for(int i=1;i<outside.Count;i++)path.Add(outside[i]);return path;
        }
        bool SafeEdge(RouteNode from,RouteNode to)
        {
            // Sample edges so a fire between nodes cannot be skipped by the planner.
            int samples=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(from.position,to.position)/.4f));
            for(int i=0;i<=samples;i++)
            {
                var p=Vector3.Lerp(from.position,to.position,i/(float)samples);
                if(session.incident.HeatAt(p)>0)return false;
                foreach(var h in session.routeFires)if(h.HeatAt(p)>0)return false;
                if(session.fireDevelopment!=null)foreach(var h in session.fireDevelopment.zones)if(h.HeatAt(p)>0)return false;
            }
            return true;
        }
        void Update()
        {
            if(session.phase!=SessionPhase.Assessing || session.player.TrackingUnavailable)return;
            float dt=Time.deltaTime;
            if(Emergency&&string.IsNullOrEmpty(ExitUsed)&&TrainingSession.Locate(transform.position)==RouteKind.Outside)
            {ExitUsed=transform.position.z<6?"Second exit beside lift":"Main entrance";session.Record("Occupant used exit",RoleLabel+": "+ExitUsed,"environment");}
            if(!Emergency&&localAlertAt>=0&&session.Elapsed>=localAlertAt)BeginEmergency();
            if(!Emergency && session.Elapsed>responseDelay && session.incident.Active &&
                Mathf.Abs(transform.position.y-session.incident.transform.position.y)<1 && Vector3.Distance(transform.position,session.incident.transform.position)<5&&CanSeeIncident())
            {BeginEmergency();session.Record("Occupant responded to visible fire","Occupant "+(identity+1),"environment");}
            // Classes and office work remain at their assigned places before an emergency.
            if(!Emergency){RestPose();return;}
            if(AtAssembly || AtRefuge){RestPose();return;}
            routeCheck+=dt;
            if(Emergency&&exitStep>=roomExit.Count&&step<route.Count&&routeCheck>1.2f)
            {routeCheck=0;var here=new RouteNode {position=transform.position};if(!SafeEdge(here,graph.nodes[route[step]])){Replan();if(AtRefuge)return;}}
            if(idle>0){idle-=dt;RestPose();return;}
            if(route.Count==0 || step>=route.Count)
            {
                if(Emergency)
                {
                    AtAssembly=TrainingSession.Locate(transform.position)==RouteKind.Assembly;
                    AtRefuge=TrainingSession.Locate(transform.position)==RouteKind.Refuge;
                    activity=AtAssembly?"At assembly":AtRefuge?"Waiting for assistance":"Waiting";
                    if(AtRefuge){int f=Mathf.Clamp(Mathf.RoundToInt(transform.position.y/3.5f),0,2);session.refugeDoors[f].SetForNPC(false);}
                    if(!arrivalLogged && (AtAssembly||AtRefuge)){arrivalLogged=true;session.Record("Occupant reached safe destination","Occupant "+(identity+1)+": "+activity,"environment");if(AtRefuge&&session.audioDirector!=null)session.audioDirector.Occupant(this,true);}
                    RestPose();return;
                }
                return;
            }
            bool leavingRoom=exitStep<roomExit.Count;
            Vector3 target=leavingRoom?roomExit[exitStep]:graph.nodes[route[step]].position;
            if(leavingRoom&&workplaceDoor!=null)workplaceDoor.SetForNPC(true);
            if(!leavingRoom&&graph.nodes[route[step]].kind==RouteKind.Assembly)target+=new Vector3((identity%3-1)*.7f,0,(identity/3-1.5f)*.55f);
            if(!leavingRoom&&graph.nodes[route[step]].kind==RouteKind.Refuge){int local=identity%4;target+=new Vector3(local%2==0?-.4f:.4f,0,local/2==0?-.35f:.35f);}
            if(!leavingRoom&&graph.nodes[route[step]].kind==RouteKind.Corridor)target.x+=(identity%2==0?-1:1)*0.3f;
            if(!leavingRoom&&session.fireDevelopment!=null&&graph.nodes[route[step]].kind==RouteKind.Corridor)
                foreach(var fire in session.fireDevelopment.zones)
                    if(fire.Active&&Mathf.Abs(fire.transform.position.y-transform.position.y)<1&&Mathf.Abs(fire.transform.position.z-target.z)<3)
                        target.x=fire.transform.position.x<0?.6f:-.6f;
            Vector3 flat=Vector3.ProjectOnPlane(target-transform.position,Vector3.up);
            if(flat.magnitude<0.24f && Mathf.Abs(target.y-transform.position.y)<0.55f)
            {
                if(leavingRoom)
                {
                    exitStep++;
                    if(exitStep==roomExit.Count&&workplaceDoor!=null)
                    {
                        bool others=false;
                        foreach(var npc in session.occupants)if(npc!=this&&npc.workplaceDoor==workplaceDoor&&Mathf.Abs(npc.transform.position.x)>1.8f)others=true;
                        if(!others){workplaceDoor.SetForNPC(false);session.Record("Occupant closed workplace door",RoleLabel,"environment");}
                    }
                }
                else step++;
                return;
            }
            foreach(var d in session.doors)
                if(Vector3.Distance(d.transform.position,transform.position)<1.5f && !d.IsOpen)
                {if(d.refugeDoor && Emergency)d.SetForNPC(true);}
            Vector3 movement=flat.normalized*speed*(Emergency?1.15f:0.65f)*dt;
            if(flat.magnitude<movement.magnitude)movement=flat;
            gravity=body.isGrounded?-1.5f:gravity-9.81f*dt;
            body.Move(movement+Vector3.up*gravity*dt);
            if(flat.sqrMagnitude>0.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(flat),dt*6);
            gait+=dt*speed*7;float angle=Mathf.Sin(gait)*22;
            if(leftArm!=null)leftArm.localRotation=Quaternion.Euler(angle,0,0);
            if(rightArm!=null)rightArm.localRotation=Quaternion.Euler(-angle,0,0);
            if(leftLeg!=null)leftLeg.localRotation=Quaternion.Euler(-angle,0,0);
            if(rightLeg!=null)rightLeg.localRotation=Quaternion.Euler(angle,0,0);
            if(Vector3.Distance(transform.position,previous)<0.003f)stuck+=dt;else stuck=0;
            previous=transform.position;
            if(stuck>4 && Emergency && replans<4){replans++;Replan();}
        }
        bool CanSeeIncident()
        {
            Vector3 eye=transform.position+Vector3.up*(seated?1.2f:1.6f),target=session.incident.transform.position+Vector3.up*1.2f;
            return !Physics.Linecast(eye,target,~((1<<5)|(1<<8)|(1<<9)),QueryTriggerInteraction.Ignore);
        }
        void RestPose()
        {
            if(leftArm!=null)leftArm.localRotation=Quaternion.identity;if(rightArm!=null)rightArm.localRotation=Quaternion.identity;
            if(leftLeg!=null)leftLeg.localRotation=Quaternion.identity;if(rightLeg!=null)rightLeg.localRotation=Quaternion.identity;
            if(poseApplied&&posedEmergency==Emergency)
            {
                if(!Emergency&&seated){leftArm.localRotation=rightArm.localRotation=Quaternion.Euler(-65,0,0);leftLeg.localRotation=rightLeg.localRotation=Quaternion.Euler(-65,0,0);}
                return;
            }
            poseApplied=true;posedEmergency=Emergency;
            // Joint positions are restored each time, so sitting never accumulates offsets.
            foreach(var limb in new[]{leftArm,rightArm})if(limb!=null){var p=limb.localPosition;p.y=!Emergency&&seated?.92f:1.37f;limb.localPosition=p;if(!Emergency&&seated)limb.localRotation=Quaternion.Euler(-65,0,0);}
            foreach(var limb in new[]{leftLeg,rightLeg})if(limb!=null){var p=limb.localPosition;p.y=!Emergency&&seated?.48f:.9f;limb.localPosition=p;if(!Emergency&&seated)limb.localRotation=Quaternion.Euler(-65,0,0);}
            foreach(string part in new[]{"Torso","Head","Neck","Hair"})
            {var t=transform.Find(part);if(t==null)continue;var p=t.localPosition;float y=part=="Torso"?1.15f:part=="Head"?1.63f:part=="Neck"?1.49f:1.74f;p.y=y-(!Emergency&&seated?.43f:0);t.localPosition=p;}
            foreach(Transform t in transform)if(t.name=="Eye"||t.name=="Pupil"){var p=t.localPosition;p.y=1.66f-(!Emergency&&seated?.43f:0);t.localPosition=p;}
        }
    }
}
