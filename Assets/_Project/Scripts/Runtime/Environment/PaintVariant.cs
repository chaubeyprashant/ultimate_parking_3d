using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>Recolours a module per placement (parked car colours, container colours) without new materials.</summary>
    [DisallowMultipleComponent]
    public sealed class PaintVariant : MonoBehaviour
    {
        [SerializeField] private Renderer[] targets = System.Array.Empty<Renderer>();
        [SerializeField] private Color[] palette = { Color.white };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock block;

        public int PaletteSize => palette.Length;

        public void Apply(int variant)
        {
            if (palette.Length == 0) return;
            Color c = palette[((variant % palette.Length) + palette.Length) % palette.Length];
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColorId, c);
            foreach (Renderer r in targets)
                if (r != null) r.SetPropertyBlock(block);
        }
    }
}
