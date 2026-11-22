using System;
using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using UnityEngine;

namespace Sealbound.Player
{
    public enum AttackKind { Basic, Skill, Heavy }

    [Serializable]
    public class AttackData
    {
        public string name;
        public AttackKind kind;
        public float windup, active, recovery;
        public float damage;
        public int hits = 1;
        public float range = 2.4f;
        public float arcDegrees = 150f;
        public float lungeSpeed;
        public float launchSpeed;
        public float hover;
        [Range(0f, 1f)] public float impact;
        public float hitStop;
        public float shake;
        public float staminaCost;
        public float cooldown;
        public bool spin;
        [Tooltip("Seconds after the attack starts when the perfect window opens. 0 = automatic.")]
        public float perfectStart;
        public float perfectLength = 0.16f;
        public Vector3 swingFrom, swingTo; // sword pivot euler angles

        public float Total => windup + active + recovery;
        public float PerfectStart => perfectStart > 0f ? perfectStart : windup + active + 0.05f;
        public float PerfectEnd => PerfectStart + perfectLength;
    }

    /// <summary>
    /// Rhythm-based Sword combat.
    ///
    /// Each attack has a short "perfect window" near the end of its swing, marked by a flash on the
    /// blade and a tick sound. Pressing in the window chains faster and hits harder. Pressing too
    /// early (mashing) breaks the combo and leaves you open. Skills used in the window are LINKS,
    /// and basic attacks timed after a skill are EXTENDERS, so skilled players can build long chains
    /// like basic, basic, skill, basic, skill, heavy finisher.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInputReader))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Basic combo")]
        public AttackData[] basicCombo =
        {
            new AttackData { name = "Slash", windup = 0.07f, active = 0.08f, recovery = 0.3f, damage = 10f, lungeSpeed = 7f,
                impact = 0.2f, hitStop = 0.035f, shake = 0.15f, swingFrom = new Vector3(10f, -80f, 0f), swingTo = new Vector3(20f, 80f, 0f) },
            new AttackData { name = "Return Slash", windup = 0.06f, active = 0.08f, recovery = 0.3f, damage = 12f, lungeSpeed = 7f,
                impact = 0.3f, hitStop = 0.04f, shake = 0.18f, swingFrom = new Vector3(25f, 85f, 0f), swingTo = new Vector3(5f, -85f, 0f) },
            new AttackData { name = "Overhead Cleave", windup = 0.11f, active = 0.1f, recovery = 0.36f, damage = 20f, lungeSpeed = 10f,
                impact = 0.75f, hitStop = 0.07f, shake = 0.4f, arcDegrees = 110f, range = 2.8f,
                swingFrom = new Vector3(-75f, 10f, 0f), swingTo = new Vector3(70f, 0f, 0f) },
        };
        public AttackData airSlash = new AttackData
        {
            name = "Air Slash", windup = 0.05f, active = 0.1f, recovery = 0.26f, damage = 12f, lungeSpeed = 6f,
            impact = 0.35f, hitStop = 0.045f, shake = 0.2f, arcDegrees = 200f, range = 2.6f, hover = 0.35f,
            swingFrom = new Vector3(-40f, -70f, 0f), swingTo = new Vector3(60f, 70f, 0f)
        };

        [Header("Skills (Q / R / F)")]
        public AttackData[] skills =
        {
            new AttackData { name = "Rising Edge", kind = AttackKind.Skill, windup = 0.08f, active = 0.12f, recovery = 0.3f,
                damage = 18f, launchSpeed = 13f, impact = 0.6f, hitStop = 0.06f, shake = 0.3f, arcDegrees = 120f, range = 2.6f,
                cooldown = 5f, swingFrom = new Vector3(60f, 0f, 0f), swingTo = new Vector3(-70f, 0f, 0f) },
            new AttackData { name = "Gale Thrust", kind = AttackKind.Skill, windup = 0.1f, active = 0.14f, recovery = 0.3f,
                damage = 24f, lungeSpeed = 24f, impact = 0.7f, hitStop = 0.06f, shake = 0.35f, arcDegrees = 60f, range = 3f,
                cooldown = 6f, swingFrom = new Vector3(5f, -15f, 0f), swingTo = new Vector3(0f, 0f, 0f) },
            new AttackData { name = "Runic Whirl", kind = AttackKind.Skill, windup = 0.08f, active = 0.4f, recovery = 0.3f,
                damage = 10f, hits = 3, impact = 0.4f, hitStop = 0.03f, shake = 0.2f, arcDegrees = 360f, range = 3f,
                cooldown = 8f, spin = true, swingFrom = new Vector3(20f, -90f, 0f), swingTo = new Vector3(20f, 90f, 0f) },
        };

        [Header("Heavy (E)")]
        public AttackData heavy = new AttackData
        {
            name = "Sealbreaker Slam", kind = AttackKind.Heavy, windup = 0.3f, active = 0.1f, recovery = 0.35f, damage = 38f,
            lungeSpeed = 9f, impact = 1f, hitStop = 0.11f, shake = 0.7f, arcDegrees = 360f, range = 3.2f, staminaCost = 25f,
            swingFrom = new Vector3(-90f, 0f, 0f), swingTo = new Vector3(80f, 0f, 0f)
        };

        [Header("Rhythm")]
        [Tooltip("Time after an attack fully ends during which a press still chains (no bonus).")]
        public float chainGrace = 0.15f;
        [Tooltip("Pause after a broken combo, during which you cannot attack.")]
        public float brokenStagger = 0.35f;
        public float perfectDamageBonus = 0.25f;
        public float linkDamageBonus = 0.4f;
        public float chainDamagePerStep = 0.06f;
        public int maxChainSteps = 6;
        public float linkCooldownRefund = 0.3f;
        [Tooltip("Part of the windup skipped when an attack is chained perfectly.")]
        [Range(0f, 1f)] public float perfectWindupSkip = 0.4f;

        [Header("Focus")]
        public float maxFocus = 100f;
        public float focusPerPerfect = 10f;
        public float focusPerLink = 18f;
        public float sealbreakerMultiplier = 2f;

        [Header("Feel")]
        public float aimAssistRange = 5f;
        public float aimAssistAngle = 70f;
        [Range(0f, 1f)] public float steerWhileAttacking = 0.15f;
        public int maxAirAttacks = 2;

        [Header("Sword")]
        public Transform swordPivot;
        public Renderer timingCueRenderer;
        public Vector3 restEuler = new Vector3(35f, 25f, 0f);

        // ---- read by the HUD ----
        public bool IsAttacking => _current != null;
        public int Chain { get; private set; }
        public int ComboHits { get; private set; }
        public float ChainMultiplier => 1f + Mathf.Min(Chain, maxChainSteps) * chainDamagePerStep;
        public float Focus { get; private set; }
        public bool FocusFull => Focus >= maxFocus;
        public float CooldownRemaining(int i) => i >= 0 && i < _cooldowns.Length ? _cooldowns[i] : 0f;
        public float CooldownTotal(int i) => i >= 0 && i < skills.Length ? skills[i].cooldown : 1f;
        /// <summary>Text popups for the HUD: "PERFECT", "LINK", "TOO EARLY"...</summary>
        public event Action<string, Color> Feedback;

        static readonly Color PerfectColor = new Color(1f, 0.85f, 0.3f);
        static readonly Color LinkColor = new Color(0.45f, 0.85f, 1f);
        static readonly Color BrokenColor = new Color(1f, 0.35f, 0.3f);

        PlayerMotor _motor;
        PlayerInputReader _input;
        Health _ownHealth;
        CombatAudio _audio;
        TrailRenderer _trail;
        Material _cueMaterial;
        Color _cueBase;
        float _cueFlash;

        AttackData _current;
        AttackKind _previousKind;
        float _attackTime;
        float _bonus = 1f;
        bool _broken, _landedCurrent, _landedPrevious, _cueShown;
        int _comboIndex, _airAttacksUsed, _lastSubHit;
        float _graceTimer, _staggerTimer, _sinceCombat = 10f;
        float[] _cooldowns;
        readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
        readonly Collider[] _overlap = new Collider[32];

        enum ChainResult { Fresh, Good, Perfect }

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _ownHealth = GetComponent<Health>();
            _audio = GetComponent<CombatAudio>();
            _cooldowns = new float[skills.Length];
            _motor.DodgeStarted += OnDodgeStarted;
        }

        void Start()
        {
            if (swordPivot != null)
            {
                swordPivot.localRotation = Quaternion.Euler(restEuler);
                _trail = swordPivot.GetComponentInChildren<TrailRenderer>();
                if (_trail != null) _trail.emitting = false;
            }
            if (timingCueRenderer != null)
            {
                _cueMaterial = timingCueRenderer.material;
                _cueBase = _cueMaterial.color;
                _cueMaterial.EnableKeyword("_EMISSION");
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
            if (_motor.IsGrounded) _airAttacksUsed = 0;
            if (_staggerTimer > 0f) _staggerTimer -= dt;

            _sinceCombat += dt;
            if (_sinceCombat > 4f) Focus = Mathf.Max(0f, Focus - 8f * dt);

            if (_current != null) UpdateAttack(dt);
            else if (_graceTimer > 0f)
            {
                _graceTimer -= dt;
                if (_graceTimer <= 0f) EndChain();
            }

            var request = ReadRequest(out int skillIndex);
            if (request != null) HandleRequest(request, skillIndex);

            UpdateSwordPose(dt);
            UpdateCue(dt);
        }

        // ---------- Input ----------

        AttackData ReadRequest(out int skillIndex)
        {
            skillIndex = -1;
            if (_motor.IsDodging) return null;

            int s = _input.SkillPressed;
            if (s >= 0 && s < skills.Length)
            {
                if (_cooldowns[s] > 0f) return null;
                skillIndex = s;
                return skills[s];
            }
            if (_input.HeavyPressed && _motor.IsGrounded && _motor.Stamina >= heavy.staminaCost) return heavy;
            if (_input.AttackPressed)
            {
                if (_motor.IsGrounded) return basicCombo[_comboIndex % basicCombo.Length];
                return _airAttacksUsed < maxAirAttacks ? airSlash : null;
            }
            return null;
        }

        void HandleRequest(AttackData request, int skillIndex)
        {
            if (_staggerTimer > 0f) return;

            if (_current == null)
            {
                Begin(request, skillIndex, _graceTimer > 0f ? ChainResult.Good : ChainResult.Fresh);
                return;
            }

            if (_broken) return;

            if (_attackTime < _current.PerfectStart)
            {
                // Too early: mashing breaks the combo.
                _broken = true;
                Feedback?.Invoke("TOO EARLY", BrokenColor);
                if (_audio) _audio.PlayBroken();
                return;
            }

            Begin(request, skillIndex, _attackTime <= _current.PerfectEnd ? ChainResult.Perfect : ChainResult.Good);
        }

        // ---------- Attacks ----------

        void Begin(AttackData a, int skillIndex, ChainResult result)
        {
            if (_motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
            bool hadPrevious = _current != null || _graceTimer > 0f;
            AttackKind prevKind = _current != null ? _current.kind : _previousKind;
            bool prevLanded = _current != null ? _landedCurrent : _landedPrevious;

            if (result == ChainResult.Fresh || !hadPrevious) { Chain = 0; ComboHits = 0; _comboIndex = 0; }
            else Chain++;

            _bonus = 1f;
            bool perfect = result == ChainResult.Perfect;
            bool isLink = perfect && prevKind == AttackKind.Basic && a.kind != AttackKind.Basic;
            bool isExtend = result != ChainResult.Fresh && prevKind != AttackKind.Basic && a.kind == AttackKind.Basic;

            if (perfect && prevLanded)
            {
                _bonus += isLink ? linkDamageBonus : perfectDamageBonus;
                Focus = Mathf.Min(maxFocus, Focus + (isLink ? focusPerLink : focusPerPerfect));
            }

            if (isLink) { Feedback?.Invoke("LINK!", LinkColor); if (_audio) _audio.PlayLink(); }
            else if (isExtend) { Feedback?.Invoke(perfect ? "PERFECT EXTEND!" : "EXTEND", LinkColor); if (_audio) { if (perfect) _audio.PlayLink(); } }
            else if (perfect) { Feedback?.Invoke("PERFECT", PerfectColor); if (_audio) _audio.PlayPerfect(); }

            if (a.kind == AttackKind.Heavy)
            {
                _motor.TrySpendStamina(a.staminaCost);
                if (FocusFull)
                {
                    _bonus *= sealbreakerMultiplier;
                    Focus = 0f;
                    Feedback?.Invoke("SEALBREAKER!", PerfectColor);
                }
            }

            if (skillIndex >= 0) _cooldowns[skillIndex] = a.cooldown * (isLink ? 1f - linkCooldownRefund : 1f);

            if (a.kind == AttackKind.Basic && a != airSlash) _comboIndex = (_comboIndex + 1) % basicCombo.Length;
            else if (a.kind != AttackKind.Basic) _comboIndex = 0; // basics after a skill start a fresh string (extender)
            if (a == airSlash) _airAttacksUsed++;

            if (_current != null) _previousKind = _current.kind;
            _current = a;
            _attackTime = perfect ? a.windup * perfectWindupSkip : 0f;
            _broken = false;
            _landedCurrent = false;
            _cueShown = false;
            _lastSubHit = -1;
            _graceTimer = 0f;
            _sinceCombat = 0f;
            _hitThisSwing.Clear();

            _motor.FaceDirection(FindAimDirection());
            _motor.RotationLocked = true;
            _motor.MoveSpeedMultiplier = steerWhileAttacking;
            if (a.hover > 0f) _motor.Hover(a.hover);
            if (_audio) _audio.PlaySwing();
        }

        void UpdateAttack(float dt)
        {
            var a = _current;
            float prev = _attackTime;
            _attackTime += dt;

            if (prev < a.windup && _attackTime >= a.windup)
            {
                if (a.lungeSpeed > 0f) _motor.Lunge(transform.forward, a.lungeSpeed);
                if (a.launchSpeed > 0f) _motor.Launch(a.launchSpeed);
            }

            // The beat: flash the blade and tick when the perfect window opens.
            if (!_cueShown && !_broken && _attackTime >= a.PerfectStart)
            {
                _cueShown = true;
                _cueFlash = 1f;
                if (_audio) _audio.PlayTick();
            }

            bool active = _attackTime >= a.windup && _attackTime <= a.windup + a.active;
            if (_trail != null) _trail.emitting = active;
            if (active)
            {
                int sub = Mathf.Min(a.hits - 1, Mathf.FloorToInt((_attackTime - a.windup) / (a.active / a.hits)));
                if (sub != _lastSubHit) { _lastSubHit = sub; _hitThisSwing.Clear(); }
                DetectHits(a);
            }

            if (_attackTime >= a.Total) FinishAttack();
        }

        void FinishAttack()
        {
            bool broken = _broken;
            _previousKind = _current.kind;
            _landedPrevious = _landedCurrent;
            ReleaseMotor();
            _current = null;

            if (broken)
            {
                _staggerTimer = brokenStagger;
                EndChain();
            }
            else
            {
                _graceTimer = chainGrace;
            }
        }

        void EndChain()
        {
            Chain = 0;
            _comboIndex = 0;
            _graceTimer = 0f;
        }

        void OnDodgeStarted()
        {
            if (_current == null) return;
            // Dodging inside the perfect window keeps the chain alive (an "evade cancel").
            bool inWindow = !_broken && _attackTime >= _current.PerfectStart && _attackTime <= _current.PerfectEnd;
            _previousKind = _current.kind;
            _landedPrevious = _landedCurrent;
            ReleaseMotor();
            _current = null;
            if (inWindow)
            {
                _graceTimer = _motor.dodgeDuration + 0.25f;
                Feedback?.Invoke("EVADE", LinkColor);
                if (_audio) _audio.PlayPerfect();
            }
            else EndChain();
        }

        void ReleaseMotor()
        {
            _motor.RotationLocked = false;
            _motor.MoveSpeedMultiplier = 1f;
            if (_trail != null) _trail.emitting = false;
            if (_motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
        }

        void DetectHits(AttackData a)
        {
            Vector3 center = transform.position + Vector3.up * 1f;
            int count = Physics.OverlapSphereNonAlloc(center, a.range, _overlap, ~0, QueryTriggerInteraction.Collide);
            bool landed = false;
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h == _ownHealth || _hitThisSwing.Contains(h)) continue;

                Vector3 closest = _overlap[i].ClosestPoint(center);
                Vector3 to = closest - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > a.arcDegrees * 0.5f) continue;

                _hitThisSwing.Add(h);
                var info = new DamageInfo
                {
                    Amount = a.damage * _bonus * ChainMultiplier,
                    Point = closest,
                    Direction = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward,
                    Source = gameObject,
                    Impact = Mathf.Clamp01(a.impact * (_bonus > 1.5f ? 1.3f : 1f))
                };
                if (h.TryDamage(info, out _)) landed = true;
            }

            if (landed)
            {
                _landedCurrent = true;
                ComboHits++;
                _sinceCombat = 0f;
                HitStop.Trigger(a.hitStop * (_bonus > 1f ? 1.3f : 1f));
                ThirdPersonCamera.Shake(a.shake * (_bonus > 1f ? 1.3f : 1f));
                if (_audio) _audio.PlayHit(a.impact > 0.6f);
            }
        }

        Vector3 FindAimDirection()
        {
            Vector3 wish = _motor.MoveInputWorld;
            Vector3 baseDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;

            Health best = null;
            float bestScore = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, aimAssistRange, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h == _ownHealth || h.IsDead) continue;
                Vector3 to = h.transform.position - transform.position; to.y = 0f;
                float angle = Vector3.Angle(baseDir, to);
                if (angle > aimAssistAngle) continue;
                float score = to.magnitude + angle * 0.05f;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            if (best == null) return baseDir;
            Vector3 dir = best.transform.position - transform.position; dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : baseDir;
        }

        // ---------- Visuals ----------

        void UpdateSwordPose(float dt)
        {
            if (swordPivot == null) return;
            if (_current == null)
            {
                swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, Quaternion.Euler(restEuler), 1f - Mathf.Exp(-18f * dt));
                return;
            }

            var a = _current;
            Quaternion from = Quaternion.Euler(a.swingFrom);
            Quaternion to = Quaternion.Euler(a.swingTo);
            if (_attackTime < a.windup)
            {
                float t = _attackTime / Mathf.Max(0.0001f, a.windup);
                swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, from, 1f - Mathf.Pow(1f - t, 3f));
            }
            else if (_attackTime < a.windup + a.active)
            {
                float t = (_attackTime - a.windup) / Mathf.Max(0.0001f, a.active);
                swordPivot.localRotation = Quaternion.Slerp(from, to, 1f - Mathf.Pow(1f - t, 2f));
                if (a.spin && _motor.visual != null)
                    _motor.visual.localRotation = Quaternion.Euler(0f, 360f * a.hits * t, 0f);
            }
            else
            {
                swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, to, 0.5f);
                if (a.spin && _motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
            }
        }

        void UpdateCue(float dt)
        {
            if (_cueMaterial == null) return;
            _cueFlash = Mathf.MoveTowards(_cueFlash, 0f, dt * 5f);
            Color glow = FocusFull ? PerfectColor : _cueBase;
            _cueMaterial.color = Color.Lerp(glow, Color.white, _cueFlash);
            _cueMaterial.SetColor("_EmissionColor", Color.Lerp(glow * 1.5f, Color.white * 6f, _cueFlash));
        }
    }
}
