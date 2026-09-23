using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Tabbed settings menu (Gameplay / Video / Audio / Controls by default,
    /// any number of tabs is supported). It applies every change immediately,
    /// persists everything in PlayerPrefs and raises static events so gameplay
    /// systems can react without referencing the menu.
    ///
    /// Every field is optional: assign only the controls you actually have and
    /// the screen silently skips the rest. Rebind rows found under this screen
    /// (see <see cref="InputRebindRowUI"/>) are picked up automatically.
    /// </summary>
    public class OptionsScreen : UIScreen
    {
        // ── Static events for gameplay systems ───────────────────────────────

        public static event Action<float> OnSensitivityChanged;
        public static event Action<bool> OnInvertYChanged;
        public static event Action<int> OnQualityChanged;
        public static event Action<int> OnWindowModeChanged;
        public static event Action<int, int> OnResolutionChanged;
        public static event Action<bool> OnVSyncChanged;
        public static event Action<int> OnFrameRateChanged;
        public static event Action<string, float> OnVolumeChanged;

        // ── Mixer parameter names (exposed AudioMixer parameters) ────────────

        public const string MixerParamMaster = "MasterVolume";
        public const string MixerParamMusic = "MusicVolume";
        public const string MixerParamSFX = "SFXVolume";
        public const string MixerParamDialog = "DialogVolume";

        // ── PlayerPrefs keys ─────────────────────────────────────────────────

        public const string PrefSensitivity = "Opt_Sensitivity";
        public const string PrefInvertY = "Opt_InvertY";
        public const string PrefQuality = "Opt_Quality";
        public const string PrefWindowMode = "Opt_WindowMode";
        public const string PrefResolutionWidth = "Opt_ResW";
        public const string PrefResolutionHeight = "Opt_ResH";
        public const string PrefVSync = "Opt_VSync";
        public const string PrefFrameRate = "Opt_FrameRate";
        public const string PrefVolumeMaster = "Opt_VolMaster";
        public const string PrefVolumeMusic = "Opt_VolMusic";
        public const string PrefVolumeSFX = "Opt_VolSFX";
        public const string PrefVolumeDialog = "Opt_VolDialog";
        public const string PrefInputRebinds = "Opt_InputRebinds";

        // ── Defaults ─────────────────────────────────────────────────────────

        public const float DefaultSensitivity = 1f;
        public const bool DefaultInvertY = false;
        public const float DefaultVolume = 0.75f;

        private static readonly int[] FrameRateOptions = { 30, 60, 90, 120, 144, 165, 240, -1 };

        private static readonly Vector2Int[] CommonResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160)
        };

        // ── Inspector fields ─────────────────────────────────────────────────

        [Serializable]
        public class Tab
        {
            [Tooltip("Label only, used for readability in the inspector.")]
            public string name;

            [Tooltip("Button that activates this tab.")]
            public Button button;

            [Tooltip("Panel shown while this tab is active.")]
            public GameObject contentPanel;
        }

        [Header("Tabs")]
        [Tooltip("One entry per options category. Buttons are wired automatically.")]
        [SerializeField] private Tab[] tabs;

        [Tooltip("Switch tabs as soon as a tab button is highlighted with keyboard/gamepad.")]
        [SerializeField] private bool switchTabOnSelect = true;

        [SerializeField] private int defaultTabIndex;

        [Header("Navigation")]
        [Tooltip("Back button that closes this screen.")]
        [SerializeField] private Button backButton;

        [Header("Gameplay")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityValueText;
        [SerializeField] private Toggle invertYToggle;

        [Header("Video")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown windowModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private TMP_Dropdown frameRateDropdown;

        [Header("Audio")]
        [Tooltip("Optional AudioMixer. Exposed parameters: MasterVolume, MusicVolume, SFXVolume, DialogVolume. Without a mixer, master volume falls back to AudioListener.volume.")]
        [SerializeField] private AudioMixer audioMixer;

        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider dialogVolumeSlider;

        [Header("Controls")]
        [Tooltip("Optional button that resets every rebind row under this screen.")]
        [SerializeField] private Button resetAllBindingsButton;

        [Header("Reset")]
        [Tooltip("Optional button that restores all saved options to their defaults.")]
        [SerializeField] private Button resetToDefaultsButton;

        // ── State ────────────────────────────────────────────────────────────

        private int _currentTab = -1;
        private Resolution[] _filteredResolutions;

        private readonly List<InputRebindRowUI> _rebindRows = new List<InputRebindRowUI>();
        private readonly List<InputActionAsset> _rebindAssets = new List<InputActionAsset>();
        private bool _isLoadingInputRebinds;

        [Serializable]
        private class InputRebindEntry
        {
            public string assetName;
            public string overridesJson;
        }

        [Serializable]
        private class InputRebindSaveData
        {
            public List<InputRebindEntry> entries = new List<InputRebindEntry>();
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            InitializeTabs();
            SetupDropdowns();
            CacheRebindRows();
        }

        private void Start()
        {
            LoadAllSettings();
            RegisterCallbacks();
            BroadcastCurrentSettings();

            if (backButton != null) backButton.onClick.AddListener(Close);
            if (resetToDefaultsButton != null) resetToDefaultsButton.onClick.AddListener(ResetToDefaults);
            if (resetAllBindingsButton != null) resetAllBindingsButton.onClick.AddListener(ResetAllBindings);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnityEngine.InputSystem.InputSystem.onActionChange -= HandleInputActionChange;
        }

        protected override void OnOpened()
        {
            SelectTabIndex(defaultTabIndex, true);

            CacheRebindRows();
            RefreshRebindRows();

            if (backButton != null)
                SetDefaultSelectable(backButton);
        }

        // ── Tabs ─────────────────────────────────────────────────────────────

        private void InitializeTabs()
        {
            if (tabs == null) return;

            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab == null) continue;

                if (tab.contentPanel != null)
                    tab.contentPanel.SetActive(false);

                if (tab.button == null) continue;

                int index = i;
                tab.button.onClick.AddListener(() => SelectTabIndex(index));

                if (switchTabOnSelect && tab.button.GetComponent<TabSelectHelper>() == null)
                    tab.button.gameObject.AddComponent<TabSelectHelper>().Initialize(index, SelectTabIndex);
            }

            _currentTab = -1;
        }

        /// <summary>Show the tab at <paramref name="index"/>.</summary>
        public void SelectTabIndex(int index)
        {
            SelectTabIndex(index, false);
        }

        /// <summary>Show the tab at <paramref name="index"/>, optionally re-applying it when already active.</summary>
        public void SelectTabIndex(int index, bool force)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            if (!force && _currentTab == index) return;

            _currentTab = index;

            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab?.contentPanel != null)
                    tab.contentPanel.SetActive(i == index);
            }
        }

        // ── Setup ────────────────────────────────────────────────────────────

        private void SetupDropdowns()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            }

            if (windowModeDropdown != null)
            {
                windowModeDropdown.ClearOptions();
                windowModeDropdown.AddOptions(new List<string>
                {
                    "Exclusive Fullscreen",
                    "Fullscreen Window",
                    "Maximized Window",
                    "Windowed"
                });
            }

            if (frameRateDropdown != null)
            {
                frameRateDropdown.ClearOptions();

                var labels = new List<string>(FrameRateOptions.Length);
                for (int i = 0; i < FrameRateOptions.Length; i++)
                    labels.Add(FrameRateOptions[i] <= 0 ? "Unlimited" : FrameRateOptions[i].ToString());

                frameRateDropdown.AddOptions(labels);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();

                var available = new HashSet<string>();
                foreach (var resolution in UnityEngine.Screen.resolutions)
                    available.Add($"{resolution.width}x{resolution.height}");

                var list = new List<Resolution>();
                foreach (var common in CommonResolutions)
                {
                    if (available.Contains($"{common.x}x{common.y}"))
                        list.Add(new Resolution { width = common.x, height = common.y });
                }

                if (list.Count == 0)
                {
                    var current = UnityEngine.Screen.currentResolution;
                    list.Add(new Resolution { width = current.width, height = current.height });
                }

                _filteredResolutions = list.ToArray();
                resolutionDropdown.AddOptions(list.ConvertAll(r => $"{r.width} x {r.height}"));
            }
        }

        private void RegisterCallbacks()
        {
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(SetInvertY);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
            if (windowModeDropdown != null) windowModeDropdown.onValueChanged.AddListener(SetWindowMode);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
            if (vsyncToggle != null) vsyncToggle.onValueChanged.AddListener(SetVSync);
            if (frameRateDropdown != null) frameRateDropdown.onValueChanged.AddListener(SetFrameRate);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(value => SetVolume(PrefVolumeMaster, MixerParamMaster, value));
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(value => SetVolume(PrefVolumeMusic, MixerParamMusic, value));
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(value => SetVolume(PrefVolumeSFX, MixerParamSFX, value));
            if (dialogVolumeSlider != null) dialogVolumeSlider.onValueChanged.AddListener(value => SetVolume(PrefVolumeDialog, MixerParamDialog, value));

            UnityEngine.InputSystem.InputSystem.onActionChange -= HandleInputActionChange;
            UnityEngine.InputSystem.InputSystem.onActionChange += HandleInputActionChange;
        }

        // ── Load / broadcast ─────────────────────────────────────────────────

        private void LoadAllSettings()
        {
            // Gameplay
            if (sensitivitySlider != null)
            {
                float sensitivity = PlayerPrefs.GetFloat(PrefSensitivity, DefaultSensitivity);
                sensitivitySlider.value = sensitivity;
                UpdateSensitivityText(sensitivity);
            }

            if (invertYToggle != null)
                invertYToggle.isOn = PlayerPrefs.GetInt(PrefInvertY, DefaultInvertY ? 1 : 0) == 1;

            // Video
            if (qualityDropdown != null)
            {
                int quality = PlayerPrefs.GetInt(PrefQuality, QualitySettings.GetQualityLevel());
                quality = Mathf.Clamp(quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
                qualityDropdown.value = quality;
                qualityDropdown.RefreshShownValue();
                QualitySettings.SetQualityLevel(quality);
            }

            if (windowModeDropdown != null)
            {
                int mode = PlayerPrefs.GetInt(PrefWindowMode, (int)UnityEngine.Screen.fullScreenMode);
                windowModeDropdown.value = Mathf.Clamp(mode, 0, 3);
                windowModeDropdown.RefreshShownValue();
                UnityEngine.Screen.fullScreenMode = (FullScreenMode)windowModeDropdown.value;
            }

            if (resolutionDropdown != null && _filteredResolutions != null && _filteredResolutions.Length > 0)
            {
                int width = PlayerPrefs.GetInt(PrefResolutionWidth, UnityEngine.Screen.currentResolution.width);
                int height = PlayerPrefs.GetInt(PrefResolutionHeight, UnityEngine.Screen.currentResolution.height);

                int index = 0;
                for (int i = 0; i < _filteredResolutions.Length; i++)
                {
                    if (_filteredResolutions[i].width == width && _filteredResolutions[i].height == height)
                    {
                        index = i;
                        break;
                    }
                }

                resolutionDropdown.value = index;
                resolutionDropdown.RefreshShownValue();

                var resolution = _filteredResolutions[index];
                UnityEngine.Screen.SetResolution(resolution.width, resolution.height, UnityEngine.Screen.fullScreenMode);
            }

            if (vsyncToggle != null)
            {
                bool vsync = PlayerPrefs.GetInt(PrefVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
                vsyncToggle.isOn = vsync;
                QualitySettings.vSyncCount = vsync ? 1 : 0;
            }

            if (frameRateDropdown != null)
            {
                int frameRate = PlayerPrefs.GetInt(PrefFrameRate, Application.targetFrameRate);
                int index = IndexOfFrameRate(frameRate);
                frameRateDropdown.value = index;
                frameRateDropdown.RefreshShownValue();
                Application.targetFrameRate = FrameRateOptions[index];
            }

            // Audio
            LoadVolume(masterVolumeSlider, PrefVolumeMaster, MixerParamMaster);
            LoadVolume(musicVolumeSlider, PrefVolumeMusic, MixerParamMusic);
            LoadVolume(sfxVolumeSlider, PrefVolumeSFX, MixerParamSFX);
            LoadVolume(dialogVolumeSlider, PrefVolumeDialog, MixerParamDialog);

            // Controls
            LoadInputRebindOverrides();
            RefreshRebindRows();
        }

        private void LoadVolume(Slider slider, string prefKey, string mixerParam)
        {
            if (slider == null) return;

            float volume = PlayerPrefs.GetFloat(prefKey, DefaultVolume);
            slider.value = volume;
            ApplyVolume(mixerParam, volume);
        }

        /// <summary>
        /// Fire every settings event with the current values so gameplay systems
        /// receive the saved settings as soon as the screen initializes.
        /// </summary>
        public void BroadcastCurrentSettings()
        {
            if (sensitivitySlider != null) OnSensitivityChanged?.Invoke(sensitivitySlider.value);
            if (invertYToggle != null) OnInvertYChanged?.Invoke(invertYToggle.isOn);
            if (qualityDropdown != null) OnQualityChanged?.Invoke(qualityDropdown.value);
            if (windowModeDropdown != null) OnWindowModeChanged?.Invoke(windowModeDropdown.value);

            if (resolutionDropdown != null && _filteredResolutions != null &&
                resolutionDropdown.value >= 0 && resolutionDropdown.value < _filteredResolutions.Length)
            {
                var resolution = _filteredResolutions[resolutionDropdown.value];
                OnResolutionChanged?.Invoke(resolution.width, resolution.height);
            }

            if (vsyncToggle != null) OnVSyncChanged?.Invoke(vsyncToggle.isOn);
            if (frameRateDropdown != null && frameRateDropdown.value >= 0 && frameRateDropdown.value < FrameRateOptions.Length)
                OnFrameRateChanged?.Invoke(FrameRateOptions[frameRateDropdown.value]);

            if (masterVolumeSlider != null) OnVolumeChanged?.Invoke(MixerParamMaster, masterVolumeSlider.value);
            if (musicVolumeSlider != null) OnVolumeChanged?.Invoke(MixerParamMusic, musicVolumeSlider.value);
            if (sfxVolumeSlider != null) OnVolumeChanged?.Invoke(MixerParamSFX, sfxVolumeSlider.value);
            if (dialogVolumeSlider != null) OnVolumeChanged?.Invoke(MixerParamDialog, dialogVolumeSlider.value);
        }

        // ── Gameplay setters ─────────────────────────────────────────────────

        private void SetSensitivity(float value)
        {
            PlayerPrefs.SetFloat(PrefSensitivity, value);
            PlayerPrefs.Save();

            UpdateSensitivityText(value);
            OnSensitivityChanged?.Invoke(value);
        }

        private void UpdateSensitivityText(float value)
        {
            if (sensitivityValueText != null)
                sensitivityValueText.text = value.ToString("F1");
        }

        private void SetInvertY(bool value)
        {
            PlayerPrefs.SetInt(PrefInvertY, value ? 1 : 0);
            PlayerPrefs.Save();
            OnInvertYChanged?.Invoke(value);
        }

        // ── Video setters ────────────────────────────────────────────────────

        private void SetQuality(int index)
        {
            QualitySettings.SetQualityLevel(index);
            PlayerPrefs.SetInt(PrefQuality, index);
            PlayerPrefs.Save();
            OnQualityChanged?.Invoke(index);
        }

        private void SetWindowMode(int index)
        {
            UnityEngine.Screen.fullScreenMode = (FullScreenMode)index;
            PlayerPrefs.SetInt(PrefWindowMode, index);
            PlayerPrefs.Save();
            OnWindowModeChanged?.Invoke(index);
        }

        private void SetResolution(int index)
        {
            if (_filteredResolutions == null || index < 0 || index >= _filteredResolutions.Length) return;

            var resolution = _filteredResolutions[index];
            UnityEngine.Screen.SetResolution(resolution.width, resolution.height, UnityEngine.Screen.fullScreenMode);

            PlayerPrefs.SetInt(PrefResolutionWidth, resolution.width);
            PlayerPrefs.SetInt(PrefResolutionHeight, resolution.height);
            PlayerPrefs.Save();

            OnResolutionChanged?.Invoke(resolution.width, resolution.height);
        }

        private void SetVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(PrefVSync, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnVSyncChanged?.Invoke(enabled);
        }

        private void SetFrameRate(int index)
        {
            if (index < 0 || index >= FrameRateOptions.Length) return;

            int frameRate = FrameRateOptions[index];
            Application.targetFrameRate = frameRate;

            PlayerPrefs.SetInt(PrefFrameRate, frameRate);
            PlayerPrefs.Save();

            OnFrameRateChanged?.Invoke(frameRate);
        }

        private static int IndexOfFrameRate(int frameRate)
        {
            if (frameRate <= 0) return FrameRateOptions.Length - 1; // Unlimited

            int bestIndex = 0;
            int bestDelta = int.MaxValue;

            for (int i = 0; i < FrameRateOptions.Length; i++)
            {
                if (FrameRateOptions[i] <= 0) continue;

                int delta = Mathf.Abs(FrameRateOptions[i] - frameRate);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        // ── Audio setters ────────────────────────────────────────────────────

        private void SetVolume(string prefKey, string mixerParam, float value)
        {
            PlayerPrefs.SetFloat(prefKey, value);
            PlayerPrefs.Save();

            ApplyVolume(mixerParam, value);
            OnVolumeChanged?.Invoke(mixerParam, value);
        }

        private void ApplyVolume(string mixerParam, float linear)
        {
            if (audioMixer != null)
            {
                float decibels = linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp(linear, 0.0001f, 1f)) * 20f;
                audioMixer.SetFloat(mixerParam, decibels);
            }
            else if (mixerParam == MixerParamMaster)
            {
                AudioListener.volume = linear;
            }
        }

        // ── Reset ────────────────────────────────────────────────────────────

        /// <summary>Restore every option to its default value (rebinds are reset separately).</summary>
        public void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PrefSensitivity);
            PlayerPrefs.DeleteKey(PrefInvertY);
            PlayerPrefs.DeleteKey(PrefQuality);
            PlayerPrefs.DeleteKey(PrefWindowMode);
            PlayerPrefs.DeleteKey(PrefResolutionWidth);
            PlayerPrefs.DeleteKey(PrefResolutionHeight);
            PlayerPrefs.DeleteKey(PrefVSync);
            PlayerPrefs.DeleteKey(PrefFrameRate);
            PlayerPrefs.DeleteKey(PrefVolumeMaster);
            PlayerPrefs.DeleteKey(PrefVolumeMusic);
            PlayerPrefs.DeleteKey(PrefVolumeSFX);
            PlayerPrefs.DeleteKey(PrefVolumeDialog);
            PlayerPrefs.Save();

            LoadAllSettings();
            BroadcastCurrentSettings();
        }

        /// <summary>Reset every rebind row under this screen to its default binding.</summary>
        public void ResetAllBindings()
        {
            for (int i = 0; i < _rebindRows.Count; i++)
                _rebindRows[i]?.ResetToDefault();

            SaveInputRebindOverrides();
        }

        // ── Input rebinding persistence ──────────────────────────────────────

        private void CacheRebindRows()
        {
            _rebindRows.Clear();
            _rebindAssets.Clear();

            var rows = GetComponentsInChildren<InputRebindRowUI>(true);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                _rebindRows.Add(row);

                var asset = row.ActionAsset;
                if (asset != null && !_rebindAssets.Contains(asset))
                    _rebindAssets.Add(asset);
            }
        }

        /// <summary>Refresh the displayed binding of every row under this screen.</summary>
        public void RefreshRebindRows()
        {
            for (int i = 0; i < _rebindRows.Count; i++)
                _rebindRows[i]?.UpdateDisplay();
        }

        private void HandleInputActionChange(object changedObject, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged || _isLoadingInputRebinds) return;
            SaveInputRebindOverrides();
        }

        private void SaveInputRebindOverrides()
        {
            if (_rebindAssets.Count == 0) return;

            var saveData = new InputRebindSaveData();
            for (int i = 0; i < _rebindAssets.Count; i++)
            {
                var asset = _rebindAssets[i];
                if (asset == null) continue;

                saveData.entries.Add(new InputRebindEntry
                {
                    assetName = asset.name,
                    overridesJson = asset.SaveBindingOverridesAsJson()
                });
            }

            PlayerPrefs.SetString(PrefInputRebinds, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }

        private void LoadInputRebindOverrides()
        {
            if (_rebindAssets.Count == 0) return;

            var json = PlayerPrefs.GetString(PrefInputRebinds, string.Empty);

            _isLoadingInputRebinds = true;
            try
            {
                for (int i = 0; i < _rebindAssets.Count; i++)
                    _rebindAssets[i]?.RemoveAllBindingOverrides();

                if (string.IsNullOrWhiteSpace(json)) return;

                var saveData = JsonUtility.FromJson<InputRebindSaveData>(json);
                if (saveData?.entries == null) return;

                for (int i = 0; i < _rebindAssets.Count; i++)
                {
                    var asset = _rebindAssets[i];
                    if (asset == null) continue;

                    for (int j = 0; j < saveData.entries.Count; j++)
                    {
                        var entry = saveData.entries[j];
                        if (entry == null || !string.Equals(entry.assetName, asset.name, StringComparison.Ordinal))
                            continue;

                        if (!string.IsNullOrWhiteSpace(entry.overridesJson))
                            asset.LoadBindingOverridesFromJson(entry.overridesJson);

                        break;
                    }
                }
            }
            finally
            {
                _isLoadingInputRebinds = false;
            }
        }

        // ── UIScreen overrides ───────────────────────────────────────────────

        public override int BackPriority => 60;

        public override bool OnBack()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }
    }
}
