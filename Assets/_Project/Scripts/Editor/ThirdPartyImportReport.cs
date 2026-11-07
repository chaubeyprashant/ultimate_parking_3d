using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Proves what Unity actually imported from Assets/_Project/ThirdParty: for every model file, the importer used,
    /// mesh / triangle / material / texture counts, largest texture, real-world bounds (scale check) and wheel nodes.
    /// Writes Documentation/IMPORT_REPORT.md. Menu: Tools ▸ Ultimate Car Parking ▸ Third-party Import Report.
    /// Batch: -executeMethod UltimateCarParking.EditorTools.ThirdPartyImportReport.Run
    /// </summary>
    public static class ThirdPartyImportReport
    {
        private const string Root = "Assets/_Project/ThirdParty";

        [MenuItem("Tools/Ultimate Car Parking/Third-party Import Report")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var sb = new StringBuilder();
            sb.AppendLine("# Third-party import report").AppendLine();
            sb.AppendLine($"Generated {System.DateTime.Now:yyyy-MM-dd HH:mm} by `ThirdPartyImportReport` from what Unity's AssetDatabase holds.").AppendLine();
            sb.AppendLine("| File | Importer | Imported | Meshes | Triangles | Materials | Textures (max px) | Bounds (m) | Notes |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

            int ok = 0, failed = 0;
            string[] exts = { ".glb", ".gltf", ".fbx", ".obj" };
            foreach (string path in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories).Where(p => exts.Contains(Path.GetExtension(p).ToLowerInvariant())).OrderBy(p => p))
            {
                string assetPath = path.Replace('\\', '/');
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                Object[] subs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                var meshes = subs.OfType<Mesh>().ToList();
                var mats = subs.OfType<Material>().ToList();
                var texs = subs.OfType<Texture2D>().ToList();
                // External textures referenced by a .gltf live next to it.
                if (texs.Count == 0)
                    texs = AssetDatabase.FindAssets("t:Texture2D", new[] { Path.GetDirectoryName(assetPath).Replace('\\', '/') })
                        .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null).ToList();
                long tris = meshes.Sum(m => (long)Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)) / 3);
                int maxTex = texs.Count > 0 ? texs.Max(t => Mathf.Max(t.width, t.height)) : 0;

                string bounds = "-", notes = "";
                if (go != null)
                {
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        // Bounds of the prefab asset are not computed (not in a scene); instantiate temporarily.
                        GameObject tmp = Object.Instantiate(go);
                        Bounds b = default;
                        bool first = true;
                        foreach (Renderer r in tmp.GetComponentsInChildren<Renderer>(true))
                        {
                            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                        }
                        Object.DestroyImmediate(tmp);
                        bounds = $"{b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}";
                    }
                    var wheels = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Wheel_") && !t.name.Contains("_Whee")).ToList();
                    if (wheels.Count > 0) notes += $"{wheels.Count} wheel nodes; ";
                    var bodies = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("Body") || t.name.EndsWith("body")).Select(t => t.name).ToList();
                    if (bodies.Count > 0) notes += "bodies: " + string.Join(", ", bodies) + "; ";
                }

                bool imported = go != null && meshes.Count > 0;
                if (imported) ok++; else failed++;
                sb.AppendLine($"| `{assetPath.Substring(Root.Length + 1)}` | {(importer != null ? importer.GetType().Name : "none")} | {(imported ? "yes" : "**NO**")} | {meshes.Count} | {tris:N0} | {mats.Count} | {texs.Count} ({maxTex}) | {bounds} | {notes} |");
            }

            sb.AppendLine().AppendLine($"**{ok} imported, {failed} failed.**");
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/IMPORT_REPORT.md", sb.ToString());
            Debug.Log($"[ImportReport] {ok} imported, {failed} failed — Documentation/IMPORT_REPORT.md");
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
