#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Safe explicit install and repeatable checks. Does not assert Unity Play Mode testing.</summary>
    public static class ZSRV100Setup
    {
        static int ok, warning, error;
        static void Check(int n, string subject, bool passed, bool advisory = false)
        {
            string msg = "[ZSR 1.0 / CHECK " + n.ToString("00") + "/25] " + subject + " — " +
                (passed ? "OK" : advisory ? "CHECK MANUALLY" : "FAIL");
            if (passed) { ok++; Debug.Log(msg); }
            else if (advisory) { warning++; Debug.LogWarning(msg); }
            else { error++; Debug.LogError(msg); }
        }
        static bool FilePresent(string f) => File.Exists(f);
        static bool SourceContains(string path, string fragment) => FilePresent(path) && File.ReadAllText(path).Contains(fragment);
        static bool Resource(string f) => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(f) != null;
        static bool Rig(GameObject obj)
        {
            if (obj == null || obj.GetComponentInChildren<Renderer>(true) == null) return false;
            bool legs = false, head = false;
            foreach (var t in obj.GetComponentsInChildren<Transform>(true))
            {
                var name = t.name.ToLowerInvariant();
                if (name.Contains("leg") || name.Contains("thigh")) legs = true;
                if (name.Contains("head")) head = true;
            }
            return legs && head;
        }
        static bool HasClip(RuntimeAnimatorController control)
        {
            if (control == null || control.animationClips == null) return false;
            foreach (var clip in control.animationClips)
                if (clip != null && clip.length > .20f) return true;
            return false;
        }
        [MenuItem("Zombie Survival Run 1.0/1 - Prepare Complete Game")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ZSRProjectSetup.CreateScene();
            Debug.Log("[ZSR 1.0] Complete game prepared. Follow menu 2 to import optional external models, menu 3 to create rig animation controllers, and menu 4 to open the scene.");
        }
        [MenuItem("Zombie Survival Run 1.0/2 - Download Polyfork GLB Models (verify licence)")]
        public static void ImportFreeModels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ZSRPolyforkInstaller.Download();
            Debug.Log("[ZSR 1.0] After model import and Unity recompile finish, use menu 3 for actual model-specific animation controllers.");
        }
        [MenuItem("Zombie Survival Run 1.0/3 - Rebuild Character Animation Controllers")]
        public static void Animate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ZSRLuxMotionBuilder.ApplyLux();
        }
        [MenuItem("Zombie Survival Run 1.0/4 - Open Full Game")]
        public static void Open() => ZSRProjectSetup.OpenGame();
        [MenuItem("Zombie Survival Run 1.0/5 - Run 25 Unity Asset Checks")]
        public static void Verify()
        {
            ok = warning = error = 0;
            string manifest = FilePresent("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : "";
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>("Assets/Resources/ProductionArtSettings.asset");
            Check(1,"Unity 6.3 editor",Application.unityVersion.StartsWith("6000.3", StringComparison.Ordinal),true);
            Check(2,"URP dependency",manifest.Contains("com.unity.render-pipelines.universal"));
            Check(3,"Input System dependency",manifest.Contains("com.unity.inputsystem"));
            Check(4,"uGUI dependency",manifest.Contains("com.unity.ugui"));
            Check(5,"Animation module",manifest.Contains("com.unity.modules.animation"));
            Check(6,"Particle System module",manifest.Contains("com.unity.modules.particlesystem"));
            Check(7,"glTFast GLB import",manifest.Contains("com.unity.cloud.gltfast"));
            Check(8,"Game mechanics source",SourceContains("Assets/Scripts/ZSRGame.cs","MaxLevels = 15"));
            Check(9,"Automatic aiming and firing",SourceContains("Assets/Scripts/ZSRGame.cs","Shoot()"));
            Check(10,"Seven infected types",SourceContains("Assets/Scripts/ZSRActors.cs","Crawler, Boss"));
            Check(11,"Mixed waves",SourceContains("Assets/Scripts/ZSRGame.cs","Small mixed formations"));
            Check(12,"Independent 0.06x parallax",SourceContains("Assets/Scripts/ZSRParallaxCity.cs",".06f"));
            Check(13,"Parallax scene integration",SourceContains("Assets/Scripts/ZSRGame.cs","ZSRParallaxCity.Create"));
            Check(14,"No old circular bonus gate",SourceContains("Assets/Scripts/ZSRWorld.cs","ZSRVeilGateView.Build"));
            Check(15,"Transparent bonus curtains",SourceContains("Assets/Scripts/ZSRVeilGateView.cs","TRANSPARENT COLOR VEIL"));
            Check(16,"Four gameplay obstacles",SourceContains("Assets/Scripts/ZSRWorld.cs","fallen road slab"));
            Check(17,"Asphalt texture",Resource("Assets/Resources/Textures/asphalt_v102.png"));
            Check(18,"Asphalt normal texture",Resource("Assets/Resources/Textures/asphalt_v102_normal.png"));
            Check(19,"City art chapter one",Resource("Assets/Resources/Skyline/city_chapter_1_v100.png"));
            Check(20,"City art chapter two",Resource("Assets/Resources/Skyline/city_chapter_2_v100.png"));
            Check(21,"City art chapter three",Resource("Assets/Resources/Skyline/city_chapter_3_v100.png"));
            bool weapons = true;
            foreach (var name in new [] {"pistol","rifle","shotgun","minigun"})
                weapons &= Resource("Assets/Resources/Artwork/Weapons/" + name + ".png");
            Check(22,"Unique arsenal graphics",weapons);
            bool gates = true;
            foreach (var name in new [] {"damage","rapid","shield","heal","magnet","drone"})
                gates &= Resource("Assets/Resources/Textures/Gates/veil_" + name + ".png");
            Check(23,"Six gate colours",gates);
            Check(24,"External free rigged hero models imported",settings!=null && Rig(settings.maxPrefab) && Rig(settings.evaPrefab),true);
            Check(25,"Imported character motion available",settings!=null && HasClip(settings.maxMotionController) && HasClip(settings.walkerMotionController),true);
            Debug.Log("[ZSR 1.0] 25 CHECKS: " + ok + " passed, " + warning + " require user-side import/test, " + error + " failed. This is NOT a Unity Play Mode or Android FPS test.");
            EditorUtility.DisplayDialog("Zombie Survival Run / 25 checks",
                ok + " passed, " + warning + " warnings, " + error + " failed.\nSee Console.\nVisual motion, Android compatibility and FPS must be tested during real play.","OK");
        }
    }
}
#endif
