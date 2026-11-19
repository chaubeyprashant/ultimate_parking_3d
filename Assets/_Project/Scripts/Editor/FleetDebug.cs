using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    public static class FleetDebug
    {
        /// <summary>Re-runs only the bootstrap verification (no regeneration), then exits.</summary>
        public static void VerifyOnly()
        {
            bool ok = ProjectBootstrapper.Verify();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void Dump()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Vehicles/Car_Coupe.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                Debug.Log($"[FleetDebug] {r.transform.parent?.name}/{r.name} centre {r.bounds.center} size {r.bounds.size}");
            Object.DestroyImmediate(go);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
