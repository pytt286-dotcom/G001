using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>A quick glowing flash where a creature is sealed.</summary>
    public class SealBurst : MonoBehaviour
    {
        float _age;
        float _size;
        const float Life = 0.35f;

        public static void Spawn(Vector3 position, float size, Color color)
        {
            var go = Shapes.Make(PrimitiveType.Sphere, null, "Seal Burst", position, Vector3.zero, color, false, true);
            go.transform.position = position;
            var b = go.AddComponent<SealBurst>();
            b._size = size;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            float s = t < 0.4f ? Mathf.Lerp(0f, _size, t / 0.4f) : Mathf.Lerp(_size, 0f, (t - 0.4f) / 0.6f);
            transform.localScale = Vector3.one * s;
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
