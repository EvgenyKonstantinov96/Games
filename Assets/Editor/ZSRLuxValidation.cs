#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRLuxValidation
    {
        [MenuItem("Zombie Survival Run 0.8/16 - Validate LUX Setup")]
        public static void Validate()
        {
            var s=AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>("Assets/Resources/ProductionArtSettings.asset");
            if (s==null) { Debug.LogError("[ZSR LUX] ProductionArtSettings not found."); return; }
            Test("MAX",s.maxPrefab,s.maxMotionController,s.maxAvatar,s.heroController);
            Test("EVA",s.evaPrefab,s.evaMotionController,s.evaAvatar,s.heroController);
            Test("WALKER",s.walkerPrefab,s.walkerMotionController,s.walkerAvatar,s.zombieController);
            Test("TOXIC",s.toxicPrefab,s.toxicMotionController,s.toxicAvatar,s.zombieController);
            Debug.Log("[ZSR LUX] Diagnostics finished. IMPORTANT: files and setup do not confirm actual Unity Game View rendering or Android FPS.");
        }
        static void Test(string slot,GameObject prefab,RuntimeAnimatorController generic,Avatar avatar,RuntimeAnimatorController humanoid)
        {
            if(prefab==null){Debug.LogError("[ZSR LUX] "+slot+" missing imported model.");return;}
            var renderers=prefab.GetComponentsInChildren<Renderer>(true);
            var skin=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(renderers.Length==0){Debug.LogError("[ZSR LUX] "+slot+": no renderers!");return;}
            string mode=humanoid!=null && avatar!=null && avatar.isValid && avatar.isHuman
                ? "Humanoid motion enabled"
                : generic!=null ? "Per-model Generic keyed motion (NOT mocap)" : "NO CLIP — run menu 15";
            Debug.Log("[ZSR LUX] "+slot+": "+renderers.Length+" renderer(s), "+skin.Length+
                " skinned mesh(es), avatar="+(avatar!=null&&avatar.isValid?"valid":"missing/invalid")+
                ", mode="+mode+", prefab="+prefab.name);
            if(skin.Length==0) Debug.LogWarning("[ZSR LUX] "+slot+": no SkinnedMeshRenderer; moving bone transforms may not deform the body.");
        }
    }
}
#endif
