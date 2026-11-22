using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// The land itself, when the scanned art is in the project: a wide terrain painted with real grass,
    /// forest floor, trail and rock textures, flat where you play and rising into wooded highland hills
    /// around it, plus thick grass drawn straight on the graphics card.
    /// </summary>
    public static class Landscape
    {
        public static Terrain Terrain { get; private set; }

        const float Size = 760f;
        const float MaxHeight = 90f;
        static readonly Vector3 Origin = new Vector3(-400f, 0f, -320f); // terrain corner; covers x -400..360, z -320..440
        static Vector2 _seed;
        static float[,,] _alphamaps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Terrain = null; _alphamaps = null; }

        /// <summary>Ground height at a point: 0 on the play field, higher on the hills.</summary>
        public static float HeightAt(Vector3 p) => Terrain != null ? Terrain.SampleHeight(p) + Terrain.transform.position.y : 0f;

        public static Terrain Build(Transform root)
        {
            if (!RealArt.Has) return null;
            _seed = new Vector2(37.1f, 91.7f);

            var data = new TerrainData { heightmapResolution = 513, alphamapResolution = 512 };
            data.size = new Vector3(Size, MaxHeight, Size);
            data.baseMapResolution = 1024;

            int hr = data.heightmapResolution;
            var heights = new float[hr, hr];
            for (int y = 0; y < hr; y++)
                for (int x = 0; x < hr; x++)
                {
                    Vector3 w = Origin + new Vector3(x / (float)(hr - 1) * Size, 0f, y / (float)(hr - 1) * Size);
                    heights[y, x] = HillHeight(w) / MaxHeight;
                }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                Layer(Has(RealArt.Meadow) ? RealArt.Meadow : RealArt.Grass, 3f),                    // 0 meadow
                Layer(Has(RealArt.MeadowMix) ? RealArt.MeadowMix : RealArt.ForestGround, 4f),       // 1 forest edge and floor
                Layer(RealArt.Trail, 3.5f),      // 2 trail and arena
                Layer(RealArt.Cliff, 9f),        // 3 rock on steep slopes
            };
            PaintLayers(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Highland Terrain (Poly Haven, CC0)";
            go.transform.SetParent(root, true);
            go.transform.position = Origin;
            go.isStatic = true;
            var t = go.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null) t.materialTemplate = new Material(shader);
            t.drawInstanced = true;
            t.heightmapPixelError = 4f;
            t.basemapDistance = 220f;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Terrain = t;
            return t;
        }

        // ---------- Shape ----------

        /// <summary>0 inside the play area; outside it the land rolls up into hills and ridges.</summary>
        static float HillHeight(Vector3 w)
        {
            float edge = DistanceOutsidePlay(w);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 70f));
            if (rise <= 0f) return 0f;
            float n = Fbm(w.x * 0.006f + _seed.x, w.z * 0.006f + _seed.y, 4);             // big rolling hills
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(w.x * 0.004f + 13f, w.z * 0.004f + 7f) * 2f - 1f); // a few ridgelines
            float far = Mathf.Clamp01((edge - 60f) / 160f);                               // taller further out
            float h = rise * (6f + n * 26f) + far * (ridge * ridge * 38f + n * 14f);
            return Mathf.Min(h, MaxHeight - 1f);
        }

        /// <summary>How far outside the flat play area a point is (0 inside).</summary>
        static float DistanceOutsidePlay(Vector3 p)
        {
            // The play field (start, yard, course, warren, ruins) and the Legend tower's clearing.
            float field = RectDistance(p, -115f, -70f, 75f, 175f);
            float tower = Mathf.Max(0f, new Vector2(p.x + 90f, p.z - 160f).magnitude - 30f);
            return Mathf.Min(field, tower);
        }

        static float RectDistance(Vector3 p, float minX, float minZ, float maxX, float maxZ)
        {
            float dx = Mathf.Max(minX - p.x, 0f, p.x - maxX);
            float dz = Mathf.Max(minZ - p.z, 0f, p.z - maxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                freq *= 2.03f; amp *= 0.5f;
            }
            return sum;
        }

        // ---------- Painting ----------

        static bool Has(string slug) => RealArt.Tex(slug, "diff") != null;

        static TerrainLayer Layer(string slug, float tile)
        {
            var l = new TerrainLayer
            {
                diffuseTexture = RealArt.Tex(slug, "diff"),
                normalMapTexture = RealArt.Tex(slug, "nor"),
                tileSize = new Vector2(tile, tile),
                normalScale = 1f,
                smoothness = 0.05f,
                metallic = 0f,
            };
            // Grass textures lean yellow under the warm sun: pull them toward a fresh highland green.
            if (slug == RealArt.Meadow || slug == RealArt.MeadowMix || slug == RealArt.Grass)
                l.diffuseRemapMax = new Vector4(0.78f, 0.95f, 0.72f, 1f);
            l.name = slug;
            return l;
        }

        static void PaintLayers(TerrainData data)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, 4];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float u = x / (float)(res - 1), v = y / (float)(res - 1);
                    Vector3 w = Origin + new Vector3(u * Size, 0f, v * Size);
                    float steep = data.GetSteepness(u, v);

                    // Meadow broken by patches of forest floor, more of it under the trees further out.
                    float patches = Fbm(w.x * 0.035f + 5f, w.z * 0.035f + 9f, 3);
                    float outward = Mathf.Clamp01((new Vector2(w.x, w.z - 40f).magnitude - 50f) / 60f);
                    float forest = Mathf.Clamp01((patches - 0.48f) * 5f + outward * 0.55f);

                    // The trail from the start to the ruins, the arena floor and the warren's trampled dirt.
                    float trail = 0f;
                    float wobble = (Mathf.PerlinNoise(w.z * 0.08f, 3f) - 0.5f) * 1.6f;
                    if (w.z > -44f && w.z < 124f) trail = Mathf.Clamp01((3.4f - Mathf.Abs(w.x - wobble)) / 1.6f);
                    float arena = Mathf.Clamp01((16f - new Vector2(w.x, w.z - 70f).magnitude) / 4f) * (0.55f + 0.45f * patches);
                    float yard = (w.x > -3f && w.x < 17f && w.z > -17f && w.z < 1f) ? 0.85f : 0f;
                    trail = Mathf.Max(trail, Mathf.Max(arena, yard));
                    float warren = Mathf.Clamp01((18f - new Vector2(w.x + 45f, w.z + 15f).magnitude) / 5f);
                    forest = Mathf.Max(forest, warren);

                    float rock = Mathf.Clamp01((steep - 34f) / 12f); // bare rock only on the steepest slopes

                    float g = (1f - forest) * (1f - trail) * (1f - rock);
                    float f = forest * (1f - trail) * (1f - rock);
                    float t = trail * (1f - rock);
                    float sum = Mathf.Max(0.0001f, g + f + t + rock);
                    maps[y, x, 0] = g / sum;
                    maps[y, x, 1] = f / sum;
                    maps[y, x, 2] = t / sum;
                    maps[y, x, 3] = rock / sum;
                }
            data.SetAlphamaps(0, 0, maps);
            _alphamaps = maps;
        }

        /// <summary>How much of a ground texture is at a point (0 meadow, 1 forest floor, 2 trail, 3 rock).</summary>
        public static float LayerWeight(Vector3 p, int layer)
        {
            if (_alphamaps == null) return layer == 0 ? 1f : 0f;
            int res = _alphamaps.GetLength(0);
            Vector3 local = p - Origin;
            int x = Mathf.Clamp(Mathf.RoundToInt(local.x / Size * (res - 1)), 0, res - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(local.z / Size * (res - 1)), 0, res - 1);
            return _alphamaps[z, x, layer];
        }
    }

    /// <summary>
    /// Draws thousands of grass clumps and flowers with GPU instancing (no game objects), in square
    /// patches that are only drawn near the camera.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        public float drawDistance = 75f;
        const float Patch = 16f;

        class Kind
        {
            public Mesh mesh;
            public Material[] materials;
            public Matrix4x4 localFix; // the model's own transform inside its prefab
        }

        class PatchData
        {
            public Vector3 centre;
            public List<Matrix4x4>[] perKind;
        }

        readonly List<Kind> _kinds = new List<Kind>();
        readonly List<PatchData> _patches = new List<PatchData>();
        Camera _cam;

        public static GrassField Build(Transform parent, System.Func<Vector3, bool> allowed)
        {
            if (!RealArt.HasNature) return null;
            var go = new GameObject("Grass (Nature MegaKit, CC0)");
            go.transform.SetParent(parent, false);
            var field = go.AddComponent<GrassField>();
            // Weighted: lots of grass, some flowers and clover.
            string[] names = { "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall", "Flower_3_Group", "Flower_4_Group", "Clover_1" };
            float[] weight = { 0.3f, 0.2f, 0.22f, 0.14f, 0.05f, 0.04f, 0.05f };
            float[] height = { 0.35f, 0.6f, 0.4f, 0.7f, 0.35f, 0.35f, 0.15f };
            var usable = new List<int>();
            for (int i = 0; i < names.Length; i++)
                if (field.AddKind(names[i], height[i])) usable.Add(i);
            if (usable.Count == 0) { Destroy(go); return null; }

            var rng = new System.Random(4242);
            for (float px = -112f; px < 72f; px += Patch)
                for (float pz = -66f; pz < 172f; pz += Patch)
                {
                    var patch = new PatchData { centre = new Vector3(px + Patch * 0.5f, 0f, pz + Patch * 0.5f), perKind = new List<Matrix4x4>[field._kinds.Count] };
                    for (int k = 0; k < field._kinds.Count; k++) patch.perKind[k] = new List<Matrix4x4>();
                    int count = 150;
                    for (int i = 0; i < count; i++)
                    {
                        var p = new Vector3(px + (float)rng.NextDouble() * Patch, 0f, pz + (float)rng.NextDouble() * Patch);
                        if (!allowed(p)) continue;
                        float meadow = Landscape.LayerWeight(p, 0) + Landscape.LayerWeight(p, 1) * 0.35f;
                        if (rng.NextDouble() > meadow) continue;
                        p.y = Landscape.HeightAt(p) - 0.03f;
                        int pick = PickWeighted(rng, weight, usable);
                        int kindIndex = usable.IndexOf(pick);
                        float s = 0.75f + (float)rng.NextDouble() * 0.6f;
                        var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s) * field._kinds[kindIndex].localFix;
                        patch.perKind[kindIndex].Add(m);
                    }
                    field._patches.Add(patch);
                }
            return field;
        }

        static int PickWeighted(System.Random rng, float[] weight, List<int> usable)
        {
            float total = 0f;
            foreach (int i in usable) total += weight[i];
            float r = (float)rng.NextDouble() * total;
            foreach (int i in usable) { r -= weight[i]; if (r <= 0f) return i; }
            return usable[usable.Count - 1];
        }

        bool AddKind(string name, float height)
        {
            var go = RealArt.Nature(name, null);
            if (go == null) return false;
            var mf = go.GetComponentInChildren<MeshFilter>();
            var mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
            if (mf == null || mr == null || mf.sharedMesh == null) { Destroy(go); return false; }
            // Scale the clump to the wanted height in metres, feet on the ground.
            go.transform.position = Vector3.zero;
            var b = ArtLibrary.WorldBounds(go);
            float s = b.size.y > 0.001f ? height / b.size.y : 1f;
            go.transform.localScale *= s;
            b = ArtLibrary.WorldBounds(go);
            go.transform.position += Vector3.up * -b.min.y;
            var fix = mf.transform.localToWorldMatrix;
            var mats = mr.sharedMaterials;
            foreach (var m in mats) if (m != null) m.enableInstancing = true;
            _kinds.Add(new Kind { mesh = mf.sharedMesh, materials = mats, localFix = fix });
            Destroy(go);
            return true;
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            Vector3 c = _cam.transform.position;
            float reach = drawDistance + Patch;
            foreach (var p in _patches)
            {
                float dx = p.centre.x - c.x, dz = p.centre.z - c.z;
                if (dx * dx + dz * dz > reach * reach) continue;
                for (int k = 0; k < _kinds.Count; k++)
                {
                    var list = p.perKind[k];
                    if (list.Count == 0) continue;
                    var kind = _kinds[k];
                    for (int sub = 0; sub < kind.mesh.subMeshCount && sub < kind.materials.Length; sub++)
                    {
                        var rp = new RenderParams(kind.materials[sub])
                        {
                            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                            receiveShadows = true,
                            worldBounds = new Bounds(p.centre + Vector3.up, new Vector3(Patch + 2f, 4f, Patch + 2f)),
                        };
                        Graphics.RenderMeshInstanced(rp, kind.mesh, sub, list);
                    }
                }
            }
        }
    }
}
