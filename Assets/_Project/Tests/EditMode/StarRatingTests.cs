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
    
        [Test]
        public void Quality_Mode_Uses_The_Mission_Score_Not_The_Time()
        {
            var t = new StarThresholds(10f, 0, 20f, 1, ScoreMode.Quality, 0.85f, 0.6f);
            Assert.AreEqual(3, StarRating.Calculate(500f, 0, t, 0.9f), "slow but accurate = 3 stars");
            Assert.AreEqual(2, StarRating.Calculate(5f, 0, t, 0.7f));
            Assert.AreEqual(1, StarRating.Calculate(5f, 0, t, 0.3f));
            Assert.AreEqual(2, StarRating.Calculate(5f, 1, t, 0.95f), "hits still cap the stars");
            Assert.AreEqual(1, StarRating.Calculate(5f, 2, t, 0.95f));
            var classic = new StarThresholds(10f, 0, 20f, 1);
            Assert.AreEqual(3, StarRating.Calculate(5f, 0, classic, 0.1f), "classic levels ignore quality");
        }
    }
}
