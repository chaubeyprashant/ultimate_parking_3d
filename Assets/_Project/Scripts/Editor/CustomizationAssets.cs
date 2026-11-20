using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Shared materials for Phase 7 (in Resources so their shaders ship with the build): the additive lamp glow used by
    /// VehicleLights (one material for every lamp; colours by property block) and the white plate backing.
    /// </summary>
    public static class CustomizationAssets
    {
        public const string Dir = "Assets/_Project/Resources/Customization";

        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Dir);
            // Lamp glow: URP Particles/Unlit, additive, soft round texture.
            Material glow = Load("LightGlow", "Universal Render Pipeline/Particles/Unlit");
            glow.SetFloat("_Surface", 1f);                 // transparent
            glow.SetFloat("_Blend", 2f);                   // additive
            glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            glow.SetFloat("_ZWrite", 0f);
            glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glow.EnableKeyword("_BLENDMODE_ADD");
            glow.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiAssets.SpritesDir + "/Glow.png");
            if (tex != null) glow.SetTexture("_BaseMap", tex);
            glow.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(glow);

            Material plate = Load("PlateBack", "Universal Render Pipeline/Unlit");
            plate.SetColor("_BaseColor", new Color(0.94f, 0.94f, 0.9f));
            EditorUtility.SetDirty(plate);
            AssetDatabase.SaveAssets();
        }

        private static Material Load(string name, string shader)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else m.shader = Shader.Find(shader);
            return m;
        }
    }
}
