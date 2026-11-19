using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltimateCarParking.Cars;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Renders every garage car to a PNG (three-quarter view) so the whole fleet can be checked by eye. Editor tool only.</summary>
    public static class FleetPreview
    {
        public static void RenderAll()
        {
            string outDir = System.Environment.GetEnvironmentVariable("FLEET_PREVIEW_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = "/tmp/fleet_preview";
            Directory.CreateDirectory(outDir);
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camGo = new GameObject("PreviewCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.66f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData car = catalog.Get(i);
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(car.ModelPrefab);
                go.transform.position = Vector3.zero;
                Bounds b = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                foreach (var view in new[] { ("front", new Vector3(-1f, 0.5f, -1.5f)), ("rear", new Vector3(1f, 0.5f, 1.5f)) })
                {
                    float d = Mathf.Max(b.size.x, b.size.z) * 1.6f + 1.5f;
                    camGo.transform.position = b.center + view.Item2.normalized * d + Vector3.up * 0.3f;
                    camGo.transform.LookAt(b.center);
                    var rt = new RenderTexture(768, 432, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(768, 432, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 768, 432), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, $"{i:00}_{car.CarId}_{view.Item1}.png"), tex.EncodeToPNG());
                    RenderTexture.active = null; cam.targetTexture = null;
                    Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
                }
                Debug.Log($"[FleetPreview] {i:00} {car.CarId} '{car.DisplayName}' ${car.Price} bounds {b.size.x:F2}x{b.size.y:F2}x{b.size.z:F2}");
                Object.DestroyImmediate(go);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
