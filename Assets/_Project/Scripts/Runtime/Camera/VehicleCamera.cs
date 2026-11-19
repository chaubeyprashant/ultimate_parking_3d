using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.CameraSystem
{
    /// <summary>The driving camera views (VIEW button cycles them).</summary>
    public enum CameraView { Chase = 0, TopDown = 1, Bumper = 2, Cockpit = 3, Orbit = 4 }

    /// <summary>
    /// Driving camera with five views:
    /// <list type="bullet">
    /// <item>Chase: behind and above the car's flattened heading, SmoothDamp follow, pulls in when a wall is in the way.</item>
    /// <item>Top-down: high overhead, slightly behind, for lining up bays.</item>
    /// <item>Bumper: low at the front bumper looking ahead.</item>
    /// <item>Cockpit: driver's eye inside the car (only cars with a modelled interior).</item>
    /// <item>Orbit: free orbit around the car, one-finger drag on open screen to turn it.</item>
    /// </list>
    /// Distance and height scale with the car's measured size, so a monster truck and a hatchback are framed alike.
    /// Pinch zoom (two fingers on open screen) and the mouse wheel change the chase / orbit distance within 4–14 m.
    /// Switching views blends over a short transition. Touches that start on UI are ignored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The car to follow. Left empty: the first VehicleController in the scene is used.")]
        [SerializeField] private Transform target;
        [SerializeField] private bool findTargetIfMissing = true;

        [Header("Framing")]
        [SerializeField, Min(0.5f)] private float distance = 7f;
        [SerializeField, Min(0.5f)] private float minDistance = 4f;
        [SerializeField, Min(0.5f)] private float maxDistance = 14f;
        [Tooltip("Camera height above the car's pivot.")]
        [SerializeField] private float height = 3f;
        [Tooltip("Height above the car's pivot that the camera looks at.")]
        [SerializeField] private float lookHeight = 1f;

        [Header("Smoothing")]
        [Tooltip("SmoothDamp time for position. Lower = tighter.")]
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.18f;
        [Tooltip("How quickly the camera turns toward the look point.")]
        [SerializeField, Min(0.1f)] private float rotationSharpness = 10f;
        [Tooltip("How quickly the follow heading catches up with the car's heading.")]
        [SerializeField, Min(0.1f)] private float headingSharpness = 4f;

        [Header("Zoom")]
        [SerializeField] private bool mouseWheelZoom = true;
        [Tooltip("Distance change per mouse-wheel notch.")]
        [SerializeField, Min(0.01f)] private float zoomStep = 0.8f;

        [Header("Obstacles")]
        [SerializeField] private bool avoidObstacles = true;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.3f;

        [Header("Top-down view")]
        [SerializeField, Min(4f)] private float topHeight = 15f;
        [SerializeField] private float topBack = 2.5f;

        [Header("Views")]
        [SerializeField, Range(30f, 90f)] private float chaseFov = 55f;
        [SerializeField, Range(40f, 100f)] private float firstPersonFov = 68f;
        [Tooltip("Seconds a view change blends over.")]
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.35f;
        [Tooltip("Orbit: degrees per screen pixel of drag.")]
        [SerializeField, Min(0.01f)] private float orbitDegreesPerPixel = 0.25f;

        private readonly RaycastHit[] hits = new RaycastHit[8];
        private Rigidbody targetBody;
        private Vector3 velocity;
        private Vector3 heading = Vector3.forward;
        private float nextSearchTime;
        private float shakeAmplitude;
        private float shakeUntil;
        private float shakeDuration;
        private Camera cam;

        private CameraView view = CameraView.Chase;
        private float transitionLeft;
        private Vector3 fromPos;
        private Quaternion fromRot;
        private float orbitYaw, orbitPitch = 22f;

        // Car measurements (local space), refreshed on SetTarget.
        private float carLength = 4.4f, carHeight = 1.5f, carWidth = 1.8f, frontZ = 2.2f, centreZ;
        private bool hasInterior;
        private CockpitMount cockpit;

        // Touch gestures.
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);
        private readonly Dictionary<int, bool> touchOnUi = new Dictionary<int, bool>(4);
        private float lastPinch = -1f;

        public Transform Target => target;
        public Camera Camera => cam != null ? cam : (cam = GetComponent<Camera>());
        public CameraView View => view;
        /// <summary>True when the current car has a modelled interior (the cockpit view is offered).</summary>
        public bool CockpitAvailable => hasInterior;
        /// <summary>Car size factor used for framing (1 ≈ a 4.4 m, 1.6 m tall car).</summary>
        public float SizeFactor => Mathf.Clamp(Mathf.Max(carLength / 4.4f, carHeight / 1.6f * 0.9f), 0.8f, 1.6f);   // long or tall cars are framed wider
        public bool InTransition => transitionLeft > 0f;

        /// <summary>High overhead view for lining up bays; kept for older callers (VIEW used to toggle only this).</summary>
        public bool TopDown
        {
            get => view == CameraView.TopDown;
            set => SetView(value ? CameraView.TopDown : (view == CameraView.TopDown ? CameraView.Chase : view));
        }

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Clamp(value, minDistance, maxDistance);
        }

        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        public void Zoom(float delta) => Distance = distance + delta;

        /// <summary>Camera height above the car (level-specific framing, e.g. under a garage ceiling).</summary>
        public float Height
        {
            get => height;
            set => height = Mathf.Max(0.5f, value);
        }

        /// <summary>Applies a level's framing; zero values keep the current setting.</summary>
        public void ApplyFraming(float newHeight, float newDistance)
        {
            if (newHeight > 0f) Height = newHeight;
            if (newDistance > 0f)
            {
                minDistance = Mathf.Min(minDistance, newDistance);
                Distance = newDistance;
            }
            lookHeight = Mathf.Min(lookHeight, height * 0.6f);
        }

        /// <summary>Short positional shake (collision feedback). Applied on top of the smoothed position.</summary>
        public void Shake(float amplitude = 0.12f, float seconds = 0.25f)
        {
            shakeAmplitude = Mathf.Max(0f, amplitude);
            shakeDuration = Mathf.Max(0.01f, seconds);
            shakeUntil = Time.time + shakeDuration;
        }

        /// <summary>Switches view with a short blend. Cockpit falls back to Bumper on cars without an interior.</summary>
        public void SetView(CameraView next, bool instant = false)
        {
            if (next == CameraView.Cockpit && !hasInterior) next = CameraView.Bumper;
            if (next == view && !instant) return;
            fromPos = transform.position;
            fromRot = transform.rotation;
            view = next;
            transitionLeft = instant ? 0f : transitionSeconds;
            if (view == CameraView.Orbit) { orbitYaw = 0f; orbitPitch = 22f; }
            ApplyLens();
            if (instant) SnapToTarget();
        }

        /// <summary>The next view in the VIEW button cycle (skips Cockpit on cars without an interior).</summary>
        public CameraView NextView()
        {
            CameraView n = (CameraView)(((int)view + 1) % 5);
            if (n == CameraView.Cockpit && !hasInterior) n = CameraView.Orbit;
            return n;
        }

        /// <summary>Turns the orbit view (degrees). Used by touch drag and tests.</summary>
        public void Orbit(float yawDegrees, float pitchDegrees)
        {
            orbitYaw = Mathf.Repeat(orbitYaw + yawDegrees + 180f, 360f) - 180f;
            orbitPitch = Mathf.Clamp(orbitPitch + pitchDegrees, 5f, 75f);
        }

        public float OrbitYaw => orbitYaw;

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            targetBody = newTarget != null ? newTarget.GetComponentInParent<Rigidbody>() : null;
            if (target != null)
            {
                MeasureCar();
                if (view == CameraView.Cockpit && !hasInterior) view = CameraView.Bumper;
                ApplyLens();
                heading = Flatten(target.forward, heading);
                if (snap) SnapToTarget();
            }
        }

        /// <summary>Jumps to the follow position with no smoothing (after spawning or teleporting the car).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            heading = Flatten(target.forward, heading);
            Pose p = DesiredPose();
            transform.SetPositionAndRotation(p.position, p.rotation);
            velocity = Vector3.zero;
            transitionLeft = 0f;
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(maxDistance, minDistance);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private void Start()
        {
            if (target == null) TryFindTarget();
            else SetTarget(target);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (findTargetIfMissing && Time.unscaledTime >= nextSearchTime) TryFindTarget();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (mouseWheelZoom && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-Mathf.Sign(scroll) * zoomStep);
            }
            ReadTouches();

            heading = Vector3.Slerp(heading, Flatten(target.forward, heading), 1f - Mathf.Exp(-headingSharpness * dt));
            Pose desired = DesiredPose();

            if (transitionLeft > 0f)
            {
                transitionLeft = Mathf.Max(0f, transitionLeft - dt);
                float k = Mathf.SmoothStep(0f, 1f, 1f - transitionLeft / transitionSeconds);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, desired.position, k), Quaternion.Slerp(fromRot, desired.rotation, k));
                velocity = Vector3.zero;
                return;
            }

            if (view == CameraView.Bumper || view == CameraView.Cockpit)
            {
                // Rigidly mounted on the car: no lag, no shake.
                transform.SetPositionAndRotation(desired.position, desired.rotation);
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired.position, ref velocity, positionSmoothTime, Mathf.Infinity, dt);
            if (Time.time < shakeUntil)
            {
                float k = (shakeUntil - Time.time) / shakeDuration;
                transform.position += Random.insideUnitSphere * (shakeAmplitude * k);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, LookRotationAt(transform.position), 1f - Mathf.Exp(-rotationSharpness * dt));
        }

        // ---------------------------------------------------------------- poses

        private Vector3 LookPoint => target.position + Vector3.up * (view == CameraView.TopDown ? 0f : lookHeight * Mathf.Max(1f, carHeight / 1.5f));

        private Pose DesiredPose()
        {
            float size = SizeFactor;
            switch (view)
            {
                case CameraView.TopDown:
                {
                    Vector3 p = target.position + Vector3.up * (topHeight * size) - heading * topBack;
                    return new Pose(p, LookRotationAt(p));
                }
                case CameraView.Bumper:
                {
                    Vector3 local = new Vector3(0f, Mathf.Clamp(carHeight * 0.38f, 0.45f, 1.4f), frontZ + 0.08f);
                    Vector3 p = target.TransformPoint(local);
                    return new Pose(p, Quaternion.LookRotation(target.forward, Vector3.up) * Quaternion.Euler(7f, 0f, 0f));
                }
                case CameraView.Cockpit:
                {
                    // The driver's eye recorded from the model's steering wheel (CockpitMount).
                    Vector3 local = cockpit != null ? cockpit.eyeLocal : new Vector3(-carWidth * 0.2f, carHeight * 0.78f, centreZ - carLength * 0.05f);
                    Vector3 p = target.TransformPoint(local);
                    return new Pose(p, Quaternion.LookRotation(target.forward, target.up) * Quaternion.Euler(5f, 0f, 0f));
                }
                case CameraView.Orbit:
                {
                    Quaternion around = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, orbitYaw + 180f, 0f) * Quaternion.Euler(-orbitPitch, 0f, 0f);   // above the car, behind by default
                    Vector3 p = ResolveObstacles(LookPoint + around * Vector3.forward * (distance * size));
                    return new Pose(p, LookRotationAt(p));
                }
                default:
                {
                    float h = height * Mathf.Lerp(1f, size, 0.7f) + Mathf.Max(0f, carHeight - 1.5f) * 0.6f;
                    Vector3 p = ResolveObstacles(target.position + Vector3.up * h - heading * (distance * size));
                    return new Pose(p, LookRotationAt(p));
                }
            }
        }

        private Quaternion LookRotationAt(Vector3 from)
        {
            Vector3 dir = LookPoint - from;
            return dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir, Vector3.up) : transform.rotation;
        }

        private void ApplyLens()
        {
            Camera c = Camera;
            if (c == null) return;
            bool firstPerson = view == CameraView.Bumper || view == CameraView.Cockpit;
            c.fieldOfView = firstPerson ? firstPersonFov : chaseFov;
            c.nearClipPlane = firstPerson ? 0.05f : 0.3f;
        }

        private Vector3 ResolveObstacles(Vector3 desired)
        {
            if (!avoidObstacles) return desired;

            Vector3 origin = LookPoint;
            Vector3 toDesired = desired - origin;
            float length = toDesired.magnitude;
            if (length < 1e-4f) return desired;
            Vector3 dir = toDesired / length;

            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, hits, length, obstacleLayers, QueryTriggerInteraction.Ignore);
            float nearest = length;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (h.distance <= 0f) continue;                                      // started inside it (usually the car)
                if (targetBody != null && h.rigidbody == targetBody) continue;      // the car itself
                if (target != null && h.transform.IsChildOf(target)) continue;
                nearest = Mathf.Min(nearest, h.distance);
            }
            Vector3 p = origin + dir * Mathf.Max(nearest - 0.05f, 0.5f);
            // Never below the car's roof line when pulled in (keeps the lens out of the ground and kerbs).
            p.y = Mathf.Max(p.y, target.position.y + Mathf.Min(carHeight, 1.2f));
            return p;
        }

        // ---------------------------------------------------------------- touch: pinch zoom and orbit drag

        private void ReadTouches()
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null) { lastPinch = -1f; return; }
            Vector2 a = default, b = default; int free = 0; Vector2 dragDelta = default;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                UnityEngine.InputSystem.TouchPhase ph = t.phase.ReadValue();
                int id = t.touchId.ReadValue();
                if (ph == UnityEngine.InputSystem.TouchPhase.None || ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (id != 0) touchOnUi.Remove(id);
                    continue;
                }
                Vector2 pos = t.position.ReadValue();
                if (!touchOnUi.TryGetValue(id, out bool onUi))
                {
                    onUi = OverUi(pos);
                    touchOnUi[id] = onUi;
                }
                if (onUi) continue;
                if (free == 0) { a = pos; dragDelta = t.delta.ReadValue(); } else if (free == 1) b = pos;
                free++;
            }
            if (free >= 2 && (view == CameraView.Chase || view == CameraView.Orbit))
            {
                float spread = Vector2.Distance(a, b);
                if (lastPinch > 0f && spread > 1f) Distance = distance * (lastPinch / spread);
                lastPinch = spread;
                return;
            }
            lastPinch = -1f;
            if (free == 1 && view == CameraView.Orbit && dragDelta.sqrMagnitude > 0f)
                Orbit(dragDelta.x * orbitDegreesPerPixel, -dragDelta.y * orbitDegreesPerPixel);
        }

        private bool OverUi(Vector2 screen)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screen };
            uiHits.Clear();
            es.RaycastAll(ped, uiHits);
            return uiHits.Count > 0;
        }

        // ---------------------------------------------------------------- car measurements

        private void MeasureCar()
        {
            // Framing follows the visible car (monster-truck wheels stick out past the collision box), using each
            // renderer's local bounds so the car's current heading does not inflate the size.
            Transform t = target;
            float minZ = float.MaxValue, maxZ = float.MinValue, maxY = 0.8f, minX = float.MaxValue, maxX = float.MinValue;
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) || !r.enabled) continue;
                Bounds lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z);
                    Vector3 l = t.InverseTransformPoint(r.transform.TransformPoint(corner));
                    minZ = Mathf.Min(minZ, l.z); maxZ = Mathf.Max(maxZ, l.z); maxY = Mathf.Max(maxY, l.y);
                    minX = Mathf.Min(minX, l.x); maxX = Mathf.Max(maxX, l.x);
                    any = true;
                }
            }
            if (any)
            {
                carLength = Mathf.Clamp(maxZ - minZ, 2f, 8f);
                carWidth = Mathf.Clamp(maxX - minX, 1f, 4f);
                carHeight = Mathf.Clamp(maxY, 0.8f, 4f);
                frontZ = maxZ;
                centreZ = (maxZ + minZ) * 0.5f;
            }
            cockpit = t.GetComponentInParent<CockpitMount>();
            hasInterior = cockpit != null;
        }

        private void TryFindTarget()
        {
            nextSearchTime = Time.unscaledTime + 1f;
            var vehicle = FindAnyObjectByType<VehicleController>();
            if (vehicle != null) SetTarget(vehicle.transform);
        }

        private static Vector3 Flatten(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { }
    }
}
