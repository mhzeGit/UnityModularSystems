using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Makes the walking animation follow the NPC's real speed instead of the navigation state.
    /// <see cref="NpcAnimation"/> only mirrors the navigation state, which keeps reporting Moving while
    /// something else holds the agent (a door interaction, a blocked path), so the character would walk
    /// on the spot. Every frame this component overrides the shared animator state parameter: above the
    /// walk speed it shows Moving, standing still it never shows Moving, and every other navigation
    /// state (Arrived, Unreachable, ...) is passed through unchanged.
    ///
    /// It is a shared root component on the base NPC prefab (like the NavMeshAgent) rather than an
    /// <see cref="NpcFeature"/>, so adding it never shifts the variants' feature arrays.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after the NPC scheduler (-9000) so it has the final word each frame
    public sealed class NpcVelocityAnimator : MonoBehaviour
    {
        [Tooltip("Int animator parameter written by NpcAnimation (the NPC state parameter of the animator controller).")]
        [SerializeField] private string _stateParameter = "NPCState";
        [Tooltip("Speed in metres per second above which the walking animation starts.")]
        [SerializeField, Min(0f)] private float _walkStartSpeed = 0.2f;
        [Tooltip("Speed in metres per second below which the walking animation stops. Keep it under the start speed so it does not flicker.")]
        [SerializeField, Min(0f)] private float _walkStopSpeed = 0.1f;

        private Npc _npc;
        private INpcNavigation _navigation;
        private Animator _animator;
        private int _parameterHash;
        private bool _parameterValid;
        private bool _walking;

        /// <summary>True while the NPC is moving fast enough to show the walking animation.</summary>
        public bool IsWalking => _walking;

        private void Awake()
        {
            _npc = GetComponent<Npc>();
        }

        private void OnDisable()
        {
            _walking = false;
        }

        private void Update()
        {
            if (!ResolveAnimator())
                return;

            Vector3 velocity = _navigation.Velocity;
            velocity.y = 0f;
            _walking = velocity.magnitude > (_walking ? _walkStopSpeed : _walkStartSpeed);

            NpcNavigationState state = _navigation.State;
            if (_walking)
                state = NpcNavigationState.Moving;
            else if (state == NpcNavigationState.Moving)
                state = NpcNavigationState.Idle;

            // Only write on change; NpcAnimation also writes the parameter when the state changes.
            if (_animator.GetInteger(_parameterHash) != (int)state)
                _animator.SetInteger(_parameterHash, (int)state);
        }

        private bool ResolveAnimator()
        {
            if (_npc == null)
                return false;

            if (_navigation == null && !_npc.Features.TryGetOperational(out _navigation))
                return false;

            if (_animator != null)
                return _parameterValid;

            if (!_npc.Features.TryGetOperational(out NpcAnimation animation))
                return false;

            Animator animator = animation.Animator;
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(_stateParameter))
                return false;

            _animator = animator;
            _parameterHash = Animator.StringToHash(_stateParameter);
            _parameterValid = false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                AnimatorControllerParameter parameter = animator.GetParameter(i);
                if (parameter.nameHash == _parameterHash && parameter.type == AnimatorControllerParameterType.Int)
                {
                    _parameterValid = true;
                    break;
                }
            }

            return _parameterValid;
        }
    }
}
