//Made By MHZE

using UnityEngine;

public interface IUsable
{

    // Called when tool is used.
    void OnUsed(GameObject targetObject, UseTargetsName useTargetname);

    // Returns the type name of this tool
    ToolsName GetToolName();

    // Returns if this tool usable or not
    bool GetIsUsable();
    float GetUseImpactDelay();
    float GetUseCooldown();

    void OnUsedOnTarget(GameObject targetObject, UseTargetsName useTargetname);
   



}