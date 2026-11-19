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
        /// <summary>How hard a counted contact was: a scrape (slow, glancing) or a significant impact.</summary>
        public enum HitSeverity { Scrape, Impact }

        public readonly struct Penalty
        {
            public readonly Collider Other;
            public readonly Vector3 Point;
            public readonly float ImpactSpeed;
            public readonly int TotalCount;
            public readonly HitSeverity Severity;
            public Penalty(Collider other, Vector3 point, float impactSpeed, int totalCount, HitSeverity severity = HitSeverity.Impact)
            {
                Other = other;
                Point = point;
                ImpactSpeed = impactSpeed;
                TotalCount = totalCount;
                Severity = severity;
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
        [Tooltip("Relative speed (m/s) from which a counted contact is an impact rather than a scrape (feedback only).")]
        [SerializeField, Min(0f)] private float impactSpeed = 1.5f;
        [Tooltip("After a counted contact, contacts with anything else within this many seconds are the same incident (one crash into a car made of several colliders counts once).")]
        [SerializeField, Min(0f)] private float incidentWindow = 0.4f;

        private float lastCountedTime = -99f;

        /// <summary>Severity of the most recent counted contact.</summary>
        public HitSeverity LastSeverity { get; private set; }
        /// <summary>Contacts too soft to count (below the impact threshold) since the level started: feedback only.</summary>
        public int GentleTouches { get; private set; }

        private readonly Dictionary<Collider, float> lastHitTime = new Dictionary<Collider, float>();

        public event Action<Penalty> PenaltyRegistered;
        /// <summary>A contact too soft to count (feedback only, never a penalty). At most once a second per object.</summary>
        public event Action<Collider> GentleContact;
        private readonly Dictionary<Collider, float> lastGentle = new Dictionary<Collider, float>();

        public int CollisionCount { get; private set; }

        /// <summary>When false, collisions are ignored (before the level starts and after it ends).</summary>
        public bool Counting { get; set; } = true;

        public void ResetCount()
        {
            CollisionCount = 0;
            GentleTouches = 0;
            lastCountedTime = -99f;
            lastHitTime.Clear();
            lastGentle.Clear();
        }

        private void OnCollisionEnter(Collision collision) => Consider(collision);

        private bool SideContact(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (Mathf.Abs(collision.GetContact(i).normal.y) < groundNormalThreshold) return true;
            return false;
        }

        private void Consider(Collision collision)
        {
            if (!Counting) return;
            Collider other = collision.collider;
            if (other == null || ((1 << other.gameObject.layer) & ignoredLayers.value) != 0) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < minImpactSpeed)
            {
                if (impact > 0.05f && SideContact(collision))
                {
                    GentleTouches++;
                    if (!lastGentle.TryGetValue(other, out float t) || Time.time - t > 1f) { lastGentle[other] = Time.time; GentleContact?.Invoke(other); }
                }
                return;
            }

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
            if (now - lastCountedTime < incidentWindow) return;   // same crash, another collider of the same thing
            lastCountedTime = now;
            LastSeverity = impact >= impactSpeed ? HitSeverity.Impact : HitSeverity.Scrape;
            CollisionCount++;
            PenaltyRegistered?.Invoke(new Penalty(other, point, impact, CollisionCount, LastSeverity));
        }
    }
}
