extern alias engine;
global using Vector3 = engine::UnityEngine.Vector3;
global using Mathf = engine::UnityEngine.Mathf;
using System;
using UnityEngine;
using ZombieSurvivalRun;

// Stand-ins record positions only. Vector3 and Mathf come from UnityEngine.CoreModule.
namespace UnityEngine
{
    public class Transform { public Vector3 position; public Quaternion rotation; }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; }
    public class Object { public static void Destroy(GameObject go) {} }
    public class GameObject { public Transform transform = new(); public void SetActive(bool active) {} }
    public class LineRenderer
    {
        public bool enabled;
        public readonly Vector3[] Points = new Vector3[6];
        public int positionCount => Points.Length;
        public void SetPosition(int i, Vector3 p) => Points[i] = p;
    }
}

namespace ZombieSurvivalRun
{
    class ZSRZombie { public Transform transform = new(); public Vector3 TravelVelocity; }
    class ZSRCombatEffects
    {
        public LineRenderer Trail = new();
        public LineRenderer GrenadeTrail(Transform parent) => Trail;
    }
    static class ZSRTrackArt { public static object Surface => null; }
    static class ZSRVisuals
    {
        public static object OrangeGlow => null;
        public static GameObject MeshPart(string key, string label, Transform parent, Vector3 pos, Vector3 scale, object mat) => new();
        public static void Sphere(string label, Transform parent, Vector3 pos, Vector3 scale, object mat) { }
    }
}

class FlightProbe
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

    static void Check(int fps, float speed, bool withTarget, bool changePace)
    {
        var hand = new Vector3(-1f, 1.35f, .3f);
        var target = withTarget ? new ZSRZombie() : null;
        var velocity = withTarget ? new Vector3(1.2f, 0f, -speed - 2f) : Vector3.back * speed;
        var destination = withTarget ? new Vector3(2, .12f, 24) : hand + Vector3.forward * 22f;
        destination.y = .12f;
        if (target != null) { target.transform.position = destination; target.TravelVelocity = velocity; }
        float duration = Mathf.Clamp(.44f + Vector3.Distance(hand, destination) * .008f, .52f, .81f);
        var fx = new ZSRCombatEffects();
        var flight = new ZSRGrenadeFlight(new Transform(), fx, hand, 3);
        bool launched = false, done = false;
        int releases = 0;
        float dt = 1f / fps, elapsed = 0f, scroll = 0f, peak = 0f;
        while (!done && elapsed < 2f)
        {
            float roadSpeed = launched && changePace ? speed * 1.24f : speed;
            bool releasing = flight.WillRelease(dt);
            if (releasing) { launched = true; releases++; }
            else if (launched && target != null) target.transform.position += new Vector3(15, 0, 45);
            float inAir = Mathf.Max(0f, Mathf.Min(elapsed + dt, .22f + duration) - Mathf.Max(elapsed, .22f));
            scroll += roadSpeed * inAir;
            done = flight.Tick(dt, roadSpeed, hand, target);
            elapsed += dt;
            peak = Math.Max(peak, flight.Position.y);
            if (!launched) Require(flight.Position == hand && !fx.Trail.enabled, "wind-up");
            if (fx.Trail.enabled)
            {
                Require(Vector3.Distance(fx.Trail.Points[^1], flight.Position) < .001f, "head detached from trail");
                var prior = flight.Position;
                Require(flight.Tick(0f, roadSpeed, hand, null) == done && flight.Position == prior, "paused flight");
            }
        }
        var expected = destination + velocity * duration;
        expected.z = Math.Max(.75f, expected.z) + speed * duration - scroll;
        Require(done && releases == 1 && peak > hand.y + .5f && flight.ItemLevel == 3, "flight lifecycle");
        Require(Vector3.Distance(expected, flight.Position) < .001f, "impact missed expected point: " + fps);
        flight.Discard();
    }

    static void Main()
    {
        foreach (int fps in new[] { 20, 30, 60, 120 })
        foreach (float speed in new[] { 5.5f, 12f, 22f })
        foreach (bool withTarget in new[] { false, true })
        foreach (bool changePace in new[] { false, true })
            Check(fps, speed, withTarget, changePace);
        Console.WriteLine("FLIGHT PASS: 48 throws at 20/30/60/120 Hz with Unity vector math. Release, fixed aim, moving road, pace change, pause, trail and impact within 1 mm of analytical endpoint.");
    }
}
