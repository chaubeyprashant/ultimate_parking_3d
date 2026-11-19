using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UltimateCarParking.Account;
using UltimateCarParking.Ads;
using UltimateCarParking.Core;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Sign-in shown after Sign out, in the same layout as the title screen: the showroom car stays on the right,
    /// and the account actions sit on the dark left panel. Kept across scenes.
    /// </summary>
    public sealed class SignInGate : MonoBehaviour
    {
        private static SignInGate instance;

        private GameObject root;
        private GameObject menuCanvas;
        private Button googleButton;
        private Button guestButton;
        private Image googleImage;
        private Image guestImage;
        private TMP_Text googleLabel;
        private TMP_Text message;
        private Sprite pill;
        private Sprite pillOutline;

        public bool IsShowing => root != null && root.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("SignInGate");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SignInGate>();
        }

        private void Awake() => Build();

        private void OnEnable()
        {
            AuthService.Changed += OnAuthChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnAuthChanged();
        }

        private void OnDisable()
        {
            AuthService.Changed -= OnAuthChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (IsShowing) AdService.HideBanner();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsShowing) return;
            AdoptTheme();
            HideMenuChrome();
        }

        private void OnAuthChanged()
        {
            if (AuthService.State != AuthState.SignedOut)
            {
                if (root != null) root.SetActive(false);
                RestoreMenuChrome();
                return;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            root.SetActive(true);
            AdoptTheme();
            HideMenuChrome();
            bool busy = AuthService.Busy;
            if (googleButton != null) googleButton.interactable = !busy && AuthService.GoogleSupported;
            if (guestButton != null) guestButton.interactable = !busy;
            if (googleLabel != null) googleLabel.text = busy ? "PLEASE WAIT…" : "SIGN IN WITH GOOGLE";
            if (message != null) message.text = string.IsNullOrEmpty(AuthService.LastError)
                ? "Sign in to keep this player, or continue as a guest."
                : AuthService.LastError;
            if (SceneManager.GetActiveScene().name != SceneNames.MainMenu && !ScreenTransition.IsTransitioning)
                ScreenTransition.LoadScene(SceneNames.MainMenu);
        }

        private void HideMenuChrome()
        {
            var canvas = GameObject.Find("MainMenuCanvas");
            if (canvas == null) return;
            menuCanvas = canvas;
            menuCanvas.SetActive(false);
        }

        private void RestoreMenuChrome()
        {
            if (menuCanvas == null) return;
            menuCanvas.SetActive(true);
            menuCanvas = null;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            root = new GameObject("SignInScreen", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            Stretch((RectTransform)root.transform);
            var blocker = root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;

            var fade = NewImage("SideFade", root.transform, new Color(0.027f, 0.029f, 0.033f, 1f));
            fade.sprite = SideFadeSprite();
            fade.type = Image.Type.Simple;
            var fadeRect = fade.rectTransform;
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = new Vector2(0f, 1f);
            fadeRect.pivot = new Vector2(0f, 0.5f);
            fadeRect.sizeDelta = new Vector2(1500f, 0f);
            fadeRect.anchoredPosition = Vector2.zero;

            var safeGo = new GameObject("SafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(root.transform, false);
            Stretch((RectTransform)safeGo.transform);
            safeGo.AddComponent<SafeAreaFitter>();

            var title = NewText("Kicker", safeGo.transform, "ULTIMATE", 28f, UiPalette.TextSecondary, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            title.characterSpacing = 16f;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -78f), new Vector2(700f, 36f));
            var wordmark = NewText("Title", safeGo.transform, "CAR PARKING<size=62%><color=#9AA3AD>  3D</color></size>", 104f, UiPalette.TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Anchor(wordmark.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -118f), new Vector2(1100f, 120f));
            var bar = NewImage("Accent", safeGo.transform, UiPalette.TextPrimary.WithAlpha(0.85f));
            Anchor(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(98f, -250f), new Vector2(72f, 4f));

            var account = NewText("AccountKicker", safeGo.transform, "ACCOUNT", 22f, UiPalette.TextSecondary, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            account.characterSpacing = 6f;
            Anchor(account.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(98f, 430f), new Vector2(700f, 32f));
            var signedOut = NewText("SignedOut", safeGo.transform, "SIGN IN", 56f, UiPalette.TextPrimary, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            signedOut.characterSpacing = 2f;
            Anchor(signedOut.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 360f), new Vector2(700f, 70f));
            message = NewText("Message", safeGo.transform, "Sign in to keep this player, or continue as a guest.", 26f, UiPalette.TextSecondary, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(message.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(98f, 286f), new Vector2(640f, 70f));

            googleButton = NewButton("Google", safeGo.transform, UiPalette.Lime, UiPalette.TextOnLime, "SIGN IN WITH GOOGLE", 42f, out googleLabel, out googleImage);
            googleLabel.characterSpacing = 8f;
            Anchor(googleButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 148f), new Vector2(700f, 124f));
            googleButton.onClick.AddListener(() => _ = AuthService.SignInWithGoogle());

            guestButton = NewButton("Guest", safeGo.transform, UiPalette.TextPrimary.WithAlpha(0.6f), UiPalette.TextPrimary, "CONTINUE AS GUEST", 30f, out TMP_Text guestLabel, out guestImage);
            guestLabel.characterSpacing = 8f;
            Anchor(guestButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 36f), new Vector2(700f, 100f));
            guestButton.onClick.AddListener(() => _ = AuthService.ContinueAsGuest());

            AdoptTheme();
            root.SetActive(false);
        }

        private static Sprite SideFadeSprite()
        {
            var tex = new Texture2D(256, 4, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < 256; x++)
            {
                float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.32f, 1f, x / 255f)) * 0.96f;
                var c = new Color(1f, 1f, 1f, a);
                for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 256, 4), new Vector2(0f, 0.5f), 100f);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string value, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private void AdoptTheme()
        {
            TMP_FontAsset heading = null;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (text.font == null || text.transform.IsChildOf(transform)) continue;
                if (text.font.name.IndexOf("Rajdhani", System.StringComparison.OrdinalIgnoreCase) >= 0) { heading = text.font; break; }
                heading ??= text.font;
            }
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (image.sprite == null || image.transform.IsChildOf(transform)) continue;
                if (pill == null && image.sprite.name == "Pill") pill = image.sprite;
                if (pillOutline == null && image.sprite.name == "PillOutline") pillOutline = image.sprite;
            }
            pill ??= FallbackPill(false);
            pillOutline ??= FallbackPill(true);
            UseSprite(googleImage, pill);
            UseSprite(guestImage, pillOutline);
            if (heading == null || root == null) return;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = heading;
        }

        private static void UseSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        private static Sprite FallbackPill(bool outline)
        {
            const int w = 128, h = 64, radius = 31;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius);
                float dy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius);
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float d = outside + Mathf.Min(Mathf.Max(dx, dy), 0f);
                float a = outline
                    ? Mathf.Clamp01(1f - Mathf.Abs(d + 2f) / 1.5f)
                    : Mathf.Clamp01(0.5f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
        }

        private static Button NewButton(string name, Transform parent, Color fill, Color labelColor, string label, float labelSize, out TMP_Text labelText, out Image image)
        {
            image = NewImage(name, parent, fill);
            image.type = Image.Type.Sliced;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            image.gameObject.AddComponent<UIButtonFX>();
            labelText = NewText("Label", image.transform, label, labelSize, labelColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
