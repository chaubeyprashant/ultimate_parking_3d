using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// The modular environment kit: one prefab per reusable piece (pivot on the ground, +Z forward), plus the three
    /// themes (City Lot, Sky Garage, The Yard) assembled from them. Levels reference these prefabs by placement.
    /// </summary>
    public static class EnvironmentKit
    {
        public const string ModulesDir = "Assets/_Project/Prefabs/Modules";
        public const string ThemesDir = "Assets/_Project/Environment/Themes";

        // Module names (prefab file = Mod_<name>.prefab).
        public const string Cone = "Cone", Barrier = "Barrier", Pillar = "Pillar", Wall = "Wall", DeckWall = "DeckWall",
            Hoarding = "Hoarding", ParkedCar = "ParkedCar", BayLine = "BayLine", WheelStop = "WheelStop", LightPole = "LightPole",
            Planter = "Planter", Container = "Container", SandPile = "SandPile", PipeStack = "PipeStack", Beam = "Beam", Kerb = "Kerb";

        public static GameObject Module(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModulesDir}/Mod_{name}.prefab");
        public static EnvironmentTheme Theme(string id) => AssetDatabase.LoadAssetAtPath<EnvironmentTheme>($"{ThemesDir}/Theme_{id}.asset");

        private static readonly Dictionary<string, (Color c, float smooth, float metal, Color emit)> Materials = new()
        {
            { "DeckConcrete", (new Color(0.50f, 0.52f, 0.55f), 0.25f, 0f, Color.black) },
            { "DeckOuter", (new Color(0.16f, 0.17f, 0.19f), 0.1f, 0f, Color.black) },
            { "Dirt", (new Color(0.42f, 0.34f, 0.25f), 0.08f, 0f, Color.black) },
            { "Gravel", (new Color(0.30f, 0.28f, 0.25f), 0.05f, 0f, Color.black) },
            { "Sand", (new Color(0.78f, 0.66f, 0.45f), 0.05f, 0f, Color.black) },
            { "HazardYellow", (new Color(1f, 0.78f, 0.05f), 0.4f, 0f, Color.black) },
            { "HazardBlack", (new Color(0.06f, 0.06f, 0.06f), 0.4f, 0f, Color.black) },
            { "Steel", (new Color(0.55f, 0.58f, 0.62f), 0.55f, 0.8f, Color.black) },
            { "DarkMetal", (new Color(0.16f, 0.17f, 0.19f), 0.5f, 0.7f, Color.black) },
            { "Lamp", (new Color(1f, 0.95f, 0.8f), 0.8f, 0f, new Color(1.6f, 1.45f, 1.1f)) },
            { "Hedge", (new Color(0.20f, 0.36f, 0.16f), 0.1f, 0f, Color.black) },
            { "Plywood", (new Color(0.36f, 0.42f, 0.50f), 0.2f, 0f, Color.black) },
            { "TintWhite", (new Color(1f, 1f, 1f), 0.6f, 0.25f, Color.black) },     // recoloured per placement
            { "TintMatte", (new Color(1f, 1f, 1f), 0.3f, 0.1f, Color.black) },
            { "LineTint", (new Color(1f, 1f, 1f), 0.2f, 0f, Color.black) },
            { "PipeOrange", (new Color(0.95f, 0.45f, 0.1f), 0.35f, 0f, Color.black) },
        };

        public static void BuildAll()
        {
            Directory.CreateDirectory(ModulesDir);
            Directory.CreateDirectory(ThemesDir);
            CreateMaterials();
            BuildModules();
            BuildThemes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] Environment kit + 3 themes written");
        }

        private static void CreateMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, m) in Materials)
            {
                string path = $"{ProjectBootstrapper.MaterialsDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
                mat.SetColor("_BaseColor", m.c);
                mat.SetFloat("_Smoothness", m.smooth);
                mat.SetFloat("_Metallic", m.metal);
                if (m.emit.maxColorComponent > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", m.emit); }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ modules

        private static void BuildModules()
        {
            Save(Cone, root =>
            {
                Box(root, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.04f, 0.42f), "HazardBlack");
                Cyl(root, "Body", new Vector3(0f, 0.38f, 0f), new Vector3(0.3f, 0.34f, 0.3f), "Cone");
                Cyl(root, "Band", new Vector3(0f, 0.46f, 0f), new Vector3(0.26f, 0.06f, 0.26f), "LineWhite");
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.36f, 0f); col.radius = 0.18f; col.height = 0.72f;
                var rb = root.AddComponent<Rigidbody>(); rb.mass = 3f;
            });

            Save(Barrier, root =>
            {
                var a = Box(root, "Base", new Vector3(0f, 0.18f, 0f), new Vector3(0.6f, 0.36f, 2f), "TintMatte");
                var b = Box(root, "Top", new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.5f, 2f), "TintMatte");
                Box(root, "StripeA", new Vector3(0f, 0.62f, -0.5f), new Vector3(0.33f, 0.2f, 0.45f), "LineWhite");
                Box(root, "StripeB", new Vector3(0f, 0.62f, 0.5f), new Vector3(0.33f, 0.2f, 0.45f), "LineWhite");
                Collider(root, new Vector3(0f, 0.425f, 0f), new Vector3(0.6f, 0.85f, 2f));
                Paint(root, new[] { a, b }, UiColor(0xF2B807), UiColor(0xE23B3B), UiColor(0xD8DCE0), UiColor(0xF07A12));
            });

            Save(Pillar, root =>
            {
                Box(root, "Column", new Vector3(0f, 2.3f, 0f), new Vector3(0.6f, 4.6f, 0.6f), "Concrete");
                Box(root, "BandYellow", new Vector3(0f, 0.25f, 0f), new Vector3(0.62f, 0.5f, 0.62f), "HazardYellow");
                Box(root, "BandBlack", new Vector3(0f, 0.6f, 0f), new Vector3(0.62f, 0.2f, 0.62f), "HazardBlack");
                Box(root, "BandYellow2", new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.3f, 0.62f), "HazardYellow");
                Collider(root, new Vector3(0f, 2.3f, 0f), new Vector3(0.6f, 4.6f, 0.6f));
            });

            Save(Wall, root =>   // 4 m segment along local X
            {
                Box(root, "Wall", new Vector3(0f, 0.6f, 0f), new Vector3(4f, 1.2f, 0.4f), "Concrete");
                Box(root, "Cap", new Vector3(0f, 1.24f, 0f), new Vector3(4f, 0.08f, 0.48f), "Curb");
                Collider(root, new Vector3(0f, 0.62f, 0f), new Vector3(4f, 1.24f, 0.48f));
            });

            Save(DeckWall, root =>   // garage parapet with a steel rail
            {
                Box(root, "Parapet", new Vector3(0f, 0.55f, 0f), new Vector3(4f, 1.1f, 0.3f), "DeckConcrete");
                Box(root, "Rail", new Vector3(0f, 1.35f, 0f), new Vector3(4f, 0.06f, 0.08f), "Steel");
                Box(root, "PostL", new Vector3(-1.9f, 1.22f, 0f), new Vector3(0.06f, 0.26f, 0.06f), "Steel");
                Box(root, "PostR", new Vector3(1.9f, 1.22f, 0f), new Vector3(0.06f, 0.26f, 0.06f), "Steel");
                Box(root, "Kick", new Vector3(0f, 0.1f, -0.16f), new Vector3(4f, 0.2f, 0.04f), "HazardYellow");
                Collider(root, new Vector3(0f, 0.7f, 0f), new Vector3(4f, 1.4f, 0.3f));
            });

            Save(Hoarding, root =>   // construction-site hoarding panel
            {
                Box(root, "Panel", new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 0.1f), "Plywood");
                Box(root, "Stripe", new Vector3(0f, 0.25f, -0.06f), new Vector3(4f, 0.3f, 0.02f), "HazardYellow");
                Box(root, "PostL", new Vector3(-1.95f, 1.2f, 0.08f), new Vector3(0.1f, 2.4f, 0.1f), "DarkMetal");
                Box(root, "FootL", new Vector3(-1.95f, 0.08f, 0.3f), new Vector3(0.3f, 0.16f, 0.6f), "Concrete");
                Collider(root, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 0.2f));
            });

            Save(ParkedCar, root =>
            {
                var body = Box(root, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.7f, 4.3f), "TintWhite");
                Box(root, "Cabin", new Vector3(0f, 1.22f, -0.2f), new Vector3(1.6f, 0.5f, 2.2f), "CarGlass");
                var roof = Box(root, "Roof", new Vector3(0f, 1.49f, -0.25f), new Vector3(1.62f, 0.05f, 1.9f), "TintWhite");
                Box(root, "LightsF", new Vector3(0f, 0.75f, 2.16f), new Vector3(1.4f, 0.1f, 0.02f), "Headlight");
                Box(root, "LightsR", new Vector3(0f, 0.78f, -2.16f), new Vector3(1.4f, 0.08f, 0.02f), "Taillight");
                foreach (float sx in new[] { -0.8f, 0.8f })
                    foreach (float sz in new[] { -1.35f, 1.35f })
                        Cyl(root, "Wheel", new Vector3(sx, 0.34f, sz), new Vector3(0.68f, 0.11f, 0.68f), "Tire", Quaternion.Euler(0f, 0f, 90f));
                Collider(root, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 1.3f, 4.3f));
                Paint(root, new[] { body, roof }, UiColor(0x2F5FB3), UiColor(0xA7ABB0), UiColor(0xE6E6E2), UiColor(0x27704A),
                    UiColor(0xB3262A), UiColor(0x1C1E22), UiColor(0xE0B21C), UiColor(0x1E8A8A));
            });

            Save(BayLine, root =>   // 1 m long, scale Z for length
            {
                var line = Box(root, "Line", new Vector3(0f, 0.006f, 0f), new Vector3(0.12f, 0.01f, 1f), "LineTint");
                Paint(root, new[] { line }, Color.white * 0.95f, UiColor(0xF2C230));
            });

            Save(WheelStop, root =>
            {
                Box(root, "Stop", new Vector3(0f, 0.075f, 0f), new Vector3(1.8f, 0.15f, 0.25f), "Curb");
                Box(root, "Paint", new Vector3(0f, 0.152f, 0f), new Vector3(1.6f, 0.005f, 0.12f), "HazardYellow");
                Collider(root, new Vector3(0f, 0.075f, 0f), new Vector3(1.8f, 0.15f, 0.25f));
            });

            Save(Kerb, root =>   // 1 m along X, scale X for length
            {
                Box(root, "Kerb", new Vector3(0f, 0.07f, 0f), new Vector3(1f, 0.14f, 0.35f), "Curb");
                Collider(root, new Vector3(0f, 0.07f, 0f), new Vector3(1f, 0.14f, 0.35f));
            });

            Save(LightPole, root =>
            {
                Box(root, "Foot", new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), "Concrete");
                Cyl(root, "Pole", new Vector3(0f, 3.2f, 0f), new Vector3(0.16f, 2.8f, 0.16f), "DarkMetal");
                Box(root, "Arm", new Vector3(0f, 5.9f, 0.55f), new Vector3(0.1f, 0.1f, 1.2f), "DarkMetal");
                Box(root, "Lamp", new Vector3(0f, 5.82f, 1.05f), new Vector3(0.36f, 0.08f, 0.6f), "Lamp");
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 3f, 0f); col.radius = 0.25f; col.height = 6f;
            });

            Save(Planter, root =>
            {
                Box(root, "Box", new Vector3(0f, 0.3f, 0f), new Vector3(3f, 0.6f, 1.2f), "Concrete");
                Box(root, "Hedge", new Vector3(0f, 0.85f, 0f), new Vector3(2.8f, 0.6f, 1.0f), "Hedge");
                Collider(root, new Vector3(0f, 0.55f, 0f), new Vector3(3f, 1.1f, 1.2f));
            });

            Save(Container, root =>   // 6 m along X
            {
                var shell = Box(root, "Shell", new Vector3(0f, 1.3f, 0f), new Vector3(6.06f, 2.6f, 2.44f), "TintMatte");
                var renderers = new List<Transform> { shell };
                for (int i = 0; i < 9; i++)
                {
                    float x = -2.7f + i * 0.675f;
                    renderers.Add(Box(root, "RibN" + i, new Vector3(x, 1.3f, 1.24f), new Vector3(0.12f, 2.4f, 0.06f), "TintMatte"));
                    renderers.Add(Box(root, "RibS" + i, new Vector3(x, 1.3f, -1.24f), new Vector3(0.12f, 2.4f, 0.06f), "TintMatte"));
                }
                Collider(root, new Vector3(0f, 1.3f, 0f), new Vector3(6.06f, 2.6f, 2.5f));
                Paint(root, renderers.ToArray(), UiColor(0xC8551F), UiColor(0x245A8C), UiColor(0x8C2424), UiColor(0x2E6B3F), UiColor(0x5A5F66));
            });

            Save(SandPile, root =>
            {
                Sphere(root, "Pile", new Vector3(0f, 0.1f, 0f), new Vector3(3.2f, 1.6f, 3.2f), "Sand");
                Collider(root, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 2.4f));
            });

            Save(PipeStack, root =>
            {
                for (int i = 0; i < 3; i++)
                    Cyl(root, "Pipe" + i, new Vector3(-0.5f + i * 0.5f, 0.25f, 0f), new Vector3(0.5f, 2f, 0.5f), "PipeOrange", Quaternion.Euler(90f, 0f, 0f));
                Cyl(root, "PipeTop", new Vector3(0f, 0.68f, 0f), new Vector3(0.5f, 2f, 0.5f), "PipeOrange", Quaternion.Euler(90f, 0f, 0f));
                Collider(root, new Vector3(0f, 0.47f, 0f), new Vector3(1.5f, 0.95f, 4f));
            });

            Save(Beam, root =>   // overhead deck beam, 1 m along X (scale X), no collider: well above the car
            {
                Box(root, "Beam", new Vector3(0f, 4.6f, 0f), new Vector3(1f, 0.45f, 0.4f), "DeckConcrete");
                Box(root, "Light", new Vector3(0f, 4.36f, 0.3f), new Vector3(0.6f, 0.04f, 0.15f), "Lamp");
            });
        }

        private static void Save(string name, System.Action<GameObject> build)
        {
            var root = new GameObject("Mod_" + name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, $"{ModulesDir}/Mod_{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static Transform Box(GameObject root, string name, Vector3 pos, Vector3 size, string mat) =>
            ProjectBootstrapper.Primitive(PrimitiveType.Cube, root.transform, name, pos, Quaternion.identity, size, mat, collider: false);

        private static Transform Cyl(GameObject root, string name, Vector3 pos, Vector3 size, string mat, Quaternion? rot = null) =>
            ProjectBootstrapper.Primitive(PrimitiveType.Cylinder, root.transform, name, pos, rot ?? Quaternion.identity, size, mat, collider: false);

        private static Transform Sphere(GameObject root, string name, Vector3 pos, Vector3 size, string mat) =>
            ProjectBootstrapper.Primitive(PrimitiveType.Sphere, root.transform, name, pos, Quaternion.identity, size, mat, collider: false);

        private static void Collider(GameObject root, Vector3 center, Vector3 size)
        {
            var c = root.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void Paint(GameObject root, Transform[] parts, params Color[] palette)
        {
            var pv = root.AddComponent<PaintVariant>();
            var so = new SerializedObject(pv);
            SerializedProperty t = so.FindProperty("targets");
            t.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++) t.GetArrayElementAtIndex(i).objectReferenceValue = parts[i].GetComponent<Renderer>();
            SerializedProperty p = so.FindProperty("palette");
            p.arraySize = palette.Length;
            for (int i = 0; i < palette.Length; i++) p.GetArrayElementAtIndex(i).colorValue = palette[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color UiColor(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        // ------------------------------------------------------------------ themes

        public const string CityId = "city", GarageId = "garage", YardId = "yard";

        private static void BuildThemes()
        {
            // City Lot: open-air asphalt, concrete walls, light poles and planters behind the bay rows.
            var cityDeco = new List<PropPlacement>();
            for (int i = 0; i < 4; i++)
            {
                float x = -24f + i * 16f;
                cityDeco.Add(new PropPlacement(Module(LightPole), new Vector3(x, 0f, 24.2f), 180f));
                cityDeco.Add(new PropPlacement(Module(LightPole), new Vector3(x + 8f, 0f, -24.2f), 0f));
            }
            foreach (var c in new[] { new Vector3(-28.5f, 0f, 23.8f), new Vector3(28.5f, 0f, 23.8f), new Vector3(-28.5f, 0f, -23.8f), new Vector3(28.5f, 0f, -23.8f) })
                cityDeco.Add(new PropPlacement(Module(Planter), c, 0f));
            WriteTheme(CityId, "City Lot", new Color(0.71f, 1f, 0.18f), new Vector2(64f, 52f), "Asphalt", "Grass", Wall, 4f, cityDeco,
                sunColor: new Color(1f, 0.96f, 0.9f), sunIntensity: 1.25f, sunEuler: new Vector3(50f, -35f, 0f),
                sky: new Color(0.62f, 0.68f, 0.78f), equator: new Color(0.45f, 0.46f, 0.47f), ground: new Color(0.22f, 0.21f, 0.2f),
                fog: false, fogColor: Color.gray, fogDensity: 0f);

            // Sky Garage: covered deck feel — overhead beams with lights, parapet walls, dim warm light, light haze.
            var garageDeco = new List<PropPlacement>();
            for (int i = 0; i < 7; i++)
                garageDeco.Add(new PropPlacement(Module(Beam), new Vector3(0f, 0f, -21f + i * 7f), 0f, new Vector3(60f, 1f, 1f)));
            WriteTheme(GarageId, "Sky Garage", new Color(0.35f, 0.85f, 1f), new Vector2(60f, 48f), "DeckConcrete", "DeckOuter", DeckWall, 4f, garageDeco,
                sunColor: new Color(1f, 0.86f, 0.66f), sunIntensity: 0.75f, sunEuler: new Vector3(68f, -20f, 0f),
                sky: new Color(0.52f, 0.5f, 0.46f), equator: new Color(0.42f, 0.4f, 0.37f), ground: new Color(0.2f, 0.19f, 0.18f),
                fog: true, fogColor: new Color(0.42f, 0.44f, 0.47f), fogDensity: 0.012f);

            // The Yard: dirt ground, hoarding fence, containers, pipe stacks and sand piles in the corners.
            var yardDeco = new List<PropPlacement>
            {
                new PropPlacement(Module(Container), new Vector3(-26f, 0f, 22.5f), 0f, default, 0),
                new PropPlacement(Module(Container), new Vector3(-26f, 2.6f, 22.5f), 0f, default, 1),
                new PropPlacement(Module(Container), new Vector3(26f, 0f, -22.5f), 0f, default, 2),
                new PropPlacement(Module(Container), new Vector3(19.5f, 0f, -22.5f), 0f, default, 3),
                new PropPlacement(Module(Container), new Vector3(26f, 2.6f, -22.5f), 0f, default, 4),
                new PropPlacement(Module(PipeStack), new Vector3(28.5f, 0f, 20f), 0f),
                new PropPlacement(Module(SandPile), new Vector3(-28.5f, 0f, -21.5f), 0f),
                new PropPlacement(Module(SandPile), new Vector3(10f, 0f, 22.8f), 0f),
            };
            WriteTheme(YardId, "The Yard", new Color(1f, 0.72f, 0.2f), new Vector2(66f, 54f), "Dirt", "Gravel", Hoarding, 4f, yardDeco,
                sunColor: new Color(1f, 0.88f, 0.7f), sunIntensity: 1.35f, sunEuler: new Vector3(36f, 120f, 0f),
                sky: new Color(0.66f, 0.62f, 0.56f), equator: new Color(0.5f, 0.45f, 0.38f), ground: new Color(0.26f, 0.21f, 0.16f),
                fog: true, fogColor: new Color(0.72f, 0.66f, 0.56f), fogDensity: 0.006f);
        }

        private static void WriteTheme(string id, string displayName, Color ui, Vector2 lot, string groundMat, string outer, string wall, float segment,
            List<PropPlacement> deco, Color sunColor, float sunIntensity, Vector3 sunEuler, Color sky, Color equator, Color ground,
            bool fog, Color fogColor, float fogDensity)
        {
            string path = $"{ThemesDir}/Theme_{id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<EnvironmentTheme>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<EnvironmentTheme>(); AssetDatabase.CreateAsset(t, path); }
            var so = new SerializedObject(t);
            so.FindProperty("themeId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("uiColor").colorValue = ui;
            so.FindProperty("lotSize").vector2Value = lot;
            so.FindProperty("groundMaterial").objectReferenceValue = ProjectBootstrapper.Mat(groundMat);
            so.FindProperty("outerGroundMaterial").objectReferenceValue = ProjectBootstrapper.Mat(outer);
            so.FindProperty("perimeterWallPrefab").objectReferenceValue = Module(wall);
            so.FindProperty("wallSegmentLength").floatValue = segment;
            LevelDesigner.WritePlacements(so.FindProperty("decoration"), deco);
            so.FindProperty("sunColor").colorValue = sunColor;
            so.FindProperty("sunIntensity").floatValue = sunIntensity;
            so.FindProperty("sunEuler").vector3Value = sunEuler;
            so.FindProperty("ambientSky").colorValue = sky;
            so.FindProperty("ambientEquator").colorValue = equator;
            so.FindProperty("ambientGround").colorValue = ground;
            so.FindProperty("fog").boolValue = fog;
            so.FindProperty("fogColor").colorValue = fogColor;
            so.FindProperty("fogDensity").floatValue = fogDensity;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(t);
        }
    }
}
