using CleanStateMachine;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Sends the NPC to the next waypoint of its <see cref="NpcPatrolRoute"/> when the state is
    /// entered. Pair it with an <see cref="NpcArrived_Condition"/> on the outgoing transition.
    /// While moving, the NPC turns to face the direction it is actually travelling, not the waypoint,
    /// so it does not strafe sideways around corners or along a curved path.
    /// </summary>
    public sealed class NpcMoveToPatrolPoint_StateBehaviour : StateBehaviour
    {
        public override string DisplayName => "NPC Move To Next Patrol Point";

        [Tooltip("Patrol speed in metres per second.")]
        public float speed = 2f;

        [Tooltip("Distance at which the waypoint counts as reached.")]
        public float stoppingDistance = 0.2f;

        [Tooltip("Turn the NPC towards the direction it is travelling while it walks.")]
        public bool faceMovementDirection = true;

        [Tooltip("Minimum speed in metres per second before the NPC turns towards its movement.")]
        public float minSpeedToFace = 0.05f;

        [Tooltip("When movement facing is disabled, turn the NPC to face the waypoint on entry.")]
        public bool faceWaypoint = true;

        [Tooltip("Logs a warning when the NPC, route or waypoints are missing.")]
        public bool warnWhenMissing = true;

        private Npc _npc;
        private INpcNavigation _navigation;
        private INpcRotation _rotation;
        private Transform _waypoint;
        private bool _warnedNoRotation;

        public override void OnStateEnter(StateMachineComponent stateMachine)
        {
            _npc = null;
            _navigation = null;
            _rotation = null;
            _waypoint = null;

            Npc npc = ResolveNpc(stateMachine);
            NpcPatrolRoute route = ResolveRoute(stateMachine);

            if (npc == null || route == null)
            {
                Warn("needs both a ModularNPC.Npc and an NpcPatrolRoute on the same GameObject.");
                return;
            }

            _npc = npc;

            if (!npc.Features.TryGetOperational(out _navigation))
            {
                Warn("has no operational navigation feature on " + npc.name + ".");
                return;
            }

            npc.Features.TryGetOperational(out _rotation);

            _waypoint = route.NextPoint();
            if (_waypoint == null)
            {
                Warn("has no waypoints. Assign pointsRoot and add waypoint children under it.");
                return;
            }

            NpcMoveOptions options = NpcMoveOptions.Default
                .WithSpeed(speed)
                .WithStoppingDistance(stoppingDistance);

            _navigation.MoveTo(
                _waypoint.position,
                options,
                new NpcCommandRequest(this, 0, NpcCommandInterruptPolicy.Always));

            if (!faceMovementDirection && faceWaypoint && _rotation != null)
            {
                _rotation.FacePosition(
                    _waypoint.position,
                    NpcRotationOptions.Default,
                    new NpcCommandRequest(this, 0, NpcCommandInterruptPolicy.Always));
            }
        }

        public override void OnStateUpdate(StateMachineComponent stateMachine)
        {
            if (!faceMovementDirection || _navigation == null)
            {
                return;
            }

            // Resolve lazily: the state machine can enter this state before the NPC features have
            // finished initializing, which would otherwise leave the NPC moving without turning.
            if (_rotation == null && _npc != null)
            {
                if (!_npc.Features.TryGetOperational(out _rotation) && !_warnedNoRotation)
                {
                    _warnedNoRotation = true;
                    Warn("has no operational rotation feature on " + _npc.name +
                         "; the NPC cannot face the direction it is moving.");
                }
            }

            if (_rotation == null)
            {
                return;
            }

            // Drive from the actual agent velocity, not the navigation state, so the NPC still turns
            // while the path is being calculated and if the state reporting lags behind the movement.
            Vector3 velocity = _navigation.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < minSpeedToFace * minSpeedToFace)
            {
                return;
            }

            _rotation.FaceDirection(
                velocity,
                NpcRotationOptions.Default,
                new NpcCommandRequest(this, 0, NpcCommandInterruptPolicy.Always));
        }

        public override void OnStateExit(StateMachineComponent stateMachine)
        {
            _npc = null;
            _navigation = null;
            _rotation = null;
            _waypoint = null;
            _warnedNoRotation = false;
        }

        private static Npc ResolveNpc(StateMachineComponent stateMachine)
        {
            if (stateMachine == null)
            {
                return null;
            }

            Npc npc = stateMachine.GetComponent<Npc>();
            return npc != null ? npc : stateMachine.GetComponentInParent<Npc>();
        }

        private static NpcPatrolRoute ResolveRoute(StateMachineComponent stateMachine)
        {
            if (stateMachine == null)
            {
                return null;
            }

            NpcPatrolRoute route = stateMachine.GetComponent<NpcPatrolRoute>();
            return route != null ? route : stateMachine.GetComponentInParent<NpcPatrolRoute>();
        }

        private void Warn(string message)
        {
            if (warnWhenMissing)
            {
                Debug.LogWarning($"[{DisplayName}] {message}");
            }
        }
    }
}
