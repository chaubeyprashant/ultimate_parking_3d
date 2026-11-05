using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Android builds (debug-signed APK; release signing is set up in a later phase).
    /// Batch: -executeMethod UltimateCarParking.EditorTools.BuildScript.BuildAndroid
    /// </summary>
    public static class BuildScript
    {
        public const string ApkPath = "Builds/UltimateCarParking3D.apk";

        [MenuItem("Tools/Ultimate Car Parking/Build Android APK")]
        public static void BuildAndroid() => Build(ApkPath, BuildOptions.None);

        [MenuItem("Tools/Ultimate Car Parking/Build Android APK (Development)")]
        public static void BuildAndroidDevelopment() => Build("Builds/UltimateCarParking3D-dev.apk", BuildOptions.Development);

        private static void Build(string output, BuildOptions options)
        {
            ProjectBootstrapper.ConfigurePlayer();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.Android.useCustomKeystore = false;

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            BuildReport report = BuildPipeline.BuildPlayer(ProjectBootstrapper.ShippedScenes, output, BuildTarget.Android, options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[Build] Android {(ok ? "SUCCEEDED" : "FAILED")}: {report.summary.totalErrors} errors, " +
                      $"{report.summary.totalSize / (1024f * 1024f):F1} MB, {report.summary.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
