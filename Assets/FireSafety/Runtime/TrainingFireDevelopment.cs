using UnityEngine;

namespace FireSafety
{
    public class TrainingFireDevelopment : MonoBehaviour
    {
        public TrainingSession session;
        public TrainingHazard[] zones;
        public TrainingDoor sourceDoor,targetDoor;
        public float SpreadExposure {get;private set;}
        public bool Uncontrolled
        {get{if(session.incident.Active)return true;foreach(var zone in zones)if(zone.Active)return true;return false;}}
        public bool CanReachStairs {get{return Uncontrolled&&SpreadExposure>=session.Scenario.escalationSeconds;}}
        public void Configure(int room,int floor,TrainingDoor source,TrainingDoor target)
        {
            Reset();sourceDoor=source;targetDoor=target;float y=floor*3.5f+.08f;
            zones[0].transform.position=new Vector3(room==1?-5.1f:5.1f,y,room==1?10:room==2?22:4);
            zones[1].transform.position=new Vector3(room==1?-1.15f:1.15f,y,room==1?13.5f:room==2?19.7f:5);
            zones[2].transform.position=new Vector3(-3.2f,y,room==0?4:17.5f);
            if(room>=3)
            {
                Vector3 start=IncidentSites.Position(room,floor);
                zones[0].transform.position=start+new Vector3(room==9?.7f:.4f,0,.7f);
                zones[1].transform.position=new Vector3(.7f,y,room==9?29:room==8?26:Mathf.Clamp(start.z+2,1,28));
                zones[2].transform.position=new Vector3(-3.2f,y,room>=8?22.5f:start.z<8?4:17.5f);
            }
        }
        public void Reset(){SpreadExposure=0;if(zones!=null)foreach(var zone in zones)zone.ResetHazard(FireKind.OrdinaryCombustibles,false);}
        public void Tick(float elapsed,float dt)
        {
            if(!session.Scenario.developingFire||!Uncontrolled)return;
            session.incident.Grow(dt);
            session.report.peakFireFootprint=Mathf.Max(session.report.peakFireFootprint,session.incident.footprintScale);
            float deadline=session.Scenario.escalationSeconds;
            if(elapsed>=deadline*.25f)Activate(0,"Fire enlarged at the incident location",1.25f);
            bool outside=sourceDoor==null||sourceDoor.IsOpen||zones[1].Active;
            if(outside)SpreadExposure+=Mathf.Max(0,dt);
            else session.report.externalSpreadDelaySeconds+=Mathf.Max(0,dt);
            if(outside&&SpreadExposure>=deadline*.5f)Activate(1,"Fire and smoke reached the corridor",.55f);
            if(outside&&SpreadExposure>=deadline*.75f&&(targetDoor==null||targetDoor.IsOpen))Activate(2,"Fire reached another open room",1.4f);
        }
        void Activate(int index,string detail,float size)
        {
            if(zones[index].Active)return;
            zones[index].ResetHazard(session.Scenario.fireKind,true,28,size);
            session.report.spreadZonesActivated++;session.Record("Fire spread",detail,"environment");
            foreach(var npc in session.occupants)if(npc.Emergency)npc.Replan();
        }
    }
}
