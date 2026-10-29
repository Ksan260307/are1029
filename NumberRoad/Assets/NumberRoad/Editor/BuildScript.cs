using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NumberRoad.EditorTools
{
    /// <summary>
    /// Builds. WebGL uses the same method locally (tools/build_webgl.ps1) and on CI (game-ci/unity-builder):
    ///   Unity -batchmode -projectPath NumberRoad -executeMethod NumberRoad.EditorTools.BuildScript.BuildWebGL -customBuildPath ../WebGLBuild
    /// Android / iOS can be built from the editor menu when those modules are installed.
    /// </summary>
    public static class BuildScript
    {
        const string DefaultWebGLOutput = "../WebGLBuild";

        [MenuItem("Number Road/Build/WebGL", priority = 20)]
        public static void BuildWebGLFromMenu() => BuildFromMenu(BuildTarget.WebGL, DefaultWebGLOutput);

        [MenuItem("Number Road/Build/Android APK", priority = 21)]
        public static void BuildAndroidFromMenu() => BuildFromMenu(BuildTarget.Android, "Builds/Android/NumberRoad.apk");

        [MenuItem("Number Road/Build/iOS Xcode Project", priority = 22)]
        public static void BuildIOSFromMenu() => BuildFromMenu(BuildTarget.iOS, "Builds/iOS");

        /// <summary>Command line / CI entry point. Exits the editor with 0 on success.</summary>
        public static void BuildWebGL()
        {
            string output = Arg("-customBuildPath") ?? Arg("-buildPath") ?? DefaultWebGLOutput;
            BuildReport report;
            try
            {
                report = Build(BuildTarget.WebGL, output);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void BuildFromMenu(BuildTarget target, string output)
        {
            BuildReport report = Build(target, output);
            if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(Path.GetFullPath(output));
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1])) return args[i + 1];
            }
            return null;
        }

        static BuildReport Build(BuildTarget target, string output)
        {
            ProjectSetup.Run();
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }

            string directory = target == BuildTarget.Android ? Path.GetDirectoryName(output) : output;
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[NumberRoad] Build {target}: {s.result} | {s.totalSize / (1024f * 1024f):0.0} MB | " +
                      $"{s.totalTime:mm\\:ss} | errors {s.totalErrors} | {Path.GetFullPath(output)}");
            return report;
        }
    }
}
