using System;
using UnityEngine;

namespace Sealbound.Player
{
    public enum AttackKind { Basic, Skill, Heavy }

    /// <summary>How an attack reaches its target.</summary>
    public enum Delivery { Melee, Bolt, Area, Trap, Blink }

    [Serializable]
    public class AttackData
    {
        public string name;
        public AttackKind kind;
        public Delivery delivery = Delivery.Melee;
        public float windup, active, recovery;
        public float damage;
        public int hits = 1;
        public float range = 2.4f;
        public float arcDegrees = 150f;
        public float lungeSpeed;
        public float launchSpeed;
        public float hover;
        [Range(0f, 1f)] public float impact;
        public float hitStop;
        public float shake;
        public float staminaCost;
        public float cooldown;
        public bool spin;

        [Header("Spells")]
        public float projectileSpeed = 42f;
        public float projectileRadius = 0.3f;
        public int projectileCount = 1;
        public bool pierce;
        public float areaRadius = 3.5f;
        public float areaDelay = 0.35f;
        [Tooltip("How far away area spells and traps land, or how far Blink goes.")]
        public float castDistance = 8f;
        [Range(0f, 1f)] public float slow = 1f;
        public float slowSeconds;
        public Color fxColor = new Color(1f, 0.5f, 0.15f);

        [Tooltip("Seconds after the attack starts when the perfect window opens. 0 = automatic.")]
        public float perfectStart;
        public float perfectLength = 0.16f;
        public Vector3 swingFrom, swingTo; // weapon pivot euler angles

        public float Total => windup + active + recovery;
        public float PerfectStart => perfectStart > 0f ? perfectStart : windup + active + 0.05f;
        public float PerfectEnd => PerfectStart + perfectLength;
        public bool IsRanged => delivery == Delivery.Bolt || delivery == Delivery.Area;
    }

    /// <summary>A class's full move set: basic string, air attack, three skills and a heavy.</summary>
    [Serializable]
    public class CombatKit
    {
        public string className;
        public AttackData[] basicCombo;
        public AttackData airAttack;
        public AttackData[] skills;
        public AttackData heavy;
    }

    public static class Kits
    {
        public static CombatKit Sword() => new CombatKit
        {
            className = "Sword",
            basicCombo = new[]
            {
                new AttackData { name = "Slash", windup = 0.07f, active = 0.08f, recovery = 0.3f, damage = 10f, lungeSpeed = 7f,
                    impact = 0.2f, hitStop = 0.035f, shake = 0.15f, swingFrom = new Vector3(10f, -80f, 0f), swingTo = new Vector3(20f, 80f, 0f) },
                new AttackData { name = "Return Slash", windup = 0.06f, active = 0.08f, recovery = 0.3f, damage = 12f, lungeSpeed = 7f,
                    impact = 0.3f, hitStop = 0.04f, shake = 0.18f, swingFrom = new Vector3(25f, 85f, 0f), swingTo = new Vector3(5f, -85f, 0f) },
                new AttackData { name = "Overhead Cleave", windup = 0.11f, active = 0.1f, recovery = 0.36f, damage = 20f, lungeSpeed = 10f,
                    impact = 0.75f, hitStop = 0.07f, shake = 0.4f, arcDegrees = 110f, range = 2.8f,
                    swingFrom = new Vector3(-75f, 10f, 0f), swingTo = new Vector3(70f, 0f, 0f) },
            },
            airAttack = new AttackData
            {
                name = "Air Slash", windup = 0.05f, active = 0.1f, recovery = 0.26f, damage = 12f, lungeSpeed = 6f,
                impact = 0.35f, hitStop = 0.045f, shake = 0.2f, arcDegrees = 200f, range = 2.6f, hover = 0.35f,
                swingFrom = new Vector3(-40f, -70f, 0f), swingTo = new Vector3(60f, 70f, 0f)
            },
            skills = new[]
            {
                new AttackData { name = "Rising Edge", kind = AttackKind.Skill, windup = 0.08f, active = 0.12f, recovery = 0.3f,
                    damage = 18f, launchSpeed = 13f, impact = 0.6f, hitStop = 0.06f, shake = 0.3f, arcDegrees = 120f, range = 2.6f,
                    cooldown = 5f, swingFrom = new Vector3(60f, 0f, 0f), swingTo = new Vector3(-70f, 0f, 0f) },
                new AttackData { name = "Gale Thrust", kind = AttackKind.Skill, windup = 0.1f, active = 0.14f, recovery = 0.3f,
                    damage = 24f, lungeSpeed = 24f, impact = 0.7f, hitStop = 0.06f, shake = 0.35f, arcDegrees = 60f, range = 3f,
                    cooldown = 6f, swingFrom = new Vector3(5f, -15f, 0f), swingTo = new Vector3(0f, 0f, 0f) },
                new AttackData { name = "Runic Whirl", kind = AttackKind.Skill, windup = 0.08f, active = 0.4f, recovery = 0.3f,
                    damage = 10f, hits = 3, impact = 0.4f, hitStop = 0.03f, shake = 0.2f, arcDegrees = 360f, range = 3f,
                    cooldown = 8f, spin = true, swingFrom = new Vector3(20f, -90f, 0f), swingTo = new Vector3(20f, 90f, 0f) },
            },
            heavy = new AttackData
            {
                name = "Sealbreaker Slam", kind = AttackKind.Heavy, windup = 0.3f, active = 0.1f, recovery = 0.35f, damage = 38f,
                lungeSpeed = 9f, impact = 1f, hitStop = 0.11f, shake = 0.7f, arcDegrees = 360f, range = 3.2f, staminaCost = 25f,
                swingFrom = new Vector3(-90f, 0f, 0f), swingTo = new Vector3(80f, 0f, 0f)
            }
        };

        static readonly Color Ember = new Color(1f, 0.5f, 0.15f);
        static readonly Color Frost = new Color(0.5f, 0.85f, 1f);

        public static CombatKit Mage() => new CombatKit
        {
            className = "Mage",
            basicCombo = new[]
            {
                new AttackData { name = "Ember Bolt", delivery = Delivery.Bolt, windup = 0.08f, active = 0.06f, recovery = 0.3f,
                    damage = 9f, impact = 0.2f, hitStop = 0.03f, shake = 0.1f, fxColor = Ember,
                    swingFrom = new Vector3(20f, -30f, 0f), swingTo = new Vector3(-10f, 0f, 0f) },
                new AttackData { name = "Ember Bolt", delivery = Delivery.Bolt, windup = 0.07f, active = 0.06f, recovery = 0.3f,
                    damage = 10f, impact = 0.25f, hitStop = 0.03f, shake = 0.1f, fxColor = Ember,
                    swingFrom = new Vector3(20f, 30f, 0f), swingTo = new Vector3(-10f, 0f, 0f) },
                new AttackData { name = "Twin Embers", delivery = Delivery.Bolt, windup = 0.12f, active = 0.08f, recovery = 0.38f,
                    damage = 11f, projectileCount = 2, projectileRadius = 0.38f, impact = 0.5f, hitStop = 0.05f, shake = 0.25f, fxColor = Ember,
                    swingFrom = new Vector3(-30f, 0f, 0f), swingTo = new Vector3(-5f, 0f, 0f) },
            },
            airAttack = new AttackData
            {
                name = "Falling Star", delivery = Delivery.Bolt, windup = 0.06f, active = 0.06f, recovery = 0.26f, damage = 11f,
                impact = 0.35f, hitStop = 0.04f, shake = 0.15f, hover = 0.4f, fxColor = new Color(1f, 0.85f, 0.4f),
                swingFrom = new Vector3(-20f, 0f, 0f), swingTo = new Vector3(30f, 0f, 0f)
            },
            skills = new[]
            {
                new AttackData { name = "Flame Burst", kind = AttackKind.Skill, delivery = Delivery.Area, windup = 0.12f, active = 0.08f,
                    recovery = 0.32f, damage = 22f, launchSpeed = 10f, areaRadius = 3.5f, areaDelay = 0.35f, castDistance = 12f,
                    impact = 0.65f, hitStop = 0.07f, shake = 0.4f, cooldown = 6f, fxColor = Ember,
                    swingFrom = new Vector3(-60f, 0f, 0f), swingTo = new Vector3(20f, 0f, 0f) },
                new AttackData { name = "Frost Snare", kind = AttackKind.Skill, delivery = Delivery.Trap, windup = 0.1f, active = 0.06f,
                    recovery = 0.28f, damage = 4f, areaRadius = 3f, castDistance = 4f, slow = 0.45f, slowSeconds = 3f,
                    impact = 0.1f, cooldown = 9f, fxColor = Frost,
                    swingFrom = new Vector3(40f, 0f, 0f), swingTo = new Vector3(70f, 0f, 0f) },
                new AttackData { name = "Blink", kind = AttackKind.Skill, delivery = Delivery.Blink, windup = 0.02f, active = 0.16f,
                    recovery = 0.2f, damage = 12f, areaRadius = 2.8f, castDistance = 8f, impact = 0.45f, hitStop = 0.03f,
                    shake = 0.2f, cooldown = 5f, fxColor = new Color(0.75f, 0.5f, 1f),
                    swingFrom = new Vector3(0f, 0f, 0f), swingTo = new Vector3(0f, 0f, 0f) },
            },
            heavy = new AttackData
            {
                name = "Meteor Lance", kind = AttackKind.Heavy, delivery = Delivery.Bolt, windup = 0.32f, active = 0.08f, recovery = 0.36f,
                damage = 34f, projectileSpeed = 55f, projectileRadius = 0.6f, pierce = true, impact = 1f, hitStop = 0.1f, shake = 0.6f,
                staminaCost = 25f, fxColor = new Color(1f, 0.35f, 0.1f),
                swingFrom = new Vector3(-80f, 0f, 0f), swingTo = new Vector3(-10f, 0f, 0f)
            }
        };
    }
}
