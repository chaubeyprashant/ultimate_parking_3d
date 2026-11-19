using UnityEngine;

namespace UltimateCarParking.Conditions
{
    /// <summary>Weather / time of day for a mission. Clear is the default and changes nothing.</summary>
    public enum RoadCondition { Clear = 0, Night = 1, Rain = 2, Snow = 3, Fog = 4 }

    public enum Precipitation { None, Rain, Snow }

    /// <summary>Everything a condition changes. Plain data so it can be unit-tested; <see cref="ConditionsApplier"/> applies it.</summary>
    public readonly struct ConditionProfile
    {
        /// <summary>Multiplier on the tyres' forward and sideways grip (1 = dry).</summary>
        public readonly float Grip;
        public readonly float SunIntensityScale, AmbientScale;
        public readonly Color SunTint, AmbientTint, SkyTint, FogColor;
        /// <summary>Fog density to use when the arena's own fog is thinner.</summary>
        public readonly float FogDensity;
        public readonly float SkyExposure;
        public readonly bool Headlights;
        public readonly Precipitation Precip;
        /// <summary>Particles per second at full quality.</summary>
        public readonly int PrecipRate;
        public readonly string Hint;

        public ConditionProfile(float grip, float sunScale, Color sunTint, float ambientScale, Color ambientTint, Color skyTint, float skyExposure,
            Color fogColor, float fogDensity, bool headlights, Precipitation precip, int precipRate, string hint)
        {
            Grip = grip; SunIntensityScale = sunScale; SunTint = sunTint; AmbientScale = ambientScale; AmbientTint = ambientTint;
            SkyTint = skyTint; SkyExposure = skyExposure; FogColor = fogColor; FogDensity = fogDensity; Headlights = headlights;
            Precip = precip; PrecipRate = precipRate; Hint = hint;
        }
    }

    public static class ConditionProfiles
    {
        private static readonly Color White = Color.white;

        public static ConditionProfile For(RoadCondition c) => c switch
        {
            // Dark and blue; the headlight beam shows the way and the bay marker stays visible.
            RoadCondition.Night => new ConditionProfile(1.00f, 0.10f, new Color(0.45f, 0.55f, 0.95f), 0.32f, new Color(0.55f, 0.65f, 1.00f),
                new Color(0.06f, 0.08f, 0.20f), 0.22f, new Color(0.03f, 0.04f, 0.09f), 0.010f, true, Precipitation.None, 0, "NIGHT: HEADLIGHTS ON"),
            // Wet road: noticeably longer braking, grey light, rain.
            RoadCondition.Rain => new ConditionProfile(0.82f, 0.50f, new Color(0.80f, 0.85f, 0.95f), 0.80f, new Color(0.85f, 0.90f, 1.00f),
                new Color(0.42f, 0.46f, 0.52f), 0.70f, new Color(0.46f, 0.50f, 0.56f), 0.014f, false, Precipitation.Rain, 600, "WET ROAD: BRAKE EARLY"),
            // Snow and ice: very low grip, bright white light, slow flakes.
            RoadCondition.Snow => new ConditionProfile(0.45f, 0.70f, new Color(0.92f, 0.95f, 1.00f), 1.00f, new Color(0.92f, 0.95f, 1.00f),
                new Color(0.72f, 0.78f, 0.86f), 0.95f, new Color(0.80f, 0.84f, 0.90f), 0.016f, false, Precipitation.Snow, 320, "ICE AND SNOW: GENTLE INPUTS"),
            // Restricted visibility: thick grey fog, normal grip.
            RoadCondition.Fog => new ConditionProfile(1.00f, 0.65f, White, 0.90f, White,
                new Color(0.68f, 0.70f, 0.73f), 0.80f, new Color(0.70f, 0.72f, 0.75f), 0.050f, false, Precipitation.None, 0, "FOG: LOW VISIBILITY"),
            _ => new ConditionProfile(1f, 1f, White, 1f, White, White, 1f, White, 0f, false, Precipitation.None, 0, ""),
        };

        /// <summary>Short label for the mission card ("NIGHT", "RAIN"...). Empty for Clear.</summary>
        public static string Label(RoadCondition c) => c switch
        {
            RoadCondition.Night => "NIGHT", RoadCondition.Rain => "RAIN", RoadCondition.Snow => "SNOW", RoadCondition.Fog => "FOG", _ => "",
        };
    }
}
