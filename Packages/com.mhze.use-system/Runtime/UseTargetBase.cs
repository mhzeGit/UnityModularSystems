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





    List<ToolsName> cachedAcceptedToolNames;
    bool isDirty = true;

    void OnValidate()
    {
        isDirty = true;
    }

    void OnEnable()
    {
        isDirty = true;
    }

    public bool GetCanUseAtTarget() => canUseAtTarget;

    public void SetCanUseAtTarget(bool Bool)
    {
        canUseAtTarget = Bool;
    }

    public IReadOnlyList<ToolsName> GetTargetAcceptedToolNames()
    {
        if (isDirty)
        {
            cachedAcceptedToolNames ??= new List<ToolsName>();
            cachedAcceptedToolNames.Clear();
            foreach (ToolData data in toolData)
            {
                cachedAcceptedToolNames.Add(data.acceptedToolName);
            }
            isDirty = false;
        }
        return cachedAcceptedToolNames;
    }

    public string GetUsePromptPrefix(ToolsName toolname)
    {
        foreach (var data in toolData)
        {
            if (data.acceptedToolName == toolname)
            {
                return data.usePromptPrefix;
            }
        }
        return string.Empty;
    }

    public string GetUsePromptSuffix(ToolsName toolname)
    {
        foreach (var data in toolData)
        {
            if (data.acceptedToolName == toolname)
            {
                return data.usePromptSuffix;
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
