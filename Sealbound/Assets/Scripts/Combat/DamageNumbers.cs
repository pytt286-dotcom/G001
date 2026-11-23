using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>Floating damage numbers drawn above whatever was hit.</summary>
    public class DamageNumbers : MonoBehaviour
    {
        struct Entry
        {
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float Born;
            public float Size;
        }

        static DamageNumbers _instance;
        readonly List<Entry> _entries = new List<Entry>();
        GUIStyle _style, _shadow;
        const float Life = 0.8f;

        public static void Show(Vector3 worldPos, float amount, bool big, bool armoured)
        {
            if (_instance == null) _instance = new GameObject("Damage Numbers").AddComponent<DamageNumbers>();
            var color = armoured ? new Color(0.7f, 0.75f, 0.85f) : big ? new Color(1f, 0.75f, 0.2f) : Color.white;
            _instance._entries.Add(new Entry
            {
                WorldPos = worldPos + Random.insideUnitSphere * 0.3f,
                Text = Mathf.RoundToInt(amount).ToString(),
                Color = color,
                Born = Time.unscaledTime,
                Size = big ? 34f : 24f
            });
        }

        void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                float age = Time.unscaledTime - e.Born;
                if (age > Life) { _entries.RemoveAt(i); continue; }

                Vector3 sp = cam.WorldToScreenPoint(e.WorldPos + Vector3.up * (age * 1.5f));
                if (sp.z < 0f) continue;
                float pop = age < 0.08f ? Mathf.Lerp(1.6f, 1f, age / 0.08f) : 1f;
                int size = Mathf.RoundToInt(e.Size * pop);
                _style.fontSize = size; _shadow.fontSize = size;
                var c = e.Color; c.a = 1f - Mathf.Clamp01((age - Life * 0.6f) / (Life * 0.4f));
                _style.normal.textColor = c;
                var r = new Rect(sp.x - 80f, Screen.height - sp.y - 25f, 160f, 50f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), e.Text, _shadow);
                GUI.Label(r, e.Text, _style);
            }
        }
    }
}
