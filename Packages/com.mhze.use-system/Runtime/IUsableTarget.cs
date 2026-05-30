//Made By MHZE

using System.Collections.Generic;
using UnityEngine;

public interface IUsableTarget
{
    bool GetCanUseAtTarget();
    void SetCanUseAtTarget(bool Bool);
    void Used(GameObject usedBy, ToolsName usedToolName, RaycastHit HitResults);
    IReadOnlyList<ToolsName> GetTargetAcceptedToolNames();
    UseTargetsName GetUseTargetName();
    string GetUsePromptPrefix(ToolsName toolname);
    string GetUsePromptSuffix(ToolsName toolname);
}