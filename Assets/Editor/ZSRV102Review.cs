#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>
    /// v1.0.2 visual asset preflight. Does not pretend to replace a Play Mode test.
    /// It specifically detects missing imported rigged prefabs after fresh installs.
    /// </summary>
    public static class ZSRV102Review
    {
        static readonly string[] Props = {
            "lux_wreck_body", "lux_wreck_wheels", "lux_wreck_hubs",
            "lux_wreck_windows", "lux_wreck_lights", "lux_concrete_block",
            "lux_spike_base", "lux_spikes", "lux_rubble", "lux_urban_tower_a",
            "lux_urban_tower_b", "lux_survival_backpack", "lux_shoulder_armor",
            "lux_weapon_pistol", "lux_weapon_rifle", "lux_weapon_shotgun",
            "lux_weapon_minigun"
        };

        [MenuItem("Zombie Survival Run 1.0/12 - Validate v1.0.2 Art Assets")]
        public static void ValidateArt()
        {
            int missing = 0;
            foreach (string model in Props)
            {
                string path = "Assets/Resources/Meshes/" + model + ".obj";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount < 8)
                {
                    missing++;
                    Debug.LogError("[ZSR 1.0.2 ART] Missing or empty mesh: " + path);
                }
            }
            string[] textures = { "UI/panel", "UI/section_wallpaper",
                "Textures/asphalt_v102", "Textures/asphalt_v102_normal" };
            foreach (var resource in textures)
                if (Resources.Load<Texture2D>(resource) == null)
                {
                    missing++;
                    Debug.LogError("[ZSR 1.0.2 ART] Missing texture: Resources/" + resource);
                }
            var settings = Resources.Load<ZSRProductionArtSettings>("ProductionArtSettings");
            bool max = settings != null && settings.maxPrefab != null;
            bool eva = settings != null && settings.evaPrefab != null;
            bool zombie = settings != null && settings.walkerPrefab != null;
            if (!max || !eva || !zombie)
                Debug.LogWarning("[ZSR 1.0.2 ART] Full rigged external character set NOT in this installation. " +
                    "Built-in OBJ animation will run, but will NOT match the concept art. " +
                    "Import CC0 FBX/GLB and bind ProductionArtSettings to enable genuine rigs.");
            Debug.Log("[ZSR 1.0.2 ART] Original mesh/texture preflight: " +
                (missing == 0 ? "all bundled art assets found" : missing + " missing") +
                "; external rigs: Max=" + max + ", Eva=" + eva + ", Walker=" + zombie +
                ". This is not visual or physics certification.");
            EditorUtility.DisplayDialog("ZSR v1.0.2 Art Diagnostics",
                missing == 0 ? "Bundled 3D assets found.\n\nExternal rigged characters: " +
                    (max && eva && zombie ? "configured" : "NOT all configured; see Console") +
                    "\n\nYou must also inspect the Game View and test on Android."
                    : missing + " bundled asset(s) missing. See Console.", "OK");
        }

        [MenuItem("Zombie Survival Run 1.0/13 - Save Current Game View Screenshot")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ZSR Game View", "Start Play Mode, start a run, then use this menu.", "OK");
                return;
            }
            const string path = "ZSR_Test_Evidence";
            Directory.CreateDirectory(path);
            string destination = path + "/GameView_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(destination);
            Debug.Log("[ZSR evidence] Screenshot requested at " + destination +
                ". Check file after Unity renders the next frame.");
        }
    }
}
#endif
