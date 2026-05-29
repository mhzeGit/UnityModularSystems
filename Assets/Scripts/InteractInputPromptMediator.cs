using MHZE.InteractSystem;
using MHZE.InputPromptSystem;
using UnityEngine;

public class InteractInputPromptMediator : MonoBehaviour
{
    [SerializeField] private InteractSystem interactSystem;
    [SerializeField] private InputPromptManager inputPromptManager;

    [Header("Prompt Configuration")]
    [SerializeField] private string promptKey = "Interact";
    [SerializeField] private string promptId = "InteractPrompt";
    [SerializeField] private string holdPromptId = "InteractHoldPrompt";
    [SerializeField] private InputPromptLocation holdPromptLocation = InputPromptLocation.Center;
    [SerializeField] private string holdPromptPrefix = "";
    [SerializeField] private string holdPromptSuffix = "Release to interact...";

    private void OnEnable()
    {
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
        if (interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(promptKey, promptId);
        }
    }

    private void HandleInteractableLost(IInteractable interactable, IInteractor interactor)
    {
        inputPromptManager.HidePrompt(promptId);
        inputPromptManager.HidePrompt(holdPromptId);
    }

    private void HandleCurrentInteractableUpdated()
    {
        var interactable = interactSystem.CurrentInteractable;
        if (interactable != null && interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(promptKey, promptId);
        }
        else
        {
            inputPromptManager.HidePrompt(promptId);
            inputPromptManager.HidePrompt(holdPromptId);
        }
    }

    private void HandleHoldAttemptStarted(float holdTime)
    {
        inputPromptManager.HidePrompt(promptId);
        inputPromptManager.ShowCustomPrompt(
            holdPromptId,
            holdPromptLocation,
            holdPromptPrefix,
            holdPromptSuffix
        );
    }

    private void HandleHoldAttemptEnded()
    {
        inputPromptManager.HidePrompt(holdPromptId);
        RefreshPrompt();
    }

    private void HandlePerformedInteraction(GameObject obj, IInteractor interactor)
    {
        inputPromptManager.HidePrompt(promptId);
        inputPromptManager.HidePrompt(holdPromptId);
    }

    private void RefreshPrompt()
    {
        var interactable = interactSystem.CurrentInteractable;
        if (interactable != null && interactable.AllowPrompt)
        {
            inputPromptManager.ShowPrompt(promptKey, promptId);
        }
    }
}
