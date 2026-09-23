using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// In-game pause menu. Lives on an ALWAYS-ACTIVE GameObject (so it can always
    /// receive the pause/back input) and shows/hides an assigned content panel.
    ///
    /// Pause input can be provided through the assigned Pause action; if no
    /// action is assigned, Escape is still handled by <see cref="UIBackRouter"/>
    /// through this screen's <see cref="OnBack"/>.
    ///
    /// Resume / Restart / Options / Main Menu / Quit buttons are all optional.
    /// Restart reloads the active scene (or an optional override scene) and
    /// Main Menu loads the configured scene if one is set.
    /// </summary>
    public class PauseScreen : UIScreen
    {
        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Sub-panels")]
        [Tooltip("Options panel, shown when the Options button is pressed.")]
        [SerializeField] private UIScreen optionsScreen;

        [Header("Scenes")]
        [Tooltip("Scene loaded by the Main Menu button. Leave empty to only fire onMainMenuClicked.")]
        [SerializeField] private string mainMenuSceneName = "";

        [Tooltip("Scene reloaded by the Restart button. Leave empty to reload the active scene.")]
        [SerializeField] private string restartSceneName = "";

        [Header("Input (optional)")]
        [Tooltip("Pause action (Escape / Start). When empty, a project-wide Pause action is used if one exists, otherwise UI/Cancel.")]
        [SerializeField] private InputActionReference pauseAction;

        [Header("Behavior")]
        [Tooltip("When enabled, the Quit button stops play mode in the editor instead of calling Application.Quit.")]
        [SerializeField] private bool stopPlayModeInEditor = true;

        [Header("Events")]
        [Tooltip("Fired after the game pauses (time scale already set to 0).")]
        [SerializeField] private UnityEvent onPaused = new UnityEvent();

        [Tooltip("Fired after the game resumes.")]
        [SerializeField] private UnityEvent onResumed = new UnityEvent();

        [SerializeField] private UnityEvent onRestartClicked = new UnityEvent();
        [SerializeField] private UnityEvent onMainMenuClicked = new UnityEvent();
        [SerializeField] private UnityEvent onQuitClicked = new UnityEvent();

        /// <summary>Fired after the game pauses (time scale already set to 0).</summary>
        public event Action Paused;

        /// <summary>Fired after the game resumes.</summary>
        public event Action Resumed;

        private bool _isPaused;
        private InputAction _resolvedPauseAction;

        protected override void Awake()
        {
            // Do not call base.Awake(): this GameObject must stay active so the
            // screen keeps receiving input while the game is running. Only the
            // content panel hides.
            startHidden = false;
            if (contentPanel != null) contentPanel.SetActive(false);
            if (optionsScreen != null) optionsScreen.Close();
        }

        private void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _resolvedPauseAction = ResolvePauseAction();
            if (_resolvedPauseAction != null)
            {
                _resolvedPauseAction.performed += OnPausePerformed;
                _resolvedPauseAction.Enable();
            }
        }

        protected override void OnDisable()
        {
            if (_resolvedPauseAction != null)
                _resolvedPauseAction.performed -= OnPausePerformed;
            _resolvedPauseAction = null;

            base.OnDisable();

            // Safety: never leave the game frozen when this screen is destroyed.
            if (_isPaused)
            {
                Time.timeScale = 1f;
                _isPaused = false;
            }
        }

        private InputAction ResolvePauseAction()
        {
            if (pauseAction != null && pauseAction.action != null)
                return pauseAction.action;

            var projectWideActions = UnityEngine.InputSystem.InputSystem.actions;
            if (projectWideActions == null) return null;

            // Prefer a dedicated Pause action (any map), fall back to UI/Cancel
            // so Escape / gamepad B can open the menu out of the box.
            return projectWideActions.FindAction("Pause") ?? projectWideActions.FindAction("UI/Cancel");
        }

        // ── Pause / Resume ───────────────────────────────────────────────────

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            // The back router runs first for the same key and may have already
            // resumed the game (or closed a sub-screen); never toggle twice.
            if (UIBackRouter.BackHandledThisFrame) return;

            bool hadOpenScreen = UIScreen.AnyOpen;

            // Escape is shared between Pause and Back on keyboard: if a menu is
            // already open, let the back router handle it instead.
            bool isKeyboardInput = context.control != null && context.control.device is Keyboard;
            if (hadOpenScreen && isKeyboardInput) return;

            if (!hadOpenScreen)
            {
                Pause();
                UIBackRouter.SuppressBackThisFrame();
                return;
            }

            if (_isPaused) Resume();
        }

        /// <summary>Freeze the game and show the pause panel.</summary>
        public void Pause()
        {
            if (_isPaused) return;

            _isPaused = true;
            IsOpen = true;

            Time.timeScale = 0f;
            if (contentPanel != null) contentPanel.SetActive(true);

            PushFocus();
            onPaused?.Invoke();
            Paused?.Invoke();
        }

        /// <summary>Hide the pause panel and resume the game.</summary>
        public void Resume()
        {
            if (!_isPaused) return;

            if (optionsScreen != null && optionsScreen.IsOpen)
            {
                optionsScreen.Closed -= OnOptionsClosed;
                optionsScreen.Close();
            }

            _isPaused = false;
            IsOpen = false;

            Time.timeScale = 1f;
            if (contentPanel != null) contentPanel.SetActive(false);

            onResumed?.Invoke();
            Resumed?.Invoke();
        }

        // ── Options ──────────────────────────────────────────────────────────

        private void OpenOptions()
        {
            if (optionsScreen == null) return;

            optionsScreen.Closed += OnOptionsClosed;
            optionsScreen.Open();
        }

        private void OnOptionsClosed()
        {
            if (optionsScreen != null) optionsScreen.Closed -= OnOptionsClosed;
            if (optionsButton != null) SelectNow(optionsButton);
        }

        // ── Other buttons ────────────────────────────────────────────────────

        private void Restart()
        {
            Resume();
            onRestartClicked?.Invoke();

            if (!string.IsNullOrWhiteSpace(restartSceneName))
            {
                SceneManager.LoadScene(restartSceneName);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0) SceneManager.LoadScene(activeScene.buildIndex);
            else SceneManager.LoadScene(activeScene.name);
        }

        private void GoToMainMenu()
        {
            Resume();
            onMainMenuClicked?.Invoke();

            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                Debug.LogWarning("[Basic Menus] Pause menu Main Menu button pressed, but no main menu scene name is set.");
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void QuitGame()
        {
            Resume();
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

        // ── UIScreen overrides ───────────────────────────────────────────────

        public override int BackPriority => 0;

        public override bool OnBack()
        {
            // Arriving here means nothing else consumed the back input.
            if (!_isPaused) return false;

            Resume();
            return true;
        }
    }
}
