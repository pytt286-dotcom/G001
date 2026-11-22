using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// The free, photo-scanned art (Poly Haven, CC0) and the painted nature models (Quaternius Stylized
    /// Nature MegaKit, CC0) that replace the cartoon look: real ground, rock and stone surfaces, scanned
    /// boulders, trees and grass. Everything falls back to the KayKit or grey-box look when missing.
    /// </summary>
    public static class RealArt
    {
        public const string Root = "Art/PolyHaven/";
        public const string Textures = Root + "Textures/";
        public const string Models = Root + "Models/";
        public const string Sky = Root + "Sky/kloofendal_48d_partly_cloudy_puresky";
        public const string MegaKit = "Art/MegaKit/Models/";

        // Surfaces (texture slugs).
        public const string Grass = "sparse_grass";
        public const string ForestGround = "forest_ground_04";
        public const string Trail = "rocky_trail";
        public const string Cliff = "rock_face_03";
        public const string CastleStone = "castle_wall_slates";
        public const string Blocks = "medieval_blocks_02";
        public const string GolemStone = "rock_pitted_mossy";
        public const string Moss = "mossy_rock";
        public const string Leaves = "forest_leaves_02";
        public const string DarkRock = "dark_rock";

        static bool? _has, _hasNature;
        static readonly Dictionary<string, Material> Surfaces = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> ScanMaterials = new Dictionary<string, Material>();
        static readonly Dictionary<Texture, Material> FoliageMaterials = new Dictionary<Texture, Material>();
        static Shader _triplanar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _has = null; _hasNature = null; _triplanar = null;
            Surfaces.Clear(); ScanMaterials.Clear(); FoliageMaterials.Clear();
        }

        /// <summary>True when the Poly Haven scans are in the project.</summary>
        public static bool Has
        {
            get
            {
                if (_has == null)
                    _has = ArtLibrary.LoadTexture(TexPath(Grass, "diff")) != null && TriplanarShader != null;
                return _has.Value;
            }
        }

        /// <summary>True when the Nature MegaKit trees and grass are in the project.</summary>
        public static bool HasNature
        {
            get
            {
                if (_hasNature == null) _hasNature = ArtLibrary.LoadModel(MegaKit + "CommonTree_1") != null;
                return _hasNature.Value;
            }
        }

        static Shader TriplanarShader
        {
            get
            {
                if (_triplanar == null)
                {
                    _triplanar = Resources.Load<Shader>("Shaders/SealboundTriplanar");
                    if (_triplanar == null) _triplanar = Shader.Find("Sealbound/Triplanar");
                }
                return _triplanar;
            }
        }

        public static string TexPath(string slug, string map) => Textures + slug + "/" + slug + "_" + map;
        static string ModelTexPath(string slug, string map) => Models + slug + "/" + slug + "_" + map;

        public static Texture2D Tex(string slug, string map) => ArtLibrary.LoadTexture(TexPath(slug, map));

        /// <summary>
        /// A scanned surface projected onto any shape from three sides, <paramref name="metres"/> per tile,
        /// with optional moss settling on the upward faces.
        /// </summary>
        public static Material Surface(string slug, float metres, float moss = 0f, Color? tint = null)
        {
            Color c = tint ?? Color.white;
            string key = slug + "|" + metres + "|" + moss + "|" + c;
            if (Surfaces.TryGetValue(key, out var m) && m != null) return m;
            if (TriplanarShader == null) return null;
            m = new Material(TriplanarShader) { name = slug + " (triplanar)" };
            SetMaps(m, "_BaseMap", "_NormalMap", "_ARMMap", Tex(slug, "diff"), Tex(slug, "nor"), Tex(slug, "arm"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Scale", metres);
            if (moss > 0f) AddMoss(m, moss);
            m.enableInstancing = true;
            Surfaces[key] = m;
            return m;
        }

        static void AddMoss(Material m, float amount)
        {
            SetMaps(m, "_TopMap", "_TopNormalMap", "_TopARMMap", Tex(Moss, "diff"), Tex(Moss, "nor"), Tex(Moss, "arm"));
            m.SetFloat("_TopScale", 2.5f);
            m.SetFloat("_TopAmount", amount);
            m.SetColor("_TopColor", new Color(0.85f, 0.95f, 0.8f));
        }

        static void SetMaps(Material m, string albedo, string normal, string arm, Texture a, Texture n, Texture r)
        {
            if (a != null) m.SetTexture(albedo, a);
            if (n != null) m.SetTexture(normal, n);
            if (r != null) m.SetTexture(arm, r);
        }

        /// <summary>
        /// Spawns a scanned model (rock, stump, shrub) with its own textures, scaled so its largest side is
        /// <paramref name="size"/> metres (0 keeps its real size). Returns null when it's missing.
        /// </summary>
        public static GameObject Scan(string slug, Transform parent, float size = 0f, float moss = 0f)
        {
            var prefab = ArtLibrary.LoadModel(Models + slug + "/" + slug);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = slug;
            var mat = ScanMaterial(slug, moss);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            if (size > 0f)
            {
                var b = ArtLibrary.WorldBounds(go);
                float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (biggest > 0.0001f) go.transform.localScale *= size / biggest;
            }
            return go;
        }

        /// <summary>A scanned model's own textures (UV mapped), with optional extra moss on top.</summary>
        public static Material ScanMaterial(string slug, float moss = 0f)
        {
            string key = slug + "|" + moss;
            if (ScanMaterials.TryGetValue(key, out var m) && m != null) return m;
            if (TriplanarShader == null) return Shapes.Material(Color.gray, false);
            m = new Material(TriplanarShader) { name = slug + " (scan)" };
            m.EnableKeyword("_USE_UV");
            m.SetFloat("_UseUV", 1f);
            SetMaps(m, "_BaseMap", "_NormalMap", "_ARMMap",
                ArtLibrary.LoadTexture(ModelTexPath(slug, "diff")), ArtLibrary.LoadTexture(ModelTexPath(slug, "nor")), ArtLibrary.LoadTexture(ModelTexPath(slug, "arm")));
            if (moss > 0f) AddMoss(m, moss);
            m.enableInstancing = true;
            ScanMaterials[key] = m;
            return m;
        }

        /// <summary>
        /// Spawns a Nature MegaKit model (tree, bush, grass) with lit materials: bark stays solid,
        /// leaves, grass and flowers are cut out along their texture's edges and seen from both sides.
        /// </summary>
        public static GameObject Nature(string name, Transform parent)
        {
            var prefab = ArtLibrary.LoadModel(MegaKit + name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = FoliageMaterial(mats[i]);
                r.sharedMaterials = mats;
            }
            return go;
        }

        public static Material FoliageMaterial(Material imported)
        {
            Texture tex = imported != null ? imported.mainTexture : null;
            string n = (imported != null ? imported.name : "") + " " + (tex != null ? tex.name : "");
            n = n.ToLowerInvariant();
            if (tex == null) tex = GuessNatureTexture(n);
            if (tex == null) return imported != null ? imported : Shapes.Material(new Color(0.3f, 0.45f, 0.2f), false);
            if (FoliageMaterials.TryGetValue(tex, out var m) && m != null) return m;

            bool cutout = IsCutout(tex.name.ToLowerInvariant()) || IsCutout(n);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            m = lit != null ? new Material(lit) : new Material(Shapes.Material(Color.white, false));
            m.name = tex.name + " (nature)";
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.color = Color.white;
            var normal = NatureNormal(tex.name);
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", cutout ? 0.25f : 0.08f);
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); // both sides
                m.doubleSidedGI = true;
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                m.SetOverrideTag("RenderType", "TransparentCutout");
            }
            m.enableInstancing = true;
            FoliageMaterials[tex] = m;
            return m;
        }

        static bool IsCutout(string n) => n.Contains("leaf") || n.Contains("leaves") || n.Contains("grass") || n.Contains("flower")
                                          || n.Contains("petal") || n.Contains("fern") || n.Contains("clover") || n.Contains("plant");

        static Texture GuessNatureTexture(string n)
        {
            string t = null;
            if (n.Contains("bark")) t = n.Contains("dead") ? "Bark_DeadTree" : n.Contains("twist") ? "Bark_TwistedTree" : "Bark_NormalTree";
            else if (n.Contains("pine")) t = "Leaf_Pine";
            else if (n.Contains("leaves") || n.Contains("leaf")) t = n.Contains("twist") ? "Leaves_TwistedTree" : "Leaves_NormalTree";
            else if (n.Contains("grass")) t = "Grass";
            else if (n.Contains("flower")) t = "Flowers";
            else if (n.Contains("mushroom")) t = "Mushrooms";
            return t == null ? null : ArtLibrary.LoadTexture("Art/MegaKit/Textures/" + t);
        }

        static Texture NatureNormal(string texName)
        {
            if (!texName.StartsWith("Bark_") || texName.EndsWith("_Normal")) return null;
            return ArtLibrary.LoadTexture("Art/MegaKit/Textures/" + texName + "_Normal");
        }

        /// <summary>
        /// Hides a box shape and fits a model inside it (stretched to the box, long sides matched).
        /// The box's collider stays. Like <see cref="ArtLibrary.FitInsideBox"/>, but for any spawned model.
        /// </summary>
        public static GameObject FitInsideBox(Transform box, GameObject model, float overfill = 1f)
        {
            var r = box.GetComponent<Renderer>();
            if (model == null) return null;
            var t = model.transform;
            t.SetParent(box, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            Bounds lb = BoneAim.LocalMeshBounds(t);
            Vector3 size = lb.size;
            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f) { Object.Destroy(model); return null; }

            // Match the model's longest side to the box's longest side.
            Vector3 boxSize = box.lossyScale;
            int boxLong = LongAxis(boxSize), modelLong = LongAxis(size);
            Quaternion rot = Quaternion.identity;
            if (boxLong != modelLong)
            {
                Vector3 from = Vector3.zero; from[modelLong] = 1f;
                Vector3 to = Vector3.zero; to[boxLong] = 1f;
                rot = Quaternion.FromToRotation(from, to);
            }
            Vector3 rotSize = Abs(rot * size);
            Vector3 scale = new Vector3(overfill / rotSize.x, overfill / rotSize.y, overfill / rotSize.z);
            // The model is rotated, then scaled in the box's axes: put the scale on a holder so it stays axis-aligned.
            var holder = new GameObject(model.name + " (fitted)").transform;
            holder.gameObject.layer = box.gameObject.layer;
            holder.SetParent(box, false);
            holder.localScale = scale;
            t.SetParent(holder, false);
            t.localRotation = rot;
            t.localScale = Vector3.one;
            t.localPosition = -(rot * lb.center);
            if (r != null) r.enabled = false;
            return holder.gameObject;
        }

        static int LongAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
