using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Tests
{
    public class ParkingGeometryTests
    {
        private static readonly Vector2 Bay = new Vector2(2.8f, 5.5f);

        private static Vector3[] Car(Vector3 center, float yaw, float width = 1.8f, float length = 4.3f)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            float hw = width * 0.5f, hl = length * 0.5f;
            return new[]
            {
                center + r * new Vector3(-hw, 0f, -hl), center + r * new Vector3(hw, 0f, -hl),
                center + r * new Vector3(hw, 0f, hl), center + r * new Vector3(-hw, 0f, hl),
            };
        }

        [Test]
        public void Centred_Car_Is_Fully_Inside()
        {
            Assert.AreEqual(4, ParkingGeometry.CountPointsInside(Vector3.zero, Quaternion.identity, Bay, Car(Vector3.zero, 0f), 0.05f));
        }

        [Test]
        public void Overhanging_Car_Is_Partially_Inside()
        {
            Assert.AreEqual(2, ParkingGeometry.CountPointsInside(Vector3.zero, Quaternion.identity, Bay, Car(new Vector3(0f, 0f, 1.5f), 0f), 0.05f));
            Assert.AreEqual(0, ParkingGeometry.CountPointsInside(Vector3.zero, Quaternion.identity, Bay, Car(new Vector3(10f, 0f, 0f), 0f), 0.05f));
        }

        [Test]
        public void Margin_Shrinks_The_Bay()
        {
            Vector3[] car = Car(new Vector3(0.45f, 0f, 0f), 0f);   // right side 0.05 m from the line
            Assert.AreEqual(4, ParkingGeometry.CountPointsInside(Vector3.zero, Quaternion.identity, Bay, car, 0f));
            Assert.AreEqual(2, ParkingGeometry.CountPointsInside(Vector3.zero, Quaternion.identity, Bay, car, 0.1f));
        }

        [Test]
        public void Rotated_Bay_And_Height_Are_Handled()
        {
            Quaternion bayRot = Quaternion.Euler(0f, 90f, 0f);
            Vector3 center = new Vector3(5f, 0f, -3f);
            Assert.AreEqual(4, ParkingGeometry.CountPointsInside(center, bayRot, Bay, Car(center + Vector3.up * 0.4f, 90f), 0.05f));
            Assert.Less(ParkingGeometry.CountPointsInside(center, bayRot, Bay, Car(center, 0f), 0.05f), 4, "car across a rotated bay does not fit");
        }

        [TestCase(0f, false, 0f)]
        [TestCase(8f, false, 8f)]
        [TestCase(-8f, false, 8f)]
        [TestCase(180f, false, 180f)]
        [TestCase(180f, true, 0f)]     // reversed in
        [TestCase(172f, true, 8f)]
        [TestCase(90f, true, 90f)]
        public void Heading_Error(float carYaw, bool eitherDirection, float expected)
        {
            Vector3 fwd = Quaternion.Euler(0f, carYaw, 0f) * Vector3.forward;
            Assert.AreEqual(expected, ParkingGeometry.HeadingError(fwd, Vector3.forward, eitherDirection), 0.01f);
        }

        [Test]
        public void Heading_Ignores_Pitch()
        {
            Vector3 pitched = Quaternion.Euler(-10f, 5f, 0f) * Vector3.forward;
            Assert.AreEqual(5f, ParkingGeometry.HeadingError(pitched, Vector3.forward, false), 0.01f);
        }
    }
}
