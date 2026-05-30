using MHZE.InputPromptSystem;
using UnityEngine;

public class UseInputPromptMediator : MonoBehaviour
{
    [SerializeField] private UseSystem useSystem;
    [SerializeField] private InputPromptManager inputPromptManager;

    [Header("Prompt Definitions")]
    [SerializeField] private InputPromptDefinition usePromptDefinition;

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

        var toolName = useSystem.currentHeldItem != null
            ? useSystem.currentHeldItem.GetToolName()
            : ToolsName.Hand;

        ShowUsePrompt(target, toolName);
    }

    private void HandleUsableTargetLost()
    {
        if (inputPromptManager == null) return;

        HideUsePrompt();
    }

    private void HandleUseItem(IUsable usable)
    {
        if (inputPromptManager == null) return;

        HideUsePrompt();
    }

    private void HandleUseItemAtTarget(IUsable usable, IUsableTarget target)
    {
        if (inputPromptManager == null) return;

        HideUsePrompt();
    }

    private void ShowUsePrompt(IUsableTarget target, ToolsName toolName)
    {
        if (inputPromptManager == null || usePromptDefinition == null) return;

        var prefix = ResolvePrefix(target, toolName, usePromptDefinition);
        var suffix = ResolveSuffix(target, toolName, usePromptDefinition);
        inputPromptManager.ShowPrompt(usePromptDefinition.Key, usePromptDefinition.Key, prefix, suffix);
    }

    private void HideUsePrompt()
    {
        if (usePromptDefinition != null)
        {
            inputPromptManager.HidePrompt(usePromptDefinition.Key);
        }
    }

    private static string ResolvePrefix(IUsableTarget target, ToolsName toolName, InputPromptDefinition definition)
    {
        var prompt = target.GetUsePrompt(toolName, 0);
        if (string.IsNullOrEmpty(prompt))
            return definition.PrefixText;
        return prompt;
    }

    private static string ResolveSuffix(IUsableTarget target, ToolsName toolName, InputPromptDefinition definition)
    {
        var prompt = target.GetUsePrompt(toolName, 1);
        if (string.IsNullOrEmpty(prompt))
            return definition.SuffixText;
        return prompt;
    }
}
