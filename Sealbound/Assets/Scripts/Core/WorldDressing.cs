using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// Dresses the grey-box test field with the free KayKit forest and dungeon pieces: a forest around
    /// the field, rocks and grass, ruins around the golem's arena, a paved training yard and path,
    /// torches and clutter in the goblin warren, and giant rocks on the horizon for a sense of scale.
    /// The gameplay shapes underneath stay as they are, so movement and combat feel the same.
    /// </summary>
    public static class WorldDressing
    {
        static System.Random _rng;

        // Places that must stay clear for play (centre x, centre z, radius).
        static readonly Vector3[] ClearCircles =
        {
            new Vector3(0f, 70f, 15f),     // golem arena
            new Vector3(-45f, -15f, 18f),  // goblin warren
            new Vector3(-90f, 160f, 13f),  // Legend tower marker
            new Vector3(0f, -6f, 7f),      // start point
        };
        // Rectangles (min x, min z, max x, max z).
        static readonly Vector4[] ClearRects =
        {
            new Vector4(-5f, -44f, 5f, 124f),   // the path
            new Vector4(-3f, -17f, 17f, 1f),    // training yard
            new Vector4(-29f, -3f, 33f, 63f),   // movement course
        };

        static readonly string[] Trees =
        {
            "Tree_1_A_Color1", "Tree_1_B_Color1", "Tree_1_C_Color1", "Tree_2_A_Color1", "Tree_2_B_Color1", "Tree_2_C_Color1",
            "Tree_2_D_Color1", "Tree_2_E_Color1", "Tree_3_A_Color1", "Tree_3_B_Color1", "Tree_3_C_Color1", "Tree_4_A_Color1",
            "Tree_4_B_Color1", "Tree_4_C_Color1",
        };
        static readonly string[] BareTrees =
        {
            "Tree_Bare_1_A_Color1", "Tree_Bare_1_B_Color1", "Tree_Bare_1_C_Color1", "Tree_Bare_2_A_Color1", "Tree_Bare_2_B_Color1", "Tree_Bare_2_C_Color1",
        };
        static readonly string[] Bushes =
        {
            "Bush_1_A_Color1", "Bush_1_C_Color1", "Bush_1_E_Color1", "Bush_2_A_Color1", "Bush_2_C_Color1", "Bush_2_E_Color1",
            "Bush_3_A_Color1", "Bush_3_B_Color1", "Bush_4_A_Color1", "Bush_4_C_Color1", "Bush_4_E_Color1",
        };
        static readonly string[] Grass =
        {
            "Grass_1_A_Color1", "Grass_1_B_Color1", "Grass_1_C_Color1", "Grass_1_D_Color1",
            "Grass_2_A_Color1", "Grass_2_B_Color1", "Grass_2_C_Color1", "Grass_2_D_Color1",
        };
        static readonly string[] SmallRocks =
        {
            "Rock_1_A_Color1", "Rock_1_C_Color1", "Rock_1_E_Color1", "Rock_1_G_Color1", "Rock_1_I_Color1", "Rock_1_K_Color1",
            "Rock_1_M_Color1", "Rock_1_O_Color1", "Rock_2_A_Color1", "Rock_2_C_Color1", "Rock_2_E_Color1", "Rock_2_G_Color1",
        };
        static readonly string[] BigRocks =
        {
            "Rock_3_A_Color1", "Rock_3_C_Color1", "Rock_3_E_Color1", "Rock_3_G_Color1", "Rock_3_I_Color1", "Rock_3_K_Color1",
            "Rock_3_M_Color1", "Rock_3_O_Color1", "Rock_3_Q_Color1", "Rock_3_R_Color1",
        };

        public static void Dress(Transform field)
        {
            if (!ArtLibrary.HasScenery) return;
            _rng = new System.Random(1286);
            var root = new GameObject("Scenery (KayKit, CC0)").transform;
            root.SetParent(field, false);

            ReskinGreyBox(field);
            PaveTrainingYard(field, root);
            PavePath(field, root);
            BuildForest(root);
            BuildRuins(root, new Vector3(0f, 0f, 70f));
            BuildHorizon(root);
            DressWarren(field, root, new Vector3(-45f, 0f, -15f));

            // Merge the many still pieces into fewer draw calls.
            StaticBatchingUtility.Combine(root.gameObject);
        }

        // ---------- Pieces ----------

        static GameObject Place(Transform parent, string path, Vector3 pos, float yaw, float scale, float sink = 0.05f)
        {
            var go = ArtLibrary.Spawn(path, parent);
            if (go == null) return null;
            var t = go.transform;
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale *= scale;
            ArtLibrary.SetBottom(go, pos.y - sink);
            go.isStatic = true;
            return go;
        }

        /// <summary>Places a model scaled to a height in metres, whatever size it was made at.</summary>
        static GameObject PlaceHeight(Transform parent, string path, Vector3 pos, float height, float sink)
        {
            var go = Place(parent, path, pos, Range(0f, 360f), 1f, 0f);
            if (go == null) return null;
            var b = ArtLibrary.WorldBounds(go);
            if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
            ArtLibrary.SetBottom(go, pos.y - sink);
            return go;
        }

        static void SetTop(GameObject go, float y)
        {
            var b = ArtLibrary.WorldBounds(go);
            go.transform.position += Vector3.up * (y - b.max.y);
        }

        static void AddBox(GameObject go, float shrink = 0.9f)
        {
            if (go == null) return;
            Bounds lb = BoneAim.LocalMeshBounds(go.transform);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = lb.center;
            bc.size = new Vector3(lb.size.x * shrink, lb.size.y, lb.size.z * shrink);
        }

        static void AddMeshColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        static void AddTrunk(GameObject go, float scale)
        {
            if (go == null) return;
            var cap = go.AddComponent<CapsuleCollider>();
            float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            cap.radius = 0.45f * scale / s;
            cap.height = 6f / s;
            cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
        }

        // ---------- Areas ----------

        /// <summary>
        /// Gives the movement course, the golem stand-in and the 60 m marker real stone and wood,
        /// stretched to exactly the same shapes, so jumps and collisions don't change.
        /// </summary>
        static void ReskinGreyBox(Transform field)
        {
            foreach (var t in field.GetComponentsInChildren<Transform>())
            {
                string piece = null;
                string n = t.name;
                if (n.StartsWith("Ramp")) piece = "floor_tile_large";
                else if (n == "Stair" || n == "Stair Top" || n == "Platform") piece = "floor_foundation_allsides";
                else if (n == "Pillar") piece = "pillar";
                else if (n == "Wall") piece = "wall";
                else if (n == "Roof") piece = "floor_tile_large";
                else if (n == "Leg L" || n == "Leg R" || n == "Arm L" || n == "Arm R") piece = "pillar_decorated";
                else if (n == "Body" && t.parent != null && t.parent.name.StartsWith("Bastion Golem")) piece = "floor_foundation_allsides";
                else if (n == "Head" && t.parent != null && t.parent.name.StartsWith("Bastion Golem")) piece = "floor_foundation_allsides";
                else if (n == "Tower") piece = "pillar_decorated";
                else if (n == "Crown") piece = "floor_foundation_allsides";
                if (piece != null) Reskin(t, ArtLibrary.Dungeon + piece);
            }
        }

        /// <summary>Hides a box shape and fits a model inside it exactly. The box's collider stays.</summary>
        static void Reskin(Transform box, string path)
        {
            var r = box.GetComponent<Renderer>();
            var mf = box.GetComponent<MeshFilter>();
            if (r == null || mf == null || !SkinnedOutline.IsBuiltInShape(mf.sharedMesh)) return;
            var go = ArtLibrary.Spawn(path, box);
            if (go == null) return;
            var t = go.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            Bounds lb = BoneAim.LocalMeshBounds(t);
            Vector3 size = lb.size;
            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f) { Object.Destroy(go); return; }

            // Keep the piece's long side along the box's long side.
            Vector3 boxSize = box.lossyScale;
            bool turn = (boxSize.z > boxSize.x) != (size.z > size.x);
            Quaternion rot = turn ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            Vector3 scale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            t.localRotation = rot;
            t.localScale = scale;
            t.localPosition = -(rot * Vector3.Scale(lb.center, scale));
            r.enabled = false;
            go.isStatic = true;
        }

        static void PaveTrainingYard(Transform field, Transform root)
        {
            var floor = ArtLibrary.FindDeep(field, "Yard Floor");
            if (floor == null) return;
            var r = floor.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            float top = floor.position.y + floor.lossyScale.y * 0.5f;
            Vector3 c = floor.position, size = floor.lossyScale;
            for (float x = c.x - size.x * 0.5f + 2f; x < c.x + size.x * 0.5f; x += 4f)
                for (float z = c.z - size.z * 0.5f + 2f; z < c.z + size.z * 0.5f + 1f; z += 4f)
                {
                    string tile = Pick("floor_tile_large", "floor_tile_large", "floor_tile_large", "floor_tile_large_rocks");
                    var go = Place(root, ArtLibrary.Dungeon + tile, new Vector3(x, 0f, z), Pick(0f, 90f, 180f, 270f), 1f, 0f);
                    if (go != null) SetTop(go, top);
                }

            // A weapon rack, barrels and crates around the yard's edge.
            Place(root, ArtLibrary.Dungeon + "sword_shield", new Vector3(15.5f, 0f, -14f), -90f, 1.4f);
            Place(root, ArtLibrary.Dungeon + "sword_shield_gold", new Vector3(15.5f, 0f, -10f), -90f, 1.4f);
            AddBox(Place(root, ArtLibrary.Dungeon + "barrel_large", new Vector3(-0.5f, 0f, -14.5f), 20f, 1.3f));
            AddBox(Place(root, ArtLibrary.Dungeon + "barrel_small_stack", new Vector3(1f, 0f, -15.5f), 75f, 1.3f));
            AddBox(Place(root, ArtLibrary.Dungeon + "crates_stacked", new Vector3(15f, 0f, -2.5f), 10f, 1.3f));
            AddBox(Place(root, ArtLibrary.Dungeon + "box_stacked", new Vector3(-0.5f, 0f, -2f), -30f, 1.3f));
            Place(root, ArtLibrary.Dungeon + "banner_patternA_blue", new Vector3(7f, 0f, -15.6f), 0f, 1.5f);
        }

        static void PavePath(Transform field, Transform root)
        {
            foreach (Transform t in field)
            {
                if (t.name != "Path") continue;
                var r = t.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                string tile = Pick("floor_tile_small", "floor_tile_small", "floor_tile_small_broken_A", "floor_tile_small_broken_B",
                    "floor_tile_small_weeds_A", "floor_tile_small_weeds_B");
                var go = Place(root, ArtLibrary.Dungeon + tile, t.position, Pick(0f, 180f), 1f, 0f);
                if (go == null) continue;
                go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(1.5f, 1f, 1f));
                SetTop(go, t.position.y + t.lossyScale.y * 0.5f);
            }
        }

        static void BuildForest(Transform root)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(root, false);
            Vector3 centre = new Vector3(0f, 0f, 40f);

            // The surrounding forest: denser further out.
            for (int i = 0; i < 520; i++)
            {
                float r = Mathf.Lerp(55f, 185f, Mathf.Sqrt(Range(0f, 1f)));
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * r;
                if (!OnGround(p, 4f) || !IsClear(p, 5f)) continue;
                float s = Range(1.05f, 1.8f);
                var tree = Place(forest, ArtLibrary.Forest + (Range(0f, 1f) < 0.08f ? Pick(BareTrees) : Pick(Trees)), p, Range(0f, 360f), s, 0.1f);
                if (r < 110f) AddTrunk(tree, s);
            }

            // Scattered single trees and bushes closer in, so the field isn't bare.
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = new Vector3(Range(-60f, 60f), 0f, Range(-45f, 120f));
                if (!IsClear(p, 6f)) continue;
                float s = Range(1f, 1.5f);
                AddTrunk(Place(forest, ArtLibrary.Forest + Pick(Trees), p, Range(0f, 360f), s, 0.1f), s);
            }
            for (int i = 0; i < 160; i++)
            {
                Vector3 p = new Vector3(Range(-80f, 80f), 0f, Range(-50f, 140f));
                if (!IsClear(p, 2f)) continue;
                PlaceHeight(forest, ArtLibrary.Forest + Pick(Bushes), p, Range(0.7f, 1.5f), 0.05f);
            }

            // Grass tufts everywhere you play (they don't block anything).
            for (int i = 0; i < 700; i++)
            {
                Vector3 p = new Vector3(Range(-70f, 70f), 0f, Range(-50f, 130f));
                if (!IsClear(p, 0.6f)) continue;
                PlaceHeight(forest, ArtLibrary.Forest + Pick(Grass), p, Range(0.3f, 0.55f), 0.02f);
            }

            // Rocks: pebbles to boulders you can hide behind.
            for (int i = 0; i < 110; i++)
            {
                Vector3 p = new Vector3(Range(-90f, 90f), 0f, Range(-55f, 150f));
                if (!IsClear(p, 1.5f)) continue;
                PlaceHeight(forest, ArtLibrary.Forest + Pick(SmallRocks), p, Range(0.3f, 1.1f), 0.1f);
            }
            for (int i = 0; i < 26; i++)
            {
                Vector3 p = new Vector3(Range(-100f, 100f), 0f, Range(-60f, 160f));
                if (!IsClear(p, 7f)) continue;
                AddMeshColliders(Place(forest, ArtLibrary.Forest + Pick(BigRocks), p, Range(0f, 360f), Range(0.8f, 2.2f), 0.3f));
            }
        }

        /// <summary>The Runestone Ruins: broken walls and pillars around the golem's arena.</summary>
        static void BuildRuins(Transform root, Vector3 centre)
        {
            var ruins = new GameObject("Runestone Ruins").transform;
            ruins.SetParent(root, false);

            // Old paving under the arena.
            for (float x = -12f; x <= 12f; x += 4f)
                for (float z = -12f; z <= 12f; z += 4f)
                {
                    if (x * x + z * z > 13f * 13f || Range(0f, 1f) < 0.12f) continue;
                    var tile = Place(ruins, ArtLibrary.Dungeon + Pick("floor_tile_large", "floor_tile_large_rocks", "floor_dirt_large_rocky"),
                        centre + new Vector3(x, 0f, z), Pick(0f, 90f, 180f, 270f), 1f, 0f);
                    if (tile != null) SetTop(tile, 0.03f);
                }

            // A broken ring of walls and pillars, with gaps to run through.
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                if (i % 4 == 1) continue; // gaps
                float a = i * 360f / count + Range(-4f, 4f);
                float r = Range(17f, 20f);
                Vector3 p = centre + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                if (Mathf.Abs(p.x) < 5f && p.z < centre.z) continue; // keep the path's entrance open
                bool pillar = i % 2 == 0;
                string piece = pillar ? Pick("pillar", "pillar_decorated", "column", "barrier_column")
                                      : Pick("wall_broken", "wall_cracked", "wall_half", "wall_arched", "wall", "wall_broken");
                float s = pillar ? Range(1.6f, 2.4f) : Range(1.3f, 1.8f);
                var go = Place(ruins, ArtLibrary.Dungeon + piece, p, a + 90f + Range(-8f, 8f), s, Range(0.1f, 0.6f));
                AddBox(go);
                if (Range(0f, 1f) < 0.6f)
                    Place(ruins, ArtLibrary.Dungeon + Pick("rubble_large", "rubble_half"),
                        p + Quaternion.Euler(0f, a, 0f) * new Vector3(Range(-2f, 2f), 0f, Range(-3f, -1.5f)), Range(0f, 360f), Range(1.2f, 2f));
            }

            // A few toppled pillars and stones inside the arena.
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(6f, 12f);
                if (Mathf.Abs(p.x) < 4f) continue;
                Place(ruins, ArtLibrary.Dungeon + Pick("rubble_large", "rubble_half"), p, Range(0f, 360f), Range(1.3f, 2.2f));
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, 60f + i * 110f, 0f) * Vector3.forward * Range(9f, 12f);
                var fallen = Place(ruins, ArtLibrary.Dungeon + "pillar", p, Range(0f, 360f), 1.8f, 0f);
                if (fallen == null) continue;
                fallen.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 90f);
                ArtLibrary.SetBottom(fallen, -0.4f);
                AddBox(fallen, 1f);
            }
        }

        /// <summary>Huge rocks far away, so the world feels big.</summary>
        static void BuildHorizon(Transform root)
        {
            var horizon = new GameObject("Horizon Rocks").transform;
            horizon.SetParent(root, false);
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f + Range(-6f, 6f);
                Vector3 p = new Vector3(0f, 0f, 40f) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(175f, 195f);
                Place(horizon, ArtLibrary.Forest + Pick(BigRocks), p, Range(0f, 360f), Range(6f, 12f), 2f);
            }
        }

        static void DressWarren(Transform field, Transform root, Vector3 centre)
        {
            var warren = ArtLibrary.FindDeep(field, "Goblin Warren");
            var deco = new GameObject("Warren Dressing").transform;
            deco.SetParent(root, false);

            if (warren != null)
            {
                // Swap the boulder shapes for real rocks (the shapes stay as invisible cover).
                foreach (Transform t in warren)
                {
                    if (t.name != "Boulder") continue;
                    var r = t.GetComponent<Renderer>();
                    if (r == null) continue;
                    r.enabled = false;
                    var rock = Place(deco, ArtLibrary.Forest + Pick("Rock_3_B_Color1", "Rock_3_D_Color1", "Rock_3_F_Color1", "Rock_3_H_Color1"),
                        t.position, Range(0f, 360f), 1f, 0f);
                    if (rock == null) continue;
                    var rb = ArtLibrary.WorldBounds(rock);
                    float fit = Mathf.Max(t.lossyScale.x, t.lossyScale.z) * 1.1f / Mathf.Max(0.01f, Mathf.Max(rb.size.x, rb.size.z));
                    rock.transform.localScale *= fit;
                    ArtLibrary.SetBottom(rock, -0.2f);
                }

                // Rocks around each burrow mouth.
                foreach (Transform t in warren)
                {
                    if (t.name != "Burrow Mound") continue;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 p = t.position + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(1.4f, 1.9f);
                        PlaceHeight(deco, ArtLibrary.Forest + Pick(SmallRocks), p, Range(0.35f, 0.8f), 0.1f);
                    }
                }
            }

            // Torches and goblin clutter.
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, i * 60f + 45f, 0f) * Vector3.forward * 13f;
                var torch = Place(deco, ArtLibrary.Dungeon + "torch_lit", p, Range(0f, 360f), 2f, 0f);
                if (torch == null) continue;
                var b = ArtLibrary.WorldBounds(torch);
                var lightGo = new GameObject("Torch Light");
                lightGo.transform.SetParent(torch.transform, false);
                lightGo.transform.position = new Vector3(b.center.x, b.max.y + 0.2f, b.center.z);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.6f, 0.25f);
                l.range = 7f;
                l.intensity = 2.5f;
                l.shadows = LightShadows.None;
            }
            string[] clutter = { "barrel_small", "box_small", "trunk_small_A", "trunk_medium_B", "keg", "bottle_A_brown", "plate_food_A", "coin_stack_small", "chest" };
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(12f, 16f);
                Place(deco, ArtLibrary.Dungeon + clutter[i % clutter.Length], p, Range(0f, 360f), 1.3f);
            }

            // A ring of forest around the warren, so it sits in a clearing.
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(21f, 34f);
                if (!IsClear(p, 3f, ignoreWarren: true)) continue;
                float s = Range(1f, 1.6f);
                AddTrunk(Place(deco, ArtLibrary.Forest + Pick(Trees), p, Range(0f, 360f), s, 0.1f), s);
            }
        }

        // ---------- Helpers ----------

        static bool OnGround(Vector3 p, float margin) => Mathf.Abs(p.x) < 200f - margin && Mathf.Abs(p.z) < 200f - margin;

        static bool IsClear(Vector3 p, float margin, bool ignoreWarren = false)
        {
            if (!OnGround(p, margin)) return false;
            foreach (var c in ClearCircles)
            {
                if (ignoreWarren && c.x == -45f) continue;
                float dx = p.x - c.x, dz = p.z - c.y;
                float r = c.z + margin;
                if (dx * dx + dz * dz < r * r) return false;
            }
            if (ignoreWarren)
            {
                float dx = p.x + 45f, dz = p.z + 15f;
                if (dx * dx + dz * dz < 19f * 19f) return false;
            }
            foreach (var r in ClearRects)
                if (p.x > r.x - margin && p.x < r.z + margin && p.z > r.y - margin && p.z < r.w + margin) return false;
            return true;
        }

        static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        static T Pick<T>(params T[] items) => items[_rng.Next(items.Length)];
    }
}
