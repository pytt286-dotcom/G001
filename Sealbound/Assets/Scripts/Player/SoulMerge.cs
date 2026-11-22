using System.Collections.Generic;
using Sealbound.CameraRig;
using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Loot;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// Soul merge, the signature feature. With a spirit bound at the soul shrine, a Soul Gauge fills as
    /// you fight (landing hits, dodging, taking hits). When it is full, V (R3) merges you with the creature
    /// for a while: you take on its look, its strength and its signature skill.
    ///
    /// Golem merge: stone skin and glowing runes, a heavier, slower body, much tougher (more health,
    /// less damage taken), a little stronger, Fortress Stance (hits never knock you back or break your
    /// attack) and Rune Quake (V again: slam the ground, damage and throw up everything nearby).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerCombat), typeof(PlayerVitals))]
    public class SoulMerge : MonoBehaviour
    {
        [Header("Soul Gauge")]
        public float maxGauge = 100f;
        public float gaugePerHit = 3f;
        public float gaugePerDamage = 0.04f;
        public float maxGaugePerHit = 8f;
        public float gaugePerDodge = 6f;
        public float gaugePerHitTaken = 3f;

        [Header("Golem merge")]
        public float duration = 40f;
        public float healthBoost = 1.5f;      // max health x1.5
        public float damageTaken = 0.6f;      // takes 40% less damage
        public float damageBoost = 1.1f;      // hits 10% harder
        public float speedScale = 0.85f;      // heavier, slower
        public float visualScale = 1.12f;

        [Header("Rune Quake")]
        public float quakeCooldown = 6f;
        public float quakeRadius = 6f;
        public float quakeDamage = 45f;
        public float quakeLaunch = 9f;

        // ---- read by the HUD ----
        public float Gauge { get; private set; }
        public bool GaugeFull => Gauge >= maxGauge;
        public bool HasSpirit => Inventory.BoundSpirit != "";
        public bool Merged { get; private set; }
        public float TimeLeft { get; private set; }
        public float QuakeCooldownLeft { get; private set; }
        public string SpiritName { get { var d = ItemCatalog.Get(Inventory.BoundSpirit); return d != null ? d.displayName : ""; } }

        static readonly Color RuneBlue = new Color(0.45f, 0.75f, 1f);
        static readonly Color StoneDust = new Color(0.8f, 0.72f, 0.6f);

        PlayerMotor _motor;
        PlayerCombat _combat;
        PlayerVitals _vitals;
        PlayerInputReader _input;
        Health _health;
        float _baseMaxHealth, _baseDamageMultiplier;
        float _pulse;

        readonly List<Renderer> _skin = new List<Renderer>();
        readonly List<Material[]> _originals = new List<Material[]>();
        readonly List<Glow> _runes = new List<Glow>();
        Glow _ring;
        Vector3 _baseScale = Vector3.one;
        static Material _stone;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _vitals = GetComponent<PlayerVitals>();
            _input = GetComponent<PlayerInputReader>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            _combat.HitLanded += OnHitLanded;
            _combat.Dodged += OnDodged;
            _health.Damaged += OnHurt;
            _vitals.Fell += OnFell;
            _vitals.Unstoppable = () => Merged;
        }

        void OnDisable()
        {
            _combat.HitLanded -= OnHitLanded;
            _combat.Dodged -= OnDodged;
            _health.Damaged -= OnHurt;
            _vitals.Fell -= OnFell;
            if (Merged) EndMerge(false);
        }

        // ---------- Filling the gauge ----------

        void AddGauge(float amount)
        {
            if (!HasSpirit || Merged) return;
            bool wasFull = GaugeFull;
            Gauge = Mathf.Min(maxGauge, Gauge + amount);
            if (!wasFull && GaugeFull) _combat.Announce("SOUL GAUGE FULL: V", RuneBlue);
        }

        void OnHitLanded(float damage) => AddGauge(Mathf.Min(maxGaugePerHit, gaugePerHit + damage * gaugePerDamage));
        void OnDodged() => AddGauge(gaugePerDodge);
        void OnHurt(DamageInfo info, float dealt) { if (dealt > 0f) AddGauge(gaugePerHitTaken); }
        void OnFell() { if (Merged) EndMerge(false); }

        /// <summary>Fills the gauge at once (a dev and testing helper).</summary>
        public void FillGauge() { if (HasSpirit) Gauge = maxGauge; }

        // ---------- Merging ----------

        void Update()
        {
            float dt = Time.deltaTime;
            QuakeCooldownLeft = Mathf.Max(0f, QuakeCooldownLeft - dt);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 2.5f);

            if (_input != null && _input.MergePressed && !_vitals.IsDown)
            {
                if (Merged) TryRuneQuake();
                else TryMerge();
            }

            if (!Merged) return;
            TimeLeft -= dt;
            Gauge = maxGauge * Mathf.Clamp01(TimeLeft / duration);
            float breathe = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f) + _pulse;
            foreach (var g in _runes) if (g != null) g.SetStrength(breathe);
            if (_ring != null) _ring.SetStrength(0.35f + 0.15f * Mathf.Sin(Time.time * 2f) + _pulse);
            if (_stone != null && _stone.HasProperty("_EmissionColor"))
                _stone.SetColor("_EmissionColor", RuneBlue * (0.06f + 0.05f * Mathf.Sin(Time.time * 3f) + _pulse * 0.6f));
            if (TimeLeft <= 0f) EndMerge(true);
        }

        /// <summary>Merges with the bound spirit if the gauge is full. Returns true if it happened.</summary>
        public bool TryMerge()
        {
            if (Merged || !HasSpirit) return false;
            if (!GaugeFull)
            {
                _combat.Announce("SOUL GAUGE NOT FULL", new Color(0.7f, 0.75f, 0.85f));
                return false;
            }
            Merged = true;
            TimeLeft = duration;
            _pulse = 1f;

            // Stats: a tougher, heavier, slightly stronger body.
            _baseMaxHealth = _health.maxHealth;
            _baseDamageMultiplier = _health.damageMultiplier;
            _health.SetMaxHealth(_baseMaxHealth * healthBoost);
            _health.Heal(_baseMaxHealth * (healthBoost - 1f));
            _health.damageMultiplier = _baseDamageMultiplier * damageTaken;
            _combat.DamageBoost = damageBoost;
            _motor.SpeedScale = speedScale;

            TurnToStone(true);
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            SealBurst.Spawn(chest, 3.2f, RuneBlue);
            SealBurst.Spawn(transform.position + Vector3.up * 0.3f, 2.4f, StoneDust);
            HitStop.Trigger(0.12f);
            ThirdPersonCamera.Shake(0.7f);
            _combat.Announce("SOUL MERGE: GOLEM", RuneBlue);
            return true;
        }

        /// <summary>Ends the merge early or when time runs out.</summary>
        public void EndMerge(bool announce)
        {
            if (!Merged) return;
            Merged = false;
            TimeLeft = 0f;
            Gauge = 0f;
            _health.damageMultiplier = _baseDamageMultiplier;
            _health.SetMaxHealth(_baseMaxHealth);
            _combat.DamageBoost = 1f;
            _motor.SpeedScale = 1f;
            TurnToStone(false);
            SealBurst.Spawn(transform.position + Vector3.up * 1f, 2f, StoneDust);
            if (announce) _combat.Announce("THE STONE FALLS AWAY", StoneDust);
        }

        /// <summary>Rune Quake: slam the ground, hurting and throwing up everything close by.</summary>
        public bool TryRuneQuake()
        {
            if (!Merged || QuakeCooldownLeft > 0f || _motor.IsStunned || _motor.IsDodging) return false;
            QuakeCooldownLeft = quakeCooldown;
            _pulse = 1f;
            _combat.Interrupt();
            _motor.Stagger(Vector3.zero, 0f, 0.35f); // planted for the slam
            Vector3 at = transform.position;
            AreaBlast.Cast(at, quakeRadius, 0.12f, quakeDamage * damageBoost, 1f, quakeLaunch, RuneBlue, gameObject, OnQuakeHit);
            SealBurst.Spawn(at + Vector3.up * 0.2f, quakeRadius * 0.8f, StoneDust);
            HitStop.Trigger(0.08f);
            ThirdPersonCamera.Shake(0.9f);
            _combat.Announce("RUNE QUAKE!", RuneBlue);
            return true;
        }

        void OnQuakeHit(bool landed, float impact)
        {
            if (landed) ThirdPersonCamera.Shake(0.4f);
        }

        // ---------- The stone look ----------

        void TurnToStone(bool on)
        {
            var visual = _motor.visual;
            if (visual == null) return;
            if (on)
            {
                _baseScale = visual.localScale;
                visual.localScale = _baseScale * visualScale;
                var stone = StoneMaterial();
                _skin.Clear();
                _originals.Clear();
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                    if (r.GetComponent<Glow>() != null || SkinnedOutline.IsOutline(r)) continue;
                    if (IsUnder(r.transform, _combat.swordPivot) || IsUnder(r.transform, _combat.staffPivot)) continue; // the weapon keeps its look
                    var mats = r.sharedMaterials;
                    _skin.Add(r);
                    _originals.Add(mats);
                    var stoneMats = new Material[mats.Length];
                    for (int i = 0; i < stoneMats.Length; i++) stoneMats[i] = stone;
                    r.sharedMaterials = stoneMats;
                }
                // Runes glowing through the stone, and a rune circle under the feet.
                _runes.Clear();
                // Runes glowing through the stone, pinned to the nearest bone so they move with the body.
                Color rune = new Color(0.2f, 0.65f, 2.4f, 1f);
                AddRune(visual, new Vector3(0f, 1.28f, 0.2f), 0.42f, rune);    // the heart rune, front
                AddRune(visual, new Vector3(0f, 1.3f, -0.2f), 0.42f, rune);    // and through the back
                AddRune(visual, new Vector3(-0.24f, 1.45f, 0f), 0.28f, rune);  // shoulders
                AddRune(visual, new Vector3(0.24f, 1.45f, 0f), 0.28f, rune);
                AddRune(visual, new Vector3(0f, 0.98f, 0.17f), 0.26f, rune);   // belt
                _ring = Glow.MakeRing(transform, new Vector3(0f, 0.06f, 0f), 2.6f, new Color(0.25f, 0.8f, 2.6f, 1f));
                return;
            }

            visual.localScale = _baseScale;
            for (int i = 0; i < _skin.Count; i++)
                if (_skin[i] != null) _skin[i].sharedMaterials = _originals[i];
            _skin.Clear();
            _originals.Clear();
            foreach (var g in _runes) if (g != null) Destroy(g.gameObject);
            _runes.Clear();
            if (_ring != null) Destroy(_ring.gameObject);
            _ring = null;
        }

        void AddRune(Transform visual, Vector3 localPos, float size, Color colour)
        {
            var g = Glow.Make(visual, localPos, size, colour);
            // Deep blue gem under the glow, so the rune reads in daylight too (additive light alone washes out).
            var gem = Shapes.Make(PrimitiveType.Sphere, g.transform, "Rune Gem", Vector3.zero, new Vector3(0.18f, 0.18f, 0.18f), new Color(0.12f, 0.5f, 1f), false, true);
            gem.transform.localScale = Vector3.one * (0.09f / Mathf.Max(0.01f, size));
            var bone = NearestBone(visual, g.transform.position);
            if (bone != null) g.transform.SetParent(bone, true);
            _runes.Add(g);
        }

        Transform NearestBone(Transform visual, Vector3 at)
        {
            Transform best = null;
            float bestD = 0.35f * 0.35f; // only bones close by; otherwise stay on the body root
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (SkinnedOutline.IsOutline(smr) || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b == null || IsUnder(b, _combat.swordPivot) || IsUnder(b, _combat.staffPivot)) continue;
                    float d = (b.position - at).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = b; }
                }
            }
            return best;
        }

        static bool IsUnder(Transform t, Transform root)
        {
            if (root == null) return false;
            for (var p = t; p != null; p = p.parent) if (p == root) return true;
            return false;
        }

        static Material StoneMaterial()
        {
            if (_stone != null) return _stone;
            var surf = RealArt.Has ? RealArt.Surface(RealArt.GolemStone, 0.6f, 0f, new Color(0.82f, 0.8f, 0.78f)) : null;
            if (surf != null) _stone = new Material(surf) { name = "Golem merge stone" };
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _stone = new Material(shader != null ? shader : Shapes.Material(Color.white, false).shader) { name = "Golem merge stone" };
                Color c = new Color(0.5f, 0.48f, 0.46f);
                _stone.color = c;
                if (_stone.HasProperty("_BaseColor")) _stone.SetColor("_BaseColor", c);
                if (_stone.HasProperty("_Smoothness")) _stone.SetFloat("_Smoothness", 0.1f);
            }
            _stone.EnableKeyword("_EMISSION");
            if (_stone.HasProperty("_EmissionColor")) _stone.SetColor("_EmissionColor", RuneBlue * 0.08f);
            return _stone;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _stone = null; }
    }
}
