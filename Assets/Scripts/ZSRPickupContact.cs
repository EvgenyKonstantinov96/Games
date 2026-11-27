using UnityEngine;

namespace ZombieSurvivalRun
{
    internal static class ZSRPickupContact
    {
        internal static bool InsideVeil(float offset) => Mathf.Abs(offset) <= 1.19f + .17f;

        internal static bool CrossedPlane(float oldDepth, float newDepth, float oldX, float newX, out float offset)
        {
            offset = 0f;
            if (oldDepth <= 0f || newDepth > 0f) return false;
            float fraction = oldDepth / (oldDepth - newDepth);
            offset = oldX + (newX - oldX) * fraction;
            return true;
        }

        // Intersect the moving lane and depth intervals during the same part of the frame.
        internal static bool CoinTime(float x0, float x1, float z0, float z1, float radius, out float time)
        {
            float enter = 0f, leave = 1f;
            bool contact = Clip(x0, x1, -radius, radius, ref enter, ref leave) &&
                           Clip(z0, z1, -1.2f, 2.2f, ref enter, ref leave);
            time = enter;
            return contact;
        }

        static bool Clip(float start, float end, float low, float high, ref float enter, ref float leave)
        {
            float delta = end - start;
            if (Mathf.Abs(delta) < .00001f) return start >= low && start <= high;
            float lo = (low - start) / delta, hi = (high - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(lo, hi));
            leave = Mathf.Min(leave, Mathf.Max(lo, hi));
            return enter <= leave;
        }
    }
}
