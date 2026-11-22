using System;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// A ground-targeted spell: a glowing circle warns where it lands, then it bursts,
    /// hurting (and optionally launching) everything inside.
    /// </summary>
    public class AreaBlast : MonoBehaviour
    {
        public float radius = 3.5f;
        public float delay = 0.35f;
        public float damage = 20f;
        public float impact = 0.6f;
        public float launch;
        public Color color = new Color(1f, 0.45f, 0.1f);
        public GameObject caster;
        public Action<bool, float> OnHit;

        float _age;
        Transform _ring;

        public static AreaBlast Cast(Vector3 at, float radius, float delay, float damage, float impact, float launch,
            Color color, GameObject caster, Action<bool, float> onHit)
        {
            var go = new GameObject("Flame Burst");
            go.transform.position = at;
            var a = go.AddComponent<AreaBlast>();
            a.radius = radius; a.delay = delay; a.damage = damage; a.impact = impact; a.launch = launch;
            a.color = color; a.caster = caster; a.OnHit = onHit;
            a._ring = Shapes.Make(PrimitiveType.Cylinder, go.transform, "Ring", new Vector3(0f, 0.05f, 0f),
                new Vector3(radius * 2f, 0.01f, radius * 2f), color * 0.8f, false, true).transform;
            return a;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_ring != null)
            {
                float grow = Mathf.Clamp01(_age / delay);
                _ring.localScale = new Vector3(radius * 2f * grow, 0.01f, radius * 2f * grow);
            }
            if (_age < delay) return;

            bool landed = false;
            foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = c.GetComponentInParent<Health>();
                if (h == null) continue;
                Vector3 dir = h.transform.position - transform.position; dir.y = 0f;
                if (SpellHit.Apply(h, caster, damage, c.ClosestPoint(transform.position), dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward,
                        impact, launch, 0f, 0f))
                    landed = true;
            }
            OnHit?.Invoke(landed, impact);
            SealBurst.Spawn(transform.position + Vector3.up * 0.5f, radius * 1.6f, color);
            Destroy(gameObject);
        }
    }
}
