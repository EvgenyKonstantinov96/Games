using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Fallback when imported GLB has no animation clip/controller.
    /// It does not replace motion-captured locomotion.
    /// Rotates real skeleton bones rather than mesh GameObjects.</summary>
    public sealed class ZSRPolyforkBoneMotion : MonoBehaviour
    {
        public bool Running = true;
        public float Speed = 1f;
        public bool IsHero;
        Transform lUpper, rUpper, lLower, rLower, lArm, rArm, head;
        Quaternion lUpper0, rUpper0, lLower0, rLower0, lArm0, rArm0, head0;
        float phase, hurt;

        static string Clean(string name)
        {
            return name.Replace(":", "").Replace("_", "").Replace("mixamorig", "").ToLowerInvariant();
        }
        static Transform FindBone(Transform root, string suffix)
        {
            string want = suffix.ToLowerInvariant();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (Clean(t.name).EndsWith(want)) return t;
            return null;
        }
        public void Init(bool hero)
        {
            IsHero = hero;
            lUpper = FindBone(transform, "leftupleg");
            rUpper = FindBone(transform, "rightupleg");
            lLower = FindBone(transform, "leftleg");
            rLower = FindBone(transform, "rightleg");
            lArm = FindBone(transform, "leftarm");
            rArm = FindBone(transform, "rightarm");
            head = FindBone(transform, "head");
            if(lUpper != null) lUpper0 = lUpper.localRotation;
            if(rUpper != null) rUpper0 = rUpper.localRotation;
            if(lLower != null) lLower0 = lLower.localRotation;
            if(rLower != null) rLower0 = rLower.localRotation;
            if(lArm != null) lArm0 = lArm.localRotation;
            if(rArm != null) rArm0 = rArm.localRotation;
            if(head != null) head0 = head.localRotation;
        }
        public void ReactHit() { hurt = .28f; }
        void LateUpdate()
        {
            phase += Time.deltaTime * (Running ? 10.3f : 1.6f) * Speed;
            float swing = Running ? Mathf.Sin(phase) : 0f;
            float bend = Running ? Mathf.Max(0f, swing) : 0f;
            if(lUpper != null) lUpper.localRotation = lUpper0 * Quaternion.Euler(swing * 29f, 0, 0);
            if(rUpper != null) rUpper.localRotation = rUpper0 * Quaternion.Euler(-swing * 29f, 0, 0);
            if(lLower != null) lLower.localRotation = lLower0 * Quaternion.Euler(bend * 30f, 0, 0);
            if(rLower != null) rLower.localRotation = rLower0 * Quaternion.Euler(Mathf.Max(0f, -swing) * 30f, 0, 0);
            if(lArm != null) lArm.localRotation = lArm0 * Quaternion.Euler(-swing * (IsHero ? 13f : 24f), 0, 0);
            if(rArm != null) rArm.localRotation = rArm0 * Quaternion.Euler(swing * (IsHero ? 13f : 24f), 0, 0);
            if(head != null) head.localRotation = head0 * Quaternion.Euler(hurt * 18f, Mathf.Sin(phase*.5f)*3f, 0);
            hurt = Mathf.MoveTowards(hurt, 0, Time.deltaTime * 2.8f);
        }
    }
}
