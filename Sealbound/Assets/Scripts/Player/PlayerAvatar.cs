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
            public HeroBodies.Profile profile = HeroBodies.KayKit;
            public Transform index, pinky, middle; // realistic hands: where the grip is and which way the blade leaves the fist
            public Transform foot; // realistic staff: its bottom end
            public string prop;    // the cartoon pack's weapon, when nothing better is held
            public bool sword;
        }

        /// <summary>True when the heroes are the realistic Quaternius bodies (no cartoon ink outline).</summary>
        public bool IsRealistic => _swordBody != null && _swordBody.profile.realistic;

        PlayerMotor _motor;
        PlayerCombat _combat;
        PlayerVitals _vitals;
        Health _health;
        Body _swordBody, _mageBody, _active;
        string _forgedSword = "", _forgedStaff = ""; // forged weapons in each class's hands ("" = plain)
        float _armWeight, _airTime;
        bool _wasDodging;

        /// <summary>
        /// Builds both class bodies under <paramref name="visual"/> and fills the two weapon pivots with
        /// real weapon models. Returns null (and builds nothing) when the art is not in the project.
        /// </summary>
        public static PlayerAvatar TryBuild(GameObject player, Transform visual, Transform swordPivot, Transform staffPivot)
        {
            if (!ArtLibrary.HasCharacters) return null;
            var av = player.AddComponent<PlayerAvatar>();
            // Forged weapons are yours: bot Sealers carry the plain steel sword and carved staff.
            var botMotor = player.GetComponent<PlayerMotor>();
            bool isBot = botMotor != null && botMotor.isBot;
            av._forgedSword = isBot ? "" : Sealbound.Loot.Inventory.EquippedWeapon(true);
            av._forgedStaff = isBot ? "" : Sealbound.Loot.Inventory.EquippedWeapon(false);
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
            if (av.IsRealistic)
            {
                // A soft key on the face, like a film's fill light: without it the face sits in the
                // hood's (and its own) shadow and reads as a black hole under a bright sky.
                var fill = new GameObject("Face Fill Light").AddComponent<Light>();
                fill.transform.SetParent(visual, false);
                fill.transform.localPosition = new Vector3(0.15f, av.modelHeight * 0.92f, 0.7f);
                fill.type = LightType.Point;
                fill.color = new Color(1f, 0.93f, 0.85f);
                fill.range = 1.5f;
                fill.intensity = 1.1f;
                fill.shadows = LightShadows.None;
            }
            return av;
        }

        Body BuildBody(string className, Transform visual, Transform pivot, string weaponProp)
        {
            bool sword = className == "Knight";
            var b = HeroBodies.Has ? BuildRealisticBody(sword, visual) : null;
            if (b == null)
            {
                var go = ArtLibrary.Spawn(ArtLibrary.Characters + className, visual, ArtLibrary.ClassTexture(className));
                if (go == null) return null;
                go.name = className + " (KayKit)";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                b = new Body { root = go, profile = HeroBodies.KayKit };
                b.scale = ArtLibrary.FitHeight(go, modelHeight);
                b.anim = CharacterAnimator.Attach(go, ArtLibrary.AllClips());
            }
            Shapes.SetLayerRecursive(b.root, 2);

            var t = b.root.transform;
            var pr = b.profile;
            b.upperArm = ArtLibrary.FindDeep(t, pr.upperArmR);
            b.lowerArm = ArtLibrary.FindDeep(t, pr.lowerArmR);
            b.handSlot = ArtLibrary.FindDeep(t, pr.handR);
            b.chest = ArtLibrary.FindDeep(t, pr.chest);
            if (b.handSlot == null) b.handSlot = ArtLibrary.FindDeep(t, "hand.r");
            if (pr.realistic)
            {
                b.index = ArtLibrary.FindDeep(t, "index_01_r");
                b.pinky = ArtLibrary.FindDeep(t, "pinky_01_r");
                b.middle = ArtLibrary.FindDeep(t, "middle_01_r");
            }
            b.anim.SetBase(sword ? pr.combatIdle : pr.idle);

            b.pivot = pivot;
            b.prop = weaponProp;
            b.sword = sword;
            MakeWeapon(b);
            return b;
        }

        /// <summary>The Quaternius hero: the Sword recruit with the hood up, the Mage with the hood down.</summary>
        Body BuildRealisticBody(bool sword, Transform visual)
        {
            var go = HeroBodies.Build(visual, female: !sword, hood: sword, outfitColour: sword ? "T_Ranger_BaseColor" : "T_Ranger_3_BaseColor");
            if (go == null) return null;
            go.name = sword ? "Sword Sealer (Quaternius)" : "Mage Sealer (Quaternius)";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // these models are made facing the other way
            var b = new Body { root = go, profile = HeroBodies.Universal };
            b.scale = ArtLibrary.FitHeight(go, sword ? modelHeight : modelHeight * 0.97f);
            b.anim = CharacterAnimator.Attach(go, HeroBodies.Clips());
            return b;
        }

        /// <summary>Builds (or rebuilds) the weapon a body holds: a forged one if equipped, else the plain one.</summary>
        void MakeWeapon(Body b)
        {
            if (b.weapon != null)
            {
                b.weapon.SetParent(null, false); // out of the pivot now, so nothing finds its parts this frame
                Destroy(b.weapon.gameObject);
                b.weapon = null;
            }
            string forged = b.sword ? _forgedSword : _forgedStaff;
            bool metres = b.profile.realistic || !string.IsNullOrEmpty(forged);
            // Weapons sized in metres (a 1 m sword, a 1.7 m staff); cartoon props at the pack's own scale.
            float length = metres ? (b.sword ? 1.05f : 1.7f) : 0f;
            b.weapon = BuildWeapon(b.pivot, b.prop, b.root.transform.localScale.x, b.sword, length, b.profile.realistic, forged, out b.align);
            b.foot = b.weapon != null ? b.weapon.Find("Staff Foot") : null;
        }

        /// <summary>Puts a forged weapon ("" = the plain one) in a class's hands, and tells combat which part flashes.</summary>
        public void SetForgedWeapon(bool sword, string weaponId)
        {
            if (sword) _forgedSword = weaponId ?? ""; else _forgedStaff = weaponId ?? "";
            var b = sword ? _swordBody : _mageBody;
            if (b == null) return;
            MakeWeapon(b);
            if (b.root != null) Shapes.SetLayerRecursive(b.pivot.gameObject, 2);
            if (_combat != null)
            {
                if (sword) _combat.swordCueRenderer = CueRenderer(true);
                else _combat.staffCueRenderer = CueRenderer(false);
                _combat.RefreshWeapon();
            }
        }

        void OnWeaponChanged(bool sword, string id)
        {
            if (_motor != null && _motor.isBot) return; // the forge changes your weapon, not the bots'
            SetForgedWeapon(sword, id);
        }

        Transform BuildWeapon(Transform pivot, string prop, float scale, bool isSword, float length, bool realistic, string forged, out Quaternion align)
        {
            align = Quaternion.identity;
            // A forged golem weapon if one is equipped; otherwise realistic heroes carry a steel sword and a
            // carved staff, and cartoon heroes the pack's props.
            GameObject go;
            if (forged == Sealbound.Loot.ItemCatalog.RunestoneBlade) go = ForgedWeapons.RunestoneBlade(pivot);
            else if (forged == Sealbound.Loot.ItemCatalog.GolemCoreStaff) go = ForgedWeapons.GolemCoreStaff(pivot);
            else go = realistic ? (isSword ? ForgedWeapons.Sword(pivot) : ForgedWeapons.Staff(pivot)) : ArtLibrary.Spawn(ArtLibrary.Props + prop, pivot);
            if (go == null) return null;
            go.name = "Weapon Model";
            var t = go.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * scale;
            if (length > 0f)
            {
                t.localScale = Vector3.one;
                Bounds raw = BoneAim.LocalMeshBounds(t);
                float longest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
                scale = longest > 0.0001f ? length / longest : 1f;
                t.localScale = Vector3.one * scale;
            }

            // Find which way the blade (or staff) points in the model's own space: its longest side,
            // away from the grip at the model's origin.
            Bounds lb = BoneAim.LocalMeshBounds(t);
            Vector3 size = lb.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            float max = lb.max[axis], min = lb.min[axis];
            Vector3 dir = Vector3.zero;
            dir[axis] = Mathf.Abs(max) >= Mathf.Abs(min) ? 1f : -1f;
            float reach = Mathf.Max(Mathf.Abs(max), Mathf.Abs(min));
            align = Quaternion.FromToRotation(dir, Vector3.forward);
            Vector3 tipLocal = dir * reach;

            if (isSword)
            {
                var tip = new GameObject("Blade Tip").transform;
                tip.SetParent(t, false);
                tip.localPosition = tipLocal;
                var trail = tip.gameObject.AddComponent<TrailRenderer>();
                // A soft, thin streak of light that fades fast: a swoosh, not a sail.
                trail.time = 0.09f;
                trail.minVertexDistance = 0.04f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(0.4f, 0.08f), new Keyframe(1f, 0f));
                trail.sharedMaterial = Sealbound.Combat.Glow.TrailMaterial();
                bool forgedRune = forged == Sealbound.Loot.ItemCatalog.RunestoneBlade;
                Color tint = forgedRune ? new Color(0.35f, 0.7f, 1f) : new Color(0.75f, 0.85f, 1f);
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 0.5f) },
                    new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.25f, 0.5f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.emitting = false;
            }
            else
            {
                // A glowing ember at the staff's head: the Mage's beat cue.
                float orbSize = realistic ? 0.095f : 0.16f;
                var socket = t.Find("Core Socket"); // a forged staff holds its ember inside the core
                Vector3 orbAt = socket != null ? socket.localPosition : tipLocal + dir * ((realistic ? 0.035f : 0.06f) / scale);
                var orb = Shapes.Make(PrimitiveType.Sphere, t, "Ember Orb", orbAt, Vector3.one * ((socket != null ? 0.07f : orbSize) / scale),
                    new Color(1f, 0.5f, 0.18f));
                orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (realistic) Sealbound.Combat.Glow.Make(orb.transform, Vector3.zero, 4.2f, new Color(2.4f, 1.05f, 0.35f, 0.75f)); // a soft halo round the ember
                if (realistic || socket != null)
                {
                    // Where the staff's foot is, so at rest it can stand on the ground.
                    var foot = new GameObject("Staff Foot").transform;
                    foot.SetParent(t, false);
                    Vector3 footLocal = Vector3.zero;
                    footLocal[axis] = dir[axis] > 0f ? min : max; // the end opposite the tip
                    foot.localPosition = footLocal;
                }
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
            var channel = b.weapon.Find("Rune Channel"); // the forged blade flashes its rune, not its stone
            if (channel != null) return channel.GetComponent<Renderer>();
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
            Sealbound.Loot.Forge.WeaponChanged += OnWeaponChanged;
            if (_vitals != null)
            {
                _vitals.Fell += () => { if (_active != null) _active.anim.PlayOnce(_active.profile.death, 1f, 0.08f, true); };
                _vitals.Respawned += () =>
                {
                    if (_active == null) return;
                    _active.anim.StopOnce(0.05f);
                    _active.anim.PlayOnce(_active.profile.respawn, 1.6f, 0.05f);
                };
            }
        }

        void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            Sealbound.Loot.Forge.WeaponChanged -= OnWeaponChanged;
        }

        void OnDamaged(DamageInfo info, float dealt)
        {
            if (_active == null || dealt <= 0f || (_vitals != null && _vitals.IsDown)) return;
            _active.anim.PlayOnce(Random.value < 0.5f ? _active.profile.hitA : _active.profile.hitB, 1.8f, 0.05f);
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
            var pr = _active.profile;
            float dt = Time.deltaTime;
            float speed = _motor.PlanarVelocity.magnitude;
            bool grounded = _motor.IsGrounded;

            if (!grounded) _airTime += dt;
            else
            {
                if (_airTime > 0.45f && speed < 2f && !_combat.IsAttacking) a.PlayOnce(pr.jumpLand, 2.2f, 0.05f);
                _airTime = 0f;
            }

            // A real roll for the dodge when the body has one.
            bool dodging = _motor.IsDodging;
            if (dodging && !_wasDodging && pr.dodge != null && a.Has(pr.dodge))
                a.PlayOnce(pr.dodge, a.Length(pr.dodge) / 0.5f, 0.04f);
            _wasDodging = dodging;

            float walkRef = pr.realistic ? pr.walkSpeed : walkClipSpeed;
            float runRef = pr.realistic ? pr.runSpeed : runClipSpeed;
            if ((dodging && pr.dodge == null) || _motor.IsDashing)
                a.SetBase(pr.sprint, 2.4f, 0.05f);
            else if (!grounded && _airTime > 0.08f)
                a.SetBase(pr.jumpLoop, 1f, 0.12f);
            else if (speed > 0.5f)
            {
                if (speed < 3.2f) a.SetBase(pr.walk, Mathf.Clamp(speed / walkRef, 0.6f, 1.8f), 0.12f);
                else if (_motor.IsSprinting && pr.realistic) a.SetBase(pr.sprint, Mathf.Clamp(speed / pr.sprintSpeed, 0.8f, 1.8f), 0.1f);
                else a.SetBase(_motor.IsSprinting ? pr.sprint : pr.run, Mathf.Clamp(speed / runRef, 0.8f, 2.4f), 0.1f);
            }
            else a.SetBase(_active == _swordBody ? pr.combatIdle : pr.idle, 1f, 0.15f);
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
            Vector3 grip = b.handSlot.position;
            Quaternion held = b.handSlot.rotation;
            if (b.profile.realistic && b.index != null && b.pinky != null && b.middle != null)
            {
                // In a fist the grip sits in the palm and the blade leaves on the thumb side, across the knuckles.
                Vector3 fingers = (b.middle.position - b.handSlot.position);
                grip = b.handSlot.position + fingers * 0.75f;
                Vector3 across = (b.index.position - b.pinky.position).normalized;
                held = Quaternion.LookRotation(across, fingers.normalized) * b.align;
                if (b == _mageBody)
                {
                    // At rest the staff stands upright at her side, leaning a touch forward and out,
                    // rather than lying along the knuckles like a sword.
                    Transform vis = _motor != null && _motor.visual != null ? _motor.visual : transform;
                    Vector3 upright = (vis.up + vis.forward * 0.12f + vis.right * 0.06f).normalized;
                    held = Quaternion.LookRotation(upright, -vis.forward) * b.align;
                }
            }
            b.weapon.position = grip;
            b.weapon.rotation = _armWeight >= 0.999f ? swing : Quaternion.Slerp(held, swing, _armWeight);
            if (b.foot != null && _armWeight < 0.999f)
            {
                // At rest the hand slides up the shaft so the staff's foot stands on the ground.
                Transform vis = _motor != null && _motor.visual != null ? _motor.visual : transform;
                Vector3 axis = (b.weapon.position - b.foot.position).normalized;
                if (axis.y > 0.5f)
                {
                    float drop = Mathf.Clamp(vis.position.y + 0.02f - b.foot.position.y, -0.9f, 0f);
                    b.weapon.position += axis * (drop / axis.y) * (1f - _armWeight);
                }
            }
        }
    }
}
