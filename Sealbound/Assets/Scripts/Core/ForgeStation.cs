using Sealbound.Combat;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Core
{
    /// <summary>
    /// The Sealers' forge in the test field: a stone hearth with glowing coals, an anvil on a stump.
    /// Walk up and press G to open it (the HUD draws the forge screen).
    /// </summary>
    public class ForgeStation : MonoBehaviour
    {
        public const float Reach = 3.6f;

        /// <summary>The forge the player is standing at, if any.</summary>
        public static ForgeStation Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; _iron = null; }

        public bool InReach { get; private set; }
        Light _fire;
        Glow _coals;
        Glow[] _flames;
        Transform _player;
        float _seed;

        public static ForgeStation Build(Transform parent, Vector3 at, float yaw)
        {
            var root = new GameObject("Sealer Forge");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Landscape.HeightAt(at), at.z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var f = root.AddComponent<ForgeStation>();
            f._seed = Random.value * 10f;

            bool real = RealArt.Has;
            Material stone = real ? RealArt.Surface(RealArt.CastleStone, 1.2f, 0.25f) : Shapes.Material(new Color(0.5f, 0.48f, 0.45f), false);
            Material dark = real ? RealArt.Surface(RealArt.GolemStone, 0.8f, 0f) : Shapes.Material(new Color(0.3f, 0.29f, 0.28f), false);
            Material wood = ForgedWeapons.Timber();
            Material iron = Iron();
            var t = root.transform;

            // The hearth: a stone block with a fire mouth, and a chimney behind.
            Piece(t, PrimitiveType.Cube, "Hearth", new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 1.1f, 1.3f), stone, true);
            Piece(t, PrimitiveType.Cube, "Hearth Top", new Vector3(0f, 1.14f, 0f), new Vector3(2.05f, 0.12f, 1.45f), dark, false);
            Piece(t, PrimitiveType.Cube, "Fire Mouth", new Vector3(0f, 0.62f, 0.62f), new Vector3(0.9f, 0.5f, 0.1f), Shapes.Material(new Color(0.03f, 0.02f, 0.015f), false), false); // the dark mouth the fire burns in
            Piece(t, PrimitiveType.Cube, "Chimney", new Vector3(0f, 2.3f, -0.35f), new Vector3(0.85f, 2.4f, 0.7f), stone, true);
            Piece(t, PrimitiveType.Cube, "Chimney Cap", new Vector3(0f, 3.55f, -0.35f), new Vector3(1.0f, 0.12f, 0.85f), dark, false);
            f._coals = Glow.Make(t, new Vector3(0f, 0.45f, 0.7f), 0.9f, new Color(2.6f, 1.0f, 0.25f, 0.9f));
            // Flames: soft tongues of light that rise and flicker in the fire mouth.
            f._flames = new Glow[4];
            for (int i = 0; i < f._flames.Length; i++)
                f._flames[i] = Glow.Make(t, new Vector3(-0.27f + i * 0.18f, 0.55f, 0.72f), 0.45f, new Color(2.8f, 1.3f, 0.35f, 0.85f));
            Glow.Make(t, new Vector3(0f, 1.25f, 0.1f), 0.9f, new Color(2.2f, 0.9f, 0.25f, 0.5f)); // embers on the bed
            var fireGo = new GameObject("Forge Fire");
            fireGo.transform.SetParent(t, false);
            fireGo.transform.localPosition = new Vector3(0f, 0.9f, 1.1f);
            f._fire = fireGo.AddComponent<Light>();
            f._fire.type = LightType.Point;
            f._fire.color = new Color(1f, 0.55f, 0.22f);
            f._fire.range = 7f;
            f._fire.intensity = 2.5f;
            f._fire.shadows = LightShadows.None;

            // The anvil on a stump, in front and to the side.
            Vector3 a = new Vector3(1.7f, 0f, 1.3f);
            Piece(t, PrimitiveType.Cylinder, "Stump", a + new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.3f, 0.6f), wood, true);
            Piece(t, PrimitiveType.Cube, "Anvil Waist", a + new Vector3(0f, 0.72f, 0f), new Vector3(0.22f, 0.22f, 0.3f), iron, false);
            Piece(t, PrimitiveType.Cube, "Anvil Face", a + new Vector3(0f, 0.9f, 0f), new Vector3(0.36f, 0.14f, 0.6f), iron, false);
            var horn = Piece(t, PrimitiveType.Capsule, "Anvil Horn", a + new Vector3(0f, 0.9f, 0.4f), new Vector3(0.12f, 0.16f, 0.12f), iron, false);
            horn.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // A hammer resting on it.
            var head = Piece(t, PrimitiveType.Cube, "Hammer Head", a + new Vector3(0.06f, 1.02f, -0.12f), new Vector3(0.08f, 0.08f, 0.18f), iron, false);
            head.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            var handle = Piece(t, PrimitiveType.Cylinder, "Hammer Handle", a + new Vector3(0.14f, 1.0f, 0.02f), new Vector3(0.03f, 0.17f, 0.03f), wood, false);
            handle.transform.localRotation = Quaternion.Euler(90f, 25f, 0f);
            // A few runestone blocks waiting to be worked.
            Piece(t, PrimitiveType.Cube, "Raw Runestone", new Vector3(-1.5f, 0.2f, 1.1f), new Vector3(0.5f, 0.4f, 0.45f), dark, false).transform.localRotation = Quaternion.Euler(0f, 20f, 4f);
            Piece(t, PrimitiveType.Cube, "Raw Runestone", new Vector3(-1.2f, 0.15f, 1.6f), new Vector3(0.35f, 0.3f, 0.35f), dark, false).transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
            return f;
        }

        static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m, bool collider)
        {
            var go = Shapes.Make(type, parent, name, pos, size, Color.white, collider);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material _iron;
        static Material Iron()
        {
            if (_iron != null) return _iron;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _iron = new Material(shader != null ? shader : Shapes.Material(Color.white, false).shader) { name = "Forge iron" };
            Color c = new Color(0.2f, 0.2f, 0.21f);
            _iron.color = c;
            if (_iron.HasProperty("_BaseColor")) _iron.SetColor("_BaseColor", c);
            if (_iron.HasProperty("_Metallic")) _iron.SetFloat("_Metallic", 0.7f);
            if (_iron.HasProperty("_Smoothness")) _iron.SetFloat("_Smoothness", 0.45f);
            return _iron;
        }

        void Update()
        {
            // The fire breathes.
            float flicker = Mathf.PerlinNoise(Time.time * 3.1f, _seed) * 0.6f + Mathf.PerlinNoise(Time.time * 9f, _seed + 3f) * 0.25f;
            if (_fire != null) _fire.intensity = 1.9f + 1.6f * flicker;
            if (_coals != null) _coals.SetStrength(0.65f + 0.45f * flicker);
            if (_flames != null)
                for (int i = 0; i < _flames.Length; i++)
                {
                    float n = Mathf.PerlinNoise(Time.time * (4f + i), _seed + i * 7.3f);
                    var ft = _flames[i].transform;
                    ft.localPosition = new Vector3(ft.localPosition.x, 0.5f + n * 0.14f, ft.localPosition.z);
                    ft.localScale = new Vector3(0.32f + 0.1f * n, 0.45f + 0.35f * n, 1f);
                    _flames[i].SetStrength(0.5f + 0.5f * n);
                }

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
