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
        public bool forgeOpen;
        string _forgeNote;
        float _forgeNoteBorn = -99f;

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

            // The forge opens with G when you stand at it, and closes with G, Esc or by walking away.
            var forge = Sealbound.Core.ForgeStation.Current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (forgeOpen && (forge == null || !forge.InReach || (kb != null && kb.escapeKey.wasPressedThisFrame))) SetForgeOpen(false);
            else if (forge != null && forge.InReach && input != null && input.InteractPressed && GolemSeal.Current == null) SetForgeOpen(!forgeOpen);
            if (forgeOpen && kb != null)
            {
                var recipes = Forge.Recipes;
                if (kb.digit1Key.wasPressedThisFrame && recipes.Count > 0) UseRecipe(recipes[0]);
                if (kb.digit2Key.wasPressedThisFrame && recipes.Count > 1) UseRecipe(recipes[1]);
            }
            // The soul shrine binds a spirit with G.
            var shrine = Sealbound.Core.SoulShrine.Current;
            if (shrine != null && shrine.InReach && !forgeOpen && input != null && input.InteractPressed && GolemSeal.Current == null)
            {
                string said = shrine.TryBind();
                if (said != null) { _shrineNote = said; _shrineNoteBorn = Time.unscaledTime; }
            }
            _feed.RemoveAll(l => Time.unscaledTime - l.Born > FeedLife);
        }

        string _shrineNote;
        float _shrineNoteBorn = -99f;

        void DrawShrinePrompt()
        {
            var shrine = Sealbound.Core.SoulShrine.Current;
            if (shrine == null || !shrine.InReach || GolemSeal.Current != null) return;
            float w = Mathf.Min(760f, Screen.width - 40f), h = 40f, x = (Screen.width - w) / 2f, y = Screen.height * 0.64f;
            Rect(new Rect(x, y, w, h), new Color(0.02f, 0.04f, 0.08f, 0.62f));
            Rect(new Rect(x, y, w, 3f), new Color(0.55f, 0.8f, 1f, 0.9f));
            GUI.Label(new Rect(x, y, w, h), "<b>" + Sealbound.Core.SoulShrine.PromptText() + "</b>", new GUIStyle(_center) { fontSize = 17 });
            if (!string.IsNullOrEmpty(_shrineNote) && Time.unscaledTime - _shrineNoteBorn < 3.5f)
                GUI.Label(new Rect(x, y - 38f, w, 34f), $"<b><color=#9fd0ff>{_shrineNote}</color></b>", new GUIStyle(_center) { fontSize = 20 });
        }

        public void SetForgeOpen(bool open)
        {
            if (forgeOpen == open) return;
            forgeOpen = open;
            if (open) inventoryOpen = false;
            // Free the mouse for the buttons while the forge is open; take it back after.
            Sealbound.CameraRig.ThirdPersonCamera.MenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        /// <summary>Forges a weapon, or swaps between it and the plain one if it is already owned.</summary>
        public void UseRecipe(Recipe r)
        {
            var def = r.Weapon;
            if (Forge.Owns(r))
            {
                bool held = Inventory.EquippedWeapon(r.forSword) == r.weapon;
                Forge.Equip(r.forSword, held ? "" : r.weapon);
                Note(held ? $"Back to the plain {(r.forSword ? "Sealer sword" : "Sealer staff")}" : $"{def.displayName} in hand");
            }
            else if (Forge.Craft(r))
            {
                Note($"FORGED: {def.displayName}!");
                Sealbound.Combat.SealBurst.Spawn(Sealbound.Core.ForgeStation.Current != null ? Sealbound.Core.ForgeStation.Current.transform.position + Vector3.up * 1.3f : Vector3.zero, 2.5f, def.glow);
            }
            else Note("Not enough parts yet.");
        }

        void Note(string text) { _forgeNote = text; _forgeNoteBorn = Time.unscaledTime; }

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
            DrawShrinePrompt();
            DrawForge();
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
            string spirit = Inventory.BoundSpirit == ItemCatalog.GolemSpirit ? "The Golem Spirit is bound. Fill the Soul Gauge in battle, then press V to soul merge." :
                Inventory.Count(ItemCatalog.GolemSpirit) > 0 ? "You hold the Golem Spirit. Bind it at the soul shrine, west of the camp." :
                shards >= ItemCatalog.ShardsPerSpirit ? "You have enough shards to make the Golem Spirit whole at the soul shrine, west of the camp." :
                $"Golem Spirit: {LootTables.GolemSpiritChance(failed) * 100f:0}% chance on your next seal  ·  shards {shards}/{ItemCatalog.ShardsPerSpirit}";
            Rect(new Rect(x + 16f, ry + 6f, w - 32f, 44f), new Color(0.6f, 0.75f, 1f, 0.08f));
            GUI.Label(new Rect(x + 28f, ry + 10f, w - 56f, 40f), spirit, new GUIStyle(_small) { fontSize = 14 });
        }

        void DrawForge()
        {
            var forge = Sealbound.Core.ForgeStation.Current;
            if (forge == null || !forge.InReach) return;
            if (!forgeOpen)
            {
                float pw = 380f, ph = 36f, px = (Screen.width - pw) / 2f, py = Screen.height * 0.66f;
                Rect(new Rect(px, py, pw, ph), new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(px, py, pw, ph), "<b>Press  G  (d-pad up) to use the FORGE</b>", _center);
                return;
            }
            var recipes = Forge.Recipes;
            float w = Mathf.Min(640f, Screen.width - 40f), cardH = 150f;
            float h = 96f + recipes.Count * (cardH + 10f) + 40f;
            float x = (Screen.width - w) / 2f, y = Mathf.Max(20f, (Screen.height - h) / 2f);
            Rect(new Rect(x, y, w, h), new Color(0.05f, 0.03f, 0.02f, 0.9f));
            Rect(new Rect(x, y, w, 3f), new Color(1f, 0.55f, 0.2f, 0.95f));
            GUI.Label(new Rect(x, y + 10f, w, 34f), "<b>THE SEALERS' FORGE</b>", new GUIStyle(_center) { fontSize = 24 });
            GUI.Label(new Rect(x, y + 44f, w, 22f), "Turn creature parts into weapons.   Keys 1 / 2 or click.   Close: G or Esc",
                new GUIStyle(_center) { fontSize = 13 });

            float cy = y + 76f;
            for (int i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                var def = r.Weapon;
                bool owned = Forge.Owns(r), can = Forge.CanCraft(r);
                bool held = owned && Inventory.EquippedWeapon(r.forSword) == r.weapon;
                var card = new Rect(x + 16f, cy, w - 32f, cardH);
                Rect(card, new Color(1f, 1f, 1f, 0.05f));
                Rect(new Rect(card.x, card.y, 5f, card.height), def.RarityColour);
                Rect(new Rect(card.x + 16f, card.y + 16f, 44f, 44f), new Color(def.glow.r, def.glow.g, def.glow.b, 0.9f));
                GUI.Label(new Rect(card.x + 72f, card.y + 8f, card.width - 250f, 26f),
                    $"<b>{i + 1}.  {def.displayName}</b>  <size=13>({(r.forSword ? "Sword" : "Mage")})</size>", _label);
                GUI.Label(new Rect(card.x + 72f, card.y + 34f, card.width - 250f, 54f), def.description, new GUIStyle(_small) { fontSize = 12 });
                // What it costs: green when you have enough, red when you don't.
                var cost = new System.Text.StringBuilder();
                foreach (var c in r.cost)
                {
                    var part = ItemCatalog.Get(c.Key);
                    int have = Inventory.Count(c.Key);
                    string col = have >= c.Value ? "80e080" : "ff7060";
                    cost.Append($"<color=#{col}>{part.displayName} {have}/{c.Value}</color>    ");
                }
                GUI.Label(new Rect(card.x + 72f, card.y + 96f, card.width - 90f, 44f), owned ? "<color=#80e080>Forged</color>" : cost.ToString(),
                    new GUIStyle(_small) { fontSize = 13, richText = true });

                string label = held ? "Put away" : owned ? "Equip" : can ? "FORGE" : "Need parts";
                GUI.enabled = owned || can;
                if (GUI.Button(new Rect(card.xMax - 150f, card.y + 20f, 130f, 44f), label)) UseRecipe(r);
                GUI.enabled = true;
                cy += cardH + 10f;
            }
            if (!string.IsNullOrEmpty(_forgeNote) && Time.unscaledTime - _forgeNoteBorn < 3f)
                GUI.Label(new Rect(x, y + h - 36f, w, 30f), $"<b>{_forgeNote}</b>", new GUIStyle(_center) { fontSize = 18 });
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
