using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    [Serializable]
    [NpcFeature(
        "Vision Sensor",
        "Perception",
        Description = "Non-allocating field-of-view sensing with optional line-of-sight checks.")]
    public sealed class NpcVisionSensor : NpcPhysicsSensorFeature
    {
        [SerializeField, Tooltip("Optional eye/camera transform. The NPC root is used when empty.")]
        private Transform _originTransform;

        [SerializeField, Tooltip("Local offset from the selected origin transform.")]
        private Vector3 _localOriginOffset = new Vector3(0f, 1.6f, 0f);

        [SerializeField, Min(0.01f), Tooltip("Maximum vision distance.")]
        private float _range = 20f;

        [SerializeField, Range(0.1f, 360f), Tooltip("Full cone angle in degrees.")]
        private float _fieldOfView = 110f;

        [SerializeField, Min(0f), Tooltip("Default interval used by continuous vision commands.")]
        private float _defaultScanInterval = 0.15f;

        [SerializeField, Tooltip("Candidate layers, categories, and team filtering.")]
        private NpcPerceptionFilter _filter = new NpcPerceptionFilter();

        [SerializeField, Tooltip("Whether obstacles must be checked between the eye and target.")]
        private bool _requireLineOfSight = true;

        [SerializeField, Tooltip("Layers that may block line of sight.")]
        private LayerMask _occlusionLayers = ~0;

        [SerializeField, Tooltip("How trigger colliders are treated by candidate queries.")]
        private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField, Range(4, 128), Tooltip("Maximum ray hits inspected without allocation.")]
        private int _lineOfSightBufferCapacity = 16;

        [NonSerialized] private HashSet<INpcPerceptionTarget> _seenTargets;
        [NonSerialized] private RaycastHit[] _lineOfSightHits;

        public override NpcSenseKind SenseKind => NpcSenseKind.Vision;

        protected override float DefaultScanInterval => _defaultScanInterval;

        /// <summary>
        /// Check a known target using this sensor's filters, cone and line of sight.
        /// Unlike discovering candidates with Scan, this does not depend on the overlap buffer.
        /// It does not publish continuous sensing events or change an active command.
        /// </summary>
        public bool TryObserve(INpcPerceptionTarget target, out NpcObservation observation)
        {
            observation = default;
            if (!IsOperational || !NpcPerceptionUtility.IsTargetObservable(target) || target.RootTransform == Transform)
                return false;
            INpcPerceptionTarget observer = null;
            Npc.Features.TryGet(out observer);
            if (!_filter.Allows(observer, target)) return false;
            Transform originTransform = _originTransform != null ? _originTransform : Transform;
            if (originTransform == null) return false;
            Vector3 origin = originTransform.TransformPoint(_localOriginOffset);
            Vector3 toTarget = target.AimPosition - origin;
            float squaredDistance = toTarget.sqrMagnitude;
            if (squaredDistance <= .000001f || squaredDistance > _range * _range) return false;
            float distance = Mathf.Sqrt(squaredDistance);
            Vector3 direction = toTarget / distance;
            float cosineThreshold = Mathf.Cos(_fieldOfView * .5f * Mathf.Deg2Rad);
            float forwardDot = Vector3.Dot(originTransform.forward, direction);
            if (forwardDot < cosineThreshold) return false;
            EnsureVisionBuffers();
            if (_requireLineOfSight && !HasLineOfSight(origin, direction, distance, target)) return false;
            observation = new NpcObservation(target, SenseKind, origin, target.AimPosition, distance,
                (1f - distance / _range) * Mathf.InverseLerp(cosineThreshold, 1f, forwardDot), Time.time);
            return true;
        }

        protected override void CollectObservations(List<NpcObservation> observations)
        {
            EnsureVisionBuffers();
            _seenTargets.Clear();

            Transform originTransform = _originTransform != null ? _originTransform : Transform;
            if (originTransform == null)
            {
                return;
            }

            Vector3 origin = originTransform.TransformPoint(_localOriginOffset);
            int count = QuerySphere(
                origin,
                _range,
                _filter.CandidateLayers,
                _triggerInteraction);

            for (int i = 0; i < count; i++)
            {
                Collider candidate = GetCollider(i);
                if (TryResolveTarget(candidate, out INpcPerceptionTarget target) && _seenTargets.Add(target) &&
                    TryObserve(target, out NpcObservation observation)) observations.Add(observation);
            }
        }

        protected override void OnFeatureValidate()
        {
            base.OnFeatureValidate();
            _range = Mathf.Max(0.01f, _range);
            _fieldOfView = Mathf.Clamp(_fieldOfView, 0.1f, 360f);
            _defaultScanInterval = Mathf.Max(0f, _defaultScanInterval);
            _lineOfSightBufferCapacity = Mathf.Clamp(_lineOfSightBufferCapacity, 4, 128);
            if (_filter == null)
            {
                _filter = new NpcPerceptionFilter();
            }

            if (_lineOfSightHits != null && _lineOfSightHits.Length != _lineOfSightBufferCapacity)
            {
                _lineOfSightHits = null;
            }
        }

        protected override void OnSensorShutdown()
        {
            base.OnSensorShutdown();
            _seenTargets?.Clear();
            _lineOfSightHits = null;
        }

        private bool HasLineOfSight(
            Vector3 origin,
            Vector3 direction,
            float distance,
            INpcPerceptionTarget target)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                _lineOfSightHits,
                distance,
                _occlusionLayers,
                QueryTriggerInteraction.Ignore);

            float closestBlocker = float.PositiveInfinity;
            float closestTargetHit = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _lineOfSightHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(Transform))
                {
                    continue;
                }

                if (TryResolveTarget(hit.collider, out INpcPerceptionTarget hitTarget) &&
                    ReferenceEquals(hitTarget, target))
                {
                    closestTargetHit = Mathf.Min(closestTargetHit, hit.distance);
                }
                else
                {
                    closestBlocker = Mathf.Min(closestBlocker, hit.distance);
                }
            }

            return float.IsPositiveInfinity(closestBlocker) || closestTargetHit < closestBlocker;
        }

        private void EnsureVisionBuffers()
        {
            if (_seenTargets == null)
            {
                _seenTargets = new HashSet<INpcPerceptionTarget>();
            }

            if (_lineOfSightHits == null || _lineOfSightHits.Length != _lineOfSightBufferCapacity)
            {
                _lineOfSightHits = new RaycastHit[_lineOfSightBufferCapacity];
            }
        }
    }
}
