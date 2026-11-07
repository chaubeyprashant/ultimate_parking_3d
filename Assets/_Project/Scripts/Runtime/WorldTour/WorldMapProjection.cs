using UnityEngine;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// Miller cylindrical projection cropped to latitudes <see cref="LatMin"/>..<see cref="LatMax"/>, normalised to
    /// 0..1 (x east, y north). Must match Tools/worldmap/build_map.py, which produced the map mesh.
    /// </summary>
    public static class WorldMapProjection
    {
        public const float LatMin = -57f, LatMax = 80f;

        private static readonly float Y0 = Miller(LatMin), Y1 = Miller(LatMax);

        /// <summary>Width / height of the projected map.</summary>
        public static float Aspect => 2f * Mathf.PI / (Y1 - Y0);

        private static float Miller(float latDeg) =>
            1.25f * Mathf.Log(Mathf.Tan(Mathf.PI / 4f + 0.4f * latDeg * Mathf.Deg2Rad));

        public static Vector2 ToNormalized(float latDeg, float lonDeg)
        {
            float lat = Mathf.Clamp(latDeg, LatMin, LatMax);
            return new Vector2((lonDeg + 180f) / 360f, (Miller(lat) - Y0) / (Y1 - Y0));
        }

        public static Vector2 ToNormalized(Vector2 latLon) => ToNormalized(latLon.x, latLon.y);

        /// <summary>Normalised rectangle covering a latitude/longitude box.</summary>
        public static Rect Box(float latMin, float latMax, float lonMin, float lonMax)
        {
            Vector2 a = ToNormalized(latMin, lonMin), b = ToNormalized(latMax, lonMax);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
    }
}
