using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// Reads input actions and exposes clean polling values and edge-detect events.
    /// </summary>
    public class FPCInput
    {
        // Action references (assigned by the controller)
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference jumpAction;
        public InputActionReference crouchAction;
        public InputActionReference sprintAction;

        // --- Polled values ---------------------------------------

        /// <summary>WASD / left-stick value this frame.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Mouse / right-stick value this frame.</summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>True on the frame the jump button was pressed.</summary>
        public bool JumpPressed { get; private set; }

        /// <summary>True while the jump button is held.</summary>
        public bool JumpHeld { get; private set; }

        /// <summary>True on the frame the crouch button was pressed.</summary>
        public bool CrouchPressed { get; private set; }

        /// <summary>True while the crouch button is held.</summary>
        public bool CrouchHeld { get; private set; }

        /// <summary>True while the sprint button is held.</summary>
        public bool SprintHeld { get; private set; }

        /// <summary>Jump was released this frame.</summary>
        public bool JumpReleased { get; private set; }

        /// <summary>Crouch was released this frame.</summary>
        public bool CrouchReleased { get; private set; }

        // --- Events ----------------------------------------------

        public event Action OnJumpPressed;
        public event Action OnCrouchPressed;
        public event Action OnCrouchReleased;

        // --- Lifecycle -------------------------------------------

        public void Enable()
        {
            if (moveAction != null) moveAction.action.Enable();
            if (lookAction != null) lookAction.action.Enable();

            if (jumpAction != null)
            {
                jumpAction.action.Enable();
                jumpAction.action.started += OnJumpStarted;
                jumpAction.action.canceled += OnJumpCanceled;
            }

            if (crouchAction != null)
            {
                crouchAction.action.Enable();
                crouchAction.action.started += OnCrouchStarted;
                crouchAction.action.canceled += OnCrouchCanceled;
            }

            if (sprintAction != null) sprintAction.action.Enable();
        }

        public void Disable()
        {
            if (moveAction != null) moveAction.action.Disable();
            if (lookAction != null) lookAction.action.Disable();

            if (jumpAction != null)
            {
                jumpAction.action.started -= OnJumpStarted;
                jumpAction.action.canceled -= OnJumpCanceled;
                jumpAction.action.Disable();
            }

            if (crouchAction != null)
            {
                crouchAction.action.started -= OnCrouchStarted;
                crouchAction.action.canceled -= OnCrouchCanceled;
                crouchAction.action.Disable();
            }

            if (sprintAction != null) sprintAction.action.Disable();

            ResetState();
        }

        /// <summary>
        /// Poll continuous inputs.  Call once per frame at the start of the controller update.
        /// </summary>
        public void Poll()
        {
            if (moveAction != null)
                MoveInput = moveAction.action.ReadValue<Vector2>();

            if (lookAction != null)
                LookInput = lookAction.action.ReadValue<Vector2>();

            if (sprintAction != null)
                SprintHeld = sprintAction.action.IsPressed();

            // Edge-detect jump using the input system's built-in helpers
            if (jumpAction != null)
            {
                if (jumpAction.action.WasPressedThisFrame())
                {
                    JumpPressed = true;
                    OnJumpPressed?.Invoke();
                }
                JumpHeld = jumpAction.action.IsPressed();
                JumpReleased = jumpAction.action.WasReleasedThisFrame();
            }

            // Edge-detect crouch: the callbacks set CrouchHeld;
            // we still need WasPressedThisFrame for the one-shot edge.
            if (crouchAction != null)
            {
                if (crouchAction.action.WasPressedThisFrame())
                {
                    CrouchPressed = true;
                    OnCrouchPressed?.Invoke();
                }
                CrouchReleased = crouchAction.action.WasReleasedThisFrame();
            }
        }

        /// <summary>
        /// Call at the END of the frame (after all components have read one-shot values).
        /// Clears edge-detect flags so they don't persist into the next frame.
        /// </summary>
        public void ConsumeFrame()
        {
            JumpPressed = false;
            CrouchPressed = false;
            JumpReleased = false;
            CrouchReleased = false;
        }

        // --- Callbacks ------------------------------------------

        private void OnJumpStarted(InputAction.CallbackContext context)
        {
            // Handled via WasPressedThisFrame in Poll() now
        }

        private void OnJumpCanceled(InputAction.CallbackContext context)
        {
            // Handled via WasReleasedThisFrame in Poll() now
        }

        private void OnCrouchStarted(InputAction.CallbackContext context)
        {
            CrouchHeld = true;
        }

        private void OnCrouchCanceled(InputAction.CallbackContext context)
        {
            CrouchHeld = false;
        }

        private void ResetState()
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            JumpPressed = false;
            JumpHeld = false;
            CrouchPressed = false;
            CrouchHeld = false;
            SprintHeld = false;
            JumpReleased = false;
            CrouchReleased = false;
        }
    }
}