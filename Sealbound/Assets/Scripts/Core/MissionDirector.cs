using System.Collections;
using System.Collections.Generic;
using System.Text;
using Sealbound.CameraRig;
using Sealbound.Enemies;
using Sealbound.Loot;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// Runs the first mission, The Runestone Ruins: take it from the board in town, travel to the camp,
    /// seal the Bastion Golem (and, for a bonus, clear the goblin warren), then step into the return circle
    /// or touch the waystone to go home with the loot.
    /// </summary>
    public class MissionDirector : MonoBehaviour
    {
        public enum State { Idle, Travelling, Active, Complete }

        public const string MissionName = "The Runestone Ruins";
        public const string MissionBrief =
            "A Bastion Golem has woken in the old ruins north of the camp. Its stone limbs are carved with runes: break them to bring it to its knees, then seal it and bring home what it leaves behind.";
        public const int Reward = 40, BonusReward = 20;

        public static MissionDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        /// <summary>Where the mission starts: the Sealers' camp at the south end of the valley.</summary>
        public static readonly Vector3 Camp = new Vector3(0f, 0.15f, -6f);
        public static readonly Vector3 GolemHome = new Vector3(0f, 0f, 70f);
        public static readonly Vector3 ReturnCirclePoint = new Vector3(0f, 0f, 49f);

        public State Current { get; private set; } = State.Idle;
        public bool GolemSealed { get; private set; }
        public bool WarrenCleared { get; private set; }
        public int GoblinsBeaten { get; private set; }
        public float Elapsed => Current == State.Active ? Time.time - _startedAt : _finishedAt - _startedAt;
        public bool InMission => Current == State.Active || Current == State.Complete;
        /// <summary>0 = clear, 1 = black (the HUD draws it).</summary>
        public float Fade { get; private set; }
        public string Results { get; private set; }
        public float ResultsShownAt { get; private set; } = -99f;
        public ReturnCircle Circle { get; private set; }
        public bool PlayerInTown => _motor != null && TownSquare.Contains(_motor.transform.position);

        PlayerMotor _motor;
        PlayerVitals _vitals;
        PlayerCombat _combat;
        GoblinWarren _warren;
        float _startedAt, _finishedAt;
        int _huntsAtStart;
        readonly Dictionary<string, int> _packAtStart = new Dictionary<string, int>();
        readonly HashSet<Goblin> _seen = new HashSet<Goblin>();

        public static MissionDirector Create(PlayerMotor player, GoblinWarren warren)
        {
            var d = new GameObject("Mission Director").AddComponent<MissionDirector>();
            d._motor = player;
            d._vitals = player.GetComponent<PlayerVitals>();
            d._combat = player.GetComponent<PlayerCombat>();
            d._warren = warren;
            Instance = d;
            // Each mission sets out with a fresh golem, so a sealed one no longer comes back by itself.
            GolemRespawner.Enabled = false;
            // Fall in the field and the squad drags you back to camp; fall in town and you wake by the fountain.
            if (d._vitals != null) d._vitals.RespawnPoint = () => d.InMission ? Camp : (d.PlayerInTown ? TownSquare.SpawnPoint : Camp);
            return d;
        }

        // ---------- Setting out ----------

        /// <summary>Takes the mission from the board and travels to the camp. False if one is already under way.</summary>
        public bool SetOut()
        {
            if (Current != State.Idle) return false;
            StartCoroutine(SetOutRoutine());
            return true;
        }

        IEnumerator SetOutRoutine()
        {
            Current = State.Travelling;
            yield return FadeTo(1f, 0.5f);
            PrepareRuins();
            _huntsAtStart = Inventory.Hunts;
            _packAtStart.Clear();
            foreach (var kv in Inventory.Owned()) _packAtStart[kv.Key.id] = kv.Value;
            GolemSealed = WarrenCleared = false;
            GoblinsBeaten = 0;
            _seen.Clear();
            Results = null;
            TravelTo(Camp, Vector3.forward);
            _startedAt = Time.time;
            Current = State.Active;
            yield return new WaitForSeconds(0.2f);
            yield return FadeTo(0f, 0.6f);
            if (_combat != null) _combat.Announce("MISSION: " + MissionName.ToUpperInvariant(), new Color(1f, 0.85f, 0.4f));
        }

        /// <summary>A fresh, sleeping golem in the ruins, and nothing left over from the last hunt.</summary>
        void PrepareRuins()
        {
            var waiting = GameObject.Find("Golem Respawner");
            if (waiting != null) Destroy(waiting);
            if (Circle != null) { Destroy(Circle.gameObject); Circle = null; }
            var golem = FindAnyObjectByType<BastionGolem>();
            if (golem != null && (golem.CurrentState != BastionGolem.State.Dormant || golem.Health.Current < golem.Health.maxHealth))
            {
                Destroy(golem.gameObject);
                golem = null;
            }
            if (golem == null) BastionGolem.Create(GolemHome, 180f);
        }

        // ---------- Going home ----------

        /// <summary>Travels back to town: hands the mission in if it is done, or gives it up if not.</summary>
        public bool ReturnHome()
        {
            if (Current == State.Travelling) return false;
            StartCoroutine(ReturnRoutine());
            return true;
        }

        IEnumerator ReturnRoutine()
        {
            bool done = Current == State.Complete, abandoned = Current == State.Active;
            Current = State.Travelling;
            yield return FadeTo(1f, 0.5f);
            int gathered = 0;
            if (done)
            {
                // The squad picks up whatever is still lying in the ruins.
                foreach (var drop in new List<LootDrop>(LootDrop.Active))
                    if (drop != null) { drop.Collect(); gathered++; }
                Inventory.Add(ItemCatalog.Essence, Reward);
                if (WarrenCleared) Inventory.Add(ItemCatalog.Essence, BonusReward);
            }
            if (Circle != null) { Destroy(Circle.gameObject); Circle = null; }
            if (done) Results = BuildResults(gathered);
            else if (abandoned) Results = $"<b>MISSION ABANDONED</b>\n{MissionName}\nThe golem still stands. You keep what you picked up.";
            else Results = null;
            if (Results != null) ResultsShownAt = Time.unscaledTime;
            TravelTo(TownSquare.SpawnPoint, TownSquare.SpawnFacing);
            Current = State.Idle;
            yield return new WaitForSeconds(0.2f);
            yield return FadeTo(0f, 0.6f);
        }

        string BuildResults(int gathered)
        {
            var sb = new StringBuilder();
            sb.Append("<b>MISSION COMPLETE</b>\n").Append(MissionName).Append('\n');
            int secs = Mathf.RoundToInt(Elapsed);
            sb.Append($"Time {secs / 60}:{secs % 60:00}   ·   Goblins beaten {GoblinsBeaten}\n");
            sb.Append(WarrenCleared ? $"Bonus: warren cleared (+{BonusReward} essence)\n" : "Bonus missed: the goblin warren was not cleared\n");
            sb.Append($"Reward: {Reward} Sealing Essence\n");
            var gained = new StringBuilder();
            foreach (var kv in Inventory.Owned())
            {
                _packAtStart.TryGetValue(kv.Key.id, out int before);
                int got = kv.Value - before;
                if (got > 0) gained.Append(gained.Length > 0 ? ", " : "").Append(kv.Key.displayName).Append(" x").Append(got);
            }
            sb.Append("Brought home: ").Append(gained.Length > 0 ? gained.ToString() : "nothing");
            if (gathered > 0) sb.Append($"\n(your squad gathered {gathered} pickups you left behind)");
            return sb.ToString();
        }

        public void HideResults() => ResultsShownAt = -99f;

        // ---------- Travel ----------

        void TravelTo(Vector3 at, Vector3 facing)
        {
            if (_motor == null) return;
            _motor.Teleport(at);
            _motor.FaceDirection(facing);
            SquadBot.GatherAround(_motor);
            ThirdPersonCamera.SnapToTarget();
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float from = Fade, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                Fade = Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            Fade = target;
        }

        // ---------- Objectives ----------

        void Update()
        {
            if (!InMission) return;
            foreach (var g in GoblinDirector.Goblins)
                if (g != null && _seen.Add(g)) g.Sealed += _ => { if (InMission) GoblinsBeaten++; };
            if (_warren != null && _warren.IsCleared) WarrenCleared = true;

            if (Current == State.Active && Inventory.Hunts > _huntsAtStart)
            {
                GolemSealed = true;
                _finishedAt = Time.time;
                Current = State.Complete;
                Circle = ReturnCircle.Build(ReturnCirclePoint);
                if (_combat != null) _combat.Announce("MISSION COMPLETE", new Color(1f, 0.85f, 0.4f));
            }
            if (Current == State.Complete && Circle != null && Circle.PlayerInside) ReturnHome();
        }
    }
}
