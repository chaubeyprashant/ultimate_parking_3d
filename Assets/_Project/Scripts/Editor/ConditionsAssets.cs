using System.IO;
using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Generates the tiny textures and materials the road conditions need (headlight beam, rain streak, snow flake) under
    /// Assets/_Project/Resources/Conditions so they are in every build. Idempotent; uses the built-in Sprites/Default shader, which
    /// the mobile pipeline renders as plain alpha-blended unlit.
    /// </summary>
    public static class ConditionsAssets
    {
        public const string Dir = "Assets/_Project/Resources/Conditions";

        /// <summary>
        /// Unity's default "Fog Modes: Automatic" strips fog shader variants when no scene enables fog in the editor, which silently removed
        /// every fog effect from device builds (arena fog included). Keep all three modes so fog set at run time works.
        /// </summary>
        public static void KeepFogVariants()
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (all == null || all.Length == 0) { Debug.LogWarning("[Conditions] GraphicsSettings not found; fog variants not forced."); return; }
            var so = new SerializedObject(all[0]);
            SerializedProperty mode = so.FindProperty("m_FogStripping");
            if (mode == null) { Debug.LogWarning("[Conditions] m_FogStripping not found."); return; }
            mode.intValue = 1;   // Custom
            foreach (string k in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" }) { var p = so.FindProperty(k); if (p != null) p.boolValue = true; }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Conditions] fog shader variants kept (Fog Modes: Custom, all three)");
        }

        public static void Generate()
        {
            KeepFogVariants();
            Directory.CreateDirectory(Dir);
            Texture2D beam = Save("Beam", MakeBeam(64, 256));
            Texture2D rain = Save("RainStreak", MakeStreak(8, 64));
            Texture2D snow = Save("SnowFlake", MakeDot(32));
            Material("Beam", beam);
            Material("Rain", rain);
            Material("Snow", snow);
            AssetDatabase.SaveAssets();
            Debug.Log("[Conditions] generated beam, rain and snow textures and materials");
        }

        private static Texture2D MakeBeam(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (h - 1f);                           // 0 near the bumper, 1 far away
                    float u = Mathf.Abs(x / (w - 1f) * 2f - 1f);      // 0 centre, 1 edge
                    float along = Mathf.Pow(1f - v, 1.4f) * Mathf.SmoothStep(0f, 1f, v * 12f);
                    float across = Mathf.Pow(Mathf.Cos(u * Mathf.PI * 0.5f), 1.6f);
                    t.SetPixel(x, y, new Color(1f, 0.95f, 0.78f, 0.62f * along * across));
                }
            t.Apply();
            return t;
        }

        private static Texture2D MakeStreak(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (h - 1f), u = Mathf.Abs(x / (w - 1f) * 2f - 1f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Sin(v * Mathf.PI) * Mathf.Pow(1f - u, 1.5f)));
                }
            t.Apply();
            return t;
        }

        private static Texture2D MakeDot(int s)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2((s - 1) / 2f, (s - 1) / 2f)) / (s / 2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d * 0.3f)));
                }
            t.Apply();
            return t;
        }

        private static Texture2D Save(string name, Texture2D tex)
        {
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;   // tiny textures; compression would band the gradients
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Material(string name, Texture2D tex)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Sprites/Default");
            mat.mainTexture = tex;
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
        }
    }
}
