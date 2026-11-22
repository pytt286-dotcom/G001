using System.Collections.Generic;
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
            r.sharedMesh = Smoothed(source.sharedMesh);
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

        /// <summary>
        /// Outline shell for a still mesh (a weapon, an ear): a child that draws the same mesh puffed out.
        /// </summary>
        public static MeshRenderer AddStill(MeshRenderer source, Material material, string name)
        {
            var mf = source != null ? source.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null || material == null) return null;
            var go = new GameObject(name);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Smoothed(mf.sharedMesh);
            var r = go.AddComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            return r;
        }

        /// <summary>Unity's own cube, sphere, capsule... These keep the older "scaled copy" outline.</summary>
        public static bool IsBuiltInShape(Mesh m) =>
            m != null && (m.name == "Cube" || m.name == "Sphere" || m.name == "Capsule" || m.name == "Cylinder" || m.name == "Quad" || m.name == "Plane");

        static readonly Dictionary<Mesh, Mesh> SmoothCache = new Dictionary<Mesh, Mesh>();

        /// <summary>
        /// A copy of the mesh whose normals are averaged across hard edges, so the puffed-out outline
        /// stays one closed shell instead of splitting into spikes at sharp corners.
        /// </summary>
        public static Mesh Smoothed(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return mesh;
            if (SmoothCache.TryGetValue(mesh, out var copy) && copy != null) return copy;

            copy = Object.Instantiate(mesh);
            copy.name = mesh.name + " (outline)";
            var verts = copy.vertices;
            var normals = copy.normals;
            if (normals == null || normals.Length != verts.Length) { SmoothCache[mesh] = mesh; return mesh; }

            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(verts[i].x * 10000f), Mathf.RoundToInt(verts[i].y * 10000f), Mathf.RoundToInt(verts[i].z * 10000f));
                keys[i] = k;
                sums.TryGetValue(k, out var n);
                sums[k] = n + normals[i];
            }
            for (int i = 0; i < verts.Length; i++)
            {
                var n = sums[keys[i]];
                normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : normals[i];
            }
            copy.normals = normals;
            SmoothCache[mesh] = copy;
            return copy;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SmoothCache.Clear();

        public static bool IsOutline(Renderer r) =>
            r == null || r.name == "Outline" || r.name == "Ink Outline" || r is TrailRenderer || r is ParticleSystemRenderer;
    }
}
