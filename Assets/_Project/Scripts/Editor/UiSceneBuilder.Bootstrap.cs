using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.UI;

namespace UltimateCarParking.EditorTools
{
    public static partial class UiSceneBuilder
    {
        /// <summary>Full-screen branded splash wired to <see cref="Core.BootstrapLoader"/>.</summary>
        public static CanvasGroup BuildBootstrapSplash()
        {
            RectTransform root = MakeCanvas("SplashCanvas", out Canvas canvas);
            canvas.sortingOrder = 100;
            Image bg = Img(root, "Background", null, UiPalette.Background);
            Fill(bg.rectTransform, 0, 0, 0, 0);

            RectTransform block = Group(root, "TitleBlock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 320f));
            Label(block, "Kicker", "ULTIMATE", false, 28f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 14f)
                .rectTransform.Also(r => Fill(r, 0, 0, 200, 0));
            Label(block, "Title", "CAR PARKING<size=58%><color=#9AA3AD>  3D</color></size>", true, 96f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 1f)
                .rectTransform.Also(r => Fill(r, 0, 0, 80, 0));
            Image bar = Img(block, "AccentBar", "RoundRect", UiPalette.TextPrimary.WithAlpha(0.85f));
            Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(72f, 4f));
            Label(block, "Tagline", "PARKING SIMULATOR", false, 22f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 8f);
            Place(block.Find("Tagline").GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(520f, 32f));

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            return group;
        }
    }
}
