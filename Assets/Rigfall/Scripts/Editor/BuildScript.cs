// Command-line and menu builds. GitHub Actions (GameCI) calls
//   Rigfall.EditorTools.BuildScript.BuildWebGL
// so the game can be built in the cloud without anyone installing the Unity Editor.
// The project has no hand-made scenes: this script creates an empty one and the game
// bootstraps itself from code (see Runtime/Systems/Bootstrap.cs).
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rigfall.EditorTools
{
    public static class BuildScript
    {
        const string ScenePath = "Assets/Rigfall/Scenes/Main.unity";

        [MenuItem("Rigfall/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, Arg("-customBuildPath", "build/WebGL/Rigfall"));

        [MenuItem("Rigfall/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, Arg("-customBuildPath", "build/Windows/Rigfall.exe"));

        [MenuItem("Rigfall/Create Main Scene")]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1])) return args[i + 1];
            return fallback;
        }

        static void ConfigurePlayer(BuildTarget target)
        {
            PlayerSettings.companyName = "Kendrew Flor S. Arrabe";
            PlayerSettings.productName = "Rigfall";
            string version = Arg("-buildVersion", "");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            PlayerSettings.colorSpace = ColorSpace.Gamma;          // vertex colours are authored in sRGB
            PlayerSettings.runInBackground = false;
            PlayerSettings.stripEngineCode = false;                // everything is created from code at runtime
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)), ManagedStrippingLevel.Low);

            if (target == BuildTarget.WebGL)
            {
                // GitHub Pages can't send Content-Encoding headers for pre-compressed files,
                // so let the Unity loader decompress in JavaScript.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            }

            // The game reads input with the built-in Input Manager (no extra packages).
            SetProjectSetting("activeInputHandler", 0);
        }

        static void SetProjectSetting(string property, int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogWarning("Rigfall build: no project setting " + property); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Build(BuildTarget target, string path)
        {
            EnsureScene();
            ConfigurePlayer(target);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("Rigfall build: " + summary.result + " · " + summary.totalSize / (1024 * 1024) + " MB · " + summary.totalErrors + " errors → " + path);
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
