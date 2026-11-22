using System;
using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// Burrow Goblins (dagger fighters) and Goblin Slingers (rock throwers).
    /// They climb out of burrows, circle the player, take turns attacking with a clear
    /// wind-up (they glow red), flinch when hit, and can be launched and juggled.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class Goblin : MonoBehaviour
    {
        public enum Kind { Burrower, Slinger }
        enum State { Emerging, Circling, Approaching, Windup, Strike, Recover, Stagger, Airborne, Dead }

        public Kind kind;

        [Header("Movement")]
        public float runSpeed = 7f;
        public float circleSpeed = 3.5f;
        public float circleRadius = 4.5f;
        public float gravity = 30f;

        [Header("Attack")]
        public float attackRange = 1.7f;
        public float windupTime = 0.45f;
        public float strikeTime = 0.14f;
        public float recoverTime = 0.6f;
        public float damage = 10f;
        public float lungeSpeed = 11f;
        public float projectileSpeed = 17f;
        public Vector2 attackDelay = new Vector2(0.8f, 2.4f);

        [Header("Parts (set by Create)")]
        public Transform visual;
        public Renderer[] tintRenderers;

        public bool IsDead => _state == State.Dead;
        public event Action<Goblin> Sealed;

        CharacterController _cc;
        Health _health;
        Transform _target;
        Health _targetHealth;
        PlayerCombat _targetCombat;

        State _state = State.Emerging;
        float _stateTime;
        float _slotAngle, _slotDrift;
        float _nextAttackAt;
        float _vertical;
        Vector3 _knock;
        bool _strikeDone;
        float _flash;
        Material[] _mats;
        AttackTelegraph _telegraph;
        Color[] _baseColors;


        // ---------- Creation ----------

        public static Goblin Create(Kind kind, Vector3 position, Transform target)
        {
            var go = new GameObject(kind == Kind.Burrower ? "Burrow Goblin" : "Goblin Slinger");
            go.transform.position = position;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.2f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.6f, 0f);
            cc.stepOffset = 0.3f;

            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);

            Color skin = kind == Kind.Burrower ? new Color(0.45f, 0.62f, 0.25f) : new Color(0.55f, 0.58f, 0.3f);
            Color cloth = kind == Kind.Burrower ? new Color(0.4f, 0.26f, 0.15f) : new Color(0.55f, 0.2f, 0.18f);
            var body = Shapes.Make(PrimitiveType.Capsule, vis, "Body", new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.45f, 0.55f), cloth);
            var head = Shapes.Make(PrimitiveType.Sphere, vis, "Head", new Vector3(0f, 1.05f, 0.05f), new Vector3(0.62f, 0.55f, 0.6f), skin);
            var earL = Shapes.Make(PrimitiveType.Cube, vis, "Ear L", new Vector3(-0.38f, 1.12f, 0f), new Vector3(0.35f, 0.08f, 0.14f), skin);
            var earR = Shapes.Make(PrimitiveType.Cube, vis, "Ear R", new Vector3(0.38f, 1.12f, 0f), new Vector3(0.35f, 0.08f, 0.14f), skin);
            earL.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            earR.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Shapes.Make(PrimitiveType.Cube, vis, "Eye L", new Vector3(-0.12f, 1.1f, 0.3f), new Vector3(0.1f, 0.07f, 0.05f), new Color(1f, 0.9f, 0.2f), false, true);
            Shapes.Make(PrimitiveType.Cube, vis, "Eye R", new Vector3(0.12f, 1.1f, 0.3f), new Vector3(0.1f, 0.07f, 0.05f), new Color(1f, 0.9f, 0.2f), false, true);
            if (kind == Kind.Burrower)
                Shapes.Make(PrimitiveType.Cube, vis, "Dagger", new Vector3(0.35f, 0.55f, 0.35f), new Vector3(0.06f, 0.05f, 0.5f), new Color(0.7f, 0.7f, 0.72f));
            else
                Shapes.Make(PrimitiveType.Sphere, vis, "Rock Pouch", new Vector3(-0.3f, 0.45f, 0.1f), Vector3.one * 0.25f, new Color(0.45f, 0.42f, 0.38f));

            var health = go.AddComponent<Health>();
            health.maxHealth = kind == Kind.Burrower ? 45f : 30f;

            var g = go.AddComponent<Goblin>();
            g.kind = kind;
            g.visual = vis;
            g.tintRenderers = new[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>(), earL.GetComponent<Renderer>(), earR.GetComponent<Renderer>() };
            g._telegraph = go.AddComponent<AttackTelegraph>();
            g._telegraph.Build(vis.GetComponentsInChildren<Renderer>());
            g._target = target;
            if (kind == Kind.Slinger)
            {
                g.circleRadius = 9f;
                g.windupTime = 0.6f;
                g.recoverTime = 0.8f;
                g.damage = 8f;
                g.attackDelay = new Vector2(1.5f, 3.5f);
            }
            return g;
        }

        // ---------- Lifecycle ----------

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _health.InvulnerableCheck = () => _state == State.Emerging && _stateTime < 0.4f;
            _health.Damaged += OnDamaged;
            _health.Died += OnSealed;
        }

        void Start()
        {
            _health.ResetHealth();
            if (_target == null)
            {
                var p = FindAnyObjectByType<PlayerMotor>();
                if (p != null) _target = p.transform;
            }
            if (_target != null)
            {
                _targetHealth = _target.GetComponent<Health>();
                _targetCombat = _target.GetComponent<PlayerCombat>();
                Vector3 from = transform.position - _target.position;
                _slotAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
            }
            _slotDrift = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(8f, 20f);
            _nextAttackAt = Time.time + UnityEngine.Random.Range(attackDelay.x, attackDelay.y);

            _mats = new Material[tintRenderers.Length];
            _baseColors = new Color[tintRenderers.Length];
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                _mats[i] = tintRenderers[i].material;
                _baseColors[i] = _mats[i].color;
            }

            GoblinDirector.Register(this);
            if (visual != null) visual.localPosition = new Vector3(0f, -1.3f, 0f);
        }

        void OnDestroy() => GoblinDirector.Unregister(this);

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;

            if (_state == State.Dead) { UpdateDead(dt); return; }

            Vector3 move = Vector3.zero;
            bool targetAlive = _target != null && (_targetHealth == null || !_targetHealth.IsDead);

            switch (_state)
            {
                case State.Emerging:
                    if (visual != null)
                        visual.localPosition = new Vector3(0f, Mathf.Lerp(-1.3f, 0f, Mathf.SmoothStep(0f, 1f, _stateTime / 0.6f)), 0f);
                    if (_stateTime >= 0.6f) SetState(State.Circling);
                    break;

                case State.Circling:
                    if (!targetAlive) break;
                    move = Circle(dt);
                    if (Time.time >= _nextAttackAt && GoblinDirector.RequestToken(this))
                        SetState(kind == Kind.Slinger ? State.Windup : State.Approaching);
                    break;

                case State.Approaching:
                {
                    if (!targetAlive) { ReleaseAndCircle(); break; }
                    Vector3 to = Flat(_target.position - transform.position);
                    if (to.magnitude <= attackRange) SetState(State.Windup);
                    else move = to.normalized * runSpeed;
                    if (_stateTime > 2.5f) ReleaseAndCircle();
                    break;
                }

                case State.Windup:
                    if (!targetAlive) { ReleaseAndCircle(); break; }
                    if (_stateTime >= windupTime) { _strikeDone = false; SetState(State.Strike); }
                    break;

                case State.Strike:
                    if (kind == Kind.Burrower)
                    {
                        if (_stateTime < 0.05f) _knock = transform.forward * lungeSpeed;
                        if (!_strikeDone && _stateTime >= strikeTime * 0.4f) { _strikeDone = true; TryHitTarget(); }
                    }
                    else if (!_strikeDone)
                    {
                        _strikeDone = true;
                        if (_target != null)
                            Projectile.Throw(transform.position + Vector3.up * 1.1f + transform.forward * 0.5f,
                                _target.position + Vector3.up * 1f, projectileSpeed, damage, gameObject);
                    }
                    if (_stateTime >= strikeTime) SetState(State.Recover);
                    break;

                case State.Recover:
                    if (_stateTime >= recoverTime) ReleaseAndCircle();
                    break;

                case State.Stagger:
                    if (_stateTime >= _staggerLength) ReleaseAndCircle();
                    break;

                case State.Airborne:
                    if (_cc.isGrounded && _vertical <= 0f && _stateTime > 0.1f)
                    {
                        _staggerLength = 0.5f;
                        SetState(State.Stagger);
                    }
                    break;
            }

            // Face the player when not mid-swing.
            if (_target != null && _state != State.Strike && _state != State.Airborne && _state != State.Emerging)
            {
                Vector3 look = Flat(_target.position - transform.position);
                if (look.sqrMagnitude > 0.01f)
                {
                    float turn = _state == State.Windup ? 360f : 720f;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), turn * dt);
                }
            }

            if (_state == State.Emerging) return;

            // Gravity, knockback and moving.
            float g = _state == State.Airborne && _vertical < 0f ? gravity * 0.6f : gravity;
            if (_cc.isGrounded && _vertical < 0f) _vertical = -2f;
            else _vertical -= g * dt;
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 30f * dt);
            Vector3 motion = (move + _knock + Separation() + Vector3.up * _vertical) * dt;
            _cc.Move(motion);

            UpdateTint(dt);
        }

        float _staggerLength;

        Vector3 Circle(float dt)
        {
            _slotAngle += _slotDrift * dt;
            Vector3 slot = _target.position + Quaternion.Euler(0f, _slotAngle, 0f) * Vector3.forward * circleRadius;
            Vector3 to = Flat(slot - transform.position);
            float dist = to.magnitude;
            if (dist < 0.2f) return Vector3.zero;
            float speed = dist > 3f ? runSpeed : circleSpeed;
            return to / dist * Mathf.Min(speed, dist * 4f);
        }

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var other in GoblinDirector.Goblins)
            {
                if (other == this || other == null || other.IsDead) continue;
                Vector3 away = Flat(transform.position - other.transform.position);
                float d = away.magnitude;
                if (d < 1.4f && d > 0.001f) push += away / d * (1.4f - d) * 4f;
            }
            return push;
        }

        void TryHitTarget()
        {
            if (_target == null || _targetHealth == null || _targetHealth.IsDead) return;
            Vector3 to = Flat(_target.position - transform.position);
            if (to.magnitude > attackRange + 0.7f) return;
            if (Vector3.Angle(transform.forward, to) > 70f) return;

            var info = new DamageInfo
            {
                Amount = damage, Point = _target.position + Vector3.up, Direction = to.normalized,
                Source = gameObject, Impact = 0.35f
            };
            if (!_targetHealth.TryDamage(info, out _) && _targetCombat != null) _targetCombat.NotifyDodged();
        }

        void ReleaseAndCircle()
        {
            GoblinDirector.ReleaseToken(this);
            _nextAttackAt = Time.time + UnityEngine.Random.Range(attackDelay.x, attackDelay.y);
            SetState(State.Circling);
        }

        void SetState(State s)
        {
            _state = s;
            _stateTime = 0f;
        }

        // ---------- Getting hit ----------

        void OnDamaged(DamageInfo info, float dealt)
        {
            _flash = 1f;
            DamageNumbers.Show(transform.position + Vector3.up * 1.4f, dealt, info.Impact > 0.6f, false);
            if (_health.IsDead) return;

            GoblinDirector.ReleaseToken(this);
            _knock = Flat(info.Direction).normalized * (3f + info.Impact * 9f);

            bool airborne = _state == State.Airborne;
            if (info.Launch >= 8f || (airborne && info.Launch > 0f))
            {
                _vertical = Mathf.Max(_vertical, info.Launch);
                _knock *= 0.3f;
                SetState(State.Airborne);
                return;
            }
            if (airborne)
            {
                _vertical = Mathf.Max(_vertical, 2f); // any hit keeps a juggled goblin up a little
                return;
            }
            _staggerLength = 0.25f + info.Impact * 0.35f;
            SetState(State.Stagger);
        }

        void OnSealed()
        {
            if (_telegraph != null) _telegraph.Hide();
            GoblinDirector.ReleaseToken(this);
            SetState(State.Dead);
            _cc.enabled = false;
            SealBurst.Spawn(transform.position + Vector3.up * 0.7f, 2f, new Color(0.35f, 0.75f, 0.95f));
            Sealed?.Invoke(this);
        }

        void UpdateDead(float dt)
        {
            if (visual != null)
            {
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(-85f, 0f, 0f), 1f - Mathf.Exp(-10f * dt));
                if (_stateTime > 0.5f) visual.localScale = Vector3.one * Mathf.Max(0f, 1f - (_stateTime - 0.5f) / 0.5f);
            }
            if (_stateTime > 1.05f) Destroy(gameObject);
        }

        void UpdateTint(float dt)
        {
            if (_mats == null) return;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            float warn = _state == State.Windup ? Mathf.Clamp01(_stateTime / windupTime) : 0f;
            for (int i = 0; i < _mats.Length; i++)
                _mats[i].color = Color.Lerp(_baseColors[i], Color.white, _flash);

            // Warning: a red outline that grows brighter and pulses faster as the attack nears.
            if (_telegraph != null)
            {
                if (_state == State.Windup) _telegraph.SetWarning(Mathf.Max(0.15f, warn));
                else if (_state == State.Strike) _telegraph.SetStrike();
                else _telegraph.Hide();
            }
            if (visual != null)
            {
                // Lean back during wind-up, lunge forward on the strike.
                float lean = _state == State.Windup ? -20f * warn : _state == State.Strike ? 25f : 0f;
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(lean, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
