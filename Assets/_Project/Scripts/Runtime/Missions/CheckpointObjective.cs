using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Route step: drive through a marked area (market lane, garage ramp, mall entrance) before the next objective.
    /// Completes as soon as the car's centre is inside the area at the area's height (so a checkpoint on garage
    /// level 2 is not ticked by driving underneath it on level 1). No stopping needed.
    /// </summary>
    [Serializable, Preserve]
    public sealed class CheckpointObjective : MissionObjective
    {
        [SerializeField] private Vector3 position;
        [SerializeField] private float yaw;
        [Tooltip("Area width (across) and length (along the yaw direction), metres.")]
        [SerializeField] private Vector2 size = new Vector2(6f, 4f);
        [Tooltip("How far above/below the area's height the car may be and still count (multi-storey garages).")]
        [SerializeField, Min(0.2f)] private float heightTolerance = 1.6f;
        [Tooltip("Instruction shown while heading for the checkpoint.")]
        [SerializeField] private string hint = "DRIVE THROUGH THE GATE";

        [NonSerialized] private ParkingZone zone;

        public CheckpointObjective() { title = "Checkpoint"; }

        public CheckpointObjective(string title, Vector3 position, float yaw, Vector2 size, string hint, float heightTolerance = 1.6f)
        {
            this.title = title;
            this.position = position;
            this.yaw = yaw;
            this.size = size;
            this.hint = hint;
            this.heightTolerance = heightTolerance;
        }

        public override Vector3 TargetPoint => position;
        public Vector3 Position => position;
        public Vector2 Size => size;

        public override void Begin(MissionContext ctx)
        {
            zone = ctx.CreateZone("Checkpoint_" + title, position, Quaternion.Euler(0f, yaw, 0f), size);
            ctx.Validator.SetArmed(false);
            ctx.SetActiveZone(zone);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime) =>
            Contains(ctx.Vehicle.transform.position) ? ObjectiveState.Completed : ObjectiveState.Running;

        public override void End(MissionContext ctx)
        {
            if (zone != null) zone.gameObject.SetActive(false);
        }

        public override string Hint(MissionContext ctx) => hint;

        /// <summary>True when a world point is inside the area (footprint and height band).</summary>
        public bool Contains(Vector3 world)
        {
            if (Mathf.Abs(world.y - position.y) > heightTolerance) return false;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (world - position);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }
    }
}
