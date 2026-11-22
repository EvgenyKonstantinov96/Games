using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieSurvivalRun
{
    public enum ZSRState { Menu, Heroes, Arsenal, Bonuses, Items, Levels, Playing, Paused, Victory, Defeat }
    public enum ItemType { Medkit, Grenade, Drone, Armor }

    /// <summary>
    /// Zombie Survival Run 0.6.0 premium 3D prototype.
    /// Self-contained vertical Unity prototype with 15 levels, improved visuals,
    /// weapon progression and upgradable tactical items.
    /// </summary>
    public sealed class ZSRGame : MonoBehaviour
    {
        public const int MaxLevels = 15;
        public const int MaxUpgradeLevel = 5;

        public static ZSRGame Instance { get; private set; }
        public ZSRState State { get; private set; } = ZSRState.Menu;
        public bool IsRunning => State == ZSRState.Playing;
        public ZSRHero Hero { get; private set; }
        public int Level { get; private set; } = 1;
        public int UnlockedLevel { get; private set; } = 1;
        public int SelectedHero { get; private set; }
        public WeaponType Weapon { get; private set; } = WeaponType.Rifle;
        public float RunSpeed => 11.7f + Mathf.Min(4.6f, (Level - 1) * .22f);
        // The road, enemies, pickups and obstacles must advance by the same capped step.
        public float FrameStep => Mathf.Min(Time.deltaTime, .05f);
        public float LevelLength => IsBossLevel ? 84f + Chapter * 4f : 66f + StageInChapter * 4f + Chapter * 2f;
        public float Elapsed { get; private set; }
        public float Progress => Mathf.Clamp01(Elapsed / LevelLength);
        public int Coins { get; private set; }
        public int Earned { get; private set; }
        public int Eliminated { get; private set; }
        public int Magazine { get; private set; } = 30;
        public int Ammo { get; private set; } = 30;
        public bool IsShielded => shieldTimer > 0;
        public bool MagnetActive => magnetTimer > 0;
        public bool IsReloading => reloading;
        public string CurrentNotice => noticeTimer > 0 ? notice : "";
        public int Grenades { get; private set; }
        public int Medkits { get; private set; }
        public int Drones { get; private set; }
        public bool BossAlive => boss != null && boss.Alive && boss.gameObject.activeInHierarchy;
        public float BossHealthFraction => BossAlive ? boss.HealthFraction : 0f;
        public string LevelTitle => LevelNames[Mathf.Clamp(Level - 1, 0, LevelNames.Length - 1)];
        public int Chapter => Mathf.Clamp((Level - 1) / 5, 0, 2);
        public int StageInChapter => (Level - 1) % 5;
        public bool IsBossLevel => Level % 5 == 0;
        public int WeaponLevel => PlayerPrefs.GetInt("zsr40_weapon_level_" + (int)Weapon, 1);
        public int UpgradeCost => WeaponLevel * 750;

        public static readonly string[] LevelNames =
        {
            "ПОСЛЕДНИЙ РУБЕЖ", "ТЁМНЫЙ ПРОСПЕКТ", "ОГНЕННАЯ ТРАССА", "ПАНИКА В ЦЕНТРЕ", "МУТАНТ У ВОРОТ",
            "РАЗРУШЕННЫЙ МОСТ", "ПРОРЫВ К КВАРТАЛАМ", "НЕОНОВЫЙ РАЙОН", "ШТУРМ БАШНИ", "КАРАТЕЛЬ КАНАЛОВ",
            "ЭВАКУАЦИЯ", "ВОЕННЫЙ КОРИДОР", "ПОСЛЕДНИЙ КОНВОЙ", "ПЕПЕЛ И СИРЕНЫ", "ПОВЕЛИТЕЛЬ ОРДЫ"
        };

        readonly List<ZSRZombie> enemies = new List<ZSRZombie>();
        readonly Dictionary<ZombieType, Queue<ZSRZombie>> pools = new Dictionary<ZombieType, Queue<ZSRZombie>>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly List<ZSRRoadChunk> road = new List<ZSRRoadChunk>();
        GameObject worldRoot, shieldEffect, drone, skyline;
        Camera camera3D;
        AudioSource audioSource;
        AudioClip shotAudio, coinAudio, hitAudio, boomAudio, winAudio;
        ZSRZombie boss;
        float shootTimer, reloadTimer, waveTimer, coinTimer, gateTimer, carTimer;
        float bonusTimer, shieldTimer, magnetTimer, droneTimer, noticeTimer, shake, droneShotTimer;
        int damageMultiplier = 1;
        bool reloading, bossSpawned;
        string notice = "";
        Vector3 camVelocity;
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
            // Conservative mobile fallback: high-end phones keep 60 FPS, low-memory devices use 30.
            QualitySettings.vSyncCount = 0;
            bool lowMemoryPhone = Application.isMobilePlatform &&
                SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2400;
            Application.targetFrameRate = lowMemoryPhone ? 30 : 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID
            Screen.orientation = ScreenOrientation.Portrait;
#endif
            Coins = PlayerPrefs.GetInt("zsr40_coins", 2200);
            UnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt("zsr40_unlocked", 1), 1, MaxLevels);
            SelectedHero = Mathf.Clamp(PlayerPrefs.GetInt("zsr40_hero", 0), 0, 1);
            Weapon = (WeaponType)Mathf.Clamp(PlayerPrefs.GetInt("zsr40_weapon", 1), 0, 3);
            if (!WeaponOwned(Weapon)) Weapon = WeaponType.Rifle;

            ZSRVisuals.Init();
            BuildScene();
            gameObject.AddComponent<ZSRInterface>();

            shotAudio = Synth("gun", .12f, 130, 63, .32f);
            coinAudio = Synth("coin", .13f, 620, 1050, .33f);
            hitAudio = Synth("hit", .16f, 100, 65, .36f);
            boomAudio = Synth("boom", .35f, 90, 32, .45f);
            winAudio = Synth("win", .28f, 360, 770, .32f);
        }

        public int WeaponUnlockLevel(WeaponType t)
            => t == WeaponType.Pistol ? 1 : t == WeaponType.Rifle ? 1 : t == WeaponType.Shotgun ? 4 : 9;

        public int WeaponPrice(WeaponType t)
            => t == WeaponType.Pistol ? 0 : t == WeaponType.Rifle ? 0 : t == WeaponType.Shotgun ? 2200 : 5400;

        public bool WeaponLevelUnlocked(WeaponType t) => UnlockedLevel >= WeaponUnlockLevel(t);

        public bool WeaponOwned(WeaponType t)
            => WeaponPrice(t) == 0 || PlayerPrefs.GetInt("zsr40_weapon_owned_" + (int)t, 0) == 1;

        public int ItemLevel(ItemType item) => Mathf.Clamp(PlayerPrefs.GetInt("zsr40_item_level_" + (int)item, 1), 1, MaxUpgradeLevel);

        public int ItemUpgradeCost(ItemType item)
        {
            int level = ItemLevel(item);
            int baseCost = item == ItemType.Medkit ? 950 : item == ItemType.Grenade ? 1100 : item == ItemType.Drone ? 1350 : 1500;
            return baseCost * level;
        }

        public int ItemStartCount(ItemType item)
        {
            int level = ItemLevel(item);
            switch (item)
            {
                case ItemType.Medkit: return 1 + level / 2;
                case ItemType.Grenade: return 1 + level / 2;
                case ItemType.Drone: return level >= 4 ? 2 : 1;
                default: return 0;
            }
        }

        public string ThemeName => Chapter == 0 ? "РУИНЫ МЕГАПОЛИСА" : Chapter == 1 ? "ОГНЕННЫЙ ГОРОД" : "ЭВАКУАЦИОННАЯ ЗОНА";

        void BuildScene()
        {
            worldRoot = new GameObject("3D WORLD / generated geometry, level props, characters");
            worldRoot.transform.SetParent(transform, false);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0095f;
            RenderSettings.fogColor = new Color(.24f, .28f, .35f);
            // Tri-lighting adds shape to the imported character models at minimal mobile cost.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.28f, .37f, .57f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .25f, .35f);
            RenderSettings.ambientGroundColor = new Color(.10f, .13f, .18f);
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
            RenderSettings.sun = sun;
            sun.intensity = 1.24f;
            sun.color = new Color(1f, .64f, .39f);
            sun.transform.rotation = Quaternion.Euler(29, -136, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;
            sun.shadowBias = .035f;
            sun.shadowNormalBias = .28f;

            var cool = new GameObject("Blue fill light").AddComponent<Light>();
            cool.transform.SetParent(worldRoot.transform, false);
            cool.type = LightType.Directional;
            cool.intensity = .62f;
            cool.color = new Color(.50f, .66f, 1f);
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
            camera3D.allowHDR = true;
            camera3D.fieldOfView = 52f;
            camera3D.nearClipPlane = .1f;
            camera3D.farClipPlane = 200f;
            camera3D.clearFlags = CameraClearFlags.SolidColor;
            camera3D.backgroundColor = new Color(.14f, .18f, .25f);
            camera3D.transform.position = new Vector3(0, 3.48f, -8.35f);
            camera3D.transform.LookAt(new Vector3(0, 1.52f, 16.5f));
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

            var shield = new GameObject("Protective energy shield");
            shield.transform.SetParent(Hero.transform, false);
            shieldEffect = shield;
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24f;
                ZSRVisuals.Sphere("energy shield link", shield.transform,
                    new Vector3(Mathf.Sin(a) * 1.22f, 1.46f, Mathf.Cos(a) * 1.22f),
                    new Vector3(.14f, .16f, .14f), ZSRVisuals.BlueGlow);
            }
            ZSRVisuals.Cylinder("shield ring", shield.transform, new Vector3(0, 1.45f, 0), new Vector3(1.28f, .05f, 1.28f), ZSRVisuals.BlueGlow, new Vector3(90, 0, 0));
            shieldEffect.SetActive(false);
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
            var bloom=profile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(.26f);
            bloom.scatter.Override(.62f);
            var grade=profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(13f);
            grade.saturation.Override(7f);
            grade.postExposure.Override(.05f);
            var vignette=profile.Add<Vignette>(true);
            vignette.intensity.Override(.18f);
            vignette.smoothness.Override(.48f);
            volume.sharedProfile=profile;
        }

        void AddAmbientParticles()
        {
            var o=new GameObject("Floating cinematic ash sparks");
            o.transform.SetParent(worldRoot.transform,false);
            o.transform.localPosition=new Vector3(0f,5f,30f);
            var ps=o.AddComponent<ParticleSystem>();
            var main=ps.main;
            main.loop=true;
            main.startLifetime=4.6f;
            main.startSpeed=.48f;
            main.startSize=.08f;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,.52f,.17f,.75f),new Color(.26f,.54f,.75f,.34f));
            main.maxParticles=Application.isMobilePlatform?90:160;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;
            emission.rateOverTime=Application.isMobilePlatform?10f:18f;
            var shape=ps.shape;
            shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale=new Vector3(13f,8f,64f);
            var particleRenderer=o.GetComponent<ParticleSystemRenderer>();
            var fxShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(fxShader!=null)
            {
                var mat=new Material(fxShader);
                if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",new Color(1f,.8f,.58f,.65f));
                particleRenderer.sharedMaterial=mat;
            }
            particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;
        }

        ZSRParallaxCity parallaxCity;

        void RebuildRoad()
        {
            if(parallaxCity!=null)Destroy(parallaxCity.gameObject);
            if(skyline!=null)Destroy(skyline);
            skyline=ZSRVisuals.CityPanorama(worldRoot.transform,Chapter);
            parallaxCity = ZSRParallaxCity.Create(worldRoot.transform, Chapter, skyline);
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
            Color fog = Chapter == 0 ? new Color(.34f, .39f, .47f) : Chapter == 1 ? new Color(.46f, .28f, .24f) : new Color(.21f, .27f, .38f);
            RenderSettings.fogColor = fog;
            RenderSettings.ambientLight = Chapter == 0 ? new Color(.49f, .52f, .58f) : Chapter == 1 ? new Color(.51f, .44f, .40f) : new Color(.40f, .43f, .52f);
            camera3D.backgroundColor = Color.Lerp(fog, Color.black, .20f);
        }

        public void StartLevel(int requested = 0)
        {
            if (requested >= 1 && requested <= MaxLevels) Level = requested;
            Level = Mathf.Clamp(Level, 1, MaxLevels);
            ClearRun();
            worldRoot.SetActive(true);
            RebuildRoad();
            Hero.gameObject.SetActive(true);
            Hero.ResetHero(SelectedHero, Weapon);
            if (shieldEffect != null) shieldEffect.SetActive(false);

            State = ZSRState.Playing;
            Elapsed = 0;
            Earned = 0;
            Eliminated = 0;
            Grenades = ItemStartCount(ItemType.Grenade);
            Medkits = ItemStartCount(ItemType.Medkit);
            Drones = ItemStartCount(ItemType.Drone);
            Magazine = Weapon == WeaponType.Pistol ? 12 : Weapon == WeaponType.Rifle ? 30 : Weapon == WeaponType.Shotgun ? 8 : 70;
            Ammo = Magazine;
            shootTimer = .3f;
            reloadTimer = 0;
            waveTimer = .8f;
            coinTimer = 1.7f;
            gateTimer = 8.5f;
            carTimer = 9f;
            bonusTimer = 0;
            shieldTimer = 0;
            magnetTimer = 0;
            droneTimer = 0;
            damageMultiplier = 1;
            reloading = false;
            bossSpawned = false;
            boss = null;
            camVelocity = Vector3.zero;
            shake = 0f;
            Notify(LevelTitle, 2.5f);
            camera3D.transform.position = new Vector3(0, 3.48f, -8.35f);
        }

        static bool ExpiredRunObject(GameObject obj) => obj == null;

        void ClearRun()
        {
            foreach (var z in new List<ZSRZombie>(enemies)) RecycleZombie(z);
            enemies.Clear();
            boss = null;
            foreach (var o in temporary) if (o != null) Destroy(o);
            temporary.Clear();
            if (drone != null) { Destroy(drone); drone = null; }
        }

        void Update()
        {
            if (State != ZSRState.Playing) return;
            temporary.RemoveAll(ExpiredRunObject);
            float dt = FrameStep;
            HandleInput(dt);
            Elapsed += dt;

            foreach (var chunk in road) chunk.Tick(dt * RunSpeed);
            if (shieldEffect != null && shieldEffect.activeSelf) shieldEffect.transform.Rotate(0, 115f * dt, 0);

            shootTimer -= dt; waveTimer -= dt; coinTimer -= dt; gateTimer -= dt; carTimer -= dt;
            bonusTimer = Mathf.Max(0, bonusTimer - dt);
            shieldTimer = Mathf.Max(0, shieldTimer - dt);
            magnetTimer = Mathf.Max(0, magnetTimer - dt);
            droneTimer = Mathf.Max(0, droneTimer - dt);
            noticeTimer = Mathf.Max(0, noticeTimer - dt);

            if (bonusTimer <= 0) damageMultiplier = 1;
            if (shieldTimer <= 0 && shieldEffect != null && shieldEffect.activeSelf) shieldEffect.SetActive(false);
            if (coinTimer <= 0) { SpawnCoins(); coinTimer = UnityEngine.Random.Range(2.4f, 4.3f); }
            if (gateTimer <= 0 && Elapsed < LevelLength - 13f) { SpawnGates(); gateTimer = 16f + Chapter * 2f; }
            if (carTimer <= 0 && Elapsed < LevelLength - 10f) { SpawnObstacle(); carTimer = UnityEngine.Random.Range(9f, 14f); }
            if (waveTimer <= 0 && Elapsed < LevelLength - (IsBossLevel ? 18f : 5f))
            {
                SpawnWave();
                waveTimer = Mathf.Max(1.0f, 2.8f - Level * .06f - Elapsed * .0065f);
            }

            if (IsBossLevel && Elapsed > LevelLength - 18f && !bossSpawned)
            {
                bossSpawned = true;
                SpawnEnemy(ZombieType.Boss, 0, 43);
                if (Level >= 10)
                {
                    SpawnEnemy(ZombieType.Brute, -2.7f, 37f);
                    SpawnEnemy(ZombieType.Brute, 2.7f, 39.2f);
                }
                Notify("СУПЕР-БОСС НА ПОДХОДЕ!", 2.6f);
            }

            if (!reloading && shootTimer <= 0) Shoot();
            if (reloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0) { reloading = false; Ammo = Magazine; Notify("ПЕРЕЗАРЯЖЕНО", .55f); }
            }

            if (droneTimer > 0)
            {
                droneShotTimer -= dt;
                if (droneShotTimer <= 0)
                {
                    droneShotTimer = Mathf.Max(.35f, .90f - ItemLevel(ItemType.Drone) * .1f);
                    var target = FindTarget();
                    if (target != null) target.TakeHit(22 + ItemLevel(ItemType.Drone) * 12, Hero.Muzzle);
                }
            }
            else if (drone != null) { Destroy(drone); drone = null; }

            shake = Mathf.MoveTowards(shake, 0, dt * 2.8f);
            float camX = Hero.PositionX * .20f;
            var desired = new Vector3(camX + UnityEngine.Random.Range(-shake, shake) * .12f, 3.48f, -8.35f);
            camera3D.transform.position = Vector3.SmoothDamp(camera3D.transform.position, desired, ref camVelocity, .17f, 60f, dt);
            camera3D.transform.rotation = Quaternion.Slerp(camera3D.transform.rotation,
                Quaternion.LookRotation(new Vector3(Hero.PositionX * .50f, 1.52f, 16.5f) - camera3D.transform.position), dt * 5f);

            if (Hero.Health <= 0) { Finish(false); return; }
            if (Elapsed >= LevelLength)
            {
                if (IsBossLevel && BossAlive)
                {
                    Notify("УНИЧТОЖЬ БОССА!", 1f);
                    if (Elapsed >= LevelLength + 24f) Finish(false);
                }
                else Finish(true);
            }
        }

        void HandleInput(float dt)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) Hero.TargetX -= dt * 8f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) Hero.TargetX += dt * 8f;
                if (keyboard.spaceKey.wasPressedThisFrame) ThrowGrenade();
                if (keyboard.hKey.wasPressedThisFrame) UseMedkit();
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                Vector2 p = touchscreen.primaryTouch.position.ReadValue();
                if (p.y > Screen.height * .22f && p.y < Screen.height * .80f)
                    Hero.TargetX += touchscreen.primaryTouch.delta.ReadValue().x / Mathf.Max(1f, Screen.width) * 11f;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                if (p.y > Screen.height * .22f && p.y < Screen.height * .80f)
                    Hero.TargetX += Mouse.current.delta.ReadValue().x / Mathf.Max(1f, Screen.width) * 11f;
            }
            Hero.TargetX = Mathf.Clamp(Hero.TargetX, -4.3f, 4.3f);
        }

        ZSRZombie FindTarget()
        {
            ZSRZombie best = null;
            float score = float.MaxValue;
            foreach (var z in enemies)
            {
                if (z == null || !z.Alive || !z.gameObject.activeInHierarchy || z.DistanceZ < .6f || z.DistanceZ > 52) continue;
                float dist = Mathf.Abs(z.X - Hero.PositionX);
                if (dist > 7.3f) continue;
                float s = z.DistanceZ + dist * .5f;
                if (s < score) { score = s; best = z; }
            }
            return best;
        }

        void Shoot()
        {
            ZSRZombie target = FindTarget();
            if (target == null) { shootTimer = .08f; return; }
            float rate = Weapon == WeaponType.Pistol ? .33f : Weapon == WeaponType.Rifle ? .13f : Weapon == WeaponType.Shotgun ? .57f : .065f;
            if (SelectedHero == 1) rate *= .90f;
            if (activeBonus == BonusKind.RapidFire && bonusTimer > 0) rate *= .62f;
            shootTimer = rate;
            Hero.Fire();
            shake = .16f;
            audioSource.PlayOneShot(shotAudio, Weapon == WeaponType.Minigun ? .30f : .60f);
            Ammo--;
            int damage = (Weapon == WeaponType.Pistol ? 27 : Weapon == WeaponType.Rifle ? 22 : Weapon == WeaponType.Shotgun ? 55 : 13) + WeaponLevel * 5;
            damage *= damageMultiplier;
            if (Weapon == WeaponType.Shotgun)
            {
                target.TakeHit(damage, Hero.Muzzle);
                foreach (var z in enemies)
                    if (z != null && z != target && z.Alive && Mathf.Abs(z.X - target.X) < 1.7f && Mathf.Abs(z.DistanceZ - target.DistanceZ) < 2.5f)
                        z.TakeHit(damage / 3, Hero.Muzzle);
            }
            else target.TakeHit(damage, Hero.Muzzle);
            Trace(Hero.Muzzle, target.AimPoint);
            if (Ammo <= 0)
            {
                reloading = true;
                reloadTimer = Weapon == WeaponType.Pistol ? 1.05f : Weapon == WeaponType.Shotgun ? 1.65f : Weapon == WeaponType.Minigun ? 2.6f : 1.35f;
                Hero.Reload(reloadTimer);
                Notify("ПЕРЕЗАРЯДКА", .9f);
            }
        }

        void Trace(Vector3 start, Vector3 end)
        {
            var fx = new GameObject("bullet tracer");
            fx.transform.position = (start + end) * .5f;
            fx.transform.rotation = Quaternion.LookRotation(end - start);
            ZSRVisuals.Box("streak", fx.transform, Vector3.zero, new Vector3(.045f, .045f, Vector3.Distance(start, end)), ZSRVisuals.GoldGlow);
            fx.AddComponent<ZSRLifetime>().Duration = .07f;
        }

        // Small mixed formations from the first level: enemies are recognizable from a distance.
        // Height and collision are still owned by each ZSRZombie actor.
        void SpawnWave()
        {
            int n = Mathf.Clamp(3 + (Level - 1) / 4 + (StageInChapter >= 3 ? 1 : 0), 3, 6);
            if (IsBossLevel) n = Mathf.Max(3, n - 1);
            float start = UnityEngine.Random.Range(-.55f, .55f);
            for (int i = 0; i < n; i++)
            {
                float roll = UnityEngine.Random.value;
                ZombieType kind = ZombieType.Walker;
                if (roll < .27f) kind = ZombieType.Runner;
                else if (Level >= 2 && roll < .44f) kind = ZombieType.Armored;
                else if (Level >= 3 && roll < .60f) kind = ZombieType.Toxic;
                else if (Level >= 5 && roll < .73f) kind = ZombieType.Crawler;
                else if (Level >= 6 && roll < .86f) kind = ZombieType.Brute;
                // Slight stagger avoids overlapping identical silhouettes in the camera.
                float lane = ((i % 3) - 1) * 2.72f + start;
                float distance = 36f + (i / 3) * 4.4f + UnityEngine.Random.Range(-.9f, .9f);
                SpawnEnemy(kind, Mathf.Clamp(lane, -3.95f, 3.95f), distance);
            }
        }

        void SpawnEnemy(ZombieType type, float x, float z)
        {
            if (enemies.Count >= 44) return;
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
            enemy.Activate(x, z, Level);
            enemies.Add(enemy);
            if (type == ZombieType.Boss) boss = enemy;
        }

        void SpawnCoins()
        {
            float x = UnityEngine.Random.Range(-3.3f, 3.3f);
            int amount = 5 + (StageInChapter >= 3 ? 1 : 0);
            for (int i = 0; i < amount; i++)
            {
                var go = new GameObject("Gold coin");
                go.transform.SetParent(worldRoot.transform, false);
                go.AddComponent<ZSRPickup>().Build(x, 36 + i * 2.4f, 25 + Chapter * 10);
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
            // The player sees a real choice instead of the same fixed pair every time.
            BonusKind first = Hero.Health < Hero.MaxHealth * .64f ? BonusKind.Heal :
                (Elapsed > 34f && UnityEngine.Random.value > .48f ? BonusKind.Magnet : BonusKind.DoubleDamage);
            BonusKind second = shieldTimer <= 0f && Level >= 2 ? BonusKind.Shield : BonusKind.RapidFire;
            if (Level >= 5 && UnityEngine.Random.value > .57f) second = BonusKind.Turret;
            if (second == first) second = first == BonusKind.Heal ? BonusKind.Shield : BonusKind.RapidFire;
            a.Build(first, -2.5f, 39f);
            b.Build(second, 2.5f, 39f);
            temporary.Add(a.gameObject); temporary.Add(b.gameObject);
            Notify("ВЫБЕРИ УЛУЧШЕНИЕ", 2f);
        }

        void SpawnObstacle()
        {
            var o = new GameObject("Wrecked vehicle");
            o.transform.SetParent(worldRoot.transform, false);
            o.AddComponent<ZSRObstacle>().Build(UnityEngine.Random.value > .5f ? -2.9f : 2.9f, 43f, Chapter);
            temporary.Add(o);
        }

        public void ApplyBonus(BonusKind k)
        {
            audioSource.PlayOneShot(winAudio, .37f);
            if (k == BonusKind.Shield)
            {
                shieldTimer = 8f + (ItemLevel(ItemType.Armor) - 1) * 1.5f;
                shieldEffect.SetActive(true);
                Notify("ЩИТ: " + Mathf.RoundToInt(shieldTimer) + " СЕК", 1.7f);
            }
            else if (k == BonusKind.DoubleDamage) { damageMultiplier = 2; activeBonus = k; bonusTimer = 10f + Chapter; Notify("ДВОЙНОЙ УРОН", 1.8f); }
            else if (k == BonusKind.RapidFire) { damageMultiplier = 1; activeBonus = k; bonusTimer = 11f; Notify("ТУРБО-СТРЕЛЬБА", 1.8f); }
            else if (k == BonusKind.Magnet) { magnetTimer = 12f; Notify("МАГНИТ МОНЕТ", 1.8f); }
            else if (k == BonusKind.Heal) { Hero.Health = Mathf.Min(Hero.MaxHealth, Hero.Health + 45); Notify("ЗДОРОВЬЕ +45", 1.8f); }
            else if (k == BonusKind.Turret) { StartDrone(); Notify("ПОДДЕРЖКА С ВОЗДУХА", 1.8f); }
        }

        public void ThrowGrenade()
        {
            if (!IsRunning || Grenades <= 0) return;
            Grenades--;
            shake = .65f;
            audioSource.PlayOneShot(boomAudio, .8f);
            float radius = 4.6f + ItemLevel(ItemType.Grenade) * .42f;
            int grenadeDamage = 105 + ItemLevel(ItemType.Grenade) * 38;
            // Explode near the closest live target; otherwise at a fixed point ahead.
            var primary = FindTarget();
            Vector3 blast = primary != null ? primary.transform.position + Vector3.up * 1.0f :
                Hero.transform.position + new Vector3(0, 1.0f, 14f);
            ZSRVisuals.Explosion(blast, 2.0f + ItemLevel(ItemType.Grenade) * .20f, ZSRVisuals.OrangeGlow, .38f);
            int hits = 0;
            foreach (var z in new List<ZSRZombie>(enemies))
            {
                if (z == null || !z.Alive || !z.gameObject.activeInHierarchy) continue;
                float range = Vector3.Distance(z.AimPoint, blast);
                if (range > radius) continue;
                float falloff = 1f - .52f * (range / radius);
                z.TakeHit(Mathf.Max(1, Mathf.RoundToInt(grenadeDamage * falloff)), blast);
                hits++;
            }
            Notify("ГРАНАТА • " + hits + " ЦЕЛЕЙ", 1.6f);
        }

        public void UseMedkit()
        {
            if (!IsRunning || Medkits <= 0 || Hero.Health >= Hero.MaxHealth) return;
            Medkits--;
            int heal = 40 + ItemLevel(ItemType.Medkit) * 18;
            Hero.Health = Mathf.Min(Hero.MaxHealth, Hero.Health + heal);
            Notify("АПТЕЧКА +" + heal, 1.5f);
            audioSource.PlayOneShot(winAudio, .35f);
        }

        public void StartDrone()
        {
            if (drone != null) Destroy(drone);
            var droneRoot = new GameObject("drone fire support");
            droneRoot.transform.SetParent(Hero.transform, false);
            droneRoot.transform.localPosition = new Vector3(-1.2f, 3.1f, .2f);
            drone = droneRoot;
            ZSRVisuals.Box("aircraft", drone.transform, Vector3.zero, new Vector3(.65f, .14f, .38f), ZSRVisuals.Gunmetal);
            for (int i = -1; i <= 1; i += 2)
                ZSRVisuals.Cylinder("rotor", drone.transform, new Vector3(i * .46f, .10f, 0), new Vector3(.38f, .015f, .38f), ZSRVisuals.Steel);
            droneTimer = 10f + ItemLevel(ItemType.Drone) * 2f;
            droneShotTimer = .25f;
        }

        public void UseDrone()
        {
            if (!IsRunning || Drones <= 0) return;
            Drones--;
            StartDrone();
            Notify("ДРОН В БОЮ", 1.5f);
        }

        public void HurtPlayer(int amount)
        {
            if (!IsRunning) return;
            if (IsShielded) { Notify("ЩИТ ПОГЛОТИЛ УРОН", .9f); return; }
            float reduction = 1f - (ItemLevel(ItemType.Armor) - 1) * .06f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(amount * reduction));
            Hero.Health = Mathf.Max(0, Hero.Health - finalDamage);
            Hero.Hit();
            shake = .48f;
            Notify("-" + finalDamage + " HP", .7f);
        }

        public void SpawnImpact(Vector3 position, bool armor)
        {
            if (!IsRunning) return;
            ZSRVisuals.Explosion(position, .70f, armor ? ZSRVisuals.BlueGlow : ZSRVisuals.RedGlow, .21f);
            audioSource.PlayOneShot(hitAudio, .13f);
        }

        public void OnZombieKilled(ZSRZombie z)
        {
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
            pools[zombie.Kind].Enqueue(zombie);
        }

        public void CollectCoin(int value, Vector3 at)
        {
            Earned += value;
            audioSource.PlayOneShot(coinAudio, .22f);
            ZSRVisuals.Explosion(at, .35f, ZSRVisuals.GoldGlow, .14f);
        }

        void Finish(bool victory)
        {
            if (!victory) Hero.Die();
            State = victory ? ZSRState.Victory : ZSRState.Defeat;
            if (victory)
            {
                Earned += 320 + Level * 95 + (IsBossLevel ? 450 : 0);
                Coins += Earned;
                UnlockedLevel = Mathf.Max(UnlockedLevel, Mathf.Min(MaxLevels, Level + 1));
                PlayerPrefs.SetInt("zsr40_unlocked", UnlockedLevel);
                PlayerPrefs.SetInt("zsr40_coins", Coins);
                PlayerPrefs.Save();
                audioSource.PlayOneShot(winAudio, .56f);
            }
            else
            {
                Coins += Mathf.Max(25, Earned / 2);
                PlayerPrefs.SetInt("zsr40_coins", Coins);
                PlayerPrefs.Save();
            }
        }

        public void Notify(string t, float seconds) { notice = t; noticeTimer = seconds; }
        void OnApplicationPause(bool p) { if (p && IsRunning) State = ZSRState.Paused; }
        public void TogglePause() { if (State == ZSRState.Playing) State = ZSRState.Paused; else if (State == ZSRState.Paused) State = ZSRState.Playing; }
        public void GoMenu() { ClearRun(); State = ZSRState.Menu; Hero.gameObject.SetActive(false); worldRoot.SetActive(false); }
        public void GoHeroes() { State = ZSRState.Heroes; }
        public void GoArsenal() { State = ZSRState.Arsenal; }
        public void GoBonuses() { State = ZSRState.Bonuses; }
        public void GoItems() { State = ZSRState.Items; }
        public void GoLevels() { State = ZSRState.Levels; }

        public void SelectHero(int index)
        {
            SelectedHero = Mathf.Clamp(index, 0, 1);
            PlayerPrefs.SetInt("zsr40_hero", SelectedHero);
            PlayerPrefs.Save();
        }

        public void SelectLevel(int index) { Level = Mathf.Clamp(index, 1, MaxLevels); }

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

        public void UpgradeWeapon()
        {
            if (WeaponLevel >= MaxUpgradeLevel || Coins < UpgradeCost) return;
            Coins -= UpgradeCost;
            PlayerPrefs.SetInt("zsr40_weapon_level_" + (int)Weapon, WeaponLevel + 1);
            PlayerPrefs.SetInt("zsr40_coins", Coins);
            PlayerPrefs.Save();
        }

        public void UpgradeItem(ItemType item)
        {
            int level = ItemLevel(item);
            int cost = ItemUpgradeCost(item);
            if (level >= MaxUpgradeLevel || Coins < cost) return;
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
                case ItemType.Medkit: return "Лечит на " + (40 + lvl * 18) + " HP";
                case ItemType.Grenade: return "Урон гранаты: " + (105 + lvl * 38);
                case ItemType.Drone: return "Длительность: " + (10 + lvl * 2) + " сек";
                default: return "Снижает входящий урон на " + Mathf.RoundToInt((lvl - 1) * 6f) + "%";
            }
        }

        public void NextLevel() { StartLevel(Mathf.Min(MaxLevels, Level + 1)); }
        public void Retry() { StartLevel(Level); }
#if UNITY_EDITOR
        public void UnlockForTesting() { UnlockedLevel = MaxLevels; PlayerPrefs.SetInt("zsr40_unlocked", MaxLevels); PlayerPrefs.Save(); }
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
