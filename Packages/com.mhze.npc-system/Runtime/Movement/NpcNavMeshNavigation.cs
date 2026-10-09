using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ModularNPC
{
    [Serializable]
    [NpcConflictsWithFeature(typeof(INpcNavigation))]
    [NpcFeature(
        "NavMesh Navigation",
        "Movement",
        Description = "Command-driven Unity NavMeshAgent navigation. Rotation remains a separate capability.")]
    public sealed class NpcNavMeshNavigation : NpcCommandFeature, INpcNavigation, INpcTickable
    {
        [SerializeField, Min(0.01f)] private float _defaultSpeed = 3.5f;
        [SerializeField, Min(0f)] private float _defaultStoppingDistance = 0.15f;
        [SerializeField, Min(0f)] private float _arrivalVelocityThreshold = 0.05f;
        [SerializeField, Min(0f)] private float _statusPollInterval = 0.05f;
        [SerializeField] private bool _disableAgentRotation = true;

        private NavMeshAgent _agent;
        private Vector3 _destination;
        private float _activeStoppingDistance;
        private bool _acceptPartialPath;
        private bool _externallyControlled;
        private NpcNavigationState _state;

        public Vector3 Destination => _destination;

        public Vector3 Velocity => _agent != null && _agent.enabled ? _agent.velocity : Vector3.zero;

        public float RemainingDistance => CanUseAgent && !_agent.pathPending
            ? _agent.remainingDistance
            : float.PositiveInfinity;

        public NpcNavigationState State => _state;

        public bool IsMoving => HasActiveCommand &&
                                (_state == NpcNavigationState.CalculatingPath ||
                                 _state == NpcNavigationState.Moving);

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _statusPollInterval);

        /// <summary>
        /// Temporarily hands the agent to another NPC feature without completing or replacing the
        /// active MoveTo command. The original destination and command handle remain active.
        /// </summary>
        public bool BeginExternalNavigationControl()
        {
            if (!HasActiveCommand || !CanUseAgent)
            {
                return false;
            }

            _externallyControlled = true;
            _agent.isStopped = true;
            _agent.ResetPath();
            return true;
        }

        /// <summary>Sets a temporary agent destination while external control is active.</summary>
        public bool SetExternalDestination(Vector3 destination, float stoppingDistance)
        {
            if (!_externallyControlled || !CanUseAgent || !NpcMath.IsFinite(destination))
            {
                return false;
            }

            _agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
            _agent.isStopped = false;
            return _agent.SetDestination(destination);
        }

        /// <summary>Returns the agent to the original active MoveTo destination.</summary>
        public bool EndExternalNavigationControl()
        {
            if (!_externallyControlled || !CanUseAgent || !HasActiveCommand)
            {
                _externallyControlled = false;
                return false;
            }

            _externallyControlled = false;
            _agent.stoppingDistance = _activeStoppingDistance;
            _agent.isStopped = false;
            return _agent.SetDestination(_destination);
        }

        private bool CanUseAgent => _agent != null &&
                                    _agent.enabled &&
                                    _agent.gameObject.activeInHierarchy &&
                                    _agent.isOnNavMesh;

        public NpcCommandStartResult MoveTo(
            Vector3 destination,
            NpcMoveOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!NpcMath.IsFinite(destination))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            if (!CanUseAgent)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.FeatureUnavailable);
            }

            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            _destination = destination;
            _activeStoppingDistance = options.OverrideStoppingDistance
                ? options.StoppingDistance
                : _defaultStoppingDistance;
            _acceptPartialPath = options.AcceptPartialPath;
            _state = NpcNavigationState.CalculatingPath;

            _agent.speed = options.OverrideSpeed ? options.Speed : _defaultSpeed;
            _agent.stoppingDistance = _activeStoppingDistance;
            if (_disableAgentRotation)
            {
                _agent.updateRotation = false;
            }

            _agent.isStopped = false;
            if (!_agent.SetDestination(destination))
            {
                _state = NpcNavigationState.Unreachable;
                FailActiveCommand();
                return result;
            }

            SetTicking(true);
            return result;
        }

        public bool Warp(Vector3 position)
        {
            if (!NpcMath.IsFinite(position) || !CanUseAgent)
            {
                return false;
            }

            CancelActiveCommand();
            bool warped = _agent.Warp(position);
            if (warped)
            {
                _destination = position;
                _state = NpcNavigationState.Idle;
            }

            return warped;
        }

        public void Tick(float deltaTime)
        {
            if (_externallyControlled)
            {
                return;
            }

            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            if (!CanUseAgent)
            {
                _state = NpcNavigationState.Unreachable;
                FailActiveCommand();
                return;
            }

            if (_agent.pathPending)
            {
                _state = NpcNavigationState.CalculatingPath;
                return;
            }

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                (!_acceptPartialPath && _agent.pathStatus == NavMeshPathStatus.PathPartial))
            {
                _state = NpcNavigationState.Unreachable;
                FailActiveCommand();
                return;
            }

            float remainingDistance = _agent.remainingDistance;
            bool insideStoppingDistance = NpcMath.IsFinite(remainingDistance) &&
                                          remainingDistance <= _activeStoppingDistance + 0.01f;
            bool nearlyStopped = _agent.velocity.sqrMagnitude <=
                                 _arrivalVelocityThreshold * _arrivalVelocityThreshold;

            if (insideStoppingDistance && (!_agent.hasPath || nearlyStopped))
            {
                _state = NpcNavigationState.Arrived;
                SucceedActiveCommand();
                return;
            }

            _state = NpcNavigationState.Moving;
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            NavMeshAgent agent = _agent != null ? _agent : GetComponent<NavMeshAgent>();
            if (agent != null && _disableAgentRotation && agent.updateRotation)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Info,
                    "The internal NavMeshAgent rotation will be disabled so rotation can be controlled separately.",
                    Npc));
            }
        }

        protected override void OnFeatureInitialized()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                _agent = GameObject.AddComponent<NavMeshAgent>();
            }

            _agent.hideFlags |= HideFlags.HideInInspector;
            _state = NpcNavigationState.Idle;
            if (_disableAgentRotation)
            {
                _agent.updateRotation = false;
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            _externallyControlled = false;
            SetTicking(false);
            if (CanUseAgent)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            if (status != NpcCommandStatus.Succeeded && _state != NpcNavigationState.Unreachable)
            {
                _state = NpcNavigationState.Idle;
            }
        }

        protected override void OnFeatureValidate()
        {
            _defaultSpeed = Mathf.Max(0.01f, _defaultSpeed);
            _defaultStoppingDistance = Mathf.Max(0f, _defaultStoppingDistance);
            _arrivalVelocityThreshold = Mathf.Max(0f, _arrivalVelocityThreshold);
            _statusPollInterval = Mathf.Max(0f, _statusPollInterval);
        }
    }
}
