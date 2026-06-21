using System;
using System.Collections.Generic;
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
        [HideInInspector] public List<IUsableTarget> currentUsableTargets = new List<IUsableTarget>();
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

            foundTargetId = currentUsableTargets.Count > 0 ? currentUsableTargets[0].GetTargetId() : "Default";
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
            if (objectFound == null)
            {
                OnLostObjectPerformed();
                return;
            }

            IUsableTarget[] allTargets = objectFound.GetComponents<IUsableTarget>();

            if (allTargets.Length == 0)
            {
                OnLostObjectPerformed();
                return;
            }

            toolIdToCheck = currentHeldItem != null ? currentHeldItem.GetToolId() : "Hand";

            List<IUsableTarget> validTargets = new List<IUsableTarget>();
            foreach (IUsableTarget target in allTargets)
            {
                if (!target.GetCanUseAtTarget()) continue;
                if (!target.GetAcceptedToolIds().Contains(toolIdToCheck)) continue;
                validTargets.Add(target);
            }

            if (validTargets.Count == 0)
            {
                OnLostObjectPerformed();
                return;
            }

            if (currentTargetObject == objectFound && TargetsEqual(currentUsableTargets, validTargets))
            {
                return;
            }

            currentUsableTargets.Clear();
            currentUsableTargets.AddRange(validTargets);
            currentTargetObject = objectFound;

            foreach (IUsableTarget target in currentUsableTargets)
            {
                OnUsableTargetFound?.Invoke(target);
            }
        }

        static bool TargetsEqual(List<IUsableTarget> a, List<IUsableTarget> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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
            if (currentUsableTargets.Count > 0)
            {
                CancelDelayedUse();
                currentUsableTargets.Clear();
                currentTargetObject = null;
                OnUsableTargetLost?.Invoke();
            }
        }

        void UseTheToolOnTarget()
        {
            if (currentUsableTargets.Count == 0) return;
            if (currentTargetObject == null) return;

            currentHeldItem.OnUsedOnTarget(currentTargetObject, foundTargetId);

            foreach (IUsableTarget target in currentUsableTargets)
            {
                if (!target.GetCanUseAtTarget()) continue;
                if (!target.GetAcceptedToolIds().Contains(toolIdToCheck)) continue;

                target.Used((currentHeldItem as MonoBehaviour)?.gameObject, toolIdToCheck, lastHitTargetResults);
                OnUseItemAtTarget?.Invoke(currentHeldItem, target);
            }
        }
    }
}
