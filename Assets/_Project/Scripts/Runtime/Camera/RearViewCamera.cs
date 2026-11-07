using UnityEngine;
using UnityEngine.Rendering.Universal;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.CameraSystem
{
    /// <summary>
    /// Backup camera: a wide camera at the rear bumper looking back and down, rendering into a small texture that the
    /// HUD shows while the car is reversing (or the player is holding reverse). Off the rest of the time, so it costs
    /// nothing while driving forwards. Also reports the predicted reverse path (from the steering angle and the real
    /// wheelbase) and the free distance behind the bumper, for the guide-line overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RearViewCamera : MonoBehaviour
    {
        [SerializeField] private Vector2Int resolution = new Vector2Int(640, 360);
        [SerializeField, Range(40f, 110f)] private float fieldOfView = 78f;
        [SerializeField, Range(0f, 60f)] private float pitchDown = 30f;
        [Tooltip("Seconds the view stays up after the car stops reversing.")]
        [SerializeField, Min(0f)] private float linger = 1.5f;
        [Tooltip("Mirror the image left-right, like a real car's backup camera (matches the mirrors).")]
        [SerializeField] private bool mirrored = true;

        private VehicleController car;
        private Camera cam;
        private RenderTexture texture;
        private float lastReverseTime = -99f;
        private float rearAxleZ, frontAxleZ, halfTrack, bumperZ, groundY, roofY;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        public bool Active { get; private set; }
        public RenderTexture Texture => texture;
        public Camera Camera => cam;
        public bool Mirrored => mirrored;
        public VehicleController Car => car;
        /// <summary>Free distance behind the rear bumper (∞ when nothing within 4 m).</summary>
        public float RearDistance { get; private set; } = float.PositiveInfinity;
        public float Wheelbase => frontAxleZ - rearAxleZ;
        public float HalfTrack => halfTrack;
        /// <summary>Distance from the rear axle back to the bumper, metres.</summary>
        public float AxleToBumper => rearAxleZ - bumperZ;
        public float GroundY => groundY;
        /// <summary>Set by tests / settings: force the view on regardless of gear.</summary>
        public bool ForceOn { get; set; }

        /// <summary>Attaches to a car (call after spawning). Measures the car and creates the camera.</summary>
        public void Attach(VehicleController vehicle)
        {
            Detach();
            car = vehicle;
            if (car == null) return;
            Measure();
            var go = new GameObject("RearViewCamera");
            go.transform.SetParent(car.transform, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Clamp(roofY * 0.62f, 0.7f, 1.3f), bumperZ + 0.05f);
            go.transform.localRotation = Quaternion.Euler(pitchDown, 180f, 0f);
            cam = go.AddComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.depth = -10f;
            texture = new RenderTexture(resolution.x, resolution.y, 16, RenderTextureFormat.ARGB32) { name = "RearView", antiAliasing = 1 };
            cam.targetTexture = texture;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            cam.enabled = false;
        }

        public void Detach()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
            cam = null;
            texture = null;
            car = null;
            Active = false;
        }

        private void OnDestroy() => Detach();

        private void Measure()
        {
            Transform t = car.transform;
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            rearAxleZ = float.MaxValue; frontAxleZ = float.MinValue; halfTrack = 0.8f; groundY = 0f;
            foreach (WheelCollider w in wheels)
            {
                Vector3 p = t.InverseTransformPoint(w.transform.TransformPoint(w.center));
                rearAxleZ = Mathf.Min(rearAxleZ, p.z);
                frontAxleZ = Mathf.Max(frontAxleZ, p.z);
                halfTrack = Mathf.Max(halfTrack, Mathf.Abs(p.x));
                groundY = Mathf.Min(groundY, p.y - w.radius);
            }
            if (wheels.Length == 0) { rearAxleZ = -1.4f; frontAxleZ = 1.4f; }
            bumperZ = rearAxleZ - 0.9f; roofY = 1.4f;
            bool first = true;
            foreach (Collider c in car.GetComponentsInChildren<Collider>())
            {
                if (c is WheelCollider || c.isTrigger) continue;
                Bounds b = c.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 l = t.InverseTransformPoint(corner);
                    if (first) { bumperZ = l.z; roofY = l.y; first = false; }
                    bumperZ = Mathf.Min(bumperZ, l.z);
                    roofY = Mathf.Max(roofY, l.y);
                }
            }
        }

        private void LateUpdate()
        {
            if (car == null) { Active = false; return; }
            bool wantsReverse = car.ControlsEnabled && (car.IsReversing || (car.Input != null && car.Input.Throttle < -0.1f && car.ForwardSpeedKmh < 1.5f));
            if (wantsReverse) lastReverseTime = Time.time;
            Active = ForceOn || (car.ControlsEnabled && Time.time - lastReverseTime <= linger);
            if (cam != null) cam.enabled = Active;
            if (Active) RearDistance = MeasureRear();
        }

        private float MeasureRear()
        {
            Transform t = car.transform;
            Vector3 origin = t.TransformPoint(new Vector3(0f, 0.55f, bumperZ + 0.3f));
            int n = Physics.BoxCastNonAlloc(origin, new Vector3(halfTrack, 0.3f, 0.05f), -t.forward, hits, t.rotation, 4.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].rigidbody == car.Body || hits[i].distance <= 0f) continue;
                best = Mathf.Min(best, hits[i].distance - 0.3f);
            }
            return best > 4f ? float.PositiveInfinity : Mathf.Max(0f, best);
        }

        /// <summary>
        /// A point on the predicted reverse path, in car-local space: <paramref name="side"/> −1 / 0 / +1 = left wheel
        /// track / centre / right wheel track, <paramref name="behindBumper"/> metres travelled back past the bumper.
        /// Uses the current steering angle and a circle about the rear axle (kinematic bicycle model).
        /// </summary>
        public Vector3 PathPoint(float behindBumper, float side)
        {
            float steer = car != null ? car.SteerAngle : 0f;
            float s = behindBumper + AxleToBumper;                          // distance travelled by the rear axle
            float L = Mathf.Max(1f, Wheelbase);
            float k = Mathf.Tan(steer * Mathf.Deg2Rad) / L;                 // curvature, +right
            Vector3 c, tangent;
            if (Mathf.Abs(k) < 1e-4f) { c = new Vector3(0f, 0f, -s); tangent = Vector3.back; }
            else
            {
                float R = 1f / k, a = s * k;                                 // reversing: rear swings toward the steer side
                c = new Vector3(R * (1f - Mathf.Cos(a)), 0f, -R * Mathf.Sin(a));
                tangent = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            }
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;  // tangent points back, so this is the car's left…
            Vector3 p = c - right * side * halfTrack;                        // …hence the minus: side +1 = car's right
            return new Vector3(p.x, groundY + 0.02f, rearAxleZ + p.z);
        }
    }
}
