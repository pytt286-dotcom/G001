using System.Collections;
using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// Freezes the game for a split second on a hit, which makes blows feel heavy.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop _instance;
        float _until;
        bool _running;

        public static void Trigger(float seconds)
        {
            if (seconds <= 0f) return;
            if (_instance == null)
            {
                var go = new GameObject("Hit Stop");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitStop>();
            }
            _instance._until = Mathf.Max(_instance._until, Time.unscaledTime + seconds);
            if (!_instance._running) _instance.StartCoroutine(_instance.Run());
        }

        IEnumerator Run()
        {
            _running = true;
            Time.timeScale = 0.02f;
            while (Time.unscaledTime < _until) yield return null;
            Time.timeScale = 1f;
            _running = false;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            _running = false;
        }
    }
}
