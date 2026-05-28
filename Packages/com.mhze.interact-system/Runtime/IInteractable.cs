// Made By MHZE
using System;

public interface IInteractable
{
    void OnInteract();
    bool GetIsInteractable();
    void SetIsInteractable(bool Bool);
    bool GetAllowInteractPrompt();
    void SetAllowInteractPrompt(bool Bool);
    float GetInteractHoldTime();
    string GetInteractPrompt(int promptIndex);
    void SetInteractPrompt(string prefix, string suffix);
    event Action OnInteractableUpdated;
}
