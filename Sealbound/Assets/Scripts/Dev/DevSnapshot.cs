using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Sealbound.Enemies;
using Sealbound.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sealbound.Dev
{
    /// <summary>
    /// Developer check, not part of the game: plays a short scripted tour of the test field and saves
    /// pictures and a report, so changes can be checked on a machine without anyone at the keyboard.
    /// Started by the editor's "PlayCheck" batch command.
    /// </summary>
    public class DevSnapshot : MonoBehaviour
    {
        public static bool Done;
        public static string OutputFolder;

        readonly StringBuilder _report = new StringBuilder();
        readonly List<string> _problems = new List<string>();
        int _errors, _warnings;

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) _warnings++;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                string line = type + ": " + message.Split('\n')[0];
                if (!_problems.Contains(line) && _problems.Count < 30) _problems.Add(line + " | " + (stack ?? "").Split('\n')[0]);
            }
        }

        // A test run must never touch the player's real pack on disk.
        void Awake() => Sealbound.Loot.Inventory.InMemoryOnly = true;

        IEnumerator Start()
        {
            Done = false;
            if (string.IsNullOrEmpty(OutputFolder)) OutputFolder = Path.Combine(Application.dataPath, "..", "PlayCheck");
            Directory.CreateDirectory(OutputFolder);
            yield return new WaitForSeconds(1.5f);

            var motor = PlayerMotor.Main;
            var combat = motor != null ? motor.GetComponent<PlayerCombat>() : null;
            Line("Player found: " + (motor != null) + ", ambient occlusion on: " + Sealbound.Core.Performance.AmbientOcclusion);
            if (motor == null) { Finish(); yield break; }
            var p = motor.transform;
            // The older checks run alone, so their numbers stay comparable; the squad gets its own tour at the end.
            Line("Bots at start: " + SquadBot.All.Count + " (sending them away for the solo checks)");
            SquadBot.SetSquadSize(0);

            Describe(p.gameObject);
            Line("Animated parts on player: " + p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);

            int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            Line($"Frame rate at start: {frames / 2f:0} fps");

            Shot("01_hero_front", p.position + p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
            Shot("01b_hero_back", p.position - p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
            Shot("01c_sword_closeup", p.position + p.forward * 1.6f + p.right * 0.9f + Vector3.up * 1.2f, p.position + p.right * 0.35f + Vector3.up * 0.8f, 40f);
            DescribeMaterials(p.gameObject, "Weapon Model");
            ShotMain("02_gameplay_view");

            // Mid-swing of the first sword attack.
            if (combat != null)
            {
                var slash = combat.Kit.basicCombo[0];
                Attack(combat, slash);
                yield return new WaitForSeconds(slash.windup * 0.95f);
                Shot("03a_sword_windup", p.position + p.forward * 3f + p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(slash.windup * 0.05f + slash.active * 0.5f);
                Shot("03b_sword_midswing", p.position + p.forward * 3f + p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(1f);

                CallPrivate(combat, "EquipKit", combat.mageKit, true);
                yield return new WaitForSeconds(0.6f);
                Shot("04_mage_front", p.position + p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
                Shot("04b_mage_back", p.position - p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
                Attack(combat, combat.Kit.basicCombo[0]);
                yield return new WaitForSeconds(combat.Kit.basicCombo[0].windup * 0.9f);
                Shot("05_mage_cast", p.position + p.forward * 3f - p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(1f);
                CallPrivate(combat, "EquipKit", combat.swordKit, false);
            }

            Shot("06_training_yard", new Vector3(-6f, 6f, -22f), new Vector3(7f, 0.5f, -8f), 55f);
            Shot("06b_dummy_close", new Vector3(6.2f, 1.8f, -9.6f), new Vector3(4f, 1.45f, -12f), 45f);
            Shot("07_field_wide", new Vector3(0f, 22f, -45f), new Vector3(0f, 2f, 55f), 60f);
            Shot("08_ruins", new Vector3(8f, 7f, 42f), new Vector3(0f, 5f, 70f), 55f);
            Shot("08b_valley_eye_level", new Vector3(4f, 1.7f, -14f), new Vector3(-2f, 7f, 80f), 60f);
            Shot("08c_hills_west", new Vector3(-62f, 15f, 100f), new Vector3(-200f, 30f, 95f), 60f);
            DescribeNature();

            // The warren: step in so the goblins come out.
            motor.Teleport(new Vector3(-45f, 0.2f, -15f));
            yield return new WaitForSeconds(3.5f);
            Shot("09_warren", new Vector3(-45f, 17f, -37f), new Vector3(-45f, 0.5f, -15f), 55f);
            var gob = FindAnyObjectByType<Goblin>();
            Line("Goblins out: " + FindObjectsByType<Goblin>(FindObjectsSortMode.None).Length);
            if (gob != null)
            {
                Describe(gob.gameObject);
                var g = gob.transform;
                Shot("10_goblin_close", g.position + g.right * 2.2f + g.forward * 0.8f + Vector3.up * 1.0f, g.position + Vector3.up * 0.6f, 40f);
                float wait = 0f;
                Goblin winding = null;
                while (wait < 8f && winding == null)
                {
                    foreach (var x in FindObjectsByType<Goblin>(FindObjectsSortMode.None))
                        if (x.CurrentState == Goblin.State.Windup && x.StateTime > 0.2f) { winding = x; break; }
                    wait += Time.deltaTime;
                    yield return null;
                }
                if (winding != null)
                {
                    var w = winding.transform;
                    Shot("11_goblin_windup", w.position + w.right * 2.4f + w.forward * 0.6f + Vector3.up * 1.1f, w.position + Vector3.up * 0.6f, 42f);
                    DescribeMaterials(winding.gameObject, "Outline");
                }
                else Line("No goblin wind-up seen within 8 s.");
            }

            frames = 0; t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            Line($"Frame rate in the warren fight: {frames / 2f:0} fps");

            yield return GolemTour(motor);
            yield return ForgeTour(motor);
            yield return SoulMergeTour(motor);
            yield return SquadTour(motor);
            DescribeAmbientOcclusion();
            Finish();
        }

        /// <summary>Wakes the Bastion Golem, watches an attack, breaks a rune, pushes it into phase two and seals it.</summary>
        IEnumerator GolemTour(PlayerMotor motor)
        {
            var golem = FindAnyObjectByType<BastionGolem>();
            Line("Bastion Golem found: " + (golem != null));
            if (golem == null) yield break;
            Line($"Golem: {golem.GetComponentsInChildren<Renderer>().Length} renderers, {golem.GetComponentsInChildren<Collider>().Length} colliders, " +
                 $"{golem.LimbRunes.Count} limb runes, health {golem.Health.Current}/{golem.Health.maxHealth}, armour x{golem.Health.damageMultiplier}");

            // Keep the hero standing for the pictures.
            var health = motor.GetComponent<Combat.Health>();
            if (health != null) health.InvulnerableCheck = () => true;

            motor.Teleport(new Vector3(0f, 0.2f, 52f));
            yield return new WaitForSeconds(1.2f);
            Line("Golem state after stepping in: " + golem.CurrentState);
            GolemShot("12_golem_waking", golem, 30f, 19f, 7f, 7f, 60f);

            float wait = 0f;
            while (wait < 12f && !(IsAttack(golem.CurrentState) && golem.StateTime > 0.45f))
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Line($"Golem attack seen: {golem.CurrentState} after {wait:0.0}s");
            GolemShot("13_golem_attack", golem, 55f, 20f, 9f, 5f, 62f);

            wait = 0f;
            while (wait < 8f && !golem.CanBeHurt) { wait += Time.deltaTime; yield return null; }
            golem.DebugBreakRune(0);
            yield return new WaitForSeconds(1.4f);
            var g = golem.transform;
            Line($"After breaking a rune: state {golem.CurrentState}, core exposed {golem.CoreExposed}, health {golem.HealthFraction:P0}");
            GolemShot("14_golem_kneel", golem, 15f, 15f, 5f, 4.5f, 58f);

            // Push it past half health for the second phase.
            float toHalf = golem.Health.Current - golem.Health.maxHealth * 0.45f;
            golem.Health.TryDamage(new Combat.DamageInfo { Amount = toHalf / Mathf.Max(0.01f, golem.Health.damageMultiplier), Point = g.position + Vector3.up * 6f }, out _);
            yield return new WaitForSeconds(1.2f);
            Line($"Phase {golem.Phase}, state {golem.CurrentState}, health {golem.HealthFraction:P0}");
            GolemShot("15_golem_phase2", golem, 25f, 19f, 7f, 6.5f, 60f);

            int fpsFrames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f || !golem.CanBeHurt)
            {
                fpsFrames++;
                if (Time.realtimeSinceStartup - t0 > 10f) break;
                yield return null;
            }
            Line($"Frame rate in the golem fight: {fpsFrames / Mathf.Max(0.1f, Time.realtimeSinceStartup - t0):0} fps, state {golem.CurrentState}");

            Vector3 centre = g.position;
            int brokenBefore = golem.RunesBrokenTotal;
            golem.Health.TryDamage(new Combat.DamageInfo { Amount = 1e6f, Point = centre + Vector3.up * 6f }, out _);
            yield return new WaitForSeconds(2f);
            Line($"After the killing blow: state {golem.CurrentState}, subdued {golem.Subdued}, seal ritual {(GolemSeal.Current != null)}, runes broken this fight {brokenBefore}");
            Vector3 side = Vector3.ProjectOnPlane(motor.transform.position - centre, Vector3.up).normalized;
            Shot("16_golem_subdued", centre + side * 20f + Vector3.up * 9f, centre + Vector3.up * 2f, 62f);

            // Walk up and seal it.
            motor.Teleport(centre + side * 8f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            GolemSeal.DevHold = true;
            yield return new WaitForSeconds(GolemSeal.HoldSeconds * 0.6f);
            var seal = GolemSeal.Current;
            Line($"Sealing: in reach {(seal != null && seal.InReach)}, holding {(seal != null && seal.Holding)}, progress {(seal != null ? seal.Progress : -1f):P0}");
            Vector3 across = Quaternion.Euler(0f, 90f, 0f) * side;
            Shot("17_sealing", centre + side * 12.5f + across * 3f + Vector3.up * 4.5f, centre + side * 3f + Vector3.up * 4f, 64f);
            float w2 = 0f;
            while (golem != null && w2 < 5f) { w2 += Time.deltaTime; yield return null; }
            GolemSeal.DevHold = false;
            Line("Golem sealed and gone: " + (golem == null) + ", respawner waiting " + (GameObject.Find("Golem Respawner") != null));
            yield return new WaitForSeconds(0.35f);
            Shot("18a_loot_burst", centre + side * 16f + Vector3.up * 7f, centre + Vector3.up * 3f, 62f);
            yield return new WaitForSeconds(1.6f);
            var drops = new List<Sealbound.Loot.LootDrop>(Sealbound.Loot.LootDrop.Active);
            var counts = new Dictionary<string, int>();
            foreach (var d in drops) { counts.TryGetValue(d.Item.displayName, out int n); counts[d.Item.displayName] = n + d.Count; }
            var sb = new StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append(" x").Append(kv.Value).Append(", ");
            Line($"Loot on the ground: {drops.Count} pickups: {sb}");
            Shot("18b_loot_on_ground", centre + side * 12f + Vector3.up * 2.6f, centre + Vector3.up * 0.6f, 60f);
            // A close look at the best drop still lying there.
            Sealbound.Loot.LootDrop best = null;
            foreach (var d in drops) if (d != null && (best == null || d.Item.rarity > best.Item.rarity)) best = d;
            if (best != null)
            {
                Vector3 bp = best.transform.position;
                Vector3 back = Vector3.ProjectOnPlane(bp - centre, Vector3.up).normalized;
                if (back.sqrMagnitude < 0.01f) back = side;
                Line("Close-up of: " + best.Item.displayName);
                Shot("18c_loot_close", bp + back * 3.5f + Vector3.up * 1.6f, bp + Vector3.up * 0.4f, 55f);
            }

            // Walk over every drop so it flies into the pack.
            foreach (var d in drops)
            {
                if (d == null) continue;
                motor.Teleport(d.transform.position + Vector3.up * 0.1f);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(1.5f);
            var pack = new StringBuilder();
            foreach (var kv in Sealbound.Loot.Inventory.Owned()) pack.Append(kv.Key.displayName).Append(" x").Append(kv.Value).Append(", ");
            Line($"Pack after collecting: {pack} (left on the ground: {Sealbound.Loot.LootDrop.Active.Count}); hunts {Sealbound.Loot.Inventory.Hunts}, " +
                 $"next spirit chance {Sealbound.Loot.LootTables.GolemSpiritChance(Sealbound.Loot.Inventory.FailedSpiritRolls(Sealbound.Loot.LootTables.GolemName)):P0}");
        }

        /// <summary>Gives the hero enough parts, forges both golem weapons at the forge and photographs them in hand.</summary>
        IEnumerator ForgeTour(PlayerMotor motor)
        {
            var forge = FindAnyObjectByType<Sealbound.Core.ForgeStation>();
            Line("Forge found: " + (forge != null));
            if (forge == null) yield break;
            var combat = motor.GetComponent<PlayerCombat>();
            Sealbound.Loot.Inventory.Add(Sealbound.Loot.ItemCatalog.Essence, 200);
            Sealbound.Loot.Inventory.Add(Sealbound.Loot.ItemCatalog.RunestoneShard, 12);
            Sealbound.Loot.Inventory.Add(Sealbound.Loot.ItemCatalog.GolemCore, 2);
            Vector3 f = forge.transform.position;
            Vector3 front = forge.transform.forward, right = forge.transform.right;
            motor.Teleport(f + front * 2.6f + right * 0.6f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.8f);
            Line($"At the forge: in reach {forge.InReach}");
            Shot("20_forge", f + front * 5.5f + right * 3f + Vector3.up * 2.4f, f + Vector3.up * 1.1f, 55f);

            var recipes = Sealbound.Loot.Forge.Recipes;
            foreach (var r in recipes)
                Line($"Forge {r.Weapon.displayName}: can {Sealbound.Loot.Forge.CanCraft(r)}, crafted {Sealbound.Loot.Forge.Craft(r)}, equipped {Sealbound.Loot.Inventory.EquippedWeapon(r.forSword) == r.weapon}");
            yield return new WaitForSeconds(0.6f);

            var p = motor.transform;
            if (combat != null) CallPrivate(combat, "EquipKit", combat.swordKit, false);
            yield return new WaitForSeconds(0.6f);
            DescribeMaterials(p.gameObject, "Weapon Model");
            Shot("21_runestone_blade", p.position + p.forward * 1.9f + p.right * 0.9f + Vector3.up * 1.4f, p.position + p.right * 0.25f + Vector3.up * 1.05f, 45f);
            if (combat != null)
            {
                var slash = combat.Kit.basicCombo[0];
                Attack(combat, slash);
                yield return new WaitForSeconds(slash.windup + slash.active * 0.5f);
                Shot("21b_runestone_swing", p.position + p.forward * 3f + p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(1f);
                CallPrivate(combat, "EquipKit", combat.mageKit, true);
                yield return new WaitForSeconds(0.8f);
                Shot("22_core_staff", p.position + p.forward * 2.4f + p.right * 0.6f + Vector3.up * 1.6f, p.position + p.right * 0.3f + Vector3.up * 1.3f, 45f);
                CallPrivate(combat, "EquipKit", combat.swordKit, false);
            }
        }

        IEnumerator SoulMergeTour(PlayerMotor motor)
        {
            var shrine = FindAnyObjectByType<Sealbound.Core.SoulShrine>();
            var merge = motor.GetComponent<SoulMerge>();
            var health = motor.GetComponent<Sealbound.Combat.Health>();
            Line("Soul shrine found: " + (shrine != null) + ", soul merge on player: " + (merge != null));
            if (shrine == null || merge == null) yield break;
            var p = motor.transform;

            // Bind the spirit the long way: ten shards made whole at the shrine.
            Sealbound.Loot.Inventory.Add(Sealbound.Loot.ItemCatalog.GolemSpiritShard, Sealbound.Loot.ItemCatalog.ShardsPerSpirit);
            Vector3 s = shrine.transform.position;
            Vector3 front = shrine.transform.forward, right = shrine.transform.right;
            motor.Teleport(s + front * 2.2f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.8f);
            Line($"At the shrine: in reach {shrine.InReach}; prompt: {Sealbound.Core.SoulShrine.PromptText()}");
            Shot("23_soul_shrine", s + front * 7f + right * 3f + Vector3.up * 3f, s + Vector3.up * 1.3f, 55f);
            string said = shrine.TryBind();
            Line($"Bind: \"{said}\"; bound {Sealbound.Loot.Inventory.BoundSpirit}; shards left {Sealbound.Loot.Inventory.Count(Sealbound.Loot.ItemCatalog.GolemSpiritShard)}; spirits {Sealbound.Loot.Inventory.Count(Sealbound.Loot.ItemCatalog.GolemSpirit)}");
            yield return new WaitForSeconds(0.6f);
            Shot("23b_shrine_bound", s + front * 5f + right * 2f + Vector3.up * 2.4f, s + Vector3.up * 1.6f, 55f);

            // Merge, out in the training yard among the dummies.
            motor.Teleport(new Vector3(7f, 0.2f, -2.5f));
            yield return new WaitForSeconds(0.6f);
            float baseMax = health.maxHealth, baseSpeed = motor.runSpeed;
            Line($"Merge with an empty gauge: {merge.TryMerge()} (should be False)");
            merge.FillGauge();
            Line($"Merge with a full gauge: {merge.TryMerge()}; max health {baseMax:0} -> {health.maxHealth:0}, health {health.Current:0}, damage taken x{health.damageMultiplier:0.00}, speed x{motor.SpeedScale:0.00}");
            yield return new WaitForSeconds(0.9f);
            Shot("24_golem_merge_front", p.position + p.forward * 3.4f + Vector3.up * 1.6f, p.position + Vector3.up * 1.05f, 42f);
            Shot("24b_golem_merge_back", p.position - p.forward * 3.4f + p.right * 1.2f + Vector3.up * 1.7f, p.position + Vector3.up * 1.05f, 42f);

            // Fortress Stance: a heavy hit hurts but does not knock you back.
            var hit = new Sealbound.Combat.DamageInfo { Amount = 10f, Point = p.position + Vector3.up, Direction = -p.forward, Source = gameObject, Impact = 1f };
            var oldCheck = health.InvulnerableCheck;
            health.InvulnerableCheck = null; // the golem tour made the hero untouchable for its photos
            float before = health.Current;
            health.TryDamage(hit, out float dealt);
            health.InvulnerableCheck = oldCheck;
            yield return null;
            Line($"Fortress Stance: took {dealt:0.0} of 10 ({before:0} -> {health.Current:0}), knocked down {motor.IsStunned} (should be False)");

            // Rune Quake next to the dummies.
            motor.Teleport(new Vector3(7f, 0.2f, -6.5f));
            yield return new WaitForSeconds(0.3f);
            float dummyBefore = 0f;
            var dummies = FindObjectsByType<Sealbound.Combat.TrainingDummy>(FindObjectsSortMode.None);
            foreach (var d in dummies) { var h = d.GetComponent<Sealbound.Combat.Health>(); if (h != null) dummyBefore += h.Current; }
            Line($"Rune Quake: {merge.TryRuneQuake()}, again at once: {merge.TryRuneQuake()} (should be False, cooldown {merge.QuakeCooldownLeft:0.0}s)");
            yield return new WaitForSeconds(0.2f);
            Shot("25_rune_quake", p.position + p.forward * 6f + p.right * 4f + Vector3.up * 4f, p.position + Vector3.up * 0.5f, 55f);
            yield return new WaitForSeconds(0.5f);
            float dummyAfter = 0f;
            foreach (var d in dummies) { var h = d.GetComponent<Sealbound.Combat.Health>(); if (h != null) dummyAfter += h.Current; }
            Line($"Rune Quake damage on the dummies: {dummyBefore - dummyAfter:0}");

            // Time runs out.
            merge.EndMerge(true);
            yield return new WaitForSeconds(0.5f);
            Line($"After the merge: merged {merge.Merged}, max health {health.maxHealth:0} (was {baseMax:0}), damage taken x{health.damageMultiplier:0.00}, speed x{motor.SpeedScale:0.00}, run speed {motor.runSpeed:0.0} (was {baseSpeed:0.0}), gauge {merge.Gauge:0}");
            Shot("26_merge_ended", p.position + p.forward * 3.4f + Vector3.up * 1.6f, p.position + Vector3.up * 1.05f, 42f);
        }


        string SquadState()
        {
            var sb = new StringBuilder();
            foreach (var b in SquadBot.All)
            {
                if (b == null) continue;
                float hp = b.Health != null ? b.Health.Current : -1f;
                sb.Append($"[{b.Label}: {b.Doing}, health {hp:0}, {Vector3.Distance(b.transform.position, PlayerMotor.Main.transform.position):0.0} m from you] ");
            }
            return sb.ToString();
        }

        string SquadStats(Dictionary<SquadBot, float> damageBefore)
        {
            var sb = new StringBuilder();
            foreach (var b in SquadBot.All)
            {
                if (b == null) continue;
                float before = 0f;
                if (damageBefore != null) damageBefore.TryGetValue(b, out before);
                sb.Append($"[{b.Label}: damage {b.DamageDealt - before:0}, dodges {b.Dodges}, sidesteps {b.Sidesteps}, wave jumps {b.Jumps}, hits taken {b.HitsTaken}, times down {b.TimesDowned}] ");
            }
            return sb.ToString();
        }

        static Dictionary<SquadBot, float> DamageNow()
        {
            var d = new Dictionary<SquadBot, float>();
            foreach (var b in SquadBot.All) if (b != null) d[b] = b.DamageDealt;
            return d;
        }

        /// <summary>Calls the three bots, checks they follow, fight goblins and the golem, dodge its attacks and can't be hurt by you.</summary>
        IEnumerator SquadTour(PlayerMotor motor)
        {
            var p = motor.transform;
            var combat = motor.GetComponent<PlayerCombat>();
            var health = motor.GetComponent<Sealbound.Combat.Health>();
            if (health != null) health.InvulnerableCheck = () => true;

            // Frame rate on the same spot with no bots, then with three.
            motor.Teleport(new Vector3(0f, 0.2f, -6f));
            yield return new WaitForSeconds(1f);
            int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            float fpsSolo = frames / 2f;

            SquadBot.SetSquadSize(SquadBot.MaxSquad);
            yield return new WaitForSeconds(2f);
            Line($"Squad called: {SquadBot.All.Count} bots; you are still the main player: {PlayerMotor.Main == motor}; {SquadState()}");
            frames = 0; t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            Line($"Frame rate at the start: {fpsSolo:0} fps alone, {frames / 2f:0} fps with 3 bots");
            if (SquadBot.All.Count > 0 && SquadBot.All[0] != null) Describe(SquadBot.All[0].gameObject);
            Shot("27_squad_formation", p.position - p.forward * 6f + p.right * 2.5f + Vector3.up * 2.8f, p.position + Vector3.up * 0.9f, 50f);
            Shot("27b_squad_front", p.position + p.forward * 6f + p.right * 1.5f + Vector3.up * 2f, p.position - p.forward * 1.5f + Vector3.up * 0.9f, 50f);

            // Follow: you move 12 m, they should catch up.
            motor.Teleport(p.position + p.forward * 12f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.6f);
            Line($"0.6 s after you moved 12 m: {SquadState()}");
            yield return new WaitForSeconds(3.4f);
            Line($"4 s after you moved 12 m: {SquadState()}");

            // Friendly fire: a full swing at a bot standing in front of you.
            var victim = SquadBot.All.Count > 0 ? SquadBot.All[0] : null;
            if (victim != null && combat != null)
            {
                victim.hold = true;
                victim.GetComponent<PlayerMotor>().Teleport(p.position + p.forward * 1.1f);
                yield return new WaitForSeconds(0.2f);
                float hpBefore = victim.Health.Current;
                var slash = combat.Kit.basicCombo[0];
                Attack(combat, slash);
                yield return new WaitForSeconds(slash.windup + slash.active + 0.3f);
                Line($"Friendly fire: you swung at {victim.Label} from {Vector3.Distance(p.position, victim.transform.position):0.0} m, its health {hpBefore:0} -> {victim.Health.Current:0} (should be unchanged)");
                victim.hold = false;
            }

            // Goblins: go back to the warren and let the bots fight (you stand still).
            int beaten = 0;
            var seen = new HashSet<Goblin>();
            var before = DamageNow();
            motor.Teleport(new Vector3(-45f, 0.2f, -15f));
            yield return new WaitForSeconds(0.5f);
            Line($"At the warren: {SquadState()}");
            float watch = 0f;
            bool shot = false;
            while (watch < 16f)
            {
                foreach (var g in GoblinDirector.Goblins)
                    if (g != null && seen.Add(g)) g.Sealed += _ => beaten++;
                if (!shot && watch > 6f)
                {
                    Shot("28_squad_vs_goblins", p.position + new Vector3(0f, 9f, -14f), p.position + Vector3.up * 0.5f, 60f);
                    Line($"Mid-fight at the warren: {SquadState()}");
                    shot = true;
                }
                watch += Time.deltaTime;
                yield return null;
            }
            Line($"Warren with the squad (you did nothing for 16 s): {seen.Count} goblins seen, {beaten} beaten; {SquadStats(before)}");

            // The golem: wake it and watch the bots fight and dodge.
            var golem = FindAnyObjectByType<BastionGolem>();
            if (golem == null)
            {
                var waiting = GameObject.Find("Golem Respawner");
                if (waiting != null) Destroy(waiting);
                golem = BastionGolem.Create(new Vector3(0f, 0f, 70f), 180f);
            }
            yield return new WaitForSeconds(0.5f);
            before = DamageNow();
            float golemHp = golem.Health.Current;
            motor.Teleport(new Vector3(0f, 0.2f, 52f));
            yield return new WaitForSeconds(1f);
            Line($"Golem: state {golem.CurrentState}; {SquadState()}");
            frames = 0; t0 = Time.realtimeSinceStartup;
            watch = 0f;
            bool shotFight = false, shotWarning = false;
            int warningFrames = 0;
            float nextLog = 6f;
            while (watch < 26f && golem != null)
            {
                frames++;
                if (Sealbound.Combat.GroundWarning.ActiveCount > 0) warningFrames++;
                if (!shotFight && watch > 8f) { GolemShot("29_squad_vs_golem", golem, 40f, 19f, 8f, 3f, 62f); shotFight = true; }
                if (!shotWarning && watch > 3f && Sealbound.Combat.GroundWarning.ActiveCount > 0 && warningFrames > 10)
                {
                    GolemShot("30_squad_dodging", golem, -35f, 19f, 10f, 1f, 64f);
                    Line($"During a red warning: {SquadState()}");
                    shotWarning = true;
                }
                if (watch > nextLog) { Line($"Golem fight {watch:0}s: golem {golem.CurrentState}, health {golem.HealthFraction:P0}; {SquadState()}"); nextLog += 6f; }
                watch += Time.deltaTime;
                yield return null;
            }
            float secs = Mathf.Max(0.1f, Time.realtimeSinceStartup - t0);
            Line($"Frame rate in the golem fight with 3 bots: {frames / secs:0} fps");
            if (golem != null)
                Line($"Golem fight with the squad (you did nothing for 26 s): golem health {golemHp:0} -> {golem.Health.Current:0}, runes broken {golem.RunesBrokenTotal}; {SquadStats(before)}");

            // B key cycle: 3 -> 0 -> 1 -> 2 -> 3.
            motor.Teleport(new Vector3(0f, 0.2f, -6f));
            yield return new WaitForSeconds(0.5f);
            var counts = new StringBuilder();
            foreach (int n in new[] { 0, 1, 2, 3 })
            {
                SquadBot.SetSquadSize(n);
                yield return null;
                int live = FindObjectsByType<SquadBot>(FindObjectsSortMode.None).Length;
                var names = new StringBuilder();
                foreach (var b in SquadBot.All) if (b != null) names.Append(b.Label).Append(", ");
                counts.Append($"{n}: {SquadBot.All.Count} listed, {live} in the world ({names}) | ");
            }
            Line("Squad sizes: " + counts);
        }

        /// <summary>Reports whether the ambient occlusion pass is really on the renderer the game uses.</summary>
        void DescribeAmbientOcclusion()
        {
            try
            {
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var field = asset != null ? asset.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic) : null;
                var list = field != null ? field.GetValue(asset) as ScriptableRendererData[] : null;
                var sb = new StringBuilder();
                sb.Append("Pipeline asset in use: ").Append(asset != null ? asset.name : "none").Append("; renderer: ");
                if (list != null && list.Length > 0 && list[0] != null)
                {
                    sb.Append(list[0].name).Append(" features: ");
                    foreach (var feat in list[0].rendererFeatures)
                    {
                        if (feat == null) { sb.Append("(null) "); continue; }
                        sb.Append(feat.GetType().Name).Append(feat.isActive ? "[on] " : "[off] ");
                        var settings = feat.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(feat);
                        if (settings != null)
                            foreach (var fi in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                                sb.Append(fi.Name).Append('=').Append(fi.GetValue(settings)).Append(' ');
                    }
                }
                Line(sb.ToString());
            }
            catch (Exception e) { Line("Could not describe ambient occlusion: " + e.Message); }
        }

        /// <summary>
        /// A picture of the golem from <paramref name="angle"/> degrees off its facing, <paramref name="distance"/> metres away,
        /// kept inside the ring of ruins so walls don't block the view.
        /// </summary>
        void GolemShot(string name, BastionGolem golem, float angle, float distance, float height, float lookHeight, float fov)
        {
            var g = golem.transform;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.ProjectOnPlane(g.forward, Vector3.up).normalized;
            Vector3 from = g.position + dir * distance;
            Vector3 off = from - golem.arenaCentre; off.y = 0f;
            if (off.magnitude > 20f) from = golem.arenaCentre + off.normalized * 20f;
            from.y = height;
            Shot(name, from, g.position + Vector3.up * lookHeight, fov);
        }

        /// <summary>Lists the colour and texture of each nature model's materials (to track down odd colours).</summary>
        void DescribeNature()
        {
            var seen = new HashSet<string>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string model = r.transform.root != null ? r.gameObject.name : "";
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.name.EndsWith("(nature)")) continue;
                    string key = r.name + "|" + m.name;
                    if (!seen.Add(key) || seen.Count > 80) continue;
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    Line($"Nature material on {r.name}: {m.name}, tint {c}, texture {(m.mainTexture != null ? m.mainTexture.name : "none")}");
                }
            }
        }

        static bool IsAttack(BastionGolem.State s) =>
            s == BastionGolem.State.Slam || s == BastionGolem.State.DoubleSlam || s == BastionGolem.State.Stomp ||
            s == BastionGolem.State.Charge || s == BastionGolem.State.Throw;

        void Finish()
        {
            Line($"Errors: {_errors}, warnings: {_warnings}");
            foreach (var pr in _problems) Line("  " + pr);
            File.WriteAllText(Path.Combine(OutputFolder, "report.txt"), _report.ToString());
            Done = true;
        }

        void Line(string s) { _report.AppendLine(s); Debug.Log("[PlayCheck] " + s); }

        void Describe(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (!(r is TrailRenderer)) b.Encapsulate(r.bounds);
            Line($"{go.name}: visible size {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m, bottom at {b.min.y - go.transform.position.y:0.00} m");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Outline")) continue;
                var m = r.sharedMaterial;
                Line($"  part {r.name} ({r.GetType().Name}, active {r.gameObject.activeInHierarchy}) material {(m != null ? m.name : "none")} texture {(m != null && m.mainTexture != null ? m.mainTexture.name : "none")}");
            }
        }

        /// <summary>Logs the colour settings of renderers with this name (to check outline and cue colours).</summary>
        void DescribeMaterials(GameObject root, string rendererName)
        {
            int shown = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != rendererName || shown >= 4) continue;
                shown++;
                var m = r.sharedMaterial;
                if (m == null) continue;
                string col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F2") : "-";
                string em = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor").ToString("F2") : "-";
                Line($"  {rendererName} on {r.transform.parent?.name}: shader {m.shader.name}, enabled {r.enabled}, colour {col}, emission {em}");
            }
        }

        static void Attack(PlayerCombat combat, AttackData a)
        {
            var begin = typeof(PlayerCombat).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultType = typeof(PlayerCombat).GetNestedType("ChainResult", BindingFlags.NonPublic);
            if (begin == null || resultType == null) return;
            begin.Invoke(combat, new object[] { a, -1, Enum.ToObject(resultType, 0) });
        }

        static void CallPrivate(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(target, args);
        }

        void ShotMain(string name)
        {
            var main = Camera.main;
            if (main == null) return;
            Shot(name, main.transform.position, main.transform.position + main.transform.forward, main.fieldOfView);
        }

        void Shot(string name, Vector3 from, Vector3 lookAt, float fov)
        {
            const int w = 1280, h = 720;
            var go = new GameObject("Snapshot Camera");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) cam.CopyFrom(main);
            cam.enabled = false;
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - from);
            cam.fieldOfView = fov;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), tex.EncodeToPNG());
            Line("Saved " + name + ".png");

            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }
    }
}
