using System.Collections.Generic;
using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Centralised cursor ownership router. Any system (menus, pause, cutscenes,
    /// gameplay camera...) can submit a cursor request with a priority and the
    /// highest priority request wins. When no request exists the configurable
    /// defaults are applied so the cursor returns to its gameplay state.
    /// </summary>
    public static class UICursorStateRouter
    {
        /// <summary>Priority used by <see cref="UIInputModeDetector"/> while a menu is open.</summary>
        public const int PriorityUIInputMode = 100;

        /// <summary>Priority for pause-menu style requests, above input-mode requests.</summary>
        public const int PriorityPause = 200;

        /// <summary>Cursor visibility applied when no system is requesting the cursor.</summary>
        public static bool DefaultVisible = false;

        /// <summary>Cursor lock state applied when no system is requesting the cursor.</summary>
        public static CursorLockMode DefaultLockState = CursorLockMode.Locked;

        /// <summary>When false, the router stops touching the cursor once every request is cleared.</summary>
        public static bool ApplyDefaultsWhenIdle = true;

        private struct CursorRequest
        {
            public bool Visible;
            public CursorLockMode LockState;
            public int Priority;
            public ulong Sequence;
        }

        private static readonly Dictionary<object, CursorRequest> _requests = new Dictionary<object, CursorRequest>();
        private static ulong _sequence;

        /// <summary>Submit (or update) a cursor request owned by <paramref name="owner"/>.</summary>
        public static void SetRequest(object owner, bool visible, CursorLockMode lockState, int priority)
        {
            if (owner == null) return;

            _requests[owner] = new CursorRequest
            {
                Visible = visible,
                LockState = lockState,
                Priority = priority,
                Sequence = ++_sequence
            };

            Apply();
        }

        /// <summary>Remove the request owned by <paramref name="owner"/>, if any.</summary>
        public static void ClearRequest(object owner)
        {
            if (owner == null) return;
            if (_requests.Remove(owner))
                Apply();
        }

        /// <summary>Remove every request. Useful when loading scenes or returning to a boot state.</summary>
        public static void ClearAll()
        {
            if (_requests.Count == 0) return;
            _requests.Clear();
            Apply();
        }

        private static void Apply()
        {
            if (_requests.Count == 0)
            {
                if (ApplyDefaultsWhenIdle)
                {
                    Cursor.visible = DefaultVisible;
                    Cursor.lockState = DefaultLockState;
                }
                return;
            }

            CursorRequest selected = default;
            bool hasSelected = false;

            foreach (var request in _requests.Values)
            {
                if (!hasSelected ||
                    request.Priority > selected.Priority ||
                    (request.Priority == selected.Priority && request.Sequence > selected.Sequence))
                {
                    selected = request;
                    hasSelected = true;
                }
            }

            Cursor.visible = selected.Visible;
            Cursor.lockState = selected.LockState;
        }
    }
}
