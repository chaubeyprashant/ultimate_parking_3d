using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Conditions;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 7 in play: every car's customisable surfaces, lights, horn, engine stop, weather ambience, garage purchase flow.</summary>
    public class CustomizationPlayTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in spawned) if (g != null) Object.Destroy(g);
            spawned.Clear();
            yield return TestScenes.ResetToEmpty();
        }

        private static CarCatalog Catalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator Every_Car_Takes_Paint_And_Returns_To_Factory_And_Reports_Its_Options()
        {
            var rows = new List<string>();
            var noPaint = new List<string>();
            CarCatalog cat = Catalog();
            for (int i = 0; i < cat.Count; i++)
            {
                CarData d = cat.Get(i);
                GameObject car = Object.Instantiate(d.ModelPrefab, new Vector3(i * 8f, -50f, 0f), Quaternion.identity);
                spawned.Add(car);
                car.GetComponent<Rigidbody>().isKinematic = true;
                yield return null;
                var a = CarLookApplier.On(car);
                rows.Add($"{d.CarId}: paint {a.Supports(CustomSlot.Paint)} tint {a.Supports(CustomSlot.Tint)} rims {a.Supports(CustomSlot.Rims)} (body {a.BodySlots}, glass {a.GlassSlots}, wheels {a.WheelSlots})");
                if (!a.Supports(CustomSlot.Paint)) { noPaint.Add(d.CarId); continue; }
                a.Apply(new CarLook { carId = d.CarId, paint = 9, finish = 2, plate = "TEST 1" });
                // The body slot now carries the chosen colour in its property block; the shared material is untouched.
                bool painted = false;
                foreach (Renderer r in car.GetComponentsInChildren<MeshRenderer>())
                    for (int m = 0; m < r.sharedMaterials.Length; m++)
                    {
                        if (!r.HasPropertyBlock()) continue;
                        var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b, m);
                        if (!b.isEmpty && b.GetColor(BaseColorId) == CustomizationCatalog.Paints[9].color) painted = true;
                    }
                Assert.IsTrue(painted, d.CarId + " body painted");
                Assert.AreEqual("TEST 1", a.PlateShown, d.CarId + " plate");
                a.Apply(new CarLook { carId = d.CarId });
                Assert.AreEqual("", a.PlateShown);
            }
            Debug.Log("[Custom] options per car:\n" + string.Join("\n", rows));
            Assert.LessOrEqual(noPaint.Count, 2, "paint works on (almost) every car: " + string.Join(", ", noPaint));
        }

        private static IEnumerator LoadLevel(int index)
        {
#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            LevelSelection.Request(cat.Get(index), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { t += Time.unscaledDeltaTime; if (t > 10f) Assert.Fail("level"); yield return null; }
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.4f);
        }

        [UnityTest]
        public IEnumerator Brake_Reverse_Indicator_Lights_Horn_And_Engine_Stop_Work()
        {
            yield return LoadLevel(30);   // C01
            VehicleController car = LevelLoader.Instance.Vehicle;
            var lights = car.GetComponent<VehicleLights>();
            Assert.IsNotNull(lights, "lights fitted on spawn");
            Assert.AreEqual(10, lights.LampCount);
            car.Input.SetThrottle(1f); yield return new WaitForSeconds(1.2f);
            car.Input.SetThrottle(0f); car.Input.SetBrake(1f); yield return null; yield return null;
            Assert.IsTrue(lights.Braking, "brake lights while braking");
            yield return new WaitForSeconds(2f);
            car.Input.SetBrake(0f); car.Input.SetThrottle(-1f); yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(lights.Reversing, "reverse lights while reversing");
            car.Input.SetThrottle(0f);
            lights.ToggleIndicator(IndicatorMode.Hazard);
            bool sawOn = false, sawOff = false;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) { yield return null; if (lights.BlinkOn) sawOn = true; else sawOff = true; }
            Assert.IsTrue(sawOn && sawOff, "hazards blink");
            lights.SetHorn(true); yield return null;
            Assert.IsTrue(lights.HornOn, "horn sounds while held");
            lights.SetHorn(false);

            yield return new WaitForSeconds(1.5f);
            lights.ToggleEngine();
            Assert.IsFalse(car.EngineOn);
            Vector3 p0 = car.transform.position;
            car.Input.SetThrottle(1f); yield return new WaitForSeconds(1.5f); car.Input.SetThrottle(0f);
            Assert.Less(Vector3.Distance(p0, car.transform.position), 0.3f, "no drive with the engine off");
            lights.ToggleEngine();
            Assert.IsTrue(car.EngineOn);
        }

        [UnityTest]
        public IEnumerator Night_And_Rain_Turn_Headlights_On_With_Ambience_And_A_Wet_Road()
        {
            yield return LoadLevel(36);   // C07 Night Precision
            var lights = LevelLoader.Instance.Vehicle.GetComponent<VehicleLights>();
            var cond = Object.FindAnyObjectByType<ConditionsApplier>();
            Assert.IsTrue(lights.Headlights, "headlights on at night");
            Assert.IsNotNull(cond.AmbienceClip, "night ambience");
            yield return LoadLevel(37);   // C08 Wet Kerb (rain)
            cond = Object.FindAnyObjectByType<ConditionsApplier>();
            Assert.AreEqual(RoadCondition.Rain, cond.Active);
            StringAssert.Contains("rain", cond.AmbienceClip.name);
            Debug.Log($"[Custom] rain: {cond.WetSurfaces} road surfaces made wet");
            Assert.Greater(cond.WetSurfaces, 0, "the road looks wet");
        }

        [UnityTest]
        public IEnumerator Garage_Customisation_Previews_Asks_To_Confirm_Then_Buys_Once_And_Persists()
        {
            SaveManager.Current.Data.coins = 3000;
            SaveManager.Current.Save();
            SceneManager.LoadScene(SceneNames.Garage);
            yield return null; yield return null;
            var panel = Object.FindAnyObjectByType<CustomizePanel>(FindObjectsInactive.Include);
            var garage = Object.FindAnyObjectByType<GarageController>();
            Assert.IsNotNull(panel);
            Assert.AreEqual("sedan", garage.Shown.CarId, "starts on the owned starter");
            panel.Open();
            yield return null;
            Assert.AreEqual(13, panel.OptionCount, "factory + 12 colours");
            panel.Pick(4);
            StringAssert.Contains("PRICE", panel.StatusText);
            panel.Buy();
            StringAssert.StartsWith("CONFIRM", panel.BuyText, "first tap asks to confirm");
            Assert.AreEqual(3000, SaveManager.Current.Data.coins, "nothing spent yet");
            panel.Buy();
            Assert.AreEqual(3000 - CustomizationCatalog.PaintPrice, SaveManager.Current.Data.coins);
            Assert.AreEqual("FITTED", panel.StatusText);
            var restarted = new SaveManager(SaveManager.Current.FilePath);
            Assert.AreEqual(4, CustomizationService.Look(restarted.Data, "sedan").paint, "saved");
            panel.ResetLook();
            Assert.IsTrue(CustomizationService.Look(SaveManager.Current.Data, "sedan").IsFactory);
            Assert.AreEqual(3000 - CustomizationCatalog.PaintPrice, SaveManager.Current.Data.coins, "reset is free");
            panel.Close();
        }
    }
}
