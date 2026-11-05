using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Generates the UI's own assets: TextMeshPro essentials, SDF font assets from the bundled OFL fonts
    /// (Rajdhani for headings, Barlow for body), and anti-aliased sprites drawn from signed distance functions
    /// (rounded panels, pill, circle, star, lock, coin, chevrons, background gradient, glow).
    /// </summary>
    public static class UiAssets
    {
        public const string UiDir = "Assets/_Project/UI";
        public const string FontsDir = UiDir + "/Fonts";
        public const string SpritesDir = UiDir + "/Sprites";
        public const string HeadingFontPath = FontsDir + "/Rajdhani-Bold SDF.asset";
        public const string BodyFontPath = FontsDir + "/Barlow-SemiBold SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        public static TMP_FontAsset HeadingFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadingFontPath);
        public static TMP_FontAsset BodyFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/{name}.png");

        public static void GenerateAll()
        {
            ImportTmpEssentials();
            Directory.CreateDirectory(SpritesDir);
            CreateFont(FontsDir + "/Rajdhani-Bold.ttf", HeadingFontPath);
            CreateFont(FontsDir + "/Barlow-SemiBold.ttf", BodyFontPath);
            GenerateSprites();
            AssetDatabase.SaveAssets();
        }

        public static bool TmpReady => File.Exists(TmpSettingsPath);

        private static void ImportTmpEssentials()
        {
            if (TmpReady) return;
            string package = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(package))
            {
                Debug.LogError($"[Bootstrap] TMP Essential Resources not found at {package}");
                return;
            }
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            Debug.Log($"[Bootstrap] Imported TMP Essential Resources: {(TmpReady ? "ok" : "pending (re-run bootstrap)")}");
        }

        private static void CreateFont(string ttfPath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError($"[Bootstrap] Font missing: {ttfPath}"); return; }
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;   // keep: regenerating breaks text references

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[Bootstrap] {fa.name} lacks: {missing}");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ sprites

        private static void GenerateSprites()
        {
            const float aa = 1f;
            WriteSprite("RoundRect", 96, 96, 30, (x, y) => Fill(RoundRect(x, y, 48, 48, 48, 48, 24), aa));
            WriteSprite("RoundRectOutline", 96, 96, 30, (x, y) => Stroke(RoundRect(x, y, 48, 48, 48, 48, 24), 3f, aa));
            WriteSprite("Pill", 128, 64, 31, (x, y) => Fill(RoundRect(x, y, 64, 32, 64, 32, 32), aa));
            WriteSprite("PillOutline", 128, 64, 31, (x, y) => Stroke(RoundRect(x, y, 64, 32, 64, 32, 32), 3f, aa));
            WriteSprite("Circle", 64, 64, 0, (x, y) => Fill(Circle(x, y, 32, 32, 30), aa));
            WriteSprite("Star", 96, 96, 0, (x, y) => Fill(Star(x, y, 48, 50, 44, 18), aa));
            WriteSprite("Lock", 96, 96, 0, (x, y) =>
            {
                float body = RoundRect(x, y, 48, 34, 30, 24, 6);
                // Shackle: upper half-ring, with straight legs running down into the body.
                float shackle = y >= 58f
                    ? Mathf.Abs(Circle(x, y, 48, 58, 17)) - 5f
                    : Mathf.Max(Mathf.Min(Mathf.Abs(x - 31f), Mathf.Abs(x - 65f)) - 5f, 40f - y);
                float keyhole = Circle(x, y, 48, 36, 6);
                return Fill(Mathf.Max(Mathf.Min(body, shackle), -keyhole), aa);
            });
            WriteSprite("Coin", 96, 96, 0, (x, y) =>
            {
                float outer = Circle(x, y, 48, 48, 44);
                float ring = Mathf.Abs(Circle(x, y, 48, 48, 32)) - 3f;
                float a = Fill(outer, aa);
                float shade = Mathf.Lerp(0.82f, 1f, y / 96f);
                Color gold = new Color(1f, 0.78f, 0.24f) * shade;
                Color dark = new Color(0.78f, 0.52f, 0.08f);
                Color c = Color.Lerp(gold, dark, Fill(ring, aa));
                c.a = a;
                return c;
            });
            WriteSprite("ChevronLeft", 64, 64, 0, (x, y) => Fill(Mathf.Min(Segment(x, y, 40, 12, 22, 32), Segment(x, y, 22, 32, 40, 52)) - 5f, aa));
            WriteSprite("ChevronRight", 64, 64, 0, (x, y) => Fill(Mathf.Min(Segment(x, y, 24, 12, 42, 32), Segment(x, y, 42, 32, 24, 52)) - 5f, aa));
            WriteSprite("Check", 64, 64, 0, (x, y) => Fill(Mathf.Min(Segment(x, y, 12, 32, 26, 18), Segment(x, y, 26, 18, 52, 46)) - 5f, aa));
            WriteSprite("Glow", 256, 256, 0, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 128f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                return new Color(1f, 1f, 1f, a);
            });
            WriteSprite("Gradient", 8, 256, 0, (x, y) =>
            {
                float t = y / 255f;
                Color bottom = new Color(0.027f, 0.031f, 0.039f), top = new Color(0.078f, 0.094f, 0.11f);
                return Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
            });
            WriteSprite("SideFade", 256, 8, 0, (x, y) =>
            {
                float t = x / 255f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, t)) * 0.92f);
            });
            WriteSprite("Pause", 64, 64, 0, (x, y) => Fill(Mathf.Min(RoundRect(x, y, 23, 32, 6, 18, 3), RoundRect(x, y, 41, 32, 6, 18, 3)), aa));
            WriteSprite("Gear", 64, 64, 0, (x, y) =>
            {
                float ang = Mathf.Atan2(y - 32f, x - 32f);
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
                float teeth = 22f + 5f * Mathf.Clamp(Mathf.Cos(ang * 8f) * 3f, -1f, 1f);   // 8 squared-off teeth
                float d = Mathf.Max(r - teeth, -(r - 9f));                                  // ring with a hole
                return Fill(d, aa);
            });
            WriteSprite("Vignette", 128, 128, 0, (x, y) =>
            {
                float dx = Mathf.Abs(x - 64f) / 64f, dy = Mathf.Abs(y - 64f) / 64f;
                float e = Mathf.Max(dx, dy);
                return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, e)));
            });
            WriteSprite("Stripes", 64, 64, 0, (x, y) =>
            {
                float v = Mathf.Repeat((x + y) / 16f, 1f);
                return new Color(1f, 1f, 1f, v < 0.5f ? 1f : 0f);
            }, wrap: TextureWrapMode.Repeat);
            AssetDatabase.Refresh();
        }

        private static void WriteSprite(string name, int w, int h, int border, Func<float, float, object> shader,
            TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    object v = shader(x + 0.5f, y + 0.5f);
                    px[y * w + x] = v is Color c ? c : new Color(1f, 1f, 1f, (float)v);
                }
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{SpritesDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = wrap;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 100f;
            ti.spriteBorder = new Vector4(border, border, border, border);
            ti.SaveAndReimport();
        }

        // Signed distance helpers (negative inside), in pixels.
        private static float Fill(float d, float aa) => Mathf.Clamp01(0.5f - d / aa);
        private static float Stroke(float d, float width, float aa) => Fill(Mathf.Abs(d + width * 0.5f) - width * 0.5f, aa);
        private static float Circle(float x, float y, float cx, float cy, float r) => Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;

        private static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(x - cx) - (hx - r), Mathf.Abs(y - cy) - (hy - r));
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// <summary>Five-pointed star: polygon distance with an inside test.</summary>
        private static float Star(float x, float y, float cx, float cy, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            Vector2 p = new Vector2(x, y);
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                d = Mathf.Min(d, Segment(x, y, pts[j].x, pts[j].y, pts[i].x, pts[i].y));
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            }
            return inside ? -d : d;
        }
    }
}
