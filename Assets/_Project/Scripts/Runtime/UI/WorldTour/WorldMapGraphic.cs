using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Draws the world map as UI geometry from the triangulated Natural Earth mesh: a soft drop shadow, all land,
    /// then each tour country in its own colour (locked / open / completed / selected), plus a faint graticule.
    /// Vector geometry stays sharp at any zoom, and recolouring a country is just a mesh rebuild.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldMapGraphic : MaskableGraphic
    {
        [SerializeField] private TextAsset mapAsset;
        [SerializeField] private Color landColor = new Color(0.20f, 0.23f, 0.27f, 1f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.004f);
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.045f);
        [SerializeField, Min(0f)] private float gridWidth = 1.2f;

        private WorldMapData data;
        private readonly Dictionary<string, Color> countryColors = new Dictionary<string, Color>();

        public WorldMapData Data => data ??= WorldMapData.Parse(mapAsset);

        public void SetCountryColor(string id, Color c)
        {
            if (countryColors.TryGetValue(id, out Color old) && old == c) return;
            countryColors[id] = c;
            SetVerticesDirty();
        }

        public Color CountryColor(string id) => countryColors.TryGetValue(id, out Color c) ? c : landColor;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Data == null) return;
            Rect r = rectTransform.rect;

            // Graticule: meridians every 30°, parallels every 20° (under the land).
            for (int lon = -180; lon <= 180; lon += 30)
            {
                float x = r.xMin + (lon + 180f) / 360f * r.width;
                Quad(vh, new Vector2(x - gridWidth * 0.5f, r.yMin), new Vector2(x + gridWidth * 0.5f, r.yMax), gridColor);
            }
            for (int lat = -40; lat <= 80; lat += 20)
            {
                float y = r.yMin + WorldMapProjection.ToNormalized(lat, 0f).y * r.height;
                Quad(vh, new Vector2(r.xMin, y - gridWidth * 0.5f), new Vector2(r.xMax, y + gridWidth * 0.5f), gridColor);
            }

            AddPart(vh, Data.land, r, shadowColor, shadowOffset);
            AddPart(vh, Data.land, r, landColor, Vector2.zero);
            foreach (WorldMapData.MeshPart c in Data.countries)
                if (countryColors.TryGetValue(c.id, out Color col)) AddPart(vh, c, r, col, Vector2.zero);
        }

        private static void AddPart(VertexHelper vh, WorldMapData.MeshPart part, Rect r, Color col, Vector2 offset)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            for (int i = 0; i < part.VertexCount; i++)
            {
                Vector2 n = part.Vertex(i) + offset;
                v.position = new Vector3(r.xMin + n.x * r.width, r.yMin + n.y * r.height);
                vh.AddVert(v);
            }
            for (int i = 0; i + 2 < part.t.Length; i += 3)
                vh.AddTriangle(start + part.t[i], start + part.t[i + 1], start + part.t[i + 2]);
        }

        private static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color col)
        {
            int s = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = new Vector3(min.x, min.y); vh.AddVert(v);
            v.position = new Vector3(min.x, max.y); vh.AddVert(v);
            v.position = new Vector3(max.x, max.y); vh.AddVert(v);
            v.position = new Vector3(max.x, min.y); vh.AddVert(v);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s + 2, s + 3, s);
        }
    }
}
