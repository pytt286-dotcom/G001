using Sealbound.Core;
using Sealbound.Enemies;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.UI
{
    /// <summary>
    /// The mission side of the HUD: the mission board, the waystone prompt, the objective tracker,
    /// the fade when travelling and the results when you come home.
    /// </summary>
    public class MissionHud : MonoBehaviour
    {
        public PlayerInputReader input;
        public bool boardOpen;

        GUIStyle _label, _center;
        Texture2D _white;

        void Awake() => _white = Texture2D.whiteTexture;

        static MissionDirector Director => MissionDirector.Instance;

        void Update()
        {
            var d = Director;
            if (d == null || input == null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool g = input.InteractPressed && GolemSeal.Current == null;

            // Results stay up until you press G or Esc, or for 20 seconds.
            if (ResultsShowing && (g || esc || Time.unscaledTime - d.ResultsShownAt > 20f)) { d.HideResults(); return; }

            var board = MissionBoard.Instance;
            bool atBoard = board != null && board.InReach;
            if (boardOpen && (!atBoard || esc || d.Current != MissionDirector.State.Idle)) boardOpen = false;
            else if (atBoard && g)
            {
                if (!boardOpen) boardOpen = true;
                else { boardOpen = false; d.SetOut(); }
                return;
            }

            var stone = Waystone.Instance;
            if (stone != null && stone.InReach && g && d.Current != MissionDirector.State.Travelling) d.ReturnHome();
        }

        bool ResultsShowing => Director != null && !string.IsNullOrEmpty(Director.Results) && Time.unscaledTime - Director.ResultsShownAt < 20f;

        void OnGUI()
        {
            var d = Director;
            if (d == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
                _label.normal.textColor = Color.white;
                _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            }

            if (d.InMission) DrawTracker(d);
            else if (d.PlayerInTown && !boardOpen && !ResultsShowing) DrawTownHint();

            var board = MissionBoard.Instance;
            if (boardOpen) DrawBoard(d);
            else if (board != null && board.InReach && d.Current == MissionDirector.State.Idle && !ResultsShowing)
                Prompt("Press  G  (d-pad up) to read the MISSION BOARD");

            var stone = Waystone.Instance;
            if (stone != null && stone.InReach && d.Current != MissionDirector.State.Travelling)
                Prompt(d.Current == MissionDirector.State.Complete ? "Press  G  (d-pad up) to go home and hand in the mission"
                    : d.Current == MissionDirector.State.Active ? "Press  G  (d-pad up) to give up the mission and go back to town"
                    : "Press  G  (d-pad up) to travel back to town");

            if (ResultsShowing) DrawResults(d);

            if (d.Fade > 0.001f) Rect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, d.Fade));
        }

        void DrawTracker(MissionDirector d)
        {
            float w = 380f, x = Screen.width - w - 16f, y = 30f;
            int secs = Mathf.FloorToInt(d.Elapsed);
            var warren = FindWarren();
            string bonus = d.WarrenCleared ? "<color=#8fe08f>✓</color> Bonus: goblin warren cleared"
                : warren != null && warren.InFight ? $"◇ Bonus: clear the goblin warren (wave {warren.Wave}/{warren.WaveCount})"
                : "◇ Bonus: clear the goblin warren (west of camp)";
            string main = d.GolemSealed ? "<color=#8fe08f>✓</color> The Bastion Golem is sealed" : "◆ Seal the Bastion Golem (the ruins, north)";
            string text = $"<b>MISSION · {MissionDirector.MissionName}</b>   {secs / 60}:{secs % 60:00}\n{main}\n{bonus}";
            float h = 74f;
            if (d.Current == MissionDirector.State.Complete)
            {
                text += "\n<color=#9fd0ff>Go home: step into the circle of light by the ruins, or touch the waystone at camp.</color>";
                h = 116f;
            }
            Rect(new Rect(x - 8f, y - 6f, w + 16f, h), new Color(0f, 0f, 0f, 0.5f));
            Rect(new Rect(x - 8f, y - 6f, 3f, h), new Color(1f, 0.82f, 0.4f, 0.9f));
            GUI.Label(new Rect(x, y, w, h), text, _label);
        }

        GoblinWarren _warren;
        GoblinWarren FindWarren()
        {
            if (_warren == null) _warren = FindAnyObjectByType<GoblinWarren>();
            return _warren;
        }

        void DrawTownHint()
        {
            float w = 620f, x = (Screen.width - w) / 2f;
            Rect(new Rect(x, 14f, w, 32f), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(x, 14f, w, 32f), "<b>TOWN SQUARE</b>  ·  The mission board stands by the gate", _center);
        }

        void DrawBoard(MissionDirector d)
        {
            float w = Mathf.Min(640f, Screen.width - 40f), h = 330f, x = (Screen.width - w) / 2f, y = Screen.height * 0.18f;
            Rect(new Rect(x, y, w, h), new Color(0.1f, 0.08f, 0.05f, 0.9f));
            Rect(new Rect(x, y, w, 4f), new Color(1f, 0.82f, 0.4f, 0.95f));
            Rect(new Rect(x + 14f, y + 16f, w - 28f, h - 30f), new Color(0.86f, 0.8f, 0.64f, 0.95f));
            var ink = new GUIStyle(_label) { fontSize = 16 };
            ink.normal.textColor = new Color(0.16f, 0.11f, 0.07f);
            var title = new GUIStyle(ink) { fontSize = 24, alignment = TextAnchor.UpperCenter };
            GUI.Label(new Rect(x + 30f, y + 26f, w - 60f, 34f), "<b>" + MissionDirector.MissionName.ToUpperInvariant() + "</b>", title);
            int bots = 0;
            foreach (var b in SquadBot.All) if (b != null) bots++;
            string body = MissionDirector.MissionBrief + "\n\n" +
                          "<b>Goal:</b> seal the Bastion Golem.\n" +
                          "<b>Bonus:</b> clear the goblin warren west of the camp.\n" +
                          $"<b>Reward:</b> {MissionDirector.Reward} Sealing Essence (+{MissionDirector.BonusReward} for the bonus), and whatever the golem drops.\n" +
                          $"<b>Squad:</b> you and {bots} bot Sealer{(bots == 1 ? "" : "s")} (B to change)";
            GUI.Label(new Rect(x + 30f, y + 66f, w - 60f, h - 120f), body, ink);
            var keys = new GUIStyle(ink) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            GUI.Label(new Rect(x + 30f, y + h - 52f, w - 60f, 30f), "<b>G (d-pad up): SET OUT</b>      Esc or walk away: close", keys);
        }

        void DrawResults(MissionDirector d)
        {
            float w = Mathf.Min(600f, Screen.width - 40f), h = 200f, x = (Screen.width - w) / 2f, y = Screen.height * 0.22f;
            Rect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.7f));
            Rect(new Rect(x, y, w, 4f), new Color(1f, 0.82f, 0.4f, 0.95f));
            var st = new GUIStyle(_label) { alignment = TextAnchor.UpperCenter, fontSize = 17 };
            GUI.Label(new Rect(x + 20f, y + 16f, w - 40f, h - 50f), d.Results, st);
            var small = new GUIStyle(_center) { fontSize = 13 };
            GUI.Label(new Rect(x, y + h - 30f, w, 24f), "G or Esc to close", small);
        }

        void Prompt(string text)
        {
            float w = Mathf.Min(760f, Screen.width - 40f), h = 40f, x = (Screen.width - w) / 2f, y = Screen.height * 0.64f;
            Rect(new Rect(x, y, w, h), new Color(0.06f, 0.05f, 0.02f, 0.62f));
            Rect(new Rect(x, y, w, 3f), new Color(1f, 0.82f, 0.4f, 0.9f));
            GUI.Label(new Rect(x, y, w, h), "<b>" + text + "</b>", _center);
        }

        void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }
    }
}
