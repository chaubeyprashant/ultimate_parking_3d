using System;
using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Moves each wheel mesh to match its WheelCollider: suspension travel, steering angle and spin.
    ///
    /// The pose is computed in the car's local space from the collider's own data, then placed on the
    /// car's (interpolated) Transform. Copying <c>WheelCollider.GetWorldPose</c> directly would mix
    /// physics-step positions with interpolated ones and make the wheels shake against the body.
    ///
    /// Each visual Transform should be an empty pivot at the wheel centre with X = axle, Y = up,
    /// Z = forward; the tyre/rim meshes are its children.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WheelVisuals : MonoBehaviour
    {
        [Serializable]
        public sealed class WheelVisual
        {
            public WheelCollider collider;
            public Transform visual;
            [NonSerialized] public float spinDegrees;
        }

        [Tooltip("Pair each WheelCollider with the pivot Transform of its mesh.")]
        [SerializeField] private WheelVisual[] wheels = Array.Empty<WheelVisual>();

        [Tooltip("The car's Rigidbody. Left empty: found on the colliders.")]
        [SerializeField] private Rigidbody body;

        public int WheelCount => wheels.Length;

        private void Awake()
        {
            if (body == null)
                foreach (WheelVisual w in wheels)
                    if (w != null && w.collider != null && w.collider.attachedRigidbody != null)
                    {
                        body = w.collider.attachedRigidbody;
                        break;
                    }

            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] == null || wheels[i].collider == null || wheels[i].visual == null)
                    Debug.LogWarning($"[WheelVisuals] {name}: wheel {i} is missing its collider or visual and will not be animated.", this);

            if (body == null)
                Debug.LogWarning($"[WheelVisuals] {name}: no Rigidbody found; wheels will follow the colliders without interpolation correction.", this);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (WheelVisual w in wheels)
            {
                if (w == null || w.collider == null || w.visual == null) continue;
                // WheelCollider.rotationSpeed is degrees per second.
                w.spinDegrees = Mathf.Repeat(w.spinDegrees + w.collider.rotationSpeed * dt, 360f);
                Place(w);
            }
        }

        private void Place(WheelVisual w)
        {
            WheelCollider wc = w.collider;
            Transform wheelTf = wc.transform;

            if (body == null)
            {
                wc.GetWorldPose(out Vector3 worldPos, out Quaternion worldRot);
                w.visual.SetPositionAndRotation(worldPos, worldRot);
                return;
            }

            Transform root = body.transform;

            // Suspension origin and axes in the car's local space (static unless the colliders are moved).
            Vector3 localOrigin = root.InverseTransformPoint(wheelTf.TransformPoint(wc.center));
            Quaternion localWheelRot = Quaternion.Inverse(root.rotation) * wheelTf.rotation;
            Vector3 localUp = localWheelRot * Vector3.up;

            // How far the wheel hangs below the origin, measured against the physics pose of the body
            // because the ground hit comes from the last physics step.
            float drop = wc.suspensionDistance;
            if (wc.GetGroundHit(out WheelHit hit))
            {
                Vector3 physicsOrigin = body.position + body.rotation * localOrigin;
                Vector3 physicsUp = body.rotation * localUp;
                drop = Mathf.Clamp(Vector3.Dot(physicsOrigin - hit.point, physicsUp) - wc.radius, 0f, wc.suspensionDistance);
            }

            Vector3 localCenter = localOrigin - localUp * drop;
            Quaternion localRot = localWheelRot * Quaternion.Euler(0f, wc.steerAngle, 0f) * Quaternion.Euler(w.spinDegrees, 0f, 0f);
            w.visual.SetPositionAndRotation(root.TransformPoint(localCenter), root.rotation * localRot);
        }

        /// <summary>Snapshot of a wheel's current visual spin in degrees (for tests / debugging).</summary>
        public float GetSpinDegrees(int index) =>
            index >= 0 && index < wheels.Length && wheels[index] != null ? wheels[index].spinDegrees : 0f;
    }
}
