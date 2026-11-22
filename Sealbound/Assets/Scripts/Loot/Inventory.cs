using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sealbound.Loot
{
    /// <summary>
    /// What the player owns, and the spirit "pity" counters. Saved to a small file on the player's
    /// computer so loot carries over between play sessions (the forge and shrine will spend it).
    /// </summary>
    public static class Inventory
    {
        [Serializable] class Entry { public string id; public int count; }
        [Serializable] class Pity { public string creature; public int failed; }
        [Serializable] class SaveData
        {
            public int version = 1;
            public List<Entry> items = new List<Entry>();
            public List<Pity> pity = new List<Pity>();
            public int hunts;
            public string sword = "", staff = "";
            public string bound = "";
        }

        static Dictionary<string, int> _counts;
        static Dictionary<string, int> _pity;
        static int _hunts;
        static string _sword = "", _staff = "";
        static string _bound = "";
        static bool _dirty;

        /// <summary>An item was added: (item, how many, total now).</summary>
        public static event Action<ItemDef, int, int> Added;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _counts = null; _pity = null; _hunts = 0; _sword = ""; _staff = ""; _bound = ""; _dirty = false; Added = null; SpiritBound = null; }

        static string SavePath => Path.Combine(Application.persistentDataPath, "sealbound_inventory.json");

        /// <summary>Set by the dev snapshot so a test run never touches the player's real save.</summary>
        public static bool InMemoryOnly;

        public static int Hunts { get { Load(); return _hunts; } }

        public static int Count(string id)
        {
            Load();
            return id != null && _counts.TryGetValue(id, out var n) ? n : 0;
        }

        /// <summary>Everything owned, most precious first.</summary>
        public static List<KeyValuePair<ItemDef, int>> Owned()
        {
            Load();
            var list = new List<KeyValuePair<ItemDef, int>>();
            foreach (var kv in _counts)
            {
                var def = ItemCatalog.Get(kv.Key);
                if (def != null && kv.Value > 0) list.Add(new KeyValuePair<ItemDef, int>(def, kv.Value));
            }
            list.Sort((a, b) => b.Key.rarity != a.Key.rarity ? b.Key.rarity.CompareTo(a.Key.rarity) : string.CompareOrdinal(a.Key.displayName, b.Key.displayName));
            return list;
        }

        public static void Add(string id, int count = 1)
        {
            var def = ItemCatalog.Get(id);
            if (def == null || count <= 0) return;
            Load();
            _counts.TryGetValue(id, out var n);
            n += count;
            _counts[id] = n;
            _dirty = true;
            Added?.Invoke(def, count, n);
            Save();
        }

        /// <summary>Spends items (for the forge). Returns false and spends nothing if there are not enough.</summary>
        public static bool Spend(string id, int count)
        {
            if (Count(id) < count) return false;
            _counts[id] -= count;
            _dirty = true;
            Save();
            return true;
        }

        /// <summary>Failed spirit rolls in a row for this creature (raises the next chance).</summary>
        public static int FailedSpiritRolls(string creature)
        {
            Load();
            return _pity.TryGetValue(creature, out var n) ? n : 0;
        }

        public static void SetFailedSpiritRolls(string creature, int failed)
        {
            Load();
            _pity[creature] = Mathf.Max(0, failed);
            _dirty = true;
            Save();
        }

        /// <summary>The forged weapon each class carries ("" = the plain Sealer steel or staff).</summary>
        public static string EquippedWeapon(bool sword) { Load(); return sword ? _sword : _staff; }

        public static void SetEquippedWeapon(bool sword, string id)
        {
            Load();
            if (sword) _sword = id ?? ""; else _staff = id ?? "";
            _dirty = true;
            Save();
        }

        /// <summary>The spirit bound at the soul shrine for battle ("" = none). One at a time.</summary>
        public static string BoundSpirit { get { Load(); return Count(_bound) > 0 ? _bound : ""; } }

        /// <summary>A spirit was bound at the shrine.</summary>
        public static event Action<ItemDef> SpiritBound;

        /// <summary>Binds an owned spirit for battle. Returns false if it is not owned.</summary>
        public static bool BindSpirit(string id)
        {
            var def = ItemCatalog.Get(id);
            if (def == null || def.kind != ItemKind.Spirit || Count(id) <= 0) return false;
            _bound = id;
            _dirty = true;
            Save();
            SpiritBound?.Invoke(def);
            return true;
        }

        public static void CountHunt()
        {
            Load();
            _hunts++;
            _dirty = true;
            Save();
        }

        /// <summary>Forgets everything (a dev and testing helper).</summary>
        public static void Clear()
        {
            _counts = new Dictionary<string, int>();
            _pity = new Dictionary<string, int>();
            _hunts = 0;
            _sword = ""; _staff = ""; _bound = "";
            _dirty = true;
            Save();
        }

        static void Load()
        {
            if (_counts != null) return;
            _counts = new Dictionary<string, int>();
            _pity = new Dictionary<string, int>();
            if (InMemoryOnly) return;
            try
            {
                if (!File.Exists(SavePath)) return;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return;
                foreach (var e in data.items) if (!string.IsNullOrEmpty(e.id)) _counts[e.id] = e.count;
                foreach (var p in data.pity) if (!string.IsNullOrEmpty(p.creature)) _pity[p.creature] = p.failed;
                _hunts = data.hunts;
                _sword = data.sword ?? "";
                _staff = data.staff ?? "";
                _bound = data.bound ?? "";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Sealbound: could not read the inventory save, starting empty. " + ex.Message);
            }
        }

        static void Save()
        {
            if (!_dirty || InMemoryOnly) return;
            _dirty = false;
            try
            {
                var data = new SaveData { hunts = _hunts, sword = _sword, staff = _staff, bound = _bound };
                foreach (var kv in _counts) data.items.Add(new Entry { id = kv.Key, count = kv.Value });
                foreach (var kv in _pity) data.pity.Add(new Pity { creature = kv.Key, failed = kv.Value });
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Sealbound: could not save the inventory. " + ex.Message);
            }
        }
    }
}
