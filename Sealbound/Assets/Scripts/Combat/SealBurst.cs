using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>A quick glowing flash where a creature is sealed.</summary>
    public class SealBurst : MonoBehaviour
    {
        float _age;
        float _size;
        float _life = 0.35f;
        Light _light;
        float _lightPeak;

        public static void Spawn(Vector3 position, float size, Color color)
        {
            // Realistic look: a short, smaller flash that lights up whatever is around it,
            // instead of a big solid glowing ball.
            bool real = RealArt.Has;
            var go = Shapes.Make(PrimitiveType.Sphere, null, "Seal Burst", position, Vector3.zero,
                real ? Color.Lerp(color, Color.white, 0.3f) : color, false, true);
            go.transform.position = position;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = go.AddComponent<SealBurst>();
            b._size = real ? Mathf.Min(size * 0.35f, 1.4f) : size;
            if (real)
            {
                b._life = 0.22f;
                b._light = go.AddComponent<Light>();
                b._light.type = LightType.Point;
                b._light.color = color;
                b._light.range = Mathf.Clamp(size * 2.2f, 3f, 14f);
                b._light.shadows = LightShadows.None;
                b._lightPeak = 5f;
                b._light.intensity = b._lightPeak;
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            float s = t < 0.4f ? Mathf.Lerp(0f, _size, t / 0.4f) : Mathf.Lerp(_size, 0f, (t - 0.4f) / 0.6f);
            transform.localScale = Vector3.one * s;
            if (_light != null) _light.intensity = _lightPeak * Mathf.Clamp01(1f - t * 0.8f);
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
