using System.Collections.Generic;
using Sealbound.Enemies;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.UI
{
    /// <summary>Temporary HUD: stamina, focus, skill cooldowns, chain counter, timing popups and controls.</summary>
    public class DebugHud : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerCombat combat;
        public PlayerVitals vitals;
        public GoblinWarren warren;
        public bool showControls = true;

        struct Popup { public string Text; public Color Color; public float Born; }
        readonly List<Popup> _popups = new List<Popup>();

        Texture2D _white;
        GUIStyle _label, _center, _big;
        bool _subscribed;

        void Awake() => _white = Texture2D.whiteTexture;

        void Update()
        {
            if (!_subscribed && combat != null)
            {
                combat.Feedback += (t, c) =>
                {
                    _popups.Add(new Popup { Text = t, Color = c, Born = Time.unscaledTime });
                    if (_popups.Count > 3) _popups.RemoveAt(0);
                };
                _subscribed = true;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) showControls = !showControls;
        }

        void OnGUI()
        {
            if (motor == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
                _label.normal.textColor = Color.white;
                _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                _big = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
            }

            // Hurt flash around the screen edges
            if (vitals != null && vitals.HurtFlash > 0f)
            {
                var c = new Color(0.8f, 0.05f, 0.05f, vitals.HurtFlash * 0.35f);
                float t = 60f;
                DrawRect(new Rect(0, 0, Screen.width, t), c);
                DrawRect(new Rect(0, Screen.height - t, Screen.width, t), c);
                DrawRect(new Rect(0, 0, t, Screen.height), c);
                DrawRect(new Rect(Screen.width - t, 0, t, Screen.height), c);
            }

            if (warren != null)
            {
                string status = warren.StatusText;
                if (!string.IsNullOrEmpty(status))
                {
                    var st = new GUIStyle(_center) { fontSize = 20 };
                    DrawRect(new Rect(Screen.width / 2f - 330f, 14f, 660f, 34f), new Color(0f, 0f, 0f, 0.45f));
                    GUI.Label(new Rect(Screen.width / 2f - 330f, 14f, 660f, 34f), $"<b>{status}</b>", st);
                }
            }

            // Health, Stamina and Focus bars
            float w = 260f, h = 14f, x = 24f, y = Screen.height - 44f;
            if (vitals != null)
                Bar(x, y - 92f, w, h + 4f, vitals.Health.Current / vitals.Health.maxHealth, new Color(0.85f, 0.2f, 0.2f),
                    $"Health {Mathf.CeilToInt(vitals.Health.Current)}");
            Bar(x, y, w, h, motor.Stamina / motor.maxStamina, new Color(0.35f, 0.85f, 0.45f), "Stamina");
            if (combat != null)
            {
                float f = combat.Focus / combat.maxFocus;
                Bar(x, y - 46f, w, h, f, combat.FocusFull ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.65f, 0.25f),
                    combat.FocusFull ? "Focus FULL: heavy (E) = SEALBREAKER" : "Focus (perfect timing & links)");
                DrawSkills();
                DrawChain();
                DrawPopups();
            }

            if (vitals != null && vitals.IsDown)
            {
                DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 50f), "YOU FELL", _big);
                GUI.Label(new Rect(0, Screen.height * 0.4f + 50f, Screen.width, 30f),
                    $"Your squad drags you back to camp in {Mathf.Max(0f, vitals.RespawnIn):0.0}s", _center);
            }

            if (!showControls) return;
            string text =
                "<b>SEALBOUND · Test Field</b>\n" +
                "Move: WASD / left stick   Look: mouse / right stick\n" +
                "Sprint: hold Shift / L3   Jump: Space / A\n" +
                "Dodge: Ctrl, C or right-click / B\n" +
                "Attack: left-click / X\n" +
                "Skills: Q / R / F  (LB / RB / RT)\n" +
                "Heavy slam: E / Y (uses stamina)\n" +
                "<b>Rhythm:</b> press again when the blade flashes\n" +
                "and ticks. Too early breaks the combo.\n" +
                "Skill on the flash = LINK. Attack after a skill = EXTEND.\n" +
                "Dodge on the flash = EVADE (keeps the chain).\n" +
                "Free mouse: Esc · Hide this help: H";
            DrawRect(new Rect(16f, 16f, 470f, 262f), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(28f, 24f, 460f, 256f), text, _label);
        }

        void DrawSkills()
        {
            float size = 64f, gap = 10f;
            int n = combat.skills.Length;
            float total = n * size + (n - 1) * gap;
            float x0 = (Screen.width - total) / 2f, y = Screen.height - size - 24f;
            string[] keys = { "Q", "R", "F" };
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (size + gap), y, size, size);
                DrawRect(r, new Color(0f, 0f, 0f, 0.55f));
                float cd = combat.CooldownRemaining(i);
                float frac = cd / Mathf.Max(0.01f, combat.CooldownTotal(i));
                if (cd > 0f) DrawRect(new Rect(r.x, r.y + r.height * (1f - frac), r.width, r.height * frac), new Color(0.2f, 0.2f, 0.25f, 0.85f));
                else DrawRect(new Rect(r.x, r.y + r.height - 4f, r.width, 4f), new Color(0.45f, 0.85f, 1f));
                GUI.Label(new Rect(r.x, r.y + 4f, r.width, 20f), $"<b>{(i < keys.Length ? keys[i] : "?")}</b>", _center);
                GUI.Label(new Rect(r.x - 10f, r.y + 22f, r.width + 20f, 40f), combat.skills[i].name.Replace(" ", "\n"), _center);
                if (cd > 0f) GUI.Label(new Rect(r.x, r.y - 22f, r.width, 20f), cd.ToString("0.0"), _center);
            }
        }

        void DrawChain()
        {
            if (combat.Chain < 1) return;
            var style = new GUIStyle(_big) { alignment = TextAnchor.MiddleRight, fontSize = 30 };
            float pct = (combat.ChainMultiplier - 1f) * 100f;
            GUI.Label(new Rect(Screen.width - 300f, Screen.height * 0.38f, 270f, 40f), $"CHAIN x{combat.Chain + 1}", style);
            style.fontSize = 18;
            GUI.Label(new Rect(Screen.width - 300f, Screen.height * 0.38f + 34f, 270f, 26f), $"+{pct:0}% damage · {combat.ComboHits} hits", style);
        }

        void DrawPopups()
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _popups[i].Born;
                if (age > 0.7f) { _popups.RemoveAt(i); continue; }
            }
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                float age = Time.unscaledTime - p.Born;
                float pop = age < 0.08f ? Mathf.Lerp(1.5f, 1f, age / 0.08f) : 1f;
                var c = p.Color; c.a = 1f - Mathf.Clamp01((age - 0.45f) / 0.25f);
                var style = new GUIStyle(_big) { fontSize = Mathf.RoundToInt(34 * pop) };
                style.normal.textColor = c;
                int slot = _popups.Count - 1 - i;
                GUI.Label(new Rect(0f, Screen.height * 0.3f - slot * 38f - age * 30f, Screen.width, 50f), p.Text, style);
            }
        }

        void Bar(float x, float y, float w, float h, float pct, Color color, string title)
        {
            DrawRect(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0f, 0f, 0f, 0.55f));
            DrawRect(new Rect(x, y, w * Mathf.Clamp01(pct), h), color);
            GUI.Label(new Rect(x, y - 24f, 420f, 22f), $"<b>{title}</b>", _label);
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }
    }
}
