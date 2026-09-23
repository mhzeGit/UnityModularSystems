using System;
using System.Collections.Generic;
using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Maps device layouts and control paths (e.g. "Keyboard" + "space",
    /// "Gamepad" + "buttonSouth") to sprite icons so rebinding rows can show
    /// button graphics instead of text. Use "*" as device layout for a
    /// fallback entry. Create one via Assets > Create > Basic Menus >
    /// Binding Icon Library and assign it on each rebinding row.
    /// </summary>
    [CreateAssetMenu(fileName = "BindingIconLibrary", menuName = "Basic Menus/Binding Icon Library")]
    public class BindingIconLibrary : ScriptableObject
    {
        [Serializable]
        public class IconEntry
        {
            [Tooltip("Device layout name. Examples: Keyboard, Mouse, Gamepad, DualShockGamepad. Use * for any layout.")]
            public string deviceLayout = "*";

            [Tooltip("Control path from the Input System display callback. Examples: space, leftButton, buttonSouth, dpad/up")]
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

        [SerializeField] private List<IconEntry> entries = new List<IconEntry>();

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
                entries[i]?.Normalize();
        }

        /// <summary>Find the best icon for a device layout + control path pair.</summary>
        public bool TryGetIcon(string deviceLayout, string controlPath, out Sprite icon)
        {
            icon = null;
            if (string.IsNullOrWhiteSpace(controlPath))
                return false;

            var normalizedPath = Normalize(controlPath);
            var normalizedLayout = Normalize(deviceLayout);
            var bestScore = -1;
            Sprite fallbackIcon = null;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.icon == null) continue;
                if (entry.normalizedControlPath != normalizedPath) continue;

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
                return true;

            if (fallbackIcon != null)
            {
                icon = fallbackIcon;
                return true;
            }

            return false;
        }

        private static int GetLayoutMatchScore(string currentLayout, string entryLayout)
        {
            if (entryLayout == "*") return 0;
            if (string.IsNullOrEmpty(entryLayout)) return -1;

            if (string.Equals(currentLayout, entryLayout, StringComparison.OrdinalIgnoreCase))
                return 3;

            if (string.IsNullOrEmpty(currentLayout)) return -1;

            try
            {
                if (UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(currentLayout, entryLayout) ||
                    UnityEngine.InputSystem.InputSystem.IsFirstLayoutBasedOnSecond(entryLayout, currentLayout))
                {
                    return 2;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Basic Menus] Failed to compare input layouts '{currentLayout}' and '{entryLayout}'. Verify the layout names are valid: {exception.Message}");
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
