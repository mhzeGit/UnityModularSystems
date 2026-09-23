using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Plays UI sound effects for hover, click, select, press and release using
    /// a <see cref="UISoundProfile"/> and the <see cref="UIAudioManager"/>
    /// singleton. Hover sounds are suppressed in Navigation mode so pointer
    /// events do not produce phantom sounds for keyboard/gamepad users.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class UIButtonSFX : MonoBehaviour,
        IPointerEnterHandler, IPointerClickHandler,
        ISelectHandler, IPointerDownHandler, IPointerUpHandler,
        ISubmitHandler
    {
        [Header("Profile")]
        [Tooltip("Per-button profile. When empty, the audio manager's default profile is used.")]
        [SerializeField] private UISoundProfile profileOverride;

        [Header("Behavior")]
        [SerializeField] private bool respectInteractable = true;

        [Tooltip("Selectable to check for interactability. Defaults to the Selectable on this GameObject.")]
        [SerializeField] private Selectable targetSelectable;

        [Header("State Toggles")]
        [SerializeField] private bool playClick = true;
        [SerializeField] private bool playHover = true;
        [SerializeField] private bool playSelect = true;
        [SerializeField] private bool playPress = false;
        [SerializeField] private bool playRelease = false;

        private void Awake()
        {
            if (targetSelectable == null)
                targetSelectable = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UIInputState.Current != UIInputMode.Pointer) return;
            if (playHover) TryPlay(UISoundProfile.UISoundState.Hover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (playClick) TryPlay(UISoundProfile.UISoundState.Click);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (playSelect) TryPlay(UISoundProfile.UISoundState.Select);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (playPress) TryPlay(UISoundProfile.UISoundState.Press);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (playRelease) TryPlay(UISoundProfile.UISoundState.Release);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (playClick) TryPlay(UISoundProfile.UISoundState.Click);
        }

        private void TryPlay(UISoundProfile.UISoundState state)
        {
            if (respectInteractable && targetSelectable != null && !targetSelectable.IsInteractable())
                return;

            if (UIAudioManager.Instance != null)
                UIAudioManager.Instance.Play(profileOverride, state);
        }
    }
}
