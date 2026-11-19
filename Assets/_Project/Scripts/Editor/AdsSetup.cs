using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Ads;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Fills the Google Mobile Ads settings asset: the AdMob app id (the test id until real ids exist in AdConfig; the SDK
    /// refuses to start without one) and keeps the plugin from adding Unity's own External Dependency Manager next to the
    /// Google one Firebase already uses. The settings class is internal to the package, so it is edited through reflection.
    /// </summary>
    public static class AdsSetup
    {
        public static void Configure()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings", false)).FirstOrDefault(t => t != null);
            if (type == null) { Debug.LogWarning("[Ads] Google Mobile Ads editor settings not found; ads not configured."); return; }
            var instance = type.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(null, null) as ScriptableObject;
            if (instance == null) { Debug.LogWarning("[Ads] Could not load the Google Mobile Ads settings asset."); return; }
            var so = new SerializedObject(instance);
            Set(so, "adMobAndroidAppId", AdConfig.AppId);
            Set(so, "enableExternalDependencyManager", false);
            Set(so, "edmPackageDetected", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ads] Configured AdMob app id ({(AdConfig.UsingRealIds ? "real" : "TEST")}).");
        }

        private static void Set(SerializedObject so, string name, object value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[Ads] Missing settings field " + name); return; }
            if (value is string s) p.stringValue = s;
            else if (value is bool b) p.boolValue = b;
        }
    }
}
