using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>
    /// References to ACTUAL imported professional prefabs, not the procedural v0.8 OBJ figures.
    /// Place the asset at Assets/Resources/ProductionArtSettings.asset.
    /// An unassigned prefab keeps existing legacy graphics; it is never replaced by an invisible object.
    /// </summary>
    [CreateAssetMenu(fileName = "ProductionArtSettings", menuName = "Zombie Survival Run/Production Art Settings")]
    public sealed class ZSRProductionArtSettings : ScriptableObject
    {
        [Header("Actual rigged survivor prefabs")]
        public GameObject maxPrefab;
        public GameObject evaPrefab;
        [Header("Actual rigged infected prefabs")]
        public GameObject walkerPrefab;
        public GameObject runnerPrefab;
        public GameObject armoredPrefab;
        public GameObject brutePrefab;
        public GameObject toxicPrefab;
        public GameObject crawlerPrefab;
        public GameObject bossPrefab;
        [Header("Polyfork free city and obstacle models")]
        public GameObject towerBuildingPrefab;
        public GameObject houseBuildingPrefab;
        public GameObject policeBarrierPrefab;
        public GameObject concreteBarrierPrefab;
        public GameObject townCarPrefab;
        public GameObject fireEnginePrefab;
        [Header("Optional common Humanoid animation controllers")]
        public RuntimeAnimatorController heroController;
        public RuntimeAnimatorController zombieController;
        // Per imported GLB fallback: keyed transform animation built from its real bone hierarchy.
        // These are not mocap, but preserve foot/knee phases instead of sine-wave joint spins.
        [Header("LUX: automatically generated per-model animation controllers")]
        public RuntimeAnimatorController maxMotionController, evaMotionController;
        public RuntimeAnimatorController walkerMotionController, runnerMotionController;
        public RuntimeAnimatorController armoredMotionController, bruteMotionController;
        public RuntimeAnimatorController toxicMotionController, crawlerMotionController;
        [Header("LUX: optional imported Humanoid Avatars")]
        public Avatar maxAvatar, evaAvatar, walkerAvatar, runnerAvatar;
        public Avatar armoredAvatar, bruteAvatar, toxicAvatar, crawlerAvatar;
        [Header("Optional Humanoid clips for generating controllers")]
        public AnimationClip heroIdle;
        public AnimationClip heroRun;
        public AnimationClip heroHit;
        public AnimationClip heroDeath;
        public AnimationClip zombieIdle;
        public AnimationClip zombieRun;
        public AnimationClip zombieHit;
        public AnimationClip zombieDeath;
        [Header("Offsets / scale applied after importing the prefab")]
        public Vector3 heroOffset = Vector3.zero;
        public Vector3 zombieOffset = Vector3.zero;
        public float heroScale = 1f;
        public float zombieScale = 1f;
        [Tooltip("Only enable when the imported survivor does not contain its own weapon.")]
        public bool spawnLegacyGun = false;

        public RuntimeAnimatorController EnemyMotion(ZombieType type)
        {
            switch(type)
            {
                case ZombieType.Runner: return runnerMotionController;
                case ZombieType.Armored: return armoredMotionController;
                case ZombieType.Brute: return bruteMotionController;
                case ZombieType.Toxic: return toxicMotionController;
                case ZombieType.Crawler: return crawlerMotionController;
                case ZombieType.Boss: return null;
                default: return walkerMotionController;
            }
        }
        public Avatar EnemyAvatar(ZombieType type)
        {
            switch(type)
            {
                case ZombieType.Runner: return runnerAvatar;
                case ZombieType.Armored: return armoredAvatar;
                case ZombieType.Brute: return bruteAvatar;
                case ZombieType.Toxic: return toxicAvatar;
                case ZombieType.Crawler: return crawlerAvatar;
                case ZombieType.Boss: return null;
                default: return walkerAvatar;
            }
        }

        public GameObject EnemyPrefab(ZombieType type)
        {
            switch (type)
            {
                case ZombieType.Runner: return runnerPrefab != null ? runnerPrefab : walkerPrefab;
                case ZombieType.Armored: return armoredPrefab != null ? armoredPrefab : walkerPrefab;
                case ZombieType.Brute: return brutePrefab != null ? brutePrefab : walkerPrefab;
                case ZombieType.Toxic: return toxicPrefab != null ? toxicPrefab : walkerPrefab;
                case ZombieType.Crawler: return crawlerPrefab != null ? crawlerPrefab : walkerPrefab;
                case ZombieType.Boss: return bossPrefab;
                default: return walkerPrefab;
            }
        }
    }

    /// <summary>
    /// Bridge between existing game mechanics and genuine Humanoid FBX/Prefab assets.
    /// Never tries to turn simple OBJ geometry into a skinned character at runtime.
    /// </summary>
    public sealed class ZSRProductionArt : MonoBehaviour
    {
        static bool warnedMissingSettings;
        static readonly System.Collections.Generic.HashSet<string> WarnedMissingPrefabs = new();

        static ZSRProductionArtSettings LoadSettings()
        {
            var settings = Resources.Load<ZSRProductionArtSettings>("ProductionArtSettings");
            if (settings == null && !warnedMissingSettings)
            {
                warnedMissingSettings = true;
                Debug.LogWarning("[ZSR ART] Missing Resources/ProductionArtSettings.asset. Character art will use bundled fallback models.");
            }
            return settings;
        }

        static bool HasPrefab(GameObject prefab, string character)
        {
            if (prefab != null) return true;
            if (WarnedMissingPrefabs.Add(character))
                Debug.LogWarning("[ZSR ART] ProductionArtSettings has no prefab for " + character + ". Using bundled fallback art.");
            return false;
        }

        Animator animator;
        ZSRPolyforkBoneMotion motion;
        GameObject muzzle;
        bool dead;
        float hitUntil;
        float shootUntil;
        int currentState = 0;
        float lateralLean;
        public Transform WeaponAnchor { get; private set; }

        public static ZSRProductionArt CreateHero(Transform parent, int selected, WeaponType gun)
        {
            var settings = LoadSettings();
            if (settings == null) return null;
            var prefab = selected == 0 ? settings.maxPrefab : settings.evaPrefab;
            if (!HasPrefab(prefab, selected == 0 ? "Max" : "Eva")) return null;
            var fallback = selected == 0 ? settings.maxMotionController : settings.evaMotionController;
            var avatar = selected == 0 ? settings.maxAvatar : settings.evaAvatar;
            return Create(parent, prefab, settings.heroController, fallback, avatar, true, gun,
                settings.heroOffset, settings.heroScale, settings.spawnLegacyGun);
        }

        public static ZSRProductionArt CreateZombie(Transform parent, ZombieType type)
        {
            var settings = LoadSettings();
            if (settings == null) return null;
            var prefab = settings.EnemyPrefab(type);
            if (!HasPrefab(prefab, type.ToString())) return null;
            var imported = Create(parent, prefab, settings.zombieController,
                settings.EnemyMotion(type), settings.EnemyAvatar(type), false,
                WeaponType.Rifle, settings.zombieOffset, settings.zombieScale, false);
            if (imported != null) imported.ConfigureInfectedStyle(type);
            return imported;
        }

        static ZSRProductionArt Create(Transform parent, GameObject prefab,
            RuntimeAnimatorController controller, RuntimeAnimatorController perModelController,
            Avatar avatar, bool hero, WeaponType weapon,
            Vector3 offset, float scale, bool legacyGun)
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, parent, false);
            instance.name = "PRODUCTION ART / " + prefab.name;
            instance.transform.localPosition = offset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
            if (hero && parent.name.ToUpperInvariant().Contains("EVA"))
                RemoveOversizedEvaAccessories(instance.transform);
            var meshes = instance.GetComponentsInChildren<Renderer>(true);
            if (meshes.Length == 0)
            {
                Debug.LogError("[ZSR Production] Prefab " + prefab.name + " has no renderers; retaining legacy model.");
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
                return null;
            }
            var production = instance.AddComponent<ZSRProductionArt>();
            // Generic curve clips have paths relative to the imported GLB root.
            // The Animator therefore MUST live on that root, never an arbitrary child.
            production.animator = instance.GetComponent<Animator>();
            if (production.animator == null) production.animator = instance.AddComponent<Animator>();
            foreach (var nested in instance.GetComponentsInChildren<Animator>(true))
                if (nested != production.animator) nested.enabled = false;
            production.animator.applyRootMotion = false;
            production.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bool humanoidReady = avatar != null && avatar.isValid && avatar.isHuman;
            // Humanoid mocap clips require a valid Avatar. Never apply such clips
            // to an imported GLB that has only generic bone transforms.
            if (controller != null && humanoidReady)
            {
                production.animator.avatar = avatar;
                production.animator.runtimeAnimatorController = controller;
            }
            else if (perModelController != null)
            {
                // Generic curves bind directly to this GLB's actual bone hierarchy.
                production.animator.avatar = null;
                production.animator.runtimeAnimatorController = perModelController;
            }
            else if (controller != null)
                Debug.LogWarning("[ZSR LUX] " + prefab.name + ": no valid Humanoid Avatar. Build Generic controllers using menu 13.");
            if (production.animator.runtimeAnimatorController == null)
                Debug.LogWarning("[ZSR LUX] " + prefab.name + ": no animation controller. Use menu 13 (LUX character animations).");
            foreach(var renderer in meshes)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            if (hero)
            {
                Transform rightHand = null;
                if (production.animator != null && production.animator.isHuman)
                    rightHand = production.animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand == null)
                {
                    foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                    {
                        string n = t.name.ToLowerInvariant().Replace("mixamorig", "")
                            .Replace(":", "").Replace("_", "").Replace(" ", "");
                        if (n == "righthand") { rightHand = t; break; }
                    }
                }
                production.WeaponAnchor = new GameObject("ZSR Weapon Socket").transform;
                production.WeaponAnchor.SetParent(rightHand != null ? rightHand : instance.transform, false);
                if (rightHand == null) production.WeaponAnchor.localPosition = new Vector3(.18f, 1.45f, .36f);
                // Do not let Eva spawn unarmed when the imported medic prefab has
                // no weapon geometry. Existing bundled survivor weapons remain intact.
                if (legacyGun || !HasEquippedWeapon(instance.transform))
                    ZSRVisuals.Rifle(production.WeaponAnchor, weapon);
                production.muzzle = ZSRVisuals.Sphere("Muzzle flash", production.WeaponAnchor,
                    rightHand != null ? new Vector3(0f, 0f, .60f) : new Vector3(0f, 0f, 1.1f),
                    new Vector3(.10f, .10f, .13f), ZSRVisuals.OrangeGlow);
                production.muzzle.SetActive(false);
            }
            if (production.animator == null || production.animator.runtimeAnimatorController == null)
            {
                production.motion = instance.AddComponent<ZSRPolyforkBoneMotion>();
                production.motion.Init(hero);
            }
            production.PlayRun();
            return production;
        }

        static bool HasEquippedWeapon(Transform root)
        {
            foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mesh == null) continue;
                string n = mesh.name.ToLowerInvariant();
                if (n.Contains("rifle") || n.Contains("gun") || n.Contains("pistol") ||
                    n.Contains("shotgun") || n.Contains("weapon") || n.Contains("carbine"))
                    return true;
            }
            foreach (var mesh in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (mesh == null) continue;
                string n = mesh.name.ToLowerInvariant();
                if (n.Contains("weapon") || n.Contains("rifle") || n.Contains("pistol")) return true;
            }
            return false;
        }

        /// <summary>Removes unmistakable non-wearable equipment from the female import.
        /// Never removes the skinned body or parts of the skeleton.</summary>
        // Silhouette and colour cues for a readable horde, even when a free GLB
        // library shares one base zombie between several variants.
        void ConfigureInfectedStyle(ZombieType type)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Color tone = type == ZombieType.Toxic ? new Color(.70f,1f,.62f) :
                    type == ZombieType.Armored ? new Color(.71f,.79f,.84f) :
                    type == ZombieType.Brute ? new Color(1f,.66f,.64f) :
                    type == ZombieType.Crawler ? new Color(.72f,.76f,.75f) :
                    type == ZombieType.Runner ? new Color(.86f,.90f,.84f) : Color.white;
                block.SetColor("_BaseColor", tone);
                renderer.SetPropertyBlock(block);
            }
            var artRoot = ZSRVisuals.Pivot("CLASS DETAIL / " + type, transform, Vector3.zero);
            if (type == ZombieType.Armored)
            {
                ZSRVisuals.Box("damaged riot chest plate",artRoot,new Vector3(0,1.37f,-.35f),
                    new Vector3(.80f,.58f,.12f),ZSRVisuals.Armor,new Vector3(-10,0,0));
                for (int s = -1;s<=1;s+=2)
                    ZSRVisuals.Box("asymmetric armored shoulder",artRoot,
                        new Vector3(s*.48f,1.56f,0),new Vector3(.32f,.20f,.43f),ZSRVisuals.MetalWorn);
            }
            else if (type == ZombieType.Toxic)
            {
                for(int i=0;i<5;i++)
                    ZSRVisuals.Sphere("luminescent infection",artRoot,
                        new Vector3(Mathf.Sin(i*2.3f)*.29f,1.12f+i*.13f,-.31f),
                        new Vector3(.12f+i*.014f,.10f,.10f),ZSRVisuals.ToxicGlow);
            }
            else if (type == ZombieType.Brute)
            {
                ZSRVisuals.Box("massive broken shoulder harness",artRoot,
                    new Vector3(0,1.49f,-.23f),new Vector3(1.12f,.27f,.22f),ZSRVisuals.MetalWorn);
            }
            // Runner and crawler distinguish themselves primarily by speed and body posture.
        }

        public void SetZombieMovementStyle(ZombieType type)
        {
            if (dead) return;
            float pitch = type == ZombieType.Crawler ? 48f : type == ZombieType.Runner ? 13f :
                type == ZombieType.Brute ? -5f : type == ZombieType.Toxic ? 7f : 3f;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                Quaternion.Euler(pitch,0,0),1f-Mathf.Exp(-Time.deltaTime*8f));
            Vector3 p=transform.localPosition;
            p.y=Mathf.Lerp(p.y,type == ZombieType.Crawler ? -.34f : 0f,
                1f-Mathf.Exp(-Time.deltaTime*8f));
            transform.localPosition=p;
        }

        static void RemoveOversizedEvaAccessories(Transform importedRoot)
        {
            foreach (var child in importedRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == importedRoot || child.GetComponent<Animator>() != null) continue;
                string n = child.name.ToLowerInvariant();
                if (!n.Contains("stretcher") && !n.Contains("wheelchair") &&
                    !n.Contains("gurney") && !n.Contains("trolley") &&
                    !n.Contains("pushcart") && !n.Contains("medicalcart") &&
                    !n.Contains("hospitalbed") && !n.Contains("wheelcart") &&
                    !n.Contains("rollingbed") && !n.Contains("medical_case") &&
                    !n.Contains("medicalcase")) continue;
                // Disable *only* a named prop renderer. Don't disable its parent rig/bones.
                foreach (var visual in child.GetComponentsInChildren<Renderer>(true))
                    visual.enabled = false;
                Debug.Log("[ZSR v0.9] Hidden oversized Eva visual: " + child.name);
            }
        }

        void ChangeState(string name, float blend = .12f)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(name);
            if (!animator.HasState(0, hash) || currentState == hash) return;
            animator.CrossFade(hash, blend, 0);
            currentState = hash;
        }

        public void PlayRun()
        {
            dead = false;
            hitUntil = 0f;
            shootUntil = 0f;
            currentState = 0;
            ChangeState("Run", 0f);
            if(motion != null) motion.Running = true;
        }

        public void SetLateralLean(float movement)
        {
            if (dead) return;
            float tilt = Mathf.Clamp(-movement * .85f, -6f, 6f);
            lateralLean = Mathf.Lerp(lateralLean, tilt, 1f - Mathf.Exp(-Time.deltaTime * 11f));
            transform.localRotation = Quaternion.Euler(0f, 0f, lateralLean);
        }

        public void Tick(bool running, float speed)
        {
            if (dead) return;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.speed = Mathf.Clamp(speed, .6f, 1.8f);
            if (Time.time >= hitUntil)
            {
                bool shooting = running && Time.time < shootUntil;
                ChangeState(shooting ? "RunShoot" : running ? "Run" : "Idle", .10f);
            }
            if(motion != null) { motion.Running = running; motion.Speed = speed; }
        }

        public void Hit()
        {
            if (dead) return;
            hitUntil = Time.time + .20f;
            ChangeState("Hit", .04f);
            if(motion != null) motion.ReactHit();
        }

        public void Die()
        {
            dead = true;
            shootUntil = 0f;
            if (animator != null) animator.speed = 1f;
            ChangeState("Death", .07f);
            if(motion != null) motion.Running = false;
        }

        public void Fire()
        {
            if (dead) return;
            shootUntil = Time.time + .20f;
            if (muzzle == null) return;
            muzzle.SetActive(true);
            CancelInvoke(nameof(HideMuzzle));
            Invoke(nameof(HideMuzzle), .06f);
        }

        void HideMuzzle() { if (muzzle != null) muzzle.SetActive(false); }
        void OnDisable() { CancelInvoke(); }
    }
}
