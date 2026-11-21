#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Validate real OBJ renderers attached to animated bones (v0.8.1).</summary>
    public static class ZSRProjectAudit
    {
        [MenuItem("Zombie Survival Run 0.8/6 - Validate 3D Assets and Animations")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ZSR AUDIT] Stop Play Mode before running an asset audit.");
                return;
            }
            ZSRRigClipGenerator.Ensure();
            ZSRVisuals.Init();
            int checks = 0, failures = 0;
            foreach (var name in new[] { "survivor_face", "infected_face", "max_vest", "eva_jacket",
                "walker_ribs", "brute_chest", "boss_core", "thigh", "calf", "upper_arm",
                "forearm", "infected_leg", "tactical_boot", "hair_short", "hair_pony",
                "energy_ring", "wreck_chassis", "barrier_shell" })
            {
                checks++;
                var go = Resources.Load<GameObject>("Meshes/" + name);
                if (go == null || go.GetComponentInChildren<MeshFilter>(true) == null)
                {
                    failures++;
                    Debug.LogError("[ZSR AUDIT] Missing imported OBJ model: " + name);
                }
            }
            checks++;
            if (Resources.Load<RuntimeAnimatorController>("Animations/ZSRHero") == null)
            {
                failures++; Debug.LogError("[ZSR AUDIT] Hero Animator Controller missing. Use 0 - Regenerate Skeleton Animations.");
            }
            checks++;
            if (Resources.Load<RuntimeAnimatorController>("Animations/ZSRZombie") == null)
            {
                failures++; Debug.LogError("[ZSR AUDIT] Zombie Animator Controller missing. Use 0 - Regenerate Skeleton Animations.");
            }
            GameObject parent = new GameObject("TEMP 3D ART AUDIT - removed on completion");
            try
            {
                var max = ZSRSkinnedArt.CreateHero(parent.transform, 0, WeaponType.Rifle);
                var eva = ZSRSkinnedArt.CreateHero(parent.transform, 1, WeaponType.Pistol);
                var walker = ZSRSkinnedArt.CreateZombie(parent.transform, ZombieType.Walker);
                var boss = ZSRSkinnedArt.CreateZombie(parent.transform, ZombieType.Boss);
                foreach (var rig in new[] { max, eva, walker, boss })
                {
                    checks++;
                    var renderers = rig != null ? rig.GetComponentsInChildren<MeshRenderer>(true) : null;
                    var bones = rig != null ? rig.GetComponentsInChildren<Transform>(true) : null;
                    bool anyMesh = false;
                    if (renderers != null)
                        foreach (var renderer in renderers)
                        {
                            var filter = renderer.GetComponent<MeshFilter>();
                            if (renderer.enabled && filter != null && filter.sharedMesh != null &&
                                filter.sharedMesh.vertexCount > 0) { anyMesh = true; break; }
                        }
                    if (rig == null || !rig.IsWorking || !anyMesh || bones == null || bones.Length < 15)
                    {
                        failures++;
                        Debug.LogError("[ZSR AUDIT] Character has no visible OBJ meshes on animated bones.");
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                Debug.LogError("[ZSR AUDIT] Building actor failed: " + ex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
            if (failures == 0) Debug.Log("[ZSR AUDIT] OK. " + checks + " import/rig/controller checks passed. Now run Play Mode to validate actual animation, visuals and gameplay.");
            else Debug.LogError("[ZSR AUDIT] FAILED. " + failures + " of " + checks + " checks failed. Read preceding Console entries.");
        }
    }
}
#endif
