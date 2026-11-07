using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Phase C: builds a drivable prefab around the imported "Sedan" of the Generic passenger car pack (CC BY 4.0,
    /// Comrade1280). The imported meshes and materials are referenced, not copied; the existing driving scripts are
    /// reused unchanged.
    ///
    /// Measured from the import (RealCarAnalysis): metre scale, body pivot on the ground at the centre, front faces
    /// −Z (rotated 180° here so it faces +Z like every car in the game), wheel pivots exactly at the wheel centres,
    /// radius 0.305 m, wheelbase 2.61 m, track 1.40 m.
    ///
    /// Car_RealSedan (Rigidbody, VehicleInput, CollisionHandler, VehicleController, WheelVisuals, CarPaint, VehicleAudio)
    /// ├─ Model            Body / Glass / Optics renderers (imported meshes + materials)
    /// ├─ Colliders        LowerBody + Cabin BoxColliders (simplified; also the parking footprint)
    /// ├─ WheelColliders   FrontLeft … RearRight
    /// └─ WheelMeshes      FrontLeftMesh … (pivot at the wheel centre, X = axle) → imported wheel mesh
    /// </summary>
    public static class RealCarFactory
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Vehicles/Car_RealSedan.prefab";
        public const string CarDataPath = "Assets/_Project/Cars/Car_RealSedan.asset";

        [MenuItem("Tools/Ultimate Car Parking/Build Real Sedan Prefab")]
        public static void Build() => BuildOne(RealCarAnalysis.BodyName, PrefabPath, writeStandaloneData: true);

        /// <summary>
        /// Drivable prefabs for the garage lineup, using the imported bodies, and a parked fleet
        /// (sport, coupe, sedan, hatchback, SUV, pickup, off-road, wagon, compact, minivan) for level traffic.
        /// </summary>
        public static void BuildPlayableFleet()
        {
            bool ok = WirePlayableFleet();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Builds the five drivable real-car prefabs and points the catalog's CarData at them (paint left to the
        /// model). Called by the bootstrap after <see cref="CarFactory.BuildAll"/>, which resets the catalog to the
        /// primitive bodies. Needs the parked props from <see cref="RealPropFactory.BuildAll"/> for the parked fleet.
        /// </summary>
        public static bool WirePlayableFleet()
        {
            var lineup = new[]
            {
                ("Sedan Body", "Car_RealSedan", "sedan"),
                ("Hatchback Body", "Car_RealHatch", "hatch"),
                ("Sport body", "Car_RealSport", "sport"),
                ("SUV Body", "Car_RealSuv", "suv"),
                ("Pickup Body", "Car_RealPickup", "pickup"),
            };
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
            int built = 0;
            foreach ((string body, string file, string id) in lineup)
            {
                GameObject prefab = BuildOne(body, $"Assets/_Project/Prefabs/Vehicles/{file}.prefab", writeStandaloneData: false);
                catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
                CarData data = catalog != null ? catalog.FindById(id) : null;
                if (prefab == null || data == null) { Debug.LogError($"[RealCar] fleet skip {id} / {body} prefab={(prefab != null)} catalog={(catalog != null)}"); continue; }
                var so = new SerializedObject(data);
                so.FindProperty("modelPrefab").objectReferenceValue = prefab;
                so.FindProperty("paintMaterial").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                built++;
            }
            ParkedFleetBuilder.Build();
            AssetDatabase.SaveAssets();
            bool ok = built == lineup.Length && AssetDatabase.LoadAssetAtPath<ParkedFleet>(ParkedFleetBuilder.AssetPath) != null;
            Debug.Log($"[RealCar] playable fleet {built}/{lineup.Length}");
            return ok;
        }

        private static GameObject BuildOne(string bodyName, string prefabPath, bool writeStandaloneData)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(RealCarAnalysis.PackPath);
            if (pack == null) { Debug.LogError("[RealCar] car pack not imported: " + RealCarAnalysis.PackPath); return null; }

            var previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject src = Object.Instantiate(pack);

            Transform body = src.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == bodyName);
            if (body == null) { Debug.LogError("[RealCar] body not found: " + bodyName); Object.DestroyImmediate(src); return null; }
            Bounds bodyBounds = Combined(body.GetComponentsInChildren<Renderer>(true));
            var wheelNodes = src.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Wheel_") && t.parent != null && !t.parent.name.StartsWith("Wheel_") && t.GetComponentInChildren<MeshRenderer>() != null)
                .Where(t =>
                {
                    Bounds b = Combined(t.GetComponentsInChildren<Renderer>(true));
                    return Mathf.Abs(b.center.x - bodyBounds.center.x) < bodyBounds.extents.x + 0.3f &&
                           Mathf.Abs(b.center.z - bodyBounds.center.z) < bodyBounds.extents.z + 0.3f;
                }).ToList();
            if (wheelNodes.Count != 4) { Debug.LogError($"[RealCar] expected 4 wheels on {bodyName}, found {wheelNodes.Count}"); Object.DestroyImmediate(src); return null; }

            // Same yaw the parked prefabs use, so every body faces +Z and the wheels land on four corners.
            Vector3 origin = new Vector3(body.position.x, 0f, body.position.z);
            Quaternion toCar = Quaternion.Euler(0f, RealPropFactory.CarYaw(body, wheelNodes), 0f);
            Vector3 Local(Vector3 world) => toCar * (world - origin);
            Quaternion LocalRot(Quaternion world) => toCar * world;

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath)) { tag = "Player" };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // --- visual body: reference the imported meshes/materials
            Transform model = ProjectBootstrapper.Child(root.transform, "Model");
            var paintRenderers = new List<Renderer>();
            foreach (MeshFilter mf in body.GetComponentsInChildren<MeshFilter>(true))
            {
                string part = mf.name.Replace("Sedan Body_", "").Replace("_0", "");
                MeshRenderer copy = CopyRenderer(mf, model, part);
                copy.transform.SetLocalPositionAndRotation(Local(mf.transform.position), LocalRot(mf.transform.rotation));
                copy.transform.localScale = mf.transform.lossyScale;
                if (part == "Body") paintRenderers.Add(copy);
            }

            // --- wheels: classify by position in the car frame
            Transform meshGroup = ProjectBootstrapper.Child(root.transform, "WheelMeshes");
            Transform colliderGroup = ProjectBootstrapper.Child(root.transform, "WheelColliders");
            var slots = new Dictionary<string, (Transform node, Vector3 centre)>();
            float radius = 0f, width = 0f;
            foreach (Transform w in wheelNodes)
            {
                Bounds wb = Combined(w.GetComponentsInChildren<Renderer>(true));
                Vector3 c = Local(wb.center);
                string key = (c.z > 0f ? "Front" : "Rear") + (c.x < 0f ? "Left" : "Right");
                slots[key] = (w, c);
                radius = Mathf.Max(radius, Mathf.Max(wb.size.y, Mathf.Max(wb.size.x, wb.size.z)) * 0.5f);
                width = Mathf.Max(width, Mathf.Min(wb.size.x, wb.size.z));
            }
            string[] order = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
            if (order.Any(k => !slots.ContainsKey(k))) { Debug.LogError("[RealCar] could not resolve wheel corners: " + string.Join(",", slots.Keys)); Object.DestroyImmediate(src); Object.DestroyImmediate(root); return null; }

            var colliders = new WheelCollider[4];
            var pivots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                (Transform node, Vector3 centre) = slots[order[i]];
                Transform pivot = ProjectBootstrapper.Child(meshGroup, order[i] + "Mesh");
                pivot.localPosition = centre;   // X = axle, Y = up, Z = forward (car frame)
                foreach (MeshFilter mf in node.GetComponentsInChildren<MeshFilter>(true))
                {
                    MeshRenderer copy = CopyRenderer(mf, pivot, "Wheel");
                    copy.transform.localPosition = Quaternion.Inverse(pivot.rotation) * (Local(mf.transform.position) - centre);
                    copy.transform.localRotation = LocalRot(mf.transform.rotation);
                    copy.transform.localScale = mf.transform.lossyScale;
                }
                pivots[i] = pivot;
                colliders[i] = Wheel(colliderGroup, order[i], centre + Vector3.up * 0.16f, radius, rb.mass);
            }

            // Bounds in the car frame (root is at the origin), so length stays on Z after the yaw.
            Bounds fitted = default;
            bool fittedAny = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (!fittedAny) { fitted = r.bounds; fittedAny = true; }
                else fitted.Encapsulate(r.bounds);
            }
            Vector3 size = fitted.size;
            Transform cols = ProjectBootstrapper.Child(root.transform, "Colliders");
            float bodyBottom = Mathf.Clamp(size.y * 0.2f, 0.16f, 0.45f);
            float beltLine = Mathf.Clamp(size.y * 0.58f, bodyBottom + 0.28f, size.y - 0.12f);
            ProjectBootstrapper.AddBoxCollider(cols, "LowerBody", new Vector3(0f, (bodyBottom + beltLine) * 0.5f, 0f), new Vector3(size.x - 0.04f, beltLine - bodyBottom, size.z));
            ProjectBootstrapper.AddBoxCollider(cols, "Cabin", new Vector3(0f, (beltLine + size.y) * 0.5f, -0.15f), new Vector3(size.x - 0.4f, size.y - beltLine, size.z * 0.55f));

            // --- behaviours (unchanged scripts)
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

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(src);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (writeStandaloneData && prefab != null) WriteCarData(prefab);
            Debug.Log($"[RealCar] {prefabPath}: body {size.x:F2}x{size.y:F2}x{size.z:F2} m, wheel r {radius:F3} w {width:F3}, " +
                      $"wheelbase {slots["FrontLeft"].centre.z - slots["RearLeft"].centre.z:F2}, track {slots["FrontRight"].centre.x - slots["FrontLeft"].centre.x:F2}");
            if (!string.IsNullOrEmpty(previous) && !Application.isBatchMode) EditorSceneManager.OpenScene(previous);
            return prefab;
        }

        private static MeshRenderer CopyRenderer(MeshFilter source, Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = ArenaPropFactory.ConvertMaterials(source.GetComponent<MeshRenderer>().sharedMaterials);   // compressed, capped textures
            return r;
        }

        private static WheelCollider Wheel(Transform parent, string name, Vector3 localPos, float radius, float mass)
        {
            var go = new GameObject(name + "Collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.mass = 20f;
            wc.radius = radius;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = 0.22f;
            wc.forceAppPointDistance = 0.15f;
            wc.suspensionSpring = new JointSpring { spring = Mathf.Round(mass * 27f / 100f) * 100f, damper = Mathf.Round(mass * 3.08f / 100f) * 100f, targetPosition = 0.5f };
            wc.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.5f };
            wc.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.25f, extremumValue = 1f, asymptoteSlip = 0.6f, asymptoteValue = 0.75f, stiffness = 1.8f };
            return wc;
        }

        private static void WriteCarData(GameObject prefab)
        {
            var data = AssetDatabase.LoadAssetAtPath<CarData>(CarDataPath);
            if (data == null) { data = ScriptableObject.CreateInstance<CarData>(); AssetDatabase.CreateAsset(data, CarDataPath); }
            var so = new SerializedObject(data);
            so.FindProperty("carId").stringValue = "real_sedan";
            so.FindProperty("displayName").stringValue = "Metro Sedan";
            so.FindProperty("tagline").stringValue = "Full-detail sedan. Real wheels, glass and lights.";
            so.FindProperty("modelPrefab").objectReferenceValue = prefab;
            so.FindProperty("paintMaterial").objectReferenceValue = null;   // keeps the model's own textured paint
            so.FindProperty("price").intValue = 0;
            so.FindProperty("ownedByDefault").boolValue = false;
            SerializedProperty h = so.FindProperty("handling");
            h.FindPropertyRelative("mass").floatValue = 1350f;
            h.FindPropertyRelative("centerOfMass").vector3Value = new Vector3(0f, 0.32f, 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private static Bounds Combined(Renderer[] rs)
        {
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Batch entry: build, then exit.</summary>
        public static void BuildBatch()
        {
            Build();
            if (Application.isBatchMode) EditorApplication.Exit(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ? 0 : 1);
        }
    }
}
