using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.Missions;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>The mechanical parking machine really carries the car: lowering + shifting, controls locked, abort on movement.</summary>
    public class MechanicalParkingTests
    {
        private GameObject ground, machine, car;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject g in new[] { car, machine, ground }) if (g != null) Object.Destroy(g);
            yield return null;
            Time.timeScale = 1f;
        }

        private VehicleController Setup(out MechanicalParkingSystem system)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            machine = new GameObject("Machine");
            var deck = new GameObject("Platform").transform;
            deck.SetParent(machine.transform, false);
            var gate = new GameObject("Gate").transform;
            gate.SetParent(machine.transform, false);
            system = machine.AddComponent<MechanicalParkingSystem>();
            system.Configure(deck, gate, new Vector2(2.6f, 5.6f), 2.4f, new Vector3(0f, 0f, 3.2f));
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Vehicles/Car_RealSedan.prefab");
#else
            GameObject prefab = null;
#endif
            car = Object.Instantiate(prefab, new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            var vc = car.GetComponent<VehicleController>();
            car.GetComponent<VehicleInput>().KeyboardEnabled = false;
            return vc;
        }

        [UnityTest]
        public IEnumerator Sequence_Carries_The_Car_Down_And_Into_The_Rack_With_Controls_Locked()
        {
            Time.timeScale = 3f;
            VehicleController vc = Setup(out MechanicalParkingSystem system);
            yield return new WaitForSeconds(1f);                       // settle on the ground
            Assert.IsTrue(system.CarOnPlatform(vc.transform));
            Vector3 before = vc.transform.position;
            bool? result = null;
            var seen = new System.Collections.Generic.List<MechanicalParkingSystem.Step>();
            system.StepChanged += s => seen.Add(s);
            system.StartCoroutine(system.Run(vc, ok => result = ok));
            float t = 0f;
            while (result == null && t < 20f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(true, result, "sequence completes");
            Assert.AreEqual(MechanicalParkingSystem.Step.Locked, system.Current);
            CollectionAssert.AreEqual(new[] { MechanicalParkingSystem.Step.SafetyCheck, MechanicalParkingSystem.Step.GateClosing,
                MechanicalParkingSystem.Step.Lowering, MechanicalParkingSystem.Step.Shifting, MechanicalParkingSystem.Step.Locked }, seen);
            Vector3 moved = vc.transform.position - before;
            Debug.Log($"[Test] Mechanical parking moved the car by {moved} in {t:F1}s (scaled x3)");
            Assert.AreEqual(-2.4f, moved.y, 0.15f, "lowered 2.4 m");
            Assert.AreEqual(3.2f, moved.z, 0.15f, "shifted 3.2 m into the slot");
            Assert.IsFalse(vc.ControlsEnabled, "controls stay locked while parked in the machine");
            Assert.IsTrue(vc.Body.isKinematic, "body carried by the platform");
        }

        [UnityTest]
        public IEnumerator Driving_Off_During_The_Safety_Check_Aborts()
        {
            VehicleController vc = Setup(out MechanicalParkingSystem system);
            yield return new WaitForSeconds(0.6f);
            bool? result = null;
            system.StartCoroutine(system.Run(vc, ok => result = ok));
            yield return new WaitForSeconds(0.3f);
            vc.ResetVehicle(new Vector3(8f, 0.3f, 0f), Quaternion.identity);   // leaves the deck
            float t = 0f;
            while (result == null && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(false, result, "aborted");
            Assert.AreEqual(MechanicalParkingSystem.Step.Aborted, system.Current);
            Assert.IsTrue(vc.ControlsEnabled, "controls untouched by an aborted check");
            Assert.IsFalse(vc.Body.isKinematic);
        }
    }
}
