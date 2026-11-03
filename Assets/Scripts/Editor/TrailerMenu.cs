using System;
using System.IO;
using DeepDrill.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepDrill.Editor
{
    /// <summary>
    /// Records the trailer's clips: Play at 1920x1080 (six times the 320x180 stage) with
    /// TrailerDriver scripting the shots into Recordings/trailer/&lt;clip&gt;/f_00000.png, on a
    /// throwaway save. Progress goes to Logs/trailer.txt. tools/trailer cuts them together.
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerMenu
    {
        private const string Folder = "Recordings/trailer";
        private const string LogPath = "Logs/trailer.txt";
        private const string ActiveKey = "DeepDrill.TrailerActive";

        static TrailerMenu()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Application.logMessageReceived += Record;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("DeepDrill/Trailer: Record")]
        public static void RecordTrailer()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("DeepDrill", "先停止 Play。", "好");
                return;
            }
            string root = Directory.GetCurrentDirectory();
            string save = Path.Combine(root, "Temp", "trailer_save.json");
            if (File.Exists(save)) File.Delete(save);
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            File.WriteAllText(Path.Combine(root, LogPath), $"[trailer] started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            Environment.SetEnvironmentVariable(CommandLine.EnvName("--trailer"), Path.Combine(root, Folder));
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--save"), save);
            SessionState.SetBool(ActiveKey, true);
            Application.logMessageReceived += Record;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "Trailer");
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void Record(string message, string stack, LogType type)
        {
            bool mine = message.StartsWith("[trailer]", StringComparison.Ordinal);
            if (!mine && type == LogType.Log) return;
            if (!mine && type == LogType.Warning) return;
            string line = type == LogType.Log ? message : $"{type}: {message}\n{stack}";
            File.AppendAllText(LogPath, line.TrimEnd() + "\n");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--trailer"), null);
            Environment.SetEnvironmentVariable(CommandLine.EnvName("--save"), null);
            SessionState.SetBool(ActiveKey, false);
            Application.logMessageReceived -= Record;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PlayModeWindow.SetCustomRenderingResolution(1280, 720, "DeepDrill");
            File.AppendAllText(LogPath, $"[trailer] finished {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }
    }
}
