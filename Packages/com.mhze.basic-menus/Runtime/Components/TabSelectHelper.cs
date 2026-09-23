using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Added at runtime by <see cref="OptionsScreen"/> to each tab button so that
    /// simply navigating onto a tab with keyboard/gamepad switches to it.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class TabSelectHelper : MonoBehaviour, ISelectHandler
    {
        private int _index;
        private Action<int> _onSelected;

        /// <summary>Configure the tab index and the callback invoked on selection.</summary>
        public void Initialize(int index, Action<int> onSelected)
        {
            _index = index;
            _onSelected = onSelected;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _onSelected?.Invoke(_index);
        }
    }
}
