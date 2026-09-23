using MHZE.BasicMenus;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MHZE.BasicMenus.Editor
{
    /// <summary>
    /// Builds fully wired, art-free menu hierarchies in the open scene.
    /// Every control is plain uGUI with solid colors, so a designer can restyle
    /// or replace it without touching any code or package asset.
    /// </summary>
    public static class BasicMenusSetupWizard
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static readonly Color CanvasBackgroundColor = new Color(0.07f, 0.08f, 0.10f, 0.95f);
        private static readonly Color PanelBackgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color ControlColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        private static readonly Color AccentColor = new Color(0.35f, 0.70f, 1.00f, 1f);
        private static readonly Color MutedTextColor = new Color(0.75f, 0.78f, 0.85f, 1f);

        private static bool _warnedAboutTmpEssentials;

        // ── Menu items ───────────────────────────────────────────────────────

        [MenuItem("Tools/Basic Menus/Create Main Menu", false, 10)]
        public static void CreateMainMenu()
        {
            if (FocusExistingIfPresent<MainMenuScreen>("main menu")) return;

            Undo.SetCurrentGroupName("Create Basic Main Menu");
            int group = Undo.GetCurrentGroup();

            EnsureEventSystem();
            EnsureSystemObject();

            RectTransform canvas = CreateCanvas("MainMenuCanvas", 10);
            CreateBackground(canvas, CanvasBackgroundColor);

            RectTransform menuRoot = CreateUIObject("MainMenu", canvas);
            Stretch(menuRoot);
            var mainMenu = Undo.AddComponent<MainMenuScreen>(menuRoot.gameObject);

            RectTransform holder = CreateUIObject("ButtonsHolder", menuRoot);
            holder.anchorMin = new Vector2(0f, 0.5f);
            holder.anchorMax = new Vector2(0f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = new Vector2(360f, 0f);
            holder.sizeDelta = new Vector2(360f, 460f);
            AddVerticalLayout(holder, TextAnchor.MiddleCenter, 14f);

            TMP_Text title = CreateText(holder, "Title", "GAME TITLE", 42f, TextAlignmentOptions.Center, Color.white);
            SetLayout(title.gameObject, preferredHeight: 70f);

            Button playButton = CreateMenuButton(holder, "PlayButton", "Play");
            Button continueButton = CreateMenuButton(holder, "ContinueButton", "Continue");
            Button optionsButton = CreateMenuButton(holder, "OptionsButton", "Options");
            Button infoButton = CreateMenuButton(holder, "InfoButton", "Info");
            Button quitButton = CreateMenuButton(holder, "QuitButton", "Quit");

            OptionsScreen optionsScreen = CreateOptionsPanel(menuRoot, "OptionsPanel", true);
            InfoScreen infoScreen = CreateInfoPanel(menuRoot, "InfoPanel", true);

            var serialized = new SerializedObject(mainMenu);
            serialized.FindProperty("playButton").objectReferenceValue = playButton;
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            serialized.FindProperty("infoButton").objectReferenceValue = infoButton;
            serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
            serialized.FindProperty("optionsScreen").objectReferenceValue = optionsScreen;
            serialized.FindProperty("infoScreen").objectReferenceValue = infoScreen;
            serialized.FindProperty("mainButtonsHolder").objectReferenceValue = holder;
            serialized.FindProperty("defaultSelectable").objectReferenceValue = playButton;
            serialized.FindProperty("startHidden").boolValue = false;
            serialized.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = mainMenu.gameObject;
            Debug.Log("[Basic Menus] Main menu created. Wire the Play button's onPlayClicked event to your scene loading logic. Delete the Continue button if the game has no save system.");
        }

        [MenuItem("Tools/Basic Menus/Create Pause Menu", false, 11)]
        public static void CreatePauseMenu()
        {
            if (FocusExistingIfPresent<PauseScreen>("pause menu")) return;

            Undo.SetCurrentGroupName("Create Basic Pause Menu");
            int group = Undo.GetCurrentGroup();

            EnsureEventSystem();
            EnsureSystemObject();

            RectTransform canvas = CreateCanvas("PauseMenuCanvas", 20);

            RectTransform pauseRoot = CreateUIObject("PauseMenu", canvas);
            Stretch(pauseRoot);
            var pauseScreen = Undo.AddComponent<PauseScreen>(pauseRoot.gameObject);

            RectTransform panel = CreateUIObject("PausePanel", pauseRoot);
            Stretch(panel);
            var panelBackground = Undo.AddComponent<Image>(panel.gameObject);
            panelBackground.color = CanvasBackgroundColor;

            RectTransform holder = CreateUIObject("ButtonsHolder", panel);
            holder.anchorMin = new Vector2(0.5f, 0.5f);
            holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = Vector2.zero;
            holder.sizeDelta = new Vector2(360f, 480f);
            AddVerticalLayout(holder, TextAnchor.MiddleCenter, 14f);

            TMP_Text title = CreateText(holder, "Title", "PAUSED", 42f, TextAlignmentOptions.Center, Color.white);
            SetLayout(title.gameObject, preferredHeight: 70f);

            Button resumeButton = CreateMenuButton(holder, "ResumeButton", "Resume");
            Button restartButton = CreateMenuButton(holder, "RestartButton", "Restart");
            Button optionsButton = CreateMenuButton(holder, "OptionsButton", "Options");
            Button mainMenuButton = CreateMenuButton(holder, "MainMenuButton", "Main Menu");
            Button quitButton = CreateMenuButton(holder, "QuitButton", "Quit");

            OptionsScreen optionsScreen = CreateOptionsPanel(pauseRoot, "OptionsPanel", true);

            var serialized = new SerializedObject(pauseScreen);
            serialized.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serialized.FindProperty("restartButton").objectReferenceValue = restartButton;
            serialized.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
            serialized.FindProperty("optionsScreen").objectReferenceValue = optionsScreen;
            serialized.FindProperty("contentPanel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("defaultSelectable").objectReferenceValue = resumeButton;
            serialized.FindProperty("startHidden").boolValue = false;

            string firstBuildScene = GetFirstEnabledBuildSceneName();
            if (!string.IsNullOrEmpty(firstBuildScene))
                serialized.FindProperty("mainMenuSceneName").stringValue = firstBuildScene;

            serialized.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = pauseScreen.gameObject;
            Debug.Log("[Basic Menus] Pause menu created. It opens with a project-wide Pause action if one exists, otherwise Escape / gamepad B. Assign a Pause action on the PauseScreen to customise. Verify the main menu scene name on the PauseScreen component.");
        }

        [MenuItem("Tools/Basic Menus/Create Options Panel", false, 12)]
        public static void CreateOptionsPanelMenuItem()
        {
            Undo.SetCurrentGroupName("Create Basic Options Panel");
            int group = Undo.GetCurrentGroup();

            EnsureEventSystem();

            RectTransform canvas = CreateCanvas("OptionsCanvas", 30);
            CreateBackground(canvas, CanvasBackgroundColor);

            OptionsScreen optionsScreen = CreateOptionsPanel(canvas, "OptionsPanel", false);

            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = optionsScreen.gameObject;
            Debug.Log("[Basic Menus] Options panel created. Open it from any script through its UIScreen.Open() method (or assign it to a MainMenuScreen / PauseScreen). Import TMP Essential Resources if the text is not visible.");
        }

        [MenuItem("Tools/Basic Menus/Add System Object", false, 30)]
        public static void AddSystemObject()
        {
            Undo.SetCurrentGroupName("Add Basic Menus System Object");
            int group = Undo.GetCurrentGroup();

            EnsureEventSystem();
            UIInputModeDetector detector = EnsureSystemObject();

            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = detector.gameObject;
            Debug.Log("[Basic Menus] System object added (input mode detector + back router). It persists across scenes automatically, so it only needs to exist in the first scene.");
        }

        // ── Shared builders ──────────────────────────────────────────────────

        private static bool FocusExistingIfPresent<T>(string label) where T : Component
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<T>();
            if (existing == null) return false;

            bool selectExisting = EditorUtility.DisplayDialog(
                "Basic Menus",
                $"This scene already contains a {label}. Select the existing one instead of creating another?",
                "Select Existing",
                "Create Another");

            if (!selectExisting) return false;

            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return true;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var gameObject = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create EventSystem");

            var inputModule = Undo.AddComponent<InputSystemUIInputModule>(gameObject);

            var projectWideActions = UnityEngine.InputSystem.InputSystem.actions;
            if (projectWideActions != null)
                inputModule.actionsAsset = projectWideActions;
        }

        private static UIInputModeDetector EnsureSystemObject()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<UIInputModeDetector>();
            if (existing != null) return existing;

            var gameObject = new GameObject("[Basic Menus System]");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Basic Menus System");

            var detector = Undo.AddComponent<UIInputModeDetector>(gameObject);
            Undo.AddComponent<UIBackRouter>(gameObject);
            return detector;
        }

        private static OptionsScreen CreateOptionsPanel(Transform parent, string name, bool startInactive)
        {
            RectTransform panel = CreateUIObject(name, parent);
            Stretch(panel);
            var background = Undo.AddComponent<Image>(panel.gameObject);
            background.color = PanelBackgroundColor;

            var options = Undo.AddComponent<OptionsScreen>(panel.gameObject);

            TMP_Text title = CreateText(panel, "Title", "OPTIONS", 40f, TextAlignmentOptions.Center, Color.white);
            AnchorTop(title.rectTransform, 64f, 24f);

            // Tab bar
            RectTransform tabBar = CreateUIObject("TabBar", panel);
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.anchoredPosition = new Vector2(0f, -96f);
            tabBar.sizeDelta = new Vector2(0f, 52f);

            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;

            Button gameplayTab = CreateButton(tabBar, "GameplayTabButton", "Gameplay", new Vector2(190f, 46f));
            Button videoTab = CreateButton(tabBar, "VideoTabButton", "Video", new Vector2(190f, 46f));
            Button audioTab = CreateButton(tabBar, "AudioTabButton", "Audio", new Vector2(190f, 46f));
            Button controlsTab = CreateButton(tabBar, "ControlsTabButton", "Controls", new Vector2(190f, 46f));

            // Content area
            RectTransform contentArea = CreateUIObject("ContentArea", panel);
            contentArea.anchorMin = Vector2.zero;
            contentArea.anchorMax = Vector2.one;
            contentArea.offsetMin = new Vector2(140f, 110f);
            contentArea.offsetMax = new Vector2(-140f, -164f);

            RectTransform gameplayContent = CreateTabContent(contentArea, "GameplayContent");
            Slider sensitivitySlider = CreateSliderRow(gameplayContent, "Row_Sensitivity", "Mouse Sensitivity", 0.1f, 10f, 1f, out TMP_Text sensitivityValueText);
            Toggle invertYToggle = CreateToggleRow(gameplayContent, "Row_InvertY", "Invert Y Axis", false);

            RectTransform videoContent = CreateTabContent(contentArea, "VideoContent");
            TMP_Dropdown qualityDropdown = CreateDropdownRow(videoContent, "Row_Quality", "Quality");
            TMP_Dropdown windowModeDropdown = CreateDropdownRow(videoContent, "Row_WindowMode", "Window Mode");
            TMP_Dropdown resolutionDropdown = CreateDropdownRow(videoContent, "Row_Resolution", "Resolution");
            TMP_Dropdown frameRateDropdown = CreateDropdownRow(videoContent, "Row_FrameRate", "Frame Rate");
            Toggle vsyncToggle = CreateToggleRow(videoContent, "Row_VSync", "VSync", true);

            RectTransform audioContent = CreateTabContent(contentArea, "AudioContent");
            Slider masterVolumeSlider = CreateSliderRow(audioContent, "Row_MasterVolume", "Master Volume", 0f, 1f, 0.75f, out _);
            Slider musicVolumeSlider = CreateSliderRow(audioContent, "Row_MusicVolume", "Music Volume", 0f, 1f, 0.75f, out _);
            Slider sfxVolumeSlider = CreateSliderRow(audioContent, "Row_SfxVolume", "SFX Volume", 0f, 1f, 0.75f, out _);
            Slider dialogVolumeSlider = CreateSliderRow(audioContent, "Row_DialogVolume", "Dialog Volume", 0f, 1f, 0.75f, out _);

            RectTransform controlsContent = CreateTabContent(contentArea, "ControlsContent");
            TMP_Text controlsNote = CreateText(controlsContent, "Note", "Duplicate the example row once per rebindable action, then assign an InputActionReference on each row.", 18f, TextAlignmentOptions.Left, MutedTextColor);
            SetLayout(controlsNote.gameObject, preferredHeight: 46f);

            GameObject rebindOverlay = CreateRebindOverlay(panel, out TMP_Text rebindOverlayText);
            CreateRebindRow(controlsContent, "Row_ExampleAction", "Example Action", rebindOverlay, rebindOverlayText);

            Button resetAllBindingsButton = CreateButton(controlsContent, "ResetAllBindingsButton", "Reset All Bindings", new Vector2(280f, 44f));
            SetLayout(resetAllBindingsButton.gameObject, preferredHeight: 44f);

            // Footer
            Button resetToDefaultsButton = CreateButton(panel, "ResetToDefaultsButton", "Reset to Defaults", new Vector2(300f, 52f));
            AnchorBottomLeft(resetToDefaultsButton.GetComponent<RectTransform>(), 60f, 40f);

            Button backButton = CreateButton(panel, "BackButton", "Back", new Vector2(200f, 52f));
            AnchorBottomRight(backButton.GetComponent<RectTransform>(), 60f, 40f);

            // Keep the overlay above every other element of the panel.
            rebindOverlay.transform.SetAsLastSibling();

            // Wiring
            var serialized = new SerializedObject(options);

            SerializedProperty tabsProperty = serialized.FindProperty("tabs");
            tabsProperty.arraySize = 4;
            ConfigureTab(tabsProperty.GetArrayElementAtIndex(0), "Gameplay", gameplayTab, gameplayContent.gameObject);
            ConfigureTab(tabsProperty.GetArrayElementAtIndex(1), "Video", videoTab, videoContent.gameObject);
            ConfigureTab(tabsProperty.GetArrayElementAtIndex(2), "Audio", audioTab, audioContent.gameObject);
            ConfigureTab(tabsProperty.GetArrayElementAtIndex(3), "Controls", controlsTab, controlsContent.gameObject);

            serialized.FindProperty("sensitivitySlider").objectReferenceValue = sensitivitySlider;
            serialized.FindProperty("sensitivityValueText").objectReferenceValue = sensitivityValueText;
            serialized.FindProperty("invertYToggle").objectReferenceValue = invertYToggle;

            serialized.FindProperty("qualityDropdown").objectReferenceValue = qualityDropdown;
            serialized.FindProperty("windowModeDropdown").objectReferenceValue = windowModeDropdown;
            serialized.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
            serialized.FindProperty("vsyncToggle").objectReferenceValue = vsyncToggle;
            serialized.FindProperty("frameRateDropdown").objectReferenceValue = frameRateDropdown;

            serialized.FindProperty("masterVolumeSlider").objectReferenceValue = masterVolumeSlider;
            serialized.FindProperty("musicVolumeSlider").objectReferenceValue = musicVolumeSlider;
            serialized.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxVolumeSlider;
            serialized.FindProperty("dialogVolumeSlider").objectReferenceValue = dialogVolumeSlider;

            serialized.FindProperty("resetAllBindingsButton").objectReferenceValue = resetAllBindingsButton;
            serialized.FindProperty("resetToDefaultsButton").objectReferenceValue = resetToDefaultsButton;
            serialized.FindProperty("backButton").objectReferenceValue = backButton;
            serialized.FindProperty("defaultSelectable").objectReferenceValue = backButton;
            serialized.FindProperty("startHidden").boolValue = true;
            serialized.ApplyModifiedProperties();

            if (startInactive)
                panel.gameObject.SetActive(false);
            else
                gameplayContent.gameObject.SetActive(true);

            return options;
        }

        private static InfoScreen CreateInfoPanel(Transform parent, string name, bool startInactive)
        {
            RectTransform panel = CreateUIObject(name, parent);
            Stretch(panel);
            var background = Undo.AddComponent<Image>(panel.gameObject);
            background.color = PanelBackgroundColor;

            var info = Undo.AddComponent<InfoScreen>(panel.gameObject);

            TMP_Text title = CreateText(panel, "Title", "ABOUT", 40f, TextAlignmentOptions.Center, Color.white);
            AnchorTop(title.rectTransform, 60f, 40f);

            TMP_Text body = CreateText(panel, "Body", "Add your credits, version and other information here.", 22f, TextAlignmentOptions.Top, MutedTextColor);
            Stretch(body.rectTransform, 240f, 180f, 240f, 160f);

            Button backButton = CreateButton(panel, "BackButton", "Back", new Vector2(220f, 54f));
            AnchorBottom(backButton.GetComponent<RectTransform>(), 0f, 60f);

            var serialized = new SerializedObject(info);
            serialized.FindProperty("backButton").objectReferenceValue = backButton;
            serialized.FindProperty("defaultSelectable").objectReferenceValue = backButton;
            serialized.FindProperty("startHidden").boolValue = true;
            serialized.ApplyModifiedProperties();

            if (startInactive)
                panel.gameObject.SetActive(false);

            return info;
        }

        // ── Row builders ─────────────────────────────────────────────────────

        private static Slider CreateSliderRow(Transform parent, string name, string label, float min, float max, float value, out TMP_Text valueText)
        {
            RectTransform row = CreateRow(parent, name, 40f);

            TMP_Text labelText = CreateText(row, "Label", label, 20f, TextAlignmentOptions.Left, Color.white);
            SetLayout(labelText.gameObject, minWidth: 200f, flexibleWidth: 1f, preferredHeight: 28f);

            Slider slider = CreateSlider(row, min, max, value);
            SetLayout(slider.gameObject, preferredWidth: 320f, preferredHeight: 24f);

            valueText = CreateText(row, "Value", value.ToString("F1"), 20f, TextAlignmentOptions.Right, MutedTextColor);
            SetLayout(valueText.gameObject, preferredWidth: 64f, preferredHeight: 28f);

            return slider;
        }

        private static Toggle CreateToggleRow(Transform parent, string name, string label, bool isOn)
        {
            RectTransform row = CreateRow(parent, name, 40f);

            TMP_Text labelText = CreateText(row, "Label", label, 20f, TextAlignmentOptions.Left, Color.white);
            SetLayout(labelText.gameObject, minWidth: 200f, flexibleWidth: 1f, preferredHeight: 28f);

            Toggle toggle = CreateToggle(row, isOn);
            SetLayout(toggle.gameObject, preferredWidth: 30f, preferredHeight: 30f);

            return toggle;
        }

        private static TMP_Dropdown CreateDropdownRow(Transform parent, string name, string label)
        {
            RectTransform row = CreateRow(parent, name, 46f);

            TMP_Text labelText = CreateText(row, "Label", label, 20f, TextAlignmentOptions.Left, Color.white);
            SetLayout(labelText.gameObject, minWidth: 200f, flexibleWidth: 1f, preferredHeight: 28f);

            TMP_Dropdown dropdown = CreateDropdown(row);
            SetLayout(dropdown.gameObject, preferredWidth: 360f, preferredHeight: 44f);

            return dropdown;
        }

        private static InputRebindRowUI CreateRebindRow(Transform parent, string name, string displayName, GameObject overlay, TMP_Text overlayText)
        {
            RectTransform row = CreateRow(parent, name, 52f);

            TMP_Text actionLabel = CreateText(row, "ActionName", displayName, 20f, TextAlignmentOptions.Left, Color.white);
            SetLayout(actionLabel.gameObject, minWidth: 200f, flexibleWidth: 1f, preferredHeight: 28f);

            TMP_Text bindingLabel = CreateText(row, "Binding", "-", 20f, TextAlignmentOptions.Center, MutedTextColor);
            SetLayout(bindingLabel.gameObject, preferredWidth: 180f, preferredHeight: 28f);

            Button rebindButton = CreateButton(row, "RebindButton", "Rebind", new Vector2(130f, 40f));
            SetLayout(rebindButton.gameObject, preferredWidth: 130f, preferredHeight: 40f);

            Button resetButton = CreateButton(row, "ResetButton", "Reset", new Vector2(110f, 40f));
            SetLayout(resetButton.gameObject, preferredWidth: 110f, preferredHeight: 40f);

            var rebindRow = Undo.AddComponent<InputRebindRowUI>(row.gameObject);

            var serialized = new SerializedObject(rebindRow);
            serialized.FindProperty("actionDisplayName").stringValue = displayName;
            serialized.FindProperty("actionNameText").objectReferenceValue = actionLabel;
            serialized.FindProperty("bindingText").objectReferenceValue = bindingLabel;
            serialized.FindProperty("rebindButton").objectReferenceValue = rebindButton;
            serialized.FindProperty("resetButton").objectReferenceValue = resetButton;
            serialized.FindProperty("rebindOverlay").objectReferenceValue = overlay;
            serialized.FindProperty("rebindPromptText").objectReferenceValue = overlayText;
            serialized.ApplyModifiedProperties();

            return rebindRow;
        }

        private static GameObject CreateRebindOverlay(Transform parent, out TMP_Text promptText)
        {
            RectTransform overlay = CreateUIObject("RebindOverlay", parent);
            Stretch(overlay);
            var background = Undo.AddComponent<Image>(overlay.gameObject);
            background.color = new Color(0f, 0f, 0f, 0.9f);

            promptText = CreateText(overlay, "Prompt", "Waiting for input...", 28f, TextAlignmentOptions.Center, Color.white);
            Stretch(promptText.rectTransform, 80f, 60f, 80f, 60f);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        // ── Primitive UI builders ────────────────────────────────────────────

        private static RectTransform CreateCanvas(string name, int sortingOrder)
        {
            var gameObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);

            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)gameObject.transform;
        }

        private static void CreateBackground(RectTransform canvas, Color color)
        {
            RectTransform background = CreateUIObject("Background", canvas);
            Stretch(background);

            var image = Undo.AddComponent<Image>(background.gameObject);
            image.color = color;
        }

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);

            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            RectTransform rect = CreateUIObject(name, parent);

            var text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;

            var fontAsset = ResolveDefaultFontAsset();
            if (fontAsset != null)
                text.font = fontAsset;

            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            RectTransform rect = CreateUIObject(name, parent);
            rect.sizeDelta = size;

            var image = Undo.AddComponent<Image>(rect.gameObject);
            image.color = ButtonColor;

            var button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = image;

            TMP_Text text = CreateText(rect, "Label", label, 22f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, 8f, 0f, 8f, 0f);

            return button;
        }

        private static Button CreateMenuButton(Transform parent, string name, string label)
        {
            Button button = CreateButton(parent, name, label, new Vector2(340f, 56f));
            SetLayout(button.gameObject, preferredHeight: 56f);
            return button;
        }

        private static Slider CreateSlider(Transform parent, float min, float max, float value)
        {
            RectTransform root = CreateUIObject("Slider", parent);
            root.sizeDelta = new Vector2(320f, 24f);

            var slider = Undo.AddComponent<Slider>(root.gameObject);
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.direction = Slider.Direction.LeftToRight;

            RectTransform background = CreateUIObject("Background", root);
            Stretch(background);
            var backgroundImage = Undo.AddComponent<Image>(background.gameObject);
            backgroundImage.color = ControlColor;

            RectTransform fillArea = CreateUIObject("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(6f, 0f);
            fillArea.offsetMax = new Vector2(-6f, 0f);

            RectTransform fill = CreateUIObject("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = Undo.AddComponent<Image>(fill.gameObject);
            fillImage.color = AccentColor;

            RectTransform handleArea = CreateUIObject("Handle Slide Area", root);
            Stretch(handleArea, 10f, 0f, 10f, 0f);

            RectTransform handle = CreateUIObject("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(20f, 0f);
            var handleImage = Undo.AddComponent<Image>(handle.gameObject);
            handleImage.color = Color.white;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;

            return slider;
        }

        private static Toggle CreateToggle(Transform parent, bool isOn)
        {
            RectTransform root = CreateUIObject("Toggle", parent);
            root.sizeDelta = new Vector2(30f, 30f);

            var toggle = Undo.AddComponent<Toggle>(root.gameObject);

            RectTransform background = CreateUIObject("Background", root);
            Stretch(background);
            var backgroundImage = Undo.AddComponent<Image>(background.gameObject);
            backgroundImage.color = ControlColor;

            RectTransform checkmark = CreateUIObject("Checkmark", background);
            Stretch(checkmark, 6f, 6f, 6f, 6f);
            var checkmarkImage = Undo.AddComponent<Image>(checkmark.gameObject);
            checkmarkImage.color = AccentColor;

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = isOn;

            return toggle;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent)
        {
            RectTransform root = CreateUIObject("Dropdown", parent);
            root.sizeDelta = new Vector2(360f, 44f);

            var background = Undo.AddComponent<Image>(root.gameObject);
            background.color = ControlColor;

            var dropdown = Undo.AddComponent<TMP_Dropdown>(root.gameObject);
            dropdown.targetGraphic = background;

            TMP_Text caption = CreateText(root, "Label", "Option", 18f, TextAlignmentOptions.Left, Color.white);
            Stretch(caption.rectTransform, 12f, 0f, 30f, 0f);

            RectTransform arrow = CreateUIObject("Arrow", root);
            arrow.anchorMin = new Vector2(1f, 0.5f);
            arrow.anchorMax = new Vector2(1f, 0.5f);
            arrow.pivot = new Vector2(1f, 0.5f);
            arrow.anchoredPosition = new Vector2(-12f, 0f);
            arrow.sizeDelta = new Vector2(18f, 18f);
            var arrowImage = Undo.AddComponent<Image>(arrow.gameObject);
            arrowImage.color = MutedTextColor;

            // TMP_Dropdown adds Canvas / GraphicRaycaster / CanvasGroup to the
            // template automatically; the hierarchy below mirrors the standard
            // uGUI dropdown so cloning and scrolling work out of the box.
            RectTransform template = CreateUIObject("Template", root);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 160f);

            var templateBackground = Undo.AddComponent<Image>(template.gameObject);
            templateBackground.color = new Color(0.08f, 0.09f, 0.11f, 0.98f);

            var scrollRect = Undo.AddComponent<ScrollRect>(template.gameObject);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            RectTransform viewport = CreateUIObject("Viewport", template);
            Stretch(viewport, 4f, 4f, 4f, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateUIObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 36f);

            RectTransform item = CreateUIObject("Item", content);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.anchoredPosition = Vector2.zero;
            item.sizeDelta = new Vector2(0f, 36f);

            var itemBackground = Undo.AddComponent<Image>(item.gameObject);
            itemBackground.color = new Color(1f, 1f, 1f, 0.06f);

            var itemToggle = Undo.AddComponent<Toggle>(item.gameObject);
            itemToggle.targetGraphic = itemBackground;

            RectTransform checkmark = CreateUIObject("Item Checkmark", item);
            checkmark.anchorMin = new Vector2(0f, 0.5f);
            checkmark.anchorMax = new Vector2(0f, 0.5f);
            checkmark.pivot = new Vector2(0.5f, 0.5f);
            checkmark.anchoredPosition = new Vector2(16f, 0f);
            checkmark.sizeDelta = new Vector2(16f, 16f);
            var checkmarkImage = Undo.AddComponent<Image>(checkmark.gameObject);
            checkmarkImage.color = AccentColor;
            itemToggle.graphic = checkmarkImage;
            itemToggle.isOn = true;

            TMP_Text itemLabel = CreateText(item, "Item Label", "Option", 18f, TextAlignmentOptions.Left, Color.white);
            Stretch(itemLabel.rectTransform, 32f, 0f, 8f, 0f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.template = template;

            template.gameObject.SetActive(false);

            return dropdown;
        }

        // ── Layout helpers ───────────────────────────────────────────────────

        private static RectTransform CreateTabContent(Transform parent, string name)
        {
            RectTransform content = CreateUIObject(name, parent);
            Stretch(content);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.gameObject.SetActive(false);
            return content;
        }

        private static RectTransform CreateRow(Transform parent, string name, float height)
        {
            RectTransform row = CreateUIObject(name, parent);
            SetLayout(row.gameObject, flexibleWidth: 1f, preferredHeight: height);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return row;
        }

        private static void AddVerticalLayout(RectTransform target, TextAnchor alignment, float spacing)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void SetLayout(GameObject gameObject, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f, float minWidth = -1f, float minHeight = -1f)
        {
            var element = gameObject.GetComponent<LayoutElement>();
            if (element == null) element = Undo.AddComponent<LayoutElement>(gameObject);

            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
            if (minWidth >= 0f) element.minWidth = minWidth;
            if (minHeight >= 0f) element.minHeight = minHeight;
        }

        private static void ConfigureTab(SerializedProperty tabProperty, string name, Button button, GameObject content)
        {
            tabProperty.FindPropertyRelative("name").stringValue = name;
            tabProperty.FindPropertyRelative("button").objectReferenceValue = button;
            tabProperty.FindPropertyRelative("contentPanel").objectReferenceValue = content;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void AnchorTop(RectTransform rect, float height, float topOffset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void AnchorBottom(RectTransform rect, float x, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AnchorBottomLeft(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AnchorBottomRight(RectTransform rect, float x, float y)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-x, y);
        }

        private static string GetFirstEnabledBuildSceneName()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                    return System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
            }

            return string.Empty;
        }

        private static TMP_FontAsset ResolveDefaultFontAsset()
        {
            TMP_FontAsset fontAsset = null;

            try
            {
                fontAsset = TMP_Settings.defaultFontAsset;
            }
            catch
            {
                // TMP settings asset is missing; the warning below covers it.
            }

            if (fontAsset == null && !_warnedAboutTmpEssentials)
            {
                _warnedAboutTmpEssentials = true;
                Debug.LogWarning("[Basic Menus] TextMeshPro essential resources were not found. Import them from Window > TextMeshPro > Import TMP Essential Resources so the generated text renders.");
            }

            return fontAsset;
        }
    }
}
