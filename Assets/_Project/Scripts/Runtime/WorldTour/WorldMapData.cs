using System;
using UnityEngine;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// Triangulated world map (Natural Earth 1:110m, public domain) as stored in the map TextAsset: flat
    /// vertex arrays (x, y pairs in 0..1) and triangle indices, for all land and for each tour country.
    /// </summary>
    [Serializable]
    public sealed class WorldMapData
    {
        [Serializable]
        public sealed class MeshPart
        {
            public string id;
            public float[] v = Array.Empty<float>();
            public int[] t = Array.Empty<int>();

            public int VertexCount => v.Length / 2;
            public Vector2 Vertex(int i) => new Vector2(v[2 * i], v[2 * i + 1]);

            /// <summary>True when the normalised point lies inside any triangle of this part.</summary>
            public bool Contains(Vector2 p)
            {
                for (int i = 0; i + 2 < t.Length; i += 3)
                    if (InTriangle(p, Vertex(t[i]), Vertex(t[i + 1]), Vertex(t[i + 2]))) return true;
                return false;
            }

            private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                return !(neg && pos);
            }

            private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        public string projection;
        public float latMin, latMax;
        public string source;
        public MeshPart land = new MeshPart();
        public MeshPart[] countries = Array.Empty<MeshPart>();

        public static WorldMapData Parse(TextAsset asset) =>
            asset != null ? JsonUtility.FromJson<WorldMapData>(asset.text) : null;

        public MeshPart Country(string id)
        {
            foreach (MeshPart c in countries)
                if (c.id == id) return c;
            return null;
        }
    }
}
