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
        Glow _glow;
        float _lightPeak;

        public static void Spawn(Vector3 position, float size, Color color)
        {
            SealBurst b;
            if (RealArt.Has)
            {
                // Realistic look: a quick soft flare that lights up whatever is around it,
                // instead of a solid glowing ball.
                var go = new GameObject("Seal Burst");
                go.layer = 2;
                go.transform.position = position;
                b = go.AddComponent<SealBurst>();
                b._life = 0.26f;
                b._size = Mathf.Clamp(size * 0.9f, 0.8f, 5f);
                b._glow = Glow.Make(go.transform, Vector3.zero, 1f, new Color(color.r * 2f, color.g * 2f, color.b * 2f, 0.9f));
                b._light = go.AddComponent<Light>();
                b._light.type = LightType.Point;
                b._light.color = color;
                b._light.range = Mathf.Clamp(size * 2.2f, 3f, 14f);
                b._light.shadows = LightShadows.None;
                b._lightPeak = 5f;
                b._light.intensity = b._lightPeak;
                go.transform.localScale = Vector3.zero;
                return;
            }
            var ball = Shapes.Make(PrimitiveType.Sphere, null, "Seal Burst", position, Vector3.zero, color, false, true);
            ball.transform.position = position;
            b = ball.AddComponent<SealBurst>();
            b._size = size;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            float s = t < 0.4f ? Mathf.Lerp(0f, _size, t / 0.4f) : Mathf.Lerp(_size, 0f, (t - 0.4f) / 0.6f);
            if (_glow != null)
            {
                // The flare grows quickly and fades out rather than shrinking back.
                transform.localScale = Vector3.one * Mathf.Lerp(_size * 0.4f, _size, Mathf.Sqrt(Mathf.Clamp01(t)));
                _glow.SetStrength(1f - t);
            }
            else transform.localScale = Vector3.one * s;
            if (_light != null) _light.intensity = _lightPeak * Mathf.Clamp01(1f - t * 0.8f);
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
