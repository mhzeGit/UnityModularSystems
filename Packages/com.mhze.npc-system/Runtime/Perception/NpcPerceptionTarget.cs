using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Optional adapter for perceivable objects that are not NPCs. NPCs use the internal
    /// NpcIdentity feature and do not need this component.
    /// </summary>
    [AddComponentMenu("Modular NPC/Perception/Standalone Perception Target")]
    [DisallowMultipleComponent]
    public sealed class NpcPerceptionTarget : MonoBehaviour, INpcPerceptionTarget
    {
        [SerializeField] private bool _isPerceivable = true;
        [SerializeField] private int _team;
        [SerializeField] private NpcPerceptionCategory _categories = NpcPerceptionCategory.Object;
        [SerializeField] private Transform _aimTransform;
        [SerializeField] private Vector3 _localAimOffset;

        public UnityEngine.Object Context => this;

        public Transform RootTransform => transform;

        public Vector3 Position => transform.position;

        public Vector3 AimPosition => _aimTransform != null
            ? _aimTransform.position
            : transform.TransformPoint(_localAimOffset);

        public int Team => _team;

        public int Layer => gameObject.layer;

        public NpcPerceptionCategory Categories => _categories;

        public bool IsPerceivable => isActiveAndEnabled && _isPerceivable;
    }
}
