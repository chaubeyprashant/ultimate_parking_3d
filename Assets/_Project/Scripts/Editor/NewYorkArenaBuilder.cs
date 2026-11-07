using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using UltimateCarParking.WorldTour;
using F = UltimateCarParking.EditorTools.ArenaPropFactory;
using W = UltimateCarParking.EditorTools.ArenaLevelWriter;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// USA — New York City arena, built from the imported New York models (brownstone and downtown building sets,
    /// skyline landmarks, yellow cab, sedans, pickups, police cruiser, hydrants, parking meters, newspaper boxes,
    /// hot-dog cart, subway entrance, traffic lights, US warning signs, a supermarket with its lot). Right-hand traffic.
    /// </summary>
    public static partial class NewYorkArenaBuilder
    {
        public const string Arena = "NewYork";
        public const string CountryId = "us";
        public static string EnvPath => F.PrefabPath(Arena, "Env_NewYork");
        public const RoadDirection Road = RoadDirection.RightHand;

        private static System.Func<Transform, IEnumerable<Transform>> Exact(params string[] names) => root =>
        {
            var hits = root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList();
            if (hits.Count == 0) throw new System.Exception("no node named " + string.Join("/", names));
            return hits.Where(t => !hits.Any(h => h != t && t.IsChildOf(h)));
        };

        public static readonly string[] Brownstones =
        {
            "Brownstone_Classic_1", "Brownstone_Commercial_1_C", "Brownstone_FlatFacade_1", "Brownstone_FlatFacade_2", "Brownstone_FlatFacade_3",
            "Brownstone_FlatFacade_4", "Brownstone_FlatFacade_5", "Brownstone_FlatFacade_6", "Brownstone_FlatFacade_7", "Brownstone_FlatFacade_8",
            "Brownstone_FlatFacade_9", "Brownstone_FlatFacade_12", "Brownstone_FlatFacade_12_C", "Brownstone_Harlem_1_B", "Brownstone_Lowrise_1",
            "Brownstone_ClassicComplex_2", "Brownstone_Commercial_1", "Brownstone_FlatFacadeComplex_1",
        };

        public static readonly string[] Downtown =
        {
            "Downtown_BrownstoneOffice_1", "Downtown_ClassicOffice_1", "Downtown_ModernOffice_4", "Downtown_ModernOffice_5", "Downtown_ArtDeco_1",
            "Downtown_Residential_2", "Downtown_Flatiron_1", "Downtown_PublicBuilding_1", "Downtown_Mall_1", "Downtown_ModernOffice_1_B",
        };

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                // Vehicles: imported with the front along -X, so yaw 90 turns it to +Z. Real-world lengths.
                new F.Spec { name = "Taxi_Yellow", source = "NewYork/nyc_taxi.glb", yaw = 90f, fit = F.Fit.Length, size = 5.2f, colliderHeightCap = 1.6f, strip = new[] { "Interior" } },
                new F.Spec { name = "Sedan_Fairheaven", source = "NewYork/nyc_fairheaven.glb", yaw = 90f, fit = F.Fit.Length, size = 5.0f, colliderHeightCap = 1.5f, strip = new[] { "Interior" } },
                new F.Spec { name = "Sedan_Negotiator", source = "NewYork/nyc_negotiator.glb", yaw = 90f, fit = F.Fit.Length, size = 5.0f, colliderHeightCap = 1.5f, strip = new[] { "Interior" } },
                new F.Spec { name = "Coupe_Phoenix", source = "NewYork/nyc_phoenix.glb", yaw = 90f, fit = F.Fit.Length, size = 4.8f, colliderHeightCap = 1.4f, strip = new[] { "Interior" } },
                new F.Spec { name = "Police_Cruiser", source = "NewYork/nyc_cop_cruiser.glb", yaw = 90f, fit = F.Fit.Length, size = 5.0f, colliderHeightCap = 1.6f, strip = new[] { "Interior" } },
                new F.Spec { name = "Pickup_Doutrive", source = "NewYork/nyc_pickup_doutrive.glb", yaw = 90f, fit = F.Fit.Length, size = 4.9f, colliderHeightCap = 1.7f, strip = new[] { "Interior" } },
                new F.Spec { name = "Pickup_Lightbody", source = "NewYork/nyc_pickup_lightbody85.glb", yaw = 90f, fit = F.Fit.Length, size = 5.2f, colliderHeightCap = 1.8f, strip = new[] { "Interior" } },
                new F.Spec { name = "Utility_Truck", source = "NewYork/nyc_pickup_lightbody90.glb", yaw = 90f, fit = F.Fit.Length, size = 5.6f, colliderHeightCap = 1.9f, strip = new[] { "Interior" } },
                // Street furniture
                new F.Spec { name = "Hydrant", source = "NewYork/nyc_hydrant.glb", fit = F.Fit.Height, size = 0.85f },
                new F.Spec { name = "Parking_Meter", source = "NewYork/nyc_parking_meter.glb", fit = F.Fit.Height, size = 1.4f },
                new F.Spec { name = "Newspaper_Box", source = "NewYork/nyc_newspaper_box.glb", fit = F.Fit.Height, size = 1.2f },
                new F.Spec { name = "Hotdog_Cart", source = "NewYork/nyc_hotdog_cart.glb", fit = F.Fit.Height, size = 2.5f },
                new F.Spec { name = "Traffic_Light", source = "NewYork/nyc_traffic_light.glb", fit = F.Fit.Height, size = 6.5f },
                new F.Spec { name = "Subway_Entrance", source = "NewYork/nyc_subway_entrance.glb", fit = F.Fit.Width, size = 6.5f },
                new F.Spec { name = "Lamp_Pole", source = "NewYork/nyc_parking_lot_uf.glb", pick = F.Named("Lamp1"), fit = F.Fit.Height, size = 7.5f },
                // US warning signs (faces +Z)
                new F.Spec { name = "Sign_LowClearance", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.090"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_TruckCrossing", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.094"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_SignalAhead", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.034"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_StopAhead", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.038"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_Ramp", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.088"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_Arrow", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.069"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_PhotoEnforced", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.079"), fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Sign_Speed35", source = "NewYork/nyc_us_signs.glb", pick = Exact("Cube.083"), fit = F.Fit.Height, size = 2.6f },
                // Supermarket with its striped lot: modelled at 1:10 (its cars are 0.46 long). Cars left out; colliders added in the layout.
                new F.Spec { name = "Supermarket", source = "NewYork/nyc_parking_lot_uf.glb", pick = Exact("ParkingLot", "Pave", "Building", "Vegetation", "Signs", "Lamps"),
                    fit = F.Fit.Scale, size = 10f, collider = F.ColliderKind.None },
            };
            foreach (string b in Brownstones) specs.Add(new F.Spec { name = b, source = "NewYork/nyc_brownstone_set.glb", pick = Exact(b) });
            foreach (string b in Downtown) specs.Add(new F.Spec { name = b, source = "NewYork/nyc_downtown_set.glb", pick = Exact(b) });
            // Skyline landmarks (backdrop, no collider), real heights.
            foreach (var (node, name, h) in new[] { ("emp_state._final_14", "Landmark_EmpireState", 443f), ("tower_one_16", "Landmark_OneWTC", 541f),
                         ("christler_17", "Landmark_Chrysler", 319f), ("woolworth_18", "Landmark_Woolworth", 241f), ("new_york_times_19", "Landmark_NYTimes", 319f),
                         ("metlife_15", "Landmark_MetLife", 246f) })
                specs.Add(new F.Spec { name = name, source = "NewYork/nyc_buildings.glb", pick = Exact(node), fit = F.Fit.Height, size = h, collider = F.ColliderKind.None });

            foreach (F.Spec s in specs) F.Make(Arena, s);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] New York props: {specs.Count}");
        }

        internal static GameObject P(string name)
        {
            GameObject g = F.Load(Arena, name);
            if (g == null) Debug.LogError("[Arena] New York prefab missing: " + name);
            return g;
        }

        /// <summary>
        /// Top-down orthographic render of a prefab with a 5 m grid (1 m ticks), used to read painted bay positions off
        /// imported lots. Writes ../UltimateCarParking3D_logs/arena_parts/top_&lt;name&gt;.png; logs bounds.
        /// </summary>
        public static void TopView(GameObject prefab, string name, float pxPerMetre = 12f)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * 0.9f;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rs = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            int w = Mathf.CeilToInt(b.size.x * pxPerMetre), h = Mathf.CeilToInt(b.size.z * pxPerMetre);
            var cam = new GameObject("Top").AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = b.size.z * 0.5f; cam.aspect = b.size.x / b.size.z;
            cam.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 5f, b.center.z), Quaternion.Euler(90f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.farClipPlane = b.size.y + 20f;
            var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            for (float x = Mathf.Ceil(b.min.x); x <= b.max.x; x += 1f)
                for (int py = 0; py < h; py += (Mathf.RoundToInt(x) % 5 == 0 ? 1 : 6))
                    tex.SetPixel(Mathf.RoundToInt((x - b.min.x) * pxPerMetre), py, Mathf.RoundToInt(x) % 5 == 0 ? Color.red : Color.magenta);
            for (float z = Mathf.Ceil(b.min.z); z <= b.max.z; z += 1f)
                for (int px = 0; px < w; px += (Mathf.RoundToInt(z) % 5 == 0 ? 1 : 6))
                    tex.SetPixel(px, Mathf.RoundToInt((z - b.min.z) * pxPerMetre), Mathf.RoundToInt(z) % 5 == 0 ? Color.cyan : Color.blue);
            tex.Apply(); RenderTexture.active = null;
            string dir = Path.GetFullPath("../UltimateCarParking3D_logs/arena_parts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"top_{name}.png"), tex.EncodeToPNG());
            Debug.Log($"[Arena] top view {name}: bounds min {b.min} max {b.max} (red/cyan lines every 5 m at multiples of 5)");
            foreach (Transform t in inst.transform)
            {
                var r = t.GetComponent<Renderer>(); if (r == null) continue;
                Debug.Log($"[Arena]   part {t.name}: centre {r.bounds.center} size {r.bounds.size}");
            }
            Object.DestroyImmediate(inst);
        }

        public static void MeasureSupermarket() => TopView(P("Supermarket"), "Supermarket");
        public static void PropsAndMeasure() { BuildProps(); MeasureSupermarket(); }
    }
}
