// Made By MHZE

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UseTargetBase : MonoBehaviour, IUsableTarget
{
    [SerializeField] bool canUseAtTarget = true;

    [System.Serializable]
    public class ToolData
    {
        public ToolsName acceptedToolName;
        public string usePromptPrefix = "Press";
        public string usePromptSuffix = "To Use!";
    }
    public UseTargetsName useTargetsName;
    public List<ToolData> toolData;

    public UnityEvent OnUsedAtTargetEvent;
    public event Action<GameObject, ToolsName, RaycastHit> OnUsedAtTarget;





    public bool GetCanUseAtTaget() => canUseAtTarget;

    public void SetCanUseAtTarget(bool Bool)
    {
        canUseAtTarget = Bool;
    }

    public List<ToolsName> GetTargetAcceptedToolNames()
    {
        List<ToolsName> result = new List<ToolsName>();
        foreach (ToolData data in toolData)
        {
            result.Add(data.acceptedToolName);
        }
        return result;
    }

    public string GetUsePrompt(ToolsName toolname, int PromptIndex)
    {
        foreach (var data in toolData)
        {
            if (data.acceptedToolName == toolname)
                if(PromptIndex == 0)
                {
                    return data.usePromptPrefix;

                }
                else if (PromptIndex == 1)
                {
                    return data.usePromptSuffix;

                }
                else
                {
                    return "ERROR: Invalid Prompt Index";
                }
        }
        return string.Empty;
    }
    public UseTargetsName GetUseTargetName() => useTargetsName;

    public virtual void Used(GameObject usedBy, ToolsName usedToolName, RaycastHit HitResults)
    {
        OnUsedAtTarget?.Invoke(usedBy, usedToolName, HitResults);
        OnUsedAtTargetEvent.Invoke();

    }


}
