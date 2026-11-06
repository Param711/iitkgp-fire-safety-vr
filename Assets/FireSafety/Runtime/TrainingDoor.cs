using UnityEngine;

namespace FireSafety
{
    public class TrainingDoor : MonoBehaviour
    {
        public Transform leaf;
        public TrainingSession session;
        public string roomName;
        public bool refugeDoor;
        public float openAngle=95;
        public bool IsOpen {get;private set;}
        public void ResetDoor(){IsOpen=false;if(leaf!=null)leaf.localRotation=Quaternion.identity;}
        public void Toggle()
        {
            IsOpen=!IsOpen;
            if(session!=null)session.Respond();
            if(session!=null)session.Record(IsOpen?"Door opened":"Door closed",roomName,"observation");
        }
        public void SetForNPC(bool open){IsOpen=open;}
        public void OpenForNPC(){if(!refugeDoor)IsOpen=true;}
        void Update()
        {
            if(leaf==null)return;
            leaf.localRotation=Quaternion.RotateTowards(leaf.localRotation,Quaternion.Euler(0,IsOpen?openAngle:0,0),Time.deltaTime*150);
        }
    }
}
