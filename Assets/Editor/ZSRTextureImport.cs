#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ZombieSurvivalRun.EditorTools
{
    // Correct tangent-space interpretation and mobile-friendly import sizes for PBR maps.
    public sealed class ZSRTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Contains("Assets/Resources/Textures/"))return;
            TextureImporter ti=assetImporter as TextureImporter;
            if(ti==null)return;
            if(assetPath.EndsWith("_normal.png"))
            {
                ti.textureType=TextureImporterType.NormalMap;
                ti.sRGBTexture=false;
                ti.maxTextureSize=512;
            }
            else
            {
                ti.maxTextureSize=1024;
                ti.wrapMode=TextureWrapMode.Repeat;
            }
        }
    }
}
#endif
