using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>
    /// Look and shell of an environment family (city lot, multi-storey garage, construction yard): ground,
    /// perimeter wall module, lighting and theme-wide decoration. Levels add their own props on top.
    /// </summary>
    [CreateAssetMenu(fileName = "Theme_", menuName = "Ultimate Car Parking/Environment Theme", order = 20)]
    public sealed class EnvironmentTheme : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string themeId = "city";
        [SerializeField] private string displayName = "City Lot";
        [Tooltip("Accent used by menus for this theme.")]
        [SerializeField] private Color uiColor = new Color(0.71f, 1f, 0.18f);

        [Header("Shell")]
        [Tooltip("Drivable area (X, Z) in metres, centred on the origin.")]
        [SerializeField] private Vector2 lotSize = new Vector2(64f, 52f);
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material outerGroundMaterial;
        [Tooltip("Wall module repeated around the lot edge.")]
        [SerializeField] private GameObject perimeterWallPrefab;
        [SerializeField, Min(0.5f)] private float wallSegmentLength = 4f;
        [Tooltip("Props every level of this theme gets (light poles, beams, ramps…).")]
        [SerializeField] private PropPlacement[] decoration = System.Array.Empty<PropPlacement>();

        [Header("Lighting")]
        [SerializeField] private Color sunColor = new Color(1f, 0.96f, 0.9f);
        [SerializeField, Min(0f)] private float sunIntensity = 1.25f;
        [SerializeField] private Vector3 sunEuler = new Vector3(50f, -35f, 0f);
        [SerializeField] private Color ambientSky = new Color(0.62f, 0.68f, 0.78f);
        [SerializeField] private Color ambientEquator = new Color(0.45f, 0.46f, 0.47f);
        [SerializeField] private Color ambientGround = new Color(0.22f, 0.21f, 0.2f);
        [SerializeField] private bool fog;
        [SerializeField] private Color fogColor = new Color(0.6f, 0.65f, 0.7f);
        [SerializeField, Min(0f)] private float fogDensity = 0.01f;

        public string ThemeId => themeId;
        public string DisplayName => displayName;
        public Color UiColor => uiColor;
        public Vector2 LotSize => lotSize;
        public Material GroundMaterial => groundMaterial;
        public Material OuterGroundMaterial => outerGroundMaterial;
        public GameObject PerimeterWallPrefab => perimeterWallPrefab;
        public float WallSegmentLength => wallSegmentLength;
        public PropPlacement[] Decoration => decoration;
        public Color SunColor => sunColor;
        public float SunIntensity => sunIntensity;
        public Quaternion SunRotation => Quaternion.Euler(sunEuler);
        public Color AmbientSky => ambientSky;
        public Color AmbientEquator => ambientEquator;
        public Color AmbientGround => ambientGround;
        public bool Fog => fog;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
    }
}
