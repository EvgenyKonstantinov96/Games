using UnityEngine;

namespace ZombieSurvivalRun
{
    internal enum ZSRPace { Run, Surge, Walk }

    internal sealed class ZSRRunPace
    {
        internal const float SurgeScale = 1.24f;
        internal const float WalkScale = .47f;
        internal const float SurgeSeconds = .90f;
        internal const float WalkSeconds = 1.55f;
        internal const float RechargeSeconds = 4.20f;

        internal ZSRPace Mode { get; private set; }
        internal float Scale { get; private set; } = 1f;
        internal float Recharge { get; private set; }
        float remaining;

        internal bool TryChange(ZSRPace next)
        {
            if (next == ZSRPace.Run || Recharge > 0f) return false;
            Mode = next;
            remaining = next == ZSRPace.Surge ? SurgeSeconds : WalkSeconds;
            Recharge = RechargeSeconds;
            return true;
        }

        internal void Advance(float step)
        {
            Recharge = Mathf.Max(0f, Recharge - step);
            float active = Mathf.Min(remaining, step);
            float target = Mode == ZSRPace.Surge ? SurgeScale : Mode == ZSRPace.Walk ? WalkScale : 1f;
            Scale = Mathf.MoveTowards(Scale, target, active * 4.7f);
            remaining -= active;
            if (remaining <= 0f)
            {
                Mode = ZSRPace.Run;
                Scale = Mathf.MoveTowards(Scale, 1f, (step - active) * 3.9f);
            }
        }

        internal void Reset()
        {
            Mode = ZSRPace.Run;
            Scale = 1f;
            Recharge = remaining = 0f;
        }
    }

    /// <summary>Classifies each press once. Deltas are expressed as fractions of screen width.</summary>
    internal sealed class ZSRStrideGesture
    {
        enum Phase { Released, Pending, Drag, Used }
        Phase phase;
        Vector2 origin, previous;

        internal (float lateral, ZSRPace request) Read(bool held, bool beganInPlayfield, Vector2 position)
        {
            if (!held) { Reset(); return default; }
            if (phase == Phase.Released)
            {
                phase = beganInPlayfield ? Phase.Pending : Phase.Used;
                origin = previous = position;
                return default;
            }
            if (phase == Phase.Used) return default;
            Vector2 offset = position - origin;
            if (phase == Phase.Drag)
            {
                float delta = position.x - previous.x;
                previous = position;
                return (delta, ZSRPace.Run);
            }
            if (Mathf.Abs(offset.y) >= .105f && Mathf.Abs(offset.y) > Mathf.Abs(offset.x) * 1.23f)
            {
                phase = Phase.Used;
                return (0f, offset.y > 0f ? ZSRPace.Surge : ZSRPace.Walk);
            }
            if (Mathf.Abs(offset.x) >= .016f && Mathf.Abs(offset.x) >= Mathf.Abs(offset.y) * .98f)
            {
                phase = Phase.Drag;
                previous = position;
                return (offset.x, ZSRPace.Run);
            }
            return default;
        }

        internal void Reset() => phase = Phase.Released;
    }
}
