using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.UI;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 4 garage screen: spec sheet, compare with the car in use, showroom zoom, purchases untouched.</summary>
    public class GarageScreenTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { yield return TestScenes.ResetToEmpty(); }

        [UnityTest]
        public IEnumerator Spec_Sheet_Shows_Measured_Figures_And_Compares_With_The_Car_In_Use()
        {
            SceneManager.LoadScene(SceneNames.Garage);
            yield return null; yield return null;
            var garage = Object.FindAnyObjectByType<GarageController>();
            var specs = Object.FindAnyObjectByType<GarageSpecsPanel>();
            Assert.IsNotNull(specs, "spec panel in the garage");
            StringAssert.Contains("STOP FROM 40", specs.ValuesText);
            StringAssert.Contains("TURN RADIUS", specs.ValuesText);
            StringAssert.Contains("PARKING", specs.DifficultyText);
            int coinsBefore = UltimateCarParking.Save.SaveManager.Current.Data.coins;

            garage.Step(1);          // a car that is not the one in use
            yield return null;
            GameObject.Find("Specs/CompareButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(specs.Comparing);
            StringAssert.Contains("<color=#", specs.ValuesText, "differences against the car in use are shown");
            Debug.Log("[Test] Garage compare: " + specs.ValuesText.Replace("\n", " | "));
            Assert.AreEqual(coinsBefore, UltimateCarParking.Save.SaveManager.Current.Data.coins, "browsing and comparing spend nothing");

            var drag = Object.FindAnyObjectByType<ShowroomDragArea>();
            float fov = drag.Fov;
            drag.Zoom(10f);
            Assert.Less(drag.Fov, fov, "zooms in");
            drag.Zoom(-100f);
            Assert.AreEqual(fov, drag.Fov, 0.01f, "never zooms out past the full view");
        }
    }
}
