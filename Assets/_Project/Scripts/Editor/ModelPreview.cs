using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Renders an imported model from outside and from eye level inside it, and lists each mesh's bounds, so a merged
    /// environment model can be understood before building on it. Output: ../UltimateCarParking3D_logs/preview/.
    /// Batch: -executeMethod UltimateCarParking.EditorTools.ModelPreview.Garage
    /// </summary>
    public static class ModelPreview
    {
        public static void Garage() => Render("Assets/_Project/ThirdParty/Garage/ParkingGarage_idmental/parking_garage_free_download.glb", "garage", true);

        public static void Render(string assetPath, string name, bool exitWhenDone)
        {
            string dir = Path.GetFullPath("../UltimateCarParking3D_logs/preview");
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
            var rs = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);

            var sb = new StringBuilder($"{assetPath}\nbounds centre {b.center:F2} size {b.size:F2}\n");
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                var m = r.sharedMaterial;
                sb.AppendLine($"  {mf.name}: tris {mf.sharedMesh.triangles.Length / 3} bounds c {r.bounds.center:F2} s {r.bounds.size:F2} mat {m.name} " +
                              $"queue {m.renderQueue} emissive {(m.HasProperty("emissiveFactor") ? m.GetColor("emissiveFactor").ToString() : "-")}");
                foreach (string p in m.GetTexturePropertyNames())
                    if (m.GetTexture(p) != null) sb.AppendLine($"     {p}: {m.GetTexture(p).width}px");
                // Height histogram of vertices: tells floor / pillars / ceiling apart in a merged mesh.
                var ys = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v).y).ToArray();
                var hist = Enumerable.Range(0, 8).Select(i => ys.Count(y => y >= b.min.y + b.size.y * i / 8f && y < b.min.y + b.size.y * (i + 1) / 8f + 1e-4f));
                sb.AppendLine("     vertex heights (8 bands, low→high): " + string.Join(" ", hist));
            }
            File.WriteAllText(Path.Combine(dir, name + ".txt"), sb.ToString());

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f); cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f;

            float eye = b.min.y + 1.6f;
            Shot(cam, b.center + new Vector3(b.size.x, b.size.y * 2.5f, -b.size.z), b.center, Path.Combine(dir, name + "_outside.png"));
            Shot(cam, new Vector3(b.min.x + 1f, eye, b.min.z + 1f), new Vector3(b.max.x, eye - 0.6f, b.max.z), Path.Combine(dir, name + "_inside_diag.png"));
            Shot(cam, new Vector3(b.center.x, eye, b.min.z + 0.8f), new Vector3(b.center.x, eye - 0.4f, b.max.z), Path.Combine(dir, name + "_inside_z.png"));
            Shot(cam, new Vector3(b.min.x + 0.8f, eye, b.center.z), new Vector3(b.max.x, eye - 0.4f, b.center.z), Path.Combine(dir, name + "_inside_x.png"));
            // Top view with the ceiling culled: camera just under the ceiling looking down.
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.size.x, b.size.z) * 0.55f;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 50f;
            Shot(cam, new Vector3(b.center.x, b.max.y - 0.35f, b.center.z), new Vector3(b.center.x, b.min.y, b.center.z + 0.001f), Path.Combine(dir, name + "_plan.png"));
            Debug.Log("[Preview] " + name + " → " + dir);
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Shot(Camera cam, Vector3 from, Vector3 at, string path)
        {
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
