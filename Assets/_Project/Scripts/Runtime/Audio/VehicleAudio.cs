using UnityEngine;
using UltimateCarParking.Settings;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Audio
{
    /// <summary>
    /// The player car's sound: an engine loop whose pitch follows speed and throttle, a brake squeal when the car is
    /// braked hard from speed, and an impact sound on every counted collision (volume by impact speed).
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    public sealed class VehicleAudio : MonoBehaviour
    {
        [SerializeField] private float idlePitch = 0.75f;
        [SerializeField] private float maxPitch = 2.1f;
        [SerializeField, Range(0f, 1f)] private float idleVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 0.55f;
        [SerializeField, Min(0f)] private float brakeSquealMinKmh = 18f;
        [SerializeField, Min(0f)] private float brakeSquealCooldown = 1.2f;

        private VehicleController vehicle;
        private VehicleInput input;
        private CollisionHandler collisions;
        private AudioSource engine;
        private float lastSqueal = -10f;
        private bool wasBraking;

        public AudioSource Engine => engine;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            input = GetComponent<VehicleInput>();
            collisions = GetComponent<CollisionHandler>();
            engine = gameObject.AddComponent<AudioSource>();
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (collisions != null) collisions.PenaltyRegistered += OnPenalty;
            AudioLibrary lib = AudioManager.Instance.Library;
            if (lib != null && lib.engineLoop != null)
            {
                engine.clip = lib.engineLoop;
                engine.Play();
            }
        }

        private void OnDisable()
        {
            if (collisions != null) collisions.PenaltyRegistered -= OnPenalty;
            if (engine != null) engine.Stop();
        }

        private void OnPenalty(CollisionHandler.Penalty p)
        {
            float v = Mathf.InverseLerp(0.3f, 6f, p.ImpactSpeed);
            AudioManager.Instance.Play(Sfx.Collision, Mathf.Lerp(0.45f, 1f, v), Random.Range(0.92f, 1.08f));
        }

        private void Update()
        {
            float speed01 = Mathf.Clamp01(vehicle.SpeedKmh / Mathf.Max(1f, vehicle.MaxForwardSpeedKmh));
            float throttle = input != null && vehicle.ControlsEnabled ? Mathf.Abs(input.Throttle) : 0f;
            float pitchTarget = Mathf.Lerp(idlePitch, maxPitch, speed01) + throttle * 0.18f;
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            engine.pitch = Mathf.Lerp(engine.pitch, pitchTarget, k);
            engine.volume = Mathf.Lerp(idleVolume, maxVolume, Mathf.Max(speed01, throttle)) * SettingsService.Current.soundVolume;

            bool braking = input != null && vehicle.ControlsEnabled &&
                           (input.Brake > 0.5f || input.Handbrake || (vehicle.ForwardSpeedKmh > 2f && input.Throttle < -0.5f) ||
                            (vehicle.ForwardSpeedKmh < -2f && input.Throttle > 0.5f));
            if (braking && !wasBraking && vehicle.SpeedKmh >= brakeSquealMinKmh && Time.time - lastSqueal > brakeSquealCooldown)
            {
                lastSqueal = Time.time;
                AudioManager.Instance.Play(Sfx.Brake, Mathf.InverseLerp(brakeSquealMinKmh, 60f, vehicle.SpeedKmh) * 0.6f + 0.3f);
            }
            wasBraking = braking;
        }
    }
}
