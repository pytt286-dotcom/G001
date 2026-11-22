using System.Collections.Generic;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// Makes goblins take turns. Only a few may attack at once, and attacks are spaced out,
    /// so a crowd surrounds you and pressures you without hitting all at the same instant.
    /// That keeps fights readable and rewards rhythm and dodging.
    /// </summary>
    public static class GoblinDirector
    {
        public static int MaxAttackers = 2;
        public static float MinGapBetweenAttacks = 0.4f;

        static readonly HashSet<Goblin> Attackers = new HashSet<Goblin>();
        static readonly List<Goblin> All = new List<Goblin>();
        static float _lastGrant = -10f;

        public static IReadOnlyList<Goblin> Goblins => All;

        public static void Register(Goblin g) { if (!All.Contains(g)) All.Add(g); }

        public static void Unregister(Goblin g)
        {
            All.Remove(g);
            Attackers.Remove(g);
        }

        public static bool RequestToken(Goblin g)
        {
            if (Attackers.Contains(g)) return true;
            Attackers.RemoveWhere(a => a == null);
            if (Attackers.Count >= MaxAttackers) return false;
            if (Time.time - _lastGrant < MinGapBetweenAttacks) return false;
            Attackers.Add(g);
            _lastGrant = Time.time;
            return true;
        }

        public static void ReleaseToken(Goblin g) => Attackers.Remove(g);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Attackers.Clear();
            All.Clear();
            _lastGrant = -10f;
        }
    }
}
