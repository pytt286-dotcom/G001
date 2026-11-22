using System;
using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// The Bastion Golem: a 15 m walking fortress of carved stone, the first slice's boss.
    /// It is built from stone blocks on a simple skeleton and animated entirely by code.
    ///
    /// Fight: its stone hide shrugs off most damage. Its weak points are four glowing runes on its
    /// forearms and shins. Breaking one drops it to its knees with its chest core exposed for heavy damage.
    /// At half health it roars, its runes blaze orange and regrow, and it fights faster with new attacks.
    /// Every attack is shown first by a red outline on the limb and a red marking on the ground.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BastionGolem : MonoBehaviour
    {
        public enum State { Dormant, Waking, Idle, Walking, Slam, DoubleSlam, Stomp, Charge, Throw, Kneel, Roar, Dead }

        public string displayName = "Bastion Golem";
        public float maxHealth = 3000f;
        [Tooltip("Share of normal hits that gets through the stone hide.")]
        public float stoneArmour = 0.3f;

        [Header("Arena")]
        public Vector3 arenaCentre;
        public float arenaRadius = 14f;
        public float wakeRadius = 21f;
        public float leashRadius = 50f;

        [Header("Movement")]
        public float walkSpeed = 3.4f;
        public float turnSpeed = 55f;
        public float chargeSpeed = 20f;

        [Header("Damage")]
        public float slamDamage = 28f;
        public float doubleSlamDamage = 36f;
        public float stompDamage = 24f;
        public float shockwaveDamage = 20f;
        public float chargeDamage = 32f;
        public float boulderDamage = 24f;

        public State CurrentState => _state;
        public float StateTime => _t;
        public int Phase { get; private set; } = 1;
        public Health Health => _health;
        public float HealthFraction => _health != null ? _health.Current / Mathf.Max(1f, _health.maxHealth) : 0f;
        public bool Engaged => _state != State.Dormant && _state != State.Dead;
        public bool CoreExposed => _core != null && _core.Exposed;
        public bool CanBeHurt => Engaged && _state != State.Waking && _state != State.Roar;
        public IReadOnlyList<GolemRune> LimbRunes => _runes;

        /// <summary>Big announcements for the screen ("RUNE BROKEN!", "SEALED!").</summary>
        public event Action<string, Color> Announce;

        static readonly Color Stone = new Color(0.52f, 0.5f, 0.47f);
        static readonly Color DarkStone = new Color(0.36f, 0.35f, 0.34f);
        static readonly Color RuneBlue = new Color(0.12f, 0.5f, 1f);
        static readonly Color RuneFire = new Color(1f, 0.36f, 0.04f);

        // ---- skeleton ----
        Transform _hips, _torso, _head, _shoulderL, _shoulderR, _elbowL, _elbowR, _fistL, _fistR;
        Transform _thighL, _thighR, _kneeL, _kneeR, _footL, _footR;
        readonly List<GolemRune> _runes = new List<GolemRune>(); // arm L, arm R, leg L, leg R
        GolemRune _core;
        AttackTelegraph _warnArmL, _warnArmR, _warnLegL, _warnLegR, _warnBody;
        readonly List<Material> _eyeMats = new List<Material>();
        readonly List<GameObject> _blocks = new List<GameObject>();

        const float HipHeight = 6f, LegLength = 6f, ArmReach = 8.9f;

        // ---- state ----
        Health _health;
        State _state = State.Dormant;
        float _t, _cooldown = 1.5f;
        float _tempo = 1f;
        bool _hitDone, _impactDone, _secondWaveDone, _rightArm, _rightLeg;
        Vector3 _targetPoint, _chargeDir, _chargeStart;
        float _chargeLength;
        GroundWarning _warning;
        GameObject _heldBoulder;
        float _stride, _lastStrideSin;
        float _eyeGlow, _shakeTimer;
        State _resumeAfterKneel;

        // ---- player ----
        Transform _player;
        Health _playerHealth;
        PlayerMotor _motor;
        PlayerCombat _combat;
        PlayerVitals _vitals;

        // ---- sound ----
        AudioSource _audio;
        AudioClip _thud, _boom, _roar, _crumble;

        struct Pose
        {
            public float hipDrop, torsoPitch, torsoYaw, torsoRoll, headPitch;
            public Quaternion armL, armR;
            public float elbowL, elbowR, liftL, liftR, swingL, swingR;

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose
                {
                    hipDrop = Mathf.Lerp(a.hipDrop, b.hipDrop, t),
                    torsoPitch = Mathf.Lerp(a.torsoPitch, b.torsoPitch, t),
                    torsoYaw = Mathf.Lerp(a.torsoYaw, b.torsoYaw, t),
                    torsoRoll = Mathf.Lerp(a.torsoRoll, b.torsoRoll, t),
                    headPitch = Mathf.Lerp(a.headPitch, b.headPitch, t),
                    armL = Quaternion.Slerp(a.armL, b.armL, t),
                    armR = Quaternion.Slerp(a.armR, b.armR, t),
                    elbowL = Mathf.Lerp(a.elbowL, b.elbowL, t),
                    elbowR = Mathf.Lerp(a.elbowR, b.elbowR, t),
                    liftL = Mathf.Lerp(a.liftL, b.liftL, t),
                    liftR = Mathf.Lerp(a.liftR, b.liftR, t),
                    swingL = Mathf.Lerp(a.swingL, b.swingL, t),
                    swingR = Mathf.Lerp(a.swingR, b.swingR, t),
                };
            }
        }

        Pose _pose;

        // ======================================================================
        // Building
        // ======================================================================

        public static BastionGolem Create(Vector3 position, float yaw)
        {
            var go = new GameObject("Bastion Golem");
            go.layer = 2; // the camera looks through it instead of zooming in on a leg
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            var health = go.AddComponent<Health>();
            var g = go.AddComponent<BastionGolem>();
            g.arenaCentre = new Vector3(position.x, 0f, position.z);
            health.maxHealth = g.maxHealth;
            health.damageMultiplier = g.stoneArmour;
            health.ResetHealth();
            g.Build();
            return g;
        }

        void Build()
        {
            var root = transform;
            var renderers = new List<Renderer>();
            var armL = new List<Renderer>(); var armR = new List<Renderer>();
            var legL = new List<Renderer>(); var legR = new List<Renderer>();
            var body = new List<Renderer>();

            _hips = Joint(root, "Hips", new Vector3(0f, HipHeight, 0f));
            body.Add(Block(_hips, "Pelvis", new Vector3(0f, 0.3f, 0f), new Vector3(5.2f, 1.8f, 3.2f), DarkStone, "floor_foundation_allsides"));

            _torso = Joint(_hips, "Torso", Vector3.zero);
            body.Add(Block(_torso, "Belly", new Vector3(0f, 1.3f, 0.1f), new Vector3(5.4f, 1.9f, 3.6f), Stone, "floor_foundation_allsides"));
            body.Add(Block(_torso, "Chest", new Vector3(0f, 3.8f, 0f), new Vector3(7.6f, 5f, 4.4f), Stone, "floor_foundation_allsides"));
            body.Add(Block(_torso, "Shoulder Plate L", new Vector3(-4.4f, 6.3f, 0f), new Vector3(3.2f, 1.1f, 3.6f), DarkStone, "floor_foundation_allsides"));
            body.Add(Block(_torso, "Shoulder Plate R", new Vector3(4.4f, 6.3f, 0f), new Vector3(3.2f, 1.1f, 3.6f), DarkStone, "floor_foundation_allsides"));

            _head = Joint(_torso, "Head", new Vector3(0f, 6.4f, 0.4f));
            body.Add(Block(_head, "Skull", new Vector3(0f, 1.3f, 0f), new Vector3(3f, 2.6f, 2.8f), DarkStone, "floor_foundation_allsides"));
            body.Add(Block(_head, "Brow", new Vector3(0f, 2.15f, 1.35f), new Vector3(3.3f, 0.55f, 0.7f), Stone, "floor_foundation_allsides"));
            for (int s = -1; s <= 1; s += 2)
            {
                float eyeZ = RealArt.Has ? 1.62f : 1.43f; // the scanned skull bulges past its block
                var eye = Shapes.Make(PrimitiveType.Sphere, _head, s < 0 ? "Eye L" : "Eye R", new Vector3(0.75f * s, 1.45f, eyeZ), new Vector3(0.6f, 0.26f, 0.2f), RuneBlue, false, true);
                eye.layer = 2;
                _eyeMats.Add(eye.GetComponent<Renderer>().material);
            }

            var aim = new GameObject("Aim Point").transform;
            aim.SetParent(_torso, false);
            aim.localPosition = new Vector3(0f, 3.6f, 2.4f);
            _health.aimPoint = aim;

            BuildArm(-1, out _shoulderL, out _elbowL, out _fistL, armL);
            BuildArm(1, out _shoulderR, out _elbowR, out _fistR, armR);
            BuildLeg(-1, out _thighL, out _kneeL, out _footL, legL);
            BuildLeg(1, out _thighR, out _kneeR, out _footR, legR);

            // Weak points.
            float lift = RealArt.Has ? 0.15f : 0f; // sit on the rock's surface, not inside it
            _runes.Add(GolemRune.Create(this, _elbowL, "Rune (left arm)", new Vector3(0f, -2.6f, 1.25f + lift), 0.95f, 150f, false));
            _runes.Add(GolemRune.Create(this, _elbowR, "Rune (right arm)", new Vector3(0f, -2.6f, 1.25f + lift), 0.95f, 150f, false));
            _runes.Add(GolemRune.Create(this, _kneeL, "Rune (left leg)", new Vector3(0f, -1.4f, 1.3f + lift), 0.95f, 190f, false));
            _runes.Add(GolemRune.Create(this, _kneeR, "Rune (right leg)", new Vector3(0f, -1.4f, 1.3f + lift), 0.95f, 190f, false));
            _core = GolemRune.Create(this, _torso, "Rune Core", new Vector3(0f, 3.6f, RealArt.Has ? 2.55f : 2.3f), 1.5f, 99999f, true);

            renderers.AddRange(body); renderers.AddRange(armL); renderers.AddRange(armR); renderers.AddRange(legL); renderers.AddRange(legR);
            if (!RealArt.Has) ToonOutline.Add(Visible(renderers), 0.08f); // ink lines suit the cartoon look only

            _warnArmL = Warn(_shoulderL, armL);
            _warnArmR = Warn(_shoulderR, armR);
            _warnLegL = Warn(_thighL, legL);
            _warnLegR = Warn(_thighR, legR);
            _warnBody = Warn(_torso, body);

            _pose = DormantPose();
            ApplyPose();
        }

        void BuildArm(int side, out Transform shoulder, out Transform elbow, out Transform fist, List<Renderer> list)
        {
            string s = side < 0 ? "L" : "R";
            shoulder = Joint(_torso, "Shoulder " + s, new Vector3(4.9f * side, 5.4f, 0f));
            list.Add(Block(shoulder, "Upper Arm " + s, new Vector3(0f, -2.1f, 0f), new Vector3(2.1f, 4.4f, 2.1f), Stone, "floor_foundation_allsides"));
            elbow = Joint(shoulder, "Elbow " + s, new Vector3(0f, -4.3f, 0f));
            list.Add(Block(elbow, "Forearm " + s, new Vector3(0f, -2f, 0f), new Vector3(2.3f, 4f, 2.3f), Stone, "pillar"));
            fist = Joint(elbow, "Fist " + s, new Vector3(0f, -4.6f, 0f));
            list.Add(Block(fist, "Fist Block " + s, Vector3.zero, new Vector3(3f, 2.4f, 3f), DarkStone, "floor_foundation_allsides"));
        }

        void BuildLeg(int side, out Transform thigh, out Transform knee, out Transform foot, List<Renderer> list)
        {
            string s = side < 0 ? "L" : "R";
            thigh = Joint(_hips, "Thigh " + s, new Vector3(2.2f * side, 0f, 0f));
            list.Add(Block(thigh, "Thigh Block " + s, new Vector3(0f, -1.5f, 0f), new Vector3(2.4f, 3.2f, 2.6f), Stone, "floor_foundation_allsides"));
            knee = Joint(thigh, "Knee " + s, new Vector3(0f, -3f, 0f));
            list.Add(Block(knee, "Shin " + s, new Vector3(0f, -1.35f, 0f), new Vector3(2.2f, 2.7f, 2.4f), Stone, "pillar"));
            foot = Joint(knee, "Foot " + s, new Vector3(0f, -2.75f, 0f));
            list.Add(Block(foot, "Foot Block " + s, new Vector3(0f, 0f, 0.5f), new Vector3(3f, 0.5f, 3.8f), DarkStone, "floor_foundation_allsides"));
        }

        static Transform Joint(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = 2;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>A stone block with a collider; dressed with a KayKit stone piece when the art is there.</summary>
        Renderer Block(Transform parent, string name, Vector3 localCentre, Vector3 size, Color color, string piece)
        {
            var go = Shapes.Make(PrimitiveType.Cube, parent, name, localCentre, size, color, true);
            go.layer = 2;
            _blocks.Add(go);
            if (RealArt.Has)
            {
                // A scanned boulder, slightly bigger than its block so the pieces overlap like a natural rock body.
                string slug = ScanFor(name);
                var rock = RealArt.FitInsideBox(go.transform, RealArt.Scan(slug, null, 0f, 0.5f), 1.14f);
                if (rock != null)
                {
                    Shapes.SetLayerRecursive(rock, 2);
                    var r = rock.GetComponentInChildren<Renderer>();
                    if (r != null) return r;
                }
            }
            if (ArtLibrary.HasScenery)
            {
                var dressed = ArtLibrary.FitInsideBox(go.transform, ArtLibrary.Dungeon + piece);
                if (dressed != null)
                {
                    Shapes.SetLayerRecursive(dressed, 2);
                    var r = dressed.GetComponentInChildren<Renderer>();
                    if (r != null) return r;
                }
            }
            return go.GetComponent<Renderer>();
        }

        /// <summary>Which scanned rock each part of the body is made from.</summary>
        static string ScanFor(string part)
        {
            if (part.StartsWith("Chest") || part.StartsWith("Pelvis")) return "boulder_01";
            if (part.StartsWith("Belly") || part.StartsWith("Fist")) return "namaqualand_boulder_02";
            if (part.StartsWith("Skull") || part.StartsWith("Upper Arm") || part.StartsWith("Thigh")) return "rock_07";
            if (part.StartsWith("Foot")) return "stone_01";
            return "rock_09"; // forearms, shins, shoulder plates, brow
        }

        static IEnumerable<Renderer> Visible(List<Renderer> list)
        {
            foreach (var r in list) if (r != null && r.enabled) yield return r;
        }

        AttackTelegraph Warn(Transform holder, List<Renderer> parts)
        {
            var t = holder.gameObject.AddComponent<AttackTelegraph>();
            t.thickness = 0.16f;
            var all = new List<Renderer>();
            foreach (var r in parts)
                if (r != null) all.AddRange(r.transform.parent.GetComponentsInChildren<Renderer>());
            var visible = new List<Renderer>();
            foreach (var r in all) if (r.enabled && !SkinnedOutline.IsOutline(r) && !visible.Contains(r) && r.GetComponentInParent<GolemRune>() == null) visible.Add(r);
            t.Build(visible);
            return t;
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.InvulnerableCheck = () => !CanBeHurt;
            _health.Damaged += OnDamaged;
            _health.Died += OnDefeated;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _thud = CombatAudio.Noise("Golem Step", 0.4f, 0.9f, 42f);
            _boom = CombatAudio.Noise("Golem Slam", 0.8f, 1f, 34f);
            _roar = Roar();
            _crumble = CombatAudio.Noise("Golem Crumble", 1.6f, 0.8f, 28f);
        }

        void Start()
        {
            var m = FindAnyObjectByType<PlayerMotor>();
            if (m != null)
            {
                _player = m.transform;
                _motor = m;
                _playerHealth = m.GetComponent<Health>();
                _combat = m.GetComponent<PlayerCombat>();
                _vitals = m.GetComponent<PlayerVitals>();
                if (_vitals != null) _vitals.Fell += OnPlayerFell;
            }
            SetEyes(0.1f);
        }

        void OnDestroy()
        {
            if (_vitals != null) _vitals.Fell -= OnPlayerFell;
            if (_warning != null) _warning.Cancel();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_shakeTimer > 0f) _shakeTimer -= dt;

            var target = TargetPose(dt, out float rate);
            _pose = Pose.Lerp(_pose, target, 1f - Mathf.Exp(-rate * dt));
            if (_state != State.Dead) ApplyPose();
            UpdateEyes(dt);
            UpdateCamera();
        }

        // ======================================================================
        // Brain
        // ======================================================================

        Pose TargetPose(float dt, out float rate)
        {
            rate = 8f;
            float sdt = dt * _tempo;
            switch (_state)
            {
                case State.Dormant: return UpdateDormant();
                case State.Waking: return UpdateWaking(ref rate);
                case State.Idle: return UpdateIdle(sdt);
                case State.Walking: return UpdateWalking(sdt, ref rate);
                case State.Slam: return UpdateSlam(false, ref rate);
                case State.DoubleSlam: return UpdateSlam(true, ref rate);
                case State.Stomp: return UpdateStomp(ref rate);
                case State.Charge: return UpdateCharge(sdt, ref rate);
                case State.Throw: return UpdateThrow(ref rate);
                case State.Kneel: return UpdateKneel(ref rate);
                case State.Roar: return UpdateRoar(ref rate);
                default: return _pose;
            }
        }

        void SetState(State s)
        {
            _state = s;
            _t = 0f;
            _hitDone = _impactDone = _secondWaveDone = false;
            HideWarnings();
        }

        float T(float seconds) => seconds / _tempo; // phase two is faster

        bool PlayerAlive => _player != null && _playerHealth != null && !_playerHealth.IsDead && (_vitals == null || !_vitals.IsDown);

        Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        float PlayerDistance => _player != null ? Flat(_player.position - transform.position).magnitude : 999f;
        float PlayerFromArena => _player != null ? Flat(_player.position - arenaCentre).magnitude : 999f;

        Pose UpdateDormant()
        {
            if (PlayerAlive && PlayerFromArena < wakeRadius)
            {
                SetState(State.Waking);
                Announce?.Invoke("THE BASTION GOLEM AWAKENS", RuneBlue);
            }
            return DormantPose();
        }

        Pose UpdateWaking(ref float rate)
        {
            float dur = 3f;
            float k = Mathf.Clamp01(_t / dur);
            rate = 3f;
            if (_shakeTimer <= 0f) { Shake(0.12f + k * 0.15f); _shakeTimer = 0.45f; if (_t < dur - 0.6f) Play(_thud, 0.6f); }
            _eyeGlow = Mathf.Clamp01((_t - 1.6f) / 0.6f);
            if (_t >= dur - 0.5f && !_impactDone)
            {
                _impactDone = true;
                Play(_roar, 1f);
                Shake(0.75f);
            }
            if (_t >= dur)
            {
                SetState(State.Idle);
                _cooldown = 0.8f;
            }
            return k < 0.75f ? Pose.Lerp(DormantPose(), IdlePose(), Mathf.SmoothStep(0f, 1f, k / 0.75f)) : RoarPose();
        }

        Pose UpdateIdle(float sdt)
        {
            if (CheckLeash()) return IdlePose();
            if (!PlayerAlive) return IdlePose();
            FacePlayer(turnSpeed * _tempo);
            _cooldown -= sdt;
            if (_cooldown <= 0f) ChooseAttack();
            else if (PlayerDistance > 10f) SetState(State.Walking);
            var p = IdlePose();
            p.torsoPitch += Mathf.Sin(Time.time * 1.6f) * 1.5f;
            return p;
        }

        Pose UpdateWalking(float sdt, ref float rate)
        {
            if (CheckLeash()) return IdlePose();
            if (!PlayerAlive) { SetState(State.Idle); return IdlePose(); }
            FacePlayer(turnSpeed * _tempo);
            _cooldown -= sdt;
            float d = PlayerDistance;

            if (_cooldown <= 0f && (d <= 10f || d > 15f)) { ChooseAttack(); return IdlePose(); }
            if (d < 7.5f && _cooldown > 0f) { SetState(State.Idle); return IdlePose(); }

            float speed = walkSpeed * _tempo * (LegsBroken >= 2 ? 0.7f : 1f);
            MoveBy(transform.forward * speed * Time.deltaTime);
            return WalkPose(speed, ref rate);
        }

        int LegsBroken => (_runes[2].Broken ? 1 : 0) + (_runes[3].Broken ? 1 : 0);

        void ChooseAttack()
        {
            float d = PlayerDistance;
            float roll = UnityEngine.Random.value;
            if (d < 6.5f)
            {
                _rightLeg = !_rightLeg;
                SetState(State.Stomp);
            }
            else if (d <= 10.5f)
            {
                _rightArm = !_rightArm;
                SetState(Phase == 2 && roll < 0.35f ? State.DoubleSlam : State.Slam);
            }
            else if (Phase == 2 && roll < 0.4f)
            {
                SetState(State.Throw);
            }
            else if (LegsBroken < 2 && roll < 0.75f)
            {
                SetState(State.Charge);
            }
            else
            {
                SetState(State.Walking);
                _cooldown = 0.6f;
                return;
            }
            _cooldown = UnityEngine.Random.Range(1.1f, 2f) / _tempo;
        }

        // ---------- Slams ----------

        Pose UpdateSlam(bool both, ref float rate)
        {
            float track = T(0.4f), windup = T(both ? 1.25f : 1.0f), fall = 0.17f, planted = both ? 2f : 1.6f, recover = T(0.7f);
            bool right = _rightArm;
            var warnArm = right ? _warnArmR : _warnArmL;
            var shoulder = right ? _shoulderR : _shoulderL;

            if (_t < windup) FacePlayer(turnSpeed * 1.4f * _tempo);
            if (_t < track && PlayerAlive) _targetPoint = PredictSlamPoint(both ? null : shoulder);
            if (_t >= track && _warning == null && !_impactDone)
            {
                if (PlayerAlive) _targetPoint = PredictSlamPoint(both ? null : shoulder);
                _warning = GroundWarning.Circle(_targetPoint, both ? 6f : 4.2f, windup + fall - _t);
            }

            Pose p = IdlePose();
            if (_t < windup)
            {
                float k = Mathf.SmoothStep(0f, 1f, _t / windup);
                if (both) { _warnArmL.SetWarning(k); _warnArmR.SetWarning(k); }
                else warnArm.SetWarning(k);
                var raised = Quaternion.Euler(150f, 0f, right ? 12f : -12f);
                if (both || !right) p.armL = Quaternion.Slerp(RestArm(false), raised, k);
                if (both || right) p.armR = Quaternion.Slerp(RestArm(true), raised, k);
                if (both || !right) p.elbowL = -45f * k;
                if (both || right) p.elbowR = -45f * k;
                p.torsoPitch = -8f * k;
                p.torsoYaw = both ? 0f : (right ? -12f : 12f) * k;
                p.hipDrop = 0.4f * k;
                rate = 10f;
            }
            else if (_t < windup + fall)
            {
                float k = (_t - windup) / fall;
                k *= k;
                p = SlamPose(both, right, k);
                rate = 40f;
                if (both) { _warnArmL.SetStrike(); _warnArmR.SetStrike(); } else warnArm.SetStrike();
            }
            else if (_t < windup + fall + planted)
            {
                if (!_impactDone)
                {
                    _impactDone = true;
                    HideWarnings();
                    Vector3 at = both ? Flat((_fistL.position + _fistR.position) * 0.5f) : Flat((right ? _fistR : _fistL).position);
                    Impact(at, both ? 6f : 4.2f, both ? doubleSlamDamage : slamDamage * (ArmBroken(right) ? 0.6f : 1f), 9f, both ? 1f : 0.8f);
                }
                p = SlamPose(both, right, 1f);
                rate = 30f;
            }
            else
            {
                float k = Mathf.Clamp01((_t - windup - fall - planted) / recover);
                p = Pose.Lerp(SlamPose(both, right, 1f), IdlePose(), Mathf.SmoothStep(0f, 1f, k));
                rate = 10f;
                if (k >= 1f) SetState(State.Idle);
            }
            return p;
        }

        bool ArmBroken(bool right) => _runes[right ? 1 : 0].Broken;

        /// <summary>Where a slam would land: the player's position, pulled in to what the arm can reach.</summary>
        Vector3 PredictSlamPoint(Transform shoulder)
        {
            Vector3 p = Flat(_player.position);
            Vector3 c = Flat(transform.position);
            Vector3 to = p - c;
            float d = Mathf.Clamp(to.magnitude, 6f, 9.5f);
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
            Vector3 point = c + dir * d;
            if (shoulder != null) point += transform.right * (shoulder == _shoulderR ? 1.2f : -1.2f);
            return point;
        }

        Pose SlamPose(bool both, bool right, float k)
        {
            var p = IdlePose();
            p.hipDrop = Mathf.Lerp(0.4f, 1.8f, k);
            p.torsoPitch = Mathf.Lerp(-8f, 36f, k);
            p.torsoYaw = both ? 0f : Mathf.Lerp(right ? -12f : 12f, right ? 10f : -10f, k);
            p.headPitch = 12f * k;
            var raised = Quaternion.Euler(150f, 0f, right ? 12f : -12f);
            // Aim the slamming arm(s) at the ground target, worked out in the torso's (planted) frame.
            if (both || !right) { p.armL = Quaternion.Slerp(raised, AimArmAt(_shoulderL, _targetPoint + transform.right * (both ? -1.6f : 0f), p), k); p.elbowL = Mathf.Lerp(-45f, 0f, k); }
            if (both || right) { p.armR = Quaternion.Slerp(raised, AimArmAt(_shoulderR, _targetPoint + transform.right * (both ? 1.6f : 0f), p), k); p.elbowR = Mathf.Lerp(-45f, 0f, k); }
            return p;
        }

        /// <summary>Shoulder rotation (relative to the torso in pose <paramref name="p"/>) that points the arm at a ground point.</summary>
        Quaternion AimArmAt(Transform shoulder, Vector3 groundPoint, Pose p)
        {
            // Rebuild where the shoulder will be in the planted pose, so the aim doesn't wobble as the body bends.
            Quaternion torsoRot = transform.rotation * Quaternion.Euler(p.torsoPitch, p.torsoYaw, p.torsoRoll);
            Vector3 hips = transform.position + Vector3.up * (HipHeight - p.hipDrop);
            Vector3 shoulderPos = hips + torsoRot * shoulder.localPosition;
            Vector3 target = groundPoint + Vector3.up * 1.2f;
            Vector3 dirLocal = Quaternion.Inverse(torsoRot) * (target - shoulderPos);
            return Quaternion.FromToRotation(Vector3.down, dirLocal.normalized);
        }

        // ---------- Stomp ----------

        Pose UpdateStomp(ref float rate)
        {
            float windup = T(0.9f), down = 0.12f, hold = T(0.75f), recover = T(0.5f);
            bool right = _rightLeg;
            var warn = right ? _warnLegR : _warnLegL;
            var foot = right ? _footR : _footL;
            Pose p = IdlePose();

            if (_t < windup)
            {
                float k = Mathf.SmoothStep(0f, 1f, _t / windup);
                warn.SetWarning(k);
                if (right) p.liftR = k; else p.liftL = k;
                p.torsoPitch = -6f * k;
                p.torsoRoll = (right ? 6f : -6f) * k;
                p.armL = Quaternion.Euler(-20f * k, 0f, -35f * k);
                p.armR = Quaternion.Euler(-20f * k, 0f, 35f * k);
                if (_warning == null) _warning = GroundWarning.Circle(Flat(foot.position) + transform.forward * 0.5f, 3.4f, windup + down);
                rate = 10f;
            }
            else if (_t < windup + down)
            {
                warn.SetStrike();
                float k = (_t - windup) / down;
                if (right) p.liftR = 1f - k; else p.liftL = 1f - k;
                p.torsoPitch = 6f;
                rate = 45f;
            }
            else
            {
                if (!_impactDone)
                {
                    _impactDone = true;
                    HideWarnings();
                    Vector3 at = Flat(foot.position) + transform.forward * 0.5f;
                    Impact(at, 3.4f, stompDamage, 7f, 0.9f);
                    Shockwave.Spawn(at, 15f * _tempo, 17f, shockwaveDamage, gameObject);
                }
                if (Phase == 2 && !_secondWaveDone && _t >= windup + down + 0.5f)
                {
                    _secondWaveDone = true;
                    Shockwave.Spawn(Flat(foot.position), 15f * _tempo, 17f, shockwaveDamage, gameObject);
                    Shake(0.35f);
                    Play(_thud, 0.9f);
                }
                p.torsoPitch = 6f;
                p.hipDrop = 0.6f;
                float k = Mathf.Clamp01((_t - windup - down - hold) / recover);
                p = Pose.Lerp(p, IdlePose(), k);
                if (k >= 1f) SetState(State.Idle);
            }
            return p;
        }

        // ---------- Charge ----------

        Pose UpdateCharge(float sdt, ref float rate)
        {
            float windup = T(1.1f), lockAt = windup * 0.7f, recover = LegsBroken > 0 ? 1.9f : 1.4f;
            Pose p = IdlePose();

            if (_t < windup)
            {
                FacePlayer(turnSpeed * 1.6f * _tempo);
                float k = Mathf.SmoothStep(0f, 1f, _t / windup);
                _warnBody.SetWarning(k); _warnLegL.SetWarning(k); _warnLegR.SetWarning(k);
                if (_t >= lockAt && _warning == null)
                {
                    _chargeDir = transform.forward;
                    _chargeStart = transform.position;
                    _chargeLength = ChargeLength(_chargeDir);
                    _warning = GroundWarning.Lane(_chargeStart, _chargeDir, _chargeLength + 3f, 6f, windup - _t);
                }
                p.hipDrop = 1.4f * k;
                p.torsoPitch = 28f * k;
                p.headPitch = -18f * k;
                p.armL = Quaternion.Slerp(RestArm(false), Quaternion.Euler(45f, 0f, -18f), k);
                p.armR = Quaternion.Slerp(RestArm(true), Quaternion.Euler(45f, 0f, 18f), k);
                return p;
            }

            float runTime = _chargeLength / (chargeSpeed * _tempo);
            if (_t < windup + runTime)
            {
                if (_chargeDir == Vector3.zero) { _chargeDir = transform.forward; _chargeStart = transform.position; _chargeLength = ChargeLength(_chargeDir); }
                _warnBody.SetStrike();
                float step = chargeSpeed * _tempo * Time.deltaTime;
                MoveBy(_chargeDir * step);
                p = WalkPose(chargeSpeed * 0.55f, ref rate);
                p.hipDrop = 1.2f;
                p.torsoPitch = 30f;
                p.headPitch = -20f;
                p.armL = Quaternion.Euler(40f, 0f, -18f);
                p.armR = Quaternion.Euler(40f, 0f, 18f);
                rate = 20f;
                if (!_hitDone && PlayerAlive)
                {
                    Vector3 rel = Flat(_player.position - transform.position);
                    float along = Vector3.Dot(rel, _chargeDir);
                    float side = Vector3.Cross(_chargeDir, rel).y;
                    if (along > -1f && along < 4.5f && Mathf.Abs(side) < 3.2f && _player.position.y < 5f)
                    {
                        _hitDone = true;
                        Vector3 push = (_chargeDir + Vector3.Cross(Vector3.up, _chargeDir) * Mathf.Sign(side == 0f ? 1f : side) * 0.8f).normalized;
                        HitPlayer(chargeDamage, push, 1f, 7f);
                        Shake(0.6f);
                    }
                }
                return p;
            }

            if (!_impactDone)
            {
                _impactDone = true;
                HideWarnings();
                Shake(0.5f);
                Play(_boom, 0.8f);
                DustPuff.Spawn(Flat(_footL.position), 2.5f, 8);
                DustPuff.Spawn(Flat(_footR.position), 2.5f, 8);
            }
            // Skids to a stop and stumbles: a window to punish the leg runes.
            float r = Mathf.Clamp01((_t - windup - runTime) / recover);
            var stumble = IdlePose();
            stumble.hipDrop = 2f;
            stumble.torsoPitch = 34f;
            stumble.headPitch = 10f;
            stumble.armL = Quaternion.Euler(-35f, 0f, -20f);
            stumble.armR = Quaternion.Euler(-35f, 0f, 20f);
            p = Pose.Lerp(stumble, IdlePose(), Mathf.SmoothStep(0.6f, 1f, r));
            rate = 12f;
            if (r >= 1f) { SetState(State.Idle); _chargeDir = Vector3.zero; }
            return p;
        }

        float ChargeLength(Vector3 dir)
        {
            // Run toward the arena edge (a little past the player), but never out of the ruins.
            Vector3 from = Flat(transform.position - arenaCentre);
            float b = Vector3.Dot(from, dir), c = from.sqrMagnitude - (arenaRadius + 1f) * (arenaRadius + 1f);
            float toEdge = -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
            float want = PlayerDistance + 7f;
            return Mathf.Clamp(Mathf.Min(want, toEdge), 6f, 26f);
        }

        // ---------- Boulder throw (phase two) ----------

        Pose UpdateThrow(ref float rate)
        {
            float windup = T(1.2f), release = 0.15f, recover = T(0.8f);
            Pose p = IdlePose();
            if (_t < windup)
            {
                FacePlayer(turnSpeed * 1.5f * _tempo);
                float k = Mathf.SmoothStep(0f, 1f, _t / windup);
                _warnArmR.SetWarning(k);
                p.armR = Quaternion.Slerp(RestArm(true), Quaternion.Euler(165f, 0f, 10f), k);
                p.elbowR = -60f * k;
                p.torsoYaw = -25f * k;
                p.torsoPitch = -10f * k;
                if (_heldBoulder == null)
                {
                    _heldBoulder = GolemBoulder.MakeRock(1.9f);
                    _heldBoulder.transform.SetParent(_fistR, false);
                    _heldBoulder.transform.localPosition = new Vector3(0f, -1.8f, 0f);
                }
                _heldBoulder.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, k);
                rate = 10f;
            }
            else if (_t < windup + release)
            {
                float k = (_t - windup) / release;
                p.armR = Quaternion.Slerp(Quaternion.Euler(165f, 0f, 10f), Quaternion.Euler(-70f, 0f, 5f), k);
                p.torsoYaw = Mathf.Lerp(-25f, 15f, k);
                p.torsoPitch = 10f * k;
                rate = 40f;
                _warnArmR.SetStrike();
            }
            else
            {
                if (!_impactDone)
                {
                    _impactDone = true;
                    HideWarnings();
                    Vector3 from = _heldBoulder != null ? _heldBoulder.transform.position : _fistR.position;
                    if (_heldBoulder != null) Destroy(_heldBoulder);
                    _heldBoulder = null;
                    Vector3 target = PlayerAlive ? Flat(_player.position + _motor.PlanarVelocity * 0.35f) : Flat(transform.position + transform.forward * 12f);
                    GolemBoulder.Throw(from, target, 1.1f, boulderDamage, gameObject);
                    Play(_thud, 0.6f);
                }
                float k = Mathf.Clamp01((_t - windup - release) / recover);
                var after = IdlePose();
                after.armR = Quaternion.Euler(-70f, 0f, 5f);
                after.torsoYaw = 15f;
                p = Pose.Lerp(after, IdlePose(), Mathf.SmoothStep(0f, 1f, k));
                if (k >= 1f) SetState(State.Idle);
            }
            return p;
        }

        // ---------- Kneel (a rune broke) ----------

        Pose UpdateKneel(ref float rate)
        {
            float down = Phase == 2 ? 3.4f : 4.2f, rise = 0.9f;
            rate = 6f;
            if (_t > 0.5f && !_core.Exposed && _t < down) _core.SetExposed(true);
            if (_t >= down && _core.Exposed) _core.SetExposed(false);
            if (_t >= down + rise)
            {
                SetState(State.Idle);
                _cooldown = 0.6f;
                return IdlePose();
            }
            return _t < down ? KneelPose() : Pose.Lerp(KneelPose(), IdlePose(), (_t - down) / rise);
        }

        // ---------- Roar (phase two begins) ----------

        Pose UpdateRoar(ref float rate)
        {
            rate = 6f;
            if (!_impactDone && _t > 0.5f)
            {
                _impactDone = true;
                Play(_roar, 1f);
                Shake(1f);
                SetRuneColour(RuneFire);
                foreach (var r in _runes) r.Restore();
                Announce?.Invoke("ITS RUNES BLAZE!", RuneFire);
            }
            if (_t >= 2.2f)
            {
                SetState(State.Idle);
                _cooldown = 0.5f;
                return IdlePose();
            }
            return RoarPose();
        }

        // ======================================================================
        // Poses
        // ======================================================================

        static Quaternion RestArm(bool right) => Quaternion.Euler(-8f, 0f, right ? 8f : -8f);

        Pose IdlePose()
        {
            return new Pose
            {
                hipDrop = 0.3f, torsoPitch = 6f, headPitch = 4f,
                armL = RestArm(false), armR = RestArm(true),
                elbowL = -14f, elbowR = -14f,
            };
        }

        Pose DormantPose()
        {
            var p = KneelPose();
            p.hipDrop = 3.9f;
            p.torsoPitch = 48f;
            p.headPitch = 30f;
            return p;
        }

        Pose KneelPose()
        {
            var p = IdlePose();
            p.hipDrop = 3.6f;
            p.torsoPitch = 50f;
            p.headPitch = 20f;
            // Hands planted on the ground in front, holding the body up.
            p.armL = AimArmAt(_shoulderL, transform.position + transform.forward * 7.5f - transform.right * 4.5f, p);
            p.armR = AimArmAt(_shoulderR, transform.position + transform.forward * 7.5f + transform.right * 4.5f, p);
            p.elbowL = p.elbowR = -10f;
            return p;
        }

        Pose RoarPose()
        {
            var p = IdlePose();
            p.torsoPitch = -14f;
            p.headPitch = -25f;
            p.armL = Quaternion.Euler(120f, 0f, -55f);
            p.armR = Quaternion.Euler(120f, 0f, 55f);
            p.elbowL = p.elbowR = -35f;
            return p;
        }

        Pose WalkPose(float speed, ref float rate)
        {
            const float strideLength = 3.4f;
            _stride += speed * Time.deltaTime / strideLength * Mathf.PI;
            float s = Mathf.Sin(_stride);
            if (Mathf.Sign(s) != Mathf.Sign(_lastStrideSin)) Footfall(s > 0f ? _footR : _footL);
            _lastStrideSin = s;

            var p = IdlePose();
            p.swingL = 24f * s;
            p.swingR = -24f * s;
            p.hipDrop = 0.35f + 0.25f * Mathf.Abs(s);
            p.torsoRoll = 3f * s;
            p.torsoYaw = 5f * s;
            p.armL = Quaternion.Euler(-8f - 16f * s, 0f, -8f);
            p.armR = Quaternion.Euler(-8f + 16f * s, 0f, 8f);
            rate = 14f;
            return p;
        }

        void ApplyPose()
        {
            var p = _pose;
            float h = HipHeight - p.hipDrop;
            _hips.localPosition = new Vector3(0f, h, 0f);
            float crouch = Mathf.Acos(Mathf.Clamp(h / LegLength, 0.05f, 1f)) * Mathf.Rad2Deg;
            PoseLeg(_thighL, _kneeL, _footL, crouch, p.swingL, p.liftL);
            PoseLeg(_thighR, _kneeR, _footR, crouch, p.swingR, p.liftR);
            _torso.localRotation = Quaternion.Euler(p.torsoPitch, p.torsoYaw, p.torsoRoll);
            _head.localRotation = Quaternion.Euler(p.headPitch, 0f, 0f);
            _shoulderL.localRotation = p.armL;
            _shoulderR.localRotation = p.armR;
            _elbowL.localRotation = Quaternion.Euler(p.elbowL, 0f, 0f);
            _elbowR.localRotation = Quaternion.Euler(p.elbowR, 0f, 0f);
        }

        static void PoseLeg(Transform thigh, Transform knee, Transform foot, float crouch, float swing, float lift)
        {
            float t = -crouch + swing - 65f * lift;
            float k = 2f * crouch + Mathf.Max(0f, -swing) * 0.9f + 80f * lift;
            thigh.localRotation = Quaternion.Euler(t, 0f, 0f);
            knee.localRotation = Quaternion.Euler(k, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-(t + k), 0f, 0f); // keep the sole flat
        }

        // ======================================================================
        // Hits, damage and effects
        // ======================================================================

        void Impact(Vector3 at, float radius, float damage, float launch, float shake)
        {
            Shake(shake * DistanceFalloff(at));
            Play(_boom, 1f);
            DustPuff.Spawn(at, radius, 14);
            if (!PlayerAlive) return;
            Vector3 rel = Flat(_player.position - at);
            if (rel.magnitude <= radius + 0.35f && _player.position.y < at.y + 2.5f)
                HitPlayer(damage, rel.sqrMagnitude > 0.01f ? rel.normalized : transform.forward, 0.9f, launch);
        }

        public void HitPlayer(float damage, Vector3 direction, float impact, float launch)
        {
            if (_playerHealth == null) return;
            var info = new DamageInfo
            {
                Amount = damage, Point = _player.position + Vector3.up, Direction = direction,
                Source = gameObject, Impact = impact
            };
            if (_playerHealth.TryDamage(info, out _))
            {
                if (launch > 0f && _motor != null) _motor.Launch(launch);
            }
            else if (_combat != null && !_playerHealth.IsDead) _combat.NotifyDodged();
        }

        void OnDamaged(DamageInfo info, float dealt)
        {
            if (info.Source != gameObject)
                DamageNumbers.Show(info.Point + Vector3.up * 0.5f, dealt, false, true);
            if (Phase == 1 && HealthFraction <= 0.5f && !_health.IsDead) BeginPhaseTwo();
        }

        /// <summary>Damage dealt to a rune passes to the golem, ignoring its stone armour.</summary>
        public void OnRuneHit(GolemRune rune, DamageInfo info, float amount)
        {
            if (!CanBeHurt || amount <= 0f) return;
            info.Amount = amount / Mathf.Max(0.01f, _health.damageMultiplier);
            info.Source = gameObject; // no second damage number
            _health.TryDamage(info, out _);
        }

        public void OnRuneBroken(GolemRune rune)
        {
            if (!CanBeHurt) return;
            var info = new DamageInfo { Amount = _health.maxHealth * 0.06f / Mathf.Max(0.01f, _health.damageMultiplier), Point = rune.transform.position, Source = gameObject, Impact = 1f };
            _health.TryDamage(info, out _);
            if (_health.IsDead || _state == State.Roar) return;
            Announce?.Invoke("RUNE BROKEN! STRIKE THE CORE!", Phase == 2 ? RuneFire : RuneBlue);
            Play(_crumble, 0.8f);
            Shake(0.7f);
            if (_warning != null) { _warning.Cancel(); _warning = null; }
            if (_heldBoulder != null) { Destroy(_heldBoulder); _heldBoulder = null; }
            SetState(State.Kneel);
        }

        void BeginPhaseTwo()
        {
            Phase = 2;
            _tempo = 1.22f;
            turnSpeed *= 1.3f;
            if (_core != null) _core.SetExposed(false);
            if (_warning != null) { _warning.Cancel(); _warning = null; }
            if (_heldBoulder != null) { Destroy(_heldBoulder); _heldBoulder = null; }
            SetState(State.Roar);
        }

        void OnDefeated()
        {
            HideWarnings();
            if (_warning != null) { _warning.Cancel(); _warning = null; }
            _state = State.Dead;
            _t = 0f;
            _core.SetExposed(false);
            Announce?.Invoke("BASTION GOLEM SEALED", new Color(1f, 0.85f, 0.3f));
            SealBurst.Spawn(_torso.position + Vector3.up * 3f, 14f, RuneBlue);
            Play(_crumble, 1f);
            Shake(1f);
            ThirdPersonCamera.SetBossFraming(0f);

            // It falls apart into a heap of stone.
            foreach (var block in _blocks)
            {
                if (block == null) continue;
                var bt = block.transform;
                bt.SetParent(null, true);
                var rb = block.AddComponent<Rigidbody>();
                rb.mass = 50f;
                Vector3 outward = Flat(bt.position - transform.position).normalized;
                rb.linearVelocity = outward * UnityEngine.Random.Range(2f, 6f) + Vector3.up * UnityEngine.Random.Range(0f, 3f);
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
                Destroy(block, 12f);
            }
            foreach (var r in _runes) if (r != null) r.gameObject.SetActive(false);
            GolemRespawner.Schedule(arenaCentre, transform.eulerAngles.y, 25f);
            Destroy(gameObject, 0.1f);
        }

        void OnPlayerFell()
        {
            if (_state == State.Dead || _state == State.Dormant) return;
            ResetFight();
        }

        bool CheckLeash()
        {
            if (_player == null || PlayerFromArena <= leashRadius) return false;
            ResetFight();
            return true;
        }

        /// <summary>The player fell or ran away: the golem heals, returns to its place and sleeps again.</summary>
        void ResetFight()
        {
            if (_warning != null) { _warning.Cancel(); _warning = null; }
            if (_heldBoulder != null) { Destroy(_heldBoulder); _heldBoulder = null; }
            Phase = 1;
            _tempo = 1f;
            turnSpeed = 55f;
            _health.ResetHealth();
            SetRuneColour(RuneBlue);
            foreach (var r in _runes) r.Restore();
            _core.SetExposed(false);
            transform.position = arenaCentre;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _eyeGlow = 0f;
            SetState(State.Dormant);
            ThirdPersonCamera.SetBossFraming(0f);
        }

        void HideWarnings()
        {
            if (_warnArmL == null) return;
            _warnArmL.Hide(); _warnArmR.Hide(); _warnLegL.Hide(); _warnLegR.Hide(); _warnBody.Hide();
        }

        void FacePlayer(float degreesPerSecond)
        {
            if (_player == null) return;
            Vector3 to = Flat(_player.position - transform.position);
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), degreesPerSecond * Time.deltaTime);
        }

        void MoveBy(Vector3 delta)
        {
            Vector3 p = transform.position + Flat(delta);
            Vector3 off = Flat(p - arenaCentre);
            if (off.magnitude > arenaRadius) p = arenaCentre + off.normalized * arenaRadius;
            p.y = arenaCentre.y;
            transform.position = p;
        }

        void Footfall(Transform foot)
        {
            Vector3 at = Flat(foot.position);
            Shake(0.16f * DistanceFalloff(at));
            Play(_thud, 0.5f * DistanceFalloff(at) + 0.2f);
            DustPuff.Spawn(at, 1.5f, 5);
        }

        float DistanceFalloff(Vector3 at) => _player == null ? 0.3f : Mathf.Clamp01(1.2f - Flat(_player.position - at).magnitude / 40f);

        void Shake(float amount) => ThirdPersonCamera.Shake(amount);

        void Play(AudioClip clip, float volume)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        void UpdateCamera()
        {
            if (_player == null) return;
            bool near = Engaged && PlayerFromArena < leashRadius;
            ThirdPersonCamera.SetBossFraming(near ? 1f : 0f);
        }

        // ---------- Eyes and rune colours ----------

        void SetEyes(float glow) => _eyeGlow = glow;

        void UpdateEyes(float dt)
        {
            if (_state == State.Dormant) _eyeGlow = Mathf.MoveTowards(_eyeGlow, 0.08f, dt);
            Color c = (Phase == 2 ? RuneFire : RuneBlue);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
            foreach (var m in _eyeMats)
            {
                m.color = Color.Lerp(DarkStone, c, _eyeGlow);
                m.SetColor("_EmissionColor", c * (1.8f * _eyeGlow * pulse));
            }
        }

        void SetRuneColour(Color c)
        {
            foreach (var r in _runes) r.SetColor(c);
            if (_core != null) _core.SetColor(c);
        }

        /// <summary>A low growl built in code, until real sound effects arrive.</summary>
        static AudioClip Roar()
        {
            int rate = CombatAudio.Rate;
            int n = Mathf.CeilToInt(rate * 1.6f);
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(85f, 48f, t) + Mathf.Sin(t * 60f) * 6f;
                phase += 2f * Mathf.PI * f / rate;
                float saw = (phase / Mathf.PI % 2f) - 1f;
                lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2.0 - 1.0), 0.08f);
                float env = Mathf.Min(1f, t * 8f) * Mathf.Pow(1f - t, 0.7f);
                data[i] = (saw * 0.55f + lp * 0.6f) * env * 0.9f;
            }
            return CombatAudio.Make("Golem Roar", data);
        }

        // ---------- For the developer play check ----------

        /// <summary>Breaks one limb rune at once (developer check only).</summary>
        public void DebugBreakRune(int index)
        {
            if (index < 0 || index >= _runes.Count) return;
            var h = _runes[index].Health;
            var info = new DamageInfo { Amount = h.Current + 1f, Point = _runes[index].transform.position };
            h.TryDamage(info, out _);
        }
    }

    /// <summary>Brings a sealed golem back after a while, so the fight can be replayed.</summary>
    public class GolemRespawner : MonoBehaviour
    {
        Vector3 _at;
        float _yaw, _wait;

        public static void Schedule(Vector3 at, float yaw, float seconds)
        {
            var r = new GameObject("Golem Respawner").AddComponent<GolemRespawner>();
            r._at = at;
            r._yaw = yaw;
            r._wait = seconds;
        }

        void Update()
        {
            _wait -= Time.deltaTime;
            if (_wait > 0f) return;
            BastionGolem.Create(_at, 180f);
            Destroy(gameObject);
        }
    }
}
