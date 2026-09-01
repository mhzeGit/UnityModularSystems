using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    [Flags]
    public enum NpcPerceptionCategory
    {
        None = 0,
        Character = 1 << 0,
        Object = 1 << 1,
        PointOfInterest = 1 << 2,
        Hazard = 1 << 3,
        Interactable = 1 << 4,
        SoundEmitter = 1 << 5,
        Custom1 = 1 << 6,
        Custom2 = 1 << 7,
        Everything = ~0
    }

    [Flags]
    public enum NpcSenseKind
    {
        None = 0,
        Vision = 1 << 0,
        Proximity = 1 << 1,
        Hearing = 1 << 2,
        Custom = 1 << 3
    }

    public enum NpcTeamFilterMode
    {
        Any,
        ExcludeSameTeam,
        OnlySameTeam,
        OnlySpecificTeam
    }

    /// <summary>Implemented by anything that can be observed by NPC sensors.</summary>
    public interface INpcPerceptionTarget
    {
        UnityEngine.Object Context { get; }

        Transform RootTransform { get; }

        Vector3 Position { get; }

        Vector3 AimPosition { get; }

        int Team { get; }

        int Layer { get; }

        NpcPerceptionCategory Categories { get; }

        bool IsPerceivable { get; }
    }

    /// <summary>Immutable raw result produced by a sensor.</summary>
    public readonly struct NpcObservation
    {
        public NpcObservation(
            INpcPerceptionTarget target,
            NpcSenseKind sense,
            Vector3 sensorPosition,
            Vector3 observedPosition,
            float distance,
            float confidence,
            float timestamp)
        {
            Target = target;
            Sense = sense;
            SensorPosition = sensorPosition;
            ObservedPosition = observedPosition;
            Distance = distance;
            Confidence = Mathf.Clamp01(confidence);
            Timestamp = timestamp;
        }

        public INpcPerceptionTarget Target { get; }

        public NpcSenseKind Sense { get; }

        public Vector3 SensorPosition { get; }

        public Vector3 ObservedPosition { get; }

        public float Distance { get; }

        public float Confidence { get; }

        public float Timestamp { get; }

        public bool IsValid => NpcPerceptionUtility.IsTargetObservable(Target);
    }

    public readonly struct NpcSensorRunOptions
    {
        public NpcSensorRunOptions(float scanInterval)
        {
            ScanInterval = Mathf.Max(0f, scanInterval);
            OverrideScanInterval = true;
        }

        public float ScanInterval { get; }

        public bool OverrideScanInterval { get; }

        public static NpcSensorRunOptions Default => default;
    }

    public interface INpcSensor
    {
        event Action<NpcObservation> Observed;

        event Action<INpcPerceptionTarget> ObservationLost;

        NpcSenseKind SenseKind { get; }

        bool IsSensing { get; }

        NpcCommandStartResult StartSensing(
            NpcSensorRunOptions options,
            NpcCommandRequest commandRequest);

        int Scan(List<NpcObservation> results);

        int CopyCurrentObservations(List<NpcObservation> results);
    }

    /// <summary>Reusable target filtering shared by physics and event-based sensors.</summary>
    [Serializable]
    public sealed class NpcPerceptionFilter
    {
        [SerializeField, Tooltip("Physics layers that sensors may treat as candidates.")]
        private LayerMask _candidateLayers = ~0;

        [SerializeField, Tooltip("Target categories accepted by this sensor.")]
        private NpcPerceptionCategory _categories = NpcPerceptionCategory.Everything;

        [SerializeField, Tooltip("Optional team relationship filter.")]
        private NpcTeamFilterMode _teamMode = NpcTeamFilterMode.Any;

        [SerializeField, Tooltip("Used only when Team Mode is Only Specific Team.")]
        private int _specificTeam;

        public LayerMask CandidateLayers => _candidateLayers;

        public bool Allows(INpcPerceptionTarget observer, INpcPerceptionTarget target)
        {
            if (target == null || !target.IsPerceivable)
            {
                return false;
            }

            if ((_candidateLayers.value & (1 << target.Layer)) == 0 ||
                (_categories & target.Categories) == 0)
            {
                return false;
            }

            switch (_teamMode)
            {
                case NpcTeamFilterMode.ExcludeSameTeam:
                    return observer == null || observer.Team != target.Team;

                case NpcTeamFilterMode.OnlySameTeam:
                    return observer != null && observer.Team == target.Team;

                case NpcTeamFilterMode.OnlySpecificTeam:
                    return target.Team == _specificTeam;

                default:
                    return true;
            }
        }
    }

    internal static class NpcPerceptionUtility
    {
        public static bool IsTargetAlive(INpcPerceptionTarget target)
        {
            if (target == null)
            {
                return false;
            }

            if (target is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            UnityEngine.Object context = target.Context;
            return context != null;
        }

        public static bool IsTargetObservable(INpcPerceptionTarget target)
        {
            return IsTargetAlive(target) && target.IsPerceivable;
        }

        public static bool TryResolveTarget(Collider collider, out INpcPerceptionTarget target)
        {
            target = null;
            if (collider == null)
            {
                return false;
            }

            NpcPerceptionTarget standaloneTarget = collider.GetComponentInParent<NpcPerceptionTarget>();
            if (standaloneTarget != null)
            {
                target = standaloneTarget;
                return target.IsPerceivable;
            }

            Npc npc = collider.GetComponentInParent<Npc>();
            return npc != null &&
                   npc.Features.TryGet(out target) &&
                   target != null &&
                   target.IsPerceivable;
        }
    }
}
