using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public sealed class ZSRMenuArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/") &&
                !assetPath.StartsWith("Assets/Resources/Artwork/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_4x4;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
