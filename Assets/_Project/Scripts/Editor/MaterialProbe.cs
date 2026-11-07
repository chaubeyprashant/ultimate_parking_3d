using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Lists shader and texture/colour properties of the materials inside arena models (diagnostics).</summary>
    public static class MaterialProbe
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (string p in new[] { "Mumbai/mum_road_signs.glb" })
            {
                string path = ArenaPropFactory.SourceRoot + "/" + p;
                foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Take(3))
                {
                    sb.AppendLine($"{p} :: {m.name} :: shader '{m.shader.name}' queue {m.renderQueue} keywords [{string.Join(",", m.shaderKeywords)}]");
                    Shader s = m.shader;
                    for (int i = 0; i < s.GetPropertyCount(); i++)
                    {
                        string n = s.GetPropertyName(i);
                        var t = s.GetPropertyType(i);
                        if (t == UnityEngine.Rendering.ShaderPropertyType.Texture) { var tx = m.GetTexture(n); if (tx != null) sb.AppendLine($"   tex {n} = {tx.name} ({tx.width}x{tx.height})"); }
                        else if (t == UnityEngine.Rendering.ShaderPropertyType.Color) sb.AppendLine($"   col {n} = {m.GetColor(n)}");
                        else if (n.ToLower().Contains("rough") || n.ToLower().Contains("metal") || n.ToLower().Contains("cutoff") || n.ToLower().Contains("cull")) sb.AppendLine($"   num {n} = {m.GetFloat(n)}");
                    }
                }
            }
            System.IO.File.WriteAllText("../UltimateCarParking3D_logs/material_probe.txt", sb.ToString());
        }
    }
}
