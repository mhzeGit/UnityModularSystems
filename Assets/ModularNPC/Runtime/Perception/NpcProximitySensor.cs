using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    [Serializable]
    [NpcFeature(
        "Proximity Sensor",
        "Perception",
        Description = "Fast non-allocating radial awareness without view-angle or occlusion requirements.")]
    public sealed class NpcProximitySensor : NpcPhysicsSensorFeature
    {
        [SerializeField, Tooltip("Optional sensor origin. The NPC root is used when empty.")]
        private Transform _originTransform;

        [SerializeField, Tooltip("Local offset from the selected origin transform.")]
        private Vector3 _localOriginOffset;

        [SerializeField, Min(0.01f), Tooltip("Detection radius.")]
        private float _radius = 3f;

        [SerializeField, Min(0f), Tooltip("Default interval used by continuous proximity commands.")]
        private float _defaultScanInterval = 0.1f;

        [SerializeField, Tooltip("Candidate layers, categories, and team filtering.")]
        private NpcPerceptionFilter _filter = new NpcPerceptionFilter();

        [SerializeField, Tooltip("How trigger colliders are treated by candidate queries.")]
        private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        [NonSerialized] private HashSet<INpcPerceptionTarget> _seenTargets;

        public override NpcSenseKind SenseKind => NpcSenseKind.Proximity;

        protected override float DefaultScanInterval => _defaultScanInterval;

        protected override void CollectObservations(List<NpcObservation> observations)
        {
            if (_seenTargets == null)
            {
                _seenTargets = new HashSet<INpcPerceptionTarget>();
            }

            _seenTargets.Clear();
            Transform originTransform = _originTransform != null ? _originTransform : Transform;
            if (originTransform == null)
            {
                return;
            }

            Vector3 origin = originTransform.TransformPoint(_localOriginOffset);
            INpcPerceptionTarget observer = null;
            Npc.Features.TryGet(out observer);
            int count = QuerySphere(origin, _radius, _filter.CandidateLayers, _triggerInteraction);

            for (int i = 0; i < count; i++)
            {
                if (!TryResolveTarget(GetCollider(i), out INpcPerceptionTarget target) ||
                    target.RootTransform == Transform ||
                    !_seenTargets.Add(target) ||
                    !_filter.Allows(observer, target))
                {
                    continue;
                }

                Vector3 targetPosition = target.Position;
                float distance = Vector3.Distance(origin, targetPosition);
                if (distance > _radius)
                {
                    continue;
                }

                observations.Add(new NpcObservation(
                    target,
                    SenseKind,
                    origin,
                    targetPosition,
                    distance,
                    1f - distance / _radius,
                    Time.time));
            }
        }

        protected override void OnFeatureValidate()
        {
            base.OnFeatureValidate();
            _radius = Mathf.Max(0.01f, _radius);
            _defaultScanInterval = Mathf.Max(0f, _defaultScanInterval);
            if (_filter == null)
            {
                _filter = new NpcPerceptionFilter();
            }
        }

        protected override void OnSensorShutdown()
        {
            base.OnSensorShutdown();
            _seenTargets?.Clear();
        }
    }
}
