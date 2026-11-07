using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Writes Resources/ParkedFleet so levels can spawn the imported cars instead of the block module.</summary>
    public static class ParkedFleetBuilder
    {
        public const string AssetPath = "Assets/_Project/Resources/ParkedFleet.asset";

        public static readonly string[] Names =
        {
            "Parked_sport", "Parked_coupe", "Parked_sedan", "Parked_hatchback", "Parked_suv",
            "Parked_pickup", "Parked_offroad", "Parked_wagon", "Parked_compact", "Parked_minivan",
        };

        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Project/Resources");
            var fleet = AssetDatabase.LoadAssetAtPath<ParkedFleet>(AssetPath);
            if (fleet == null)
            {
                fleet = ScriptableObject.CreateInstance<ParkedFleet>();
                AssetDatabase.CreateAsset(fleet, AssetPath);
            }
            GameObject[] cars = Names.Select(RealPropFactory.Load).Where(g => g != null).ToArray();
            var so = new SerializedObject(fleet);
            SerializedProperty list = so.FindProperty("cars");
            list.arraySize = cars.Length;
            for (int i = 0; i < cars.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fleet);
            Debug.Log($"[ParkedFleet] {cars.Length} cars");
        }
    }
}
