using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// A soft, camera-facing glow (a bright centre fading to nothing at the edge), drawn additively.
    /// Used for embers and flashes in the realistic look, where a solid glowing ball reads as a flat disc.
    /// </summary>
    public class Glow : MonoBehaviour
    {
        static Texture2D _soft, _ring;
        static Mesh _quad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _soft = null; _ring = null; _quad = null; }

        Material _material;
        Color _color;
        bool _flat;

        /// <summary>Adds a glow <paramref name="size"/> metres across under <paramref name="parent"/>.</summary>
        public static Glow Make(Transform parent, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Glow");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var g = go.AddComponent<Glow>();
            g._color = color;
            g._material = NewMaterial(color);
            r.sharedMaterial = g._material;
            return g;
        }

        /// <summary>
        /// A glowing circle of runes lying flat on the ground, <paramref name="size"/> metres across
        /// (a sealing circle, or a marker).
        /// </summary>
        public static Glow MakeRing(Transform parent, Vector3 localPos, float size, Color color)
        {
            var g = Make(parent, localPos, size, color);
            g._flat = true;
            g.name = "Rune Circle";
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g._material.mainTexture = Ring();
            if (g._material.HasProperty("_BaseMap")) g._material.SetTexture("_BaseMap", Ring());
            return g;
        }

        /// <summary>Fades the glow: 1 is full strength, 0 is gone.</summary>
        public void SetStrength(float k)
        {
            if (_material != null) _material.color = new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Clamp01(k));
        }

        void LateUpdate()
        {
            if (_flat) return;
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        static Material NewMaterial(Color color)
        {
            Material m;
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particles != null)
            {
                // Additive: light adds up instead of covering what is behind it.
                m = new Material(particles) { name = "Glow" };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Soft());
            }
            else
            {
                m = new Material(Shader.Find("Sprites/Default")) { name = "Glow" }; // premultiplied blend
            }
            m.mainTexture = Soft();
            m.color = color;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            return m;
        }

        static Texture2D Soft()
        {
            if (_soft != null) return _soft;
            const int n = 64;
            _soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Soft Glow", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (0.35f + 0.65f * a * a); // a hot centre with a long soft falloff
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, b);
                }
            _soft.SetPixels32(px);
            _soft.Apply(false, true);
            return _soft;
        }

        static Texture2D Ring()
        {
            if (_ring != null) return _ring;
            const int n = 256;
            _ring = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Rune Ring", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float a = Mathf.Exp(-Mathf.Pow((r - 0.92f) / 0.018f, 2f));         // outer line
                    a += 0.8f * Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.012f, 2f));        // inner line
                    // A band of rune marks between the lines: short strokes at irregular angles.
                    float seg = ang / (2f * Mathf.PI) * 24f;
                    float cell = seg - Mathf.Floor(seg);
                    int idx = Mathf.FloorToInt(seg + 24f) % 24;
                    float h = Mathf.Abs(Mathf.Sin(idx * 12.9898f) * 43758.5453f) % 1f;
                    bool stroke = Mathf.Abs(cell - 0.5f) < 0.06f + 0.05f * h || (h > 0.5f && Mathf.Abs(r - 0.86f) < 0.01f && cell > 0.2f && cell < 0.8f);
                    if (r > 0.82f && r < 0.9f && stroke) a += 0.9f;
                    a += 0.18f * Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.08f, 2f));       // soft glow under it all
                    a = Mathf.Clamp01(a) * (r < 1f ? 1f : 0f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _ring.SetPixels32(px);
            _ring.Apply(true, true);
            return _ring;
        }

        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Glow Quad" };
            _quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
            _quad.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            _quad.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0); // faces the camera (it is turned to match the camera each frame)
            _quad.RecalculateBounds();
            return _quad;
        }
    }
}
