using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Tests
{
    /// <summary>Keyboard mapping of VehicleInput, using a virtual keyboard from the Input System test fixture.</summary>
    public class VehicleKeyboardInputTests : InputTestFixture
    {
        private Keyboard keyboard;
        private VehicleInput input;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            input = new GameObject("InputUnderTest").AddComponent<VehicleInput>();
        }

        public override void TearDown()
        {
            if (input != null) Object.Destroy(input.gameObject);
            base.TearDown();
        }

        private void PressNow(UnityEngine.InputSystem.Controls.ButtonControl key)
        {
            Press(key);
            InputSystem.Update();
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator W_Ramps_Throttle_Up()
        {
            Press(keyboard.wKey);
            yield return null;
            float early = input.Throttle;
            float t = 0f;
            while (t < 0.6f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.Less(early, 1f, "keyboard throttle ramps rather than snapping");
            Assert.AreEqual(1f, input.Throttle, 1e-4f);
            Release(keyboard.wKey);
            yield return Frames(2);
            Assert.AreEqual(0f, input.Throttle, 1e-4f, "release drops throttle at once");
        }

        [UnityTest]
        public IEnumerator Keys_Map_To_Controls()
        {
            // Flush between presses: back-to-back Press calls queue whole-device states that overwrite each other.
            PressNow(keyboard.sKey);
            PressNow(keyboard.aKey);
            PressNow(keyboard.spaceKey);
            PressNow(keyboard.xKey);
            float t = 0f;
            while (t < 0.6f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(-1f, input.Throttle, 1e-4f, "S = reverse/brake");
            Assert.AreEqual(-1f, input.Steering, 1e-4f, "A = steer left");
            Assert.IsTrue(input.Handbrake, "Space = handbrake");
            Assert.AreEqual(1f, input.Brake, 1e-4f, "X = foot brake");
            Release(keyboard.aKey);
            InputSystem.Update();
            PressNow(keyboard.rightArrowKey);
            yield return Frames(2);
            Assert.AreEqual(1f, input.Steering, 1e-4f, "Right arrow = steer right");
        }

        [UnityTest]
        public IEnumerator Mobile_And_Keyboard_Combine_And_Clamp()
        {
            input.SetSteering(0.5f);
            Press(keyboard.dKey);
            yield return Frames(2);
            Assert.AreEqual(1f, input.Steering, 1e-4f);
            input.ResetMobileInput();
            Release(keyboard.dKey);
            yield return Frames(2);
            Assert.AreEqual(0f, input.Steering, 1e-4f);
            input.KeyboardEnabled = false;
            Press(keyboard.wKey);
            input.SetThrottle(-0.4f);
            yield return Frames(2);
            Assert.AreEqual(-0.4f, input.Throttle, 1e-4f, "keyboard ignored when disabled; mobile value used");
        }
    }
}
