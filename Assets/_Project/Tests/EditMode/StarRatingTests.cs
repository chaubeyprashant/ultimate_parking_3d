using NUnit.Framework;
using UltimateCarParking.Levels;

namespace UltimateCarParking.Tests
{
    public class StarRatingTests
    {
        private static readonly StarThresholds T = new StarThresholds(threeStarTime: 40f, threeStarMaxCollisions: 0, twoStarTime: 65f, twoStarMaxCollisions: 1);

        [TestCase(10f, 0, 3)]
        [TestCase(40f, 0, 3)]      // boundaries are inclusive
        [TestCase(40.1f, 0, 2)]    // too slow for 3
        [TestCase(10f, 1, 2)]      // one bump costs a star
        [TestCase(65f, 1, 2)]
        [TestCase(65.1f, 0, 1)]
        [TestCase(10f, 2, 1)]
        [TestCase(300f, 9, 1)]     // completing always earns at least one
        public void Stars_From_Time_And_Collisions(float time, int collisions, int expected)
        {
            Assert.AreEqual(expected, StarRating.Calculate(time, collisions, T));
        }

        [Test]
        public void Two_Star_Ceilings_Never_Below_Three_Star_Ceilings()
        {
            var t = new StarThresholds(50f, 2, 30f, 0);
            Assert.AreEqual(50f, t.TwoStarTime);
            Assert.AreEqual(2, t.TwoStarMaxCollisions);
        }
    }
}
