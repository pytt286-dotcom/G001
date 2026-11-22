using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// A steel sword and a wooden staff built in code with realistic materials (polished steel, leather,
    /// brass, bark), for the realistic heroes. The grip is at the origin and the blade or staff head points up (+Y).
    /// </summary>
    public static class ForgedWeapons
    {
        static Material _steel, _leather, _brass, _wood;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _steel = null; _leather = null; _brass = null; _wood = null; }

        public static GameObject Sword(Transform parent)
        {
            var root = new GameObject("Sealer Sword");
            root.transform.SetParent(parent, false);

            // Blade first: it's the part that flashes on the beat.
            var blade = new GameObject("Blade");
            blade.transform.SetParent(root.transform, false);
            blade.AddComponent<MeshFilter>().sharedMesh = BladeMesh();
            blade.AddComponent<MeshRenderer>().sharedMaterial = new Material(Steel()) { name = "Sword steel" };

            Part(root.transform, PrimitiveType.Cube, "Crossguard", new Vector3(0f, 0.085f, 0f), new Vector3(0.2f, 0.024f, 0.034f), Brass());
            Part(root.transform, PrimitiveType.Cylinder, "Grip", new Vector3(0f, -0.01f, 0f), new Vector3(0.032f, 0.085f, 0.032f), Leather());
            Part(root.transform, PrimitiveType.Sphere, "Pommel", new Vector3(0f, -0.115f, 0f), new Vector3(0.048f, 0.04f, 0.048f), Brass());
            return root;
        }

        public static GameObject Staff(Transform parent)
        {
            var root = new GameObject("Sealer Staff");
            root.transform.SetParent(parent, false);
            // A slightly crooked shaft of three tapering pieces, held a third of the way up.
            Vector3 a = new Vector3(0f, -0.6f, 0f), b = new Vector3(0.012f, 0.05f, 0.006f), c = new Vector3(-0.01f, 0.65f, -0.008f), d = new Vector3(0.018f, 1.02f, 0.01f);
            Shaft(root.transform, a, b, 0.019f);
            Shaft(root.transform, b, c, 0.021f);
            Shaft(root.transform, c, d, 0.026f);
            // A carved head: a knot of wood cupping the ember (the ember itself is added by the hero).
            Part(root.transform, PrimitiveType.Sphere, "Knot", d + new Vector3(0f, 0.02f, 0f), new Vector3(0.075f, 0.06f, 0.075f), Wood());
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f;
                var prong = Part(root.transform, PrimitiveType.Capsule, "Prong", d + Quaternion.Euler(0f, ang, 0f) * new Vector3(0.032f, 0.07f, 0f),
                    new Vector3(0.022f, 0.06f, 0.022f), Wood());
                prong.transform.localRotation = Quaternion.Euler(0f, ang, -18f);
            }
            return root;
        }

        static void Shaft(Transform parent, Vector3 from, Vector3 to, float radius)
        {
            var p = Part(parent, PrimitiveType.Cylinder, "Shaft", (from + to) * 0.5f, new Vector3(radius * 2f, (to - from).magnitude * 0.5f, radius * 2f), Wood());
            p.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
        }

        static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = Shapes.Make(type, parent, name, pos, size, Color.white);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A double-edged blade with a raised centre ridge, flat-shaded so the edges catch the light.</summary>
        static Mesh _blade;
        static Mesh BladeMesh()
        {
            if (_blade != null) return _blade;
            // Cross-section: edge, ridge, edge, ridge (diamond). Rings at the base, near the tip, and the point.
            float[] ys = { 0.095f, 0.86f };
            float[] half = { 0.026f, 0.02f };
            float thick = 0.0065f;
            var ring = new List<Vector3[]>();
            for (int i = 0; i < ys.Length; i++)
                ring.Add(new[]
                {
                    new Vector3(half[i], ys[i], 0f), new Vector3(0f, ys[i], thick),
                    new Vector3(-half[i], ys[i], 0f), new Vector3(0f, ys[i], -thick),
                });
            Vector3 tip = new Vector3(0f, 1.0f, 0f);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s < 4; s++)
            {
                int n = (s + 1) % 4;
                Quad(verts, tris, ring[0][s], ring[1][s], ring[1][n], ring[0][n]);
                Tri(verts, tris, ring[1][s], tip, ring[1][n]);
            }
            // Base cap (hidden in the guard, but closes the shape).
            Quad(verts, tris, ring[0][3], ring[0][2], ring[0][1], ring[0][0]);

            _blade = new Mesh { name = "Sword Blade" };
            _blade.SetVertices(verts);
            _blade.SetTriangles(tris, 0);
            _blade.RecalculateNormals();
            _blade.RecalculateBounds();
            // Some faces may have come out inward: flip any whose normal points toward the blade's centre line.
            var normals = _blade.normals;
            var t = _blade.triangles;
            bool flipped = false;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 centre = (verts[t[i]] + verts[t[i + 1]] + verts[t[i + 2]]) / 3f;
                Vector3 axisPoint = new Vector3(0f, centre.y, 0f);
                if (Vector3.Dot(normals[t[i]], centre - axisPoint) < 0f && (centre - axisPoint).sqrMagnitude > 1e-8f)
                {
                    int swap = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = swap;
                    flipped = true;
                }
            }
            if (flipped)
            {
                _blade.triangles = t;
                _blade.RecalculateNormals();
            }
            return _blade;
        }

        static void Quad(List<Vector3> verts, List<int> tris, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int k = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
        }

        static void Tri(List<Vector3> verts, List<int> tris, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            int k = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }

        // ---------- Materials ----------

        static Material Lit(string name, Color c, float metal, float smooth, Texture tex = null, Texture normal = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shapes.Material(Color.white, false).shader) { name = name };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            if (normal != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            return m;
        }

        static Material Steel() => _steel != null ? _steel : (_steel = Lit("Steel", new Color(0.78f, 0.79f, 0.8f), 1f, 0.78f));
        static Material Leather() => _leather != null ? _leather : (_leather = Lit("Leather", new Color(0.2f, 0.12f, 0.07f), 0f, 0.3f));
        static Material Brass() => _brass != null ? _brass : (_brass = Lit("Brass", new Color(0.72f, 0.56f, 0.3f), 1f, 0.6f));
        static Material Wood() => _wood != null ? _wood : (_wood = Lit("Staff wood", new Color(0.55f, 0.45f, 0.36f), 0f, 0.2f,
            ArtLibrary.LoadTexture("Art/MegaKit/Textures/Bark_TwistedTree"), ArtLibrary.LoadTexture("Art/MegaKit/Textures/Bark_TwistedTree_Normal")));
    }
}
