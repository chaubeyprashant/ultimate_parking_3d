using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Android;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.Android;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Country arenas are delivered as Addressables, one group (= one Android asset pack) per country, so they are
    /// not part of the base app. Sets up Addressables + Addressables for Android (Play Asset Delivery), and registers
    /// each mission's baked environment prefab under "arena/&lt;country&gt;/&lt;levelId&gt;".
    /// </summary>
    public static class ArenaDelivery
    {
        public const string GroupPrefix = "Arena_";
        public static string Address(string countryId, string levelId) => $"arena/{countryId}/{levelId}";

        public static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.GetSettings(true);

        /// <summary>Creates Addressables settings and enables Play Asset Delivery (idempotent).</summary>
        public static void Configure()
        {
            AddressableAssetSettings settings = Settings;
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(settings));
            if (settings.InitializationObjects.FindIndex(i => i is PlayAssetDeliveryInitializationSettings) == -1)
                settings.AddInitializationObject(Create<PlayAssetDeliveryInitializationSettings>($"{dir}/InitObjects/PlayAssetDeliveryInitializationSettings.asset"));
            int builder = settings.DataBuilders.FindIndex(b => b is BuildScriptPlayAssetDelivery);
            if (builder == -1)
            {
                settings.AddDataBuilder(Create<BuildScriptPlayAssetDelivery>($"{dir}/DataBuilders/BuildScriptPlayAssetDelivery.asset"));
                builder = settings.DataBuilders.FindIndex(b => b is BuildScriptPlayAssetDelivery);
            }
            settings.ActivePlayerDataBuilderIndex = builder;
            // Play mode in the editor (tests): load straight from the AssetDatabase.
            int fast = settings.DataBuilders.FindIndex(b => b is BuildScriptFastMode);
            if (fast >= 0) settings.ActivePlayModeDataBuilderIndex = fast;
            settings.BuildRemoteCatalog = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static T Create<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var obj = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        /// <summary>Puts a country's mission environments in its own install-time asset pack group.</summary>
        public static void RegisterCountry(string countryId, IEnumerable<(string levelId, string prefabPath)> missions,
            DeliveryType? deliveryOverride = null)
        {
            // India is the first stop and stays in the install; every other country downloads on demand.
            DeliveryType delivery = deliveryOverride ?? (countryId == FirstCountry ? DeliveryType.InstallTime : DeliveryType.OnDemand);
            RegisterCountryCore(countryId, missions, delivery);
        }

        public const string FirstCountry = "in";

        private static void RegisterCountryCore(string countryId, IEnumerable<(string levelId, string prefabPath)> missions, DeliveryType delivery)
        {
            Configure();
            AddressableAssetSettings settings = Settings;
            string name = GroupPrefix + countryId;
            AddressableAssetGroup group = settings.FindGroup(name) ??
                settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema), typeof(PlayAssetDeliverySchema));
            if (!group.HasSchema<PlayAssetDeliverySchema>()) group.AddSchema<PlayAssetDeliverySchema>();
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            // One named asset pack per country ("arena_in"), so its content leaves the base module.
            string pack = "arena_" + countryId;
            CustomAssetPackSettings packs = CustomAssetPackSettings.GetSettings(true);
            if (packs.CustomAssetPacks.All(p => p.AssetPackName != pack)) packs.AddAssetPack(pack, delivery);
            else packs.SetDeliveryType(pack, delivery);
            EditorUtility.SetDirty(packs);
            var pad = group.GetSchema<PlayAssetDeliverySchema>();
            pad.IncludeInCustomAssetPack = true;
            pad.CustomAssetPackName = pack;
            foreach (var (levelId, prefabPath) in missions)
            {
                string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                if (string.IsNullOrEmpty(guid)) { Debug.LogError("[Delivery] missing " + prefabPath); continue; }
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = Address(countryId, levelId);
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Delivery] {name}: {group.entries.Count} arena prefabs ({delivery})");
        }

        /// <summary>Editor lookup: the prefab registered under an address (previews, tests).</summary>
        public static GameObject LoadInEditor(string address)
        {
            AddressableAssetEntry e = Settings.groups.Where(g => g != null).SelectMany(g => g.entries).FirstOrDefault(x => x.address == address);
            return e != null ? AssetDatabase.LoadAssetAtPath<GameObject>(e.AssetPath) : null;
        }

        /// <summary>Builds Addressables content (asset packs) for the active build target.</summary>
        public static bool BuildContent()
        {
            Configure();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) { Debug.LogError("[Delivery] Addressables build failed: " + result.Error); return false; }
            Debug.Log($"[Delivery] Addressables content built in {result.Duration:F1}s");
            return true;
        }
    }
}
