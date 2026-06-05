using System;
using UnityEngine;
using ArgEvent;

namespace MHZE.UseSystem
{
    public class UsableItemBase : MonoBehaviour, IUsable
    {
        [Header("Use Settings")]
        [SerializeField] bool isUsable;
        [SerializeField] ToolId toolId;
        [SerializeField] float useImpactDelay;
        [SerializeField] float useCooldown = 0.5f;
        public ArgEventBinding OnUsedItemEvent = new ArgEventBinding();
        public ArgEventBinding OnUsedItemOnTargetEvent = new ArgEventBinding();

        public event Action<GameObject, string> OnUsedItem;
        public event Action<GameObject, string> OnUsedItemOnTarget;

        public bool GetIsUsable() => isUsable;
        public string GetToolId() => toolId.value;
        public float GetUseImpactDelay() => useImpactDelay;
        public float GetUseCooldown() => useCooldown;

        public void OnUsed(GameObject target, string targetId)
        {
            OnUsedItemEvent.Invoke();
            OnUsedItem?.Invoke(target, targetId);
        }

        public void OnUsedOnTarget(GameObject targetObject, string targetId)
        {
            OnUsedItemOnTarget?.Invoke(targetObject, targetId);
            OnUsedItemOnTargetEvent.Invoke();
        }
    }
}
