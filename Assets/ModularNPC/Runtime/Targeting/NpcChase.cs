using System;
using UnityEngine;

namespace ModularNPC
{
    public readonly struct NpcChaseOptions
    {
        public NpcChaseOptions(
            float stoppingDistance,
            float repathInterval = 0.15f,
            bool keepFollowing = false,
            bool faceTarget = false,
            float maximumDuration = 0f)
        {
            StoppingDistance = Mathf.Max(0f, stoppingDistance);
            RepathInterval = Mathf.Max(0.01f, repathInterval);
            KeepFollowing = keepFollowing;
            FaceTarget = faceTarget;
            MaximumDuration = Mathf.Max(0f, maximumDuration);
            OverrideStoppingDistance = true;
            OverrideRepathInterval = true;
        }

        public float StoppingDistance { get; }

        public float RepathInterval { get; }

        public bool KeepFollowing { get; }

        public bool FaceTarget { get; }

        public float MaximumDuration { get; }

        public bool OverrideStoppingDistance { get; }

        public bool OverrideRepathInterval { get; }

        public static NpcChaseOptions Default => default;
    }

    public interface INpcChase
    {
        Transform ChaseTarget { get; }

        bool IsChasing { get; }

        NpcCommandStartResult Chase(
            Transform target,
            NpcChaseOptions options,
            NpcCommandRequest commandRequest);
    }

    [Serializable]
    [NpcRequiresFeature(typeof(INpcNavigation))]
    [NpcFeature(
        "Chase",
        "Targeting",
        Description = "Explicit composite command that follows a moving target through any navigation implementation.")]
    public sealed class NpcChase : NpcCommandFeature, INpcChase, INpcTickable
    {
        [SerializeField, Min(0f)] private float _defaultStoppingDistance = 1.5f;
        [SerializeField, Min(0.01f)] private float _defaultRepathInterval = 0.15f;
        [SerializeField, Min(0f)] private float _resumeDistanceBuffer = 0.2f;

        private INpcNavigation _navigation;
        private INpcRotation _rotation;
        private Transform _target;
        private NpcCommandHandle _navigationCommand;
        private NpcCommandHandle _rotationCommand;
        private float _stoppingDistance;
        private float _repathInterval;
        private float _maximumDuration;
        private float _elapsed;
        private bool _keepFollowing;
        private bool _faceTarget;

        public Transform ChaseTarget => _target;

        public bool IsChasing => HasActiveCommand;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _repathInterval);

        public NpcCommandStartResult Chase(
            Transform target,
            NpcChaseOptions options,
            NpcCommandRequest commandRequest)
        {
            if (target == null ||
                target == Transform ||
                !Npc.Features.TryGetOperational(out INpcNavigation navigation))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            _navigation = navigation;
            Npc.Features.TryGetOperational(out _rotation);
            _target = target;
            _stoppingDistance = options.OverrideStoppingDistance
                ? options.StoppingDistance
                : _defaultStoppingDistance;
            _repathInterval = options.OverrideRepathInterval
                ? options.RepathInterval
                : _defaultRepathInterval;
            _maximumDuration = options.MaximumDuration;
            _keepFollowing = options.KeepFollowing;
            _faceTarget = options.FaceTarget;
            _elapsed = 0f;

            if (_faceTarget && _rotation != null)
            {
                NpcRotationOptions rotationOptions = NpcRotationOptions.Default.WithTracking(true);
                NpcCommandStartResult rotationResult = _rotation.FaceTarget(
                    target,
                    rotationOptions,
                    new NpcCommandRequest(this, commandRequest.Priority));
                _rotationCommand = rotationResult.Handle;
            }

            RefreshTickSchedule();
            if (!RefreshDestination())
            {
                FailActiveCommand();
                return result;
            }

            SetTicking(true);
            return result;
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            if (_target == null || _navigation == null)
            {
                FailActiveCommand();
                return;
            }

            _elapsed += deltaTime;
            if (_maximumDuration > 0f && _elapsed >= _maximumDuration)
            {
                FailActiveCommand();
                return;
            }

            float distance = Vector3.Distance(Transform.position, _target.position);
            if (distance <= _stoppingDistance)
            {
                _navigationCommand.Cancel();
                _navigationCommand = NpcCommandHandle.Invalid;
                if (!_keepFollowing)
                {
                    SucceedActiveCommand();
                }

                return;
            }

            if (_keepFollowing && distance <= _stoppingDistance + _resumeDistanceBuffer)
            {
                return;
            }

            if (_navigationCommand.IsValid && _navigationCommand.Status == NpcCommandStatus.Failed)
            {
                FailActiveCommand();
                return;
            }

            if (!RefreshDestination())
            {
                FailActiveCommand();
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            _navigationCommand.Cancel();
            _rotationCommand.Cancel();
            _navigationCommand = NpcCommandHandle.Invalid;
            _rotationCommand = NpcCommandHandle.Invalid;
            _target = null;
            _navigation = null;
            _rotation = null;
        }

        private bool RefreshDestination()
        {
            if (_target == null || _navigation == null)
            {
                return false;
            }

            NpcMoveOptions moveOptions = NpcMoveOptions.Default.WithStoppingDistance(_stoppingDistance);
            NpcCommandStartResult moveResult = _navigation.MoveTo(
                _target.position,
                moveOptions,
                new NpcCommandRequest(this, ActiveCommandRequest.Priority));

            if (!moveResult.Accepted)
            {
                return false;
            }

            _navigationCommand = moveResult.Handle;
            return _navigationCommand.Status != NpcCommandStatus.Failed;
        }

        protected override void OnFeatureValidate()
        {
            _defaultStoppingDistance = Mathf.Max(0f, _defaultStoppingDistance);
            _defaultRepathInterval = Mathf.Max(0.01f, _defaultRepathInterval);
            _resumeDistanceBuffer = Mathf.Max(0f, _resumeDistanceBuffer);
        }
    }
}
