using UnityEditor;
using UnityEngine;

namespace NumberRoad.EditorTools
{
    /// <summary>
    /// Import settings for the Blender FBX files in Resources/Models: no cameras,
    /// lights, animation or tangents, and the Blender material "NR_VertexColor"
    /// is remapped to the project's NR_Toon material.
    /// </summary>
    public sealed class ModelImportRules : AssetPostprocessor
    {
        public const string ModelsFolder = ProjectSetup.Root + "/Resources/Models";
        public const string BlenderMaterial = "NR_VertexColor";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            Apply((ModelImporter)assetImporter);
        }

        /// <summary>Returns true when something changed.</summary>
        public static bool Apply(ModelImporter importer)
        {
            bool changed = false;

            void Set<T>(T current, T wanted, System.Action<T> assign)
            {
                if (Equals(current, wanted)) return;
                assign(wanted);
                changed = true;
            }

            Set(importer.globalScale, 1f, v => importer.globalScale = v);
            Set(importer.useFileScale, true, v => importer.useFileScale = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.importVisibility, false, v => importer.importVisibility = v);
            Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set(importer.importNormals, ModelImporterNormals.Import, v => importer.importNormals = v);
            Set(importer.importTangents, ModelImporterTangents.None, v => importer.importTangents = v);
            Set(importer.meshCompression, ModelImporterMeshCompression.Off, v => importer.meshCompression = v);
            Set(importer.isReadable, false, v => importer.isReadable = v);
            Set(importer.addCollider, false, v => importer.addCollider = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => importer.materialImportMode = v);

            var toon = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.MaterialPath("NR_Toon"));
            if (toon != null)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), BlenderMaterial);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out Object current) || current != toon)
                {
                    importer.AddRemap(id, toon);
                    changed = true;
                }
            }
            return changed;
        }
    }

    /// <summary>The Blender portraits in Resources/Portraits are UI images: no mipmaps, clamped, clean alpha edges.</summary>
    public sealed class PortraitImportRules : AssetPostprocessor
    {
        public const string PortraitsFolder = ProjectSetup.Root + "/Resources/Portraits";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PortraitsFolder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
