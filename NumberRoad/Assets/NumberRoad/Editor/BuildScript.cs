using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NumberRoad.EditorTools
{
    /// <summary>
    /// Builds from the editor menu or from the command line / GameCI:
    ///   -executeMethod NumberRoad.EditorTools.BuildScript.BuildFromCommandLine
    ///   -customBuildTarget WebGL|Android|iOS -customBuildPath &lt;path&gt;
    ///   [-buildVersion 1.2.3] [-androidVersionCode 42]
    ///   [-androidKeystoreName file -androidKeystorePass .. -androidKeyaliasName .. -androidKeyaliasPass ..]
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Number Road/Build/WebGL", priority = 20)]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, DefaultPath(BuildTarget.WebGL), false);

        [MenuItem("Number Road/Build/Android APK", priority = 21)]
        public static void BuildAndroid() => Build(BuildTarget.Android, DefaultPath(BuildTarget.Android), false);

        [MenuItem("Number Road/Build/iOS Xcode Project", priority = 22)]
        public static void BuildIOS() => Build(BuildTarget.iOS, DefaultPath(BuildTarget.iOS), false);

        static string DefaultPath(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android: return "Builds/Android/NumberRoad.apk";
                case BuildTarget.iOS: return "Builds/iOS";
                default: return "Builds/" + target;
            }
        }

        public static void BuildFromCommandLine()
        {
            Dictionary<string, string> args = ParseArgs(Environment.GetCommandLineArgs());
            string targetName = Arg(args, "customBuildTarget") ?? Arg(args, "buildTarget");
            if (string.IsNullOrEmpty(targetName) || !Enum.TryParse(targetName, true, out BuildTarget target))
            {
                Debug.LogError($"[NumberRoad] Unknown or missing build target '{targetName}'.");
                EditorApplication.Exit(2);
                return;
            }

            string path = Arg(args, "customBuildPath");
            if (string.IsNullOrEmpty(path)) path = DefaultPath(target);
            ApplyVersion(Arg(args, "buildVersion"), Arg(args, "androidVersionCode"));
            if (target == BuildTarget.Android) ApplyKeystore(args);

            bool ok;
            try
            {
                ok = Build(target, path, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(BuildTarget target, string path, bool batch)
        {
            ProjectSetup.Run();
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }

            string directory = target == BuildTarget.Android ? Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[NumberRoad] Build {target}: {summary.result} | {summary.totalSize / (1024f * 1024f):0.0} MB | " +
                      $"{summary.totalTime:mm\\:ss} | errors {summary.totalErrors} | {path}");

            bool ok = summary.result == BuildResult.Succeeded;
            if (ok && !batch) EditorUtility.RevealInFinder(path);
            return ok;
        }

        static void ApplyVersion(string version, string versionCode)
        {
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            if (int.TryParse(versionCode, out int code) && code > 0)
            {
                PlayerSettings.Android.bundleVersionCode = code;
                PlayerSettings.iOS.buildNumber = code.ToString();
            }
        }

        static void ApplyKeystore(Dictionary<string, string> args)
        {
            string keystore = Arg(args, "androidKeystoreName");
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("[NumberRoad] No keystore given: the APK is signed with the debug key.");
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
            PlayerSettings.Android.keystorePass = Arg(args, "androidKeystorePass");
            PlayerSettings.Android.keyaliasName = Arg(args, "androidKeyaliasName");
            PlayerSettings.Android.keyaliasPass = Arg(args, "androidKeyaliasPass");
            Debug.Log("[NumberRoad] Signing the APK with the provided keystore.");
        }

        static Dictionary<string, string> ParseArgs(string[] argv)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < argv.Length; i++)
            {
                if (!argv[i].StartsWith("-")) continue;
                string key = argv[i].TrimStart('-');
                string value = i + 1 < argv.Length && !argv[i + 1].StartsWith("-") ? argv[i + 1] : string.Empty;
                result[key] = value;
            }
            return result;
        }

        static string Arg(Dictionary<string, string> args, string key)
        {
            return args.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : null;
        }
    }
}
