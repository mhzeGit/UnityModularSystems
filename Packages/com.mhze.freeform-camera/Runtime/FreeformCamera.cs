#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.FreeformCamera
{
    public static class FreeformCameraManager
    {
        private static FreeformManagerBehaviour s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (s_instance != null) return;

            var existing = Object.FindObjectOfType<FreeformManagerBehaviour>();
            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            var go = new GameObject("[FreeformCameraManager]");
            Object.DontDestroyOnLoad(go);
            s_instance = go.AddComponent<FreeformManagerBehaviour>();
        }
    }

    public class FreeformManagerBehaviour : MonoBehaviour
    {
        public Key toggleKey = Key.F8;
        public float baseSpeed = 10f;
        public float boostMultiplier = 2f;
        public float smoothTime = 0.15f;
        public float lookSensitivity = 1f;
        public bool invertY;
        public bool enableCollision;
        public LayerMask collisionMask = -1;
        public float collisionRadius = 0.5f;
        public float collisionOffset = 0.05f;

        private GameObject _freeformCameraObject;
        private Camera _originalCamera;
        private string _originalCameraTag;
        private bool _isActive;
        private Keyboard _keyboard;
        private GameObject _suspendedRootObject;
        private List<SuspendEntry> _suspendEntries;

        private struct SuspendEntry
        {
            public MonoBehaviour component;
            public MethodInfo enableMethod;
        }

        private const string PrefToggleKey = "FFC_ToggleKey";
        private const string PrefBaseSpeed = "FFC_BaseSpeed";
        private const string PrefBoostMul = "FFC_BoostMultiplier";
        private const string PrefSmoothTime = "FFC_SmoothTime";
        private const string PrefLookSens = "FFC_LookSensitivity";
        private const string PrefInvertY = "FFC_InvertY";
        private const string PrefEnableCol = "FFC_EnableCollision";
        private const string PrefColMask = "FFC_CollisionLayerMask";
        private const string PrefColRadius = "FFC_CollisionRadius";
        private const string PrefColOffset = "FFC_CollisionOffset";

        private void Awake()
        {
            _keyboard = Keyboard.current;
            _suspendEntries = new List<SuspendEntry>();
            LoadFromPlayerPrefs();
        }

        private void LoadFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(PrefToggleKey))
                toggleKey = (Key)PlayerPrefs.GetInt(PrefToggleKey);
            if (PlayerPrefs.HasKey(PrefBaseSpeed))
                baseSpeed = PlayerPrefs.GetFloat(PrefBaseSpeed);
            if (PlayerPrefs.HasKey(PrefBoostMul))
                boostMultiplier = PlayerPrefs.GetFloat(PrefBoostMul);
            if (PlayerPrefs.HasKey(PrefSmoothTime))
                smoothTime = PlayerPrefs.GetFloat(PrefSmoothTime);
            if (PlayerPrefs.HasKey(PrefLookSens))
                lookSensitivity = PlayerPrefs.GetFloat(PrefLookSens);
            if (PlayerPrefs.HasKey(PrefInvertY))
                invertY = PlayerPrefs.GetInt(PrefInvertY) == 1;
            if (PlayerPrefs.HasKey(PrefEnableCol))
                enableCollision = PlayerPrefs.GetInt(PrefEnableCol) == 1;
            if (PlayerPrefs.HasKey(PrefColMask))
                collisionMask = PlayerPrefs.GetInt(PrefColMask);
            if (PlayerPrefs.HasKey(PrefColRadius))
                collisionRadius = PlayerPrefs.GetFloat(PrefColRadius);
            if (PlayerPrefs.HasKey(PrefColOffset))
                collisionOffset = PlayerPrefs.GetFloat(PrefColOffset);
        }

        private void Update()
        {
            if (_keyboard == null) return;

            if (_keyboard[toggleKey].wasPressedThisFrame)
                Toggle();
        }

        private void Toggle()
        {
            if (_isActive)
                Deactivate();
            else
                Activate();
        }

        private void Activate()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[FreeformCamera] No Camera.main found. Cannot activate freeform camera.");
                return;
            }

            SupressPlayerControl(mainCam);

            _originalCamera = mainCam;
            _originalCamera.enabled = false;
            _originalCameraTag = _originalCamera.tag;
            _originalCamera.tag = "Untagged";

            _freeformCameraObject = new GameObject("Freeform Camera");

            var cam = _freeformCameraObject.AddComponent<Camera>();
            cam.CopyFrom(_originalCamera);
            cam.enabled = true;
            _freeformCameraObject.tag = "MainCamera";

            var listener = _originalCamera.GetComponent<AudioListener>();
            if (listener != null)
                _freeformCameraObject.AddComponent<AudioListener>();

            _freeformCameraObject.transform.SetPositionAndRotation(
                _originalCamera.transform.position,
                _originalCamera.transform.rotation);

            var ctrl = _freeformCameraObject.AddComponent<FreeformCameraController>();
            ctrl.baseSpeed = baseSpeed;
            ctrl.boostMultiplier = boostMultiplier;
            ctrl.smoothTime = smoothTime;
            ctrl.lookSensitivity = lookSensitivity;
            ctrl.invertY = invertY;
            ctrl.enableCollision = enableCollision;
            ctrl.collisionMask = collisionMask;
            ctrl.collisionRadius = collisionRadius;
            ctrl.collisionOffset = collisionOffset;

            _isActive = true;
        }

        private void Deactivate()
        {
            RestorePlayerControl();

            if (_originalCamera != null)
            {
                _originalCamera.enabled = true;
                _originalCamera.tag = _originalCameraTag;
            }

            if (_freeformCameraObject != null)
                Destroy(_freeformCameraObject);

            _freeformCameraObject = null;
            _originalCamera = null;
            _isActive = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SupressPlayerControl(Camera mainCam)
        {
            _suspendEntries.Clear();

            var root = mainCam.transform.root.gameObject;
            _suspendedRootObject = root;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null || !mb.enabled) continue;
                if (mb is FreeformCameraController) continue;

                var type = mb.GetType();
                var disableMethod = type.GetMethod("DisableControls",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    System.Type.EmptyTypes, null);
                var enableMethod = type.GetMethod("EnableControls",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    System.Type.EmptyTypes, null);

                if (disableMethod != null && enableMethod != null)
                {
                    disableMethod.Invoke(mb, null);
                    _suspendEntries.Add(new SuspendEntry
                    {
                        component = mb,
                        enableMethod = enableMethod
                    });
                }
            }

            root.SetActive(false);
        }

        private void RestorePlayerControl()
        {
            foreach (var entry in _suspendEntries)
            {
                if (entry.component != null && entry.enableMethod != null)
                    entry.enableMethod.Invoke(entry.component, null);
            }
            _suspendEntries.Clear();

            if (_suspendedRootObject != null)
                _suspendedRootObject.SetActive(true);

            _suspendedRootObject = null;
        }
    }
}

#endif
