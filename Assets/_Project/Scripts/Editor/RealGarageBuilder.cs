using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Phase D: assembles "Garage B1" — the first level built from real imported models.
    ///
    /// Plan (metres, +X = driving direction from the entry):
    ///   Entry lane   x −27 … −9.75, z ±6      deck concrete, ceiling, walls, headroom bar, EXIT door, bollards
    ///   Parking hall x −9.75 … 9.75, z ±14.6  imported garage module (painted bays, columns, light strips),
    ///                                         6 bays per side, ceiling with beams, fluorescent fittings, signage
    ///   Ramp         x 9.75 … 34, z ±3.6      flat, then up 2.4 m to an upper landing closed by barriers (open sky)
    /// Ceiling slab at 3.88 m (top of the imported columns), beams down to 3.38 m; chase camera at 2.1 m.
    /// </summary>
    public static class RealGarageBuilder
    {
        public const string EnvPrefabPath = "Assets/_Project/Prefabs/Real/Env_RealGarage_B1.prefab";
        public const string MeshDir = "Assets/_Project/Environment/RealGarage/Meshes";
        public const string LevelPath = "Assets/_Project/Levels/Real/Level_RG01.asset";
        public const string CatalogPath = "Assets/_Project/Levels/Real/RealCatalog.asset";

        public const float HallHalfX = 9.755f, HallHalfZ = 14.637f, CeilingY = 3.88f, Slab = 0.3f;
        public const float EntryStartX = -27f, EntryHalfZ = 6f, RampHalfZ = 3.6f, RampFlatEndX = 13f, RampTopX = 27f, RampEndX = 34f, RampRise = 2.4f;
        public const float AisleHalf = 2.79f, BayBack = 8.89f;
        /// <summary>Bay centres along X (from the module's painted lines), same for both rows.</summary>
        public static readonly float[] BayX = { -8.08f, -4.78f, -1.45f, 1.71f, 4.80f, 8.10f };
        public const int TargetBay = 3;   // row +Z
        public static Vector3 TargetCentre => new Vector3(BayX[TargetBay], 0f, (AisleHalf + BayBack) * 0.5f);

        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        public static void Build()
        {
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LevelPath));
            meshCache.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Env_RealGarage_B1");
            var lighting = root.AddComponent<SceneLightingOverride>();
            Transform structure = Group(root.transform, "Structure");
            Transform dressing = Group(root.transform, "Dressing");
            Transform signs = Group(root.transform, "Signs");
            Transform lights = Group(root.transform, "Lighting");
            Transform dynamicProps = Group(root.transform, "DynamicProps");

            // --- hall: the imported module
            Place(RealPropFactory.Load("GarageModule"), structure, Vector3.zero, 0f);

            // --- entry lane floor + ramp floors (deck concrete, real-world UVs)
            Box(structure, "EntryFloor", new Vector3((EntryStartX - HallHalfX) * 0.5f, -0.15f, 0f), new Vector3(-HallHalfX - EntryStartX, 0.3f, EntryHalfZ * 2f), "M_DeckConcrete", 4f);
            Box(structure, "RampFlat", new Vector3((HallHalfX + RampFlatEndX) * 0.5f, -0.15f, 0f), new Vector3(RampFlatEndX - HallHalfX, 0.3f, RampHalfZ * 2f), "M_DeckConcrete", 4f);
            float runLen = RampTopX - RampFlatEndX;
            float slopeLen = Mathf.Sqrt(runLen * runLen + RampRise * RampRise);
            float angle = Mathf.Atan2(RampRise, runLen) * Mathf.Rad2Deg;
            Transform ramp = Box(structure, "RampSlope", new Vector3((RampFlatEndX + RampTopX) * 0.5f, RampRise * 0.5f - 0.15f, 0f), new Vector3(slopeLen, 0.3f, RampHalfZ * 2f), "M_DeckConcrete", 4f);
            ramp.localRotation = Quaternion.Euler(0f, 0f, angle);
            Box(structure, "RampLanding", new Vector3((RampTopX + RampEndX) * 0.5f, RampRise - 0.15f, 0f), new Vector3(RampEndX - RampTopX, 0.3f, RampHalfZ * 2f), "M_DeckConcrete", 4f);
            Box(structure, "RampUnderfill", new Vector3((RampFlatEndX + RampEndX) * 0.5f, RampRise * 0.5f - 0.5f, 0f), new Vector3(RampEndX - RampFlatEndX, RampRise, RampHalfZ * 2f - 0.02f), "M_ConcreteWall", 4f, collider: false);
            Box(structure, "OuterGround", new Vector3(0f, -0.35f, 0f), new Vector3(140f, 0.3f, 80f), "M_DeckConcrete", 6f);

            // --- walls (concrete, 0.3 thick, with a hazard kick band on the inner face)
            Wall(structure, "HallWallNorth", new Vector3(0f, 0f, HallHalfZ + 0.15f), HallHalfX * 2f, false, -1f);
            Wall(structure, "HallWallSouth", new Vector3(0f, 0f, -HallHalfZ - 0.15f), HallHalfX * 2f, false, 1f);
            float side = HallHalfZ - EntryHalfZ;
            Wall(structure, "HallWestN", new Vector3(-HallHalfX - 0.15f, 0f, EntryHalfZ + side * 0.5f), side, true, 1f);
            Wall(structure, "HallWestS", new Vector3(-HallHalfX - 0.15f, 0f, -EntryHalfZ - side * 0.5f), side, true, 1f);
            float eside = HallHalfZ - RampHalfZ;
            Wall(structure, "HallEastN", new Vector3(HallHalfX + 0.15f, 0f, RampHalfZ + eside * 0.5f), eside, true, -1f);
            Wall(structure, "HallEastS", new Vector3(HallHalfX + 0.15f, 0f, -RampHalfZ - eside * 0.5f), eside, true, -1f);
            float elen = -HallHalfX - EntryStartX;
            Wall(structure, "EntryWallN", new Vector3(EntryStartX + elen * 0.5f, 0f, EntryHalfZ + 0.15f), elen, false, -1f);
            Wall(structure, "EntryWallS", new Vector3(EntryStartX + elen * 0.5f, 0f, -EntryHalfZ - 0.15f), elen, false, 1f);
            Wall(structure, "EntryEnd", new Vector3(EntryStartX - 0.15f, 0f, 0f), EntryHalfZ * 2f, true, 1f);
            // Ramp parapets (1.1 m) along both sides, following the slope.
            Parapet(structure, "RampFlatN", HallHalfX, RampFlatEndX, 0f, 0f, RampHalfZ + 0.15f);
            Parapet(structure, "RampFlatS", HallHalfX, RampFlatEndX, 0f, 0f, -RampHalfZ - 0.15f);
            Parapet(structure, "RampSlopeN", RampFlatEndX, RampTopX, 0f, RampRise, RampHalfZ + 0.15f);
            Parapet(structure, "RampSlopeS", RampFlatEndX, RampTopX, 0f, RampRise, -RampHalfZ - 0.15f);
            Parapet(structure, "RampTopN", RampTopX, RampEndX, RampRise, RampRise, RampHalfZ + 0.15f);
            Parapet(structure, "RampTopS", RampTopX, RampEndX, RampRise, RampRise, -RampHalfZ - 0.15f);
            Parapet(structure, "RampEnd", RampEndX, RampEndX, RampRise, RampRise, 0f, endWall: true);

            // --- ceiling slab + downstand beams over the hall and entry (ramp slope is open to the sky)
            Box(structure, "CeilingHall", new Vector3(0f, CeilingY + Slab * 0.5f, 0f), new Vector3(HallHalfX * 2f, Slab, HallHalfZ * 2f), "M_CeilingConcrete", 4f, collider: false);
            Box(structure, "CeilingEntry", new Vector3(EntryStartX + elen * 0.5f, CeilingY + Slab * 0.5f, 0f), new Vector3(elen, Slab, EntryHalfZ * 2f), "M_CeilingConcrete", 4f, collider: false);
            Box(structure, "CeilingRampStart", new Vector3((HallHalfX + RampFlatEndX) * 0.5f, CeilingY + Slab * 0.5f, 0f), new Vector3(RampFlatEndX - HallHalfX, Slab, RampHalfZ * 2f), "M_CeilingConcrete", 4f, collider: false);
            float[] columnX = { -6.49f, -3.14f, 0.24f, 3.61f, 6.98f };
            foreach (float x in columnX)
                Box(structure, "Beam", new Vector3(x, CeilingY - 0.25f, 0f), new Vector3(0.45f, 0.5f, HallHalfZ * 2f), "M_CeilingConcrete", 2f, collider: false);
            for (float x = EntryStartX + 4f; x < -HallHalfX; x += 5f)
                Box(structure, "EntryBeam", new Vector3(x, CeilingY - 0.25f, 0f), new Vector3(0.45f, 0.5f, EntryHalfZ * 2f), "M_CeilingConcrete", 2f, collider: false);

            // --- lighting fixtures (Poly Haven) along the aisles, plus the module's own light strips
            for (float x = EntryStartX + 2.5f; x < -HallHalfX - 0.5f; x += 5f)
                Place(RealPropFactory.Load("FluorescentLight"), lights, new Vector3(x, CeilingY - 0.02f, 0f), 90f);
            foreach (float x in new[] { -8.0f, -1.5f, 5.0f })
                Place(RealPropFactory.Load("FluorescentLight"), lights, new Vector3(x, CeilingY - 0.02f, 0f), 90f);

            // --- floor paint: aisle arrows and a stop line at the ramp
            for (float x = EntryStartX + 5f; x < 8f; x += 7f) Arrow(dressing, new Vector3(x, 0.012f, -1.4f));
            Box(dressing, "StopLine", new Vector3(HallHalfX + 1.2f, 0.01f, 0f), new Vector3(0.3f, 0.02f, RampHalfZ * 2f - 0.2f), "M_FloorPaint", 1f, collider: false);

            // --- signage (3D text on boards, game fonts)
            // Boards span the lane (Z) and face along the driving direction (±X).
            HangingSign(signs, new Vector3(-20f, 2.85f, 0f), 0f, "P  LEVEL B1", "M_SignBoard", "PARKING  >");
            HangingSign(signs, new Vector3(-12.5f, 2.85f, 0f), 0f, "EXIT", "M_SignExit", "<  STREET");
            HangingSign(signs, new Vector3(7.8f, 2.85f, 0f), 0f, "RAMP", "M_SignBoard", "LEVEL 2  >");
            WallSign(signs, new Vector3(EntryStartX + 0.02f, 1.9f, 3.6f), 90f, "EXIT", "M_SignExit");
            WallSign(signs, new Vector3(EntryStartX + 0.02f, 1.9f, -3.2f), 90f, "B1", "M_SignBoard");
            HeadroomBar(structure, new Vector3(EntryStartX + 3.2f, 3.0f, 0f));
            foreach (float x in new[] { -8.08f, 1.71f, 8.10f })   // bay row labels on the back zone wall
            {
                WallSign(signs, new Vector3(x, 1.7f, HallHalfZ - 0.01f), 180f, x < 0f ? "A" : x < 5f ? "B" : "C", "M_SignBoard", 0.9f);
                WallSign(signs, new Vector3(x, 1.7f, -HallHalfZ + 0.01f), 0f, x < 0f ? "D" : x < 5f ? "E" : "F", "M_SignBoard", 0.9f);
            }

            // --- props: parked cars, covered car, barriers, cones, bollards
            string[] parked = { "Parked_wagon", "Parked_hatchback", "Parked_compact", "Parked_coupe", "Parked_minivan", "Parked_sport", "Parked_suv", "Parked_pickup", "Parked_sedan", "Parked_offroad" };
            var rng = new System.Random(2026);
            int k = 0;
            for (int row = 0; row < 2; row++)
                for (int b = 0; b < BayX.Length; b++)
                {
                    float s = row == 0 ? 1f : -1f;
                    var c = new Vector3(BayX[b], 0f, s * (AisleHalf + BayBack) * 0.5f);
                    if (row == 0 && b == TargetBay) continue;                                   // the player's bay
                    if (row == 1 && b == 1) { Place(RealPropFactory.Load("CoveredCar"), dressing, c, 0f); continue; }
                    if (row == 1 && b == 4)                                                      // closed bay
                    {
                        Place(RealPropFactory.Load("Cones_Cluster"), dynamicProps, c + new Vector3(0f, 0f, -1.8f * s), 0f);
                        Place(RealPropFactory.Load("Barricade_AFrame"), dynamicProps, c + new Vector3(0f, 0f, -0.6f * s), 0f);
                        Place(RealPropFactory.Load("WetFloorSign"), dynamicProps, c + new Vector3(0.9f, 0f, 1.2f * s), 30f);
                        continue;
                    }
                    if (row == 0 && b == 0) continue;                                            // a decoy empty bay
                    float yaw = (s > 0f ? 0f : 180f) + (rng.NextDouble() < 0.3 ? 180f : 0f) + (float)(rng.NextDouble() * 3.0 - 1.5);
                    Place(RealPropFactory.Load(parked[k++ % parked.Length]), dressing, c + new Vector3((float)(rng.NextDouble() * 0.2 - 0.1), 0f, 0f), yaw);
                }
            Place(RealPropFactory.Load("Barrier_Concrete"), dressing, new Vector3(RampEndX - 1.2f, RampRise, 0f), 90f);
            Place(RealPropFactory.Load("WaterBarrier_Red"), dressing, new Vector3(RampTopX + 2.5f, RampRise, 1.2f), 90f);
            Place(RealPropFactory.Load("WaterBarrier_White"), dressing, new Vector3(RampTopX + 2.5f, RampRise, -0.2f), 90f);
            Place(RealPropFactory.Load("WaterBarrier_Red"), dressing, new Vector3(RampTopX + 2.5f, RampRise, -1.6f), 90f);
            Place(RealPropFactory.Load("Sign_Stop"), dressing, new Vector3(HallHalfX + 1.6f, 0f, RampHalfZ - 0.45f), -90f);
            Place(RealPropFactory.Load("Sign_NoWaiting"), dressing, new Vector3(-16f, 0f, EntryHalfZ - 0.45f), 180f);
            Place(RealPropFactory.Load("Sign_Warning"), dressing, new Vector3(RampTopX - 1f, RampRise, -RampHalfZ + 0.45f), -90f);
            foreach (float z in new[] { 2.4f, 3.2f, 4.0f, 4.8f })
                Place(RealPropFactory.Load("Bollard"), dressing, new Vector3(EntryStartX + 1.4f, 0f, z), 0f);
            Place(RealPropFactory.Load("Bin"), dressing, new Vector3(-9.0f, 0f, HallHalfZ - 0.6f), 180f);
            Place(RealPropFactory.Load("Barrel"), dynamicProps, new Vector3(9.0f, 0f, -HallHalfZ + 0.7f), 0f);
            Place(RealPropFactory.Load("Barrier_Metal"), dressing, new Vector3(-3f, 0f, HallHalfZ - 0.8f), 0f);
            Place(RealPropFactory.Load("Delineator"), dynamicProps, new Vector3(HallHalfX + 0.6f, 0f, RampHalfZ - 0.4f), 0f);
            Place(RealPropFactory.Load("Delineator"), dynamicProps, new Vector3(HallHalfX + 0.6f, 0f, -RampHalfZ + 0.4f), 0f);

            foreach (Transform t in structure.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnvPrefabPath);
            Object.DestroyImmediate(root);
            WriteLevel(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[RealGarage] " + EnvPrefabPath + " + " + LevelPath);
        }

        // ------------------------------------------------------------------ level data

        private static void WriteLevel(GameObject env)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, LevelPath); }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = "RG01";
            so.FindProperty("displayName").stringValue = "Garage B1";
            so.FindProperty("levelNumber").intValue = 1;
            so.FindProperty("theme").objectReferenceValue = null;
            so.FindProperty("environmentPrefab").objectReferenceValue = env;
            LevelDesigner.WritePlacements(so.FindProperty("props"), new List<PropPlacement>());
            so.FindProperty("forcedCar").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarData>(RealCarFactory.CarDataPath);
            so.FindProperty("cameraHeight").floatValue = 2.1f;
            so.FindProperty("cameraDistance").floatValue = 5.6f;
            so.FindProperty("spawnPosition").vector3Value = new Vector3(-22f, 0f, -1.2f);
            so.FindProperty("spawnYaw").floatValue = 90f;
            so.FindProperty("targetPosition").vector3Value = TargetCentre;
            so.FindProperty("targetYaw").floatValue = 0f;
            so.FindProperty("targetSize").vector2Value = new Vector2(3.0f, 5.6f);
            so.FindProperty("timeLimitSeconds").floatValue = 150f;
            so.FindProperty("collisionLimit").intValue = 3;
            so.FindProperty("overrideParkingRules").boolValue = true;
            SerializedProperty r = so.FindProperty("parkingRules");
            r.FindPropertyRelative("edgeMargin").floatValue = 0.05f;
            r.FindPropertyRelative("angleTolerance").floatValue = 10f;
            r.FindPropertyRelative("allowEitherDirection").boolValue = true;
            r.FindPropertyRelative("maxSpeedKmh").floatValue = 2f;
            r.FindPropertyRelative("holdDuration").floatValue = 1.5f;
            so.FindProperty("threeStarTime").floatValue = 60f;
            so.FindProperty("threeStarMaxCollisions").intValue = 0;
            so.FindProperty("twoStarTime").floatValue = 100f;
            so.FindProperty("twoStarMaxCollisions").intValue = 1;
            so.FindProperty("baseCoins").intValue = 200;
            so.FindProperty("coinsPerStar").intValue = 50;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<LevelCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var cso = new SerializedObject(catalog);
            cso.FindProperty("levels").arraySize = 1;
            cso.FindProperty("levels").GetArrayElementAtIndex(0).objectReferenceValue = level;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ pieces

        internal static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        internal static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw)
        {
            if (prefab == null) { Debug.LogError("[RealGarage] missing prop prefab"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>Box with UVs in metres / <paramref name="texMetres"/> on every face (no stretched textures).</summary>
        internal static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, string material, float texMetres, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<MeshFilter>().sharedMesh = TiledBox(size, texMetres);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = RealPropFactory.Mat(material);
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go.transform;
        }

        private static void Wall(Transform parent, string name, Vector3 baseCentre, float length, bool alongZ, float inwardSign)
        {
            Vector3 size = alongZ ? new Vector3(0.3f, CeilingY, length) : new Vector3(length, CeilingY, 0.3f);
            Box(parent, name, baseCentre + Vector3.up * (CeilingY * 0.5f), size, "M_ConcreteWall", 3f);
            Vector3 inward = alongZ ? new Vector3(inwardSign * 0.16f, 0f, 0f) : new Vector3(0f, 0f, inwardSign * 0.16f);
            Vector3 bandSize = alongZ ? new Vector3(0.02f, 0.5f, length) : new Vector3(length, 0.5f, 0.02f);
            Transform band = Box(parent, name + "_Band", baseCentre + inward + Vector3.up * 0.35f, bandSize, "M_HazardYellow", 1f, collider: false);
            Transform stripe = Box(parent, name + "_BandTrim", baseCentre + inward * 1.05f + Vector3.up * 0.62f, alongZ ? new Vector3(0.02f, 0.06f, length) : new Vector3(length, 0.06f, 0.02f), "M_HazardBlack", 1f, collider: false);
            _ = band; _ = stripe;
        }

        /// <summary>1.1 m concrete parapet from (x0, y0) to (x1, y1) along a line of constant z (or across the end).</summary>
        private static void Parapet(Transform parent, string name, float x0, float x1, float y0, float y1, float z, bool endWall = false)
        {
            if (endWall)
            {
                Box(parent, name, new Vector3(x0 + 0.15f, y0 + 0.55f, 0f), new Vector3(0.3f, 1.1f, RampHalfZ * 2f + 0.6f), "M_ConcreteWall", 3f);
                return;
            }
            float run = x1 - x0, rise = y1 - y0;
            float len = Mathf.Sqrt(run * run + rise * rise);
            Transform t = Box(parent, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f + 0.55f, z), new Vector3(len + 0.05f, 1.1f, 0.3f), "M_ConcreteWall", 3f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(rise, run) * Mathf.Rad2Deg);
        }

        private static void HeadroomBar(Transform parent, Vector3 centre)
        {
            Transform bar = Group(parent, "HeadroomBar");
            bar.localPosition = centre;
            for (int i = 0; i < 12; i++)
                Box(bar, "Stripe", new Vector3(0f, 0f, -EntryHalfZ + 0.5f + i * ((EntryHalfZ * 2f - 1f) / 12f)), new Vector3(0.18f, 0.25f, (EntryHalfZ * 2f - 1f) / 12f), i % 2 == 0 ? "M_HazardYellow" : "M_HazardBlack", 1f, collider: false);
            Label(bar, new Vector3(-0.1f, 0f, 0f), -90f, "MAX HEADROOM 2.1 m", 0.16f, Color.black);
            foreach (float z in new[] { -EntryHalfZ + 0.3f, EntryHalfZ - 0.3f })
                Box(bar, "Chain", new Vector3(0f, (CeilingY - centre.y) * 0.5f, z), new Vector3(0.04f, CeilingY - centre.y, 0.04f), "M_SteelDark", 1f, collider: false);
        }

        private static void HangingSign(Transform parent, Vector3 centre, float yaw, string title, string boardMat, string sub)
        {
            Transform s = Group(parent, "Sign_" + title.Replace(" ", ""));
            s.localPosition = centre;
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(s, "Board", Vector3.zero, new Vector3(0.08f, 0.62f, 2.6f), boardMat, 1f, collider: false);
            Box(s, "Frame", new Vector3(0f, 0.33f, 0f), new Vector3(0.1f, 0.04f, 2.64f), "M_SteelDark", 1f, collider: false);
            foreach (float z in new[] { -1.1f, 1.1f })
                Box(s, "Hanger", new Vector3(0f, (CeilingY - centre.y) * 0.5f + 0.15f, z), new Vector3(0.03f, CeilingY - centre.y - 0.3f, 0.03f), "M_SteelDark", 1f, collider: false);
            foreach (float face in new[] { -1f, 1f })
            {
                Label(s, new Vector3(face * 0.045f, 0.08f, 0f), face > 0 ? -90f : 90f, title, 0.32f, Color.white);
                Label(s, new Vector3(face * 0.045f, -0.18f, 0f), face > 0 ? -90f : 90f, sub, 0.16f, new Color(0.85f, 0.9f, 1f));
            }
        }

        private static void WallSign(Transform parent, Vector3 pos, float yaw, string text, string boardMat, float size = 0.7f)
        {
            Transform s = Group(parent, "WallSign_" + text);
            s.localPosition = pos;
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(s, "Board", Vector3.zero, new Vector3(size * 1.4f, size, 0.04f), boardMat, 1f, collider: false);
            Label(s, new Vector3(0f, 0f, 0.025f), 180f, text, size * 0.55f, Color.white);
        }

        internal static void Label(Transform parent, Vector3 pos, float yaw, string text, float height, Color colour)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var t = go.AddComponent<TextMeshPro>();
            t.font = UiAssets.HeadingFont;
            t.text = text;
            t.fontSize = height * 10f;           // TMP world units: fontSize 10 ≈ 1 m cap height
            t.color = colour;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(6f, height * 1.6f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Arrow(Transform parent, Vector3 pos)
        {
            Transform a = Group(parent, "FloorArrow");
            a.localPosition = pos;
            Box(a, "Shaft", new Vector3(-0.5f, 0f, 0f), new Vector3(1.6f, 0.02f, 0.25f), "M_FloorPaint", 1f, collider: false);
            Transform h1 = Box(a, "HeadA", new Vector3(0.45f, 0f, 0.25f), new Vector3(0.75f, 0.02f, 0.22f), "M_FloorPaint", 1f, collider: false);
            h1.localRotation = Quaternion.Euler(0f, 40f, 0f);
            Transform h2 = Box(a, "HeadB", new Vector3(0.45f, 0f, -0.25f), new Vector3(0.75f, 0.02f, 0.22f), "M_FloorPaint", 1f, collider: false);
            h2.localRotation = Quaternion.Euler(0f, -40f, 0f);
        }

        /// <summary>Box mesh whose UVs are in metres / texMetres on each face; cached and saved as an asset.</summary>
        private static Mesh TiledBox(Vector3 size, float texMetres)
        {
            string key = $"box_{size.x:F2}_{size.y:F2}_{size.z:F2}_{texMetres:F1}".Replace('.', 'p').Replace('-', 'm');
            if (meshCache.TryGetValue(key, out Mesh cached)) return cached;
            string path = $"{MeshDir}/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = key };
                Vector3 h = size * 0.5f;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                void Face(Vector3 normal, Vector3 u, Vector3 w, float uLen, float wLen)
                {
                    int i = v.Count;
                    Vector3 c = Vector3.Scale(normal, h);
                    Vector3 du = u * (uLen * 0.5f), dw = w * (wLen * 0.5f);
                    v.Add(c - du - dw); v.Add(c + du - dw); v.Add(c + du + dw); v.Add(c - du + dw);
                    for (int k = 0; k < 4; k++) n.Add(normal);
                    uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(uLen / texMetres, 0f)); uv.Add(new Vector2(uLen / texMetres, wLen / texMetres)); uv.Add(new Vector2(0f, wLen / texMetres));
                    tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                }
                Face(Vector3.up, Vector3.right, Vector3.back, size.x, size.z);
                Face(Vector3.down, Vector3.right, Vector3.forward, size.x, size.z);
                // Winding: cross(w, u) must equal −normal for Unity's clockwise front faces.
                Face(Vector3.forward, Vector3.right, Vector3.up, size.x, size.y);
                Face(Vector3.back, Vector3.left, Vector3.up, size.x, size.y);
                Face(Vector3.right, Vector3.back, Vector3.up, size.z, size.y);
                Face(Vector3.left, Vector3.forward, Vector3.up, size.z, size.y);
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>Batch entry: props, materials and the level, then exit.</summary>
        public static void BuildAllBatch()
        {
            RealPropFactory.BuildAll();
            Build();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
