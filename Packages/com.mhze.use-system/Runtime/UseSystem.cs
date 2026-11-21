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
        [Tooltip("A wall or other solid collider between the camera and the target stops the use. Turn off only for a game where you can reach through things.")]
        [SerializeField] bool blockedByObstacles = true;
        [Tooltip("Layers that can stand in the way when Blocked By Obstacles is on. Triggers and the player's own colliders (including the held tool) never block. Ignore Raycast is off by default.")]
        [SerializeField] LayerMask obstacleLayers = ~(1 << 2);
        readonly RaycastHit[] obstacleBuffer = new RaycastHit[32];

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

            if (Physics.Raycast(ray, out RaycastHit hit, maxUseDistance, usableLayers) && !IsObstructed(ray, hit))
            {
                OnDetectedObjectPerformed(hit);
            }
            else
            {
                OnLostObjectPerformed();
            }
        }
        /// <summary>True when a solid, non-trigger collider that is not the target, not part of it and not the player's own lies closer along the ray than the hit.</summary>
        bool IsObstructed(Ray ray, RaycastHit hit)
        {
            if (!blockedByObstacles) return false;
            int count = Physics.RaycastNonAlloc(ray, obstacleBuffer, hit.distance, obstacleLayers, QueryTriggerInteraction.Ignore);
            Transform self = mainCamera.transform.root;
            Transform target = hit.collider.transform;
            for (int i = 0; i < count; i++)
            {
                Transform t = obstacleBuffer[i].collider.transform;
                if (t.IsChildOf(self) || t.IsChildOf(target) || target.IsChildOf(t)) continue;
                if (obstacleBuffer[i].distance < hit.distance - 0.001f) return true;
            }
            return false;
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

            // Switching directly to a different usable target still needs a paired Lost/Found so listeners
            // (like prompt UI) that key state off those events don't end up with stale state from the old target.
            if (currentUsableTargets.Count > 0)
            {
                CancelDelayedUse();
                OnUsableTargetLost?.Invoke();
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
