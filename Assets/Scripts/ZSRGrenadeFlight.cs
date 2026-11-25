using UnityEngine;

namespace ZombieSurvivalRun
{
    sealed class ZSRGrenadeFlight
    {
        const float ReleaseTime = .22f;
        readonly GameObject model;
        readonly TrailRenderer trail;
        readonly ZSRZombie target;
        Vector3 start, landing;
        float elapsed;
        bool tracking;
        readonly float duration, height;

        public Vector3 Position => model.transform.position;
        public int ItemLevel { get; }

        public ZSRGrenadeFlight(Transform parent, ZSRCombatEffects effects, Vector3 hand, ZSRZombie target,
            Vector3 destination, int itemLevel)
        {
            this.target = target;
            tracking = target != null;
            ItemLevel = itemLevel;
            start = hand;
            landing = destination;
            float distance = Vector3.Distance(hand, destination);
            duration = Mathf.Clamp(.44f + distance * .008f, .52f, .81f);
            height = Mathf.Clamp(.85f + distance * .042f, 1.2f, 2.8f);
            model = ZSRVisuals.MeshPart("combat_grenade", "Live grenade", parent, Vector3.zero, Vector3.one, ZSRTrackArt.Surface);
            model.transform.position = hand;
            ZSRVisuals.Sphere("Fuse indicator", model.transform, new Vector3(0, .106f, 0),
                new Vector3(.035f, .027f, .035f), ZSRVisuals.OrangeGlow);
            trail = effects.GrenadeTrail(model.transform);
            if (trail != null) trail.emitting = false;
        }

        public bool Tick(float dt, float runSpeed, Vector3 hand)
        {
            elapsed += dt;
            if (tracking && target != null && target.Alive && Mathf.Abs(target.DistanceZ - landing.z) < 4f)
            {
                landing.x = target.X;
                landing.z = Mathf.Max(.75f, target.DistanceZ);
            }
            else
            {
                // A recycled enemy can become a different wave member.
                tracking = false;
                landing.z = Mathf.Max(.75f, landing.z - runSpeed * dt);
            }

            if (elapsed < ReleaseTime)
            {
                start = hand;
                model.transform.position = hand;
                return false;
            }
            if (trail != null && !trail.emitting) trail.emitting = true;
            float t = Mathf.Clamp01((elapsed - ReleaseTime) / duration);
            model.transform.position = Vector3.Lerp(start, landing, t) + Vector3.up * (4f * t * (1f - t) * height);
            model.transform.rotation = Quaternion.Euler(610f * t, 105f * t, 40f);
            return t >= 1f;
        }

        public void Discard()
        {
            model.SetActive(false);
            Object.Destroy(model);
        }
    }
}
