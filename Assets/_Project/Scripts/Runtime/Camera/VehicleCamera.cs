using UnityEngine;
using UnityEngine.InputSystem;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.CameraSystem
{
    /// <summary>
    /// Chase camera: sits behind and above the target's flattened heading, follows with SmoothDamp,
    /// looks at a point above the car, and pulls in when a wall or obstacle is between it and the car.
    /// Distance is adjustable from the Inspector, from code (<see cref="Zoom"/>, <see cref="Distance"/>)
    /// and with the mouse wheel in the Editor.
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

        [Header("Obstacle avoidance")]
        [SerializeField] private bool avoidObstacles = true;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.3f;

        private readonly RaycastHit[] hits = new RaycastHit[8];
        private Rigidbody targetBody;
        private Vector3 velocity;
        private Vector3 heading = Vector3.forward;
        private float nextSearchTime;
        private float shakeAmplitude;
        private float shakeUntil;
        private float shakeDuration;

        public Transform Target => target;

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Clamp(value, minDistance, maxDistance);
        }

        public void Zoom(float delta) => Distance = distance + delta;

        /// <summary>Short positional shake (collision feedback). Applied on top of the smoothed position.</summary>
        public void Shake(float amplitude = 0.12f, float seconds = 0.25f)
        {
            shakeAmplitude = Mathf.Max(0f, amplitude);
            shakeDuration = Mathf.Max(0.01f, seconds);
            shakeUntil = Time.time + shakeDuration;
        }

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            targetBody = newTarget != null ? newTarget.GetComponentInParent<Rigidbody>() : null;
            if (target != null)
            {
                heading = Flatten(target.forward, heading);
                if (snap) SnapToTarget();
            }
        }

        /// <summary>Jumps to the follow position with no smoothing (after spawning or teleporting the car).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            heading = Flatten(target.forward, heading);
            transform.position = ResolveObstacles(DesiredPosition());
            transform.rotation = LookRotationAt(transform.position);
            velocity = Vector3.zero;
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

            heading = Vector3.Slerp(heading, Flatten(target.forward, heading), 1f - Mathf.Exp(-headingSharpness * dt));

            Vector3 desired = ResolveObstacles(DesiredPosition());
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmoothTime, Mathf.Infinity, dt);
            if (Time.time < shakeUntil)
            {
                float k = (shakeUntil - Time.time) / shakeDuration;
                transform.position += Random.insideUnitSphere * (shakeAmplitude * k);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, LookRotationAt(transform.position), 1f - Mathf.Exp(-rotationSharpness * dt));
        }

        private Vector3 LookPoint => target.position + Vector3.up * lookHeight;

        private Vector3 DesiredPosition() => target.position + Vector3.up * height - heading * distance;

        private Quaternion LookRotationAt(Vector3 from)
        {
            Vector3 dir = LookPoint - from;
            return dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir, Vector3.up) : transform.rotation;
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
            return origin + dir * Mathf.Max(nearest - 0.05f, 0.5f);
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
    }
}
