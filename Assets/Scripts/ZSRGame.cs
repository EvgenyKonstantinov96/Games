using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieSurvivalRun
{
    public enum ZSRState { Menu, Heroes, Arsenal, Bonuses, Items, Levels, Playing, Paused, Victory, Defeat, Briefing }
    public enum ZSRDamageKind { Bite, Claw, Acid, Collision, Slam }
    public enum ItemType { Medkit, Grenade, Drone, Armor }

    /// <summary>
    /// Zombie Survival Run 0.6.0 premium 3D prototype.
    /// Self-contained vertical Unity prototype with a 1,000-level campaign, improved visuals,
    /// weapon progression and upgradable tactical items.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class ZSRGame : MonoBehaviour
    {
        public const int MaxLevels = ZSRLevelPlan.Count;
        public const int MaxUpgradeLevel = ZSRLevelPlan.MaximumRank;

        const float RunCameraHeight = 2.96f;
        const float RunCameraDepth = -6.82f;
        const float RunCameraLookHeight = .85f;
        const float RunCameraLookDepth = 15.8f;
        const float RunCameraFollow = .94f;
        const float RunCameraLead = .33f;
        const float RunCameraFov = 56f;
        const float RunCameraReferenceAspect = .45f;
        const float RunLaneLimit = 4.3f;

        public static ZSRGame Instance { get; private set; }
        public ZSRState State { get; private set; } = ZSRState.Menu;
        public bool IsRunning => State == ZSRState.Playing;
        public ZSRHero Hero { get; private set; }
        internal ZSRLevelPlan Plan { get; private set; } = new ZSRLevelPlan(1);
        public int Level => Plan.Level;
        public int UnlockedLevel { get; private set; } = 1;
        internal int ClearedLevel { get; private set; }
        internal int Payout { get; private set; }
        internal bool Replaying { get; private set; }
        internal int ReplayLevel
        {
            get
            {
                int candidate = Mathf.Min(ClearedLevel, Level - 1);
                if (candidate > 1 && new ZSRLevelPlan(candidate).IsBoss) candidate--;
                return candidate;
            }
        }
        internal ZSRStory.Beat StoryBeat { get; private set; }
        internal bool HasStoryBeat { get; private set; }
        internal int DialogueIndex { get; private set; }
        internal ZSRStory.Beat Briefing { get; private set; }
        public int SelectedHero { get; private set; }
        public WeaponType Weapon { get; private set; } = WeaponType.Rifle;
        public float RunSpeed => Plan.Speed * pace.Scale;
        internal float PaceScale => pace.Scale;
        internal ZSRPace Pace => pace.Mode;
        internal float PaceRecharge => pace.Recharge;
        internal float RunPaceMultiplier => Plan.SpawnDepthScale;
        // The road, enemies, pickups and obstacles must advance by the same capped step.
        public float FrameStep => Mathf.Min(Time.deltaTime, .05f);
        public float LevelLength => Plan.Duration;
        public float Elapsed { get; private set; }
        public float Progress => Mathf.Clamp01(Elapsed / LevelLength);
        public int Coins { get; private set; }
        public int Earned { get; private set; }
        public int Eliminated { get; private set; }
        public int Magazine { get; private set; } = 30;
        public int Ammo { get; private set; } = 30;
        internal const int GateHealAmount = 45;
        internal const float RapidFireInterval = .62f;
        internal float DamageFlash { get; private set; }
        internal float DamageSourceX { get; private set; }
        internal ZSRDamageKind LastDamageKind { get; private set; }
        internal static string DamageName(ZSRDamageKind kind) => kind switch
        {
            ZSRDamageKind.Bite => "УКУС",
            ZSRDamageKind.Claw => "УДАР ЗОМБИ",
            ZSRDamageKind.Acid => "КИСЛОТА",
            ZSRDamageKind.Collision => "СТОЛКНОВЕНИЕ",
            _ => "УДАР БОССА"
        };
        public bool IsShielded => shieldTimer > 0;
        internal float BonusDuration(BonusKind kind) => kind switch
        {
            BonusKind.DoubleDamage => 10f + Chapter,
            BonusKind.RapidFire => 11f,
            BonusKind.Shield => ShieldSeconds(ItemLevel(ItemType.Armor)),
            BonusKind.Magnet => 12f,
            BonusKind.Turret => DroneDuration(ItemLevel(ItemType.Drone)),
            _ => 0f
        };
        internal float BonusRemaining(BonusKind kind) => kind switch
        {
            BonusKind.Shield => shieldTimer,
            BonusKind.Magnet => magnetTimer,
            BonusKind.Turret => droneTimer,
            BonusKind.DoubleDamage or BonusKind.RapidFire when kind == activeBonus => bonusTimer,
            _ => 0f
        };
        internal const float MagnetRadius = 3.6f;
        public bool MagnetActive => magnetTimer > 0;
        public bool IsReloading => reloading;
        public string CurrentNotice => noticeTimer > 0 ? notice : "";
        public int Grenades { get; private set; }
        public int Medkits { get; private set; }
        public int Drones { get; private set; }
        public float DroneSeconds => droneTimer;
        public bool BossAlive => boss != null && boss.Alive && boss.gameObject.activeInHierarchy;
        public float BossHealthFraction => BossAlive ? boss.HealthFraction : 0f;
        public string LevelTitle => Level <= LevelNames.Length ? LevelNames[Level - 1] :
            Plan.District.ToUpperInvariant();
        public int Chapter => Plan.Environment;
        public int StageInChapter => Plan.Position;
        public bool IsBossLevel => Plan.IsBoss;
        public int WeaponLevel => WeaponLevelFor(Weapon);
        public int UpgradeCost => WeaponUpgradeCost(Weapon);
        internal int WeaponLevelFor(WeaponType weapon) =>
            Mathf.Clamp(PlayerPrefs.GetInt("zsr40_weapon_level_" + (int)weapon, 1), 1, MaxUpgradeLevel);
        internal int CampaignUpgradeCap => ZSRLevelPlan.UpgradeCap(UnlockedLevel);
        internal int ItemUpgradeCap(ItemType item) => item == ItemType.Medkit
            ? Mathf.Min(CampaignUpgradeCap, ZSRLevelPlan.MedicalRank) : CampaignUpgradeCap;
        internal int WeaponUpgradeCost(WeaponType weapon) => ZSRLevelPlan.UpgradeCost(750, WeaponLevelFor(weapon));

        public static readonly string[] LevelNames =
        {
            "ПОСЛЕДНИЙ РУБЕЖ", "ТЁМНЫЙ ПРОСПЕКТ", "ОГНЕННАЯ ТРАССА", "ПАНИКА В ЦЕНТРЕ", "МУТАНТ У ВОРОТ",
            "РАЗРУШЕННЫЙ МОСТ", "ПРОРЫВ К КВАРТАЛАМ", "НЕОНОВЫЙ РАЙОН", "ШТУРМ БАШНИ", "КАРАТЕЛЬ КАНАЛОВ",
            "ЭВАКУАЦИЯ", "ВОЕННЫЙ КОРИДОР", "ПОСЛЕДНИЙ КОНВОЙ", "ПЕПЕЛ И СИРЕНЫ", "ПОВЕЛИТЕЛЬ ОРДЫ"
        };

        readonly ZSRRunPace pace = new();
        readonly ZSRStrideGesture gesture = new();
        enum RunPointer { None, Touch, Mouse }
        RunPointer pointerSource;
        bool waitForPointerRelease;

        static RunPointer ActivePointer => Touchscreen.current?.primaryTouch.press.isPressed == true ? RunPointer.Touch :
            Mouse.current?.leftButton.isPressed == true ? RunPointer.Mouse : RunPointer.None;

        void CancelGesture()
        {
            gesture.Reset();
            waitForPointerRelease = ActivePointer != RunPointer.None;
        }

        readonly List<ZSRZombie> enemies = new List<ZSRZombie>();
        readonly Dictionary<ZombieType, Queue<ZSRZombie>> pools = new Dictionary<ZombieType, Queue<ZSRZombie>>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly List<float> trafficDepths = new List<float>(8);
        readonly List<ZSRRoadChunk> road = new List<ZSRRoadChunk>();
        readonly List<ZSRGrenadeFlight> grenadesInFlight = new();
        GameObject worldRoot, skyline, distantDeck;
        ZSRSupportDrone drone;
        ZSRCombatEffects combatEffects;
        ZSRRunMusic music;
        readonly AudioClip[] weaponAudio = new AudioClip[4];
        Camera camera3D;
        Light chapterKey, chapterFill;
        ParticleSystem chapterAir;
        VolumeProfile postProfile;
        Material chapterAirMaterial;
        AudioSource audioSource;
        AudioClip droneAudio, coinAudio, hitAudio, boomAudio, winAudio, biteAudio, clawAudio, acidAudio, spitAudio;
        public string MusicLabel => music != null ? music.Label : "МУЗЫКА";
        public void CycleMusic() { if (music != null) music.Cycle(); }
        ZSRZombie boss;
        float shootTimer, reloadTimer, waveTimer, coinTimer, gateTimer, carTimer;
        float bonusTimer, shieldTimer, magnetTimer, droneTimer, noticeTimer, shake, droneShotTimer, hitRecovery;
        int damageMultiplier = 1;
        int waveIndex, coinIndex, gateIndex, obstacleIndex;
        bool reloading, bossSpawned;
        string notice = "";
        BonusKind activeBonus = BonusKind.DoubleDamage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<ZSRGame>() != null) return;
            new GameObject("ZSR 0.7 ART DIRECTION ROOT").AddComponent<ZSRGame>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // URP's runtime debug shortcut creates an opaque [Debug Canvas]
            // over Game View. Normal play starts with both debug panels closed.
            DebugManager.instance.displayRuntimeUI = false;
            DebugManager.instance.displayPersistentRuntimeUI = false;
            DebugManager.instance.enableRuntimeUI = false;
            // Conservative mobile fallback: high-end phones keep 60 FPS, low-memory devices use 30.
            QualitySettings.vSyncCount = 0;
            bool lowMemoryPhone = Application.isMobilePlatform &&
                SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2400;
            Application.targetFrameRate = lowMemoryPhone ? 30 : 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID
            Screen.orientation = ScreenOrientation.Portrait;
#endif
            Coins = Mathf.Clamp(PlayerPrefs.GetInt("zsr40_coins", 2200), 0, 999999999);
            (UnlockedLevel, ClearedLevel) = ZSRLevelPlan.RestoreProgress(
                PlayerPrefs.GetInt("zsr40_unlocked", 1), PlayerPrefs.GetInt("zsr40_story_cleared", -1));
            SelectLevel(ClearedLevel + 1);
            SelectedHero = Mathf.Clamp(PlayerPrefs.GetInt("zsr40_hero", 0), 0, 1);
            Weapon = (WeaponType)Mathf.Clamp(PlayerPrefs.GetInt("zsr40_weapon", 0), 0, 3);
            Weapon = ZSRWeaponProfile.RestoreSelection(Weapon, UnlockedLevel, WeaponOwned(Weapon));

            ZSRVisuals.Init();
            BuildScene();
            gameObject.AddComponent<ZSRInterface>();

            music = gameObject.AddComponent<ZSRRunMusic>();
            for (int i = 0; i < weaponAudio.Length; i++)
                weaponAudio[i] = Resources.Load<AudioClip>("Audio/" + ((WeaponType)i)) ?? Synth("gun", .12f, 130, 63, .32f);
            biteAudio = Resources.Load<AudioClip>("Audio/Bite");
            clawAudio = Resources.Load<AudioClip>("Audio/Claw");
            acidAudio = Resources.Load<AudioClip>("Audio/AcidHit");
            spitAudio = Resources.Load<AudioClip>("Audio/Spit");
            droneAudio = Resources.Load<AudioClip>("Audio/Drone") ?? weaponAudio[0];
            coinAudio = Synth("coin", .13f, 620, 1050, .33f);
            hitAudio = Synth("hit", .16f, 100, 65, .36f);
            boomAudio = Resources.Load<AudioClip>("Audio/Blast") ?? Synth("boom", .35f, 90, 32, .45f);
            winAudio = Synth("win", .28f, 360, 770, .32f);
        }

        public int WeaponUnlockLevel(WeaponType t) => ZSRWeaponProfile.For(t).UnlockLevel;

        public int WeaponPrice(WeaponType t) => ZSRWeaponProfile.For(t).Price;

        public bool WeaponLevelUnlocked(WeaponType t) => UnlockedLevel >= WeaponUnlockLevel(t);

        public bool WeaponOwned(WeaponType t)
            => WeaponPrice(t) == 0 || PlayerPrefs.GetInt("zsr40_weapon_owned_" + (int)t, 0) == 1;

        public int ItemLevel(ItemType item) =>
            Mathf.Clamp(PlayerPrefs.GetInt("zsr40_item_level_" + (int)item, 1), 1, MaximumItemRank(item));
        internal static int MaximumItemRank(ItemType item) =>
            item == ItemType.Medkit ? ZSRLevelPlan.MedicalRank : MaxUpgradeLevel;

        public int ItemUpgradeCost(ItemType item)
        {
            int level = ItemLevel(item);
            int baseCost = item == ItemType.Medkit ? 950 : item == ItemType.Grenade ? 1100 : item == ItemType.Drone ? 1350 : 1500;
            return ZSRLevelPlan.UpgradeCost(baseCost, level);
        }

        public int ItemStartCount(ItemType item) => ItemStartCount(item, ItemLevel(item));

        internal static int ItemStartCount(ItemType item, int level) => item switch
        {
            ItemType.Medkit => 1 + Mathf.Min(level, 5) / 2 + BeyondFive(level),
            ItemType.Grenade => 1 + Mathf.Min(level, 5) / 2 + BeyondFive(level) / 4,
            ItemType.Drone => (level >= 4 ? 2 : 1) + BeyondFive(level) / 7,
            _ => 0
        };

        static int BeyondFive(int level) => Mathf.Max(0, level - 5);
        internal static int MedkitHealth(int level) => 40 + Mathf.Min(5, level) * 18;
        internal static int GrenadeDamage(int level) => 105 + Mathf.Min(5, level) * 38 + BeyondFive(level) * 22;
        internal static float GrenadeRadius(int level) => 4.6f + Mathf.Min(5, level) * .42f + BeyondFive(level) * .09f;
        internal static float DroneDuration(int level) => 10f + Mathf.Min(5, level) * 2f + BeyondFive(level) * .63f;
        internal static int DroneDamage(int level) => 22 + Mathf.Min(5, level) * 12 + BeyondFive(level) * 6;
        internal static float ShieldSeconds(int level) => 8f + (Mathf.Min(5, level) - 1) * 1.5f + BeyondFive(level) * .24f;
        internal static float ArmorReduction(int level) => (Mathf.Min(5, level) - 1) * .06f + BeyondFive(level) * .012f;

        public string ThemeName => ZSRCampaignStyle.For(Chapter).Title.ToUpperInvariant();

        void BuildScene()
        {
            worldRoot = new GameObject("3D WORLD / generated geometry, level props, characters");
            worldRoot.transform.SetParent(transform, false);
            combatEffects = worldRoot.AddComponent<ZSRCombatEffects>();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0125f;
            RenderSettings.fogColor = new Color(.24f, .28f, .35f);
            // Tri-lighting adds shape to the imported character models at minimal mobile cost.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.28f, .37f, .57f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .29f, .40f);
            RenderSettings.ambientGroundColor = new Color(.15f, .17f, .22f);
            RenderSettings.skybox = null;

            // A new Unity scene can already contain the template directional light.
            // An unsaved template must use the same light rig as ZSR_Main.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                foreach (var sceneRoot in activeScene.GetRootGameObjects())
                {
                    if (sceneRoot.name == "Directional Light" &&
                        sceneRoot.TryGetComponent<Light>(out var templateSun) &&
                        templateSun.type == LightType.Directional)
                        templateSun.enabled = false;
                }
            }

            var sun = new GameObject("Warm sunset key light").AddComponent<Light>();
            sun.transform.SetParent(worldRoot.transform, false);
            sun.type = LightType.Directional;
            chapterKey = sun;
            RenderSettings.sun = sun;
            sun.intensity = 1.36f;
            sun.color = new Color(1f, .64f, .39f);
            sun.transform.rotation = Quaternion.Euler(29, -136, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .66f;
            sun.shadowBias = .02f;
            sun.shadowNormalBias = .12f;

            var cool = new GameObject("Blue fill light").AddComponent<Light>();
            cool.transform.SetParent(worldRoot.transform, false);
            cool.type = LightType.Directional;
            chapterFill = cool;
            cool.intensity = 1.10f;
            cool.color = new Color(.70f, .79f, 1f);
            cool.shadows = LightShadows.None;
            cool.transform.rotation = Quaternion.Euler(31, 27, 0);

            camera3D = Camera.main;
            if (camera3D == null)
            {
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                camera3D = cam.AddComponent<Camera>();
            }
            // Ensure this is a Base Camera that actually draws the 3D world in URP.
            camera3D.enabled = true;
            camera3D.cullingMask = ~0;
            camera3D.targetTexture = null;
            var cameraData = camera3D.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera3D.allowHDR = true;
            camera3D.nearClipPlane = .1f;
            camera3D.farClipPlane = 230f;
            camera3D.clearFlags = CameraClearFlags.SolidColor;
            camera3D.backgroundColor = new Color(.14f, .18f, .25f);
            FrameRunCamera(0f);
            if (camera3D.GetComponent<AudioListener>() == null) camera3D.gameObject.AddComponent<AudioListener>();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            audioSource.volume = .40f;
            AddCinematicPostFX();
            AddAmbientParticles();

            var heroObject = new GameObject("PLAYABLE MAX / EVA");
            heroObject.transform.SetParent(worldRoot.transform, false);
            Hero = heroObject.AddComponent<ZSRHero>();
            Hero.Build(SelectedHero, Weapon);

            Hero.gameObject.SetActive(false);
            worldRoot.SetActive(false);
        }

        // Modest URP post-processing to keep performance reasonable on mid-range Android devices.
        // Gameplay remains functional if post-processing is disabled by the device quality setting.
        void AddCinematicPostFX()
        {
            var post=new GameObject("Cinematic URP bloom + grading");
            post.transform.SetParent(worldRoot.transform,false);
            var volume=post.AddComponent<Volume>();
            volume.isGlobal=true;
            volume.priority=8;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            postProfile = profile;
            var bloom=profile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(.26f);
            bloom.scatter.Override(.62f);
            var grade=profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(7f);
            grade.saturation.Override(-5f);
            grade.postExposure.Override(.18f);
            var vignette=profile.Add<Vignette>(true);
            vignette.intensity.Override(.12f);
            vignette.smoothness.Override(.48f);
            volume.sharedProfile=profile;
        }

        void AddAmbientParticles()
        {
            var shader = Resources.Load<Shader>("ProductionArt/ZSRCombatFX");
            var texture = Resources.Load<Texture2D>("ProductionArt/ParticleSoft");
            if (shader == null || !shader.isSupported || texture == null) return;
            var node = ZSRVisuals.Pivot("Chapter air particles", worldRoot.transform, new Vector3(0f, 3.65f, 27f));
            chapterAir = node.gameObject.AddComponent<ParticleSystem>();
            chapterAir.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = chapterAir.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = Application.isMobilePlatform ? 72 : 122;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = chapterAir.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 6.6f, 52f);
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.94f, .23f), new GradientAlphaKey(0f, 1f) });
            var overLife = chapterAir.colorOverLifetime;
            overLife.enabled = true;
            overLife.color = fade;
            chapterAirMaterial = new Material(shader) { name = "Soft chapter weather" };
            chapterAirMaterial.SetTexture("_BaseMap", texture);
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = chapterAirMaterial;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void ApplyChapterAtmosphere(ZSRCampaignStyle style)
        {
            chapterKey.color = style.Key;
            chapterKey.intensity = style.KeyIntensity;
            chapterFill.color = style.Fill;
            RenderSettings.fogColor = style.Fog;
            RenderSettings.fogDensity = style.FogDensity;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.28f, .37f, .57f), style.Fog, .35f);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.25f, .29f, .40f), style.Fog, .36f);
            camera3D.backgroundColor = Color.Lerp(style.Fog, Color.black, .20f);
            if (chapterAir == null) return;

            chapterAir.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = chapterAir.main;
            main.startLifetime = Chapter == 1 ? new ParticleSystem.MinMaxCurve(2.5f, 4.2f) : new ParticleSystem.MinMaxCurve(3.8f, 5.3f);
            main.startSpeed = Chapter == 1 ? new ParticleSystem.MinMaxCurve(.28f, .79f) : new ParticleSystem.MinMaxCurve(.14f, .42f);
            main.gravityModifier = Chapter == 1 ? -.033f : .012f;
            main.startSize = Chapter == 2 ? new ParticleSystem.MinMaxCurve(.07f, .17f) : new ParticleSystem.MinMaxCurve(.034f, .083f);
            Color mist = Color.Lerp(style.Fog, Color.white, .40f);
            mist.a = Chapter == 2 ? .22f : .33f;
            Color ember = style.Accent;
            ember.a = Chapter == 1 ? .82f : .49f;
            main.startColor = new ParticleSystem.MinMaxGradient(mist, ember);
            var emission = chapterAir.emission;
            emission.rateOverTime = (Chapter == 1 ? 17f : 11f) * (Application.isMobilePlatform ? .74f : 1f);
            chapterAir.Play();
        }

        void OnDestroy()
        {
            if (chapterAirMaterial != null) Destroy(chapterAirMaterial);
            if (postProfile != null) Destroy(postProfile);
        }

        ZSRParallaxCity parallaxCity;

        void RebuildRoad()
        {
            var style = ZSRCampaignStyle.For(Chapter);
            if (parallaxCity != null) Destroy(parallaxCity.gameObject);
            if (skyline != null) Destroy(skyline);
            if (distantDeck == null) distantDeck = ZSRTrackArt.BuildDistantDeck(worldRoot.transform);
            skyline = ZSRVisuals.CityPanorama(worldRoot.transform, Chapter);
            parallaxCity = ZSRParallaxCity.Create(worldRoot.transform, Chapter, skyline, style.Fog);
            foreach (var r in road) if (r != null) Destroy(r.gameObject);
            road.Clear();
            for (int i = 0; i < 4; i++)
            {
                var o = new GameObject("Infinite ruined highway segment " + i);
                o.transform.SetParent(worldRoot.transform, false);
                var r = o.AddComponent<ZSRRoadChunk>();
                r.Build(i, Level);
                road.Add(r);
            }
            ApplyChapterAtmosphere(style);
        }

        public void StartLevel(int requested = 0)
        {
            if (requested < 0 || requested > UnlockedLevel) return;
            Weapon = ZSRWeaponProfile.RestoreSelection(Weapon, UnlockedLevel, WeaponOwned(Weapon));
            if (requested != 0) SelectLevel(requested);
            Replaying = Level <= ClearedLevel;
            HasStoryBeat = ZSRStory.TryGet(Level, out var beat);
            StoryBeat = beat;
            bool hasBriefing = ZSRStory.TryBriefing(Level, out var briefing);
            Briefing = briefing;
            DialogueIndex = 0;
            ClearRun();
            worldRoot.SetActive(true);
            RebuildRoad();
            Hero.gameObject.SetActive(true);
            Hero.ResetHero(SelectedHero, Weapon);

            pace.Reset();
            CancelGesture();
            State = hasBriefing ? ZSRState.Briefing : ZSRState.Playing;
            if (hasBriefing && chapterAir != null) chapterAir.Pause();
            Elapsed = 0;
            Earned = Payout = 0;
            waveIndex = coinIndex = gateIndex = obstacleIndex = 0;
            Eliminated = 0;
            Grenades = ItemStartCount(ItemType.Grenade);
            Medkits = ItemStartCount(ItemType.Medkit);
            Drones = ItemStartCount(ItemType.Drone);
            Magazine = ZSRWeaponProfile.For(Weapon).Magazine;
            Ammo = Magazine;
            shootTimer = .3f;
            reloadTimer = 0;
            waveTimer = .8f;
            coinTimer = 1.7f;
            gateTimer = 8.5f;
            carTimer = 9f;
            bonusTimer = 0;
            shieldTimer = hitRecovery = DamageFlash = 0;
            magnetTimer = 0;
            droneTimer = 0;
            damageMultiplier = 1;
            reloading = false;
            bossSpawned = false;
            boss = null;
            shake = 0f;
            Notify(LevelTitle, 2.5f);
            FrameRunCamera(0f);
        }

        public void AdvanceDialogue()
        {
            if (State != ZSRState.Briefing) return;
            if (DialogueIndex == 0) { DialogueIndex = 1; return; }
            CancelGesture();
            State = ZSRState.Playing;
            if (chapterAir != null) chapterAir.Play();
            Notify(LevelTitle, 2.5f);
        }

        static bool ExpiredRunObject(GameObject obj) => obj == null;

        void ClearRun()
        {
            foreach (var z in new List<ZSRZombie>(enemies)) RecycleZombie(z);
            enemies.Clear();
            boss = null;
            foreach (var o in temporary) if (o != null) Destroy(o);
            temporary.Clear();
            ClearCombat();
        }

        void ClearCombat()
        {
            if (drone != null) { drone.gameObject.SetActive(false); Destroy(drone.gameObject); drone = null; }
            droneTimer = 0f;
            foreach (var grenade in grenadesInFlight) grenade.Discard();
            grenadesInFlight.Clear();
            combatEffects.Clear();
        }

        void Update()
        {
            if ((State == ZSRState.Playing || State == ZSRState.Paused) &&
                Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                TogglePause();
                return;
            }
            if (State != ZSRState.Playing) return;
            temporary.RemoveAll(ExpiredRunObject);
            float dt = FrameStep;
            HandleInput(dt);
            if (!IsRunning) return;
            pace.Advance(dt);
            Elapsed += dt;
            UpdateGrenades(dt);
            combatEffects.Tick(dt, RunSpeed, Hero.ItemOrigin);

            foreach (var chunk in road) chunk.Tick(dt * RunSpeed);

            shootTimer -= dt; waveTimer -= dt; coinTimer -= dt; gateTimer -= dt; carTimer -= dt;
            bonusTimer = Mathf.Max(0, bonusTimer - dt);
            shieldTimer = Mathf.Max(0, shieldTimer - dt);
            hitRecovery = Mathf.Max(0, hitRecovery - dt);
            DamageFlash = Mathf.Max(0f, DamageFlash - dt);
            magnetTimer = Mathf.Max(0, magnetTimer - dt);
            droneTimer = Mathf.Max(0, droneTimer - dt);
            noticeTimer = Mathf.Max(0, noticeTimer - dt);

            if (bonusTimer <= 0) damageMultiplier = 1;
            combatEffects.UpdateShield(dt, shieldTimer, Hero.transform.position);
            if (coinTimer <= 0) { SpawnCoins(); coinTimer = 2.4f + Plan.Roll(6, coinIndex) * 1.9f; }
            if (gateTimer <= 0 && Elapsed < LevelLength - 13f) { SpawnGates(); gateTimer = 16f + Chapter * 2f; }
            if (carTimer <= 0 && Elapsed < LevelLength - 10f) { SpawnObstacle(); carTimer = Plan.HazardDelay(obstacleIndex); }
            if (waveTimer <= 0 && Elapsed < LevelLength - (IsBossLevel ? 18f : 5f))
            {
                SpawnWave();
                waveTimer = Plan.WaveDelay(Elapsed);
            }

            if (IsBossLevel && Elapsed > LevelLength - 18f && !bossSpawned)
            {
                SpawnEnemy(ZombieType.Boss, Plan.Level > 15 ? (Plan.Roll(13, 0) - .5f) * 2f : 0f, 43);
                bossSpawned = BossAlive;
                if (bossSpawned && Level >= 10)
                {
                    SpawnEnemy(ZombieType.Brute, -2.7f, 37f);
                    SpawnEnemy(ZombieType.Brute, 2.7f, 39.2f);
                }
                if (bossSpawned) Notify("СУПЕР-БОСС НА ПОДХОДЕ!", 2.6f);
            }

            if (!reloading && !Hero.IsUsingItem && shootTimer <= 0) Shoot();
            if (reloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0) { reloading = false; Ammo = Magazine; Notify("ПЕРЕЗАРЯЖЕНО", .55f); }
            }

            UpdateDrone(dt);

            shake = Mathf.MoveTowards(shake, 0, dt * 2.8f);
            if (Hero.Health <= 0) { Finish(false); return; }
            if (Elapsed >= LevelLength)
            {
                if (IsBossLevel && (!bossSpawned || BossAlive))
                {
                    Notify("УНИЧТОЖЬ БОССА!", 1f);
                    if (Elapsed >= LevelLength + Plan.BossOvertime) Finish(false);
                }
                else Finish(true);
            }
        }

        void LateUpdate()
        {
            if (music != null) music.Mix(State, Time.unscaledDeltaTime);
            if (IsRunning)
            {
                combatEffects.TickSpits(FrameStep, RunSpeed);
                float jitter = Mathf.PerlinNoise(Time.unscaledTime * 20f, .37f) * 2f - 1f;
                FrameRunCamera(Hero.PositionX, jitter * shake * .09f);
            }
        }

        void FrameRunCamera(float heroX, float shakeOffset = 0f)
        {
            float aspect = Mathf.Max(.1f, camera3D.aspect);
            float halfAngle = (RunCameraFov + (pace.Scale - 1f) * 4f) * Mathf.Deg2Rad * .5f;
            float extraWidth = Mathf.Max(1f, RunCameraReferenceAspect / aspect);
            float fov = 2f * Mathf.Atan(Mathf.Tan(halfAngle) * extraWidth) * Mathf.Rad2Deg;
            if (Mathf.Abs(camera3D.fieldOfView - fov) > .01f) camera3D.fieldOfView = fov;

            // Follow the hero's already damped position after movement. Aim into
            // the road so its forward edges stay readable during a turn.
            var position = new Vector3(heroX * RunCameraFollow + shakeOffset, RunCameraHeight, RunCameraDepth);
            var focus = new Vector3(heroX * RunCameraLead, RunCameraLookHeight, RunCameraLookDepth);
            camera3D.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
        }

        void HandleInput(float dt)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) Hero.TargetX -= dt * 8f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) Hero.TargetX += dt * 8f;
                bool surge = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
                bool walk = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
                if (surge != walk) pace.TryChange(surge ? ZSRPace.Surge : ZSRPace.Walk);
                if (keyboard.spaceKey.wasPressedThisFrame) ThrowGrenade();
                if (keyboard.hKey.wasPressedThisFrame) UseMedkit();
            }

            RunPointer source = ActivePointer;
            if (source == RunPointer.None) waitForPointerRelease = false;
            if (source != pointerSource)
            {
                gesture.Reset();
                pointerSource = source;
            }
            if (!waitForPointerRelease)
            {
                Vector2 point = source switch
                {
                    RunPointer.Touch => Touchscreen.current.primaryTouch.position.ReadValue(),
                    RunPointer.Mouse => Mouse.current.position.ReadValue(),
                    _ => Vector2.zero
                };
                bool inPlayfield = point.y > Screen.height * .22f && point.y < Screen.height * .80f;
                var input = gesture.Read(source != RunPointer.None, inPlayfield, point / Mathf.Max(1f, Screen.width));
                Hero.TargetX += input.lateral * 11f;
                pace.TryChange(input.request);
            }
            Hero.TargetX = Mathf.Clamp(Hero.TargetX, -RunLaneLimit, RunLaneLimit);
        }

        ZSRZombie FindTarget() => FindTarget(Hero.PositionX, .6f);

        ZSRZombie FindTarget(float lateralCenter, float minDepth, ZSRSupportDrone turret = null)
        {
            ZSRZombie best = null;
            float score = float.MaxValue;
            foreach (var z in enemies)
            {
                if (z == null || !z.Alive || !z.gameObject.activeInHierarchy || z.DistanceZ < minDepth || z.DistanceZ > 52) continue;
                float dist = Mathf.Abs(z.X - lateralCenter);
                if (dist > 7.3f || (turret != null && !turret.CanReach(z.AimPoint))) continue;
                float s = z.DistanceZ + dist * .5f;
                if (s < score) { score = s; best = z; }
            }
            return best;
        }

        void Shoot()
        {
            ZSRZombie target = FindTarget();
            if (target == null) { shootTimer = .08f; return; }
            var profile = ZSRWeaponProfile.For(Weapon);
            float rate = profile.ShotInterval(SelectedHero);
            if (activeBonus == BonusKind.RapidFire && bonusTimer > 0) rate *= RapidFireInterval;
            // Carry the fraction of the frame after the scheduled shot without
            // building a backlog during reloads, item use, or a pause.
            shootTimer = Mathf.Max(-FrameStep, shootTimer) + rate;
            Vector3 aim = target.AimPoint;
            Hero.Fire(aim);
            Vector3 muzzle = Hero.Muzzle;
            shake = Mathf.Max(shake, Weapon == WeaponType.Shotgun ? .29f : .15f);
            audioSource.PlayOneShot(weaponAudio[(int)Weapon], Weapon == WeaponType.Minigun ? .38f : .68f);
            Ammo--;
            int damage = profile.Damage(WeaponLevel) * damageMultiplier;
            if (Weapon == WeaponType.Shotgun)
            {
                target.TakeHit(damage, muzzle);
                foreach (var z in enemies)
                    if (z != null && z != target && z.Alive && Mathf.Abs(z.X - target.X) < 1.7f && Mathf.Abs(z.DistanceZ - target.DistanceZ) < 2.5f)
                        z.TakeHit(damage / 3, muzzle);
            }
            else target.TakeHit(damage, muzzle);
            var tracerColor = profile.Tracer;
            combatEffects.Streak(muzzle, aim, tracerColor, profile.TracerWidth);
            if (Weapon == WeaponType.Shotgun)
            {
                combatEffects.Streak(muzzle, aim + new Vector3(.58f, .16f, .22f), tracerColor, .043f);
                combatEffects.Streak(muzzle, aim + new Vector3(-.49f, -.19f, -.16f), tracerColor, .043f);
            }
            if (Ammo <= 0)
            {
                reloading = true;
                reloadTimer = profile.Reload;
                Hero.Reload(reloadTimer);
                Notify("ПЕРЕЗАРЯДКА", .9f);
            }
        }

        void SpawnWave()
        {
            int wave = waveIndex++;
            int count = Plan.WaveSize(wave);
            for (int i = 0; i < count; i++)
            {
                var unit = Plan.WaveUnit(wave, i);
                SpawnEnemy(unit.kind, unit.x, unit.z);
            }
        }

        void SpawnEnemy(ZombieType type, float x, float z)
        {
            const int enemyLimit = 44;
            int limit = IsBossLevel && !bossSpawned && type != ZombieType.Boss ? enemyLimit - 1 : enemyLimit;
            if (enemies.Count >= limit) return;
            if (!pools.ContainsKey(type)) pools[type] = new Queue<ZSRZombie>();
            ZSRZombie enemy;
            if (pools[type].Count > 0) enemy = pools[type].Dequeue();
            else
            {
                var go = new GameObject("ZOMBIE");
                go.transform.SetParent(worldRoot.transform, false);
                enemy = go.AddComponent<ZSRZombie>();
                enemy.Construct(type);
            }
            enemy.Activate(x, z, Plan);
            enemies.Add(enemy);
            if (type == ZombieType.Boss) boss = enemy;
        }

        void SpawnCoins()
        {
            float x = (Plan.Roll(7, coinIndex++) - .5f) * 6.6f;
            int amount = 5 + (StageInChapter >= 3 ? 1 : 0);
            for (int i = 0; i < amount; i++)
            {
                var go = new GameObject("Gold coin");
                go.transform.SetParent(worldRoot.transform, false);
                go.AddComponent<ZSRPickup>().Build(x, (36 + i * 2.4f) * RunPaceMultiplier, Plan.PickupValue);
                temporary.Add(go);
            }
        }

        void SpawnGates()
        {
            var a = new GameObject("Bonus gate / Left").AddComponent<ZSRBonusGate>();
            var b = new GameObject("Bonus gate / Right").AddComponent<ZSRBonusGate>();
            a.transform.SetParent(worldRoot.transform, false);
            b.transform.SetParent(worldRoot.transform, false);
            a.Sister = b; b.Sister = a;
            int sequence = gateIndex++;
            // Keep the two choices responsive to low health.
            BonusKind first = Hero.Health < Hero.MaxHealth * .64f ? BonusKind.Heal :
                (Elapsed > 34f && Plan.Roll(8, sequence) > .48f ? BonusKind.Magnet : BonusKind.DoubleDamage);
            BonusKind second = shieldTimer <= 0f && Level >= 2 ? BonusKind.Shield : BonusKind.RapidFire;
            if (Level >= 5 && Plan.Roll(9, sequence) > .57f) second = BonusKind.Turret;
            if (second == first) second = first == BonusKind.Heal ? BonusKind.Shield : BonusKind.RapidFire;
            float depth = AvailableTrafficDepth(39f * RunPaceMultiplier);
            a.Build(first, -2.5f, depth);
            b.Build(second, 2.5f, depth);
            temporary.Add(a.gameObject); temporary.Add(b.gameObject);
            Notify("ВЫБЕРИ УЛУЧШЕНИЕ", 2f);
        }

        void SpawnObstacle()
        {
            int sequence = obstacleIndex++;
            float side = Plan.Roll(10, sequence) < .5f ? -1f : 1f;
            float depth = AvailableTrafficDepth(43f * RunPaceMultiplier);
            bool split = Plan.SplitHazard(sequence);
            AddObstacle(side * 2.9f, depth, Plan.ObstacleKind(sequence));
            if (split) AddObstacle(-side * 2.9f, depth, Plan.ObstacleKind(sequence + 900));
        }

        void AddObstacle(float x, float depth, int kind)
        {
            var obstacle = new GameObject("Road hazard");
            obstacle.transform.SetParent(worldRoot.transform, false);
            obstacle.AddComponent<ZSRObstacle>().Build(x, depth, Chapter, kind);
            temporary.Add(obstacle);
        }

        float AvailableTrafficDepth(float desiredDepth)
        {
            trafficDepths.Clear();
            foreach (var item in temporary)
            {
                if (item == null || item.transform.position.z <= 0f) continue;
                if (item.GetComponent<ZSRBonusGate>() != null || item.GetComponent<ZSRObstacle>() != null)
                    trafficDepths.Add(item.transform.position.z);
            }
            return SpaceTraffic(desiredDepth, Plan.Speed * ZSRRunPace.SurgeScale * 1.30f, trafficDepths);
        }

        internal static float SpaceTraffic(float depth, float clearance, List<float> occupied)
        {
            occupied.Sort();
            foreach (float other in occupied)
                if (Mathf.Abs(depth - other) < clearance) depth = other + clearance;
            return depth;
        }

        public void ApplyBonus(BonusKind kind)
        {
            audioSource.PlayOneShot(winAudio, .37f);
            var appearance = ZSRVeilGateView.Appearance(kind);
            combatEffects.BonusPickup(Hero.transform.position, appearance.tint);
            switch (kind)
            {
                case BonusKind.Shield:
                    shieldTimer = BonusDuration(kind);
                    break;
                case BonusKind.DoubleDamage:
                case BonusKind.RapidFire:
                    damageMultiplier = kind == BonusKind.DoubleDamage ? 2 : 1;
                    activeBonus = kind;
                    bonusTimer = BonusDuration(kind);
                    break;
                case BonusKind.Magnet:
                    magnetTimer = BonusDuration(kind);
                    break;
                case BonusKind.Heal:
                    HealHero(GateHealAmount, false);
                    return;
                case BonusKind.Turret:
                    StartDrone();
                    break;
            }
            Notify(appearance.title.ToUpperInvariant(), 1.65f);
        }

        internal void WarnBossStrike(float x, float z, float halfWidth, float charge)
            => combatEffects.WarnBossStrike(x, z, halfWidth, charge);

        internal void Footstep(Vector3 ankle)
            => combatEffects.Footstep(new Vector3(ankle.x, .035f, ankle.z));

        internal void BossStrike(float x, float z)
        {
            combatEffects.BossStrike(new Vector3(x, .14f, z));
            shake = Mathf.Max(shake, .24f);
        }

        internal void ToxicBurst(Vector3 at) => combatEffects.ToxicBurst(at);
        internal void LaunchToxicSpit(Vector3 at, float lane)
        {
            combatEffects.LaunchSpit(at, lane, RunSpeed);
            if (spitAudio != null) audioSource.PlayOneShot(spitAudio, .6f);
        }

        public void ThrowGrenade()
        {
            if (!IsRunning || Grenades <= 0 || Hero.IsUsingItem) return;
            Grenades--;
            Hero.UseItem(false);
            grenadesInFlight.Add(new ZSRGrenadeFlight(worldRoot.transform, combatEffects,
                Hero.ItemOrigin, ItemLevel(ItemType.Grenade)));
        }

        void UpdateGrenades(float dt)
        {
            for (int i = grenadesInFlight.Count - 1; i >= 0; i--)
            {
                var grenade = grenadesInFlight[i];
                var aim = grenade.WillRelease(dt) ? FindTarget() : null;
                if (!grenade.Tick(dt, RunSpeed, Hero.ItemOrigin, aim)) continue;
                Detonate(grenade.Position, grenade.ItemLevel);
                grenade.Discard();
                grenadesInFlight.RemoveAt(i);
            }
        }

        void Detonate(Vector3 position, int itemLevel)
        {
            shake = Mathf.Max(shake, .65f);
            audioSource.PlayOneShot(boomAudio, .85f);
            float radius = GrenadeRadius(itemLevel);
            int grenadeDamage = GrenadeDamage(itemLevel);
            combatEffects.Explode(position, radius);
            // Damage still uses the existing radial falloff. The effect starts on the ground.
            Vector3 damageCenter = position + Vector3.up * .88f;
            int hits = 0;
            foreach (var zombie in enemies)
            {
                if (zombie == null || !zombie.Alive || !zombie.gameObject.activeInHierarchy) continue;
                float distance = Vector3.Distance(zombie.AimPoint, damageCenter);
                if (distance > radius) continue;
                float falloff = 1f - .52f * (distance / radius);
                zombie.TakeHit(Mathf.Max(1, Mathf.RoundToInt(grenadeDamage * falloff)), damageCenter);
                hits++;
            }
            Notify("ГРАНАТА • " + hits + " ЦЕЛЕЙ", 1.6f);
        }

        public void UseMedkit()
        {
            if (!IsRunning || Medkits <= 0 || Hero.Health >= Hero.MaxHealth || Hero.IsUsingItem) return;
            Medkits--;
            Hero.UseItem(true);
            HealHero(MedkitHealth(ItemLevel(ItemType.Medkit)), true);
            audioSource.PlayOneShot(winAudio, .35f);
        }

        void HealHero(int amount, bool useKit)
        {
            int restored = Mathf.Max(0, Mathf.Min(amount, Hero.MaxHealth - Hero.Health));
            Hero.Health += restored;
            if (restored > 0)
                combatEffects.Heal(Hero.ItemOrigin, Hero.transform.position + Vector3.up * 1.15f, useKit);
            Notify(restored == 0 ? "ЗДОРОВЬЕ ПОЛНОЕ" :
                (useKit ? "АПТЕЧКА +" : "ЗДОРОВЬЕ +") + restored + " HP", 1.5f);
        }

        public void StartDrone()
        {
            if (drone == null) drone = ZSRSupportDrone.Launch(worldRoot.transform, Hero.transform.position);
            droneTimer = BonusDuration(BonusKind.Turret);
            droneShotTimer = .25f;
        }

        void UpdateDrone(float dt)
        {
            if (drone == null) return;
            if (droneTimer <= 0f)
            {
                Destroy(drone.gameObject);
                drone = null;
                return;
            }
            var target = FindTarget(drone.transform.position.x, drone.transform.position.z + .65f, drone);
            Vector3 aim = target != null ? target.AimPoint : drone.transform.position + Vector3.forward * 12f;
            drone.Tick(Hero.PositionX, aim, dt);
            droneShotTimer -= dt;
            if (droneShotTimer > 0f || target == null || !drone.IsOnTarget(aim)) return;
            int itemLevel = ItemLevel(ItemType.Drone);
            droneShotTimer = Mathf.Max(.35f, .90f - itemLevel * .1f);
            Vector3 origin = drone.Muzzle;
            drone.Fire();
            combatEffects.Streak(origin, aim, new Color(.24f, 1f, .88f), .073f);
            audioSource.PlayOneShot(droneAudio, .41f);
            target.TakeHit(DroneDamage(itemLevel), origin);
        }

        public void UseDrone()
        {
            if (!IsRunning || Drones <= 0) return;
            Drones--;
            StartDrone();
            Notify("ДРОН В БОЮ", 1.5f);
        }

        public void HurtPlayer(int amount, ZSRDamageKind kind = ZSRDamageKind.Claw, float sourceX = 0f)
        {
            if (!IsRunning || hitRecovery > 0f) return;
            if (IsShielded)
            {
                combatEffects.ShieldImpact(Hero.transform.position);
                Notify("ЩИТ ПОГЛОТИЛ УРОН", .9f);
                return;
            }
            float reduction = 1f - ArmorReduction(ItemLevel(ItemType.Armor));
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(amount * Plan.IncomingDamage * reduction));
            hitRecovery = Level > 15 ? .26f : 0f;
            Hero.Health = Mathf.Max(0, Hero.Health - finalDamage);
            Hero.Hit();
            DamageFlash = .65f;
            DamageSourceX = sourceX;
            LastDamageKind = kind;
            AudioClip sound = kind == ZSRDamageKind.Bite ? biteAudio :
                kind == ZSRDamageKind.Acid ? acidAudio : clawAudio;
            audioSource.PlayOneShot(sound != null ? sound : hitAudio, .86f);
            combatEffects.PlayerHit(Hero.transform.position, sourceX, kind == ZSRDamageKind.Acid);
            shake = .48f;
            Notify(DamageName(kind) + "  -" + finalDamage + " HP", 1.1f);
        }

        public void SpawnImpact(Vector3 position, bool armor)
        {
            if (!IsRunning) return;
            combatEffects.Hit(position, armor);
            audioSource.PlayOneShot(hitAudio, .13f);
        }

        public void OnZombieKilled(ZSRZombie z)
        {
            if (z.Kind == ZombieType.Boss) combatEffects.ClearBossWarning();
            Eliminated++;
            Earned += z.Kind == ZombieType.Boss ? 1200 : z.Kind == ZombieType.Brute ? 115 : z.Kind == ZombieType.Armored ? 44 : 20;
            if (Eliminated % 10 == 0) Notify("КОМБО " + Eliminated, 1.0f);
        }

        public void RecycleZombie(ZSRZombie zombie)
        {
            if (zombie == null) return;
            enemies.Remove(zombie);
            zombie.gameObject.SetActive(false);
            if (!pools.ContainsKey(zombie.Kind)) pools[zombie.Kind] = new Queue<ZSRZombie>();
            int keep = zombie.Kind == ZombieType.Boss ? 1 : 12;
            if (pools[zombie.Kind].Count < keep) pools[zombie.Kind].Enqueue(zombie);
            else Destroy(zombie.gameObject);
        }

        public void CollectCoin(int value, Vector3 at)
        {
            Earned += value;
            audioSource.PlayOneShot(coinAudio, .22f);
            combatEffects.CoinPickup(at);
        }

        void Finish(bool victory)
        {
            if (!victory) Hero.Die();
            State = victory ? ZSRState.Victory : ZSRState.Defeat;
            ClearCombat();
            Payout = Plan.Payout(Earned, victory, !Replaying, Progress);
            Coins = Mathf.Min(999999999, Coins + Payout);
            if (victory)
            {
                UnlockedLevel = Mathf.Max(UnlockedLevel, Mathf.Min(MaxLevels, Level + 1));
                ClearedLevel = Mathf.Max(ClearedLevel, Level);
                PlayerPrefs.SetInt("zsr40_story_cleared", ClearedLevel);
                PlayerPrefs.SetInt("zsr40_unlocked", UnlockedLevel);
                audioSource.PlayOneShot(winAudio, .56f);
            }
            PlayerPrefs.SetInt("zsr40_coins", Coins);
            PlayerPrefs.Save();
        }

        public void Notify(string t, float seconds) { notice = t; noticeTimer = seconds; }
        void OnApplicationPause(bool paused) { if (paused && IsRunning) TogglePause(); }
        public void TogglePause()
        {
            if (State != ZSRState.Playing && State != ZSRState.Paused) return;
            bool pause = State == ZSRState.Playing;
            State = pause ? ZSRState.Paused : ZSRState.Playing;
            CancelGesture();
            combatEffects.SetPaused(pause);
            if (chapterAir != null)
            {
                if (pause) chapterAir.Pause();
                else chapterAir.Play();
            }
        }
        public void GoMenu() { ClearRun(); State = ZSRState.Menu; Hero.gameObject.SetActive(false); worldRoot.SetActive(false); }
        public void GoHeroes() { State = ZSRState.Heroes; }
        public void GoArsenal()
        {
            if (State == ZSRState.Defeat || State == ZSRState.Victory) GoMenu();
            State = ZSRState.Arsenal;
        }
        public void GoBonuses() { State = ZSRState.Bonuses; }
        public void GoItems() { State = ZSRState.Items; }
        public void GoLevels() { State = ZSRState.Levels; }

        public void SelectHero(int index)
        {
            SelectedHero = Mathf.Clamp(index, 0, 1);
            PlayerPrefs.SetInt("zsr40_hero", SelectedHero);
            PlayerPrefs.Save();
        }

        public void SelectLevel(int index) { Plan = new ZSRLevelPlan(Mathf.Clamp(index, 1, UnlockedLevel)); }

        public void SelectWeapon(WeaponType chosen)
        {
            if (!WeaponLevelUnlocked(chosen))
            {
                Notify("ОТКРОЕТСЯ НА УРОВНЕ " + WeaponUnlockLevel(chosen), 1.5f);
                return;
            }
            if (WeaponOwned(chosen))
            {
                Weapon = chosen;
                PlayerPrefs.SetInt("zsr40_weapon", (int)Weapon);
                PlayerPrefs.Save();
            }
            else if (Coins >= WeaponPrice(chosen))
            {
                Coins -= WeaponPrice(chosen);
                PlayerPrefs.SetInt("zsr40_weapon_owned_" + (int)chosen, 1);
                Weapon = chosen;
                PlayerPrefs.SetInt("zsr40_weapon", (int)Weapon);
                PlayerPrefs.SetInt("zsr40_coins", Coins);
                PlayerPrefs.Save();
            }
            else Notify("НУЖНО БОЛЬШЕ МОНЕТ", 1.3f);
        }

        public void UpgradeWeapon() => UpgradeWeapon(Weapon);

        internal void UpgradeWeapon(WeaponType weapon)
        {
            int level = WeaponLevelFor(weapon);
            int cost = WeaponUpgradeCost(weapon);
            if (!WeaponLevelUnlocked(weapon) || !WeaponOwned(weapon) || level >= CampaignUpgradeCap || Coins < cost) return;
            Coins -= cost;
            PlayerPrefs.SetInt("zsr40_weapon_level_" + (int)weapon, level + 1);
            PlayerPrefs.SetInt("zsr40_coins", Coins);
            PlayerPrefs.Save();
        }

        public void UpgradeItem(ItemType item)
        {
            int level = ItemLevel(item);
            int cost = ItemUpgradeCost(item);
            if (level >= ItemUpgradeCap(item) || Coins < cost) return;
            Coins -= cost;
            PlayerPrefs.SetInt("zsr40_item_level_" + (int)item, level + 1);
            PlayerPrefs.SetInt("zsr40_coins", Coins);
            PlayerPrefs.Save();
        }

        public string ItemDescription(ItemType item)
        {
            int lvl = ItemLevel(item);
            switch (item)
            {
                case ItemType.Medkit: return "Лечит на " + (MedkitHealth(lvl)) + " HP";
                case ItemType.Grenade: return "Урон гранаты: " + (GrenadeDamage(lvl));
                case ItemType.Drone: return "Длительность: " + (DroneDuration(lvl)) + " сек";
                default: return "Снижает входящий урон на " + Mathf.RoundToInt(ArmorReduction(lvl) * 100f) + "%";
            }
        }

        public void NextLevel() { StartLevel(Mathf.Min(MaxLevels, Level + 1)); }
        public void Retry() { StartLevel(Level); }
#if UNITY_EDITOR
        public void UnlockForTesting()
        {
            UnlockedLevel = MaxLevels;
            PlayerPrefs.SetInt("zsr40_story_cleared", ClearedLevel);
            PlayerPrefs.SetInt("zsr40_unlocked", MaxLevels);
            PlayerPrefs.Save();
        }
#endif

        static AudioClip Synth(string n, float duration, float startHz, float finishHz, float gain)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] values = new float[count];
            System.Random rng = new System.Random(n.GetHashCode());
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startHz, finishHz, t);
                phase += 2 * Mathf.PI * frequency / sampleRate;
                float noise = ((float)rng.NextDouble() * 2 - 1);
                float env = Mathf.Pow(1 - t, 2.3f);
                values[i] = (Mathf.Sin(phase) * .35f + noise * .65f) * gain * env;
            }
            AudioClip clip = AudioClip.Create(n, count, 1, sampleRate, false);
            clip.SetData(values, 0);
            return clip;
        }
    }
}
