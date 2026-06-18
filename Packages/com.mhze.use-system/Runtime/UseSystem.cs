using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.UseSystem
{
    public class UseSystem : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] InputActionReference UseInputAction;

        [Header("Raycast Settings")]
        [SerializeField] float maxUseDistance = 3f;
        [SerializeField] LayerMask usableLayers = -1;

        [Header("References")]
        public UseSystemDefinitions definitions;
        public Transform playerHand;
        [HideInInspector] public IUsableTarget currentUsableTarget;
        [HideInInspector] public IUsable currentHeldItem;
        [HideInInspector] public GameObject currentTargetObject;
        string foundTargetId;
        RaycastHit lastHitTargetResults;
        float nextUseTime = 0f;

        public event Action<IUsable> OnUseItem;
        public event Action<IUsable, IUsableTarget> OnUseItemAtTarget;

        public event Action<IUsableTarget> OnUsableTargetFound;
        public event Action OnUsableTargetLost;
        private bool canUse = true;

        string toolIdToCheck;
        Coroutine delayedUseCoroutine;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Start()
        {
            SetUseEnable(true);
            RefreshHeldItem();
        }

        void RefreshHeldItem()
        {
            currentHeldItem = playerHand != null ? playerHand.GetComponentInChildren<IUsable>() : null;
        }

        [SerializeField] Camera mainCamera;

        private void Update()
        {
            if (playerHand == null) return;
            if (!canUse) return;
            RefreshHeldItem();
            PerformTargetDetection();
        }

        void PerformTargetDetection()
        {
            if (mainCamera == null) return;

            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxUseDistance, usableLayers))
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
            CancelDelayedUse();
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

            RefreshHeldItem();
            if (currentHeldItem == null) return;

            if (!currentHeldItem.GetIsUsable()) return;

            foundTargetId = currentUsableTarget?.GetTargetId() ?? "Default";
            currentHeldItem.OnUsed(currentTargetObject, foundTargetId);

            CancelDelayedUse();
            delayedUseCoroutine = StartCoroutine(DelayedUseOnTarget(currentHeldItem.GetUseImpactDelay()));

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

            toolIdToCheck = currentHeldItem != null ? currentHeldItem.GetToolId() : "Hand";

            if (usableTarget == currentUsableTarget)
            {
                return;
            }

            if (!usableTarget.GetCanUseAtTarget())
            {
                OnLostObjectPerformed();
                return;
            }

            if (!usableTarget.GetAcceptedToolIds().Contains(toolIdToCheck))
            {
                OnLostObjectPerformed();
                return;
            }

            currentUsableTarget = usableTarget;
            currentTargetObject = objectFound;
            OnUsableTargetFound?.Invoke(currentUsableTarget);
        }

        void CancelDelayedUse()
        {
            if (delayedUseCoroutine != null)
            {
                StopCoroutine(delayedUseCoroutine);
                delayedUseCoroutine = null;
            }
        }

        public void OnLostObjectPerformed()
        {
            if (currentUsableTarget != null)
            {
                CancelDelayedUse();
                currentUsableTarget = null;
                currentTargetObject = null;
                OnUsableTargetLost?.Invoke();
            }
        }

        void UseTheToolOnTarget()
        {
            if (currentUsableTarget == null) return;
            if (currentTargetObject == null) return;
            if (!currentUsableTarget.GetCanUseAtTarget()) return;
            if (!currentUsableTarget.GetAcceptedToolIds().Contains(toolIdToCheck)) return;

            currentHeldItem.OnUsedOnTarget(currentTargetObject, foundTargetId);
            currentUsableTarget.Used((currentHeldItem as MonoBehaviour)?.gameObject, toolIdToCheck, lastHitTargetResults);
            OnUseItemAtTarget?.Invoke(currentHeldItem, currentUsableTarget);
        }
    }
}
