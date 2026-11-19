using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Conditions;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Gives the World Tour missions that MISSION_PLAN_90 marked as night, rain, snow or fog their condition (previously they used
    /// a plain daytime substitute). Runs after the arenas are built; safe to run repeatedly.
    /// </summary>
    public static class MissionConditions
    {
        public readonly struct Entry
        {
            public readonly RoadCondition Condition;
            public readonly MissionType Type;
            public readonly string BriefingNote;
            public Entry(RoadCondition c, MissionType t, string note) { Condition = c; Type = t; BriefingNote = note; }
        }

        public static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
        {
            // Night: headlights, dark bay.
            ["CN3"] = new Entry(RoadCondition.Night, MissionType.NightParking, "It is night: your headlights are on. Find the bay by its glowing markers."),
            ["ES2"] = new Entry(RoadCondition.Night, MissionType.NightParking, "Night in Madrid: your headlights are on. Find the bay by its glowing markers."),
            ["MX3"] = new Entry(RoadCondition.Night, MissionType.NightParking, "Night in Mexico City: your headlights are on. Find the bay by its glowing markers."),
            ["EG3"] = new Entry(RoadCondition.Night, MissionType.NightParking, "Night in Cairo: your headlights are on. Find the bay by its glowing markers."),
            // Rain: wet road, longer braking.
            ["MY1"] = new Entry(RoadCondition.Rain, MissionType.SlipperySurface, "Heavy rain: the road is wet, so brake earlier and steer gently."),
            ["NL2"] = new Entry(RoadCondition.Rain, MissionType.SlipperySurface, "Rain on the canal streets: the road is wet, so brake earlier."),
            ["BR2"] = new Entry(RoadCondition.Rain, MissionType.SlipperySurface, "A tropical downpour: the road is wet, so brake earlier."),
            ["AU2"] = new Entry(RoadCondition.Rain, MissionType.SlipperySurface, "Rain in the city: the road is wet, so brake earlier."),
            // Snow and ice: very low grip.
            ["RU1"] = new Entry(RoadCondition.Snow, MissionType.SlipperySurface, "Snow and ice: grip is very low. Use gentle steering and braking."),
            ["CA1"] = new Entry(RoadCondition.Snow, MissionType.SlipperySurface, "Snow and ice: grip is very low. Use gentle steering and braking."),
            ["CH1"] = new Entry(RoadCondition.Snow, MissionType.SlipperySurface, "Alpine snow: grip is very low. Use gentle steering and braking."),
            ["NZ2"] = new Entry(RoadCondition.Snow, MissionType.SlipperySurface, "Snow and ice: grip is very low. Use gentle steering and braking."),
            // Fog: restricted visibility.
            ["NL1"] = new Entry(RoadCondition.Fog, MissionType.RestrictedVisibility, "Thick morning fog on the canal: you can only see a short way ahead."),
            ["CH3"] = new Entry(RoadCondition.Fog, MissionType.RestrictedVisibility, "Lake fog: you can only see a short way ahead. Park precisely."),
        };

        public static int ApplyAll()
        {
            int applied = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level == null || !Table.TryGetValue(level.LevelId, out Entry e)) continue;
                var so = new SerializedObject(level);
                so.FindProperty("condition").enumValueIndex = (int)e.Condition;
                so.FindProperty("missionType").enumValueIndex = (int)e.Type;
                SerializedProperty brief = so.FindProperty("briefing");
                if (!brief.stringValue.Contains(e.BriefingNote)) brief.stringValue = (brief.stringValue.TrimEnd() + " " + e.BriefingNote).Trim();
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(level);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Conditions] applied road conditions to {applied}/{Table.Count} missions");
            return applied;
        }
    }
}
