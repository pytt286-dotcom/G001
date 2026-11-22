using System.Collections.Generic;
using Sealbound.Enemies;
using Sealbound.Loot;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.UI
{
    /// <summary>
    /// The loot side of the HUD: the "hold G to seal" prompt and its progress, a feed of items picked up,
    /// and the inventory (I / d-pad down).
    /// </summary>
    public class LootHud : MonoBehaviour
    {
        public PlayerInputReader input;
        public bool inventoryOpen;

        struct Line { public ItemDef Item; public int Count, Total; public float Born; }
        readonly List<Line> _feed = new List<Line>();
        GUIStyle _label, _center, _small;
        Texture2D _white;

        const float FeedLife = 4.5f;

        void Awake() => _white = Texture2D.whiteTexture;
        void OnEnable() => Inventory.Added += OnAdded;
        void OnDisable() => Inventory.Added -= OnAdded;

        void OnAdded(ItemDef item, int count, int total)
        {
            // Stack repeats of the same item that arrive close together (essence comes in motes).
            for (int i = _feed.Count - 1; i >= 0; i--)
                if (_feed[i].Item == item && Time.unscaledTime - _feed[i].Born < 2f)
                {
                    var l = _feed[i];
                    l.Count += count; l.Total = total; l.Born = Time.unscaledTime;
                    _feed[i] = l;
                    return;
                }
            _feed.Add(new Line { Item = item, Count = count, Total = total, Born = Time.unscaledTime });
            if (_feed.Count > 6) _feed.RemoveAt(0);
        }

        void Update()
        {
            if (input != null && input.InventoryPressed) inventoryOpen = !inventoryOpen;
            _feed.RemoveAll(l => Time.unscaledTime - l.Born > FeedLife);
        }

        void OnGUI()
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 17, richText = true };
                _label.normal.textColor = Color.white;
                _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                _small = new GUIStyle(_label) { fontSize = 13, wordWrap = true };
                _small.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
            DrawSealPrompt();
            DrawFeed();
            if (inventoryOpen) DrawInventory();
        }

        void DrawSealPrompt()
        {
            var seal = GolemSeal.Current;
            if (seal == null) return;
            float w = 460f, h = 40f, x = (Screen.width - w) / 2f, y = Screen.height * 0.64f;
            Rect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.55f));
            string text = !seal.InReach ? "<b>The golem kneels. Get close to seal it.</b>"
                        : seal.Holding ? "<b>SEALING...</b>"
                        : "<b>Hold  G  (d-pad up) to SEAL the golem</b>";
            GUI.Label(new Rect(x, y, w, h), text, _center);
            if (seal.Progress > 0f)
            {
                Rect(new Rect(x, y + h, w, 8f), new Color(0f, 0f, 0f, 0.6f));
                Rect(new Rect(x, y + h, w * seal.Progress, 8f), new Color(1f, 0.82f, 0.35f));
            }
        }

        void DrawFeed()
        {
            float w = 330f, h = 30f, x = Screen.width - w - 24f, y = Screen.height * 0.42f;
            for (int i = 0; i < _feed.Count; i++)
            {
                var l = _feed[i];
                float age = Time.unscaledTime - l.Born;
                float a = Mathf.Clamp01((FeedLife - age) / 0.8f) * Mathf.Clamp01(age / 0.15f);
                float slide = (1f - Mathf.Clamp01(age / 0.2f)) * 40f;
                var r = new Rect(x + slide, y + i * (h + 6f), w, h);
                Rect(r, new Color(0f, 0f, 0f, 0.55f * a));
                Color rc = l.Item.RarityColour;
                Rect(new Rect(r.x, r.y, 5f, r.height), new Color(rc.r, rc.g, rc.b, a));
                var st = new GUIStyle(_label) { fontSize = 16 };
                st.normal.textColor = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(r.x + 14f, r.y + 4f, w - 18f, h), $"<b><color=#{ColorUtility.ToHtmlStringRGB(rc)}>{l.Item.displayName}</color></b>  +{l.Count}   <size=13>({l.Total})</size>", st);
            }
        }

        void DrawInventory()
        {
            var owned = Inventory.Owned();
            float w = Mathf.Min(560f, Screen.width - 40f), rowH = 62f;
            float h = 110f + Mathf.Max(1, owned.Count) * rowH + 70f;
            float x = (Screen.width - w) / 2f, y = Mathf.Max(20f, (Screen.height - h) / 2f);
            Rect(new Rect(x, y, w, h), new Color(0.03f, 0.03f, 0.05f, 0.88f));
            Rect(new Rect(x, y, w, 3f), new Color(1f, 0.82f, 0.35f, 0.9f));
            var title = new GUIStyle(_center) { fontSize = 24 };
            GUI.Label(new Rect(x, y + 10f, w, 34f), "<b>SEALER'S PACK</b>", title);
            GUI.Label(new Rect(x, y + 42f, w, 22f), $"<size=14>Creatures sealed: {Inventory.Hunts}     Close: I / d-pad down</size>", new GUIStyle(_center) { fontSize = 14 });

            float ry = y + 76f;
            if (owned.Count == 0)
            {
                GUI.Label(new Rect(x + 24f, ry, w - 48f, rowH), "Nothing yet. Seal goblins and the golem to fill your pack.", _small);
                ry += rowH;
            }
            foreach (var kv in owned)
            {
                var def = kv.Key;
                Color rc = def.RarityColour;
                Rect(new Rect(x + 16f, ry, w - 32f, rowH - 8f), new Color(1f, 1f, 1f, 0.05f));
                Rect(new Rect(x + 16f, ry, 5f, rowH - 8f), rc);
                Rect(new Rect(x + 32f, ry + 12f, 30f, 30f), new Color(def.glow.r, def.glow.g, def.glow.b, 0.9f));
                GUI.Label(new Rect(x + 74f, ry + 4f, w - 200f, 24f),
                    $"<b>{def.displayName}</b>  <size=13><color=#{ColorUtility.ToHtmlStringRGB(rc)}>{def.rarity}</color></size>", _label);
                GUI.Label(new Rect(x + 74f, ry + 26f, w - 190f, 32f), def.description, new GUIStyle(_small) { fontSize = 12 });
                var count = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, fontSize = 22 };
                GUI.Label(new Rect(x + w - 130f, ry + 4f, 100f, rowH - 16f), $"<b>x{kv.Value}</b>", count);
                ry += rowH;
            }

            // How close the golem's spirit is.
            int failed = Inventory.FailedSpiritRolls(LootTables.GolemName);
            int shards = Inventory.Count(ItemCatalog.GolemSpiritShard);
            string spirit = Inventory.Count(ItemCatalog.GolemSpirit) > 0 ? "You hold the Golem Spirit. Bind it at the soul shrine (coming soon)." :
                $"Golem Spirit: {LootTables.GolemSpiritChance(failed) * 100f:0}% chance on your next seal  ·  shards {shards}/{ItemCatalog.ShardsPerSpirit}";
            Rect(new Rect(x + 16f, ry + 6f, w - 32f, 44f), new Color(0.6f, 0.75f, 1f, 0.08f));
            GUI.Label(new Rect(x + 28f, ry + 10f, w - 56f, 40f), spirit, new GUIStyle(_small) { fontSize = 14 });
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
