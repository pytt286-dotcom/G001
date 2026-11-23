using System;
using System.Collections.Generic;
using Sealbound.Combat;
using Sealbound.Enemies;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// A computer-controlled Sealer in your squad. It is a full Sealer (the same body, movement and
    /// combat as you) whose controller is pressed by this brain instead of a person:
    /// it keeps formation behind you, picks fights with goblins and the golem, steps or dodges out of
    /// the golem's red ground warnings, jumps its ground waves, and gets back up beside you if it falls.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerCombat), typeof(PlayerInputReader))]
    public class SquadBot : MonoBehaviour
    {
        public static readonly List<SquadBot> All = new List<SquadBot>();
        public const int MaxSquad = 3;

        /// <summary>Builds one bot Sealer body at a point (set by the level, which knows how Sealers are made).</summary>
        public static Func<Vector3, bool, GameObject> Factory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Factory = null; _golem = null; }

        [Header("Squad")]
        public int slot;
        public bool mage;
        public float followDistance = 1.6f;
        public float engageRange = 16f;       // how far from you a fight can be for a bot to join it
        public float meleeReach = 1.7f;
        public float spellRange = 9f;
        public float dangerMargin = 1.1f;
        /// <summary>Stand still and do nothing (used by the developer check).</summary>
        public bool hold;

        // ---- read by the HUD and the play check ----
        public string Label => (mage ? "Mage" : "Sword") + " bot " + (slot + 1);
        public string Doing { get; private set; } = "Following";
        public bool IsDown => _vitals != null && _vitals.IsDown;
        public Health Health => _health;
        public float DamageDealt => _combat != null ? _combat.DamageDealt : 0f;
        public int Dodges { get; private set; }
        public int Sidesteps { get; private set; }
        public int Jumps { get; private set; }
        public int HitsTaken { get; private set; }
        public int TimesDowned { get; private set; }

        static readonly Vector3[] Formation =
        {
            new Vector3(-1.9f, 0f, -1.7f), new Vector3(1.9f, 0f, -1.7f), new Vector3(0f, 0f, -2.8f),
        };

        PlayerMotor _motor;
        PlayerCombat _combat;
        PlayerInputReader _input;
        PlayerVitals _vitals;
        Health _health;
        Health _target;
        Collider[] _targetColliders;
        float _retargetAt, _nextAttackAt, _stuckTime, _lastHitAt = -9f;
        bool _wasInDanger;
        static BastionGolem _golem;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _input = GetComponent<PlayerInputReader>();
            _vitals = GetComponent<PlayerVitals>();
            _health = GetComponent<Health>();
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Start()
        {
            _motor.isBot = true;
            _input.MakeScripted();
            _combat.feelEffects = false;
            if (_vitals != null)
            {
                _vitals.feelEffects = false;
                _vitals.respawnDelay = 6f;
                _vitals.RespawnPoint = () => SlotPosition(PlayerMotor.Main);
                _vitals.Fell += () => { TimesDowned++; Doing = "Down"; };
            }
            if (_health != null) _health.Damaged += (info, dealt) => { if (dealt > 0f) HitsTaken++; };
            if (mage) _input.Drive(Vector2.zero, false, switchClass: true);
            _nextAttackAt = Time.time + 0.5f + slot * 0.2f;
        }

        // ---------- Squad size ----------

        /// <summary>Adds or removes bots so the squad has <paramref name="count"/> of them (0 to 3).</summary>
        public static void SetSquadSize(int count)
        {
            count = Mathf.Clamp(count, 0, MaxSquad);
            All.RemoveAll(b => b == null);
            while (All.Count > count)
            {
                var last = All[All.Count - 1];
                All.RemoveAt(All.Count - 1);
                Combat.SealBurst.Spawn(last.transform.position + Vector3.up, 1.6f, new Color(1f, 0.85f, 0.4f));
                Destroy(last.gameObject);
            }
            var leader = PlayerMotor.Main;
            while (All.Count < count && Factory != null && leader != null)
            {
                int slot = FreeSlot();
                bool mage = slot == 1; // the squad: sword, mage, sword
                var go = Factory(SlotPosition(leader, slot), mage);
                if (go == null) break;
                var bot = go.GetComponent<SquadBot>();
                if (bot == null) bot = go.AddComponent<SquadBot>();
                bot.slot = slot;
                bot.mage = mage;
                go.name = bot.Label;
                Combat.SealBurst.Spawn(go.transform.position + Vector3.up, 1.6f, new Color(1f, 0.85f, 0.4f));
                if (!All.Contains(bot)) All.Add(bot);
            }
        }

        static int FreeSlot()
        {
            for (int s = 0; s < MaxSquad; s++)
            {
                bool used = false;
                foreach (var b in All) if (b != null && b.slot == s) used = true;
                if (!used) return s;
            }
            return 0;
        }

        static Vector3 SlotPosition(PlayerMotor leader, int slot)
        {
            if (leader == null) return Vector3.zero;
            Vector3 p = leader.transform.position + leader.transform.rotation * Formation[Mathf.Clamp(slot, 0, Formation.Length - 1)];
            p.y = leader.transform.position.y + 0.2f;
            return p;
        }

        Vector3 SlotPosition(PlayerMotor leader) => SlotPosition(leader, slot);

        // ---------- Golem hits (its attacks are aimed at you, but bots in the way get hurt too) ----------

        /// <summary>Hurts every bot inside a circle (a golem slam, stomp or boulder).</summary>
        public static void HitCircle(Vector3 at, float radius, float maxY, float damage, float impact, float launch, GameObject source)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b == null || b.IsDown) continue;
                Vector3 rel = b.transform.position - at; rel.y = 0f;
                if (rel.magnitude <= radius + 0.35f && b.transform.position.y < maxY)
                    b.TakeHit(damage, rel.sqrMagnitude > 0.01f ? rel.normalized : Vector3.forward, impact, launch, source);
            }
        }

        /// <summary>A hit from a big attack. Dodging through it (the dodge's untouchable moment) avoids it.</summary>
        public void TakeHit(float damage, Vector3 direction, float impact, float launch, GameObject source)
        {
            if (Time.time - _lastHitAt < 0.8f) return; // one hit per attack
            _lastHitAt = Time.time;
            var info = new DamageInfo { Amount = damage, Point = transform.position + Vector3.up, Direction = direction, Source = source, Impact = impact };
            if (_health.TryDamage(info, out _) && launch > 0f) _motor.Launch(launch);
        }

        // ---------- The brain ----------

        void Update()
        {
            var leader = PlayerMotor.Main;
            if (leader == null || _input == null) return;
            if (IsDown) { _input.Drive(Vector2.zero, false); return; }
            if (hold) { Doing = "Holding still"; _input.Drive(Vector2.zero, false); return; }
            Vector3 pos = transform.position;

            // Lost far behind (or fell off something): catch up.
            if (Flat(leader.transform.position - pos).magnitude > 45f || pos.y < -20f)
            {
                _motor.Teleport(SlotPosition(leader));
                _target = null;
                return;
            }

            // 1. Get out of the golem's red warnings: walk out early, dodge out late.
            if (GroundWarning.Danger(pos, dangerMargin, out Vector3 escape, out float timeLeft))
            {
                bool dodge = timeLeft < 0.45f && !_motor.IsDodging && _motor.Stamina >= _motor.dodgeStaminaCost;
                if (dodge) Dodges++;
                else if (!_wasInDanger) Sidesteps++;
                _wasInDanger = true;
                Doing = "Avoiding the golem";
                _input.Drive(new Vector2(escape.x, escape.z), true, dodge: dodge);
                return;
            }
            _wasInDanger = false;

            // 2. Jump the golem's ground wave just before it arrives.
            bool jump = false;
            if (_motor.IsGrounded && Shockwave.Incoming(pos, out float waveIn) && waveIn < 0.22f)
            {
                jump = true;
                Jumps++;
            }

            // 3. Fight, or keep formation.
            if (Time.time >= _retargetAt || !Alive(_target)) PickTarget(leader);
            if (_target != null) Fight(pos, jump);
            else Follow(leader, pos, jump);
        }

        void Follow(PlayerMotor leader, Vector3 pos, bool jump)
        {
            Vector3 to = Flat(SlotPosition(leader) - pos);
            float d = to.magnitude;
            if (d < followDistance * 0.5f)
            {
                Doing = "Following";
                _input.Drive(Vector2.zero, false, jump);
                _stuckTime = 0f;
                return;
            }
            Doing = "Following";
            Vector3 dir = to / d;
            float push = Mathf.Clamp01(d / followDistance);
            bool sprint = d > 6f || leader.IsSprinting;
            _input.Drive(new Vector2(dir.x, dir.z) * push, sprint, jump || Stuck(d > 2f));
        }

        void Fight(Vector3 pos, bool jump)
        {
            Vector3 aim = AimPoint(pos);
            Vector3 to = Flat(aim - pos);
            float d = to.magnitude;
            Vector3 dir = d > 0.01f ? to / d : transform.forward;
            float reach = mage ? spellRange : meleeReach;

            if (d > reach + 0.4f)
            {
                Doing = mage ? "Moving to cast" : "Charging in";
                _input.Drive(new Vector2(dir.x, dir.z), d > 7f, jump || Stuck(true));
                return;
            }
            _stuckTime = 0f;
            if (mage && d < 4f)
            {
                // Too close for a mage: back away while still facing the enemy.
                Doing = "Keeping distance";
                _input.Drive(new Vector2(-dir.x, -dir.z), false, jump);
                return;
            }

            Doing = "Fighting " + TargetName();
            if (!_combat.IsAttacking) _motor.FaceDirection(dir);
            bool attack = false;
            int skill = -1;
            if (!_combat.IsAttacking && Time.time >= _nextAttackAt)
            {
                // A steady chain of basic attacks, with a skill now and then when it's ready.
                if (UnityEngine.Random.value < 0.18f && _combat.CooldownRemaining(0) <= 0f) skill = 0;
                else attack = true;
                _nextAttackAt = Time.time + (mage ? 0.25f : 0.12f) + UnityEngine.Random.Range(0f, 0.15f);
            }
            _input.Drive(new Vector2(dir.x, dir.z) * 0.05f, false, jump, attack: attack, skill: skill);
        }

        bool Stuck(bool trying)
        {
            if (!trying || _motor.PlanarVelocity.magnitude > 1.5f) { _stuckTime = 0f; return false; }
            _stuckTime += Time.deltaTime;
            if (_stuckTime < 0.8f) return false;
            _stuckTime = 0f;
            return true; // hop over whatever is in the way
        }

        // ---------- Choosing a fight ----------

        void PickTarget(PlayerMotor leader)
        {
            _retargetAt = Time.time + 0.6f;
            Vector3 lp = leader.transform.position, pos = transform.position;
            Health best = null;
            float bestScore = float.MaxValue;

            foreach (var g in GoblinDirector.Goblins)
            {
                if (g == null || g.IsDead || g.CurrentState == Goblin.State.Emerging) continue;
                var h = g.GetComponent<Health>();
                if (!Alive(h)) continue;
                float fromLeader = Flat(g.transform.position - lp).magnitude;
                if (fromLeader > engageRange) continue;
                // Goblins near you first, then near me; spread out so we don't all chase one.
                float score = fromLeader * 0.7f + Flat(g.transform.position - pos).magnitude + Claimed(h) * 6f;
                if (score < bestScore) { bestScore = score; best = h; }
            }

            if (best == null)
            {
                if (_golem == null) _golem = FindAnyObjectByType<BastionGolem>();
                if (_golem != null && _golem.Engaged && _golem.CanBeHurt && Flat(_golem.transform.position - lp).magnitude < 32f)
                    best = _golem.GetComponent<Health>();
            }

            if (best != _target)
            {
                _target = best;
                _targetColliders = best != null ? best.GetComponentsInChildren<Collider>() : null;
            }
        }

        int Claimed(Health h)
        {
            int n = 0;
            foreach (var b in All) if (b != null && b != this && b._target == h) n++;
            return n;
        }

        static bool Alive(Health h)
        {
            if (h == null || h.IsDead || !h.gameObject.activeInHierarchy) return false;
            var golem = h.GetComponent<BastionGolem>();
            if (golem != null) return golem.Engaged && golem.CanBeHurt;
            return true;
        }

        string TargetName() => _target == null ? "" : _target.GetComponent<BastionGolem>() != null ? "the golem" : "a goblin";

        /// <summary>The nearest point of the target's body (the golem is huge, so aim for its legs, not its middle).</summary>
        Vector3 AimPoint(Vector3 from)
        {
            if (_target == null) return from;
            Vector3 best = _target.transform.position;
            float bestD = float.MaxValue;
            if (_targetColliders != null)
                foreach (var c in _targetColliders)
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    if (c is MeshCollider mc && !mc.convex) continue; // closest point only works on solid shapes
                    Vector3 p = c.ClosestPoint(from + Vector3.up);
                    float dd = (p - from).sqrMagnitude;
                    if (dd < bestD) { bestD = dd; best = p; }
                }
            return best;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>On you: B (d-pad left) changes the squad size, 3, 0, 1, 2 and back to 3.</summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class SquadCaller : MonoBehaviour
    {
        PlayerInputReader _input;
        PlayerCombat _combat;

        void Awake() { _input = GetComponent<PlayerInputReader>(); _combat = GetComponent<PlayerCombat>(); }

        void Update()
        {
            if (_input == null || !_input.SquadPressed) return;
            int now = 0;
            foreach (var b in SquadBot.All) if (b != null) now++;
            int next = now >= SquadBot.MaxSquad ? 0 : now + 1;
            SquadBot.SetSquadSize(next);
            if (_combat != null) _combat.Announce(next == 0 ? "SQUAD DISMISSED" : "SQUAD: " + next + (next == 1 ? " BOT" : " BOTS"), new Color(1f, 0.85f, 0.4f));
        }
    }
}
