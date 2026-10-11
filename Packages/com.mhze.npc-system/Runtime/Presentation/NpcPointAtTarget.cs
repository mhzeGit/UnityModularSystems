using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace ModularNPC
{
    /// <summary>One bone of an arm's aim chain and the share of the pointing rotation it takes.</summary>
    [Serializable]
    public struct NpcPointBone
    {
        [Tooltip("Bone name, searched inside this NPC.")]
        public string boneName;

        [Tooltip("Relative share of the pointing rotation this bone takes. Weights are relative to the other bones of the same arm, so 1 and 3 give the first bone a quarter of the rotation. The last bone of the chain always completes the aim, whatever its weight.")]
        [Min(0f)] public float weight;

        public NpcPointBone(string boneName, float weight)
        {
            this.boneName = boneName;
            this.weight = weight;
        }
    }

    /// <summary>
    /// Extends one of the NPC's arms in a straight line toward a world position - the gesture of
    /// pointing out a room, a door or a landmark - and lowers it again when told to stop.
    ///
    /// Both arms are supported and the NPC picks the better one on its own. Every tick the feature
    /// measures the target's bearing from the NPC's current facing (so it keeps up while the body is
    /// still turning), scores each arm by how far it has to travel from its animated pose plus a
    /// penalty for reaching across the chest, and raises the cheaper one. A small hysteresis keeps it
    /// from flipping arms when the two are nearly tied, and a target that is behind the NPC is not
    /// pointed at until the body has turned enough to make it a sensible gesture.
    ///
    /// Each arm is built from two bone lists. The <b>aim bones</b> (shoulder, upper arm, twist bones -
    /// as many as the rig has, root first) share the rotation that points the arm: each takes its
    /// weight's portion of what is still missing, and the last one takes all of the remainder, so the
    /// chain as a whole ends exactly on the target while the rotation is spread over every bone. The
    /// <b>follow bones</b> (forearm, hand, ...) are then laid along the same line, which makes the
    /// arm and hand straight.
    ///
    /// The feature is target-agnostic: it knows nothing about a player or a dialog. Whatever drives
    /// the NPC calls <see cref="PointAt(Transform, Vector3, NpcPointArm)"/> and
    /// <see cref="StopPointing"/>.
    ///
    /// It drives the bones directly in <see cref="NpcTickPhase.LateUpdate"/>, after the Animator has
    /// posed the skeleton, and ticks only while pointing or lowering, so an idle feature costs nothing.
    /// The pose is blended over the animated one by an eased weight so the arm raises and lowers
    /// smoothly. Bones are found by name, with the humanoid avatar as fallback.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "ModularNPC", "Assembly-CSharp", null)]
    [NpcFeature(
        "Point At Target",
        "Presentation",
        Description = "Extends the arm that is best placed toward any target with a straight arm and hand, choosing between the left and right arm from the target's bearing, spreading the aim over several weighted bones, and lowers it when the point ends.")]
    public sealed class NpcPointAtTarget : NpcFeature, INpcPointAt, INpcTickable
    {
        private const string DefaultLeftUpperArm = "LeftArm";
        private const string DefaultLeftForeArm = "LeftForeArm";
        private const string DefaultLeftHand = "LeftHand";
        private const string DefaultRightUpperArm = "RightArm";
        private const string DefaultRightForeArm = "RightForeArm";
        private const string DefaultRightHand = "RightHand";

        private const float DefaultRaiseDuration = 0.35f;
        private const float DefaultLowerDuration = 0.5f;
        private const float DefaultMaxBearing = 150f;
        private const float MinimumDuration = 0.01f;
        private const float AppliedWeightEpsilon = 0.0005f;

        [Header("Left arm bones")]
        [Tooltip("Bones that share the rotation, root first (for example shoulder, then upper arm). The last one points straight at the target; the others each take their weight's share of the rotation on the way there.")]
        [SerializeField]
        private List<NpcPointBone> _leftAimBones = new List<NpcPointBone>
        {
            new NpcPointBone(DefaultLeftUpperArm, 1f)
        };

        [Tooltip("Bones laid along the finished line after the aim bones, in order (forearm, then hand).")]
        [SerializeField]
        private List<string> _leftFollowBones = new List<string> { DefaultLeftForeArm, DefaultLeftHand };

        [Header("Right arm bones")]
        [SerializeField]
        private List<NpcPointBone> _rightAimBones = new List<NpcPointBone>
        {
            new NpcPointBone(DefaultRightUpperArm, 1f)
        };

        [SerializeField]
        private List<string> _rightFollowBones = new List<string> { DefaultRightForeArm, DefaultRightHand };

        [Header("Arm choice")]
        [Tooltip("The arm used when both are equally good, for example when the target is straight ahead.")]
        [SerializeField] private NpcPointArm _dominantArm = NpcPointArm.Right;

        [Tooltip("Degrees the other arm must beat the raised one by before the NPC swaps arms. Stops flicker when the target is almost centred.")]
        [SerializeField, Range(0f, 60f)] private float _switchHysteresisDegrees = 15f;

        [Tooltip("How much a target on the opposite side of the body counts against an arm, per degree it is across the midline. 0 ignores crossing; higher prefers the same-side arm.")]
        [SerializeField, Range(0f, 2f)] private float _crossBodyPenalty = 0.6f;

        [Tooltip("The arm stays lowered while the target is more than this many degrees off the NPC's facing, and rises once the body has turned enough. 180 points at anything.")]
        [SerializeField, Range(30f, 180f)] private float _maxBearingDegrees = DefaultMaxBearing;

        [Header("Motion")]
        [Tooltip("Seconds for the arm to reach full extension.")]
        [SerializeField, Min(0.01f)] private float _raiseDuration = DefaultRaiseDuration;

        [Tooltip("Seconds for the arm to drop back to the animated pose.")]
        [SerializeField, Min(0.01f)] private float _lowerDuration = DefaultLowerDuration;

        [Tooltip("Also aligns the last follow bone (the hand) with the arm, so the fingers continue the line instead of keeping the animated wrist bend.")]
        [SerializeField] private bool _straightenHand = true;

        [Tooltip("Twists the extended arm about its pointing axis, in degrees. Mirrored for the left arm. Use it to turn the palm toward the ground or sideways.")]
        [SerializeField, Range(-180f, 180f)] private float _rollDegrees;

        [NonSerialized] private Arm _left;
        [NonSerialized] private Arm _right;
        [NonSerialized] private bool _bonesSearched;
        [NonSerialized] private bool _pointing;
        [NonSerialized] private bool _hasTransformTarget;
        [NonSerialized] private Transform _target;
        [NonSerialized] private Vector3 _aimOffset;
        [NonSerialized] private Vector3 _fixedPoint;
        [NonSerialized] private Vector3 _lastAim;
        [NonSerialized] private NpcPointArm _requestedArm;
        [NonSerialized] private NpcPointArm _chosenArm;

        /// <summary>True from a successful <c>PointAt</c> until <see cref="StopPointing"/>.</summary>
        public bool IsPointing => _pointing;

        /// <summary>The transform being pointed at, or null when pointing at a fixed point or idle.</summary>
        public Transform Target => _pointing && _hasTransformTarget ? _target : null;

        /// <summary>The world position the arm is aimed at.</summary>
        public Vector3 TargetPosition => _lastAim;

        /// <summary>The arm currently raised or being raised, <see cref="NpcPointArm.None"/> when idle.</summary>
        public NpcPointArm ActiveArm
        {
            get
            {
                float left = _left != null ? _left.Raw : 0f;
                float right = _right != null ? _right.Raw : 0f;
                if (left <= 0f && right <= 0f)
                {
                    return NpcPointArm.None;
                }

                return left >= right ? NpcPointArm.Left : NpcPointArm.Right;
            }
        }

        /// <summary>0 with every arm at its animated pose, 1 with the active arm fully extended.</summary>
        public float Weight
        {
            get
            {
                float left = _left != null ? _left.Eased : 0f;
                float right = _right != null ? _right.Eased : 0f;
                return Mathf.Max(left, right);
            }
        }

        // The arm bones are animated; the Animator applies its pose before LateUpdate in this tick
        // order, so ticking here keeps the point from being overwritten each frame.
        public NpcTickSettings TickSettings => NpcTickSettings.EveryLateUpdate;

        public bool PointAt(Transform target, Vector3 aimOffset = default, NpcPointArm arm = NpcPointArm.None)
        {
            if (target == null || target == Transform || !EnsureBones())
            {
                return false;
            }

            _target = target;
            _hasTransformTarget = true;
            _aimOffset = aimOffset;
            Begin(arm);
            return true;
        }

        public bool PointAt(Vector3 worldPosition, NpcPointArm arm = NpcPointArm.None)
        {
            if (!EnsureBones())
            {
                return false;
            }

            _target = null;
            _hasTransformTarget = false;
            _fixedPoint = worldPosition;
            Begin(arm);
            return true;
        }

        public void StopPointing()
        {
            _pointing = false;
            _target = null;
            _hasTransformTarget = false;
            _chosenArm = NpcPointArm.None;
            if (EnsureBones())
            {
                SetTicking(true);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!EnsureBones())
            {
                SetTicking(false);
                return;
            }

            // Put each arm back to the pose the Animator last wrote (or leave it if the Animator
            // already wrote a fresh one) so the pointing pose is always built from the animation.
            Prepare(_left);
            Prepare(_right);

            Vector3 aim = default;
            bool hasAim = _pointing && TryResolveAim(out aim);
            if (!hasAim)
            {
                _pointing = false;
                _chosenArm = NpcPointArm.None;
            }
            else
            {
                _lastAim = aim;
            }

            NpcPointArm desired = hasAim ? SelectArm(aim) : NpcPointArm.None;
            _chosenArm = desired;

            Advance(_left, desired == NpcPointArm.Left, deltaTime);
            Advance(_right, desired == NpcPointArm.Right, deltaTime);

            if (hasAim)
            {
                Apply(_left, aim);
                Apply(_right, aim);
            }
            else
            {
                // Lowering: keep each remaining arm aimed where it was while it blends back to the
                // animated pose, so the drop stays smooth after the target is gone.
                ApplyLowering(_left);
                ApplyLowering(_right);
            }

            if (!_pointing && Weight <= AppliedWeightEpsilon && !HasRaisedArm())
            {
                SetTicking(false);
            }
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            EnsureBones();
            ReportArm(issues, _left, "left");
            ReportArm(issues, _right, "right");
        }

        protected override void OnFeatureInitialized()
        {
            ResetState();
            EnsureBones();
        }

        protected override void OnFeatureActivated()
        {
            EnsureBones();
            SetTicking(_pointing || HasRaisedArm());
        }

        protected override void OnFeatureDeactivated()
        {
            RestoreAnimatedPose(_left);
            RestoreAnimatedPose(_right);
            ResetState();
            SetTicking(false);
        }

        protected override void OnFeatureShutdown()
        {
            RestoreAnimatedPose(_left);
            RestoreAnimatedPose(_right);
            ResetState();
            _left = null;
            _right = null;
            _bonesSearched = false;
        }

        protected override void OnFeatureValidate()
        {
            _switchHysteresisDegrees = Mathf.Clamp(_switchHysteresisDegrees, 0f, 60f);
            _crossBodyPenalty = Mathf.Clamp(_crossBodyPenalty, 0f, 2f);
            _maxBearingDegrees = Mathf.Clamp(_maxBearingDegrees, 30f, 180f);
            _raiseDuration = Mathf.Max(MinimumDuration, _raiseDuration);
            _lowerDuration = Mathf.Max(MinimumDuration, _lowerDuration);
            ClampWeights(_leftAimBones);
            ClampWeights(_rightAimBones);
        }

        private static void ClampWeights(List<NpcPointBone> bones)
        {
            if (bones == null)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                NpcPointBone bone = bones[i];
                if (bone.weight < 0f)
                {
                    bone.weight = 0f;
                    bones[i] = bone;
                }
            }
        }

        private void Begin(NpcPointArm arm)
        {
            _pointing = true;
            _requestedArm = arm;
            SetTicking(true);
        }

        private bool TryResolveAim(out Vector3 aim)
        {
            if (_hasTransformTarget)
            {
                // A destroyed target ends the point rather than freezing the arm on its last aim.
                if (_target == null)
                {
                    aim = default;
                    return false;
                }

                aim = _target.position + _aimOffset;
                return true;
            }

            aim = _fixedPoint;
            return true;
        }

        private bool HasRaisedArm()
        {
            return (_left != null && _left.Raw > 0f) || (_right != null && _right.Raw > 0f);
        }

        private void ResetState()
        {
            _pointing = false;
            _target = null;
            _hasTransformTarget = false;
            _chosenArm = NpcPointArm.None;
            _requestedArm = NpcPointArm.None;
            if (_left != null)
            {
                _left.Raw = 0f;
                _left.HasWritten = false;
                _left.HasLastAim = false;
            }

            if (_right != null)
            {
                _right.Raw = 0f;
                _right.HasWritten = false;
                _right.HasLastAim = false;
            }
        }

        // ---------------------------------------------------------------- arm choice

        private NpcPointArm SelectArm(Vector3 aim)
        {
            bool leftOk = _left != null && _left.IsComplete;
            bool rightOk = _right != null && _right.IsComplete;
            if (!leftOk && !rightOk)
            {
                return NpcPointArm.None;
            }

            float bearing = BearingTo(aim, leftOk ? _left : null, rightOk ? _right : null);
            if (Mathf.Abs(bearing) > _maxBearingDegrees)
            {
                // Behind the NPC: wait for the body to turn rather than reach backwards.
                return NpcPointArm.None;
            }

            if (_requestedArm == NpcPointArm.Left && leftOk)
            {
                return NpcPointArm.Left;
            }

            if (_requestedArm == NpcPointArm.Right && rightOk)
            {
                return NpcPointArm.Right;
            }

            if (!leftOk)
            {
                return NpcPointArm.Right;
            }

            if (!rightOk)
            {
                return NpcPointArm.Left;
            }

            float leftCost = Cost(_left, aim, bearing);
            float rightCost = Cost(_right, aim, bearing);

            if (_chosenArm == NpcPointArm.None)
            {
                if (Mathf.Abs(leftCost - rightCost) < 1f)
                {
                    return _dominantArm == NpcPointArm.Left ? NpcPointArm.Left : NpcPointArm.Right;
                }

                return leftCost < rightCost ? NpcPointArm.Left : NpcPointArm.Right;
            }

            // Keep the raised arm unless the other one is clearly better, so a target near the
            // midline does not make the NPC keep swapping arms as the body turns.
            float currentCost = _chosenArm == NpcPointArm.Left ? leftCost : rightCost;
            float otherCost = _chosenArm == NpcPointArm.Left ? rightCost : leftCost;
            if (otherCost + _switchHysteresisDegrees < currentCost)
            {
                return _chosenArm == NpcPointArm.Left ? NpcPointArm.Right : NpcPointArm.Left;
            }

            return _chosenArm;
        }

        /// <summary>
        /// Signed degrees from the NPC's facing to the target, measured from the middle of the
        /// shoulders on the horizontal plane. Positive is to the NPC's right.
        /// </summary>
        private float BearingTo(Vector3 aim, Arm left, Arm right)
        {
            Vector3 origin;
            if (left != null && right != null)
            {
                origin = (left.Origin.position + right.Origin.position) * 0.5f;
            }
            else if (left != null)
            {
                origin = left.Origin.position;
            }
            else
            {
                origin = right.Origin.position;
            }

            Vector3 toTarget = aim - origin;
            toTarget.y = 0f;
            Vector3 forward = Transform.forward;
            forward.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(forward, toTarget, Vector3.up);
        }

        /// <summary>
        /// How much work an arm needs to point at the target: the angle it must swing from its
        /// animated pose, plus a penalty for each degree the target lies across the body midline.
        /// </summary>
        private float Cost(Arm arm, Vector3 aim, float bearing)
        {
            Vector3 toTarget = aim - arm.Origin.position;
            float swing = toTarget.sqrMagnitude > 0.0001f
                ? Vector3.Angle(arm.AnimatedDirection, toTarget)
                : 0f;

            float side = arm.Side == NpcPointArm.Right ? 1f : -1f;
            float crossing = Mathf.Max(0f, -side * bearing);
            return swing + _crossBodyPenalty * crossing;
        }

        // ---------------------------------------------------------------- posing

        private void Advance(Arm arm, bool raise, float deltaTime)
        {
            if (arm == null)
            {
                return;
            }

            float duration = raise
                ? Mathf.Max(MinimumDuration, _raiseDuration)
                : Mathf.Max(MinimumDuration, _lowerDuration);
            float step = Mathf.Max(0f, deltaTime) / duration;
            arm.Raw = Mathf.MoveTowards(arm.Raw, raise ? 1f : 0f, step);
        }

        private void Apply(Arm arm, Vector3 aim)
        {
            if (arm == null || !arm.IsComplete)
            {
                return;
            }

            float weight = arm.Eased;
            if (weight <= AppliedWeightEpsilon)
            {
                return;
            }

            arm.LastAim = aim;
            arm.HasLastAim = true;
            Pose(arm, aim, weight);
        }

        private void ApplyLowering(Arm arm)
        {
            if (arm == null || !arm.IsComplete || arm.Raw <= 0f || !arm.HasLastAim)
            {
                return;
            }

            float weight = arm.Eased;
            if (weight <= AppliedWeightEpsilon)
            {
                return;
            }

            Pose(arm, arm.LastAim, weight);
        }

        /// <summary>
        /// Builds the pointing pose from the animated one and blends it in by
        /// <paramref name="weight"/>.
        ///
        /// Aim bones, root first: each recomputes the rotation still missing to bring the last aim
        /// bone's segment (itself to the next bone) onto the line to the target, and applies its
        /// share of it - its weight over the weights of itself and the bones after it. The shares
        /// multiply out to each bone's weight over the total, and the last bone's share is 1, so the
        /// segment ends exactly on the target however the weights are split. Because a bone is
        /// turned about its own pivot, any shoulder bone that moves the arm's origin is accounted
        /// for by the next bone's remaining rotation.
        ///
        /// Follow bones are then laid on that line, one after another, which makes the arm straight.
        /// </summary>
        private void Pose(Arm arm, Vector3 aim, float weight)
        {
            int aimCount = arm.AimCount;
            Transform last = arm.Bones[aimCount - 1];

            float remainingWeight = 0f;
            for (int i = 0; i < aimCount; i++)
            {
                remainingWeight += arm.Weights[i];
            }

            for (int i = 0; i < aimCount; i++)
            {
                float own = arm.Weights[i];
                float share = i == aimCount - 1
                    ? 1f
                    : remainingWeight > 0.0001f ? Mathf.Clamp01(own / remainingWeight) : 0f;
                remainingWeight -= own;
                if (share <= 0f)
                {
                    continue;
                }

                Vector3 current = arm.EndChild.position - last.position;
                Vector3 wanted = aim - last.position;
                if (current.sqrMagnitude < 0.000001f || wanted.sqrMagnitude < 0.000001f)
                {
                    break;
                }

                Quaternion remaining = Quaternion.FromToRotation(current, wanted);
                Quaternion part = share >= 1f ? remaining : Quaternion.Slerp(Quaternion.identity, remaining, share);
                Transform bone = arm.Bones[i];
                bone.rotation = part * bone.rotation;
            }

            Vector3 line = aim - last.position;
            if (line.sqrMagnitude > 0.000001f)
            {
                line.Normalize();
                for (int i = aimCount; i < arm.Bones.Length; i++)
                {
                    bool isLast = i == arm.Bones.Length - 1;
                    if (isLast && !_straightenHand)
                    {
                        break;
                    }

                    Transform child = isLast ? arm.Tip : arm.Bones[i + 1];
                    if (child != null)
                    {
                        AlignToDirection(arm.Bones[i], child.position, line);
                    }
                }

                if (Mathf.Abs(_rollDegrees) > 0.01f)
                {
                    float roll = arm.Side == NpcPointArm.Right ? _rollDegrees : -_rollDegrees;
                    last.rotation = Quaternion.AngleAxis(roll, line) * last.rotation;
                }
            }

            for (int i = 0; i < arm.Bones.Length; i++)
            {
                Transform bone = arm.Bones[i];
                bone.localRotation = Quaternion.Slerp(arm.Animated[i], bone.localRotation, weight);
                arm.Written[i] = bone.localRotation;
            }

            arm.HasWritten = true;
        }

        private static void AlignToDirection(Transform bone, Vector3 childPosition, Vector3 direction)
        {
            Vector3 current = childPosition - bone.position;
            if (current.sqrMagnitude < 0.000001f)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>
        /// Makes the bones hold the pose the Animator produced this frame. When the Animator did not
        /// rewrite a bone (no curve animates it), the value is still the one this feature wrote last
        /// tick, so the stored animated pose is put back instead of blending from the pointed pose.
        /// </summary>
        private static void Prepare(Arm arm)
        {
            RestoreAnimatedPose(arm);
            if (arm != null && arm.IsComplete)
            {
                arm.AnimatedDirection = (arm.EndChild.position - arm.Origin.position).normalized;
            }
        }

        private static void RestoreAnimatedPose(Arm arm)
        {
            if (arm == null || !arm.IsComplete)
            {
                return;
            }

            for (int i = 0; i < arm.Bones.Length; i++)
            {
                Transform bone = arm.Bones[i];
                Quaternion current = bone.localRotation;
                bool untouched = arm.HasWritten && IsSame(current, arm.Written[i]);
                if (untouched)
                {
                    bone.localRotation = arm.Animated[i];
                }
                else
                {
                    arm.Animated[i] = current;
                }
            }

            arm.HasWritten = false;
        }

        private static bool IsSame(Quaternion a, Quaternion b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }

        // ---------------------------------------------------------------- bones

        private bool EnsureBones()
        {
            if (!_bonesSearched)
            {
                if (Transform == null)
                {
                    return false;
                }

                _bonesSearched = true;
                _left = BuildArm(
                    NpcPointArm.Left,
                    _leftAimBones,
                    _leftFollowBones,
                    DefaultLeftUpperArm,
                    DefaultLeftForeArm,
                    DefaultLeftHand,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand);
                _right = BuildArm(
                    NpcPointArm.Right,
                    _rightAimBones,
                    _rightFollowBones,
                    DefaultRightUpperArm,
                    DefaultRightForeArm,
                    DefaultRightHand,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand);
            }

            return (_left != null && _left.IsComplete) || (_right != null && _right.IsComplete);
        }

        private Arm BuildArm(
            NpcPointArm side,
            List<NpcPointBone> aimNames,
            List<string> followNames,
            string defaultUpper,
            string defaultFore,
            string defaultHand,
            HumanBodyBones upperBone,
            HumanBodyBones foreBone,
            HumanBodyBones handBone)
        {
            Transform root = Transform;
            var bones = new List<Transform>(6);
            var weights = new List<float>(4);
            var missing = new List<string>(2);

            // A serialized list that predates the field deserializes empty: fall back to the rig's
            // standard three arm bones rather than an arm that can never be raised.
            bool hasAimList = aimNames != null && aimNames.Count > 0;
            if (hasAimList)
            {
                for (int i = 0; i < aimNames.Count; i++)
                {
                    Transform bone = FindByName(root, aimNames[i].boneName);
                    if (bone == null)
                    {
                        missing.Add(aimNames[i].boneName);
                        continue;
                    }

                    bones.Add(bone);
                    weights.Add(Mathf.Max(0f, aimNames[i].weight));
                }
            }
            else
            {
                Transform upper = FindByName(root, defaultUpper);
                if (upper != null)
                {
                    bones.Add(upper);
                    weights.Add(1f);
                }
                else
                {
                    missing.Add(defaultUpper);
                }
            }

            int aimCount = bones.Count;

            bool hasFollowList = followNames != null && followNames.Count > 0;
            var followCandidates = hasFollowList
                ? followNames
                : new List<string> { defaultFore, defaultHand };
            for (int i = 0; i < followCandidates.Count; i++)
            {
                Transform bone = FindByName(root, followCandidates[i]);
                if (bone == null)
                {
                    missing.Add(followCandidates[i]);
                    continue;
                }

                bones.Add(bone);
            }

            // Generic rigs are named; a humanoid avatar can stand in for whichever group is empty.
            if ((aimCount == 0 || bones.Count == aimCount) &&
                TryGetFeature(out NpcAnimation animation) &&
                animation.Animator != null &&
                animation.Animator.isHuman)
            {
                Animator animator = animation.Animator;
                if (aimCount == 0)
                {
                    Transform upper = animator.GetBoneTransform(upperBone);
                    if (upper != null)
                    {
                        bones.Insert(0, upper);
                        weights.Add(1f);
                        aimCount = 1;
                    }
                }

                if (bones.Count == aimCount)
                {
                    Transform fore = animator.GetBoneTransform(foreBone);
                    Transform hand = animator.GetBoneTransform(handBone);
                    if (fore != null)
                    {
                        bones.Add(fore);
                    }

                    if (hand != null)
                    {
                        bones.Add(hand);
                    }
                }
            }

            if (aimCount == 0)
            {
                return new Arm { Side = side, Missing = missing };
            }

            var arm = new Arm
            {
                Side = side,
                Bones = bones.ToArray(),
                AimCount = aimCount,
                Weights = weights.ToArray(),
                Missing = missing
            };

            arm.Animated = new Quaternion[arm.Bones.Length];
            arm.Written = new Quaternion[arm.Bones.Length];

            Transform lastBone = arm.Bones[arm.Bones.Length - 1];
            arm.Tip = FindEndChild(lastBone);

            // The aim segment of the last aim bone runs to the next bone of the arm, or - when there
            // are no follow bones - to the end child of the aim bone itself.
            arm.EndChild = arm.Bones.Length > aimCount ? arm.Bones[aimCount] : arm.Tip;
            return arm;
        }

        /// <summary>
        /// The child that gives a bone its direction: the middle finger's base, then the index's,
        /// then simply the first child. Null for a bone with no children.
        /// </summary>
        private static Transform FindEndChild(Transform bone)
        {
            Transform index = null;
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                if (child.name.IndexOf("middle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                if (index == null && child.name.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    index = child;
                }
            }

            if (index != null)
            {
                return index;
            }

            return bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName))
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, boneName, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private void ReportArm(List<NpcValidationIssue> issues, Arm arm, string label)
        {
            if (arm == null || !arm.IsComplete)
            {
                issues.Add(new NpcValidationIssue(
                    NpcValidationSeverity.Warning,
                    "Point At Target could not build the " + label + " arm: no aim bone was found inside this NPC. " +
                    "Fix the bone names, or use a humanoid avatar. That arm will not be used.",
                    Npc));
                return;
            }

            if (arm.Missing != null)
            {
                for (int i = 0; i < arm.Missing.Count; i++)
                {
                    issues.Add(new NpcValidationIssue(
                        NpcValidationSeverity.Warning,
                        "Point At Target: the " + label + " arm bone '" + arm.Missing[i] + "' was not found and is skipped.",
                        Npc));
                }
            }

            // The shared rotation only makes sense down a limb: every bone must sit below the one
            // before it, or the bones would be turned about unrelated pivots.
            for (int i = 1; i < arm.Bones.Length; i++)
            {
                if (!arm.Bones[i].IsChildOf(arm.Bones[i - 1]))
                {
                    issues.Add(new NpcValidationIssue(
                        NpcValidationSeverity.Warning,
                        "Point At Target: '" + arm.Bones[i].name + "' is not below '" + arm.Bones[i - 1].name +
                        "' in the " + label + " arm. List bones root first, each inside the previous one.",
                        Npc));
                }
            }
        }

        /// <summary>Runtime state for one arm: its bones and how far it is raised.</summary>
        private sealed class Arm
        {
            public NpcPointArm Side;

            /// <summary>Aim bones first (root to tip), then follow bones.</summary>
            public Transform[] Bones;

            /// <summary>How many leading <see cref="Bones"/> share the aim rotation.</summary>
            public int AimCount;

            /// <summary>Weight of each aim bone.</summary>
            public float[] Weights;

            /// <summary>The transform whose position, from the last aim bone, defines the aim segment.</summary>
            public Transform EndChild;

            /// <summary>The child of the last bone that gives it a direction (the finger base).</summary>
            public Transform Tip;

            public List<string> Missing;

            /// <summary>Local rotations as the Animator last posed them.</summary>
            public Quaternion[] Animated;

            /// <summary>Local rotations this feature wrote last tick, to tell whether the Animator rewrote them.</summary>
            public Quaternion[] Written;

            public bool HasWritten;

            /// <summary>World position this arm was last aimed at, so a lowering arm keeps its line.</summary>
            public Vector3 LastAim;
            public bool HasLastAim;

            /// <summary>Unit direction of the aim segment in the animated pose.</summary>
            public Vector3 AnimatedDirection;

            /// <summary>Linear 0..1 raise progress.</summary>
            public float Raw;

            public bool IsComplete => Bones != null && AimCount > 0 && EndChild != null;

            /// <summary>The joint the arm rotates about: the last aim bone, the upper arm.</summary>
            public Transform Origin => Bones[AimCount - 1];

            public float Eased => Raw * Raw * (3f - 2f * Raw);
        }
    }
}
