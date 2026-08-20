using System;
using UnityEngine;

namespace ModularNPC
{
    public readonly struct NpcHearingStimulus
    {
        public NpcHearingStimulus(
            INpcPerceptionTarget source,
            Vector3 position,
            float range,
            float intensity,
            float timestamp)
        {
            Source = source;
            Position = position;
            Range = Mathf.Max(0f, range);
            Intensity = Mathf.Max(0f, intensity);
            Timestamp = timestamp;
        }

        public INpcPerceptionTarget Source { get; }

        public Vector3 Position { get; }

        public float Range { get; }

        public float Intensity { get; }

        public float Timestamp { get; }

        public bool IsValid => Source != null && Range > 0f && Intensity > 0f && NpcMath.IsFinite(Position);
    }

    /// <summary>Explicit, allocation-free global bus for event-driven hearing stimuli.</summary>
    public static class NpcHearing
    {
        private static event Action<NpcHearingStimulus> StimulusEmitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StimulusEmitted = null;
        }

        public static bool Emit(
            INpcPerceptionTarget source,
            Vector3 position,
            float range,
            float intensity = 1f)
        {
            NpcHearingStimulus stimulus = new NpcHearingStimulus(
                source,
                position,
                range,
                intensity,
                Time.time);
            if (!stimulus.IsValid)
            {
                return false;
            }

            StimulusEmitted?.Invoke(stimulus);
            return true;
        }

        public static bool Emit(Npc sourceNpc, Vector3 position, float range, float intensity = 1f)
        {
            if (sourceNpc == null || !sourceNpc.Features.TryGet(out INpcPerceptionTarget source))
            {
                return false;
            }

            return Emit(source, position, range, intensity);
        }

        internal static void Subscribe(Action<NpcHearingStimulus> listener)
        {
            StimulusEmitted += listener;
        }

        internal static void Unsubscribe(Action<NpcHearingStimulus> listener)
        {
            StimulusEmitted -= listener;
        }
    }
}
