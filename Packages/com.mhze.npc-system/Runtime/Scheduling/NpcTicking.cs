namespace ModularNPC
{
    public enum NpcTickPhase
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    /// <summary>Scheduling policy for an active feature.</summary>
    public readonly struct NpcTickSettings
    {
        public NpcTickSettings(
            NpcTickPhase phase,
            float interval = 0f,
            bool useUnscaledTime = false)
        {
            Phase = phase;
            Interval = interval < 0f ? 0f : interval;
            UseUnscaledTime = useUnscaledTime;
        }

        public NpcTickPhase Phase { get; }

        /// <summary>Seconds between ticks. Zero means every tick of the selected phase.</summary>
        public float Interval { get; }

        public bool UseUnscaledTime { get; }

        public static NpcTickSettings EveryUpdate => new NpcTickSettings(NpcTickPhase.Update);

        public static NpcTickSettings EveryFixedUpdate => new NpcTickSettings(NpcTickPhase.FixedUpdate);

        public static NpcTickSettings EveryLateUpdate => new NpcTickSettings(NpcTickPhase.LateUpdate);
    }

    /// <summary>Implemented by features that need centralized ticking while explicitly active.</summary>
    public interface INpcTickable
    {
        NpcTickSettings TickSettings { get; }

        void Tick(float deltaTime);
    }
}
