using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    [Serializable]
    [NpcFeature(
        "Transform Look",
        "Presentation",
        Description = "Command-driven head, eye, turret, or aim-transform tracking with optional angular limits.")]
    public sealed class NpcTransformLook : NpcCommandFeature, INpcLook, INpcTickable
    {
        private enum LookTargetMode
        {
            Position,
            Direction,
            Transform,
            Rest
        }

        [SerializeField] private Transform _lookTransform;
        [SerializeField, Min(0.01f)] private float _defaultDegreesPerSecond = 360f;
        [SerializeField, Min(0f)] private float _defaultAngularTolerance = 0.5f;
        [SerializeField] private bool _limitAngles = true;
        [SerializeField, Range(-180f, 0f)] private float _minimumYaw = -80f;
        [SerializeField, Range(0f, 180f)] private float _maximumYaw = 80f;
        [SerializeField, Range(-90f, 0f)] private float _minimumPitch = -45f;
        [SerializeField, Range(0f, 90f)] private float _maximumPitch = 60f;

        private LookTargetMode _targetMode;
        private Transform _targetTransform;
        private Vector3 _targetVector;
        private Quaternion _restLocalRotation;
        private float _degreesPerSecond;
        private float _angularTolerance;
        private float _remainingAngle;
        private bool _keepTracking;

        public Transform LookTransform => _lookTransform;

        public bool IsLooking => HasActiveCommand;

        public float RemainingAngle => _remainingAngle;

        public NpcTickSettings TickSettings => NpcTickSettings.EveryLateUpdate;

        public NpcCommandStartResult LookAtPosition(
            Vector3 worldPosition,
            NpcLookOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!HasValidLookTransform ||
                !NpcMath.IsFinite(worldPosition) ||
                (worldPosition - _lookTransform.position).sqrMagnitude <= 0.000001f)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginLook(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = LookTargetMode.Position;
                _targetVector = worldPosition;
                _keepTracking = false;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult LookInDirection(
            Vector3 worldDirection,
            NpcLookOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!HasValidLookTransform ||
                !NpcMath.IsFinite(worldDirection) ||
                worldDirection.sqrMagnitude <= 0.000001f)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginLook(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = LookTargetMode.Direction;
                _targetVector = worldDirection;
                _keepTracking = false;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult LookAtTarget(
            Transform target,
            NpcLookOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!HasValidLookTransform ||
                target == null ||
                target == _lookTransform ||
                (target.position - _lookTransform.position).sqrMagnitude <= 0.000001f)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginLook(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = LookTargetMode.Transform;
                _targetTransform = target;
                _keepTracking = options.KeepTracking;
                SetTicking(true);
            }

            return result;
        }

        public NpcCommandStartResult ReturnToRest(
            NpcLookOptions options,
            NpcCommandRequest commandRequest)
        {
            if (!HasValidLookTransform)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.FeatureUnavailable);
            }

            NpcCommandStartResult result = BeginLook(options, commandRequest);
            if (result.Accepted)
            {
                _targetMode = LookTargetMode.Rest;
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

            if (!TryResolveTargetRotation(out Quaternion targetRotation))
            {
                FailActiveCommand();
                return;
            }

            _remainingAngle = Quaternion.Angle(_lookTransform.rotation, targetRotation);
            if (_remainingAngle <= _angularTolerance)
            {
                _lookTransform.rotation = targetRotation;
                _remainingAngle = 0f;
                if (!_keepTracking)
                {
                    SucceedActiveCommand();
                }

                return;
            }

            _lookTransform.rotation = Quaternion.RotateTowards(
                _lookTransform.rotation,
                targetRotation,
                _degreesPerSecond * deltaTime);
            _remainingAngle = Quaternion.Angle(_lookTransform.rotation, targetRotation);
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            if (_lookTransform == null)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Error,
                    "Transform Look requires a look transform.",
                    Npc));
                return;
            }

            if (_lookTransform != Transform && !_lookTransform.IsChildOf(Transform))
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "The look transform is outside this NPC hierarchy.",
                    Npc));
            }

            if (_lookTransform == Transform && Npc.Features.Contains(typeof(INpcRotation)))
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "Transform Look and Rotation target the same root Transform and may compete. Assign a child head/eye transform.",
                    Npc));
            }
        }

        protected override void OnFeatureInitialized()
        {
            if (_lookTransform == null)
            {
                _lookTransform = Transform;
            }

            _restLocalRotation = _lookTransform.localRotation;
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            _targetTransform = null;
            _remainingAngle = 0f;
        }

        private bool HasValidLookTransform => _lookTransform != null;

        private NpcCommandStartResult BeginLook(
            NpcLookOptions options,
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
            if (_lookTransform == null)
            {
                rotation = default;
                return false;
            }

            if (_targetMode == LookTargetMode.Rest)
            {
                rotation = _lookTransform.parent != null
                    ? _lookTransform.parent.rotation * _restLocalRotation
                    : _restLocalRotation;
                return true;
            }

            Vector3 direction;
            switch (_targetMode)
            {
                case LookTargetMode.Position:
                    direction = _targetVector - _lookTransform.position;
                    break;

                case LookTargetMode.Direction:
                    direction = _targetVector;
                    break;

                case LookTargetMode.Transform:
                    if (_targetTransform == null)
                    {
                        rotation = default;
                        return false;
                    }

                    direction = _targetTransform.position - _lookTransform.position;
                    break;

                default:
                    rotation = default;
                    return false;
            }

            if (direction.sqrMagnitude <= 0.000001f)
            {
                rotation = default;
                return false;
            }

            rotation = _limitAngles
                ? GetLimitedRotation(direction)
                : Quaternion.LookRotation(direction.normalized, GetReferenceUp());
            return true;
        }

        private Quaternion GetLimitedRotation(Vector3 worldDirection)
        {
            Quaternion neutralWorldRotation = _lookTransform.parent != null
                ? _lookTransform.parent.rotation * _restLocalRotation
                : _restLocalRotation;
            Vector3 localDirection = Quaternion.Inverse(neutralWorldRotation) * worldDirection.normalized;

            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float horizontalLength = Mathf.Sqrt(
                localDirection.x * localDirection.x + localDirection.z * localDirection.z);
            float pitch = -Mathf.Atan2(localDirection.y, horizontalLength) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, _minimumYaw, _maximumYaw);
            pitch = Mathf.Clamp(pitch, _minimumPitch, _maximumPitch);
            return neutralWorldRotation * Quaternion.Euler(pitch, yaw, 0f);
        }

        private Vector3 GetReferenceUp()
        {
            return _lookTransform.parent != null ? _lookTransform.parent.up : Vector3.up;
        }

        protected override void OnFeatureValidate()
        {
            _defaultDegreesPerSecond = Mathf.Max(0.01f, _defaultDegreesPerSecond);
            _defaultAngularTolerance = Mathf.Max(0f, _defaultAngularTolerance);
            _minimumYaw = Mathf.Clamp(_minimumYaw, -180f, 0f);
            _maximumYaw = Mathf.Clamp(_maximumYaw, 0f, 180f);
            _minimumPitch = Mathf.Clamp(_minimumPitch, -90f, 0f);
            _maximumPitch = Mathf.Clamp(_maximumPitch, 0f, 90f);
        }
    }
}
