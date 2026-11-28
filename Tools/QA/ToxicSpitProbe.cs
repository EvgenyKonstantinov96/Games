extern alias engine;
global using Vector2 = engine::UnityEngine.Vector2;
global using Vector3 = engine::UnityEngine.Vector3;
global using Color = engine::UnityEngine.Color;
global using Mathf = engine::UnityEngine.Mathf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieSurvivalRun;

// Managed scene stand-ins. Unity vector math and the extracted gameplay methods
// are used as shipped. This probe does not drive PlayMode or validate a shader.
namespace UnityEngine
{
    public sealed class Transform
    {
        public Vector3 position, localEulerAngles, localPosition, localScale;
        public Quaternion rotation, localRotation;
        public void SetParent(Transform parent, bool worldPositionStays) {}
    }
    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion identity => default;
    }
    public class MonoBehaviour
    {
        public GameObject gameObject = new GameObject();
        public Transform transform => gameObject.transform;
    }
    public sealed class GameObject
    {
        public GameObject(string name = "") {}
        public readonly Transform transform = new Transform();
        public bool active = true;
        public void SetActive(bool value) => active = value;
        public T AddComponent<T>() where T : MonoBehaviour, new() => new T { gameObject = this };
    }
    public sealed class LineRenderer
    {
        public readonly Vector3[] Points;
        public LineRenderer(int count=2) { Points = new Vector3[count]; }
        public bool loop;
        public float widthMultiplier;
        public bool enabled;
        public float startWidth, endWidth;
        public Color startColor, endColor;
        public int numCapVertices;
        public void SetPosition(int index, Vector3 point) => Points[index] = point;
    }
    public sealed class ParticleSystem
    {
        public sealed class Velocity { public float z; }
        public readonly Velocity velocityOverLifetime = new Velocity();
        public bool isPaused;
        public void Pause() => isPaused = true;
        public void Play() => isPaused = false;
    }
    public class AudioClip {}
    public class AudioSource { public void PlayOneShot(AudioClip sound, float gain) {} }
    public static class Time
    {
        public static int frameCount;
        public static float deltaTime;
    }
}

namespace UnityEngine.InputSystem
{
    public sealed class Keyboard
    {
        public static Keyboard current => null;
        public Keyboard escapeKey => this;
        public bool wasPressedThisFrame => false;
    }
}

namespace ZombieSurvivalRun
{
    sealed class ZSRHero : MonoBehaviour
    {
        public int Health = 200;
        public float PreviousX => PositionX;
        public float PositionX => transform.position.x;
        public Vector3 ItemOrigin => transform.position;
        public void Hit() {}
    }
    sealed class ZSRProductionArt
    {
        public Vector3 MouthPosition => ZSRGame.Instance.First.AimPoint;
        public int Attacks;
        public void Attack() => Attacks++;
        public void Tick(bool running, float cadence) {}
        public void SetZombieMovementStyle(ZombieType kind) {}
        public void PlayRun(float phase) {}
        public void Die() {}
    }
    sealed class ZSRSkinnedArt
    {
        public readonly Transform transform = new Transform();
        public void PlayRun() {}
        public void SetRunningSpeed(float speed) {}
        public void Die() {}
    }
    public sealed partial class ZSRZombie
    {
        public void Construct(ZombieType type)
        {
            Kind = type;
            body = torso = armL = armR = legL = legR = head = new Transform();
            production = new ZSRProductionArt();
            rigged = null;
            phase = 0f;
        }
        public void Step() => Update();
        public int Attacks => production.Attacks;
        public float Windup => attackTimer;
    }
    sealed partial class ZSRCombatEffects
    {
        object lineMaterial = new object();
        readonly ParticleSystem particles = new ParticleSystem();
        public int Allocated { get; private set; }
        public int Active => spits.Count(slot => slot.Line != null && slot.Line.enabled);
        public Vector3 Head => spits[0].Head;
        public Vector3 Tail => spits[0].Line.Points[0];
        public float Age => spits[0].Age;
        public Vector3[] Footprint => spits[0].Mark.Points;
        public bool MarkVisible => spits[0].Mark.enabled;
        public bool Paused => particles.isPaused;
        public void HideSource() => lineMaterial = null;
        LineRenderer MakeLine(string name, int count)
        {
            if (name == "Toxic spit") Allocated++;
            return new LineRenderer(count);
        }
        public void ToxicBurst(Vector3 at) {}
        public void PlayerHit(Vector3 at, float sourceX, bool acid) {}
        public void ShieldImpact(Vector3 at) {}
    }
    sealed partial class ZSRGame
    {
        public static ZSRGame Instance { get; private set; }
        public ZSRState State { get; private set; } = ZSRState.Playing;
        public readonly ZSRHero Hero = new ZSRHero();
        public readonly ZSRCombatEffects combatEffects = new ZSRCombatEffects();
        public readonly ZSRRunPace pace = new ZSRRunPace();
        readonly List<ZSRZombie> enemies = new List<ZSRZombie>();
        readonly Dictionary<ZombieType, Queue<ZSRZombie>> pools = new Dictionary<ZombieType, Queue<ZSRZombie>>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly GameObject worldRoot = new GameObject();
        readonly ParticleSystem chapterAir = new ParticleSystem();
        public readonly ZSRLevelPlan Plan;
        ZSRZombie boss;
        bool bossSpawned;
        public float DamageFlash, DamageSourceX;
        public ZSRDamageKind LastDamageKind;
        readonly AudioSource audioSource = new();
        readonly AudioClip biteAudio = new(), clawAudio = new(), acidAudio = new(), hitAudio = new(), spitAudio = new();
        float hitRecovery, shake, shieldTimer = 0f;
        public ZSRGame(int level)
        {
            Plan = new ZSRLevelPlan(level);
            Instance = this;
        }
        public float Elapsed { get; private set; }
        int ItemLevel(ItemType item) => 1;
        float ArmorReduction(int level) => 0f;
        static void Destroy(GameObject obj) => obj.SetActive(false);
        public int Retained => pools.Values.Sum(queue => queue.Count);
        static bool ExpiredRunObject(GameObject obj) => !obj.active;
        void CancelGesture() {}
        void HandleInput(float dt) {}
        void UpdateGrenades(float dt) {}
        public void WarnBossStrike(float x, float z, float width, float phase) {}
        public void BossStrike(float x, float z) {}
        public void Notify(string message, float seconds) {}
        public void Spawn(ZombieType kind, float z)
        {
            SpawnEnemy(kind, 0f, z);
            if (kind == ZombieType.Boss) bossSpawned = boss != null;
        }
        public float ImpactShake => shake;
        public int Live => enemies.Count;
        public ZSRZombie First => enemies[0];
        public ZSRZombie Boss => boss;
        public void BeginFrame(float dt)
        {
            Time.deltaTime = dt;
            Time.frameCount++;
            Update();
        }
        public void EndFrame() => LateUpdate();
    }
}

static class ToxicSpitProbe
{
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    static (float windup, float flight) Attack(int hz, int level, bool lateralDodge, bool paceDodge)
    {
        var game = new ZSRGame(level);
        game.Spawn(ZombieType.Toxic, 28f);
        var actor = game.First;
        var fx = game.combatEffects;
        var dt = 1f / hz;

        void Step()
        {
            game.BeginFrame(dt);
            if (actor.gameObject.active) actor.Step();
            game.EndFrame();
        }

        void Pause()
        {
            var before = (game.Elapsed, actor.transform.position, actor.Windup, fx.Head, fx.Age, fx.Active);
            game.TogglePause();
            Require(game.State == ZSRState.Paused && fx.Paused, "Pause state");
            for (int i = 0; i < hz; i++) Step();
            Require(before == (game.Elapsed, actor.transform.position, actor.Windup, fx.Head, fx.Age, fx.Active),
                "Run clock, pursuit, wind-up and shot must freeze together");
            game.TogglePause();
            Require(game.IsRunning && !fx.Paused, "Resume state");
        }

        for (int i = 0; i < 4 * hz && actor.Attacks == 0; i++) Step();
        Require(actor.Attacks == 1 && fx.Active == 0 && game.Hero.Health == 200, "Wind-up before release");
        float chargeTime = game.Elapsed;
        Pause();
        if (lateralDodge) game.Hero.transform.position = Vector3.right * 3f;
        for (int i = 0; i < hz && fx.Active == 0; i++) Step();
        Require(fx.Active == 1 && fx.Age == 0 && fx.Tail == fx.Head && game.Hero.Health == 200,
            "One shot released, no advancement or damage on launch frame");
        Require(fx.MarkVisible && Math.Abs(fx.Footprint[0].x+.83f)<.0001f &&
            Math.Abs(fx.Footprint[2].x-.83f)<.0001f &&
            Math.Abs(fx.Footprint[0].z-(game.RunSpeed*.48f-.95f))<.0001f,
            "Ground footprint initialized at the damage width and road-relative landing on release");
        float releaseTime = game.Elapsed;
        if (paceDodge) Require(game.pace.TryChange(ZSRPace.Walk), "Walk pace entered");
        for (int i = 0; i < hz && fx.Age < .12f; i++) Step();
        Require(fx.Age >= .12f && fx.Active == 1 && Vector3.Distance(fx.Tail, fx.Head) <= .951f &&
            game.Hero.Health == 200, "Visible arc before any damage");
        Pause();
        for (int i = 0; i < hz && fx.Active > 0; i++) Step();
        Require(fx.Active == 0 && Math.Abs(fx.Head.x) < .0001f && fx.Age > .479f,
            "Completed flight aims at the lane chosen during wind-up");
        Require(!fx.MarkVisible && Math.Abs(fx.Footprint[0].z-(fx.Head.z-.95f))<.0001f,
            "Footprint follows the road and clears on impact");
        Require(paceDodge ? Math.Abs(fx.Head.z) > .95f : Math.Abs(fx.Head.z) < .001f, "Road-relative landing");
        int expected = lateralDodge || paceDodge ? 200 : 200 - Mathf.RoundToInt(8 * game.Plan.IncomingDamage);
        Require(game.Hero.Health == expected, "Exactly one hit on a stationary target, no hit after evasion");
        Require((game.ImpactShake > 0f) == (expected < 200), "Hit feedback only on successful contact");
        float flightTime = game.Elapsed - releaseTime;
        for (int i = 0; i < 2 * hz; i++) Step();
        Require(fx.Active == 0 && actor.Attacks == 1 && !actor.gameObject.active &&
            game.Live == 0 && game.Hero.Health == expected, "Single attack and clean recycle after passing");
        return (releaseTime - chargeTime, flightTime);
    }

    static void CheckPools(int hz)
    {
        var game = new ZSRGame(5);
        for (int i = 0; i < 46; i++) game.Spawn(ZombieType.Walker, 40f);
        Require(game.Live == 43, "Hold one slot for the boss");
        game.Spawn(ZombieType.Boss, 43f);
        Require(game.Live == 44 && game.Boss != null, "Boss occupies the reserved slot");
        var reused = game.First;
        game.RecycleZombie(reused);
        Require(!reused.gameObject.active && game.Live == 43, "Return entity to its kind pool");
        game.Spawn(ZombieType.Walker, 38f);
        Require(game.Live == 44 && reused.gameObject.active, "Boss plus normal encounter cap");
        game = new ZSRGame(4);
        for (int i = 0; i < 46; i++) game.Spawn(ZombieType.Walker, 40f);
        reused = game.First;
        game.RecycleZombie(reused);
        game.Spawn(ZombieType.Walker, 38f);
        Require(game.Live == 44 && reused.gameObject.active && reused.transform.position.z == 38f,
            "Ordinary levels honor cap and reactivate a pooled enemy");

        while (game.Live > 0) game.RecycleZombie(game.First);
        Require(game.Retained == 12, "Bound inactive high-detail actor pool");
        var fx = game.combatEffects;
        var from = new Vector3(0, 1.5f, 11f);
        for (int i = 0; i < 8; i++) fx.LaunchSpit(from, 0f, game.RunSpeed);
        Require(fx.Active == 6 && fx.Allocated == 6, "Projectile pool holds six visible shots");
        for (int i = 0; i < hz; i++) { game.BeginFrame(1f / hz); game.EndFrame(); }
        Require(fx.Active == 0 && game.Hero.Health == 200 - 6 * 8, "Only six projectiles cause damage");
        fx.LaunchSpit(from, 0f, game.RunSpeed);
        Require(fx.Allocated == 6 && fx.Active == 1, "Reuse disabled line with no extra allocation");
        var hidden = new ZSRCombatEffects();
        hidden.HideSource();
        hidden.LaunchSpit(from, 0f, game.RunSpeed);
        Require(hidden.Active == 0 && hidden.Allocated == 0, "Rendering unavailable: no invisible projectile");
    }

    static void Melee(int hz)
    {
        foreach (var type in new[] { ZombieType.Walker, ZombieType.Runner, ZombieType.Brute })
        foreach (float offset in new[] { 0f, 2f })
        {
            var game = new ZSRGame(4);
            game.Hero.transform.position = Vector3.right * offset;
            game.Spawn(type, 3.7f);
            var enemy = game.First;
            for (int step = 0; step < hz*2; step++)
            {
                game.BeginFrame(1f/hz);
                if (enemy.gameObject.active) enemy.Step();
                game.EndFrame();
            }
            int contactDamage = type == ZombieType.Brute ? 25 : type == ZombieType.Runner ? 12 : 8;
            Require(game.Hero.Health == (offset == 0 ? 200 - contactDamage : 200),
                "Swept melee contact in the occupied lane only");
            if (offset == 0)
                Require(game.LastDamageKind == (type == ZombieType.Walker ? ZSRDamageKind.Bite : ZSRDamageKind.Claw),
                    "Visible damage cause");
        }
    }

    static void Main()
    {
        foreach (int hz in new[] { 20, 30, 60, 120 })
        {
            var first = Attack(hz, 5, false, false);
            Attack(hz, 5, true, false);
            var late = Attack(hz, 1000, false, false);
            Attack(hz, 1000, true, false);
            Attack(hz, 1000, false, true);
            CheckPools(hz);
            Melee(hz);
            Console.WriteLine($"MELEE PASS {hz} Hz: three enemy types, one physical contact per attack, no adjacent-lane damage, distinct bite/claw cause.");
            float dt = 1f / hz;
            Require(new[] { first.windup, late.windup }.All(t => t >= .42f - .0001f && t <= .42f + dt + .0001f),
                "Bounded wind-up duration");
            Require(new[] { first.flight, late.flight }.All(t => t >= .48f - .0001f && t <= .48f + dt + .0001f),
                "Bounded flight duration");
            Console.WriteLine($"TOXIC PASS {hz} Hz: wind-up {first.windup:F3}s, flight {first.flight:F3}s, 5 scenarios, pause, 6 shot / 44 enemy caps.");
        }
    }
}
