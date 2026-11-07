using UnityEngine;
using UltimateCarParking.Audio;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Parking-sensor beeps: faster as the obstacle gets closer, continuous under 0.3 m, silent beyond 1.6 m.
    /// The beep is synthesised once (no audio asset). Scaled time, so pausing silences it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ParkingSensorAudio : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float startDistance = 1.6f;
        [SerializeField, Min(0f)] private float solidDistance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        private AudioSource source;
        private AudioClip beep;
        private float distance = float.PositiveInfinity;
        private float timer;

        public int BeepCount { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            beep = MakeBeep();
        }

        /// <summary>Distance to report this frame. +∞ (or never called) = silent.</summary>
        public void SetDistance(float metres) => distance = metres;

        public void Silence()
        {
            distance = float.PositiveInfinity;
            if (source != null) source.Stop();
        }

        private void Update()
        {
            if (distance > startDistance) { timer = 0f; return; }
            float interval = distance <= solidDistance ? 0.09f : Mathf.Lerp(0.12f, 0.6f, Mathf.InverseLerp(solidDistance, startDistance, distance));
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            source.pitch = distance <= solidDistance ? 1.15f : 1f;
            source.PlayOneShot(beep, volume * AudioManager.Instance.SoundVolume);
            BeepCount++;
        }

        private static AudioClip MakeBeep()
        {
            const int rate = 22050;
            int n = rate * 7 / 100;   // 70 ms
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 400f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.6f * env;
            }
            AudioClip clip = AudioClip.Create("SensorBeep", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
