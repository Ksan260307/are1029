using System;
using System.IO;
using NumberRoad.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NumberRoad.EditorTools
{
    /// <summary>
    /// Creates/refreshes everything that is generated rather than hand-made:
    /// materials, model import settings, the boot scene and the player settings
    /// for Android, iOS/iPadOS and WebGL. Idempotent; the build script runs it
    /// before every build so CI always builds with the intended settings.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string Root = "Assets/NumberRoad";
        public const string ScenePath = Root + "/Scenes/Main.unity";
        public const string MaterialsFolder = Root + "/Resources/Materials";
        public const string IconPath = Root + "/Art/Icons/icon_1024.png";
        public const string ProductName = "Number Road";
        public const string CompanyName = "Ksan";
        public const string BundleId = "com.ksan260307.numberroad";
        public const string WebGLTemplate = "PROJECT:NumberRoad";

        static ProjectSetup()
        {
            // first open of a fresh clone: generate the missing assets once the editor is idle
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && NeedsSetup()) Run();
            };
        }

        public static string MaterialPath(string name) => $"{MaterialsFolder}/{name}.mat";

        static bool NeedsSetup()
        {
            return !File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<Material>(MaterialPath("NR_Toon")) == null;
        }

        [MenuItem("Number Road/Setup Project", priority = 0)]
        public static void Run()
        {
            EnsureMaterials();
            ConfigureModels();
            EnsureScene();
            SyncWebIcons();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[NumberRoad] Project setup complete.");
        }

        /// <summary>Copies the Blender-rendered icons into the WebGL template (home-screen / PWA icons).</summary>
        static void SyncWebIcons()
        {
            string iconFolder = Path.GetDirectoryName(IconPath);
            const string templateData = "Assets/WebGLTemplates/NumberRoad/TemplateData";
            if (iconFolder == null || !Directory.Exists(templateData)) return;
            foreach (int size in new[] { 180, 192, 512 })
            {
                string source = Path.Combine(iconFolder, $"icon_{size}.png");
                string target = $"{templateData}/icon-{size}.png";
                if (!File.Exists(source)) continue;
                if (File.Exists(target) && File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(target))) continue;
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target);
            }
        }

        /// <summary>Command line: -executeMethod NumberRoad.EditorTools.ProjectSetup.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureMaterials()
        {
            EnsureFolder(MaterialsFolder);
            EnsureMaterial("NR_Toon", "NumberRoad/Toon");
            EnsureMaterial("NR_Transparent", "NumberRoad/Transparent");
            EnsureMaterial("NR_Additive", "NumberRoad/Additive");
            EnsureMaterial("NR_Water", "NumberRoad/Water");
        }

        static void EnsureMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' was not found. Did the shaders fail to compile?");
            string path = MaterialPath(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
                return;
            }
            if (material.shader == shader && material.enableInstancing) return;
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        static void ConfigureModels()
        {
            if (!AssetDatabase.IsValidFolder(ModelImportRules.ModelsFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelImportRules.ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && ModelImportRules.Apply(importer))
                {
                    importer.SaveAndReimport();
                }
            }
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Game").AddComponent<GameRoot>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length != 1 || scenes[0].path != ScenePath || !scenes[0].enabled)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.WebGL, NamedBuildTarget.Standalone })
            {
                PlayerSettings.SetApplicationIdentifier(target, BundleId);
            }

            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.statusBarHidden = true;

            // every orientation is allowed by the OS; GameRoot locks phones to portrait at runtime
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android: 64-bit IL2CPP APK, drawn under the notch (UI handles the safe area)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            EditorUserBuildSettings.buildAppBundle = false;

            // iOS / iPadOS
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;

            // WebGL: gzip + JS decompression fallback works on any static host (GitHub Pages)
            PlayerSettings.WebGL.template = WebGLTemplate;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}
