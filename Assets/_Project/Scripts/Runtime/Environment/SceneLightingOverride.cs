using UnityEngine;
using UnityEngine.Rendering;

namespace UltimateCarParking.Environment
{
    /// <summary>
    /// Put on an environment prefab: when it spawns, sets the scene's ambient light, fog and sun for that place
    /// (an enclosed garage is far darker and warmer than an open lot). Applied once, no per-frame cost.
    /// </summary>
    public sealed class SceneLightingOverride : MonoBehaviour
    {
        [SerializeField] private Color ambientSky = new Color(0.36f, 0.37f, 0.39f);
        [SerializeField] private Color ambientEquator = new Color(0.30f, 0.30f, 0.31f);
        [SerializeField] private Color ambientGround = new Color(0.16f, 0.16f, 0.17f);
        [SerializeField] private bool fog = true;
        [SerializeField] private Color fogColor = new Color(0.18f, 0.19f, 0.21f);
        [SerializeField, Min(0f)] private float fogDensity = 0.012f;
        [SerializeField] private Color sunColor = new Color(1f, 0.95f, 0.88f);
        [SerializeField, Min(0f)] private float sunIntensity = 1.1f;
        [SerializeField] private Vector3 sunEuler = new Vector3(55f, -30f, 0f);
        [SerializeField] private float reflectionIntensity = 0.5f;

        private void Awake()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.transform.rotation = Quaternion.Euler(sunEuler);
            }
        }
    }
}
