using System.IO;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Builds the five drivable cars from one parametric primitive model (body, cabin, lights, wheels, colliders),
    /// their paint materials, CarData assets and the CarCatalog. The sedan keeps the Phase 1 dimensions and
    /// handling, at the original PlayerCar.prefab path.
    /// </summary>
    public static class CarFactory
    {
        public const string CarsDir = "Assets/_Project/Cars";
        public const string CarCatalogPath = CarsDir + "/CarCatalog.asset";
        private const string PrefabsDir = ProjectBootstrapper.PrefabsDir;

        private enum Extra { None, Spoiler, RoofRails, PickupBed }

        private sealed class Spec
        {
            public string Id, Prefab, Name, Tagline, Paint;
            public Color PaintColor;
            public int Price;
            public bool Starter;
            public float Length, Width, ChassisY, ChassisH, CabinLength, CabinH, CabinZ;
            public float WheelRadius, WheelWidth = 0.22f, HalfTrack, HalfWheelbase;
            public Extra Extra;
            public VehicleHandling Handling;
        }

        private static Spec[] Specs => new[]
        {
            new Spec
            {
                Id = "sedan", Prefab = "PlayerCar", Name = "City Sedan", Tagline = "Balanced, forgiving, made for tight lots.",
                Paint = "Paint_Crimson", PaintColor = new Color(0.78f, 0.12f, 0.10f), Price = 0, Starter = true,
                Length = 4.2f, Width = 1.8f, ChassisY = 0.72f, ChassisH = 0.5f, CabinLength = 2.2f, CabinH = 0.48f, CabinZ = -0.25f,
                WheelRadius = 0.34f, HalfTrack = 0.78f, HalfWheelbase = 1.35f,
                Handling = new VehicleHandling(),                                   // the Phase 1 defaults
            },
            new Spec
            {
                Id = "hatch", Prefab = "Car_Hatch", Name = "Volt Hatch", Tagline = "Short, light and nimble. Squeezes in anywhere.",
                Paint = "Paint_ElectricBlue", PaintColor = new Color(0.10f, 0.42f, 0.95f), Price = 750,
                Length = 3.7f, Width = 1.7f, ChassisY = 0.64f, ChassisH = 0.48f, CabinLength = 2.0f, CabinH = 0.52f, CabinZ = -0.2f,
                WheelRadius = 0.31f, WheelWidth = 0.2f, HalfTrack = 0.72f, HalfWheelbase = 1.2f,
                Handling = new VehicleHandling
                {
                    driveType = VehicleController.DriveType.FrontWheelDrive, mass = 1050f, centerOfMass = new Vector3(0f, 0.28f, 0.05f),
                    motorTorque = 1450f, reverseTorque = 1000f, maxForwardSpeedKmh = 55f, maxReverseSpeedKmh = 20f,
                    brakeTorque = 2200f, handbrakeTorque = 3500f, maxSteerAngle = 38f, highSpeedSteerAngle = 14f, steerSpeed = 130f,
                },
            },
            new Spec
            {
                Id = "sport", Prefab = "Car_Sport", Name = "Apex GT", Tagline = "Rear-drive coupe. Quick, sharp brakes, wide nose.",
                Paint = "Paint_Lime", PaintColor = new Color(0.62f, 0.92f, 0.10f), Price = 2000,
                Length = 4.4f, Width = 1.85f, ChassisY = 0.6f, ChassisH = 0.42f, CabinLength = 1.8f, CabinH = 0.4f, CabinZ = -0.35f,
                WheelRadius = 0.34f, WheelWidth = 0.25f, HalfTrack = 0.8f, HalfWheelbase = 1.4f, Extra = Extra.Spoiler,
                Handling = new VehicleHandling
                {
                    driveType = VehicleController.DriveType.RearWheelDrive, mass = 1250f, centerOfMass = new Vector3(0f, 0.25f, 0f),
                    motorTorque = 2500f, reverseTorque = 1200f, maxForwardSpeedKmh = 75f, maxReverseSpeedKmh = 22f,
                    brakeTorque = 3300f, handbrakeTorque = 4500f, maxSteerAngle = 32f, highSpeedSteerAngle = 10f, steerSpeed = 120f,
                },
            },
            new Spec
            {
                Id = "suv", Prefab = "Car_SUV", Name = "Ridge SUV", Tagline = "All-wheel drive and tall. Wide, so aim carefully.",
                Paint = "Paint_Graphite", PaintColor = new Color(0.20f, 0.22f, 0.25f), Price = 3000,
                Length = 4.6f, Width = 1.95f, ChassisY = 0.95f, ChassisH = 0.6f, CabinLength = 2.6f, CabinH = 0.55f, CabinZ = -0.35f,
                WheelRadius = 0.4f, WheelWidth = 0.26f, HalfTrack = 0.85f, HalfWheelbase = 1.45f, Extra = Extra.RoofRails,
                Handling = new VehicleHandling
                {
                    driveType = VehicleController.DriveType.AllWheelDrive, mass = 1900f, centerOfMass = new Vector3(0f, 0.42f, 0f),
                    motorTorque = 3000f, reverseTorque = 2000f, maxForwardSpeedKmh = 58f, maxReverseSpeedKmh = 18f,
                    brakeTorque = 3800f, handbrakeTorque = 5000f, maxSteerAngle = 33f, highSpeedSteerAngle = 11f, steerSpeed = 100f,
                },
            },
            new Spec
            {
                Id = "pickup", Prefab = "Car_Pickup", Name = "Hauler", Tagline = "Long-bed pickup. Big turning circle, big reward.",
                Paint = "Paint_Arctic", PaintColor = new Color(0.90f, 0.91f, 0.92f), Price = 4500,
                Length = 4.9f, Width = 1.95f, ChassisY = 0.9f, ChassisH = 0.55f, CabinLength = 1.5f, CabinH = 0.6f, CabinZ = 0.55f,
                WheelRadius = 0.4f, WheelWidth = 0.26f, HalfTrack = 0.85f, HalfWheelbase = 1.6f, Extra = Extra.PickupBed,
                Handling = new VehicleHandling
                {
                    driveType = VehicleController.DriveType.RearWheelDrive, mass = 2000f, centerOfMass = new Vector3(0f, 0.42f, 0.2f),
                    motorTorque = 3000f, reverseTorque = 2000f, maxForwardSpeedKmh = 55f, maxReverseSpeedKmh = 18f,
                    brakeTorque = 3800f, handbrakeTorque = 5000f, maxSteerAngle = 32f, highSpeedSteerAngle = 10f, steerSpeed = 95f,
                },
            },
        };

        public static void BuildAll()
        {
            Directory.CreateDirectory(CarsDir);
            Spec[] specs = Specs;
            var datas = new CarData[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                Material paint = PaintMaterial(specs[i].Paint, specs[i].PaintColor);
                GameObject prefab = BuildPrefab(specs[i]);
                datas[i] = WriteCarData(specs[i], prefab, paint);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CarCatalog>();
                AssetDatabase.CreateAsset(catalog, CarCatalogPath);
            }
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("cars");
            list.arraySize = datas.Length;
            for (int i = 0; i < datas.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = datas[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bootstrap] {datas.Length} cars + catalog written");
        }

        private static Material PaintMaterial(string name, Color color)
        {
            string path = $"{ProjectBootstrapper.MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.78f);
            mat.SetFloat("_Metallic", 0.35f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject BuildPrefab(Spec s)
        {
            var root = new GameObject(s.Prefab) { tag = "Player" };
            var body = root.AddComponent<Rigidbody>();
            body.mass = s.Handling.mass;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            float hl = s.Length * 0.5f, hw = s.Width * 0.5f;
            float chassisTop = s.ChassisY + s.ChassisH * 0.5f;
            float cabinY = chassisTop + s.CabinH * 0.5f;
            float roofY = chassisTop + s.CabinH + 0.03f;

            Transform model = ProjectBootstrapper.Child(root.transform, "Model");
            var paintParts = new System.Collections.Generic.List<Renderer>();
            Renderer Paint(Transform t) { var r = t.GetComponent<Renderer>(); paintParts.Add(r); return r; }

            Paint(Part(model, "Chassis", new Vector3(0f, s.ChassisY, 0f), new Vector3(s.Width, s.ChassisH, s.Length), "CarPaint"));
            Part(model, "Cabin", new Vector3(0f, cabinY, s.CabinZ), new Vector3(s.Width - 0.2f, s.CabinH, s.CabinLength), "CarGlass");
            Paint(Part(model, "Roof", new Vector3(0f, roofY, s.CabinZ - 0.05f), new Vector3(s.Width - 0.18f, 0.06f, s.CabinLength - 0.3f), "CarPaint"));
            Part(model, "Underbody", new Vector3(0f, s.ChassisY - s.ChassisH * 0.5f - 0.04f, 0f), new Vector3(s.Width - 0.1f, 0.08f, s.Length - 0.3f), "CarTrim");
            float bumperY = s.ChassisY - s.ChassisH * 0.3f;
            Part(model, "BumperFront", new Vector3(0f, bumperY, hl + 0.02f), new Vector3(s.Width + 0.02f, 0.22f, 0.12f), "CarTrim");
            Part(model, "BumperRear", new Vector3(0f, bumperY, -hl - 0.02f), new Vector3(s.Width + 0.02f, 0.22f, 0.12f), "CarTrim");
            float lightY = s.ChassisY + s.ChassisH * 0.15f;
            Part(model, "HeadlightL", new Vector3(-hw + 0.28f, lightY, hl + 0.01f), new Vector3(0.36f, 0.12f, 0.04f), "Headlight");
            Part(model, "HeadlightR", new Vector3(hw - 0.28f, lightY, hl + 0.01f), new Vector3(0.36f, 0.12f, 0.04f), "Headlight");
            Part(model, "TaillightL", new Vector3(-hw + 0.24f, lightY, -hl - 0.01f), new Vector3(0.3f, 0.1f, 0.04f), "Taillight");
            Part(model, "TaillightR", new Vector3(hw - 0.24f, lightY, -hl - 0.01f), new Vector3(0.3f, 0.1f, 0.04f), "Taillight");
            Part(model, "Grille", new Vector3(0f, lightY - 0.02f, hl + 0.01f), new Vector3(s.Width * 0.38f, 0.14f, 0.03f), "CarTrim");

            switch (s.Extra)
            {
                case Extra.Spoiler:
                    Paint(Part(model, "Wing", new Vector3(0f, chassisTop + 0.24f, -hl + 0.2f), new Vector3(s.Width - 0.1f, 0.05f, 0.32f), "CarPaint"));
                    Part(model, "WingPostL", new Vector3(-hw + 0.35f, chassisTop + 0.11f, -hl + 0.22f), new Vector3(0.06f, 0.22f, 0.12f), "CarTrim");
                    Part(model, "WingPostR", new Vector3(hw - 0.35f, chassisTop + 0.11f, -hl + 0.22f), new Vector3(0.06f, 0.22f, 0.12f), "CarTrim");
                    break;
                case Extra.RoofRails:
                    Part(model, "RailL", new Vector3(-hw + 0.2f, roofY + 0.07f, s.CabinZ), new Vector3(0.05f, 0.06f, s.CabinLength - 0.2f), "CarTrim");
                    Part(model, "RailR", new Vector3(hw - 0.2f, roofY + 0.07f, s.CabinZ), new Vector3(0.05f, 0.06f, s.CabinLength - 0.2f), "CarTrim");
                    break;
                case Extra.PickupBed:
                    float bedStart = s.CabinZ - s.CabinLength * 0.5f - 0.05f, bedLen = bedStart + hl;
                    float bedZ = bedStart - bedLen * 0.5f, wallY = chassisTop + 0.2f;
                    Paint(Part(model, "BedWallL", new Vector3(-hw + 0.05f, wallY, bedZ), new Vector3(0.1f, 0.4f, bedLen), "CarPaint"));
                    Paint(Part(model, "BedWallR", new Vector3(hw - 0.05f, wallY, bedZ), new Vector3(0.1f, 0.4f, bedLen), "CarPaint"));
                    Paint(Part(model, "Tailgate", new Vector3(0f, wallY, -hl + 0.05f), new Vector3(s.Width, 0.4f, 0.1f), "CarPaint"));
                    Part(model, "BedFloor", new Vector3(0f, chassisTop + 0.01f, bedZ), new Vector3(s.Width - 0.2f, 0.02f, bedLen - 0.1f), "CarTrim");
                    break;
            }

            // Physics colliders: the body box defines the parking footprint (Width x Length+0.1).
            Transform colliders = ProjectBootstrapper.Child(root.transform, "Colliders");
            ProjectBootstrapper.AddBoxCollider(colliders, "BodyCollider", new Vector3(0f, s.ChassisY, 0f), new Vector3(s.Width, s.ChassisH, s.Length + 0.1f));
            ProjectBootstrapper.AddBoxCollider(colliders, "CabinCollider", new Vector3(0f, cabinY, s.CabinZ), new Vector3(s.Width - 0.2f, s.CabinH + 0.02f, s.CabinLength));

            Transform colliderGroup = ProjectBootstrapper.Child(root.transform, "WheelColliders");
            Transform meshGroup = ProjectBootstrapper.Child(root.transform, "WheelMeshes");
            string[] names = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
            Vector3[] pos =
            {
                new Vector3(-s.HalfTrack, 0f, s.HalfWheelbase), new Vector3(s.HalfTrack, 0f, s.HalfWheelbase),
                new Vector3(-s.HalfTrack, 0f, -s.HalfWheelbase), new Vector3(s.HalfTrack, 0f, -s.HalfWheelbase),
            };
            var wheelColliders = new WheelCollider[4];
            var pivots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                wheelColliders[i] = WheelColliderFor(colliderGroup, names[i], pos[i] + Vector3.up * (s.WheelRadius + 0.16f), s);
                pivots[i] = WheelMesh(meshGroup, names[i], pos[i] + Vector3.up * s.WheelRadius, s);
            }

            var input = root.AddComponent<VehicleInput>();
            root.AddComponent<CollisionHandler>();
            var controller = root.AddComponent<VehicleController>();
            var visuals = root.AddComponent<WheelVisuals>();
            root.AddComponent<UltimateCarParking.Audio.VehicleAudio>();   // after VehicleController (RequireComponent)
            var carPaint = root.AddComponent<CarPaint>();

            var cso = new SerializedObject(controller);
            cso.FindProperty("input").objectReferenceValue = input;
            cso.FindProperty("frontLeft").objectReferenceValue = wheelColliders[0];
            cso.FindProperty("frontRight").objectReferenceValue = wheelColliders[1];
            cso.FindProperty("rearLeft").objectReferenceValue = wheelColliders[2];
            cso.FindProperty("rearRight").objectReferenceValue = wheelColliders[3];
            cso.FindProperty("centerOfMass").vector3Value = s.Handling.centerOfMass;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var vso = new SerializedObject(visuals);
            vso.FindProperty("body").objectReferenceValue = body;
            SerializedProperty wheels = vso.FindProperty("wheels");
            wheels.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                wheels.GetArrayElementAtIndex(i).FindPropertyRelative("collider").objectReferenceValue = wheelColliders[i];
                wheels.GetArrayElementAtIndex(i).FindPropertyRelative("visual").objectReferenceValue = pivots[i];
            }
            vso.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(carPaint);
            SerializedProperty pr = pso.FindProperty("paintRenderers");
            pr.arraySize = paintParts.Count;
            for (int i = 0; i < paintParts.Count; i++) pr.GetArrayElementAtIndex(i).objectReferenceValue = paintParts[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabsDir}/{s.Prefab}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Part(Transform parent, string name, Vector3 pos, Vector3 size, string material) =>
            ProjectBootstrapper.Box(parent, name, pos, size, material, collider: false);

        private static WheelCollider WheelColliderFor(Transform parent, string name, Vector3 localPos, Spec s)
        {
            var go = new GameObject(name + "Collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.mass = 20f;
            wc.radius = s.WheelRadius;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = 0.22f;
            wc.forceAppPointDistance = 0.15f;
            // Spring/damper scale with mass so every car sits at the same static compression (~45 %).
            float spring = Mathf.Round(s.Handling.mass * 27f / 100f) * 100f;
            float damper = Mathf.Round(s.Handling.mass * 3.08f / 100f) * 100f;
            wc.suspensionSpring = new JointSpring { spring = spring, damper = damper, targetPosition = 0.5f };
            wc.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.5f };
            wc.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.25f, extremumValue = 1f, asymptoteSlip = 0.6f, asymptoteValue = 0.75f, stiffness = 1.8f };
            return wc;
        }

        private static Transform WheelMesh(Transform parent, string name, Vector3 localPos, Spec s)
        {
            Transform pivot = ProjectBootstrapper.Child(parent, name + "Mesh");
            pivot.localPosition = localPos;
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);
            float d = s.WheelRadius * 2f;
            ProjectBootstrapper.Primitive(PrimitiveType.Cylinder, pivot, "Tire", Vector3.zero, axle, new Vector3(d, s.WheelWidth * 0.5f, d), "Tire", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Cylinder, pivot, "Rim", Vector3.zero, axle, new Vector3(d * 0.62f, s.WheelWidth * 0.5f + 0.005f, d * 0.62f), "Rim", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Cube, pivot, "SpokeA", Vector3.zero, Quaternion.identity, new Vector3(s.WheelWidth + 0.02f, d * 0.6f, 0.07f), "CarTrim", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Cube, pivot, "SpokeB", Vector3.zero, Quaternion.identity, new Vector3(s.WheelWidth + 0.02f, 0.07f, d * 0.6f), "CarTrim", false);
            return pivot;
        }

        // ------------------------------------------------------------------ data

        private static CarData WriteCarData(Spec s, GameObject prefab, Material paint)
        {
            string path = $"{CarsDir}/Car_{char.ToUpperInvariant(s.Id[0])}{s.Id.Substring(1)}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CarData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CarData>();
                AssetDatabase.CreateAsset(data, path);
            }
            var so = new SerializedObject(data);
            so.FindProperty("carId").stringValue = s.Id;
            so.FindProperty("displayName").stringValue = s.Name;
            so.FindProperty("tagline").stringValue = s.Tagline;
            so.FindProperty("modelPrefab").objectReferenceValue = prefab;
            so.FindProperty("paintMaterial").objectReferenceValue = paint;
            so.FindProperty("price").intValue = s.Price;
            so.FindProperty("ownedByDefault").boolValue = s.Starter;
            SerializedProperty h = so.FindProperty("handling");
            VehicleHandling v = s.Handling;
            h.FindPropertyRelative("driveType").enumValueIndex = (int)v.driveType;
            h.FindPropertyRelative("mass").floatValue = v.mass;
            h.FindPropertyRelative("centerOfMass").vector3Value = v.centerOfMass;
            h.FindPropertyRelative("motorTorque").floatValue = v.motorTorque;
            h.FindPropertyRelative("reverseTorque").floatValue = v.reverseTorque;
            h.FindPropertyRelative("maxForwardSpeedKmh").floatValue = v.maxForwardSpeedKmh;
            h.FindPropertyRelative("maxReverseSpeedKmh").floatValue = v.maxReverseSpeedKmh;
            h.FindPropertyRelative("brakeTorque").floatValue = v.brakeTorque;
            h.FindPropertyRelative("handbrakeTorque").floatValue = v.handbrakeTorque;
            h.FindPropertyRelative("maxSteerAngle").floatValue = v.maxSteerAngle;
            h.FindPropertyRelative("highSpeedSteerAngle").floatValue = v.highSpeedSteerAngle;
            h.FindPropertyRelative("steerSpeed").floatValue = v.steerSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
