using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Loot
{
    /// <summary>What each creature leaves behind when it is sealed.</summary>
    public static class LootTables
    {
        public const string GolemName = "Bastion Golem";

        /// <summary>Chance of the golem's spirit this hunt: 10%, plus 8% for every hunt in a row without it, sure by the 10th.</summary>
        public static float GolemSpiritChance(int failedInARow) => failedInARow >= 9 ? 1f : Mathf.Clamp01(0.1f + 0.08f * failedInARow);

        /// <summary>
        /// Rolls the Bastion Golem's drops. Every limb rune broken during the fight adds a shard,
        /// more essence, and a better chance of the Golem Core.
        /// </summary>
        public static List<KeyValuePair<string, int>> RollGolem(int runesBroken, out bool gotSpirit)
        {
            var loot = new List<KeyValuePair<string, int>>();
            loot.Add(Pair(ItemCatalog.Essence, Random.Range(25, 36) + 5 * runesBroken));
            loot.Add(Pair(ItemCatalog.RunestoneShard, 3 + Mathf.Min(runesBroken, 6) + Random.Range(0, 3)));
            float coreChance = Mathf.Clamp01(0.5f + 0.125f * runesBroken);
            if (Random.value < coreChance) loot.Add(Pair(ItemCatalog.GolemCore, 1));

            int failed = Inventory.FailedSpiritRolls(GolemName);
            gotSpirit = Random.value < GolemSpiritChance(failed);
            if (gotSpirit)
            {
                loot.Add(Pair(ItemCatalog.GolemSpirit, 1));
                Inventory.SetFailedSpiritRolls(GolemName, 0);
            }
            else
            {
                // A miss still leaves splinters of its soul, so no hunt is wasted.
                loot.Add(Pair(ItemCatalog.GolemSpiritShard, Random.Range(1, 3)));
                Inventory.SetFailedSpiritRolls(GolemName, failed + 1);
            }
            Inventory.CountHunt();
            return loot;
        }

        /// <summary>A Burrow Goblin: a little essence, and sometimes a fang.</summary>
        public static List<KeyValuePair<string, int>> RollGoblin()
        {
            var loot = new List<KeyValuePair<string, int>>();
            loot.Add(Pair(ItemCatalog.Essence, Random.Range(2, 5)));
            if (Random.value < 0.3f) loot.Add(Pair(ItemCatalog.BurrowFang, 1));
            return loot;
        }

        static KeyValuePair<string, int> Pair(string id, int n) => new KeyValuePair<string, int>(id, n);
    }
}
