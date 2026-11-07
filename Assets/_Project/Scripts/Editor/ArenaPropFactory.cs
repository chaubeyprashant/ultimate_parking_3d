using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Turns imported country-arena models (glTF/GLB from Sketchfab, Poly Haven) into game prefabs:
    /// pivot on the ground at the centre, optional real-world scale fix (target length or height), front turned to +Z,
    /// a BoxCollider fitted to the visuals (or a flat ground collider for road pieces), static flags for batching.
    /// Imported meshes and materials are referenced, never copied. Also writes a node inventory used to pick parts.
    /// </summary>
    public static class ArenaPropFactory
    {
        public const string SourceRoot = "Assets/_Project/ThirdParty/Arenas";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Arenas";

        public enum Fit { None, Length, Height, Width, Scale }
        public enum ColliderKind { Box, Ground, None }

        public sealed class Spec
        {
            public string name;
            public string source;                     // path under SourceRoot, e.g. "Mumbai/mum_bus.glb"
            public Func<Transform, IEnumerable<Transform>> pick = r => new[] { r };
            public float yaw;                         // extra rotation so the model's front faces +Z
            public Fit fit = Fit.None;
            public float size;                        // metres for the fitted dimension
            public ColliderKind collider = ColliderKind.Box;
            public float colliderHeightCap;           // e.g. 1.6 m for cars so roofs don't snag the camera
            public string[] strip;                    // mesh node name fragments to leave out (hidden high-poly details)
            public bool combine;                      // merge all parts into one mesh per material (many-part models: fewer draw calls)
        }

        public static string PrefabPath(string arena, string name) => $"{PrefabRoot}/{arena}/{name}.prefab";
        public static GameObject Load(string arena, string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(arena, name));

        /// <summary>Picks direct children of the model root whose names contain <paramref name="fragment"/> (case-insensitive).</summary>
        public static Func<Transform, IEnumerable<Transform>> Named(params string[] fragments) => root =>
        {
            var hits = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.GetComponent<MeshFilter>() != null || t.childCount > 0)
                .Where(t => fragments.Any(f => t.name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            // Keep only top-most matches (a matching parent already includes its children).
            var top = hits.Where(t => !hits.Any(h => h != t && t.IsChildOf(h))).ToList();
            if (top.Count == 0) throw new Exception($"no node matching {string.Join("/", fragments)} under {root.name}");
            return top;
        };

        public static GameObject Make(string arena, Spec s)
        {
            string src = $"{SourceRoot}/{s.source}";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(src);
            if (source == null) { Debug.LogError($"[ArenaProps] missing {src}"); return null; }
            Directory.CreateDirectory($"{PrefabRoot}/{arena}");
            GameObject inst = Object.Instantiate(source);
            try
            {
                List<Transform> parts = s.pick(inst.transform).ToList();
                Quaternion rot = Quaternion.Euler(0f, s.yaw, 0f);
                // Bounds of the rotated visuals (so length is measured along the final +Z).
                Bounds b = RotatedBounds(parts, rot);
                if (b.size == Vector3.zero) throw new Exception("no renderers");
                float scale = s.fit switch
                {
                    Fit.Length => s.size / Mathf.Max(0.001f, b.size.z),
                    Fit.Height => s.size / Mathf.Max(0.001f, b.size.y),
                    Fit.Width => s.size / Mathf.Max(0.001f, b.size.x),
                    Fit.Scale => s.size,
                    _ => 1f,
                };
                Vector3 originRot = new Vector3(b.center.x, b.min.y, b.center.z);   // in rotated space

                var root = new GameObject(s.name);
                foreach (Transform part in parts)
                    foreach (MeshFilter mf in part.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (s.strip != null && s.strip.Any(f => mf.name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mr == null || mf.sharedMesh == null) continue;
                        var go = new GameObject(mf.name);
                        go.transform.SetParent(root.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials.Select(m => Convert(m, arena, src)).ToArray();
                        Vector3 p = rot * mf.transform.position - originRot;
                        go.transform.SetLocalPositionAndRotation(p * scale, rot * mf.transform.rotation);
                        go.transform.localScale = mf.transform.lossyScale * scale;
                    }

                Vector3 size = b.size * scale;
                if (s.collider == ColliderKind.Box)
                {
                    if (s.colliderHeightCap > 0f) size.y = Mathf.Min(size.y, s.colliderHeightCap);
                    var box = root.AddComponent<BoxCollider>();
                    box.size = size;
                    box.center = new Vector3(0f, size.y * 0.5f, 0f);
                }
                else if (s.collider == ColliderKind.Ground)
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.size = new Vector3(size.x, 0.1f, size.z);
                    box.center = new Vector3(0f, size.y - 0.05f, 0f);   // top surface drivable
                }
                const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
                GameObjectUtility.SetStaticEditorFlags(root, flags);
                foreach (Transform t in root.transform) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                if (s.combine) Combine(root, arena, s.name);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(arena, s.name));
                Object.DestroyImmediate(root);
                return prefab;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ArenaProps] {arena}/{s.name}: {e.Message}");
                return null;
            }
            finally { Object.DestroyImmediate(inst); }
        }

        /// <summary>
        /// Merges every mesh part under <paramref name="root"/> into one mesh per material (submeshes split out), saved
        /// as assets in Art/Arenas/&lt;arena&gt;/Meshes. A 300-part model becomes a handful of renderers.
        /// </summary>
        private static void Combine(GameObject root, string arena, string name)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            var parts = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            foreach (MeshFilter mf in parts)
            {
                Material[] mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                {
                    Material m = mats[Mathf.Min(sub, mats.Length - 1)];
                    if (m == null) continue;
                    if (!groups.TryGetValue(m, out var list)) groups[m] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = toRoot * mf.transform.localToWorldMatrix });
                }
            }
            foreach (MeshFilter mf in parts) Object.DestroyImmediate(mf.gameObject);
            string dir = $"{TextureRoot}/{arena}/Meshes";
            Directory.CreateDirectory(dir);
            int i = 0;
            foreach (var kv in groups)
            {
                long verts = 0;
                foreach (CombineInstance ci in kv.Value) verts += ci.mesh.vertexCount;
                // Size budget: 16-bit indices when they fit, and no tangents / vertex colours / extra UV sets in combined meshes.
                var mesh = new Mesh { name = $"{name}_{i}", indexFormat = verts <= 65000 ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.tangents = null; mesh.colors32 = null; mesh.uv2 = null; mesh.uv3 = null; mesh.uv4 = null;
                mesh.RecalculateBounds();
                string path = $"{dir}/{name}_{i}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject($"{name}_{i}");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                i++;
            }
            Debug.Log($"[ArenaProps] {arena}/{name}: {parts.Count} parts combined into {i} meshes");
        }

        // ------------------------------------------------------------------ materials

        public const bool StripNormalMaps = true;
        public const string MaterialRoot = "Assets/_Project/Materials/Arenas";
        public const string TextureRoot = "Assets/_Project/Art/Arenas";
        private static readonly Dictionary<string, Material> Converted = new Dictionary<string, Material>();
        public static int TextureMismatches { get; private set; }

        /// <summary>
        /// Replaces a glTFast shader-graph material with a URP/Lit material whose textures are extracted to PNG files
        /// with unique names (glTFast names every embedded texture "image_N", and the same names recur across models).
        /// Each texture is checked to come from <paramref name="sourcePath"/>; a texture from another model is replaced
        /// by the same-named texture inside the right file. Extracted textures are capped at 1024 px, compressed.
        /// </summary>
        /// <summary>Converts a glTF material for use outside the arena factory (older real-asset prefabs).</summary>
        public static Material ConvertMaterial(Material src, string folder = "Shared") =>
            src == null ? null : Convert(src, folder, AssetDatabase.GetAssetPath(src));

        public static Material[] ConvertMaterials(Material[] src, string folder = "Shared") =>
            src.Select(m => ConvertMaterial(m, folder)).ToArray();

        private static Material Convert(Material src, string arena, string sourcePath)
        {
            if (src == null) return null;
            string model = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string key = model + "/" + src.name;
            if (Converted.TryGetValue(key, out Material done) && done != null) return done;
            if (!src.shader.name.Contains("glTF")) return src;   // already a Unity material

            Directory.CreateDirectory($"{MaterialRoot}/{arena}");
            Directory.CreateDirectory($"{TextureRoot}/{arena}");
            string safe = Safe(src.name);
            string matPath = $"{MaterialRoot}/{arena}/{model}__{safe}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }

            Color baseColor = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : Color.white;
            Texture baseTex = OwnTexture(src, "baseColorTexture", sourcePath);
            Texture normTex = OwnTexture(src, "normalTexture", sourcePath);
            m.SetColor("_BaseColor", baseColor);
            m.SetTexture("_BaseMap", baseTex != null ? Extract(baseTex, $"{TextureRoot}/{arena}/{model}__{safe}__base.png", false) : null);
            // glTFast stores UV flips / KHR_texture_transform as the texture's scale-offset: keep it.
            if (src.HasProperty("baseColorTexture_ST"))
            {
                Vector4 st = src.GetVector("baseColorTexture_ST");
                if (st.x != 0f || st.y != 0f)
                {
                    m.SetTextureScale("_BaseMap", new Vector2(st.x, st.y));
                    m.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
                }
            }
            // Size budget: arena props ship without normal maps (about 130 MB less; far building faces barely show them).
            Texture2D n = (!StripNormalMaps && normTex != null) ? Extract(normTex, $"{TextureRoot}/{arena}/{model}__{safe}__normal.png", true) : null;
            m.SetTexture("_BumpMap", n);
            if (n != null && src.HasProperty("normalTexture_ST"))
            {
                Vector4 nst = src.GetVector("normalTexture_ST");
                if (nst.x != 0f || nst.y != 0f) { m.SetTextureScale("_BumpMap", new Vector2(nst.x, nst.y)); m.SetTextureOffset("_BumpMap", new Vector2(nst.z, nst.w)); }
            }
            if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            float metal = src.HasProperty("metallicFactor") ? src.GetFloat("metallicFactor") : 0f;
            float rough = src.HasProperty("roughnessFactor") ? src.GetFloat("roughnessFactor") : 0.6f;
            m.SetFloat("_Metallic", Mathf.Clamp01(metal) * 0.8f);
            m.SetFloat("_Smoothness", Mathf.Clamp01(1f - rough) * 0.75f);
            if (src.HasProperty("emissiveFactor"))
            {
                Color e = src.GetColor("emissiveFactor");
                if (e.maxColorComponent > 0.01f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", e); }
            }
            // Alpha: glTF MASK (cutout) and BLEND (transparent) map to URP alpha clip / transparent surface.
            float cutoff = src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0f;
            if (src.renderQueue >= 3000 || baseColor.a < 0.99f)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            else if (src.renderQueue >= 2450 && cutoff > 0f)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
            }
            // glTFast's URP graphs use the same _Cull enum as URP Lit (0 = off / double-sided, 2 = back).
            if (src.HasProperty("_Cull")) m.SetFloat("_Cull", src.GetFloat("_Cull"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            Converted[key] = m;
            return m;
        }

        /// <summary>A material texture, guaranteed to come from <paramref name="sourcePath"/>.</summary>
        private static Texture OwnTexture(Material src, string prop, string sourcePath)
        {
            if (!src.HasProperty(prop)) return null;
            Texture t = src.GetTexture(prop);
            if (t == null) return null;
            string from = AssetDatabase.GetAssetPath(t);
            if (from == sourcePath) return t;
            // .gltf files keep their textures as separate files next to them: those belong to the model too.
            string dir = System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            if (sourcePath.EndsWith(".gltf") && from.StartsWith(dir + "/")) return t;
            TextureMismatches++;
            Texture own = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Texture>().FirstOrDefault(x => x.name == t.name);
            Debug.LogWarning($"[ArenaProps] {sourcePath} material '{src.name}' referenced texture '{t.name}' from {from}; using the model's own {(own != null ? "texture" : "(none found)")}");
            return own;
        }

        private static Texture2D Extract(Texture tex, string path, bool normal)
        {
            if (!File.Exists(path))
            {
                int w = Mathf.Min(tex.width, 1024), h = Mathf.Min(tex.height, 1024);
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, normal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false, normal);
                t2.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t2.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(path, t2.EncodeToPNG());
                Object.DestroyImmediate(t2);
                AssetDatabase.ImportAsset(path);
            }
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                TextureImporterPlatformSettings android = ti.GetPlatformTextureSettings("Android");
                bool dirty = ti.maxTextureSize != 1024 || ti.textureCompression != TextureImporterCompression.Compressed ||
                             (normal && ti.textureType != TextureImporterType.NormalMap) ||
                             !android.overridden || android.format != (normal ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8);
                if (dirty)
                {
                    // Size budget: ASTC 8x8 (2 bits/pixel) for colour maps on Android.
                    ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android", overridden = true, maxTextureSize = 1024, compressionQuality = 50,
                        format = normal ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8,
                    });
                    ti.maxTextureSize = 1024;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.mipmapEnabled = true;
                    if (normal) ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        private static Bounds RotatedBounds(IEnumerable<Transform> parts, Quaternion rot)
        {
            bool first = true;
            var b = new Bounds();
            foreach (Transform part in parts)
                foreach (MeshFilter mf in part.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Bounds mb = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 w = rot * mf.transform.TransformPoint(c);
                        if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
                    }
                }
            return b;
        }

        /// <summary>
        /// Writes Documentation/ARENA_MODEL_NODES.md: for each model under SourceRoot, its mesh nodes with size (m) and
        /// triangle counts, so builders can pick parts by name. Batch: -executeMethod ...ArenaPropFactory.Inventory
        /// </summary>
        [MenuItem("Tools/Ultimate Car Parking/Arena Model Inventory")]
        public static void Inventory()
        {
            var sb = new StringBuilder("# Arena model inventory\n\nGenerated by ArenaPropFactory.Inventory. Sizes in metres (x, y, z) as imported.\n");
            foreach (string path in Directory.GetFiles(SourceRoot, "*.*", SearchOption.AllDirectories)
                         .Where(p => p.EndsWith(".glb") || p.EndsWith(".gltf")).OrderBy(p => p))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                sb.Append($"\n## {path.Substring(SourceRoot.Length + 1)}\n\n");
                if (go == null) { sb.Append("NOT IMPORTED\n"); continue; }
                GameObject inst = Object.Instantiate(go);
                try
                {
                    var all = inst.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToList();
                    Bounds total = RotatedBounds(new[] { inst.transform }, Quaternion.identity);
                    int tris = all.Sum(m => m.sharedMesh.triangles.Length / 3);
                    sb.Append($"total {Fmt(total.size)} m, {all.Count} meshes, {tris:N0} tris, min y {total.min.y:F2}\n\n| node (path) | size | tris |\n|---|---|---|\n");
                    foreach (MeshFilter m in all.Take(80))
                    {
                        Bounds nb = RotatedBounds(new[] { m.transform }, Quaternion.identity);
                        sb.Append($"| {Path(m.transform, inst.transform)} | {Fmt(nb.size)} | {m.sharedMesh.triangles.Length / 3:N0} |\n");
                    }
                    if (all.Count > 80) sb.Append($"| … {all.Count - 80} more | | |\n");
                }
                finally { Object.DestroyImmediate(inst); }
            }
            File.WriteAllText("Documentation/ARENA_MODEL_NODES.md", sb.ToString());
            Debug.Log("[ArenaProps] inventory written");
        }

        /// <summary>
        /// Renders every arena model (and every built arena prefab) from the front (+Z, looking back along -Z) and
        /// from the right (+X) to ../UltimateCarParking3D_logs/arena_preview/, for orientation and look checks.
        /// </summary>
        public static void PreviewAll()
        {
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arena_preview");
            Directory.CreateDirectory(dir);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.72f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.sun = sun;
            var rt = new RenderTexture(512, 384, 24) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 2 };
            var tex = new Texture2D(512, 384, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f); cam.fieldOfView = 35f;
            var paths = Directory.GetFiles(SourceRoot, "*.gl*", SearchOption.AllDirectories).Where(p => p.EndsWith(".glb") || p.EndsWith(".gltf"))
                .Concat(Directory.Exists(PrefabRoot) ? Directory.GetFiles(PrefabRoot, "*.prefab", SearchOption.AllDirectories) : new string[0]);
            string filter = System.Environment.GetEnvironmentVariable("ARENA_PREVIEW_FILTER");
            foreach (string path in paths.Select(p => p.Replace('\\', '/')).Where(p => string.IsNullOrEmpty(filter) || System.Text.RegularExpressions.Regex.IsMatch(p, filter)))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                GameObject inst = Object.Instantiate(go);
                try
                {
                    Bounds b = RotatedBounds(new[] { inst.transform }, Quaternion.identity);
                    if (b.size == Vector3.zero) continue;
                    float r = b.extents.magnitude;
                    // Red marker pole at the +Z end so "front" is unambiguous in the side view.
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.transform.position = new Vector3(b.center.x, b.center.y, b.max.z + r * 0.08f);
                    marker.transform.localScale = new Vector3(r * 0.05f, b.size.y + r * 0.1f, r * 0.05f);
                    var mm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mm.color = Color.red;
                    marker.GetComponent<Renderer>().sharedMaterial = mm;
                    foreach (var (tag, dirv) in new[] { ("front", Vector3.forward), ("side", Vector3.right), ("top", new Vector3(0.35f, 2.2f, -0.6f)) })
                    {
                        Vector3 from = b.center + (dirv + Vector3.up * 0.35f).normalized * r * 3.2f;
                        cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(b.center - from));
                        cam.nearClipPlane = r * 0.05f; cam.farClipPlane = r * 10f;
                        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 512, 384), 0, 0); tex.Apply(); RenderTexture.active = null;
                        string n = System.IO.Path.GetFileNameWithoutExtension(path);
                        File.WriteAllBytes(System.IO.Path.Combine(dir, $"{n}__{tag}.jpg"), tex.EncodeToJPG(85));
                    }
                    Object.DestroyImmediate(marker);
                }
                finally { Object.DestroyImmediate(inst); }
            }
            Debug.Log("[ArenaProps] previews written to " + dir);
        }

        /// <summary>
        /// Renders every top-level part of each model matching ARENA_PREVIEW_FILTER (wrappers with a single child are
        /// skipped) from four sides into one labelled sheet per model: ../UltimateCarParking3D_logs/arena_parts/.
        /// Used to pick parts (signs, buildings) by name. Batch: -executeMethod ...ArenaPropFactory.PreviewParts
        /// </summary>
        public static void PreviewParts()
        {
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arena_parts");
            Directory.CreateDirectory(dir);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.78f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(40f, 20f, 0f);
            const int T = 200;
            var rt = new RenderTexture(T, T, 24) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 2 };
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f); cam.fieldOfView = 30f;
            string filter = System.Environment.GetEnvironmentVariable("ARENA_PREVIEW_FILTER");
            foreach (string path in Directory.GetFiles(SourceRoot, "*.glb", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                         .Where(p => !string.IsNullOrEmpty(filter) && System.Text.RegularExpressions.Regex.IsMatch(p, filter)))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                GameObject inst = Object.Instantiate(go);
                try
                {
                    List<Transform> parts = new List<Transform> { inst.transform };
                    while (parts.Count == 1 && parts[0].childCount > 0) parts = parts[0].Cast<Transform>().ToList();
                    parts = parts.Where(t => t.GetComponentInChildren<Renderer>(true) != null).ToList();
                    var allParts = parts;
                    parts = parts.Take(48).ToList();   // keep the sheet under the max texture size; the .txt lists every part
                    int cols = 4 * 2, rows = Mathf.CeilToInt(parts.Count / 2f);
                    var sheet = new Texture2D(T * cols, T * rows, TextureFormat.RGB24, false);
                    var tex = new Texture2D(T, T, TextureFormat.RGB24, false);
                    var names = new StringBuilder();
                    for (int i = 0; i < parts.Count; i++)
                    {
                        foreach (Transform p in parts) p.gameObject.SetActive(p == parts[i]);
                        Bounds b = RotatedBounds(new[] { parts[i] }, Quaternion.identity);
                        float r = Mathf.Max(0.01f, b.extents.magnitude);
                        Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                        for (int d = 0; d < 4; d++)
                        {
                            Vector3 from = b.center + (dirs[d] + Vector3.up * 0.25f).normalized * r * 3.6f;
                            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(b.center - from));
                            cam.nearClipPlane = r * 0.05f; cam.farClipPlane = r * 10f;
                            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, T, T), 0, 0); RenderTexture.active = null;
                            int cx = (i % 2) * 4 + d, cy = rows - 1 - i / 2;
                            sheet.SetPixels(cx * T, cy * T, T, T, tex.GetPixels());
                        }
                        names.AppendLine($"{i}: {parts[i].name}  {Fmt(b.size)}  (views: +Z, +X, -Z, -X)");
                    }
                    sheet.Apply();
                    string n = System.IO.Path.GetFileNameWithoutExtension(path);
                    File.WriteAllBytes(System.IO.Path.Combine(dir, n + ".jpg"), sheet.EncodeToJPG(80));
                    for (int i = parts.Count; i < allParts.Count; i++) names.AppendLine($"{i}: {allParts[i].name}  (not rendered)");
                    File.WriteAllText(System.IO.Path.Combine(dir, n + ".txt"), names.ToString());
                }
                finally { Object.DestroyImmediate(inst); }
            }
            Debug.Log("[ArenaProps] part sheets written to " + dir);
        }

        private static string Fmt(Vector3 v) => $"{v.x:F2} x {v.y:F2} x {v.z:F2}";

        private static string Path(Transform t, Transform root)
        {
            var names = new List<string>();
            for (Transform c = t; c != null && c != root; c = c.parent) names.Add(c.name);
            names.Reverse();
            return string.Join("/", names.Count > 3 ? names.Skip(names.Count - 3) : names);
        }
    }
}
