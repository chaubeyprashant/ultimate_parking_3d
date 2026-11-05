using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Gameplay panels: Pause (Resume / Restart / Settings / Levels), Level Completed (stars revealed one by one with
    /// chimes, time, collisions, coins rolling up; Next / Retry / Levels) and Level Failed (reason; Retry / Levels).
    /// Pausing freezes time and audio; every way out restores time first.
    /// </summary>
    public sealed class GameplayUiController : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ProgressionRecorder progression;
        [SerializeField] private MobileControls controls;
        [SerializeField] private ScreenFlash flash;
        [SerializeField] private GameObject hudRoot;

        [Header("Pause")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private CanvasGroup pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button pauseLevelsButton;
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("Completed")]
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private TMP_Text completeTitle;
        [SerializeField] private Image[] completeStars = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text completeTime;
        [SerializeField] private TMP_Text completeCollisions;
        [SerializeField] private TMP_Text completeCoins;
        [SerializeField] private TMP_Text completeBadge;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button completeRetryButton;
        [SerializeField] private Button completeLevelsButton;

        [Header("Failed")]
        [SerializeField] private CanvasGroup failPanel;
        [SerializeField] private TMP_Text failReason;
        [SerializeField] private Button failRetryButton;
        [SerializeField] private Button failLevelsButton;

        [SerializeField, Min(0f)] private float resultDelay = 0.6f;

        private InputAction backAction;

        public bool IsPaused { get; private set; }
        public bool CompleteShown => completePanel != null && completePanel.gameObject.activeSelf;
        public bool FailShown => failPanel != null && failPanel.gameObject.activeSelf;
        public bool PauseShown => pausePanel != null && pausePanel.gameObject.activeSelf;
        public int StarsRevealed { get; private set; }

        private void Awake()
        {
            Hide(pausePanel); Hide(completePanel); Hide(failPanel);
            Bind(pauseButton, Pause);
            Bind(resumeButton, Resume);
            Bind(pauseRestartButton, Restart);
            Bind(pauseSettingsButton, () => { if (settingsPanel != null) settingsPanel.Open(); });
            Bind(pauseLevelsButton, ToLevels);
            Bind(nextButton, Next);
            Bind(completeRetryButton, Restart);
            Bind(completeLevelsButton, ToLevels);
            Bind(failRetryButton, Restart);
            Bind(failLevelsButton, ToLevels);
            if (levelManager == null) { Debug.LogError("[GameplayUI] No LevelManager assigned.", this); return; }
            levelManager.LevelCompleted += OnCompleted;
            levelManager.LevelFailed += OnFailed;

            // Android Back / Escape: pause, or resume when paused. Callback-driven (no polling).
            backAction = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
            backAction.performed += _ => { if (IsPaused) Resume(); else Pause(); };
            backAction.Enable();
        }

        private void Start() => AudioManager.Instance.PlayMusic(ducked: true);

        private void OnDestroy()
        {
            backAction?.Dispose();
            if (levelManager != null)
            {
                levelManager.LevelCompleted -= OnCompleted;
                levelManager.LevelFailed -= OnFailed;
            }
            if (IsPaused) SetPaused(false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && levelManager != null && levelManager.State == LevelState.Playing) Pause();
        }

        // ---------------------------------------------------------------- pause

        public void Pause()
        {
            if (IsPaused || levelManager == null || levelManager.State != LevelState.Playing) return;
            SetPaused(true);
            Show(pausePanel);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            if (settingsPanel != null) settingsPanel.Close();
            Hide(pausePanel);
            SetPaused(false);
        }

        private void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (paused && controls != null) controls.ReleaseAll();
            if (hudRoot != null) hudRoot.SetActive(!paused);
        }

        // ---------------------------------------------------------------- results

        private void OnCompleted(LevelResult r) => StartCoroutine(ShowComplete(r));

        private IEnumerator ShowComplete(LevelResult r)
        {
            if (controls != null) controls.ReleaseAll();
            AudioManager.Instance.Play(Sfx.Success, 0.9f);
            yield return new WaitForSecondsRealtime(resultDelay);
            if (hudRoot != null) hudRoot.SetActive(false);

            int coins = progression != null && progression.LastReward is LevelReward reward ? reward.Coins : r.Coins;
            bool newBest = progression != null && progression.LastReward is LevelReward rw && rw.NewBest && !rw.FirstCompletion;
            if (completeTitle != null) completeTitle.text = "PARKED";
            if (completeTime != null) completeTime.text = $"{r.ElapsedSeconds:0.0}s";
            if (completeCollisions != null) completeCollisions.text = r.Collisions.ToString();
            if (completeCoins != null) completeCoins.text = "+0";
            if (completeBadge != null) completeBadge.text = newBest ? "NEW BEST" : "";
            if (nextButton != null) nextButton.gameObject.SetActive(levelManager.HasNextLevel);
            foreach (Image s in completeStars) if (s != null) { s.color = UiPalette.Outline; s.transform.localScale = Vector3.one; }
            Show(completePanel);

            yield return new WaitForSecondsRealtime(0.35f);
            for (int i = 0; i < completeStars.Length && i < r.Stars; i++)
            {
                Image star = completeStars[i];
                if (star == null) continue;
                star.color = UiPalette.Lime;
                StartCoroutine(UiMotion.Scale(star.transform, 1.6f, 1f, 0.35f, back: true));
                AudioManager.Instance.Play(Sfx.Star, 0.8f, 1f + i * 0.12f);
                StarsRevealed = i + 1;
                yield return new WaitForSecondsRealtime(0.28f);
            }

            if (completeCoins != null && coins > 0)
            {
                AudioManager.Instance.Play(Sfx.Coin, 0.8f);
                for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
                {
                    completeCoins.text = $"+{Mathf.RoundToInt(coins * UiMotion.EaseOutCubic(t / 0.7f))}";
                    yield return null;
                }
            }
            if (completeCoins != null) completeCoins.text = $"+{coins}";
        }

        private void OnFailed(LevelResult r) => StartCoroutine(ShowFail(r));

        private IEnumerator ShowFail(LevelResult r)
        {
            if (controls != null) controls.ReleaseAll();
            AudioManager.Instance.Play(Sfx.Fail, 0.9f);
            if (flash != null) flash.Flash(UiPalette.Danger, 0.45f, 0.8f);
            yield return new WaitForSecondsRealtime(resultDelay);
            if (hudRoot != null) hudRoot.SetActive(false);
            if (failReason != null)
                failReason.text = r.FailReason == FailReason.TimeUp ? "TIME'S UP" : r.FailReason == FailReason.TooManyCollisions ? "TOO MANY COLLISIONS" : "FAILED";
            Show(failPanel);
            if (failPanel != null) StartCoroutine(UiMotion.Shake((RectTransform)failPanel.transform, 10f, 0.3f));
        }

        // ---------------------------------------------------------------- navigation

        public void Restart()
        {
            SetPaused(false);
            levelManager.RestartLevel();
        }

        public void Next()
        {
            SetPaused(false);
            levelManager.NextLevel();
        }

        public void ToLevels()
        {
            SetPaused(false);
            ScreenTransition.LoadScene(SceneNames.LevelSelect);
        }

        // ---------------------------------------------------------------- helpers

        private static void Bind(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b != null) b.onClick.AddListener(action);
        }

        private void Show(CanvasGroup g)
        {
            if (g == null) return;
            g.gameObject.SetActive(true);
            g.interactable = true;
            g.blocksRaycasts = true;
            StartCoroutine(UiMotion.Fade(g, 0f, 1f, 0.25f));
            StartCoroutine(UiMotion.Scale(g.transform, 0.94f, 1f, 0.3f));
        }

        private static void Hide(CanvasGroup g)
        {
            if (g == null) return;
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            g.gameObject.SetActive(false);
        }
    }
}
