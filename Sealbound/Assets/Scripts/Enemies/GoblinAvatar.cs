using System.Collections.Generic;
using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// A goblin's real body. No free goblin model exists, so this reshapes a KayKit adventurer:
    /// small, green-skinned, a bigger head and long pointed ears, a dagger for Burrowers and
    /// hand-thrown rocks for Slingers. It plays the shared adventurer animations and bends the
    /// arm for the dagger swing and the rock throw.
    /// </summary>
    public class GoblinAvatar : MonoBehaviour
    {
        public float headScale = 1.18f;
        public float runClipSpeed = 3f;
        public float walkClipSpeed = 1.2f;

        Goblin _goblin;
        CharacterAnimator _anim;
        Transform _upperArm, _lowerArm, _handSlot, _head;
        GameObject _rock;
        Goblin.State _lastState = (Goblin.State)(-1);
        Vector3 _lastPos;
        float _armWeight;

        static readonly Dictionary<string, Texture2D> Repainted = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Repainted.Clear();

        /// <summary>Builds the body under <paramref name="visual"/>. Returns the renderers to tint and outline, or null without the art.</summary>
        public static List<Renderer> TryBuild(Goblin goblin, Transform visual, Goblin.Kind kind)
        {
            if (!ArtLibrary.HasCharacters) return null;

            bool burrower = kind == Goblin.Kind.Burrower;
            string cls = burrower ? "Barbarian" : "Rogue_Hooded";
            string sheetName = burrower ? "barbarian" : "rogue";
            float hue = burrower ? 0.26f : 0.21f; // fresh green for Burrowers, olive for Slingers
            const float satScale = 1.25f, valScale = 0.72f;

            var sheet = GoblinSheet(sheetName, hue, satScale, valScale);
            var go = ArtLibrary.Spawn(ArtLibrary.Characters + cls, visual, sheet);
            if (go == null) return null;
            go.name = "Goblin body (KayKit " + cls + ")";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Goblins don't wear the barbarian's bear hat.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("BearHat")) r.gameObject.SetActive(false);

            float bodyScale = go.transform.localScale.x * ArtLibrary.FitHeight(go, burrower ? 1.15f : 1.08f);

            var av = goblin.gameObject.AddComponent<GoblinAvatar>();
            av._goblin = goblin;
            var t = go.transform;
            av._upperArm = ArtLibrary.FindDeep(t, "upperarm.r");
            av._lowerArm = ArtLibrary.FindDeep(t, "lowerarm.r");
            av._handSlot = ArtLibrary.FindDeep(t, "handslot.r");
            av._head = ArtLibrary.FindDeep(t, "head");

            Color skin = ArtLibrary.RepaintedSkinColor(hue, satScale, valScale);
            av.AddEars(go, skin);

            if (burrower)
            {
                var dagger = ArtLibrary.Spawn(ArtLibrary.Props + "dagger", av._handSlot);
                if (dagger != null)
                {
                    dagger.transform.localPosition = Vector3.zero;
                    dagger.transform.localRotation = Quaternion.identity;
                    // Bones can carry their own import scale, so size the dagger to match the body, not the bone.
                    float boneScale = Mathf.Max(0.0001f, av._handSlot.lossyScale.x / Mathf.Max(0.0001f, visual.lossyScale.x));
                    dagger.transform.localScale = Vector3.one * (bodyScale / boneScale) * 1.15f;
                }
            }
            else if (av._handSlot != null)
            {
                av._rock = Shapes.Make(PrimitiveType.Sphere, null, "Sling Rock", Vector3.zero, Vector3.one * 0.22f, new Color(0.45f, 0.42f, 0.38f));
                av._rock.transform.SetParent(av._handSlot, false);
                av._rock.transform.localScale = Vector3.one * (0.22f / Mathf.Max(0.01f, av._handSlot.lossyScale.x));
                av._rock.SetActive(false);
            }

            av._anim = CharacterAnimator.Attach(go, ArtLibrary.AllClips());
            av._anim.SetBase("Idle_A");

            var list = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.activeSelf && !SkinnedOutline.IsOutline(r)) list.Add(r);
            return list;
        }

        static Texture2D GoblinSheet(string sheetName, float hue, float sat, float val)
        {
            string key = sheetName + hue;
            if (!Repainted.TryGetValue(key, out var tex) || tex == null)
            {
                tex = ArtLibrary.RepaintSkin(ArtLibrary.ClassTexture(sheetName), hue, sat, val);
                Repainted[key] = tex;
            }
            return tex;
        }

        void AddEars(GameObject body, Color skin)
        {
            if (_head == null) return;
            Renderer headRenderer = null;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                if (r.name.EndsWith("_Head")) { headRenderer = r; break; }
            Bounds hb = headRenderer != null ? headRenderer.bounds : new Bounds(_head.position + Vector3.up * 0.2f, Vector3.one * 0.4f);

            var mesh = EarMesh();
            Transform root = body.transform;
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = new GameObject(side < 0 ? "Ear L" : "Ear R");
                ear.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = ear.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Shapes.Material(skin, false);
                ear.layer = body.layer;

                Vector3 right = root.right * side;
                Vector3 pos = hb.center + right * hb.extents.x * 0.85f + root.up * hb.extents.y * 0.12f - root.forward * hb.extents.z * 0.1f;
                Vector3 dir = (right + root.up * 0.35f - root.forward * 0.3f).normalized;
                ear.transform.position = pos;
                ear.transform.rotation = Quaternion.LookRotation(dir, root.up);
                float len = hb.extents.x * 1.1f;
                ear.transform.localScale = new Vector3(hb.extents.y * 0.1f, hb.extents.y * 0.32f, len); // thin front to back, like an elf's ear
                ear.transform.SetParent(_head, true);
            }
        }

        /// <summary>A flattened cone: wide at the head, pointed at the tip.</summary>
        static Mesh _earMesh;
        static Mesh EarMesh()
        {
            if (_earMesh != null) return _earMesh;
            const int n = 10;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            verts.Add(new Vector3(0f, 0f, 1f)); // tip
            verts.Add(Vector3.zero);            // base centre
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % n;
                // Clockwise as seen from outside, so the faces point outwards.
                tris.Add(0); tris.Add(a); tris.Add(b);
                tris.Add(1); tris.Add(b); tris.Add(a);
            }
            _earMesh = new Mesh { name = "Goblin Ear" };
            _earMesh.SetVertices(verts);
            _earMesh.SetTriangles(tris, 0);
            _earMesh.RecalculateNormals();
            _earMesh.RecalculateBounds();
            return _earMesh;
        }

        void Start() => _lastPos = transform.position;

        void Update()
        {
            if (_anim == null || _goblin == null) return;
            float dt = Time.deltaTime;
            var state = _goblin.CurrentState;
            bool entered = state != _lastState;
            _lastState = state;

            Vector3 delta = transform.position - _lastPos; delta.y = 0f;
            _lastPos = transform.position;
            float speed = dt > 0f ? delta.magnitude / dt : 0f;

            switch (state)
            {
                case Goblin.State.Emerging:
                    if (entered) _anim.PlayOnce("Spawn_Ground", _anim.Length("Spawn_Ground") / 0.6f, 0.02f);
                    break;
                case Goblin.State.Windup:
                    if (entered && _goblin.kind == Goblin.Kind.Slinger)
                    {
                        // Time the throw so the rock leaves the hand when the wind-up ends.
                        float release = _anim.Length("Throw") * 0.5f;
                        _anim.PlayOnce("Throw", release / Mathf.Max(0.1f, _goblin.windupTime), 0.06f);
                        if (_rock != null) _rock.SetActive(true);
                    }
                    break;
                case Goblin.State.Strike:
                    if (_rock != null) _rock.SetActive(false);
                    break;
                case Goblin.State.Stagger:
                    if (entered) _anim.PlayOnce(Random.value < 0.5f ? "Hit_A" : "Hit_B", 1.7f, 0.04f);
                    break;
                case Goblin.State.Dead:
                    if (entered)
                    {
                        if (_rock != null) _rock.SetActive(false);
                        _anim.PlayOnce("Death_A", 1.3f, 0.05f, true);
                    }
                    break;
            }

            if (state == Goblin.State.Airborne) _anim.SetBase("Jump_Idle", 1f, 0.08f);
            else if (speed > 4f) _anim.SetBase("Running_A", Mathf.Clamp(speed / runClipSpeed, 0.9f, 2.6f), 0.1f);
            else if (speed > 0.4f) _anim.SetBase("Walking_A", Mathf.Clamp(speed / walkClipSpeed, 0.7f, 2.2f), 0.12f);
            else _anim.SetBase("Idle_A", 1f, 0.15f);
        }

        void LateUpdate()
        {
            if (_head != null) _head.localScale = Vector3.one * headScale;
            if (_goblin == null || _goblin.kind != Goblin.Kind.Burrower || _upperArm == null) return;

            // Burrower dagger: raise it high in the wind-up, stab forward on the strike.
            float dt = Time.deltaTime;
            var state = _goblin.CurrentState;
            Transform t = transform;
            Vector3 dir;
            float target;
            switch (state)
            {
                case Goblin.State.Windup:
                    dir = (t.up * 0.9f - t.forward * 0.35f + t.right * 0.25f).normalized;
                    target = 1f;
                    break;
                case Goblin.State.Strike:
                    dir = (t.forward - t.up * 0.15f).normalized;
                    target = 1f;
                    break;
                case Goblin.State.Recover:
                    dir = (t.forward - t.up * 0.3f).normalized;
                    target = 0f;
                    break;
                default:
                    dir = t.forward;
                    target = 0f;
                    break;
            }
            float rate = state == Goblin.State.Strike ? 30f : state == Goblin.State.Windup ? 8f : 4f;
            _armWeight = Mathf.MoveTowards(_armWeight, target, rate * dt);
            if (_armWeight > 0.001f) BoneAim.AimArm(_upperArm, _lowerArm, _handSlot, dir, _armWeight);
        }
    }
}
