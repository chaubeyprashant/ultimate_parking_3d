using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Applies upload-keystore signing for release AAB/APK (env vars or Builds/signing.local.env).</summary>
    public static class AndroidReleaseSigning
    {
        public const string DefaultKeystoreRelative = "Builds/ultimate-parking-upload.keystore";
        public const string DefaultAlias = "upload";
        public const string LocalEnvRelative = "Builds/signing.local.env";

        /// <returns>True if a release keystore was applied; false keeps Unity default (debug) signing.</returns>
        public static bool TryApply()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            var env = LoadEnvFile(Path.Combine(projectRoot, LocalEnvRelative));
            string keystorePath = Get(env, "ANDROID_KEYSTORE_PATH")
                ?? Path.Combine(projectRoot, DefaultKeystoreRelative);
            if (!File.Exists(keystorePath))
            {
                Debug.LogWarning("[Build] Release keystore not found at " + keystorePath +
                                 ". Run Tools/create_upload_keystore.sh — output will be debug-signed.");
                PlayerSettings.Android.useCustomKeystore = false;
                return false;
            }

            string alias = Get(env, "ANDROID_KEY_ALIAS") ?? DefaultAlias;
            string storePass = Get(env, "ANDROID_KEYSTORE_PASS") ?? System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string keyPass = Get(env, "ANDROID_KEY_PASS") ?? System.Environment.GetEnvironmentVariable("ANDROID_KEY_PASS") ?? storePass;
            if (string.IsNullOrEmpty(storePass))
            {
                Debug.LogError("[Build] Keystore found but no password. Set ANDROID_KEYSTORE_PASS or Builds/signing.local.env");
                return false;
            }

            string keystoreForUnity = ToUnityKeystorePath(projectRoot, keystorePath);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystoreForUnity;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log("[Build] Release signing: " + keystoreForUnity + " alias " + alias);
            return true;
        }

        private static string ToUnityKeystorePath(string projectRoot, string path)
        {
            if (!Path.IsPathRooted(path)) return path.Replace('\\', '/');
            string rel = Path.GetRelativePath(projectRoot, path);
            return rel.Replace('\\', '/');
        }

        private static Dictionary<string, string> LoadEnvFile(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return map;
            foreach (string line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                map[t[..eq].Trim()] = t[(eq + 1)..].Trim();
            }
            return map;
        }

        private static string Get(Dictionary<string, string> env, string key) =>
            env.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v) ? v : null;
    }
}
