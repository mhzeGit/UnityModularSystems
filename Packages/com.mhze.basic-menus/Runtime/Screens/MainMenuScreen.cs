using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Root menu shown when the game starts. Provides Play / Continue / Options /
    /// Info / Quit buttons; any button can be left empty and its handler is then
    /// skipped, so the screen adapts to the game.
    ///
    /// Options and Info are sub-panels: opening one slides the main button
    /// holder off-screen and opening the panel, closing the panel slides the
    /// buttons back in. Buttons fire UnityEvents so scene loading and quit
    /// behaviour stay decoupled from this package.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        [Header("Buttons")]
        [Tooltip("Starts a new game. Wire scene loading through onPlayClicked.")]
        [SerializeField] private Button playButton;

        [Tooltip("Optional continue button for games with a save system.")]
        [SerializeField] private Button continueButton;

        [SerializeField] private Button optionsButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private Button quitButton;

        [Header("Sub-panels")]
        [Tooltip("Options panel, shown when the Options button is pressed.")]
        [SerializeField] private UIScreen optionsScreen;

        [Tooltip("Info/credits panel, shown when the Info button is pressed.")]
        [SerializeField] private UIScreen infoScreen;

        [Header("Slide Animation")]
        [Tooltip("RectTransform holding the main buttons. It slides off-screen while a sub-panel is open.")]
        [SerializeField] private RectTransform mainButtonsHolder;

        [SerializeField] private float slideDuration = 0.4f;

        [Tooltip("Extra distance added when sliding the holder off-screen.")]
        [SerializeField] private float slideMargin = 80f;

        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Behavior")]
        [Tooltip("When enabled, the Quit button stops play mode in the editor instead of calling Application.Quit.")]
        [SerializeField] private bool stopPlayModeInEditor = true;

        [Header("Events")]
        [Tooltip("Fired when Play is clicked. Wire your scene loading here.")]
        [SerializeField] private UnityEvent onPlayClicked = new UnityEvent();

        [Tooltip("Fired when Continue is clicked. Wire your save loading here.")]
        [SerializeField] private UnityEvent onContinueClicked = new UnityEvent();

        [Tooltip("Fired right before quitting.")]
        [SerializeField] private UnityEvent onQuitClicked = new UnityEvent();

        private Vector2 _mainOrigin;
        private Vector2 _mainHidden;
        private bool _isAnimating;
        private UIScreen _openSubPanel;
        private Selectable _subPanelSource;

        protected override void Awake()
        {
            startHidden = false; // the main menu is visible at start
            base.Awake();
            IsOpen = true;
        }

        private void Start()
        {
            CacheSlidePositions();

            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (optionsButton != null) optionsButton.onClick.AddListener(() => OpenSubPanel(optionsScreen, optionsButton));
            if (infoButton != null) infoButton.onClick.AddListener(() => OpenSubPanel(infoScreen, infoButton));
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            // Make sure sub-panels start closed even if they were left active in the scene.
            if (optionsScreen != null) optionsScreen.Close();
            if (infoScreen != null) infoScreen.Close();

            PushFocus();
        }

        // ── Button handlers ──────────────────────────────────────────────────

        private void OnPlay()
        {
            onPlayClicked?.Invoke();
        }

        private void OnContinue()
        {
            onContinueClicked?.Invoke();
        }

        private void OnQuit()
        {
            onQuitClicked?.Invoke();

#if UNITY_EDITOR
            if (stopPlayModeInEditor)
                UnityEditor.EditorApplication.isPlaying = false;
            else
                Application.Quit();
#else
            Application.Quit();
#endif
        }

        // ── Sub-panel management ─────────────────────────────────────────────

        private void OpenSubPanel(UIScreen panel, Selectable source)
        {
            if (panel == null || _isAnimating || _openSubPanel != null) return;

            _openSubPanel = panel;
            _subPanelSource = source;

            panel.Closed += OnSubPanelClosed;
            panel.transform.SetAsLastSibling();
            StartCoroutine(SlideMainButtonsOff(panel));
        }

        private void OnSubPanelClosed()
        {
            if (_openSubPanel == null) return;

            _openSubPanel.Closed -= OnSubPanelClosed;
            _openSubPanel = null;

            StartCoroutine(SlideMainButtonsBack());
        }

        private IEnumerator SlideMainButtonsOff(UIScreen panel)
        {
            _isAnimating = true;

            panel.Open();
            yield return SlideTo(_mainHidden);

            _isAnimating = false;
        }

        private IEnumerator SlideMainButtonsBack()
        {
            _isAnimating = true;

            yield return SlideTo(_mainOrigin);

            var eventSystem = EventSystem.current;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);

            if (_subPanelSource != null) SelectNow(_subPanelSource);
            else PushFocus();

            _subPanelSource = null;
            _isAnimating = false;
        }

        private IEnumerator SlideTo(Vector2 target)
        {
            if (mainButtonsHolder == null) yield break;

            Vector2 from = mainButtonsHolder.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = slideCurve.Evaluate(Mathf.Clamp01(elapsed / slideDuration));
                mainButtonsHolder.anchoredPosition = Vector2.LerpUnclamped(from, target, t);
                yield return null;
            }

            mainButtonsHolder.anchoredPosition = target;
        }

        private void CacheSlidePositions()
        {
            if (mainButtonsHolder == null) return;

            _mainOrigin = mainButtonsHolder.anchoredPosition;

            float parentWidth = mainButtonsHolder.parent is RectTransform parentRect && parentRect.rect.width > 1f
                ? parentRect.rect.width
                : UnityEngine.Screen.width;

            float holderWidth = mainButtonsHolder.rect.width > 1f
                ? mainButtonsHolder.rect.width
                : mainButtonsHolder.sizeDelta.x;

            _mainHidden = _mainOrigin + Vector2.left * (parentWidth + Mathf.Max(holderWidth, 0f) + slideMargin);
        }

        // ── UIScreen overrides ───────────────────────────────────────────────

        public override int BackPriority => 40;

        public override bool OnBack()
        {
            // Sub-panels handle back themselves (higher priority). This only
            // fires at the menu root, where there is nothing to go back from.
            if (_isAnimating) return true; // swallow input during the transition
            return false;
        }
    }
}
