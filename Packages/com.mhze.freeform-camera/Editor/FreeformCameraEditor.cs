// Ensures the cursor is unlocked and visible when the editor exits play mode, preventing the cursor from staying locked after a freeform camera session stops.

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace MHZE.FreeformCamera.Editor
{
    [InitializeOnLoad]
    internal static class FreeformCameraEditorHooks
    {
        static FreeformCameraEditorHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}

#endif
