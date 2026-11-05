using UnityEngine;

namespace UltimateCarParking.Cars
{
    /// <summary>Lists a car model's body-paint renderers so CarData can repaint it.</summary>
    [DisallowMultipleComponent]
    public sealed class CarPaint : MonoBehaviour
    {
        [SerializeField] private Renderer[] paintRenderers = System.Array.Empty<Renderer>();

        public int RendererCount => paintRenderers.Length;

        public void Apply(Material paint)
        {
            if (paint == null) return;
            foreach (Renderer r in paintRenderers)
                if (r != null) r.sharedMaterial = paint;
        }

        public Material CurrentPaint => paintRenderers.Length > 0 && paintRenderers[0] != null ? paintRenderers[0].sharedMaterial : null;
    }
}
