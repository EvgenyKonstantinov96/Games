using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Import the shipped FBX skins and their single baked motion take.</summary>
    public sealed class ZSRProductionArtImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/ProductionArt/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder) || Path.GetExtension(assetPath) != ".fbx") return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = name.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = name.StartsWith("Highway") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.anisoLevel = name.StartsWith("Highway") ? 4 : 1;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = name == "Horizon" ? 1024 : 512;
            android.format = name == "Highway_normal" ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
