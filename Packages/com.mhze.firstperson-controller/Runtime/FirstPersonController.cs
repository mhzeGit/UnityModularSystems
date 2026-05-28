using System;
using UnityEngine;

namespace MHZE.FirstPersonController
{
    public enum FPCPhysicsMode
    {
        CharacterController,
        Rigidbody
    }

    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private FPCPhysicsMode physicsMode = FPCPhysicsMode.CharacterController;

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private CapsuleCollider playerCapsule;
        [SerializeField] private Camera playerCamera;
        [Tooltip("Optional. Uses camera parent if null.")]
        [SerializeField] private Transform cameraPivot;

        [Header("Settings")]
        [SerializeField] private FPCSettings settings;

        [Header("Input Actions")]
        [SerializeField] private UnityEngine.InputSystem.InputActionReference moveAction;
        [SerializeField] private UnityEngine.InputSystem.InputActionReference lookAction;
        [SerializeField] private UnityEngine.InputSystem.InputActionReference jumpAction;
        [SerializeField] private UnityEngine.InputSystem.InputActionReference crouchAction;
        [SerializeField] private UnityEngine.InputSystem.InputActionReference sprintAction;

        // Sub-components
        private IFPCMovementDriver driver;
        private FPCInput input;
        private FPCMovement movement;
        private FPCLook look;
        private FPCForceLook forceLook;
        private FPCForceMove forceMove;
        private FPCHeadbob headbob;

        // State
        private bool controlsEnabled = true;
        private FPCState currentState;
        private Transform cachedCameraPivot;
        private float standingCameraHeight;
        private float currentFov;
        private Vector3 lastPosition;           // for actual velocity measurement
        private float actualHorizontalSpeed;    // post-collision |v.xz|
        private UnityEngine.InputSystem.Keyboard keyboard;
        private UnityEngine.InputSystem.Mouse mouse;

        // --- Context menu ----------------------------------------

        [ContextMenu("Switch to Character Controller Mode")]
        private void SwitchToCharacterControllerMode()
        {
            physicsMode = FPCPhysicsMode.CharacterController;
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FPC] Mode change will take effect after scene reload.", this);
                return;
            }
            MigrateComponents();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Switch to Rigidbody (Physics) Mode")]
        private void SwitchToRigidbodyMode()
        {
            physicsMode = FPCPhysicsMode.Rigidbody;
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FPC] Mode change will take effect after scene reload.", this);
                return;
            }
            MigrateComponents();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        private void MigrateComponents()
        {
            if (physicsMode == FPCPhysicsMode.CharacterController)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) DestroyImmediate(rb);
                var cap = GetComponent<CapsuleCollider>();
                if (cap != null) DestroyImmediate(cap);

                var cc = GetComponent<CharacterController>();
                if (cc == null) cc = gameObject.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.radius = 0.3f;
                cc.stepOffset = 0.2f;
                cc.slopeLimit = 45f;

                characterController = cc;
                playerRigidbody = null;
                playerCapsule = null;
            }
            else
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) DestroyImmediate(cc);

                var rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    rb.mass = 75f;
                    rb.useGravity = false;
                    rb.freezeRotation = true;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                var cap = GetComponent<CapsuleCollider>();
                if (cap == null)
                {
                    cap = gameObject.AddComponent<CapsuleCollider>();
                    cap.height = 1.8f;
                    cap.center = new Vector3(0f, 0.9f, 0f);
                    cap.radius = 0.3f;
                }

                characterController = null;
                playerRigidbody = rb;
                playerCapsule = cap;
            }

            Transform pivot = transform.Find("CameraPivot");
            if (pivot != null)
                pivot.localPosition = new Vector3(0f, 1.65f, 0f);
        }
#endif

        // --- Public API ------------------------------------------

        public FPCPhysicsMode PhysicsMode => physicsMode;
        public FPCState CurrentState => currentState;
        public bool IsGrounded => movement?.IsGrounded ?? false;
        public bool IsCrouching => movement?.IsCrouching ?? false;
        public Camera PlayerCamera => playerCamera;
        public bool ControlsEnabled => controlsEnabled;
        public Vector3 Velocity => movement?.Velocity ?? Vector3.zero;

        public event Action<FPCState> OnStateChanged;
        public event Action OnCrouchStarted;
        public event Action OnCrouchEnded;
        public event Action OnJumped;
        public event Action OnGrounded;
        public event Action OnAirborne;
        public event Action<Vector3> OnForceMoveArrived;

        // --- Force Look API --------------------------------------

        public void ForceLookAt(Vector3 worldPoint, float? duration = null, float? speed = null)
            => forceLook?.LookAt(worldPoint, duration, speed);

        public void ForceLookAt(Transform target, float? duration = null, float? speed = null)
            => forceLook?.LookAt(target, duration, speed);

        public void StopForceLook(bool snapBack = false)
            => forceLook?.Release(snapBack);

        public bool IsForceLooking => forceLook != null && forceLook.IsActive;

        // --- Force Move API --------------------------------------

        public void ForceMoveTo(Vector3 position, float? speed = null, Action onArrived = null)
        {
            if (forceMove == null) return;
            Action wrapped = () =>
            {
                OnForceMoveArrived?.Invoke(position);
                onArrived?.Invoke();
            };
            forceMove.MoveTo(position, speed, wrapped);
        }

        public void StopForceMove() => forceMove?.Stop();
        public bool IsForceMoving => forceMove != null && forceMove.IsActive;

        // --- Direct Control API ----------------------------------

        public void SetPosition(Vector3 position)
        {
            movement?.Teleport(position, transform.rotation);
            look?.SyncWithTransform();
            headbob?.Snap();
            lastPosition = transform.position; // avoid velocity spike after teleport
        }

        public void SetRotation(Quaternion rotation)
        {
            movement?.Teleport(transform.position, rotation);
            look?.SyncWithTransform();
            headbob?.Snap();
        }

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            movement?.Teleport(position, rotation);
            look?.SyncWithTransform();
            headbob?.Snap();
            lastPosition = transform.position;
        }

        public void EnableControls()
        {
            controlsEnabled = true;
            input?.Enable();
            LockCursor();
        }

        public void DisableControls()
        {
            controlsEnabled = false;
            input?.Disable();
            UnlockCursor();
            StopForceMove();
            headbob?.Snap();
        }

        // --- Cursor management ----------------------------------

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // --- Unity Lifecycle -------------------------------------

        private void Awake()
        {
            keyboard = UnityEngine.InputSystem.Keyboard.current;
            mouse = UnityEngine.InputSystem.Mouse.current;

            ResolveReferences();
            CreateDriver();
            InitialiseSubComponents();

            if (cachedCameraPivot != null)
                standingCameraHeight = cachedCameraPivot.localPosition.y;

            if (playerCamera != null)
                currentFov = playerCamera.fieldOfView;

            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (input != null && controlsEnabled)
                input.Enable();
        }

        private void OnDisable()
        {
            input?.Disable();
            UnlockCursor();
        }

        private void Start()
        {
            if (controlsEnabled)
                LockCursor();
            UpdateState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && controlsEnabled)
                LockCursor();
            else
                UnlockCursor();
        }

        private void Update()
        {
            if (!controlsEnabled || settings == null || movement == null) return;

            // --- Cursor management -------------------------------
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                UnlockCursor();
            else if (Cursor.lockState == CursorLockMode.None && mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame)
                    LockCursor();
            }

            input.Poll();
            movement.Update(input, Time.deltaTime);

            forceMove.Update(Time.deltaTime);
            look.Update(input, Time.deltaTime);
            forceLook.Update(Time.deltaTime);

            // --- Actual velocity from position delta -------------
            // This captures the TRUE movement after collision resolution.
            // Holding W against a wall ? actualHorizontalSpeed ˜ 0.
            Vector3 displacement = transform.position - lastPosition;
            lastPosition = transform.position;

            float dt = Time.deltaTime;
            actualHorizontalSpeed = dt > 0.0001f
                ? new Vector3(displacement.x, 0f, displacement.z).magnitude / dt
                : 0f;

            ApplyCameraCrouchOffset();
            ApplyFovSpeedEffect();

            if (headbob != null)
                headbob.Update(actualHorizontalSpeed, movement.IsGrounded, Time.deltaTime);

            UpdateState();
            input.ConsumeFrame();

            if (settings.debugLogging)
            {
                Debug.Log(
                    $"[FPC] G={currentState.GroundState} P={currentState.Posture} " +
                    $"Mv={currentState.IsMoving} Rn={currentState.IsRunning} " +
                    $"FL={currentState.InForceLook} FM={currentState.InForceMove}",
                    this);
            }
        }

        // --- Internal --------------------------------------------

        private void ResolveReferences()
        {
            if (physicsMode == FPCPhysicsMode.CharacterController)
            {
                if (characterController == null)
                    characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    Debug.LogError("[FPC] CharacterController mode but no CharacterController found.", this);
                    enabled = false;
                    return;
                }
            }
            else
            {
                if (playerRigidbody == null)
                    playerRigidbody = GetComponent<Rigidbody>();
                if (playerCapsule == null)
                    playerCapsule = GetComponent<CapsuleCollider>();
                if (playerRigidbody == null || playerCapsule == null)
                {
                    Debug.LogError("[FPC] Physics mode requires a Rigidbody and CapsuleCollider.", this);
                    enabled = false;
                    return;
                }
            }

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();

            if (cameraPivot != null)
                cachedCameraPivot = cameraPivot;
            else if (playerCamera != null)
                cachedCameraPivot = playerCamera.transform.parent != null
                                    && playerCamera.transform.parent != transform
                    ? playerCamera.transform.parent
                    : playerCamera.transform;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FPCSettings>();
                Debug.LogWarning("[FPC] No settings asset assigned; using defaults.", this);
            }
        }

        private void CreateDriver()
        {
            if (physicsMode == FPCPhysicsMode.CharacterController)
            {
                if (characterController == null) return;
                driver = new CharacterDriver(characterController);
            }
            else
            {
                if (playerRigidbody == null || playerCapsule == null) return;
                driver = new PhysicsDriver(playerRigidbody, playerCapsule);
                playerRigidbody.isKinematic = false;
                playerRigidbody.useGravity = false;
            }
        }

        private void InitialiseSubComponents()
        {
            if (driver == null || playerCamera == null)
            {
                Debug.LogError("[FPC] Driver or Camera missing. Disabling.", this);
                enabled = false;
                return;
            }

            input = new FPCInput
            {
                moveAction = moveAction,
                lookAction = lookAction,
                jumpAction = jumpAction,
                crouchAction = crouchAction,
                sprintAction = sprintAction
            };

            movement = new FPCMovement(driver, settings);
            look = new FPCLook(driver.Transform, cachedCameraPivot, settings);
            forceLook = new FPCForceLook(look, settings);
            forceMove = new FPCForceMove(movement, settings);
            headbob = new FPCHeadbob(playerCamera.transform, settings.headbobSettings);

            movement.OnCrouchStarted += () => OnCrouchStarted?.Invoke();
            movement.OnCrouchEnded   += () => OnCrouchEnded?.Invoke();
            movement.OnJumped        += () => OnJumped?.Invoke();
            movement.OnGrounded      += () => OnGrounded?.Invoke();
            movement.OnAirborne      += () => OnAirborne?.Invoke();
        }

        // --- Camera crouch offset -------------------------------

        private void ApplyCameraCrouchOffset()
        {
            if (cachedCameraPivot == null) return;

            float norm = movement.GetCrouchNormalized();
            float crouchCamHeight = standingCameraHeight
                * (settings.crouchHeight / settings.standHeight);
            float targetY = Mathf.Lerp(standingCameraHeight, crouchCamHeight, norm);

            Vector3 pos = cachedCameraPivot.localPosition;
            pos.y = targetY;
            cachedCameraPivot.localPosition = pos;
        }

        // --- FOV speed effect (actual-velocity-based) ------------

        private void ApplyFovSpeedEffect()
        {
            if (playerCamera == null) return;
            if (!settings.enableFovSpeedEffect) return;

            // actualHorizontalSpeed is computed from position delta,
            // so it goes to ~0 when the player is blocked by geometry.
            float t = Mathf.Clamp01(actualHorizontalSpeed / settings.fovSpeedThreshold);
            float targetFov = Mathf.Lerp(settings.fovBaseValue, settings.fovMaxValue, t);

            float speed = targetFov >= currentFov
                ? settings.fovIncreaseSpeed
                : settings.fovDecreaseSpeed;

            currentFov = Mathf.Lerp(currentFov, targetFov, speed * Time.deltaTime);
            playerCamera.fieldOfView = currentFov;
        }

        // --- State -----------------------------------------------

        private void UpdateState()
        {
            if (movement == null) return;
            FPCState s = movement.GetState();
            s.InForceLook = IsForceLooking;
            s.InForceMove = IsForceMoving;

            if (!StateEquals(currentState, s))
            {
                currentState = s;
                OnStateChanged?.Invoke(currentState);
            }
        }

        private static bool StateEquals(FPCState a, FPCState b)
            => a.GroundState == b.GroundState
            && a.Posture == b.Posture
            && a.IsMoving == b.IsMoving
            && a.IsRunning == b.IsRunning
            && a.InForceLook == b.InForceLook
            && a.InForceMove == b.InForceMove;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
            Vector3 checkPos = transform.position + Vector3.down * (1.8f * 0.5f - 0.05f);
            Gizmos.DrawWireSphere(checkPos, 0.1f);
        }
    }
}