namespace UltimateCarParking.Parking
{
    /// <summary>
    /// Extra requirement a mission adds on top of the validator's rectangle / heading / stop checks
    /// (backed fully in, close enough to the kerb…). Evaluated once per physics step while the car is inside and aligned.
    /// </summary>
    public interface IParkingCondition
    {
        /// <summary>True when met. Otherwise <paramref name="hint"/> is the short HUD instruction ("BACK IN FURTHER").</summary>
        bool IsMet(ParkingValidator validator, out string hint);
    }
}
