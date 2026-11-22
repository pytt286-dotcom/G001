using System;
using UnityEngine;

namespace Sealbound.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        /// <summary>0 = light tap, 1 = big finisher. Drives knockback and screen feedback.</summary>
        public float Impact;
        /// <summary>Upward speed given to the target (launchers). 0 = none.</summary>
        public float Launch;
    }

    /// <summary>Hit points for anything that can be damaged: players, creatures, dummies.</summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 100f;
        [Tooltip("Below 1 means armoured: e.g. 0.5 halves incoming damage.")]
        public float damageMultiplier = 1f;

        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>Optional check, e.g. the player's dodge i-frames.</summary>
        public Func<bool> InvulnerableCheck;

        [Tooltip("Where spells aim. Empty = a little above the feet. Big creatures set this to their chest.")]
        public Transform aimPoint;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 0.8f;

        public event Action<DamageInfo, float> Damaged; // info, damage actually dealt
        public event Action Died;

        void Awake() => Current = maxHealth;

        public bool TryDamage(DamageInfo info, out float dealt)
        {
            dealt = 0f;
            if (IsDead) return false;
            if (InvulnerableCheck != null && InvulnerableCheck()) return false;

            dealt = Mathf.Max(0f, info.Amount * damageMultiplier);
            Current = Mathf.Max(0f, Current - dealt);
            Damaged?.Invoke(info, dealt);
            if (IsDead) Died?.Invoke();
            return true;
        }

        public void ResetHealth() => Current = maxHealth;

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }
    }
}
