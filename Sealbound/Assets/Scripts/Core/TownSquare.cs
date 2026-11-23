using Sealbound.Combat;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// The players' home base for the first slice: a small walled town square out past the hills, with the
    /// mission board, the Sealers' forge and the soul shrine. Built from simple shapes wearing real stone.
    /// </summary>
    public static class TownSquare
    {
        public static readonly Vector3 Centre = new Vector3(230f, 0f, -200f);
        public const float WallHalf = 24f;     // the town wall is a square 48 m across
        public const float ClearRadius = 42f;  // no trees inside this
        public const float FlatRadius = 46f;   // the land is flat this far out, then rolls up into hills

        /// <summary>Where you arrive in town, by the fountain, looking into the square.</summary>
        public static Vector3 SpawnPoint => Centre + new Vector3(0f, 0.15f, -6f);
        public static Vector3 SpawnFacing => Vector3.forward;

        public static bool Contains(Vector3 p) =>
            Mathf.Abs(p.x - Centre.x) < WallHalf + 8f && Mathf.Abs(p.z - Centre.z) < WallHalf + 8f;

        static Material _stone, _paving, _plaster, _roof, _dark, _timber, _water, _glass, _red, _blue, _gold, _parchment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _stone = _paving = _plaster = _roof = _dark = _timber = _water = _glass = _red = _blue = _gold = _parchment = null; }

        public static Transform Build(Transform parent)
        {
            var root = new GameObject("Town Square").transform;
            root.SetParent(parent, false);
            root.position = Centre;
            var built = new GameObject("Buildings").transform;
            built.SetParent(root, false);
            MakeMaterials();

            Ground(built);
            Walls(built);
            Houses(built);
            Fountain(built, new Vector3(0f, 0f, 4f));
            Stall(built, new Vector3(-7.5f, 0f, 11.5f), 0f, _red);
            Stall(built, new Vector3(7.5f, 0f, 11.5f), 0f, _blue);
            Clutter(built);
            Brazier(root, new Vector3(-5.2f, 0f, -21.4f));
            Brazier(root, new Vector3(5.2f, 0f, -21.4f));

            ForgeStation.Build(root, Centre + new Vector3(12.5f, 0f, 2.5f), -90f);
            SoulShrine.Build(root, Centre + new Vector3(-12f, 0f, 2.5f), 90f);
            MissionBoard.Build(root, Centre + new Vector3(-4.5f, 0f, -13.5f), 0f);

            StaticBatchingUtility.Combine(built.gameObject);
            return root;
        }

        static void MakeMaterials()
        {
            bool real = RealArt.Has;
            _stone = real ? RealArt.Surface(RealArt.CastleStone, 2.5f, 0.08f) : Shapes.Material(new Color(0.58f, 0.56f, 0.52f), false);
            _paving = real ? RealArt.Surface(RealArt.Blocks, 2.2f, 0f, new Color(0.85f, 0.83f, 0.8f)) : Shapes.Material(new Color(0.5f, 0.48f, 0.45f), false);
            _roof = real ? RealArt.Surface(RealArt.DarkRock, 1.4f, 0f, new Color(0.62f, 0.55f, 0.52f)) : Shapes.Material(new Color(0.32f, 0.23f, 0.2f), false);
            _plaster = Lit("Plaster", new Color(0.78f, 0.72f, 0.62f), 0.1f);
            _dark = Lit("Dark wood", new Color(0.16f, 0.11f, 0.08f), 0.15f);
            _glass = Lit("Window", new Color(0.06f, 0.07f, 0.08f), 0.85f);
            _timber = ForgedWeapons.Timber();
            _water = Lit("Water", new Color(0.1f, 0.17f, 0.2f), 0.96f);
            _red = Lit("Red cloth", new Color(0.42f, 0.07f, 0.06f), 0.1f);
            _blue = Lit("Blue cloth", new Color(0.09f, 0.14f, 0.33f), 0.1f);
            _gold = Lit("Gold thread", new Color(0.78f, 0.6f, 0.25f), 0.4f, 0.7f);
            _parchment = Lit("Parchment", new Color(0.88f, 0.82f, 0.66f), 0.05f);
        }

        static Material Lit(string name, Color c, float smooth, float metal = 0f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shapes.Material(Color.white, false).shader) { name = name };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        // ---------- Pieces ----------

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool solid = false, float yaw = 0f)
        {
            var go = Shapes.Make(PrimitiveType.Cube, parent, name, pos, size, Color.white, solid);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (yaw != 0f) go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 pos, float diameter, float height, Material m, bool solid = false)
        {
            var go = Shapes.Make(PrimitiveType.Cylinder, parent, name, pos, new Vector3(diameter, height * 0.5f, diameter), Color.white, solid);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static void Ground(Transform t)
        {
            // Without the terrain (grey-box look) the town needs its own ground to stand on.
            if (Landscape.Terrain == null)
                Box(t, "Town Ground", new Vector3(0f, -0.5f, 0f), new Vector3(FlatRadius * 2f + 20f, 1f, FlatRadius * 2f + 20f), Shapes.Material(new Color(0.42f, 0.6f, 0.3f), false), true);
            // Paving inside the walls, and a lane to the gate.
            Box(t, "Paving", new Vector3(0f, 0f, 0f), new Vector3(WallHalf * 2f, 0.12f, WallHalf * 2f), _paving, true);
            // A ring of kerbstones round the square proper.
            for (int side = 0; side < 4; side++)
            {
                float a = side * 90f;
                Vector3 outward = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var k = Box(t, "Kerb", outward * 15.5f + Vector3.up * 0.08f, new Vector3(31.4f, 0.06f, 0.35f), _stone);
                k.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
        }

        static void Walls(Transform t)
        {
            const float h = 5.2f, thick = 1.2f;
            float e = WallHalf;
            // North, east and west walls; the south wall leaves room for the gatehouse.
            Box(t, "Town Wall N", new Vector3(0f, h / 2f, e), new Vector3(e * 2f, h, thick), _stone, true);
            Box(t, "Town Wall E", new Vector3(e, h / 2f, 0f), new Vector3(thick, h, e * 2f), _stone, true);
            Box(t, "Town Wall W", new Vector3(-e, h / 2f, 0f), new Vector3(thick, h, e * 2f), _stone, true);
            float seg = e - 5.4f;
            Box(t, "Town Wall S", new Vector3(-(5.4f + seg / 2f), h / 2f, -e), new Vector3(seg, h, thick), _stone, true);
            Box(t, "Town Wall S", new Vector3(5.4f + seg / 2f, h / 2f, -e), new Vector3(seg, h, thick), _stone, true);
            // Battlements along the top.
            for (float x = -e + 1f; x < e; x += 2f)
            {
                Box(t, "Merlon", new Vector3(x, h + 0.4f, e), new Vector3(1f, 0.8f, thick), _stone);
                if (Mathf.Abs(x) > 5.6f) Box(t, "Merlon", new Vector3(x, h + 0.4f, -e), new Vector3(1f, 0.8f, thick), _stone);
                Box(t, "Merlon", new Vector3(e, h + 0.4f, x), new Vector3(thick, 0.8f, 1f), _stone);
                Box(t, "Merlon", new Vector3(-e, h + 0.4f, x), new Vector3(thick, 0.8f, 1f), _stone);
            }
            // Round towers at the corners.
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(i < 2 ? -e : e, 0f, i % 2 == 0 ? -e : e);
                Cyl(t, "Corner Tower", c + Vector3.up * 4.5f, 5.2f, 9f, _stone, true);
                Cyl(t, "Tower Rim", c + Vector3.up * 9.1f, 5.7f, 0.3f, _stone);
                for (int m = 0; m < 10; m++)
                {
                    Vector3 o = Quaternion.Euler(0f, m * 36f, 0f) * Vector3.forward * 2.55f;
                    Box(t, "Merlon", c + o + Vector3.up * 9.65f, new Vector3(0.8f, 0.8f, 0.6f), _stone, false, m * 36f);
                }
            }
            // The gatehouse: two square towers, an arch over the gate and its heavy doors (shut: the road
            // out is through the waystone's magic for now).
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, "Gate Tower", new Vector3(s * 4.3f, 3.8f, -e), new Vector3(2.6f, 7.6f, 2.8f), _stone, true);
                for (int m = -1; m <= 1; m += 2)
                    Box(t, "Merlon", new Vector3(s * 4.3f + m * 0.85f, 8f, -e + 1.1f), new Vector3(0.7f, 0.8f, 0.5f), _stone);
                Box(t, "Gate Banner", new Vector3(s * 4.3f, 4.6f, -e + 1.44f), new Vector3(1.3f, 3.2f, 0.05f), _blue);
                Box(t, "Banner Ring", new Vector3(s * 4.3f, 5.1f, -e + 1.48f), new Vector3(0.7f, 0.08f, 0.02f), _gold);
                Box(t, "Banner Ring", new Vector3(s * 4.3f, 5.1f, -e + 1.48f), new Vector3(0.08f, 0.7f, 0.02f), _gold);
                Box(t, "Banner Pole", new Vector3(s * 4.3f, 6.25f, -e + 1.46f), new Vector3(1.5f, 0.08f, 0.08f), _timber);
            }
            Box(t, "Gate Arch", new Vector3(0f, 6.2f, -e), new Vector3(6.0f, 2.0f, 2.4f), _stone, true);
            Box(t, "Gate Door", new Vector3(-1.5f, 2.6f, -e - 0.2f), new Vector3(3f, 5.2f, 0.3f), _dark, true);
            Box(t, "Gate Door", new Vector3(1.5f, 2.6f, -e - 0.2f), new Vector3(3f, 5.2f, 0.3f), _dark, true);
            for (int b = 0; b < 3; b++)
                Box(t, "Door Band", new Vector3(0f, 0.9f + b * 1.7f, -e - 0.02f), new Vector3(5.8f, 0.14f, 0.06f), Shapes.Material(new Color(0.2f, 0.2f, 0.21f), false));
        }

        static void Houses(Transform t)
        {
            // North row, facing south into the square.
            House(t, new Vector3(-13f, 0f, 19.6f), 180f, 8.5f, 7f, true, 0);
            House(t, new Vector3(-4.3f, 0f, 19.6f), 180f, 8f, 7f, false, 1);
            House(t, new Vector3(4.3f, 0f, 19.6f), 180f, 8.5f, 7f, true, 2);
            House(t, new Vector3(13f, 0f, 19.6f), 180f, 8f, 7f, true, 3);
            // East row, facing west.
            House(t, new Vector3(19.6f, 0f, 8.5f), -90f, 8f, 7f, true, 4);
            House(t, new Vector3(19.6f, 0f, -0.5f), -90f, 7.5f, 7f, false, 5);
            House(t, new Vector3(19.6f, 0f, -9.5f), -90f, 8.5f, 7f, true, 6);
            // West row, facing east.
            House(t, new Vector3(-19.6f, 0f, 9f), 90f, 8.5f, 7f, true, 7);
            House(t, new Vector3(-19.6f, 0f, -0.5f), 90f, 7.5f, 7f, true, 8);
            House(t, new Vector3(-19.6f, 0f, -9.5f), 90f, 8f, 7f, false, 9);
            // Either side of the gate lane, facing north.
            House(t, new Vector3(-13f, 0f, -19.6f), 0f, 9f, 7f, true, 10);
            House(t, new Vector3(13f, 0f, -19.6f), 0f, 9f, 7f, false, 11);
        }

        /// <summary>A house: a stone ground floor, sometimes a timber-framed upper floor, and a steep slate roof. Its front faces local +z.</summary>
        static void House(Transform parent, Vector3 pos, float yaw, float w, float d, bool twoStorey, int seed)
        {
            var rng = new System.Random(4100 + seed * 31);
            var h = new GameObject("House").transform;
            h.SetParent(parent, false);
            h.localPosition = pos;
            h.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const float baseH = 3.2f;
            Box(h, "Ground Floor", new Vector3(0f, baseH / 2f, 0f), new Vector3(w, baseH, d), _stone, true);
            float front = d / 2f;
            // A door and a window or two.
            float doorX = (float)(rng.NextDouble() - 0.5) * (w - 3f);
            Box(h, "Door", new Vector3(doorX, 1.1f, front + 0.04f), new Vector3(1.15f, 2.2f, 0.1f), _dark);
            Box(h, "Lintel", new Vector3(doorX, 2.3f, front + 0.08f), new Vector3(1.5f, 0.22f, 0.16f), _timber);
            for (float x = -w / 2f + 1.4f; x < w / 2f - 1f; x += 2.4f)
            {
                if (Mathf.Abs(x - doorX) < 1.5f) continue;
                Box(h, "Window", new Vector3(x, 1.65f, front + 0.04f), new Vector3(0.8f, 1f, 0.08f), _glass);
                Box(h, "Sill", new Vector3(x, 1.1f, front + 0.1f), new Vector3(1f, 0.1f, 0.16f), _timber);
                Box(h, "Shutter", new Vector3(x - 0.62f, 1.65f, front + 0.06f), new Vector3(0.42f, 1.05f, 0.06f), _dark);
            }

            float top = baseH, body = d;
            if (twoStorey)
            {
                const float upH = 2.8f;
                body = d + 0.3f;
                Box(h, "Upper Floor", new Vector3(0f, baseH + upH / 2f, 0.15f), new Vector3(w + 0.3f, upH, body), _plaster, true);
                float f2 = 0.15f + body / 2f + 0.03f;
                // Timber frame on the front: posts, rails and a pair of braces.
                Box(h, "Rail", new Vector3(0f, baseH + 0.11f, f2), new Vector3(w + 0.4f, 0.22f, 0.12f), _timber);
                Box(h, "Rail", new Vector3(0f, baseH + upH - 0.11f, f2), new Vector3(w + 0.4f, 0.22f, 0.12f), _timber);
                int posts = Mathf.Max(3, Mathf.RoundToInt(w / 1.8f));
                for (int i = 0; i <= posts; i++)
                {
                    float x = -w / 2f - 0.05f + i * (w + 0.1f) / posts;
                    Box(h, "Post", new Vector3(x, baseH + upH / 2f, f2), new Vector3(0.18f, upH, 0.1f), _timber);
                    if (i < posts && i % 2 == 1)
                        Box(h, "Window", new Vector3(x + (w + 0.1f) / posts / 2f, baseH + 1.45f, f2 - 0.02f), new Vector3(0.7f, 0.9f, 0.06f), _glass);
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    var brace = Box(h, "Brace", new Vector3(s * (w / 2f - 0.75f), baseH + upH / 2f, f2 + 0.01f), new Vector3(0.14f, upH * 1.1f, 0.08f), _timber);
                    brace.transform.localRotation = Quaternion.Euler(0f, 0f, s * 32f);
                }
                top = baseH + upH;
            }

            // A steep slate roof (45 degrees), with gable ends.
            float hd = body / 2f + 0.45f;
            float zc = twoStorey ? 0.15f : 0f;
            float len = hd * 1.4142f;
            var r1 = Box(h, "Roof", new Vector3(0f, top + hd / 2f, zc + hd / 2f), new Vector3(w + 0.8f, 0.24f, len), _roof);
            r1.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            var r2 = Box(h, "Roof", new Vector3(0f, top + hd / 2f, zc - hd / 2f), new Vector3(w + 0.8f, 0.24f, len), _roof);
            r2.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            Box(h, "Ridge", new Vector3(0f, top + hd + 0.05f, zc), new Vector3(w + 0.9f, 0.18f, 0.3f), _roof);
            float g = body / 1.4142f;
            for (int s = -1; s <= 1; s += 2)
            {
                var gable = Box(h, "Gable", new Vector3(s * (w / 2f - 0.12f), top, zc), new Vector3(0.24f, g, g), twoStorey ? _plaster : _stone);
                gable.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
            if (rng.NextDouble() < 0.6)
                Box(h, "Chimney", new Vector3((float)(rng.NextDouble() - 0.5) * (w - 2f), top + hd * 0.75f, zc - hd * 0.35f), new Vector3(0.75f, hd * 1.2f, 0.75f), _stone, true);
            // A cloth banner on some upper floors.
            if (twoStorey && rng.NextDouble() < 0.5)
            {
                float bx = (float)(rng.NextDouble() - 0.5) * (w - 3f);
                Box(h, "House Banner", new Vector3(bx, baseH + 1.3f, 0.15f + body / 2f + 0.12f), new Vector3(0.9f, 2.1f, 0.04f), rng.NextDouble() < 0.5 ? _red : _blue);
            }
        }

        static void Fountain(Transform t, Vector3 at)
        {
            Cyl(t, "Fountain Basin", at + Vector3.up * 0.35f, 5.2f, 0.7f, _stone, true);
            Cyl(t, "Fountain Rim", at + Vector3.up * 0.72f, 5.5f, 0.08f, _stone);
            Cyl(t, "Fountain Water", at + Vector3.up * 0.74f, 4.6f, 0.04f, _water);
            Cyl(t, "Fountain Column", at + Vector3.up * 1.3f, 0.6f, 1.6f, _stone);
            Cyl(t, "Fountain Bowl", at + Vector3.up * 2.1f, 1.9f, 0.22f, _stone);
            Cyl(t, "Bowl Water", at + Vector3.up * 2.22f, 1.6f, 0.03f, _water);
            Cyl(t, "Fountain Spire", at + Vector3.up * 2.75f, 0.3f, 1.1f, _stone);
            var top = Shapes.Make(PrimitiveType.Sphere, t, "Fountain Finial", at + Vector3.up * 3.4f, Vector3.one * 0.42f, Color.white);
            top.GetComponent<Renderer>().sharedMaterial = _stone;
        }

        static void Stall(Transform t, Vector3 at, float yaw, Material cloth)
        {
            var s = new GameObject("Market Stall").transform;
            s.SetParent(t, false);
            s.localPosition = at;
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 4; i++)
                Box(s, "Stall Post", new Vector3(i < 2 ? -1.5f : 1.5f, 1.2f, i % 2 == 0 ? -0.9f : 0.9f), new Vector3(0.12f, 2.4f + (i % 2 == 0 ? 0f : 0.4f), 0.12f), _timber, i < 2);
            var canopy = Box(s, "Canopy", new Vector3(0f, 2.6f, 0f), new Vector3(3.4f, 0.05f, 2.2f), cloth);
            canopy.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            Box(s, "Counter", new Vector3(0f, 0.5f, -0.6f), new Vector3(3f, 1f, 0.7f), _timber, true);
            Box(s, "Cloth", new Vector3(0f, 1.02f, -0.6f), new Vector3(3.05f, 0.03f, 0.75f), cloth);
            for (int i = 0; i < 3; i++)
                Box(s, "Crate", new Vector3(-1f + i * 1f, 1.17f, -0.6f), new Vector3(0.5f, 0.3f, 0.4f), _timber, false, i * 12f);
            Cyl(s, "Barrel", new Vector3(1.9f, 0.5f, 0.5f), 0.7f, 1f, _timber, true);
        }

        static void Clutter(Transform t)
        {
            // Barrels, crates and sacks by the houses, so the square feels lived in.
            Vector3[] spots = { new Vector3(-15.2f, 0f, 14.5f), new Vector3(15.6f, 0f, -14.8f), new Vector3(-15.8f, 0f, -14.6f), new Vector3(16f, 0f, 13.8f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Cyl(t, "Barrel", spots[i] + Vector3.up * 0.5f, 0.75f, 1f, _timber, true);
                Cyl(t, "Barrel", spots[i] + new Vector3(0.85f, 0.5f, 0.2f), 0.75f, 1f, _timber, true);
                Box(t, "Crate", spots[i] + new Vector3(-0.2f, 0.4f, 0.95f), new Vector3(0.8f, 0.8f, 0.8f), _timber, true, i * 17f);
                var sack = Shapes.Make(PrimitiveType.Sphere, t, "Sack", spots[i] + new Vector3(0.6f, 0.3f, 1.1f), new Vector3(0.6f, 0.55f, 0.5f), Color.white);
                sack.GetComponent<Renderer>().sharedMaterial = ForgedWeapons.Burlap();
            }
            // Benches round the fountain.
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                Vector3 p = new Vector3(0f, 0f, 4f) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 4.4f;
                Box(t, "Bench", p + Vector3.up * 0.45f, new Vector3(1.8f, 0.1f, 0.45f), _timber, true, a);
                Box(t, "Bench Leg", p + Vector3.up * 0.2f + Quaternion.Euler(0f, a, 0f) * Vector3.right * 0.7f, new Vector3(0.12f, 0.4f, 0.4f), _stone, false, a);
                Box(t, "Bench Leg", p + Vector3.up * 0.2f - Quaternion.Euler(0f, a, 0f) * Vector3.right * 0.7f, new Vector3(0.12f, 0.4f, 0.4f), _stone, false, a);
            }
        }

        static void Brazier(Transform root, Vector3 at)
        {
            var t = new GameObject("Brazier").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            Cyl(t, "Brazier Stand", Vector3.up * 0.55f, 0.3f, 1.1f, _stone, true);
            Cyl(t, "Brazier Bowl", Vector3.up * 1.2f, 0.9f, 0.3f, Shapes.Material(new Color(0.18f, 0.17f, 0.16f), false));
            Glow.Make(t, Vector3.up * 1.55f, 1.1f, new Color(2.4f, 0.75f, 0.15f, 0.85f));
            Glow.Make(t, Vector3.up * 1.4f, 0.6f, new Color(2.6f, 1.4f, 0.4f, 1f));
            var l = new GameObject("Brazier Light").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = Vector3.up * 1.8f;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.28f);
            l.range = 6f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
        }

        // Shared by the board, the waystone and the return circle.
        public static readonly Color RuneGem = new Color(0.12f, 0.5f, 1f);
        internal static Material ParchmentMaterial { get { if (_parchment == null) MakeMaterials(); return _parchment; } }
        internal static Material TimberMaterial => ForgedWeapons.Timber();
        internal static Material DarkMaterial { get { if (_dark == null) MakeMaterials(); return _dark; } }
        internal static Material RedMaterial { get { if (_red == null) MakeMaterials(); return _red; } }
        internal static Material StoneMaterial { get { if (_stone == null) MakeMaterials(); return _stone; } }
        internal static GameObject MakeBox(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool solid = false) => Box(parent, name, pos, size, m, solid);
    }

    /// <summary>Something you walk up to and press G at. Tracks whether the player is close enough.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public float reach = 3f;
        public bool InReach { get; private set; }
        Transform _player;

        protected virtual void Update()
        {
            if (_player == null)
            {
                var m = PlayerMotor.Main;
                if (m != null) _player = m.transform;
            }
            Vector3 d = _player != null ? _player.position - transform.position : Vector3.one * 999f;
            d.y = 0f;
            InReach = d.magnitude <= reach;
        }
    }

    /// <summary>The mission board in the town square: notices pinned to a board under a little roof.</summary>
    public class MissionBoard : Interactable
    {
        public static MissionBoard Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public static MissionBoard Build(Transform parent, Vector3 at, float yaw)
        {
            var root = new GameObject("Mission Board");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Landscape.HeightAt(at), at.z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = root.AddComponent<MissionBoard>();
            b.reach = 3.4f;
            Instance = b;
            var t = root.transform;
            var wood = TownSquare.TimberMaterial;
            for (int s = -1; s <= 1; s += 2)
                TownSquare.MakeBox(t, "Board Post", new Vector3(s * 1.35f, 1.35f, 0f), new Vector3(0.18f, 2.7f, 0.18f), wood, true);
            TownSquare.MakeBox(t, "Board", new Vector3(0f, 1.6f, 0f), new Vector3(2.6f, 1.5f, 0.1f), TownSquare.DarkMaterial, true);
            TownSquare.MakeBox(t, "Board Frame", new Vector3(0f, 2.38f, 0.02f), new Vector3(2.8f, 0.12f, 0.16f), wood);
            TownSquare.MakeBox(t, "Board Frame", new Vector3(0f, 0.83f, 0.02f), new Vector3(2.8f, 0.12f, 0.16f), wood);
            for (int s = -1; s <= 1; s += 2)
            {
                var roof = TownSquare.MakeBox(t, "Board Roof", new Vector3(0f, 2.82f, s * 0.28f), new Vector3(3.1f, 0.07f, 0.66f), wood);
                roof.transform.localRotation = Quaternion.Euler(s * -28f, 0f, 0f);
            }
            // Notices: the big one with a red seal is the golem hunt.
            var paper = TownSquare.ParchmentMaterial;
            var main = TownSquare.MakeBox(t, "Mission Notice", new Vector3(-0.35f, 1.62f, 0.065f), new Vector3(0.62f, 0.8f, 0.01f), paper);
            main.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            var seal = Shapes.Make(PrimitiveType.Sphere, t, "Wax Seal", new Vector3(-0.35f, 1.32f, 0.075f), new Vector3(0.11f, 0.11f, 0.02f), Color.white);
            seal.GetComponent<Renderer>().sharedMaterial = TownSquare.RedMaterial;
            Vector3[] small = { new Vector3(0.45f, 1.85f, 0.065f), new Vector3(0.85f, 1.45f, 0.065f), new Vector3(0.35f, 1.25f, 0.065f), new Vector3(-0.95f, 1.9f, 0.065f), new Vector3(-0.9f, 1.25f, 0.065f) };
            for (int i = 0; i < small.Length; i++)
            {
                var n = TownSquare.MakeBox(t, "Notice", small[i], new Vector3(0.36f, 0.46f, 0.008f), paper);
                n.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * (2f + i * 1.3f));
            }
            // A small lantern hanging from the roof, lit even by day.
            Glow.Make(t, new Vector3(1.15f, 2.45f, 0.35f), 0.5f, new Color(2.2f, 1.3f, 0.5f, 0.8f));
            return b;
        }
    }

    /// <summary>The waystone at camp: an old carved stone whose rune carries Sealers back to town.</summary>
    public class Waystone : Interactable
    {
        public static Waystone Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        Glow _ring;

        public static Waystone Build(Transform parent, Vector3 at, float yaw)
        {
            var root = new GameObject("Waystone");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Landscape.HeightAt(at), at.z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var w = root.AddComponent<Waystone>();
            w.reach = 3f;
            Instance = w;
            var t = root.transform;
            var stone = RealArt.Has ? RealArt.Surface(RealArt.GolemStone, 1.2f, 0.3f) : TownSquare.StoneMaterial;
            TownSquare.MakeBox(t, "Waystone Base", new Vector3(0f, 0.12f, 0f), new Vector3(1.6f, 0.24f, 1.2f), stone, true);
            var body = TownSquare.MakeBox(t, "Waystone", new Vector3(0f, 1.5f, 0f), new Vector3(0.85f, 2.6f, 0.55f), stone, true);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            // The rune that does the carrying, cut into its face.
            Vector3 face = new Vector3(0f, 0f, 0.29f);
            Shapes.Make(PrimitiveType.Cube, t, "Waystone Rune", face + Vector3.up * 1.65f, new Vector3(0.08f, 1.3f, 0.02f), TownSquare.RuneGem, false, true);
            for (int i = 0; i < 3; i++)
                Shapes.Make(PrimitiveType.Cube, t, "Waystone Rune", face + Vector3.up * (1.15f + i * 0.45f), new Vector3(0.36f - i * 0.08f, 0.06f, 0.02f), TownSquare.RuneGem, false, true);
            Glow.Make(t, face + new Vector3(0f, 1.65f, 0.06f), 0.9f, new Color(0.2f, 0.6f, 2f, 0.55f));
            for (int i = 0; i < 16; i++)
            {
                Vector3 q = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * 1.5f;
                var inlay = Shapes.Make(PrimitiveType.Cube, t, "Waystone Ring", q + Vector3.up * 0.03f, new Vector3(i % 2 == 0 ? 0.3f : 0.14f, 0.02f, 0.06f), TownSquare.RuneGem, false, true);
                inlay.transform.localRotation = Quaternion.Euler(0f, i * 22.5f + 90f, 0f);
            }
            w._ring = Glow.MakeRing(t, new Vector3(0f, 0.05f, 0f), 3.4f, new Color(0.6f, 1.2f, 2.6f, 0.8f));
            return w;
        }

        protected override void Update()
        {
            base.Update();
            if (_ring != null) _ring.SetStrength(0.45f + 0.2f * Mathf.Sin(Time.time * 1.4f) + (InReach ? 0.3f : 0f));
        }
    }

    /// <summary>A circle of light that opens by the ruins once the mission is done: step in to go home.</summary>
    public class ReturnCircle : MonoBehaviour
    {
        public const float Radius = 1.9f;
        Glow _ring, _pillar;
        Transform _runes;
        Light _light;
        float _born;

        public static ReturnCircle Build(Vector3 at)
        {
            var root = new GameObject("Return Circle");
            root.transform.position = new Vector3(at.x, Landscape.HeightAt(at), at.z);
            var c = root.AddComponent<ReturnCircle>();
            var t = root.transform;
            c._born = Time.time;
            c._runes = new GameObject("Runes").transform;
            c._runes.SetParent(t, false);
            for (int i = 0; i < 20; i++)
            {
                Vector3 q = Quaternion.Euler(0f, i * 18f, 0f) * Vector3.forward * Radius;
                var r = Shapes.Make(PrimitiveType.Cube, c._runes, "Circle Rune", q + Vector3.up * 0.05f, new Vector3(i % 2 == 0 ? 0.36f : 0.16f, 0.03f, 0.08f), TownSquare.RuneGem, false, true);
                r.transform.localRotation = Quaternion.Euler(0f, i * 18f + 90f, 0f);
            }
            c._ring = Glow.MakeRing(t, new Vector3(0f, 0.08f, 0f), Radius * 2.4f, new Color(0.6f, 1.2f, 2.6f, 0.9f));
            c._pillar = Glow.Make(t, new Vector3(0f, 3.5f, 0f), 1f, new Color(0.5f, 1.1f, 2.6f, 0.6f));
            c._pillar.transform.localScale = new Vector3(1.6f, 8f, 1f);
            c._pillar.name = "Light Pillar";
            var lg = new GameObject("Circle Light");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = Vector3.up * 1.5f;
            c._light = lg.AddComponent<Light>();
            c._light.type = LightType.Point;
            c._light.color = new Color(0.55f, 0.75f, 1f);
            c._light.range = 7f;
            c._light.intensity = 2f;
            c._light.shadows = LightShadows.None;
            SealBurst.Spawn(root.transform.position + Vector3.up, 3f, new Color(0.6f, 0.85f, 1f));
            return c;
        }

        /// <summary>Is the player standing in the circle?</summary>
        public bool PlayerInside
        {
            get
            {
                var m = PlayerMotor.Main;
                if (m == null) return false;
                Vector3 d = m.transform.position - transform.position; d.y = 0f;
                return d.magnitude <= Radius && Time.time - _born > 1f;
            }
        }

        void Update()
        {
            float t = Time.time;
            if (_runes != null) _runes.localRotation = Quaternion.Euler(0f, t * 20f, 0f);
            if (_ring != null) _ring.SetStrength(0.6f + 0.25f * Mathf.Sin(t * 2.2f));
            if (_pillar != null) _pillar.SetStrength(0.55f + 0.2f * Mathf.Sin(t * 1.7f));
            if (_light != null) _light.intensity = 1.8f + 0.5f * Mathf.Sin(t * 2.2f);
        }
    }
}
