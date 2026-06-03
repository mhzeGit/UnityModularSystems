using System;
using UnityEngine;
using ArgEvent;

namespace MHZE.UseSystem
{
    public class UsableItemBase : MonoBehaviour, IUsable
{
    [Header("Use Settings")]
    [SerializeField] bool isUsable;
    [SerializeField] ToolsName toolName;
    [SerializeField] float useImpactDelay;
    [SerializeField] float useCooldown = 0.5f;
    public ArgEventBinding OnUsedItemEvent = new ArgEventBinding();
    public ArgEventBinding OnUsedItemOnTargetEvent = new ArgEventBinding();


    public event Action<GameObject, UseTargetsName> OnUsedItem;
    public event Action<GameObject, UseTargetsName> OnUsedItemOnTarget;






    public bool GetIsUsable() => isUsable;
    public ToolsName GetToolName() => toolName;
    public float GetUseImpactDelay() => useImpactDelay;
    public float GetUseCooldown() => useCooldown;

    public void OnUsed(GameObject target, UseTargetsName useTargetname)
    {
        OnUsedItemEvent.Invoke();
        OnUsedItem?.Invoke(target, useTargetname);
    }

    public void OnUsedOnTarget(GameObject targetObject, UseTargetsName useTargetname)
    {

        OnUsedItemOnTarget?.Invoke(targetObject, useTargetname);
        OnUsedItemOnTargetEvent.Invoke();
    }
}
}
