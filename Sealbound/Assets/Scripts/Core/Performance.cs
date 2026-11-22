using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sealbound.Core
{
    /// <summary>
    /// Keeps the game smooth: a steady 60 frames a second cap, lighter shadows, and a render
    /// resolution that drops a little when the computer struggles and climbs back when it can.
    /// The project's own settings asset is never changed: the game plays on a copy of it.
    /// </summary>
    public class Performance : MonoBehaviour
    {
        public static Performance Instance { get; private set; }

        /// <summary>Frames a second, smoothed.</summary>
        public float Fps { get; private set; } = 60f;
        /// <summary>The share of the screen's pixels being drawn (1 = full resolution).</summary>
        public float RenderScale => _asset != null ? _asset.renderScale : 1f;
        /// <summary>Lets the benchmark pin the resolution.</summary>
        public bool adaptive = true;

        public const int TargetFps = 60;
        const float MinScale = 0.7f; // below this the picture turns soft; the editor's own drawing also counts in its timings

        RenderPipelineAsset _original;
        UniversalRenderPipelineAsset _asset;
        float _maxScale = 1f;
        float _avgDt = 1f / 60f;
        float _judgeTimer;
        float _slowFor, _fastFor;
        float _gpuMs;
        readonly FrameTiming[] _timing = new FrameTiming[1];

        /// <summary>How long the graphics chip took for the last frames, in milliseconds (0 if the computer doesn't say).</summary>
        public float GpuMs => _gpuMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("Performance");
            DontDestroyOnLoad(go);
            go.AddComponent<Performance>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            Application.targetFrameRate = TargetFps;

            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (current == null) return;
            _original = QualitySettings.renderPipeline;
            _asset = Instantiate(current);
            _asset.name = current.name + " (in game)";
            // Shadows only where they show: two cascades out to 45 m.
            _asset.shadowDistance = 45f;
            _asset.shadowCascadeCount = 2;
            _asset.msaaSampleCount = 1;
            // Start near a 1080p pixel count: high-density laptop screens otherwise draw 2 to 4 times as many pixels.
            float pixels = Mathf.Max(1f, Screen.width * (float)Screen.height);
            _maxScale = Mathf.Clamp(Mathf.Sqrt(2.1e6f / pixels), MinScale, 1f);
            _asset.renderScale = _maxScale;
            AmbientOcclusion = AddAmbientOcclusion(_asset);
            QualitySettings.renderPipeline = _asset;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_asset != null)
            {
                QualitySettings.renderPipeline = _original;
                Destroy(_asset);
            }
        }

        /// <summary>True when soft contact shadows (ambient occlusion) were switched on.</summary>
        public static bool AmbientOcclusion { get; private set; }

        /// <summary>
        /// Adds screen-space ambient occlusion: soft shadow where things meet (under rocks, in the folds
        /// of clothes, where the golem's stones touch), which takes away the flat "clay" look.
        /// Works on copies, so the project's renderer settings are never changed.
        /// </summary>
        static bool AddAmbientOcclusion(UniversalRenderPipelineAsset asset)
        {
            try
            {
                const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", F);
                var list = listField != null ? listField.GetValue(asset) as ScriptableRendererData[] : null;
                if (list == null || list.Length == 0 || list[0] == null) return false;
                var aoType = typeof(ScriptableRendererFeature).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (aoType == null) return false;
                foreach (var f in list[0].rendererFeatures) if (f != null && aoType.IsInstanceOfType(f)) return true; // already there

                var data = Instantiate(list[0]);
                data.name = list[0].name + " (in game)";
                var ao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(aoType);
                ao.name = "Ambient Occlusion";
                var settingsField = aoType.GetField("m_Settings", F);
                var settings = settingsField != null ? settingsField.GetValue(ao) : null;
                if (settings != null)
                {
                    Set(settings, "Intensity", 1.4f);
                    Set(settings, "Radius", 0.4f);
                    Set(settings, "DirectLightingStrength", 0.3f);
                    Set(settings, "Downsample", true);
                    settingsField.SetValue(ao, settings);
                }
                data.rendererFeatures.Add(ao);
                var map = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", F);
                if (map != null && map.GetValue(data) is System.Collections.Generic.List<long> ids) ids.Add(ao.GetInstanceID());
                data.SetDirty();
                var copy = (ScriptableRendererData[])list.Clone();
                copy[0] = data;
                listField.SetValue(asset, copy);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Sealbound: ambient occlusion could not be switched on: " + e.Message);
                return false;
            }
        }

        static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null && f.FieldType == value.GetType()) f.SetValue(target, value);
        }

        public void SetRenderScale(float s)
        {
            if (_asset != null) _asset.renderScale = Mathf.Clamp(s, 0.3f, 1f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            _avgDt = Mathf.Lerp(_avgDt, dt, 0.05f);
            Fps = 1f / Mathf.Max(0.0001f, _avgDt);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].gpuFrameTime > 0.0)
                _gpuMs = Mathf.Lerp(_gpuMs, (float)_timing[0].gpuFrameTime, 0.08f);
            if (!adaptive || _asset == null) return;

            // Judge by real frame time: below about 52 fps for a while, draw fewer pixels; back at 60, draw more.
            // (The GPU timing includes the editor's own windows, so it is shown, not used.)
            bool slow = _avgDt > 1f / 52f;
            bool fast = _avgDt < 1f / 58f;
            if (slow) { _slowFor += dt; _fastFor = 0f; }
            else if (fast) { _fastFor += dt; _slowFor = 0f; }
            else { _slowFor = 0f; _fastFor = 0f; }

            _judgeTimer -= dt;
            if (_judgeTimer > 0f) return;
            if (_slowFor > 1.2f && _asset.renderScale > MinScale + 0.001f)
            {
                _asset.renderScale = Mathf.Max(MinScale, _asset.renderScale - 0.08f);
                _judgeTimer = 1.5f; _slowFor = 0f;
            }
            else if (_fastFor > 4f && _asset.renderScale < _maxScale - 0.001f)
            {
                _asset.renderScale = Mathf.Min(_maxScale, _asset.renderScale + 0.05f);
                _judgeTimer = 3f; _fastFor = 0f;
            }
        }
    }
}
