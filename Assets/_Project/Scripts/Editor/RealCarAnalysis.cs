using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Inspects one car of the imported Generic passenger car pack: its parts, the four wheels that belong to it,
    /// wheel pivots vs mesh centres, triangle counts, materials, and renders side/front/top previews to PNG so the
    /// orientation can be checked by eye. Writes ../UltimateCarParking3D_logs/realcar/*. Read-only on assets.
    /// Batch: -executeMethod UltimateCarParking.EditorTools.RealCarAnalysis.Run
    /// </summary>
    public static class RealCarAnalysis
    {
        public const string PackPath = "Assets/_Project/ThirdParty/Vehicles/GenericPassengerCarPack/generic_passenger_car_pack.glb";
        public const string BodyName = "Sedan Body";

        public static void Run()
        {
            string outDir = Path.GetFullPath("../UltimateCarParking3D_logs/realcar");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(PackPath);
            GameObject inst = Object.Instantiate(pack);
            var sb = new StringBuilder();

            Transform body = inst.GetComponentsInChildren<Transform>(true).First(t => t.name == BodyName);
            Bounds bb = Combined(body.GetComponentsInChildren<Renderer>(true));
            sb.AppendLine($"{BodyName}: world center {bb.center:F3} size {bb.size:F3}  pivot {body.position:F3} rot {body.rotation.eulerAngles:F1} lossyScale {body.lossyScale:F4}");
            foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                var m = r.sharedMaterial;
                sb.AppendLine($"  part {mf.name}: tris {mf.sharedMesh.triangles.Length / 3} subMeshes {mf.sharedMesh.subMeshCount} bounds {r.bounds.size:F2} mat '{m.name}' shader '{m.shader.name}' renderQueue {m.renderQueue} keywords [{string.Join(",", m.shaderKeywords)}]");
                foreach (string p in new[] { "baseColorTexture", "_BaseMap", "_MainTex", "metallicRoughnessTexture", "normalTexture", "emissiveTexture", "occlusionTexture" })
                    if (m.HasProperty(p) && m.GetTexture(p) != null) sb.AppendLine($"     {p}: {m.GetTexture(p).name} {m.GetTexture(p).width}px");
            }

            // Wheels belonging to this car: wheel nodes whose mesh centre lies inside the body's footprint (XZ, padded).
            var wheels = inst.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Wheel_") && t.GetComponentInChildren<MeshRenderer>() != null && t.parent != null && !t.parent.name.StartsWith("Wheel_"))
                .Select(t => (t, b: Combined(t.GetComponentsInChildren<Renderer>(true))))
                .Where(w => Mathf.Abs(w.b.center.x - bb.center.x) < bb.extents.x + 0.3f && Mathf.Abs(w.b.center.z - bb.center.z) < bb.extents.z + 0.3f)
                .ToList();
            sb.AppendLine($"wheels matched: {wheels.Count}");
            foreach (var (t, b) in wheels)
                sb.AppendLine($"  {t.name}: node pos {t.position:F3} mesh centre {b.center:F3} size {b.size:F3} offset {(b.center - t.position):F3} rot {t.rotation.eulerAngles:F1} tris {t.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3)}");

            File.WriteAllText(Path.Combine(outDir, "sedan_analysis.txt"), sb.ToString());

            // Previews: isolate the sedan + its wheels.
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (var (t, _) in wheels) foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.27f, 0.3f); cam.fieldOfView = 30f;
            float d = bb.size.magnitude * 2.2f;
            Shot(cam, bb.center + new Vector3(d, 0.3f, 0f), bb.center, Path.Combine(outDir, "sedan_from_+X.png"));
            Shot(cam, bb.center + new Vector3(0f, 0.3f, d), bb.center, Path.Combine(outDir, "sedan_from_+Z.png"));
            Shot(cam, bb.center + new Vector3(d * 0.6f, d * 0.4f, d * 0.6f), bb.center, Path.Combine(outDir, "sedan_three_quarter.png"));
            Debug.Log("[RealCar] analysis written to " + outDir);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Bounds Combined(Renderer[] rs)
        {
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static void Shot(Camera cam, Vector3 from, Vector3 at, string path)
        {
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
