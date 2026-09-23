using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Shared behaviour for presentation features that raise a boolean animator parameter from a
    /// condition — the built-in <see cref="NpcTalking"/> and <see cref="NpcTurningAnimation"/> states,
    /// for example. The Animator is resolved through the NPC's <see cref="NpcAnimation"/> feature and
    /// the parameter is written only when the current controller declares it, so a character without
    /// the matching state simply keeps its normal animation.
    ///
    /// Subclasses provide the parameter name and the condition; this base owns the animator tracking,
    /// the change detection, the deactivation clean-up (the flag is dropped when the feature is
    /// switched off mid-state) and the validation warning for a controller that has no matching bool.
    /// </summary>
    [Serializable]
    [NpcRequiresFeature(typeof(NpcAnimation))]
    public abstract class NpcAnimatorFlagFeature : NpcFeature, INpcTickable
    {
        [NonSerialized] private NpcAnimation _animation;
        [NonSerialized] private Animator _animator;
        [NonSerialized] private bool _raised;
        [NonSerialized] private bool _applied;
        [NonSerialized] private bool _appliedValue;

        /// <summary>Animator bool parameter raised while the subclass condition holds.</summary>
        protected abstract string ParameterName { get; }

        /// <summary>True while the parameter is raised.</summary>
        public bool IsFlagRaised => _raised;

        public NpcTickSettings TickSettings => NpcTickSettings.EveryUpdate;

        /// <summary>Evaluated every Update tick to decide whether the parameter is raised.</summary>
        protected abstract bool EvaluateFlag();

        /// <summary>The Animator used by this feature, or null while none is available.</summary>
        protected Animator ResolveAnimator()
        {
            if (_animation == null)
            {
                TryGetFeature(out _animation);
            }

            return _animation != null ? _animation.Animator : null;
        }

        protected override void OnFeatureInitialized()
        {
            SetTicking(true);
        }

        protected override void OnFeatureActivated()
        {
            _applied = false;
        }

        protected override void OnFeatureDeactivated()
        {
            // Drop the flag when the feature is switched off mid-state, so the animator is not left
            // raised with nothing driving it.
            ApplyFlag(false);
            _applied = false;
        }

        protected override void OnFeatureShutdown()
        {
            ApplyFlag(false);
            _animation = null;
            _animator = null;
        }

        public void Tick(float deltaTime)
        {
            Animator animator = ResolveAnimator();
            if (animator == null)
            {
                return;
            }

            if (animator != _animator)
            {
                _animator = animator;
                _applied = false;
            }

            ApplyFlag(EvaluateFlag());
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            string parameterName = ParameterName;
            if (string.IsNullOrEmpty(parameterName))
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Error,
                    $"{GetType().Name} has no animator parameter name.",
                    Npc));
                return;
            }

            if (!TryGetFeature(out NpcAnimation animation))
            {
                return;
            }

            Animator animator = animation.Animator;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                // The Animation feature already reports a missing animator or controller.
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName &&
                    parameters[i].type == AnimatorControllerParameterType.Bool)
                {
                    return;
                }
            }

            issues.Add(new NpcValidationIssue(
                NpcValidationSeverity.Warning,
                $"{GetType().Name}: the assigned Animator has no bool parameter \"{parameterName}\", " +
                "so this animation state will never play.",
                animator));
        }

        private void ApplyFlag(bool value)
        {
            _raised = value;
            if (_applied && value == _appliedValue)
            {
                return;
            }

            _applied = true;
            _appliedValue = value;

            string parameterName = ParameterName;
            if (_animator == null || string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName &&
                    parameters[i].type == AnimatorControllerParameterType.Bool)
                {
                    _animator.SetBool(parameterName, value);
                    return;
                }
            }
        }
    }
}
