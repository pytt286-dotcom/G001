using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Sealbound.Core;
using Sealbound.Enemies;
using Sealbound.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sealbound.Dev
{
    /// <summary>
    /// A fixed tour of the test field that measures smoothness: average frame rate, the slowest 1% of
    /// frames, CPU and GPU time, and memory churn, at each stop. At the golem it switches features
    /// off one by one to show what each one costs. Writes a text report and sets <see cref="Done"/>.
    /// </summary>
    public class PerfBenchmark : MonoBehaviour
    {
        public static string OutputFile;
        public static bool Done;

        readonly StringBuilder _report = new StringBuilder();
        readonly List<float> _frames = new List<float>();
        readonly FrameTiming[] _timing = new FrameTiming[1];
        double _cpu, _gpu; int _timed;

        IEnumerator Start()
        {
            Done = false;
            Inventory();
            yield return new WaitForSeconds(3f);
            var motor = FindAnyObjectByType<PlayerMotor>();
            if (motor == null) { Line("No player found."); Finish(); yield break; }
            var health = motor.GetComponent<Combat.Health>();
            if (health != null) health.InvulnerableCheck = () => true;
            var perf = Performance.Instance;
            Line($"Screen {Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB RAM");
            Line($"Quality: render scale start {(perf != null ? perf.RenderScale : 1f):0.00}, adaptive {(perf != null && perf.adaptive)}, target {Application.targetFrameRate} fps");
            Line("");
            Line("Stop | avg fps | slowest 1% fps | worst frame ms | CPU ms | GPU ms | KB allocated per frame | render scale");

            // Each stop: the hero stands here and the camera turns slowly round.
            yield return Stop("Spawn", motor, new Vector3(0f, 0.2f, -30f), 0f);
            yield return Stop("Training yard", motor, new Vector3(4f, 0.2f, -16f), 30f);
            yield return Stop("Goblin warren (fighting)", motor, new Vector3(-45f, 0.2f, -15f), 90f);
            yield return Stop("Meadow, looking at the forest", motor, new Vector3(-60f, 0.2f, 60f), 270f);
            yield return Stop("Golem arena (fight)", motor, new Vector3(0f, 0.2f, 52f), 0f);

            // What does each feature cost? Switch them off one after another at the golem.
            Line("");
            Line("At the golem, switching features off one after another (each row also has the ones above it off):");
            if (perf != null) { perf.adaptive = false; perf.SetRenderScale(perf.RenderScale); }
            yield return Measure("Everything on", 4f);
            SetPost(false); yield return Measure("- post-processing (colour grade, bloom, SMAA)", 4f);
            SetShadows(false); yield return Measure("- sun shadows", 4f);
            SetGrass(false); yield return Measure("- grass", 4f);
            SetNature(false); yield return Measure("- trees, bushes, rocks", 4f);
            SetTerrain(false); yield return Measure("- terrain", 4f);
            float scale = perf != null ? perf.RenderScale : 1f;
            if (perf != null) perf.SetRenderScale(scale * 0.5f);
            yield return Measure("- half the resolution", 4f);
            if (perf != null) perf.SetRenderScale(scale);
            SetPost(true); SetShadows(true); SetGrass(true); SetNature(true); SetTerrain(true);
            if (perf != null) perf.adaptive = true;
            Finish();
        }

        IEnumerator Stop(string name, PlayerMotor motor, Vector3 at, float yaw)
        {
            motor.Teleport(at);
            yield return new WaitForSeconds(1.5f);
            var cam = FindAnyObjectByType<CameraRig.ThirdPersonCamera>();
            var yawField = typeof(CameraRig.ThirdPersonCamera).GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            float t = 0f;
            yield return Measure(name, 6f, () =>
            {
                t += Time.unscaledDeltaTime;
                if (cam != null && yawField != null) yawField.SetValue(cam, yaw + t * 30f);
            });
        }

        IEnumerator Measure(string name, float seconds, Action each = null)
        {
            yield return null;
            _frames.Clear(); _cpu = 0; _gpu = 0; _timed = 0;
            long gc0 = GC.GetAllocatedBytesForCurrentThread();
            int gcCount0 = GC.CollectionCount(0);
            float start = Time.realtimeSinceStartup;
            int guard = 0;
            while (Time.realtimeSinceStartup - start < seconds && guard++ < 2000)
            {
                each?.Invoke();
                yield return null;
                _frames.Add(Time.unscaledDeltaTime);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                {
                    _cpu += _timing[0].cpuFrameTime; _gpu += _timing[0].gpuFrameTime; _timed++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - gc0;
            int n = Mathf.Max(1, _frames.Count);
            float total = 0f; foreach (var f in _frames) total += f;
            var sorted = new List<float>(_frames); sorted.Sort();
            float p99 = sorted.Count > 0 ? sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * 0.99f), 0, sorted.Count - 1)] : 0f;
            float worst = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;
            var perf = Performance.Instance;
            // A stall (the window losing focus, say) would swamp the average: leave such frames out and count them.
            int stalls = _frames.RemoveAll(f => f > 1f);
            if (stalls > 0) { total = 0f; foreach (var f in _frames) total += f; n = Mathf.Max(1, _frames.Count); sorted = new List<float>(_frames); sorted.Sort(); }
            p99 = sorted.Count > 0 ? sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * 0.99f), 0, sorted.Count - 1)] : 0f;
            worst = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;
            if (stalls > 0) name += $" ({stalls} stalled frame(s) left out)";
            Line($"{name} | {n / Mathf.Max(0.001f, total):0} | {1f / Mathf.Max(0.0001f, p99):0} | {worst * 1000f:0} | " +
                 $"{(_timed > 0 ? _cpu / _timed : 0):0.0} | {(_timed > 0 ? _gpu / _timed : 0):0.0} | {allocated / 1024f / n:0.0} (GCs {GC.CollectionCount(0) - gcCount0}) | {(perf != null ? perf.RenderScale : 1f):0.00}");
        }

        // ---------- Feature switches ----------

        static void SetPost(bool on)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = on;
        }

        static readonly Dictionary<Light, LightShadows> Shadows = new Dictionary<Light, LightShadows>();
        static void SetShadows(bool on)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (!on) { Shadows[l] = l.shadows; l.shadows = LightShadows.None; }
                else if (Shadows.TryGetValue(l, out var s)) l.shadows = s;
            }
        }

        static void SetGrass(bool on)
        {
            foreach (var g in FindObjectsByType<GrassField>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.enabled = on;
        }

        static void SetNature(bool on)
        {
            foreach (var lod in FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lod.GetComponentInParent<BastionGolem>() != null) continue;
                foreach (var r in lod.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            }
        }

        static void SetTerrain(bool on)
        {
            foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) t.enabled = on;
        }

        void Inventory()
        {
            int renderers = 0, lodGroups = 0; long tris = 0; int lights = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                renderers++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) tris += mf.sharedMesh.triangles.LongLength / 3;
                else if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) tris += sk.sharedMesh.triangles.LongLength / 3;
            }
            lodGroups = FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length;
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            Line($"Scene: {renderers} renderers, {lodGroups} LOD groups, {lights} lights, about {tris / 1000} thousand triangles in readable meshes");
        }

        void Line(string s)
        {
            _report.AppendLine(s);
            Debug.Log("[Benchmark] " + s);
        }

        void Finish()
        {
            try
            {
                if (!string.IsNullOrEmpty(OutputFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
                    File.WriteAllText(OutputFile, _report.ToString());
                }
            }
            catch (Exception e) { Debug.LogWarning("Benchmark: could not write the report: " + e.Message); }
            Done = true;
        }
    }
}
