using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace ZombieSurvivalRun
{
    // Shared materials and organic primitives. The procedural characters are replaceable by FBX prefabs.
    public static class ZSRVisuals
    {
        public static Material Asphalt, Concrete, ConcreteDark, Sidewalk, White, Yellow, Gold,
            Gunmetal, Steel, Armor, Olive, OliveLight, RedJacket, Skin, EvaSkin, Hair,
            ZombieSkin, ZombieDark, ZombieCloth, MutantSkin, Blood, OrangeGlow, BlueGlow,
            GoldGlow, RedGlow, Window, WindowDark, Fire, Tire, Black, Smoke, Glass,
            SkylineAsh, SkylineRust, SkylineBlue, HazeAsh, HazeBurn, HazeBlue;
        public static Material ClothTextured, RedClothTextured, ZombieSkinTextured, MetalWorn, AsphaltDeluxe, ConcreteDeluxe,
            ToxicSkin, ToxicGlow, MutantBossMaterial, SurvivorSkinMaterial, WreckRustMaterial;
        public static Material[] SkylineMaterials = new Material[3];
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        static bool ready;
        static Shader shader;
        public static void Init()
        {
            if (ready) return;
            ready = true;
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Asphalt = Mat(new Color(.12f,.145f,.17f), .09f);
            Concrete = Mat(new Color(.285f,.30f,.32f));
            ConcreteDark = Mat(new Color(.18f,.21f,.24f));
            Sidewalk = Mat(new Color(.24f,.28f,.30f));
            White = Mat(new Color(.80f,.82f,.79f));
            Yellow = Mat(new Color(.95f,.51f,.085f),.25f);
            Gold = Mat(new Color(.95f,.68f,.20f),.67f);
            Gunmetal = Mat(new Color(.065f,.075f,.085f),.68f);
            Steel = Mat(new Color(.33f,.39f,.43f),.70f);
            Armor = Mat(new Color(.125f,.17f,.19f),.32f);
            Olive = Mat(new Color(.24f,.26f,.19f),.24f);
            OliveLight = Mat(new Color(.33f,.34f,.23f),.25f);
            RedJacket = Mat(new Color(.43f,.09f,.085f),.22f);
            Skin = Mat(new Color(.68f,.43f,.32f),.23f);
            EvaSkin = Mat(new Color(.75f,.49f,.39f),.28f);
            Hair = Mat(new Color(.09f,.065f,.055f),.18f);
            ZombieSkin = Mat(new Color(.43f,.53f,.44f),.12f);
            ZombieDark = Mat(new Color(.24f,.31f,.29f),.08f);
            ZombieCloth = Mat(new Color(.39f,.36f,.34f),.11f);
            MutantSkin = Mat(new Color(.50f,.23f,.25f),.12f);
            Blood = Mat(new Color(.46f,.045f,.045f),.21f);
            OrangeGlow = Mat(new Color(1.0f,.40f,.05f),.26f,3.1f);
            BlueGlow = Mat(new Color(.065f,.61f,1f),.25f,3f);
            GoldGlow = Mat(new Color(1f,.70f,.13f),.35f,2.7f);
            RedGlow = Mat(new Color(.88f,.08f,.055f),.24f,4.1f);
            Window = Mat(new Color(.24f,.48f,.60f),.57f,.42f);
            WindowDark = Mat(new Color(.055f,.09f,.13f),.46f);
            Fire = Mat(new Color(1f,.24f,.018f),.09f,3.5f);
            Tire = Mat(new Color(.04f,.052f,.055f),.12f);
            Black = Mat(new Color(.025f,.027f,.03f),.27f);
            Smoke = Mat(new Color(.20f,.22f,.25f),.05f);
            Glass = Mat(new Color(.13f,.30f,.39f),.69f);
            SkylineAsh = Mat(new Color(.18f,.21f,.28f),.04f);
            SkylineRust = Mat(new Color(.30f,.17f,.17f),.04f);
            SkylineBlue = Mat(new Color(.10f,.16f,.26f),.04f);
            HazeAsh = Mat(new Color(.24f,.31f,.39f),.02f);
            HazeBurn = Mat(new Color(.42f,.18f,.14f),.02f);
            HazeBlue = Mat(new Color(.10f,.28f,.38f),.02f);
            // Surface detail from small, original, procedural textures.
            Texture2D roadTexture = Resources.Load<Texture2D>("Textures/asphalt");
            Texture2D wallTexture = Resources.Load<Texture2D>("Textures/concrete");
            if (roadTexture != null)
            {
                Asphalt.mainTexture = roadTexture;
                Asphalt.mainTextureScale = new Vector2(2f, 10f);
            }
            if (wallTexture != null)
            {
                Concrete.mainTexture = wallTexture;
                ConcreteDark.mainTexture = wallTexture;
                Sidewalk.mainTexture = wallTexture;
                Concrete.mainTextureScale = new Vector2(2f, 5f);
                ConcreteDark.mainTextureScale = new Vector2(2f, 5f);
                Sidewalk.mainTextureScale = new Vector2(2f, 6f);
            }
            AsphaltDeluxe = MatWithTexture(Color.white,"ProductionArt/Highway",.41f,new Vector2(2f,8f));
            ConcreteDeluxe = MatWithTexture(Color.white,"Textures/worn_concrete_v7",.13f,new Vector2(2.2f,5f));
            ClothTextured = MatWithTexture(Color.white,"Textures/fabric_survivor",.20f);
            RedClothTextured = MatWithTexture(Color.white,"Textures/jacket_eva_pbr",.24f);
            ZombieSkinTextured = MatWithTexture(Color.white,"Textures/zombie_infected",.16f);
            MetalWorn = MatWithTexture(Color.white,"Textures/gate_titanium_v7",.45f);
            SurvivorSkinMaterial=MatWithTexture(Color.white,"Textures/skin_hero",.29f);
            MutantBossMaterial=MatWithTexture(Color.white,"Textures/mutant_boss_pbr",.14f);
            WreckRustMaterial=MatWithTexture(Color.white,"Textures/rusty_car_v7",.30f);
            Skin=SurvivorSkinMaterial;
            EvaSkin=SurvivorSkinMaterial;
            ZombieSkin=ZombieSkinTextured;
            MutantSkin=MutantBossMaterial;
            ToxicSkin=Mat(new Color(.38f,.58f,.31f),.12f);
            ToxicGlow=Mat(new Color(.48f,1.00f,.12f),.16f,3f);
            for(int i=0;i<3;i++)
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if(unlit==null)unlit=Shader.Find("Unlit/Texture");
                SkylineMaterials[i]=new Material(unlit==null?shader:unlit);
                SkylineMaterials[i].name="Cinematic city panorama chapter "+(i+1);
                var sky = Resources.Load<Texture2D>(i == 0 ? "ProductionArt/Horizon" : "Skyline/city_chapter_" + (i+1) + "_v100");
                if(sky!=null)
                {
                    SkylineMaterials[i].mainTexture=sky;
                    if(SkylineMaterials[i].HasProperty("_BaseMap"))SkylineMaterials[i].SetTexture("_BaseMap",sky);
                }
                if(SkylineMaterials[i].HasProperty("_BaseColor"))SkylineMaterials[i].SetColor("_BaseColor",Color.white);
                if(SkylineMaterials[i].HasProperty("_Cull"))SkylineMaterials[i].SetFloat("_Cull",0f);
            }
        }
        public static Material Mat(Color c, float smooth=.2f, float emission=0f)
        {
            Material m = new Material(shader);
            m.name = "ZSR Procedural Material";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",c);
            else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",smooth);
            if (emission>0)
            {
                m.EnableKeyword("_EMISSION");
                if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",c*emission);
            }
            m.enableInstancing=true;
            return m;
        }

        public static Material MatWithTexture(Color tint,string resource,float smooth=.2f,Vector2? tiling=null)
        {
            var m=Mat(tint,smooth);
            var tex=Resources.Load<Texture2D>(resource);
            if(tex!=null)
            {
                m.mainTexture=tex;
                if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",tex);
                if(tiling.HasValue)
                {
                    m.mainTextureScale=tiling.Value;
                    if(m.HasProperty("_BaseMap"))m.SetTextureScale("_BaseMap",tiling.Value);
                }
                string normalPath=resource+"_normal";
                Texture2D normal=Resources.Load<Texture2D>(normalPath);
                if(normal!=null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap",normal);
                    m.EnableKeyword("_NORMALMAP");
                    if(m.HasProperty("_BumpScale"))m.SetFloat("_BumpScale",.35f);
                }
            }
            return m;
        }

        // These are genuine triangulated OBJ meshes included with the game archive;
        // each one is imported by Unity as a model asset and rendered using an explicit URP material.
        // If model import is unavailable on first open, safe fallback geometry remains visible.
        public static GameObject MeshPart(string meshName,string displayName,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3? rotation=null)
        {
            Mesh mesh;
            if(!MeshCache.TryGetValue(meshName,out mesh))
            {
                var prefab=Resources.Load<GameObject>("Meshes/"+meshName);
                var filter=prefab==null?null:prefab.GetComponentInChildren<MeshFilter>(true);
                mesh=filter==null?null:filter.sharedMesh;
                MeshCache[meshName]=mesh;
                if(mesh==null)Debug.LogWarning("[ZSR VISUALS] Model missing: Assets/Resources/Meshes/"+meshName+".obj — using geometric fallback.");
            }
            if(mesh==null)
                return MeshFallback(meshName, displayName + " (fallback)", parent, position, scale, material, rotation);
            var go=new GameObject(displayName);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localScale=scale;
            if(rotation.HasValue)go.transform.localEulerAngles=rotation.Value;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;
            renderer.receiveShadows=true;
            return go;
        }


        static GameObject MeshFallback(string meshName, string displayName, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3? rotation)
        {
            var root = new GameObject(displayName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = scale;
            if (rotation.HasValue) root.transform.localEulerAngles = rotation.Value;
            switch (meshName)
            {
                case "hair_short":
                case "hair_pony":
                    Sphere("hair silhouette", root.transform, new Vector3(0,.18f,-.04f),new Vector3(.52f,.28f,.50f),material);
                    break;
                case "energy_ring":
                    for(int i=0;i<24;i++)
                    {
                        float a=i*Mathf.PI*2f/24f;
                        Sphere("energy segment",root.transform,new Vector3(Mathf.Cos(a)*.98f,Mathf.Sin(a)*.98f,0),new Vector3(.095f,.095f,.11f),material);
                    }
                    break;
                case "survivor_face":
                case "infected_face":
                    Sphere("head mass", root.transform, new Vector3(0,.10f,.02f), new Vector3(.82f,.92f,.82f), material);
                    Sphere("jaw", root.transform, new Vector3(0,-.20f,.20f), new Vector3(.34f,.24f,.26f), material);
                    Sphere("nose ridge", root.transform, new Vector3(0,.02f,.35f), new Vector3(.14f,.20f,.12f), material);
                    break;
                case "upper_arm":
                case "forearm":
                case "infected_arm":
                case "thigh":
                case "calf":
                case "infected_leg":
                    Capsule("limb bulk", root.transform, Vector3.zero, new Vector3(.46f,1.0f,.44f), material);
                    Sphere("joint cap", root.transform, new Vector3(0,.42f,0), new Vector3(.28f,.20f,.28f), material);
                    break;
                case "tactical_boot":
                    Box("boot body", root.transform, new Vector3(0,-.05f,0), new Vector3(.62f,.46f,.92f), material);
                    Sphere("boot toe", root.transform, new Vector3(0,-.10f,.36f), new Vector3(.46f,.30f,.44f), material);
                    Box("sole", root.transform, new Vector3(0,-.28f,.06f), new Vector3(.58f,.10f,.86f), Black);
                    break;
                case "max_vest":
                case "eva_jacket":
                case "walker_ribs":
                case "brute_chest":
                case "boss_core":
                    Capsule("torso core", root.transform, new Vector3(0,.06f,0), new Vector3(.96f,1.08f,.62f), material);
                    Sphere("upper chest", root.transform, new Vector3(0,.36f,.10f), new Vector3(.88f,.52f,.54f), material);
                    Sphere("lower torso", root.transform, new Vector3(0,-.36f,-.04f), new Vector3(.70f,.38f,.42f), material);
                    break;
                case "medical_case":
                    Box("pack body", root.transform, Vector3.zero, new Vector3(.72f,.90f,.46f), material);
                    Capsule("pack roll", root.transform, new Vector3(0,.20f,.10f), new Vector3(.48f,.18f,.20f), material, new Vector3(90,0,0));
                    Box("strap", root.transform, new Vector3(0,.38f,.18f), new Vector3(.24f,.06f,.08f), Yellow);
                    break;
                case "portal_column":
                    Capsule("column", root.transform, new Vector3(0,.70f,0), new Vector3(.66f,1.45f,.66f), material);
                    Cylinder("ring", root.transform, new Vector3(0,1.95f,-.12f), new Vector3(.34f,.10f,.34f), OrangeGlow);
                    break;
                case "barrier_shell":
                    Box("barrier mass", root.transform, new Vector3(0,.10f,0), new Vector3(1.55f,.95f,1.05f), material);
                    Capsule("barrier top", root.transform, new Vector3(0,.56f,0), new Vector3(1.34f,.18f,.36f), material, new Vector3(0,0,90));
                    break;
                case "wreck_chassis":
                    Capsule("car shell", root.transform, new Vector3(0,.85f,0), new Vector3(2.35f,.44f,1.10f), material, new Vector3(0,0,90));
                    Capsule("hood", root.transform, new Vector3(0,.54f,.74f), new Vector3(1.60f,.22f,.76f), material, new Vector3(18,0,90));
                    break;
                case "mutant_spike":
                case "claw":
                    Capsule("spike", root.transform, new Vector3(0,.18f,0), new Vector3(.16f,.52f,.16f), material);
                    break;
                default:
                    Sphere("generic rounded fallback", root.transform, Vector3.zero, Vector3.one, material);
                    break;
            }
            return root;
        }

        public static GameObject CityPanorama(Transform parent,int chapter)
        {
            var go=Shape("Distant city art - BACKGROUND ONLY",PrimitiveType.Quad,parent,
                new Vector3(0f,35f,145f),new Vector3(250f,164f,1f),SkylineMaterials[Mathf.Clamp(chapter,0,2)],new Vector3(0,180,0));
            return go;
        }
        public static Transform Pivot(string name,Transform parent,Vector3 pos)
        {
            var t=new GameObject(name).transform;
            t.SetParent(parent,false);
            t.localPosition=pos;
            return t;
        }
        public static GameObject Shape(string name, PrimitiveType shape, Transform parent,
            Vector3 p,Vector3 size,Material mat,Vector3? rotation=null)
        {
            GameObject o=GameObject.CreatePrimitive(shape);
            o.name=name;
            o.transform.SetParent(parent,false);
            o.transform.localPosition=p;
            o.transform.localScale=size;
            if(rotation.HasValue)o.transform.localEulerAngles=rotation.Value;
            var renderer=o.GetComponent<Renderer>();
            renderer.sharedMaterial=mat;
            // Cast shadows from buildings and large obstacles, not every small window/stripe.
            renderer.shadowCastingMode=(name=="building mass" || name.Contains("car") || name.Contains("blockade") || name.Contains("barricade"))
                ? ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=true;
            var collider=o.GetComponent<Collider>();
            if(collider!=null) Object.Destroy(collider);
            return o;
        }
        public static GameObject Box(string n,Transform p,Vector3 a,Vector3 b,Material m,Vector3? r=null)
          =>Shape(n,PrimitiveType.Cube,p,a,b,m,r);
        public static GameObject Sphere(string n,Transform p,Vector3 a,Vector3 b,Material m)
          =>Shape(n,PrimitiveType.Sphere,p,a,b,m);
        public static GameObject Capsule(string n,Transform p,Vector3 a,Vector3 b,Material m,Vector3? r=null)
          =>Shape(n,PrimitiveType.Capsule,p,a,b,m,r);
        public static GameObject Cylinder(string n,Transform p,Vector3 a,Vector3 b,Material m,Vector3? r=null)
          =>Shape(n,PrimitiveType.Cylinder,p,a,b,m,r);
        public static GameObject Beam(string n,Transform p,Vector3 a,Vector3 b,float thick,Material m)
        {
            GameObject o=Cylinder(n,p,(a+b)*.5f,new Vector3(thick*.5f,Vector3.Distance(a,b)*.5f,thick*.5f),m);
            o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            return o;
        }
        public static TextMesh WorldText(string s, Transform parent,Vector3 pos,float size,Color c)
        {
            var o=new GameObject("3D marker: "+s);
            o.transform.SetParent(parent,false);
            o.transform.localPosition=pos;
            var t=o.AddComponent<TextMesh>();
            t.text=s;t.characterSize=size/64f;t.fontSize=64;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.color=c;
            return t;
        }
        /// <summary>
        /// Four independent, authored OBJ weapon silhouettes shipped in Resources/Meshes.
        /// No generic assault rifle reused for pistol, shotgun or minigun.
        /// </summary>
        public static Transform Rifle(Transform parent, WeaponType kind)
        {
            string key = kind == WeaponType.Pistol ? "pistol" :
                kind == WeaponType.Shotgun ? "shotgun" :
                kind == WeaponType.Minigun ? "minigun" : "rifle";
            var root = Pivot("ZSR 3D " + kind + " / weapon", parent, Vector3.zero);
            MeshPart("lux_weapon_" + key, "detailed weapon housing", root,
                Vector3.zero, Vector3.one, Gunmetal);
            MeshPart("lux_weapon_" + key + "_steel", "detailed steel barrel", root,
                Vector3.zero, Vector3.one, Steel);
            return root;
        }
        static Material wreckParticles;

        public static void WreckFire(Transform parent, Vector3 origin)
        {
            if (wreckParticles == null)
            {
                var fxShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (fxShader == null) return;
                wreckParticles = new Material(fxShader) { name = "ZSR soft wreck particles / shared" };
                wreckParticles.SetTexture("_BaseMap", Resources.Load<Texture2D>("ProductionArt/ParticleSoft"));
                wreckParticles.SetFloat("_Surface", 1f);
                wreckParticles.SetFloat("_ZWrite", 0f);
                wreckParticles.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                wreckParticles.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                wreckParticles.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                wreckParticles.SetOverrideTag("RenderType", "Transparent");
                wreckParticles.renderQueue = (int)RenderQueue.Transparent;
            }

            WreckEmitter(parent, origin, false);
            WreckEmitter(parent, origin + Vector3.up * .27f, true);
        }

        static void WreckEmitter(Transform parent, Vector3 origin, bool smoke)
        {
            Transform emitter = Pivot(smoke ? "Wreck smoke" : "Wreck flames", parent, origin);
            emitter.localRotation = Quaternion.Euler(-76f, 0f, 0f);
            var particles = emitter.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.startLifetime = smoke ? new ParticleSystem.MinMaxCurve(.7f, 1.55f) : new ParticleSystem.MinMaxCurve(.22f, .55f);
            main.startSpeed = smoke ? new ParticleSystem.MinMaxCurve(.65f, 1.17f) : new ParticleSystem.MinMaxCurve(.75f, 1.55f);
            main.startSize = smoke ? new ParticleSystem.MinMaxCurve(.38f, .61f) : new ParticleSystem.MinMaxCurve(.21f, .48f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.maxParticles = smoke ? 14 : 20;
            var emission = particles.emission;
            emission.rateOverTime = smoke ? 8 : 26;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = smoke ? 18f : 29f;
            shape.radius = smoke ? .24f : .37f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(smoke ? new Color(.38f, .27f, .24f) : new Color(1f, .86f, .40f), 0),
                new GradientColorKey(smoke ? new Color(.20f, .25f, .33f) : new Color(1f, .20f, .035f), 1)
            }, new[]
            {
                new GradientAlphaKey(smoke ? .18f : .84f, 0),
                new GradientAlphaKey(smoke ? .51f : .91f, .24f),
                new GradientAlphaKey(0, 1)
            });
            color.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, smoke ? .5f : .9f, 1f, smoke ? 2.4f : .4f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = wreckParticles;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        public static GameObject Explosion(Vector3 position,float size,Material color,float lifetime=.30f)
        {
            var fx=new GameObject("Impact / animated VFX");fx.transform.position=position;
            for(int i=0;i<9;i++)
            {
                Vector3 offset=Random.onUnitSphere*.13f*size;
                var s=Sphere("spark",fx.transform,offset,Vector3.one*Random.Range(.055f,.15f)*size,color);
                var life=s.AddComponent<ZSRLifetime>();
                life.Duration=lifetime*Random.Range(.5f,1f);
                life.Velocity=Random.onUnitSphere*Random.Range(1.5f,5f)*size;
            }
            Object.Destroy(fx,lifetime+.2f);
            return fx;
        }
    }
}
