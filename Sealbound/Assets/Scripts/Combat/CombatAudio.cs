using UnityEngine;

namespace Sealbound.Combat
{
    /// <summary>
    /// Placeholder combat sounds generated in code (no audio files needed yet):
    /// a soft tick when the timing window opens, a chime on perfect timing, a thump on hits,
    /// a low buzz when a combo is broken.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CombatAudio : MonoBehaviour
    {
        AudioSource _source;
        AudioClip _tick, _perfect, _link, _hit, _heavyHit, _broken, _swing;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _tick = Tone("Tick", 1400f, 0.035f, 0.25f, 0f);
            _perfect = Chord("Perfect", new[] { 880f, 1320f }, 0.16f, 0.35f);
            _link = Chord("Link", new[] { 660f, 990f, 1320f }, 0.22f, 0.35f);
            _hit = Noise("Hit", 0.08f, 0.6f, 120f);
            _heavyHit = Noise("Heavy Hit", 0.18f, 0.8f, 70f);
            _broken = Tone("Broken", 140f, 0.18f, 0.35f, -60f);
            _swing = Noise("Swing", 0.07f, 0.12f, 0f);
        }

        public void PlayTick() => _source.PlayOneShot(_tick);
        public void PlayPerfect() => _source.PlayOneShot(_perfect);
        public void PlayLink() => _source.PlayOneShot(_link);
        public void PlayHit(bool heavy) => _source.PlayOneShot(heavy ? _heavyHit : _hit);
        public void PlayBroken() => _source.PlayOneShot(_broken);
        public void PlaySwing() => _source.PlayOneShot(_swing);

        public const int Rate = 44100;

        public static AudioClip Tone(string name, float freq, float length, float volume, float slide)
        {
            int n = Mathf.CeilToInt(Rate * length);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = freq + slide * t;
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Min(1f, t * 40f) * (1f - t) * (1f - t);
                data[i] = Mathf.Sin(phase) * env * volume;
            }
            return Make(name, data);
        }

        public static AudioClip Chord(string name, float[] freqs, float length, float volume)
        {
            int n = Mathf.CeilToInt(Rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 5f);
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * freqs[k] * i / Rate);
                data[i] = s / freqs.Length * env * volume;
            }
            return Make(name, data);
        }

        public static AudioClip Noise(string name, float length, float volume, float bodyFreq)
        {
            int n = Mathf.CeilToInt(Rate * length);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = (1f - t) * (1f - t);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, white, 0.25f);
                float body = bodyFreq > 0f ? Mathf.Sin(2f * Mathf.PI * bodyFreq * i / Rate) * 0.8f : 0f;
                data[i] = (lp * 0.7f + body) * env * volume;
            }
            return Make(name, data);
        }

        public static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
