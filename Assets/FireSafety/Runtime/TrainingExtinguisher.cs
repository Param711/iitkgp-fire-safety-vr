using UnityEngine;

namespace FireSafety
{
    public class TrainingExtinguisher : MonoBehaviour
    {
        public AgentKind agent;
        public bool installedProfileVerified;
        public TrainingSession session;
        public Transform nozzle;
        public GameObject pin;
        public ParticleSystem discharge;
        public AudioSource sprayAudio;
        public float remaining=22;
        public bool PinRemoved {get;private set;}
        public bool Held {get;private set;}
        TrainingXRPlayer holder;
        Vector3 home;
        Quaternion homeRotation;
        Transform homeParent;
        Collider[] colliders;
        bool initialized;
        bool spraying;
        Vector3 firstAim;
        float sweepMinimum,sweepMaximum;
        float baseAimTime;
        bool wrongAgentLogged;
        bool pinAttemptLogged;
        bool baseAimEstablished;
        void Update()
        {
            if(sprayAudio==null)return;
            bool sound=spraying&&Held&&session.phase==SessionPhase.Assessing&&!session.player.TrackingUnavailable;
            if(sound&&!sprayAudio.isPlaying)sprayAudio.Play();else if(!sound&&sprayAudio.isPlaying)sprayAudio.Stop();
        }
        public void Initialize()
        {
            if(initialized)return;initialized=true;home=transform.position;homeRotation=transform.rotation;homeParent=transform.parent;
            colliders=GetComponentsInChildren<Collider>(true);
        }
        void Awake(){Initialize();}
        public void ResetTool()
        {
            Initialize();Held=false;holder=null;PinRemoved=false;remaining=22;wrongAgentLogged=false;pinAttemptLogged=false;baseAimTime=0;baseAimEstablished=false;sweepMinimum=sweepMaximum=0;spraying=false;
            transform.SetParent(homeParent);transform.SetPositionAndRotation(home,homeRotation);
            foreach(var c in colliders)c.enabled=true;
            if(pin!=null)pin.SetActive(true);
            if(nozzle!=null){nozzle.SetParent(transform);nozzle.localPosition=new Vector3(0.12f,0.35f,0);nozzle.localRotation=Quaternion.identity;}
            if(discharge!=null)discharge.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void PickUp(TrainingXRPlayer player)
        {
            Initialize();holder=player;Held=true;
            foreach(var c in colliders)c.enabled=false;
            transform.SetParent(player.leftHand,false);transform.localPosition=new Vector3(0,-0.22f,0.08f);transform.localRotation=Quaternion.identity;
            if(nozzle!=null){nozzle.SetParent(player.rightHand,false);nozzle.localPosition=new Vector3(0,0,0.12f);nozzle.localRotation=Quaternion.identity;}
            session.Respond();session.Record("Extinguisher picked up",agent.ToString(),"observation");
            session.report.extinguisherProfile=agent.ToString();session.report.extinguisherProfileVerified=installedProfileVerified;
        }
        public void Release()
        {
            if(!Held)return;Held=false;spraying=false;
            transform.SetParent(homeParent);transform.SetPositionAndRotation(home,homeRotation);
            if(nozzle!=null){nozzle.SetParent(transform);nozzle.localPosition=new Vector3(0.12f,0.35f,0);nozzle.localRotation=Quaternion.identity;}
            foreach(var c in colliders)c.enabled=true;
            if(discharge!=null)discharge.Stop();holder=null;
            session.Record("Extinguisher released",agent.ToString(),"observation");
        }
        public void PullPin()
        {
            if(!Held || PinRemoved || session.phase!=SessionPhase.Assessing)return;
            PinRemoved=true;if(pin!=null)pin.SetActive(false);session.report.pinPulled=true;
            session.Record("PASS: pull","Pin removed","observed");holder.Haptic(0.3f,0.08f);
        }
        public void Tick(bool squeeze,Ray aim,float dt)
        {
            if(!Held || session.phase!=SessionPhase.Assessing)return;
            var report=session.report;
            var fire=session.incident;
            Vector3 source=holder.IsVR?nozzle.position:aim.origin;
            Vector3 target=fire.transform.position+Vector3.up*0.18f;
            float distance=Vector3.Distance(source,target);
            float along=Vector3.Dot(target-source,aim.direction);
            float miss=Vector3.Distance(source+aim.direction*Mathf.Max(0,along),target);
            bool clear=distance>.45f && !Physics.Raycast(source,aim.direction,out RaycastHit hit,distance-0.45f,~(1<<5),QueryTriggerInteraction.Ignore);
            bool baseAim=fire.Active && along>0 && distance>=1.8f && distance<=4.5f && miss<0.55f && clear;
            baseAimTime=PinRemoved&&baseAim?baseAimTime+dt:0;
            if(baseAimTime>=0.4f && !baseAimEstablished)
            {baseAimEstablished=true;report.aimedAtBase=true;session.Record("PASS: aim","Base aim maintained for 0.4 seconds","observed");}
            if(squeeze && !spraying)
            {
                report.extinguisherAttempted=true;session.Respond();
                if(!PinRemoved)
                {
                    if(!pinAttemptLogged){report.passInOrder=false;session.Unsafe("PASS sequence error","Squeeze attempted before pin removal","pass-pin",420);pinAttemptLogged=true;}
                    return;
                }
                if(remaining<=0)return;
                if(!baseAimEstablished){report.passInOrder=false;session.Unsafe("PASS sequence error","Squeezed before establishing base aim","pass-aim",420);}
                spraying=true;firstAim=aim.direction;sweepMinimum=sweepMaximum=0;
                report.squeezed=true;session.Record("PASS: squeeze",agent.ToString(),"observed");
                if(discharge!=null)discharge.Play();
                if(!session.AlarmRaised)session.Unsafe("Suppression before alarm","Alarm not activated","suppression-before-alarm",420);
            }
            if(!squeeze || remaining<=0){if(spraying){spraying=false;if(discharge!=null)discharge.Stop();}return;}
            if(!spraying)return;
            remaining=Mathf.Max(0,remaining-dt);report.spraySeconds+=dt;
            if(!fire.Active)return;
            bool compatible=AssessmentRules.Compatible(agent,fire.kind);
            bool manageable=fire.PortableSuppressionSuitable;
            if(!manageable)session.Unsafe("Fire too developed for portable suppression","Large or spreading incident requires withdrawal","fire-large",420);
            if(!compatible && !wrongAgentLogged){wrongAgentLogged=true;session.Unsafe("Unsuitable extinguishing agent",agent+" used on "+fire.kind,"agent-"+agent,420);}
            if(distance<1.8f)session.Unsafe("Extinguisher too close","Within simplified minimum training distance of 1.8 m","distance",15);
            bool escape=session.HasSafeEgress;
            if(!escape)session.Unsafe("Suppression with no clear escape route","Main staircase or entrance unavailable","no-escape",420);
            if(baseAim)
            {
                float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(firstAim,Vector3.up),Vector3.ProjectOnPlane(aim.direction,Vector3.up),Vector3.up);
                sweepMinimum=Mathf.Min(sweepMinimum,angle);sweepMaximum=Mathf.Max(sweepMaximum,angle);
                if(sweepMaximum-sweepMinimum>=8 && !report.swept){report.swept=true;session.Record("PASS: sweep","At least 8 degrees of lateral nozzle movement while aimed at base","observed");}
                if(compatible)report.correctAgent=true;
                if(compatible && manageable && escape && session.AlarmRaised && report.swept)
                {report.effectiveSpraySeconds+=dt;fire.Suppress(dt);}
            }
        }
    }
}
