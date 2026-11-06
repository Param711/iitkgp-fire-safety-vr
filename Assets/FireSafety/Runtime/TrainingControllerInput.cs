using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using LegacyDevice = UnityEngine.XR.InputDevice;
using XRUsage = UnityEngine.XR.CommonUsages;

namespace FireSafety
{
    // The game has one interaction path for both the Oculus provider and XRI's simulator.
    // Prefer a tracked hardware device; do not mix poses from one backend with buttons from another.
    public struct TrainingControllerState
    {
        public bool tracked, trigger, grip, menu;
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 stick;
    }

    public static class TrainingControllerInput
    {
        public static bool SimulatedHeadActive
        {
            get
            {
                foreach(var device in InputSystem.devices)
                    if(device is XRHMD hmd && device.layout.IndexOf("Simulated",System.StringComparison.OrdinalIgnoreCase)>=0 && hmd.isTracked.isPressed)return true;
                return false;
            }
        }
        public static bool ReadHead(out Vector3 position,out Quaternion rotation,out bool presenceAvailable,out bool worn)
        {
            var legacy=InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if(!legacy.isValid)legacy=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            presenceAvailable=legacy.TryGetFeatureValue(XRUsage.userPresence,out worn);
            if(IsTracked(legacy) && legacy.TryGetFeatureValue(XRUsage.devicePosition,out position) && legacy.TryGetFeatureValue(XRUsage.deviceRotation,out rotation))return true;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRHMD hmd)||!hmd.isTracked.isPressed||(XRSettings.isDeviceActive&&IsSimulated(device)))continue;
                position=hmd.centerEyePosition.ReadValue();rotation=hmd.centerEyeRotation.ReadValue();
                // Some providers expose only devicePose, leaving the centre-eye quaternion unset.
                if(Quaternion.Dot(rotation,rotation)<.5f){position=hmd.devicePosition.ReadValue();rotation=hmd.deviceRotation.ReadValue();}
                if(!legacy.isValid){presenceAvailable=false;worn=true;}return true;
            }
            position=Vector3.zero;rotation=Quaternion.identity;return false;
        }
        public static TrainingControllerState Read(bool left,TrainingControllerState previous)
        {
            var legacy=InputDevices.GetDeviceAtXRNode(left?XRNode.LeftHand:XRNode.RightHand);
            var result=new TrainingControllerState {rotation=Quaternion.identity};
            if(IsTracked(legacy))
            {
                result.tracked=legacy.TryGetFeatureValue(XRUsage.devicePosition,out result.position)&&legacy.TryGetFeatureValue(XRUsage.deviceRotation,out result.rotation);
                legacy.TryGetFeatureValue(XRUsage.primary2DAxis,out result.stick);
                legacy.TryGetFeatureValue(XRUsage.trigger,out float trigger);legacy.TryGetFeatureValue(XRUsage.grip,out float grip);
                result.trigger=Pressed(trigger,previous.trigger)||Button(legacy,XRUsage.triggerButton);
                result.grip=Pressed(grip,previous.grip)||Button(legacy,XRUsage.gripButton);
                result.menu=Button(legacy,XRUsage.secondaryButton)||Button(legacy,XRUsage.menuButton);
                return result;
            }
            var device=left?XRController.leftHand:XRController.rightHand;
            if(device!=null&&XRSettings.isDeviceActive&&IsSimulated(device))device=null;
            if(device==null||!device.isTracked.isPressed)return result;
            result.tracked=true;result.position=device.devicePosition.ReadValue();result.rotation=device.deviceRotation.ReadValue();
            result.stick=(device.TryGetChildControl<Vector2Control>("primary2DAxis")??device.TryGetChildControl<Vector2Control>("thumbstick"))?.ReadValue()??Vector2.zero;
            result.trigger=Pressed(Axis(device,"trigger"),previous.trigger)||Axis(device,"triggerPressed")>.5f;
            result.grip=Pressed(Axis(device,"grip"),previous.grip)||Axis(device,"gripPressed")>.5f;
            result.menu=Axis(device,"secondaryButton")>.5f||Axis(device,"menu")>.5f||Axis(device,"menuButton")>.5f;
            return result;
        }
        public static bool Pressed(float value,bool previous){return value >= (previous ? .35f : .65f);}
        public static Vector2 DeadZone(Vector2 value,float threshold=.18f)
        {
            float magnitude=Mathf.Clamp01(value.magnitude);
            return magnitude<=threshold?Vector2.zero:value.normalized*((magnitude-threshold)/(1-threshold));
        }
        public static void Haptic(float amplitude,float duration)
        {
            var legacy=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if(legacy.isValid && legacy.SendHapticImpulse(0,amplitude,duration))return;
            if(XRController.rightHand is XRControllerWithRumble rumble)rumble.SendImpulse(amplitude,duration);
        }
        static float Axis(UnityEngine.InputSystem.InputDevice device,string name){return device.TryGetChildControl<AxisControl>(name)?.ReadValue()??0;}
        static bool IsSimulated(UnityEngine.InputSystem.InputDevice device){return device.layout.IndexOf("Simulated",System.StringComparison.OrdinalIgnoreCase)>=0;}
        static bool Button(LegacyDevice device,InputFeatureUsage<bool> usage){return device.TryGetFeatureValue(usage,out bool value)&&value;}
        static bool IsTracked(LegacyDevice device){return device.isValid&&(!device.TryGetFeatureValue(XRUsage.isTracked,out bool value)||value);}
    }
}
