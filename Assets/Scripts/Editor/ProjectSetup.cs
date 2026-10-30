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

            AssetDatabase.SaveAssets();
            Debug.Log("DeepDrill setup finished");
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
