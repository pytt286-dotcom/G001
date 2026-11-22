using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// Loads the free CC0 art (KayKit by Kay Lousberg) from Assets/Resources/Art and gives it
    /// a consistent look. Everything falls back to the grey-box shapes when the art is missing,
    /// so the game still runs on a copy without the art folder.
    /// </summary>
    public static class ArtLibrary
    {
        public const string Root = "Art/KayKit/";
        public const string Characters = Root + "Adventurers/Characters/";
        public const string Animations = Root + "Adventurers/Animations/";
        public const string Props = Root + "Adventurers/Props/";
        public const string AdventurerTextures = Root + "Adventurers/Textures/";
        public const string Forest = Root + "Forest/";
        public const string Dungeon = Root + "Dungeon/";

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        static readonly Dictionary<Texture, Material> TexturedMaterials = new Dictionary<Texture, Material>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static List<AnimationClip> _clips;
        static bool? _hasCharacters, _hasScenery;

        static readonly string[] ClassTextures = { "knight", "mage", "barbarian", "rogue", "ranger" };

        /// <summary>True when the KayKit adventurers and their animations are in the project.</summary>
        public static bool HasCharacters
        {
            get
            {
                if (_hasCharacters == null)
                    _hasCharacters = LoadModel(Characters + "Knight") != null && AllClips().Count > 0;
                return _hasCharacters.Value;
            }
        }

        /// <summary>True when the KayKit forest and dungeon pieces are in the project.</summary>
        public static bool HasScenery
        {
            get
            {
                if (_hasScenery == null)
                    _hasScenery = LoadModel(Forest + "Tree_1_A_Color1") != null && LoadModel(Dungeon + "wall") != null;
                return _hasScenery.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Models.Clear();
            TexturedMaterials.Clear();
            Textures.Clear();
            _clips = null;
            _hasCharacters = null;
            _hasScenery = null;
        }

        public static GameObject LoadModel(string path)
        {
            if (!Models.TryGetValue(path, out var prefab))
            {
                prefab = Resources.Load<GameObject>(path);
                Models[path] = prefab;
            }
            return prefab;
        }

        public static Texture2D LoadTexture(string path)
        {
            if (!Textures.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                Textures[path] = tex;
            }
            return tex;
        }

        /// <summary>All KayKit Rig_Medium animations (idle, walk, run, jump, hit, death, throw...).</summary>
        public static List<AnimationClip> AllClips()
        {
            if (_clips != null) return _clips;
            _clips = new List<AnimationClip>();
            foreach (var file in new[] { "Rig_Medium_General", "Rig_Medium_MovementBasic" })
                foreach (var c in Resources.LoadAll<AnimationClip>(Animations + file))
                    if (c != null && !c.name.StartsWith("__preview__") && !_clips.Contains(c)) _clips.Add(c);
            return _clips;
        }

        /// <summary>The colour sheet for a KayKit class ("knight", "mage", "barbarian", "rogue", "ranger").</summary>
        public static Texture2D ClassTexture(string className) =>
            LoadTexture(AdventurerTextures + className.ToLowerInvariant() + "_texture");

        /// <summary>One shared lit material per colour sheet, matching the rest of the world's lighting.</summary>
        public static Material TexturedMaterial(Texture tex)
        {
            if (tex == null) return Shapes.Material(Color.white, false);
            if (!TexturedMaterials.TryGetValue(tex, out var mat) || mat == null)
            {
                mat = new Material(Shapes.Material(Color.white, false)) { name = tex.name + " (Sealbound)" };
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.color = Color.white;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                mat.enableInstancing = true;
                TexturedMaterials[tex] = mat;
            }
            return mat;
        }

        /// <summary>
        /// Spawns a model and gives every part the right colour sheet. When <paramref name="sheet"/> is null,
        /// the sheet is worked out from the model's own material, or the pack it comes from.
        /// </summary>
        public static GameObject Spawn(string path, Transform parent, Texture sheet = null)
        {
            var prefab = LoadModel(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            ApplySheet(go, sheet != null ? sheet : GuessSheet(path, go));
            return go;
        }

        public static void ApplySheet(GameObject go, Texture sheet)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                Texture use = sheet;
                if (use == null && mats.Length > 0 && mats[0] != null) use = mats[0].mainTexture;
                var m = TexturedMaterial(use);
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        static Texture GuessSheet(string path, GameObject go)
        {
            if (path.StartsWith(Forest)) return LoadTexture(Forest + "forest_texture");
            if (path.StartsWith(Dungeon)) return LoadTexture(Dungeon + "dungeon_texture");

            // Adventurer props: use the texture the importer found, or match the material's name.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.mainTexture != null) return m.mainTexture;
                    string n = m.name.ToLowerInvariant();
                    foreach (var c in ClassTextures)
                        if (n.Contains(c)) return ClassTexture(c);
                }

            string p = path.ToLowerInvariant();
            if (p.Contains("staff") || p.Contains("wand") || p.Contains("spellbook")) return ClassTexture("mage");
            if (p.Contains("axe") || p.Contains("barbarian")) return ClassTexture("barbarian");
            if (p.Contains("dagger") || p.Contains("crossbow") || p.Contains("smokebomb")) return ClassTexture("rogue");
            if (p.Contains("bow") || p.Contains("arrow") || p.Contains("quiver")) return ClassTexture("ranger");
            return ClassTexture("knight");
        }

        // ---------- Placement helpers ----------

        public static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Scales a model uniformly so it is <paramref name="height"/> metres tall, feet at its origin.</summary>
        public static float FitHeight(GameObject go, float height)
        {
            var b = WorldBounds(go);
            if (b.size.y < 0.001f) return 1f;
            float s = height / b.size.y;
            go.transform.localScale *= s;
            b = WorldBounds(go);
            float feetY = go.transform.parent != null ? go.transform.parent.position.y : 0f;
            go.transform.position += Vector3.up * (feetY - b.min.y);
            return s;
        }

        /// <summary>Moves a placed model up or down so its lowest point sits at <paramref name="y"/>.</summary>
        public static void SetBottom(GameObject go, float y)
        {
            var b = WorldBounds(go);
            go.transform.position += Vector3.up * (y - b.min.y);
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ---------- Recolouring ----------

        /// <summary>
        /// Copies a character colour sheet and repaints the shared skin swatch (the peach cell in the
        /// top-left corner of every KayKit adventurer sheet) to another colour, e.g. goblin green.
        /// </summary>
        public static Texture2D RepaintSkin(Texture2D source, float hue, float saturationScale, float valueScale)
        {
            if (source == null) return null;
            if (!source.isReadable) return source; // keep the original colours
            Color32[] px;
            try { px = source.GetPixels32(); }
            catch (UnityException) { return source; } // not readable: keep the original colours

            int w = source.width, h = source.height;
            int cellW = w / 8, cellTop = h - h / 4; // top-left 128 x 256 cell on a 1024 sheet
            for (int y = cellTop; y < h; y++)
                for (int x = 0; x < cellW; x++)
                {
                    int i = y * w + x;
                    Color c = px[i];
                    Color.RGBToHSV(c, out float hh, out float s, out float v);
                    if (s < 0.12f || hh > 0.12f) continue; // only warm skin tones
                    Color g = Color.HSVToRGB(hue, Mathf.Clamp01(s * saturationScale), Mathf.Clamp01(v * valueScale));
                    g.a = c.a;
                    px[i] = g;
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = source.name + " (repainted)" };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>The colour that the skin swatch's middle tone turns into with <see cref="RepaintSkin"/>.</summary>
        public static Color RepaintedSkinColor(float hue, float saturationScale, float valueScale)
        {
            Color.RGBToHSV(new Color(0.96f, 0.74f, 0.6f), out _, out float s, out float v);
            return Color.HSVToRGB(hue, Mathf.Clamp01(s * saturationScale), Mathf.Clamp01(v * valueScale));
        }
    }
}
