using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sealbound.Core
{
    /// <summary>
    /// A thin dark cartoon outline around characters so they stand out from the background.
    /// Uses the same inside-out-copy trick as the red attack warning.
    /// </summary>
    public static class ToonOutline
    {
        static Material _material, _skinMaterial;
        static readonly Color Ink = new Color(0.08f, 0.06f, 0.05f);

        public static void Add(IEnumerable<Renderer> parts, float thickness = 0.022f)
        {
            if (_material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _material = shader != null ? new Material(shader) : new Material(Shapes.Material(Color.black, false));
                Color ink = new Color(0.08f, 0.06f, 0.05f);
                _material.color = ink;
                if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", ink);
                if (_material.HasProperty("_Cull")) _material.SetFloat("_Cull", (float)CullMode.Front);
            }

            var list = new List<Renderer>(parts);
            foreach (var part in list)
            {
                if (SkinnedOutline.IsOutline(part)) continue;
                if (part is SkinnedMeshRenderer skinned)
                {
                    // Animated characters: an outline that bends with the same bones.
                    if (_skinMaterial == null) _skinMaterial = SkinnedOutline.NewMaterial(Ink, thickness);
                    SkinnedOutline.Add(skinned, _skinMaterial, "Ink Outline");
                    continue;
                }
                var mf = part.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var go = new GameObject("Ink Outline");
                go.layer = part.gameObject.layer;
                go.transform.SetParent(part.transform, false);
                Vector3 s = part.transform.lossyScale;
                go.transform.localScale = new Vector3(
                    1f + 2f * thickness / Mathf.Max(0.01f, Mathf.Abs(s.x)),
                    1f + 2f * thickness / Mathf.Max(0.01f, Mathf.Abs(s.y)),
                    1f + 2f * thickness / Mathf.Max(0.01f, Mathf.Abs(s.z)));
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
