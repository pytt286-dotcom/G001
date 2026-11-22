using System;
using System.Collections.Generic;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>A magic bolt that flies straight (curving slightly toward its target) and hits the first enemy.</summary>
    public class PlayerBolt : MonoBehaviour
    {
        public float speed = 40f;
        public float radius = 0.3f;
        public float damage = 10f;
        public float impact = 0.3f;
        public bool pierce;
        public float homing = 6f;
        public GameObject caster;
        public Transform target;
        public Action<bool, float> OnHit; // (landed, impact)

        Vector3 _dir;
        float _life = 1.6f;
        readonly HashSet<Health> _hit = new HashSet<Health>();

        public static PlayerBolt Fire(Vector3 from, Vector3 dir, Transform target, float speed, float radius, float damage,
            float impact, bool pierce, Color color, GameObject caster, Action<bool, float> onHit)
        {
            // Realistic look: a small white-hot core that lights its surroundings, with a thin fiery streak.
            // The hit radius stays the same; only the drawn ball is smaller.
            bool real = RealArt.Has;
            float drawn = real ? Mathf.Min(radius * 2f, 0.2f) : radius * 2f;
            var go = Shapes.Make(PrimitiveType.Sphere, null, "Ember Bolt", from, Vector3.one * drawn,
                real ? Color.Lerp(color, Color.white, 0.45f) : color, false, true);
            go.transform.position = from;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (real)
            {
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = 4f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
            }
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = real ? 0.12f : 0.15f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, real ? drawn * 0.9f : radius * 1.6f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) trail.sharedMaterial = new Material(shader);
            trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0f);

            var b = go.AddComponent<PlayerBolt>();
            b._dir = dir.normalized;
            b.target = target;
            b.speed = speed; b.radius = radius; b.damage = damage; b.impact = impact; b.pierce = pierce;
            b.caster = caster; b.OnHit = onHit;
            return b;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }

            if (target != null)
            {
                var th = target.GetComponent<Health>();
                Vector3 aimAt = th != null ? th.AimPosition : target.position; // an aim point itself, or a creature's own aim
                Vector3 want = (aimAt - transform.position).normalized;
                _dir = Vector3.RotateTowards(_dir, want, homing * dt, 0f);
            }

            float step = speed * dt;
            var hits = Physics.SphereCastAll(transform.position, radius, _dir, step, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (caster != null && hit.collider.transform.IsChildOf(caster.transform)) continue;
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null)
                {
                    if (_hit.Contains(h)) continue;
                    _hit.Add(h);
                    bool landed = SpellHit.Apply(h, caster, damage, hit.point, _dir, impact, 0f, 0f, 0f);
                    OnHit?.Invoke(landed, impact);
                    if (!pierce) { Burst(hit.point); return; }
                }
                else if (hit.distance > 0f)
                {
                    Burst(hit.point); // hit a wall or the ground
                    return;
                }
            }
            transform.position += _dir * step;
        }

        void Burst(Vector3 at)
        {
            SealBurst.Spawn(at, radius * 4f, new Color(1f, 0.55f, 0.2f));
            Destroy(gameObject);
        }
    }
}
