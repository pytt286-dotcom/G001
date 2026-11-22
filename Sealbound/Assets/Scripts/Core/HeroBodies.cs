using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// Realistic-proportion heroes from Quaternius' free CC0 "Universal" line: an outfit (Ranger) worn on a
    /// shared humanoid skeleton, a face taken from the base character, hair, and the Universal Animation
    /// Libraries 1 and 2. Everything is assembled at start-up, so swapping outfits later is easy.
    /// </summary>
    public static class HeroBodies
    {
        public const string Root = "Art/Quaternius/";
        public const string Characters = Root + "Characters/";
        public const string Animations = Root + "Animations/";
        public const string Textures = Root + "Textures/";

        /// <summary>Names of the clips and bones a body uses, so the same code drives either art set.</summary>
        public class Profile
        {
            public string idle, combatIdle, walk, run, sprint, jumpLoop, jumpLand, hitA, hitB, death, respawn, dodge;
            public string upperArmR = "upperarm.r", lowerArmR = "lowerarm.r", handR = "handslot.r", chest = "chest", head = "head";
            public float walkSpeed = 1.7f, runSpeed = 4.5f, sprintSpeed = 7f;
            public bool realistic;
        }

        public static readonly Profile KayKit = new Profile
        {
            idle = "Idle_A", combatIdle = "Idle_A", walk = "Walking_A", run = "Running_A", sprint = "Running_B",
            jumpLoop = "Jump_Idle", jumpLand = "Jump_Land", hitA = "Hit_A", hitB = "Hit_B", death = "Death_A",
            respawn = "Spawn_Ground", dodge = null,
        };

        public static readonly Profile Universal = new Profile
        {
            idle = "Idle_Loop", combatIdle = "Sword_Idle", walk = "Walk_Loop", run = "Jog_Fwd_Loop", sprint = "Sprint_Loop",
            jumpLoop = "Jump_Loop", jumpLand = "Jump_Land", hitA = "Hit_Chest", hitB = "Hit_Head", death = "Death01",
            respawn = "LayToIdle", dodge = "Roll",
            upperArmR = "upperarm_r", lowerArmR = "lowerarm_r", handR = "hand_r", chest = "spine_03", head = "Head",
            walkSpeed = 1.45f, runSpeed = 3.6f, sprintSpeed = 6.2f, realistic = true,
        };

        static bool? _has;
        static List<AnimationClip> _clips;
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _has = null; _clips = null; Materials.Clear(); }

        public static bool Has
        {
            get
            {
                if (_has == null)
                    _has = ArtLibrary.LoadModel(Characters + "Male_Ranger") != null && ArtLibrary.LoadModel(Characters + "Superhero_Male_FullBody") != null
                           && Clips().Count > 0;
                return _has.Value;
            }
        }

        public static List<AnimationClip> Clips()
        {
            if (_clips != null) return _clips;
            _clips = new List<AnimationClip>();
            var seen = new HashSet<string>();
            foreach (var file in new[] { "UAL1_Standard", "UAL2_Standard" })
                foreach (var c in Resources.LoadAll<AnimationClip>(Animations + file))
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    string shortName = c.name.Substring(c.name.LastIndexOf('|') + 1);
                    if (seen.Add(shortName)) _clips.Add(c); // first file wins (A_TPose is in both)
                }
            return _clips;
        }

        /// <summary>
        /// Builds a hero under <paramref name="parent"/>: the Ranger outfit, a face and hands from the base
        /// character, and hair. <paramref name="female"/> picks the body; <paramref name="hood"/> keeps the hood up.
        /// </summary>
        public static GameObject Build(Transform parent, bool female, bool hood, string outfitColour = "T_Ranger_BaseColor")
        {
            var outfitPrefab = ArtLibrary.LoadModel(Characters + (female ? "Female_Ranger" : "Male_Ranger"));
            var basePrefab = ArtLibrary.LoadModel(Characters + (female ? "Superhero_Female_FullBody" : "Superhero_Male_FullBody"));
            if (outfitPrefab == null || basePrefab == null) return null;

            var hero = Object.Instantiate(outfitPrefab, parent, false);
            hero.name = female ? "Hero (female, Quaternius)" : "Hero (male, Quaternius)";
            var bones = BoneMap(hero.transform);

            // Outfit pieces: the hood is optional; everything wears the outfit or skin material.
            foreach (var smr in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name.Contains("Hood") && !hood) { smr.gameObject.SetActive(false); continue; }
                AssignMaterials(smr, female, outfitColour);
            }

            // The face (and eyes and eyebrows) come from the base character, moved onto the outfit's skeleton.
            var donor = Object.Instantiate(basePrefab);
            foreach (var smr in donor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool body = smr.name.ToLowerInvariant().StartsWith("superhero");
                if (body)
                {
                    var head = HeadOnly(smr);
                    if (head == null) continue;
                    smr.sharedMesh = head;
                }
                if (!Rebind(smr, bones, hero.transform)) continue;
                AssignMaterials(smr, female, outfitColour);
            }
            Object.Destroy(donor);

            // Hair when the hood is down.
            if (!hood)
            {
                var hairPrefab = ArtLibrary.LoadModel(Characters + (female ? "Hair_Long" : "Hair_SimpleParted"));
                if (hairPrefab != null)
                {
                    var hair = Object.Instantiate(hairPrefab);
                    foreach (var smr in hair.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (Rebind(smr, bones, hero.transform)) AssignMaterials(smr, female, outfitColour);
                    Object.Destroy(hair);
                }
            }
            return hero;
        }

        static Dictionary<string, Transform> BoneMap(Transform root)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name)) map[t.name] = t;
            return map;
        }

        /// <summary>Moves a skinned mesh onto another copy of the same skeleton (matching bones by name).</summary>
        static bool Rebind(SkinnedMeshRenderer smr, Dictionary<string, Transform> bones, Transform newParent)
        {
            var old = smr.bones;
            var mapped = new Transform[old.Length];
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] == null || !bones.TryGetValue(old[i].name, out mapped[i])) return false;
            }
            Transform rootBone = smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out var rb) ? rb : null;
            smr.transform.SetParent(newParent, false);
            smr.bones = mapped;
            if (rootBone != null) smr.rootBone = rootBone;
            smr.updateWhenOffscreen = true;
            return true;
        }

        /// <summary>A copy of the base body keeping only the head and neck (the outfit covers the rest).</summary>
        static Mesh HeadOnly(SkinnedMeshRenderer smr)
        {
            var src = smr.sharedMesh;
            if (src == null || !src.isReadable) return null;
            var keep = new bool[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
                keep[i] = smr.bones[i] != null && (smr.bones[i].name == "Head" || smr.bones[i].name == "neck_01");
            var weights = src.boneWeights;
            if (weights.Length != src.vertexCount) return null;

            var mesh = Object.Instantiate(src);
            mesh.name = src.name + " (head)";
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                var tris = src.GetTriangles(sub);
                var kept = new List<int>(tris.Length / 8);
                for (int t = 0; t < tris.Length; t += 3)
                    if (HeadVertex(weights[tris[t]], keep) && HeadVertex(weights[tris[t + 1]], keep) && HeadVertex(weights[tris[t + 2]], keep))
                    {
                        kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                    }
                mesh.SetTriangles(kept, sub);
            }
            return mesh;
        }

        static bool HeadVertex(BoneWeight w, bool[] keep)
        {
            int b = w.boneIndex0; float best = w.weight0;
            if (w.weight1 > best) { b = w.boneIndex1; best = w.weight1; }
            if (w.weight2 > best) { b = w.boneIndex2; best = w.weight2; }
            if (w.weight3 > best) { b = w.boneIndex3; }
            return b >= 0 && b < keep.Length && keep[b];
        }

        // ---------- Materials ----------

        static void AssignMaterials(Renderer r, bool female, string outfitColour)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : r.name.ToLowerInvariant();
                Material m;
                if (n.Contains("eye") && !n.Contains("brow")) m = Lit("eyes", "T_Eye_Brown", "T_Eye_Normal", null, 0.75f, false);
                else if (n.Contains("hair") || n.Contains("brow")) m = Lit(n.Contains("2") ? "hair2" : "hair1",
                    n.Contains("2") ? "T_Hair_2_BaseColor" : "T_Hair_1_BaseColor", n.Contains("2") ? "T_Hair_2_Normal" : "T_Hair_1_Normal", null, 0.3f, true);
                else if (n.Contains("superhero")) m = Lit(female ? "faceF" : "faceM",
                    female ? "T_Superhero_Female_Dark_BaseColor" : "T_Superhero_Male_Dark", female ? "T_Superhero_Female_Normal" : "T_Superhero_Male_Normal", null, 0.4f, false);
                else if (n.Contains("regular")) m = Lit(female ? "skinF" : "skinM",
                    female ? "T_Regular_Female_Dark_BaseColor" : "T_Regular_Male_Dark_BaseColor", female ? "T_Regular_Female_Normal" : "T_Regular_Male_Normal", null, 0.4f, false);
                else m = Lit("outfit " + outfitColour, outfitColour, "T_Ranger_Normal", "T_Ranger_ORM", 0f, false);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }

        /// <summary>
        /// A lit material: colour, normal map and (when given) a packed occlusion/roughness/metal map,
        /// drawn with the project's scan shader in UV mode; otherwise URP Lit with a fixed smoothness.
        /// </summary>
        static Material Lit(string key, string colour, string normal, string orm, float smoothness, bool cutout)
        {
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var baseTex = ArtLibrary.LoadTexture(Textures + colour);
            var normalTex = ArtLibrary.LoadTexture(Textures + normal);
            var ormTex = orm != null ? ArtLibrary.LoadTexture(Textures + orm) : null;
            var scan = Resources.Load<Shader>("Shaders/SealboundTriplanar");
            Material m;
            if (ormTex != null && scan != null && !cutout)
            {
                m = new Material(scan);
                m.EnableKeyword("_USE_UV");
                m.SetFloat("_UseUV", 1f);
                if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
                if (normalTex != null) m.SetTexture("_NormalMap", normalTex);
                m.SetTexture("_ARMMap", ormTex);
            }
            else
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(lit != null ? lit : Shapes.Material(Color.white, false).shader);
                if (baseTex != null) { m.mainTexture = baseTex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex); }
                m.color = Color.white;
                if (normalTex != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normalTex); m.EnableKeyword("_NORMALMAP"); }
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (cutout)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.4f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0f);
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
            }
            m.name = "Hero " + key;
            Materials[key] = m;
            return m;
        }
    }
}
