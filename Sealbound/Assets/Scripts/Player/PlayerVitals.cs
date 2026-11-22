using System;
using Sealbound.CameraRig;
using Sealbound.Combat;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// The player's health reactions: knockback when hit, falling when health runs out,
    /// getting back up at the start point, and slow healing out of combat.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerMotor))]
    public class PlayerVitals : MonoBehaviour
    {
        public float hitStun = 0.28f;
        public float knockback = 9f;
        public float respawnDelay = 3f;
        public float regenDelay = 5f;
        public float regenPerSecond = 12f;

        public Health Health { get; private set; }
        public bool IsDown { get; private set; }
        public float RespawnIn { get; private set; }
        public float HurtFlash { get; private set; }
        public event Action Fell;
        public event Action Respawned;
        /// <summary>When true, hits still hurt but no longer knock you back or break your attack (Fortress Stance).</summary>
        public Func<bool> Unstoppable;

        PlayerMotor _motor;
        PlayerCombat _combat;
        Vector3 _spawnPoint;
        float _sinceHit = 10f;

        void Awake()
        {
            Health = GetComponent<Health>();
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            Health.InvulnerableCheck = () => _motor.IsInvulnerable || IsDown;
            Health.Damaged += OnHit;
            Health.Died += OnFell;
        }

        void Start() => _spawnPoint = transform.position;

        void OnHit(DamageInfo info, float dealt)
        {
            _sinceHit = 0f;
            HurtFlash = 1f;
            if (Health.IsDead) return;
            if (Unstoppable != null && Unstoppable())
            {
                ThirdPersonCamera.Shake(0.15f + info.Impact * 0.15f);
                return;
            }
            _motor.Stagger(info.Direction, knockback * (0.5f + info.Impact), hitStun + info.Impact * 0.2f);
            if (_combat != null) _combat.Interrupt();
            HitStop.Trigger(0.05f + info.Impact * 0.05f);
            ThirdPersonCamera.Shake(0.3f + info.Impact * 0.3f);
        }

        void OnFell()
        {
            IsDown = true;
            RespawnIn = respawnDelay;
            _motor.Stagger(Vector3.zero, 0f, respawnDelay + 0.2f);
            if (_combat != null) _combat.Interrupt();
            // Stand-in body tips over; a real model plays its death animation instead.
            if (_motor.visual != null && GetComponent<PlayerAvatar>() == null) _motor.visual.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            Fell?.Invoke();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            HurtFlash = Mathf.MoveTowards(HurtFlash, 0f, dt * 3f);
            _sinceHit += dt;

            if (IsDown)
            {
                RespawnIn -= dt;
                if (RespawnIn <= 0f) Respawn();
                return;
            }

            if (_sinceHit > regenDelay && Health.Current < Health.maxHealth)
                Health.Heal(regenPerSecond * dt);
        }

        void Respawn()
        {
            IsDown = false;
            Health.ResetHealth();
            _motor.Teleport(_spawnPoint);
            if (_motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
            Respawned?.Invoke();
        }
    }
}
