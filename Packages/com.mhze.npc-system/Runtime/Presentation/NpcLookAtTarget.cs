using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Turns one bone (a head, an eye, a turret, an aim transform) toward any <see cref="Transform"/>
    /// the caller hands it, and eases it back to its rest pose when told to stop.
    ///
    /// The feature is target-agnostic: it knows nothing about the player. Whatever drives the NPC - a
    /// state behaviour, a perception callback, a dialog - calls <see cref="LookAt"/> with the
    /// transform to follow and <see cref="StopLooking"/> when the look should end.
    ///
    /// A bone's pivot is rarely where the eyes are (a head bone sits at the base of the skull), so
    /// aiming the bone straight at the target makes the eyes look past it. <c>Eye Height Offset</c>
    /// is the distance from the bone's pivot to the middle of the eyes; the aim is corrected so the
    /// line from the eyes, not from the bone, passes through the target.
    ///
    /// Ticking runs in <see cref="NpcTickPhase.LateUpdate"/>, after the Animator has posed the
    /// skeleton, and only while looking or easing back, so an idle look costs nothing.
    /// </summary>
    [Serializable]
    [NpcFeature(
        "Look At Target",
        "Presentation",
        Description = "Turns a head, eye or aim bone toward any target Transform, with an eye-height offset so the eyes meet the target, and eases it back to rest when the look ends.")]
    public sealed class NpcLookAtTarget : NpcFeature, INpcLook, INpcTickable
    {
        private const string DefaultLookBoneName = "Head";
        private const float DefaultDegreesPerSecond = 360f;
        private const float DefaultSettleToleranceDegrees = 0.5f;

        [Tooltip("The bone that is turned. Leave empty to find it by name below.")]
        [SerializeField] private Transform _lookTransform;

        [Tooltip("Name of the bone to turn when no Look Transform is assigned, searched inside this NPC. Defaults to 'Head'.")]
        [SerializeField] private string _lookBoneName = DefaultLookBoneName;

        [Tooltip("Metres from the bone's pivot up to the middle of the eyes, along the bone's up axis. " +
                 "A head bone usually sits at the base of the skull, so aiming the bone itself at a target " +
                 "makes the eyes look past it. Raise this until the eyes meet the target; 0 aims the pivot " +
                 "directly at it.")]
        [SerializeField, Min(0f)] private float _eyeHeightOffset;

        [Tooltip("How fast the bone turns toward the target, in degrees per second.")]
        [SerializeField, Min(1f)] private float _degreesPerSecond = DefaultDegreesPerSecond;

        [Tooltip("How close to the rest rotation the bone must get before the ease-back stops.")]
        [SerializeField, Min(0.01f)] private float _settleToleranceDegrees = DefaultSettleToleranceDegrees;

        [Tooltip("Clamp the look to the yaw/pitch range below, measured from the bone's rest pose. " +
                 "Off lets the bone always aim straight at the target.")]
        [SerializeField] private bool _limitAngles;
        [SerializeField, Range(-180f, 0f)] private float _minimumYaw = -80f;
        [SerializeField, Range(0f, 180f)] private float _maximumYaw = 80f;
        [SerializeField, Range(-90f, 0f)] private float _minimumPitch = -45f;
        [SerializeField, Range(0f, 90f)] private float _maximumPitch = 60f;

        [Tooltip("While stationary, turn the body to face the target when its horizontal bearing exceeds this angle. 0 disables body turns. Existing rotation commands take precedence.")]
        [SerializeField, Range(0f, 180f)] private float _bodyTurnThresholdDegrees;

        [Tooltip("Disable automatic eye tracking. Otherwise LeftEye/RightEye bones follow the head target when present.")]
        [SerializeField] private bool _disableEyeTracking;
        [SerializeField] private string _leftEyeBoneName = "LeftEye";
        [SerializeField] private string _rightEyeBoneName = "RightEye";
        [Tooltip("Maximum eye yaw/pitch from neutral. Zero uses 30/20 degrees for older serialized features.")]
        [SerializeField] private Vector2 _eyeAngleLimits = new Vector2(30f, 20f);
        [Tooltip("Eye speed in degrees/second. Zero uses 720 for older serialized features.")]
        [SerializeField, Min(0f)] private float _eyeDegreesPerSecond = 720f;

        private sealed class EyePose
        {
            public Transform Bone;
            public Quaternion Rest;
            public Quaternion Current;
            public Vector3 Forward;
            public Vector3 Up;
        }

        [NonSerialized] private EyePose[] _eyes;
        [NonSerialized] private Transform _bone;
        [NonSerialized] private Transform _target;
        [NonSerialized] private float _rootScale = 1f;
        [NonSerialized] private Quaternion _restLocalRotation = Quaternion.identity;
        [NonSerialized] private Quaternion _currentWorldRotation;
        [NonSerialized] private float _remainingAngle;
        [NonSerialized] private bool _hasCurrentRotation;
        [NonSerialized] private bool _looking;
        [NonSerialized] private NpcCommandHandle _bodyTurn = NpcCommandHandle.Invalid;

        /// <summary>Horizontal angle that triggers a body turn; 0 keeps head-only tracking.</summary>
        public float BodyTurnThresholdDegrees
        {
            get => _bodyTurnThresholdDegrees;
            set => _bodyTurnThresholdDegrees = Mathf.Clamp(value, 0f, 180f);
        }

        /// <summary>The bone being turned, or null until it has been found.</summary>
        public Transform LookTransform => EnsureBone();

        /// <summary>The transform currently being looked at, or null when not looking.</summary>
        public Transform Target => _looking ? _target : null;

        /// <summary>True from <see cref="LookAt"/> until <see cref="StopLooking"/>.</summary>
        public bool IsLooking => _looking;

        /// <summary>Degrees left between the bone and its goal (the target, or rest while easing back).</summary>
        public float RemainingAngle => _remainingAngle;

        /// <summary>Metres from the bone's pivot to the eyes. Safe to change at runtime.</summary>
        public float EyeHeightOffset
        {
            get => _eyeHeightOffset;
            set => _eyeHeightOffset = Mathf.Max(0f, value);
        }

        // Look bones are animated, and the Animator applies its pose before LateUpdate in this tick
        // order, so ticking here keeps the look from being overwritten each frame.
        public NpcTickSettings TickSettings => NpcTickSettings.EveryLateUpdate;

        // A field added to a [SerializeReference] feature after a prefab was authored deserializes to
        // its default (0/empty), not to its C# initializer, so fall back to a constant when unset.
        private float TurnSpeed => _degreesPerSecond > 0f ? _degreesPerSecond : DefaultDegreesPerSecond;

        private float SettleTolerance =>
            _settleToleranceDegrees > 0f ? _settleToleranceDegrees : DefaultSettleToleranceDegrees;

        /// <summary>
        /// Starts (or retargets) a continuous look at <paramref name="target"/>. The bone follows the
        /// transform every frame until <see cref="StopLooking"/> or the target is destroyed.
        /// </summary>
        /// <returns>False when the target is null, the NPC itself or no bone could be found.</returns>
        public bool LookAt(Transform target)
        {
            if (target == null || target == Transform || EnsureBone() == null)
            {
                return false;
            }

            if (_target != target) CancelBodyTurn();
            _target = target;
            if (!_looking)
            {
                // Pick the pose up from wherever the Animator left the bone so the turn starts smoothly.
                _hasCurrentRotation = false;
                _looking = true;
            }

            SetTicking(true);
            return true;
        }

        /// <summary>Ends the look and eases the bone back to its rest pose.</summary>
        public void StopLooking()
        {
            CancelBodyTurn();
            _looking = false;
            _target = null;
            if (EnsureBone() != null)
            {
                SetTicking(true);
            }
        }

        public void Tick(float deltaTime)
        {
            Transform bone = EnsureBone();
            if (bone == null)
            {
                SetTicking(false);
                return;
            }

            // A destroyed target ends the look rather than freezing the bone on its last pose.
            if (_looking && _target == null)
            {
                CancelBodyTurn();
                _looking = false;
            }

            TickBodyTurn();

            Transform parent = bone.parent;
            Vector3 up = parent != null ? parent.up : Vector3.up;

            Quaternion goal;
            if (_looking)
            {
                Vector3 toTarget = _target.position - bone.position;
                float distance = toTarget.magnitude;
                if (distance <= 0.001f)
                {
                    return;
                }

                Vector3 aim = AimWithEyes(toTarget / distance, distance, up);
                goal = _limitAngles
                    ? GetLimitedRotation(aim, parent)
                    : Quaternion.LookRotation(aim, up);
            }
            else
            {
                goal = parent != null ? parent.rotation * _restLocalRotation : _restLocalRotation;
            }

            // The bone is animated, so it is reset to the clip pose every frame. Advancing a separately
            // accumulated rotation keeps the turn progressing instead of restarting from that pose.
            if (!_hasCurrentRotation)
            {
                _currentWorldRotation = bone.rotation;
                _hasCurrentRotation = true;
            }

            _currentWorldRotation = Quaternion.RotateTowards(
                _currentWorldRotation,
                goal,
                TurnSpeed * Mathf.Max(0f, deltaTime));
            bone.rotation = _currentWorldRotation;
            _remainingAngle = Quaternion.Angle(_currentWorldRotation, goal);

            bool eyesSettled = TickEyes(deltaTime);
            if (!_looking && _remainingAngle <= SettleTolerance && eyesSettled)
            {
                _remainingAngle = 0f;
                _hasCurrentRotation = false;
                SetTicking(false);
            }
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            if (_lookTransform != null)
            {
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
                        "Look At Target and Rotation target the same root Transform and may compete. Assign a child head/eye transform.",
                        Npc));
                }

                return;
            }

            if (FindBoneByName() == null)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "Look At Target could not find a bone named '" + ResolveBoneName() +
                    "' inside this NPC. Assign a Look Transform or fix the bone name.",
                    Npc));
            }
        }

        protected override void OnFeatureInitialized()
        {
            ResetLook();
            EnsureBone();
        }

        protected override void OnFeatureActivated()
        {
            EnsureBone();
            SetTicking(_looking);
        }

        protected override void OnFeatureDeactivated()
        {
            ResetLook();
            SetTicking(false);
        }

        protected override void OnFeatureShutdown()
        {
            ResetLook();
            _bone = null;
            _eyes = null;
        }

        protected override void OnFeatureValidate()
        {
            // Speed and tolerance clamp only negatives: 0 means "unset" on a serialized instance that
            // predates the field, and TurnSpeed/SettleTolerance fall back to their defaults for it.
            // Clamping 0 up to the inspector minimum would turn that fallback into 1 degree/second.
            _eyeHeightOffset = Mathf.Max(0f, _eyeHeightOffset);
            _degreesPerSecond = Mathf.Max(0f, _degreesPerSecond);
            _settleToleranceDegrees = Mathf.Max(0f, _settleToleranceDegrees);
            _minimumYaw = Mathf.Clamp(_minimumYaw, -180f, 0f);
            _maximumYaw = Mathf.Clamp(_maximumYaw, 0f, 180f);
            _minimumPitch = Mathf.Clamp(_minimumPitch, -90f, 0f);
            _maximumPitch = Mathf.Clamp(_maximumPitch, 0f, 90f);
            _bodyTurnThresholdDegrees = Mathf.Clamp(_bodyTurnThresholdDegrees, 0f, 180f);
            _eyeDegreesPerSecond = Mathf.Max(0f, _eyeDegreesPerSecond);
            _eyeAngleLimits.x = Mathf.Clamp(_eyeAngleLimits.x, 0f, 60f);
            _eyeAngleLimits.y = Mathf.Clamp(_eyeAngleLimits.y, 0f, 45f);
        }

        private void TickBodyTurn()
        {
            if (!_looking || _bodyTurnThresholdDegrees <= 0f)
            {
                CancelBodyTurn();
                return;
            }

            if (Npc.Features.TryGetOperational(out INpcNavigation navigation) &&
                (navigation.State == NpcNavigationState.Moving ||
                 navigation.State == NpcNavigationState.CalculatingPath))
            {
                CancelBodyTurn();
                return;
            }

            if (!Npc.Features.TryGetOperational(out INpcRotation rotation) || rotation.IsRotating)
                return;

            Vector3 direction = _target.position - Transform.position;
            direction.y = 0f;
            Vector3 forward = Transform.forward;
            forward.y = 0f;
            if (direction.sqrMagnitude > 0.0001f &&
                Vector3.Angle(forward, direction) > _bodyTurnThresholdDegrees)
            {
                _bodyTurn = rotation.FaceTarget(_target, NpcRotationOptions.Default,
                    new NpcCommandRequest(this, -1, NpcCommandInterruptPolicy.Always)).Handle;
            }
        }

        private void CancelBodyTurn()
        {
            if (_bodyTurn.IsValid) _bodyTurn.Cancel();
            _bodyTurn = NpcCommandHandle.Invalid;
        }

        /// <summary>
        /// Turns the pivot-to-target direction into the bone forward that puts the eyes on the target.
        /// The eyes sit <c>h</c> above the pivot and gaze parallel to the bone forward, so aiming the
        /// pivot at the target leaves the gaze <c>h</c> too high. The eye-to-target line is parallel to
        /// the bone forward exactly when the forward is pitched down from the pivot-to-target direction
        /// by <c>asin(h / distance)</c>; the yaw is unchanged, and an eye offset along the forward axis
        /// does not change the aim, so one height is all that is needed.
        /// </summary>
        private Vector3 AimWithEyes(Vector3 directionToTarget, float distance, Vector3 up)
        {
            float height = _eyeHeightOffset * _rootScale;
            if (height <= 0f)
            {
                return directionToTarget;
            }

            // Pitching down is a rotation about the axis perpendicular to both up and the direction.
            // It is undefined when the target is straight above/below the pivot; no correction then.
            Vector3 pitchAxis = Vector3.Cross(up, directionToTarget);
            if (pitchAxis.sqrMagnitude <= 0.000001f)
            {
                return directionToTarget;
            }

            float pitchDownDegrees = Mathf.Asin(Mathf.Min(height / distance, 1f)) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(pitchDownDegrees, pitchAxis.normalized) * directionToTarget;
        }

        private Quaternion GetLimitedRotation(Vector3 worldDirection, Transform parent)
        {
            Quaternion neutralWorldRotation = parent != null
                ? parent.rotation * _restLocalRotation
                : _restLocalRotation;
            Vector3 localDirection = Quaternion.Inverse(neutralWorldRotation) * worldDirection;

            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float horizontalLength = Mathf.Sqrt(
                localDirection.x * localDirection.x + localDirection.z * localDirection.z);
            float pitch = -Mathf.Atan2(localDirection.y, horizontalLength) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, _minimumYaw, _maximumYaw);
            pitch = Mathf.Clamp(pitch, _minimumPitch, _maximumPitch);
            return neutralWorldRotation * Quaternion.Euler(pitch, yaw, 0f);
        }

        private void ResetLook()
        {
            CancelBodyTurn();
            if (_eyes != null)
                foreach (var eye in _eyes)
                    if (eye.Bone != null) { eye.Bone.localRotation = eye.Rest; eye.Current = eye.Rest; }
            _looking = false;
            _target = null;
            _hasCurrentRotation = false;
            _remainingAngle = 0f;
        }

        private Transform EnsureBone()
        {
            if (_bone != null)
            {
                return _bone;
            }

            Transform root = Transform;
            if (root == null)
            {
                return null;
            }

            _bone = _lookTransform != null ? _lookTransform : FindBoneByName();
            if (_bone != null)
            {
                _restLocalRotation = _bone.localRotation;

                // The offset is authored in metres at the character's design scale, so a scaled
                // character keeps its eyes on the right spot. Cached: the scale does not change.
                _rootScale = Mathf.Abs(root.lossyScale.y);
                BindEyes(root);
            }

            return _bone;
        }

        // Calibrate gaze from the head's neutral forward, not the eye bone's authored axes.
        // Imported rigs commonly give the two eyeballs different local orientations.
        private void BindEyes(Transform root)
        {
            var eyes = new List<EyePose>();
            if (!_disableEyeTracking)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                string[] names = { string.IsNullOrEmpty(_leftEyeBoneName) ? "LeftEye" : _leftEyeBoneName,
                    string.IsNullOrEmpty(_rightEyeBoneName) ? "RightEye" : _rightEyeBoneName };
                foreach (string name in names)
                    foreach (var candidate in transforms)
                        if (candidate != _bone && candidate.IsChildOf(_bone) &&
                            string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase) &&
                            !eyes.Exists(e => e.Bone == candidate))
                        {
                            Quaternion parentRotation = candidate.parent.rotation;
                            eyes.Add(new EyePose { Bone = candidate, Rest = candidate.localRotation,
                                Current = candidate.localRotation,
                                Forward = Quaternion.Inverse(parentRotation) * _bone.forward,
                                Up = Quaternion.Inverse(parentRotation) * _bone.up });
                            break;
                        }
            }
            _eyes = eyes.ToArray();
        }

        // Runs after the head, so each eye converges on the same world target using its own pivot.
        // Only eyeball bones are changed; lids, expressions and the existing body turn keep ownership.
        private bool TickEyes(float deltaTime)
        {
            bool settled = true;
            if (_eyes == null) return settled;
            foreach (var eye in _eyes)
            {
                if (eye.Bone == null) continue;
                Quaternion goal = eye.Rest;
                if (_looking && _target != null)
                {
                    Vector3 direction = _target.position - eye.Bone.position;
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        Quaternion basis = Quaternion.LookRotation(eye.Forward, eye.Up);
                        Vector3 local = Quaternion.Inverse(basis) *
                            (Quaternion.Inverse(eye.Bone.parent.rotation) * direction);
                        float yawLimit = _eyeAngleLimits.x > 0f ? _eyeAngleLimits.x : 30f;
                        float pitchLimit = _eyeAngleLimits.y > 0f ? _eyeAngleLimits.y : 20f;
                        float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
                        float pitch = Mathf.Clamp(-Mathf.Atan2(local.y,
                            new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -pitchLimit, pitchLimit);
                        Vector3 aim = basis * (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);
                        goal = Quaternion.FromToRotation(eye.Forward, aim) * eye.Rest;
                    }
                }
                eye.Current = Quaternion.RotateTowards(eye.Current, goal,
                    (_eyeDegreesPerSecond > 0f ? _eyeDegreesPerSecond : 720f) * Mathf.Max(0f, deltaTime));
                eye.Bone.localRotation = eye.Current;
                settled &= Quaternion.Angle(eye.Current, goal) <= SettleTolerance;
            }
            return settled;
        }

        private Transform FindBoneByName()
        {
            Transform root = Transform;
            if (root == null)
            {
                return null;
            }

            string boneName = ResolveBoneName();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, boneName, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private string ResolveBoneName()
        {
            return string.IsNullOrEmpty(_lookBoneName) ? DefaultLookBoneName : _lookBoneName;
        }
    }
}
