using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Every garage car, on a flat test road: it settles on its wheels, drives forward, reaches a sensible speed, brakes to a stop and never produces NaN.</summary>
    public class FleetDrivingTests
    {
        [UnityTest, Timeout(900000)]   // 35 cars at about 9 s each
        public IEnumerator Every_Garage_Car_Drives_And_Stops()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            var problems = new List<string>();
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.transform.position = new Vector3(0f, -0.5f, 100f);
            road.transform.localScale = new Vector3(60f, 1f, 600f);
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData data = catalog.Get(i);
                GameObject car = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.45f, 0f), Quaternion.identity);
                data.ApplyTo(car);
                var vc = car.GetComponent<VehicleController>();
                var input = car.GetComponent<VehicleInput>();
                var rb = car.GetComponent<Rigidbody>();
                input.KeyboardEnabled = false;
                vc.ControlsEnabled = true;
                yield return new WaitForSeconds(1.2f);                         // settle on the suspension
                Vector3 start = rb.position;
                if (start.y < -0.2f || start.y > 1.2f) problems.Add($"{data.CarId}: did not settle on its wheels (y {start.y:F2})");

                input.SetThrottle(1f);
                yield return new WaitForSeconds(3.5f);
                float travelled = Vector3.Dot(rb.position - start, car.transform.forward);
                float speed = vc.SpeedKmh;
                if (float.IsNaN(rb.position.x) || float.IsNaN(speed)) problems.Add($"{data.CarId}: NaN");
                if (travelled < 3f) problems.Add($"{data.CarId}: only moved {travelled:F1} m in 3.5 s");
                if (speed > data.Handling.maxForwardSpeedKmh * 1.15f) problems.Add($"{data.CarId}: {speed:F0} km/h exceeds its {data.Handling.maxForwardSpeedKmh:F0} limit");
                if (Mathf.Abs(car.transform.position.x) > 4f) problems.Add($"{data.CarId}: drifted {car.transform.position.x:F1} m sideways on a straight road");

                input.SetThrottle(0f);
                input.SetBrake(1f);
                float t = 0f;
                while (vc.SpeedKmh > 1f && t < 9f) { t += Time.deltaTime; yield return null; }
                if (vc.SpeedKmh > 1f) problems.Add($"{data.CarId}: still at {vc.SpeedKmh:F1} km/h after 9 s of braking");
                Debug.Log($"[Test] Fleet {data.CarId,-12} drove {travelled,5:F1} m in 3.5 s, reached {speed,4:F0} km/h (limit {data.Handling.maxForwardSpeedKmh:F0}), stopped in {t:F1} s");
                Object.Destroy(car);
                yield return null;
            }
            Object.Destroy(road);
            Assert.IsEmpty(problems, string.Join("\n", problems));
#else
            yield break;
#endif
        }
    }
}
