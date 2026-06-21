using MHZE.InputPromptSystem;
using MHZE.UseSystem;
using UnityEngine;

public class UseInputPromptMediator : MonoBehaviour
{
    [SerializeField] private UseSystem useSystem;
    [SerializeField] private InputPromptManager inputPromptManager;

    [Header("Prompt Definitions")]
    [SerializeField] private InputPromptDefinition usePromptDefinition;

    private IUsableTarget currentTarget;

    private void OnEnable()
    {
        if (useSystem == null)
        {
            Debug.LogError("[UseInputPromptMediator] useSystem is not assigned.", this);
            return;
        }

        if (inputPromptManager == null)
        {
            Debug.LogError("[UseInputPromptMediator] inputPromptManager is not assigned.", this);
            return;
        }

        if (usePromptDefinition == null)
        {
            Debug.LogWarning("[UseInputPromptMediator] usePromptDefinition is not assigned.", this);
        }

        useSystem.OnUsableTargetFound += HandleUsableTargetFound;
        useSystem.OnUsableTargetLost += HandleUsableTargetLost;
        useSystem.OnUseItem += HandleUseItem;
        useSystem.OnUseItemAtTarget += HandleUseItemAtTarget;
    }

    private void OnDisable()
    {
        if (useSystem == null) return;

        useSystem.OnUsableTargetFound -= HandleUsableTargetFound;
        useSystem.OnUsableTargetLost -= HandleUsableTargetLost;
        useSystem.OnUseItem -= HandleUseItem;
        useSystem.OnUseItemAtTarget -= HandleUseItemAtTarget;
    }

    private void HandleUsableTargetFound(IUsableTarget target)
    {
        if (inputPromptManager == null || usePromptDefinition == null) return;

        currentTarget = target;

        var toolId = useSystem.currentHeldItem != null
            ? useSystem.currentHeldItem.GetToolId()
            : "Hand";

        ShowUsePrompt(target, toolId);
    }

    private void HandleUsableTargetLost()
    {
        if (inputPromptManager == null) return;

        currentTarget = null;

        RefreshPrompt();
    }

    private void HandleUseItem(IUsable usable)
    {
        if (inputPromptManager == null) return;

        RefreshPrompt();
    }

    private void HandleUseItemAtTarget(IUsable usable, IUsableTarget target)
    {
        if (inputPromptManager == null) return;

        RefreshPrompt();
    }

    private void ShowUsePrompt(IUsableTarget target, string toolId)
    {
        if (inputPromptManager == null || usePromptDefinition == null) return;

        var prefix = ResolvePrefix(target, toolId, usePromptDefinition);
        var suffix = ResolveSuffix(target, toolId, usePromptDefinition);
        inputPromptManager.ShowPrompt(usePromptDefinition.Key, usePromptDefinition.Key, prefix, suffix);
    }

    private void RefreshPrompt()
    {
        if (inputPromptManager == null || usePromptDefinition == null) return;

        if (currentTarget != null)
        {
            var toolId = useSystem.currentHeldItem != null
                ? useSystem.currentHeldItem.GetToolId()
                : "Hand";

            ShowUsePrompt(currentTarget, toolId);
        }
        else
        {
            inputPromptManager.HidePrompt(usePromptDefinition.Key);
        }
    }

    private static string ResolvePrefix(IUsableTarget target, string toolId, InputPromptDefinition definition)
    {
        var prompt = target.GetUsePromptPrefix(toolId);
        if (string.IsNullOrEmpty(prompt))
            return definition.PrefixText;
        return prompt;
    }

    private static string ResolveSuffix(IUsableTarget target, string toolId, InputPromptDefinition definition)
    {
        var prompt = target.GetUsePromptSuffix(toolId);
        if (string.IsNullOrEmpty(prompt))
            return definition.SuffixText;
        return prompt;
    }
}
