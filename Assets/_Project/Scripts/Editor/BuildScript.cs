using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Android builds. Release AAB uses Builds/ultimate-parking-upload.keystore when present (see Tools/create_upload_keystore.sh).
    /// BuildAndroid = Android App Bundle (.aab) with country arenas as Play Asset Delivery asset packs (Addressables
    /// content is built first). BuildAndroidApk = single APK for quick device tests (arenas inside the APK).
    /// Batch: -executeMethod UltimateCarParking.EditorTools.BuildScript.BuildAndroid
    /// </summary>
    public static class BuildScript
    {
        public const string ApkPath = "Builds/UltimateCarParking3D.apk";
        public const string AabPath = "Builds/UltimateCarParking3D.aab";

        [MenuItem("Tools/Ultimate Car Parking/Build Android App Bundle (asset packs)")]
        public static void BuildAndroid() => Build(AabPath, BuildOptions.None, appBundle: true);

        [MenuItem("Tools/Ultimate Car Parking/Build Android APK")]
        public static void BuildAndroidApk() => Build(ApkPath, BuildOptions.None, appBundle: false);

        [MenuItem("Tools/Ultimate Car Parking/Build Android APK (Development)")]
        public static void BuildAndroidDevelopment() => Build("Builds/UltimateCarParking3D-dev.apk", BuildOptions.Development, appBundle: false);

        private static void Build(string output, BuildOptions options, bool appBundle)
        {
            ProjectBootstrapper.ConfigurePlayer();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            // Addressables for Android only moves groups into asset packs when the binary is split (AAB).
            PlayerSettings.Android.splitApplicationBinary = appBundle;
            PlayerSettings.stripUnusedMeshComponents = true;     // size budget: drop unused vertex channels
            ResolveAndroidDependencies();
            if (!ArenaDelivery.BuildContent())
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            AndroidReleaseSigning.TryApply();

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            string[] scenes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Where(EditorBuildSettings.scenes, sc => sc.enabled), sc => sc.path));
            BuildReport report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[Build] Android {(ok ? "SUCCEEDED" : "FAILED")}: {report.summary.totalErrors} errors, " +
                      $"{report.summary.totalSize / (1024f * 1024f):F1} MB, {report.summary.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Runs the External Dependency Manager's Android resolver (Firebase + Credential Manager) so the Gradle
        /// templates carry every dependency; batch builds don't trigger auto-resolution.
        /// </summary>
        public static void ResolveAndroidDependencies()
        {
            System.Type resolver = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("GooglePlayServices.PlayServicesResolver", false)).FirstOrDefault(t => t != null);
            var method = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
            if (method == null) { Debug.LogWarning("[Build] External Dependency Manager not found; Android dependencies not resolved."); return; }
            bool ok = (bool)method.Invoke(null, new object[] { true });
            Debug.Log("[Build] Android dependencies resolved: " + ok);
        }
    }
}
