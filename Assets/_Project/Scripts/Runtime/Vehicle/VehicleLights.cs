using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Audio;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Vehicle
{
    public enum IndicatorMode { Off, Left, Right, Hazard }

    /// <summary>
    /// Working lights for any garage car: headlights, tail / brake lights, reverse lights and amber indicators with
    /// hazards, plus the horn and engine start / stop. The models bake their lamps into one lights-and-glass material,
    /// so each lamp is a small glow card placed at the car's measured corners (one shared additive material, colours by
    /// MaterialPropertyBlock: no material instances). Brake and reverse lights follow the real controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleLights : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private const float BlinkHz = 1.4f;

        private VehicleController car;
        private readonly List<Renderer> heads = new List<Renderer>(), tails = new List<Renderer>(), reverses = new List<Renderer>(),
            leftInd = new List<Renderer>(), rightInd = new List<Renderer>();
        private MaterialPropertyBlock block;
        private AudioSource horn, tick;
        private bool lastBlinkOn;

        public bool Headlights { get; set; }
        public IndicatorMode Indicators { get; set; }
        public bool EngineOn => car == null || car.EngineOn;
        public Color HeadlightColor { get; set; } = new Color(1f, 0.92f, 0.78f);
        public bool Braking { get; private set; }
        public bool Reversing { get; private set; }
        public bool BlinkOn { get; private set; }
        public bool HornOn => horn != null && horn.isPlaying;
        public int LampCount => heads.Count + tails.Count + reverses.Count + leftInd.Count + rightInd.Count;

        public static VehicleLights On(GameObject go) => go.TryGetComponent(out VehicleLights l) ? l : go.AddComponent<VehicleLights>();

        private void Awake()
        {
            car = GetComponent<VehicleController>();
            block = new MaterialPropertyBlock();
            Build();
            AudioLibrary lib = AudioManager.Instance.Library;
            horn = Source(lib != null ? lib.horn : null, loop: true);
            tick = Source(lib != null ? lib.indicatorTick : null, loop: false);
        }

        private AudioSource Source(AudioClip clip, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        // ---------------------------------------------------------------- lamps

        private void Build()
        {
            Material mat = Resources.Load<Material>("Customization/LightGlow");
            if (mat == null) return;
            Bounds b = VisualBounds();
            float halfW = b.extents.x, y = Mathf.Lerp(b.min.y, b.max.y, 0.42f);
            float front = b.max.z + 0.01f, rear = b.min.z - 0.01f;
            float lampX = Mathf.Max(0.25f, halfW - 0.28f), indX = Mathf.Max(0.3f, halfW - 0.08f);
            for (int side = -1; side <= 1; side += 2)
            {
                heads.Add(Lamp(mat, new Vector3(side * lampX, y, front), 0f, 0.34f));
                tails.Add(Lamp(mat, new Vector3(side * lampX, y + 0.04f, rear), 180f, 0.30f));
                reverses.Add(Lamp(mat, new Vector3(side * Mathf.Max(0.15f, lampX - 0.22f), y - 0.03f, rear), 180f, 0.18f));
                (side < 0 ? leftInd : rightInd).Add(Lamp(mat, new Vector3(side * indX, y - 0.06f, front), 0f, 0.2f));
                (side < 0 ? leftInd : rightInd).Add(Lamp(mat, new Vector3(side * indX, y - 0.02f, rear), 180f, 0.2f));
            }
            Render(true);
        }

        private Renderer Lamp(Material mat, Vector3 local, float yaw, float size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Lamp";
            DestroyImmediate(q.GetComponent<Collider>());   // now: a concave collider under a moving car errors even for one frame
            q.transform.SetParent(transform, false);
            q.transform.localPosition = local;
            q.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);   // quad faces -Z by default: point it outward
            q.transform.localScale = new Vector3(size, size, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private Bounds VisualBounds()
        {
            var b = new Bounds(Vector3.zero, Vector3.one); bool any = false;
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name == "Lamp" || r.name == "PlateBack") continue;
                Bounds lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ---------------------------------------------------------------- controls

        public void ToggleHeadlights() => Headlights = !Headlights;
        public void ToggleIndicator(IndicatorMode m) => Indicators = Indicators == m ? IndicatorMode.Off : m;

        public void SetHorn(bool on)
        {
            if (horn == null || horn.clip == null) return;
            if (on && !horn.isPlaying && EngineOn) { horn.volume = 0.55f * SettingsService.Current.soundVolume; horn.Play(); }
            else if (!on && horn.isPlaying) horn.Stop();
        }

        /// <summary>Engine start / stop. Off: no drive, engine sound stops, lights off (hazards still work).</summary>
        public void ToggleEngine()
        {
            if (car == null) return;
            car.EngineOn = !car.EngineOn;
            if (car.EngineOn) AudioManager.Instance.Play(Sfx.EngineStart, 0.8f);
            else SetHorn(false);
        }

        private void Update()
        {
            if (car == null) return;
            VehicleInput input = car.Input;
            float thr = input != null ? input.Throttle : 0f, brk = input != null ? input.Brake : 0f;
            Braking = car.ControlsEnabled && (brk > 0.1f || (car.ForwardSpeedKmh > 1f && thr < -0.1f) || (car.ForwardSpeedKmh < -1f && thr > 0.1f));
            Reversing = car.ControlsEnabled && EngineOn && (car.IsReversing || thr < -0.1f && car.ForwardSpeedKmh < 1f);
            BlinkOn = Indicators != IndicatorMode.Off && Mathf.Repeat(Time.time * BlinkHz, 1f) < 0.5f;
            if (BlinkOn != lastBlinkOn)
            {
                lastBlinkOn = BlinkOn;
                if (tick != null && tick.clip != null) tick.PlayOneShot(tick.clip, 0.4f * SettingsService.Current.soundVolume);
            }
            Render(false);
        }

        private void Render(bool force)
        {
            bool engine = EngineOn;
            Paint(heads, engine && Headlights ? HeadlightColor * 1.6f : HeadlightColor * 0.0f);
            Color red = new Color(1f, 0.08f, 0.06f);
            float tail = !engine ? 0f : Braking ? 1.6f : Headlights ? 0.55f : 0f;
            Paint(tails, red * tail);
            Paint(reverses, Reversing ? new Color(1f, 1f, 0.95f) * 1.2f : Color.clear);
            Color amber = new Color(1f, 0.55f, 0.05f) * 1.5f;
            bool left = BlinkOn && (Indicators == IndicatorMode.Left || Indicators == IndicatorMode.Hazard);
            bool right = BlinkOn && (Indicators == IndicatorMode.Right || Indicators == IndicatorMode.Hazard);
            Paint(leftInd, left ? amber : Color.clear);
            Paint(rightInd, right ? amber : Color.clear);
        }

        private void Paint(List<Renderer> lamps, Color c)
        {
            bool on = c.maxColorComponent > 0.01f;
            foreach (Renderer r in lamps)
            {
                if (r == null) continue;
                if (r.enabled != on) r.enabled = on;
                if (!on) continue;
                block.SetColor(ColorId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
