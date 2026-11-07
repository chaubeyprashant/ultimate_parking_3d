using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Turns pieces of the imported third-party models into game prefabs: pivot on the ground at the centre, a simple
    /// BoxCollider fitted to the visual bounds (or per-column boxes for the garage module), optional Rigidbody for
    /// knock-over props. The imported meshes/materials are referenced, not duplicated. Also builds URP/Lit materials
    /// from the Poly Haven texture sets (roughness converted to URP's metallic/smoothness map).
    /// Output: Assets/_Project/Prefabs/Real/*.prefab, Assets/_Project/ThirdParty/Materials/*.mat
    /// </summary>
    public static class RealPropFactory
    {
        public const string PrefabDir = "Assets/_Project/Prefabs/Real";
        public const string MaterialDir = "Assets/_Project/ThirdParty/Materials";
        private const string TP = "Assets/_Project/ThirdParty/";
        public const string CarPack = TP + "Vehicles/GenericPassengerCarPack/generic_passenger_car_pack.glb";
        public const string GaragePath = TP + "Garage/ParkingGarage_idmental/parking_garage_free_download.glb";
        public const string StreetPack = TP + "Obstacles/StreetAssetPack_vmatthew/street_asset_pack.glb";
        public const string BarrierSet = TP + "Obstacles/Innerscene_RoadBarrierSet/road-barrier-set.glb";
        public const string Bollard = TP + "Obstacles/T_Bollard/t_-_bollard.glb";
        public const string WetFloor = TP + "Obstacles/PolyHaven_WetFloorSign/WetFloorSign_01_1k.gltf";
        public const string CoveredCar = TP + "Vehicles/PolyHaven_CoveredCar/covered_car_2k.gltf";
        public const string Fluorescent = TP + "Garage/PolyHaven_MountedFluorescentLights/mounted_fluorescent_lights_1k.gltf";

        /// <summary>Garage module stretch along its bay direction (the model's painted bays are ~3.4 m deep; cars need ~5.5 m).</summary>
        public const float GarageDepthScale = 1.7f;

        public static readonly string[] CarBodies =
            { "Sedan Body", "Wagon Body", "Hatchback Body", "Compact Body", "Coupe Body", "minivan body", "Sport body", "Pickup Body", "SUV Body", "Offroad Body" };

        public static string PrefabPath(string name) => $"{PrefabDir}/{name}.prefab";
        public static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));

        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MaterialDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Parked cars: every body of the car pack with its own four wheels, front turned to +Z.
            foreach (string body in CarBodies)
                Make("Parked_" + body.Split(' ')[0].ToLowerInvariant(), CarPack, root => CarParts(root, body), colliderHeight: 1.45f,
                    yawFn: parts => CarYaw(parts[0], parts.Skip(1).ToList()));

            // Street Asset Pack (vmatthew)
            Make("Sign_Stop", StreetPack, r => Node(r, "stop"));
            Make("Sign_GiveWay", StreetPack, r => Node(r, "give_way"));
            Make("Sign_NoWaiting", StreetPack, r => Node(r, "no_waiting"));
            Make("Sign_Warning", StreetPack, r => Node(r, "warning"));
            Make("Barrier_Concrete", StreetPack, r => Node(r, "concrete_barrier"));
            Make("Barrier_Metal", StreetPack, r => Node(r, "metal_barrier1"));
            Make("Barrier_Red", StreetPack, r => Node(r, "red_barrier"));
            Make("Barrel", StreetPack, r => Node(r, "Barrel"), mass: 25f);
            Make("Bin", StreetPack, r => Node(r, "Bin"));
            Make("Cone_Street", StreetPack, r => Node(r, "Cone"), mass: 3f);

            // Innerscene road barrier set (CC0)
            Make("Barricade_AFrame", BarrierSet, r => Node(r, "A-frame barricade"), mass: 18f);
            Make("WaterBarrier_Red", BarrierSet, r => Node(r, "Water-filled barrier (red)"));
            Make("WaterBarrier_White", BarrierSet, r => Node(r, "Water-filled barrier (white)"));
            Make("Delineator", BarrierSet, r => Node(r, "Delineator post"), mass: 4f);
            Make("Cones_Cluster", BarrierSet, r => r.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Traffic cone")), mass: 9f);

            // Single-object models
            Make("Bollard", Bollard, r => new[] { r }, targetHeight: 1.0f);
            Make("WetFloorSign", WetFloor, r => new[] { r }, mass: 1.5f);
            Make("CoveredCar", CoveredCar, r => new[] { r });
            Make("FluorescentLight", Fluorescent, r => new[] { r }, collider: false, pivotOnTop: true);

            GarageModule();
            Materials();
            AssetDatabase.SaveAssets();
            Debug.Log("[RealProps] prefabs and materials written to " + PrefabDir);
        }

        // ------------------------------------------------------------------ extraction

        private static IEnumerable<Transform> Node(Transform root, string name)
        {
            Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (t == null) throw new Exception($"node '{name}' not found");
            return new[] { t };
        }

        /// <summary>A car body node plus the four wheel nodes nearest to it (they sit inside its footprint).</summary>
        public static IEnumerable<Transform> CarParts(Transform root, string bodyName)
        {
            Transform body = Node(root, bodyName).First();
            Bounds bb = Bounds(body);
            var wheels = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Wheel_") && t.parent != null && !t.parent.name.StartsWith("Wheel_") && t.GetComponentInChildren<MeshRenderer>() != null)
                .Select(t => (t, c: Bounds(t).center))
                .Where(w => Mathf.Abs(w.c.x - bb.center.x) < bb.extents.x + 0.3f && Mathf.Abs(w.c.z - bb.center.z) < bb.extents.z + 0.3f)
                .OrderBy(w => (w.c - bb.center).sqrMagnitude).Take(4).Select(w => w.t);
            return new[] { body }.Concat(wheels);
        }

        private static GameObject Make(string name, string sourcePath, Func<Transform, IEnumerable<Transform>> pick, float yaw = 0f,
            float mass = 0f, bool collider = true, float targetHeight = 0f, bool pivotOnTop = false, float colliderHeight = 0f,
            Func<List<Transform>, float> yawFn = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) { Debug.LogError($"[RealProps] missing {sourcePath}"); return null; }
            GameObject inst = Object.Instantiate(source);
            try
            {
                List<Transform> parts = pick(inst.transform).ToList();
                if (yawFn != null) yaw = yawFn(parts);
                // Bounds measured in the car's own (straightened) frame so the collider fits a rotated source.
                Quaternion straighten = Quaternion.Euler(0f, yaw, 0f);
                Bounds b = parts.Select(Bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                if (yawFn != null) b = StraightBounds(parts, straighten, b.center);
                float scale = targetHeight > 0f ? targetHeight / b.size.y : 1f;
                Vector3 origin = new Vector3(b.center.x, pivotOnTop ? b.max.y : b.min.y, b.center.z);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

                var root = new GameObject(name);
                foreach (Transform part in parts)
                    foreach (MeshFilter mf in part.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var go = new GameObject(mf.name);
                        go.transform.SetParent(root.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterials = ArenaPropFactory.ConvertMaterials(mf.GetComponent<MeshRenderer>().sharedMaterials);   // compressed, capped textures
                        go.transform.SetLocalPositionAndRotation(rot * ((mf.transform.position - origin) * scale), rot * mf.transform.rotation);
                        go.transform.localScale = mf.transform.lossyScale * scale;
                    }

                if (collider)
                {
                    Vector3 size = new Vector3(b.size.x, b.size.y, b.size.z) * scale;
                    if (yawFn == null && (Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f)) size = new Vector3(size.z, size.y, size.x);
                    if (colliderHeight > 0f) size.y = Mathf.Min(size.y, colliderHeight);
                    var box = root.AddComponent<BoxCollider>();
                    box.size = size;
                    box.center = new Vector3(0f, pivotOnTop ? -size.y * 0.5f : size.y * 0.5f, 0f);
                }
                if (mass > 0f)
                {
                    var rb = root.AddComponent<Rigidbody>();
                    rb.mass = mass;
                }
                else
                {
                    GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                    foreach (Transform t in root.transform)
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name)).Also(_ => Object.DestroyImmediate(root));
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>
        /// The imported garage section (floor with painted bays + 10 columns + light strips), stretched along Z,
        /// re-centred on the origin, with a floor box and one box per column found from the mesh vertices.
        /// </summary>
        private static void GarageModule()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePath);
            GameObject inst = Object.Instantiate(source);
            Bounds b = Bounds(inst.transform);
            var root = new GameObject("GarageModule");
            Vector3 origin = new Vector3(b.center.x, b.min.y, b.center.z);
            var stretch = new Vector3(1f, 1f, GarageDepthScale);
            // Visuals keep their imported transforms under a parent that stretches world Z; colliders stay unscaled.
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);
            visuals.localScale = stretch;
            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = new GameObject(mf.name == "Object_3" ? "LightStrips" : "FloorAndColumns");
                go.transform.SetParent(visuals, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mf.name == "Object_3" ? ArenaPropFactory.ConvertMaterials(mf.GetComponent<MeshRenderer>().sharedMaterials) : new[] { GarageFloorMaterial(mf.GetComponent<MeshRenderer>().sharedMaterial) };
                go.transform.SetLocalPositionAndRotation(mf.transform.position - origin, mf.transform.rotation);
                go.transform.localScale = mf.transform.lossyScale;
            }

            // Floor collider (slab top at y = 0) and column colliders from the mesh's tall vertices.
            var floor = root.AddComponent<BoxCollider>();
            floor.size = new Vector3(b.size.x, 0.4f, b.size.z * GarageDepthScale);
            floor.center = new Vector3(0f, -0.2f, 0f);
            MeshFilter structure = inst.GetComponentsInChildren<MeshFilter>(true).First(m => m.name != "Object_3");
            var tall = structure.sharedMesh.vertices.Select(v => structure.transform.TransformPoint(v))
                .Where(p => p.y > b.min.y + 0.8f && p.y < b.max.y - 0.2f)
                .Select(p => Vector3.Scale(p - origin, stretch)).ToList();
            var columns = new List<Bounds>();
            foreach (Vector3 p in tall)
            {
                int i = columns.FindIndex(c => Mathf.Abs(c.center.x - p.x) < 1.2f && Mathf.Abs(c.center.z - p.z) < 1.6f);
                if (i < 0) columns.Add(new Bounds(p, Vector3.zero));
                else { Bounds c = columns[i]; c.Encapsulate(p); columns[i] = c; }
            }
            var colGroup = new GameObject("ColumnColliders").transform;
            colGroup.SetParent(root.transform, false);
            foreach (Bounds c in columns.Where(c => c.size.x > 0.2f && c.size.z > 0.2f))
            {
                var col = new GameObject("Column").AddComponent<BoxCollider>();
                col.transform.SetParent(colGroup, false);
                col.center = new Vector3(c.center.x, b.size.y * 0.5f, c.center.z);
                col.size = new Vector3(c.size.x, b.size.y, c.size.z);
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("GarageModule"));
            Debug.Log($"[RealProps] GarageModule: {b.size.x:F2} x {b.size.z * GarageDepthScale:F2} m, {colGroup.childCount} column colliders");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(inst);
        }

        // ------------------------------------------------------------------ materials

        private static void Materials()
        {
            MakeLit("M_ConcreteWall", "PolyHaven_concrete_wall_004", "concrete_wall_004");
            MakeLit("M_CeilingConcrete", "PolyHaven_concrete_wall_004", "concrete_wall_004");
            Mat("M_CeilingConcrete").SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.8f));
            MakeLit("M_DeckConcrete", "PolyHaven_hangar_concrete_floor", "hangar_concrete_floor");
            Solid("M_HazardYellow", new Color(0.95f, 0.72f, 0.05f), 0.35f);
            Solid("M_HazardBlack", new Color(0.04f, 0.04f, 0.045f), 0.35f);
            Solid("M_FloorPaint", new Color(0.92f, 0.92f, 0.88f), 0.25f);
            Solid("M_SignBoard", new Color(0.06f, 0.11f, 0.22f), 0.55f);
            Solid("M_SignExit", new Color(0.05f, 0.42f, 0.18f), 0.55f);
            Solid("M_SteelDark", new Color(0.18f, 0.19f, 0.21f), 0.6f, 0.7f);
        }

        public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");

        private static void MakeLit(string name, string folder, string id)
        {
            string dir = TP + "Textures/" + folder;
            Texture2D diff = Tex($"{dir}/{id}_diff_2k.jpg", false);
            Texture2D nor = Tex($"{dir}/{id}_nor_gl_2k.jpg", true);
            Texture2D ao = Tex($"{dir}/{id}_ao_2k.jpg", false);
            Texture2D ms = Smoothness($"{dir}/{id}_rough_2k.jpg", $"{dir}/{id}_ms_1k.png");
            Material m = MaterialAt(name);
            m.SetTexture("_BaseMap", diff);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", nor);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", ao);
            m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
        }

        /// <summary>URP/Lit rebuild of the imported garage material: same textures, matte concrete (the import is glassy).</summary>
        private static Material GarageFloorMaterial(Material imported)
        {
            Material m = MaterialAt("M_GarageModule");
            m.SetTexture("_BaseMap", ArenaPropFactory.ConvertMaterial(imported).GetTexture("_BaseMap"));   // extracted, compressed copy
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            // The import's normal/occlusion textures are encoded for glTFast's own shader (wrong decode under URP/Lit
            // shows as black patches), so the floor uses its colour texture only — the detail is painted into it.
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", null);
            m.DisableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Solid(string name, Color c, float smooth, float metal = 0f)
        {
            Material m = MaterialAt(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
        }

        private static Material MaterialAt(string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Texture import for Android: 1024 max, mipmaps, normal maps flagged.</summary>
        private static Texture2D Tex(string path, bool normal)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = true;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP Lit reads smoothness from the alpha of the metallic map: write metallic 0, alpha = 1 − roughness.</summary>
        private static Texture2D Smoothness(string roughPath, string outPath)
        {
            if (!File.Exists(outPath))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(roughPath);
                ti.isReadable = true;
                ti.sRGBTexture = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                Texture2D rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
                const int size = 1024;
                var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(rough, rt);
                RenderTexture.active = rt;
                var small = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                small.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = small.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 1f - px[i].r);
                small.SetPixels(px);
                File.WriteAllBytes(outPath, small.EncodeToPNG());
                Object.DestroyImmediate(small);
                AssetDatabase.ImportAsset(outPath);
            }
            var oi = (TextureImporter)AssetImporter.GetAtPath(outPath);
            oi.sRGBTexture = false;
            oi.alphaSource = TextureImporterAlphaSource.FromInput;
            oi.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            oi.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>
        /// Heading of a car in the pack (they stand in a showroom circle at different angles). The axle line comes from
        /// the wheel positions (front/rear pairs); the nose side is chosen to agree with the body node's orientation,
        /// calibrated on the sedan whose nose is known to face −Z. Returns the yaw that turns the nose to +Z.
        /// </summary>
        public static float CarYaw(Transform body, List<Transform> wheels)
        {
            Vector3 c = wheels.Aggregate(Vector3.zero, (a, w) => a + Bounds(w).center) / wheels.Count;
            // Longest in-plane spread of the wheel centres = the car's length axis.
            Vector3 axis = Vector3.zero; float best = 0f;
            foreach (Transform w in wheels)
                foreach (Transform v in wheels)
                {
                    Vector3 d = Bounds(w).center - Bounds(v).center; d.y = 0f;
                    if (d.sqrMagnitude > best) { best = d.sqrMagnitude; axis = d; }
                }
            // The diagonal is not the length axis; average the two front/rear vectors instead.
            var sorted = wheels.Select(w => Bounds(w).center).OrderBy(p => Vector3.Dot(p - c, axis)).ToList();
            Vector3 rear = (sorted[0] + sorted[1]) * 0.5f, front = (sorted[2] + sorted[3]) * 0.5f;
            Vector3 length = front - rear; length.y = 0f;
            // Nose sign from the node orientation, calibrated on the sedan (nose −Z at rotation (270,180,0)).
            Vector3 localNose = Quaternion.Inverse(Quaternion.Euler(270f, 180f, 0f)) * Vector3.back;
            Vector3 nodeNose = body.rotation * localNose; nodeNose.y = 0f;
            if (Vector3.Dot(length, nodeNose) < 0f) length = -length;
            return Vector3.SignedAngle(length.normalized, Vector3.forward, Vector3.up);
        }

        private static Bounds StraightBounds(List<Transform> parts, Quaternion rot, Vector3 pivot)
        {
            bool first = true; Bounds b = default;
            foreach (Transform p in parts)
                foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>(true))
                    foreach (Vector3 v in mf.sharedMesh.vertices)
                    {
                        Vector3 w = rot * (mf.transform.TransformPoint(v) - pivot) + pivot;
                        if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
                    }
            return b;
        }

        public static Bounds Bounds(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static T Also<T>(this T value, Action<T> act)
        {
            act(value);
            return value;
        }
    }
}
