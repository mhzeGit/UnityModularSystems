using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// One "action binding" row for the Controls tab of the options menu:
    /// shows the action name and current binding, rebinds on click and resets
    /// to the default binding. Rows auto-register with <see cref="OptionsScreen"/>
    /// (any row found under the options screen is displayed and saved).
    ///
    /// Assign an InputActionReference, optionally a <see cref="BindingIconLibrary"/>
    /// for button icons, and the label/button references.
    /// </summary>
    public class InputRebindRowUI : MonoBehaviour
    {
        public enum RebindInputTarget
        {
            Any,
            KeyboardAndMouse,
            Gamepad
        }

        [Header("Binding Target")]
        [SerializeField] private InputActionReference actionReference;

        [HideInInspector] [SerializeField] private string bindingId;

        [Tooltip("Optional explicit binding index. Use this when Binding Id is not set or rows are created as empty placeholders.")]
        [SerializeField] private int bindingIndexOverride = -1;

        [Header("Labels")]
        [SerializeField] private string actionDisplayName;
        [SerializeField] private TMP_Text actionNameText;
        [SerializeField] private TMP_Text bindingText;
        [SerializeField] private Image bindingIconImage;

        [Header("Buttons")]
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button resetButton;

        [Header("Rebind Overlay")]
        [Tooltip("Optional overlay shown while waiting for input (usually a full-screen panel with a prompt).")]
        [SerializeField] private GameObject rebindOverlay;
        [SerializeField] private TMP_Text rebindPromptText;

        [Header("Behavior")]
        [Tooltip("Optional icon library used to display button sprites instead of text.")]
        [SerializeField] private BindingIconLibrary iconLibrary;

        [Tooltip("Optional fallback actions asset. Its UI action map is disabled while rebinding.")]
        [SerializeField] private InputActionAsset defaultInputActions;

        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private RebindInputTarget inputTarget = RebindInputTarget.Any;
        [SerializeField] private string keyboardMouseBindingGroup = "Keyboard&Mouse";
        [SerializeField] private string gamepadBindingGroup = "Gamepad";

        [Tooltip("When enabled, this row's target binding starts unbound (empty) by default.")]
        [SerializeField] private bool defaultBindingEmpty = false;

        [Tooltip("When enabled, rebinding swaps bindings when the selected control is already used by another action in the same Input Action Asset.")]
        [SerializeField] private bool preventDuplicateBindings = false;

        [Tooltip("When enabled, specific controls can clear a binding and leave it unbound.")]
        [SerializeField] private bool allowEmptyBindings = true;

        [SerializeField] private List<string> emptyBindingControls = new List<string> { "<Keyboard>/backspace", "<Keyboard>/delete" };

        [Tooltip("Controls that cancel rebinding and keep the previous binding unchanged.")]
        [SerializeField] private List<string> cancelRebindControls = new List<string> { "<Keyboard>/escape" };

        [SerializeField] private List<string> excludedControls = new List<string> { "<Pointer>/position", "<Pointer>/delta", "<Keyboard>/anyKey" };

        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private InputActionMap _uiActionMap;
        private string _preRebindEffectivePath;
        private bool _completeCurrentRebindAsEmpty;
        private bool _restoreEmptyOverrideOnCancel;
        private bool _isBackHandlerRegistered;
        private bool _cancelCurrentRebindAsEmpty;

        private static readonly List<InputRebindRowUI> ActiveRows = new List<InputRebindRowUI>();

        /// <summary>The action asset this row belongs to, if resolvable.</summary>
        public InputActionAsset ActionAsset => actionReference?.action?.actionMap?.asset;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (actionReference?.action == null) return;
            if (bindingIndexOverride < 0 || bindingIndexOverride >= actionReference.action.bindings.Count) return;

            var resolvedBindingId = actionReference.action.bindings[bindingIndexOverride].id.ToString();
            if (!string.Equals(bindingId, resolvedBindingId, StringComparison.OrdinalIgnoreCase))
                bindingId = resolvedBindingId;
        }
#endif

        private void OnEnable()
        {
            RegisterButtons();

            if (defaultInputActions != null && _uiActionMap == null)
                _uiActionMap = defaultInputActions.FindActionMap(uiActionMapName);

            ActiveRows.Add(this);
            if (ActiveRows.Count == 1)
                UnityEngine.InputSystem.InputSystem.onActionChange += HandleActionChange;

            ApplyDefaultEmptyBindingIfNeeded();
            UpdateDisplay();
        }

        private void OnDisable()
        {
            UnregisterButtons();
            UnregisterBackHandler();

            _rebindOperation?.Dispose();
            _rebindOperation = null;

            ActiveRows.Remove(this);
            if (ActiveRows.Count == 0)
                UnityEngine.InputSystem.InputSystem.onActionChange -= HandleActionChange;
        }

        /// <summary>Start listening for the next pressed control and bind it.</summary>
        public void StartInteractiveRebind()
        {
            if (!TryResolveBinding(out var action, out var bindingIndex))
            {
                action = actionReference?.action;
                if (action != null)
                    bindingIndex = FindFallbackBindingIndex(action);

                if (action == null || bindingIndex < 0)
                {
                    Debug.LogWarning($"[Basic Menus] Could not resolve a binding for action '{actionReference?.action?.name ?? "<null>"}'. Assign a valid Binding Id or Binding Index Override on this row.");
                    return;
                }
            }

            if (bindingIndex >= action.bindings.Count)
            {
                Debug.LogWarning($"[Basic Menus] Resolved binding index '{bindingIndex}' is out of range for action '{action.name}'.");
                return;
            }

            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                    PerformRebind(action, firstPartIndex, true);
                return;
            }

            PerformRebind(action, bindingIndex);
        }

        /// <summary>Remove any override and return to the default binding(s).</summary>
        public void ResetToDefault()
        {
            if (!TryResolveBinding(out var action, out var bindingIndex))
                return;

            if (action.bindings[bindingIndex].isComposite)
            {
                for (var i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
                    action.RemoveBindingOverride(i);
            }
            else
            {
                action.RemoveBindingOverride(bindingIndex);
            }

            UpdateDisplay();
        }

        /// <summary>Refresh the action name, binding text and icon.</summary>
        public void UpdateDisplay()
        {
            if (actionNameText != null)
            {
                actionNameText.text = string.IsNullOrWhiteSpace(actionDisplayName)
                    ? actionReference?.action?.name ?? string.Empty
                    : actionDisplayName;
            }

            if (!TryResolveBinding(out var action, out var bindingIndex))
            {
                if (bindingText != null)
                    bindingText.text = string.Empty;

                if (bindingIconImage != null)
                    bindingIconImage.gameObject.SetActive(false);

                return;
            }

            var displayString = action.GetBindingDisplayString(
                bindingIndex,
                out var deviceLayoutName,
                out var controlPath,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

            Sprite icon = null;
            var hasIcon = false;
            if (iconLibrary != null)
                hasIcon = iconLibrary.TryGetIcon(deviceLayoutName, controlPath, out icon);

            if (bindingIconImage != null)
            {
                bindingIconImage.gameObject.SetActive(hasIcon);
                bindingIconImage.sprite = hasIcon ? icon : null;
            }

            if (bindingText != null)
            {
                bindingText.gameObject.SetActive(!hasIcon);
                bindingText.text = displayString;
            }
        }

        private static int FindFallbackBindingIndex(InputAction action)
        {
            if (action == null || action.bindings.Count == 0) return -1;

            for (var i = 0; i < action.bindings.Count; i++)
            {
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                    return i;
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isPartOfComposite)
                    return i;
            }

            return 0;
        }

        private void ApplyDefaultEmptyBindingIfNeeded()
        {
            if (!defaultBindingEmpty) return;
            if (!TryResolveBinding(out var action, out var bindingIndex)) return;

            var binding = action.bindings[bindingIndex];
            if (binding.hasOverrides) return;

            action.ApplyBindingOverride(bindingIndex, string.Empty);
        }

        private bool TryResolveBinding(out InputAction action, out int bindingIndex)
        {
            action = actionReference?.action;
            bindingIndex = -1;

            if (action == null) return false;

            if (bindingIndexOverride >= 0 && bindingIndexOverride < action.bindings.Count)
            {
                bindingIndex = bindingIndexOverride;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(bindingId))
            {
                var trimmedBindingId = bindingId.Trim();
                if (Guid.TryParse(trimmedBindingId, out var parsedId))
                {
                    bindingIndex = action.bindings.IndexOf(binding => binding.id == parsedId);
                    if (bindingIndex >= 0) return true;
                }

                for (var i = 0; i < action.bindings.Count; i++)
                {
                    var currentId = action.bindings[i].id.ToString();
                    var currentIdNoDashes = action.bindings[i].id.ToString("N");
                    if (string.Equals(currentId, trimmedBindingId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(currentIdNoDashes, trimmedBindingId, StringComparison.OrdinalIgnoreCase))
                    {
                        bindingIndex = i;
                        return true;
                    }
                }
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite || binding.isComposite) continue;
                if (!DoesBindingMatchTarget(binding)) continue;

                bindingIndex = i;
                return true;
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite || binding.isComposite) continue;
                if (!string.IsNullOrWhiteSpace(binding.groups)) continue;

                bindingIndex = i;
                return true;
            }

            return false;
        }

        private void PerformRebind(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            _rebindOperation?.Dispose();
            _rebindOperation = null;
            _completeCurrentRebindAsEmpty = false;
            _cancelCurrentRebindAsEmpty = false;
            _restoreEmptyOverrideOnCancel = false;
            _preRebindEffectivePath = action.bindings[bindingIndex].effectivePath;

            if (IsExplicitEmptyOverride(action.bindings[bindingIndex]))
            {
                _restoreEmptyOverrideOnCancel = true;
                action.RemoveBindingOverride(bindingIndex);
            }

            action.actionMap?.Disable();
            _uiActionMap?.Disable();

            _rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .OnPotentialMatch(operation =>
                {
                    if (!allowEmptyBindings || operation.selectedControl == null)
                    {
                        if (operation.selectedControl != null && ShouldCancelRebind(operation.selectedControl.path))
                            operation.Cancel();
                        return;
                    }

                    if (ShouldCancelRebind(operation.selectedControl.path))
                    {
                        operation.Cancel();
                        return;
                    }

                    if (!ShouldClearBinding(operation.selectedControl.path))
                        return;

                    _cancelCurrentRebindAsEmpty = true;
                    operation.Cancel();
                })
                .OnCancel(_ => FinishRebind(action, bindingIndex, allCompositeParts, false))
                .OnComplete(_ => FinishRebind(action, bindingIndex, allCompositeParts, true));

            for (var i = 0; i < cancelRebindControls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(cancelRebindControls[i]))
                    _rebindOperation.WithCancelingThrough(cancelRebindControls[i]);
            }

            for (var i = 0; i < emptyBindingControls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(emptyBindingControls[i]))
                    _rebindOperation.WithCancelingThrough(emptyBindingControls[i]);
            }

            for (var i = 0; i < excludedControls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(excludedControls[i]))
                    _rebindOperation.WithControlsExcluding(excludedControls[i]);
            }

            rebindOverlay?.SetActive(true);
            RegisterBackHandler();

            if (rebindPromptText != null)
            {
                var partName = action.bindings[bindingIndex].isPartOfComposite
                    ? $"Binding '{action.bindings[bindingIndex].name}'. "
                    : string.Empty;
                var clearHint = allowEmptyBindings && emptyBindingControls.Count > 0
                    ? " Press a clear key to leave this bind empty."
                    : string.Empty;

                rebindPromptText.text = $"{partName}Waiting for input...{clearHint}";
            }

            _rebindOperation.Start();
        }

        private bool DoesBindingMatchTarget(InputBinding binding)
        {
            var groups = binding.groups ?? string.Empty;
            switch (inputTarget)
            {
                case RebindInputTarget.KeyboardAndMouse:
                    return IsInGroup(groups, keyboardMouseBindingGroup) || groups.Length == 0;
                case RebindInputTarget.Gamepad:
                    return IsInGroup(groups, gamepadBindingGroup) || groups.Length == 0;
                default:
                    return true;
            }
        }

        private static bool IsInGroup(string groups, string targetGroup)
        {
            if (string.IsNullOrWhiteSpace(groups) || string.IsNullOrWhiteSpace(targetGroup))
                return false;

            var split = groups.Split(';');
            for (var i = 0; i < split.Length; i++)
            {
                if (string.Equals(split[i].Trim(), targetGroup, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void FinishRebind(InputAction action, int bindingIndex, bool allCompositeParts, bool isComplete)
        {
            UnregisterBackHandler();
            rebindOverlay?.SetActive(false);

            _rebindOperation?.Dispose();
            _rebindOperation = null;

            action.actionMap?.Enable();
            _uiActionMap?.Enable();

            if (!isComplete && _cancelCurrentRebindAsEmpty)
            {
                action.ApplyBindingOverride(bindingIndex, string.Empty);
                _completeCurrentRebindAsEmpty = true;
            }

            var completed = isComplete || _completeCurrentRebindAsEmpty;

            if (!completed && _restoreEmptyOverrideOnCancel)
                action.ApplyBindingOverride(bindingIndex, string.Empty);

            _completeCurrentRebindAsEmpty = false;
            _cancelCurrentRebindAsEmpty = false;
            _restoreEmptyOverrideOnCancel = false;

            if (completed && preventDuplicateBindings && TryFindDuplicateBinding(action, bindingIndex, out var duplicateAction, out var duplicateBindingIndex))
            {
                if (string.IsNullOrWhiteSpace(_preRebindEffectivePath))
                    duplicateAction.ApplyBindingOverride(duplicateBindingIndex, string.Empty);
                else
                    duplicateAction.ApplyBindingOverride(duplicateBindingIndex, _preRebindEffectivePath);

                for (var i = 0; i < ActiveRows.Count; i++)
                    ActiveRows[i]?.UpdateDisplay();
            }

            UpdateDisplay();

            if (!completed || !allCompositeParts) return;

            var nextBindingIndex = bindingIndex + 1;
            if (nextBindingIndex < action.bindings.Count && action.bindings[nextBindingIndex].isPartOfComposite)
                PerformRebind(action, nextBindingIndex, true);
        }

        private void RegisterBackHandler()
        {
            if (_isBackHandlerRegistered) return;

            UIBackRouter.RegisterModalBackHandler(HandleBackWhileRebinding);
            _isBackHandlerRegistered = true;
        }

        private void UnregisterBackHandler()
        {
            if (!_isBackHandlerRegistered) return;

            UIBackRouter.UnregisterModalBackHandler(HandleBackWhileRebinding);
            _isBackHandlerRegistered = false;
        }

        private bool HandleBackWhileRebinding()
        {
            if (_rebindOperation == null || rebindOverlay == null || !rebindOverlay.activeSelf)
                return false;

            _rebindOperation.Cancel();
            return true;
        }

        private bool ShouldClearBinding(string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath) || emptyBindingControls == null)
                return false;

            for (var i = 0; i < emptyBindingControls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(emptyBindingControls[i]) &&
                    ControlPathMatches(controlPath, emptyBindingControls[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldCancelRebind(string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath) || cancelRebindControls == null)
                return false;

            for (var i = 0; i < cancelRebindControls.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(cancelRebindControls[i]) &&
                    ControlPathMatches(controlPath, cancelRebindControls[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ControlPathMatches(string controlPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath) || string.IsNullOrWhiteSpace(configuredPath))
                return false;

            if (string.Equals(controlPath, configuredPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var configuredControlNameStart = configuredPath.LastIndexOf('/') + 1;
            if (configuredControlNameStart <= 0 || configuredControlNameStart >= configuredPath.Length)
                return false;

            var configuredControlName = configuredPath.Substring(configuredControlNameStart);
            return controlPath.EndsWith("/" + configuredControlName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExplicitEmptyOverride(InputBinding binding)
        {
            return binding.hasOverrides && binding.overridePath != null && binding.overridePath.Length == 0;
        }

        private static bool TryFindDuplicateBinding(InputAction action, int bindingIndex, out InputAction duplicateAction, out int duplicateBindingIndex)
        {
            duplicateAction = null;
            duplicateBindingIndex = -1;

            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            var newPath = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrWhiteSpace(newPath))
                return false;

            var asset = action.actionMap?.asset;
            if (asset == null)
                return false;

            foreach (var map in asset.actionMaps)
            {
                foreach (var mapAction in map.actions)
                {
                    for (var i = 0; i < mapAction.bindings.Count; i++)
                    {
                        if (mapAction == action && i == bindingIndex) continue;
                        if (mapAction.bindings[i].isComposite || mapAction.bindings[i].isPartOfComposite) continue;

                        if (string.Equals(mapAction.bindings[i].effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            duplicateAction = mapAction;
                            duplicateBindingIndex = i;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void RegisterButtons()
        {
            if (rebindButton != null)
                rebindButton.onClick.AddListener(StartInteractiveRebind);

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefault);
        }

        private void UnregisterButtons()
        {
            if (rebindButton != null)
                rebindButton.onClick.RemoveListener(StartInteractiveRebind);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetToDefault);
        }

        private static void HandleActionChange(object changedObject, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged) return;

            for (var i = 0; i < ActiveRows.Count; i++)
                ActiveRows[i]?.UpdateDisplay();
        }
    }
}
