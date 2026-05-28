using System;

namespace MHZE.InteractSystem
{
    public interface IInteractable
    {
        void OnInteract(IInteractor interactor);
        void OnInteractReleased(IInteractor interactor);
        void OnHoverEnter(IInteractor interactor);
        void OnHoverExit(IInteractor interactor);

        bool IsInteractable { get; set; }
        bool AllowPrompt { get; set; }
        float HoldTime { get; }
        string PromptText { get; set; }

        event Action OnInteractableUpdated;
    }
}
