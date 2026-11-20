using UnityEngine;
using UnityEngine.Rendering;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Conditions
{
    /// <summary>
    /// Applies a mission's <see cref="RoadCondition"/>: scene light, sky and fog, tyre grip, a camera-following rain or snow particle
    /// system, and a headlight beam on the car at night. Everything is cheap on purpose (one particle system, one small mesh, no real-time
    /// lights, which the mobile render pipeline has switched off) and fully undone by <see cref="Clear"/>. One instance lives next to the
    /// LevelLoader.
    /// </summary>
    public sealed class ConditionsApplier : MonoBehaviour
    {
        private const string RainMaterial = "Conditions/Rain", SnowMaterial = "Conditions/Snow", BeamMaterial = "Conditions/Beam";

        public RoadCondition Active { get; private set; } = RoadCondition.Clear;
        public bool HasHeadlightBeam => beam != null;
        public ParticleSystem Precipitation => precip;
        public float PrecipitationScale { get; set; } = 1f;

        private bool saved;
        private Color sunColor, ambSky, ambEq, ambGround, fogColor;
        private float sunIntensity, fogDensity, reflection;
        private bool fogOn;
        private FogMode fogMode;
        private AmbientMode ambMode;
        private Material originalSky, skyCopy;
        private VehicleController vehicle;
        private Transform follow;
        private ParticleSystem precip;
        private GameObject beam;

        public void Apply(RoadCondition condition, VehicleController car, Transform cameraTransform)
        {
            Clear();
            vehicle = car;
            if (condition == RoadCondition.Clear)
            {
                if (car != null) car.SetSurfaceGrip(1f);
                Ambience(condition);
                return;
            }
            Active = condition;
            ConditionProfile p = ConditionProfiles.For(condition);
            SaveSceneLook();

            Light sun = RenderSettings.sun;
            if (sun != null) { sun.color = Color.Lerp(sun.color, p.SunTint, 0.75f); sun.intensity = sunIntensity * p.SunIntensityScale; }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Tint(ambSky, p.AmbientTint, p.AmbientScale);
            RenderSettings.ambientEquatorColor = Tint(ambEq, p.AmbientTint, p.AmbientScale);
            RenderSettings.ambientGroundColor = Tint(ambGround, p.AmbientTint, p.AmbientScale);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = p.FogColor;
            RenderSettings.fogDensity = Mathf.Max(fogOn ? fogDensity : 0f, p.FogDensity);
            RenderSettings.reflectionIntensity = reflection * p.AmbientScale;
            TintSky(p);

            if (car != null) car.SetSurfaceGrip(p.Grip);
            follow = cameraTransform;
            if (p.Precip != Conditions.Precipitation.None) BuildPrecipitation(p);
            if (p.Headlights && car != null) BuildBeam(car.transform);
            // Lights on in the dark, rain and fog; ambience; wet or snowy road surface.
            if (car != null && (p.Headlights || condition == RoadCondition.Rain || condition == RoadCondition.Fog || condition == RoadCondition.Snow))
                UltimateCarParking.Vehicle.VehicleLights.On(car.gameObject).Headlights = true;
            Ambience(condition);
            if (condition == RoadCondition.Rain || condition == RoadCondition.Snow) Surface(condition);
        }

        // ------------------------------------------------------------ ambience and road surface

        private AudioSource ambience;
        private readonly System.Collections.Generic.List<(Renderer r, int i)> surfaced = new System.Collections.Generic.List<(Renderer, int)>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), SmoothId = Shader.PropertyToID("_Smoothness");
        public AudioClip AmbienceClip => ambience != null ? ambience.clip : null;
        public int WetSurfaces => surfaced.Count;

        private void Ambience(RoadCondition c)
        {
            Audio.AudioLibrary lib = Audio.AudioManager.Instance.Library;
            if (lib == null) return;
            AudioClip clip = c == RoadCondition.Rain ? lib.ambienceRain : c == RoadCondition.Snow || c == RoadCondition.Fog ? lib.ambienceWind : lib.ambienceCity;
            if (clip == null) return;
            if (ambience == null) { ambience = gameObject.AddComponent<AudioSource>(); ambience.loop = true; ambience.playOnAwake = false; ambience.spatialBlend = 0f; }
            ambience.clip = clip;
            float level = c == RoadCondition.Rain ? 0.32f : c == RoadCondition.Snow ? 0.22f : c == RoadCondition.Fog ? 0.12f : 0.08f;
            ambience.volume = level * Settings.SettingsService.Current.soundVolume;
            ambience.Play();
        }

        /// <summary>Wet road: darker and glossy. Snow: paler and matte. Only ground materials (road, asphalt, concrete, pavement), by property block.</summary>
        private void Surface(RoadCondition c)
        {
            var block = new MaterialPropertyBlock();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.gameObject.isStatic && r.GetComponentInParent<Rigidbody>() != null) continue;   // cars and moving props stay as they are
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !m.HasProperty(BaseColorId)) continue;
                    string n = m.name.ToLowerInvariant();
                    if (!(n.Contains("road") || n.Contains("asphalt") || n.Contains("concrete") || n.Contains("pavement") || n.Contains("ground"))) continue;
                    Color col = m.GetColor(BaseColorId);
                    block.Clear();
                    if (c == RoadCondition.Rain) { block.SetColor(BaseColorId, col * 0.72f); block.SetFloat(SmoothId, 0.82f); }
                    else { block.SetColor(BaseColorId, Color.Lerp(col, new Color(0.92f, 0.94f, 0.97f), 0.38f)); block.SetFloat(SmoothId, 0.15f); }
                    r.SetPropertyBlock(block, i);
                    surfaced.Add((r, i));
                }
            }
        }

        /// <summary>Restores the scene's own light, fog and sky and removes every effect.</summary>
        public void Clear()
        {
            if (saved)
            {
                Light sun = RenderSettings.sun;
                if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
                RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientSkyColor = ambSky; RenderSettings.ambientEquatorColor = ambEq; RenderSettings.ambientGroundColor = ambGround;
                RenderSettings.fog = fogOn; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity;
                RenderSettings.reflectionIntensity = reflection;
                RenderSettings.skybox = originalSky;
                saved = false;
            }
            if (skyCopy != null) { Destroy(skyCopy); skyCopy = null; }
            if (precip != null) { Destroy(precip.gameObject); precip = null; }
            if (beam != null) { Destroy(beam); beam = null; }
            if (ambience != null) ambience.Stop();
            var empty = new MaterialPropertyBlock();
            foreach (var (r, i) in surfaced) if (r != null) r.SetPropertyBlock(empty, i);
            surfaced.Clear();
            if (vehicle != null) vehicle.SetSurfaceGrip(1f);
            vehicle = null; follow = null;
            Active = RoadCondition.Clear;
        }

        private void OnDestroy() => Clear();

        private static Color Tint(Color baseColor, Color tint, float scale) => new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, 1f) * scale;

        private void SaveSceneLook()
        {
            Light sun = RenderSettings.sun;
            sunColor = sun != null ? sun.color : Color.white;
            sunIntensity = sun != null ? sun.intensity : 1f;
            ambMode = RenderSettings.ambientMode;
            ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor;
            fogOn = RenderSettings.fog; fogMode = RenderSettings.fogMode; fogColor = RenderSettings.fogColor; fogDensity = RenderSettings.fogDensity;
            reflection = RenderSettings.reflectionIntensity;
            originalSky = RenderSettings.skybox;
            saved = true;
        }

        private void TintSky(ConditionProfile p)
        {
            if (originalSky == null) return;
            skyCopy = new Material(originalSky) { name = originalSky.name + " (condition)" };
            if (skyCopy.HasProperty("_Exposure")) skyCopy.SetFloat("_Exposure", originalSky.GetFloat("_Exposure") * p.SkyExposure);
            if (skyCopy.HasProperty("_SkyTint")) skyCopy.SetColor("_SkyTint", p.SkyTint);
            if (skyCopy.HasProperty("_GroundColor")) skyCopy.SetColor("_GroundColor", p.SkyTint * 0.6f);
            if (skyCopy.HasProperty("_SunSize")) skyCopy.SetFloat("_SunSize", 0.0f);
            RenderSettings.skybox = skyCopy;
        }

        // ------------------------------------------------------------ precipitation

        private void BuildPrecipitation(ConditionProfile p)
        {
            Material mat = Resources.Load<Material>(p.Precip == Conditions.Precipitation.Rain ? RainMaterial : SnowMaterial);
            if (mat == null) { Debug.LogWarning("[Conditions] precipitation material missing (Resources/Conditions); skipping the effect."); return; }

            var go = new GameObject(p.Precip == Conditions.Precipitation.Rain ? "Rain" : "Snow");
            go.transform.SetParent(transform, false);
            precip = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            bool rain = p.Precip == Conditions.Precipitation.Rain;
            var main = precip.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true; main.playOnAwake = true;
            main.startLifetime = rain ? 0.9f : 5f;
            main.startSpeed = 0f;
            main.startSize = rain ? new ParticleSystem.MinMaxCurve(0.03f, 0.05f) : new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startColor = rain ? new Color(0.80f, 0.86f, 0.95f, 0.45f) : new Color(1f, 1f, 1f, 0.85f);
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.RoundToInt(p.PrecipRate * (rain ? 1.2f : 6f) * PrecipitationScale) + 16;

            var emission = precip.emission;
            emission.rateOverTime = p.PrecipRate * PrecipitationScale;

            var shape = precip.shape;
            // A flat disc above the camera with an empty middle: nothing falls within a few metres of the lens (those would be huge smears).
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 18f;
            shape.radiusThickness = 0.72f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var vel = precip.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(rain ? -1.5f : -0.6f, rain ? -0.5f : 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(rain ? -24f : -3.0f, rain ? -20f : -2.2f);   // all three axes use the same (random between two constants) mode
            vel.z = new ParticleSystem.MinMaxCurve(rain ? -0.5f : -0.6f, rain ? 0.5f : 0.6f);

            if (rain)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.05f;
                r.lengthScale = 6f;
            }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            precip.Play();
            PositionPrecipitation();
        }

        private void LateUpdate() => PositionPrecipitation();

        private void PositionPrecipitation()
        {
            if (precip == null || follow == null) return;
            precip.transform.position = follow.position + Vector3.up * 13f;
        }

        // ------------------------------------------------------------ headlight beam

        /// <summary>Local height of the road under the car (bottom of the wheels), so the beam lies on the ground, not in the air.</summary>
        private static float RoadHeightLocal(Transform car)
        {
            float y = -0.3f;
            foreach (WheelCollider w in car.GetComponentsInChildren<WheelCollider>())
                y = Mathf.Min(y, car.InverseTransformPoint(w.transform.position).y - w.radius);
            return y;
        }

        private void BuildBeam(Transform car)
        {
            Material mat = Resources.Load<Material>(BeamMaterial);
            if (mat == null) { Debug.LogWarning("[Conditions] beam material missing (Resources/Conditions); skipping headlights."); return; }
            beam = new GameObject("HeadlightBeam");
            beam.transform.SetParent(car, false);
            beam.transform.localPosition = new Vector3(0f, RoadHeightLocal(car) + 0.06f, 0f);
            var mesh = new Mesh { name = "BeamMesh" };
            // A flat trapezoid on the road ahead of the car: narrow near the bumper, wide and fading far away.
            mesh.vertices = new[] { new Vector3(-0.9f, 0f, 1.8f), new Vector3(0.9f, 0f, 1.8f), new Vector3(3.6f, 0f, 17f), new Vector3(-3.6f, 0f, 17f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };   // both faces: visible whichever way the road tilts
            mesh.RecalculateBounds();
            beam.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = beam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
