using System;
using System.IO;
using Sealbound.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sealbound.EditorTools
{
    /// <summary>
    /// Batch check for developers: opens the test scene, plays it, runs <see cref="DevSnapshot"/>
    /// (pictures and a report), then quits. Usage:
    /// Unity -batchmode -projectPath &lt;project&gt; -executeMethod Sealbound.EditorTools.PlayCheck.Run [-playCheckOut &lt;folder&gt;]
    /// </summary>
    [InitializeOnLoad]
    public static class PlayCheck
    {
        const string ActiveKey = "Sealbound.PlayCheck.Active";
        const string OutKey = "Sealbound.PlayCheck.Out";
        const string StartKey = "Sealbound.PlayCheck.Start";
        const double TimeoutSeconds = 180;

        static PlayCheck()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlayCheck"));
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-playCheckOut") output = Path.GetFullPath(args[i + 1]);

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
            DevSnapshot.OutputFolder = SessionState.GetString(OutKey, "PlayCheck");
            new GameObject("Play Check").AddComponent<DevSnapshot>();
        }

        static void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f);
            if (DevSnapshot.Done || elapsed > TimeoutSeconds)
            {
                bool ok = DevSnapshot.Done;
                SessionState.EraseBool(ActiveKey);
                EditorApplication.update -= Tick;
                Debug.Log(ok ? "[PlayCheck] Finished." : "[PlayCheck] Timed out.");
                EditorApplication.Exit(ok ? 0 : 3);
            }
        }
    }
}
