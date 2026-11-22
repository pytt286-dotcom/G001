using System.Collections.Generic;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// A low wall of force that races outward along the ground from a stomp.
    /// Jump over it or dodge through it; standing on the ground when it passes hurts and throws you up.
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        public float speed = 16f;
        public float maxRadius = 15f;
        public float height = 0.8f;
        public float damage = 20f;
        public float launch = 8f;
        public GameObject source;

        static readonly Color Glow = new Color(1f, 0.45f, 0.15f);
        float _radius = 0.5f;
        Mesh _mesh;
        readonly HashSet<Health> _hit = new HashSet<Health>();
        Health _player;
        PlayerMotor _motor;
        PlayerCombat _combat;

        public static Shockwave Spawn(Vector3 centre, float speed, float maxRadius, float damage, GameObject source)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = new Vector3(centre.x, 0f, centre.z);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Shapes.Material(Glow, true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var s = go.AddComponent<Shockwave>();
            s.speed = speed; s.maxRadius = maxRadius; s.damage = damage; s.source = source;
            return s;
        }

        void Start()
        {
            _mesh = new Mesh { name = "Shockwave Ring" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _motor = FindAnyObjectByType<PlayerMotor>();
            if (_motor != null)
            {
                _player = _motor.GetComponent<Health>();
                _combat = _motor.GetComponent<PlayerCombat>();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float before = _radius;
            _radius += speed * dt;
            float fade = 1f - Mathf.Clamp01((_radius - maxRadius * 0.75f) / (maxRadius * 0.25f));
            BuildRing(_radius, 0.5f + 0.4f * fade, height * (0.4f + 0.6f * fade));

            if (_player != null && !_hit.Contains(_player))
            {
                Vector3 p = _motor.transform.position;
                float d = new Vector2(p.x - transform.position.x, p.z - transform.position.z).magnitude;
                bool crossed = d >= before - 0.6f && d <= _radius + 0.3f;
                bool low = p.y - transform.position.y < height * 0.9f;
                if (crossed && low)
                {
                    _hit.Add(_player);
                    Vector3 dir = p - transform.position; dir.y = 0f;
                    var info = new DamageInfo
                    {
                        Amount = damage, Point = p, Direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward,
                        Source = source, Impact = 0.7f
                    };
                    if (_player.TryDamage(info, out _)) _motor.Launch(launch);
                    else if (_combat != null) _combat.NotifyDodged();
                }
            }
            if (_radius >= maxRadius) Destroy(gameObject);
        }

        void BuildRing(float radius, float thickness, float h)
        {
            // A low ridge: outer wall, sloped top, inner wall. Built twice (once per side) so it shows from anywhere.
            const int n = 72;
            var v = new Vector3[n * 8];
            var tris = new int[n * 36];
            float inner = Mathf.Max(0.05f, radius - thickness);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 4 + 0] = d * radius;
                v[i * 4 + 1] = d * radius + Vector3.up * h * 0.6f;
                v[i * 4 + 2] = d * inner + Vector3.up * h;
                v[i * 4 + 3] = d * inner;
            }
            System.Array.Copy(v, 0, v, n * 4, n * 4);
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int a = i * 4, b = ((i + 1) % n) * 4;
                for (int s = 0; s < 3; s++)
                {
                    tris[k++] = a + s; tris[k++] = a + s + 1; tris[k++] = b + s;
                    tris[k++] = b + s; tris[k++] = a + s + 1; tris[k++] = b + s + 1;
                    int o = n * 4;
                    tris[k++] = o + a + s; tris[k++] = o + b + s; tris[k++] = o + a + s + 1;
                    tris[k++] = o + b + s; tris[k++] = o + b + s + 1; tris[k++] = o + a + s + 1;
                }
            }
            _mesh.Clear();
            _mesh.vertices = v;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
