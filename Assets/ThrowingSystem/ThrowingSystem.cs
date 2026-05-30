using UnityEngine;
using UnityEngine.InputSystem;

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

    // Starts charge when input is pressed while holding an object
    void OnThrowInputStarted(InputAction.CallbackContext ctx)
    {
        if (currentHeldObject == null) return;

        isCharging = true;
        chargeTimer = 0f;
        OnThrowChargeStarted?.Invoke();
    }

    // Throws or cancels depending on charge duration when input is released
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

    // Tracks charge time and handles early release via polling
    void Update()
    {
        if (!isCharging) return;

        chargeTimer += Time.deltaTime;

        if (!ThrowInputAction.action.IsPressed())
        {
            if (chargeTimer < minChargeTime)
                CancelCharge();
            else
                PerformThrow();
            return;
        }

        OnThrowChargeProgress?.Invoke(GetChargePercent());
    }

    // Resets charge, fires OnItemThrown, then applies force to the object
    void PerformThrow()
    {
        if (currentHeldObject == null)
        {
            CancelCharge();
            return;
        }

        GameObject thrownObject = currentHeldObject;
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, GetChargePercent());

        isCharging = false;
        chargeTimer = 0f;

        OnItemThrown?.Invoke(thrownObject);
        ApplyThrowForce(thrownObject, force);
    }

    // Resets charge and fires cancel event
    void CancelCharge()
    {
        isCharging = false;
        chargeTimer = 0f;
        OnThrowChargeCanceled?.Invoke();
    }

    // Applies directional impulse force and optional tumble torque
    void ApplyThrowForce(GameObject obj, float force)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;

        Camera mainCamera = Camera.main;
        Vector3 direction;

        if (mainCamera != null)
            direction = mainCamera.transform.forward;
        else
            direction = ThrowOrigin != null ? ThrowOrigin.forward : transform.forward;

        direction = (direction + Vector3.up * upwardForceRatio).normalized;

        rb.AddForce(direction * force, ForceMode.Impulse);

        if (tumbleTorque > 0f)
            rb.AddTorque(Random.insideUnitSphere * tumbleTorque, ForceMode.Impulse);
    }

    // Updates the currently held object reference; cancels charge if object was removed
    public void SetCurrentHeldObject(GameObject obj)
    {
        currentHeldObject = obj;

        if (obj == null && isCharging)
            CancelCharge();
    }

    // Whether a throw is currently being charged
    public bool IsCharging() => isCharging;

    // Returns 0-1 charge percent using an exponential curve
    public float GetChargePercent()
    {
        if (!isCharging) return 0f;
        if (maxChargeTime <= minChargeTime) return 1f;

        float linearProgress = Mathf.Clamp01((chargeTimer - minChargeTime) / (maxChargeTime - minChargeTime));
        float exponentialProgress = 1f - Mathf.Exp(-exponentialRate * linearProgress);

        return Mathf.Clamp01(exponentialProgress);
    }
}
