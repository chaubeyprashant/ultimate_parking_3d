using UnityEngine;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Environment
{
    /// <summary>
    /// Road-direction rules in one place, so a layout can be authored once and generated for left- or right-hand
    /// traffic. Vehicle controls are untouched: only where things go changes (start lane, kerb you park against,
    /// which way parked cars face, the "keep left/right" prompt).
    /// "Right" below is the right-hand side when looking along a road's travel direction.
    /// </summary>
    public static class TrafficSide
    {
        /// <summary>+1 when traffic keeps right, -1 when it keeps left.</summary>
        public static int KeepSign(RoadDirection d) => d == RoadDirection.LeftHand ? -1 : 1;

        /// <summary>Unit vector to the right of a travel direction (on the ground plane).</summary>
        public static Vector3 RightOf(Vector3 forward)
        {
            forward.y = 0f;
            return Vector3.Cross(Vector3.up, forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward);
        }

        /// <summary>Centre of the lane used when driving along <paramref name="forward"/> on a two-lane road.</summary>
        public static Vector3 LaneCentre(Vector3 roadCentre, Vector3 forward, float laneWidth, RoadDirection d) =>
            roadCentre + RightOf(forward) * (laneWidth * 0.5f * KeepSign(d));

        /// <summary>
        /// Kerb side for parallel parking, relative to the travel direction: the kerb on your own side of the road
        /// (+1 right in right-hand traffic, -1 left in left-hand traffic). Matches ParallelParkObjective.kerbSide
        /// when the slot's yaw is the travel direction.
        /// </summary>
        public static int KerbSide(RoadDirection d) => KeepSign(d);

        /// <summary>Yaw of a car parked at the kerb on one side of a road whose travel direction is <paramref name="roadYaw"/>.</summary>
        public static float ParkedYaw(float roadYaw, int sideOfRoad, RoadDirection d) =>
            sideOfRoad == KeepSign(d) ? roadYaw : roadYaw + 180f;

        /// <summary>Signed offset (metres, along RightOf) of the kerb-side parking strip on your own side.</summary>
        public static float KerbOffset(float halfRoadWidth, float parkedHalfWidth, RoadDirection d) =>
            KeepSign(d) * (halfRoadWidth - parkedHalfWidth);

        public static string KeepPrompt(RoadDirection d) => d == RoadDirection.LeftHand ? "DRIVE ON THE LEFT" : "DRIVE ON THE RIGHT";
    }
}
