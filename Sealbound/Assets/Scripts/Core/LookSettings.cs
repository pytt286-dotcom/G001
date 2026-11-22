using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sealbound.Core
{
    /// <summary>
    /// The game's overall look, set up in code: a warm sun, soft coloured sky light, gentle haze,
    /// a painted-style sky, and post-processing (bloom so runes and spells glow, richer colours).
    /// Aims toward a warm, storybook feel. Costs nothing and needs no art files.
    /// </summary>
    public static class LookSettings
    {
        public static void Apply(Camera cam)
        {
            // Sun
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                sun.color = new Color(1f, 0.92f, 0.78f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                sun.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
                RenderSettings.sun = sun;
            }

            // Sky light: blue from above, warm bounce from the ground.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.33f, 0.26f);

            // Gentle haze for depth, so giants far away fade into the air.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.9f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 320f;

            // Painted-style sky.
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.9f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.6f, 0.62f));
                sky.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = sky;
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            DynamicGI.UpdateEnvironment();

            // Post-processing.
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                }
            }

            var go = new GameObject("Sealbound Look (post-processing)");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(18f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.15f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(8f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
        }
    }
}
