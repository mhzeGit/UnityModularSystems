// With the First Person Controller package installed, holds the player's view still while the answer buttons of a question are up, so moving the mouse to a button does not turn the camera and a click does not recapture the cursor.

using System.Collections.Generic;
using UnityEngine;
using Controller = MHZE.FirstPersonController.FirstPersonController;

namespace MHZE.DialogSystem
{
/// <summary>Holds the first person look (<c>HoldLook</c>) for as long as a <see cref="DialogChoiceView"/> that uses the mouse shows its answers.</summary>
internal static class DialogChoiceLookHold
{
    private static readonly Dictionary<DialogChoiceView, Controller> held = new Dictionary<DialogChoiceView, Controller>();

    // Subscribes again on every play, also when domain reload is off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        held.Clear();
        DialogChoiceView.ShownChanged -= OnShownChanged;
        DialogChoiceView.ShownChanged += OnShownChanged;
    }

    private static void OnShownChanged(DialogChoiceView view, bool shown)
    {
        if (held.TryGetValue(view, out Controller controller))
        {
            held.Remove(view);
            if (controller != null)
            {
                controller.ReleaseLook(view);
            }
        }

        if (!shown || !view.UsesPointer)
        {
            return;
        }

        controller = Object.FindAnyObjectByType<Controller>();
        if (controller != null)
        {
            controller.HoldLook(view);
            held[view] = controller;
        }
    }
}
}
