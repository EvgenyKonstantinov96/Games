#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Optional downloader of a separately published CC0 FBX library.
    /// No external model bytes are embedded or claimed to be included in this patch.</summary>
    public static class ZSRLuxMocapImporter
    {
        const string Source = "https://huggingface.co/datasets/Kinemiq/free-game-animations/resolve/main/KinemIQ_Free_Animations_CC0_FBX.zip?download=true";
        const string Folder = "Assets/ProductionArt/External/KinemIQ_CC0";
        const string ConfigPath = "Assets/Resources/ProductionArtSettings.asset";

        [MenuItem("Zombie Survival Run 0.8/14 - Download CC0 Humanoid Motion Pack")]
        public static void Download()
        {
            string temporary = Path.GetTempFileName();
            try
            {
                EditorUtility.DisplayProgressBar("Zombie Survival Run LUX", "Downloading external CC0 FBX animation library", .1f);
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent,"Unity-Editor-Zombie-Survival-Run/0.8.4");
                    client.DownloadFile(Source, temporary);
                }
                using (var archive = ZipFile.OpenRead(temporary))
                {
                    Directory.CreateDirectory(Folder);
                    int count=0;
                    foreach(var entry in archive.Entries)
                    {
                        if (!entry.FullName.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)) continue;
                        // Only the name of each .fbx is used; no path traversal or executable content.
                        string name=Path.GetFileName(entry.Name);
                        if (string.IsNullOrEmpty(name)) continue;
                        string target=Path.Combine(Folder,name);
                        using (var from=entry.Open()) using(var to=File.Create(target)) from.CopyTo(to);
                        count++;
                    }
                    if(count==0) throw new InvalidDataException("No FBX clips found. Archive layout has changed.");
                    Debug.Log("[ZSR LUX] Extracted " + count + " FBX files from the official external CC0 library.");
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureImportedFbx();
                AssignClips();
                EditorUtility.DisplayDialog("ZSR LUX motion", "FBX assets imported and suitable clips assigned when found.\nUse menu 13 to build an Avatar for the Polyfork models, then menu 8 to build Humanoid controllers.\nAny invalid Avatar will continue using model-specific Generic animation.","OK");
            }
            catch(Exception ex)
            {
                Debug.LogError("[ZSR LUX] Could not download/install CC0 animations. " + ex + "\nManual source: https://huggingface.co/datasets/Kinemiq/free-game-animations");
                EditorUtility.DisplayDialog("ZSR LUX", "Automatic CC0 download failed. Check Console and your connection.\nYou can download the FBX archive from the project page and import manually.","OK");
            }
            finally { EditorUtility.ClearProgressBar(); if(File.Exists(temporary)) File.Delete(temporary); }
        }

        static void ConfigureImportedFbx()
        {
            foreach (var path in Directory.GetFiles(Folder,"*.fbx",SearchOption.AllDirectories))
            {
                string assetPath=path.Replace('\\','/');
                var imp=AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (imp==null) continue;
                // Unity 6000.3 removed the writable importMaterials API.
                // Configure materials explicitly with materialImportMode instead.
                if (imp.animationType != ModelImporterAnimationType.Human ||
                    imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                    !imp.importAnimation ||
                    imp.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.importAnimation = true;
                    imp.materialImportMode = ModelImporterMaterialImportMode.None;
                    imp.SaveAndReimport();
                }
            }
        }
        static void AssignClips()
        {
            var config=AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(ConfigPath);
            if (config==null) {Debug.LogWarning("[ZSR LUX] ProductionArtSettings not found.");return;}
            var clips=new List<AnimationClip>();
            foreach(var path in Directory.GetFiles(Folder,"*.fbx",SearchOption.AllDirectories))
            {
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\','/')))
                {
                    var clip=asset as AnimationClip;
                    if(clip!=null && !clip.name.StartsWith("__preview__",StringComparison.OrdinalIgnoreCase)) clips.Add(clip);
                }
            }
            config.heroIdle=Pick(clips,"idle");
            config.heroRun=Pick(clips,"run","jog","sprint");
            config.heroHit=Pick(clips,"hit","impact","hurt");
            config.heroDeath=Pick(clips,"death","die","dying");
            config.zombieIdle=Pick(clips,"idle");
            config.zombieRun=Pick(clips,"walk","run","jog");
            config.zombieHit=Pick(clips,"hit","hurt");
            config.zombieDeath=Pick(clips,"death","die","dying");
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            int count=new[]{config.heroIdle,config.heroRun,config.heroHit,config.heroDeath,
                config.zombieIdle,config.zombieRun,config.zombieHit,config.zombieDeath}.Count(x=>x!=null);
            Debug.Log("[ZSR LUX] Matched " + count + "/8 animation slots. Inspector: ProductionArtSettings. " +
                "Ensure each chosen clip's FBX Rig type is Humanoid and that the GLB Character Avatar is valid.");
            Selection.activeObject=config;
            EditorGUIUtility.PingObject(config);
        }
        static AnimationClip Pick(List<AnimationClip> clips, params string[] keywords)
        {
            foreach(var word in keywords)
                foreach(var clip in clips)
                    if(clip.name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0) return clip;
            return null;
        }
    }
}
#endif
