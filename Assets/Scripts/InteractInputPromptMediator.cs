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
        if (interactSystem == null)
        {
            Debug.LogError("[InteractInputPromptMediator] interactSystem is not assigned.", this);
            return;
        }

        if (inputPromptManager == null)
        {
            Debug.LogError("[InteractInputPromptMediator] inputPromptManager is not assigned.", this);
            return;
        }

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
        if (interactSystem == null)
        {
            return;
        }

        interactSystem.OnInteractableFound -= HandleInteractableFound;
        interactSystem.OnInteractableLost -= HandleInteractableLost;
        interactSystem.OnCurrentInteractableUpdated -= HandleCurrentInteractableUpdated;
        interactSystem.OnHoldAttemptStarted -= HandleHoldAttemptStarted;
        interactSystem.OnHoldAttemptEnded -= HandleHoldAttemptEnded;
        interactSystem.OnPerformedInteraction -= HandlePerformedInteraction;
    }

    private void HandleInteractableFound(IInteractable interactable, IInteractor interactor)
    {
        if (inputPromptManager == null || interactPromptDefinition == null) return;

        if (interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(interactPromptDefinition.Key);
        }
    }

    private void HandleInteractableLost(IInteractable interactable, IInteractor interactor)
    {
        if (inputPromptManager == null) return;

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
        if (inputPromptManager == null) return;

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
        if (inputPromptManager == null) return;

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
        if (inputPromptManager == null) return;

        if (holdPromptDefinition != null)
        {
            inputPromptManager.HidePrompt(holdPromptDefinition.Key);
        }

        RefreshPrompt();
    }

    private void HandlePerformedInteraction(GameObject obj, IInteractor interactor)
    {
        if (inputPromptManager == null) return;

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
        if (inputPromptManager == null || interactPromptDefinition == null) return;

        var interactable = interactSystem.CurrentInteractable;
        if (interactable != null && interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(interactPromptDefinition.Key);
        }
    }
}
