using System;
using UnityEngine;

namespace ModularNPC
{
    [Serializable]
    [NpcFeature(
        "Rotation",
        "Movement",
        Description = "Command-driven body rotation toward positions, directions, transforms, or rotations.")]
    public sealed class NpcRotation : NpcCommandFeature, INpcRotation, INpcTickable
    {
        private enum RotationTargetMode
        {
            WorldPosition,
            WorldDirection,
            Transform,
            Quaternion
        }

        [SerializeField, Min(0.01f)] private float _defaultDegreesPerSecond = 360f;
        [SerializeField, Min(0f)] private float _defaultAngularTolerance = 0.5f;
        [SerializeField] private NpcRotationAxes _axes = NpcRotationAxes.YawOnly;
        [SerializeField] private NpcTickPhase _tickPhase = NpcTickPhase.Update;

        private RotationTargetMode _targetMode;
        private Transform _targetTransform;
        private Vector3 _targetVector;
        private Quaternion _targetRotation;
        private float _degreesPerSecond;
        private float _angularTolerance;
        private bool _keepTracking;
        private float _remainingAngle;

        public bool IsRotating => HasActiveCommand;

        public float RemainingAngle => _remainingAngle;

        public NpcTickSettings TickSettings => new NpcTickSettings(_tickPhase);

        public NpcCommandStartResult FacePosition(
            Vector3 worldPosition,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!NpcMath.IsFinite(worldPosition) ||
                !TryGetLookRotation(worldPosition - Transform.position, out _))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginRotation(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = RotationTargetMode.WorldPosition;
                _targetVector = worldPosition;
                _keepTracking = false;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult FaceDirection(
            Vector3 worldDirection,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!NpcMath.IsFinite(worldDirection) || !TryGetLookRotation(worldDirection, out _))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginRotation(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = RotationTargetMode.WorldDirection;
                _targetVector = worldDirection;
                _keepTracking = false;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult FaceTarget(
            Transform target,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest)
        {
            if (target == null || !TryGetLookRotation(target.position - Transform.position, out _))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginRotation(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = RotationTargetMode.Transform;
                _targetTransform = target;
                _keepTracking = options.KeepTracking;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult RotateTo(
            Quaternion worldRotation,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!NpcMath.IsFinite(worldRotation) || worldRotation == default)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginRotation(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = RotationTargetMode.Quaternion;
                _targetRotation = worldRotation;
                _keepTracking = false;
                SetTicking(true);
            }

            return result;
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            if (!TryResolveTargetRotation(out Quaternion desiredRotation))
            {
                FailActiveCommand();
                return;
            }

            _remainingAngle = Quaternion.Angle(Transform.rotation, desiredRotation);
            if (_remainingAngle <= _angularTolerance)
            {
                Transform.rotation = desiredRotation;
                _remainingAngle = 0f;
                if (!_keepTracking)
                {
                    SucceedActiveCommand();
                }

                return;
            }

            Transform.rotation = Quaternion.RotateTowards(
                Transform.rotation,
                desiredRotation,
                _degreesPerSecond * deltaTime);
            _remainingAngle = Quaternion.Angle(Transform.rotation, desiredRotation);
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            _targetTransform = null;
            _remainingAngle = 0f;
        }

        private NpcCommandStartResult BeginRotation(
            NpcRotationOptions options,
            NpcCommandRequest commandRequest)
        {
            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            _degreesPerSecond = options.OverrideSpeed
                ? options.DegreesPerSecond
                : _defaultDegreesPerSecond;
            _angularTolerance = options.OverrideTolerance
                ? options.AngularTolerance
                : _defaultAngularTolerance;
            _keepTracking = options.KeepTracking;
            return result;
        }

        private bool TryResolveTargetRotation(out Quaternion rotation)
        {
            switch (_targetMode)
            {
                case RotationTargetMode.WorldPosition:
                    return TryGetLookRotation(_targetVector - Transform.position, out rotation);

                case RotationTargetMode.WorldDirection:
                    return TryGetLookRotation(_targetVector, out rotation);

                case RotationTargetMode.Transform:
                    if (_targetTransform == null)
                    {
                        rotation = default;
                        return false;
                    }

                    return TryGetLookRotation(_targetTransform.position - Transform.position, out rotation);

                default:
                    rotation = _targetRotation;
                    if (_axes == NpcRotationAxes.YawOnly)
                    {
                        Vector3 forward = rotation * Vector3.forward;
                        return TryGetLookRotation(forward, out rotation);
                    }

                    return true;
            }
        }

        private bool TryGetLookRotation(Vector3 direction, out Quaternion rotation)
        {
            if (_axes == NpcRotationAxes.YawOnly)
            {
                direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            }

            if (direction.sqrMagnitude <= 0.000001f)
            {
                rotation = default;
                return false;
            }

            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }

        protected override void OnFeatureValidate()
        {
            _defaultDegreesPerSecond = Mathf.Max(0.01f, _defaultDegreesPerSecond);
            _defaultAngularTolerance = Mathf.Max(0f, _defaultAngularTolerance);
            if (Application.isPlaying)
            {
                RefreshTickSchedule();
            }
        }
    }
}
