using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Loot
{
    public enum Rarity { Common, Fine, Rare, Epic, Legendary }

    public enum ItemKind { Essence, Part, SpiritShard, Spirit }

    /// <summary>
    /// One kind of thing a player can own: a creature part, essence, a spirit or a spirit shard.
    /// Items are described once in <see cref="ItemCatalog"/>; the inventory only stores ids and counts.
    /// </summary>
    public class ItemDef
    {
        public string id;
        public string displayName;
        public string description;
        public Rarity rarity;
        public ItemKind kind;
        public string creature; // which creature it comes from ("" for shared currency)
        public Color glow;       // the colour of its light when it drops

        public Color RarityColour => ItemCatalog.RarityColour(rarity);
    }

    /// <summary>Every item in the first slice. New creatures add their parts here.</summary>
    public static class ItemCatalog
    {
        public const string Essence = "essence";
        public const string BurrowFang = "burrow_fang";
        public const string RunestoneShard = "runestone_shard";
        public const string GolemCore = "golem_core";
        public const string GolemSpiritShard = "golem_spirit_shard";
        public const string GolemSpirit = "golem_spirit";

        /// <summary>Spirit shards needed to bind a spirit without finding it whole (used by the shrine, Milestone 8).</summary>
        public const int ShardsPerSpirit = 10;

        static Dictionary<string, ItemDef> _all;

        static readonly Color RuneBlue = new Color(0.25f, 0.6f, 1f);

        public static IEnumerable<ItemDef> All { get { Build(); return _all.Values; } }

        public static ItemDef Get(string id)
        {
            Build();
            return id != null && _all.TryGetValue(id, out var d) ? d : null;
        }

        static void Build()
        {
            if (_all != null) return;
            _all = new Dictionary<string, ItemDef>();
            Add(Essence, "Sealing Essence", "Raw power left behind by every sealed creature. The forge burns it to shape parts into gear.",
                Rarity.Common, ItemKind.Essence, "", new Color(0.55f, 0.85f, 1f));
            Add(BurrowFang, "Burrow Fang", "A chipped goblin fang. Small, sharp and surprisingly common.",
                Rarity.Common, ItemKind.Part, "Burrow Goblin", new Color(0.95f, 0.85f, 0.6f));
            Add(RunestoneShard, "Runestone Shard", "A sliver of the Bastion Golem's body, still faintly humming. Breaking its runes in the fight frees more of them.",
                Rarity.Common, ItemKind.Part, "Bastion Golem", RuneBlue);
            Add(GolemCore, "Golem Core", "The burning heart stone that held the golem together. The forge's key ingredient for golem weapons.",
                Rarity.Rare, ItemKind.Part, "Bastion Golem", new Color(1f, 0.5f, 0.15f));
            Add(GolemSpiritShard, "Golem Spirit Shard", "A splinter of the golem's soul. Gather " + ShardsPerSpirit + " to bind the whole spirit at the shrine.",
                Rarity.Fine, ItemKind.SpiritShard, "Bastion Golem", new Color(0.7f, 0.85f, 1f));
            Add(GolemSpirit, "Golem Spirit", "The Bastion Golem's soul, sealed whole. Bind it at the soul shrine to take on its stone form in battle.",
                Rarity.Legendary, ItemKind.Spirit, "Bastion Golem", new Color(0.85f, 0.95f, 1f));
        }

        static void Add(string id, string name, string text, Rarity rarity, ItemKind kind, string creature, Color glow)
        {
            _all[id] = new ItemDef { id = id, displayName = name, description = text, rarity = rarity, kind = kind, creature = creature, glow = glow };
        }

        public static Color RarityColour(Rarity r)
        {
            switch (r)
            {
                case Rarity.Fine: return new Color(0.45f, 0.85f, 0.4f);
                case Rarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case Rarity.Epic: return new Color(0.75f, 0.45f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.72f, 0.2f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
