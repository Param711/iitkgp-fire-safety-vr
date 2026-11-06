using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireSafety.Tests
{
    public class ControllerPlayTests
    {
        TrainingSession session;
        XRHMD head;
        XRController left,right;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        [UnitySetUp] public IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("FireSafetyTraining");
            session=Object.FindObjectOfType<TrainingSession>();session.persistLatestReport=false;
            oldBackground=InputSystem.settings.backgroundBehavior;
            oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.RegisterLayout("{\"name\":\"TrainingSimulatedHead\",\"extend\":\"XRHMD\"}");
            InputSystem.RegisterLayout("{\"name\":\"TrainingTestController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"trigger\",\"layout\":\"Axis\"},{\"name\":\"grip\",\"layout\":\"Axis\"},{\"name\":\"primary2DAxis\",\"layout\":\"Vector2\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\"}]}");
            head=(XRHMD)InputSystem.AddDevice("TrainingSimulatedHead");
            left=(XRController)InputSystem.AddDevice("TrainingTestController");right=(XRController)InputSystem.AddDevice("TrainingTestController");
            InputSystem.SetDeviceUsage(left,UnityEngine.InputSystem.CommonUsages.LeftHand);InputSystem.SetDeviceUsage(right,UnityEngine.InputSystem.CommonUsages.RightHand);
            Tracked(head.isTracked,true);Set(head.centerEyePosition,new Vector3(0,1.65f,0));Set(head.centerEyeRotation,Quaternion.identity);
            Pose(left,new Vector3(-.25f,1.2f,.3f),Quaternion.identity);Pose(right,new Vector3(.25f,1.2f,.3f),Quaternion.identity);
            yield return null;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(session!=null){session.player.enabled=false;if(session.phase==SessionPhase.Assessing)session.Finish("Controller test cleanup");}
            if(head!=null)InputSystem.RemoveDevice(head);if(left!=null)InputSystem.RemoveDevice(left);if(right!=null)InputSystem.RemoveDevice(right);
            InputSystem.settings.backgroundBehavior=oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;
            yield return null;
        }
        static void Pose(XRController controller,Vector3 p,Quaternion r)
        {Tracked(controller.isTracked,true);Set(controller.devicePosition,p);Set(controller.deviceRotation,r);}
        static void Tracked(ButtonControl control,bool tracked)
        {
            Set(control,tracked?1f:0f);
        }
        static void Set<T>(InputControl<T> control,T value) where T:struct
        {
            // UnityTest coroutines queue deltas for the next player input update. Full
            // snapshots can overwrite another control's pending state with an old buffer.
            using(DeltaStateEvent.From(control,out var state))
            {control.WriteValueIntoEvent(value,state);InputSystem.QueueEvent(state);}
        }
        static void Axis(XRController controller,string name,float value){Set(controller.GetChildControl<AxisControl>(name),value);}
        [UnityTest] public IEnumerator ControllerRaySelectsTheoryAndPanelPreventsWalking()
        {
            Assert.IsTrue(session.player.IsVR,"Head tracked="+head.isTracked.isPressed+"; layout="+head.layout+"; simulated="+TrainingControllerInput.SimulatedHeadActive);Assert.IsTrue(session.player.ControllersReady);
            Vector3 initial=session.player.transform.position;
            Set(left.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.up);
            yield return null;yield return null;
            Assert.Less(Vector3.Distance(initial,session.player.transform.position),.03f);
            Set(left.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.zero);yield return null;
            var button=session.presentation.buttons[0];var space=session.player.trackingSpace;
            Vector3 origin=session.player.Head.position+session.player.Head.forward*.25f;
            Pose(right,space.InverseTransformPoint(origin),Quaternion.Inverse(space.rotation)*Quaternion.LookRotation(button.transform.position-origin));
            Axis(right,"trigger",1);yield return null;
            StringAssert.Contains("THEORY",session.presentation.heading.text);
            Axis(right,"trigger",0);
        }
        [UnityTest] public IEnumerator LostControllerPausesClockAndRecoveryRequiresNeutralSticks()
        {
            session.StartAssessment(0);yield return null;yield return null;
            Assert.AreEqual(SessionPhase.Assessing,session.phase);
            Tracked(right.isTracked,false);yield return null;
            float stopped=session.Elapsed;Vector3 position=session.player.transform.position;
            yield return new WaitForSeconds(.15f);
            Assert.IsTrue(session.player.TrackingUnavailable);Assert.AreEqual(stopped,session.Elapsed,.001f);
            Set(left.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.up);
            Tracked(right.isTracked,true);yield return null;yield return null;
            Assert.IsFalse(session.player.TrackingUnavailable);
            Assert.Less(Vector3.Distance(position,session.player.transform.position),.03f,"Held stick must not move the player on reconnect.");
            Set(left.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.zero);yield return null;
            Set(left.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.up);yield return null;yield return null;
            Assert.Greater(Vector3.Distance(position,session.player.transform.position),.001f);
        }
        [UnityTest] public IEnumerator ControllerGripPicksEquipmentAndSecondPressReturnsIt()
        {
            session.StartAssessment(0);yield return null;
            var tool=session.extinguishers[2];var space=session.player.trackingSpace;
            Vector3 origin=tool.transform.position+Vector3.back*.7f;
            Pose(right,space.InverseTransformPoint(origin),Quaternion.Inverse(space.rotation)*Quaternion.LookRotation(tool.transform.position-origin));
            Axis(right,"grip",1);yield return null;
            Assert.AreSame(tool,session.player.HeldTool);Assert.IsTrue(tool.Held);
            Axis(right,"grip",0);yield return null;Axis(left,"trigger",1);yield return null;
            Assert.IsTrue(tool.PinRemoved);Axis(left,"trigger",0);
            Axis(right,"grip",1);yield return null;
            Assert.IsNull(session.player.HeldTool);Assert.IsFalse(tool.Held);
        }
        [UnityTest] public IEnumerator NearbyEquipmentCannotBeGrabbedThroughAWall()
        {
            session.player.enabled=false;
            Vector3 point=new Vector3(50,2,50);session.player.rightHand.position=point;
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.position=point+Vector3.forward*.16f;item.transform.localScale=Vector3.one*.08f;
            var action=item.AddComponent<TrainingInteractable>();action.tool=item.AddComponent<TrainingExtinguisher>();
            Physics.SyncTransforms();Assert.AreSame(action,session.player.FindNearbyEquipment());
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=point+Vector3.forward*.07f;wall.transform.localScale=new Vector3(1,1,.02f);
            Physics.SyncTransforms();Assert.IsNull(session.player.FindNearbyEquipment());
            Object.Destroy(item);Object.Destroy(wall);yield return null;
        }
    }
}
