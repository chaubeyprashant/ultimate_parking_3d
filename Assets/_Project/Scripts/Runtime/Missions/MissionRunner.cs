using System;
using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Runs a level's objective sequence: one current objective at a time, in order. The LevelManager prepares it
    /// with the level and car, starts it when the level starts playing and stops it when the attempt ends; the HUD and
    /// the world guide listen to the events. Knows nothing about specific mission types.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionRunner : MonoBehaviour
    {
        [SerializeField] private ParkingValidator validator;
        [Tooltip("Material for the bay marker strips.")]
        [SerializeField] private Material zoneMaterial;
        [SerializeField] private ParkingSensorAudio sensorAudio;

        private readonly List<MissionObjective> objectives = new List<MissionObjective>();
        private MissionContext ctx;
        private Transform attemptRoot;
        private LevelData level;
        private CollisionHandler hits;
        private int objImpacts, objScrapes;
        private float runStart;

        /// <summary>Scorecards of the parks finished in this attempt (Parking score mode only).</summary>
        public List<ParkingScorecard> Scorecards { get; } = new List<ParkingScorecard>();

        public event Action<int, MissionObjective> ObjectiveStarted;
        public event Action<int, MissionObjective> ObjectiveCompleted;
        public event Action<string, bool> Notice;
        public event Action<ParkingZone> ActiveZoneChanged;
        public event Action Completed;
        public event Action<string> Failed;

        public IReadOnlyList<MissionObjective> Objectives => objectives;
        public int CurrentIndex { get; private set; } = -1;
        public MissionObjective Current => CurrentIndex >= 0 && CurrentIndex < objectives.Count ? objectives[CurrentIndex] : null;
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public MissionGauge Gauge { get; } = new MissionGauge();
        public ParkingZone ActiveZone { get; private set; }
        public string Hint { get; private set; } = "";
        public float Progress => Current != null && ctx != null ? Current.Progress(ctx) : IsComplete ? 1f : 0f;
        public ParkingValidator Validator => validator;
        public string LastNotice { get; private set; } = "";
        /// <summary>Lowest 0..1 score of the completed scored objectives (1 when none is scored).</summary>
        public float Quality { get; private set; } = 1f;
        /// <summary>Labels of the scored objectives, for the results screen ("ACCURACY 92%").</summary>
        public string ScoreLabel { get; private set; } = "";

        public void Configure(ParkingValidator parkingValidator, Material markerMaterial)
        {
            if (parkingValidator != null) validator = parkingValidator;
            if (markerMaterial != null) zoneMaterial = markerMaterial;
        }

        /// <summary>Clones the level's objectives for a new attempt. Nothing runs until <see cref="Run"/>.</summary>
        public void Prepare(LevelData level, VehicleController vehicle)
        {
            Clear();
            if (validator == null && !TryGetComponent(out validator))
            {
                Debug.LogError("[MissionRunner] No ParkingValidator; missions cannot run.", this);
                return;
            }
            this.level = level;
            foreach (MissionObjective o in level.CreateObjectives())
                if (o != null) objectives.Add(o.CloneForRun());
            hits = vehicle != null ? vehicle.GetComponent<CollisionHandler>() : null;
            if (hits != null) hits.PenaltyRegistered += OnPenalty;
            attemptRoot = new GameObject($"Mission_{level.LevelId}").transform;
            ctx = new MissionContext(this, vehicle, validator, zoneMaterial, attemptRoot, sensorAudio);
            if (objectives.Count == 0) Debug.LogError($"[MissionRunner] {level.LevelId} has no objectives.", this);
        }

        /// <summary>Starts the first objective.</summary>
        public void Run()
        {
            if (ctx == null || objectives.Count == 0) return;
            IsRunning = true;
            runStart = Time.time;
            Advance();
        }

        /// <summary>
        /// Picks a stopped attempt back up (the "+30 seconds" continue): re-begins the current objective, keeping the
        /// objectives already completed. Does nothing when the attempt finished or never ran.
        /// </summary>
        public bool Resume()
        {
            if (ctx == null || IsRunning || IsComplete || Current == null) return false;
            IsRunning = true;
            Current.Begin(ctx);
            SetHint(Current.Hint(ctx));
            return true;
        }

        /// <summary>Stops the attempt (level completed, failed or unloaded). Keeps the zones until the next Prepare.</summary>
        public void Stop()
        {
            if (Current != null && IsRunning) Current.End(ctx);
            IsRunning = false;
            Gauge.Hide();
            if (sensorAudio != null) sensorAudio.Silence();
        }

        private void Update()
        {
            if (!IsRunning || Current == null) return;
            ObjectiveState state = Current.Tick(ctx, Time.deltaTime);
            if (!IsRunning) return;   // a listener stopped us
            SetHint(Current.Hint(ctx));
            if (state == ObjectiveState.Completed)
            {
                MissionObjective done = Current;
                done.End(ctx);
                if (level != null && level.ScoreMode == ScoreMode.Parking && done is ParkObjective park) ScorePark(park);
                if (done is IScoredObjective scored)
                {
                    Quality = Mathf.Min(Quality, Mathf.Clamp01(scored.Quality));
                    ScoreLabel = ScoreLabel.Length == 0 ? scored.ScoreLabel : ScoreLabel + "  " + scored.ScoreLabel;
                }
                Debug.Log($"[Mission] Objective {CurrentIndex + 1}/{objectives.Count} done: {done.Title}{(done is IScoredObjective sc ? " (" + sc.ScoreLabel + ")" : "")}");
                ObjectiveCompleted?.Invoke(CurrentIndex, done);
                Advance();
            }
            else if (state == ObjectiveState.Failed)
            {
                MissionObjective failed = Current;
                failed.End(ctx);
                IsRunning = false;
                Gauge.Hide();
                Debug.Log($"[Mission] Objective failed: {failed.Title} ({failed.FailDetail})");
                Failed?.Invoke(failed.FailDetail);
            }
        }

        private void Advance()
        {
            CurrentIndex++;
            if (CurrentIndex >= objectives.Count)
            {
                IsRunning = false;
                IsComplete = true;
                SetHint("");
                Completed?.Invoke();
                return;
            }
            objImpacts = objScrapes = 0;
            Current.Begin(ctx);
            SetHint(Current.Hint(ctx));
            ObjectiveStarted?.Invoke(CurrentIndex, Current);
        }

        internal void SetActiveZone(ParkingZone zone)
        {
            ActiveZone = zone;
            ActiveZoneChanged?.Invoke(zone);
        }

        internal void RaiseNotice(string message, bool bad)
        {
            LastNotice = message;
            Notice?.Invoke(message, bad);
        }

        private void SetHint(string hint) => Hint = hint ?? "";

        private void OnPenalty(CollisionHandler.Penalty p)
        {
            if (!IsRunning) return;
            if (p.Severity == CollisionHandler.HitSeverity.Impact) objImpacts++; else objScrapes++;
        }

        /// <summary>Realistic score for a finished park (centring for this car's room in the bay, angle, hits, time, stillness).</summary>
        private void ScorePark(ParkObjective park)
        {
            ParkingValidator v = validator;
            if (v == null || v.Zone == null || v.WorldFootprint == null || v.LocalFootprint == null) return;
            Vector3 centre = Vector3.zero;
            foreach (Vector3 c in v.WorldFootprint) centre += c;
            centre /= v.WorldFootprint.Length;
            Vector3 local = Quaternion.Inverse(v.Zone.Rotation) * (centre - v.Zone.Center);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 c in v.LocalFootprint) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); }
            ParkingScorecard card = ParkingScorecard.Score(new Vector2(local.x, local.z), new Vector2(maxX - minX, maxZ - minZ), v.Zone.Size,
                v.HeadingErrorDegrees, v.ActiveRules.angleTolerance, objImpacts, objScrapes, Time.time - runStart, level.TimeLimitSeconds, v.SpeedKmh);
            Scorecards.Add(card);
            Quality = Mathf.Min(Quality, card.Overall);
            ScoreLabel = ScoreLabel.Length == 0 ? card.Label : ScoreLabel + "  " + card.Label;
            Debug.Log($"[Mission] {park.Title}: {card.Label} (side {card.SideOffset:F2} m, depth {card.DepthOffset:F2} m)");
        }

        private void Clear()
        {
            Stop();
            ctx?.DestroySpawned();
            if (attemptRoot != null) Destroy(attemptRoot.gameObject);
            objectives.Clear();
            ctx = null;
            CurrentIndex = -1;
            IsComplete = false;
            ActiveZone = null;
            Hint = "";
            LastNotice = "";
            Quality = 1f;
            ScoreLabel = "";
            Scorecards.Clear();
            if (hits != null) hits.PenaltyRegistered -= OnPenalty;
            hits = null;
            level = null;
        }

        private void OnDestroy() => Clear();
    }
}
