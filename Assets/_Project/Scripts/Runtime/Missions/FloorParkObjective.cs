using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Multi-storey parking: the bay is on a given floor. A car inside the same rectangle on another floor (above or
    /// below) is "Unpositioned" with a WRONG FLOOR hint, so only the correct level completes.
    /// </summary>
    [Serializable, Preserve]
    public sealed class FloorParkObjective : ParkObjective
    {
        [Tooltip("Floor name shown to the player, e.g. \"LEVEL 3\".")]
        [SerializeField] private string floorLabel = "LEVEL 2";
        [Tooltip("Max height difference between the car and the bay floor, metres.")]
        [SerializeField, Min(0.2f)] private float floorTolerance = 1.2f;

        public FloorParkObjective() { }

        public FloorParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, ParkDirection direction, string floorLabel,
            ParkingRules rules = null) : base(title, position, bayYaw, size, direction, rules)
        {
            this.floorLabel = floorLabel;
        }

        public string FloorLabel => floorLabel;
        protected override bool HasCondition => true;

        public bool OnFloor(Vector3 world) => Mathf.Abs(world.y - position.y) <= floorTolerance;

        public override bool IsMet(ParkingValidator validator, out string hint)
        {
            bool ok = validator.Vehicle != null && OnFloor(validator.Vehicle.transform.position);
            hint = ok ? "" : $"WRONG FLOOR - PARK ON {floorLabel}";
            return ok;
        }

        public override string Hint(MissionContext ctx)
        {
            float dy = position.y - ctx.Vehicle.transform.position.y;
            if (dy > floorTolerance) return $"GO UP TO {floorLabel}";
            if (dy < -floorTolerance) return $"GO DOWN TO {floorLabel}";
            return base.Hint(ctx);
        }
    }
}
