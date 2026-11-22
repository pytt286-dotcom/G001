using Sealbound.Player;
using UnityEngine;

namespace Sealbound.UI
{
    /// <summary>Temporary on-screen stamina bar and control hints for the test field.</summary>
    public class DebugHud : MonoBehaviour
    {
        public PlayerMotor motor;
        public bool showControls = true;

        Texture2D _white;
        GUIStyle _label;

        void Awake()
        {
            _white = Texture2D.whiteTexture;
        }

        void Update()
        {
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
            }

            // Stamina bar
            float w = 260f, h = 14f, x = 24f, y = Screen.height - 44f;
            DrawRect(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0f, 0f, 0f, 0.55f));
            float pct = motor.Stamina / motor.maxStamina;
            DrawRect(new Rect(x, y, w * pct, h), Color.Lerp(new Color(0.9f, 0.3f, 0.2f), new Color(0.35f, 0.85f, 0.45f), pct));
            GUI.Label(new Rect(x, y - 24f, 200f, 22f), "<b>Stamina</b>", _label);

            if (!showControls) return;
            string text =
                "<b>SEALBOUND · Test Field</b>\n" +
                "Move: WASD / left stick\n" +
                "Look: mouse / right stick\n" +
                "Sprint: hold Shift / L3\n" +
                "Jump: Space / A (hold for higher)\n" +
                "Dodge: Ctrl, C or right-click / B\n" +
                "Free mouse: Esc · Recapture: click\n" +
                "Hide this help: H";
            DrawRect(new Rect(16f, 16f, 330f, 196f), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(28f, 24f, 320f, 190f), text, _label);
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
