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
                if ((name == "Max" || name == "Eva") &&
                    (!bones.Contains("WeaponGrip") || !bones.Contains("WeaponForward")))
                    errors.Add(name + ": weapon anchors missing");

                bool importedTake = false;
                foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (subAsset is AnimationClip clip && !clip.legacy &&
                        !clip.name.StartsWith("__preview__", StringComparison.Ordinal) && clip.length >= 5.5f)
                        importedTake = true;
                }
                if (!importedTake) errors.Add(name + ": baked motion timeline missing");
            }

            foreach (string name in new[] { "CharacterAtlas", "Highway", "Highway_normal", "Horizon", "ParticleSoft" })
                if (Resources.Load<Texture2D>("ProductionArt/" + name) == null)
                    errors.Add(name + ": Texture2D missing");

            if (errors.Count > 0)
                throw new InvalidOperationException("[ZSR ART] " + string.Join("\n[ZSR ART] ", errors));
            Debug.Log("[ZSR ART] All nine FBX skins, motion timelines, sockets and five textures imported.");
        }
    }
}
