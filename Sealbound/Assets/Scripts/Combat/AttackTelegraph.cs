using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sealbound.Combat
{
    /// <summary>
    /// A glowing red outline around a creature that warns of an incoming attack.
    /// Built as a slightly larger "inside-out" copy of each body part (the classic toon-outline trick),
    /// so it stays readable from any angle without changing the creature's own colours.
    /// </summary>
    public class AttackTelegraph : MonoBehaviour
    {
        public Color warningColor = new Color(1f, 0.1f, 0.05f);
        public Color strikeColor = new Color(1f, 0.95f, 0.8f);
        [Tooltip("Outline thickness in metres.")]
        public float thickness = 0.045f;

        readonly List<Renderer> _outlines = new List<Renderer>();
        Material _material, _skinMaterial;
        bool _visible;

        public void Build(IEnumerable<Renderer> parts)
        {
            _material = CreateOutlineMaterial();
            foreach (var part in new List<Renderer>(parts))
            {
                if (Core.SkinnedOutline.IsOutline(part)) continue;
                if (part is SkinnedMeshRenderer skinned)
                {
                    // Animated body parts: an outline that bends with the same bones.
                    if (_skinMaterial == null) _skinMaterial = Core.SkinnedOutline.NewMaterial(warningColor, thickness * 0.7f);
                    var outline = Core.SkinnedOutline.Add(skinned, _skinMaterial, "Outline");
                    if (outline != null)
                    {
                        outline.enabled = false;
                        _outlines.Add(outline);
                    }
                    continue;
                }
                var mf = part.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (!Core.SkinnedOutline.IsBuiltInShape(mf.sharedMesh) && part is MeshRenderer still && Core.SkinnedOutline.Available)
                {
                    if (_skinMaterial == null) _skinMaterial = Core.SkinnedOutline.NewMaterial(warningColor, thickness * 0.7f);
                    var shell = Core.SkinnedOutline.AddStill(still, _skinMaterial, "Outline");
                    if (shell != null) { shell.enabled = false; _outlines.Add(shell); }
                    continue;
                }

                var go = new GameObject("Outline");
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
                r.enabled = false;
                _outlines.Add(r);
            }
        }

        /// <summary>0 = hidden, 0..1 = wind-up building (pulses faster near 1).</summary>
        public void SetWarning(float amount)
        {
            if (amount <= 0f) { Show(false); return; }
            Show(true);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 30f, amount));
            SetColor(warningColor * Mathf.Lerp(0.6f, 1.6f, amount) * pulse);
        }

        /// <summary>A bright flash the moment the attack lands.</summary>
        public void SetStrike()
        {
            Show(true);
            SetColor(strikeColor * 2f);
        }

        public void Hide() => Show(false);

        void Show(bool on)
        {
            if (_visible == on) return;
            _visible = on;
            foreach (var r in _outlines) if (r != null) r.enabled = on;
        }

        void SetColor(Color c)
        {
            if (_material == null) return;
            c.a = 1f;
            _material.color = c;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_material.HasProperty("_EmissionColor")) _material.SetColor("_EmissionColor", c);
            if (_skinMaterial != null) _skinMaterial.SetColor("_BaseColor", c);
        }

        Material CreateOutlineMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material m;
            if (shader != null) m = new Material(shader);
            else m = new Material(Core.Shapes.Material(warningColor, true));
            // Draw only the inside faces of the enlarged copy: only the rim shows around the body.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Front);
            m.renderQueue = (int)RenderQueue.Geometry + 10;
            m.color = warningColor;
            return m;
        }
    }
}
