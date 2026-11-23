using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>A thrown rock that flies in an arc and hurts the first thing it hits.</summary>
    public class Projectile : MonoBehaviour
    {
        public float damage = 8f;
        public float gravity = 9f;
        public float radius = 0.25f;
        public GameObject owner;

        Vector3 _velocity;
        float _life = 4f;

        public static void Throw(Vector3 from, Vector3 target, float speed, float damage, GameObject owner)
        {
            var go = Shapes.Make(PrimitiveType.Sphere, null, "Sling Rock", from, Vector3.one * 0.35f, new Color(0.45f, 0.42f, 0.38f));
            go.transform.position = from;
            var p = go.AddComponent<Projectile>();
            p.damage = damage;
            p.owner = owner;

            // Aim with a slight upward arc so the rock can be read and dodged.
            Vector3 to = target - from;
            float time = Mathf.Max(0.25f, to.magnitude / speed);
            p._velocity = to / time + Vector3.up * (0.5f * p.gravity * time);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }

            _velocity += Vector3.down * gravity * dt;
            Vector3 step = _velocity * dt;
            if (Physics.SphereCast(transform.position, radius, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && (owner == null || h.gameObject != owner) && h.GetComponent<Goblin>() == null)
                {
                    bool wasDead = h.IsDead;
                    var info = new DamageInfo
                    {
                        Amount = damage, Point = hit.point, Direction = _velocity.normalized,
                        Source = owner, Impact = 0.3f
                    };
                    if (!h.TryDamage(info, out _) && !wasDead)
                    {
                        var combat = h.GetComponent<PlayerCombat>();
                        if (combat != null) combat.NotifyDodged();
                        transform.position += step; // passed through the dodge, keep flying
                        return;
                    }
                }
                if (h == null || h.GetComponent<Goblin>() == null)
                {
                    SealBurst.Spawn(hit.point, 0.6f, new Color(0.6f, 0.55f, 0.5f));
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position += step;
        }
    }
}
