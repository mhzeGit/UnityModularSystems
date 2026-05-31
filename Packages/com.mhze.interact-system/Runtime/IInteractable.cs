// Interface for any object the player can interact with. Defines methods for interact, interact released, hover enter, hover exit, and properties for whether it is interactable, whether a prompt is allowed, hold time, and optional prompt prefix/suffix overrides. Also exposes an update event for when any of these change.

using System;

namespace MHZE.InteractSystem
{
    public interface IInteractable
    {
        void OnInteract(IInteractor interactor);
        void OnInteractReleased(IInteractor interactor);
        void OnHoverEnter(IInteractor interactor);
        void OnHoverExit(IInteractor interactor);

        bool IsInteractable { get; }
        bool AllowPrompt { get; }
        bool OneTimeInteract { get; }
        bool InteractedOnce { get; }
        void SetIsInteractable(bool value);
        void SetAllowPrompt(bool value);
        void SetInteractedOnce(bool value);
        float HoldTime { get; }
        string PromptPrefix { get; }
        string PromptSuffix { get; }
        void SetPromptPrefix(string value);
        void SetPromptSuffix(string value);

        event Action OnInteractableUpdated;
    }
}
