#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>Creates actual Unity Animator Controllers and animation assets for the rigged 0.8 figures.</summary>
    [InitializeOnLoad]
    public static class ZSRRigClipGenerator
    {
        const string Folder = "Assets/Resources/Animations";
        static bool queued;
        static ZSRRigClipGenerator() { Queue(); }
        static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += Ensure;
        }
        [MenuItem("Zombie Survival Run 0.8/0 - Regenerate Skeleton Animations")]
        public static void Regenerate()
        {
            if (Directory.Exists(Folder))
            {
                foreach (string name in new[] { "ZSRHero", "ZSRZombie" })
                {
                    string controller = Folder + "/" + name + ".controller";
                    if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controller) != null)
                        AssetDatabase.DeleteAsset(controller);
                    foreach (string clip in new[] { "Run", "Idle", "Hit", "Death" })
                    {
                        string p = Folder + "/" + name + "_" + clip + ".anim";
                        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(p) != null)
                            AssetDatabase.DeleteAsset(p);
                    }
                }
            }
            Ensure();
        }
        public static void Ensure()
        {
            queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Queue(); return;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                BuildController("ZSRHero", true);
                BuildController("ZSRZombie", false);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZSR 0.8] Animation setup postponed: " + e.Message);
            }
        }

        static void BuildController(string prefix, bool hero)
        {
            string controllerPath = Folder + "/" + prefix + ".controller";
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null) return;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null) throw new Exception("Unable to create AnimatorController " + controllerPath);
            var machine = controller.layers[0].stateMachine;
            AnimatorState run = null;
            foreach (string kind in new[] { "Run", "Idle", "Hit", "Death" })
            {
                string clipPath = Folder + "/" + prefix + "_" + kind + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = GenerateClip(kind, hero);
                    clip.name = prefix + "_" + kind;
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                var state = machine.AddState(kind);
                state.motion = clip;
                if (kind == "Run") run = state;
            }
            if (run != null) machine.defaultState = run;
            EditorUtility.SetDirty(controller);
        }

        const string Hips = "Rig/Hips";
        const string Spine = Hips + "/Spine";
        const string Chest = Spine + "/Chest";
        const string Head = Chest + "/Neck/Head";
        const string LeftArm = Chest + "/LeftUpperArm";
        const string LeftForearm = LeftArm + "/LeftLowerArm";
        const string RightArm = Chest + "/RightUpperArm";
        const string RightForearm = RightArm + "/RightLowerArm";
        const string LeftLeg = Hips + "/LeftUpperLeg";
        const string LeftShin = LeftLeg + "/LeftLowerLeg";
        const string RightLeg = Hips + "/RightUpperLeg";
        const string RightShin = RightLeg + "/RightLowerLeg";

        static AnimationClip GenerateClip(string kind, bool hero)
        {
            bool loop = kind == "Run" || kind == "Idle";
            float length = kind == "Run" ? (hero ? .84f : 1.11f) : kind == "Idle" ? 2.4f : kind == "Hit" ? .48f : .80f;
            int steps = kind == "Run" ? 20 : kind == "Idle" ? 18 : 15;
            var clip = new AnimationClip { frameRate = 30 };
            for (int joint = 0; joint < 12; joint++)
            {
                string path = JointPath(joint);
                if (path == null) continue;
                var rx = new Keyframe[steps + 1];
                var ry = new Keyframe[steps + 1];
                var rz = new Keyframe[steps + 1];
                var rw = new Keyframe[steps + 1];
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    var angles = Pose(kind, hero, joint, t);
                    Quaternion q = Quaternion.Euler(angles);
                    float time = length * t;
                    rx[s] = new Keyframe(time, q.x);
                    ry[s] = new Keyframe(time, q.y);
                    rz[s] = new Keyframe(time, q.z);
                    rw[s] = new Keyframe(time, q.w);
                }
                clip.SetCurve(path, typeof(Transform), "localRotation.x", Smooth(rx));
                clip.SetCurve(path, typeof(Transform), "localRotation.y", Smooth(ry));
                clip.SetCurve(path, typeof(Transform), "localRotation.z", Smooth(rz));
                clip.SetCurve(path, typeof(Transform), "localRotation.w", Smooth(rw));
            }
            var hipY = new Keyframe[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float y = 1.03f;
                if (kind == "Run") y += Mathf.Sin(t * 4 * Mathf.PI) * (hero ? .038f : .032f);
                if (kind == "Idle") y += Mathf.Sin(t * 2 * Mathf.PI) * .012f;
                if (kind == "Hit") y += .12f * Mathf.Sin(t * Mathf.PI);
                if (kind == "Death") y = Mathf.Lerp(1.03f, .40f, Mathf.SmoothStep(0, 1, t));
                hipY[s] = new Keyframe(length * t, y);
            }
            clip.SetCurve(Hips, typeof(Transform), "localPosition.y", Smooth(hipY));
            AnimationClipSettings s0 = AnimationUtility.GetAnimationClipSettings(clip);
            s0.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s0);
            return clip;
        }

        static AnimationCurve Smooth(Keyframe[] k)
        {
            for (int i = 0; i < k.Length; i++)
            {
                int lo = Mathf.Max(0, i - 1);
                int hi = Mathf.Min(k.Length - 1, i + 1);
                float slope = (k[hi].value - k[lo].value) /
                              Mathf.Max(.00001f, k[hi].time - k[lo].time);
                var v = k[i];
                v.inTangent = slope;
                v.outTangent = slope;
                k[i] = v;
            }
            return new AnimationCurve(k);
        }

        static string JointPath(int joint)
        {
            switch (joint)
            {
                case 0: return Hips;
                case 1: return Spine;
                case 2: return Chest;
                case 3: return Head;
                case 4: return LeftArm;
                case 5: return LeftForearm;
                case 6: return RightArm;
                case 7: return RightForearm;
                case 8: return LeftLeg;
                case 9: return LeftShin;
                case 10: return RightLeg;
                case 11: return RightShin;
                default: return null;
            }
        }

        static Vector3 Pose(string kind, bool hero, int bone, float t)
        {
            float gait = Mathf.Sin(t * Mathf.PI * 2);
            float bounce = Mathf.Sin(t * 4 * Mathf.PI);
            if (kind == "Death")
            {
                float w = Mathf.SmoothStep(0, 1, t);
                switch (bone)
                {
                    case 0: return new Vector3(70f * w, 0, -25f * w);
                    case 1: return new Vector3(-15f * w, 0, 7 * w);
                    case 2: return new Vector3(10f * w, 0, 0);
                    case 4: return new Vector3(-22f * w, 0, -65 * w);
                    case 6: return new Vector3(-22f * w, 0, 65 * w);
                    case 8: return new Vector3(-30 * w, 0, 0);
                    case 10: return new Vector3(35 * w, 0, 0);
                    default: return Vector3.zero;
                }
            }
            if (kind == "Hit")
            {
                float shock = Mathf.Sin(t * Mathf.PI);
                if (bone == 1) return new Vector3(-15 * shock, 0, 6 * shock);
                if (bone == 2) return new Vector3(-10 * shock, 0, -7 * shock);
                return Vector3.zero;
            }
            if (kind == "Idle")
            {
                if (bone == 1) return new Vector3(1.5f * gait, 0, 0);
                if (bone == 2) return new Vector3(2f * gait, 2 * gait, 0);
                if (bone == 3) return new Vector3(-gait, 0, 0);
                if (bone == 4) return new Vector3(-22, 0, -10);
                if (bone == 5) return new Vector3(-18, 0, 0);
                if (bone == 6) return new Vector3(-22, 0, 10);
                if (bone == 7) return new Vector3(-18, 0, 0);
                return Vector3.zero;
            }
            float swing = hero ? 36f : 29f;
            if (bone == 0) return new Vector3(3, 1.8f * gait, bounce * 1.5f);
            if (bone == 1) return new Vector3(2 + Mathf.Abs(gait) * 1.4f, 3 * gait, -2f * gait);
            if (bone == 2) return new Vector3(hero ? -2 : 9, 2f * gait, -2.8f * gait);
            if (bone == 3) return new Vector3(-2 + 1.0f * Mathf.Abs(gait), -1.8f * gait, 0);
            if (bone == 4) return new Vector3((hero ? -38f : -56f) - 8f * gait, 0, -13);
            if (bone == 5) return new Vector3(hero ? -22f : -32f, 0, -6);
            if (bone == 6) return new Vector3((hero ? -38f : -56f) + 8f * gait, 0, 13);
            if (bone == 7) return new Vector3(hero ? -22f : -32f, 0, 6);
            if (bone == 8) return new Vector3(swing * gait, 0, 0);
            if (bone == 9) return new Vector3(Mathf.Max(0, -gait) * 48f, 0, 0);
            if (bone == 10) return new Vector3(-swing * gait, 0, 0);
            if (bone == 11) return new Vector3(Mathf.Max(0, gait) * 48f, 0, 0);
            return Vector3.zero;
        }
    }
}
#endif
