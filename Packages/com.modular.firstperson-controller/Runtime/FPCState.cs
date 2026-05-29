// Defines enums and a struct for tracking the player's current state. FPCGroundState tracks grounded/jumping/falling/landing. FPCPosture tracks standing/crouching/transitioning. FPCState bundles these together with movement flags and force-move/look indicators for event consumers.

namespace ModularSystems.FirstPersonController
{
    public enum FPCGroundState
    {
        Grounded,
        Jumping,
        Falling,
        Landing
    }

    public enum FPCPosture
    {
        Standing,
        Crouching,
        TransitioningToCrouch,
        TransitioningToStand
    }

    public struct FPCState
    {
        public FPCGroundState GroundState;
        public FPCPosture Posture;
        public bool IsMoving;
        public bool IsRunning;
        public bool InForceLook;
        public bool InForceMove;
    }
}