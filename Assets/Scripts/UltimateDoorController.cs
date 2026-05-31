using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using MHZE.EventSystem;

public class UltimateDoorController : MonoBehaviour
{
    public enum DoorState { Open, Closed, Locked }
    public enum DoorMode { Rotating, Sliding }

    [SerializeField] private DoorState _state = DoorState.Closed;
    [SerializeField] private DoorMode _mode = DoorMode.Rotating;

    [Tooltip("Local axis to rotate around when mode is Rotating.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;
    [Tooltip("Angle in degrees to rotate from closed to open.")]
    [SerializeField] private float _openAngle = 90f;

    [Tooltip("Local axis to translate along when mode is Sliding.")]
    [SerializeField] private Vector3 _slideAxis = Vector3.right;
    [Tooltip("Distance in world units to slide from closed to open.")]
    [SerializeField] private float _slideDistance = 2f;

    [Tooltip("Duration of the open/close animation in seconds.")]
    [SerializeField] private float _animationDuration = 0.5f;
    [Tooltip("Curve sampled over animation duration; evaluated value drives Lerp/Slerp between closed and open transforms.")]
    [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("When enabled, entering the trigger zone opens the door.")]
    [SerializeField] private bool _autoOpen;
    [Tooltip("When enabled, exiting the trigger zone closes the door.")]
    [SerializeField] private bool _autoClose;
    [Tooltip("Layers that can trigger the automatic door. Defaults to Player.")]
    [SerializeField] private LayerMask _triggerLayer = 1 << 3;
    [Tooltip("If false, the door will not reverse mid-animation; it completes its current motion first.")]
    [SerializeField] private bool _allowIntruption = true;

    [Tooltip("When enabled, unlocking requires a matching password.")]
    [SerializeField] private bool _hasPassword;
    [Tooltip("Base64-encoded SHA256 hash of the password set via the Password property.")]
    [SerializeField] private string _passwordHash;

    public EventBinding OnOpened = new EventBinding();
    public EventBinding OnClosed = new EventBinding();
    public EventBinding OnLocked = new EventBinding();
    public EventBinding OnUnlocked = new EventBinding();
    public EventBinding OnLockFailed = new EventBinding();

    private Transform _transform;
    private Quaternion _restRotation;
    private Vector3 _restPosition;
    private Coroutine _moveRoutine;
    private bool _reverseDirection;
    private bool _isAnimating;
    private bool _isOpening;

    public DoorState State => _state;
    public DoorMode Mode => _mode;

    public bool AllowIntruption
    {
        get => _allowIntruption;
        set => _allowIntruption = value;
    }

    public string Password
    {
        get => _passwordHash;
        set => _passwordHash = HashPassword(value);
    }

    private void Awake()
    {
        _transform = transform;
        _restRotation = _transform.localRotation;
        _restPosition = _transform.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_autoOpen) return;
        if (!IsInTriggerLayer(other.gameObject.layer)) return;

        _reverseDirection = DetermineOpenDirection(other.transform.position);
        Open();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_autoClose) return;
        if (!IsInTriggerLayer(other.gameObject.layer)) return;

        Close();
    }

    private bool IsInTriggerLayer(int layer)
    {
        return (_triggerLayer & (1 << layer)) != 0;
    }

    private bool DetermineOpenDirection(Vector3 targetPosition)
    {
        Vector3 toTarget = (targetPosition - _transform.position).normalized;
        return Vector3.Dot(_transform.forward, toTarget) > 0f;
    }

    private IEnumerator AnimateMove(Quaternion startRot, Vector3 startPos, Quaternion targetRot, Vector3 targetPos, bool opening)
    {
        float elapsed = 0f;

        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _animationDuration);
            float curveT = _movementCurve.Evaluate(t);

            if (_mode == DoorMode.Rotating)
                _transform.localRotation = Quaternion.SlerpUnclamped(startRot, targetRot, curveT);
            else
                _transform.localPosition = Vector3.LerpUnclamped(startPos, targetPos, curveT);

            yield return null;
        }

        if (_mode == DoorMode.Rotating)
            _transform.localRotation = targetRot;
        else
            _transform.localPosition = targetPos;

        _moveRoutine = null;
        _isAnimating = false;

        if (opening)
        {
            _state = DoorState.Open;
            OnOpened?.Invoke();
        }
        else
        {
            _state = DoorState.Closed;
            OnClosed?.Invoke();
        }
    }

    private void StopExistingRoutine()
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }
    }

    public void Open(Transform interactor = null)
    {
        if (_state == DoorState.Locked) return;
        if (!_isAnimating && _state == DoorState.Open) return;
        if (!_allowIntruption && _isAnimating) return;

        StopExistingRoutine();
        _isAnimating = true;
        _isOpening = true;

        bool reverse;
        if (interactor != null)
        {
            reverse = DetermineOpenDirection(interactor.position);
        }
        else
        {
            reverse = _reverseDirection;
        }
        _reverseDirection = false;

        Quaternion startRot = _transform.localRotation;
        Vector3 startPos = _transform.localPosition;

        Quaternion targetRot = _restRotation;
        Vector3 targetPos = _restPosition;

        if (_mode == DoorMode.Rotating)
        {
            float angle = reverse ? -_openAngle : _openAngle;
            targetRot = _restRotation * Quaternion.AngleAxis(angle, _rotationAxis.normalized);
        }
        else
        {
            Vector3 axis = (reverse ? -_slideAxis : _slideAxis).normalized;
            targetPos = _restPosition + axis * _slideDistance;
        }

        _moveRoutine = StartCoroutine(AnimateMove(startRot, startPos, targetRot, targetPos, true));
    }

    public void Close()
    {
        if (_state == DoorState.Locked) return;
        if (!_isAnimating && _state == DoorState.Closed) return;
        if (!_allowIntruption && _isAnimating) return;

        StopExistingRoutine();
        _isAnimating = true;
        _isOpening = false;

        _moveRoutine = StartCoroutine(AnimateMove(
            _transform.localRotation,
            _transform.localPosition,
            _restRotation,
            _restPosition,
            false
        ));
    }

    public void Toggle(Transform interactor = null)
    {
        if (_state == DoorState.Locked) return;

        if (_isAnimating)
        {
            if (_isOpening)
                Close();
            else
                Open(interactor);
        }
        else
        {
            if (_state == DoorState.Open)
                Close();
            else
                Open(interactor);
        }
    }

    public void Lock()
    {
        if (_state == DoorState.Locked) return;

        _state = DoorState.Locked;
        OnLocked?.Invoke();
    }

    public void Unlock(string input)
    {
        if (_state != DoorState.Locked) return;

        if (!_hasPassword || string.IsNullOrEmpty(_passwordHash))
        {
            _state = DoorState.Closed;
            OnUnlocked?.Invoke();
            return;
        }

        if (string.Equals(_passwordHash, HashPassword(input), StringComparison.Ordinal))
        {
            _state = DoorState.Closed;
            OnUnlocked?.Invoke();
        }
        else
        {
            OnLockFailed?.Invoke();
        }
    }

    [ContextMenu("Test Open")]
    private void TestOpen() => Open();

    [ContextMenu("Test Close")]
    private void TestClose() => Close();

    [ContextMenu("Test Lock")]
    private void TestLock() => Lock();

    private static string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
