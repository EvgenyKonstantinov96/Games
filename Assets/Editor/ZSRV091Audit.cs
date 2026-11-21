#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Fast in-editor preflight for the v0.9.1 integration. No false claim of a play test.</summary>
    public static class ZSRV091Audit
    {
        [MenuItem("Zombie Survival Run 0.9/3 - BUILD v0.9.1 Motion Profiles")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ZSR 0.9.1", "Stop Play mode first.", "OK");
                return;
            }
            ZSRLuxMotionBuilder.ApplyLux();
        }

        [MenuItem("Zombie Survival Run 0.9/4 - VALIDATE v0.9.1 Integration")]
        public static void Validate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(
                "Assets/Resources/ProductionArtSettings.asset");
            int passed=0, missing=0;
            if (settings == null)
            {
                Debug.LogError("[ZSR 0.9.1] ProductionArtSettings.asset missing. Install earlier Polyfork setup first.");
                missing++;
            }
            else
            {
                CheckMotion("Max", settings.maxPrefab, settings.maxMotionController, true, ref passed, ref missing);
                CheckMotion("Eva", settings.evaPrefab, settings.evaMotionController, true, ref passed, ref missing);
                CheckMotion("Walker", settings.walkerPrefab, settings.walkerMotionController, false, ref passed, ref missing);
                CheckMotion("Runner", settings.runnerPrefab, settings.runnerMotionController, false, ref passed, ref missing);
                CheckMotion("Armored", settings.armoredPrefab, settings.armoredMotionController, false, ref passed, ref missing);
                CheckMotion("Brute", settings.brutePrefab, settings.bruteMotionController, false, ref passed, ref missing);
                CheckMotion("Toxic", settings.toxicPrefab, settings.toxicMotionController, false, ref passed, ref missing);
                CheckMotion("Crawler", settings.crawlerPrefab, settings.crawlerMotionController, false, ref passed, ref missing);
            }
            CheckTexture("Assets/Resources/Textures/asphalt_v091.png",ref passed,ref missing);
            CheckTexture("Assets/Resources/Textures/asphalt_v091_normal.png",ref passed,ref missing);
            foreach (var suffix in new [] { "damage", "rapid", "shield", "heal", "magnet", "drone" })
                CheckTexture("Assets/Resources/Textures/Gates/veil_"+suffix+".png",ref passed,ref missing);
            Debug.Log("[ZSR 0.9.1] Asset/animation preflight: " + passed + " OK, " + missing + " missing. " +
                "This does not replace Unity Game View and Android testing.");
            EditorUtility.DisplayDialog("Zombie Survival Run 0.9.1",
                passed + " items OK; " + missing + " missing. See Console.\n" +
                "Test actual motions in Play Mode: hero run / shoot, Walker, Runner, Brute, and Eva.", "OK");
        }

        static void CheckTexture(string path, ref int passed,ref int missing)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path)!=null) passed++;
            else { missing++; Debug.LogError("[ZSR 0.9.1] Missing texture " + path); }
        }

        static void CheckMotion(string label, GameObject model, RuntimeAnimatorController controller,
            bool hero, ref int passed, ref int missing)
        {
            if (model == null)
            {
                missing++; Debug.LogWarning("[ZSR 0.9.1] " + label + ": model not assigned. Legacy fallback will be shown.");
                return;
            }
            var candidate=controller as AnimatorController;
            if (candidate==null)
            {
                missing++; Debug.LogWarning("[ZSR 0.9.1] " + label + ": Generic motion controller missing; use menu 3.");
                return;
            }
            bool hasRun=false,hasIdle=false,hasShoot=!hero;
            bool missingCurves=false;
            if (candidate.layers == null || candidate.layers.Length == 0)
            {
                missing++;
                Debug.LogWarning("[ZSR 0.9.1] " + label + ": controller has no animation layers.");
                return;
            }
            foreach (var entry in candidate.layers[0].stateMachine.states)
            {
                bool required=entry.state.name == "Run" || entry.state.name == "Idle" ||
                    (hero && entry.state.name == "RunShoot");
                if (!required) continue;
                var clip = entry.state.motion as AnimationClip;
                bool valid = clip != null && AnimationUtility.GetCurveBindings(clip).Length > 0;
                if (!valid)
                {
                    missingCurves=true;
                    Debug.LogWarning("[ZSR 0.9.1] " + label + ": " + entry.state.name +
                        " has no working keyframed animation clip.");
                }
                if (entry.state.name == "Run") hasRun=valid;
                if (entry.state.name == "Idle") hasIdle=valid;
                if (entry.state.name == "RunShoot") hasShoot=valid;
            }
            if(hasRun && hasIdle && hasShoot && !missingCurves) passed++;
            else { missing++; Debug.LogWarning("[ZSR 0.9.1] " + label +
                ": required animated states missing or empty. Rebuild with menu 3."); }

        }
    }
}
#endif
