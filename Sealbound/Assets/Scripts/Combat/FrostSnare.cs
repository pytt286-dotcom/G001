using System.Collections.Generic;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>A frost trap on the ground: enemies that cross it are chilled (slowed) and take light damage.</summary>
    public class FrostSnare : MonoBehaviour
    {
        public float radius = 3f;
        public float lifetime = 8f;
        public float damage = 4f;
        public float slow = 0.45f;
        public float slowSeconds = 3f;
        public GameObject caster;

        static FrostSnare _active;
        float _age, _tick;
        readonly Dictionary<Health, float> _lastHit = new Dictionary<Health, float>();
        Transform _disc;

        public static void Place(Vector3 at, float radius, float damage, float slow, float slowSeconds, GameObject caster)
        {
            if (_active != null) Destroy(_active.gameObject); // only one snare at a time
            var go = new GameObject("Frost Snare");
            go.transform.position = at;
            var s = go.AddComponent<FrostSnare>();
            s.radius = radius; s.damage = damage; s.slow = slow; s.slowSeconds = slowSeconds; s.caster = caster;
            s._disc = Shapes.Make(PrimitiveType.Cylinder, go.transform, "Frost", new Vector3(0f, 0.04f, 0f),
                new Vector3(radius * 2f, 0.01f, radius * 2f), new Color(0.5f, 0.85f, 1f), false, true).transform;
            for (int i = 0; i < 6; i++)
            {
                var spike = Shapes.Make(PrimitiveType.Cube, go.transform, "Ice Spike",
                    Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * radius * 0.7f + Vector3.up * 0.25f,
                    new Vector3(0.15f, 0.5f, 0.15f), new Color(0.7f, 0.92f, 1f), false, true);
                spike.transform.localRotation = Quaternion.Euler(15f, i * 60f, 0f);
            }
            _active = s;
            SealBurst.Spawn(at + Vector3.up * 0.3f, radius, new Color(0.6f, 0.9f, 1f));
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime) { Destroy(gameObject); return; }
            if (_disc != null && _age > lifetime - 1f)
                _disc.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f) * Mathf.Max(0.05f, lifetime - _age);

            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = 0.2f;

            foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up * 0.6f, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead || (caster != null && h.gameObject == caster)) continue;
                if (_lastHit.TryGetValue(h, out float t) && Time.time - t < 1f) continue;
                _lastHit[h] = Time.time;
                Vector3 dir = h.transform.position - transform.position; dir.y = 0f;
                SpellHit.Apply(h, caster, damage, h.transform.position + Vector3.up, dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward,
                    0.1f, 0f, slow, slowSeconds);
            }
        }
    }
}
