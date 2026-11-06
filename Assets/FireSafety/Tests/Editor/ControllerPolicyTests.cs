using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class ControllerPolicyTests
    {
        [Test] public void StickDriftDoesNotMovePlayer()
        {
            Assert.AreEqual(Vector2.zero,TrainingControllerInput.DeadZone(new Vector2(.12f,.05f)));
            Assert.AreEqual(Vector2.zero,TrainingControllerInput.DeadZone(new Vector2(.18f,0)));
            Assert.AreEqual(1,TrainingControllerInput.DeadZone(new Vector2(1,1)).magnitude,.001f);
            Assert.Less(TrainingControllerInput.DeadZone(new Vector2(.2f,0)).magnitude,.03f);
        }
        [Test] public void AnalogButtonHysteresisAvoidsRepeatedActions()
        {
            Assert.IsFalse(TrainingControllerInput.Pressed(.5f,false));
            Assert.IsTrue(TrainingControllerInput.Pressed(.7f,false));
            Assert.IsTrue(TrainingControllerInput.Pressed(.5f,true));
            Assert.IsFalse(TrainingControllerInput.Pressed(.3f,true));
        }
    }
}
