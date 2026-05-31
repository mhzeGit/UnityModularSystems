using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

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

    [Tooltip("When enabled, unlocking requires a matching password.")]
    [SerializeField] private bool _hasPassword;
    [Tooltip("Base64-encoded SHA256 hash of the password set via the Password property.")]
    [SerializeField] private string _passwordHash;

    [SerializeField] private UnityEvent _onOpened;
    [SerializeField] private UnityEvent _onClosed;
    [SerializeField] private UnityEvent _onLocked;
    [SerializeField] private UnityEvent _onUnlocked;
    [SerializeField] private UnityEvent _onLockFailed;

    private Transform _transform;
    private Quaternion _restRotation;
    private Vector3 _restPosition;
    private Coroutine _moveRoutine;
    private bool _reverseDirection;

    public DoorState State => _state;
    public DoorMode Mode => _mode;

    public UnityEvent OnOpened => _onOpened;
    public UnityEvent OnClosed => _onClosed;
    public UnityEvent OnLocked => _onLocked;
    public UnityEvent OnUnlocked => _onUnlocked;
    public UnityEvent OnLockFailed => _onLockFailed;

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
        return Vector3.Dot(_transform.forward, toTarget) < 0f;
    }

    private IEnumerator AnimateMove(Quaternion startRot, Vector3 startPos, Quaternion targetRot, Vector3 targetPos, bool opening)
    {
        StopExistingRoutine();

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

        if (opening)
        {
            _state = DoorState.Open;
            _onOpened.Invoke();
        }
        else
        {
            _state = DoorState.Closed;
            _onClosed.Invoke();
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

    public void Open()
    {
        if (_state == DoorState.Locked) return;
        if (_state == DoorState.Open) return;

        _state = DoorState.Closed;

        bool reverse = _reverseDirection;
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
        if (_state == DoorState.Closed) return;

        _state = DoorState.Open;

        _moveRoutine = StartCoroutine(AnimateMove(
            _transform.localRotation,
            _transform.localPosition,
            _restRotation,
            _restPosition,
            false
        ));
    }

    public void Toggle()
    {
        if (_state == DoorState.Locked) return;

        if (_state == DoorState.Open)
            Close();
        else if (_state == DoorState.Closed)
            Open();
    }

    public void Lock()
    {
        if (_state == DoorState.Locked) return;

        _state = DoorState.Locked;
        _onLocked.Invoke();
    }

    public void Unlock(string input)
    {
        if (_state != DoorState.Locked) return;

        if (!_hasPassword || string.IsNullOrEmpty(_passwordHash))
        {
            _state = DoorState.Closed;
            _onUnlocked.Invoke();
            return;
        }

        if (string.Equals(_passwordHash, HashPassword(input), StringComparison.Ordinal))
        {
            _state = DoorState.Closed;
            _onUnlocked.Invoke();
        }
        else
        {
            _onLockFailed.Invoke();
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
