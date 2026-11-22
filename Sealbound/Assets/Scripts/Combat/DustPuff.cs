using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>A puff of dust and stone chips where something heavy hits the ground.</summary>
    public class DustPuff : MonoBehaviour
    {
        static readonly Color Dust = new Color(0.62f, 0.55f, 0.45f);

        Vector3 _velocity;
        float _age, _life, _size;

        public static void Spawn(Vector3 at, float radius, int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count + Random.Range(-0.3f, 0.3f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var go = Shapes.Make(PrimitiveType.Sphere, null, "Dust", Vector3.zero, Vector3.zero, Dust * Random.Range(0.85f, 1.1f));
                go.transform.position = at + dir * radius * 0.3f + Vector3.up * 0.2f;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = go.AddComponent<DustPuff>();
                p._velocity = dir * Random.Range(2.5f, 5f) * Mathf.Max(1f, radius * 0.4f) + Vector3.up * Random.Range(1f, 3f);
                p._life = Random.Range(0.5f, 0.9f);
                p._size = Random.Range(0.6f, 1.2f) * Mathf.Max(0.6f, radius * 0.35f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float t = _age / _life;
            _velocity = Vector3.Lerp(_velocity, Vector3.up * 0.5f, dt * 4f);
            transform.position += _velocity * dt;
            float s = t < 0.25f ? Mathf.Lerp(0.2f, 1f, t / 0.25f) : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * _size * s;
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
