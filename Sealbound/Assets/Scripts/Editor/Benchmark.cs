using System;
using System.IO;
using Sealbound.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sealbound.EditorTools
{
    /// <summary>
    /// Measures how smoothly the game runs in the editor, the way it is played: a real window, the
    /// Game view maximised. Run WITHOUT -batchmode:
    /// Unity -projectPath &lt;project&gt; -executeMethod Sealbound.EditorTools.Benchmark.Run [-benchmarkOut &lt;file&gt;]
    /// </summary>
    [InitializeOnLoad]
    public static class Benchmark
    {
        const string ActiveKey = "Sealbound.Benchmark.Active";
        const string OutKey = "Sealbound.Benchmark.Out";
        const string StartKey = "Sealbound.Benchmark.Start";
        const double TimeoutSeconds = 480;

        static Benchmark()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlayCheck", "benchmark.txt"));
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-benchmarkOut") output = Path.GetFullPath(args[i + 1]);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(OutKey, output);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            // Play maximised, as a player would.
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null)
            {
                var w = EditorWindow.GetWindow(gameView);
                if (w != null) w.maximized = true;
            }
            PerfBenchmark.OutputFile = SessionState.GetString(OutKey, "benchmark.txt");
            new GameObject("Benchmark").AddComponent<PerfBenchmark>();
        }

        static void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f);
            if (PerfBenchmark.Done || elapsed > TimeoutSeconds)
            {
                bool ok = PerfBenchmark.Done;
                SessionState.EraseBool(ActiveKey);
                EditorApplication.update -= Tick;
                Debug.Log(ok ? "[Benchmark] Finished." : "[Benchmark] Timed out.");
                EditorApplication.Exit(ok ? 0 : 3);
            }
        }
    }
}
