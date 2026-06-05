using UnityEngine;

namespace MHZE.UseSystem
{
    public interface IUsable
    {
        void OnUsed(GameObject targetObject, string targetId);

        string GetToolId();

        bool GetIsUsable();
        float GetUseImpactDelay();
        float GetUseCooldown();

        void OnUsedOnTarget(GameObject targetObject, string targetId);
    }
}
