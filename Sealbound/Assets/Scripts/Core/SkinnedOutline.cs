using UnityEngine;
using UnityEngine.Rendering;

namespace Sealbound.Core
{
    /// <summary>
    /// Outlines for animated characters. A bent, animated mesh can't use the "scaled copy" trick
    /// that the box shapes use, so this draws a copy that follows the same bones with the
    /// Sealbound/Outline shader, which puffs the mesh out by a fixed width in metres.
    /// </summary>
    public static class SkinnedOutline
    {
        static Shader _shader;
        static bool _looked;

        public static Shader Shader
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _shader = Resources.Load<Shader>("Shaders/SealboundOutline");
                    if (_shader == null) _shader = Shader.Find("Sealbound/Outline");
                    if (_shader != null && !_shader.isSupported) _shader = null;
                }
                return _shader;
            }
        }

        public static bool Available => Shader != null;

        public static Material NewMaterial(Color color, float width)
        {
            if (Shader == null) return null;
            var m = new Material(Shader) { name = "Sealbound Outline" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Width", width);
            return m;
        }

        public static SkinnedMeshRenderer Add(SkinnedMeshRenderer source, Material material, string name)
        {
            if (source == null || material == null || source.sharedMesh == null) return null;
            var go = new GameObject(name);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;

            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = source.sharedMesh;
            r.rootBone = source.rootBone;
            r.bones = source.bones;
            r.localBounds = source.localBounds;
            var mats = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            return r;
        }

        public static bool IsOutline(Renderer r) =>
            r == null || r.name == "Outline" || r.name == "Ink Outline" || r is TrailRenderer || r is ParticleSystemRenderer;
    }
}
