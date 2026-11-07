namespace UltimateCarParking.Parking
{
    /// <summary>What the validator currently sees, in order of how close the car is to being parked.</summary>
    public enum ParkingStatus
    {
        Inactive,          // not bound / not armed (level not running)
        Outside,           // no part of the car is in the bay
        PartiallyInside,   // some, not all, footprint corners are inside
        Misaligned,        // fully inside but angled beyond the tolerance
        Unpositioned,      // inside and aligned, but a mission condition is unmet (rear depth, kerb gap…)
        TooFast,           // fully inside and aligned but still moving
        Holding,           // all conditions met; hold timer running
        Parked,            // held for the full duration: success
    }
}
