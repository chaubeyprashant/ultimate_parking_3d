using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// World-space guidance: a lime chevron hovering above the target bay (bobbing gently), recoloured by the
    /// parking status and hidden once parked. Created per level by the LevelLoader's zone; one light Update.
    /// </summary>
    public sealed class ParkingGuide : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private LevelLoader loader;
        [SerializeField] private Material chevronMaterial;
        [SerializeField] private float height = 2.6f;
        [SerializeField] private float bob = 0.18f;

        private Transform chevron;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public Transform Chevron => chevron;

        private void OnEnable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted += OnStarted;
            if (levelManager.Validator != null) levelManager.Validator.StatusChanged += OnStatus;
            if (levelManager.Mission != null) levelManager.Mission.ActiveZoneChanged += OnZone;
        }

        private void OnDisable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted -= OnStarted;
            if (levelManager.Validator != null) levelManager.Validator.StatusChanged -= OnStatus;
            if (levelManager.Mission != null) levelManager.Mission.ActiveZoneChanged -= OnZone;
        }

        private void OnStarted(LevelData _)
        {
            if (loader != null) OnZone(loader.Zone);
        }

        /// <summary>Follows the current objective's bay (multi-stage missions move it).</summary>
        private void OnZone(ParkingZone zone)
        {
            if (zone == null) return;
            if (chevron == null) Build();
            chevron.SetParent(zone.transform, false);
            chevron.gameObject.SetActive(true);
            Color(UiPalette.Lime);
        }

        private void Build()
        {
            chevron = new GameObject("GuideChevron").transform;
            block = new MaterialPropertyBlock();
            // Two thin slabs in a V, pointing down at the bay.
            var parts = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(slab.GetComponent<Collider>());
                slab.transform.SetParent(chevron, false);
                slab.transform.localScale = new Vector3(0.18f, 0.9f, 0.18f);
                slab.transform.localPosition = new Vector3(i == 0 ? -0.26f : 0.26f, 0.36f, 0f);
                slab.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 35f : -35f);
                var r = slab.GetComponent<Renderer>();
                if (chevronMaterial != null) r.sharedMaterial = chevronMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts[i] = r;
            }
            renderers = parts;
        }

        private void OnStatus(ParkingStatus status)
        {
            if (chevron == null) return;
            chevron.gameObject.SetActive(status != ParkingStatus.Parked && status != ParkingStatus.Inactive);
            Color(status == ParkingStatus.Holding ? UiPalette.Lime : status == ParkingStatus.Outside ? UiPalette.Lime : UiPalette.Gold);
        }

        private void Color(Color c)
        {
            if (renderers == null) return;
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * 1.5f);
            foreach (Renderer r in renderers) r.SetPropertyBlock(block);
        }

        private void Update()
        {
            if (chevron == null || !chevron.gameObject.activeSelf) return;
            float t = Time.time;
            chevron.localPosition = new Vector3(0f, height + Mathf.Sin(t * 2.4f) * bob, 0f);
            chevron.localRotation = Quaternion.Euler(0f, t * 40f, 0f);
        }
    }
}
