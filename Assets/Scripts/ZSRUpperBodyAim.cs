using UnityEngine;

namespace ZombieSurvivalRun
{
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
