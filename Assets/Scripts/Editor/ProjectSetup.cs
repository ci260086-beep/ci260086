using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeepDrill.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepDrill.Editor
{
    /// <summary>Command line entry points: project setup and Windows build.</summary>
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string LibraryPath = "Assets/Resources/ArtLibrary.asset";

        [MenuItem("DeepDrill/Setup Project")]
        public static void Run()
        {
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            BuildArtLibrary();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Deep Drill";
            PlayerSettings.bundleVersion = "0.1";
            PlayerSettings.companyName = "DeepDrill";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.gcIncremental = true;

            AssetDatabase.SaveAssets();
            Debug.Log("DeepDrill setup finished");
        }

        // Starts the next Play from a fresh game. The old save is renamed, not deleted, so it can be put back.
        [MenuItem("DeepDrill/New Game (Reset Save)")]
        public static void ResetSave()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("DeepDrill", "先停止 Play 再重置存档，否则退出时会把当前进度写回去。", "好");
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, "save.json");
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("DeepDrill", "还没有存档，下次 Play 就是新游戏。", "好");
                return;
            }
            string backup = path + ".old-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(path, backup);
            Debug.Log($"[DeepDrill] Save moved to {backup}. The next Play starts a new game.");
            EditorUtility.DisplayDialog("DeepDrill", "存档已重置，下次 Play 从头开始。\n旧存档改名保存在：\n" + backup, "好");
        }

        [MenuItem("DeepDrill/Build Windows")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/DeepDrill.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"Build result: {report.summary.result}, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build failed");
        }

        /// <summary>Enters Play mode at the real window size; the --shots flag drives the rest.</summary>
        public static void PlayAndCapture()
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1280, 720, "DeepDrill");
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        public static void RefreshArtLibrary()
        {
            Directory.CreateDirectory("Assets/Resources");
            BuildArtLibrary();
            AssetDatabase.SaveAssets();
        }

        private static void BuildArtLibrary()
        {
            var sprites = new List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
            }

            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.SetSprites(sprites);
            EditorUtility.SetDirty(library);
            Debug.Log($"ArtLibrary holds {sprites.Count} sprites");
        }
    }
}
