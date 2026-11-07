using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace UltimateCarParking.Missions
{
    public enum ObjectiveState { Running, Completed, Failed }

    /// <summary>
    /// One step of a mission ("Reverse into bay 4", "Park at the kerb"). Configuration is serialized inside the
    /// LevelData ([SerializeReference]); the <see cref="MissionRunner"/> works on a clone per attempt, so runtime
    /// fields can never leak into the asset. Subclasses own their behaviour: the runner never switches on type.
    /// </summary>
    [Serializable, Preserve]
    public abstract class MissionObjective
    {
        [Tooltip("Objective line on the HUD, e.g. \"Reverse into bay 4\".")]
        [SerializeField] protected string title = "Park in the bay";

        public string Title => title;

        /// <summary>Called when this objective becomes the current one. Reset all runtime state here.</summary>
        public virtual void Begin(MissionContext ctx) { }

        /// <summary>Called every frame while current.</summary>
        public abstract ObjectiveState Tick(MissionContext ctx, float deltaTime);

        /// <summary>Called once after Completed / Failed, or when the attempt stops.</summary>
        public virtual void End(MissionContext ctx) { }

        /// <summary>Live instruction for the HUD guide line ("BACK IN FURTHER"). Empty hides the line.</summary>
        public virtual string Hint(MissionContext ctx) => "";

        /// <summary>Why the objective failed, shown on the fail panel.</summary>
        public virtual string FailDetail => "FAILED";

        /// <summary>Hold-to-confirm progress 0..1 for the HUD bar.</summary>
        public virtual float Progress(MissionContext ctx) => 0f;

        /// <summary>Where the player is heading for this objective (guide, reachability tests).</summary>
        public virtual Vector3 TargetPoint => Vector3.zero;

        /// <summary>Copy for one attempt. Configuration is immutable, so a shallow copy is enough.</summary>
        public MissionObjective CloneForRun() => (MissionObjective)MemberwiseClone();
    }
}
