using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    [Serializable]
    [NpcFeature(
        "Hearing Sensor",
        "Perception",
        Description = "Event-driven hearing that consumes explicitly emitted sound stimuli while commanded.")]
    public sealed class NpcHearingSensor : NpcCommandFeature, INpcSensor, INpcTickable
    {
        private struct RetainedObservation
        {
            public NpcObservation Observation;
            public float ExpiryTime;
        }

        [SerializeField, Tooltip("Optional hearing origin. The NPC root is used when empty.")]
        private Transform _originTransform;

        [SerializeField, Tooltip("Local offset from the selected hearing origin.")]
        private Vector3 _localOriginOffset = new Vector3(0f, 1.5f, 0f);

        [SerializeField, Min(0f), Tooltip("Multiplier applied to the range supplied by each sound stimulus.")]
        private float _rangeMultiplier = 1f;

        [SerializeField, Min(0f), Tooltip("How long a heard observation remains current after the last matching sound.")]
        private float _retentionTime = 0.5f;

        [SerializeField, Min(0.02f), Tooltip("Default interval used to expire retained hearing observations.")]
        private float _defaultExpiryCheckInterval = 0.1f;

        [SerializeField, Tooltip("Source layers, categories, and team filtering.")]
        private NpcPerceptionFilter _filter = new NpcPerceptionFilter();

        [SerializeField, Tooltip("Attenuate sounds that have an obstacle between source and listener.")]
        private bool _useOcclusion;

        [SerializeField, Tooltip("Layers considered sound-occluding obstacles.")]
        private LayerMask _occlusionLayers = ~0;

        [SerializeField, Range(0f, 1f), Tooltip("Confidence multiplier applied when a sound is occluded.")]
        private float _occludedConfidenceMultiplier = 0.35f;

        [NonSerialized] private Dictionary<INpcPerceptionTarget, RetainedObservation> _retained;
        [NonSerialized] private List<INpcPerceptionTarget> _expiredTargets;
        [NonSerialized] private RaycastHit[] _occlusionHits;
        [NonSerialized] private float _activeInterval;
        [NonSerialized] private bool _subscribed;

        [field: NonSerialized] public event Action<NpcObservation> Observed;

        [field: NonSerialized] public event Action<INpcPerceptionTarget> ObservationLost;

        public NpcSenseKind SenseKind => NpcSenseKind.Hearing;

        public bool IsSensing => HasActiveCommand;

        public NpcTickSettings TickSettings =>
            new NpcTickSettings(NpcTickPhase.Update, _activeInterval);

        public NpcCommandStartResult StartSensing(
            NpcSensorRunOptions options,
            NpcCommandRequest commandRequest)
        {
            NpcCommandStartResult result = BeginCommand(commandRequest);
            if (!result.Accepted)
            {
                return result;
            }

            EnsureRuntimeState();
            _activeInterval = options.OverrideScanInterval
                ? Mathf.Max(0.02f, options.ScanInterval)
                : _defaultExpiryCheckInterval;
            Subscribe();
            RefreshTickSchedule();
            SetTicking(true);
            return result;
        }

        /// <summary>Copies retained sound observations; hearing itself is event-driven.</summary>
        public int Scan(List<NpcObservation> results)
        {
            return CopyCurrentObservations(results);
        }

        public int CopyCurrentObservations(List<NpcObservation> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureRuntimeState();
            int initialCount = results.Count;
            foreach (KeyValuePair<INpcPerceptionTarget, RetainedObservation> pair in _retained)
            {
                results.Add(pair.Value.Observation);
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
            _expiredTargets.Clear();
            float now = Time.time;
            foreach (KeyValuePair<INpcPerceptionTarget, RetainedObservation> pair in _retained)
            {
                if (pair.Key == null || pair.Value.ExpiryTime <= now)
                {
                    _expiredTargets.Add(pair.Key);
                }
            }

            for (int i = 0; i < _expiredTargets.Count; i++)
            {
                INpcPerceptionTarget target = _expiredTargets[i];
                _retained.Remove(target);
                if (target != null)
                {
                    ObservationLost?.Invoke(target);
                }
            }
        }

        protected override void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
            SetTicking(false);
            Unsubscribe();
            if (_retained == null)
            {
                return;
            }

            foreach (INpcPerceptionTarget target in _retained.Keys)
            {
                ObservationLost?.Invoke(target);
            }

            _retained.Clear();
        }

        protected override void OnCommandFeatureShutdown()
        {
            Unsubscribe();
            _retained?.Clear();
            _expiredTargets?.Clear();
            Observed = null;
            ObservationLost = null;
        }

        protected override void OnFeatureValidate()
        {
            _rangeMultiplier = Mathf.Max(0f, _rangeMultiplier);
            _retentionTime = Mathf.Max(0f, _retentionTime);
            _defaultExpiryCheckInterval = Mathf.Max(0.02f, _defaultExpiryCheckInterval);
            _occludedConfidenceMultiplier = Mathf.Clamp01(_occludedConfidenceMultiplier);
            if (_filter == null)
            {
                _filter = new NpcPerceptionFilter();
            }
        }

        private void OnStimulus(NpcHearingStimulus stimulus)
        {
            if (!HasActiveCommand || !stimulus.IsValid || stimulus.Source.RootTransform == Transform)
            {
                return;
            }

            Transform originTransform = _originTransform != null ? _originTransform : Transform;
            if (originTransform == null)
            {
                return;
            }

            INpcPerceptionTarget observer = null;
            Npc.Features.TryGet(out observer);
            if (!_filter.Allows(observer, stimulus.Source))
            {
                return;
            }

            Vector3 origin = originTransform.TransformPoint(_localOriginOffset);
            Vector3 toSound = stimulus.Position - origin;
            float distance = toSound.magnitude;
            float effectiveRange = stimulus.Range * _rangeMultiplier * stimulus.Intensity;
            if (effectiveRange <= 0f || distance > effectiveRange)
            {
                return;
            }

            float confidence = 1f - distance / effectiveRange;
            if (_useOcclusion && IsOccluded(origin, toSound, distance, stimulus.Source))
            {
                confidence *= _occludedConfidenceMultiplier;
            }

            NpcObservation observation = new NpcObservation(
                stimulus.Source,
                SenseKind,
                origin,
                stimulus.Position,
                distance,
                confidence,
                stimulus.Timestamp);
            _retained[stimulus.Source] = new RetainedObservation
            {
                Observation = observation,
                ExpiryTime = Time.time + _retentionTime
            };
            Observed?.Invoke(observation);
        }

        private bool IsOccluded(
            Vector3 origin,
            Vector3 toSound,
            float distance,
            INpcPerceptionTarget source)
        {
            if (distance <= 0.0001f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toSound / distance,
                _occlusionHits,
                distance,
                _occlusionLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _occlusionHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(Transform))
                {
                    continue;
                }

                if (NpcPerceptionUtility.TryResolveTarget(collider, out INpcPerceptionTarget hitTarget) &&
                    ReferenceEquals(hitTarget, source))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            NpcHearing.Subscribe(OnStimulus);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            NpcHearing.Unsubscribe(OnStimulus);
            _subscribed = false;
        }

        private void EnsureRuntimeState()
        {
            if (_retained == null)
            {
                _retained = new Dictionary<INpcPerceptionTarget, RetainedObservation>(16);
                _expiredTargets = new List<INpcPerceptionTarget>(16);
                _occlusionHits = new RaycastHit[16];
            }
        }
    }
}
