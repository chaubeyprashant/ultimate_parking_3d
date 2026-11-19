using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Builds a drivable car prefab from a low-poly model whose four wheels are separate, named parts (…_Wheel_FL / …_WheelStock_RR,
    /// the naming of the Zhabotinsky car series). The front and rear wheel pairs give the car's heading directly, so no per-model
    /// orientation guessing is needed. Mirrors RealCarFactory's rig: Rigidbody, four WheelColliders, rotating wheel visuals, box
    /// colliders, VehicleController and the other driving behaviours.
    /// </summary>
    public static class RiggedCarFactory
    {
        // Zhabotinsky series: …_WheelStock_FL, …_WheelsStock_FL, …_WheelHubcaps_FL, …_WheelLift_FL, …_WheelTuner_FL, …_WhellStock_FL (sic).
        private static readonly Regex WheelName = new Regex(@"^(?!.*(caliper|steering)).*(wheel|whell)\w*?_(fl|fr|rl|rr)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Han66st sports car: glr-wheel.Ft.L_2, glr-wheel.Bk.R.001_6 (tyre, rim and disc are separate meshes per corner).
        // Low Poly-F1: FR_Tire / FL_Tire / RR_Tire / RL_Tire (the tyre part with the rim as a coloured sub-mesh).
        private static readonly Regex WheelNameTire = new Regex(@"^(fl|fr|rl|rr)_tire$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WheelNameDotted = new Regex(@"^glr-wheel\.(ft|bk)\.(l|r)(\.\d+)?_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static GameObject Build(string modelPath, string prefabPath, float mass, float targetLength = 0f, float extraScale = 1f, bool autoFit = false, float wheelScale = 1f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null) { Debug.LogError("[Fleet] model not imported: " + modelPath); return null; }

            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject src = Object.Instantiate(asset);

            var corners = new Dictionary<string, List<Transform>>();
            void AddCorner(string key, Transform t)
            {
                if (!corners.TryGetValue(key, out List<Transform> l)) corners[key] = l = new List<Transform>();
                l.Add(t);
            }
            foreach (Transform t in src.GetComponentsInChildren<Transform>(true))
            {
                Match m = WheelName.Match(t.name);
                if (m.Success && t.GetComponentInChildren<MeshRenderer>() != null) { AddCorner(m.Groups[3].Value.ToUpperInvariant(), t); continue; }
                Match tire = WheelNameTire.Match(t.name);
                if (tire.Success && t.GetComponentInChildren<MeshRenderer>() != null) { AddCorner(tire.Groups[1].Value.ToUpperInvariant(), t); continue; }
                Match d = WheelNameDotted.Match(t.name);
                if (d.Success && t.GetComponentInChildren<MeshRenderer>() != null)
                    AddCorner((d.Groups[1].Value.ToLowerInvariant() == "ft" ? "F" : "R") + d.Groups[2].Value.ToUpperInvariant(), t);
            }
            if (new[] { "FL", "FR", "RL", "RR" }.Any(k => !corners.ContainsKey(k)))
            {
                Debug.LogError($"[Fleet] {modelPath}: wheel parts not found ({string.Join(",", corners.Keys)})");
                Object.DestroyImmediate(src);
                return null;
            }
            var wheelSet = new HashSet<Transform>(corners.Values.SelectMany(l => l));
            bool InWheel(Transform t) { for (; t != null; t = t.parent) if (wheelSet.Contains(t)) return true; return false; }

            Vector3 Centre(List<Transform> ts) => Combined(ts.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToArray()).center;
            Vector3 front = (Centre(corners["FL"]) + Centre(corners["FR"])) * 0.5f, rear = (Centre(corners["RL"]) + Centre(corners["RR"])) * 0.5f;
            Vector3 heading = front - rear; heading.y = 0f;
            float yaw = Vector3.SignedAngle(heading.normalized, Vector3.forward, Vector3.up);
            Quaternion toCar = Quaternion.Euler(0f, yaw, 0f);

            MeshFilter[] bodyMeshes = src.GetComponentsInChildren<MeshFilter>(true).Where(mf => !InWheel(mf.transform) && mf.sharedMesh != null).ToArray();
            Bounds all = Combined(src.GetComponentsInChildren<Renderer>(true));
            float groundY = 0f;
            if (autoFit) groundY = corners.Values.Min(l => Combined(l.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToArray()).min.y);
            Vector3 origin = new Vector3(all.center.x, groundY, all.center.z);
            float lengthNative = heading.magnitude + 1f;
            float scale = 1f;
            if (targetLength > 0f)
            {
                float len = Mathf.Max(all.size.x, all.size.z);
                if (len > 0.01f && (len < targetLength * 0.8f || len > targetLength * 1.25f)) scale = targetLength / len;
            }
            if (autoFit)
            {
                // Every mission bay must fit every car: trim any model wider than 1.93 m or longer than 5.2 m.
                Bounds native = default; bool nb = false;
                foreach (MeshFilter mf in bodyMeshes)
                    foreach (Vector3 v in mf.sharedMesh.vertices)
                    {
                        Vector3 q = toCar * (mf.transform.TransformPoint(v) - origin);
                        if (!nb) { native = new Bounds(q, Vector3.zero); nb = true; } else native.Encapsulate(q);
                    }
                float fit = Mathf.Min(1f, 1.93f / Mathf.Max(0.01f, native.size.x), 5.2f / Mathf.Max(0.01f, native.size.z));
                scale *= fit;
            }
            scale *= extraScale;   // lets a wide model be trimmed so every mission bay still fits it
            Vector3 Local(Vector3 world) => toCar * (world - origin) * scale;
            Quaternion LocalRot(Quaternion world) => toCar * world;
            // Monster truck: bigger wheels and a body raised by the extra wheel radius (ground contact stays at y = 0).
            float rawRadius = Mathf.Max(Combined(corners["FL"].SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToArray()).size.y, 0.1f) * 0.5f * scale;
            float lift = rawRadius * (wheelScale - 1f);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath)) { tag = "Player" };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.02f; rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Transform model = ProjectBootstrapper.Child(root.transform, "Model");
            var paintRenderers = new List<Renderer>();
            foreach (MeshFilter mf in bodyMeshes)
            {
                string partName = mf.transform.parent != null && mf.transform.parent.name != "RootNode" ? mf.transform.parent.name : mf.name;
                MeshRenderer copy = RealCarFactory.CopyRenderer(mf, model, partName);
                copy.transform.SetLocalPositionAndRotation(Local(mf.transform.position) + Vector3.up * lift, LocalRot(mf.transform.rotation));
                copy.transform.localScale = mf.transform.lossyScale * scale;
                if (partName.EndsWith("_Body", System.StringComparison.OrdinalIgnoreCase)) paintRenderers.Add(copy);
            }

            Transform meshGroup = ProjectBootstrapper.Child(root.transform, "WheelMeshes");
            Transform colliderGroup = ProjectBootstrapper.Child(root.transform, "WheelColliders");
            string[] order = { "FL", "FR", "RL", "RR" };
            string[] label = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
            var colliders = new WheelCollider[4];
            var pivots = new Transform[4];
            float radius = 0f;
            for (int i = 0; i < 4; i++)
            {
                List<Transform> nodes = corners[order[i]];
                Bounds wb = Combined(nodes.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToArray());
                Vector3 c = Local(wb.center) + Vector3.up * lift;
                radius = Mathf.Max(radius, Mathf.Max(wb.size.y, Mathf.Max(wb.size.x, wb.size.z)) * 0.5f * scale * wheelScale);
                Transform pivot = ProjectBootstrapper.Child(meshGroup, label[i] + "Mesh");
                pivot.localPosition = c;
                foreach (MeshFilter mf in nodes.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)))
                {
                    MeshRenderer copy = RealCarFactory.CopyRenderer(mf, pivot, "Wheel");
                    copy.transform.localPosition = Quaternion.Inverse(pivot.rotation) * (Local(mf.transform.position) + Vector3.up * lift - c) * wheelScale;
                    copy.transform.localRotation = LocalRot(mf.transform.rotation);
                    copy.transform.localScale = mf.transform.lossyScale * scale * wheelScale;
                }
                pivots[i] = pivot;
                colliders[i] = RealCarFactory.Wheel(colliderGroup, label[i], c + Vector3.up * 0.16f, radius, rb.mass);
            }

            Bounds fitted = default; bool any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) { fitted = r.bounds; any = true; } else fitted.Encapsulate(r.bounds);
            }
            fitted = RealCarFactory.TightBounds(model.GetComponentsInChildren<MeshFilter>());
            Vector3 size = fitted.size;
            Transform cols = ProjectBootstrapper.Child(root.transform, "Colliders");
            float bodyBottom = Mathf.Clamp(size.y * 0.2f, 0.16f, 0.45f);
            float beltLine = Mathf.Clamp(size.y * 0.58f, bodyBottom + 0.28f, size.y - 0.12f);
            ProjectBootstrapper.AddBoxCollider(cols, "LowerBody", new Vector3(0f, (bodyBottom + beltLine) * 0.5f, 0f), new Vector3(size.x - 0.04f, beltLine - bodyBottom, size.z));
            ProjectBootstrapper.AddBoxCollider(cols, "Cabin", new Vector3(0f, (beltLine + size.y) * 0.5f, -0.15f), new Vector3(size.x - 0.4f, size.y - beltLine, size.z * 0.55f));

            var input = root.AddComponent<VehicleInput>();
            root.AddComponent<CollisionHandler>();
            var controller = root.AddComponent<VehicleController>();
            var visuals = root.AddComponent<WheelVisuals>();
            root.AddComponent<VehicleAudio>();
            var paint = root.AddComponent<CarPaint>();

            var cso = new SerializedObject(controller);
            cso.FindProperty("input").objectReferenceValue = input;
            cso.FindProperty("frontLeft").objectReferenceValue = colliders[0];
            cso.FindProperty("frontRight").objectReferenceValue = colliders[1];
            cso.FindProperty("rearLeft").objectReferenceValue = colliders[2];
            cso.FindProperty("rearRight").objectReferenceValue = colliders[3];
            cso.FindProperty("centerOfMass").vector3Value = new Vector3(0f, 0.32f, 0.05f);
            cso.ApplyModifiedPropertiesWithoutUndo();

            var vso = new SerializedObject(visuals);
            vso.FindProperty("body").objectReferenceValue = rb;
            SerializedProperty list = vso.FindProperty("wheels");
            list.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("collider").objectReferenceValue = colliders[i];
                list.GetArrayElementAtIndex(i).FindPropertyRelative("visual").objectReferenceValue = pivots[i];
            }
            vso.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(paint);
            SerializedProperty pr = pso.FindProperty("paintRenderers");
            pr.arraySize = paintRenderers.Count;
            for (int i = 0; i < paintRenderers.Count; i++) pr.GetArrayElementAtIndex(i).objectReferenceValue = paintRenderers[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Cockpit view: models with a modelled interior get the driver's eye just behind and above the steering wheel.
            Transform steering = src.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.IndexOf("steering", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentsInChildren<Renderer>(true).Length > 0);
            if (steering != null)
            {
                Bounds sw = Combined(steering.GetComponentsInChildren<Renderer>(true));
                Vector3 eye = Local(sw.center) + Vector3.up * lift + new Vector3(0f, 0.3f * Mathf.Max(0.6f, scale), -0.42f * Mathf.Max(0.6f, scale));
                root.AddComponent<UltimateCarParking.Vehicle.CockpitMount>().eyeLocal = eye;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(src);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Log($"[Fleet] {prefabPath}: {size.x:F2}x{size.y:F2}x{size.z:F2} m (scale {scale:F2}), wheel r {radius:F3}, wheelbase {Local(front).z - Local(rear).z:F2}");
            if (!string.IsNullOrEmpty(previous) && !Application.isBatchMode) EditorSceneManager.OpenScene(previous);
            return prefab;
        }

        private static Bounds Combined(Renderer[] rs) => RealCarFactory.Combined(rs);
    }
}
