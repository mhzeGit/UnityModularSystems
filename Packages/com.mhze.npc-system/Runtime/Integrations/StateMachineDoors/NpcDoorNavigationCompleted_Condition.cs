using CleanStateMachine;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>True when door-aware navigation has reached its requested destination.</summary>
    public sealed class NpcDoorNavigationCompleted_Condition : ConditionScript
    {
        public override string DisplayName => "NPC Door Navigation Completed";

        public override bool Evaluate(StateMachineComponent stateMachine)
        {
            ModularNPC.Npc npc = ResolveNpc(stateMachine);
            return npc != null && npc.Features.TryGet(out NpcDoorNavigation feature) &&
                   feature.Status == NpcDoorNavigationStatus.Completed;
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
