using Sealbound.Core;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// A red marking on the ground that shows where a big attack will land. A faint outline appears first,
    /// then a brighter fill grows inside it; when the fill reaches the edge, the attack lands.
    /// </summary>
    public class GroundWarning : MonoBehaviour
    {
        static readonly Color Edge = new Color(0.9f, 0.12f, 0.06f);
        static readonly Color Fill = new Color(1f, 0.3f, 0.1f);

        Transform _fill;
        float _age, _duration;
        bool _lane;
        Vector3 _fullSize;

        // ---- every warning on the ground right now, so bot Sealers can step out of them ----
        static readonly System.Collections.Generic.List<GroundWarning> _active = new System.Collections.Generic.List<GroundWarning>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _active.Clear();

        void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
        void OnDisable() => _active.Remove(this);

        /// <summary>Seconds until the attack lands.</summary>
        public float TimeLeft => _duration - _age;
        /// <summary>How many red warnings are on the ground right now.</summary>
        public static int ActiveCount => _active.Count;

        /// <summary>Is <paramref name="p"/> inside this warning (plus a margin)? If so, which way is the quickest way out.</summary>
        public bool Contains(Vector3 p, float margin, out Vector3 escape)
        {
            escape = Vector3.zero;
            Vector3 rel = p - transform.position; rel.y = 0f;
            if (_lane)
            {
                Vector3 dir = transform.forward, right = transform.right;
                float along = Vector3.Dot(rel, dir), side = Vector3.Dot(rel, right);
                if (along < -margin || along > _fullSize.z + margin || Mathf.Abs(side) > _fullSize.x * 0.5f + margin) return false;
                escape = right * (side >= 0f ? 1f : -1f); // sideways out of a charge
                return true;
            }
            float r = _fullSize.x * 0.5f + margin;
            if (rel.sqrMagnitude > r * r) return false;
            escape = rel.sqrMagnitude > 0.04f ? rel.normalized : transform.right;
            return true;
        }

        /// <summary>The soonest warning that <paramref name="p"/> stands in, if any.</summary>
        public static bool Danger(Vector3 p, float margin, out Vector3 escape, out float timeLeft)
        {
            escape = Vector3.zero;
            timeLeft = float.MaxValue;
            bool found = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var w = _active[i];
                if (w == null) { _active.RemoveAt(i); continue; }
                if (w.TimeLeft < -0.05f || !w.Contains(p, margin, out var e)) continue;
                if (w.TimeLeft < timeLeft) { timeLeft = w.TimeLeft; escape = e; found = true; }
            }
            return found;
        }

        /// <summary>A circle warning centred on <paramref name="at"/>.</summary>
        public static GroundWarning Circle(Vector3 at, float radius, float duration)
        {
            var go = new GameObject("Ground Warning");
            go.transform.position = new Vector3(at.x, 0.06f, at.z);
            Shapes.Make(PrimitiveType.Cylinder, go.transform, "Edge", Vector3.zero, new Vector3(radius * 2f, 0.004f, radius * 2f), Edge * 0.55f, false, false);
            var w = go.AddComponent<GroundWarning>();
            w._fullSize = new Vector3(radius * 2f, 0.008f, radius * 2f);
            w._fill = Shapes.Make(PrimitiveType.Cylinder, go.transform, "Fill", Vector3.up * 0.005f, Vector3.zero, Fill, false, true).transform;
            w._duration = Mathf.Max(0.05f, duration);
            return w;
        }

        /// <summary>A straight lane warning (e.g. a charge) from <paramref name="start"/> along <paramref name="dir"/>.</summary>
        public static GroundWarning Lane(Vector3 start, Vector3 dir, float length, float width, float duration)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            var go = new GameObject("Ground Warning (lane)");
            go.transform.position = new Vector3(start.x, 0.06f, start.z);
            go.transform.rotation = Quaternion.LookRotation(dir);
            Shapes.Make(PrimitiveType.Cube, go.transform, "Edge", new Vector3(0f, 0f, length * 0.5f), new Vector3(width, 0.004f, length), Edge * 0.55f, false, false);
            var w = go.AddComponent<GroundWarning>();
            w._lane = true;
            w._fullSize = new Vector3(width, 0.008f, length);
            w._fill = Shapes.Make(PrimitiveType.Cube, go.transform, "Fill", new Vector3(0f, 0.005f, 0f), Vector3.zero, Fill, false, true).transform;
            w._duration = Mathf.Max(0.05f, duration);
            return w;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            if (_lane)
            {
                // The lane fills from the start toward the far end.
                _fill.localScale = new Vector3(_fullSize.x, _fullSize.y, _fullSize.z * t);
                _fill.localPosition = new Vector3(0f, 0.005f, _fullSize.z * t * 0.5f);
            }
            else _fill.localScale = new Vector3(_fullSize.x * t, _fullSize.y, _fullSize.z * t);
            if (_age >= _duration + 0.05f) Destroy(gameObject);
        }

        public void Cancel() => Destroy(gameObject);
    }
}
