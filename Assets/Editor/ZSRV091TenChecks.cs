#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>
    /// Ten read-only integration checks against the user's imported assets in Unity 6.3.
    /// Does not claim to compile in an external Unity editor or test on Android.
    /// </summary>
    public static class ZSRV091TenChecks
    {
        static int successes, failures, cautions;
        static void Check(int no, string title, bool condition, string detail, bool warning = false)
        {
            string label = "[ZSR 0.9.1a / " + no + "/10] " + title + " — " +
                (condition ? "OK" : warning ? "WARNING" : "FAIL") + ". " + detail;
            if (condition) { successes++; Debug.Log(label); }
            else if (warning) { cautions++; Debug.LogWarning(label); }
            else { failures++; Debug.LogError(label); }
        }
        static bool HasClip(RuntimeAnimatorController controller, string state)
        {
            var ctrl = controller as AnimatorController;
            if (ctrl == null || ctrl.layers == null || ctrl.layers.Length == 0) return false;
            foreach (var item in ctrl.layers[0].stateMachine.states)
            {
                if (item.state.name != state) continue;
                var clip = item.state.motion as AnimationClip;
                return clip != null && AnimationUtility.GetCurveBindings(clip).Length > 0;
            }
            return false;
        }
        static bool HasRig(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponentsInChildren<Renderer>(true).Length == 0) return false;
            bool left = false, right = false;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                var n=t.name.ToLowerInvariant().Replace("mixamorig", "").Replace(":", "").Replace("_", "").Replace("-", "");
                if (n.EndsWith("leftupleg") || n.EndsWith("leftupperleg")) left = true;
                if (n.EndsWith("rightupleg") || n.EndsWith("rightupperleg")) right = true;
            }
            return left && right;
        }
        static bool Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null;
        [MenuItem("Zombie Survival Run 0.9/5 - VERIFY TEN INTEGRATION CHECKS")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ZSR 0.9.1a", "Stop Play Mode to inspect imported assets.", "OK");
                return;
            }
            successes = failures = cautions = 0;
            string manifestPath = "Packages/manifest.json";
            string json = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "";
            var s=AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>("Assets/Resources/ProductionArtSettings.asset");
            Check(1, "Unity 6.3", Application.unityVersion.StartsWith("6000.3", StringComparison.Ordinal), Application.unityVersion, true);
            Check(2, "Animation module", json.Contains("com.unity.modules.animation"), "Required for UnityEngine.Animator");
            Check(3, "glTFast package", json.Contains("com.unity.cloud.gltfast"), "Required for Polyfork GLB import");
            Check(4, "Production model references", s != null && s.maxPrefab != null && s.evaPrefab != null && s.walkerPrefab != null,
                "Max, Eva and Walker prefabs must all be assigned in ProductionArtSettings",true);
            Check(5, "Renderable hero skeletons", s != null && HasRig(s.maxPrefab) && HasRig(s.evaPrefab),
                "Both characters need visible renderers and left/right upper-leg bones",true);
            bool heroMotion = s != null && HasClip(s.maxMotionController, "Run") && HasClip(s.maxMotionController, "RunShoot") &&
                HasClip(s.evaMotionController, "Run") && HasClip(s.evaMotionController, "RunShoot");
            Check(6, "Hero animation clips", heroMotion, "Both Run and RunShoot need actual curves for Max and Eva",true);
            bool infectedMotion = s != null && HasClip(s.walkerMotionController, "Run") &&
                HasClip(s.runnerMotionController, "Run") && HasClip(s.bruteMotionController, "Run");
            Check(7, "Zombie animation clips", infectedMotion, "Walker / Runner / Brute need nonempty Run clips",true);
            string[] weapons={"pistol", "rifle", "shotgun", "minigun"};
            var seen=new HashSet<Texture2D>();
            bool arsenal=true;
            foreach (string w in weapons)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Artwork/Weapons/"+w+".png");
                if (texture==null || !seen.Add(texture)) arsenal=false;
            }
            Check(8, "Unique arsenal images", arsenal, "Four distinct textures (pistol/rifle/shotgun/minigun)");
            string[] gateNames={"damage", "rapid", "shield", "heal", "magnet", "drone"};
            bool veils=true;
            foreach(string g in gateNames) if(!Tex("Assets/Resources/Textures/Gates/veil_"+g+".png")) veils=false;
            Check(9,"Six translucent gate textures",veils,"Expected six veil_*.png textures");
            var normalImporter=AssetImporter.GetAtPath("Assets/Resources/Textures/asphalt_v091_normal.png") as TextureImporter;
            bool correctNormal=Tex("Assets/Resources/Textures/asphalt_v091.png") &&
                normalImporter!=null && normalImporter.textureType==TextureImporterType.NormalMap;
            Check(10,"Asphalt / normal map import",correctNormal,
                "Normal map should use NormalMap import mode. Select PNG > Inspector > Texture Type if needed",true);
            Debug.Log("[ZSR 0.9.1a] FINAL: " + successes + " OK, " + cautions +
                " warnings, " + failures + " errors. NOT a Play Mode / Android visual test.");
            EditorUtility.DisplayDialog("Zombie Survival Run / 10 checks",
                successes + " OK; " + cautions + " warnings; " + failures + " errors.\n"+
                "See Console for the exact findings.\n"+
                "This does NOT replace Play Mode, model inspection and Android testing.", "OK");
        }
    }
}
#endif
