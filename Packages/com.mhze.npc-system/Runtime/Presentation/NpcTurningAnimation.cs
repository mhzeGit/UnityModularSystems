using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace ModularNPC
{
    /// <summary>
    /// Plays a dedicated turning animation whenever the NPC rotates its whole body in place, so a
    /// character that pivots to face a new direction while standing still does not slide around in its
    /// idle pose.
    ///
    /// The body rotation itself is owned by the navigation-independent <see cref="INpcRotation"/>
    /// feature (commands such as "face the player"). While a rotation command is active and the NPC is
    /// not travelling, this feature raises the boolean animator parameter
    /// <see cref="TurningParameterName"/> (default "Turning"); it clears it the moment the turn
    /// finishes or movement starts, so the walk animation always wins on the move. A character without
    /// a turning state simply keeps the parameter and plays its normal state.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "BlahBlahFamily.Gameplay", "Assembly-CSharp", null)]
    [NpcFeature(
        "Turning",
        "Presentation",
        Description = "Raises a boolean animator turning state while the NPC rotates its body in place, so pivoting characters animate instead of sliding in their idle pose.")]
    [NpcRequiresFeature(typeof(INpcRotation))]
    public sealed class NpcTurningAnimation : NpcAnimatorFlagFeature
    {
        /// <summary>Canonical animator bool parameter. Controller generators should write the same name.</summary>
        public const string TurningParameterName = "Turning";

        [SerializeField, Tooltip("Animator bool parameter raised while the body turns in place.")]
        private string _turningParameter = TurningParameterName;

        [NonSerialized] private INpcRotation _rotation;
        [NonSerialized] private INpcNavigation _navigation;

        /// <summary>True while the turning animation is playing.</summary>
        public bool IsTurning => IsFlagRaised;

        /// <summary>Animator bool parameter this feature drives.</summary>
        public string TurningParameter =>
            string.IsNullOrEmpty(_turningParameter) ? TurningParameterName : _turningParameter;

        protected override string ParameterName => TurningParameter;

        protected override void OnFeatureShutdown()
        {
            base.OnFeatureShutdown();
            _rotation = null;
            _navigation = null;
        }

        protected override bool EvaluateFlag()
        {
            if (_rotation == null)
            {
                TryGetFeature(out _rotation);
            }

            if (_navigation == null)
            {
                TryGetFeature(out _navigation);
            }

            // A turn only counts while the body is rotating and the NPC is standing still. Rotation
            // issued while walking is the "face the travel direction" behaviour and must keep the
            // walk clip. Navigation is optional: an NPC without it is never considered moving.
            return _rotation != null &&
                   _rotation.IsRotating &&
                   (_navigation == null || !_navigation.IsMoving);
        }
    }
}
