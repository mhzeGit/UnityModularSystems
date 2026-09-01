using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>Animator parameter types supported by the NPC animation feature.</summary>
    public enum NpcAnimationParameterType
    {
        Float,
        Int,
        Bool,
        Trigger
    }

    /// <summary>One animator parameter assignment configured for an NPC state.</summary>
    [Serializable]
    public sealed class NpcAnimationParameterBinding
    {
        [SerializeField] private string _parameterName = string.Empty;
        [SerializeField] private NpcAnimationParameterType _parameterType;
        [SerializeField] private float _floatValue;
        [SerializeField] private int _intValue;
        [SerializeField] private bool _boolValue;

        public string ParameterName
        {
            get => _parameterName;
            set => _parameterName = value ?? string.Empty;
        }

        public NpcAnimationParameterType ParameterType
        {
            get => _parameterType;
            set => _parameterType = value;
        }

        public float FloatValue
        {
            get => _floatValue;
            set => _floatValue = value;
        }

        public int IntValue
        {
            get => _intValue;
            set => _intValue = value;
        }

        public bool BoolValue
        {
            get => _boolValue;
            set => _boolValue = value;
        }
    }

    /// <summary>Per-NPC-state collection of animator parameter assignments.</summary>
    [Serializable]
    public sealed class NpcAnimationStateBinding
    {
        [SerializeField] private NpcNavigationState _state;
        [SerializeField] private List<NpcAnimationParameterBinding> _parameters = new List<NpcAnimationParameterBinding>(4);

        public NpcAnimationStateBinding()
        {
        }

        public NpcAnimationStateBinding(NpcNavigationState state)
        {
            _state = state;
        }

        public NpcNavigationState State
        {
            get => _state;
            set => _state = value;
        }

        public List<NpcAnimationParameterBinding> Parameters => _parameters;
    }

    /// <summary>Editor marker: draws the animator field with auto-detection UI.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NpcAnimatorFieldAttribute : PropertyAttribute
    {
    }

    /// <summary>Editor marker: draws the per-state parameter binding cards.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NpcAnimationStatesFieldAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// Drives an assigned or auto-detected Animator from the NPC navigation state. The inspector
    /// auto-fills parameter dropdowns from the Animator controller and filters them by type.
    /// </summary>
    [Serializable]
    [NpcFeature(
        "Animation",
        "Presentation",
        Description = "Drives an assigned or auto-detected Animator from the NPC navigation state. Parameter names are detected from the Animator controller and type-filtered in the inspector.")]
    [NpcRequiresFeature(typeof(INpcNavigation))]
    public sealed class NpcAnimation : NpcFeature, INpcTickable
    {
        [SerializeField, NpcAnimatorField]
        private Animator _animator;

        [SerializeField, HideInInspector]
        private bool _autoDetectAnimator = true;

        [SerializeField, NpcAnimationStatesField]
        private List<NpcAnimationStateBinding> _stateBindings = CreateDefaultBindings();

        [NonSerialized] private INpcNavigation _navigation;
        [NonSerialized] private NpcNavigationState _currentState;
        [NonSerialized] private bool _stateApplied;
        [NonSerialized] private readonly List<string> _previousTriggerNames = new List<string>(8);

        /// <summary>The animator used for playback, resolving auto-detection.</summary>
        public Animator Animator => ResolveAnimator();

        public bool HasAnimator => ResolveAnimator() != null;

        public NpcNavigationState CurrentState => _currentState;

        public bool AutoDetectAnimator
        {
            get => _autoDetectAnimator;
            set => _autoDetectAnimator = value;
        }

        public NpcTickSettings TickSettings => NpcTickSettings.EveryUpdate;

        protected override void OnFeatureInitialized()
        {
            TryGetFeature(out _navigation);
            SetTicking(true);
        }

        protected override void OnFeatureActivated()
        {
            _stateApplied = false;
        }

        protected override void OnFeatureShutdown()
        {
            Animator animator = ResolveAnimator();
            if (animator != null)
            {
                ResetPreviousTriggers(animator);
            }

            _navigation = null;
        }

        public void Tick(float deltaTime)
        {
            if (_navigation == null)
            {
                TryGetFeature(out _navigation);
            }

            if (_navigation == null)
            {
                return;
            }

            NpcNavigationState state = _navigation.State;
            if (_stateApplied && state == _currentState)
            {
                return;
            }

            Animator animator = ResolveAnimator();
            if (animator == null)
            {
                return;
            }

            ApplyState(animator, state);
            _currentState = state;
            _stateApplied = true;
        }

        protected override void OnFeatureValidate()
        {
            if (_autoDetectAnimator && _animator == null && Npc != null && !Application.isPlaying)
            {
                _animator = Npc.GetComponentInChildren<Animator>(true);
            }

            if (_stateBindings == null)
            {
                _stateBindings = CreateDefaultBindings();
            }
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            Animator animator = ResolveAnimator();
            if (animator == null)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Error,
                    "Animation has no Animator. Assign one, or keep auto-detect enabled to find it on this NPC or a child.",
                    Npc));
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "The Animation Animator has no controller assigned, so its parameters cannot be detected.",
                    animator));
                return;
            }

            if (_stateBindings == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < _stateBindings.Count; i++)
            {
                NpcAnimationStateBinding binding = _stateBindings[i];
                if (binding == null)
                {
                    continue;
                }

                List<NpcAnimationParameterBinding> parameterBindings = binding.Parameters;
                for (int j = 0; j < parameterBindings.Count; j++)
                {
                    NpcAnimationParameterBinding parameter = parameterBindings[j];
                    if (parameter == null || string.IsNullOrEmpty(parameter.ParameterName))
                    {
                        continue;
                    }

                    AnimatorControllerParameter match = null;
                    for (int k = 0; k < parameters.Length; k++)
                    {
                        if (parameters[k].name == parameter.ParameterName)
                        {
                            match = parameters[k];
                            break;
                        }
                    }

                    if (match == null)
                    {
                        issues.Add(new NpcValidationIssue(
                            NpcValidationSeverity.Warning,
                            $"State {binding.State}: animator parameter \"{parameter.ParameterName}\" does not exist on the assigned Animator.",
                            animator));
                        continue;
                    }

                    if (match.type != ToAnimatorParameterType(parameter.ParameterType))
                    {
                        issues.Add(new NpcValidationIssue(
                            NpcValidationSeverity.Warning,
                            $"State {binding.State}: animator parameter \"{parameter.ParameterName}\" is a {match.type}, but it is bound as {parameter.ParameterType}.",
                            animator));
                    }
                }
            }
        }

        private void ApplyState(Animator animator, NpcNavigationState state)
        {
            ResetPreviousTriggers(animator);

            NpcAnimationStateBinding binding = FindBinding(state);
            if (binding == null)
            {
                return;
            }

            List<NpcAnimationParameterBinding> parameters = binding.Parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                NpcAnimationParameterBinding parameter = parameters[i];
                if (parameter == null || string.IsNullOrEmpty(parameter.ParameterName))
                {
                    continue;
                }

                switch (parameter.ParameterType)
                {
                    case NpcAnimationParameterType.Float:
                        animator.SetFloat(parameter.ParameterName, parameter.FloatValue);
                        break;

                    case NpcAnimationParameterType.Int:
                        animator.SetInteger(parameter.ParameterName, parameter.IntValue);
                        break;

                    case NpcAnimationParameterType.Bool:
                        animator.SetBool(parameter.ParameterName, parameter.BoolValue);
                        break;

                    case NpcAnimationParameterType.Trigger:
                        animator.SetTrigger(parameter.ParameterName);
                        _previousTriggerNames.Add(parameter.ParameterName);
                        break;
                }
            }
        }

        private void ResetPreviousTriggers(Animator animator)
        {
            for (int i = 0; i < _previousTriggerNames.Count; i++)
            {
                animator.ResetTrigger(_previousTriggerNames[i]);
            }

            _previousTriggerNames.Clear();
        }

        private NpcAnimationStateBinding FindBinding(NpcNavigationState state)
        {
            if (_stateBindings == null)
            {
                return null;
            }

            for (int i = 0; i < _stateBindings.Count; i++)
            {
                NpcAnimationStateBinding binding = _stateBindings[i];
                if (binding != null && binding.State == state)
                {
                    return binding;
                }
            }

            return null;
        }

        private Animator ResolveAnimator()
        {
            if (_animator != null)
            {
                return _animator;
            }

            if (_autoDetectAnimator && Npc != null)
            {
                return Npc.GetComponentInChildren<Animator>(true);
            }

            return null;
        }

        private static List<NpcAnimationStateBinding> CreateDefaultBindings()
        {
            NpcNavigationState[] states = (NpcNavigationState[])Enum.GetValues(typeof(NpcNavigationState));
            List<NpcAnimationStateBinding> bindings = new List<NpcAnimationStateBinding>(states.Length);
            for (int i = 0; i < states.Length; i++)
            {
                bindings.Add(new NpcAnimationStateBinding(states[i]));
            }

            return bindings;
        }

        private static AnimatorControllerParameterType ToAnimatorParameterType(NpcAnimationParameterType type)
        {
            switch (type)
            {
                case NpcAnimationParameterType.Int:
                    return AnimatorControllerParameterType.Int;

                case NpcAnimationParameterType.Bool:
                    return AnimatorControllerParameterType.Bool;

                case NpcAnimationParameterType.Trigger:
                    return AnimatorControllerParameterType.Trigger;

                default:
                    return AnimatorControllerParameterType.Float;
            }
        }
    }
}
