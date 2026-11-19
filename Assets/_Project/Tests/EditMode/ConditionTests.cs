using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Conditions;
using UltimateCarParking.EditorTools;
using UltimateCarParking.Missions;

namespace UltimateCarParking.Tests
{
    public class ConditionProfileTests
    {
        [Test]
        public void Clear_Changes_Nothing()
        {
            ConditionProfile p = ConditionProfiles.For(RoadCondition.Clear);
            Assert.AreEqual(1f, p.Grip);
            Assert.AreEqual(1f, p.SunIntensityScale);
            Assert.AreEqual(1f, p.AmbientScale);
            Assert.AreEqual(0f, p.FogDensity);
            Assert.IsFalse(p.Headlights);
            Assert.AreEqual(Precipitation.None, p.Precip);
            Assert.AreEqual("", p.Hint);
        }

        [Test]
        public void Grip_Orders_Dry_Above_Rain_Above_Snow()
        {
            float rain = ConditionProfiles.For(RoadCondition.Rain).Grip, snow = ConditionProfiles.For(RoadCondition.Snow).Grip;
            Assert.Less(rain, 1f);
            Assert.Less(snow, rain);
            Assert.Greater(snow, 0.2f, "still drivable");
            Assert.AreEqual(1f, ConditionProfiles.For(RoadCondition.Night).Grip, "night is only about light");
            Assert.AreEqual(1f, ConditionProfiles.For(RoadCondition.Fog).Grip, "fog is only about visibility");
        }

        [Test]
        public void Each_Condition_Has_Its_Own_Defining_Effect()
        {
            Assert.IsTrue(ConditionProfiles.For(RoadCondition.Night).Headlights);
            Assert.Less(ConditionProfiles.For(RoadCondition.Night).SunIntensityScale, 0.2f, "much darker");
            Assert.AreEqual(Precipitation.Rain, ConditionProfiles.For(RoadCondition.Rain).Precip);
            Assert.AreEqual(Precipitation.Snow, ConditionProfiles.For(RoadCondition.Snow).Precip);
            Assert.Greater(ConditionProfiles.For(RoadCondition.Fog).FogDensity, ConditionProfiles.For(RoadCondition.Rain).FogDensity, "fog is the thickest");
            foreach (RoadCondition c in new[] { RoadCondition.Night, RoadCondition.Rain, RoadCondition.Snow, RoadCondition.Fog })
            {
                Assert.IsNotEmpty(ConditionProfiles.For(c).Hint, c + " has a start warning");
                Assert.IsNotEmpty(ConditionProfiles.Label(c));
            }
        }

        [Test]
        public void Precipitation_Rates_Are_Mobile_Sized()
        {
            Assert.LessOrEqual(ConditionProfiles.For(RoadCondition.Rain).PrecipRate, 800);
            Assert.LessOrEqual(ConditionProfiles.For(RoadCondition.Snow).PrecipRate, 500);
        }

        [Test]
        public void The_Mission_Table_Matches_The_Plan_And_Is_Consistent()
        {
            Assert.AreEqual(14, MissionConditions.Table.Count);
            foreach (var kv in MissionConditions.Table)
            {
                MissionType expected = kv.Value.Condition switch
                {
                    RoadCondition.Night => MissionType.NightParking,
                    RoadCondition.Fog => MissionType.RestrictedVisibility,
                    _ => MissionType.SlipperySurface,
                };
                Assert.AreEqual(expected, kv.Value.Type, kv.Key + " label matches its condition");
                Assert.IsNotEmpty(kv.Value.BriefingNote);
                Assert.IsTrue(char.IsLetter(kv.Key[0]) && char.IsDigit(kv.Key[kv.Key.Length - 1]), kv.Key + " is a mission id");
            }
            int night = 0, rain = 0, snow = 0, fog = 0;
            foreach (var e in MissionConditions.Table.Values)
            {
                if (e.Condition == RoadCondition.Night) night++;
                if (e.Condition == RoadCondition.Rain) rain++;
                if (e.Condition == RoadCondition.Snow) snow++;
                if (e.Condition == RoadCondition.Fog) fog++;
            }
            Assert.AreEqual((4, 4, 4, 2), (night, rain, snow, fog));
        }
    }
}
