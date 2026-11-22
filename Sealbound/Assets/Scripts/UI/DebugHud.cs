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
        public BastionGolem boss;
        public bool showControls = true;
        public bool showFps = true;

        struct Popup { public string Text; public Color Color; public float Born; }
        readonly List<Popup> _popups = new List<Popup>();

        Texture2D _white;
        GUIStyle _label, _center, _big;
        bool _subscribed;

        // Boss banners ("RUNE BROKEN!", "SEALED!"), and the golem the HUD is listening to.
        string _banner;
        Color _bannerColor;
        float _bannerBorn = -99f;
        BastionGolem _listening;
        float _findBossIn;

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
            // The golem is rebuilt after it is sealed, so look for the new one now and then.
            if (boss == null)
            {
                _findBossIn -= Time.unscaledDeltaTime;
                if (_findBossIn <= 0f) { boss = FindAnyObjectByType<BastionGolem>(); _findBossIn = 1f; }
            }
            if (boss != _listening)
            {
                if (_listening != null) _listening.Announce -= OnAnnounce;
                _listening = boss;
                if (_listening != null) _listening.Announce += OnAnnounce;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) showControls = !showControls;
            if (kb != null && kb.f3Key.wasPressedThisFrame) showFps = !showFps;
        }

        void OnAnnounce(string text, Color color)
        {
            _banner = text;
            _bannerColor = color;
            _bannerBorn = Time.unscaledTime;
        }

        bool BossEngaged => boss != null && boss.Engaged;

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

            var perf = Sealbound.Core.Performance.Instance;
            if (showFps && perf != null)
            {
                var fs = new GUIStyle(_label) { alignment = TextAnchor.UpperRight, fontSize = 13 };
                Color fc = perf.Fps >= 50f ? new Color(0.6f, 1f, 0.6f) : perf.Fps >= 30f ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.4f);
                fs.normal.textColor = fc;
                GUI.Label(new Rect(Screen.width - 230f, 6f, 220f, 20f), $"{perf.Fps:0} fps · resolution {perf.RenderScale * 100f:0}%  (F3)", fs);
            }

            if (BossEngaged) DrawBossBar();
            else if (boss != null) DrawBossHint();
            DrawBanner();

            if (warren != null && !BossEngaged)
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
                "Switch class (Sword / Mage): Tab / Select\n" +
                "Heavy slam: E / Y (uses stamina)\n" +
                "<b>Rhythm:</b> press again when the blade flashes\n" +
                "and ticks. Too early breaks the combo.\n" +
                "Skill on the flash = LINK. Attack after a skill = EXTEND.\n" +
                "Dodge on the flash = EVADE (keeps the chain).\n" +
                "<b>Golem</b> (ruins to the north): jump its ground wave,\n" +
                "break the glowing runes, then hit the exposed core.\n" +
                "Beaten creatures: hold G (d-pad up) to SEAL. Pack: I\n" +
                "Free mouse: Esc · Hide this help: H";
            DrawRect(new Rect(16f, 16f, 470f, 346f), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(28f, 24f, 460f, 342f), text, _label);
        }

        void DrawBossBar()
        {
            float w = Mathf.Min(720f, Screen.width - 80f), h = 18f;
            float x = (Screen.width - w) / 2f, y = 46f;
            bool p2 = boss.Phase == 2;
            DrawRect(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), new Color(0f, 0f, 0f, 0.6f));
            Color fill = p2 ? new Color(1f, 0.45f, 0.12f) : new Color(0.8f, 0.18f, 0.16f);
            DrawRect(new Rect(x, y, w * Mathf.Clamp01(boss.HealthFraction), h), fill);
            DrawRect(new Rect(x + w * 0.5f - 1f, y - 3f, 2f, h + 6f), new Color(1f, 1f, 1f, 0.7f)); // second phase mark

            var title = new GUIStyle(_center) { fontSize = 20 };
            string phase = p2 ? "  <color=#ffa040>· ENRAGED</color>" : "";
            GUI.Label(new Rect(x, y - 32f, w, 28f), $"<b>{boss.displayName.ToUpperInvariant()}</b>{phase}", title);

            // One pip per limb rune: lit while it stands, dark once broken.
            var runes = boss.LimbRunes;
            float pip = 14f, gap = 10f;
            float px = Screen.width / 2f - (runes.Count * pip + (runes.Count - 1) * gap) / 2f;
            Color lit = p2 ? new Color(1f, 0.55f, 0.2f) : new Color(0.4f, 0.85f, 1f);
            for (int i = 0; i < runes.Count; i++)
            {
                var r = new Rect(px + i * (pip + gap), y + h + 10f, pip, pip);
                DrawRect(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(0f, 0f, 0f, 0.6f));
                DrawRect(r, runes[i] != null && !runes[i].Broken ? lit : new Color(0.2f, 0.2f, 0.22f));
            }

            if (boss.CoreExposed)
            {
                var st = new GUIStyle(_center) { fontSize = 22 };
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 10f);
                st.normal.textColor = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), pulse);
                GUI.Label(new Rect(x, y + h + 30f, w, 30f), "<b>CORE EXPOSED: HIT THE CHEST!</b>", st);
            }
        }

        void DrawBossHint()
        {
            if (boss.CurrentState != BastionGolem.State.Dormant) return;
            Vector3 d = boss.arenaCentre - motor.transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > 45f) return;
            var st = new GUIStyle(_center) { fontSize = 18 };
            DrawRect(new Rect(Screen.width / 2f - 300f, 14f, 600f, 32f), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(Screen.width / 2f - 300f, 14f, 600f, 32f),
                $"<b>A Bastion Golem sleeps in the ruins ({dist:0} m). Step inside to wake it.</b>", st);
        }

        void DrawBanner()
        {
            float age = Time.unscaledTime - _bannerBorn;
            if (string.IsNullOrEmpty(_banner) || age > 2.4f) return;
            float pop = age < 0.12f ? Mathf.Lerp(1.6f, 1f, age / 0.12f) : 1f;
            var c = _bannerColor; c.a = 1f - Mathf.Clamp01((age - 1.8f) / 0.6f);
            var style = new GUIStyle(_big) { fontSize = Mathf.RoundToInt(40 * pop) };
            style.normal.textColor = c;
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.7f);
            var r = new Rect(0f, Screen.height * 0.2f, Screen.width, 60f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), _banner, shadow);
            GUI.Label(r, _banner, style);
        }

        void DrawSkills()
        {
            float size = 64f, gap = 10f;
            int n = combat.skills.Length;
            float total = n * size + (n - 1) * gap;
            float x0 = (Screen.width - total) / 2f, y = Screen.height - size - 24f;
            string[] keys = { "Q", "R", "F" };
            GUI.Label(new Rect(x0, y - 50f, total, 24f), $"<b>{combat.Kit.className.ToUpperInvariant()}</b>  <size=12>(Tab to switch)</size>", _center);
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
