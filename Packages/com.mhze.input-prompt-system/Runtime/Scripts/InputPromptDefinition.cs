// Defines a single prompt as a reusable asset. Stores the on-screen text (like "Press" and "to interact"), the input action it refers to, which binding group to use for keyboard+mouse vs gamepad, and where on screen it appears. Can convert the current input binding into a readable display string (e.g. "E") or extract the raw device-and-control-path info so the icon library can find the correct sprite.

using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InputPromptSystem
{
[CreateAssetMenu(fileName = "InputPromptDefinition", menuName = "Input Prompts/Input Prompt Definition")]
public class InputPromptDefinition : ScriptableObject
{
    [SerializeField] private string key;
    [SerializeField] private string prefixText = "Press";
    [SerializeField] private string suffixText = "to interact";
    [SerializeField] private InputActionReference inputAction;
    private const string KeyboardMouseBindingGroup = "Keyboard&Mouse";
    private const string GamepadBindingGroup = "Gamepad";
    [SerializeField] private InputPromptLocation location = InputPromptLocation.Center;

    public string Key => key;
    public InputPromptLocation Location => location;
    public string PrefixText => prefixText;
    public string SuffixText => suffixText;

    public string ResolveBinding(InputPromptManager.DeviceType deviceType, out string deviceLayoutName, out string controlPath)
    {
        return ResolveBindingText(deviceType, out deviceLayoutName, out controlPath);
    }

    public string BuildDisplayText(InputPromptManager.DeviceType deviceType)
    {
        var bindingText = ResolveBindingText(deviceType, out _, out _);
        return BuildDisplayText(bindingText);
    }

    public string BuildDisplayText(string bindingText)
    {
        if (string.IsNullOrWhiteSpace(bindingText))
        {
            return JoinNonEmpty(prefixText, suffixText);
        }

        return JoinNonEmpty(prefixText, bindingText, suffixText);
    }

    public bool TryResolveBindingMetadata(InputPromptManager.DeviceType deviceType, out string deviceLayoutName, out string controlPath)
    {
        _ = ResolveBinding(deviceType, out deviceLayoutName, out controlPath);
        return !string.IsNullOrWhiteSpace(controlPath);
    }

    private string ResolveBindingText(InputPromptManager.DeviceType deviceType, out string deviceLayoutName, out string controlPath)
    {
        deviceLayoutName = string.Empty;
        controlPath = string.Empty;

        if (inputAction == null || inputAction.action == null)
        {
            return string.Empty;
        }

        var action = inputAction.action;
        var group = deviceType == InputPromptManager.DeviceType.Gamepad
            ? GamepadBindingGroup
            : KeyboardMouseBindingGroup;

        var bindingIndex = FindBindingIndex(action, group);
        if (bindingIndex >= 0)
        {
            return action.GetBindingDisplayString(
                bindingIndex,
                out deviceLayoutName,
                out controlPath,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        var fallbackIndex = FindFallbackBindingIndex(action, deviceType);
        if (fallbackIndex < 0)
        {
            return string.Empty;
        }

        return action.GetBindingDisplayString(
            fallbackIndex,
            out deviceLayoutName,
            out controlPath,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }

    private static int FindBindingIndex(InputAction action, string group)
    {
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (HasExactBindingGroup(binding.groups, group))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFallbackBindingIndex(InputAction action, InputPromptManager.DeviceType deviceType)
    {
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (BindingMatchesDevice(binding.path, deviceType))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HasExactBindingGroup(string groups, string group)
    {
        if (string.IsNullOrEmpty(groups))
        {
            return false;
        }

        foreach (var part in groups.Split(';'))
        {
            if (part.Trim() == group)
            {
                return true;
            }
        }

        return false;
    }

    private static bool BindingMatchesDevice(string path, InputPromptManager.DeviceType deviceType)
    {
        if (deviceType == InputPromptManager.DeviceType.Gamepad)
        {
            return path.Contains("<Gamepad>");
        }

        return path.Contains("<Keyboard>") || path.Contains("<Mouse>");
    }

    private static string JoinNonEmpty(params string[] parts)
    {
        if (parts == null || parts.Length == 0)
        {
            return string.Empty;
        }

        var result = string.Empty;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            result = string.IsNullOrEmpty(result) ? part.Trim() : $"{result} {part.Trim()}";
        }

        return result;
    }
}
}