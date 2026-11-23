using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>Builds simple coloured stand-in shapes until real art arrives.</summary>
    public static class Shapes
    {
        static readonly Dictionary<(Color, bool), Material> Cache = new Dictionary<(Color, bool), Material>();
        static Material _base;

        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 size,
            Color color, bool keepCollider = false, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Paint(go, color, glow);
            return go;
        }

        public static void Paint(GameObject go, Color color, bool glow = false)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = Material(color, glow, r.sharedMaterial);
        }

        public static Material Material(Color color, bool glow, Material template = null)
        {
            if (_base == null)
            {
                if (template == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    template = tmp.GetComponent<Renderer>().sharedMaterial;
                    Object.Destroy(tmp);
                }
                _base = template;
            }
            if (!Cache.TryGetValue((color, glow), out var mat) || mat == null)
            {
                mat = new Material(_base) { color = color };
                if (glow && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 2f);
                }
                Cache[(color, glow)] = mat;
            }
            return mat;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
