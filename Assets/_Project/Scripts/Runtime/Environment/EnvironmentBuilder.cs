using System.Collections.Generic;
using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>
    /// Assembles a level world at runtime from modules: theme ground and perimeter walls, theme decoration,
    /// then the level's own props. Applies the theme lighting to the scene's sun (RenderSettings.sun).
    /// </summary>
    public static class EnvironmentBuilder
    {
        public static GameObject Build(EnvironmentTheme theme, IReadOnlyList<PropPlacement> levelProps, string rootName = "Environment")
        {
            var root = new GameObject(rootName);
            if (theme == null)
            {
                Debug.LogError("[EnvironmentBuilder] No theme; building props only.");
            }
            else
            {
                BuildShell(theme, root.transform);
                Place(theme.Decoration, Child(root.transform, "Decoration"));
                ApplyLighting(theme);
            }
            Place(levelProps, Child(root.transform, "Props"));
            return root;
        }

        public static void ApplyLighting(EnvironmentTheme theme)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
            RenderSettings.fog = theme.Fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = theme.FogColor;
            RenderSettings.fogDensity = theme.FogDensity;
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = theme.SunColor;
                sun.intensity = theme.SunIntensity;
                sun.transform.rotation = theme.SunRotation;
            }
        }

        private static void BuildShell(EnvironmentTheme theme, Transform root)
        {
            Vector2 size = theme.LotSize;
            Plane(root, "OuterGround", new Vector3(0f, -0.02f, 0f), new Vector2(size.x + 240f, size.y + 240f), theme.OuterGroundMaterial);
            Plane(root, "LotGround", Vector3.zero, size, theme.GroundMaterial);

            if (theme.PerimeterWallPrefab == null) return;
            Transform walls = Child(root, "Perimeter");
            float seg = theme.WallSegmentLength;
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / seg)), nz = Mathf.Max(1, Mathf.RoundToInt(size.y / seg));
            float sx = size.x / nx, sz = size.y / nz;
            for (int i = 0; i < nx; i++)
            {
                float x = -hx + sx * (i + 0.5f);
                Spawn(theme.PerimeterWallPrefab, walls, new Vector3(x, 0f, hz), 0f, new Vector3(sx / seg, 1f, 1f), 0);
                Spawn(theme.PerimeterWallPrefab, walls, new Vector3(x, 0f, -hz), 180f, new Vector3(sx / seg, 1f, 1f), 0);
            }
            for (int i = 0; i < nz; i++)
            {
                float z = -hz + sz * (i + 0.5f);
                Spawn(theme.PerimeterWallPrefab, walls, new Vector3(hx, 0f, z), 90f, new Vector3(sz / seg, 1f, 1f), 0);
                Spawn(theme.PerimeterWallPrefab, walls, new Vector3(-hx, 0f, z), -90f, new Vector3(sz / seg, 1f, 1f), 0);
            }
        }

        /// <summary>Places a level's props under a new child of <paramref name="root"/> (prefab-based levels).</summary>
        public static void PlaceProps(IReadOnlyList<PropPlacement> props, Transform root, string groupName)
        {
            if (props == null || props.Count == 0) return;
            Place(props, Child(root, groupName));
        }

        private static void Place(IReadOnlyList<PropPlacement> props, Transform parent)
        {
            if (props == null) return;
            int missing = 0;
            foreach (PropPlacement p in props)
            {
                if (p.prefab == null) { missing++; continue; }
                Spawn(p.prefab, parent, p.position, p.yaw, p.EffectiveScale, p.variant);
            }
            if (missing > 0) Debug.LogWarning($"[EnvironmentBuilder] {missing} placement(s) under '{parent.name}' have no prefab and were skipped.");
        }

        private static ParkedFleet parkedFleet;

        private static void Spawn(GameObject prefab, Transform parent, Vector3 pos, float yaw, Vector3 scale, int variant)
        {
            prefab = ResolveParked(prefab, pos, variant);
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);
            if (go.TryGetComponent(out PaintVariant paint)) paint.Apply(variant);
        }

        /// <summary>Level data still points at the block module. Swap it for a real body, varied by bay.</summary>
        private static GameObject ResolveParked(GameObject prefab, Vector3 pos, int variant)
        {
            if (prefab == null || prefab.name != "Mod_ParkedCar") return prefab;
            parkedFleet ??= Resources.Load<ParkedFleet>("ParkedFleet");
            if (parkedFleet == null || parkedFleet.Count == 0) return prefab;
            int index = variant * 13 + Mathf.RoundToInt(pos.x * 4f) + Mathf.RoundToInt(pos.z);
            GameObject real = parkedFleet.Pick(index);
            return real != null ? real : prefab;
        }

        /// <summary>
        /// Ground slab: a thin box whose top sits at <paramref name="pos"/>.y. A box (BoxCollider) rather than a Plane
        /// primitive: Plane needs MeshCollider, which IL2CPP engine stripping removes from device builds when no
        /// shipped asset references it — and a box is cheaper for physics anyway.
        /// </summary>
        private static void Plane(Transform parent, string name, Vector3 pos, Vector2 size, Material mat)
        {
            const float thickness = 0.2f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos - Vector3.up * (thickness * 0.5f);
            go.transform.localScale = new Vector3(size.x, thickness, size.y);
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
