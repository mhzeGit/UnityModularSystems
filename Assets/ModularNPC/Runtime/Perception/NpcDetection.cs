using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    public readonly struct NpcDetectionRecord
    {
        public NpcDetectionRecord(
            INpcPerceptionTarget target,
            Vector3 lastKnownPosition,
            float score,
            float lastObservedTime,
            NpcSenseKind senses,
            bool isDetected)
        {
            Target = target;
            LastKnownPosition = lastKnownPosition;
            Score = score;
            LastObservedTime = lastObservedTime;
            Senses = senses;
            IsDetected = isDetected;
        }

        public INpcPerceptionTarget Target { get; }

        public Vector3 LastKnownPosition { get; }

        public float Score { get; }

        public float LastObservedTime { get; }

        public NpcSenseKind Senses { get; }

        public bool IsDetected { get; }
    }

    public interface INpcDetection
    {
        event Action<NpcDetectionRecord> TargetDetected;

        event Action<NpcDetectionRecord> DetectionUpdated;

        event Action<INpcPerceptionTarget> TargetLost;

        bool IsRunning { get; }

        NpcCommandStartResult StartDetection(NpcCommandRequest commandRequest);

        bool TryGetDetection(INpcPerceptionTarget target, out NpcDetectionRecord record);

        int CopyDetections(List<NpcDetectionRecord> results, bool detectedOnly = true);
    }

    [Serializable]
    [NpcRequiresFeature(typeof(INpcSensor))]
    [NpcFeature(
        "Detection",
        "Perception",
        Description = "Combines raw sensor evidence into stable detected/lost target state.")]
    public sealed class NpcDetection : NpcCommandFeature, INpcDetection, INpcTickable
    {
        private sealed class DetectionState
        {
            public INpcPerceptionTarget Target;
            public Vector3 LastKnownPosition;
            public float Score;
            public float LastObservedTime;
            public NpcSenseKind Senses;
            public bool IsDetected;
        }

        [SerializeField, Range(0.01f, 1f), Tooltip("Evidence added per observation, multiplied by observation confidence.")]
        private float _evidencePerObservation = 0.35f;

        [SerializeField, Range(0.01f, 1f), Tooltip("Score required to enter the detected state.")]
        private float _detectionThreshold = 0.75f;

        [SerializeField, Range(0f, 1f), Tooltip("Score at which a detected target becomes lost.")]
        private float _forgetThreshold = 0.1f;

        [SerializeField, Min(0f), Tooltip("Delay after the latest observation before evidence starts decaying.")]
        private float _decayDelay = 0.3f;

        [SerializeField, Min(0f), Tooltip("Detection score removed per second after the decay delay.")]
        private float _decayPerSecond = 0.5f;

        [SerializeField, Min(0f), Tooltip("How long an empty, non-detected record remains queryable.")]
        private float _removeEmptyRecordDelay = 1f;

        [SerializeField, Min(0.02f), Tooltip("Update interval for score decay and target cleanup.")]
        private float _tickInterval = 0.1f;

        [SerializeField, Tooltip("Clear all detection state when the external detection command stops.")]
        private bool _clearOnStop = true;

        [NonSerialized] private Dictionary<INpcPerceptionTarget, DetectionState> _states;
        [NonSerialized] private List<INpcPerceptionTarget> _removalBuffer;
        [NonSerialized] private List<INpcSensor> _sensors;

        [field: NonSerialized] public event Action<NpcDetectionRecord> TargetDetected;

        [field: NonSerialized] public event Action<NpcDetectionRecord> DetectionUpdated;

        [field: NonSerialized] public event Action<INpcPerceptionTarget> TargetLost;

        public bool IsRunning => HasActiveCommand;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _tickInterval);

        public NpcCommandStartResult StartDetection(NpcCommandRequest commandRequest)
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

        public bool TryGetDetection(INpcPerceptionTarget target, out NpcDetectionRecord record)
        {
            EnsureRuntimeState();
            if (target != null && _states.TryGetValue(target, out DetectionState state))
            {
                record = ToRecord(state);
                return true;
            }

            record = default;
            return false;
        }

        public int CopyDetections(List<NpcDetectionRecord> results, bool detectedOnly = true)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureRuntimeState();
            int initialCount = results.Count;
            foreach (DetectionState state in _states.Values)
            {
                if (!detectedOnly || state.IsDetected)
                {
                    results.Add(ToRecord(state));
                }
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
            foreach (KeyValuePair<INpcPerceptionTarget, DetectionState> pair in _states)
            {
                DetectionState state = pair.Value;
                if (!NpcPerceptionUtility.IsTargetAlive(state.Target))
                {
                    if (state.IsDetected)
                    {
                        TargetLost?.Invoke(state.Target);
                    }

                    _removalBuffer.Add(pair.Key);
                    continue;
                }

                float timeSinceObservation = now - state.LastObservedTime;
                if (timeSinceObservation > _decayDelay)
                {
                    state.Score = Mathf.Max(0f, state.Score - _decayPerSecond * deltaTime);
                }

                if (state.IsDetected && state.Score <= _forgetThreshold)
                {
                    state.IsDetected = false;
                    TargetLost?.Invoke(state.Target);
                }

                if (!state.IsDetected &&
                    state.Score <= 0f &&
                    timeSinceObservation >= _removeEmptyRecordDelay)
                {
                    _removalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                _states.Remove(_removalBuffer[i]);
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            UnbindSensors();
            if (_clearOnStop)
            {
                ClearStates(true);
            }
        }

        protected override void OnCommandFeatureShutdown()
        {
            UnbindSensors();
            ClearStates(false);
            TargetDetected = null;
            DetectionUpdated = null;
            TargetLost = null;
        }

        protected override void OnFeatureValidate()
        {
            _evidencePerObservation = Mathf.Clamp(_evidencePerObservation, 0.01f, 1f);
            _detectionThreshold = Mathf.Clamp(_detectionThreshold, 0.01f, 1f);
            _forgetThreshold = Mathf.Clamp(_forgetThreshold, 0f, _detectionThreshold);
            _decayDelay = Mathf.Max(0f, _decayDelay);
            _decayPerSecond = Mathf.Max(0f, _decayPerSecond);
            _removeEmptyRecordDelay = Mathf.Max(0f, _removeEmptyRecordDelay);
            _tickInterval = Mathf.Max(0.02f, _tickInterval);
        }

        private void OnObserved(NpcObservation observation)
        {
            if (!HasActiveCommand || !observation.IsValid)
            {
                return;
            }

            EnsureRuntimeState();
            if (!_states.TryGetValue(observation.Target, out DetectionState state))
            {
                state = new DetectionState { Target = observation.Target };
                _states.Add(observation.Target, state);
            }

            state.LastKnownPosition = observation.ObservedPosition;
            state.LastObservedTime = observation.Timestamp;
            state.Senses |= observation.Sense;
            state.Score = Mathf.Clamp01(
                state.Score + observation.Confidence * _evidencePerObservation);

            bool becameDetected = !state.IsDetected && state.Score >= _detectionThreshold;
            if (becameDetected)
            {
                state.IsDetected = true;
            }

            NpcDetectionRecord record = ToRecord(state);
            DetectionUpdated?.Invoke(record);
            if (becameDetected)
            {
                TargetDetected?.Invoke(record);
            }
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

        private void ClearStates(bool notifyLost)
        {
            if (_states == null)
            {
                return;
            }

            if (notifyLost)
            {
                foreach (DetectionState state in _states.Values)
                {
                    if (state.IsDetected)
                    {
                        TargetLost?.Invoke(state.Target);
                    }
                }
            }

            _states.Clear();
        }

        private void EnsureRuntimeState()
        {
            if (_states == null)
            {
                _states = new Dictionary<INpcPerceptionTarget, DetectionState>(16);
                _removalBuffer = new List<INpcPerceptionTarget>(16);
                _sensors = new List<INpcSensor>(4);
            }
        }

        private static NpcDetectionRecord ToRecord(DetectionState state)
        {
            return new NpcDetectionRecord(
                state.Target,
                state.LastKnownPosition,
                state.Score,
                state.LastObservedTime,
                state.Senses,
                state.IsDetected);
        }
    }
}
