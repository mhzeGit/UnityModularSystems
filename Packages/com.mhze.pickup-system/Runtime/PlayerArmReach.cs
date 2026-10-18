using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>
    /// Makes the first-person arm reach out and put the tip of whatever it holds on a point in the
    /// world (the brush on a canvas, a probe on a button), over the arm's animated pose. A target is
    /// the world point for the tip and the direction the held thing points back toward the hand; the
    /// arm is solved with two bones, the hand is turned so the thing points the right way, and the
    /// whole arm slides toward the target when it would otherwise be out of reach. Release it and
    /// the arm eases back to the animation. Put it on the object with the arm's Animator.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    [AddComponentMenu("MHZE/Pickup/Player Arm Reach")]
    public sealed class PlayerArmReach : MonoBehaviour
    {
        [Header("Rig (found by name when empty)")]
        [Tooltip("What is moved when the arm has to slide toward a target: the arm's root.")]
        [SerializeField] private Transform armRoot;
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Transform hand;
        [SerializeField] private Transform socket;

        [Tooltip("The other arm: scaled away while this one reaches, so its cut end never shows.")]
        [SerializeField] private Transform otherArm;

        [Header("Posture")]
        [Tooltip("How far the whole arm is moved forward (x right, y up, z forward, in the arm root's parent space) while it reaches.")]
        [SerializeField] private Vector3 reachShift = new Vector3(0f, -0.04f, 0.06f);

        [Tooltip("Where the elbow points, relative to the shoulder, in the arm root's parent space.")]
        [SerializeField] private Vector3 elbowPole = new Vector3(0.45f, -0.7f, -0.1f);

        [Tooltip("How much longer the arm is made while it reaches, at least (the viewmodel's arm is short for a real person's).")]
        [Range(1f, 2.5f)]
        [SerializeField] private float restStretch = 1.4f;

        [Tooltip("How much longer the arm can be made to reach a point; beyond that the whole arm slides toward it.")]
        [Range(1f, 3f)]
        [SerializeField] private float maxStretch = 2.1f;

        [Tooltip("Seconds to blend between the animated pose and the reaching pose.")]
        [Min(0.01f)]
        [SerializeField] private float blendSeconds = 0.4f;

        [Tooltip("How much of the arm's full length the tip may need before the whole arm slides toward it.")]
        [Range(0.6f, 1f)]
        [SerializeField] private float comfortableReach = 0.9f;

        [Tooltip("How quickly the arm slides toward a target that is out of reach (per second).")]
        [Min(1f)]
        [SerializeField] private float slideSpeed = 10f;

        private Transform _tip;
        private Vector3 _tipTarget;
        private Vector3 _towardHand;
        private bool _active;
        private float _weight;
        private Vector3 _base;
        private Vector3 _lastSet;
        private bool _hasBase;
        private Vector3 _slide;
        private bool _reached;
        private Vector3 _forearmRest;
        private Vector3 _handRest;
        private Vector3 _otherArmScale = Vector3.one;
        private bool _restCaptured;
        private float _stretch = 1f;

        /// <summary>Wanted by something: the arm is reaching, or still easing back.</summary>
        public bool IsReaching => _active || _weight > 0.001f;

        /// <summary>From 0 (the animated pose) to 1 (fully reaching).</summary>
        public float Weight => _weight;

        /// <summary>The tip is on its target (within a centimetre) at the end of the last frame.</summary>
        public bool TipReached => _reached;

        /// <summary>Starts reaching with the tip of what the hand holds.</summary>
        public void Begin(Transform heldTip)
        {
            _tip = heldTip;
            _active = heldTip != null;
        }

        /// <summary>Where the tip should be, and the way the thing points from the tip back toward the hand.</summary>
        public void SetTarget(Vector3 tipWorld, Vector3 towardHandWorld)
        {
            _tipTarget = tipWorld;
            _towardHand = towardHandWorld.sqrMagnitude > 0.0001f ? towardHandWorld.normalized : Vector3.back;
        }

        /// <summary>Lets go: the arm eases back to its animation.</summary>
        public void End()
        {
            _active = false;
        }

        /// <summary>Back to the animation at once.</summary>
        public void EndNow()
        {
            _active = false;
            _weight = 0f;
            _slide = Vector3.zero;
            RestoreRoot();
            RestoreLimbs();
        }

        private void Awake()
        {
            Resolve();
        }

        private void OnDisable()
        {
            _active = false;
            _weight = 0f;
            RestoreRoot();
            RestoreLimbs();
        }

        private void Resolve()
        {
            if (armRoot == null)
            {
                armRoot = transform;
            }

            if (upperArm == null)
            {
                upperArm = transform.Find("Armature/Root/Right Arm");
            }

            if (forearm == null)
            {
                forearm = transform.Find("Armature/Root/Right Arm/Right Forearm");
            }

            if (hand == null)
            {
                hand = transform.Find("Armature/Root/Right Arm/Right Forearm/Right Hand");
            }

            if (socket == null)
            {
                socket = transform.Find("Armature/Root/Right Arm/Right Forearm/Right Hand/Right Hand Socket");
            }

            if (otherArm == null)
            {
                otherArm = transform.Find("Armature/Root/Right Arm.001");
            }

            if (!_restCaptured && forearm != null && hand != null)
            {
                _restCaptured = true;
                _forearmRest = forearm.localPosition;
                _handRest = hand.localPosition;
                _otherArmScale = otherArm != null ? otherArm.localScale : Vector3.one;
            }
        }

        private void LateUpdate()
        {
            if (upperArm == null || forearm == null || hand == null || socket == null)
            {
                Resolve();
                if (upperArm == null || forearm == null || hand == null || socket == null)
                {
                    return;
                }
            }

            float goal = _active && _tip != null ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, goal, Time.deltaTime / blendSeconds);
            if (_weight <= 0f)
            {
                _reached = false;
                RestoreRoot();
                RestoreLimbs();
                return;
            }

            if (_tip == null)
            {
                // The held thing went away while the arm was easing back: nothing left to aim.
                _weight = 0f;
                RestoreRoot();
                RestoreLimbs();
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, _weight);
            PlaceRoot(eased);
            StretchArm(eased);
            Solve(eased);
        }

        // ---------------------------------------------------------------- the root

        private void PlaceRoot(float eased)
        {
            Vector3 current = armRoot.localPosition;
            if (!_hasBase || (current - _lastSet).sqrMagnitude > 1e-8f)
            {
                // Somebody else (the pickup) moved the arm: that is the new neutral place.
                _base = current;
                _hasBase = true;
            }

            armRoot.localPosition = _base + reachShift * eased;

            // If the target is more than the arm can comfortably reach from here, the whole arm slides toward it.
            Vector3 shoulder = upperArm.position;
            Vector3 wrist = WristFor(Orientation(out _, out _, out _), out float gripToTip, out _);
            float reach = NaturalLength() * comfortableReach * maxStretch;
            Vector3 toTarget = wrist - shoulder;
            float excess = Mathf.Max(0f, toTarget.magnitude - reach);
            Vector3 wanted = excess > 0f ? toTarget.normalized * excess : Vector3.zero;
            _slide = Vector3.Lerp(_slide, wanted, 1f - Mathf.Exp(-slideSpeed * Time.deltaTime));
            Transform parent = armRoot.parent;
            Vector3 local = parent != null ? parent.InverseTransformVector(_slide) : _slide;
            armRoot.localPosition += local * eased;
            _lastSet = armRoot.localPosition;
        }

        /// <summary>The arm's length at rest, in the world.</summary>
        private float NaturalLength()
        {
            float scale = upperArm.lossyScale.y;
            return (_forearmRest.magnitude + _handRest.magnitude) * scale;
        }

        /// <summary>Lengthens the arm (the bones move apart, the hand stays the same size) so it reaches without sliding far.</summary>
        private void StretchArm(float eased)
        {
            Vector3 wrist = WristFor(Orientation(out _, out _, out _), out _, out _);
            float needed = Vector3.Distance(upperArm.position, wrist) / Mathf.Max(0.05f, NaturalLength() * comfortableReach);
            float goal = Mathf.Clamp(Mathf.Max(restStretch, needed), restStretch, maxStretch);
            _stretch = Mathf.Lerp(_stretch, goal, 1f - Mathf.Exp(-slideSpeed * Time.deltaTime));
            float amount = Mathf.Lerp(1f, _stretch, eased);
            forearm.localPosition = _forearmRest * amount;
            hand.localPosition = _handRest * amount;
            if (otherArm != null)
            {
                otherArm.localScale = Vector3.Lerp(_otherArmScale, Vector3.zero, eased);
            }
        }

        private void RestoreLimbs()
        {
            if (!_restCaptured)
            {
                return;
            }

            forearm.localPosition = _forearmRest;
            hand.localPosition = _handRest;
            _stretch = 1f;
            if (otherArm != null)
            {
                otherArm.localScale = _otherArmScale;
            }
        }

        private void RestoreRoot()
        {
            if (!_hasBase)
            {
                return;
            }

            Vector3 current = armRoot.localPosition;
            if ((current - _lastSet).sqrMagnitude <= 1e-8f)
            {
                armRoot.localPosition = _base;
            }

            _hasBase = false;
            _slide = Vector3.zero;
        }

        // ---------------------------------------------------------------- the solve

        /// <summary>The hand's rotation that makes the held thing point where it should, keeping the animated roll.</summary>
        private Quaternion Orientation(out Vector3 axisLocal, out Vector3 socketLocal, out float gripToTip)
        {
            Vector3 gripToTipWorld = _tip.position - socket.position;
            gripToTip = gripToTipWorld.magnitude;
            axisLocal = Quaternion.Inverse(hand.rotation) * (gripToTip > 1e-4f ? gripToTipWorld / gripToTip : hand.up);
            socketLocal = hand.InverseTransformPoint(socket.position);
            Vector3 wanted = -_towardHand;
            return Quaternion.FromToRotation(hand.rotation * axisLocal, wanted) * hand.rotation;
        }

        /// <summary>Where the wrist joint has to be for the tip to be on its target with the hand turned as given.</summary>
        private Vector3 WristFor(Quaternion handRotation, out float gripToTip, out Vector3 gripTarget)
        {
            Orientation(out _, out Vector3 socketLocal, out gripToTip);
            gripTarget = _tipTarget + _towardHand * gripToTip;
            Vector3 socketOffset = handRotation * Vector3.Scale(socketLocal, hand.lossyScale);
            return gripTarget - socketOffset;
        }

        private void Solve(float eased)
        {
            Quaternion animatedUpper = upperArm.rotation;
            Quaternion animatedFore = forearm.rotation;
            Quaternion animatedHand = hand.rotation;

            Quaternion handRotation = Orientation(out _, out _, out _);
            Vector3 wrist = WristFor(handRotation, out _, out _);

            Vector3 root = upperArm.position;
            float a = Vector3.Distance(root, forearm.position);
            float b = Vector3.Distance(forearm.position, hand.position);
            Vector3 toWrist = wrist - root;
            float distance = Mathf.Clamp(toWrist.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 direction = toWrist.normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Transform parent = armRoot.parent;
            Vector3 pole = root + (parent != null ? parent.TransformDirection(elbowPole) : elbowPole);
            Vector3 bend = Vector3.ProjectOnPlane(pole - root, direction);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.down;
            Vector3 elbow = root + direction * along + bend * height;

            upperArm.rotation = Quaternion.FromToRotation(forearm.position - root, elbow - root) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, root + direction * distance - forearm.position) * forearm.rotation;
            Quaternion solvedUpper = upperArm.rotation;
            Quaternion solvedFore = forearm.rotation;

            upperArm.rotation = Quaternion.Slerp(animatedUpper, solvedUpper, eased);
            forearm.rotation = Quaternion.Slerp(animatedFore, solvedFore, eased);
            hand.rotation = Quaternion.Slerp(animatedHand, handRotation, eased);
            _reached = Vector3.Distance(_tip.position, _tipTarget) < 0.01f;
        }
    }
}
