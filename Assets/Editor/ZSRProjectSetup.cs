#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Idempotent first-run project/scene setup for Unity 6.3.28f1.</summary>
    [InitializeOnLoad]
    public static class ZSRProjectSetup
    {
        const string ScenePath="Assets/Scenes/ZSR_Main.unity";
        const string PreviewScenePath="Assets/Scenes/ZSR_AssetGallery.unity";
        const string RendererPath="Assets/Settings/ZSR_Renderer.asset";
        const string PipelinePath="Assets/Settings/ZSR_URP.asset";
        static bool queued;

        static ZSRProjectSetup(){QueueSetup();}
        static void QueueSetup()
        {
            if(queued)return;
            queued=true;
            EditorApplication.delayCall += AutoSetup;
        }
        static void AutoSetup()
        {
            queued=false;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
            {
                QueueSetup();return;
            }
            if(File.Exists(ScenePath)&&AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath)!=null)return;
            CreateScene();
        }
        [MenuItem("Zombie Survival Run 0.8/1 • Prepare Project & Scene")]
        public static void CreateScene()
        {
            try
            {
                Directory.CreateDirectory("Assets/Scenes");
                Directory.CreateDirectory("Assets/Settings");
                var rp=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if(rp==null)
                {
                    rp=ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rp,RendererPath);
                }
                var urp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if(urp==null)
                {
                    urp=UniversalRenderPipelineAsset.Create(rp);
                    urp.supportsHDR=true;
                    urp.msaaSampleCount=1;
                    AssetDatabase.CreateAsset(urp,PipelinePath);
                }
                GraphicsSettings.defaultRenderPipeline=urp;
                QualitySettings.renderPipeline=urp;
                if(!File.Exists(ScenePath))
                {
                    var previousScene = SceneManager.GetActiveScene();
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    try
                    {
                        if (!EditorSceneManager.SaveScene(scene, ScenePath))
                            throw new IOException("Could not save " + ScenePath);
                    }
                    finally
                    {
                        if (previousScene.IsValid() && previousScene.isLoaded)
                        {
                            SceneManager.SetActiveScene(previousScene);
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
                var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                int mainSceneIndex = buildScenes.FindIndex(scene => scene.path == ScenePath);
                var mainScene = new EditorBuildSettingsScene(ScenePath, true);
                if (mainSceneIndex < 0) buildScenes.Insert(0, mainScene);
                else buildScenes[mainSceneIndex] = mainScene;
                EditorBuildSettings.scenes = buildScenes.ToArray();
                PlayerSettings.companyName="Zombie Survival Studio";
                PlayerSettings.productName="Zombie Survival Run";
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.zombiesurvival.run3d");
                ZSRRigClipGenerator.Ensure();
                AssetDatabase.SaveAssets();
                Debug.Log("[ZSR 0.8] Ready. Open Zombie Survival Run 0.8 > 2 • Open Game and press Play. Enable new Input System if Unity prompts.");
            }
            catch(System.Exception ex)
            {
                Debug.LogError("[ZSR 0.8] Setup incomplete: "+ex.Message+". Rerun Zombie Survival Run 0.8 > 1 after packages have finished importing.");
            }
        }
        [MenuItem("Zombie Survival Run 0.8/2 • Open Game")]
        public static void OpenGame()
        {
            if(!File.Exists(ScenePath))CreateScene();
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);
        }
        // Editor-only asset gallery: view the actual Unity 3D figurines without entering a level.
        [MenuItem("Zombie Survival Run 0.8/4 • Preview 3D Models")]
        public static void PreviewModels()
        {
            if(EditorApplication.isPlaying)return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            ZSRVisuals.Init();
            var floor=new GameObject("Asset showroom floor").transform;
            ZSRVisuals.Box("black exhibition deck",floor,new Vector3(0,-.25f,0),
                new Vector3(26f,.4f,10f),ZSRVisuals.AsphaltDeluxe);
            var key=new GameObject("Gallery key light").AddComponent<Light>();
            key.type=LightType.Directional;
            key.intensity=1.7f;
            key.color=new Color(1f,.80f,.62f);
            key.transform.rotation=Quaternion.Euler(48,-27,0);
            var fill=new GameObject("Gallery cool fill").AddComponent<Light>();
            fill.type=LightType.Directional;
            fill.intensity=.7f;
            fill.color=new Color(.40f,.67f,1f);
            fill.transform.rotation=Quaternion.Euler(26,155,0);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.48f,.52f,.56f);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"HERO MAX / real OBJ mesh assets":"HERO EVA / real OBJ mesh assets");
                go.transform.position=new Vector3(-9+i*2.9f,0,0);
                go.AddComponent<ZSRHero>().Build(i,WeaponType.Rifle);
                ZSRVisuals.Cylinder("display halo",go.transform,new Vector3(0,.015f,0),
                    new Vector3(.85f,.03f,.85f),ZSRVisuals.GoldGlow);
            }
            ZombieType[] types={ZombieType.Walker,ZombieType.Runner,ZombieType.Armored,
                ZombieType.Brute,ZombieType.Toxic,ZombieType.Crawler,ZombieType.Boss};
            for(int i=0;i<types.Length;i++)
            {
                var go=new GameObject("INFECTED MODEL "+types[i]);
                go.transform.position=new Vector3(-2.4f+i*2.6f,0,0);
                go.transform.rotation=Quaternion.Euler(0,180,0);
                go.AddComponent<ZSRZombie>().Construct(types[i]);
            }
            var c=new GameObject("Showroom Camera");
            c.tag="MainCamera";
            var camera=c.AddComponent<Camera>();
            camera.fieldOfView=62f;
            camera.transform.position=new Vector3(2f,6f,-26f);
            camera.transform.LookAt(new Vector3(2f,2f,0));
            c.AddComponent<AudioListener>();
            BakeRiggedGalleryAssets();
            EditorSceneManager.SaveScene(scene,PreviewScenePath);
            UnityEngine.Debug.Log("[ZSR 0.8] Model gallery saved. Open Scene view and press F to focus a character. Return with 2 • Open Game.");
        }

        static void BakeRiggedGalleryAssets()
        {
            Directory.CreateDirectory("Assets/GeneratedRigMeshes");
            foreach (var rig in UnityEngine.Object.FindObjectsByType<ZSRSkinnedArt>(FindObjectsSortMode.None))
            {
                if (rig.RigMesh == null) continue;
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/GeneratedRigMeshes/" + rig.RigMesh.name + ".asset");
                var mesh = UnityEngine.Object.Instantiate(rig.RigMesh);
                AssetDatabase.CreateAsset(mesh, path);
                rig.SetGalleryMesh(mesh);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Zombie Survival Run 0.8/5 • Preview Obstacles & Gates")]
        public static void PreviewProps()
        {
            if(EditorApplication.isPlaying)return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            ZSRVisuals.Init();
            var root=new GameObject("ART REVIEW: actual 3D props, not UI artwork").transform;
            ZSRVisuals.Box("exhibition road",root,new Vector3(0,-.25f,0),new Vector3(26f,.4f,12f),ZSRVisuals.AsphaltDeluxe);
            var sun=new GameObject("key spotlight").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.8f;sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(42,-27,0);
            for(int i=0;i<3;i++)
            {
                var o=new GameObject("OBSTACLE " +(i+1)+" : car / barrier / spikes");
                o.transform.SetParent(root,false);
                o.AddComponent<ZSRObstacle>().Build(-7.2f+i*4.4f,0,0,i);
            }
            var a=new GameObject("BONUS GATE GOLD").AddComponent<ZSRBonusGate>();
            a.transform.SetParent(root,false);
            a.Build(BonusKind.DoubleDamage,7.4f,1.2f);
            var b=new GameObject("BONUS GATE BLUE").AddComponent<ZSRBonusGate>();
            b.transform.SetParent(root,false);
            b.Build(BonusKind.Shield,11.2f,1.2f);
            var c=new GameObject("Asset camera");c.tag="MainCamera";
            var camera=c.AddComponent<Camera>();camera.fieldOfView=56f;
            camera.transform.position=new Vector3(1.6f,5.5f,-18f);
            camera.transform.LookAt(new Vector3(1.5f,1.3f,0));
            c.AddComponent<AudioListener>();
            string path="Assets/Scenes/ZSR_PremiumPropsPreview.unity";
            EditorSceneManager.SaveScene(scene,path);
            Debug.Log("[ZSR 0.8] Open Scene view, focus an object with F. To return: Zombie Survival Run 0.8 > 2 • Open Game.");
        }

        [MenuItem("Zombie Survival Run 0.8/3 • Select Android")]
        public static void Android()
        {
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
        }
    }
}
#endif
