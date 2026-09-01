using System;
using UnityEngine;

namespace ModularNPC
{
    public enum NpcTransformMovementSpace
    {
        ThreeDimensional,
        HorizontalPlane
    }

    [Serializable]
    [NpcConflictsWithFeature(typeof(INpcNavigation))]
    [NpcFeature(
        "Transform Navigation",
        "Movement",
        Description = "Lightweight straight-line movement fallback with no NavMesh dependency.")]
    public sealed class NpcTransformNavigation : NpcCommandFeature, INpcNavigation, INpcTickable
    {
        [SerializeField, Min(0.01f)] private float _defaultSpeed = 3.5f;
        [SerializeField, Min(0f)] private float _defaultStoppingDistance = 0.05f;
        [SerializeField] private NpcTransformMovementSpace _movementSpace =
            NpcTransformMovementSpace.ThreeDimensional;
        [SerializeField] private NpcTickPhase _tickPhase = NpcTickPhase.Update;

        private Vector3 _destination;
        private Vector3 _velocity;
        private float _speed;
        private float _stoppingDistance;
        private NpcNavigationState _state;

        public Vector3 Destination => _destination;

        public Vector3 Velocity => _velocity;

        public float RemainingDistance => CalculateRemainingDistance(Transform.position, _destination);

        public NpcNavigationState State => _state;

        public bool IsMoving => HasActiveCommand && _state == NpcNavigationState.Moving;

        public NpcTickSettings TickSettings => new NpcTickSettings(_tickPhase);

        public NpcCommandStartResult MoveTo(
            Vector3 destination,
            NpcMoveOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!NpcMath.IsFinite(destination))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            _destination = destination;
            _speed = options.OverrideSpeed ? options.Speed : _defaultSpeed;
            _stoppingDistance = options.OverrideStoppingDistance
                ? options.StoppingDistance
                : _defaultStoppingDistance;
            _state = NpcNavigationState.Moving;
            _velocity = Vector3.zero;
            SetTicking(true);
            return result;
        }

        public bool Warp(Vector3 position)
        {
            if (!NpcMath.IsFinite(position))
            {
                return false;
            }

            CancelActiveCommand();
            Transform.position = position;
            _destination = position;
            _velocity = Vector3.zero;
            _state = NpcNavigationState.Idle;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            Vector3 current = Transform.position;
            Vector3 delta = _destination - current;
            if (_movementSpace == NpcTransformMovementSpace.HorizontalPlane)
            {
                delta.y = 0f;
            }

            float distance = delta.magnitude;
            if (distance <= _stoppingDistance)
            {
                _velocity = Vector3.zero;
                _state = NpcNavigationState.Arrived;
                SucceedActiveCommand();
                return;
            }

            float maxDistance = Mathf.Max(0f, _speed * deltaTime);
            Vector3 movement = delta.normalized * Mathf.Min(maxDistance, distance - _stoppingDistance);
            Transform.position = current + movement;
            _velocity = deltaTime > 0f ? movement / deltaTime : Vector3.zero;
            _state = NpcNavigationState.Moving;
        }

        protected override void OnFeatureInitialized()
        {
            _destination = Transform.position;
            _state = NpcNavigationState.Idle;
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            _velocity = Vector3.zero;
            if (status != NpcCommandStatus.Succeeded)
            {
                _state = NpcNavigationState.Idle;
            }
        }

        private float CalculateRemainingDistance(Vector3 current, Vector3 destination)
        {
            Vector3 delta = destination - current;
            if (_movementSpace == NpcTransformMovementSpace.HorizontalPlane)
            {
                delta.y = 0f;
            }

            return delta.magnitude;
        }

        protected override void OnFeatureValidate()
        {
            _defaultSpeed = Mathf.Max(0.01f, _defaultSpeed);
            _defaultStoppingDistance = Mathf.Max(0f, _defaultStoppingDistance);
            if (Application.isPlaying)
            {
                RefreshTickSchedule();
            }
        }
    }
}
