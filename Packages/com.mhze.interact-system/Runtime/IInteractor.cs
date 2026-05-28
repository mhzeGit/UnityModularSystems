using UnityEngine;

namespace MHZE.InteractSystem
{
    public interface IInteractor
    {
        Camera PlayerCamera { get; }
        string InteractionBindingDisplayString { get; }
    }
}
