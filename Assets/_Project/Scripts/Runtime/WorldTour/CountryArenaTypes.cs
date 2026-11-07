using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltimateCarParking.WorldTour
{
    public enum ArenaSource
    {
        /// <summary>No dedicated arena yet: the country's missions use their own level environments.</summary>
        None,
        /// <summary>A prefab under a Resources folder, loaded with Resources.LoadAsync and instantiated.</summary>
        ResourcesPrefab,
        /// <summary>A scene in Build Settings, loaded additively with SceneManager.LoadSceneAsync.</summary>
        Scene,
    }

    /// <summary>
    /// Where a country's arena lives, by name only. CountryData never holds a direct object reference to the arena,
    /// so loading the country list (all 30 countries) does not pull any arena into memory — only
    /// <see cref="CountryArenaLoader"/> resolves the name, for the one selected country.
    /// (Swap the resolver for Addressables later without touching the data: the key stays a string.)
    /// </summary>
    [Serializable]
    public sealed class CountryArenaReference
    {
        public ArenaSource source = ArenaSource.None;
        [Tooltip("ResourcesPrefab: path under a Resources folder without extension (e.g. \"Arenas/Arena_TestA\"). Scene: scene name.")]
        public string key = "";

        public bool IsSet => source != ArenaSource.None && !string.IsNullOrWhiteSpace(key);
        public override string ToString() => IsSet ? $"{source}:{key}" : "none";
    }

    /// <summary>What it takes to open a country. All set conditions must hold (AND).</summary>
    [Serializable]
    public sealed class CountryUnlockRequirement
    {
        [Tooltip("Open from the start (the first tour stop).")]
        public bool unlockedByDefault;
        [Tooltip("The previous country in tour order must be completed (the classic tour chain).")]
        public bool requirePreviousCountry = true;
        [Tooltip("These countries (ids) must be completed.")]
        public string[] requiredCountryIds = Array.Empty<string>();
        [Tooltip("Stars earned across the whole tour.")]
        [Min(0)] public int minTourStars;
        [Tooltip("Number of completed countries across the tour.")]
        [Min(0)] public int minCompletedCountries;
    }

    public enum RoadDirection
    {
        /// <summary>Traffic keeps right (most of the world).</summary>
        RightHand,
        /// <summary>Traffic keeps left (UK, Japan, India, Australia, …).</summary>
        LeftHand,
    }

    /// <summary>Ambient traffic for an arena. Data only today: no moving-traffic system consumes it yet.</summary>
    [Serializable]
    public sealed class TrafficSettings
    {
        [Range(0f, 1f)] public float parkedCarDensity = 0.7f;
        public bool movingTraffic;
        [Range(0f, 1f)] public float movingTrafficDensity;
        [Min(0f)] public float trafficSpeedKmh = 20f;
        public bool pedestrians;
    }

    public enum WeatherPreset { Clear, Overcast, Rain, Fog, Snow, Dusk, Night }

    /// <summary>
    /// Country weather: a preset plus the lighting / fog it implies. Applied by <see cref="CountryArenaLoader"/>
    /// when the arena loads. Road wetness and precipitation are carried as data for gameplay / VFX to use.
    /// </summary>
    [Serializable]
    public sealed class WeatherSettings
    {
        public WeatherPreset preset = WeatherPreset.Clear;
        public Color sunColor = new Color(1f, 0.96f, 0.9f);
        [Min(0f)] public float sunIntensity = 1.25f;
        public Vector3 sunEuler = new Vector3(50f, -35f, 0f);
        public Color ambientSky = new Color(0.62f, 0.68f, 0.78f);
        public Color ambientEquator = new Color(0.45f, 0.46f, 0.47f);
        public Color ambientGround = new Color(0.22f, 0.21f, 0.20f);
        public bool fog;
        public Color fogColor = new Color(0.6f, 0.65f, 0.7f);
        [Min(0f)] public float fogDensity = 0.01f;
        [Range(0f, 1f)] public float roadWetness;
        [Range(0f, 1f)] public float precipitation;

        /// <summary>Sensible values for a preset (used by tools and as a starting point in the Inspector).</summary>
        public static WeatherSettings ForPreset(WeatherPreset p)
        {
            var w = new WeatherSettings { preset = p };
            switch (p)
            {
                case WeatherPreset.Overcast:
                    w.sunIntensity = 0.75f; w.sunColor = new Color(0.86f, 0.88f, 0.92f);
                    w.ambientSky = new Color(0.55f, 0.58f, 0.62f); w.fog = true; w.fogColor = new Color(0.62f, 0.64f, 0.67f); w.fogDensity = 0.008f;
                    break;
                case WeatherPreset.Rain:
                    w.sunIntensity = 0.55f; w.sunColor = new Color(0.78f, 0.82f, 0.9f);
                    w.ambientSky = new Color(0.42f, 0.46f, 0.52f); w.fog = true; w.fogColor = new Color(0.45f, 0.48f, 0.53f); w.fogDensity = 0.016f;
                    w.roadWetness = 0.8f; w.precipitation = 0.7f;
                    break;
                case WeatherPreset.Fog:
                    w.sunIntensity = 0.6f; w.fog = true; w.fogColor = new Color(0.7f, 0.72f, 0.74f); w.fogDensity = 0.035f;
                    break;
                case WeatherPreset.Snow:
                    w.sunIntensity = 0.9f; w.sunColor = new Color(0.92f, 0.95f, 1f); w.ambientSky = new Color(0.72f, 0.76f, 0.84f);
                    w.fog = true; w.fogColor = new Color(0.82f, 0.85f, 0.9f); w.fogDensity = 0.012f; w.roadWetness = 0.4f; w.precipitation = 0.5f;
                    break;
                case WeatherPreset.Dusk:
                    w.sunIntensity = 0.9f; w.sunColor = new Color(1f, 0.66f, 0.42f); w.sunEuler = new Vector3(8f, -60f, 0f);
                    w.ambientSky = new Color(0.55f, 0.45f, 0.48f); w.ambientEquator = new Color(0.4f, 0.32f, 0.3f);
                    break;
                case WeatherPreset.Night:
                    w.sunIntensity = 0.18f; w.sunColor = new Color(0.6f, 0.7f, 1f); w.sunEuler = new Vector3(40f, 30f, 0f);
                    w.ambientSky = new Color(0.12f, 0.14f, 0.2f); w.ambientEquator = new Color(0.08f, 0.09f, 0.12f); w.ambientGround = new Color(0.04f, 0.04f, 0.05f);
                    break;
            }
            return w;
        }

        /// <summary>Applies lighting and fog to the current render settings and the given sun (may be null).</summary>
        public void Apply(Light sun)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            if (sun == null) return;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.transform.rotation = Quaternion.Euler(sunEuler);
        }
    }
}
