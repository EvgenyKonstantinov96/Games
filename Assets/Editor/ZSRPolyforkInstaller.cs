#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEditor;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRPolyforkInstaller
    {
        // Identifiers are directly from public Polyfork model pages.
        // All URLs point to the site's publicly documented FREE CDN, not HD editions.
        struct AssetDef
        {
            public string key, slug;
            public AssetDef(string key, string slug) { this.key=key; this.slug=slug; }
        }
        static readonly AssetDef[] Assets =
        {
            new AssetDef("max", "infantry-rifleman-c1c0a5"),
            new AssetDef("eva", "field-medic-fd8919"),
            new AssetDef("walker", "teen-zombie-b5bb27"),
            new AssetDef("toxic", "undead-seaman-base"),
            new AssetDef("tower", "prewar-apartment-house-1081a6"),
            new AssetDef("house", "brownstone-row-house-1b84fd"),
            new AssetDef("policebarrier", "police-barricade-0bbf37"),
            new AssetDef("concretebarrier", "skated-jersey-barrier-7f0a04"),
            new AssetDef("car", "town-car-56eeb6"),
            new AssetDef("fireengine", "fire-engine-7d476b")
        };
        const string Folder = "Assets/Polyfork/Models";
        const string ConfigPath = "Assets/Resources/ProductionArtSettings.asset";

        [MenuItem("Zombie Survival Run 0.8/10 • Download Polyfork Models (verify licence)")]
        public static void Download()
        {
            Directory.CreateDirectory(Folder);
            int success=0, errors=0;
            try
            {
                using(var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "ZombieSurvivalRun-Unity-Editor/0.8.3");
                    for(int i=0; i<Assets.Length; i++)
                    {
                        var asset=Assets[i];
                        var dest=Folder+"/"+asset.slug+".glb";
                        var url="https://polyfork.dev/cdn/"+asset.slug+".glb";
                        EditorUtility.DisplayProgressBar("Zombie Survival Run / Polyfork", "Downloading " + asset.key + " (" +(i+1)+"/"+Assets.Length+")",(float)i/Assets.Length);
                        try
                        {
                            if(!File.Exists(dest) || !ValidGlb(dest))
                            {
                                string temp=Path.GetTempFileName();
                                try
                                {
                                    client.DownloadFile(url, temp);
                                    if(!ValidGlb(temp)) throw new IOException("Not a valid GLB response: " + asset.slug);
                                    File.Copy(temp,dest,true);
                                }
                                finally { if(File.Exists(temp)) File.Delete(temp); }
                            }
                            success++;
                            Debug.Log("[ZSR Polyfork] Downloaded " + asset.slug);
                        }
                        catch(Exception e)
                        {
                            errors++;
                            Debug.LogError("[ZSR Polyfork] Failed to download " + url + ": " + e.Message);
                        }
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BindImportedModels();
            EditorUtility.DisplayDialog("ZSR Polyfork", success + " downloaded/available; " + errors + " failed.\nCheck Console. Models without clips need real animation assets for premium movement.", "OK");
        }
        static bool ValidGlb(string path)
        {
            if(!File.Exists(path) || new FileInfo(path).Length < 32) return false;
            using(var file=File.OpenRead(path))
            {
                var magic=new byte[4];
                return file.Read(magic,0,4)==4 && magic[0]==0x67 && magic[1]==0x6C && magic[2]==0x54 && magic[3]==0x46;
            }
        }
        [MenuItem("Zombie Survival Run 0.8/11 • Link Imported Polyfork Models")]
        public static void BindImportedModels()
        {
            Directory.CreateDirectory("Assets/Resources");
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(ConfigPath);
            if(settings == null)
            {
                settings = ScriptableObject.CreateInstance<ZSRProductionArtSettings>();
                AssetDatabase.CreateAsset(settings, ConfigPath);
            }
            GameObject[] imported = new GameObject[Assets.Length];
            int ready=0;
            for(int i=0;i<Assets.Length;i++)
            {
                string path=Folder+"/"+Assets[i].slug+".glb";
                imported[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(imported[i] != null && imported[i].GetComponentsInChildren<Renderer>(true).Length>0) ready++;
                else { imported[i]=null; Debug.LogWarning("[ZSR Polyfork] Not imported: " + path + ". Install com.unity.cloud.gltfast in Package Manager if needed."); }
            }
            if(imported[0] != null) settings.maxPrefab = imported[0];
            if(imported[1] != null) settings.evaPrefab = imported[1];
            if(imported[2] != null) {settings.walkerPrefab=imported[2];settings.runnerPrefab=imported[2];settings.crawlerPrefab=imported[2];}
            if(imported[3] != null) {settings.toxicPrefab=imported[3];settings.armoredPrefab=imported[3];settings.brutePrefab=imported[3];}
            if(imported[4] != null) settings.towerBuildingPrefab = imported[4];
            if(imported[5] != null) settings.houseBuildingPrefab = imported[5];
            if(imported[6] != null) settings.policeBarrierPrefab = imported[6];
            if(imported[7] != null) settings.concreteBarrierPrefab = imported[7];
            if(imported[8] != null) settings.townCarPrefab = imported[8];
            if(imported[9] != null) settings.fireEnginePrefab = imported[9];
            settings.heroScale = 1.12f;
            settings.zombieScale = 1.1f;
            settings.spawnLegacyGun = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Selection.activeObject=settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log("[ZSR Polyfork] " + ready + "/" + Assets.Length + " models bound. Asset list: https://polyfork.dev/. Open a NEW game after importing to see replacements.");
        }
        [MenuItem("Zombie Survival Run 0.8/12 • Validate Polyfork Model Bindings")]
        public static void Validate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(ConfigPath);
            if(settings == null) { Debug.LogError("[ZSR Polyfork] No settings. Use menu item 10 first."); return; }
            ZSRProductionArtSetup.Validate();
            if(settings.towerBuildingPrefab != null && settings.townCarPrefab != null)
                Debug.Log("[ZSR Polyfork] Imported city + car prefabs are assigned. Visual check required in Game view.");
            else Debug.LogWarning("[ZSR Polyfork] Building/car missing; road geometry will use previous fallback.");
        }
    }
}
#endif
