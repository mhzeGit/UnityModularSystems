using CleanStateMachine;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>Legacy wrapper. Door handling is automatic for every NPC navigation command.</summary>
    public sealed class NpcNavigateThroughDoors_StateBehaviour : StateBehaviour
    {
        public override string DisplayName => "NPC Navigate To Destination";

        public Vector3 destination;
        [Tooltip("Navigation speed in metres per second.")]
        public float speed = 2f;
        [Tooltip("Arrival distance from the destination.")]
        public float stoppingDistance = 0.2f;
        public bool useTransformPosition;
        public Transform destinationTransform;

        public override void OnStateEnter(StateMachineComponent stateMachine)
        {
            Npc npc = stateMachine != null ? stateMachine.GetComponent<Npc>() : null;
            if (npc == null && stateMachine != null)
                npc = stateMachine.GetComponentInParent<Npc>();
            if (npc == null || !npc.Features.TryGetOperational(out INpcNavigation navigation))
            {
                Debug.LogWarning("[NPC Navigate Through Doors] An operational NPC navigation feature is required.");
                return;
            }

            Vector3 target = useTransformPosition && destinationTransform != null
                ? destinationTransform.position
                : destination;
            navigation.MoveTo(
                target,
                NpcMoveOptions.Default.WithSpeed(speed).WithStoppingDistance(stoppingDistance),
                new NpcCommandRequest(this, 0, NpcCommandInterruptPolicy.Always));
        }

        public override void OnStateExit(StateMachineComponent stateMachine)
        {
        }
    }
}
