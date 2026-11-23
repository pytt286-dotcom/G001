using Sealbound.Combat;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// A glowing rune set into the Bastion Golem: its weak point. Damage to a rune also hurts the golem
    /// (ignoring its stone armour), and breaking one brings the golem to its knees with its core exposed.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class GolemRune : MonoBehaviour
    {
        public BastionGolem golem;
        public bool isCore;
        [Tooltip("How much of the damage dealt to this rune passes on to the golem.")]
        public float passThrough = 1f;

        public Health Health { get; private set; }
        public bool Broken => !isCore && Health != null && Health.IsDead;
        public bool Exposed { get; private set; } = true;

        Renderer _gem;
        Material _mat;
        Collider _collider;
        Light _light;
        Color _color = new Color(0.12f, 0.5f, 1f);
        float _flash, _size;

        public static GolemRune Create(BastionGolem golem, Transform parent, string name, Vector3 localPos, float size, float hp, bool core)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // A flat glowing rune set into the rock, with a light that glows on the stone around it.
            var gem = Shapes.Make(PrimitiveType.Sphere, go.transform, "Rune Gem", Vector3.zero, new Vector3(size, size, size * 0.4f), new Color(0.12f, 0.5f, 1f), true, true);
            gem.layer = 2;
            gem.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var health = go.AddComponent<Health>();
            health.maxHealth = hp;
            health.ResetHealth();

            var rune = go.AddComponent<GolemRune>();
            rune.golem = golem;
            rune.isCore = core;
            rune.passThrough = core ? 2.5f : 1f;
            rune._gem = gem.GetComponent<Renderer>();
            rune._mat = rune._gem.material;
            rune._collider = gem.GetComponent<Collider>();
            rune._size = size;
            var lightGo = new GameObject("Rune Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.forward * size * 0.9f;
            rune._light = lightGo.AddComponent<Light>();
            rune._light.type = LightType.Point;
            rune._light.range = core ? 7f : 4.5f;
            rune._light.intensity = core ? 4f : 2.5f;
            rune._light.shadows = LightShadows.None;
            if (core) rune.SetExposed(false);
            return rune;
        }

        void Awake()
        {
            Health = GetComponent<Health>();
            Health.InvulnerableCheck = () => !Exposed || golem == null || !golem.CanBeHurt;
            Health.Damaged += OnDamaged;
            Health.Died += OnBroken;
        }

        void OnDamaged(DamageInfo info, float dealt)
        {
            _flash = 1f;
            DamageNumbers.Show(transform.position + Vector3.up * 0.8f, dealt, true, false);
            if (golem != null) golem.OnRuneHit(this, info, dealt * passThrough);
        }

        void OnBroken()
        {
            SealBurst.Spawn(transform.position, 2.4f, _color);
            DustPuff.Spawn(transform.position, 1.5f, 6);
            if (_gem != null) _gem.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (_light != null) _light.enabled = false;
            if (golem != null) golem.OnRuneBroken(this);
        }

        /// <summary>Brings a broken rune back (second phase, or the fight resetting).</summary>
        public void Restore()
        {
            Health.ResetHealth();
            if (_gem != null) _gem.enabled = Exposed;
            if (_collider != null) _collider.enabled = Exposed;
            if (_light != null) _light.enabled = Exposed;
            _flash = 1f;
        }

        /// <summary>The core only shows (and can be hit) while the golem is down on its knees.</summary>
        public void SetExposed(bool on)
        {
            Exposed = on;
            bool show = on && !Broken;
            if (_gem != null) _gem.enabled = show;
            if (_collider != null) _collider.enabled = show;
            if (_light != null) _light.enabled = show;
        }

        public void SetColor(Color c) => _color = c;

        void Update()
        {
            if (_mat == null) return;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 4f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (isCore ? 7f : 3f) + transform.position.x);
            // Keep the glow modest: strong emission blooms to white and the rune colour is lost.
            Color c = Color.Lerp(_color * pulse, Color.white, _flash * 0.6f);
            _mat.color = c;
            _mat.SetColor("_EmissionColor", c * (isCore ? 1.6f : 1.2f));
            if (_light != null) _light.color = _color;
            if (_gem != null)
                _gem.transform.localScale = new Vector3(1f, 1f, 0.4f) * _size * (1f + 0.15f * _flash + (isCore ? 0.08f * Mathf.Sin(Time.time * 7f) : 0f));
        }
    }
}
