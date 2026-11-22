using Sealbound.Combat;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Player
{
    /// <summary>
    /// The player's real body: the KayKit Knight for the Sword class and the KayKit Mage for the Mage class.
    /// It picks walk, run, jump, hit and death animations from what the player is doing, and during attacks
    /// it bends the sword arm so the weapon follows the combat system's timed swing arcs.
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        [Tooltip("Height of the character model in metres.")]
        public float modelHeight = 1.75f;
        [Tooltip("Ground speed (m/s) that the run animation matches at normal playback speed.")]
        public float runClipSpeed = 4.5f;
        public float walkClipSpeed = 1.7f;
        [Tooltip("How much the upper body twists to follow a swing.")]
        [Range(0f, 1f)] public float chestTwist = 0.5f;

        class Body
        {
            public GameObject root;
            public CharacterAnimator anim;
            public Transform upperArm, lowerArm, handSlot, chest;
            public Transform pivot, weapon;
            public Quaternion align = Quaternion.identity; // turns the weapon's blade onto the pivot's forward
            public float scale = 1f;
        }

        PlayerMotor _motor;
        PlayerCombat _combat;
        PlayerVitals _vitals;
        Health _health;
        Body _swordBody, _mageBody, _active;
        float _armWeight, _airTime;

        /// <summary>
        /// Builds both class bodies under <paramref name="visual"/> and fills the two weapon pivots with
        /// real weapon models. Returns null (and builds nothing) when the art is not in the project.
        /// </summary>
        public static PlayerAvatar TryBuild(GameObject player, Transform visual, Transform swordPivot, Transform staffPivot)
        {
            if (!ArtLibrary.HasCharacters) return null;
            var av = player.AddComponent<PlayerAvatar>();
            av._swordBody = av.BuildBody("Knight", visual, swordPivot, "sword_1handed");
            av._mageBody = av.BuildBody("Mage", visual, staffPivot, "staff");
            if (av._swordBody == null || av._mageBody == null)
            {
                if (av._swordBody != null) Destroy(av._swordBody.root);
                if (av._mageBody != null) Destroy(av._mageBody.root);
                Destroy(av);
                return null;
            }
            av._mageBody.root.SetActive(false);
            av._active = av._swordBody;
            return av;
        }

        Body BuildBody(string className, Transform visual, Transform pivot, string weaponProp)
        {
            var go = ArtLibrary.Spawn(ArtLibrary.Characters + className, visual, ArtLibrary.ClassTexture(className));
            if (go == null) return null;
            go.name = className + " (KayKit)";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var b = new Body { root = go };
            b.scale = ArtLibrary.FitHeight(go, modelHeight);
            Shapes.SetLayerRecursive(go, 2);

            var t = go.transform;
            b.upperArm = ArtLibrary.FindDeep(t, "upperarm.r");
            b.lowerArm = ArtLibrary.FindDeep(t, "lowerarm.r");
            b.handSlot = ArtLibrary.FindDeep(t, "handslot.r");
            b.chest = ArtLibrary.FindDeep(t, "chest");
            if (b.handSlot == null) b.handSlot = ArtLibrary.FindDeep(t, "hand.r");
            b.anim = CharacterAnimator.Attach(go, ArtLibrary.AllClips());
            b.anim.SetBase("Idle_A");

            b.pivot = pivot;
            b.weapon = BuildWeapon(pivot, weaponProp, go.transform.localScale.x, className == "Knight", out b.align);
            return b;
        }

        Transform BuildWeapon(Transform pivot, string prop, float scale, bool isSword, out Quaternion align)
        {
            align = Quaternion.identity;
            var go = ArtLibrary.Spawn(ArtLibrary.Props + prop, pivot);
            if (go == null) return null;
            go.name = "Weapon Model";
            var t = go.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * scale;

            // Find which way the blade (or staff) points in the model's own space: its longest side,
            // away from the grip at the model's origin.
            Bounds lb = BoneAim.LocalMeshBounds(t);
            Vector3 size = lb.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            float max = lb.max[axis], min = lb.min[axis];
            Vector3 dir = Vector3.zero;
            dir[axis] = Mathf.Abs(max) >= Mathf.Abs(min) ? 1f : -1f;
            float length = Mathf.Max(Mathf.Abs(max), Mathf.Abs(min));
            align = Quaternion.FromToRotation(dir, Vector3.forward);
            Vector3 tipLocal = dir * length;

            if (isSword)
            {
                var tip = new GameObject("Blade Tip").transform;
                tip.SetParent(t, false);
                tip.localPosition = tipLocal;
                var trail = tip.gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.1f;
                trail.minVertexDistance = 0.05f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) trail.sharedMaterial = new Material(shader);
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.35f, 0.75f, 0.95f), 1f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.emitting = false;
            }
            else
            {
                // A glowing ember at the staff's head: the Mage's beat cue.
                var orb = Shapes.Make(PrimitiveType.Sphere, t, "Ember Orb", tipLocal + dir * (0.06f / scale), Vector3.one * (0.16f / scale),
                    new Color(1f, 0.5f, 0.18f));
                orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Shapes.SetLayerRecursive(go, 2);
            return t;
        }

        /// <summary>The renderer that flashes on the beat: the sword itself, or the staff's ember.</summary>
        public Renderer CueRenderer(bool sword)
        {
            var b = sword ? _swordBody : _mageBody;
            if (b == null || b.weapon == null) return null;
            if (!sword)
            {
                var orb = b.weapon.Find("Ember Orb");
                if (orb != null) return orb.GetComponent<Renderer>();
            }
            foreach (var r in b.weapon.GetComponentsInChildren<Renderer>(true))
                if (!(r is TrailRenderer) && !SkinnedOutline.IsOutline(r)) return r;
            return null;
        }

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _vitals = GetComponent<PlayerVitals>();
            _health = GetComponent<Health>();
        }

        void Start()
        {
            if (_health != null) _health.Damaged += OnDamaged;
            if (_vitals != null)
            {
                _vitals.Fell += () => { if (_active != null) _active.anim.PlayOnce("Death_A", 1f, 0.08f, true); };
                _vitals.Respawned += () =>
                {
                    if (_active == null) return;
                    _active.anim.StopOnce(0.05f);
                    _active.anim.PlayOnce("Spawn_Ground", 1.6f, 0.05f);
                };
            }
        }

        void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        void OnDamaged(DamageInfo info, float dealt)
        {
            if (_active == null || dealt <= 0f || (_vitals != null && _vitals.IsDown)) return;
            _active.anim.PlayOnce(Random.value < 0.5f ? "Hit_A" : "Hit_B", 1.8f, 0.05f);
        }

        Body Wanted => _combat == null || _combat.Kit == _combat.swordKit ? _swordBody : _mageBody;

        void Update()
        {
            var want = Wanted;
            if (want != _active && want != null)
            {
                if (_active != null) _active.root.SetActive(false);
                want.root.SetActive(true);
                _active = want;
                _armWeight = 0f;
            }
            if (_active == null || _motor == null) return;
            if (_vitals != null && _vitals.IsDown) return;

            var a = _active.anim;
            float dt = Time.deltaTime;
            float speed = _motor.PlanarVelocity.magnitude;
            bool grounded = _motor.IsGrounded;

            if (!grounded) _airTime += dt;
            else
            {
                if (_airTime > 0.45f && speed < 2f && !_combat.IsAttacking) a.PlayOnce("Jump_Land", 2.2f, 0.05f);
                _airTime = 0f;
            }

            if (_motor.IsDodging || _motor.IsDashing)
                a.SetBase("Running_B", 2.4f, 0.05f);
            else if (!grounded && _airTime > 0.08f)
                a.SetBase("Jump_Idle", 1f, 0.12f);
            else if (speed > 0.5f)
            {
                if (speed < 3.2f) a.SetBase("Walking_A", Mathf.Clamp(speed / walkClipSpeed, 0.6f, 1.8f), 0.12f);
                else a.SetBase(_motor.IsSprinting ? "Running_B" : "Running_A", Mathf.Clamp(speed / runClipSpeed, 0.8f, 2.4f), 0.1f);
            }
            else a.SetBase("Idle_A", 1f, 0.15f);
        }

        void LateUpdate()
        {
            // Runs after the animation has posed the skeleton this frame.
            var b = _active;
            if (b == null || b.weapon == null || b.handSlot == null) return;
            float dt = Time.deltaTime;
            bool swinging = _combat != null && _combat.IsAttacking && (_vitals == null || !_vitals.IsDown);
            _armWeight = Mathf.MoveTowards(_armWeight, swinging ? 1f : 0f, dt / (swinging ? 0.05f : 0.2f));

            if (b.upperArm != null) b.pivot.position = b.upperArm.position;

            if (_armWeight > 0.001f && b.upperArm != null)
            {
                Vector3 dir = b.pivot.forward;
                Transform vis = _motor != null && _motor.visual != null ? _motor.visual : transform;
                if (b.chest != null && chestTwist > 0f)
                {
                    Vector3 local = vis.InverseTransformDirection(dir);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                    b.chest.rotation = Quaternion.AngleAxis(yaw * chestTwist * _armWeight, vis.up) * b.chest.rotation;
                }
                BoneAim.AimArm(b.upperArm, b.lowerArm, b.handSlot, dir, _armWeight);
            }

            // Held in the hand normally; during a swing it follows the combat system's swing arc.
            Quaternion swing = b.pivot.rotation * b.align;
            b.weapon.position = b.handSlot.position;
            b.weapon.rotation = _armWeight >= 0.999f ? swing : Quaternion.Slerp(b.handSlot.rotation, swing, _armWeight);
        }
    }
}
