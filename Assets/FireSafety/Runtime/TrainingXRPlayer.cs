using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace FireSafety
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    public class TrainingXRPlayer : MonoBehaviour
    {
        public Transform trackingSpace;
        public Transform Head;
        public Transform leftHand;
        public Transform rightHand;
        public LineRenderer pointer;
        public TrainingSession session;
        public float walkSpeed=2.0f;
        public float snapTurnAngle=30;
        public GameObject leftControllerModel,rightControllerModel;
        public float interactionReach=3.5f;
        public float nearGrabRadius=.2f;
        public bool IsVR {get;private set;}
        public bool TrackingUnavailable {get;private set;}
        public HeadsetState TrackingState {get;private set;}
        public bool LeftControllerTracked {get;private set;}
        public bool RightControllerTracked {get;private set;}
        public bool ControllersReady {get{return LeftControllerTracked&&RightControllerTracked;}}
        public GameObject trackingNotice;
        public TextMesh trackingNoticeText;
        public string ReadinessSummary
        {
            get
            {
                if(TrackingState==HeadsetState.Desktop)return "Desktop mode | No active VR headset";
                if(TrackingState==HeadsetState.Disconnected)return "VR headset disconnected";
                if(TrackingState==HeadsetState.NotWorn)return "Put on the headset to continue";
                if(TrackingState==HeadsetState.TrackingLost)return "Headset tracking unavailable";
                if(!LeftControllerTracked&&!RightControllerTracked)return "Connect both VR controllers";
                if(!LeftControllerTracked)return "Left controller tracking unavailable";
                if(!RightControllerTracked)return "Right controller tracking unavailable";
                if(!applicationFocused)return "Return to the game window to continue";
                if(TrainingControllerInput.SimulatedHeadActive)return "XR simulator | Controller practice only";
                return "VR ready | Headset and both controllers tracked";
            }
        }
        public TrainingExtinguisher HeldTool {get;private set;}
        public TrainingBucket HeldBucket {get;private set;}
        public CharacterController Body {get;private set;}
        public bool OnLift;
        public Vector3 Feet { get {var p=Head.position;p.y=transform.position.y;return p;} }
        TrainingControllerState leftInput,rightInput;
        bool triggerWasDown,gripWasDown,pinWasDown,menuWasDown;
        bool turnLatched;
        bool vrSeen;
        bool floorOrigin;
        bool originSet;
        float verticalSpeed;
        float pitch;
        float trackingRetry;
        bool lastTrackingLost;
        bool applicationFocused=true;
        Vector3 recenterOffset;
        bool requireNeutralInput;
        bool suppressUntilTriggerReleased;
        bool controllerVRSeen;
        TrainingInteractable hovered;
        TrainingInteractable previousHover;
        readonly Collider[] nearby=new Collider[24];
        readonly List<XRInputSubsystem> subsystems=new List<XRInputSubsystem>();

        void Awake()
        {
            Body=GetComponent<CharacterController>();
            // Sample rigs contain their own camera, listener, locomotion and rays. One game rig
            // owns these so an accidentally added sample cannot move/render the player twice.
            foreach(var rig in FindObjectsOfType<XROrigin>())
                if(rig.transform!=transform && !transform.IsChildOf(rig.transform))
                {rig.gameObject.SetActive(false);Debug.Log("Fire Safety: disabled duplicate sample rig "+rig.name+"; the game controller rig owns tracking and interactions.");}
        }
        void OnEnable(){Application.onBeforeRender+=Track;}
        void OnDisable(){Application.onBeforeRender-=Track;Cursor.lockState=CursorLockMode.None;}
        void OnApplicationFocus(bool focused){applicationFocused=focused;}
        void Track()
        {
            if(!IsVR)return;
            bool tracked=TrainingControllerInput.ReadHead(out Vector3 hp,out Quaternion hr,out bool presenceAvailable,out bool worn);
            TrackingState=VRTrackingPolicy.Evaluate(IsVR,vrSeen,tracked,tracked,presenceAvailable,worn);
            leftInput=TrainingControllerInput.Read(true,leftInput);rightInput=TrainingControllerInput.Read(false,rightInput);
            LeftControllerTracked=leftInput.tracked;RightControllerTracked=rightInput.tracked;
            TrackingUnavailable=VRTrackingPolicy.Paused(TrackingState)||!applicationFocused||(!ControllersReady&&session.phase==SessionPhase.Assessing);
            if(TrackingUnavailable)return;
            Head.localPosition=hp;Head.localRotation=hr;
            TrackHand(leftInput,leftHand);TrackHand(rightInput,rightHand);
        }
        void TrackHand(TrainingControllerState device,Transform hand)
        {
            if(!device.tracked)return;
            hand.localPosition=device.position;hand.localRotation=device.rotation;
        }
        void Update()
        {
            bool wasVR=IsVR;
            IsVR=XRSettings.isDeviceActive||TrainingControllerInput.SimulatedHeadActive;
            if(wasVR!=IsVR){Cursor.lockState=CursorLockMode.None;session.ConfigureRendering(IsVR);}
            if(IsVR){vrSeen=true;if(!controllerVRSeen){requireNeutralInput=true;controllerVRSeen=true;}}
            TrackingState=VRTrackingPolicy.Evaluate(IsVR,vrSeen,false,false,false,false);
            TrackingUnavailable=VRTrackingPolicy.Paused(TrackingState);
            if(IsVR && (!originSet || (trackingRetry-=Time.deltaTime)<=0))
            {
                subsystems.Clear();SubsystemManager.GetInstances(subsystems);
                foreach(var s in subsystems)if(s.running){s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);floorOrigin=s.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor;originSet=true;}
                if(TrainingControllerInput.SimulatedHeadActive&&!XRSettings.isDeviceActive){floorOrigin=true;originSet=true;}
                trackingSpace.localPosition=recenterOffset+Vector3.up*(floorOrigin?0:1.65f);trackingRetry=2;
            }
            if(!IsVR && !vrSeen){trackingSpace.localPosition=Vector3.zero;Head.localPosition=Vector3.up*1.65f;}
            Track();
            if(leftControllerModel!=null)leftControllerModel.SetActive(!IsVR||LeftControllerTracked);
            if(rightControllerModel!=null)rightControllerModel.SetActive(!IsVR||RightControllerTracked);
            if(trackingNotice!=null)
            {
                trackingNotice.SetActive(TrackingUnavailable);
                if(trackingNoticeText!=null)trackingNoticeText.text=(session.phase==SessionPhase.Assessing?"ASSESSMENT PAUSED\n":"VR CONNECTION\n")+ReadinessSummary;
            }
            if(TrackingUnavailable!=lastTrackingLost){session.Record(TrackingUnavailable?"Tracking interrupted":"Tracking restored",ReadinessSummary+"; clock suspended while tracking is unavailable","observation");lastTrackingLost=TrackingUnavailable;}
            if(TrackingUnavailable)
            {
                if(pointer!=null)pointer.enabled=false;
                if(HeldTool!=null)HeldTool.Tick(false,new Ray(rightHand.position,rightHand.forward),0);
                if(HeldBucket!=null)HeldBucket.Tick(false,new Ray(rightHand.position,rightHand.forward),0);
                triggerWasDown=gripWasDown=pinWasDown=menuWasDown=false;
                requireNeutralInput=true;
                return;
            }
            if(Input.GetKeyDown(KeyCode.Escape)){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            bool allowMove=!session.presentation.Visible && applicationFocused;
            if(IsVR)
            {
                if(allowMove&&!requireNeutralInput)MoveVR();
                Body.height=Mathf.Clamp(Head.localPosition.y+trackingSpace.localPosition.y,1.0f,2.1f);
                Vector3 localHead=transform.InverseTransformPoint(Head.position);
                Body.center=new Vector3(localHead.x,Body.height*0.5f,localHead.z);
            }
            else
            {
                if(allowMove&&Input.GetMouseButtonDown(1)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
                if(allowMove && (Cursor.lockState==CursorLockMode.Locked || Input.GetMouseButton(1)))
                {
                    transform.Rotate(0,Input.GetAxis("Mouse X")*2,0);
                    pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-80,80);Head.localRotation=Quaternion.Euler(pitch,0,0);
                }
                if(allowMove)
                {
                    Vector3 input=Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical")),1);
                    Body.Move(transform.TransformDirection(input)*walkSpeed*Time.deltaTime);
                }
                leftHand.position=Head.TransformPoint(new Vector3(-0.25f,-0.35f,0.4f));leftHand.rotation=Head.rotation;
                rightHand.position=Head.TransformPoint(new Vector3(0.2f,-0.18f,0.4f));rightHand.rotation=Head.rotation;
            }
            float movementDelta=Mathf.Min(Time.deltaTime,.05f);
            if(!OnLift){verticalSpeed=Body.isGrounded?-1.5f:verticalSpeed-9.81f*movementDelta;Body.Move(Vector3.up*verticalSpeed*movementDelta);}
            else verticalSpeed=0;
            bool trigger=applicationFocused && (IsVR?RightControllerTracked&&rightInput.trigger:Input.GetMouseButton(0));
            bool grip=applicationFocused && (IsVR?RightControllerTracked&&rightInput.grip:Input.GetKey(KeyCode.G));
            bool pin=applicationFocused && (IsVR?LeftControllerTracked&&leftInput.trigger:Input.GetKey(KeyCode.P));
            bool menu=applicationFocused && (IsVR?LeftControllerTracked&&leftInput.menu:Input.GetKey(KeyCode.Tab));
            if(IsVR && !RightControllerTracked)requireNeutralInput=true;
            if(requireNeutralInput)
            {
                if(applicationFocused && (!IsVR||ControllersReady) && !trigger&&!grip&&!pin&&!menu && (!IsVR||(leftInput.stick.magnitude<.18f&&rightInput.stick.magnitude<.25f)))requireNeutralInput=false;
                trigger=grip=pin=menu=false;
            }
            Ray ray=IsVR?new Ray(rightHand.position,rightHand.forward):new Ray(Head.position,Head.forward);
            if(!IsVR && session.presentation.Visible && Cursor.lockState!=CursorLockMode.Locked)
                ray=Head.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            hovered=null;Vector3 end=ray.origin+ray.direction*interactionReach;
            int mask=session.presentation.Visible?1<<5:~((1<<5)|(1<<9)|(1<<2));
            if(pointer!=null)pointer.gameObject.layer=session.presentation.Visible?5:0;
            if(Physics.Raycast(ray,out RaycastHit hit,interactionReach,mask,QueryTriggerInteraction.Ignore))
            {end=hit.point;hovered=hit.collider.GetComponentInParent<TrainingInteractable>();}
            if(IsVR&&!session.presentation.Visible&&HeldTool==null&&HeldBucket==null)
            {
                var close=FindNearbyEquipment();if(close!=null){hovered=close;end=close.transform.position;}
            }
            if(IsVR&&hovered!=null&&hovered!=previousHover)Haptic(.07f,.025f);
            previousHover=hovered;
            if(pointer!=null){pointer.enabled=(!IsVR||RightControllerTracked)&&(HeldTool==null&&HeldBucket==null || session.presentation.Visible);pointer.SetPosition(0,ray.origin);pointer.SetPosition(1,end);pointer.startColor=pointer.endColor=hovered!=null?new Color(0.2f,1,0.8f):new Color(0.8f,0.9f,1,0.45f);pointer.endWidth=hovered!=null?.006f:.002f;}
            bool panelWasVisible=session.presentation.Visible;
            if(menu && !menuWasDown){if(session.phase==SessionPhase.Assessing)session.presentation.TogglePausePanel();else session.presentation.ShowMenu();}
            if(grip && !gripWasDown && !session.presentation.Visible)
            {
                if(HeldTool!=null||HeldBucket!=null)DropTool();
                else if(hovered!=null&&hovered.bucket!=null)GrabBucket(hovered.bucket);
                else if(hovered!=null&&hovered.tool!=null)Grab(hovered.tool);
            }
            if(pin && !pinWasDown && HeldTool!=null && !session.presentation.Visible)HeldTool.PullPin();
            if(trigger && !triggerWasDown)
                if(hovered!=null){hovered.Activate(this);if(IsVR)Haptic(.12f,.04f);}
            if(!IsVR && Input.GetKeyDown(KeyCode.E) && hovered!=null)hovered.Activate(this);
            if(panelWasVisible&&!session.presentation.Visible)suppressUntilTriggerReleased=true;
            if(!trigger)suppressUntilTriggerReleased=false;
            bool useEquipment=trigger&&!suppressUntilTriggerReleased&&!session.presentation.Visible&&hovered==null&&(!IsVR||ControllersReady);
            if(HeldTool!=null)HeldTool.Tick(useEquipment,ray,Time.deltaTime);
            if(HeldBucket!=null)HeldBucket.Tick(useEquipment,ray,Time.deltaTime);
            triggerWasDown=trigger;gripWasDown=grip;pinWasDown=pin;menuWasDown=menu;
        }
        void MoveVR()
        {
            Vector2 move=TrainingControllerInput.DeadZone(leftInput.stick),turn=rightInput.stick;
            if(!LeftControllerTracked)move=Vector2.zero;
            if(!RightControllerTracked)turn=Vector2.zero;
            Vector3 forward=Vector3.ProjectOnPlane(Head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=transform.forward;
            Vector3 rightward=Vector3.Cross(Vector3.up,forward);
            if(move.sqrMagnitude>0)Body.Move((forward*move.y+rightward*move.x)*walkSpeed*Mathf.Min(Time.deltaTime,.05f));
            if(Mathf.Abs(turn.x)>0.7f && !turnLatched){Vector3 pivot=Head.position;transform.RotateAround(pivot,Vector3.up,Mathf.Sign(turn.x)*snapTurnAngle);turnLatched=true;}
            if(Mathf.Abs(turn.x)<0.25f)turnLatched=false;
        }
        public TrainingInteractable FindNearbyEquipment()
        {
            int count=Physics.OverlapSphereNonAlloc(rightHand.position,nearGrabRadius,nearby,~((1<<5)|(1<<9)|(1<<2)),QueryTriggerInteraction.Ignore);
            TrainingInteractable best=null;float nearest=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var item=nearby[i].GetComponentInParent<TrainingInteractable>();
                if(item==null||(item.tool==null&&item.bucket==null))continue;
                Vector3 point=nearby[i].ClosestPoint(rightHand.position),difference=point-rightHand.position;float distance=difference.magnitude;
                if(distance>=nearest)continue;
                if(distance>.01f&&Physics.Raycast(rightHand.position,difference.normalized,out RaycastHit obstacle,distance,~((1<<5)|(1<<9)|(1<<2)),QueryTriggerInteraction.Ignore)&&obstacle.collider.GetComponentInParent<TrainingInteractable>()!=item)continue;
                best=item;nearest=distance;
            }
            return best;
        }
        public void Grab(TrainingExtinguisher tool)
        {
            if(HeldTool!=null || HeldBucket!=null || session.phase!=SessionPhase.Assessing)return;
            HeldTool=tool;tool.PickUp(this);suppressUntilTriggerReleased=true;Haptic(0.3f,0.1f);
        }
        public void GrabBucket(TrainingBucket bucket)
        {
            if(HeldTool!=null||HeldBucket!=null||session.phase!=SessionPhase.Assessing)return;
            HeldBucket=bucket;bucket.PickUp(this);suppressUntilTriggerReleased=true;Haptic(.3f,.1f);
        }
        public void DropTool()
        {
            if(HeldTool!=null){var e=HeldTool;HeldTool=null;e.Release();}
            if(HeldBucket!=null){var b=HeldBucket;HeldBucket=null;b.Release();}
        }
        public void Teleport(Vector3 position,Quaternion rotation)
        {
            if(Body==null)Body=GetComponent<CharacterController>();
            Body.enabled=false;transform.SetPositionAndRotation(position,rotation);Body.center=Vector3.up*0.85f;Body.height=1.7f;Body.enabled=true;verticalSpeed=0;OnLift=false;
            // Keep the physical headset yaw and horizontal offset aligned with the spawn.
            if(IsVR){Vector3 local=Head.localPosition;recenterOffset=new Vector3(-local.x,0,-local.z);trackingSpace.localPosition=recenterOffset+Vector3.up*(floorOrigin?0:1.65f);transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y-Head.localEulerAngles.y,0);requireNeutralInput=true;turnLatched=true;}
        }
        public void Haptic(float amplitude,float duration){if(IsVR)TrainingControllerInput.Haptic(amplitude,duration);}
        public void LevelDesktopView()
        {
            if(IsVR)return;
            pitch=0;Head.localRotation=Quaternion.identity;
        }
    }
}
