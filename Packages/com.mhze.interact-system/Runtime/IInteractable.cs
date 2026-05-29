// Interface for any object the player can interact with. Defines methods for interact, interact released, hover enter, hover exit, and properties for whether it is interactable, whether a prompt is allowed, hold time, and prompt text. Also exposes an update event for when any of these change.

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
