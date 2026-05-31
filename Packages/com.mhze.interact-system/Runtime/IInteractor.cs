// Interface for the interactor (typically the player). Provides the player camera and the display string for the current interaction key binding, used by interactables to build their prompt text.

using UnityEngine;

namespace MHZE.InteractSystem
{
    public interface IInteractor
    {
        Camera PlayerCamera { get; }
        Transform InteractorTransform { get; }
        string InteractionBindingDisplayString { get; }
    }
}
