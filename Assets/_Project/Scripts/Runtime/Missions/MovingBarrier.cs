using System;
using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// A barrier that opens and closes on a cycle: retractable bollards sinking into the road, a sliding gate, or a
    /// swinging arm. Moves <see cref="moving"/> (the imported model and its colliders) on a kinematic rigidbody so the
    /// car collides with it properly. A safety sensor stops it closing onto a car, like a real gate's loop detector.
    /// Missions find it by <see cref="barrierId"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovingBarrier : MonoBehaviour
    {
        public enum Motion { Slide, Sink, Swing }
        public enum Phase { Closed, Opening, Open, Closing }

        [SerializeField] private string barrierId = "gate";
        [Tooltip("The part that moves (model + colliders). Its pose at start is the closed pose.")]
        [SerializeField] private Transform moving;
        [SerializeField] private Motion motion = Motion.Slide;
        [Tooltip("Slide / Sink: local offset of the open pose from the closed pose.")]
        [SerializeField] private Vector3 openOffset = new Vector3(-4f, 0f, 0f);
        [Tooltip("Swing: degrees around the local axis below.")]
        [SerializeField] private float swingDegrees = 85f;
        [SerializeField] private Vector3 swingAxis = Vector3.forward;
        [SerializeField, Min(0.2f)] private float closedSeconds = 4f;
        [SerializeField, Min(0.2f)] private float openSeconds = 4f;
        [SerializeField, Min(0.2f)] private float travelSeconds = 1.5f;
        [Tooltip("Seconds into the cycle at start (staggers several barriers).")]
        [SerializeField, Min(0f)] private float phaseOffset;
        [Tooltip("Stop closing while a car is in the barrier's path.")]
        [SerializeField] private bool safetySensor = true;

        private Rigidbody body;
        private Vector3 closedLocalPos;
        private Quaternion closedLocalRot;
        private float clock;
        private Collider[] colliders;
        private Bounds lane;
        private bool hasLane;
        private readonly Collider[] overlap = new Collider[16];

        public string BarrierId => barrierId;
        public Transform Moving => moving;
        public Phase Current { get; private set; }
        /// <summary>0 = closed, 1 = fully open.</summary>
        public float OpenFraction { get; private set; }
        public bool IsPassable => OpenFraction >= 0.95f;
        public bool HeldBySensor { get; private set; }
        public float CycleSeconds => closedSeconds + openSeconds + travelSeconds * 2f;
        public event Action<Phase> PhaseChanged;

        /// <summary>Seconds until the barrier is fully open (0 while open).</summary>
        public float SecondsUntilOpen
        {
            get
            {
                float t = Mathf.Repeat(clock, CycleSeconds);
                if (t < closedSeconds) return closedSeconds - t + travelSeconds;
                if (t < closedSeconds + travelSeconds) return closedSeconds + travelSeconds - t;
                if (t < closedSeconds + travelSeconds + openSeconds) return 0f;
                return CycleSeconds - t + closedSeconds + travelSeconds;
            }
        }

        /// <summary>Seconds of open time left (0 when not open).</summary>
        public float SecondsUntilClosing
        {
            get
            {
                float t = Mathf.Repeat(clock, CycleSeconds);
                float openStart = closedSeconds + travelSeconds;
                return t >= openStart && t < openStart + openSeconds ? openStart + openSeconds - t : 0f;
            }
        }

        public void Configure(string id, Transform movingPart, Motion kind, Vector3 offset, float closed, float open, float travel,
            float phase = 0f, float swing = 85f, Vector3? axis = null)
        {
            barrierId = id;
            moving = movingPart;
            motion = kind;
            openOffset = offset;
            closedSeconds = closed;
            openSeconds = open;
            travelSeconds = travel;
            phaseOffset = phase;
            swingDegrees = swing;
            swingAxis = axis ?? Vector3.forward;
            Init();
        }

        private void Awake() => Init();

        private void Init()
        {
            if (moving == null) return;
            closedLocalPos = moving.localPosition;
            closedLocalRot = moving.localRotation;
            if (!moving.TryGetComponent(out body)) body = moving.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            colliders = moving.GetComponentsInChildren<Collider>();
            // The gate lane = where the barrier stands when closed (the loop detector's area).
            moving.SetLocalPositionAndRotation(closedLocalPos, closedLocalRot);
            Physics.SyncTransforms();
            hasLane = false;
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled) continue;
                if (!hasLane) { lane = c.bounds; hasLane = true; }
                else lane.Encapsulate(c.bounds);
            }
            clock = phaseOffset;
            Apply(Target(clock), immediate: true);
        }

        /// <summary>Puts the cycle at a given time (tests, previews).</summary>
        public void SetClock(float seconds)
        {
            clock = seconds;
            Apply(Target(clock), immediate: true);
        }

        private float Target(float t)
        {
            t = Mathf.Repeat(t, CycleSeconds);
            if (t < closedSeconds) return 0f;
            t -= closedSeconds;
            if (t < travelSeconds) return Smooth(t / travelSeconds);
            t -= travelSeconds;
            if (t < openSeconds) return 1f;
            t -= openSeconds;
            return 1f - Smooth(t / travelSeconds);
        }

        private Phase PhaseAt(float t)
        {
            t = Mathf.Repeat(t, CycleSeconds);
            if (t < closedSeconds) return Phase.Closed;
            if (t < closedSeconds + travelSeconds) return Phase.Opening;
            if (t < closedSeconds + travelSeconds + openSeconds) return Phase.Open;
            return Phase.Closing;
        }

        private void FixedUpdate()
        {
            if (moving == null) return;
            Phase next = PhaseAt(clock + Time.fixedDeltaTime);
            // Safety: hold the barrier where it is while a car is in its path and it would close further.
            HeldBySensor = safetySensor && next == Phase.Closing && CarInPath();
            if (!HeldBySensor) clock += Time.fixedDeltaTime;
            Phase p = PhaseAt(clock);
            if (p != Current) { Current = p; PhaseChanged?.Invoke(p); }
            Apply(Target(clock), immediate: false);
        }

        private bool CarInPath()
        {
            if (!hasLane) return false;
            int n = Physics.OverlapBoxNonAlloc(lane.center, lane.extents + new Vector3(0.4f, 0.5f, 0.4f), overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (overlap[i].attachedRigidbody != null && overlap[i].attachedRigidbody.GetComponent<VehicleController>() != null) return true;
            return false;
        }

        /// <summary>World-space area the closed barrier occupies (the sensor's lane).</summary>
        public Bounds Lane => lane;

        private void Apply(float open, bool immediate)
        {
            OpenFraction = open;
            Vector3 lp = closedLocalPos;
            Quaternion lr = closedLocalRot;
            if (motion == Motion.Swing) lr = closedLocalRot * Quaternion.AngleAxis(swingDegrees * open, swingAxis);
            else lp = closedLocalPos + openOffset * open;
            Transform parent = moving.parent;
            Vector3 wp = parent != null ? parent.TransformPoint(lp) : lp;
            Quaternion wr = parent != null ? parent.rotation * lr : lr;
            if (immediate || body == null) { moving.SetLocalPositionAndRotation(lp, lr); if (body != null) { body.position = wp; body.rotation = wr; } }
            else { body.MovePosition(wp); body.MoveRotation(wr); }
        }

        /// <summary>True when a collider belongs to this barrier's moving part.</summary>
        public bool Owns(Collider c) => c != null && moving != null && c.transform.IsChildOf(moving);

        public static MovingBarrier Find(string id)
        {
            foreach (MovingBarrier b in FindObjectsByType<MovingBarrier>())
                if (b.barrierId == id) return b;
            return null;
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);
    }
}
