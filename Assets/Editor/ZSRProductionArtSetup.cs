#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRProductionArtSetup
    {
        const string SettingsPath = "Assets/Resources/ProductionArtSettings.asset";
        const string GenFolder = "Assets/ProductionArt/Generated";

        [MenuItem("Zombie Survival Run 0.8/7 • Configure Production 3D Art")]
        public static void OpenSettings()
        {
            Directory.CreateDirectory("Assets/Resources");
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ZSRProductionArtSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log("[ZSR Production] Drag imported Max/Eva/Zombie PREFABS and optional animation clips into the Inspector. Generate controllers using menu item 8. Any missing slot keeps legacy visuals.");
        }

        [MenuItem("Zombie Survival Run 0.8/8 • Build Production Animation Controllers")]
        public static void BuildControllers()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(SettingsPath);
            if (settings == null) { OpenSettings(); return; }
            Directory.CreateDirectory(GenFolder);
            var hero = BuildOne("Hero", settings.heroIdle, settings.heroRun, settings.heroHit, settings.heroDeath);
            var zombie = BuildOne("Zombie", settings.zombieIdle, settings.zombieRun, settings.zombieHit, settings.zombieDeath);
            if (hero != null) settings.heroController = hero;
            if (zombie != null) settings.zombieController = zombie;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZSR Production] Controllers created where both Idle and Run clips were supplied. Open 7 • Configure Production 3D Art to inspect results.");
        }

        static RuntimeAnimatorController BuildOne(string prefix, AnimationClip idle, AnimationClip run,
            AnimationClip hit, AnimationClip death)
        {
            if (idle == null || run == null)
            {
                Debug.LogWarning("[ZSR Production] " + prefix + ": assign actual Idle and Run Humanoid clips first.");
                return null;
            }
            var path = GenFolder + "/ZSR_Production_" + prefix + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            Add(machine, "Idle", idle);
            var runState = Add(machine, "Run", run);
            if (hit != null) Add(machine, "Hit", hit);
            if (death != null) Add(machine, "Death", death);
            machine.defaultState = runState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState Add(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            return state;
        }

        [MenuItem("Zombie Survival Run 0.8/9 • Validate Production Prefabs")]
        public static void Validate()
        {
            var config = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(SettingsPath);
            if (config == null) { Debug.LogWarning("[ZSR Production] Configure settings using item 7 first."); return; }
            ValidateOne("MAX", config.maxPrefab);
            ValidateOne("EVA", config.evaPrefab);
            ValidateOne("WALKER", config.walkerPrefab);
            ValidateOne("RUNNER", config.runnerPrefab);
            ValidateOne("ARMORED", config.armoredPrefab);
            ValidateOne("BRUTE", config.brutePrefab);
            ValidateOne("TOXIC", config.toxicPrefab);
            ValidateOne("CRAWLER", config.crawlerPrefab);
            ValidateOne("BOSS", config.bossPrefab);
        }

        static void ValidateOne(string slot, GameObject prefab)
        {
            if (prefab == null) { Debug.LogWarning("[ZSR Production] " + slot + ": no production prefab, legacy look will be used."); return; }
            var animator = prefab.GetComponentInChildren<Animator>(true);
            var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || mesh == null)
                Debug.LogError("[ZSR Production] " + slot + ": prefab must contain Animator and SkinnedMeshRenderer (real rigged model).");
            else if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                Debug.LogWarning("[ZSR Production] " + slot + ": Avatar is not a valid Humanoid. Set FBX Rig = Humanoid and Apply.");
            else Debug.Log("[ZSR Production] " + slot + ": Humanoid Avatar, Animator and SkinnedMeshRenderer found.");
        }
    }
}
#endif
