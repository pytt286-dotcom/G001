using System.Collections.Generic;
using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Loot
{
    /// <summary>
    /// A dropped item in the world. It bursts out of the sealed creature, lands, glows (rare items
    /// with a pillar of light), and flies into the player's pack when they come near.
    /// </summary>
    public class LootDrop : MonoBehaviour
    {
        ItemDef _def;
        int _count;
        Vector3 _velocity;
        float _age, _groundY, _spinSpeed;
        bool _landed, _collecting;
        float _magnetRadius;
        Transform _model, _player;
        Glow _halo;
        float _flySpeed;

        const float Gravity = 18f;
        static AudioClip _pickupLow, _pickupHigh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pickupLow = null; _pickupHigh = null; _crystal = null; }

        /// <summary>All drops still lying in the world.</summary>
        public static readonly List<LootDrop> Active = new List<LootDrop>();

        public ItemDef Item => _def;
        public int Count => _count;

        /// <summary>Throws <paramref name="count"/> of an item out from <paramref name="from"/>.</summary>
        public static LootDrop Spawn(string id, int count, Vector3 from, Vector3 velocity)
        {
            var def = ItemCatalog.Get(id);
            if (def == null || count <= 0) return null;
            var go = new GameObject("Loot: " + def.displayName);
            go.layer = 2;
            go.transform.position = from;
            var d = go.AddComponent<LootDrop>();
            d._def = def;
            d._count = count;
            d._velocity = velocity;
            d._spinSpeed = Random.Range(60f, 120f) * (Random.value < 0.5f ? -1f : 1f);
            d._magnetRadius = def.kind == ItemKind.Essence ? 40f : def.kind == ItemKind.Spirit ? 2.2f : 3.2f;
            d._groundY = GroundBelow(from);
            d.BuildLook();
            return d;
        }

        /// <summary>Sprays a whole loot list out of a point, like a burst of sparks.</summary>
        public static void Burst(List<KeyValuePair<string, int>> loot, Vector3 from, float spread)
        {
            int i = 0;
            foreach (var kv in loot)
            {
                var def = ItemCatalog.Get(kv.Key);
                if (def == null) continue;
                // Essence comes as a handful of motes, parts one by one, spirits as a single soul.
                int pieces = def.kind == ItemKind.Essence ? Mathf.Clamp(kv.Value / 5, 3, 8) : def.kind == ItemKind.Spirit ? 1 : kv.Value;
                int left = kv.Value;
                for (int p = 0; p < pieces; p++, i++)
                {
                    int n = p == pieces - 1 ? left : Mathf.Max(1, kv.Value / pieces);
                    left -= n;
                    float ang = i * 137.5f + Random.Range(-20f, 20f);
                    Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                    float sp = def.kind == ItemKind.Spirit ? 1f : Random.Range(spread * 0.5f, spread);
                    Vector3 v = dir * sp + Vector3.up * Random.Range(7f, 11f);
                    Spawn(kv.Key, n, from + Vector3.up * Random.Range(0f, 0.6f), v);
                }
            }
        }

        static float GroundBelow(Vector3 p)
        {
            float y = Landscape.HeightAt(p);
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                y = Mathf.Max(y, hit.point.y);
            return y;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void BuildLook()
        {
            var root = new GameObject("Look").transform;
            root.SetParent(transform, false);
            _model = root;
            Color g = _def.glow;
            switch (_def.kind)
            {
                case ItemKind.Essence:
                    _halo = Glow.Make(root, Vector3.zero, 0.45f, new Color(g.r * 2f, g.g * 2f, g.b * 2f, 0.9f));
                    Glow.Make(root, Vector3.zero, 0.12f, new Color(2f, 2f, 2f, 1f));
                    break;
                case ItemKind.Spirit:
                    _halo = Glow.Make(root, Vector3.zero, 1.6f, new Color(g.r * 1.6f, g.g * 1.6f, g.b * 1.6f, 0.8f));
                    Glow.Make(root, Vector3.zero, 0.45f, new Color(2.5f, 2.5f, 2.5f, 1f));
                    AddLight(g, 6f, 2.5f);
                    break;
                default:
                {
                    bool core = _def.id == ItemCatalog.GolemCore;
                    bool fang = _def.id == ItemCatalog.BurrowFang;
                    var mesh = new GameObject("Model");
                    mesh.layer = 2;
                    mesh.transform.SetParent(root, false);
                    mesh.AddComponent<MeshFilter>().sharedMesh = Crystal(); // facets catch the light; a smooth ball reads as a bulb
                    var r = mesh.AddComponent<MeshRenderer>();
                    r.sharedMaterial = PartMaterial(_def, fang ? 0f : core ? 0.9f : 1.4f);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mesh.transform.localScale = core ? new Vector3(0.44f, 0.5f, 0.44f) : fang ? new Vector3(0.09f, 0.2f, 0.09f) : new Vector3(0.19f, 0.42f, 0.19f);
                    _halo = Glow.Make(root, Vector3.zero, core ? 1.4f : 0.95f, new Color(g.r * 1.8f, g.g * 1.8f, g.b * 1.8f, fang ? 0.3f : 0.65f));
                    if (core)
                    {
                        // A dark stone heart with fire showing through: a hot glow at its centre.
                        Glow.Make(root, Vector3.zero, 0.35f, new Color(2.6f, 1.6f, 0.7f, 1f));
                        AddLight(g, 4f, 2f);
                    }
                    break;
                }
            }
            // Rare and better items stand out from far away with a pillar of light.
            if (_def.rarity >= Rarity.Rare)
            {
                Color rc = _def.RarityColour;
                var pillar = Glow.Make(transform, new Vector3(0f, 3f, 0f), 1f, new Color(rc.r * 2.6f, rc.g * 2.6f, rc.b * 2.6f, 0.75f));
                pillar.transform.localScale = new Vector3(0.55f, 7f, 1f);
                pillar.name = "Light Pillar";
                pillar.gameObject.SetActive(false); // shown once it lands
            }
        }

        void AddLight(Color c, float range, float intensity)
        {
            var l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_player == null)
            {
                var m = FindAnyObjectByType<PlayerMotor>();
                if (m != null) _player = m.transform;
            }

            if (_collecting) { Fly(dt); return; }

            if (!_landed)
            {
                _velocity += Vector3.down * Gravity * dt;
                Vector3 p = transform.position + _velocity * dt;
                float rest = _groundY + (_def.kind == ItemKind.Spirit ? 1.3f : 0.45f);
                if (p.y <= rest && _velocity.y < 0f)
                {
                    p.y = rest;
                    if (_velocity.y < -4f && _def.kind != ItemKind.Spirit) _velocity = new Vector3(_velocity.x * 0.4f, -_velocity.y * 0.3f, _velocity.z * 0.4f); // a small bounce
                    else
                    {
                        _landed = true;
                        var pillar = transform.Find("Light Pillar");
                        if (pillar != null) pillar.gameObject.SetActive(true);
                    }
                }
                transform.position = p;
            }
            else
            {
                // Bob and turn, so it catches the eye.
                float bob = Mathf.Sin(_age * 2.4f + _spinSpeed) * (_def.kind == ItemKind.Spirit ? 0.18f : 0.08f);
                _model.localPosition = new Vector3(0f, bob, 0f);
                _model.Rotate(0f, _spinSpeed * dt, 0f, Space.World);
            }
            if (_halo != null) _halo.SetStrength(0.8f + 0.2f * Mathf.Sin(_age * 5f));

            if (_player != null && _age > 0.9f)
            {
                Vector3 d = Chest() - transform.position;
                if (d.magnitude <= _magnetRadius) { _collecting = true; _flySpeed = _def.kind == ItemKind.Essence ? 4f : 6f; }
            }
        }

        Vector3 Chest() => _player.position + Vector3.up * 1.1f;

        void Fly(float dt)
        {
            if (_player == null) { _collecting = false; return; }
            _flySpeed += dt * 40f;
            Vector3 to = Chest() - transform.position;
            float step = _flySpeed * dt;
            if (to.magnitude <= Mathf.Max(0.35f, step))
            {
                Collect();
                return;
            }
            transform.position += to.normalized * step;
            transform.localScale = Vector3.one * Mathf.Clamp(to.magnitude / 1.5f, 0.35f, 1f);
        }

        void Collect()
        {
            Inventory.Add(_def.id, _count);
            PlayPickup(_def);
            if (_def.rarity >= Rarity.Rare) SealBurst.Spawn(transform.position, _def.kind == ItemKind.Spirit ? 3f : 1.5f, _def.glow);
            Destroy(gameObject);
        }

        static void PlayPickup(ItemDef def)
        {
            if (_pickupLow == null)
            {
                _pickupLow = CombatAudio.Tone("Pickup", 880f, 0.12f, 0.25f, 300f);
                _pickupHigh = CombatAudio.Chord("Rare pickup", new[] { 523f, 659f, 784f, 1046f }, 0.9f, 0.22f);
            }
            var clip = def.rarity >= Rarity.Rare ? _pickupHigh : _pickupLow;
            var cam = Camera.main;
            if (clip != null && cam != null) AudioSource.PlayClipAtPoint(clip, cam.transform.position, def.kind == ItemKind.Essence ? 0.35f : 0.8f);
        }

        // ---------- Looks ----------

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        static Material PartMaterial(ItemDef def, float emission)
        {
            if (Materials.TryGetValue(def.id, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader != null ? shader : Shapes.Material(Color.white, false).shader) { name = def.displayName };
            Color c = def.id == ItemCatalog.BurrowFang ? new Color(0.9f, 0.84f, 0.7f)
                    : def.id == ItemCatalog.GolemCore ? new Color(0.35f, 0.2f, 0.12f)
                    : Color.Lerp(def.glow, Color.white, 0.25f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", def.glow * emission);
            }
            Materials[def.id] = m;
            return m;
        }

        static Mesh _crystal;

        /// <summary>A six-sided crystal, pointed at both ends, about 1 x 1 x 1 before scaling, flat shaded.</summary>
        static Mesh Crystal()
        {
            if (_crystal != null) return _crystal;
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                ring[i] = new Vector3(Mathf.Cos(a) * 0.5f, (i % 2 == 0 ? 0.08f : -0.08f), Mathf.Sin(a) * 0.5f);
            }
            Vector3 top = new Vector3(0f, 0.5f, 0f), bottom = new Vector3(0f, -0.5f, 0f);
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 6];
                Face(v, t, top, b, a);
                Face(v, t, bottom, a, b);
            }
            _crystal = new Mesh { name = "Crystal" };
            _crystal.SetVertices(v);
            _crystal.SetTriangles(t, 0);
            _crystal.RecalculateNormals();
            _crystal.RecalculateBounds();
            return _crystal;
        }

        /// <summary>Adds a triangle facing away from the centre (Unity draws the side its normal points to).</summary>
        static void Face(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(n, (p0 + p1 + p2) / 3f) < 0f) { Vector3 tmp = p1; p1 = p2; p2 = tmp; }
            int k = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2);
            t.Add(k); t.Add(k + 1); t.Add(k + 2);
        }

    }
}
