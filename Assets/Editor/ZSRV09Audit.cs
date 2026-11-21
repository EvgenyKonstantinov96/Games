#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRV09Audit
    {
        [MenuItem("Zombie Survival Run 0.9/1 - REBUILD v0.9 Character Motion")]
        public static void RebuildMotion()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Zombie Survival Run v0.9",
                    "Exit Play mode before rebuilding animation controllers.", "OK");
                return;
            }
            ZSRLuxMotionBuilder.ApplyLux();
        }

        [MenuItem("Zombie Survival Run 0.9/2 - CHECK v0.9 Art Resources")]
        public static void Validate()
        {
            int ok = 0, missing = 0;
            string[] weapons={"pistol","rifle","shotgun","minigun"};
            foreach (var name in weapons)
                Check("Assets/Resources/Artwork/Weapons/"+name+".png",ref ok,ref missing);
            string[] veils={"damage","rapid","shield","heal","magnet","drone"};
            foreach (var name in veils)
                Check("Assets/Resources/Textures/Gates/veil_"+name+".png",ref ok,ref missing);
            var art=AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(
                "Assets/Resources/ProductionArtSettings.asset");
            if (art==null) { Debug.LogWarning("[ZSR v0.9] ProductionArtSettings.asset is missing. Imported models must be relinked."); missing++; }
            else
            {
                if (art.maxPrefab==null || art.evaPrefab==null)
                    Debug.LogWarning("[ZSR v0.9] Max/Eva prefabs missing; fallback models will be shown.");
                if (art.maxMotionController==null || art.evaMotionController==null)
                    Debug.LogWarning("[ZSR v0.9] Click menu 1 to regenerate new natural arm-targeted animation clips.");
            }
            Debug.Log("[ZSR v0.9] Art audit: "+ok+" present, "+missing+" missing. " +
                "This is a file check, NOT a Unity Game View or Android performance test.");
            EditorUtility.DisplayDialog("Zombie Survival Run v0.9",
                "File check: "+ok+" present, "+missing+" missing.\nSee Console for details.\n" +
                "Run menu 1 once after importing the patch, then test in Game View.", "OK");
        }
        static void Check(string path,ref int ok,ref int missing)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path)!=null) ok++;
            else { missing++; Debug.LogError("[ZSR v0.9] Missing texture: "+path); }
        }
    }
}
#endif
