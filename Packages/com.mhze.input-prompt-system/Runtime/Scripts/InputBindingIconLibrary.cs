// A library that maps device-and-control combinations (e.g. "Keyboard" + "space" or "Gamepad" + "buttonSouth") to Sprite icons so the correct button graphic appears on screen. Supports a wildcard device layout for fallback, and picks the best match using a scoring system: exact device match wins, then layout-subtype match, then wildcard. If no device match is found, it falls back to matching by control path alone.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InputPromptSystem
{
[CreateAssetMenu(fileName = "InputBindingIconLibrary", menuName = "Input Prompts/Input Binding Icon Library")]
public class InputBindingIconLibrary : ScriptableObject
{
    [Serializable]
    public class IconEntry
    {
        [Tooltip("Device layout name. Examples: Keyboard, Mouse, Gamepad, DualShockGamepad. Use * for any layout.")]
        public string deviceLayout = "*";

        [Tooltip("Control path from Input System display callback. Examples: space, leftButton, buttonSouth, dpad/up")]
        public string controlPath;

        public Sprite icon;

        [NonSerialized] internal string normalizedControlPath;
        [NonSerialized] internal string normalizedDeviceLayout;

        internal void Normalize()
        {
            normalizedControlPath = string.IsNullOrWhiteSpace(controlPath)
                ? string.Empty
                : controlPath.Trim().ToLowerInvariant();
            normalizedDeviceLayout = string.IsNullOrWhiteSpace(deviceLayout)
                ? string.Empty
                : deviceLayout.Trim().ToLowerInvariant();
        }
    }

    [SerializeField] private List<IconEntry> entries = new();

    private void OnEnable()
    {
        NormalizeEntries();
    }

    private void OnValidate()
    {
        NormalizeEntries();
    }

    private void NormalizeEntries()
    {
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i]?.Normalize();
        }
    }

    public bool TryGetIcon(string deviceLayout, string controlPath, out Sprite icon)
    {
        icon = null;
        if (string.IsNullOrWhiteSpace(controlPath))
        {
            return false;
        }

        var normalizedPath = Normalize(controlPath);
        var normalizedLayout = Normalize(deviceLayout);
        var bestScore = -1;
        Sprite fallbackIcon = null;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.icon == null)
            {
                continue;
            }

            if (entry.normalizedControlPath != normalizedPath)
            {
                continue;
            }

            var score = GetLayoutMatchScore(normalizedLayout, entry.normalizedDeviceLayout);
            if (score >= 0)
            {
                if (score > bestScore)
                {
                    icon = entry.icon;
                    bestScore = score;
                }
            }
            else if (fallbackIcon == null)
            {
                fallbackIcon = entry.icon;
            }
        }

        if (bestScore >= 0)
        {
            return true;
        }

        if (fallbackIcon != null)
        {
            icon = fallbackIcon;
            return true;
        }

        return false;
    }

    private static int GetLayoutMatchScore(string currentLayout, string entryLayout)
    {
        if (entryLayout == "*")
        {
            return 0;
        }

        if (string.IsNullOrEmpty(entryLayout))
        {
            return -1;
        }

        if (string.Equals(currentLayout, entryLayout, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.IsNullOrEmpty(currentLayout))
        {
            return -1;
        }

        try
        {
            if (UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(currentLayout, entryLayout) ||
                UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(entryLayout, currentLayout))
            {
                return 2;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InputBindingIconLibrary] Failed to compare layouts '{currentLayout}' and '{entryLayout}'. Verify layout names are valid: {ex.Message}");
            return -1;
        }

        return -1;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
}