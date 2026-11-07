using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    public enum ParkDirection { Either, NoseIn, ReverseIn }

    /// <summary>
    /// Standard parking: stop inside one bay, aligned, and hold. Uses the shared <see cref="ParkingValidator"/> for the
    /// rectangle / heading / speed / hold checks; subclasses add conditions and feedback through the virtual hooks.
    /// </summary>
    [Serializable, Preserve]
    public class ParkObjective : MissionObjective, IParkingCondition
    {
        [Tooltip("Centre of the bay on the ground.")]
        [SerializeField] protected Vector3 position;
        [Tooltip("The bay's nose-in direction in degrees around Y (the way a car drives into it forwards).")]
        [SerializeField] protected float bayYaw;
        [Tooltip("Bay width (across) and length (along the nose-in direction), metres.")]
        [SerializeField] protected Vector2 size = new Vector2(2.8f, 5.5f);
        [SerializeField] protected ParkDirection direction = ParkDirection.Either;
        [Tooltip("Use the rules below instead of the validator's defaults. Direction always comes from the field above.")]
        [SerializeField] protected bool overrideRules;
        [SerializeField] protected ParkingRules rules = new ParkingRules();

        [NonSerialized] protected ParkingZone zone;

        public ParkObjective() { }

        public ParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, ParkDirection direction, ParkingRules rules = null)
        {
            this.title = title;
            this.position = position;
            this.bayYaw = bayYaw;
            this.size = size;
            this.direction = direction;
            overrideRules = rules != null;
            if (rules != null) this.rules = rules.Clone();
        }

        public override Vector3 TargetPoint => position;
        public Vector3 Position => position;
        public Vector2 Size => size;
        public ParkDirection Direction => direction;
        public ParkingZone Zone => zone;

        /// <summary>The heading the car must end with (ReverseIn: facing out of the bay).</summary>
        public Quaternion ZoneRotation => Quaternion.Euler(0f, direction == ParkDirection.ReverseIn ? bayYaw + 180f : bayYaw, 0f);

        /// <summary>Subclasses with an extra condition return true so the validator calls <see cref="IsMet"/>.</summary>
        protected virtual bool HasCondition => false;

        public override void Begin(MissionContext ctx)
        {
            zone = ctx.CreateZone("Bay_" + title, position, ZoneRotation, size);
            ParkingRules r = (overrideRules ? rules : ctx.Validator.DefaultRules).Clone();
            r.allowEitherDirection = direction == ParkDirection.Either;
            ctx.Validator.Bind(zone, ctx.Vehicle, r, HasCondition ? this : null);
            ctx.Validator.SetArmed(true);
            ctx.SetActiveZone(zone);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime) =>
            ctx.Validator.Status == ParkingStatus.Parked ? ObjectiveState.Completed : ObjectiveState.Running;

        public override void End(MissionContext ctx)
        {
            if (ctx.Validator.Status != ParkingStatus.Parked) ctx.Validator.SetArmed(false);
            ctx.Gauge.Hide();
        }

        public override float Progress(MissionContext ctx) => ctx.Validator.HoldProgress;

        public override string Hint(MissionContext ctx) => ctx.Validator.Status switch
        {
            ParkingStatus.Outside => "DRIVE TO THE BAY",
            ParkingStatus.PartiallyInside => "GET FULLY INSIDE THE LINES",
            ParkingStatus.Misaligned => "STRAIGHTEN UP",
            ParkingStatus.Unpositioned => ctx.Validator.ConditionHint,
            ParkingStatus.TooFast => "STOP THE CAR",
            ParkingStatus.Holding => "HOLD IT...",
            ParkingStatus.Parked => "PARKED!",
            _ => "",
        };

        public virtual bool IsMet(ParkingValidator validator, out string hint)
        {
            hint = "";
            return true;
        }

        /// <summary>A world point in the bay's frame (x across, z along the zone's forward), metres from its centre.</summary>
        protected Vector3 ToZoneLocal(Vector3 world) => Quaternion.Inverse(ZoneRotation) * (world - position);
    }
}
