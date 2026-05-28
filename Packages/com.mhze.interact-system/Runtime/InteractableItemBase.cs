// Made By MHZE

using System;
using UnityEngine;
using UnityEngine.Events;

public class InteractableItemBase : MonoBehaviour, IInteractable
{
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private bool allowInteractPrompt = true;
    [SerializeField] private float interactHoldTime = 0f;
    [SerializeField] private string interactPromptPrefix = "Press";
    [SerializeField] private string interactPromptSuffix = "To Interact";

    public event Action OnInteractableUpdated;

    public event Action<float> OnHoldAttemptStarted;
    public event Action OnHoldAttemptEnded;
    public event Action OnInteractedWith;
    public event Action OnInteractReleased;
    public UnityEvent OnInteractedWithEvent;

    public virtual void OnInteract()
    {
        OnInteractedWith?.Invoke();
        OnInteractedWithEvent?.Invoke();
    }

    public bool GetIsInteractable() => isInteractable;

    public void SetIsInteractable(bool value)
    {
        if (isInteractable == value) return;
        isInteractable = value;
        OnInteractableUpdated?.Invoke();
    }

    public bool GetAllowInteractPrompt() => allowInteractPrompt;

    public void SetAllowInteractPrompt(bool value)
    {
        if (allowInteractPrompt == value) return;
        allowInteractPrompt = value;
        OnInteractableUpdated?.Invoke();
    }

    public float GetInteractHoldTime() => interactHoldTime;

    public string GetInteractPrompt(int promptIndex)
    {
        if (!allowInteractPrompt) return string.Empty;
        return promptIndex == 0 ? interactPromptPrefix : interactPromptSuffix;
    }

    public void SetInteractPrompt(string prefix, string suffix)
    {
        interactPromptPrefix = prefix;
        interactPromptSuffix = suffix;
        OnInteractableUpdated?.Invoke();
    }

    public void TriggerHoldStarted(float holdTime) => OnHoldAttemptStarted?.Invoke(holdTime);
    public void TriggerHoldEnded() => OnHoldAttemptEnded?.Invoke();
    public void TriggerInteractReleased() => OnInteractReleased?.Invoke();
}
