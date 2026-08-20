using System;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Internal identity that makes an NPC perceivable without attaching another component.
    /// </summary>
    [Serializable]
    [NpcFeature(
        "Perception Identity",
        "Perception",
        Description = "Defines this NPC's perception category, team, and sensor aim point.")]
    public sealed class NpcIdentity : NpcFeature, INpcPerceptionTarget
    {
        [SerializeField, Tooltip("Whether sensors can currently observe this NPC.")]
        private bool _isPerceivable = true;

        [SerializeField, Tooltip("Generic team/faction identifier used by sensor filters.")]
        private int _team;

        [SerializeField, Tooltip("Broad categories used by reusable sensor filters.")]
        private NpcPerceptionCategory _categories = NpcPerceptionCategory.Character;

        [SerializeField, Tooltip("Optional child transform used for vision line-of-sight checks.")]
        private Transform _aimTransform;

        [SerializeField, Tooltip("Local-space aim offset used when no Aim Transform is assigned.")]
        private Vector3 _localAimOffset = new Vector3(0f, 1.5f, 0f);

        public UnityEngine.Object Context => Npc;

        public Transform RootTransform => Transform;

        public Vector3 Position => Transform != null ? Transform.position : Vector3.zero;

        public Vector3 AimPosition => _aimTransform != null
            ? _aimTransform.position
            : (Transform != null ? Transform.TransformPoint(_localAimOffset) : Vector3.zero);

        public int Team => _team;

        public int Layer => GameObject != null ? GameObject.layer : 0;

        public NpcPerceptionCategory Categories => _categories;

        public bool IsPerceivable => Enabled && _isPerceivable && Npc != null;
    }
}
