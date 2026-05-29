using MHZE.InteractSystem;
using MHZE.InputPromptSystem;
using UnityEngine;

public class InteractInputPromptMediator : MonoBehaviour
{
    [SerializeField] private InteractSystem interactSystem;
    [SerializeField] private InputPromptManager inputPromptManager;

    [Header("Prompt Definitions")]
    [SerializeField] private InputPromptDefinition interactPromptDefinition;
    [SerializeField] private InputPromptDefinition holdPromptDefinition;

    private void OnEnable()
    {
        if (interactPromptDefinition == null)
        {
            Debug.LogWarning("[InteractInputPromptMediator] interactPromptDefinition is not assigned.", this);
        }

        interactSystem.OnInteractableFound += HandleInteractableFound;
        interactSystem.OnInteractableLost += HandleInteractableLost;
        interactSystem.OnCurrentInteractableUpdated += HandleCurrentInteractableUpdated;
        interactSystem.OnHoldAttemptStarted += HandleHoldAttemptStarted;
        interactSystem.OnHoldAttemptEnded += HandleHoldAttemptEnded;
        interactSystem.OnPerformedInteraction += HandlePerformedInteraction;
    }

    private void OnDisable()
    {
        interactSystem.OnInteractableFound -= HandleInteractableFound;
        interactSystem.OnInteractableLost -= HandleInteractableLost;
        interactSystem.OnCurrentInteractableUpdated -= HandleCurrentInteractableUpdated;
        interactSystem.OnHoldAttemptStarted -= HandleHoldAttemptStarted;
        interactSystem.OnHoldAttemptEnded -= HandleHoldAttemptEnded;
        interactSystem.OnPerformedInteraction -= HandlePerformedInteraction;
    }

    private void HandleInteractableFound(IInteractable interactable, IInteractor interactor)
    {
        if (interactPromptDefinition == null) return;

        if (interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(interactPromptDefinition.Key);
        }
    }

    private void HandleInteractableLost(IInteractable interactable, IInteractor interactor)
    {
        if (interactPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(interactPromptDefinition.Key);
        }

        if (holdPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(holdPromptDefinition.Key);
        }
    }

    private void HandleCurrentInteractableUpdated()
    {
        var interactable = interactSystem.CurrentInteractable;
        if (interactable != null && interactable.AllowPrompt)
        {
            if (interactPromptDefinition != null)
            {
                inputPromptManager.ShowPrompt(interactPromptDefinition.Key);
            }
        }
        else
        {
            if (interactPromptDefinition != null)
            {
                inputPromptManager.HidePrompt(interactPromptDefinition.Key);
            }

            if (holdPromptDefinition != null)
            {
                inputPromptManager.HidePrompt(holdPromptDefinition.Key);
            }
        }
    }

    private void HandleHoldAttemptStarted(float holdTime)
    {
        if (interactPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(interactPromptDefinition.Key);
        }

        if (holdPromptDefinition != null)
        {
            inputPromptManager.ShowCustomPrompt(
                holdPromptDefinition.Key,
                holdPromptDefinition.Location,
                holdPromptDefinition.PrefixText,
                holdPromptDefinition.SuffixText
            );
        }
    }

    private void HandleHoldAttemptEnded()
    {
        if (holdPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(holdPromptDefinition.Key);
        }

        RefreshPrompt();
    }

    private void HandlePerformedInteraction(GameObject obj, IInteractor interactor)
    {
        if (interactPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(interactPromptDefinition.Key);
        }

        if (holdPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(holdPromptDefinition.Key);
        }
    }

    private void RefreshPrompt()
    {
        if (interactPromptDefinition == null) return;

        var interactable = interactSystem.CurrentInteractable;
        if (interactable != null && interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(interactPromptDefinition.Key);
        }
    }
}
