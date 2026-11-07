using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Tests
{
    /// <summary>Left- / right-hand traffic layout rules (controls are unaffected; only placement changes).</summary>
    public class TrafficSideTests
    {
        [Test]
        public void Keep_Sign_And_Prompt()
        {
            Assert.AreEqual(1, TrafficSide.KeepSign(RoadDirection.RightHand));
            Assert.AreEqual(-1, TrafficSide.KeepSign(RoadDirection.LeftHand));
            Assert.AreEqual("DRIVE ON THE LEFT", TrafficSide.KeepPrompt(RoadDirection.LeftHand));
            Assert.AreEqual("DRIVE ON THE RIGHT", TrafficSide.KeepPrompt(RoadDirection.RightHand));
        }

        [Test]
        public void Lane_Centre_Is_On_The_Keep_Side_For_Any_Heading()
        {
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f, 33f })
            {
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 right = TrafficSide.RightOf(fwd);
                Assert.AreEqual(0f, Vector3.Dot(right, fwd), 1e-5f, "perpendicular");
                Assert.AreEqual(1f, Vector3.Cross(fwd, right).y, 1e-4f, "right really is to the right (forward x right = up)");
                Vector3 rht = TrafficSide.LaneCentre(Vector3.zero, fwd, 3.5f, RoadDirection.RightHand);
                Vector3 lht = TrafficSide.LaneCentre(Vector3.zero, fwd, 3.5f, RoadDirection.LeftHand);
                Assert.AreEqual(1.75f, Vector3.Dot(rht, right), 1e-4f, $"yaw {yaw}: right-hand lane on the right");
                Assert.AreEqual(-1.75f, Vector3.Dot(lht, right), 1e-4f, $"yaw {yaw}: left-hand lane on the left");
            }
            // Driving north (+Z) the right is east (+X).
            Assert.AreEqual(Vector3.right, TrafficSide.RightOf(Vector3.forward));
        }

        [Test]
        public void Kerb_Side_And_Parked_Car_Heading_Follow_The_Road_Direction()
        {
            Assert.AreEqual(1, TrafficSide.KerbSide(RoadDirection.RightHand), "park against the right kerb");
            Assert.AreEqual(-1, TrafficSide.KerbSide(RoadDirection.LeftHand), "park against the left kerb (UK)");
            // Parked cars face their side's traffic.
            Assert.AreEqual(90f, TrafficSide.ParkedYaw(90f, +1, RoadDirection.RightHand));
            Assert.AreEqual(270f, TrafficSide.ParkedYaw(90f, -1, RoadDirection.RightHand));
            Assert.AreEqual(90f, TrafficSide.ParkedYaw(90f, -1, RoadDirection.LeftHand));
            Assert.AreEqual(270f, TrafficSide.ParkedYaw(90f, +1, RoadDirection.LeftHand));
            Assert.AreEqual(-2.25f, TrafficSide.KerbOffset(3.25f, 1f, RoadDirection.LeftHand), 1e-5f);
            Assert.AreEqual(2.25f, TrafficSide.KerbOffset(3.25f, 1f, RoadDirection.RightHand), 1e-5f);
        }
    }
}
