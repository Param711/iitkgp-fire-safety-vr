using UnityEngine;

namespace FireSafety
{
    public class TrainingBucket : MonoBehaviour
    {
        public TrainingSession session;
        public string locationName;
        public Transform rim;
        public Transform waterSurface;
        public Transform visual;
        public ParticleSystem pourStream;
        public float capacityLiters=4;
        public float waterLiters;
        public bool Held {get;private set;}
        TrainingXRPlayer holder;
        Transform homeParent;
        Vector3 home;
        Quaternion homeRotation;
        Collider[] colliders;
        bool initialized,pouring,fillRecorded,wrongWaterRecorded;
        void Awake(){Initialize();}
        void Initialize()
        {
            if(initialized)return;initialized=true;homeParent=transform.parent;home=transform.position;homeRotation=transform.rotation;
            colliders=GetComponentsInChildren<Collider>(true);
        }
        public void ResetBucket()
        {
            Initialize();Held=false;holder=null;waterLiters=0;pouring=false;fillRecorded=false;wrongWaterRecorded=false;
            transform.SetParent(homeParent);transform.SetPositionAndRotation(home,homeRotation);
            foreach(var c in colliders)c.enabled=true;
            if(visual!=null)visual.localRotation=Quaternion.identity;
            if(pourStream!=null)pourStream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            UpdateWater();
        }
        public void PickUp(TrainingXRPlayer player)
        {
            Initialize();holder=player;Held=true;foreach(var c in colliders)c.enabled=false;
            transform.SetParent(player.rightHand,false);transform.localPosition=new Vector3(0,-.33f,.08f);transform.localRotation=Quaternion.identity;
            session.Respond();session.Record("Bucket picked up",waterLiters.ToString("0.0")+" L available","observed");
        }
        public void Release()
        {
            if(!Held)return;Held=false;holder=null;pouring=false;
            transform.SetParent(homeParent);transform.SetPositionAndRotation(home,homeRotation);
            foreach(var c in colliders)c.enabled=true;
            if(visual!=null)visual.localRotation=Quaternion.identity;
            if(pourStream!=null)pourStream.Stop();
            session.Record("Bucket put down",waterLiters.ToString("0.0")+" L remaining","observed");
        }
        public float FillFrom(TrainingWaterSource source,float dt)
        {
            if(!Held || !source.Flowing || session.phase!=SessionPhase.Assessing || holder.TrackingUnavailable || waterLiters>=capacityLiters)return 0;
            if(holder.IsVR&&!holder.RightControllerTracked)return 0;
            Vector3 difference=source.spout.position-rim.position;
            bool aligned=new Vector2(difference.x,difference.z).magnitude<=.35f && difference.y>=-.08f && difference.y<=.8f;
            // A desktop mouse cannot place a hand independently of the camera.
            if(!holder.IsVR)aligned=Vector3.Distance(holder.Head.position,source.spout.position)<=1.6f;
            if(!aligned)return 0;
            float amount=Mathf.Min(capacityLiters-waterLiters,Mathf.Max(0,dt)*source.litersPerSecond);
            waterLiters+=amount;session.report.waterCollectedLiters+=amount;UpdateWater();
            if(amount>0 && !fillRecorded){fillRecorded=true;session.Record("Bucket filling",source.sourceName,"observed");}
            return amount;
        }
        public void Tick(bool pour,Ray aim,float dt)
        {
            if(!Held || session.phase!=SessionPhase.Assessing)return;
            bool active=pour&&waterLiters>0;
            if(active&&!pouring)
            {
                session.Respond();session.report.bucketUsed=true;session.Record("Water poured",waterLiters.ToString("0.0")+" L in bucket","observed");
                if(pourStream!=null)pourStream.Play();
            }
            if(!active&&pouring&&pourStream!=null)pourStream.Stop();pouring=active;
            if(visual!=null)visual.localRotation=Quaternion.Slerp(visual.localRotation,Quaternion.Euler(active?65:0,0,0),Mathf.Min(1,dt*6));
            if(!active)return;
            float amount=Mathf.Min(waterLiters,Mathf.Max(0,dt)*1.2f);waterLiters-=amount;session.report.waterPouredLiters+=amount;UpdateWater();
            if(pourStream!=null)pourStream.transform.rotation=Quaternion.LookRotation(aim.direction);
            var fire=session.incident;if(!fire.Active)return;
            Vector3 source=holder.IsVR?rim.position:aim.origin;Vector3 target=fire.transform.position+Vector3.up*.18f;
            float distance=Vector3.Distance(source,target);float along=Vector3.Dot(target-source,aim.direction);
            float miss=Vector3.Distance(source+aim.direction*Mathf.Max(0,along),target);
            bool aimed=along>0&&miss<.65f&&distance>=1.6f&&distance<=3.8f;
            bool clear=distance>.5f&&!Physics.Raycast(source,aim.direction,distance-.5f,~(1<<5),QueryTriggerInteraction.Ignore);
            if(!aimed || !clear)return;
            if(!AssessmentRules.Compatible(AgentKind.Water,fire.kind))
            {
                if(!wrongWaterRecorded){wrongWaterRecorded=true;session.report.unsafeWaterUses++;session.Unsafe("Unsafe water use","Water poured toward "+fire.kind,"bucket-water",420);}
                return;
            }
            if(!fire.PortableSuppressionSuitable){session.Unsafe("Water attempted on a developed fire","Fire exceeds the portable-equipment training limit","bucket-developed",420);return;}
            if(!session.AlarmRaised)session.Unsafe("Water response before alarm","Alarm not activated","bucket-alarm",420);
            if(!session.HasSafeEgress){session.Unsafe("Water response with no clear escape route","Main stair or entrance unavailable","bucket-escape",420);return;}
            if(session.AlarmRaised){session.report.waterEffectiveLiters+=amount;fire.Suppress(amount*2.5f);}
        }
        void UpdateWater()
        {
            if(waterSurface==null)return;
            waterSurface.gameObject.SetActive(waterLiters>.01f);
            var p=waterSurface.localPosition;p.y=.035f+.25f*Mathf.Clamp01(waterLiters/capacityLiters);waterSurface.localPosition=p;
            var scale=waterSurface.localScale;scale.x=scale.z=.2f+.09f*Mathf.Clamp01(waterLiters/capacityLiters);waterSurface.localScale=scale;
        }
    }
}
