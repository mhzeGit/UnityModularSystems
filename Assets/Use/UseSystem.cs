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
    public GameObject playerHand; // Object that holds the item (It will be assigned by CentralMediator)
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
        if (playerHand == null || !canUse) return;
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
        if (playerHand == null || !canUse) return;

        currentHeldItem = playerHand.GetComponentInChildren<IUsable>();
        if (currentHeldItem != null)
        {
            foundedUseTargetName = currentUsableTarget?.GetUseTargetName() ?? UseTargetsName.Default;
            currentHeldItem.OnUsed(currentTargetObject, foundedUseTargetName);

            // Start optimized delayed call
            StartCoroutine(DelayedUseOnTarget(currentHeldItem.GetUseImpactDelay()));

            OnUseItem.Invoke(currentHeldItem);
            nextUseTime = Time.time + currentHeldItem.GetUseCooldown();
        }
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
        // Check if the detected object has a usable target component
        IUsableTarget usableTarget = objectFound?.GetComponent<IUsableTarget>();


        // Check if the player is holding a usable item
        currentHeldItem = playerHand != null ? playerHand.GetComponentInChildren<IUsable>() : null;

        toolNameToCheck = currentHeldItem != null ? currentHeldItem.GetToolName() : ToolsName.Hand; // get the name of the tool that player is holding, if player is not holding anyhting then give "Hand" as the name of the tool

        ///Here we check validation if player can use an item on the target.
        ///First it check if the target that player is trying to use on is exist or not?
        ///Then it checks if the target is new or not?
        ///Lastly checks if the target contains tool  (that could be used on it) names, for example the target can only be used truly if player has a hammer in the hand.
        ///This validation does not stop the player from using the tool they have in the hand but rather does 2 things:
        ///1. make sures the prompt to use the item on does not show
        ///2. make sures later on when player performs use, it would avoid trying to send used function to the target


        if (usableTarget != null && usableTarget != currentUsableTarget && usableTarget.GetTargetAcceptedToolNames().Contains(toolNameToCheck) && usableTarget.GetCanUseAtTaget())
        {

            currentUsableTarget = usableTarget;
            currentTargetObject = objectFound;
            OnUsableTargetFound?.Invoke(currentUsableTarget); // Invoking this will create a prompt "Press 'LMB' To Use".
        }
        else
        {
            OnLostObjectPerformed();
        }
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
        // when tried using (aka presssing LMB) check if there is any target to use it on, if there is nothing.. then dont do anyhting and ignore calling the target being used
        if (currentUsableTarget != null &&
            currentTargetObject != null && currentUsableTarget.GetCanUseAtTaget() &&
            currentUsableTarget.GetTargetAcceptedToolNames().Contains(toolNameToCheck))

        {
            currentHeldItem.OnUsedOnTarget(currentTargetObject, foundedUseTargetName);
            currentUsableTarget.Used((currentHeldItem as MonoBehaviour)?.gameObject, toolNameToCheck, lastHitTargetResults);
            OnUseItemAtTarget.Invoke(currentHeldItem, currentUsableTarget);
            
        }
    }
}
