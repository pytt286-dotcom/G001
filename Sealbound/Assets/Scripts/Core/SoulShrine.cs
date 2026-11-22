using Sealbound.Combat;
using Sealbound.Loot;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// The soul shrine: a ring of standing stones around an altar with a floating seal.
    /// Walk up and press G to bind a spirit for battle (or to make one whole from shards).
    /// The HUD shows the prompt and handles the key.
    /// </summary>
    public class SoulShrine : MonoBehaviour
    {
        public const float Reach = 3.4f;

        /// <summary>The shrine the player is standing at, if any.</summary>
        public static SoulShrine Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }

        public bool InReach { get; private set; }

        static readonly Color SpiritBlue = new Color(0.9f, 1.7f, 3.4f, 1f);

        Transform _seal, _sealRing, _player;
        Glow _sealGlow, _ring;
        Light _light;
        float _pulse;

        public static SoulShrine Build(Transform parent, Vector3 at, float yaw)
        {
            var root = new GameObject("Soul Shrine");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Landscape.HeightAt(at), at.z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var s = root.AddComponent<SoulShrine>();
            var t = root.transform;

            bool real = RealArt.Has;
            Material stone = real ? RealArt.Surface(RealArt.GolemStone, 1.4f, 0.35f) : Shapes.Material(new Color(0.48f, 0.47f, 0.46f), false);
            Material pale = real ? RealArt.Surface(RealArt.CastleStone, 1.2f, 0.15f) : Shapes.Material(new Color(0.62f, 0.6f, 0.58f), false);

            // A low round dais with the altar in the middle.
            Piece(t, PrimitiveType.Cylinder, "Dais", new Vector3(0f, 0.08f, 0f), new Vector3(5.2f, 0.08f, 5.2f), pale, true);
            Piece(t, PrimitiveType.Cube, "Altar", new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.85f, 0.9f), stone, true).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Piece(t, PrimitiveType.Cube, "Altar Top", new Vector3(0f, 0.97f, 0f), new Vector3(1.05f, 0.1f, 1.05f), pale, false).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            // Five weathered standing stones, open on the side facing the camp.
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2f) * 52f + 180f; // the gap faces local +z
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 3.1f;
                float h = 2.1f + (i % 2) * 0.6f;
                var st = Piece(t, PrimitiveType.Cube, "Standing Stone", p + Vector3.up * h * 0.5f, new Vector3(0.7f, h, 0.45f), stone, true);
                st.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), a, Random.Range(-5f, 5f));
                Glow.Make(t, p + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, -0.3f) + Vector3.up * h * 0.6f, 0.35f, new Color(0.8f, 1.5f, 3f, 0.9f)); // a rune cut into each stone
            }

            // The floating seal the spirit is bound through.
            var seal = new GameObject("Floating Seal").transform;
            seal.SetParent(t, false);
            seal.localPosition = new Vector3(0f, 2f, 0f);
            s._seal = seal;
            s._sealGlow = Glow.Make(seal, Vector3.zero, 1.6f, SpiritBlue);
            // A ring of glowing rune bars, so the seal reads even in daylight.
            var ring = new GameObject("Seal Ring").transform;
            ring.SetParent(seal, false);
            s._sealRing = ring;
            Color runeLight = new Color(0.55f, 0.85f, 1f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var bar = Shapes.Make(PrimitiveType.Cube, ring, "Seal Rune", Quaternion.Euler(0f, 0f, a) * Vector3.up * 0.62f,
                    new Vector3(i % 3 == 0 ? 0.2f : 0.12f, 0.06f, 0.05f), runeLight, false, true);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            Shapes.Make(PrimitiveType.Cube, ring, "Seal Core", Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), runeLight, false, true).transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            s._ring = Glow.MakeRing(t, new Vector3(0f, 0.14f, 0f), 4.6f, new Color(0.7f, 1.3f, 2.6f, 0.9f));

            var lightGo = new GameObject("Shrine Light");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            s._light = lightGo.AddComponent<Light>();
            s._light.type = LightType.Point;
            s._light.color = new Color(0.55f, 0.75f, 1f);
            s._light.range = 8f;
            s._light.intensity = 1.6f;
            s._light.shadows = LightShadows.None;
            return s;
        }

        static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m, bool collider)
        {
            var go = Shapes.Make(type, parent, name, pos, size, Color.white, collider);
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>What pressing G here would do, for the HUD prompt.</summary>
        public static string PromptText()
        {
            string bound = Inventory.BoundSpirit;
            if (Inventory.Count(ItemCatalog.GolemSpirit) > 0)
                return bound == ItemCatalog.GolemSpirit
                    ? "The Golem Spirit is bound. Fill the Soul Gauge in battle, then press  V  (R3) to SOUL MERGE"
                    : "Press  G  (d-pad up) to BIND the Golem Spirit";
            int shards = Inventory.Count(ItemCatalog.GolemSpiritShard);
            if (shards >= ItemCatalog.ShardsPerSpirit)
                return $"Press  G  (d-pad up) to make the Golem Spirit whole from {ItemCatalog.ShardsPerSpirit} shards and BIND it";
            return $"The shrine is silent. Bring a creature's spirit, or {ItemCatalog.ShardsPerSpirit} spirit shards ({shards}/{ItemCatalog.ShardsPerSpirit}).";
        }

        /// <summary>Binds the Golem Spirit (making it from shards if needed). Returns a line for the HUD, or null if nothing happened.</summary>
        public string TryBind()
        {
            if (Inventory.BoundSpirit == ItemCatalog.GolemSpirit) return null;
            bool made = false;
            if (Inventory.Count(ItemCatalog.GolemSpirit) <= 0)
            {
                if (!Inventory.Spend(ItemCatalog.GolemSpiritShard, ItemCatalog.ShardsPerSpirit)) return null;
                Inventory.Add(ItemCatalog.GolemSpirit, 1);
                made = true;
            }
            if (!Inventory.BindSpirit(ItemCatalog.GolemSpirit)) return null;
            _pulse = 1f;
            SealBurst.Spawn(transform.position + Vector3.up * 2f, 3f, new Color(0.6f, 0.85f, 1f));
            return made ? "The shards fuse. Golem Spirit bound." : "Golem Spirit bound. Fill the Soul Gauge, then press V.";
        }

        void Update()
        {
            float t = Time.time;
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 0.8f);
            bool bound = Inventory.BoundSpirit != "";
            if (_seal != null)
            {
                _seal.localPosition = new Vector3(0f, 2f + Mathf.Sin(t * 1.3f) * 0.12f, 0f);
                _seal.localScale = Vector3.one * (1f + _pulse * 0.8f);
                if (_sealRing != null) _sealRing.localRotation = Quaternion.Euler(0f, t * 25f, t * (bound ? 60f : 20f));
            }
            float breathe = 0.6f + 0.25f * Mathf.Sin(t * 2f) + _pulse;
            if (_sealGlow != null) _sealGlow.SetStrength(bound ? breathe : breathe * 0.6f);
            if (_ring != null) _ring.SetStrength(0.45f + 0.2f * Mathf.Sin(t * 1.1f) + _pulse);
            if (_light != null) _light.intensity = 1.3f + 0.4f * Mathf.Sin(t * 2f) + _pulse * 4f;

            if (_player == null)
            {
                var m = FindAnyObjectByType<PlayerMotor>();
                if (m != null) _player = m.transform;
            }
            Vector3 d = _player != null ? _player.position - transform.position : Vector3.one * 999f;
            d.y = 0f;
            InReach = d.magnitude <= Reach;
            if (InReach) Current = this;
            else if (Current == this) Current = null;
        }
    }
}
