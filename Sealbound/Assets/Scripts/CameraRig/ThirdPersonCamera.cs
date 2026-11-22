using Sealbound.Player;
using UnityEngine;

namespace Sealbound.CameraRig
{
    /// <summary>
    /// Smooth orbit camera that follows the player, avoids walls and widens its view when moving fast.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public PlayerInputReader input;
        public PlayerMotor motor;

        [Header("Framing")]
        public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
        public float distance = 6f;
        public float shoulderOffset = 0.5f;
        public float minPitch = -35f;
        public float maxPitch = 70f;

        [Header("Feel")]
        public float mouseSensitivity = 0.12f;      // degrees per pixel
        public float stickSensitivity = 260f;       // degrees per second
        public bool invertY;
        public float followSmoothTime = 0.035f;
        public float collisionRadius = 0.3f;
        public float baseFov = 60f;
        public float sprintFov = 73f;
        public float dodgeFov = 80f;

        [Header("Shake")]
        public float maxShakeAngle = 2.5f;
        public float maxShakeOffset = 0.25f;
        public float shakeDecay = 2.5f;

        static ThirdPersonCamera _instance;
        float _trauma;

        /// <summary>Shake the camera. 0.1 = small tap, 1 = huge impact.</summary>
        public static void Shake(float amount)
        {
            if (_instance != null) _instance._trauma = Mathf.Clamp01(_instance._trauma + amount);
        }

        float _yaw, _pitch = 15f;
        float _currentDistance;
        Vector3 _pivot, _pivotVelocity;
        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _currentDistance = distance;
            _instance = this;
        }

        void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pivot = target.position + targetOffset;
            }
            LockCursor(true);
        }

        void Update()
        {
            // Esc frees the mouse; clicking in the game window captures it again.
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked &&
                     UnityEngine.InputSystem.Mouse.current != null &&
                     UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (input != null)
            {
                Vector2 look = input.LookMouse * mouseSensitivity + input.LookStick * (stickSensitivity * dt);
                _yaw += look.x;
                _pitch += invertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            Vector3 desiredPivot = target.position + targetOffset;
            _pivot = Vector3.SmoothDamp(_pivot, desiredPivot, ref _pivotVelocity, followSmoothTime);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 shoulder = rot * Vector3.right * shoulderOffset;
            Vector3 back = rot * Vector3.back;

            // Pull the camera in when something is between it and the player.
            float wanted = distance;
            if (Physics.SphereCast(_pivot + shoulder, collisionRadius, back, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.5f, hit.distance);
            _currentDistance = wanted < _currentDistance
                ? wanted
                : Mathf.Lerp(_currentDistance, wanted, 1f - Mathf.Exp(-6f * dt));

            Vector3 camPos = _pivot + shoulder + back * _currentDistance;
            if (_trauma > 0f)
            {
                float s = _trauma * _trauma;
                float time = Time.unscaledTime * 25f;
                rot *= Quaternion.Euler(
                    (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * maxShakeAngle * s,
                    (Mathf.PerlinNoise(0f, time) * 2f - 1f) * maxShakeAngle * s,
                    (Mathf.PerlinNoise(time, time) * 2f - 1f) * maxShakeAngle * s);
                camPos += rot * new Vector3(Mathf.PerlinNoise(time + 7f, 3f) * 2f - 1f, Mathf.PerlinNoise(3f, time + 7f) * 2f - 1f, 0f) * (maxShakeOffset * s);
                _trauma = Mathf.Max(0f, _trauma - shakeDecay * Time.unscaledDeltaTime);
            }
            transform.SetPositionAndRotation(camPos, rot);

            float fov = baseFov;
            if (motor != null)
            {
                if (motor.IsDodging) fov = dodgeFov;
                else if (motor.IsSprinting) fov = sprintFov;
            }
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, 1f - Mathf.Exp(-10f * dt));
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
