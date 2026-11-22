using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// Sword class combat: a 3-hit light combo, a heavy slam, an air slash, soft aim toward nearby
    /// targets, and dodge-cancelling out of any attack.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInputReader))]
    public class PlayerCombat : MonoBehaviour
    {
        [System.Serializable]
        public class AttackData
        {
            public string name;
            public float windup, active, recovery;
            public float damage;
            public float range = 2.4f;
            public float arcDegrees = 150f;
            public float lungeSpeed;
            [Range(0f, 1f)] public float impact;
            public float hitStop;
            public float shake;
            public float staminaCost;
            public float hover;
            public Vector3 swingFrom, swingTo; // sword pivot euler angles
            public float Total => windup + active + recovery;
        }

        [Header("Attacks")]
        public AttackData[] lightCombo =
        {
            new AttackData { name = "Slash", windup = 0.05f, active = 0.08f, recovery = 0.16f, damage = 10f, lungeSpeed = 7f,
                impact = 0.2f, hitStop = 0.035f, shake = 0.15f, swingFrom = new Vector3(10f, -80f, 0f), swingTo = new Vector3(20f, 80f, 0f) },
            new AttackData { name = "Return Slash", windup = 0.04f, active = 0.08f, recovery = 0.16f, damage = 12f, lungeSpeed = 7f,
                impact = 0.3f, hitStop = 0.04f, shake = 0.18f, swingFrom = new Vector3(25f, 85f, 0f), swingTo = new Vector3(5f, -85f, 0f) },
            new AttackData { name = "Overhead Cleave", windup = 0.09f, active = 0.1f, recovery = 0.26f, damage = 22f, lungeSpeed = 11f,
                impact = 0.8f, hitStop = 0.08f, shake = 0.45f, arcDegrees = 110f, range = 2.8f,
                swingFrom = new Vector3(-75f, 10f, 0f), swingTo = new Vector3(70f, 0f, 0f) },
        };
        public AttackData heavy = new AttackData
        {
            name = "Sealbreaker Slam", windup = 0.24f, active = 0.1f, recovery = 0.3f, damage = 38f, lungeSpeed = 9f,
            impact = 1f, hitStop = 0.11f, shake = 0.7f, arcDegrees = 360f, range = 3.2f, staminaCost = 20f,
            swingFrom = new Vector3(-90f, 0f, 0f), swingTo = new Vector3(80f, 0f, 0f)
        };
        public AttackData airSlash = new AttackData
        {
            name = "Air Slash", windup = 0.04f, active = 0.1f, recovery = 0.14f, damage = 12f, lungeSpeed = 6f,
            impact = 0.35f, hitStop = 0.045f, shake = 0.2f, arcDegrees = 200f, range = 2.6f, hover = 0.3f,
            swingFrom = new Vector3(-40f, -70f, 0f), swingTo = new Vector3(60f, 70f, 0f)
        };

        [Header("Feel")]
        public float inputBuffer = 0.25f;
        public float comboResetTime = 0.5f;
        public float aimAssistRange = 5f;
        public float aimAssistAngle = 70f;
        [Range(0f, 1f)] public float steerWhileAttacking = 0.15f;
        public int maxAirAttacks = 2;

        [Header("Sword")]
        public Transform swordPivot;
        public Vector3 restEuler = new Vector3(35f, 25f, 0f);

        public bool IsAttacking => _current != null;
        public int ComboHits { get; private set; }
        public string CurrentAttackName => _current?.name;

        PlayerMotor _motor;
        PlayerInputReader _input;
        Health _ownHealth;
        TrailRenderer _trail;

        AttackData _current;
        float _attackTime;
        int _comboIndex;
        float _sinceLastAttack = 10f;
        float _lightBuffer, _heavyBuffer;
        float _sinceLastHit = 10f;
        int _airAttacksUsed;
        readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
        readonly Collider[] _overlap = new Collider[32];

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _ownHealth = GetComponent<Health>();
            _motor.DodgeStarted += CancelAttack;
        }

        void Start()
        {
            if (swordPivot != null)
            {
                swordPivot.localRotation = Quaternion.Euler(restEuler);
                _trail = swordPivot.GetComponentInChildren<TrailRenderer>();
                if (_trail != null) _trail.emitting = false;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _sinceLastAttack += dt;
            _sinceLastHit += dt;
            if (_sinceLastHit > 1.5f) ComboHits = 0;
            if (_motor.IsGrounded) _airAttacksUsed = 0;

            if (_input.AttackPressed) _lightBuffer = inputBuffer; else _lightBuffer -= dt;
            if (_input.HeavyPressed) _heavyBuffer = inputBuffer; else _heavyBuffer -= dt;

            if (_motor.IsDodging) { _lightBuffer = Mathf.Min(_lightBuffer, 0.1f); }

            if (_current != null) UpdateAttack(dt);

            if (CanStartNext())
            {
                if (_heavyBuffer > 0f && _motor.IsGrounded && _motor.Stamina >= heavy.staminaCost)
                {
                    _heavyBuffer = 0f;
                    _motor.TrySpendStamina(heavy.staminaCost);
                    StartAttack(heavy);
                    _comboIndex = 0;
                }
                else if (_lightBuffer > 0f)
                {
                    _lightBuffer = 0f;
                    if (!_motor.IsGrounded)
                    {
                        if (_airAttacksUsed < maxAirAttacks)
                        {
                            _airAttacksUsed++;
                            StartAttack(airSlash);
                        }
                    }
                    else
                    {
                        if (_sinceLastAttack > comboResetTime + 0.3f) _comboIndex = 0;
                        StartAttack(lightCombo[_comboIndex]);
                        _comboIndex = (_comboIndex + 1) % lightCombo.Length;
                    }
                }
            }

            UpdateSwordPose(dt);
        }

        bool CanStartNext()
        {
            if (_motor.IsDodging) return false;
            if (_current == null) return true;
            // Chain into the next attack once this one's hit is finished.
            return _attackTime >= _current.windup + _current.active;
        }

        void StartAttack(AttackData attack)
        {
            _current = attack;
            _attackTime = 0f;
            _sinceLastAttack = 0f;
            _hitThisSwing.Clear();

            Vector3 aim = FindAimDirection();
            _motor.FaceDirection(aim);
            _motor.RotationLocked = true;
            _motor.MoveSpeedMultiplier = steerWhileAttacking;
            if (attack.hover > 0f) _motor.Hover(attack.hover);
        }

        void UpdateAttack(float dt)
        {
            var a = _current;
            float prev = _attackTime;
            _attackTime += dt;

            // Lunge at the moment the swing starts.
            if (prev < a.windup && _attackTime >= a.windup && a.lungeSpeed > 0f)
                _motor.Lunge(transform.forward, a.lungeSpeed);

            bool active = _attackTime >= a.windup && _attackTime <= a.windup + a.active;
            if (_trail != null) _trail.emitting = active;
            if (active) DetectHits(a);

            if (_attackTime >= a.Total) EndAttack();
        }

        void DetectHits(AttackData a)
        {
            Vector3 center = transform.position + Vector3.up * 1f;
            int count = Physics.OverlapSphereNonAlloc(center, a.range, _overlap, ~0, QueryTriggerInteraction.Collide);
            bool landed = false;
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h == _ownHealth || _hitThisSwing.Contains(h)) continue;

                Vector3 closest = _overlap[i].ClosestPoint(center);
                Vector3 to = closest - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > a.arcDegrees * 0.5f) continue;

                _hitThisSwing.Add(h);
                var info = new DamageInfo
                {
                    Amount = a.damage,
                    Point = closest,
                    Direction = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward,
                    Source = gameObject,
                    Impact = a.impact
                };
                if (h.TryDamage(info, out _)) landed = true;
            }

            if (landed)
            {
                ComboHits++;
                _sinceLastHit = 0f;
                HitStop.Trigger(a.hitStop);
                ThirdPersonCamera.Shake(a.shake);
            }
        }

        Vector3 FindAimDirection()
        {
            Vector3 wish = _motor.MoveInputWorld;
            Vector3 baseDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;

            Health best = null;
            float bestScore = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, aimAssistRange, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h == _ownHealth || h.IsDead) continue;
                Vector3 to = h.transform.position - transform.position; to.y = 0f;
                float angle = Vector3.Angle(baseDir, to);
                if (angle > aimAssistAngle) continue;
                float score = to.magnitude + angle * 0.05f;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            if (best == null) return baseDir;
            Vector3 dir = best.transform.position - transform.position; dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : baseDir;
        }

        void EndAttack()
        {
            _current = null;
            _motor.RotationLocked = false;
            _motor.MoveSpeedMultiplier = 1f;
            if (_trail != null) _trail.emitting = false;
        }

        void CancelAttack()
        {
            if (_current == null) return;
            EndAttack();
            _comboIndex = 0;
        }

        void UpdateSwordPose(float dt)
        {
            if (swordPivot == null) return;
            Quaternion target;
            if (_current == null)
            {
                target = Quaternion.Euler(restEuler);
                swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, target, 1f - Mathf.Exp(-18f * dt));
                return;
            }

            var a = _current;
            Quaternion from = Quaternion.Euler(a.swingFrom);
            Quaternion to = Quaternion.Euler(a.swingTo);
            if (_attackTime < a.windup)
            {
                float t = _attackTime / Mathf.Max(0.0001f, a.windup);
                target = Quaternion.Slerp(swordPivot.localRotation, from, 1f - Mathf.Pow(1f - t, 3f));
            }
            else if (_attackTime < a.windup + a.active)
            {
                float t = (_attackTime - a.windup) / Mathf.Max(0.0001f, a.active);
                target = Quaternion.Slerp(from, to, 1f - Mathf.Pow(1f - t, 2f));
            }
            else
            {
                target = Quaternion.Slerp(swordPivot.localRotation, to, 0.5f);
            }
            swordPivot.localRotation = target;
        }
    }
}
