using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRProductionArtValidation
    {
        [MenuItem("Zombie Survival Run 1.0/Check Shipped Character Pack")]
        public static void Validate()
        {
            var errors = new List<string>();
            ZSRVisuals.Init();
            foreach (string name in new[]
            {
                "Max", "Eva", "Walker", "Runner", "Armored", "Brute", "Toxic", "Crawler", "Boss"
            })
            {
                string path = "Assets/Resources/ProductionArt/" + name + ".fbx";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    errors.Add(name + ": FBX failed to import");
                    continue;
                }

                var skin = asset.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skin == null || skin.sharedMesh == null || skin.bones.Length < 13)
                    errors.Add(name + ": skinned mesh or bones missing");

                var bones = new HashSet<string>();
                foreach (var child in asset.GetComponentsInChildren<Transform>(true))
                    bones.Add(child.name);
                if (!bones.Contains("Pelvis") || !bones.Contains("ForwardMarker"))
                    errors.Add(name + ": body anchors missing");
                bool hero = name == "Max" || name == "Eva";
                if (hero && (!bones.Contains("WeaponGrip") || !bones.Contains("WeaponForward") || !bones.Contains("HandL")))
                    errors.Add(name + ": weapon or item anchors missing");

                bool importedTake = false;
                foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (subAsset is AnimationClip clip && !clip.legacy &&
                        !clip.name.StartsWith("__preview__", StringComparison.Ordinal) && clip.length >= (hero ? 7.16f : 5.5f))
                        importedTake = true;
                }
                if (!importedTake) errors.Add(name + ": baked motion timeline missing");
                else CheckGameplayRig(name, errors);
            }

            foreach (string name in new[] { "CharacterAtlas", "CharacterAtlas_emission", "Highway", "Highway_normal", "Horizon",
                         "Horizon_Industrial", "Horizon_Evacuation", "Highway_surface", "ParticleSoft" })
                if (Resources.Load<Texture2D>("ProductionArt/" + name) == null)
                    errors.Add(name + ": Texture2D missing");

            foreach (string name in new[] { "combat_drone", "combat_rotor", "combat_grenade", "combat_medkit", "track_guardrail",
                         "track_chapter_ruins", "track_chapter_industrial", "track_chapter_evacuation" })
            {
                var model = Resources.Load<GameObject>("Meshes/" + name);
                if (model == null || model.GetComponentInChildren<MeshFilter>() == null)
                    errors.Add(name + ": game mesh missing");
            }
            if (Resources.Load<Font>("UI/ZSRMenuFont") == null)
                errors.Add("Cyrillic menu font missing");
            foreach (string name in new[] { "max", "eva" })
                if (Resources.Load<Texture2D>("UI/campaign_cover_" + name) == null)
                    errors.Add(name + ": campaign cover missing");
            foreach (string name in new[] { "Menu", "Run", "Pistol", "Rifle", "Shotgun", "Minigun", "Drone", "Blast" })
                if (Resources.Load<AudioClip>("Audio/" + name) == null)
                    errors.Add(name + ": audio clip missing");

            if (ZSRVisuals.AsphaltDeluxe.mainTexture == null ||
                ZSRVisuals.SkylineMaterials[0].mainTexture == null)
                errors.Add("Gameplay asphalt or skyline material has no texture");

            if (errors.Count > 0)
                throw new InvalidOperationException("[ZSR ART] " + string.Join("\n[ZSR ART] ", errors));
            Debug.Log("[ZSR ART] Nine character rigs, environment, combat meshes and audio loaded.");
        }

        static void CheckGameplayRig(string name, List<string> errors)
        {
            var host = new GameObject("Character frame calibration check");
            var baked = new Mesh();
            try
            {
                bool hero = name == "Max" || name == "Eva";
                var rig = ZSRBundledCharacter.Create(host.transform, name, hero, WeaponType.Rifle);
                if (rig == null)
                {
                    errors.Add(name + ": gameplay factory could not build the rig");
                    return;
                }

                var skin = rig.GetComponentInChildren<SkinnedMeshRenderer>();
                skin.BakeMesh(baked);
                Vector3 lo = Vector3.one * float.PositiveInfinity;
                Vector3 hi = Vector3.one * float.NegativeInfinity;
                foreach (var vertex in baked.vertices)
                {
                    var point = host.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                    lo = Vector3.Min(lo, point);
                    hi = Vector3.Max(hi, point);
                }
                Vector3 size = hi - lo;
                if (baked.vertexCount == 0 || size.y < 1.25f || size.y > 2.6f ||
                    size.x > 2f || size.z > 2f || lo.y < -.30f)
                    errors.Add(name + ": outside gameplay rig bounds " + lo + " .. " + hi);

                if (hero)
                {
                    var muzzle = rig.Muzzle;
                    var point = muzzle == null ? Vector3.zero : host.transform.InverseTransformPoint(muzzle.position);
                    if (point.y < .65f || point.y > 2.25f || point.z <= .2f || point.z > 2f)
                        errors.Add(name + ": muzzle is outside the playable rig " + point);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
