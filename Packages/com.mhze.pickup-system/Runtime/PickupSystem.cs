using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.PickupSystem
{
    public class PickupSystem : MonoBehaviour
    {
        public static PickupSystem Instance { get; private set; }

        public Transform PlayerArmBase;
        public Transform PickableObjectHolder;
        [Header("Inputs")]
        [SerializeField] InputActionReference DropInputAction;

        public event System.Action<GameObject> OnPickedItemChanged;
        public event System.Action<IPickable> OnItemPicked;
        public event System.Action OnAttemptedPickWhileOccupied;

        IPickable currentPickableItem;
        GameObject currentPickedObject;

        IPickable attemptedPickableItem;
        GameObject attemptedPickableObject;

        Coroutine delayedPickCoroutine;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (PlayerArmBase == null)
                Debug.LogWarning("PlayerArmBase is not assigned in the Inspector — hand position will not move on pickup.", this);

            if (PickableObjectHolder == null)
                Debug.LogWarning("PickableObjectHolder is not assigned in the Inspector — picked items will not reparent.", this);

            if (DropInputAction == null)
                Debug.LogWarning("DropInputAction is not assigned in the Inspector — drop input will not work.", this);
        }

        #region Input Setup
        void OnEnable()
        {
            if (DropInputAction == null)
            {
                Debug.LogError("DropInputAction is null — cannot enable drop input. Assign an InputActionReference in the Inspector.", this);
                return;
            }

            DropInputAction.action.performed += DropInputPerformed;
            DropInputAction.action.Enable();
        }

        void OnDisable()
        {
            if (DropInputAction == null) return;

            DropInputAction.action.performed -= DropInputPerformed;
            DropInputAction.action.Disable();
        }

        void DropInputPerformed(InputAction.CallbackContext context)
        {
            if (currentPickedObject == null) return;

            if (currentPickableItem == null)
            {
                Debug.LogWarning("currentPickedObject exists but currentPickableItem is null — dropping via transform parent only.", this);
            }

            DropObject(currentPickedObject, currentPickableItem);
        }
        #endregion

        public void CheckIfPickable(GameObject interactedObject)
        {
            if (interactedObject == null)
            {
                Debug.LogWarning("CheckIfPickable called with null object.", this);
                return;
            }

            if (!interactedObject.TryGetComponent(out IPickable pickable))
            {
                Debug.LogWarning($"Object '{interactedObject.name}' does not have an IPickable component. Add PickableItemBase (or a custom IPickable implementation) to it.", interactedObject);
                return;
            }

            if (!pickable.GetIsPickable())
            {
                Debug.LogWarning($"Object '{interactedObject.name}' has IPickable but GetIsPickable() returned false. Check that PickableItemData is assigned and IsPickable is true.", interactedObject);
                return;
            }

            if (currentPickableItem != null)
            {
                attemptedPickableItem = pickable;
                attemptedPickableObject = interactedObject;

                OnAttemptedPickWhileOccupied?.Invoke();

                if (delayedPickCoroutine != null)
                    StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = StartCoroutine(PickAfterDelay(0.2f));
            }
            else
            {
                PickupObject(interactedObject, pickable);
            }
        }

        IEnumerator PickAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            delayedPickCoroutine = null;

            if (attemptedPickableObject != null && attemptedPickableItem != null)
            {
                PickupObject(attemptedPickableObject, attemptedPickableItem);
            }
        }

        public void PickupObject(GameObject obj, IPickable pickable)
        {
            if (obj == null)
            {
                Debug.LogError("PickupObject called with null GameObject.", this);
                return;
            }

            if (pickable == null)
            {
                Debug.LogError("PickupObject called with null IPickable.", this);
                return;
            }

            if (PickableObjectHolder == null)
            {
                Debug.LogError("PickableObjectHolder is not assigned — cannot reparent picked object. Assign a Transform in the Inspector.", this);
                return;
            }

            if (PlayerArmBase == null)
            {
                Debug.LogError("PlayerArmBase is not assigned — cannot position hand. Assign a Transform in the Inspector.", this);
                return;
            }

            currentPickableItem = pickable;
            currentPickedObject = obj;

            obj.transform.parent = PickableObjectHolder;
            obj.transform.localPosition = pickable.GetItemOffsetLocation();
            obj.transform.localRotation = pickable.GetItemOffsetRotation();
            PlayerArmBase.localPosition = pickable.GetHandOffsetLocation();
            pickable.SetPickState(false);
            pickable.Picked();

            OnItemPicked?.Invoke(pickable);
            OnPickedItemChanged?.Invoke(obj);

            attemptedPickableItem = null;
            attemptedPickableObject = null;
        }

        public void DropCurrentHeldItem()
        {
            if (currentPickedObject == null && currentPickableItem == null)
            {
                Debug.LogWarning("DropCurrentHeldItem called but nothing is currently held.", this);
                return;
            }

            if (currentPickedObject == null && currentPickableItem != null)
            {
                Debug.LogWarning("currentPickableItem is set but currentPickedObject is null. Forcing cleanup.", this);
                currentPickableItem = null;
                OnPickedItemChanged?.Invoke(null);
                return;
            }

            DropObject(currentPickedObject, currentPickableItem);
        }

        void DropObject(GameObject obj, IPickable pickable)
        {
            if (obj == null)
            {
                Debug.LogWarning("DropObject called with null GameObject.", this);
                return;
            }

            if (pickable == null)
            {
                Debug.LogWarning("DropObject called with null IPickable — dropping without calling IPickable callbacks.", this);
            }

            pickable?.SetPickState(true);
            pickable?.Dropped();

            obj.transform.parent = null;

            currentPickableItem = null;
            currentPickedObject = null;

            OnPickedItemChanged?.Invoke(null);
        }

        public void ForceReleaseWithoutCallbacks()
        {
            currentPickableItem = null;
            currentPickedObject = null;
            OnPickedItemChanged?.Invoke(null);
        }

        public bool IsHoldingItem() => currentPickedObject != null;
        public GameObject GetCurrentObject() => currentPickedObject;
        public IPickable GetCurrentPickable() => currentPickableItem;
        public GameObject GetAttemptedObject() => attemptedPickableObject;
        public IPickable GetAttemptedPickable() => attemptedPickableItem;
    }
}
