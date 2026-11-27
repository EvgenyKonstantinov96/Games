using UnityEngine;

namespace ZombieSurvivalRun
{
    // All velocities here are relative to the pavement. The runner scrolls
    // the road independently, including under an infected that has fallen.
    internal struct ZSRInfectedMotion
    {
        const float Drag = 7f;
        const float PoseResponse = 21f;
        ZombieType kind;
        float acceleration, lateralLimit, impulseScale, turnLimit;
        float sideSpeed, pitch, roll, slideZ, hitRecovery;
        Vector2 impulse;

        internal float Yaw { get; private set; }
        internal float Pitch => pitch;
        internal float Roll => roll;
        internal float SideSpeed => sideSpeed;

        internal void Reset(ZombieType type)
        {
            this = default;
            kind = type;
            (acceleration, lateralLimit, impulseScale, turnLimit) = type switch
            {
                ZombieType.Runner => (12f, 1.90f, 1.08f, 32f),
                ZombieType.Crawler => (9f, .84f, .78f, 24f),
                ZombieType.Brute => (3.7f, .84f, .42f, 17f),
                ZombieType.Armored => (5f, .84f, .57f, 19f),
                ZombieType.Boss => (2.8f, .38f, .21f, 13f),
                ZombieType.Toxic => (6f, .84f, .88f, 22f),
                _ => (7f, .84f, 1f, 25f)
            };
        }

        internal bool Hit(Vector2 away, int damage, float health)
        {
            if (away.sqrMagnitude < .0001f) away = Vector2.up;
            float strength = (.85f + 2.2f * Mathf.Clamp01(damage / Mathf.Max(1f, health))) * impulseScale;
            impulse = Vector2.ClampMagnitude(impulse + away.normalized * strength, 3.2f * impulseScale);
            // Fast weapons still transfer impulse every time. Allow the current
            // authored flinch to play through instead of restarting at frame one.
            if (hitRecovery > 0f) return false;
            hitRecovery = .30f;
            return true;
        }

        internal Vector2 Pursue(float dx, float strideSpeed, float roadSpeed, float dt)
        {
            if (dt <= 0f) return Vector2.zero;
            float target = Mathf.Clamp(dx, -1f, 1f) * lateralLimit;
            float previous = sideSpeed;
            float retained = Mathf.Exp(-acceleration * dt);
            sideSpeed = target + (previous - target) * retained;
            float sideTravel = target * dt + (previous - target) * (1f - retained) / acceleration;
            Yaw = Mathf.Clamp(Mathf.Atan2(sideSpeed, Mathf.Max(.6f, strideSpeed)) * Mathf.Rad2Deg,
                -turnLimit, turnLimit);
            var push = AdvanceImpulse(dt);
            if (kind == ZombieType.Boss) return new Vector2(sideTravel, -2.5f * dt);
            return new Vector2(sideTravel + push.x, push.y - (roadSpeed + strideSpeed) * dt);
        }

        internal void Fall(float strideSpeed)
        {
            slideZ = -strideSpeed;
        }

        internal Vector2 Slide(float roadSpeed, float dt)
        {
            if (dt <= 0f) return Vector2.zero;
            const float friction = 6f;
            float retained = Mathf.Exp(-friction * dt);
            float travel = (1f - retained) / friction;
            var motion = new Vector2(sideSpeed * travel, slideZ * travel - roadSpeed * dt);
            sideSpeed *= retained;
            slideZ *= retained;
            return motion + AdvanceImpulse(dt);
        }

        Vector2 AdvanceImpulse(float dt)
        {
            float retained = Mathf.Exp(-Drag * dt);
            float poseDecay = Mathf.Exp(-PoseResponse * dt);
            float transfer = PoseResponse / (PoseResponse - Drag) * (retained - poseDecay);
            pitch = pitch * poseDecay - impulse.y * 5f * transfer;
            roll = roll * poseDecay + impulse.x * 6f * transfer;
            Vector2 travel = impulse * ((1f - retained) / Drag);
            impulse *= retained;
            hitRecovery = Mathf.Max(0f, hitRecovery - dt);
            return travel;
        }
    }
}
