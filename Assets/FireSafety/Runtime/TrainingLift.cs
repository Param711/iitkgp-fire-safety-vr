using UnityEngine;

namespace FireSafety
{
    public class TrainingLift : MonoBehaviour
    {
        public TrainingSession session;
        public Transform cabin;
        public Transform leftCabinDoor,rightCabinDoor;
        public Transform[] leftLandingDoors,rightLandingDoors;
        public int floor=2;
        public int destination=2;
        public bool emergency;
        public bool moving;
        public float doorProgress=1;
        float wait=8;
        bool wantsOpen=true;
        Vector3 leftHome,rightHome;
        Vector3[] leftHomes,rightHomes;
        bool initialized;
        bool carryingPlayer;
        void Start(){Initialize();}
        void Initialize()
        {
            if(initialized)return;initialized=true;
            leftHome=leftCabinDoor.localPosition;rightHome=rightCabinDoor.localPosition;
            leftHomes=new Vector3[3];rightHomes=new Vector3[3];for(int f=0;f<3;f++){leftHomes[f]=leftLandingDoors[f].localPosition;rightHomes[f]=rightLandingDoors[f].localPosition;}
        }
        public void ResetLift()
        {
            Initialize();floor=destination=2;moving=false;emergency=false;doorProgress=1;wantsOpen=true;wait=8;carryingPlayer=false;
            Vector3 p=cabin.position;p.y=7;cabin.position=p;ApplyDoors();
        }
        public void SetEmergency(bool value)
        {
            emergency=value;
            if(value){if(!moving)wantsOpen=true;destination=floor;}
        }
        public void RequestFloor(int requested,bool byParticipant)
        {
            if(session.phase==SessionPhase.Assessing && byParticipant)
            {session.Respond();session.Unsafe("Elevator attempted during fire","Requested floor "+requested,"lift",8);}
            // The simulation never transports occupants during a fire, even before the alarm is raised.
            if(session.phase==SessionPhase.Assessing || emergency)return;
            requested=Mathf.Clamp(requested,0,2);
            if(moving)return;
            destination=requested;if(destination!=floor)wantsOpen=false;else{wantsOpen=true;wait=5;}
        }
        public void ToggleDoors()
        {
            if(moving)return;
            if(session.phase==SessionPhase.Assessing){session.Unsafe("Elevator door attempted during fire","Lift unavailable","lift",8);return;}
            wantsOpen=!wantsOpen;wait=5;
        }
        void Update()
        {
            Initialize();
            var p=session.player;
            Vector3 relative=p.Feet-cabin.position;
            bool inside=Mathf.Abs(relative.x)<0.95f&&Mathf.Abs(relative.z)<0.95f&&relative.y>-0.2f&&relative.y<1.8f;
            carryingPlayer=inside;p.OnLift=inside;
            if(session.phase==SessionPhase.Assessing)
            {
                if(inside)session.Unsafe("Entered elevator during fire","Lift remains unavailable","lift-entered",15);
                if(!moving){wantsOpen=true;destination=floor;}
            }
            if(moving)
            {
                Vector3 old=cabin.position;Vector3 next=old;next.y=Mathf.MoveTowards(old.y,destination*3.5f,Time.deltaTime*1.5f);cabin.position=next;
                if(carryingPlayer)p.Body.Move(next-old);
                if(Mathf.Abs(next.y-destination*3.5f)<0.001f){floor=destination;moving=false;wantsOpen=true;wait=5;}
            }
            else
            {
                if(wantsOpen){doorProgress=Mathf.MoveTowards(doorProgress,1,Time.deltaTime);wait-=Time.deltaTime;if(wait<=0&&!inside&&session.phase!=SessionPhase.Assessing)wantsOpen=false;}
                else{doorProgress=Mathf.MoveTowards(doorProgress,0,Time.deltaTime);if(doorProgress<=0&&destination!=floor)moving=true;}
            }
            ApplyDoors();
        }
        void ApplyDoors()
        {
            float gap=moving?0:doorProgress*0.8f;
            leftCabinDoor.localPosition=leftHome+Vector3.back*gap;rightCabinDoor.localPosition=rightHome+Vector3.forward*gap;
            for(int i=0;i<3;i++){leftLandingDoors[i].localPosition=leftHomes[i]+Vector3.back*(i==floor?gap:0);rightLandingDoors[i].localPosition=rightHomes[i]+Vector3.forward*(i==floor?gap:0);}
        }
    }
}
