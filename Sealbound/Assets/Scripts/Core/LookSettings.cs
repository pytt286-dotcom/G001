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
            bool real = ApplyPhotoSky(sun, cam);
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
            bloom.threshold.Override(real ? 1.1f : 0.95f);
            bloom.intensity.Override(real ? 0.55f : 0.9f);
            bloom.scatter.Override(0.65f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(real ? 6f : 18f);
            color.contrast.Override(real ? 14f : 10f);
            color.postExposure.Override(real ? 0.1f : 0.15f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(real ? 6f : 8f);

            // Film-style tone curve for the photo-real look; softer and brighter for the cartoon look.
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(real ? TonemappingMode.ACES : TonemappingMode.Neutral);

            if (real)
            {
                // Warm light, cool shadows: the golden-hour feel of an animated film.
                var smh = profile.Add<ShadowsMidtonesHighlights>(true);
                smh.shadows.Override(new Vector4(0.94f, 0.98f, 1.06f, 0f));
                smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.93f, 0f));
            }

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(real ? 0.18f : 0.22f);
            vignette.smoothness.Override(0.5f);
        }

        /// <summary>
        /// Uses the photographed sky (Poly Haven HDRI, CC0) when it's in the project: it lights the world,
        /// gives reflections, points the sun where the real sun was, and tints the haze to the horizon.
        /// </summary>
        static bool ApplyPhotoSky(Light sun, Camera cam)
        {
            var hdri = Resources.Load<Texture2D>(RealArt.Sky);
            var shader = Shader.Find("Skybox/Panoramic");
            if (hdri == null || shader == null) return false;

            var sky = new Material(shader) { name = "Photo sky" };
            sky.SetTexture("_MainTex", hdri);
            sky.SetFloat("_Mapping", 1f);      // latitude-longitude
            sky.SetFloat("_ImageType", 0f);    // 360 degrees
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = sky;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;

            Color horizon = new Color(0.72f, 0.78f, 0.84f);
            if (FindSun(hdri, out Vector3 toSun, out Color found))
            {
                horizon = found;
                if (sun != null)
                {
                    // Keep the sun at least 25 degrees up so shadows don't get too long.
                    float elev = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
                    if (elev < 25f)
                    {
                        Vector3 flat = new Vector3(toSun.x, 0f, toSun.z).normalized;
                        toSun = Quaternion.AngleAxis(-(25f - elev), Vector3.Cross(flat, Vector3.up)) * toSun;
                    }
                    sun.transform.rotation = Quaternion.LookRotation(-toSun.normalized);
                }
            }
            if (sun != null)
            {
                sun.color = new Color(1f, 0.93f, 0.82f);
                sun.intensity = 2.1f;
                sun.shadowStrength = 0.9f;
            }

            // Exponential haze in the colour of the horizon, so the hills fade into the sky.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = horizon;
            return true;
        }

        /// <summary>Finds the brightest spot in the sky picture (the sun) and the average horizon colour.</summary>
        static bool FindSun(Texture2D hdri, out Vector3 toSun, out Color horizon)
        {
            toSun = Vector3.up;
            horizon = Color.gray;
            // Shrink the sky into a small picture on the graphics card and read that back
            // (the full sky stays compressed and unreadable, which saves memory).
            const int w = 256, h = 128;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(hdri, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = small.GetPixels();
            Object.Destroy(small);
            if (px.Length < w * h) return false;

            int best = 0; float bestLum = -1f;
            for (int i = 0; i < px.Length; i++)
            {
                float l = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f;
                if (l > bestLum) { bestLum = l; best = i; }
            }
            float u = (best % w + 0.5f) / w, v = (best / w + 0.5f) / h;
            // Inverse of Unity's Skybox/Panoramic lookup.
            float lon = (0.5f - u) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
            toSun = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));

            // Horizon colour: average a band just above the horizon, tone-mapped to a displayable colour.
            Color sum = Color.black; int n = 0;
            int y0 = Mathf.RoundToInt(h * 0.5f), y1 = Mathf.RoundToInt(h * 0.56f);
            for (int y = y0; y <= y1; y++)
                for (int x = 0; x < w; x++) { sum += px[y * w + x]; n++; }
            if (n > 0)
            {
                Color c = sum / n;
                horizon = new Color(c.r / (1f + c.r), c.g / (1f + c.g), c.b / (1f + c.b)) * 1.35f;
                horizon.a = 1f;
            }
            return true;
        }
    }
}
