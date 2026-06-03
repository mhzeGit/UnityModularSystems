// Editor window (Window > Freecamera Settings) for configuring the freeform camera settings at edit time or at runtime. Settings are saved to both EditorPrefs and PlayerPrefs and are pushed to the live manager while in play mode. Includes a reset-to-defaults button and a keyboard shortcut reference.

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.FreeformCamera.Editor
{
    public class FreeformCameraSettingsEditor : EditorWindow
    {
        private const string PrefToggleKey = "FFC_ToggleKey";
        private const string PrefBaseSpeed = "FFC_BaseSpeed";
        private const string PrefBoostMul = "FFC_BoostMultiplier";
        private const string PrefSmoothTime = "FFC_SmoothTime";
        private const string PrefLookSens = "FFC_LookSensitivity";
        private const string PrefInvertY = "FFC_InvertY";
        private const string PrefEnableCol = "FFC_EnableCollision";
        private const string PrefColMask = "FFC_CollisionLayerMask";
        private const string PrefColRadius = "FFC_CollisionRadius";
        private const string PrefColOffset = "FFC_CollisionOffset";

        private Key _toggleKey = Key.F8;
        private float _baseSpeed = 10f;
        private float _boostMultiplier = 2f;
        private float _smoothTime = 0.15f;
        private float _lookSensitivity = 1f;
        private bool _invertY;
        private bool _enableCollision;
        private int _collisionMask = -1;
        private float _collisionRadius = 0.5f;
        private float _collisionOffset = 0.05f;

        private Vector2 _scrollPos;

        [MenuItem("Window/Freecamera Settings")]
        private static void ShowWindow()
        {
            var w = GetWindow<FreeformCameraSettingsEditor>("Freecamera Settings");
            w.minSize = new Vector2(320, 480);
        }

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSettings();
            EditorGUILayout.Space(8);
            DrawShortcuts();
            EditorGUILayout.Space(8);
            DrawFooter();

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUI.IndentLevelScope(1))
            {
                _toggleKey = (Key)EditorGUILayout.EnumPopup("Toggle Key", _toggleKey);
                _baseSpeed = EditorGUILayout.Slider("Base Speed", _baseSpeed, 0.1f, 100f);
                _boostMultiplier = EditorGUILayout.Slider("Boost Multiplier", _boostMultiplier, 1f, 10f);
                _smoothTime = EditorGUILayout.Slider("Smooth Time", _smoothTime, 0.01f, 1f);
                _lookSensitivity = EditorGUILayout.Slider("Look Sensitivity", _lookSensitivity, 0.1f, 10f);
                _invertY = EditorGUILayout.Toggle("Invert Y", _invertY);
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUI.IndentLevelScope(1))
            {
                _enableCollision = EditorGUILayout.Toggle("Enabled", _enableCollision);

                using (new EditorGUI.DisabledGroupScope(!_enableCollision))
                {
                    _collisionMask = LayerMaskField("Layer Mask", _collisionMask);
                    _collisionRadius = EditorGUILayout.Slider("Radius", _collisionRadius, 0.1f, 2f);
                    _collisionOffset = EditorGUILayout.Slider("Offset", _collisionOffset, 0f, 0.5f);
                }
            }
        }

        private void DrawShortcuts()
        {
            EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawShortcutRow(_toggleKey.ToString(), "Toggle freeform camera on / off");
            DrawShortcutRow("Right-click + drag", "Look around (cursor locks while held)");
            DrawShortcutRow("W / A / S / D", "Move forward / left / backward / right");
            DrawShortcutRow("E / Q", "Move up / down");
            DrawShortcutRow("Scroll wheel", "Adjust movement speed");
            DrawShortcutRow("Shift (hold)", "Double current speed");
            DrawShortcutRow("Escape", "Release cursor");
        }

        private static void DrawShortcutRow(string key, string description)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30)))
            {
                _toggleKey = Key.F8;
                _baseSpeed = 10f;
                _boostMultiplier = 2f;
                _smoothTime = 0.15f;
                _lookSensitivity = 1f;
                _invertY = false;
                _enableCollision = false;
                _collisionMask = -1;
                _collisionRadius = 0.5f;
                _collisionOffset = 0.05f;
                SavePrefs();
                Repaint();
            }
        }

        private static int LayerMaskField(string label, int mask)
        {
            var layers = UnityEditorInternal.InternalEditorUtility.layers;
            var layerNames = new string[layers.Length];
            var layerValues = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                layerNames[i] = layers[i];
                layerValues[i] = LayerMask.NameToLayer(layers[i]);
            }
            int currentMask = 0;
            for (int i = 0; i < layerValues.Length; i++)
            {
                if (layerValues[i] >= 0 && (mask & (1 << layerValues[i])) != 0)
                    currentMask |= (1 << i);
            }
            int newMask = EditorGUILayout.MaskField(label, currentMask, layerNames);
            int result = 0;
            for (int i = 0; i < layerValues.Length; i++)
            {
                if ((newMask & (1 << i)) != 0 && layerValues[i] >= 0)
                    result |= (1 << layerValues[i]);
            }
            if (newMask == ~0)
                result = -1;
            return result;
        }

        private void LoadPrefs()
        {
            _toggleKey = (Key)EditorPrefs.GetInt(PrefToggleKey, (int)Key.F8);
            _baseSpeed = EditorPrefs.GetFloat(PrefBaseSpeed, 10f);
            _boostMultiplier = EditorPrefs.GetFloat(PrefBoostMul, 2f);
            _smoothTime = EditorPrefs.GetFloat(PrefSmoothTime, 0.15f);
            _lookSensitivity = EditorPrefs.GetFloat(PrefLookSens, 1f);
            _invertY = EditorPrefs.GetBool(PrefInvertY, false);
            _enableCollision = EditorPrefs.GetBool(PrefEnableCol, false);
            _collisionMask = EditorPrefs.GetInt(PrefColMask, -1);
            _collisionRadius = EditorPrefs.GetFloat(PrefColRadius, 0.5f);
            _collisionOffset = EditorPrefs.GetFloat(PrefColOffset, 0.05f);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefToggleKey, (int)_toggleKey);
            EditorPrefs.SetFloat(PrefBaseSpeed, _baseSpeed);
            EditorPrefs.SetFloat(PrefBoostMul, _boostMultiplier);
            EditorPrefs.SetFloat(PrefSmoothTime, _smoothTime);
            EditorPrefs.SetFloat(PrefLookSens, _lookSensitivity);
            EditorPrefs.SetBool(PrefInvertY, _invertY);
            EditorPrefs.SetBool(PrefEnableCol, _enableCollision);
            EditorPrefs.SetInt(PrefColMask, _collisionMask);
            EditorPrefs.SetFloat(PrefColRadius, _collisionRadius);
            EditorPrefs.SetFloat(PrefColOffset, _collisionOffset);

            PlayerPrefs.SetInt(PrefToggleKey, (int)_toggleKey);
            PlayerPrefs.SetFloat(PrefBaseSpeed, _baseSpeed);
            PlayerPrefs.SetFloat(PrefBoostMul, _boostMultiplier);
            PlayerPrefs.SetFloat(PrefSmoothTime, _smoothTime);
            PlayerPrefs.SetFloat(PrefLookSens, _lookSensitivity);
            PlayerPrefs.SetInt(PrefInvertY, _invertY ? 1 : 0);
            PlayerPrefs.SetInt(PrefEnableCol, _enableCollision ? 1 : 0);
            PlayerPrefs.SetInt(PrefColMask, _collisionMask);
            PlayerPrefs.SetFloat(PrefColRadius, _collisionRadius);
            PlayerPrefs.SetFloat(PrefColOffset, _collisionOffset);
            PlayerPrefs.Save();

            if (EditorApplication.isPlaying)
            {
                var manager = FindFirstObjectByType<FreeformManagerBehaviour>();
                if (manager != null)
                {
                    manager.toggleKey = _toggleKey;
                    manager.baseSpeed = _baseSpeed;
                    manager.boostMultiplier = _boostMultiplier;
                    manager.smoothTime = _smoothTime;
                    manager.lookSensitivity = _lookSensitivity;
                    manager.invertY = _invertY;
                    manager.enableCollision = _enableCollision;
                    manager.collisionMask = _collisionMask;
                    manager.collisionRadius = _collisionRadius;
                    manager.collisionOffset = _collisionOffset;
                }
            }
        }
    }
}
