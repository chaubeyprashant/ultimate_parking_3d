using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// Runs one level attempt: start (after a short settle delay), timer, collision limit, completion with stars,
    /// failure, restart and next level. The LevelLoader builds the level and hands it over through
    /// <see cref="BeginLevel"/>; UI listens to the events and reads the properties.
    /// One per Gameplay scene: a second instance removes itself.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private ParkingValidator validator;
        [Tooltip("Needed for Next Level (catalog order). Optional otherwise.")]
        [SerializeField] private LevelLoader loader;
        [Tooltip("Seconds with the controls held after spawning, so the car settles on its suspension.")]
        [SerializeField, Min(0f)] private float startDelay = 0.5f;

        public static LevelManager Instance { get; private set; }

        public event Action<LevelState> StateChanged;
        public event Action<LevelData> LevelStarted;
        public event Action<LevelResult> LevelCompleted;
        public event Action<LevelResult> LevelFailed;
        public event Action<int> CollisionCountChanged;

        public LevelState State { get; private set; } = LevelState.Idle;
        public LevelData Level { get; private set; }
        public VehicleController Vehicle { get; private set; }
        public ParkingValidator Validator => validator;
        public float ElapsedSeconds { get; private set; }
        public int Collisions => collisionHandler != null ? collisionHandler.CollisionCount : 0;
        public LevelResult? LastResult { get; private set; }

        /// <summary>Seconds left, or +∞ when the level has no time limit.</summary>
        public float RemainingSeconds => Level != null && Level.HasTimeLimit
            ? Mathf.Max(0f, Level.TimeLimitSeconds - ElapsedSeconds)
            : float.PositiveInfinity;

        public bool HasNextLevel => loader != null && loader.Catalog != null && Level != null && loader.Catalog.Next(Level) != null;

        private CollisionHandler collisionHandler;
        private float startTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[LevelManager] A LevelManager already exists on '{Instance.name}'; removing the duplicate on '{name}'.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (validator == null && !TryGetComponent(out validator))
                Debug.LogError("[LevelManager] No ParkingValidator assigned; levels cannot be completed.", this);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        /// <summary>Starts an attempt. Called by the LevelLoader once the world, car and bay exist.</summary>
        public void BeginLevel(LevelData level, VehicleController vehicle, CollisionHandler collisions, ParkingZone zone)
        {
            if (level == null || vehicle == null)
            {
                Debug.LogError("[LevelManager] BeginLevel needs a level and a vehicle.", this);
                return;
            }

            Unsubscribe();
            Level = level;
            Vehicle = vehicle;
            collisionHandler = collisions;
            ElapsedSeconds = 0f;
            LastResult = null;

            if (collisionHandler != null)
            {
                collisionHandler.ResetCount();
                collisionHandler.Counting = false;
                collisionHandler.PenaltyRegistered += OnPenalty;
            }
            else
            {
                Debug.LogWarning("[LevelManager] No CollisionHandler on the car; collisions will not be counted.", this);
            }

            if (validator != null)
            {
                validator.Bind(zone, vehicle, level.ParkingRulesOverride);
                validator.Parked += OnParked;
            }

            Vehicle.ControlsEnabled = false;
            startTimer = startDelay;
            SetState(LevelState.Starting);
            if (startDelay <= 0f) StartPlaying();
        }

        private void StartPlaying()
        {
            if (Vehicle != null) Vehicle.ControlsEnabled = true;
            if (collisionHandler != null) collisionHandler.Counting = true;
            if (validator != null) validator.SetArmed(true);
            SetState(LevelState.Playing);
            LevelStarted?.Invoke(Level);
            Debug.Log($"[Level] Started {Level.LevelId} ({Level.DisplayName})");
        }

        private void Update()
        {
            if (State == LevelState.Starting)
            {
                startTimer -= Time.deltaTime;
                if (startTimer <= 0f) StartPlaying();
                return;
            }
            if (State != LevelState.Playing) return;

            ElapsedSeconds += Time.deltaTime;
            if (Level.HasTimeLimit && ElapsedSeconds >= Level.TimeLimitSeconds)
            {
                ElapsedSeconds = Level.TimeLimitSeconds;
                FailLevel(FailReason.TimeUp);
            }
        }

        private void OnPenalty(CollisionHandler.Penalty penalty)
        {
            if (State != LevelState.Playing) return;
            CollisionCountChanged?.Invoke(penalty.TotalCount);
            if (Level.HasCollisionLimit && penalty.TotalCount > Level.CollisionLimit)
                FailLevel(FailReason.TooManyCollisions);
        }

        private void OnParked()
        {
            if (State == LevelState.Playing) CompleteLevel();
        }

        /// <summary>Ends the attempt as a success and scores it.</summary>
        public void CompleteLevel()
        {
            if (State != LevelState.Playing) return;
            int stars = StarRating.Calculate(ElapsedSeconds, Collisions, Level.Stars);
            var result = new LevelResult(Level.LevelId, true, FailReason.None, stars, ElapsedSeconds, Collisions, Level.CoinsFor(stars));
            Finish(LevelState.Completed, result);
            LevelCompleted?.Invoke(result);
        }

        /// <summary>Ends the attempt as a failure.</summary>
        public void FailLevel(FailReason reason)
        {
            if (State != LevelState.Playing) return;
            var result = new LevelResult(Level.LevelId, false, reason, 0, ElapsedSeconds, Collisions, 0);
            Finish(LevelState.Failed, result);
            LevelFailed?.Invoke(result);
        }

        private void Finish(LevelState state, LevelResult result)
        {
            LastResult = result;
            if (Vehicle != null) Vehicle.ControlsEnabled = false;
            if (collisionHandler != null) collisionHandler.Counting = false;
            if (validator != null && validator.Status != ParkingStatus.Parked) validator.SetArmed(false);
            SetState(state);
            Debug.Log($"[Level] {result}");
        }

        /// <summary>Reloads the scene with the same level (resets every prop the car knocked over).</summary>
        public void RestartLevel()
        {
            if (Level == null) { Debug.LogWarning("[LevelManager] Restart: no level running.", this); return; }
            LoadLevelScene(Level);
        }

        /// <summary>Loads the next level in the catalog. Returns false (and does nothing) on the last level.</summary>
        public bool NextLevel()
        {
            LevelData next = loader != null && loader.Catalog != null && Level != null ? loader.Catalog.Next(Level) : null;
            if (next == null)
            {
                Debug.LogWarning("[LevelManager] Next: there is no next level.", this);
                return false;
            }
            LoadLevelScene(next);
            return true;
        }

        private void LoadLevelScene(LevelData level)
        {
            LevelSelection.Request(level, loader != null ? loader.Catalog : null);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SetState(LevelState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void Unsubscribe()
        {
            if (collisionHandler != null) collisionHandler.PenaltyRegistered -= OnPenalty;
            if (validator != null) validator.Parked -= OnParked;
        }
    }
}
