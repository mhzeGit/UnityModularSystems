#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.FreeformCamera
{
    public class FreeformCameraController : MonoBehaviour
    {
        public float baseSpeed = 10f;
        public float boostMultiplier = 2f;
        public float lookSensitivity = 3f;
        public bool invertY;
        public bool enableCollision;
        public LayerMask collisionMask = -1;
        public float collisionRadius = 0.5f;
        public float collisionOffset = 0.05f;

        public bool IsActive { get; private set; } = true;
        public float CurrentSpeed { get; private set; }

        private Transform _transform;
        private Camera _camera;
        private float _pitch;
        private float _yaw;
        private Vector3 _targetPosition;
        private float _speed;

        private Keyboard _keyboard;
        private Mouse _mouse;
        private Vector2 _lastScroll;

        private void Awake()
        {
            _transform = transform;
            _camera = GetComponent<Camera>();
            _targetPosition = _transform.position;
            _speed = baseSpeed;
            CurrentSpeed = _speed;
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;

            Vector3 euler = _transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            if (_pitch > 180f) _pitch -= 360f;
        }

        private void OnDisable()
        {
            IsActive = false;
        }

        private void Update()
        {
            if (!IsActive || _keyboard == null || _mouse == null) return;

            HandleCursor();
            HandleRotation();
            HandleSpeed();
            HandleMovement();
            ApplyMovement();
        }

        private void HandleCursor()
        {
            if (_mouse.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (_mouse.rightButton.wasReleasedThisFrame || _keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleRotation()
        {
            if (!_mouse.rightButton.isPressed) return;

            Vector2 delta = _mouse.delta.ReadValue();
            float sens = lookSensitivity * 0.1f;

            _yaw += delta.x * sens;
            _pitch += (invertY ? delta.y : -delta.y) * sens;
            _pitch = Mathf.Clamp(_pitch, -90f, 90f);

            _transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleSpeed()
        {
            Vector2 currentScroll = _mouse.scroll.ReadValue();
            float scrollDelta = currentScroll.y - _lastScroll.y;
            _lastScroll = currentScroll;

            if (Mathf.Abs(scrollDelta) > 0.001f)
            {
                _speed *= Mathf.Pow(1.1f, scrollDelta);
                _speed = Mathf.Clamp(_speed, 0.001f, 100000f);
                CurrentSpeed = _speed;
            }
        }

        private void HandleMovement()
        {
            Vector3 direction = Vector3.zero;

            if (_keyboard.wKey.isPressed) direction += _transform.forward;
            if (_keyboard.sKey.isPressed) direction -= _transform.forward;
            if (_keyboard.aKey.isPressed) direction -= _transform.right;
            if (_keyboard.dKey.isPressed) direction += _transform.right;
            if (_keyboard.eKey.isPressed) direction += Vector3.up;
            if (_keyboard.qKey.isPressed) direction += Vector3.down;

            if (direction == Vector3.zero) return;

            float speedMul = _keyboard.shiftKey.isPressed ? boostMultiplier : 1f;
            direction.Normalize();
            _targetPosition += direction * (_speed * speedMul * Time.unscaledDeltaTime);
        }

        private void ApplyMovement()
        {
            Vector3 moveDir = _targetPosition - _transform.position;
            float dist = moveDir.magnitude;

            if (dist <= 0.0001f) return;

            Vector3 dir = moveDir / dist;

            if (enableCollision)
            {
                if (Physics.SphereCast(_transform.position, collisionRadius, dir,
                    out RaycastHit hit, dist + collisionOffset, collisionMask))
                {
                    float maxDist = Mathf.Max(0f, hit.distance - collisionOffset);
                    float remainder = dist - maxDist;

                    if (remainder > 0.001f)
                    {
                        Vector3 slideDir = Vector3.ProjectOnPlane(dir, hit.normal);
                        if (slideDir.sqrMagnitude > 0.0001f)
                        {
                            slideDir.Normalize();
                            _targetPosition = _transform.position + dir * maxDist + slideDir * remainder;
                        }
                        else
                        {
                            _targetPosition = _transform.position + dir * maxDist;
                        }
                    }
                    else
                    {
                        _targetPosition = _transform.position + dir * maxDist;
                    }
                }
            }

            _transform.position = _targetPosition;
        }
    }
}

#endif
