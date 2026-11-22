using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// Third-person movement: run, sprint, jump and dodge, tuned to feel smooth and fast.
    /// All the numbers below are exposed in the Inspector so they can be tuned while playing.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Running")]
        public float runSpeed = 6.5f;
        public float sprintSpeed = 10f;
        public float acceleration = 45f;
        public float deceleration = 55f;
        [Range(0f, 1f)] public float airControl = 0.45f;
        public float turnSpeed = 900f;

        [Header("Jumping")]
        public float jumpHeight = 1.8f;
        public float gravity = 28f;
        public float fallGravityMultiplier = 1.7f;
        public float lowJumpGravityMultiplier = 2.2f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.15f;
        public float maxFallSpeed = 40f;

        [Header("Dodge")]
        public float dodgeDistance = 5f;
        public float dodgeDuration = 0.32f;
        public float dodgeRecovery = 0.08f;
        public float dodgeStaminaCost = 22f;
        [Tooltip("Part of the dodge during which hits will not land (used once combat exists).")]
        public float dodgeInvulnerableTime = 0.22f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float sprintStaminaPerSecond = 14f;
        public float staminaRegenPerSecond = 35f;
        public float staminaRegenDelay = 0.6f;

        [Header("References")]
        public Transform cameraTransform;
        public Transform visual;

        public float Stamina { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDodging => _dodgeTimer > 0f;
        public bool IsInvulnerable => IsDodging && (dodgeDuration - _dodgeTimer) < dodgeInvulnerableTime;
        public bool IsSprinting { get; private set; }
        public Vector3 PlanarVelocity => _planarVelocity;

        CharacterController _cc;
        PlayerInputReader _input;
        Vector3 _planarVelocity;
        float _verticalVelocity;
        float _coyoteTimer, _jumpBufferTimer, _dodgeTimer, _recoveryTimer, _regenDelayTimer;
        Vector3 _dodgeDirection;
        bool _airDodgeUsed;
        bool _wasGrounded;
        Vector3 _visualBaseScale = Vector3.one;
        float _squash; // >0 squash (landing), <0 stretch (jump)

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            Stamina = maxStamina;
            if (visual != null) _visualBaseScale = visual.localScale;
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (visual != null) _visualBaseScale = visual.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            IsGrounded = _cc.isGrounded;

            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
                _airDodgeUsed = false;
                if (!_wasGrounded && _verticalVelocity < -6f) _squash = Mathf.Clamp01(-_verticalVelocity / 30f) * 0.35f;
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (_input.JumpPressed) _jumpBufferTimer = jumpBufferTime;
            else _jumpBufferTimer -= dt;

            Vector3 wishDir = GetCameraRelativeInput();

            if (_input.DodgePressed) TryStartDodge(wishDir);

            if (IsDodging)
            {
                UpdateDodge(dt);
            }
            else
            {
                if (_recoveryTimer > 0f) _recoveryTimer -= dt;
                UpdateRunning(wishDir, dt);
                UpdateJump();
            }

            UpdateGravity(dt);
            UpdateStamina(dt);

            Vector3 motion = (_planarVelocity + Vector3.up * _verticalVelocity) * dt;
            _cc.Move(motion);

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // keep snapped to slopes

            _wasGrounded = IsGrounded;
            UpdateVisual(dt);
        }

        Vector3 GetCameraRelativeInput()
        {
            Vector2 raw = _input.Move;
            if (raw.sqrMagnitude < 0.0001f) return Vector3.zero;
            Vector3 fwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();
            Vector3 dir = fwd * raw.y + right * raw.x;
            return Vector3.ClampMagnitude(dir, 1f);
        }

        void UpdateRunning(Vector3 wishDir, float dt)
        {
            bool hasInput = wishDir.sqrMagnitude > 0.01f;
            IsSprinting = hasInput && _input.SprintHeld && Stamina > 0f && IsGrounded;

            float speed = IsSprinting ? sprintSpeed : runSpeed;
            Vector3 target = wishDir * speed;
            float rate = hasInput ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            if (_recoveryTimer > 0f) rate *= 0.5f;

            // Turning sharply keeps momentum feeling snappy rather than slidey.
            if (hasInput && IsGrounded && Vector3.Dot(_planarVelocity.normalized, wishDir.normalized) < -0.3f)
                rate *= 2f;

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, rate * dt);

            if (hasInput)
            {
                Quaternion look = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
            }
        }

        void UpdateJump()
        {
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                _squash = -0.25f;
            }
        }

        void UpdateGravity(float dt)
        {
            float g = gravity;
            if (IsDodging && !IsGrounded) g *= 0.25f; // brief hang time on air dodges
            else if (_verticalVelocity < 0f) g *= fallGravityMultiplier;
            else if (!_input.JumpHeld) g *= lowJumpGravityMultiplier; // tap = short hop, hold = full jump

            _verticalVelocity = Mathf.Max(_verticalVelocity - g * dt, -maxFallSpeed);
        }

        void TryStartDodge(Vector3 wishDir)
        {
            if (IsDodging || _recoveryTimer > 0f) return;
            if (Stamina < dodgeStaminaCost * 0.5f) return;
            if (!IsGrounded && _airDodgeUsed) return;

            _dodgeDirection = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            _dodgeDirection.y = 0f;
            _dodgeTimer = dodgeDuration;
            Stamina = Mathf.Max(0f, Stamina - dodgeStaminaCost);
            _regenDelayTimer = staminaRegenDelay;
            if (!IsGrounded)
            {
                _airDodgeUsed = true;
                _verticalVelocity = Mathf.Max(_verticalVelocity, 0f);
            }
            transform.rotation = Quaternion.LookRotation(_dodgeDirection, Vector3.up);
        }

        void UpdateDodge(float dt)
        {
            // Fast start, smooth finish: speed follows a falling curve that covers dodgeDistance.
            float t = 1f - (_dodgeTimer / dodgeDuration);
            float avgSpeed = dodgeDistance / dodgeDuration;
            float speed = avgSpeed * 2f * (1f - t); // integrates to dodgeDistance over the dodge
            _planarVelocity = _dodgeDirection * Mathf.Max(speed, runSpeed * 0.6f);

            _dodgeTimer -= dt;
            if (_dodgeTimer <= 0f)
            {
                _dodgeTimer = 0f;
                _recoveryTimer = dodgeRecovery;
                _planarVelocity = _dodgeDirection * runSpeed; // carry momentum into the run
            }
        }

        void UpdateStamina(float dt)
        {
            if (IsSprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - sprintStaminaPerSecond * dt);
                _regenDelayTimer = staminaRegenDelay;
                return;
            }
            if (_regenDelayTimer > 0f) { _regenDelayTimer -= dt; return; }
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * dt);
        }

        void UpdateVisual(float dt)
        {
            if (visual == null) return;
            _squash = Mathf.MoveTowards(_squash, 0f, dt * 2.5f);
            float y = 1f - _squash;
            float xz = 1f + _squash * 0.5f;
            Vector3 target = new Vector3(_visualBaseScale.x * xz, _visualBaseScale.y * y, _visualBaseScale.z * xz);
            if (IsDodging)
            {
                // Lean low into the dodge.
                target = new Vector3(_visualBaseScale.x * 1.1f, _visualBaseScale.y * 0.7f, _visualBaseScale.z * 1.1f);
            }
            visual.localScale = Vector3.Lerp(visual.localScale, target, 1f - Mathf.Exp(-20f * dt));
        }
    }
}
