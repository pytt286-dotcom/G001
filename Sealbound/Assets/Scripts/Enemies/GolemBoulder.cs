using Sealbound.CameraRig;
using Sealbound.Combat;
using Sealbound.Core;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// A boulder the Bastion Golem tears up and hurls (second phase). A red circle on the ground shows
    /// where it will land; get out of the circle or dodge through the impact.
    /// </summary>
    public class GolemBoulder : MonoBehaviour
    {
        public float radius = 3.2f;
        public float launch = 6f;

        Vector3 _from, _to, _velocity;
        float _t, _flight, _damage;
        GameObject _owner;
        GroundWarning _warning;
        Transform _model;
        Vector3 _spin;

        const float Gravity = 22f;

        /// <summary>A rough rock about <paramref name="size"/> metres across. Its root has scale 1.</summary>
        public static GameObject MakeRock(float size)
        {
            var root = new GameObject("Golem Boulder");
            root.layer = 2;
            GameObject rock = null;
            if (ArtLibrary.HasScenery)
                rock = ArtLibrary.Spawn(ArtLibrary.Forest + "Rock_3_C_Color1", root.transform);
            if (rock != null)
            {
                rock.transform.localPosition = Vector3.zero;
                rock.transform.localRotation = Quaternion.identity;
                var b = ArtLibrary.WorldBounds(rock);
                float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (biggest > 0.001f) rock.transform.localScale *= size / biggest;
                b = ArtLibrary.WorldBounds(rock);
                rock.transform.position -= b.center - root.transform.position; // centre it on the root
            }
            else
            {
                rock = Shapes.Make(PrimitiveType.Sphere, root.transform, "Rock", Vector3.zero, Vector3.one * size, new Color(0.42f, 0.4f, 0.37f));
            }
            foreach (var c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
            Shapes.SetLayerRecursive(root, 2);
            ToonOutline.Add(root.GetComponentsInChildren<Renderer>(), 0.06f);
            return root;
        }

        /// <summary>Throws a boulder from <paramref name="from"/> so it lands on <paramref name="target"/> after <paramref name="flightTime"/> seconds.</summary>
        public static void Throw(Vector3 from, Vector3 target, float flightTime, float damage, GameObject owner)
        {
            var rock = MakeRock(1.9f);
            rock.transform.position = from;
            var b = rock.AddComponent<GolemBoulder>();
            b._from = from;
            b._to = new Vector3(target.x, 0f, target.z);
            b._flight = Mathf.Max(0.3f, flightTime);
            b._damage = damage;
            b._owner = owner;
            b._model = rock.transform;
            b._velocity = (b._to - from) / b._flight + Vector3.up * (0.5f * Gravity * b._flight);
            b._spin = Random.onUnitSphere * 280f;
            b._warning = GroundWarning.Circle(b._to, b.radius, b._flight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            float t = Mathf.Min(_t, _flight);
            transform.position = _from + _velocity * t + Vector3.down * (0.5f * Gravity * t * t);
            _model.Rotate(_spin * dt, Space.World);
            if (_t >= _flight) Land();
        }

        void Land()
        {
            if (_warning != null) _warning.Cancel();
            Vector3 at = _to;
            DustPuff.Spawn(at, radius, 12);
            SealBurst.Spawn(at + Vector3.up * 0.3f, radius * 0.9f, new Color(0.55f, 0.48f, 0.4f));

            var motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                Vector3 p = motor.transform.position;
                Vector3 rel = p - at; rel.y = 0f;
                float dist = rel.magnitude;
                ThirdPersonCamera.Shake(Mathf.Clamp01(1.1f - dist / 30f) * 0.55f);
                var health = motor.GetComponent<Health>();
                if (health != null && !health.IsDead && dist <= radius + 0.35f && p.y < 2.5f)
                {
                    var info = new DamageInfo
                    {
                        Amount = _damage, Point = p + Vector3.up, Direction = dist > 0.1f ? rel / dist : Vector3.forward,
                        Source = _owner, Impact = 0.85f
                    };
                    if (health.TryDamage(info, out _)) motor.Launch(launch);
                    else
                    {
                        var combat = motor.GetComponent<PlayerCombat>();
                        if (combat != null) combat.NotifyDodged();
                    }
                }
            }
            Destroy(gameObject);
        }
    }
}
