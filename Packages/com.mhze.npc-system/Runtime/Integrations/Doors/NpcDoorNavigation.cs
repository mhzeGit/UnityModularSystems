using System;
using MHZE.UltimateDoorSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;

namespace ModularNPC
{
    public enum NpcDoorNavigationStatus
    {
        Idle,
        Navigating,
        OpeningDoor,
        Completed,
        OpenFailed,
        Unreachable
    }

    /// <summary>
    /// Lets an NPC open any closed Ultimate Door that is directly in front of it while it walks.
    /// It never plans routes or targets doors: while a navigation command is moving, it senses what is
    /// ahead of the agent and, when a closed door is within <c>Stop Distance</c>, it
    /// stops, pauses, opens the door, waits for it to finish opening, pauses again and then resumes
    /// the original destination. State machines are unaware of it and only issue normal MoveTo
    /// commands; <see cref="Status"/> is exposed for the rare machine that wants to react to a locked door.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "BlahBlahFamily.Gameplay", "Assembly-CSharp", null)]
    [NpcFeature("Door Navigation", "Movement", Description = "Opens any closed door directly in front of the NPC while it walks: stops close to the door, pauses, opens it, pauses again, then resumes the destination. No state-machine setup needed.")]
    [NpcRequiresFeature(typeof(NpcNavMeshNavigation))]
    public sealed class NpcDoorNavigation : NpcFeature, INpcTickable
    {
        private enum Phase
        {
            /// <summary>Walking normally, watching for a door ahead.</summary>
            Scanning,
            /// <summary>Stopped at the door, pausing before reaching for it.</summary>
            Settling,
            /// <summary>Door is opening, waiting for it to finish.</summary>
            Opening,
            /// <summary>Door is open, pausing before continuing the journey.</summary>
            Passing,
            /// <summary>Door could not be opened (locked or stuck). The NPC stays put.</summary>
            Failed
        }

        // Vertical half-size of the sensing box, centred on Sense Height. Covers the door panel but
        // not the floor, so ramps and thresholds are never mistaken for a door.
        private const float SenseHalfHeight = 0.6f;
        private const int MaxSensedColliders = 16;

        [Tooltip("Distance from the NPC's pivot to the door surface at which it stops. The NPC capsule radius is added by the body itself, so ~0.6 leaves a small gap.")]
        [SerializeField, Min(0.2f)] private float _stopDistance = 0.6f;
        [Tooltip("Seconds the NPC stands still at the door before it reaches for it. Counted from when it has actually come to rest.")]
        [SerializeField, Min(0f)] private float _pauseBeforeOpen = 0.1f;
        [Tooltip("Seconds the NPC waits after the door has finished opening before it walks on. 0 resumes in the same frame the door reports Open.")]
        [SerializeField, Min(0f)] private float _pauseAfterOpen = 0f;
        [Tooltip("Seconds to wait for the door to report Open before giving up.")]
        [SerializeField, Min(0.1f)] private float _openTimeout = 4f;
        [Tooltip("Height above the NPC's feet at which the sensing box is centred.")]
        [SerializeField, Min(0.1f)] private float _senseHeight = 1f;
        [Tooltip("Layers searched for doors.")]
        [SerializeField] private LayerMask _doorLayers = ~0;

        [NonSerialized] private NpcNavMeshNavigation _navigation;
        [NonSerialized] private NavMeshAgent _agent;
        [NonSerialized] private Collider[] _sensed;
        [NonSerialized] private UltimateDoorController _door;
        [NonSerialized] private NpcCommandHandle _command;
        [NonSerialized] private Phase _phase;
        [NonSerialized] private float _phaseEndsAt;
        [NonSerialized] private bool _failedBecauseLocked;

        public NpcTickSettings TickSettings => NpcTickSettings.EveryUpdate;

        /// <summary>High level result for state machines. Door handling itself needs no state-machine support.</summary>
        public NpcDoorNavigationStatus Status
        {
            get
            {
                switch (_phase)
                {
                    case Phase.Settling:
                    case Phase.Opening:
                    case Phase.Passing:
                        return NpcDoorNavigationStatus.OpeningDoor;
                    case Phase.Failed:
                        return NpcDoorNavigationStatus.OpenFailed;
                }

                if (_navigation == null)
                    return NpcDoorNavigationStatus.Idle;

                switch (_navigation.State)
                {
                    case NpcNavigationState.Arrived:
                        return NpcDoorNavigationStatus.Completed;
                    case NpcNavigationState.Unreachable:
                        return NpcDoorNavigationStatus.Unreachable;
                    case NpcNavigationState.CalculatingPath:
                    case NpcNavigationState.Moving:
                        return NpcDoorNavigationStatus.Navigating;
                    default:
                        return NpcDoorNavigationStatus.Idle;
                }
            }
        }

        /// <summary>The door the NPC is currently waiting at or failed to open, otherwise null.</summary>
        public UltimateDoorController ActiveDoor => _phase == Phase.Scanning ? null : _door;

        public UltimateDoorController FailedDoor => _phase == Phase.Failed ? _door : null;

        public bool HasOpenFailed => _phase == Phase.Failed;

        public void Tick(float deltaTime)
        {
            if (!IsOperational || !ResolveDependencies())
                return;

            // The navigation command we paused was cancelled, replaced or finished (a state machine
            // issued a new MoveTo, the NPC was warped, ...). Navigation already released the agent.
            if (_phase != Phase.Scanning && (!_navigation.HasActiveCommand || _navigation.ActiveCommand != _command))
            {
                EndInteraction(false);
                return;
            }

            switch (_phase)
            {
                case Phase.Scanning:
                    ScanForDoor();
                    break;

                case Phase.Settling:
                    // The pause only counts once the agent has actually braked to a halt.
                    if (_agent.velocity.sqrMagnitude > 0.01f)
                        _phaseEndsAt = Time.time + _pauseBeforeOpen;
                    else if (Time.time >= _phaseEndsAt)
                        TryOpenDoor();
                    break;

                case Phase.Opening:
                    TickOpening();
                    break;

                case Phase.Passing:
                    if (Time.time >= _phaseEndsAt)
                        EndInteraction(true);
                    break;

                case Phase.Failed:
                    TickFailed();
                    break;
            }
        }

        protected override void OnFeatureInitialized()
        {
            _sensed = new Collider[MaxSensedColliders];
            _phase = Phase.Scanning;
        }

        protected override void OnFeatureActivated() => SetTicking(true);
        protected override void OnFeatureDeactivated() => ReleaseNavigation();
        protected override void OnFeatureShutdown() => ReleaseNavigation();

        private bool ResolveDependencies()
        {
            if (_navigation == null && !Npc.Features.TryGetOperational(out _navigation))
                return false;

            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            return _agent != null && _agent.enabled && _agent.isOnNavMesh;
        }

        // ---------------------------------------------------------------- sensing

        private void ScanForDoor()
        {
            if (!_navigation.IsMoving || _agent.pathPending)
                return;

            // Where the agent is actually heading, which is available as soon as it has a path
            // (even before it has picked up speed), unlike the body's facing or velocity.
            Vector3 heading = _agent.desiredVelocity;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f)
                return;
            heading.Normalize();

            // Look far enough ahead that braking at the agent's acceleration brings it to rest
            // roughly Stop Distance from the door, whatever its walking speed.
            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float braking = speed * speed / (2f * Mathf.Max(0.1f, _agent.acceleration));
            float reach = _stopDistance + braking + speed * Time.deltaTime;

            UltimateDoorController door = FindClosedDoorAhead(heading, reach);
            if (door == null || !_navigation.BeginExternalNavigationControl())
                return;

            _door = door;
            _command = _navigation.ActiveCommand;
            _failedBecauseLocked = false;
            EnterPhase(Phase.Settling, _pauseBeforeOpen);
        }

        private UltimateDoorController FindClosedDoorAhead(Vector3 heading, float reach)
        {
            Vector3 origin = Npc.transform.position + Vector3.up * _senseHeight;
            float halfWidth = Mathf.Max(0.1f, _agent.radius);

            int count = Physics.OverlapBoxNonAlloc(
                origin + heading * (reach * 0.5f),
                new Vector3(halfWidth, SenseHalfHeight, reach * 0.5f),
                _sensed,
                Quaternion.LookRotation(heading),
                _doorLayers,
                QueryTriggerInteraction.Ignore);

            UltimateDoorController nearest = null;
            float nearestDistance = reach;
            for (int i = 0; i < count; i++)
            {
                Collider sensed = _sensed[i];
                _sensed[i] = null;
                if (sensed == null)
                    continue;

                UltimateDoorController door = sensed.GetComponentInParent<UltimateDoorController>();
                if (door == null || door.State == UltimateDoorController.DoorState.Open)
                    continue;

                Vector3 toDoor = ClosestPointOn(sensed, origin) - origin;
                toDoor.y = 0f;
                float distance = toDoor.magnitude;
                if (distance > nearestDistance)
                    continue;

                // Must really be in front, not just clipped by the corner of the sensing box.
                if (distance > 0.01f && Vector3.Dot(toDoor / distance, heading) < 0.5f)
                    continue;

                nearest = door;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static Vector3 ClosestPointOn(Collider collider, Vector3 point)
        {
            // Collider.ClosestPoint is only valid on convex colliders.
            if (collider is MeshCollider mesh && !mesh.convex)
                return collider.bounds.ClosestPoint(point);
            return collider.ClosestPoint(point);
        }

        // ------------------------------------------------------------ interaction

        private void TryOpenDoor()
        {
            if (_door == null)
            {
                EndInteraction(true);
                return;
            }

            // Somebody else (the player, another NPC) already opened it while we were pausing.
            if (_door.State == UltimateDoorController.DoorState.Open)
            {
                BeginPassing();
                return;
            }

            // Also plays the door's own "attempt to open locked door" feedback when it is locked.
            _door.Open(Npc.transform);

            if (_door.State == UltimateDoorController.DoorState.Locked)
                Fail(true);
            else
                EnterPhase(Phase.Opening, _openTimeout);
        }

        private void TickOpening()
        {
            if (_door == null)
            {
                EndInteraction(true);
                return;
            }

            switch (_door.State)
            {
                case UltimateDoorController.DoorState.Open:
                    BeginPassing();
                    break;

                case UltimateDoorController.DoorState.Locked:
                    Fail(true);
                    break;

                default:
                    if (Time.time >= _phaseEndsAt)
                        Fail(false);
                    break;
            }
        }

        private void TickFailed()
        {
            // A locked door that gets unlocked later (player, script, another NPC) is simply tried again.
            if (_door == null)
                EndInteraction(true);
            else if (_failedBecauseLocked && _door.State != UltimateDoorController.DoorState.Locked)
            {
                _failedBecauseLocked = false;
                EnterPhase(Phase.Settling, _pauseBeforeOpen);
            }
        }

        /// <summary>The door is open: resume at once, or after the optional pause.</summary>
        private void BeginPassing()
        {
            if (_pauseAfterOpen > 0f)
                EnterPhase(Phase.Passing, _pauseAfterOpen);
            else
                EndInteraction(true);
        }

        private void Fail(bool locked)
        {
            _failedBecauseLocked = locked;
            EnterPhase(Phase.Failed, 0f);
        }

        private void EnterPhase(Phase phase, float duration)
        {
            _phase = phase;
            _phaseEndsAt = Time.time + duration;
        }

        /// <summary>Leaves the door interaction and, if the command is still ours, resumes the journey.</summary>
        private void EndInteraction(bool resumeNavigation)
        {
            bool wasInteracting = _phase != Phase.Scanning;
            _phase = Phase.Scanning;
            _door = null;
            _command = default;
            _failedBecauseLocked = false;

            if (wasInteracting && resumeNavigation && _navigation != null)
                _navigation.EndExternalNavigationControl();
        }

        private void ReleaseNavigation()
        {
            bool resume = _phase != Phase.Scanning && _navigation != null &&
                          _navigation.HasActiveCommand && _navigation.ActiveCommand == _command;
            EndInteraction(resume);
        }
    }
}
