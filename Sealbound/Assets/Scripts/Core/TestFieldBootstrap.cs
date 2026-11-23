using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using Sealbound.Enemies;
using Sealbound.Player;
using Sealbound.UI;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// Milestone 1 test field. When a scene with no player is played, this builds a grey-box
    /// training ground, a stand-in Sealer recruit and the follow camera, so pressing Play is all it takes.
    /// </summary>
    public static class TestFieldBootstrap
    {
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        static Material _baseMaterial;

        static readonly Color Grass = new Color(0.42f, 0.6f, 0.3f);
        static readonly Color Stone = new Color(0.58f, 0.56f, 0.52f);
        static readonly Color DarkStone = new Color(0.38f, 0.37f, 0.36f);
        static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
        static readonly Color Rune = new Color(0.35f, 0.75f, 0.95f);
        static readonly Color RealmBrown = new Color(0.45f, 0.30f, 0.18f);
        static readonly Color Iron = new Color(0.72f, 0.72f, 0.76f);
        static readonly Color SwordRune = new Color(0.36f, 0.76f, 0.96f); // own material: it flashes on the beat

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Build()
        {
            if (Object.FindAnyObjectByType<PlayerMotor>() != null) return; // a real level is in charge

            Materials.Clear();
            var root = new GameObject("Test Field").transform;

            EnsureLight();
            BuildGround(root);
            BuildCourse(root);
            BuildScaleMarkers(root);
            BuildTrainingYard(root);

            var player = BuildPlayer(new Vector3(0f, 0.1f, -6f));
            SetupCamera(player);
            LookSettings.Apply(Camera.main);
            var heroAvatar = player.GetComponent<PlayerAvatar>();
            if (heroAvatar == null || !heroAvatar.IsRealistic) ToonOutline.Add(player.GetComponentsInChildren<Renderer>(true)); // ink lines suit the cartoon heroes only
            if (!RealArt.Has)
                foreach (var d in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
                    ToonOutline.Add(d.GetComponentsInChildren<Renderer>());
            var warren = BuildGoblinWarren(root, new Vector3(-45f, 0f, -15f), player.transform);
            WorldDressing.Dress(root); // free KayKit trees, rocks and ruins, when the art is in the project
            ForgeStation.Build(root, new Vector3(-9f, 0f, -3f), 90f); // the Sealers' forge, near where you start
            SoulShrine.Build(root, new Vector3(-10f, 0f, -12f), 59f); // the soul shrine, where spirits are bound
            // Built after the scenery so the grey-box reskin leaves the golem's own stone alone.
            var golem = BastionGolem.Create(new Vector3(0f, 0f, 70f), 180f);
            var hud = Object.FindAnyObjectByType<DebugHud>();
            if (hud != null) { hud.warren = warren; hud.boss = golem; }

            // The bot squad: computer-controlled Sealers built exactly like you. B (d-pad left) changes how many.
            SquadBot.Factory = (at, mage) => BuildSealer("Bot Sealer", at, true);
            player.AddComponent<SquadCaller>();
            SquadBot.SetSquadSize(SquadBot.MaxSquad);
        }

        // ---------- World ----------

        static void EnsureLight()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        static void BuildGround(Transform root)
        {
            var ground = Box(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(400f, 1f, 400f), Grass);
            ground.isStatic = true;

            // Darker path tiles so speed is easy to read while running.
            for (int i = -10; i <= 30; i++)
                Box(root, "Path", new Vector3(0f, 0.01f, i * 4f), new Vector3(3f, 0.02f, 2f), DarkStone);
        }

        static void BuildCourse(Transform root)
        {
            // Ramps of rising steepness.
            Ramp(root, new Vector3(10f, 0f, 6f), 12f, 4f, 15f);
            Ramp(root, new Vector3(16f, 0f, 6f), 12f, 4f, 30f);
            Ramp(root, new Vector3(22f, 0f, 6f), 12f, 4f, 45f); // too steep to walk up

            // Stairs.
            for (int i = 0; i < 10; i++)
                Box(root, "Stair", new Vector3(-12f, 0.2f + i * 0.4f, 4f + i * 0.7f), new Vector3(4f, 0.4f, 0.7f), Stone);
            Box(root, "Stair Top", new Vector3(-12f, 3.8f, 13f), new Vector3(6f, 0.4f, 6f), Stone);

            // Jumping platforms with growing gaps.
            float z = 20f, h = 1f;
            for (int i = 0; i < 7; i++)
            {
                Box(root, "Platform", new Vector3(-4f + (i % 2) * 8f, h * 0.5f, z), new Vector3(3f, h, 3f), Wood);
                z += 4.5f + i * 0.4f;
                h += 0.6f;
            }

            // Pillars to weave through while sprinting and dodging.
            for (int x = 0; x < 5; x++)
            for (int zz = 0; zz < 4; zz++)
                Box(root, "Pillar", new Vector3(10f + x * 4f + (zz % 2) * 2f, 2f, 28f + zz * 4f), new Vector3(1f, 4f, 1f), Stone);

            // A low wall and a gap to test camera collision.
            Box(root, "Wall", new Vector3(-20f, 2.5f, 25f), new Vector3(0.8f, 5f, 18f), DarkStone);
            Box(root, "Wall", new Vector3(-26f, 2.5f, 25f), new Vector3(0.8f, 5f, 18f), DarkStone);
            Box(root, "Roof", new Vector3(-23f, 5.2f, 25f), new Vector3(7f, 0.4f, 18f), DarkStone);
        }

        /// <summary>
        /// Size reference from the design document (section 7.3): a 60 m Legend on the horizon,
        /// so the sense of scale can be judged next to the 15 m Bastion Golem.
        /// </summary>
        static void BuildScaleMarkers(Transform root)
        {
            // The Bastion Golem itself is built in Build(), after the scenery, at (0, 0, 70).

            // A 60 m Legend-tier silhouette on the horizon (an elder dragon would be this size).
            var legend = new GameObject("Legend tier marker (60 m)").transform;
            legend.SetParent(root);
            legend.position = new Vector3(-90f, 0f, 160f);
            BoxLocal(legend, "Tower", new Vector3(0f, 30f, 0f), new Vector3(12f, 60f, 12f), DarkStone);
            BoxLocal(legend, "Crown", new Vector3(0f, 61f, 0f), new Vector3(18f, 2f, 18f), Stone);
        }

        static void Ramp(Transform root, Vector3 basePos, float length, float width, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            float height = Mathf.Sin(rad) * length;
            var t = Box(root, $"Ramp {angle:0}°", Vector3.zero, new Vector3(width, 0.4f, length), Stone).transform;
            t.rotation = Quaternion.Euler(-angle, 0f, 0f);
            t.position = basePos + new Vector3(0f, height * 0.5f, Mathf.Cos(rad) * length * 0.5f);
        }

        // ---------- Player ----------

        static GameObject BuildPlayer(Vector3 position)
        {
            var player = BuildSealer("Sealer Recruit", position, false);
            var motor = player.GetComponent<PlayerMotor>();
            var hud = new GameObject("Debug HUD").AddComponent<DebugHud>();
            hud.motor = motor;
            hud.combat = player.GetComponent<PlayerCombat>();
            hud.vitals = player.GetComponent<PlayerVitals>();
            var loot = hud.gameObject.AddComponent<Sealbound.UI.LootHud>();
            loot.input = player.GetComponent<PlayerInputReader>();
            return player;
        }

        /// <summary>One Sealer: you, or a bot squad mate (same body, movement and combat; a brain presses its buttons).</summary>
        static GameObject BuildSealer(string name, Vector3 position, bool bot)
        {
            var player = new GameObject(name);
            player.layer = 2; // Ignore Raycast, so the camera does not collide with the player
            player.transform.position = position;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 40f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.04f;

            // Visual pivot sits at the feet so squash-and-stretch stays grounded.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            visual.gameObject.layer = 2;

            var reader = player.AddComponent<PlayerInputReader>();
            if (bot) reader.MakeScripted(); // never reads your keyboard, not even for one frame
            var motor = player.AddComponent<PlayerMotor>();
            motor.visual = visual;
            motor.isBot = bot;
            if (!bot)
            {
                player.AddComponent<AudioSource>();
                player.AddComponent<CombatAudio>(); // the beat ticks are yours alone
            }
            var combat = player.AddComponent<PlayerCombat>();
            var health = player.AddComponent<Health>();
            health.maxHealth = 100f;
            var vitals = player.AddComponent<PlayerVitals>();
            if (bot) player.AddComponent<SquadBot>();
            else player.AddComponent<SoulMerge>();

            // Real characters (KayKit Knight and Mage) when the free art is in the project,
            // otherwise the grey-box stand-in.
            var swordPivot = NewPivot(visual, "Sword Pivot");
            var staffPivot = NewPivot(visual, "Staff Pivot");
            var avatar = PlayerAvatar.TryBuild(player, visual, swordPivot, staffPivot);
            if (avatar != null)
            {
                combat.swordCueRenderer = avatar.CueRenderer(true);
                combat.swordCueIsWeapon = true;
                combat.staffCueRenderer = avatar.CueRenderer(false);
            }
            else
            {
                player.name = "Sealer Recruit (stand-in)";
                BuildStandInBody(visual);
                BuildSword(swordPivot);
                BuildStaff(staffPivot);
                combat.swordCueRenderer = swordPivot.Find("Rune Edge").GetComponent<Renderer>();
                combat.staffCueRenderer = staffPivot.Find("Ember Orb").GetComponent<Renderer>();
            }
            staffPivot.gameObject.SetActive(false);
            combat.swordPivot = swordPivot;
            combat.staffPivot = staffPivot;
            if (bot)
            {
                var heroAvatar = player.GetComponent<PlayerAvatar>();
                if (heroAvatar == null || !heroAvatar.IsRealistic) ToonOutline.Add(player.GetComponentsInChildren<Renderer>(true));
            }
            return player;
        }

        static Transform NewPivot(Transform visual, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = new Vector3(0.4f, 1.25f, 0.1f);
            pivot.gameObject.layer = 2;
            return pivot;
        }

        static void BuildStandInBody(Transform visual)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Paint(body, RealmBrown);

            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Sealer Badge";
            Object.Destroy(chest.GetComponent<Collider>());
            chest.transform.SetParent(visual, false);
            chest.transform.localPosition = new Vector3(0f, 1.25f, 0.33f);
            chest.transform.localScale = new Vector3(0.3f, 0.3f, 0.08f);
            Paint(chest, Iron);

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor (shows facing)";
            Object.Destroy(visor.GetComponent<Collider>());
            visor.transform.SetParent(visual, false);
            visor.transform.localPosition = new Vector3(0f, 1.55f, 0.3f);
            visor.transform.localScale = new Vector3(0.45f, 0.12f, 0.15f);
            Paint(visor, Rune);

            foreach (Transform t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        }

        static void BuildSword(Transform pivot)
        {

            Part(pivot, "Grip", new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.28f), Wood);
            Part(pivot, "Guard", new Vector3(0f, 0f, 0.16f), new Vector3(0.36f, 0.06f, 0.07f), Iron);
            Part(pivot, "Blade", new Vector3(0f, 0f, 0.85f), new Vector3(0.14f, 0.04f, 1.3f), Iron);
            Part(pivot, "Rune Edge", new Vector3(0f, 0.025f, 0.85f), new Vector3(0.05f, 0.012f, 1.15f), SwordRune);

            var tip = new GameObject("Blade Tip").transform;
            tip.SetParent(pivot, false);
            tip.localPosition = new Vector3(0f, 0f, 1.45f);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.1f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) trail.sharedMaterial = new Material(shader);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Rune, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            foreach (Transform t in pivot.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        }

        static void BuildStaff(Transform pivot)
        {

            Part(pivot, "Shaft", new Vector3(0f, 0f, 0.55f), new Vector3(0.07f, 0.07f, 1.7f), Wood);
            Part(pivot, "Head Ring", new Vector3(0f, 0f, 1.42f), new Vector3(0.28f, 0.28f, 0.06f), Iron);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Ember Orb";
            Object.Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(pivot, false);
            orb.transform.localPosition = new Vector3(0f, 0f, 1.55f);
            orb.transform.localScale = Vector3.one * 0.26f;
            Paint(orb, new Color(1f, 0.5f, 0.18f));

            foreach (Transform t in pivot.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        }

        static void Part(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Paint(go, color);
        }

        // ---------- Training yard (Milestone 2) ----------

        static void BuildTrainingYard(Transform root)
        {
            var yard = new GameObject("Training Yard").transform;
            yard.SetParent(root);

            Box(yard, "Yard Floor", new Vector3(7f, 0.02f, -8f), new Vector3(16f, 0.04f, 14f), Stone);
            Dummy(yard, new Vector3(4f, 0f, -12f), 1f, false);
            Dummy(yard, new Vector3(7f, 0f, -9f), 1f, false);
            Dummy(yard, new Vector3(4f, 0f, -5f), 1f, false);
            Dummy(yard, new Vector3(10f, 0f, -12.5f), 1f, false);
            Dummy(yard, new Vector3(10.6f, 0f, -11f), 1f, false);
            Dummy(yard, new Vector3(11.4f, 0f, -12.2f), 1f, false);
            Dummy(yard, new Vector3(12f, 0f, -5f), 1.9f, true);
        }

        static void Dummy(Transform parent, Vector3 pos, float scale, bool armoured)
        {
            var root = new GameObject(armoured ? "Armoured Dummy" : "Training Dummy");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var pivot = new GameObject("Wobble").transform;
            pivot.SetParent(root.transform, false);

            Color body = armoured ? DarkStone : new Color(0.85f, 0.72f, 0.45f);
            var renderers = new List<Renderer>();
            if (RealArt.Has) RealDummy(pivot, scale, armoured, renderers);
            else
            {
                renderers.Add(Primitive(pivot, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.6f, 0f) * scale, new Vector3(0.18f, 0.6f, 0.18f) * scale, Wood, true));
                renderers.Add(Primitive(pivot, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.35f, 0f) * scale, new Vector3(0.75f, 0.6f, 0.75f) * scale, body, true));
                renderers.Add(Primitive(pivot, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.05f, 0f) * scale, Vector3.one * 0.45f * scale, body, true));
                renderers.Add(Primitive(pivot, PrimitiveType.Cube, "Arms", new Vector3(0f, 1.55f, 0f) * scale, new Vector3(1.4f, 0.14f, 0.14f) * scale, Wood, false));
                if (armoured)
                {
                    renderers.Add(Primitive(pivot, PrimitiveType.Cube, "Stone Plate", new Vector3(0f, 1.4f, -0.36f) * scale, new Vector3(0.6f, 0.7f, 0.08f) * scale, Stone, false));
                    renderers.Add(Primitive(pivot, PrimitiveType.Cube, "Rune", new Vector3(0f, 1.45f, -0.41f) * scale, new Vector3(0.18f, 0.18f, 0.02f) * scale, Rune, false));
                }
            }

            var health = root.AddComponent<Health>();
            health.maxHealth = armoured ? 300f : 80f;
            health.damageMultiplier = armoured ? 0.4f : 1f;

            var dummy = root.AddComponent<TrainingDummy>();
            dummy.wobblePivot = pivot;
            dummy.flashRenderers = renderers.ToArray();
            dummy.label = armoured ? "Armoured (golem stone)" : "Training Dummy";
            dummy.labelHeight = 2.6f * scale;
        }

        /// <summary>
        /// Realistic look: a timber post and crossbar with a straw-stuffed sack body and head, tied with rope.
        /// The armoured one wears a slab of golem stone with a glowing rune.
        /// </summary>
        static void RealDummy(Transform pivot, float scale, bool armoured, List<Renderer> flash)
        {
            var wood = ForgedWeapons.Timber();
            var sack = ForgedWeapons.Burlap();
            var rope = ForgedWeapons.Rope();
            float seed = pivot.position.x * 1.7f + pivot.position.z * 0.9f;
            Dressed(pivot, scale, PrimitiveType.Cylinder, "Post", new Vector3(0f, 1.05f, 0f), new Vector3(0.14f, 1.05f, 0.14f), wood, true, Vector3.zero);
            Dressed(pivot, scale, PrimitiveType.Cylinder, "Crossbar", new Vector3(0f, 1.6f, 0.02f), new Vector3(0.09f, 0.72f, 0.09f), wood, false, new Vector3(0f, 0f, 90f));
            Dressed(pivot, scale, PrimitiveType.Cylinder, "Foot", new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.06f, 0.5f), wood, false, Vector3.zero);
            // A stuffed sack body, tied in at the waist and gathered at the neck, and a sack head.
            var body = Dressed(pivot, scale, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.3f, 0f), new Vector3(0.62f, 0.5f, 0.5f), sack, true, Vector3.zero);
            Stuff(body, 0.15f, new[] { -0.6f, 0.84f }, seed);
            flash.Add(body);
            var head = Dressed(pivot, scale, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.0f, 0f), new Vector3(0.36f, 0.4f, 0.34f), sack, true, new Vector3(0f, 0f, 6f));
            Stuff(head, 0.07f, null, seed + 5f);
            flash.Add(head);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Dressed(pivot, scale, PrimitiveType.Sphere, side < 0 ? "Arm Sack L" : "Arm Sack R", new Vector3(0.58f * side, 1.6f, 0.02f), new Vector3(0.24f, 0.19f, 0.19f), sack, false, Vector3.zero);
                Stuff(arm, 0.08f, null, seed + side * 3f);
                flash.Add(arm);
                Tuft(pivot, scale, new Vector3(0.7f * side, 1.6f, 0.02f), new Vector3(0f, 0f, -90f * side));
            }
            Dressed(pivot, scale, PrimitiveType.Cylinder, "Rope Neck", new Vector3(0f, 1.72f, 0f), new Vector3(0.4f, 0.045f, 0.33f), rope, false, Vector3.zero);
            Dressed(pivot, scale, PrimitiveType.Cylinder, "Rope Waist", new Vector3(0f, 1.0f, 0f), new Vector3(0.54f, 0.05f, 0.45f), rope, false, Vector3.zero);
            Tuft(pivot, scale, new Vector3(0f, 1.74f, 0f), Vector3.zero);
            Tuft(pivot, scale, new Vector3(0f, 0.98f, 0f), new Vector3(180f, 0f, 0f));
            if (armoured)
            {
                var plate = Primitive(pivot, PrimitiveType.Cube, "Stone Plate", new Vector3(0f, 1.38f, -0.27f) * scale, new Vector3(0.56f, 0.64f, 0.1f) * scale, Color.white, false);
                plate.sharedMaterial = RealArt.Surface(RealArt.GolemStone, 0.8f, 0.3f, Color.white);
                flash.Add(Primitive(pivot, PrimitiveType.Sphere, "Rune", new Vector3(0f, 1.42f, -0.33f) * scale, new Vector3(0.14f, 0.14f, 0.05f) * scale, Rune, false));
            }
        }

        static void Stuff(Renderer r, float amount, float[] ties, float seed)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = ForgedWeapons.Lumpy(mf.sharedMesh, amount, ties, seed);
        }

        static void Tuft(Transform pivot, float scale, Vector3 pos, Vector3 euler)
        {
            var go = new GameObject("Straw");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = pos * scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = ForgedWeapons.StrawTuft();
            go.AddComponent<MeshRenderer>().sharedMaterial = ForgedWeapons.Straw();
        }

        static Renderer Dressed(Transform pivot, float scale, PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m, bool collider, Vector3 euler)
        {
            var r = Primitive(pivot, type, name, pos * scale, size * scale, Color.white, collider);
            r.sharedMaterial = m;
            r.transform.localRotation = Quaternion.Euler(euler);
            return r;
        }

        static Renderer Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 size, Color color, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Paint(go, color);
            return go.GetComponent<Renderer>();
        }

        // ---------- Goblin Warren (Milestone 3) ----------

        static GoblinWarren BuildGoblinWarren(Transform root, Vector3 center, Transform player)
        {
            var w = new GameObject("Goblin Warren").transform;
            w.SetParent(root);
            w.position = center;

            Color dirt = new Color(0.42f, 0.32f, 0.2f);
            Color hole = new Color(0.12f, 0.09f, 0.06f);
            Color banner = new Color(0.75f, 0.15f, 0.12f);

            var floor = Primitive(w, PrimitiveType.Cylinder, "Warren Floor", Vector3.zero, new Vector3(30f, 0.03f, 30f), dirt, false);
            floor.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var burrows = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f;
                Vector3 local = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 10.5f;
                Primitive(w, PrimitiveType.Sphere, "Burrow Mound", local + Vector3.up * 0.1f, new Vector3(3f, 0.9f, 3f), dirt, true);
                Primitive(w, PrimitiveType.Cylinder, "Burrow Hole", local + Vector3.up * 0.52f, new Vector3(1.3f, 0.02f, 1.3f), hole, false);
                burrows.Add(center + local + Quaternion.Euler(0f, a, 0f) * Vector3.back * 2f);
            }

            // Tall red banners so the warren can be found from the training field.
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 15f;
                Primitive(w, PrimitiveType.Cylinder, "Banner Pole", local + Vector3.up * 5f, new Vector3(0.25f, 5f, 0.25f), Wood, true);
                Primitive(w, PrimitiveType.Cube, "Banner", local + new Vector3(0f, 8.3f, 0f) + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.right * 0.9f,
                    new Vector3(1.6f, 2.6f, 0.08f), banner, false).transform.localRotation = Quaternion.Euler(0f, i * 90f + 45f, 0f);
            }

            // A few boulders for cover from slingers.
            Primitive(w, PrimitiveType.Sphere, "Boulder", new Vector3(4f, 0.6f, 3f), new Vector3(2.2f, 1.6f, 2f), DarkStone, true);
            Primitive(w, PrimitiveType.Sphere, "Boulder", new Vector3(-5f, 0.6f, -2f), new Vector3(2.6f, 1.8f, 2.2f), DarkStone, true);
            Primitive(w, PrimitiveType.Sphere, "Boulder", new Vector3(1f, 0.5f, -6f), new Vector3(1.8f, 1.3f, 1.8f), DarkStone, true);

            var warren = w.gameObject.AddComponent<GoblinWarren>();
            warren.player = player;
            warren.burrows = burrows.ToArray();
            return warren;
        }

        static void SetupCamera(GameObject player)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;

            var rig = cam.GetComponent<ThirdPersonCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            rig.target = player.transform;
            rig.input = player.GetComponent<PlayerInputReader>();
            rig.motor = player.GetComponent<PlayerMotor>();
            rig.motor.cameraTransform = cam.transform;
        }

        // ---------- Helpers ----------

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            Paint(go, color);
            return go;
        }

        static GameObject BoxLocal(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Paint(go, color);
            return go;
        }

        static void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (_baseMaterial == null) _baseMaterial = r.sharedMaterial;
            if (!Materials.TryGetValue(color, out var mat))
            {
                mat = new Material(_baseMaterial) { color = color };
                if (color == Rune && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 2f);
                }
                Materials[color] = mat;
            }
            r.sharedMaterial = mat;
        }
    }
}
