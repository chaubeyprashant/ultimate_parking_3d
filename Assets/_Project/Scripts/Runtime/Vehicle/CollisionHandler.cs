using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Counts the car's collisions as penalties. Ignores the ground (contacts whose normal points up), soft touches
    /// below an impact speed, ignored layers, and repeat contacts with the same object within a cooldown, so one
    /// scrape along a wall counts once. Counting is switched on and off by the LevelManager.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class CollisionHandler : MonoBehaviour
    {
        public readonly struct Penalty
        {
            public readonly Collider Other;
            public readonly Vector3 Point;
            public readonly float ImpactSpeed;
            public readonly int TotalCount;

            public Penalty(Collider other, Vector3 point, float impactSpeed, int totalCount)
            {
                Other = other;
                Point = point;
                ImpactSpeed = impactSpeed;
                TotalCount = totalCount;
            }
        }

        [Tooltip("Relative speed (m/s) below which a contact is a touch, not a collision.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 0.3f;
        [Tooltip("Contacts with a normal pointing up more than this (0..1) are ground, kerb tops or ramps and never count.")]
        [SerializeField, Range(0f, 1f)] private float groundNormalThreshold = 0.7f;
        [Tooltip("Seconds before the same object can count again.")]
        [SerializeField, Min(0f)] private float sameObjectCooldown = 1f;
        [Tooltip("Layers that never count (e.g. a 'NoPenalty' layer for kerbs you want drivable).")]
        [SerializeField] private LayerMask ignoredLayers;

        private readonly Dictionary<Collider, float> lastHitTime = new Dictionary<Collider, float>();

        public event Action<Penalty> PenaltyRegistered;

        public int CollisionCount { get; private set; }

        /// <summary>When false, collisions are ignored (before the level starts and after it ends).</summary>
        public bool Counting { get; set; } = true;

        public void ResetCount()
        {
            CollisionCount = 0;
            lastHitTime.Clear();
        }

        private void OnCollisionEnter(Collision collision) => Consider(collision);

        private void Consider(Collision collision)
        {
            if (!Counting) return;
            Collider other = collision.collider;
            if (other == null || ((1 << other.gameObject.layer) & ignoredLayers.value) != 0) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < minImpactSpeed) return;

            // Ground hit only if every contact faces up; any side contact makes it an obstacle hit.
            Vector3 point = Vector3.zero;
            bool sideContact = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint c = collision.GetContact(i);
                if (Mathf.Abs(c.normal.y) < groundNormalThreshold)
                {
                    sideContact = true;
                    point = c.point;
                    break;
                }
            }
            if (!sideContact) return;

            float now = Time.time;
            if (lastHitTime.TryGetValue(other, out float last) && now - last < sameObjectCooldown)
            {
                lastHitTime[other] = now;   // still scraping: extend the cooldown
                return;
            }
            lastHitTime[other] = now;

            CollisionCount++;
            PenaltyRegistered?.Invoke(new Penalty(other, point, impact, CollisionCount));
        }
    }
}
