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
            new Vector3(0f, 70f, 24f),     // golem arena and its ring of ruins
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

        static bool _real, _nature;

        public static void Dress(Transform field)
        {
            if (!ArtLibrary.HasScenery) return;
            _rng = new System.Random(1286);
            _real = RealArt.Has;
            _nature = _real && RealArt.HasNature;
            var root = new GameObject(_real ? "Scenery (Poly Haven, MegaKit, KayKit; CC0)" : "Scenery (KayKit, CC0)").transform;
            root.SetParent(field, false);

            if (_real && Landscape.Build(root) != null)
            {
                // The painted terrain replaces the flat green ground and the path tiles.
                HideRenderer(ArtLibrary.FindDeep(field, "Ground"));
                HideRenderer(ArtLibrary.FindDeep(field, "Warren Floor")); // the terrain paints trampled earth there
                foreach (Transform t in field) if (t.name == "Path") HideRenderer(t);
            }

            ReskinGreyBox(field);
            PaveTrainingYard(field, root);
            if (!_real) PavePath(field, root);
            BuildForest(root);
            BuildRuins(root, new Vector3(0f, 0f, 70f));
            BuildHorizon(root);
            DressWarren(field, root, new Vector3(-45f, 0f, -15f));

            // Merge the many still pieces into fewer draw calls.
            StaticBatchingUtility.Combine(root.gameObject);
            if (_nature) GrassField.Build(root, p => IsClear(p, 0.4f) || InPlayableClearing(p));
        }

        /// <summary>Grass may grow in the arena and the warren too, just not on the course, path or yard.</summary>
        static bool InPlayableClearing(Vector3 p)
        {
            foreach (var r in ClearRects)
                if (p.x > r.x - 0.4f && p.x < r.z + 0.4f && p.z > r.y - 0.4f && p.z < r.w + 0.4f) return false;
            return OnGround(p, 1f);
        }

        static void HideRenderer(Transform t)
        {
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        // ---------- Pieces ----------

        static GameObject Place(Transform parent, string path, Vector3 pos, float yaw, float scale, float sink = 0.05f)
        {
            var go = ArtLibrary.Spawn(path, parent);
            return Settle(go, pos, yaw, scale, sink);
        }

        /// <summary>Puts a spawned model on the ground at <paramref name="pos"/> (following the hills).</summary>
        static GameObject Settle(GameObject go, Vector3 pos, float yaw, float scale, float sink)
        {
            if (go == null) return null;
            pos.y += Landscape.HeightAt(pos);
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
            => FitHeight(Place(parent, path, pos, Range(0f, 360f), 1f, 0f), pos, height, sink);

        static GameObject FitHeight(GameObject go, Vector3 pos, float height, float sink)
        {
            if (go == null) return null;
            var b = ArtLibrary.WorldBounds(go);
            if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
            ArtLibrary.SetBottom(go, pos.y + Landscape.HeightAt(pos) - sink);
            return go;
        }

        /// <summary>A Nature MegaKit model (tree, bush, plant) a set height tall.</summary>
        static GameObject PlaceNature(Transform parent, string name, Vector3 pos, float height, float sink = 0.1f)
        {
            var go = Settle(RealArt.Nature(name, parent), pos, Range(0f, 360f), 1f, 0f);
            go = FitHeight(go, pos, height, sink);
            // Small plants vanish while they are still only a few pixels tall; trees stay as far as the eye sees.
            CullWhenSmall(go, height > 5f ? 0.006f : height > 1.5f ? 0.02f : 0.035f);
            return go;
        }

        /// <summary>Stops drawing <paramref name="go"/> once it is shorter than <paramref name="screenFraction"/> of the screen.</summary>
        static void CullWhenSmall(GameObject go, float screenFraction)
        {
            if (go == null || go.GetComponent<LODGroup>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(screenFraction, renderers) });
            lod.RecalculateBounds();
        }

        /// <summary>A scanned rock or stump, its largest side <paramref name="size"/> metres.</summary>
        static GameObject PlaceScan(Transform parent, string slug, Vector3 pos, float size, float sink, float moss = 0f)
        {
            var go = RealArt.Scan(slug, parent, size, moss);
            if (go == null) return null;
            var p = pos; p.y += Landscape.HeightAt(pos);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f));
            ArtLibrary.SetBottom(go, p.y - sink);
            go.isStatic = true;
            CullWhenSmall(go, size > 4f ? 0.006f : size > 1.2f ? 0.015f : 0.03f);
            return go;
        }

        static void AddTrunkMetres(GameObject go, float radius, float height)
        {
            if (go == null) return;
            var cap = go.AddComponent<CapsuleCollider>();
            float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            cap.radius = radius / s;
            cap.height = height / s;
            cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
        }

        static readonly string[] NatureTrees = { "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5" };
        static readonly string[] NaturePines = { "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5" };
        static readonly string[] NatureTwisted = { "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "TwistedTree_4", "TwistedTree_5" };
        static readonly string[] NatureDead = { "DeadTree_1", "DeadTree_2", "DeadTree_3" };
        static readonly string[] NatureBushes = { "Bush_Common", "Bush_Common_Flowers", "Bush_Common_Flowers", "Fern_1", "Fern_1", "Plant_1", "Plant_7" };
        static readonly string[] ScanSmallRocks = { "rock_07", "rock_09", "stone_01" };
        static readonly string[] ScanBigRocks = { "boulder_01", "namaqualand_boulder_02", "rock_moss_set_01", "rock_moss_set_02" };

        /// <summary>A tree that fits the highland: mostly pines, with broadleaf and the odd twisted or dead one.</summary>
        static GameObject PlaceTree(Transform parent, Vector3 p, bool solid)
        {
            float roll = Range(0f, 1f);
            string name; float height;
            if (roll < 0.58f) { name = Pick(NaturePines); height = Range(11f, 19f); }
            else if (roll < 0.86f) { name = Pick(NatureTrees); height = Range(8f, 13f); }
            else if (roll < 0.9f) { name = Pick(NatureTwisted); height = Range(7f, 11f); } // red autumn crowns: a few accents
            else { name = Pick(NatureDead); height = Range(6f, 10f); }
            var tree = PlaceNature(parent, name, p, height, 0.15f);
            if (solid) AddTrunkMetres(tree, 0.5f, 6f);
            return tree;
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
                if (piece != null) ArtLibrary.FitInsideBox(t, ArtLibrary.Dungeon + piece);
            }
        }

        static void PaveTrainingYard(Transform field, Transform root)
        {
            var floor = ArtLibrary.FindDeep(field, "Yard Floor");
            if (floor == null) return;
            var r = floor.GetComponent<Renderer>();
            if (_real && r != null)
            {
                r.sharedMaterial = RealArt.Surface(RealArt.Blocks, 2.2f, 0.12f);
                PlaceYardProps(root);
                return;
            }
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

            PlaceYardProps(root);
        }

        static void PlaceYardProps(Transform root)
        {
            if (_real) return; // the chunky cartoon props look like toys next to the realistic heroes
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
            if (_nature) { BuildRealForest(forest, centre); return; }

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

        /// <summary>The highland forest from the painted nature pack and the scanned rocks.</summary>
        static void BuildRealForest(Transform forest, Vector3 centre)
        {
            // The surrounding forest, climbing the hills: denser further out.
            for (int i = 0; i < 620; i++)
            {
                float r = Mathf.Lerp(55f, 230f, Mathf.Sqrt(Range(0f, 1f)));
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * r;
                if (!InWorld(p, 4f) || !IsClear(p, 5f, worldEdge: false)) continue;
                PlaceTree(forest, p, r < 120f);
            }
            // Clumps of trees closer in, so the field has shape.
            for (int c = 0; c < 9; c++)
            {
                Vector3 clump = new Vector3(Range(-60f, 60f), 0f, Range(-40f, 125f));
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = clump + new Vector3(Range(-9f, 9f), 0f, Range(-9f, 9f));
                    if (!IsClear(p, 6f)) continue;
                    PlaceTree(forest, p, true);
                }
            }
            for (int i = 0; i < 180; i++)
            {
                Vector3 p = new Vector3(Range(-95f, 95f), 0f, Range(-60f, 160f));
                if (!IsClear(p, 2f)) continue;
                PlaceNature(forest, Pick(NatureBushes), p, Range(0.6f, 1.4f), 0.05f);
            }
            // Fallen wood: stumps, dead trunks and branches under the trees.
            for (int i = 0; i < 50; i++)
            {
                float r = Mathf.Lerp(45f, 140f, Range(0f, 1f));
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * r;
                if (!IsClear(p, 2f, worldEdge: false)) continue;
                string slug = Pick("tree_stump_01", "tree_stump_01", "dead_tree_trunk");
                float size = slug == "dead_tree_trunk" ? Range(4f, 7f) : Range(0.8f, 1.4f);
                var wood = PlaceScan(forest, slug, p, size, 0.1f);
                if (slug == "dead_tree_trunk") AddBox(wood, 0.8f);
            }
            // Rocks: pebbles to boulders you can hide behind, mossy where they sit in the grass.
            for (int i = 0; i < 120; i++)
            {
                Vector3 p = new Vector3(Range(-100f, 100f), 0f, Range(-60f, 165f));
                if (!IsClear(p, 1.5f)) continue;
                PlaceScan(forest, Pick(ScanSmallRocks), p, Range(0.3f, 1.2f), 0.12f, 0.25f);
            }
            for (int i = 0; i < 30; i++)
            {
                Vector3 p = new Vector3(Range(-110f, 110f), 0f, Range(-65f, 175f));
                if (!IsClear(p, 7f)) continue;
                AddBox(PlaceScan(forest, Pick(ScanBigRocks), p, Range(3f, 7.5f), 0.5f, 0.35f), 0.75f);
            }
        }

        /// <summary>The Runestone Ruins: broken walls and pillars around the golem's arena.</summary>
        static void BuildRuins(Transform root, Vector3 centre)
        {
            var ruins = new GameObject("Runestone Ruins").transform;
            ruins.SetParent(root, false);

            // Old paving under the arena (the real look uses the terrain's trail instead).
            if (!_real)
            for (float x = -12f; x <= 12f; x += 4f)
                for (float z = -12f; z <= 12f; z += 4f)
                {
                    if (x * x + z * z > 13f * 13f || Range(0f, 1f) < 0.12f) continue;
                    var tile = Place(ruins, ArtLibrary.Dungeon + Pick("floor_tile_large", "floor_tile_large_rocks", "floor_dirt_large_rocky"),
                        centre + new Vector3(x, 0f, z), Pick(0f, 90f, 180f, 270f), 1f, 0f);
                    if (tile != null) SetTop(tile, 0.03f);
                }

            // A broken ring of walls and pillars, with gaps to run through. It stands outside the
            // golem's arena, so the fight stays open and the golem can be seen whole.
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                if (i % 4 == 1) continue; // gaps
                float a = i * 360f / count + Range(-4f, 4f);
                float r = Range(22f, 25f);
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
                        p + Quaternion.Euler(0f, a, 0f) * new Vector3(Range(-2f, 2f), 0f, Range(1.5f, 3f)), Range(0f, 360f), Range(1.2f, 2f));
            }

            // Two toppled pillars at the arena's edge (low, so they don't hide the golem's legs).
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, 70f + i * 200f, 0f) * Vector3.forward * Range(17f, 19f);
                var fallen = Place(ruins, ArtLibrary.Dungeon + "pillar", p, Range(0f, 360f), 1.6f, 0f);
                if (fallen == null) continue;
                fallen.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 90f);
                ArtLibrary.SetBottom(fallen, -0.6f);
                AddBox(fallen, 1f);
            }
        }

        /// <summary>Huge rocks far away, so the world feels big.</summary>
        static void BuildHorizon(Transform root)
        {
            var horizon = new GameObject("Horizon Rocks").transform;
            horizon.SetParent(root, false);
            if (_real)
            {
                // Rock faces and boulders breaking out of the hills around the valley.
                for (int i = 0; i < 26; i++)
                {
                    float a = i * (360f / 26f) + Range(-5f, 5f);
                    Vector3 p = new Vector3(-20f, 0f, 55f) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(190f, 260f);
                    if (!InWorld(p, 10f)) continue;
                    var cliff = PlaceScan(horizon, Pick("rock_face_01", "rock_face_02", "boulder_01", "namaqualand_boulder_02"), p, Range(25f, 55f), Range(4f, 10f), 0.3f);
                    if (cliff != null) cliff.transform.rotation = Quaternion.Euler(0f, a + 180f + Range(-30f, 30f), 0f);
                }
                return;
            }
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
                    var rock = _real
                        ? PlaceScan(deco, Pick(ScanBigRocks), t.position, 3f, 0f, 0.3f)
                        : Place(deco, ArtLibrary.Forest + Pick("Rock_3_B_Color1", "Rock_3_D_Color1", "Rock_3_F_Color1", "Rock_3_H_Color1"),
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
                        if (_real) PlaceScan(deco, Pick(ScanSmallRocks), p, Range(0.4f, 0.9f), 0.1f, 0.2f);
                        else PlaceHeight(deco, ArtLibrary.Forest + Pick(SmallRocks), p, Range(0.35f, 0.8f), 0.1f);
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
            string[] clutter = _real
                ? new[] { "barrel_small", "box_small", "trunk_small_A", "keg", "chest" } // the toy-like small props stay out of the real look
                : new[] { "barrel_small", "box_small", "trunk_small_A", "trunk_medium_B", "keg", "bottle_A_brown", "plate_food_A", "coin_stack_small", "chest" };
            for (int i = 0; i < (_real ? 6 : 14); i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(12f, 16f);
                Place(deco, ArtLibrary.Dungeon + clutter[i % clutter.Length], p, Range(0f, 360f), 1.3f);
            }

            // A ring of forest around the warren, so it sits in a clearing.
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = centre + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(21f, 34f);
                if (!IsClear(p, 3f, ignoreWarren: true)) continue;
                if (_nature) { PlaceTree(deco, p, true); continue; }
                float s = Range(1f, 1.6f);
                AddTrunk(Place(deco, ArtLibrary.Forest + Pick(Trees), p, Range(0f, 360f), s, 0.1f), s);
            }
        }

        // ---------- Helpers ----------

        static bool OnGround(Vector3 p, float margin) => Mathf.Abs(p.x) < 200f - margin && Mathf.Abs(p.z) < 200f - margin;

        /// <summary>Inside the terrain when there is one (it reaches further than the old ground).</summary>
        static bool InWorld(Vector3 p, float margin) =>
            Landscape.Terrain == null ? OnGround(p, margin) : p.x > -400f + margin && p.x < 360f - margin && p.z > -320f + margin && p.z < 440f - margin;

        static bool IsClear(Vector3 p, float margin, bool ignoreWarren = false, bool worldEdge = true)
        {
            if (worldEdge ? !OnGround(p, margin) : !InWorld(p, margin)) return false;
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
