using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Two fixed-length arm segments, preserving the sampled elbow's bend plane.</summary>
    internal static class ZSRArmReach
    {
        internal static bool Solve(Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 target,
            out Vector3 joint, out Vector3 end)
        {
            joint = elbow; end = wrist;
            float upper = (elbow-shoulder).magnitude, lower = (wrist-elbow).magnitude;
            Vector3 offset = target-shoulder;
            if (upper < .001f || lower < .001f || offset.sqrMagnitude < .000001f) return false;
            Vector3 direction = offset.normalized;
            float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(upper-lower)+.008f, upper+lower-.005f);
            Vector3 pole = Vector3.ProjectOnPlane(elbow-shoulder,direction);
            if (pole.sqrMagnitude < .000001f)
                pole = Vector3.ProjectOnPlane(Vector3.down,direction);
            if (pole.sqrMagnitude < .000001f)
                pole = Vector3.ProjectOnPlane(Vector3.right,direction);
            float along = (upper*upper + distance*distance - lower*lower)/(2f*distance);
            joint = shoulder + direction*along + pole.normalized*Mathf.Sqrt(Mathf.Max(0f,upper*upper-along*along));
            end = shoulder + direction*distance;
            return true;
        }
    }

    /// <summary>Bounded shoulder aiming shared by the generic survivor rigs.</summary>
    internal sealed class ZSRUpperBodyAim
    {
        internal Vector2 Angles { get; private set; }

        internal void Reset() => Angles = Vector2.zero;

        internal Vector2 Tick(Vector3 relativeTarget, bool aiming, float dt)
        {
            dt = Mathf.Clamp(dt,0f,.05f);
            Vector2 desired = Vector2.zero;
            if (aiming && relativeTarget.sqrMagnitude > .0001f && relativeTarget.z > .01f)
            {
                float horizontal = Mathf.Sqrt(relativeTarget.x*relativeTarget.x+relativeTarget.z*relativeTarget.z);
                desired = new Vector2(Mathf.Clamp(Mathf.Atan2(relativeTarget.x,relativeTarget.z)*Mathf.Rad2Deg,-28f,28f),
                    Mathf.Clamp(-Mathf.Atan2(relativeTarget.y,horizontal)*Mathf.Rad2Deg,-12f,8f));
            }
            Vector2 smooth = Vector2.Lerp(Angles,desired,1f-Mathf.Exp(-dt*14f));
            Angles = Vector2.MoveTowards(Angles,smooth,dt*150f);
            return Angles;
        }

        internal static float RecoilPitch(float recoil) => -Mathf.Clamp(recoil*55f,0f,6f);
    }
}
