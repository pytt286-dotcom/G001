using System;
using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// Rhythm-based combat shared by every class.
    ///
    /// Each attack has a short "perfect window" near the end of its swing or cast, marked by a flash
    /// on the weapon and a tick sound. Pressing in the window chains faster and hits harder. Pressing
    /// too early (mashing) breaks the combo and leaves you open. Skills used in the window are LINKS,
    /// and basic attacks timed after a skill are EXTENDERS. Tab switches between Sword and Mage.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInputReader))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Classes")]
        public CombatKit swordKit = Kits.Sword();
        public CombatKit mageKit = Kits.Mage();

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
        public float rangedAimRange = 24f;
        public float rangedAimAngle = 35f;
        [Range(0f, 1f)] public float steerWhileAttacking = 0.15f;
        public int maxAirAttacks = 2;

        [Header("Weapons")]
        public Transform swordPivot;
        public Transform staffPivot;
        public Renderer swordCueRenderer;
        public Renderer staffCueRenderer;
        [Tooltip("The sword's beat cue is the whole weapon model (real art), not a glowing strip.")]
        public bool swordCueIsWeapon;
        public bool staffCueIsWeapon;
        public Vector3 restEuler = new Vector3(35f, 25f, 0f);

        // ---- read by the HUD ----
        public CombatKit Kit => _kit;
        public AttackData[] skills => _kit.skills;
        public bool IsAttacking => _current != null;
        public int Chain { get; private set; }
        public int ComboHits { get; private set; }
        public float ChainMultiplier => 1f + Mathf.Min(Chain, maxChainSteps) * chainDamagePerStep;
        public float Focus { get; private set; }
        public bool FocusFull => Focus >= maxFocus;
        public float CooldownRemaining(int i) => i >= 0 && i < _cooldowns.Length ? _cooldowns[i] : 0f;
        public float CooldownTotal(int i) => i >= 0 && i < _kit.skills.Length ? _kit.skills[i].cooldown : 1f;
        /// <summary>Text popups for the HUD: "PERFECT", "LINK", "TOO EARLY"...</summary>
        public event Action<string, Color> Feedback;
        /// <summary>An attack landed (damage dealt). The Soul Gauge fills from this.</summary>
        public event Action<float> HitLanded;
        /// <summary>An enemy attack passed through a dodge.</summary>
        public event Action Dodged;
        /// <summary>Extra damage from a soul merge (1 = none).</summary>
        public float DamageBoost { get; set; } = 1f;
        /// <summary>Hit-stop and camera shake on hits. Off for bots, so their fights do not shake your screen.</summary>
        public bool feelEffects = true;
        /// <summary>Damage dealt so far (read by the play check and the squad).</summary>
        public float DamageDealt { get; private set; }

        /// <summary>Shows a popup on the HUD (used by the soul merge).</summary>
        public void Announce(string text, Color colour) => Feedback?.Invoke(text, colour);

        static readonly Color PerfectColor = new Color(1f, 0.85f, 0.3f);
        static readonly Color LinkColor = new Color(0.45f, 0.85f, 1f);
        static readonly Color BrokenColor = new Color(1f, 0.35f, 0.3f);

        PlayerMotor _motor;
        PlayerInputReader _input;
        CombatAudio _audio;
        CombatKit _kit;
        Transform _weapon;
        TrailRenderer _trail;
        Material _cueMaterial;
        Color _cueBase;
        bool _cueWhole;
        float _cueFlash;

        AttackData _current;
        AttackKind _previousKind;
        float _attackTime;
        float _bonus = 1f;
        bool _broken, _landedCurrent, _landedPrevious, _cueShown, _released;
        int _comboIndex, _airAttacksUsed, _lastSubHit;
        float _graceTimer, _staggerTimer, _sinceCombat = 10f;
        float[] _cooldowns = new float[3];
        readonly Dictionary<CombatKit, float[]> _kitCooldowns = new Dictionary<CombatKit, float[]>();
        readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
        readonly Collider[] _overlap = new Collider[48];

        enum ChainResult { Fresh, Good, Perfect }

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _audio = GetComponent<CombatAudio>();
            _motor.DodgeStarted += OnDodgeStarted;
            _kit = swordKit;
        }

        void Start() => EquipKit(swordKit, false);

        // ---------- Forged weapons ----------

        /// <summary>The forged weapon each class holds ("" = plain).</summary>
        string SwordWeapon => Sealbound.Loot.Inventory.EquippedWeapon(true);
        string StaffWeapon => Sealbound.Loot.Inventory.EquippedWeapon(false);
        bool RunestoneBlade => _kit == swordKit && SwordWeapon == Sealbound.Loot.ItemCatalog.RunestoneBlade;
        bool CoreStaff => _kit == mageKit && StaffWeapon == Sealbound.Loot.ItemCatalog.GolemCoreStaff;
        /// <summary>Forged weapons hit 15% harder.</summary>
        float WeaponDamage => (RunestoneBlade || CoreStaff ? 1.15f : 1f) * DamageBoost;

        /// <summary>The weapon model changed: pick up its trail and beat flash again.</summary>
        public void RefreshWeapon() => EquipKit(_kit, false);

        void EquipKit(CombatKit kit, bool announce)
        {
            if (_current != null) Interrupt();
            if (_kit != null) _kitCooldowns[_kit] = _cooldowns;
            _kit = kit;
            if (!_kitCooldowns.TryGetValue(kit, out _cooldowns)) _cooldowns = new float[kit.skills.Length];

            bool sword = kit == swordKit;
            if (swordPivot != null) swordPivot.gameObject.SetActive(sword);
            if (staffPivot != null) staffPivot.gameObject.SetActive(!sword);
            _weapon = sword ? swordPivot : staffPivot;
            _trail = _weapon != null ? _weapon.GetComponentInChildren<TrailRenderer>() : null;
            if (_trail != null) _trail.emitting = false;
            if (_weapon != null) _weapon.localRotation = Quaternion.Euler(restEuler);

            var cue = sword ? swordCueRenderer : staffCueRenderer;
            _cueWhole = sword ? swordCueIsWeapon : staffCueIsWeapon;
            _cueMaterial = null;
            if (cue != null)
            {
                _cueMaterial = cue.material;
                _cueBase = _cueMaterial.color;
                _cueMaterial.EnableKeyword("_EMISSION");
            }
            if (announce) Feedback?.Invoke(kit.className.ToUpperInvariant(), LinkColor);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
            foreach (var kv in _kitCooldowns)
                if (kv.Value != _cooldowns)
                    for (int i = 0; i < kv.Value.Length; i++) kv.Value[i] = Mathf.Max(0f, kv.Value[i] - dt);

            if (_motor.IsGrounded) _airAttacksUsed = 0;
            if (_staggerTimer > 0f) _staggerTimer -= dt;

            _sinceCombat += dt;
            if (_sinceCombat > 4f) Focus = Mathf.Max(0f, Focus - 8f * dt);

            if (_input.SwitchClassPressed && !_motor.IsStunned)
                EquipKit(_kit == swordKit ? mageKit : swordKit, true);

            if (_current != null) UpdateAttack(dt);
            else if (_graceTimer > 0f)
            {
                _graceTimer -= dt;
                if (_graceTimer <= 0f) EndChain();
            }

            var request = ReadRequest(out int skillIndex);
            if (request != null) HandleRequest(request, skillIndex);

            UpdateWeaponPose(dt);
            UpdateCue(dt);
        }

        // ---------- Input ----------

        AttackData ReadRequest(out int skillIndex)
        {
            skillIndex = -1;
            if (_motor.IsDodging || _motor.IsStunned) return null;

            int s = _input.SkillPressed;
            if (s >= 0 && s < _kit.skills.Length)
            {
                if (_cooldowns[s] > 0f) return null;
                skillIndex = s;
                return _kit.skills[s];
            }
            if (_input.HeavyPressed && _motor.IsGrounded && _motor.Stamina >= _kit.heavy.staminaCost) return _kit.heavy;
            if (_input.AttackPressed)
            {
                if (_motor.IsGrounded) return _kit.basicCombo[_comboIndex % _kit.basicCombo.Length];
                return _airAttacksUsed < maxAirAttacks ? _kit.airAttack : null;
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
            else if (isExtend) { Feedback?.Invoke(perfect ? "PERFECT EXTEND!" : "EXTEND", LinkColor); if (_audio && perfect) _audio.PlayLink(); }
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

            bool isAir = a == _kit.airAttack;
            if (a.kind == AttackKind.Basic && !isAir) _comboIndex = (_comboIndex + 1) % _kit.basicCombo.Length;
            else if (a.kind != AttackKind.Basic) _comboIndex = 0; // basics after a skill start a fresh string (extender)
            if (isAir) _airAttacksUsed++;

            if (_current != null) _previousKind = _current.kind;
            _current = a;
            _attackTime = perfect ? a.windup * perfectWindupSkip : 0f;
            _broken = false;
            _landedCurrent = false;
            _cueShown = false;
            _released = false;
            _lastSubHit = -1;
            _graceTimer = 0f;
            _sinceCombat = 0f;
            _hitThisSwing.Clear();

            _motor.FaceDirection(AimDirection(a, out _));
            _motor.RotationLocked = true;
            _motor.MoveSpeedMultiplier = steerWhileAttacking;
            if (a.hover > 0f) _motor.Hover(a.hover);
            if (_audio) _audio.PlaySwing();
        }

        void UpdateAttack(float dt)
        {
            var a = _current;
            _attackTime += dt;

            if (!_released && _attackTime >= a.windup)
            {
                _released = true;
                Release(a);
            }

            // The beat: flash the weapon and tick when the perfect window opens.
            if (!_cueShown && !_broken && _attackTime >= a.PerfectStart)
            {
                _cueShown = true;
                _cueFlash = 1f;
                if (_audio) _audio.PlayTick();
            }

            bool active = _attackTime >= a.windup && _attackTime <= a.windup + a.active;
            if (_trail != null) _trail.emitting = active && a.delivery == Delivery.Melee;
            if (active && a.delivery == Delivery.Melee)
            {
                int sub = Mathf.Min(a.hits - 1, Mathf.FloorToInt((_attackTime - a.windup) / (a.active / a.hits)));
                if (sub != _lastSubHit) { _lastSubHit = sub; _hitThisSwing.Clear(); }
                DetectMeleeHits(a);
            }

            if (_current != null && _attackTime >= a.Total) FinishAttack();
        }

        /// <summary>The moment the swing starts or the spell leaves the hand.</summary>
        void Release(AttackData a)
        {
            float power = _bonus * ChainMultiplier;
            Vector3 dir = AimDirection(a, out Health target);
            Vector3 hand = transform.position + Vector3.up * 1.3f + transform.forward * 0.6f;

            switch (a.delivery)
            {
                case Delivery.Melee:
                    if (a.lungeSpeed > 0f) _motor.Lunge(transform.forward, a.lungeSpeed);
                    if (a.launchSpeed > 0f) _motor.Launch(a.launchSpeed);
                    break;

                case Delivery.Bolt:
                {
                    // Ranged hit-confirm: a cast at a locked target counts as landing for the rhythm bonus.
                    if (target != null) _landedCurrent = true;
                    Vector3 aim = target != null
                        ? (target.AimPosition - hand).normalized
                        : transform.forward;
                    if (a == _kit.airAttack) aim = (aim + Vector3.down * 0.6f).normalized;
                    for (int i = 0; i < a.projectileCount; i++)
                    {
                        Vector3 side = transform.right * (a.projectileCount > 1 ? (i - (a.projectileCount - 1) * 0.5f) * 0.6f : 0f);
                        var bolt = PlayerBolt.Fire(hand + side, aim, target != null ? (target.aimPoint != null ? target.aimPoint : target.transform) : null, a.projectileSpeed, a.projectileRadius,
                            a.damage * power * WeaponDamage, a.impact, a.pierce, a.fxColor, gameObject, (landed, impact) => OnSpellHit(a, landed));
                        if (CoreStaff && bolt != null) { bolt.splashRadius = 2.5f; bolt.splashFraction = 0.45f; } // core fire bursts on impact
                    }
                    if (a.kind == AttackKind.Heavy) _motor.Lunge(-transform.forward, 6f); // recoil
                    break;
                }

                case Delivery.Area:
                {
                    if (target != null) _landedCurrent = true;
                    Vector3 at = target != null && Vector3.Distance(target.transform.position, transform.position) <= a.castDistance
                        ? target.transform.position
                        : transform.position + transform.forward * a.castDistance;
                    at.y = transform.position.y;
                    AreaBlast.Cast(at, a.areaRadius, a.areaDelay, a.damage * power, a.impact, a.launchSpeed > 0f ? a.launchSpeed : 0f,
                        a.fxColor, gameObject, (landed, impact) => OnSpellHit(a, landed));
                    break;
                }

                case Delivery.Trap:
                {
                    Vector3 at = transform.position + transform.forward * a.castDistance;
                    FrostSnare.Place(at, a.areaRadius, a.damage * power, a.slow, a.slowSeconds, gameObject);
                    _landedCurrent = true; // setting a trap keeps the rhythm going
                    break;
                }

                case Delivery.Blink:
                {
                    // Leave a burst of arcane fire behind and dash forward, untouchable.
                    AreaBlast.Cast(transform.position, a.areaRadius, 0.05f, a.damage * power, a.impact, 0f, a.fxColor, gameObject,
                        (landed, impact) => OnSpellHit(a, landed));
                    Vector3 wish = _motor.MoveInputWorld;
                    _motor.Dash(wish.sqrMagnitude > 0.01f ? wish : transform.forward, a.castDistance, a.active);
                    _landedCurrent = true;
                    break;
                }
            }
        }

        void OnSpellHit(AttackData a, bool landed)
        {
            if (!landed) return;
            if (_current == a) _landedCurrent = true;
            else _landedPrevious = true;
            ComboHits++;
            _sinceCombat = 0f;
            HitLanded?.Invoke(a.damage * _bonus);
            DamageDealt += a.damage * _bonus;
            if (feelEffects) { HitStop.Trigger(a.hitStop); ThirdPersonCamera.Shake(a.shake); }
            if (_audio) _audio.PlayHit(a.impact > 0.6f);
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

        /// <summary>Called when the player is hit: the current attack and chain are lost.</summary>
        public void Interrupt()
        {
            if (_current != null)
            {
                _previousKind = _current.kind;
                ReleaseMotor();
                _current = null;
            }
            EndChain();
        }

        /// <summary>Called when an enemy attack passes through the player's dodge.</summary>
        public void NotifyDodged()
        {
            Focus = Mathf.Min(maxFocus, Focus + 8f);
            _sinceCombat = 0f;
            Dodged?.Invoke();
            Feedback?.Invoke("DODGED", LinkColor);
            if (_audio) _audio.PlayPerfect();
        }

        void ReleaseMotor()
        {
            _motor.RotationLocked = false;
            _motor.MoveSpeedMultiplier = 1f;
            if (_trail != null) _trail.emitting = false;
            if (_motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
        }

        void DetectMeleeHits(AttackData a)
        {
            Vector3 center = transform.position + Vector3.up * 1f;
            int count = Physics.OverlapSphereNonAlloc(center, a.range, _overlap, ~0, QueryTriggerInteraction.Collide);
            bool landed = false;
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h.gameObject == gameObject || _hitThisSwing.Contains(h) || SpellHit.IsSealer(h)) continue; // no friendly fire

                Vector3 closest = _overlap[i].ClosestPoint(center);
                Vector3 to = closest - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > a.arcDegrees * 0.5f) continue;

                _hitThisSwing.Add(h);
                var info = new DamageInfo
                {
                    // The Runestone Blade cracks armour: against armoured foes it makes up much of what armour takes off.
                    Amount = a.damage * _bonus * ChainMultiplier * WeaponDamage * (RunestoneBlade ? 1f + 0.9f * (1f - Mathf.Clamp01(h.damageMultiplier)) : 1f),
                    Point = closest,
                    Direction = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward,
                    Source = gameObject,
                    Impact = Mathf.Clamp01(a.impact * (_bonus > 1.5f ? 1.3f : 1f)),
                    Launch = a.launchSpeed > 0f ? 10f : (a == _kit.airAttack ? 4.5f : 0f)
                };
                if (h.TryDamage(info, out float dealt)) { landed = true; DamageDealt += dealt; HitLanded?.Invoke(dealt); }
            }

            if (landed)
            {
                _landedCurrent = true;
                ComboHits++;
                _sinceCombat = 0f;
                if (feelEffects)
                {
                    HitStop.Trigger(a.hitStop * (_bonus > 1f ? 1.3f : 1f));
                    ThirdPersonCamera.Shake(a.shake * (_bonus > 1f ? 1.3f : 1f));
                }
                if (_audio) _audio.PlayHit(a.impact > 0.6f);
            }
        }

        Vector3 AimDirection(AttackData a, out Health target)
        {
            bool ranged = a.IsRanged;
            target = FindTarget(ranged ? rangedAimRange : aimAssistRange, ranged ? rangedAimAngle : aimAssistAngle, ranged);
            Vector3 wish = _motor.MoveInputWorld;
            Vector3 baseDir = ranged ? CameraForward() : (wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward);
            if (target == null) return baseDir;
            Vector3 dir = target.transform.position - transform.position; dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : baseDir;
        }

        Health FindTarget(float range, float maxAngle, bool ranged)
        {
            Vector3 wish = _motor.MoveInputWorld;
            Vector3 baseDir = ranged ? CameraForward() : (wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward);
            Health best = null;
            float bestScore = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var h = _overlap[i].GetComponentInParent<Health>();
                if (h == null || h.gameObject == gameObject || h.IsDead || SpellHit.IsSealer(h)) continue;
                Vector3 to = h.transform.position - transform.position; to.y = 0f;
                float angle = Vector3.Angle(baseDir, to);
                if (angle > maxAngle) continue;
                float score = to.magnitude + angle * (ranged ? 0.4f : 0.05f);
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        Vector3 CameraForward()
        {
            var cam = _motor.cameraTransform;
            Vector3 f = cam != null ? cam.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.01f ? f.normalized : transform.forward;
        }

        // ---------- Visuals ----------

        void UpdateWeaponPose(float dt)
        {
            if (_weapon == null) return;
            if (_current == null)
            {
                _weapon.localRotation = Quaternion.Slerp(_weapon.localRotation, Quaternion.Euler(restEuler), 1f - Mathf.Exp(-18f * dt));
                return;
            }

            var a = _current;
            Quaternion from = Quaternion.Euler(a.swingFrom);
            Quaternion to = Quaternion.Euler(a.swingTo);
            if (_attackTime < a.windup)
            {
                float t = _attackTime / Mathf.Max(0.0001f, a.windup);
                _weapon.localRotation = Quaternion.Slerp(_weapon.localRotation, from, 1f - Mathf.Pow(1f - t, 3f));
            }
            else if (_attackTime < a.windup + a.active)
            {
                float t = (_attackTime - a.windup) / Mathf.Max(0.0001f, a.active);
                _weapon.localRotation = Quaternion.Slerp(from, to, 1f - Mathf.Pow(1f - t, 2f));
                if (a.spin && _motor.visual != null)
                    _motor.visual.localRotation = Quaternion.Euler(0f, 360f * a.hits * t, 0f);
            }
            else
            {
                _weapon.localRotation = Quaternion.Slerp(_weapon.localRotation, to, 0.5f);
                if (a.spin && _motor.visual != null) _motor.visual.localRotation = Quaternion.identity;
            }
        }

        void UpdateCue(float dt)
        {
            if (_cueMaterial == null) return;
            _cueFlash = Mathf.MoveTowards(_cueFlash, 0f, dt * 5f);
            if (_cueWhole)
            {
                // A painted weapon: keep its colours, flash white on the beat and glow gold at full Focus.
                _cueMaterial.color = Color.Lerp(FocusFull ? Color.Lerp(_cueBase, PerfectColor, 0.35f) : _cueBase, Color.white, _cueFlash);
                _cueMaterial.SetColor("_EmissionColor", Color.Lerp(FocusFull ? PerfectColor * 0.8f : Color.black, Color.white * 2.5f, _cueFlash));
                return;
            }
            Color glow = FocusFull ? PerfectColor : _cueBase;
            _cueMaterial.color = Color.Lerp(glow, Color.white, _cueFlash);
            _cueMaterial.SetColor("_EmissionColor", Color.Lerp(glow * 0.45f, Color.white * 1.6f, _cueFlash)); // strong enough to read, not a blinding blob
        }
    }
}
