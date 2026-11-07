using System;
using UnityEngine;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// What kind of mission a level is. Used for labels (HUD kicker, level select, briefing); behaviour comes from
    /// the level's objective classes, never from switching on this value.
    /// </summary>
    public enum MissionType
    {
        StandardParking,
        ReverseParking,
        ParallelParking,
        PrecisionParking,
        ParkingExam,
        TimedParking,
        NarrowRoute,
        MultiStoreyRamp,
        MovingObstacles,
        ValetParking,
        MultiStage,
        SlipperySurface,
        NightParking,
        RestrictedVisibility,
    }

    /// <summary>Display names for <see cref="MissionType"/> (a lookup table, not behaviour).</summary>
    public static class MissionTypeInfo
    {
        private static readonly string[] Titles =
        {
            "STANDARD PARKING", "REVERSE PARKING", "PARALLEL PARKING", "PRECISION PARKING", "PARKING EXAM",
            "TIMED PARKING", "NARROW ROUTE", "MULTI-STOREY RAMP", "MOVING TRAFFIC", "VALET PARKING",
            "MULTI-STAGE", "SLIPPERY SURFACE", "NIGHT PARKING", "LIMITED VIEW",
        };

        public static string Title(MissionType type)
        {
            int i = (int)type;
            return i >= 0 && i < Titles.Length ? Titles[i] : type.ToString().ToUpperInvariant();
        }

        public static int Count => Enum.GetValues(typeof(MissionType)).Length;

        /// <summary>Guards the table against enum edits (called by tests).</summary>
        public static bool TableMatchesEnum() => Titles.Length == Count;
    }
}
