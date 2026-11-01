using System;
using System.IO;
using DeepDrill.Game;
using UnityEditor;
using UnityEngine;

namespace DeepDrill.Editor
{
    /// <summary>
    /// Runs the screenshot play-through inside an editor that is already open, the same one
    /// tools\check.ps1 starts from the command line. Shots go to Screenshots\check, and the
    /// [demo] lines, warnings and errors of that Play go to Logs\check_play.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class CheckMenu
    {
        private const string ShotsFolder = "Screenshots/check";
        private const string LogPath = "Logs/check_play.txt";
        private const string ActiveKey = "DeepDrill.CheckActive";

        static CheckMenu()
        {
            // survives the domain reload that entering Play mode does
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Application.logMessageReceived += Record;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("DeepDrill/Check: Play And Capture")]
        public static void PlayAndCaptureInEditor()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("DeepDrill", "先停止 Play。", "好");
                return;
            }
            string root = Directory.GetCurrentDirectory();
            string shots = Path.Combine(root, ShotsFolder);
            if (Directory.Exists(shots))
                foreach (string old in Directory.GetFiles(shots, "*.png")) File.Delete(old);
            string save = Path.Combine(root, "Temp", "check_save.json");
            if (File.Exists(save)) File.Delete(save);
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            File.WriteAllText(Path.Combine(root, LogPath), $"[check] started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            Environment.SetEnvironmentVariable(CommandLine.EnvName("--shots"), shots);
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--save"), save);
            SessionState.SetBool(ActiveKey, true);
            Application.logMessageReceived += Record;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ProjectSetup.PlayAndCapture();
        }

        private static void Record(string message, string stack, LogType type)
        {
            bool demo = message.StartsWith("[demo]", StringComparison.Ordinal);
            if (!demo && type == LogType.Log) return;
            string line = type == LogType.Log ? message : $"{type}: {message}\n{stack}";
            File.AppendAllText(LogPath, line.TrimEnd() + "\n");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--shots"), null);
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--save"), null);
            SessionState.SetBool(ActiveKey, false);
            Application.logMessageReceived -= Record;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            int count = Directory.Exists(ShotsFolder) ? Directory.GetFiles(ShotsFolder, "*.png").Length : 0;
            File.AppendAllText(LogPath, $"[check] finished {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {count} shots\n");
        }
    }
}
