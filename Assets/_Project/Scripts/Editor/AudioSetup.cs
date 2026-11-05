using System.IO;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Audio;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Import settings for the synthesised clips (Tools/audio/gen_audio.py) and the Resources/AudioLibrary asset.
    /// Mobile-friendly: mono, Vorbis; short effects decompress on load, the music loop streams.
    /// </summary>
    public static class AudioSetup
    {
        public const string AudioDir = "Assets/_Project/Audio";
        public const string LibraryPath = "Assets/_Project/Resources/AudioLibrary.asset";

        public static void BuildAll()
        {
            Directory.CreateDirectory("Assets/_Project/Resources");
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = path.Contains("music");
                importer.forceToMono = true;
                importer.loadInBackground = music;
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.55f : 0.7f;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
            }

            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            lib.engineLoop = Clip("engine_loop");
            lib.brakeSqueal = Clip("brake_squeal");
            lib.collision = Clip("collision");
            lib.collisionWarning = Clip("collision_warn");
            lib.buttonClick = Clip("ui_click");
            lib.rewardStar = Clip("reward_star");
            lib.rewardCoin = Clip("reward_coin");
            lib.levelSuccess = Clip("level_success");
            lib.levelFail = Clip("level_fail");
            lib.musicLoop = Clip("music_loop");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] Audio library written");
        }

        public static bool LibraryComplete()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            return lib != null && lib.engineLoop && lib.brakeSqueal && lib.collision && lib.collisionWarning && lib.buttonClick
                   && lib.rewardStar && lib.rewardCoin && lib.levelSuccess && lib.levelFail && lib.musicLoop;
        }
    }
}
