using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>One bone that jiggles: its name, an optional tip bone, and how much it differs from the group.</summary>
    [Serializable]
    public struct NpcJiggleBone
    {
        [Tooltip("Bone name, searched inside this NPC (for example LeftButt).")]
        public string boneName;

        [Tooltip("Bone whose position is the swinging tip. Empty uses the bone's first child (for example LeftButt_end); with no child, a point beyond the bone along its parent's direction is used.")]
        public string tipBoneName;

        [Tooltip("Multiplies the group's jiggle speed for this bone. Give each side a slightly different value (for example 1.0 and 1.08) so they never move in perfect sync, which looks more alive.")]
        [Range(0.25f, 3f)] public float frequencyScale;

        [Tooltip("Multiplies the group's amount for this bone. 1 = as set on the feature; above 1 exaggerates this bone. To switch a bone off, remove it from the list.")]
        [Range(0.05f, 4f)] public float strength;

        [Tooltip("This bone's own angle limit in degrees. 0 = use the feature's Max Angle. Raise it for a bone that should swing wider than the rest of the body.")]
        [Range(0f, 60f)] public float maxAngleDegrees;

        [Tooltip("This bone's own damping ratio. 0 = use the feature's Damping Ratio. Lower rings for longer (bouncier); a bone can be bouncy while the rest of the body stays heavy.")]
        [Range(0f, 1f)] public float dampingRatio;

        [Tooltip("How much the bone also slides with the flesh, on top of swinging. 0 = rotation only (default). 1 moves the bone by the same distance the tip lags behind; about 0.3 to 0.6 gives buttocks a soft bounce. The slide never exceeds the feature's Max Position Offset.")]
        [Range(0f, 2f)] public float positionAmount;

        public NpcJiggleBone(
            string boneName,
            string tipBoneName = "",
            float frequencyScale = 1f,
            float strength = 1f,
            float positionAmount = 0f,
            float maxAngleDegrees = 0f,
            float dampingRatio = 0f)
        {
            this.boneName = boneName;
            this.tipBoneName = tipBoneName;
            this.frequencyScale = frequencyScale;
            this.strength = strength;
            this.positionAmount = positionAmount;
            this.maxAngleDegrees = maxAngleDegrees;
            this.dampingRatio = dampingRatio;
        }
    }

    /// <summary>
    /// The damped spring behind <see cref="NpcBoneJiggle"/>, free of Unity components so it can be tested.
    /// It follows a moving rest point and reports how far it lags behind it.
    /// </summary>
    public struct NpcJiggleSpring
    {
        /// <summary>Highest simulation step (seconds). A frame is split into steps no longer than this.</summary>
        public const float MaxStep = 1f / 120f;

        private const int MaxSteps = 8;

        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 LastRest;
        public bool Primed;

        /// <summary>Forgets all motion; the next <see cref="Advance"/> starts at rest on the rest point.</summary>
        public void Reset() => Primed = false;

        /// <summary>
        /// Moves the spring one frame toward <paramref name="rest"/>.
        /// <paramref name="frequency"/> is the natural speed in Hz; <paramref name="dampingRatio"/> is 0 (never stops)
        /// to 1 (no overshoot); a frame in which the rest point moves further than <paramref name="snapDistance"/>
        /// (a warp or a placement) restarts the spring on it instead of whipping.
        /// Returns the lag of the spring behind the rest point.
        /// </summary>
        public Vector3 Advance(Vector3 rest, float deltaTime, float frequency, float dampingRatio, float snapDistance)
        {
            if (!Primed || (rest - LastRest).sqrMagnitude > snapDistance * snapDistance)
            {
                Position = rest;
                Velocity = Vector3.zero;
                LastRest = rest;
                Primed = true;
                return Vector3.zero;
            }

            if (deltaTime <= 0f)
            {
                return Position - rest;
            }

            float omega = 2f * Mathf.PI * Mathf.Max(0.01f, frequency);
            float stiffness = omega * omega;
            float damping = 2f * Mathf.Clamp(dampingRatio, 0f, 2f) * omega;

            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / MaxStep), 1, MaxSteps);
            float h = deltaTime / steps;
            Vector3 restVelocity = (rest - LastRest) / deltaTime;

            for (int i = 1; i <= steps; i++)
            {
                // The rest point moves in a straight line over the frame. Each step is pulled toward where
                // the rest point was at the start of that step, so a steady walk ends every step exactly on
                // it (no half-step bias).
                Vector3 restNow = Vector3.Lerp(LastRest, rest, (i - 1) / (float)steps);

                // Damping acts on the motion relative to the rest point, so walking at a steady
                // speed leaves no permanent lag; only changes of speed make the flesh swing.
                Vector3 acceleration = stiffness * (restNow - Position) - damping * (Velocity - restVelocity);
                Velocity += acceleration * h;
                Position += Velocity * h;
            }

            LastRest = rest;
            return Position - rest;
        }
    }

    /// <summary>
    /// Secondary motion for fleshy bones: gives bones such as buttocks, a belly or ears a soft, springy
    /// follow-through on top of whatever the Animator plays, so the body wobbles when it starts, stops,
    /// steps or is struck.
    ///
    /// Each listed bone has a tip (its child bone, normally). The tip is simulated as a damped spring
    /// that chases the animated tip. When the body accelerates the spring lags and then overshoots, and the
    /// bone is rotated toward the sprung tip, so the skinned mesh swings. Nothing moves while the NPC is
    /// still and the clip is still, and walking at a steady pace only bobs it with the steps.
    ///
    /// The goofiness is set by four numbers on the feature: <b>Frequency</b> (how fast it wobbles),
    /// <b>Damping Ratio</b> (low rings for longer, around 0.3 gives about two bounces), <b>Inertia</b> (how
    /// much the flesh lags the body) and <b>Max Angle</b> (a hard cap so it never looks broken). Each bone
    /// can also be given its own speed and strength so the left and right side drift apart, and a <b>Position
    /// Amount</b> so it slides with the flesh as well as swinging (capped by Max Position Offset). A bone can also
    /// override the feature's Max Angle and Damping Ratio, so buttocks can swing wide and bouncy while the torso
    /// stays heavy.
    ///
    /// It drives the bones in <see cref="NpcTickPhase.LateUpdate"/>, after the Animator has posed the
    /// skeleton, and removes its own offset again before the Animator runs, so it works on bones the clips
    /// animate and on bones they do not.
    /// </summary>
    [Serializable]
    [NpcFeature(
        "Bone Jiggle",
        "Presentation",
        Description = "Springy secondary motion for fleshy bones (buttocks, belly, ears): the bones lag, overshoot and settle on top of the animation, with adjustable speed, bounce, amount and angle limit.")]
    public sealed class NpcBoneJiggle : NpcFeature, INpcTickable, INpcPreAnimationTickable
    {
        private const float MaxDeltaTime = 0.1f;
        private const float FallbackTipLength = 0.1f;
        private const float MaxWindUp = 1.5f;

        // Unity does not run field initializers when it loads an existing prefab's feature data, so a setting
        // added after a prefab was saved arrives as 0. Where 0 is not a useful value it means "use the default".
        private const float DefaultMaxPositionOffset = 0.03f;
        private const float DefaultPreviewInterval = 1.5f;
        private const float DefaultPreviewSpeed = 1.5f;

        [Tooltip("Bones that jiggle, each with an optional tip bone.")]
        [SerializeField] private List<NpcJiggleBone> _bones = new List<NpcJiggleBone>();

        [Tooltip("Overall amount, 0 = off, 1 = as set below. Blends the jiggle in and out, so a scene can calm or exaggerate it.")]
        [SerializeField, Range(0f, 2f)] private float _weight = 1f;

        [Tooltip("How fast the flesh wobbles, in cycles per second. Lower is slow and heavy, higher is tight and quick. About 3 to 6 reads as buttocks.")]
        [SerializeField, Range(0.5f, 12f)] private float _frequency = 4f;

        [Tooltip("How quickly the wobble dies out. 0 never stops, 1 does not overshoot at all. Around 0.25 to 0.35 gives a funny bounce that settles after about two swings.")]
        [SerializeField, Range(0.05f, 1f)] private float _dampingRatio = 0.3f;

        [Tooltip("How much the flesh lags behind the body. 1 is natural, above 1 exaggerates it for a goofier look.")]
        [SerializeField, Range(0f, 3f)] private float _inertia = 1f;

        [Tooltip("The most a bone is ever turned away from its animated pose, in degrees. Keeps violent moves from folding the mesh.")]
        [SerializeField, Range(1f, 60f)] private float _maxAngleDegrees = 22f;

        [Tooltip("The furthest a bone with a Position Amount slides from its animated place, in metres (world scale). Keeps the mesh from tearing away from its neighbours. 0 = use the default (0.03).")]
        [SerializeField, Range(0f, 0.2f)] private float _maxPositionOffset = 0.03f;

        [Header("Preview")]
        [Tooltip("Tuning aid: jolts every bone's spring on a timer (as if the body hopped), so you can see and adjust the jiggle on an NPC that is standing still. Off for normal play.")]
        [SerializeField] private bool _previewKick;

        [Tooltip("Seconds between preview jolts.")]
        [SerializeField, Range(0.3f, 5f)] private float _previewInterval = 1.5f;

        [Tooltip("Speed of the preview jolt in metres per second. 1 is a small hop, 3 is a hard jump. It alternates up-and-forward with down-and-back.")]
        [SerializeField, Range(0.1f, 5f)] private float _previewSpeed = 1.5f;

        [Tooltip("A frame in which the animated tip jumps further than this many metres (a warp, or the NPC being placed for a scene) restarts the wobble instead of whipping the bone.")]
        [SerializeField, Min(0.05f)] private float _snapDistance = 0.6f;

        [NonSerialized] private Bone[] _runtime;
        [NonSerialized] private bool _bonesSearched;
        [NonSerialized] private int _boundCount;
        [NonSerialized] private float _kickTimer;
        [NonSerialized] private float _kickSign = 1f;

        private sealed class Bone
        {
            public Transform Transform;
            public Transform Tip;
            public int Index;
            public NpcJiggleSpring Spring;
            public bool Applied;
            public Quaternion LocalBefore;
            public Quaternion LocalAfter;
            public bool PositionApplied;
            public Vector3 LocalPositionBefore;
            public Vector3 LocalPositionAfter;
        }

        /// <summary>
        /// The distance a bone slides with the flesh: the (already scaled) sprung lag times the bone's position
        /// amount, capped at <paramref name="maxOffset"/> metres.
        /// </summary>
        public static Vector3 PositionOffset(Vector3 lag, float amount, float maxOffset)
        {
            Vector3 offset = lag * Mathf.Max(0f, amount);
            return offset.sqrMagnitude > maxOffset * maxOffset
                ? offset.normalized * maxOffset
                : offset;
        }

        /// <summary>Overall amount: 0 = off, 1 = as configured. Can be changed at any time.</summary>
        public float Weight
        {
            get => _weight;
            set => _weight = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>The bones this feature drives (name lists are read when the NPC starts).</summary>
        public IReadOnlyList<NpcJiggleBone> Bones => _bones;

        public NpcTickSettings TickSettings => NpcTickSettings.EveryLateUpdate;

        /// <summary>Calms the bones at once: the next frame starts them at rest.</summary>
        public void ResetMotion()
        {
            if (_runtime == null)
            {
                return;
            }

            for (int i = 0; i < _runtime.Length; i++)
            {
                _runtime[i].Spring.Reset();
            }
        }

        public void BeforeAnimation()
        {
            if (_runtime == null)
            {
                return;
            }

            // Take back our own rotation so the Animator (or, for a bone no clip animates, the bind pose
            // we saved) is what the next frame starts from. A bone someone else changed is left alone.
            for (int i = 0; i < _runtime.Length; i++)
            {
                Bone bone = _runtime[i];
                if (!bone.Applied && !bone.PositionApplied)
                {
                    continue;
                }

                bool wasRotated = bone.Applied;
                bone.Applied = false;
                if (wasRotated && bone.Transform != null && bone.Transform.localRotation == bone.LocalAfter)
                {
                    bone.Transform.localRotation = bone.LocalBefore;
                }

                if (bone.PositionApplied)
                {
                    bone.PositionApplied = false;
                    if (bone.Transform != null && bone.Transform.localPosition == bone.LocalPositionAfter)
                    {
                        bone.Transform.localPosition = bone.LocalPositionBefore;
                    }
                }
            }
        }

        public void Tick(float deltaTime)
        {
            EnsureBones();
            if (_runtime == null || _runtime.Length == 0)
            {
                SetTicking(false);
                return;
            }

            deltaTime = Mathf.Min(deltaTime, MaxDeltaTime);
            float weight = _weight;
            TickPreviewKick(deltaTime);

            for (int i = 0; i < _runtime.Length; i++)
            {
                Bone bone = _runtime[i];
                if (bone.Transform == null)
                {
                    continue;
                }

                Vector3 pivot = bone.Transform.position;
                Vector3 restTip = TipPosition(bone, pivot);
                Vector3 axis = restTip - pivot;
                float length = axis.magnitude;
                if (length < 0.0001f)
                {
                    continue;
                }

                // Per-bone settings are read from the list every tick, so editing them in the Inspector during
                // Play Mode takes effect at once.
                NpcJiggleBone setting = _bones[bone.Index];
                float frequencyScale = setting.frequencyScale > 0f ? setting.frequencyScale : 1f;
                float strength = setting.strength > 0f ? setting.strength : 1f;
                float positionAmount = Mathf.Max(0f, setting.positionAmount);

                Vector3 lag = bone.Spring.Advance(
                    restTip,
                    deltaTime,
                    _frequency * frequencyScale,
                    setting.dampingRatio > 0f ? setting.dampingRatio : _dampingRatio,
                    _snapDistance);

                float maxAngle = setting.maxAngleDegrees > 0f ? setting.maxAngleDegrees : _maxAngleDegrees;
                float limit = length * Mathf.Tan(Mathf.Min(maxAngle, 80f) * Mathf.Deg2Rad);
                if (lag.sqrMagnitude > limit * limit * MaxWindUp * MaxWindUp)
                {
                    // Do not let the spring wind up far past what is shown.
                    lag = lag.normalized * (limit * MaxWindUp);
                    bone.Spring.Position = restTip + lag;
                }

                Vector3 scaled = lag * (_inertia * weight * strength);
                Vector3 shown = scaled;
                if (shown.sqrMagnitude > limit * limit)
                {
                    shown = shown.normalized * limit;
                }

                if (shown.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                Quaternion turn = Quaternion.FromToRotation(axis, axis + shown);
                bone.LocalBefore = bone.Transform.localRotation;
                bone.Transform.rotation = turn * bone.Transform.rotation;
                bone.LocalAfter = bone.Transform.localRotation;
                bone.Applied = true;

                float maxPositionOffset = _maxPositionOffset > 0f ? _maxPositionOffset : DefaultMaxPositionOffset;
                if (positionAmount > 0f)
                {
                    // The flesh slides as well as swings. It follows the tip's lag, so a body that stops
                    // pushes the cheeks on past where the animation holds them, then they settle back.
                    bone.LocalPositionBefore = bone.Transform.localPosition;
                    bone.Transform.position += PositionOffset(scaled, positionAmount, maxPositionOffset);
                    bone.LocalPositionAfter = bone.Transform.localPosition;
                    bone.PositionApplied = true;
                }
            }
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            if (_bones == null || _bones.Count == 0)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "Bone Jiggle has no bones listed, so it does nothing.",
                    Npc));
                return;
            }

            _bonesSearched = false;
            EnsureBones();
            for (int i = 0; i < _bones.Count; i++)
            {
                if (string.IsNullOrEmpty(_bones[i].boneName) || FindDeep(_bones[i].boneName) == null)
                {
                    issues.Add(new NpcValidationIssue(
                        NpcValidationSeverity.Warning,
                        $"Bone Jiggle cannot find the bone '{_bones[i].boneName}' inside this NPC.",
                        Npc));
                }
            }
        }

        protected override void OnFeatureInitialized()
        {
            _bonesSearched = false;
        }

        protected override void OnFeatureActivated()
        {
            ResetMotion();
            SetTicking(true);
        }

        protected override void OnFeatureDeactivated()
        {
            // The Animator rewrites animated bones and BeforeAnimation restores the rest, but a feature
            // switched off between those two moments must not leave a rotation behind.
            BeforeAnimation();
        }

        protected override void OnFeatureShutdown()
        {
            BeforeAnimation();
        }

        private void EnsureBones()
        {
            if (Transform == null)
            {
                return;
            }

            // The list was edited (a bone added or removed in the Inspector): take our offsets off first,
            // then search again.
            if (_bonesSearched && _bones != null && _bones.Count != _boundCount)
            {
                BeforeAnimation();
                _bonesSearched = false;
            }

            if (_bonesSearched)
            {
                return;
            }

            _bonesSearched = true;
            _boundCount = _bones != null ? _bones.Count : 0;
            var found = new List<Bone>(_bones != null ? _bones.Count : 0);
            if (_bones != null)
            {
                for (int i = 0; i < _bones.Count; i++)
                {
                    NpcJiggleBone entry = _bones[i];
                    Transform bone = FindDeep(entry.boneName);
                    if (bone == null)
                    {
                        continue;
                    }

                    Transform tip = null;
                    if (!string.IsNullOrEmpty(entry.tipBoneName))
                    {
                        tip = FindDeep(entry.tipBoneName);
                    }

                    if (tip == null && bone.childCount > 0)
                    {
                        tip = bone.GetChild(0);
                    }

                    found.Add(new Bone
                    {
                        Transform = bone,
                        Tip = tip,
                        Index = i
                    });
                }
            }

            _runtime = found.ToArray();
        }

        private void TickPreviewKick(float deltaTime)
        {
            if (!_previewKick)
            {
                _kickTimer = 0f;
                return;
            }

            _kickTimer += deltaTime;
            if (_kickTimer < (_previewInterval > 0f ? _previewInterval : DefaultPreviewInterval))
            {
                return;
            }

            _kickTimer = 0f;
            _kickSign = -_kickSign;
            // Up and forward together, so the bones both slide and swing (a bone only turns for motion across its own
            // length, never for motion straight along it).
            Vector3 jolt = (Vector3.up + Transform.forward * 0.8f).normalized * (_kickSign * (_previewSpeed > 0f ? _previewSpeed : DefaultPreviewSpeed));
            for (int i = 0; i < _runtime.Length; i++)
            {
                if (_runtime[i].Spring.Primed)
                {
                    _runtime[i].Spring.Velocity += jolt;
                }
            }
        }

        private static Vector3 TipPosition(Bone bone, Vector3 pivot)
        {
            if (bone.Tip != null)
            {
                return bone.Tip.position;
            }

            // No child: continue the bone away from its parent.
            Transform parent = bone.Transform.parent;
            Vector3 direction = parent != null ? pivot - parent.position : bone.Transform.up;
            if (direction.sqrMagnitude < 1e-8f)
            {
                direction = bone.Transform.up;
            }

            return pivot + direction.normalized * (FallbackTipLength * bone.Transform.lossyScale.y);
        }

        private Transform FindDeep(string boneName)
        {
            if (string.IsNullOrEmpty(boneName) || Transform == null)
            {
                return null;
            }

            Transform[] all = Transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == boneName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
