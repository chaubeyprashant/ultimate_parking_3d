using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.EditorTools;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 5: realistic parking scorecard, difficulty tiers, assistance caps and the challenge chapter data.</summary>
    public class ParkingScoreTests
    {
        private static readonly Vector2 Bay = new Vector2(2.6f, 5.8f);

        [Test]
        public void A_Centred_Straight_Clean_Park_Scores_Near_Full()
        {
            var card = ParkingScorecard.Score(Vector2.zero, new Vector2(1.8f, 4.4f), Bay, 0.5f, 8f, 0, 0, 30f, 150f, 0f);
            Assert.Greater(card.Overall, 0.97f);
            StringAssert.StartsWith("PARKING 9", card.Label);
        }

        [Test]
        public void Offset_Angle_And_Hits_Each_Cost_Points_Impacts_More_Than_Scrapes()
        {
            Vector2 car = new Vector2(1.8f, 4.4f);
            float perfect = ParkingScorecard.Score(Vector2.zero, car, Bay, 0f, 8f, 0, 0, 0f, 0f, 0f).Overall;
            float off = ParkingScorecard.Score(new Vector2(0.3f, 0f), car, Bay, 0f, 8f, 0, 0, 0f, 0f, 0f).Overall;
            float crooked = ParkingScorecard.Score(Vector2.zero, car, Bay, 6f, 8f, 0, 0, 0f, 0f, 0f).Overall;
            float scraped = ParkingScorecard.Score(Vector2.zero, car, Bay, 0f, 8f, 0, 1, 0f, 0f, 0f).Overall;
            float hit = ParkingScorecard.Score(Vector2.zero, car, Bay, 0f, 8f, 1, 0, 0f, 0f, 0f).Overall;
            Assert.Less(off, perfect); Assert.Less(crooked, perfect); Assert.Less(scraped, perfect);
            Assert.Less(hit, scraped, "an impact costs more than a scrape");
            float slow = ParkingScorecard.Score(Vector2.zero, car, Bay, 0f, 8f, 0, 0, 140f, 150f, 0f).Overall;
            Assert.Less(slow, perfect, "near the time limit costs a little");
        }

        [Test]
        public void Centring_Is_Judged_Against_Each_Cars_Own_Room_In_The_Bay()
        {
            // Offsets are measured against the room each car has: 0.2 m uses most of a wide truck's spare room but little of a small car's.
            var small = ParkingScorecard.Score(new Vector2(0.2f, 0f), new Vector2(1.6f, 3.8f), Bay, 0f, 8f, 0, 0, 0f, 0f, 0f);
            var wide = ParkingScorecard.Score(new Vector2(0.2f, 0f), new Vector2(2.1f, 5.2f), Bay, 0f, 8f, 0, 0, 0f, 0f, 0f);
            Assert.Greater(small.Centring, 0f);
            Assert.Less(wide.Centring, small.Centring + 1e-4f, "same offset uses more of the truck's smaller room");
            // A truck centred as well as its room allows is not punished for its size.
            var truckCentred = ParkingScorecard.Score(Vector2.zero, new Vector2(2.1f, 5.2f), Bay, 0f, 8f, 0, 0, 0f, 0f, 0f);
            Assert.AreEqual(1f, truckCentred.Centring, 1e-4f);
        }

        [Test]
        public void Parking_Mode_Stars_Follow_The_Score_And_Hit_Ceilings()
        {
            var t = new StarThresholds(0f, 0, 0f, 1, ScoreMode.Parking, 0.85f, 0.6f);
            Assert.AreEqual(3, StarRating.Calculate(999f, 0, t, 0.9f));
            Assert.AreEqual(2, StarRating.Calculate(999f, 0, t, 0.7f));
            Assert.AreEqual(2, StarRating.Calculate(999f, 1, t, 0.95f), "a hit caps at two stars");
            Assert.AreEqual(1, StarRating.Calculate(999f, 0, t, 0.4f));
        }

        [Test]
        public void Difficulty_Tiers_Get_Strictly_Harder()
        {
            var tiers = new[] { ChallengeDifficulty.Beginner, ChallengeDifficulty.Intermediate, ChallengeDifficulty.Advanced, ChallengeDifficulty.Expert }
                .Select(DifficultyProfile.For).ToArray();
            for (int i = 1; i < tiers.Length; i++)
            {
                Assert.Less(tiers[i].BayExtraWidth, tiers[i - 1].BayExtraWidth, "bays get tighter");
                Assert.Less(tiers[i].AngleTolerance, tiers[i - 1].AngleTolerance, "alignment gets stricter");
                Assert.LessOrEqual(tiers[i].CollisionLimit, tiers[i - 1].CollisionLimit, "fewer hits allowed");
                Assert.GreaterOrEqual((int)tiers[i].MaxAssist, (int)tiers[i - 1].MaxAssist, "less assistance");
            }
            Assert.AreEqual(AssistLevel.Expert, tiers[3].MaxAssist, "Expert: minimal assistance");
            Assert.AreEqual(AssistLevel.Beginner, tiers[0].MaxAssist, "Beginner: guides on");
        }

        [Test]
        public void Every_Challenge_Bay_Fits_The_Largest_Garage_Car()
        {
            foreach (ChallengeDifficulty d in new[] { ChallengeDifficulty.Beginner, ChallengeDifficulty.Intermediate, ChallengeDifficulty.Advanced, ChallengeDifficulty.Expert })
            {
                Vector2 bay = ChallengeLevels.Bay(d);
                Assert.Greater(bay.x, 1.93f + 0.3f, d + " width");
                Assert.Greater(bay.y, 5.2f + 0.3f, d + " length");
            }
        }

        [Test]
        public void The_Challenge_Chapter_Follows_The_Existing_Campaign()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            Assert.AreEqual(LevelDesigner.LevelCount + ChallengeLevels.Count, catalog.Count);
            Assert.AreEqual("L01", catalog.Get(0).LevelId, "existing levels keep their ids and order");
            for (int i = 0; i < LevelDesigner.LevelCount; i++) Assert.AreEqual(ChallengeDifficulty.None, catalog.Get(i).Difficulty, "existing levels unchanged");
            var types = new System.Collections.Generic.HashSet<MissionType>();
            for (int i = 0; i < ChallengeLevels.Count; i++)
            {
                LevelData l = catalog.Get(LevelDesigner.LevelCount + i);
                Assert.AreEqual($"C{i + 1:00}", l.LevelId);
                Assert.AreEqual(LevelDesigner.LevelCount + i + 1, l.LevelNumber);
                Assert.AreNotEqual(ChallengeDifficulty.None, l.Difficulty);
                Assert.AreEqual(ScoreMode.Parking, l.ScoreMode);
                Assert.IsTrue(l.HasObjectives);
                types.Add(l.MissionType);
            }
            foreach (MissionType t in new[] { MissionType.ParallelParking, MissionType.ReverseParking, MissionType.PrecisionParking, MissionType.ValetParking,
                         MissionType.TimedParking, MissionType.MultiStage, MissionType.ObstacleCourse, MissionType.HeavyVehicle, MissionType.ParkingExam })
                Assert.IsTrue(types.Contains(t), "chapter covers " + t);
        }
    }
}
