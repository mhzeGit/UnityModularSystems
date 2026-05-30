// Made By MHZE

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class UseSystem : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] InputActionReference UseInputAction;

    [Header("Raycast Settings")]
    [SerializeField] float maxUseDistance = 3f;
    [SerializeField] LayerMask useableLayers = -1;

    [Header("References")]
    public GameObject playerHand;
    [HideInInspector] public IUsableTarget currentUsableTarget;
    [HideInInspector] public IUsable currentHeldItem;
    [HideInInspector] public GameObject currentTargetObject;
    UseTargetsName foundedUseTargetName;
    RaycastHit lastHitTargetResults;
    float nextUseTime = 0f;

    Camera mainCamera;

    public event Action<IUsable> OnUseItem;
    public event Action<IUsable, IUsableTarget> OnUseItemAtTarget;

    public event Action<IUsableTarget> OnUsableTargetFound;
    public event Action OnUsableTargetLost;
    private bool canUse = true;

    ToolsName toolNameToCheck;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        SetUseEnable(true);
    }

    private void Update()
    {
        if (playerHand == null) return;
        if (!canUse) return;
        PerformTargetDetection();
    }

    void PerformTargetDetection()
    {
        if (mainCamera == null) return;

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxUseDistance, useableLayers))
        {
            OnDetectedObjectPerformed(hit);
        }
        else
        {
            OnLostObjectPerformed();
        }
    }
    #region Input Setup
    void OnEnable()
    {
        UseInputAction.action.performed += UseInputPerformed;
        UseInputAction.action.Enable();
    }

    void OnDisable()
    {
        UseInputAction.action.performed -= UseInputPerformed;
        UseInputAction.action.Disable();
    }

    void UseInputPerformed(InputAction.CallbackContext context)
    {
        if (Time.time >= nextUseTime)
            PerformUse();
    }
    #endregion

    void PerformUse()
    {
        if (playerHand == null) return;
        if (!canUse) return;

        currentHeldItem = playerHand.GetComponentInChildren<IUsable>();
        if (currentHeldItem == null) return;

        if (!currentHeldItem.GetIsUsable()) return;

        foundedUseTargetName = currentUsableTarget?.GetUseTargetName() ?? UseTargetsName.Default;
        currentHeldItem.OnUsed(currentTargetObject, foundedUseTargetName);

        StartCoroutine(DelayedUseOnTarget(currentHeldItem.GetUseImpactDelay()));

        OnUseItem?.Invoke(currentHeldItem);
        nextUseTime = Time.time + currentHeldItem.GetUseCooldown();
    }

    private System.Collections.IEnumerator DelayedUseOnTarget(float delay)
    {
        yield return new WaitForSeconds(delay);
        UseTheToolOnTarget();
    }
    public void SetUseEnable(bool enable)
    {
        canUse = enable;
    }

    public void OnDetectedObjectPerformed(RaycastHit hitResult)
    {
        GameObject ObjectFound = hitResult.collider.gameObject;
        lastHitTargetResults = hitResult;
        CheckTargetObjectCanBeUsedOn(ObjectFound);

    }
    public void CheckTargetObjectCanBeUsedOn(GameObject objectFound)
    {
        IUsableTarget usableTarget = objectFound?.GetComponent<IUsableTarget>();

        if (usableTarget == null)
        {
            OnLostObjectPerformed();
            return;
        }

        currentHeldItem = playerHand != null ? playerHand.GetComponentInChildren<IUsable>() : null;

        toolNameToCheck = currentHeldItem != null ? currentHeldItem.GetToolName() : ToolsName.Hand;

        if (usableTarget == currentUsableTarget)
        {
            return;
        }

        if (!usableTarget.GetCanUseAtTaget())
        {
            OnLostObjectPerformed();
            return;
        }

        if (!usableTarget.GetTargetAcceptedToolNames().Contains(toolNameToCheck))
        {
            OnLostObjectPerformed();
            return;
        }

        currentUsableTarget = usableTarget;
        currentTargetObject = objectFound;
        OnUsableTargetFound?.Invoke(currentUsableTarget);
    }

    public void OnLostObjectPerformed()
    {
        if (currentUsableTarget != null)
        {
            currentUsableTarget = null;
            currentTargetObject = null;
            OnUsableTargetLost?.Invoke();
        }
    }

    void UseTheToolOnTarget()
    {
        if (currentUsableTarget == null) return;
        if (currentTargetObject == null) return;
        if (!currentUsableTarget.GetCanUseAtTaget()) return;
        if (!currentUsableTarget.GetTargetAcceptedToolNames().Contains(toolNameToCheck)) return;

        currentHeldItem.OnUsedOnTarget(currentTargetObject, foundedUseTargetName);
        currentUsableTarget.Used((currentHeldItem as MonoBehaviour)?.gameObject, toolNameToCheck, lastHitTargetResults);
        OnUseItemAtTarget?.Invoke(currentHeldItem, currentUsableTarget);
    }
}
