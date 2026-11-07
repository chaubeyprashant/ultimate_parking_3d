using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Mobile pass over a baked arena prefab:
    /// - shadows: only objects taller than <see cref="ShadowMinHeight"/> and larger than <see cref="ShadowMinSize"/> cast
    ///   (buildings, buses, parked cars); ground tiles, road markings, signs, cones and small clutter do not;
    /// - LOD culling: every prop gets a LODGroup that culls it when it covers less than a few % of the screen
    ///   (small props earlier than buildings). There are no simplified meshes (the imported models have one LOD), so
    ///   this is distance culling, not mesh LOD;
    /// - colliders: mesh colliders on props are reported (none expected: the prop factory uses boxes).
    /// Also writes a size report per arena prefab for the Android performance report.
    /// </summary>
    public static class ArenaOptimizer
    {
        public const float ShadowMinHeight = 1.2f, ShadowMinSize = 2.5f;
        public const float CullSmall = 0.025f, CullLarge = 0.006f;

        public struct Stats
        {
            public int renderers, shadowCasters, triangles, materials, lodGroups, meshColliders, boxColliders;
            public override string ToString() =>
                $"{renderers} renderers, {shadowCasters} shadow casters, {triangles:N0} tris, {materials} materials, {lodGroups} LOD groups, {boxColliders} box / {meshColliders} mesh colliders";
        }

        public static Stats Measure(GameObject root)
        {
            var s = new Stats();
            var mats = new HashSet<Material>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                s.renderers++;
                if (r.shadowCastingMode != ShadowCastingMode.Off) s.shadowCasters++;
                foreach (Material m in r.sharedMaterials) if (m != null) mats.Add(m);
                if (r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null) s.triangles += Triangles(f.sharedMesh);
                else if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) s.triangles += Triangles(sk.sharedMesh);
            }
            s.materials = mats.Count;
            s.lodGroups = root.GetComponentsInChildren<LODGroup>(true).Length;
            s.meshColliders = root.GetComponentsInChildren<MeshCollider>(true).Length;
            s.boxColliders = root.GetComponentsInChildren<BoxCollider>(true).Length;
            return s;
        }

        private static int Triangles(Mesh m)
        {
            int t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)m.GetIndexCount(i) / 3;
            return t;
        }

        /// <summary>Applies the shadow and LOD rules to an arena instance (before it is saved as a prefab).</summary>
        public static void Optimize(GameObject root)
        {
            foreach (Transform unit in Units(root))
            {
                Renderer[] rs = unit.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                bool casts = b.size.y >= ShadowMinHeight && Mathf.Max(b.size.x, b.size.z) >= ShadowMinSize;
                foreach (Renderer r in rs)
                {
                    if (!casts) r.shadowCastingMode = ShadowCastingMode.Off;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                bool ground = b.size.y < 0.4f;   // road tiles, ground planes: always visible, never culled
                if (ground || unit.GetComponent<LODGroup>() != null) continue;
                var lod = unit.gameObject.AddComponent<LODGroup>();
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                lod.SetLODs(new[] { new LOD(size < 4f ? CullSmall : CullLarge, rs) });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
            }
        }

        /// <summary>The placed props / modules: children of "Props", and the arena prefab's own top-level parts.</summary>
        private static IEnumerable<Transform> Units(GameObject root)
        {
            var units = new List<Transform>();
            foreach (Transform child in root.transform)
            {
                bool container = child.name == "Props" || child.GetComponent<Renderer>() == null && child.childCount > 0 &&
                    child.GetComponentsInChildren<Renderer>(true).Length > 0 && child.Cast<Transform>().Count(c => c.GetComponentInChildren<Renderer>(true) != null) > 1;
                if (container) foreach (Transform c in child) CollectUnits(c, units, 0);
                else units.Add(child);
            }
            return units;
        }

        // An arena env prefab nests groups (Buildings/, Roads/, ...); descend through renderer-less groups.
        private static void CollectUnits(Transform t, List<Transform> units, int depth)
        {
            bool group = t.GetComponent<Renderer>() == null && depth < 3 &&
                         t.Cast<Transform>().Count(c => c.GetComponentInChildren<Renderer>(true) != null) > 1 &&
                         !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);
            if (group) foreach (Transform c in t) CollectUnits(c, units, depth + 1);
            else units.Add(t);
        }

        /// <summary>Report for every baked mission prefab: before/after numbers are logged by the bake.</summary>
        public static void Report()
        {
            var sb = new StringBuilder("| Prefab | Renderers | Shadow casters | Triangles | Materials | LOD groups | Box / mesh colliders |\n|---|---|---|---|---|---|---|\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab Env_", new[] { ArenaPropFactory.PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Missions/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Stats s = Measure(go);
                sb.AppendLine($"| {Path.GetFileNameWithoutExtension(path)} | {s.renderers} | {s.shadowCasters} | {s.triangles:N0} | {s.materials} | {s.lodGroups} | {s.boxColliders} / {s.meshColliders} |");
            }
            // Heaviest source meshes across the mission prefabs (what to decimate / swap first).
            var heavy = new Dictionary<string, (int tris, int count)>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab Env_", new[] { ArenaPropFactory.PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Missions/")) continue;
                foreach (MeshFilter f in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null) continue;
                    string key = Path.GetFileName(AssetDatabase.GetAssetPath(f.sharedMesh)) + ":" + f.sharedMesh.name;
                    heavy.TryGetValue(key, out var v);
                    heavy[key] = (Triangles(f.sharedMesh), v.count + 1);
                }
            }
            sb.AppendLine("\n| Source mesh | Tris each | Instances (all missions) | Total tris |\n|---|---|---|---|");
            foreach (var kv in heavy.OrderByDescending(k => (long)k.Value.tris * k.Value.count).Take(15))
                sb.AppendLine($"| {kv.Key} | {kv.Value.tris:N0} | {kv.Value.count} | {(long)kv.Value.tris * kv.Value.count:N0} |");
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/arena_perf.md"));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[Perf] arena report\n" + sb);
        }
    }
}
