// Central controller that manages all input prompts on screen. It detects whether the player is using keyboard+mouse or a gamepad, looks up the right binding icon from the icon library, and coordinates prompt visibility. UI management (pool, anchors, views) is delegated to InputPromptUI. Supports both predefined prompts from a collection and fully custom ones with your own text and icon. Automatically refreshes everything when the player switches input devices.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InputPromptSystem
{
public class InputPromptManager : MonoBehaviour
{
    public enum DeviceType
    {
        KeyboardMouse,
        Gamepad
    }

    private class ActivePrompt
    {
        public string PromptId;
        public InputPromptDefinition Definition;
        public InputPromptView View;
        public bool HasTextOverride;
        public string OverridePrefix;
        public string OverrideSuffix;
        public bool IsCustom;
        public string CustomPrefix;
        public string CustomSuffix;
        public Sprite CustomIcon;
    }

    [Header("Data")]
    [SerializeField] private InputPromptCollection inputPromptCollection;
    [SerializeField] private InputBindingIconLibrary bindingIconLibrary;

    [Header("UI")]
    [SerializeField] private InputPromptUI promptUI;

    [Header("Settings")]
    [SerializeField] private bool promptSystemEnabled = true;

    private readonly Dictionary<string, ActivePrompt> activePrompts = new();

    private DeviceType currentDevice = DeviceType.KeyboardMouse;

    public bool PromptSystemEnabled => promptSystemEnabled;

    private void OnEnable()
    {
        UnityEngine.InputSystem.InputSystem.onActionChange += HandleActionChange;
    }

    private void OnDisable()
    {
        UnityEngine.InputSystem.InputSystem.onActionChange -= HandleActionChange;
    }

    public void ShowPrompt(string promptKey)
    {
        ShowPrompt(promptKey, promptKey);
    }

    public void ShowPrompt(string promptKey, string promptId)
    {
        ShowPrompt(promptKey, promptId, string.Empty, string.Empty, false);
    }

    public void ShowPrompt(string promptKey, string promptId, string overridePrefix, string overrideSuffix)
    {
        ShowPrompt(promptKey, promptId, overridePrefix, overrideSuffix, true);
    }

    private void ShowPrompt(string promptKey, string promptId, string overridePrefix, string overrideSuffix, bool hasTextOverride)
    {
        if (!promptSystemEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(promptKey) || string.IsNullOrWhiteSpace(promptId))
        {
            Debug.LogWarning("[InputPromptManager] Prompt key and prompt id are required.");
            return;
        }

        if (inputPromptCollection == null)
        {
            Debug.LogWarning("[InputPromptManager] InputPromptCollection is not assigned.");
            return;
        }

        if (!inputPromptCollection.TryGetPrompt(promptKey, out var definition) || definition == null)
        {
            Debug.LogWarning($"[InputPromptManager] Prompt key '{promptKey}' was not found in InputPromptCollection.");
            return;
        }

        HidePrompt(promptId);

        var view = promptUI != null ? promptUI.GetView(definition.Location) : null;
        if (view == null)
        {
            return;
        }

        var prompt = new ActivePrompt
        {
            PromptId = promptId,
            Definition = definition,
            View = view,
            HasTextOverride = hasTextOverride,
            OverridePrefix = overridePrefix,
            OverrideSuffix = overrideSuffix
        };

        activePrompts[promptId] = prompt;
        RefreshPrompt(prompt);
    }

    public void ShowCustomPrompt(string promptId, InputPromptLocation location, string text, Sprite icon = null)
    {
        ShowCustomPrompt(promptId, location, string.Empty, text, icon);
    }

    public void ShowCustomPrompt(string promptId, InputPromptLocation location, string prefix, string suffix, Sprite icon = null)
    {
        if (!promptSystemEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(promptId))
        {
            Debug.LogWarning("[InputPromptManager] Prompt id is required.");
            return;
        }

        HidePrompt(promptId);

        var view = promptUI != null ? promptUI.GetView(location) : null;
        if (view == null)
        {
            return;
        }

        var prompt = new ActivePrompt
        {
            PromptId = promptId,
            View = view,
            IsCustom = true,
            CustomPrefix = prefix,
            CustomSuffix = suffix,
            CustomIcon = icon
        };

        activePrompts[promptId] = prompt;
        RefreshPrompt(prompt);
    }

    public void HidePrompt(string promptId)
    {
        if (string.IsNullOrWhiteSpace(promptId))
        {
            return;
        }

        if (!activePrompts.TryGetValue(promptId, out var prompt))
        {
            return;
        }

        if (promptUI != null)
        {
            promptUI.ReleaseView(prompt.View);
        }

        activePrompts.Remove(promptId);
    }

    public void HidePromptByKey(string promptKey)
    {
        HidePrompt(promptKey);
    }

    public void HideAllPrompts()
    {
        if (promptUI != null)
        {
            promptUI.ReleaseAllViews();
        }

        activePrompts.Clear();
    }

    public void SetPromptSystemEnabled(bool enabled)
    {
        promptSystemEnabled = enabled;

        if (!enabled)
        {
            HideAllPrompts();
        }
    }

    private void Update()
    {
        var detected = DetectDeviceType();
        if (detected == currentDevice)
        {
            return;
        }

        currentDevice = detected;
        RefreshAllPrompts();
    }

    private DeviceType DetectDeviceType()
    {
        if (WasKeyboardOrMouseUsed())
        {
            return DeviceType.KeyboardMouse;
        }

        if (WasGamepadUsed())
        {
            return DeviceType.Gamepad;
        }

        return currentDevice;
    }

    private static bool WasKeyboardOrMouseUsed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        var mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        return mouse.leftButton.wasPressedThisFrame
               || mouse.rightButton.wasPressedThisFrame
               || mouse.middleButton.wasPressedThisFrame
               || mouse.forwardButton.wasPressedThisFrame
               || mouse.backButton.wasPressedThisFrame
               || mouse.scroll.ReadValue().sqrMagnitude > 0.0001f;
    }

    private static bool WasGamepadUsed()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return false;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame
            || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame
            || gamepad.buttonNorth.wasPressedThisFrame
            || gamepad.leftShoulder.wasPressedThisFrame
            || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.leftTrigger.wasPressedThisFrame
            || gamepad.rightTrigger.wasPressedThisFrame
            || gamepad.startButton.wasPressedThisFrame
            || gamepad.selectButton.wasPressedThisFrame
            || gamepad.leftStickButton.wasPressedThisFrame
            || gamepad.rightStickButton.wasPressedThisFrame
            || gamepad.dpad.up.wasPressedThisFrame
            || gamepad.dpad.down.wasPressedThisFrame
            || gamepad.dpad.left.wasPressedThisFrame
            || gamepad.dpad.right.wasPressedThisFrame)
        {
            return true;
        }

        return gamepad.leftStick.ReadValue().sqrMagnitude > 0.01f
               || gamepad.rightStick.ReadValue().sqrMagnitude > 0.01f;
    }

    private void RefreshAllPrompts()
    {
        if (activePrompts.Count == 0)
        {
            return;
        }

        var snapshot = new List<ActivePrompt>(activePrompts.Values);
        for (var i = 0; i < snapshot.Count; i++)
        {
            RefreshPrompt(snapshot[i]);
        }
    }

    private void RefreshPrompt(ActivePrompt prompt)
    {
        if (prompt == null || prompt.View == null)
        {
            return;
        }

        if (prompt.IsCustom)
        {
            prompt.View.SetContent(prompt.CustomIcon, prompt.CustomPrefix, prompt.CustomSuffix);
            prompt.View.SetVisible(true);
            return;
        }

        if (prompt.Definition == null)
        {
            if (promptUI != null)
            {
                promptUI.ReleaseView(prompt.View);
            }

            if (!string.IsNullOrWhiteSpace(prompt.PromptId))
            {
                activePrompts.Remove(prompt.PromptId);
            }
            return;
        }

        var icon = ResolveIcon(prompt.Definition);
        var prefix = prompt.HasTextOverride ? prompt.OverridePrefix : prompt.Definition.PrefixText;
        var suffix = prompt.HasTextOverride ? prompt.OverrideSuffix : prompt.Definition.SuffixText;

        if (icon == null)
        {
            if (prompt.HasTextOverride)
            {
                prefix = prefix ?? string.Empty;
                suffix = suffix ?? string.Empty;
            }
            else
            {
                prefix = string.Empty;
                suffix = prompt.Definition.BuildDisplayText(currentDevice);
            }
        }

        prompt.View.SetContent(icon, prefix, suffix);
        prompt.View.SetVisible(true);
    }

    private Sprite ResolveIcon(InputPromptDefinition definition)
    {
        if (definition == null || bindingIconLibrary == null)
        {
            return null;
        }

        if (!definition.TryResolveBindingMetadata(currentDevice, out var deviceLayoutName, out var controlPath))
        {
            return null;
        }

        return bindingIconLibrary.TryGetIcon(deviceLayoutName, controlPath, out var icon)
            ? icon
            : null;
    }

    private void HandleActionChange(object changedObject, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged)
        {
            return;
        }

        if (activePrompts.Count == 0)
        {
            return;
        }

        RefreshAllPrompts();
    }
}
}
