using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using ArgEvent;

namespace MHZE.UltimateDoorSystem
{
    public class UltimateDoorController : MonoBehaviour
{
    public enum DoorState { Open, Closed, Locked }
    public enum DoorMode { Rotating, Sliding }
    public enum AnimationType { Procedural, Animation, Animator, None }
    public enum LockState { Unlocked, Locked }

    [HideInInspector]
    [SerializeField] private DoorState _state = DoorState.Closed;
    [SerializeField] private LockState _lockState = LockState.Unlocked;

    [SerializeField] private AnimationType _animationType = AnimationType.Procedural;
    [SerializeField] private DoorMode _mode = DoorMode.Rotating;

    [Tooltip("Duration of the open/close animation in seconds.")]
    [SerializeField] private float _animationDuration = 0.5f;
    [Tooltip("Curve sampled over animation duration; evaluated value drives Lerp/Slerp between closed and open transforms.")]
    [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("If false, the door will not reverse mid-animation; it completes its current motion first.")]
    [SerializeField] private bool _allowIntruption = true;

    [Tooltip("Local axis to rotate around when mode is Rotating.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;
    [Tooltip("Angle in degrees to rotate from closed to open.")]
    [SerializeField] private float _openAngle = 90f;
    [Tooltip("Local axis pointing through the doorway (the door panel's normal). Used to detect which side the interactor is on so the door swings away from them. Forward preserves the original behaviour.")]
    [SerializeField] private Vector3 _openDirectionAxis = Vector3.forward;

    [Tooltip("Local axis to translate along when mode is Sliding.")]
    [SerializeField] private Vector3 _slideAxis = Vector3.right;
    [Tooltip("Distance in world units to slide from closed to open.")]
    [SerializeField] private float _slideDistance = 2f;

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

    [Header("Animation Component (Legacy)")]
    [SerializeField] private Animation _animationComponent;
    [SerializeField] private AnimationClip _openClip;
    [SerializeField] private AnimationClip _closeClip;
    [SerializeField] private AnimationClip _unlockClip;
    [SerializeField] private AnimationClip _lockClip;
    [SerializeField] private AnimationClip _attemptOpenLockedClip;

    [Header("Animator")]
    [SerializeField] private Animator _animatorComponent;
    [SerializeField] private string _openTrigger = "Open";
    [SerializeField] private string _closeTrigger = "Close";
    [SerializeField] private string _openBool = "isOpen";
    [SerializeField] private string _unlockTrigger = "Unlock";
    [SerializeField] private string _lockTrigger = "Lock";
    [SerializeField] private string _attemptOpenLockedTrigger = "AttemptOpenLocked";

    [Header("Audio")]
    [Tooltip("AudioSource used for door sounds. If left empty, one is found or created on this GameObject at runtime.")]
    [SerializeField] private AudioSource _audioSource;
    [Tooltip("Played when the door starts opening.")]
    [SerializeField] private AudioClip _openSound;
    [Tooltip("Played when the door starts closing.")]
    [SerializeField] private AudioClip _closeSound;
    [Tooltip("Played when someone tries to open the door while it is locked (handle rattle).")]
    [SerializeField] private AudioClip _lockedSound;

    [Header("Locked Rattle")]
    [Tooltip("Curve and numbers of the small shake on a locked try. Empty = DoorRattleProfile.Default.")]
    [SerializeField] private DoorRattleProfile _rattleProfile;
    [Tooltip("Per-door multiplier of the rattle size. 0 turns the shake off (the sound still plays).")]
    [SerializeField] private float _rattleAmplitude = 1f;

    public ArgEventBinding OnOpened = new ArgEventBinding();
    public ArgEventBinding OnClosed = new ArgEventBinding();
    public ArgEventBinding OnLocked = new ArgEventBinding();
    public ArgEventBinding OnUnlocked = new ArgEventBinding();
    public ArgEventBinding OnLockFailed = new ArgEventBinding();
    public ArgEventBinding OnAttemptOpenLocked = new ArgEventBinding();

    private Transform _transform;
    private Quaternion _restRotation;
    private Vector3 _restPosition;
    private Vector3 _restOpenDirection;
    private Coroutine _moveRoutine;
    private bool _reverseDirection;
    private bool _isAnimating;
    private bool _isOpening;
    private Coroutine _rattleRoutine;
    private Quaternion _rattleBaseRot;
    private Vector3 _rattleBasePos;
    private float _lastLockedSoundTime = -100f;

    /// <remarks>
    /// While a door swings open <see cref="State"/> is still <see cref="DoorState.Closed"/>, and while it
    /// swings shut it is still <see cref="DoorState.Open"/>: the state only changes when the motion ends.
    /// Use <see cref="IsOpening"/> / <see cref="IsClosing"/> to tell a door that is on its way from one
    /// that is standing still.
    /// </remarks>
    public DoorState State => _state;

    /// <summary>The door is moving (opening or closing) right now.</summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>The door is swinging open and has not finished yet.</summary>
    public bool IsOpening => _isAnimating && _isOpening;

    /// <summary>The door is swinging shut and has not finished yet (its <see cref="State"/> may still say Open).</summary>
    public bool IsClosing => _isAnimating && !_isOpening;

    /// <summary>
    /// Whoever last passed themselves to <see cref="Open(Transform)"/>, <see cref="CloseBy"/> or
    /// <see cref="Toggle"/>; null when the last call named nobody. Lets a listener tell the player's hand
    /// from a script's.
    /// </summary>
    public Transform LastInteractor { get; private set; }

    public DoorMode Mode => _mode;
    public AnimationType AnimType => _animationType;

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

        Vector3 openDirection = _openDirectionAxis.sqrMagnitude > 0.0001f
            ? _openDirectionAxis.normalized
            : Vector3.forward;
        _restOpenDirection = _transform.TransformDirection(openDirection);

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_lockState == LockState.Locked)
            _state = DoorState.Locked;
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
        return Vector3.Dot(_restOpenDirection, toTarget) > 0f;
    }

    private IEnumerator AnimateMove(Quaternion startRot, Vector3 startPos, Quaternion targetRot, Vector3 targetPos, bool opening)
        => AnimateMove(startRot, startPos, targetRot, targetPos, opening, _animationDuration, _movementCurve);

    private IEnumerator AnimateMove(Quaternion startRot, Vector3 startPos, Quaternion targetRot, Vector3 targetPos, bool opening, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = curve.Evaluate(t);

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

    private void OnDisable()
    {
        CancelRattle();
    }

    /// <summary>Stops a running locked rattle and puts the door back exactly on the pose it started from.</summary>
    private void CancelRattle()
    {
        if (_rattleRoutine == null) return;
        StopCoroutine(_rattleRoutine);
        _rattleRoutine = null;
        _transform.localRotation = _rattleBaseRot;
        _transform.localPosition = _rattleBasePos;
    }

    private void StartRattle()
    {
        DoorRattleProfile profile = _rattleProfile != null ? _rattleProfile : DoorRattleProfile.Default;

        // Every click restarts from the stored pose first, so nothing accumulates.
        CancelRattle();

        if (Time.time - _lastLockedSoundTime >= profile.soundCooldown)
        {
            _lastLockedSoundTime = Time.time;
            PlayLockedSound(profile);
        }

        if (_isAnimating || _rattleAmplitude <= 0f || profile.duration <= 0f) return;
        if (_animationType != AnimationType.Procedural && _animationType != AnimationType.None) return;

        _rattleBaseRot = _transform.localRotation;
        _rattleBasePos = _transform.localPosition;
        _rattleRoutine = StartCoroutine(RattleRoutine(profile, _rattleAmplitude, true));
    }

    /// <summary>
    /// Shakes the shut door on its hinge (or along its slide) with <paramref name="profile"/>'s curve: a knock, a bang or a
    /// shove from the other side. Works whether the door is locked or not and plays no sound (the caller plays its own
    /// knock). <paramref name="amplitude"/> scales the profile's peak; a negative one swings the other way first. A shake
    /// already running restarts from the pose it started from, and the door always ends exactly there; opening, closing,
    /// unlocking or disabling the door cancels it. Returns false (and does nothing) while the door is open or moving, or for
    /// a door animated by an Animation or Animator component.
    /// </summary>
    /// <param name="profile">Curve, duration and peak of the shake. Null = the door's own rattle profile (or the default).</param>
    public bool Shake(DoorRattleProfile profile = null, float amplitude = 1f)
    {
        if (_transform == null) _transform = transform;
        if (_isAnimating || _state == DoorState.Open) return false;
        if (_animationType != AnimationType.Procedural && _animationType != AnimationType.None) return false;
        if (profile == null) profile = _rattleProfile != null ? _rattleProfile : DoorRattleProfile.Default;
        if (Mathf.Approximately(amplitude, 0f) || profile.duration <= 0f) return false;

        CancelRattle();
        _rattleBaseRot = _transform.localRotation;
        _rattleBasePos = _transform.localPosition;
        _rattleRoutine = StartCoroutine(RattleRoutine(profile, amplitude, false));
        return true;
    }

    private IEnumerator RattleRoutine(DoorRattleProfile profile, float amplitude, bool lockedOnly)
    {
        float elapsed = 0f;
        while (elapsed < profile.duration)
        {
            if ((lockedOnly ? _state != DoorState.Locked : _state == DoorState.Open) || _isAnimating) break;
            elapsed += Time.deltaTime;
            float v = profile.offsetCurve.Evaluate(Mathf.Clamp01(elapsed / profile.duration)) * amplitude;

            if (_mode == DoorMode.Rotating)
                _transform.localRotation = _rattleBaseRot * Quaternion.AngleAxis(v * profile.peakDegrees, _rotationAxis.normalized);
            else
                _transform.localPosition = _rattleBasePos + _slideAxis.normalized * (v * profile.peakSlide);

            yield return null;
        }

        _transform.localRotation = _rattleBaseRot;
        _transform.localPosition = _rattleBasePos;
        _rattleRoutine = null;
    }

    private void PlayLockedSound(DoorRattleProfile profile)
    {
        if (_lockedSound == null) return;
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.pitch = 1f + UnityEngine.Random.Range(-profile.pitchVariation, profile.pitchVariation);
        _audioSource.PlayOneShot(_lockedSound, 1f - UnityEngine.Random.Range(0f, profile.volumeVariation));
    }

    private void StopExistingRoutine()
    {
        CancelRattle();
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _isAnimating = false;
        }

        if (_animationComponent != null)
            _animationComponent.Stop();
    }

    private IEnumerator PlayAnimationClip(AnimationClip clip, Action onComplete)
    {
        if (clip != null && _animationComponent != null)
        {
            _animationComponent.clip = clip;
            _animationComponent.Play();
            yield return new WaitForSeconds(clip.length);
        }
        onComplete?.Invoke();
    }

    private void SetAnimatorTrigger(string trigger)
    {
        if (_animatorComponent != null && !string.IsNullOrEmpty(trigger))
            _animatorComponent.SetTrigger(trigger);
    }

    private void SetAnimatorBool(bool value)
    {
        if (_animatorComponent != null && !string.IsNullOrEmpty(_openBool))
            _animatorComponent.SetBool(_openBool, value);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.pitch = 1f;
        _audioSource.PlayOneShot(clip);
    }

    private void FireStateEvent(bool opening)
    {
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

    private void HandleAttemptOpenLocked()
    {
        StartRattle();
        switch (_animationType)
        {
            case AnimationType.Animation:
                _moveRoutine = StartCoroutine(PlayAnimationClip(_attemptOpenLockedClip, () => OnAttemptOpenLocked?.Invoke()));
                break;
            case AnimationType.Animator:
                SetAnimatorTrigger(_attemptOpenLockedTrigger);
                OnAttemptOpenLocked?.Invoke();
                break;
            default:
                OnAttemptOpenLocked?.Invoke();
                break;
        }
    }

    public void Open(Transform interactor = null) => OpenSwing(interactor, _animationDuration, _movementCurve, true);

    /// <summary>
    /// Opens the door like <see cref="Open(Transform)"/>, but with its own timing and motion for this one swing: a door kicked
    /// or burst open, slamming against its stop and bouncing back a little. <paramref name="curve"/> runs over
    /// <paramref name="duration"/> seconds from 0 (where the door is now) to 1 (open); it is not clamped, so values over 1
    /// swing past the open pose. It should end at 1. A locked door only rattles, as with <see cref="Open(Transform)"/>.
    /// Only a Procedural door uses the duration and curve; the other animation types open as usual. A separate name from
    /// <c>Open</c> on purpose: event bindings look methods up by name.
    /// </summary>
    /// <param name="interactor">Who opens it (the door swings away from them); kept in <see cref="LastInteractor"/>.</param>
    /// <param name="curve">Null = the door's own movement curve.</param>
    /// <param name="playOpenSound">False when the caller plays its own sound (a slam).</param>
    public void OpenWith(Transform interactor, float duration, AnimationCurve curve, bool playOpenSound = true)
    {
        OpenSwing(interactor, duration > 0f ? duration : _animationDuration, curve ?? _movementCurve, playOpenSound);
    }

    private void OpenSwing(Transform interactor, float duration, AnimationCurve curve, bool playOpenSound)
    {
        if (_state == DoorState.Locked)
        {
            HandleAttemptOpenLocked();
            return;
        }
        if (!_isAnimating && _state == DoorState.Open) return;
        if (!_allowIntruption && _isAnimating) return;

        LastInteractor = interactor;
        StopExistingRoutine();
        _isOpening = true;

        if (playOpenSound) PlaySound(_openSound);

        switch (_animationType)
        {
            case AnimationType.Procedural:
            {
                _isAnimating = true;
                _isOpening = true;

                bool reverse;
                if (interactor != null)
                    reverse = DetermineOpenDirection(interactor.position);
                else
                    reverse = _reverseDirection;
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

                _moveRoutine = StartCoroutine(AnimateMove(startRot, startPos, targetRot, targetPos, true, duration, curve));
                break;
            }
            case AnimationType.Animation:
                _isAnimating = true;
                _moveRoutine = StartCoroutine(PlayAnimationClip(_openClip, () => FireStateEvent(true)));
                break;
            case AnimationType.Animator:
                SetAnimatorTrigger(_openTrigger);
                SetAnimatorBool(true);
                FireStateEvent(true);
                break;
            case AnimationType.None:
                FireStateEvent(true);
                break;
        }
    }

    public void Close() => CloseBy(null);

    /// <summary>
    /// Closes the door; <paramref name="interactor"/> (who is doing it) is kept in <see cref="LastInteractor"/>.
    /// A different name from <see cref="Close()"/> on purpose: event bindings find methods by name, and an
    /// overload would make <c>Close</c> ambiguous for them.
    /// </summary>
    public void CloseBy(Transform interactor)
    {
        if (_state == DoorState.Locked) return;
        if (!_isAnimating && _state == DoorState.Closed) return;
        if (!_allowIntruption && _isAnimating) return;

        LastInteractor = interactor;
        StopExistingRoutine();
        _isOpening = false;

        PlaySound(_closeSound);

        switch (_animationType)
        {
            case AnimationType.Procedural:
                _isAnimating = true;
                _isOpening = false;
                _moveRoutine = StartCoroutine(AnimateMove(
                    _transform.localRotation,
                    _transform.localPosition,
                    _restRotation,
                    _restPosition,
                    false
                ));
                break;
            case AnimationType.Animation:
                _isAnimating = true;
                _moveRoutine = StartCoroutine(PlayAnimationClip(_closeClip, () => FireStateEvent(false)));
                break;
            case AnimationType.Animator:
                SetAnimatorTrigger(_closeTrigger);
                SetAnimatorBool(false);
                FireStateEvent(false);
                break;
            case AnimationType.None:
                FireStateEvent(false);
                break;
        }
    }

    public void Toggle(Transform interactor)
    {
        if (_state == DoorState.Locked)
        {
            HandleAttemptOpenLocked();
            return;
        }

        if (_isAnimating)
        {
            if (_isOpening)
                CloseBy(interactor);
            else
                Open(interactor);
        }
        else
        {
            if (_state == DoorState.Open)
                CloseBy(interactor);
            else
                Open(interactor);
        }
    }

    public void Lock()
    {
        if (_state == DoorState.Locked) return;

        _state = DoorState.Locked;

        switch (_animationType)
        {
            case AnimationType.Animation:
                _moveRoutine = StartCoroutine(PlayAnimationClip(_lockClip, () => OnLocked?.Invoke()));
                break;
            case AnimationType.Animator:
                SetAnimatorTrigger(_lockTrigger);
                OnLocked?.Invoke();
                break;
            default:
                OnLocked?.Invoke();
                break;
        }
    }

    public void Unlock(string input)
    {
        if (_state != DoorState.Locked) return;
        CancelRattle();

        if (!_hasPassword || string.IsNullOrEmpty(_passwordHash))
        {
            _state = DoorState.Closed;
            PlayUnlockAnimation();
            return;
        }

        if (string.Equals(_passwordHash, HashPassword(input), StringComparison.Ordinal))
        {
            _state = DoorState.Closed;
            PlayUnlockAnimation();
        }
        else
        {
            OnLockFailed?.Invoke();
        }
    }

    private void PlayUnlockAnimation()
    {
        switch (_animationType)
        {
            case AnimationType.Animation:
                _moveRoutine = StartCoroutine(PlayAnimationClip(_unlockClip, () => OnUnlocked?.Invoke()));
                break;
            case AnimationType.Animator:
                SetAnimatorTrigger(_unlockTrigger);
                OnUnlocked?.Invoke();
                break;
            default:
                OnUnlocked?.Invoke();
                break;
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
}
