using Sealbound.Enemies;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>Shared rules for spells: what they can hit and how damage is applied.</summary>
    public static class SpellHit
    {
        /// <summary>Damages a target hit by a player spell. Returns true if damage landed.</summary>
        public static bool Apply(Health h, GameObject caster, float damage, Vector3 point, Vector3 direction,
            float impact, float launch, float slow, float slowSeconds)
        {
            if (h == null || h.IsDead || (caster != null && h.gameObject == caster)) return false;
            var info = new DamageInfo
            {
                Amount = damage, Point = point, Direction = direction, Source = caster,
                Impact = impact, Launch = launch
            };
            bool landed = h.TryDamage(info, out _);
            if (landed && slow > 0f && slow < 1f)
            {
                var g = h.GetComponent<Goblin>();
                if (g != null) g.ApplySlow(slow, slowSeconds);
            }
            return landed;
        }
    }
}
