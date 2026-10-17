using System;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>Blends authored facial offsets over the Animator pose while preserving body clips and head look.</summary>
    [Serializable]
    [NpcFeature("Expressions", "Presentation", Description = "Blends a small facial pose library, with timed reactions and neutral recovery.")]
    public sealed class NpcExpressions : NpcFeature, INpcTickable
    {
        [SerializeField] private NpcExpressionLibrary _library;
        [NonSerialized] private Transform[] _bones;
        [NonSerialized] private NpcExpressionLibrary.BonePose[] _current, _from, _goal, _applied;
        [NonSerialized] private float _elapsed, _blendSeconds, _remaining;
        [NonSerialized] private string _expression;
        [NonSerialized] private object _owner;
        public NpcTickSettings TickSettings => NpcTickSettings.EveryLateUpdate;
        public string CurrentExpression => _expression;
        public NpcExpressionLibrary Library { get => _library; set { ResetImmediate(); _library = value; _bones = null; } }

        /// <summary>Last request wins. Duration 0 holds until Clear. Unknown poses leave the current request alone.</summary>
        public bool Show(string id, float intensity = 0.5f, float duration = 2f, float blendSeconds = 0.3f, object owner = null)
        {
            if (!IsOperational || !Bind()) return false;
            var pose = _library.Find(id);
            if (pose == null || pose.bones.Length != _bones.Length) return false;
            _owner = owner;
            _expression = id;
            _remaining = Mathf.Max(0, duration);
            BeginBlend(blendSeconds);
            for (int i = 0; i < _goal.Length; i++) _goal[i] = Blend(_library.neutral[i], pose.bones[i], Mathf.Clamp01(intensity));
            return true;
        }

        /// <summary>An owner may clear only its own request; null clears unconditionally. Immediate reset is for rewind/disable.</summary>
        public void Clear(float blendSeconds = 0.35f, object owner = null)
        {
            if (owner != null && !ReferenceEquals(owner, _owner)) return;
            _owner = null; _expression = null; _remaining = 0;
            if (!Bind()) return;
            BeginBlend(blendSeconds);
            Array.Copy(_library.neutral, _goal, _goal.Length);
        }
        public void ResetImmediate() { Clear(0); if (_bones != null) Apply(_goal); }
        protected override void OnFeatureActivated() { if (Bind()) SetTicking(true); }
        protected override void OnFeatureDeactivated() { ResetImmediate(); SetTicking(false); }
        protected override void OnFeatureShutdown() { ResetImmediate(); _bones = null; }

        public void Tick(float deltaTime)
        {
            if (!Bind()) return;
            if (_remaining > 0) { _remaining -= deltaTime; if (_remaining <= 0) Clear(); }
            _elapsed += deltaTime;
            float t = _blendSeconds <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(_elapsed / _blendSeconds));
            for (int i = 0; i < _current.Length; i++) _current[i] = Blend(_from[i], _goal[i], t);
            Apply(_current);
        }
        private bool Bind()
        {
            if (_bones != null) return true;
            if (_library == null || Npc == null || _library.bonePaths.Length == 0 || _library.neutral.Length != _library.bonePaths.Length) return false;
            var animator = Npc.GetComponentInChildren<Animator>(true);
            if (animator == null) return false;
            int count = _library.bonePaths.Length;
            var bones = new Transform[count];
            for (int i = 0; i < count; i++) { bones[i] = animator.transform.Find(_library.bonePaths[i]); if (bones[i] == null) return false; }
            _bones = bones;
            _current = (NpcExpressionLibrary.BonePose[])_library.neutral.Clone();
            _from = (NpcExpressionLibrary.BonePose[])_current.Clone();
            _goal = (NpcExpressionLibrary.BonePose[])_current.Clone();
            _applied = (NpcExpressionLibrary.BonePose[])_library.neutral.Clone();
            SetTicking(true);
            return true;
        }
        private void BeginBlend(float seconds) { Array.Copy(_current, _from, _current.Length); _elapsed = 0; _blendSeconds = Mathf.Max(0, seconds); }
        private void Apply(NpcExpressionLibrary.BonePose[] pose)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform bone = _bones[i];
                if (bone == null) continue;

                // The Animator has evaluated this frame's animation before the NPC late update.
                // Remove last frame's expression offset first, otherwise this offset accumulates.
                NpcExpressionLibrary.BonePose neutral = _library.neutral[i];
                NpcExpressionLibrary.BonePose previous = _applied[i];
                bone.localPosition -= previous.position - neutral.position;
                bone.localRotation *= Quaternion.Inverse(Quaternion.Inverse(neutral.rotation) * previous.rotation);
                bone.localScale = Divide(bone.localScale, Divide(previous.scale, neutral.scale));

                // Expression data is authored relative to Expression_Idle. Apply only its delta
                // to the live Animator pose so talking can continue to animate the jaw and lips.
                NpcExpressionLibrary.BonePose expression = pose[i];
                bone.localPosition += expression.position - neutral.position;
                bone.localRotation *= Quaternion.Inverse(neutral.rotation) * expression.rotation;
                bone.localScale = Multiply(bone.localScale, Divide(expression.scale, neutral.scale));
                _applied[i] = expression;
            }
        }
        private static Vector3 Divide(Vector3 value, Vector3 divisor) => new Vector3(
            Mathf.Abs(divisor.x) > 0.000001f ? value.x / divisor.x : 1f,
            Mathf.Abs(divisor.y) > 0.000001f ? value.y / divisor.y : 1f,
            Mathf.Abs(divisor.z) > 0.000001f ? value.z / divisor.z : 1f);
        private static Vector3 Multiply(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        private static NpcExpressionLibrary.BonePose Blend(NpcExpressionLibrary.BonePose a, NpcExpressionLibrary.BonePose b, float t) => new NpcExpressionLibrary.BonePose {
            position = Vector3.Lerp(a.position,b.position,t), rotation = Quaternion.Slerp(a.rotation,b.rotation,t), scale = Vector3.Lerp(a.scale,b.scale,t) };
    }
}
