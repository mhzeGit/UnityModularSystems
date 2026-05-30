using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.ThrowSystem
{
    public class ThrowingSystem : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] InputActionReference ThrowInputAction;

        [Header("Charge Settings")]
        [SerializeField] float minChargeTime = 0.15f;
        [SerializeField] float maxChargeTime = 1.5f;
        [Range(1f, 10f)]
        [SerializeField] float exponentialRate = 3f;

        [Header("Force Settings")]
        [SerializeField] float minThrowForce = 4f;
        [SerializeField] float maxThrowForce = 18f;
        [Range(0f, 1f)]
        [SerializeField] float upwardForceRatio = 0.25f;
        [SerializeField] float tumbleTorque = 5f;

        [HideInInspector] public Transform ThrowOrigin;

        public event System.Action OnThrowChargeStarted;
        public event System.Action OnThrowChargeCanceled;
        public event System.Action<float> OnThrowChargeProgress;
        public event System.Action<GameObject> OnItemThrown;

        GameObject currentHeldObject;
        bool isCharging;
        float chargeTimer;
        float currentChargePercent;
        Camera mainCamera;

        void Awake()
        {
            mainCamera = Camera.main;
        }

        void Start()
        {
            enabled = false;
        }

        void OnEnable()
        {
            ThrowInputAction.action.started += OnThrowInputStarted;
            ThrowInputAction.action.canceled += OnThrowInputCanceled;
            ThrowInputAction.action.Enable();
        }

        void OnDisable()
        {
            ThrowInputAction.action.started -= OnThrowInputStarted;
            ThrowInputAction.action.canceled -= OnThrowInputCanceled;
            ThrowInputAction.action.Disable();
        }

        void OnThrowInputStarted(InputAction.CallbackContext ctx)
        {
            if (currentHeldObject == null) return;

            isCharging = true;
            chargeTimer = 0f;
            enabled = true;
            OnThrowChargeStarted?.Invoke();
        }

        void OnThrowInputCanceled(InputAction.CallbackContext ctx)
        {
            if (!isCharging) return;

            if (chargeTimer < minChargeTime)
            {
                CancelCharge();
                return;
            }

            PerformThrow();
        }

        void Update()
        {
            chargeTimer += Time.deltaTime;
            currentChargePercent = GetChargePercent();
            OnThrowChargeProgress?.Invoke(currentChargePercent);
        }

        void PerformThrow()
        {
            if (currentHeldObject == null)
            {
                CancelCharge();
                return;
            }

            GameObject thrownObject = currentHeldObject;

            isCharging = false;
            chargeTimer = 0f;
            enabled = false;

            float force = Mathf.Lerp(minThrowForce, maxThrowForce, currentChargePercent);
            OnItemThrown?.Invoke(thrownObject);
            ApplyThrowForce(thrownObject, force);
        }

        void CancelCharge()
        {
            isCharging = false;
            chargeTimer = 0f;
            enabled = false;
            OnThrowChargeCanceled?.Invoke();
        }

        void ApplyThrowForce(GameObject obj, float force)
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 direction;

            if (mainCamera != null)
                direction = mainCamera.transform.forward;
            else if (ThrowOrigin != null)
                direction = ThrowOrigin.forward;
            else
                direction = transform.forward;

            direction = (direction + Vector3.up * upwardForceRatio).normalized;

            rb.AddForce(direction * force, ForceMode.Impulse);

            if (tumbleTorque > 0f)
                rb.AddTorque(Random.insideUnitSphere * tumbleTorque, ForceMode.Impulse);
        }

        public void SetCurrentHeldObject(GameObject obj)
        {
            currentHeldObject = obj;

            if (obj == null && isCharging)
                CancelCharge();
        }

        public bool IsCharging() => isCharging;

        public float GetChargePercent()
        {
            if (!isCharging) return 0f;
            if (maxChargeTime <= minChargeTime) return 1f;

            float linearProgress = Mathf.Clamp01((chargeTimer - minChargeTime) / (maxChargeTime - minChargeTime));
            float exponentialProgress = 1f - Mathf.Exp(-exponentialRate * linearProgress);

            return Mathf.Clamp01(exponentialProgress);
        }
    }
}
