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
        const float MinScale = 0.55f;

        RenderPipelineAsset _original;
        UniversalRenderPipelineAsset _asset;
        float _maxScale = 1f;
        float _avgDt = 1f / 60f;
        float _judgeTimer;
        float _slowFor, _fastFor;

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

        public void SetRenderScale(float s)
        {
            if (_asset != null) _asset.renderScale = Mathf.Clamp(s, 0.3f, 1f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            _avgDt = Mathf.Lerp(_avgDt, dt, 0.05f);
            Fps = 1f / Mathf.Max(0.0001f, _avgDt);
            if (!adaptive || _asset == null) return;

            // Too slow for a while: draw fewer pixels. Comfortably fast for a while: draw more again.
            if (_avgDt > 1f / 50f) { _slowFor += dt; _fastFor = 0f; }
            else if (_avgDt < 1f / 57f) { _fastFor += dt; _slowFor = 0f; }
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
