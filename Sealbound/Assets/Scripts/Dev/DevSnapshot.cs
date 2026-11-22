using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Sealbound.Enemies;
using Sealbound.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sealbound.Dev
{
    /// <summary>
    /// Developer check, not part of the game: plays a short scripted tour of the test field and saves
    /// pictures and a report, so changes can be checked on a machine without anyone at the keyboard.
    /// Started by the editor's "PlayCheck" batch command.
    /// </summary>
    public class DevSnapshot : MonoBehaviour
    {
        public static bool Done;
        public static string OutputFolder;

        readonly StringBuilder _report = new StringBuilder();
        readonly List<string> _problems = new List<string>();
        int _errors, _warnings;

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) _warnings++;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                string line = type + ": " + message.Split('\n')[0];
                if (!_problems.Contains(line) && _problems.Count < 30) _problems.Add(line + " | " + (stack ?? "").Split('\n')[0]);
            }
        }

        IEnumerator Start()
        {
            Done = false;
            if (string.IsNullOrEmpty(OutputFolder)) OutputFolder = Path.Combine(Application.dataPath, "..", "PlayCheck");
            Directory.CreateDirectory(OutputFolder);
            yield return new WaitForSeconds(1.5f);

            var motor = FindAnyObjectByType<PlayerMotor>();
            var combat = motor != null ? motor.GetComponent<PlayerCombat>() : null;
            Line("Player found: " + (motor != null));
            if (motor == null) { Finish(); yield break; }
            var p = motor.transform;

            Describe(p.gameObject);
            Line("Animated parts on player: " + p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);

            int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            Line($"Frame rate at start: {frames / 2f:0} fps");

            Shot("01_hero_front", p.position + p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
            ShotMain("02_gameplay_view");

            // Mid-swing of the first sword attack.
            if (combat != null)
            {
                Attack(combat, combat.Kit.basicCombo[0]);
                yield return new WaitForSeconds(combat.Kit.basicCombo[0].windup + combat.Kit.basicCombo[0].active * 0.5f);
                Shot("03_sword_swing", p.position + p.forward * 3f + p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(1f);

                CallPrivate(combat, "EquipKit", combat.mageKit, true);
                yield return new WaitForSeconds(0.6f);
                Shot("04_mage_front", p.position + p.forward * 3.2f + Vector3.up * 1.5f, p.position + Vector3.up * 0.95f, 40f);
                Attack(combat, combat.Kit.basicCombo[0]);
                yield return new WaitForSeconds(combat.Kit.basicCombo[0].windup * 0.9f);
                Shot("05_mage_cast", p.position + p.forward * 3f - p.right * 1.5f + Vector3.up * 1.6f, p.position + Vector3.up * 1f, 45f);
                yield return new WaitForSeconds(1f);
                CallPrivate(combat, "EquipKit", combat.swordKit, false);
            }

            Shot("06_training_yard", new Vector3(-6f, 6f, -22f), new Vector3(7f, 0.5f, -8f), 55f);
            Shot("07_field_wide", new Vector3(0f, 22f, -45f), new Vector3(0f, 2f, 55f), 60f);
            Shot("08_ruins", new Vector3(8f, 7f, 42f), new Vector3(0f, 5f, 70f), 55f);

            // The warren: step in so the goblins come out.
            motor.Teleport(new Vector3(-45f, 0.2f, -15f));
            yield return new WaitForSeconds(3.5f);
            Shot("09_warren", new Vector3(-30f, 9f, -30f), new Vector3(-45f, 0.5f, -15f), 55f);
            var gob = FindAnyObjectByType<Goblin>();
            Line("Goblins out: " + FindObjectsByType<Goblin>(FindObjectsSortMode.None).Length);
            if (gob != null)
            {
                Describe(gob.gameObject);
                var g = gob.transform;
                Shot("10_goblin_close", g.position + g.forward * 2.4f + Vector3.up * 1.1f, g.position + Vector3.up * 0.6f, 40f);
                float wait = 0f;
                Goblin winding = null;
                while (wait < 8f && winding == null)
                {
                    foreach (var x in FindObjectsByType<Goblin>(FindObjectsSortMode.None))
                        if (x.CurrentState == Goblin.State.Windup && x.StateTime > 0.2f) { winding = x; break; }
                    wait += Time.deltaTime;
                    yield return null;
                }
                if (winding != null)
                {
                    var w = winding.transform;
                    Shot("11_goblin_windup", w.position + w.forward * 2.6f + w.right * 1f + Vector3.up * 1.2f, w.position + Vector3.up * 0.6f, 42f);
                }
                else Line("No goblin wind-up seen within 8 s.");
            }

            frames = 0; t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            Line($"Frame rate in the warren fight: {frames / 2f:0} fps");
            Finish();
        }

        void Finish()
        {
            Line($"Errors: {_errors}, warnings: {_warnings}");
            foreach (var pr in _problems) Line("  " + pr);
            File.WriteAllText(Path.Combine(OutputFolder, "report.txt"), _report.ToString());
            Done = true;
        }

        void Line(string s) { _report.AppendLine(s); Debug.Log("[PlayCheck] " + s); }

        void Describe(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (!(r is TrailRenderer)) b.Encapsulate(r.bounds);
            Line($"{go.name}: visible size {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m, bottom at {b.min.y - go.transform.position.y:0.00} m");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Outline")) continue;
                var m = r.sharedMaterial;
                Line($"  part {r.name} ({r.GetType().Name}, active {r.gameObject.activeInHierarchy}) material {(m != null ? m.name : "none")} texture {(m != null && m.mainTexture != null ? m.mainTexture.name : "none")}");
            }
        }

        static void Attack(PlayerCombat combat, AttackData a)
        {
            var begin = typeof(PlayerCombat).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultType = typeof(PlayerCombat).GetNestedType("ChainResult", BindingFlags.NonPublic);
            if (begin == null || resultType == null) return;
            begin.Invoke(combat, new object[] { a, -1, Enum.ToObject(resultType, 0) });
        }

        static void CallPrivate(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(target, args);
        }

        void ShotMain(string name)
        {
            var main = Camera.main;
            if (main == null) return;
            Shot(name, main.transform.position, main.transform.position + main.transform.forward, main.fieldOfView);
        }

        void Shot(string name, Vector3 from, Vector3 lookAt, float fov)
        {
            const int w = 1280, h = 720;
            var go = new GameObject("Snapshot Camera");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) cam.CopyFrom(main);
            cam.enabled = false;
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - from);
            cam.fieldOfView = fov;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), tex.EncodeToPNG());
            Line("Saved " + name + ".png");

            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }
    }
}
