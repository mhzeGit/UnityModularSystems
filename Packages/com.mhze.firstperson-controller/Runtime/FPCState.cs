namespace MHZE.FirstPersonController
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