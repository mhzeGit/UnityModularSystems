using CleanStateMachine;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>True when the shared NPC door-navigation feature could not open its current door.</summary>
    public sealed class NpcDoorOpenFailed_Condition : ConditionScript
    {
        public override string DisplayName => "NPC Door Open Failed";

        public override bool Evaluate(StateMachineComponent stateMachine)
        {
            ModularNPC.Npc npc = ResolveNpc(stateMachine);
            return npc != null && npc.Features.TryGet(out NpcDoorNavigation feature) && feature.HasOpenFailed;
        }

        private static ModularNPC.Npc ResolveNpc(StateMachineComponent stateMachine)
        {
            if (stateMachine == null)
                return null;
            ModularNPC.Npc npc = stateMachine.GetComponent<ModularNPC.Npc>();
            return npc != null ? npc : stateMachine.GetComponentInParent<ModularNPC.Npc>();
        }
    }
}
