using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    public readonly struct NpcMemoryRecord
    {
        public NpcMemoryRecord(
            INpcPerceptionTarget target,
            Vector3 lastKnownPosition,
            float firstObservedTime,
            float lastObservedTime,
            float confidence,
            NpcSenseKind senses)
        {
            Target = target;
            LastKnownPosition = lastKnownPosition;
            FirstObservedTime = firstObservedTime;
            LastObservedTime = lastObservedTime;
            Confidence = confidence;
            Senses = senses;
        }

        public INpcPerceptionTarget Target { get; }

        public Vector3 LastKnownPosition { get; }

        public float FirstObservedTime { get; }

        public float LastObservedTime { get; }

        public float Confidence { get; }

        public NpcSenseKind Senses { get; }
    }

    public interface INpcPerceptionMemory
    {
        event Action<NpcMemoryRecord> MemoryChanged;

        event Action<INpcPerceptionTarget> MemoryForgotten;

        NpcCommandStartResult StartRemembering(NpcCommandRequest commandRequest);

        bool Remember(NpcObservation observation);

        bool Forget(INpcPerceptionTarget target);

        bool TryRecall(INpcPerceptionTarget target, out NpcMemoryRecord record);

        int CopyMemories(List<NpcMemoryRecord> results);
    }

    [Serializable]
    [NpcRequiresFeature(typeof(INpcSensor))]
    [NpcFeature(
        "Perception Memory",
        "Perception",
        Description = "Retains last-known positions and observation history from active sensors.")]
    public sealed class NpcPerceptionMemory : NpcCommandFeature, INpcPerceptionMemory, INpcTickable
    {
        private sealed class MemoryState
        {
            public INpcPerceptionTarget Target;
            public Vector3 LastKnownPosition;
            public float FirstObservedTime;
            public float LastObservedTime;
            public float Confidence;
            public NpcSenseKind Senses;
        }

        [SerializeField, Min(0f), Tooltip("Seconds before an unrefreshed memory is forgotten. Zero retains forever.")]
        private float _retentionDuration = 10f;

        [SerializeField, Min(0.05f), Tooltip("Interval used to remove expired or destroyed targets.")]
        private float _cleanupInterval = 0.25f;

        [SerializeField, Tooltip("Clear memory when the external recording command stops.")]
        private bool _clearOnStop;

        [NonSerialized] private Dictionary<INpcPerceptionTarget, MemoryState> _memories;
        [NonSerialized] private List<INpcPerceptionTarget> _removalBuffer;
        [NonSerialized] private List<INpcSensor> _sensors;

        [field: NonSerialized] public event Action<NpcMemoryRecord> MemoryChanged;

        [field: NonSerialized] public event Action<INpcPerceptionTarget> MemoryForgotten;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _cleanupInterval);

        public NpcCommandStartResult StartRemembering(NpcCommandRequest commandRequest)
        {
            if (!Npc.Features.Contains(typeof(INpcSensor), true))
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.FeatureUnavailable);
            }

            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            EnsureRuntimeState();
            BindSensors();
            SetTicking(true);
            return result;
        }

        public bool Remember(NpcObservation observation)
        {
            if (!IsOperational || !observation.IsValid)
            {
                return false;
            }

            EnsureRuntimeState();
            if (!_memories.TryGetValue(observation.Target, out MemoryState state))
            {
                state = new MemoryState
                {
                    Target = observation.Target,
                    FirstObservedTime = observation.Timestamp
                };
                _memories.Add(observation.Target, state);
            }

            state.LastKnownPosition = observation.ObservedPosition;
            state.LastObservedTime = observation.Timestamp;
            state.Confidence = observation.Confidence;
            state.Senses |= observation.Sense;
            MemoryChanged?.Invoke(ToRecord(state));
            return true;
        }

        public bool Forget(INpcPerceptionTarget target)
        {
            EnsureRuntimeState();
            if (target == null || !_memories.Remove(target))
            {
                return false;
            }

            MemoryForgotten?.Invoke(target);
            return true;
        }

        public bool TryRecall(INpcPerceptionTarget target, out NpcMemoryRecord record)
        {
            EnsureRuntimeState();
            if (target != null && _memories.TryGetValue(target, out MemoryState state))
            {
                record = ToRecord(state);
                return true;
            }

            record = default;
            return false;
        }

        public int CopyMemories(List<NpcMemoryRecord> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureRuntimeState();
            int initialCount = results.Count;
            foreach (MemoryState state in _memories.Values)
            {
                results.Add(ToRecord(state));
            }

            return results.Count - initialCount;
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveCommand)
            {
                SetTicking(false);
                return;
            }

            EnsureRuntimeState();
            _removalBuffer.Clear();
            float now = Time.time;
            foreach (KeyValuePair<INpcPerceptionTarget, MemoryState> pair in _memories)
            {
                if (!NpcPerceptionUtility.IsTargetAlive(pair.Key) ||
                    (_retentionDuration > 0f && now - pair.Value.LastObservedTime >= _retentionDuration))
                {
                    _removalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                Forget(_removalBuffer[i]);
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            UnbindSensors();
            if (_clearOnStop)
            {
                ClearMemories(true);
            }
        }

        protected override void OnCommandFeatureShutdown()
        {
            UnbindSensors();
            ClearMemories(false);
            MemoryChanged = null;
            MemoryForgotten = null;
        }

        protected override void OnFeatureValidate()
        {
            _retentionDuration = Mathf.Max(0f, _retentionDuration);
            _cleanupInterval = Mathf.Max(0.05f, _cleanupInterval);
        }

        private void BindSensors()
        {
            UnbindSensors();
            _sensors.Clear();
            Npc.Features.GetAll(_sensors, true);
            for (int i = 0; i < _sensors.Count; i++)
            {
                _sensors[i].Observed += OnObserved;
            }
        }

        private void UnbindSensors()
        {
            if (_sensors == null)
            {
                return;
            }

            for (int i = 0; i < _sensors.Count; i++)
            {
                if (_sensors[i] != null)
                {
                    _sensors[i].Observed -= OnObserved;
                }
            }

            _sensors.Clear();
        }

        private void OnObserved(NpcObservation observation)
        {
            if (HasActiveCommand)
            {
                Remember(observation);
            }
        }

        private void ClearMemories(bool notify)
        {
            if (_memories == null)
            {
                return;
            }

            if (notify)
            {
                foreach (INpcPerceptionTarget target in _memories.Keys)
                {
                    MemoryForgotten?.Invoke(target);
                }
            }

            _memories.Clear();
        }

        private void EnsureRuntimeState()
        {
            if (_memories == null)
            {
                _memories = new Dictionary<INpcPerceptionTarget, MemoryState>(16);
                _removalBuffer = new List<INpcPerceptionTarget>(16);
                _sensors = new List<INpcSensor>(4);
            }
        }

        private static NpcMemoryRecord ToRecord(MemoryState state)
        {
            return new NpcMemoryRecord(
                state.Target,
                state.LastKnownPosition,
                state.FirstObservedTime,
                state.LastObservedTime,
                state.Confidence,
                state.Senses);
        }
    }
}
