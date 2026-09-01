using System;
using UnityEngine;

namespace ModularNPC
{
    public interface INpcTargeting
    {
        event Action<Transform, Transform> TargetChanged;

        Transform Target { get; }

        bool HasTarget { get; }

        NpcCommandStartResult SetTarget(Transform target, NpcCommandRequest commandRequest);

        bool ClearTarget();
    }

    [Serializable]
    [NpcFeature(
        "Targeting",
        "Targeting",
        Description = "Externally controlled, priority-arbitrated persistent target selection.")]
    public sealed class NpcTargeting : NpcCommandFeature, INpcTargeting, INpcTickable
    {
        [SerializeField, Min(0.05f)] private float _validityCheckInterval = 0.25f;

        private Transform _target;

        public event Action<Transform, Transform> TargetChanged;

        public Transform Target => _target;

        public bool HasTarget => _target != null;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _validityCheckInterval);

        public NpcCommandStartResult SetTarget(Transform target, NpcCommandRequest commandRequest)
        {
            if (target == null || target == Transform)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.InvalidArgument);
            }

            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            SetTargetInternal(target);
            SetTicking(true);
            return result;
        }

        public bool ClearTarget()
        {
            return CancelActiveCommand();
        }

        public void Tick(float deltaTime)
        {
            if (_target == null)
            {
                FailActiveCommand();
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            SetTargetInternal(null);
        }

        private void SetTargetInternal(Transform target)
        {
            if (_target == target)
            {
                return;
            }

            Transform previous = _target;
            _target = target;
            TargetChanged?.Invoke(previous, target);
        }

        protected override void OnFeatureValidate()
        {
            _validityCheckInterval = Mathf.Max(0.05f, _validityCheckInterval);
            if (Application.isPlaying)
            {
                RefreshTickSchedule();
            }
        }
    }
}
