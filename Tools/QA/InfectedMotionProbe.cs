using System;
using UnityEngine;
using ZombieSurvivalRun;

// Runs against the game's ZSRInfectedMotion and Unity's managed vector math.
static class InfectedMotionProbe
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    static Vector2 Pursuit(ZombieType type, int hz, float sign)
    {
        var state = new ZSRInfectedMotion();
        state.Reset(type);
        var p = Vector2.zero;
        for (int i = 0; i < 2 * hz; i++)
        {
            float target = i < hz ? 3f * sign : -2f * sign;
            p += state.Pursue(target - p.x, 1.5f, 13f, 1f / hz);
            Check(!float.IsNaN(state.Yaw) && Mathf.Abs(state.Yaw) <= 32.001f, "heading");
        }
        return p;
    }

    static Vector2 ImpulseTravel(ZombieType type, int hz, float direction)
    {
        var state = new ZSRInfectedMotion();
        state.Reset(type);
        Check(state.Hit(new Vector2(direction, 0f), 100, 100f), "first flinch");
        Check(!state.Hit(new Vector2(direction, 0f), 18, 100f), "overlapping flinch");
        Vector2 travel = Vector2.zero;
        for (int i = 0; i < hz; i++)
        {
            travel += state.Pursue(0f, 1.5f, 13f, 1f / hz);
            if (i == hz / 10)
            {
                Check(state.Roll * direction > .2f, "directional hit");
                float roll = state.Roll, yaw = state.Yaw;
                Check(state.Pursue(0f, 1.5f, 13f, 0f) == Vector2.zero &&
                      state.Yaw == yaw && state.Roll == roll, "frozen movement");
                Check(state.Slide(13f, 0f) == Vector2.zero && state.Roll == roll, "frozen slide");
            }
        }
        Check(state.Hit(Vector2.zero, 15, 100f), "flinch release");
        state.Reset(type);
        Check(state.Yaw == 0f && state.SideSpeed == 0f && state.Roll == 0f && state.Pitch == 0f,
            "pooled pose");
        var clean = state.Pursue(0f, 1.5f, 13f, 1f / hz);
        Check(Mathf.Abs(clean.x) < .00001f, "pooled drift");
        Check(state.Hit(Vector2.zero, 10, 100f), "pooled flinch cooldown");
        return travel;
    }

    static Vector2 Corpse(ZombieType type, int hz, float road)
    {
        var state = new ZSRInfectedMotion();
        state.Reset(type);
        for (int i = 0; i < hz; i++) state.Pursue(-3f, 3f, road, 1f / hz);
        state.Hit(new Vector2(.8f, .6f), 70, 95f);
        state.Fall(3f);
        var p = Vector2.zero;
        for (int i = 0; i < hz; i++) p += state.Slide(road, 1f / hz);
        var last = state.Slide(road, 1f / hz) * hz;
        Check(Mathf.Abs(last.x) < .012f && Mathf.Abs(last.y + road) < .012f, "ground lock");
        return p;
    }

    static void Main()
    {
        float worst = 0f;
        foreach (ZombieType type in Enum.GetValues(typeof(ZombieType)))
        {
            var reference = Pursuit(type, 120, 1f);
            foreach (int hz in new[] { 20, 30, 60, 120 })
            {
                var travel = Pursuit(type, hz, 1f);
                var mirror = Pursuit(type, hz, -1f);
                worst = Mathf.Max(worst, (reference - travel).magnitude);
                Check(Mathf.Abs(travel.x + mirror.x) < .0001f, "mirrored steering");
                var right = ImpulseTravel(type, hz, 1f);
                var left = ImpulseTravel(type, hz, -1f);
                if (type == ZombieType.Boss) Check(right.x == 0f, "boss anchoring");
                else Check(right.x > .05f && right.x < .52f, "continuous bounded displacement");
                Check(Mathf.Abs(right.x + left.x) < .0001f, "mirrored impulse");
                Check((right - ImpulseTravel(type, 120, 1f)).magnitude < .001f, "impulse time step");
                var dry = Corpse(type, hz, 0f);
                var moving = Corpse(type, hz, 17f);
                Check((moving - dry + new Vector2(0f, 17f)).magnitude < .001f, "road displacement");
                Check((dry - Corpse(type, 120, 0f)).magnitude < .001f, "fall step invariance");
            }
        }
        float walker = ImpulseTravel(ZombieType.Walker, 60, 1f).x;
        float brute = ImpulseTravel(ZombieType.Brute, 60, 1f).x;
        Check(brute < walker * .6f, "heavy enemies resist impact");
        Check(worst < .004f, "pursuit time step");
        Console.WriteLine($"7 infected x 4 time steps (20/30/60/120 Hz). Max pursuit deviation {worst:F4} m.");
        Console.WriteLine($"Lateral hit travel Walker {walker:F3} m, Brute {brute:F3} m. Boss held its anchor.");
        Console.WriteLine("Verified mirror response, bounded impulses, flinch recovery, reset and ground lock.");
    }
}
