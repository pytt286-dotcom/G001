using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sealbound.Core
{
    /// <summary>
    /// Plays a character's animations from code, with smooth blends between them.
    /// A looping "base" animation (idle, walk, run, falling) runs underneath, and one-shot
    /// animations (hit, death, throw, spawn) play over it and hand back when they finish.
    /// It follows game time, so hit-stop freezes the pose too.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAnimator : MonoBehaviour
    {
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>();
        readonly Dictionary<string, int> _byName = new Dictionary<string, int>();
        float[] _weights;

        int _base = -1, _once = -1;
        float _baseFade = 0.15f, _onceFade = 0.08f, _onceSpeed = 1f;
        bool _onceHold;

        public string BaseName { get; private set; }
        public string OnceName => _once >= 0 ? _playables[_once].GetAnimationClip().name : null;
        public bool OncePlaying => _once >= 0;

        public static CharacterAnimator Attach(GameObject model, IEnumerable<AnimationClip> clips)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;

            var ca = model.AddComponent<CharacterAnimator>();
            ca.Build(animator, clips);
            return ca;
        }

        void Build(Animator animator, IEnumerable<AnimationClip> clips)
        {
            var list = new List<AnimationClip>(clips);
            _graph = PlayableGraph.Create(name + " Animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _mixer = AnimationMixerPlayable.Create(_graph, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var p = AnimationClipPlayable.Create(_graph, list[i]);
                p.SetApplyFootIK(false);
                _graph.Connect(p, 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _playables.Add(p);
                _byName[ShortName(list[i].name)] = i;
            }
            _weights = new float[list.Count];
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        /// <summary>"Rig_Medium|Running_A" and "Running_A" are both found as "Running_A".</summary>
        static string ShortName(string n)
        {
            int k = n.LastIndexOf('|');
            return k >= 0 ? n.Substring(k + 1) : n;
        }

        public bool Has(string clip) => _byName.ContainsKey(clip);

        public float Length(string clip) =>
            _byName.TryGetValue(clip, out int i) ? _playables[i].GetAnimationClip().length : 0f;

        /// <summary>Sets the looping animation underneath (idle, walk, run...). Calling it every frame is fine.</summary>
        public void SetBase(string clip, float speed = 1f, float fade = 0.15f)
        {
            if (!_byName.TryGetValue(clip, out int i)) return;
            _playables[i].SetSpeed(speed);
            if (i == _base) return;
            if (_weights[i] < 0.01f) _playables[i].SetTime(0);
            if (_base < 0 && _once < 0) _weights[i] = 1f; // first pose: no blend from the bind pose
            _base = i;
            _baseFade = fade;
            BaseName = clip;
        }

        /// <summary>Plays an animation once over the base. With hold, it freezes on its last frame (e.g. death).</summary>
        public void PlayOnce(string clip, float speed = 1f, float fade = 0.08f, bool hold = false)
        {
            if (!_byName.TryGetValue(clip, out int i)) return;
            _once = i;
            _onceSpeed = Mathf.Max(0.01f, speed);
            _onceFade = fade;
            _onceHold = hold;
            _playables[i].SetTime(0);
            _playables[i].SetSpeed(_onceSpeed);
        }

        public void StopOnce(float fade = 0.12f)
        {
            if (_once < 0) return;
            _once = -1;
            _baseFade = fade;
        }

        void Update()
        {
            if (!_graph.IsValid() || _weights == null) return;
            float dt = Time.deltaTime;

            if (_once >= 0)
            {
                var p = _playables[_once];
                double len = p.GetAnimationClip().length;
                double t = p.GetTime();
                if (_onceHold)
                {
                    if (t >= len) { p.SetTime(len); p.SetSpeed(0); }
                }
                else if ((len - t) / _onceSpeed <= _onceFade)
                {
                    _baseFade = _onceFade;
                    _once = -1;
                }
            }

            int target = _once >= 0 ? _once : _base;
            float fade = _once >= 0 ? _onceFade : _baseFade;
            float step = dt / Mathf.Max(0.01f, fade);
            float sum = 0f;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], i == target ? 1f : 0f, step);
                sum += _weights[i];
            }
            for (int i = 0; i < _weights.Length; i++)
                _mixer.SetInputWeight(i, sum > 0.0001f ? _weights[i] / sum : 0f);
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
