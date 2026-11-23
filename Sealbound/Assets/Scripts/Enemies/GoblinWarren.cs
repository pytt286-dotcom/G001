using System.Collections;
using System.Collections.Generic;
using Sealbound.Player;
using UnityEngine;

namespace Sealbound.Enemies
{
    /// <summary>
    /// The Goblin Warren: step inside and goblins climb out of the burrows in three waves.
    /// </summary>
    public class GoblinWarren : MonoBehaviour
    {
        enum Phase { Idle, Spawning, Fighting, BetweenWaves, Cleared }

        public float radius = 14f;
        public Transform player;
        public Vector3[] burrows;
        [Tooltip("Each wave: x = Burrow Goblins, y = Goblin Slingers.")]
        public Vector2Int[] waves = { new Vector2Int(3, 0), new Vector2Int(4, 1), new Vector2Int(5, 2) };
        public float spawnSpacing = 0.35f;
        public float pauseBetweenWaves = 2.5f;

        Phase _phase = Phase.Idle;
        int _wave;
        float _timer;
        readonly List<Goblin> _alive = new List<Goblin>();
        PlayerVitals _vitals;

        public string StatusText
        {
            get
            {
                switch (_phase)
                {
                    case Phase.Spawning:
                    case Phase.Fighting: return $"GOBLIN WARREN · Wave {_wave + 1}/{waves.Length} · Goblins left: {_alive.Count}";
                    case Phase.BetweenWaves: return $"Wave {_wave + 1} cleared! Next wave coming...";
                    case Phase.Cleared: return "WARREN CLEARED! Leave the warren and come back to fight again.";
                    default:
                        if (player == null) return "";
                        float d = DistanceToPlayer();
                        return d <= radius + 10f ? "Step into the warren to start the fight" : $"Goblin Warren: {d:0} m {DirectionWord()} · follow the red banners";
                }
            }
        }

        public bool IsCleared => _phase == Phase.Cleared;
        public int Wave => _wave + 1;
        public int WaveCount => waves.Length;
        public bool InFight => _phase == Phase.Spawning || _phase == Phase.Fighting || _phase == Phase.BetweenWaves;

        void Start()
        {
            if (player != null)
            {
                _vitals = player.GetComponent<PlayerVitals>();
                if (_vitals != null) _vitals.Fell += ResetWarren;
            }
        }

        void Update()
        {
            if (player == null) return;
            float d = DistanceToPlayer();

            switch (_phase)
            {
                case Phase.Idle:
                    if (d < radius && (_vitals == null || !_vitals.IsDown)) { _wave = 0; StartCoroutine(SpawnWave()); }
                    break;
                case Phase.Fighting:
                    _alive.RemoveAll(g => g == null || g.IsDead);
                    if (_alive.Count == 0)
                    {
                        if (_wave >= waves.Length - 1) _phase = Phase.Cleared;
                        else { _phase = Phase.BetweenWaves; _timer = pauseBetweenWaves; }
                    }
                    break;
                case Phase.BetweenWaves:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) { _wave++; StartCoroutine(SpawnWave()); }
                    break;
                case Phase.Cleared:
                    if (d > radius + 4f) _phase = Phase.Idle;
                    break;
            }
        }

        IEnumerator SpawnWave()
        {
            _phase = Phase.Spawning;
            var w = waves[_wave];
            var order = new List<Goblin.Kind>();
            for (int i = 0; i < w.x; i++) order.Add(Goblin.Kind.Burrower);
            for (int i = 0; i < w.y; i++) order.Insert(Random.Range(0, order.Count + 1), Goblin.Kind.Slinger);

            foreach (var kind in order)
            {
                if (_phase != Phase.Spawning) yield break; // reset mid-spawn
                Vector3 pos = burrows != null && burrows.Length > 0
                    ? burrows[Random.Range(0, burrows.Length)]
                    : transform.position + Random.insideUnitSphere.Flat() * radius;
                var g = Goblin.Create(kind, pos + Vector3.up * 0.05f, player);
                _alive.Add(g);
                yield return new WaitForSeconds(spawnSpacing);
            }
            if (_phase == Phase.Spawning) _phase = Phase.Fighting;
        }

        void ResetWarren()
        {
            StopAllCoroutines();
            foreach (var g in _alive) if (g != null) Destroy(g.gameObject);
            _alive.Clear();
            _phase = Phase.Idle;
        }

        float DistanceToPlayer()
        {
            Vector3 d = player.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        string DirectionWord()
        {
            var cam = Camera.main;
            if (cam == null) return "";
            Vector3 to = transform.position - player.position; to.y = 0f;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            if (Mathf.Abs(angle) < 35f) return "ahead";
            if (Mathf.Abs(angle) > 145f) return "behind you";
            return angle > 0f ? "to your right" : "to your left";
        }
    }

    static class VectorExtensions
    {
        public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; }
    }
}
