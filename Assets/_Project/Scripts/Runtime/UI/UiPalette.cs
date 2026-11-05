using UnityEngine;

namespace UltimateCarParking.UI
{
    /// <summary>The dark automotive palette with the electric-lime accent. One place to restyle every screen.</summary>
    public static class UiPalette
    {
        public static readonly Color Background = Hex(0x0B0D10);
        public static readonly Color Surface = Hex(0x14181D);
        public static readonly Color SurfaceRaised = Hex(0x1C2128);
        public static readonly Color Outline = Hex(0x2A313A);
        public static readonly Color Lime = Hex(0xB6FF2E);
        public static readonly Color LimeDim = new Color(0.71f, 1f, 0.18f, 0.18f);
        public static readonly Color TextPrimary = Hex(0xF2F5F7);
        public static readonly Color TextSecondary = Hex(0x8C96A3);
        public static readonly Color TextOnLime = Hex(0x0B0D10);
        public static readonly Color Locked = Hex(0x3A414B);
        public static readonly Color Danger = Hex(0xFF4D5E);
        public static readonly Color Gold = Hex(0xFFC83D);

        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
