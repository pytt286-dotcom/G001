using System;
using System.Collections.Generic;

namespace Sealbound.Loot
{
    /// <summary>What a forged weapon costs, and what it does.</summary>
    public class Recipe
    {
        public string weapon;
        public bool forSword;
        public List<KeyValuePair<string, int>> cost = new List<KeyValuePair<string, int>>();
        public ItemDef Weapon => ItemCatalog.Get(weapon);
    }

    /// <summary>
    /// The kingdom's forge: turns creature parts into weapons and puts them in the Sealer's hands.
    /// Each class can carry one forged weapon at a time.
    /// </summary>
    public static class Forge
    {
        /// <summary>A class's weapon changed: (sword class?, weapon id or "").</summary>
        public static event Action<bool, string> WeaponChanged;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { WeaponChanged = null; _recipes = null; }

        static List<Recipe> _recipes;

        public static List<Recipe> Recipes
        {
            get
            {
                if (_recipes != null) return _recipes;
                _recipes = new List<Recipe>
                {
                    Make(ItemCatalog.RunestoneBlade, true, ItemCatalog.RunestoneShard, 6, ItemCatalog.GolemCore, 1, ItemCatalog.Essence, 60),
                    Make(ItemCatalog.GolemCoreStaff, false, ItemCatalog.RunestoneShard, 4, ItemCatalog.GolemCore, 1, ItemCatalog.Essence, 60),
                };
                return _recipes;
            }
        }

        static Recipe Make(string weapon, bool sword, params object[] cost)
        {
            var r = new Recipe { weapon = weapon, forSword = sword };
            for (int i = 0; i + 1 < cost.Length; i += 2) r.cost.Add(new KeyValuePair<string, int>((string)cost[i], (int)cost[i + 1]));
            return r;
        }

        public static bool Owns(Recipe r) => Inventory.Count(r.weapon) > 0;

        public static bool CanCraft(Recipe r)
        {
            if (Owns(r)) return false;
            foreach (var c in r.cost) if (Inventory.Count(c.Key) < c.Value) return false;
            return true;
        }

        /// <summary>Spends the parts, adds the weapon and equips it. Returns false if it can't be made.</summary>
        public static bool Craft(Recipe r)
        {
            if (!CanCraft(r)) return false;
            foreach (var c in r.cost) Inventory.Spend(c.Key, c.Value);
            Inventory.Add(r.weapon, 1);
            Equip(r.forSword, r.weapon);
            return true;
        }

        /// <summary>Puts a forged weapon (or "" for the plain one) in a class's hands.</summary>
        public static void Equip(bool sword, string weapon)
        {
            if (!string.IsNullOrEmpty(weapon) && Inventory.Count(weapon) <= 0) return;
            Inventory.SetEquippedWeapon(sword, weapon);
            WeaponChanged?.Invoke(sword, weapon ?? "");
        }
    }
}
