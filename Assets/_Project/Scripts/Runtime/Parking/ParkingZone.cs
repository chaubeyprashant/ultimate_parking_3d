using UnityEngine;

namespace UltimateCarParking.Parking
{
    /// <summary>
    /// The target bay: a trigger box that tracks whether the player's car overlaps it, plus four marker strips
    /// that change colour with the parking status. Built at runtime by the LevelLoader from LevelData, or placed
    /// by hand (then call <see cref="Configure"/> or set Size in the Inspector).
    ///
    /// The trigger is the cheap "is the car anywhere near" test; exact containment of the car's footprint is
    /// checked by <see cref="ParkingValidator"/> with <see cref="ParkingGeometry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkingZone : MonoBehaviour
    {
        [Tooltip("Bay width (local X) and length (local Z) in metres. Local +Z is the bay's 'forward' (nose-in direction).")]
        [SerializeField] private Vector2 size = new Vector2(2.8f, 5.5f);
        [Tooltip("Height of the trigger volume.")]
        [SerializeField, Min(0.5f)] private float triggerHeight = 3f;

        [Header("Markers")]
        [SerializeField] private Material markerMaterial;
        [SerializeField, Min(0.02f)] private float markerWidth = 0.15f;
        [SerializeField] private Color idleColor = new Color(1f, 0.78f, 0.1f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.45f, 0.05f);
        [SerializeField] private Color holdingColor = new Color(0.55f, 0.95f, 0.35f);
        [SerializeField] private Color parkedColor = new Color(0.1f, 0.85f, 0.25f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private BoxCollider trigger;
        private Renderer[] markers = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private Rigidbody trackedBody;
        private int overlapCount;

        public Vector2 Size => size;
        public Vector3 Center => transform.position;
        public Quaternion Rotation => transform.rotation;
        public Vector3 Forward => transform.forward;

        /// <summary>True while any collider of the tracked vehicle overlaps the trigger.</summary>
        public bool IsOccupied => trackedBody != null && overlapCount > 0;

        private void Awake()
        {
            if (trigger == null) Configure(size, markerMaterial);
        }

        /// <summary>(Re)builds the trigger and the marker strips for a bay of the given size.</summary>
        public void Configure(Vector2 baySize, Material markers)
        {
            size = new Vector2(Mathf.Max(0.5f, baySize.x), Mathf.Max(0.5f, baySize.y));
            if (markers != null) markerMaterial = markers;

            if (trigger == null && !TryGetComponent(out trigger)) trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, triggerHeight, size.y);
            trigger.center = new Vector3(0f, triggerHeight * 0.5f, 0f);

            BuildMarkers();
            ShowStatus(ParkingStatus.Inactive);
        }

        /// <summary>Only this body's colliders count as occupying the bay (the player car).</summary>
        public void Track(Rigidbody body)
        {
            trackedBody = body;
            overlapCount = 0;   // re-counted by the next trigger callbacks (a body already inside gets OnTriggerEnter next step)
        }

        public void ShowStatus(ParkingStatus status)
        {
            Color c = status switch
            {
                ParkingStatus.Parked => parkedColor,
                ParkingStatus.Holding => holdingColor,
                ParkingStatus.PartiallyInside or ParkingStatus.Misaligned or ParkingStatus.TooFast => occupiedColor,
                _ => idleColor,
            };
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            foreach (Renderer r in markers)
                if (r != null) r.SetPropertyBlock(block);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (trackedBody != null && other.attachedRigidbody == trackedBody) overlapCount++;
        }

        private void OnTriggerExit(Collider other)
        {
            if (trackedBody != null && other.attachedRigidbody == trackedBody) overlapCount = Mathf.Max(0, overlapCount - 1);
        }

        private void BuildMarkers()
        {
            foreach (Renderer r in markers)
                if (r != null) DestroyMarker(r.gameObject);

            float hw = size.x * 0.5f - markerWidth * 0.5f, hl = size.y * 0.5f - markerWidth * 0.5f;
            markers = new[]
            {
                Strip("MarkerLeft", new Vector3(-hw, 0f, 0f), new Vector3(markerWidth, 0.012f, size.y)),
                Strip("MarkerRight", new Vector3(hw, 0f, 0f), new Vector3(markerWidth, 0.012f, size.y)),
                Strip("MarkerFront", new Vector3(0f, 0f, hl), new Vector3(size.x, 0.012f, markerWidth)),
                Strip("MarkerBack", new Vector3(0f, 0f, -hl), new Vector3(size.x, 0.012f, markerWidth)),
            };
        }

        private Renderer Strip(string stripName, Vector3 localPos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = stripName;
            DestroyMarker(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos + Vector3.up * 0.012f;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (markerMaterial != null) r.sharedMaterial = markerMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        private static void DestroyMarker(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, 0.05f, 0f), new Vector3(size.x, 0.1f, size.y));
            Gizmos.DrawWireCube(new Vector3(0f, triggerHeight * 0.5f, 0f), new Vector3(size.x, triggerHeight, size.y));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size.y * 0.5f);
        }
    }
}
