using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.MousePhysicsGrabSystem
{
    public class MousePhysicsGrabSystem : MonoBehaviour
    {
        public static MousePhysicsGrabSystem Instance { get; private set; }

        public bool IsGrabbing => _grabbedBody != null;
        public Rigidbody GrabbedBody => _grabbedBody;
        public Vector3 GrabTargetPosition { get; private set; }

        public event System.Action<Rigidbody> OnGrabbed;
        public event System.Action<Rigidbody> OnReleased;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference grabAction;
        [SerializeField] private InputActionReference scrollAction;

        [Header("Camera")]
        [SerializeField] private Camera grabCamera;

        [Header("Grab Settings")]
        [SerializeField] private float grabReach = 50f;
        [SerializeField] private float springForce = 600f;
        [SerializeField] private float damping = 10f;
        [SerializeField] private LayerMask grabMask = ~0;

        [Header("Drag Override")]
        [Tooltip("Linear drag applied to the Rigidbody while grabbed. -1 means don't override.")]
        [SerializeField] private float overrideLinearDrag = 5f;
        [Tooltip("Angular drag applied to the Rigidbody while grabbed. -1 means don't override.")]
        [SerializeField] private float overrideAngularDrag = 5f;

        [Header("Depth Control")]
        [SerializeField] private float minGrabDepth = 1f;
        [SerializeField] private float maxGrabDepth = 100f;
        [SerializeField] private float depthScrollMultiplier = 0.1f;

        [Header("Velocity Limit")]
        [Tooltip("Maximum speed (m/s) while grabbed. Prevents tunnelling through geometry.")]
        [SerializeField] private float maxGrabSpeed = 20f;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = true;

        private Rigidbody _grabbedBody;
        private float _grabDepth;
        private Vector3 _localHitOffset;
        private float _originalDrag;
        private float _originalAngularDrag;
        private bool _dragOverridden;
        private Camera _cam;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _cam = grabCamera != null ? grabCamera : Camera.main;

            if (_cam == null)
                Debug.LogError("[MousePhysicsGrab] No Camera assigned and Camera.main is null!", this);
        }

        private void OnEnable()
        {
            if (grabAction == null || scrollAction == null) return;

            grabAction.action.Enable();
            scrollAction.action.Enable();

            grabAction.action.started += OnGrabInputStarted;
            grabAction.action.canceled += OnGrabInputCanceled;
        }

        private void OnDisable()
        {
            if (grabAction == null || scrollAction == null) return;

            grabAction.action.started -= OnGrabInputStarted;
            grabAction.action.canceled -= OnGrabInputCanceled;

            grabAction.action.Disable();
            scrollAction.action.Disable();

            ReleaseObject();
        }

        private void OnGrabInputStarted(InputAction.CallbackContext ctx)
        {
            if (_cam == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = _cam.ScreenPointToRay(screenPos);

            if (!Physics.Raycast(ray, out RaycastHit hit, grabReach, grabMask))
                return;

            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null)
                rb = hit.collider.GetComponentInParent<Rigidbody>();

            if (rb == null || rb.isKinematic)
                return;

            GrabRigidbody(rb, hit.point);
        }

        private void OnGrabInputCanceled(InputAction.CallbackContext ctx)
        {
            ReleaseObject();
        }

        private void Update()
        {
            if (_grabbedBody == null) return;

            float scroll = scrollAction.action.ReadValue<Vector2>().y;
            if (scroll != 0f)
            {
                float notches = scroll / 120f;
                float factor = 1f + notches * depthScrollMultiplier;
                _grabDepth = Mathf.Clamp(_grabDepth * factor, minGrabDepth, maxGrabDepth);
            }
        }

        private void FixedUpdate()
        {
            if (_grabbedBody == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldTarget = _cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, _grabDepth));
            GrabTargetPosition = worldTarget;

            Vector3 grabWorldPos = _grabbedBody.transform.TransformPoint(_localHitOffset);
            Vector3 delta = worldTarget - grabWorldPos;

            float mass = _grabbedBody.mass;
            Vector3 force = (delta * springForce - _grabbedBody.linearVelocity * damping) * mass;
            _grabbedBody.AddForceAtPosition(force, grabWorldPos, ForceMode.Force);

            float speedSq = _grabbedBody.linearVelocity.sqrMagnitude;
            if (speedSq > maxGrabSpeed * maxGrabSpeed)
                _grabbedBody.linearVelocity = _grabbedBody.linearVelocity * (maxGrabSpeed / Mathf.Sqrt(speedSq));

            if (showDebugRay)
                Debug.DrawLine(grabWorldPos, worldTarget, Color.yellow);
        }

        private void GrabRigidbody(Rigidbody rb, Vector3 hitPoint)
        {
            _grabbedBody = rb;
            _grabDepth = Vector3.Dot(hitPoint - _cam.transform.position, _cam.transform.forward);
            _localHitOffset = rb.transform.InverseTransformPoint(hitPoint);

            if (overrideLinearDrag >= 0f || overrideAngularDrag >= 0f)
            {
                _dragOverridden = true;
                _originalDrag = rb.linearDamping;
                _originalAngularDrag = rb.angularDamping;

                if (overrideLinearDrag >= 0f)
                    rb.linearDamping = overrideLinearDrag;
                if (overrideAngularDrag >= 0f)
                    rb.angularDamping = overrideAngularDrag;
            }

            OnGrabbed?.Invoke(rb);
        }

        private void ReleaseObject()
        {
            if (_grabbedBody != null)
            {
                if (_dragOverridden)
                {
                    _grabbedBody.linearDamping = _originalDrag;
                    _grabbedBody.angularDamping = _originalAngularDrag;
                }

                OnReleased?.Invoke(_grabbedBody);
            }

            _grabbedBody = null;
            _dragOverridden = false;
        }

        public void GrabAt(RaycastHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || _grabbedBody != null) return;
            GrabRigidbody(rb, hit.point);
        }

        public void ForceRelease()
        {
            ReleaseObject();
        }
    }
}
