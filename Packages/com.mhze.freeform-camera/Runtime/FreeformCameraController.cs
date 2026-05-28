#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.FreeformCamera
{
    public class FreeformCameraController : MonoBehaviour
    {
        public float baseSpeed = 10f;
        public float boostMultiplier = 2f;
        public float smoothTime = 0.15f;
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
        private Vector3 _smoothVelocity;
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
            Vector3 desiredPos = Vector3.SmoothDamp(
                _transform.position, _targetPosition, ref _smoothVelocity, smoothTime);

            if (enableCollision)
            {
                Vector3 moveDir = desiredPos - _transform.position;
                float dist = moveDir.magnitude;

                if (dist > 0.0001f)
                {
                    Vector3 dir = moveDir / dist;
                    if (Physics.SphereCast(_transform.position, collisionRadius, dir,
                        out RaycastHit hit, dist + collisionOffset, collisionMask))
                    {
                        Vector3 newPos = _transform.position
                            + dir * Mathf.Max(0f, hit.distance - collisionOffset);
                        _targetPosition = newPos;
                        _smoothVelocity = Vector3.zero;
                        _transform.position = newPos;
                        return;
                    }
                }
            }

            _transform.position = desiredPos;
        }
    }
}

#endif
