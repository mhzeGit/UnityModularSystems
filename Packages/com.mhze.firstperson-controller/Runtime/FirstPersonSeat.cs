using System;
using System.Collections;
using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// A seated posture for the first-person controller: the player is moved onto a seat, cannot
    /// walk, run, jump or crouch, can still look around (and use whatever the interaction systems
    /// offer), and stands up again on request. The eye drops to a seated height by offsetting the
    /// camera, so the controller itself, its collider and its settings are not touched.
    ///
    /// Put it on the player object, or call <see cref="For"/> and let it add itself. A seat is any
    /// transform: its position is the top of the seat (where the player's feet would rest if they
    /// were standing on it - give the seat a collider so the capsule is held there) and its forward
    /// is the way the player faces when they sit down.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("MHZE/First Person/First Person Seat")]
    public sealed class FirstPersonSeat : MonoBehaviour
    {
        [Header("Who")]
        [Tooltip("The controller to seat. Empty: the one on this object.")]
        [SerializeField] private FirstPersonController controller;

        [Header("Posture")]
        [Tooltip("Height of the eyes above the top of the seat, in metres.")]
        [Min(0.3f)]
        [SerializeField] private float eyeHeightAboveSeat = 0.72f;

        [Tooltip("Height of the eyes above the player's feet when standing, in metres. Used to work out how far the camera drops.")]
        [Min(0.5f)]
        [SerializeField] private float standingEyeHeight = 1.65f;

        [Header("Timing")]
        [Min(0.05f)]
        [SerializeField] private float sitSeconds = 0.7f;

        [Min(0.05f)]
        [SerializeField] private float standSeconds = 0.55f;

        private Coroutine _routine;
        private Transform _seat;
        private Vector3 _appliedLocalOffset;
        private float _drop;
        private float _targetDrop;

        /// <summary>The player has finished sitting down.</summary>
        public event Action Seated;

        /// <summary>The player has finished standing up.</summary>
        public event Action StoodUp;

        /// <summary>The player is sitting, or on the way down. False once they start to stand.</summary>
        public bool IsSeated { get; private set; }

        /// <summary>The player is moving to or from the seat.</summary>
        public bool IsTransitioning => _routine != null;

        /// <summary>The seat the player is on (or heading for).</summary>
        public Transform Seat => IsSeated ? _seat : null;

        /// <summary>The seat component of this controller, added to its object when it has none.</summary>
        public static FirstPersonSeat For(FirstPersonController target)
        {
            if (target == null)
            {
                return null;
            }

            FirstPersonSeat seat = target.GetComponent<FirstPersonSeat>();
            if (seat == null)
            {
                seat = target.gameObject.AddComponent<FirstPersonSeat>();
            }

            seat.Resolve();
            return seat;
        }

        // ---------------------------------------------------------------- control

        /// <summary>
        /// Sits the player down on a seat. Walking, running, jumping and crouching are switched off
        /// until <see cref="StandUp"/>; looking stays on.
        /// </summary>
        public void SitDown(Transform seat)
        {
            if (seat == null || !Resolve())
            {
                return;
            }

            StopRoutine();
            _seat = seat;
            IsSeated = true;
            controller.StopForceMove();
            SetWalking(false);
            _routine = StartCoroutine(SitRoutine(seat));
        }

        /// <summary>
        /// Stands the player up and puts their feet on <paramref name="exit"/> (position and facing).
        /// Without an exit they stand where they sit.
        /// </summary>
        public void StandUp(Transform exit = null)
        {
            if (!IsSeated || !Resolve())
            {
                return;
            }

            StopRoutine();
            IsSeated = false;
            _routine = StartCoroutine(StandRoutine(exit));
        }

        /// <summary>Ends the posture at once, wherever the player is: for resets and rewinds.</summary>
        public void Cancel(Transform exit = null)
        {
            StopRoutine();
            bool wasSeated = IsSeated;
            IsSeated = false;
            _seat = null;
            _drop = _targetDrop = 0f;
            if (Resolve())
            {
                SetWalking(true);
                if (exit != null)
                {
                    controller.SetPositionAndRotation(exit.position, exit.rotation);
                }
            }

            if (wasSeated)
            {
                StoodUp?.Invoke();
            }
        }

        // ---------------------------------------------------------------- routines

        private IEnumerator SitRoutine(Transform seat)
        {
            Vector3 from = controller.transform.position;
            float yaw = seat.eulerAngles.y;
            Quaternion fromRotation = controller.transform.rotation;
            Quaternion toRotation = Quaternion.Euler(0f, yaw, 0f);
            _targetDrop = Mathf.Max(0f, standingEyeHeight - eyeHeightAboveSeat);

            for (float t = 0f; t < sitSeconds; t += Time.deltaTime)
            {
                float k = Smooth(t / sitSeconds);
                Vector3 position = Vector3.Lerp(from, seat.position, k);
                // A small hop so the body rises over the edge of the seat instead of through it.
                position.y += Mathf.Sin(k * Mathf.PI) * 0.12f;
                controller.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, toRotation, k));
                _drop = _targetDrop * k;
                yield return null;
            }

            _drop = _targetDrop;
            controller.SetPositionAndRotation(seat.position, toRotation);
            _routine = null;
            Seated?.Invoke();
        }

        private IEnumerator StandRoutine(Transform exit)
        {
            float startDrop = _drop;
            Vector3 from = controller.transform.position;
            Vector3 to = exit != null ? exit.position : from;
            for (float t = 0f; t < standSeconds; t += Time.deltaTime)
            {
                float k = Smooth(t / standSeconds);
                Vector3 position = Vector3.Lerp(from, to, k);
                position.y += Mathf.Sin(k * Mathf.PI) * 0.08f;
                controller.SetPositionAndRotation(position, controller.transform.rotation);
                _drop = Mathf.Lerp(startDrop, 0f, k);
                yield return null;
            }

            _drop = 0f;
            _targetDrop = 0f;
            if (exit != null)
            {
                controller.SetPositionAndRotation(exit.position, controller.transform.rotation);
            }

            SetWalking(true);
            _seat = null;
            _routine = null;
            StoodUp?.Invoke();
        }

        // ---------------------------------------------------------------- camera

        // The controller rewrites the camera's position every frame (head bob, landing dip), so the
        // drop is taken off first thing in the frame and put back last thing: whatever the
        // controller does in between sees an untouched camera.
        private void Update()
        {
            RemoveOffset();
        }

        private void LateUpdate()
        {
            ApplyOffset();
        }

        private void OnDisable()
        {
            RemoveOffset();
        }

        private void ApplyOffset()
        {
            Camera camera = controller != null ? controller.PlayerCamera : null;
            if (camera == null || _drop < 0.0001f)
            {
                return;
            }

            Transform parent = camera.transform.parent;
            Vector3 world = Vector3.down * _drop;
            _appliedLocalOffset = parent != null ? parent.InverseTransformVector(world) : world;
            camera.transform.localPosition += _appliedLocalOffset;
        }

        private void RemoveOffset()
        {
            if (_appliedLocalOffset == Vector3.zero)
            {
                return;
            }

            Camera camera = controller != null ? controller.PlayerCamera : null;
            if (camera != null)
            {
                camera.transform.localPosition -= _appliedLocalOffset;
            }

            _appliedLocalOffset = Vector3.zero;
        }

        // ---------------------------------------------------------------- helpers

        private bool Resolve()
        {
            if (controller == null)
            {
                controller = GetComponent<FirstPersonController>();
            }

            return controller != null;
        }

        private void SetWalking(bool allowed)
        {
            controller.EnableMove(allowed);
            controller.EnableRun(allowed);
            controller.EnableJump(allowed);
            controller.EnableCrouch(allowed);
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
