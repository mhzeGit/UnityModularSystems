using CleanStateMachine;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// True once the NPC has reached the destination it was last sent to. Unreachable destinations can
    /// optionally count as done, so a patrol keeps cycling instead of stalling on a bad waypoint.
    /// </summary>
    public sealed class NpcArrived_Condition : ConditionScript
    {
        public override string DisplayName => "NPC Arrived";

        [Tooltip("Treat an unreachable destination as done and move on to the next state.")]
        public bool treatUnreachableAsDone = true;

        public override bool Evaluate(StateMachineComponent stateMachine)
        {
            if (stateMachine == null)
            {
                return false;
            }

            Npc npc = stateMachine.GetComponent<Npc>();
            if (npc == null)
            {
                npc = stateMachine.GetComponentInParent<Npc>();
            }

            if (npc == null || !npc.Features.TryGetOperational(out INpcNavigation navigation))
            {
                return false;
            }

            NpcNavigationState state = navigation.State;
            if (state == NpcNavigationState.Arrived)
            {
                return true;
            }

            return treatUnreachableAsDone && state == NpcNavigationState.Unreachable;
        }
    }
}
