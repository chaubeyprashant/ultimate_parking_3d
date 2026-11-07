using TMPro;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Chapter 1 environment: an outdoor driving-school yard dressed with the real imported props (jersey barriers,
    /// bollards, water barriers, delineators, cones, road signs). Shared by levels 1–5; each level adds its own parked
    /// cars through LevelData props and its own targets through mission objectives.
    ///
    /// Plan (metres, +X east):
    ///   Yard            x ±44, z ±20, concrete; jersey-barrier perimeter
    ///   Training lane   z −4 … 4, dashed centre line, start line at x −38
    ///   Area A (bays)   north-west: 6 nose-in bays, x −26 … −8, z 4.5 … 10, white lines, bollards behind
    ///   Area B (rev.)   north-east: 6 reverse bays, x 8 … 26, z 4.5 … 10, yellow lines, jersey-barrier back wall
    ///   Area C (kerb)   south: parallel slots z −4.5 … −7.5 (x −10 … 25), kerb at z −7.5, raised pavement to z −12
    /// </summary>
    public static class DrivingSchoolBuilder
    {
        public const string EnvPrefabPath = "Assets/_Project/Prefabs/Real/Env_DrivingSchool.prefab";
        private const string MaterialDir = "Assets/_Project/ThirdParty/Materials";

        public const float YardHalfX = 44f, YardHalfZ = 20f, LaneHalf = 4f;
        public const float BayWidth = 3f, BayFront = 4.5f, BayBack = 10.5f;   // 6 m deep: the real pickup is 5.18 m
        public const float AreaAStartX = -26f, AreaBStartX = 8f;
        public const float KerbZ = -7.5f, SlotOuterZ = -4.5f, PavementBackZ = -12f, KerbHeight = 0.15f;
        public static readonly float[] SlotLinesX = { -10f, -3f, 4f, 11f, 18f, 25f };

        public static float BayCentreZ => (BayFront + BayBack) * 0.5f;
        public static float AreaABayX(int i) => AreaAStartX + (i + 0.5f) * BayWidth;
        public static float AreaBBayX(int i) => AreaBStartX + (i + 0.5f) * BayWidth;
        public static float SlotCentreZ => (KerbZ + SlotOuterZ) * 0.5f;

        public static void Build()
        {
            Materials();
            var root = new GameObject("Env_DrivingSchool");
            Transform ground = RealGarageBuilder.Group(root.transform, "Ground");
            Transform paint = RealGarageBuilder.Group(root.transform, "Markings");
            Transform kerb = RealGarageBuilder.Group(root.transform, "KerbAndPavement");
            Transform fence = RealGarageBuilder.Group(root.transform, "Perimeter");
            Transform dressing = RealGarageBuilder.Group(root.transform, "Dressing");

            // Ground: the yard and a wider apron so the horizon is not a void.
            RealGarageBuilder.Box(ground, "Yard", new Vector3(0f, -0.1f, 0f), new Vector3(YardHalfX * 2f, 0.2f, YardHalfZ * 2f), "M_TrainingYard", 4f);
            RealGarageBuilder.Box(ground, "Apron", new Vector3(0f, -0.13f, 0f), new Vector3(240f, 0.2f, 240f), "M_YardApron", 6f);

            // Training lane.
            for (float x = -36f; x < 40f; x += 6f)
                Paint(paint, "CentreDash", new Vector3(x + 1.5f, 0f, 0f), new Vector3(3f, 0.15f), "M_FloorPaint");
            Paint(paint, "LaneEdgeN_W", new Vector3(-35f, 0f, LaneHalf), new Vector3(18f, 0.12f), "M_FloorPaint");
            Paint(paint, "LaneEdgeN_C", new Vector3(0f, 0f, LaneHalf), new Vector3(16f, 0.12f), "M_FloorPaint");
            Paint(paint, "LaneEdgeN_E", new Vector3(35f, 0f, LaneHalf), new Vector3(18f, 0.12f), "M_FloorPaint");
            Paint(paint, "LaneEdgeS_W", new Vector3(-27f, 0f, -LaneHalf), new Vector3(34f, 0.12f), "M_FloorPaint");
            Paint(paint, "LaneEdgeS_E", new Vector3(34.5f, 0f, -LaneHalf), new Vector3(19f, 0.12f), "M_FloorPaint");
            Paint(paint, "StartLine", new Vector3(-38f, 0f, 0f), new Vector3(0.4f, LaneHalf * 2f), "M_FloorPaint");
            FloorText(paint, new Vector3(-40.5f, 0f, 0f), 90f, "START", 1.2f, Color.white);

            // Area A: nose-in bays.
            Bays(paint, "AreaA", AreaAStartX, "M_FloorPaint", "", Color.white);
            FloorText(paint, new Vector3(-17f, 0f, 12.2f), 0f, "BAY PRACTICE", 1.1f, Color.white);
            for (int i = 0; i <= 6; i++)
                RealGarageBuilder.Place(RealPropFactory.Load("Bollard"), dressing, new Vector3(AreaAStartX + i * BayWidth, 0f, BayBack + 0.6f), 0f);

            // Area B: reverse bays with a jersey-barrier back wall (gives the rear sensor something real to read).
            Bays(paint, "AreaB", AreaBStartX, "M_HazardYellow", "R", new Color(0.95f, 0.75f, 0.1f));
            FloorText(paint, new Vector3(17f, 0f, 12.6f), 0f, "REVERSE ONLY", 1.1f, new Color(0.95f, 0.75f, 0.1f));
            float wallZ = BayBack + 0.25f + PropDepth("Barrier_Concrete") * 0.5f;   // 0.25 m behind the back line
            Row(dressing, "Barrier_Concrete", new Vector3(AreaBStartX - 0.5f, 0f, wallZ), new Vector3(AreaBStartX + 6 * BayWidth + 0.5f, 0f, wallZ), 0f);

            // Area C: kerb, raised pavement (both KerbMarker), parallel slot T-marks.
            Transform kerbStone = RealGarageBuilder.Box(kerb, "Kerb", new Vector3(4f, KerbHeight * 0.5f, KerbZ - 0.15f), new Vector3(80f, KerbHeight, 0.3f), "M_KerbStone", 1f);
            kerbStone.gameObject.AddComponent<KerbMarker>();
            float pavDepth = KerbZ - 0.3f - PavementBackZ;
            Transform pavement = RealGarageBuilder.Box(kerb, "Pavement", new Vector3(4f, KerbHeight * 0.5f - 0.005f, KerbZ - 0.3f - pavDepth * 0.5f), new Vector3(80f, KerbHeight - 0.01f, pavDepth), "M_Pavement", 2f);
            pavement.gameObject.AddComponent<KerbMarker>();
            Paint(paint, "SlotEdge", new Vector3(7.5f, 0f, SlotOuterZ), new Vector3(SlotLinesX[^1] - SlotLinesX[0], 0.12f), "M_FloorPaint");
            foreach (float x in SlotLinesX)
                Paint(paint, "SlotT", new Vector3(x, 0f, SlotOuterZ - 0.5f), new Vector3(0.12f, 1f), "M_FloorPaint");
            FloorText(paint, new Vector3(-14.5f, 0f, SlotCentreZ), 90f, "PARALLEL", 0.9f, Color.white);
            for (float x = -30f; x <= 38f; x += 7f)
                RealGarageBuilder.Place(RealPropFactory.Load("Bollard"), dressing, new Vector3(x + 3.5f, KerbHeight, KerbZ - 0.75f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Sign_NoWaiting"), dressing, new Vector3(-12f, KerbHeight, KerbZ - 0.9f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Bin"), dressing, new Vector3(29f, KerbHeight, KerbZ - 1.2f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Bin"), dressing, new Vector3(-20f, KerbHeight, KerbZ - 1.2f), 0f);

            // Perimeter: real jersey barriers all round, water barriers at the lane ends.
            Row(fence, "Barrier_Concrete", new Vector3(-YardHalfX + 1f, 0f, YardHalfZ - 0.6f), new Vector3(YardHalfX - 1f, 0f, YardHalfZ - 0.6f), 0f);
            Row(fence, "Barrier_Concrete", new Vector3(-YardHalfX + 1f, 0f, -YardHalfZ + 0.6f), new Vector3(YardHalfX - 1f, 0f, -YardHalfZ + 0.6f), 0f);
            Row(fence, "Barrier_Concrete", new Vector3(-YardHalfX + 0.6f, 0f, -YardHalfZ + 1.5f), new Vector3(-YardHalfX + 0.6f, 0f, YardHalfZ - 1.5f), 90f);
            Row(fence, "Barrier_Concrete", new Vector3(YardHalfX - 0.6f, 0f, -YardHalfZ + 1.5f), new Vector3(YardHalfX - 0.6f, 0f, YardHalfZ - 1.5f), 90f);
            for (int i = 0; i < 5; i++)
                RealGarageBuilder.Place(RealPropFactory.Load(i % 2 == 0 ? "WaterBarrier_Red" : "WaterBarrier_White"), fence,
                    new Vector3(YardHalfX - 2.4f, 0f, -3.2f + i * 1.6f), 90f);

            // Signs and small furniture.
            RealGarageBuilder.Place(RealPropFactory.Load("Sign_Stop"), dressing, new Vector3(YardHalfX - 3.6f, 0f, LaneHalf + 0.8f), -90f);
            RealGarageBuilder.Place(RealPropFactory.Load("Sign_GiveWay"), dressing, new Vector3(-6.5f, 0f, LaneHalf + 0.9f), -90f);
            RealGarageBuilder.Place(RealPropFactory.Load("Sign_Warning"), dressing, new Vector3(6.5f, 0f, LaneHalf + 0.9f), -90f);
            foreach (float x in new[] { -27f, -7f, 7f, 27f })
                RealGarageBuilder.Place(RealPropFactory.Load("Delineator"), dressing, new Vector3(x, 0f, LaneHalf + 0.6f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Cones_Cluster"), dressing, new Vector3(-38f, 0f, 14f), 20f);
            RealGarageBuilder.Place(RealPropFactory.Load("Cones_Cluster"), dressing, new Vector3(36f, 0f, -15f), -30f);
            RealGarageBuilder.Place(RealPropFactory.Load("Barricade_AFrame"), dressing, new Vector3(38f, 0f, 14f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Barrel"), dressing, new Vector3(39.5f, 0f, 15.5f), 0f);
            RealGarageBuilder.Place(RealPropFactory.Load("Barrel"), dressing, new Vector3(40.5f, 0f, 14.6f), 0f);
            SchoolBoard(dressing, new Vector3(0f, 0f, YardHalfZ - 2.2f));

            Directory();
            PrefabUtility.SaveAsPrefabAsset(root, EnvPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[DrivingSchool] " + EnvPrefabPath);
        }

        // ------------------------------------------------------------------ pieces

        private static void Bays(Transform parent, string name, float startX, string mat, string prefix, Color textColour)
        {
            float len = BayBack - BayFront;
            for (int i = 0; i <= 6; i++)
                Paint(parent, name + "_Line", new Vector3(startX + i * BayWidth, 0f, BayCentreZ), new Vector3(0.12f, len), mat);
            Paint(parent, name + "_Back", new Vector3(startX + 3f * BayWidth, 0f, BayBack), new Vector3(6f * BayWidth + 0.12f, 0.12f), mat);
            for (int i = 0; i < 6; i++)
                FloorText(parent, new Vector3(startX + (i + 0.5f) * BayWidth, 0f, BayBack - 0.9f), 0f, prefix + (i + 1), 0.8f, textColour);
        }

        /// <summary>Flat paint strip (size = X, Z) on the ground.</summary>
        private static void Paint(Transform parent, string name, Vector3 pos, Vector2 size, string mat) =>
            RealGarageBuilder.Box(parent, name, pos + Vector3.up * 0.006f, new Vector3(size.x, 0.012f, size.y), mat, 1f, collider: false);

        private static void FloorText(Transform parent, Vector3 pos, float yaw, string text, float height, Color colour)
        {
            var go = new GameObject("Paint_" + text);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos + Vector3.up * 0.015f, Quaternion.Euler(90f, yaw, 0f));
            var t = go.AddComponent<TextMeshPro>();
            t.font = UiAssets.HeadingFont;
            t.text = text;
            t.fontSize = height * 10f;
            t.color = colour * 0.92f;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(12f, height * 1.6f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Real props end to end from a to b, spaced by the prop's own length.</summary>
        private static void Row(Transform parent, string prefabName, Vector3 a, Vector3 b, float yaw)
        {
            GameObject prefab = RealPropFactory.Load(prefabName);
            if (prefab == null) { Debug.LogError("[DrivingSchool] missing " + prefabName); return; }
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Bounds bounds = RealPropFactory.Bounds(probe.transform);
            Object.DestroyImmediate(probe);
            float pieceLength = Mathf.Max(0.5f, bounds.size.x) + 0.05f;   // length along local X at yaw 0
            float run = Vector3.Distance(a, b);
            int count = Mathf.Max(1, Mathf.FloorToInt(run / pieceLength));
            Vector3 dir = (b - a).normalized;
            float start = (run - count * pieceLength) * 0.5f + pieceLength * 0.5f;
            for (int i = 0; i < count; i++)
                RealGarageBuilder.Place(prefab, parent, a + dir * (start + i * pieceLength), yaw);
        }

        /// <summary>Depth (local Z at yaw 0) of a real prop.</summary>
        private static float PropDepth(string prefabName)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(RealPropFactory.Load(prefabName));
            float depth = RealPropFactory.Bounds(probe.transform).size.z;
            Object.DestroyImmediate(probe);
            return depth;
        }

        private static void SchoolBoard(Transform parent, Vector3 pos)
        {
            Transform s = RealGarageBuilder.Group(parent, "SchoolBoard");
            s.localPosition = pos;
            foreach (float x in new[] { -3.2f, 3.2f })
                RealGarageBuilder.Box(s, "Post", new Vector3(x, 1.4f, 0f), new Vector3(0.12f, 2.8f, 0.12f), "M_SteelDark", 1f);
            RealGarageBuilder.Box(s, "Board", new Vector3(0f, 2.55f, 0f), new Vector3(7.2f, 1.5f, 0.1f), "M_SignBoard", 1f, collider: false);
            RealGarageBuilder.Label(s, new Vector3(0f, 2.75f, -0.06f), 0f, "CITY DRIVING SCHOOL", 0.5f, Color.white);
            RealGarageBuilder.Label(s, new Vector3(0f, 2.15f, -0.06f), 0f, "TRAINING YARD  -  L PLATES ONLY", 0.22f, new Color(0.85f, 0.9f, 1f));
        }

        private static void Materials()
        {
            Derive("M_TrainingYard", "M_DeckConcrete", new Color(0.62f, 0.62f, 0.64f));
            Derive("M_YardApron", "M_DeckConcrete", new Color(0.48f, 0.5f, 0.47f));
            Derive("M_KerbStone", "M_ConcreteWall", new Color(0.92f, 0.92f, 0.9f));
            Derive("M_Pavement", "M_ConcreteWall", new Color(0.8f, 0.8f, 0.78f));
        }

        /// <summary>A copy of a real-texture material with a tint (same maps, no new texture memory).</summary>
        private static void Derive(string name, string source, Color tint)
        {
            Material src = RealPropFactory.Mat(source);
            if (src == null) { Debug.LogError("[DrivingSchool] missing material " + source); return; }
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetColor("_BaseColor", tint);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        private static void Directory() => System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(EnvPrefabPath));
    }
}
