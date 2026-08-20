using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>Shared command lifecycle and observation publication for active sensors.</summary>
    [Serializable]
    public abstract class NpcSensorFeature : NpcCommandFeature, INpcSensor, INpcTickable
    {
        [NonSerialized] private List<NpcObservation> _currentObservations;
        [NonSerialized] private List<NpcObservation> _scratchObservations;
        [NonSerialized] private HashSet<INpcPerceptionTarget> _currentTargets;
        [NonSerialized] private HashSet<INpcPerceptionTarget> _previousTargets;
        [NonSerialized] private float _activeScanInterval;

        [field: NonSerialized] public event Action<NpcObservation> Observed;

        [field: NonSerialized] public event Action<INpcPerceptionTarget> ObservationLost;

        public abstract NpcSenseKind SenseKind { get; }

        public bool IsSensing => HasActiveCommand;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _activeScanInterval);

        protected abstract float DefaultScanInterval { get; }

        public NpcCommandStartResult StartSensing(
            NpcSensorRunOptions options,
            NpcCommandRequest commandRequest)
        {
            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            EnsureRuntimeCollections();
            _activeScanInterval = options.OverrideScanInterval
                ? options.ScanInterval
                : DefaultScanInterval;
            RefreshTickSchedule();
            ScanAndPublish();
            SetTicking(true);
            return result;
        }

        public int Scan(List<NpcObservation> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureRuntimeCollections();
            _scratchObservations.Clear();
            CollectObservations(_scratchObservations);
            int initialCount = results.Count;
            results.AddRange(_scratchObservations);
            return results.Count - initialCount;
        }

        public int CopyCurrentObservations(List<NpcObservation> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureRuntimeCollections();
            int initialCount = results.Count;
            results.AddRange(_currentObservations);
            return results.Count - initialCount;
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            ScanAndPublish();
        }

        protected abstract void CollectObservations(List<NpcObservation> observations);

        protected virtual void OnSensorShutdown()
        {
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            EnsureRuntimeCollections();
            foreach (INpcPerceptionTarget target in _currentTargets)
            {
                ObservationLost?.Invoke(target);
            }

            _currentTargets.Clear();
            _previousTargets.Clear();
            _currentObservations.Clear();
            _scratchObservations.Clear();
        }

        protected override void OnCommandFeatureShutdown()
        {
            OnSensorShutdown();
            Observed = null;
            ObservationLost = null;
        }

        private void ScanAndPublish()
        {
            EnsureRuntimeCollections();

            _previousTargets.Clear();
            foreach (INpcPerceptionTarget target in _currentTargets)
            {
                _previousTargets.Add(target);
            }

            _currentObservations.Clear();
            _currentTargets.Clear();
            CollectObservations(_currentObservations);

            for (int i = 0; i < _currentObservations.Count; i++)
            {
                NpcObservation observation = _currentObservations[i];
                if (!observation.IsValid)
                {
                    continue;
                }

                _currentTargets.Add(observation.Target);
                Observed?.Invoke(observation);
            }

            foreach (INpcPerceptionTarget previousTarget in _previousTargets)
            {
                if (!_currentTargets.Contains(previousTarget))
                {
                    ObservationLost?.Invoke(previousTarget);
                }
            }
        }

        private void EnsureRuntimeCollections()
        {
            if (_currentObservations == null)
            {
                _currentObservations = new List<NpcObservation>(16);
                _scratchObservations = new List<NpcObservation>(16);
                _currentTargets = new HashSet<INpcPerceptionTarget>();
                _previousTargets = new HashSet<INpcPerceptionTarget>();
            }
        }
    }

    [Serializable]
    public abstract class NpcPhysicsSensorFeature : NpcSensorFeature
    {
        [SerializeField, Range(8, 512), Tooltip("Maximum colliders processed by one non-allocating physics scan.")]
        private int _colliderBufferCapacity = 64;

        [NonSerialized] private Collider[] _colliderBuffer;
        [NonSerialized] private Dictionary<Collider, INpcPerceptionTarget> _targetCache;

        protected int QuerySphere(
            Vector3 origin,
            float radius,
            LayerMask layers,
            QueryTriggerInteraction triggerInteraction)
        {
            EnsurePhysicsBuffers();
            return Physics.OverlapSphereNonAlloc(
                origin,
                radius,
                _colliderBuffer,
                layers,
                triggerInteraction);
        }

        protected Collider GetCollider(int index)
        {
            return _colliderBuffer[index];
        }

        protected bool TryResolveTarget(Collider collider, out INpcPerceptionTarget target)
        {
            EnsurePhysicsBuffers();
            if (collider == null)
            {
                target = null;
                return false;
            }

            if (_targetCache.TryGetValue(collider, out target) && target != null && target.IsPerceivable)
            {
                return true;
            }

            if (!NpcPerceptionUtility.TryResolveTarget(collider, out target))
            {
                _targetCache.Remove(collider);
                return false;
            }

            _targetCache[collider] = target;
            return true;
        }

        protected override void OnSensorShutdown()
        {
            _targetCache?.Clear();
            _colliderBuffer = null;
        }

        protected override void OnFeatureValidate()
        {
            _colliderBufferCapacity = Mathf.Clamp(_colliderBufferCapacity, 8, 512);
            if (_colliderBuffer != null && _colliderBuffer.Length != _colliderBufferCapacity)
            {
                _colliderBuffer = null;
            }
        }

        private void EnsurePhysicsBuffers()
        {
            if (_colliderBuffer == null || _colliderBuffer.Length != _colliderBufferCapacity)
            {
                _colliderBuffer = new Collider[_colliderBufferCapacity];
            }

            if (_targetCache == null)
            {
                _targetCache = new Dictionary<Collider, INpcPerceptionTarget>(_colliderBufferCapacity);
            }
        }
    }
}
