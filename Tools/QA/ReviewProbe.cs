// Unused scene fields are required by the extracted production methods.
#pragma warning disable 0649, 0414
using System;
using N = System.Numerics;

namespace UnityEngine
{
    public struct Quaternion
    {
        public N.Quaternion value;
        public static Quaternion Identity => new() { value = N.Quaternion.Identity };
        public static Quaternion AngleAxis(float degrees, N.Vector3 axis) =>
            new() { value = N.Quaternion.CreateFromAxisAngle(axis, degrees * MathF.PI / 180f) };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new() { value = a.value * b.value };
        public static float Angle(Quaternion a, Quaternion b) =>
            2f * MathF.Acos(Math.Clamp(MathF.Abs(N.Quaternion.Dot(a.value, b.value)), 0f, 1f)) * 180f / MathF.PI;
    }
    public class Transform
    {
        public Transform parent;
        public Quaternion localRotation = Quaternion.Identity;
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : new Quaternion
            { value = N.Quaternion.Inverse(parent.rotation.value) * value.value };
        }
        public N.Vector3 up => N.Vector3.UnitY;
        public N.Vector3 forward => N.Vector3.UnitZ;
    }
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Abs(float a) => Math.Abs(a);
    }
}

namespace PoseCheck
{
    using UnityEngine;
    partial class Actor
    {
        const float Fps = 30f;
        readonly Transform spine = new(), head, transform = new();
        Quaternion sampledSpine, sampledHead;
        bool sampledUpperBody, walking;
        float steeringRoll, steeringHeading, aimTime = .19f, gaitPhase, age, blend, blendDuration = .1f;
        float walkBlend, legWeight;
        Motion motion = Motion.Move;
        Motion Locomotion => Motion.Move;
        bool hasWalkClips = true;
        static bool IsGait(Motion value) => value == Motion.Move;
        readonly Graph graph;
        readonly Mixer mixer = new(), lowerBody = new(), heroLayers = new();
        readonly Clip currentPose = new(), previousPose = new(), legRun = new(), legWalk = new();
        object legMask = null;
        readonly Quaternion restHead = Quaternion.AngleAxis(7f, N.Vector3.UnitX);
        class Clip { public void SetTime(double t) {} }
        class Mixer { public void SetInputWeight(int index, float weight) {} }
        class Graph
        {
            public Action evaluate;
            public void Evaluate(float t) => evaluate();
        }
        public Actor(bool keyedHead)
        {
            head = new Transform { parent = spine, localRotation = restHead };
            graph = new Graph { evaluate = () =>
            {
                spine.localRotation = Quaternion.AngleAxis(MathF.Sin(gaitPhase)*3f, N.Vector3.UnitZ);
                if (keyedHead) head.localRotation = restHead;
            }};
        }
        public void Check()
        {
            for (int frame = 0; frame < 12000; frame++)
            {
                gaitPhase = frame / 40f;
                steeringRoll = 10f * MathF.Sin(frame * .019f);
                steeringHeading = 18f * MathF.Cos(frame * .023f);
                Sample();
                Assert(Quaternion.Angle(head.localRotation, restHead) < .08f, "head must recover sampled pose");
                Steer();
                Assert(Quaternion.Angle(head.localRotation, restHead) < 4.1f, "bounded neck compensation");
            }
            steeringRoll = steeringHeading = 0f;
            Sample();
            Steer();
            Assert(Quaternion.Angle(head.localRotation, restHead) < .08f, "head returns to neutral");
        }
        static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    }
}

namespace ZombieSurvivalRun
{
    class ZSRLevelPlan { public const int Count = 1000; }
    enum ItemType { Grenade, Medkit, Drone }
    enum WeaponType { Rifle }
    class ZSRWeaponProfile
    {
        public int Magazine = 30;
        public static ZSRWeaponProfile For(WeaponType gun) => new();
    }
    class Node { public void SetActive(bool active) {} }
    class HeroModel
    {
        public readonly Node gameObject = new();
        public void ResetHero(int selected, WeaponType weapon) {}
    }
    class Pace { public void Reset() {} }
    class Air { public void Pause() {} public void Play() {} }
    partial class ZSRGame
    {
        public int Level = 1, UnlockedLevel = 1000, ClearedLevel;
        public int SelectedHero, Earned, Payout, Eliminated, Grenades, Medkits, Drones, Magazine, Ammo;
        int waveIndex, coinIndex, gateIndex, obstacleIndex;
        float shootTimer, reloadTimer, waveTimer, coinTimer, gateTimer, carTimer, bonusTimer;
        float shieldTimer, hitRecovery, DamageFlash, magnetTimer, droneTimer, shake;
        int damageMultiplier;
        bool reloading, bossSpawned;
        object boss;
        readonly Pace pace = new();
        readonly Air chapterAir = new();
        readonly Node worldRoot = new();
        public readonly HeroModel Hero = new();
        public WeaponType Weapon;
        public bool Replaying, HasStoryBeat;
        public ZSRStory.Beat StoryBeat, Briefing;
        public int DialogueIndex;
        public ZSRState State = ZSRState.Menu;
        public float Elapsed;
        public string LevelTitle => "Level";
        public bool IsRunning => State == ZSRState.Playing;
        public int Cancellations;
        void ClearRun() {}
        void RebuildRoad() {}
        void CancelGesture() => Cancellations++;
        void FrameRunCamera(float x) {}
        void Notify(string text, float duration) {}
        int ItemStartCount(ItemType item) => 1;
        void SelectLevel(int requested) => Level = requested;

        public static void Check()
        {
            for (int level = 1; level <= 1000; level++)
            {
                var game = new ZSRGame();
                game.StartLevel(level);
                bool hasDialogue = ZSRStory.TryBriefing(level, out _);
                if (hasDialogue)
                {
                    Assert(!game.IsRunning && game.Elapsed == 0 && game.DialogueIndex == 0, "opening is stopped");
                    // Reading time must never advance a speaker or open the run.
                    for (int frame = 0; frame < 12000; frame++)
                        Assert(game.State == ZSRState.Briefing && game.DialogueIndex == 0, "manual dialogue");
                    game.AdvanceDialogue();
                    Assert(!game.IsRunning && game.DialogueIndex == 1 && game.Elapsed == 0, "second speaker");
                    game.AdvanceDialogue();
                    Assert(game.IsRunning && game.Elapsed == 0 && game.Cancellations == 2, "release input on start");
                    game.AdvanceDialogue();
                    Assert(game.IsRunning && game.Cancellations == 2, "button cannot restart run");
                    game.StartLevel(level);
                    game.GoMenu();
                    game.AdvanceDialogue();
                    Assert(game.State == ZSRState.Menu, "cancelled briefing stays in menu");
                }
                else Assert(game.IsRunning, "ordinary stage starts");
            }
        }
        static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    }
}

class ReviewProbe
{
    static void Main()
    {
        new PoseCheck.Actor(false).Check();
        new PoseCheck.Actor(true).Check();
        ZombieSurvivalRun.ZSRGame.Check();
        Console.WriteLine("POSE PASS: 24000 frames, keyed and omitted head tracks, bounded steering and neutral recovery.");
        Console.WriteLine("BRIEFING PASS: 1000 level entries, manual speakers, blocked run clock, cancellation and clean final start. Managed scene stand-ins.");
    }
}
