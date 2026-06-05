using System.Collections.Generic;
using UnityEngine;

namespace MHZE.UseSystem
{
    public interface IUsableTarget
    {
        bool GetCanUseAtTarget();
        void SetCanUseAtTarget(bool Bool);
        void Used(GameObject usedBy, string usedToolId, RaycastHit HitResults);
        IReadOnlyList<string> GetAcceptedToolIds();
        string GetTargetId();
        string GetUsePromptPrefix(string toolId);
        string GetUsePromptSuffix(string toolId);
    }
}
