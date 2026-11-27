using UnityEngine;

namespace ZombieSurvivalRun
{
    sealed class ZSRGrenadeFlight
    {
        const float Windup = .22f;
        const float GroundHeight = .12f;
        const float TrailSeconds = .13f;
        readonly GameObject model;
        readonly LineRenderer trail;
        float windupLeft = Windup;
        float age, duration, height, roadTravel;
        Vector3 launch, landingOnRoad;

        public Vector3 Position => model.transform.position;
        public int ItemLevel { get; }
        public bool WillRelease(float step) => windupLeft > 0f && step >= windupLeft;

        public ZSRGrenadeFlight(Transform parent, ZSRCombatEffects effects, Vector3 hand, int itemLevel)
        {
            ItemLevel = itemLevel;
            model = ZSRVisuals.MeshPart("combat_grenade", "Live grenade", parent, Vector3.zero, Vector3.one, ZSRTrackArt.Surface);
            model.transform.position = hand;
            ZSRVisuals.Sphere("Fuse indicator", model.transform, new Vector3(0, .106f, 0),
                new Vector3(.035f, .027f, .035f), ZSRVisuals.OrangeGlow);
            trail = effects.GrenadeTrail(model.transform);
        }

        public bool Tick(float step, float runSpeed, Vector3 hand, ZSRZombie aimAtRelease)
        {
            if (windupLeft > 0f)
            {
                float held = Mathf.Min(windupLeft, step);
                windupLeft -= held;
                step -= held;
                model.transform.position = hand;
                if (windupLeft > 0f) return false;

                launch = hand;
                var target = aimAtRelease != null ? aimAtRelease.transform.position : hand + Vector3.forward * 22f;
                target.y = GroundHeight;
                float distance = Vector3.Distance(launch, target);
                duration = Mathf.Clamp(.44f + distance * .008f, .52f, .81f);
                height = Mathf.Clamp(.85f + distance * .042f, 1.2f, 2.8f);
                // Freeze the aim at release. Store it in road coordinates so a subsequent
                // change of runner pace moves the whole flight with the scrolling scene.
                var velocity = aimAtRelease != null ? aimAtRelease.TravelVelocity : Vector3.back * runSpeed;
                landingOnRoad = target + velocity * duration;
                landingOnRoad.z = Mathf.Max(.75f, landingOnRoad.z) + runSpeed * duration;
                landingOnRoad.y = GroundHeight;
            }

            float flying = Mathf.Min(step, duration - age);
            age += flying;
            roadTravel += runSpeed * flying;
            model.transform.position = ArcPoint(age);
            float turn = age / duration;
            model.transform.rotation = Quaternion.Euler(610f * turn, 105f * turn, 40f * turn);
            if (trail != null && age > 0f)
            {
                trail.enabled = true;
                float tailAge = Mathf.Max(0f, age - TrailSeconds);
                for (int i = 0; i < trail.positionCount; i++)
                    trail.SetPosition(i, ArcPoint(Mathf.Lerp(tailAge, age, i / (trail.positionCount - 1f))));
            }
            return age >= duration;
        }

        Vector3 ArcPoint(float time)
        {
            float t = time / duration;
            var p = Vector3.Lerp(launch, landingOnRoad, t);
            p.y += 4f * height * t * (1f - t);
            p.z -= roadTravel;
            return p;
        }

        public void Discard()
        {
            model.SetActive(false);
            Object.Destroy(model);
        }
    }
}
