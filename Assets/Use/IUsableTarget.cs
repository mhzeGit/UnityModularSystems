//Made By MHZE

using System.Collections.Generic;
using UnityEngine;

public interface IUsableTarget
{
    bool GetCanUseAtTaget();
    void SetCanUseAtTarget(bool Bool);
    void Used(GameObject usedBy, ToolsName usedToolName, RaycastHit HitResults);
    List<ToolsName> GetTargetAcceptedToolNames();
    UseTargetsName GetUseTargetName();
    string GetUsePrompt(ToolsName toolname, int PromptIndex);
}