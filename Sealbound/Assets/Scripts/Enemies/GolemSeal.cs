using Sealbound.CameraRig;
using Sealbound.Combat;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// The sealing ritual on a beaten Bastion Golem: a circle of runes opens round it, and the player
    /// holds Seal (G) nearby to bind it. A beam of light joins the Sealer to the golem while they hold.
    /// </summary>
    public class GolemSeal : MonoBehaviour
    {
        public const float HoldSeconds = 2.2f;
        public const float Reach = 14f;

        /// <summary>The ritual waiting for the player, if any (the HUD shows its prompt).</summary>
        public static GolemSeal Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; DevHold = false; }

        public float Progress { get; private set; }
        public bool InReach { get; private set; }
        public bool Holding { get; private set; }

        BastionGolem _golem;
        Glow _circle, _innerCircle;
        LineRenderer _beam;
        Light _light;
        PlayerInputReader _input;
        PlayerVitals _vitals;
        Transform _player;
        float _age;

        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);

        public static void Begin(BastionGolem golem)
        {
            var go = new GameObject("Golem Seal");
            go.transform.position = new Vector3(golem.transform.position.x, golem.transform.position.y + 0.06f, golem.transform.position.z);
            var s = go.AddComponent<GolemSeal>();
            s._golem = golem;
            s._circle = Glow.MakeRing(go.transform, Vector3.zero, Reach * 1.6f, new Color(Gold.r * 1.6f, Gold.g * 1.6f, Gold.b * 1.6f, 0.9f));
            s._innerCircle = Glow.MakeRing(go.transform, new Vector3(0f, 0.02f, 0f), Reach * 0.9f, new Color(Gold.r * 1.4f, Gold.g * 1.4f, Gold.b * 1.4f, 0.9f));
            s._circle.SetStrength(0f);
            s._innerCircle.SetStrength(0f);
            var lightGo = new GameObject("Seal Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 3f;
            s._light = lightGo.AddComponent<Light>();
            s._light.type = LightType.Point;
            s._light.color = Gold;
            s._light.range = 18f;
            s._light.intensity = 0f;
            s._light.shadows = LightShadows.None;
            Current = s;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_golem == null || !_golem.Subdued) { Destroy(gameObject); return; }
            if (_player == null)
            {
                var m = FindAnyObjectByType<PlayerMotor>();
                if (m != null)
                {
                    _player = m.transform;
                    _input = m.GetComponent<PlayerInputReader>();
                    _vitals = m.GetComponent<PlayerVitals>();
                }
            }

            Vector3 d = _player != null ? _player.position - transform.position : Vector3.one * 999f;
            d.y = 0f;
            InReach = _player != null && d.magnitude <= Reach && (_vitals == null || !_vitals.IsDown);
            Holding = InReach && (_input != null && _input.InteractHeld || DevHold);

            Progress = Holding ? Mathf.Min(1f, Progress + dt / HoldSeconds) : Mathf.Max(0f, Progress - dt * 0.6f);

            // The circle fades in, then brightens and turns as the seal takes hold.
            float open = Mathf.Clamp01(_age / 1.2f);
            _circle.SetStrength(open * (0.35f + 0.65f * Progress));
            _innerCircle.SetStrength(open * Progress);
            _circle.transform.Rotate(0f, 0f, dt * (8f + 70f * Progress), Space.Self);
            _innerCircle.transform.Rotate(0f, 0f, -dt * (12f + 110f * Progress), Space.Self);
            _light.intensity = 0.6f * open + 5f * Progress;
            if (Holding) ThirdPersonCamera.Shake(0.05f + 0.15f * Progress);

            UpdateBeam();

            if (Progress >= 1f)
            {
                Current = null;
                _golem.CompleteSeal();
                Destroy(gameObject);
            }
        }

        /// <summary>Lets the dev snapshot hold the seal without a keyboard.</summary>
        public static bool DevHold;

        void UpdateBeam()
        {
            if (!Holding || _player == null)
            {
                if (_beam != null) _beam.enabled = false;
                return;
            }
            if (_beam == null)
            {
                var go = new GameObject("Seal Beam");
                go.transform.SetParent(transform, false);
                _beam = go.AddComponent<LineRenderer>();
                _beam.positionCount = 12;
                _beam.useWorldSpace = true;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _beam.sharedMaterial = new Material(shader);
                _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _beam.numCapVertices = 4;
            }
            _beam.enabled = true;
            Vector3 from = _player.position + Vector3.up * 1.3f + _player.forward * 0.4f;
            Vector3 to = _golem.SealPoint;
            for (int i = 0; i < _beam.positionCount; i++)
            {
                float t = i / (_beam.positionCount - 1f);
                // A gentle arc that shivers while the seal fights back.
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.5f;
                p += Random.insideUnitSphere * 0.12f * Mathf.Sin(t * Mathf.PI) * (1f - Progress * 0.5f);
                _beam.SetPosition(i, p);
            }
            float w = 0.08f + 0.18f * Progress;
            _beam.startWidth = w;
            _beam.endWidth = w * 1.6f;
            Color c = new Color(Gold.r * 2f, Gold.g * 2f, Gold.b * 2f, 0.9f);
            _beam.startColor = c;
            _beam.endColor = new Color(c.r, c.g, c.b, 0.5f);
        }
    }
}
