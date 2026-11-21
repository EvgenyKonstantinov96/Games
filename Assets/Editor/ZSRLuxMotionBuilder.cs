#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    /// <summary>
    /// Builds actual imported-GLB-bone-targeted keyframed animation clips. These are
    /// deliberately NOT advertised as motion capture. Real Humanoid FBX clips take
    /// priority when a matching valid Avatar has been built and assigned.
    /// </summary>
    public static class ZSRLuxMotionBuilder
    {
        const string SettingsPath = "Assets/Resources/ProductionArtSettings.asset";
        const string Dir = "Assets/Resources/LuxMotion";
        static readonly float[] T = { 0f, .11f, .22f, .33f, .44f, .55f, .66f, .77f, .88f };

        [MenuItem("Zombie Survival Run 0.8/13 - Build LUX Character Animations")]
        public static void Build()
        {
            BuildInternal(true);
        }

        [MenuItem("Zombie Survival Run 0.8/15 - APPLY LUX to Imported Polyfork Models")]
        public static void ApplyLux()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ZSR LUX", "Stop Play mode before rebuilding imported character animations.", "OK");
                return;
            }
            ZSRPolyforkInstaller.BindImportedModels();
            BuildInternal(false);
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(SettingsPath);
            if (settings != null && settings.heroIdle != null && settings.heroRun != null &&
                settings.zombieIdle != null && settings.zombieRun != null)
                ZSRProductionArtSetup.BuildControllers();
            EditorUtility.DisplayDialog("Zombie Survival Run / LUX", "Imported characters relinked and animation controllers rebuilt.\nStart a fresh Play session and check the character visuals.\nFor real motion-capture clips use menu 14; Humanoid retargeting requires a valid Avatar.","OK");
        }

        static void BuildInternal(bool showDialog)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ZSRProductionArtSettings>(SettingsPath);
            if (settings == null) { Debug.LogError("[ZSR LUX] First import Polyfork models using menu item 10."); return; }
            Directory.CreateDirectory(Dir);
            int created = 0;
            created += Generate("Max", settings.maxPrefab, true, out settings.maxMotionController, out settings.maxAvatar);
            created += Generate("Eva", settings.evaPrefab, true, out settings.evaMotionController, out settings.evaAvatar);
            created += Generate("Walker", settings.walkerPrefab, false, out settings.walkerMotionController, out settings.walkerAvatar);
            created += Generate("Runner", settings.runnerPrefab, false, out settings.runnerMotionController, out settings.runnerAvatar);
            created += Generate("Armored", settings.armoredPrefab, false, out settings.armoredMotionController, out settings.armoredAvatar);
            created += Generate("Brute", settings.brutePrefab, false, out settings.bruteMotionController, out settings.bruteAvatar);
            created += Generate("Toxic", settings.toxicPrefab, false, out settings.toxicMotionController, out settings.toxicAvatar);
            created += Generate("Crawler", settings.crawlerPrefab, false, out settings.crawlerMotionController, out settings.crawlerAvatar);
            // Each enemy slot receives its own curves even if several roles share a source GLB.
            // A fast runner must never silently inherit the same locomotion as a heavy brute.
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZSR LUX] Built " + created + " character-specific controller(s). " +
                "These include authored keyframe locomotion, hit and death. For capture-based realism install optional Humanoid FBX clips and use menu 8.");
            if (showDialog)
                EditorUtility.DisplayDialog("Zombie Survival Run / LUX", created + " character-specific animation controller(s) created.\nRun the game again (stop Play first).\nFor true mocap, import CC0 Humanoid FBX animation clips, then build Production Animation Controllers.", "OK");
        }

        static int Generate(string slot, GameObject prefab, bool hero,
            out RuntimeAnimatorController generic, out Avatar avatar)
        {
            generic = null;
            avatar = null;
            if (prefab == null) { Debug.LogWarning("[ZSR LUX] " + slot + " prefab missing."); return 0; }
            GameObject source = null;
            try
            {
                source = (GameObject)UnityEngine.Object.Instantiate(prefab);
                source.name = "PRODUCTION ART / " + prefab.name; // match runtime Animator root name
                source.hideFlags = HideFlags.HideAndDontSave;
                var bone = new BoneSet(source.transform);
                if (!bone.HasLegs)
                {
                    Debug.LogWarning("[ZSR LUX] " + slot + ": leg bones not found. Not creating a fake controller. Check actual GLB rig.");
                    return 0;
                }
                // Avatar creation is best-effort; Generic transform clips below do not rely on it.
                avatar = CreateHumanAvatar(slot, source);
                var clips = new Dictionary<string, AnimationClip>();
                foreach (string state in hero ? new [] { "Idle", "Run", "RunShoot", "Hit", "Death" } : new [] { "Idle", "Run", "Hit", "Death" })
                {
                    bool loop = state == "Idle" || state == "Run" || state == "RunShoot";
                    string path = Dir + "/" + slot + "_" + state + ".anim";
                    var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (old != null) AssetDatabase.DeleteAsset(path);
                    var clip = new AnimationClip { frameRate = 30f, name = slot + "_" + state };
                    clip.legacy = false;
                    WriteClip(clip, bone, state, hero, slot);
                    clip.EnsureQuaternionContinuity();
                    clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                    var serializedClip = new SerializedObject(clip);
                    var looping = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
                    if (looping != null)
                    {
                        looping.boolValue = loop;
                        serializedClip.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AssetDatabase.CreateAsset(clip, path);
                    clips[state] = clip;
                }
                string ctrlPath = Dir + "/" + slot + "_Generic.controller";
                var previous = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                if (previous != null) AssetDatabase.DeleteAsset(ctrlPath);
                var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var child in machine.states) machine.RemoveState(child.state);
                foreach (string state in hero ? new [] { "Run", "RunShoot", "Idle", "Hit", "Death" } : new [] { "Run", "Idle", "Hit", "Death" })
                {
                    var s = machine.AddState(state);
                    s.motion = clips[state];
                    if (state == "Run") machine.defaultState = s;
                }
                EditorUtility.SetDirty(controller);
                generic = controller;
                Debug.Log("[ZSR LUX] " + slot + ": authored transforms animated; Human Avatar " +
                    (avatar != null ? "VALID" : "unavailable — Generic controller remains usable"));
                return 1;
            }
            catch (Exception ex)
            {
                Debug.LogError("[ZSR LUX] " + slot + ": animation generation error: " + ex);
                return 0;
            }
            finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); }
        }

        sealed class BoneSet
        {
            public readonly Transform root, hips, spine, chest, neck, head;
            public readonly Transform leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot;
            public readonly Transform leftArm, rightArm, leftForearm, rightForearm, leftHand, rightHand;
            public bool HasLegs { get { return leftThigh != null && rightThigh != null && leftShin != null && rightShin != null; } }
            public BoneSet(Transform root)
            {
                this.root = root;
                var all = root.GetComponentsInChildren<Transform>(true);
                hips=Find(all, "hips"); spine=Find(all, "spine"); chest=Find(all, "spine2", "chest", "upperchest");
                neck=Find(all, "neck"); head=Find(all, "head");
                leftThigh=Find(all,"leftupleg","leftupperleg");rightThigh=Find(all,"rightupleg","rightupperleg");
                leftShin=Find(all,"leftleg","leftlowerleg");rightShin=Find(all,"rightleg","rightlowerleg");
                leftFoot=Find(all,"leftfoot");rightFoot=Find(all,"rightfoot");
                leftArm=Find(all,"leftarm","leftupperarm");rightArm=Find(all,"rightarm","rightupperarm");
                leftForearm=Find(all,"leftforearm","leftlowerarm");rightForearm=Find(all,"rightforearm","rightlowerarm");
                leftHand=Find(all,"lefthand"); rightHand=Find(all,"righthand");
            }
        }

        static string Clean(string s)
        {
            return s.ToLowerInvariant().Replace("mixamorig", "").Replace(":", "")
                .Replace("_", "").Replace("-", "").Replace(" ", "");
        }
        static Transform Find(Transform[] all, params string[] names)
        {
            foreach (var n in names)
                foreach (var t in all)
                    if (Clean(t.name) == n) return t;
            foreach (var n in names)
                foreach (var t in all)
                    if (Clean(t.name).EndsWith(n, StringComparison.Ordinal)) return t;
            return null;
        }

        static void WriteClip(AnimationClip clip, BoneSet b, string state, bool hero, string slot)
        {
            if (state == "Run" || state == "RunShoot")
            {
                // A different stride profile is authored for every enemy role. Using each
                // model's bind rotations keeps curves compatible with generic GLB rigs.
                float stride = hero ? 33f : slot == "Runner" ? 44f :
                    slot == "Crawler" ? 25f : slot == "Brute" ? 16f :
                    slot == "Armored" ? 21f : slot == "Toxic" ? 26f : 29f;
                float knees = hero ? 47f : slot == "Runner" ? 58f :
                    slot == "Crawler" ? 55f : slot == "Brute" ? 21f : 38f;
                float lean = hero ? 5f : slot == "Runner" ? 18f :
                    slot == "Crawler" ? 38f : slot == "Brute" ? 11f : 7f;
                var lThigh = new float[T.Length]; var rThigh = new float[T.Length];
                var lShin = new float[T.Length]; var rShin = new float[T.Length];
                var lFoot = new float[T.Length]; var rFoot = new float[T.Length];
                var upper = new float[T.Length]; var torsoYaw = new float[T.Length];
                var chestPitch = new float[T.Length]; var torsoRoll = new float[T.Length];
                var hipBounce = new float[T.Length];
                for (int i = 0; i < T.Length; i++)
                {
                    float phase = (float)i / (T.Length - 1) * Mathf.PI * 2f;
                    float l = Mathf.Sin(phase), r = -l;
                    // Support phase / flight phase: lower leg bends more when its foot
                    // is travelling forwards; this is more plausible than symmetric knees.
                    float leftSwing = Mathf.Max(0f,-l);
                    float rightSwing = Mathf.Max(0f,l);
                    lThigh[i] = l * stride; rThigh[i] = r * stride;
                    lShin[i] = 5f + leftSwing * knees + Mathf.Max(0f,Mathf.Cos(phase)) * 3f;
                    rShin[i] = 5f + rightSwing * knees + Mathf.Max(0f,-Mathf.Cos(phase)) * 3f;
                    // Toe roll compensates the thigh pitch during stance.
                    lFoot[i] = -lThigh[i] * .36f - lShin[i] * .18f + leftSwing*5f;
                    rFoot[i] = -rThigh[i] * .36f - rShin[i] * .18f + rightSwing*5f;
                    upper[i] = lean + Mathf.Abs(l) * (hero ? 1.8f : 3.2f);
                    torsoYaw[i] = l * (hero ? 2.8f : slot == "Brute" ? 4f : 6f);
                    torsoRoll[i] = l * (hero ? 2f : 4f);
                    chestPitch[i] = 1.5f + Mathf.Abs(l) * 1.5f;
                    hipBounce[i] = Mathf.Abs(l) * (hero ? .065f : slot == "Brute" ? .027f : .045f);
                }
                Rot(clip,b,b.leftThigh,T,lThigh,0f,0f);
                Rot(clip,b,b.rightThigh,T,rThigh,0f,0f);
                Rot(clip,b,b.leftShin,T,lShin,0f,0f);
                Rot(clip,b,b.rightShin,T,rShin,0f,0f);
                Rot(clip,b,b.leftFoot,T,lFoot,0f,0f);
                Rot(clip,b,b.rightFoot,T,rFoot,0f,0f);
                PoseArms(clip,b,T,hero,false,state == "RunShoot");
                Rot(clip,b,b.spine,T,upper,torsoYaw,torsoRoll);
                Rot(clip,b,b.chest,T,chestPitch,Repeat(0f,T.Length),
                    Repeat(0f,T.Length));
                Pos(clip,b,b.hips,T,hipBounce);
            }
            else if (state == "Idle")
            {
                float[] u = {0,.4f,.8f,1.2f,1.6f};
                Rot(clip,b,b.leftThigh,u, new float[]{ 0,1,0,-1,0 },0,0);
                Rot(clip,b,b.rightThigh,u,new float[]{ 0,-1,0,1,0 },0,0);
                Rot(clip,b,b.spine,u,new float[]{ 2,3,5,3,2 },0,0);
                Rot(clip,b,b.chest,u,new float[]{ 0,1,2,1,0 },0,0);
                Pos(clip,b,b.hips,u,new float[]{0,.005f,.012f,.006f,0});
                PoseArms(clip,b,u,hero,true,false);
            }
            else if (state == "Hit")
            {
                float[] u = {0,.06f,.16f,.35f};
                Rot(clip,b,b.spine,u,new float[]{ 5,-20,-7,5 },0,new float[]{0,7,-4,0});
                Rot(clip,b,b.head,u,new float[]{ 0,19,-11,0 },0,0);
                PoseArms(clip,b,u,hero,true,false);
            }
            else if (state == "Death")
            {
                float[] u = {0,.18f,.43f,.68f,1.0f};
                Rot(clip,b,b.hips,u,new float[]{ 0,10,38,58,76 },0,new float[]{0,4,8,12,13});
                Rot(clip,b,b.spine,u,new float[]{ 4,14,25,31,37 },0,0);
                PoseArms(clip,b,u,hero,true,false);
                Rot(clip,b,b.leftThigh,u,new float[]{ 0,12,28,36,40 },0,0);
                Rot(clip,b,b.rightThigh,u,new float[]{ 0,-9,-12,-21,-24 },0,0);
            }
        }

        /// <summary>Author shoulder and elbow motion against the imported skeleton's real bind axes.</summary>
        static void PoseArms(AnimationClip clip, BoneSet b, float[] times, bool hero, bool idle, bool shooting)
        {
            var lUpper = new Vector3[times.Length];var rUpper = new Vector3[times.Length];
            var lLower = new Vector3[times.Length];var rLower = new Vector3[times.Length];
            for(int i=0;i<times.Length;i++)
            {
                float phase = idle ? 0f : Mathf.Sin(i*2f*Mathf.PI/(times.Length-1));
                float sway = idle ? 0f : phase*.18f;
                // The shoot pose stabilizes the rifle without locking the runner's legs.
                // Idle/run keeps elbows relaxed; neither role uses permanent T-pose arms.
                float forward = shooting ? .59f : hero ? .31f : .26f;
                float down = shooting ? -.79f : hero ? -.93f : -.90f;
                float swing = shooting ? 0f : sway;
                lUpper[i] = new Vector3(-.18f,down,forward+swing).normalized;
                rUpper[i] = new Vector3(.18f,down,forward-swing).normalized;
                float forearmForward = shooting ? .90f : hero ? .75f : .51f;
                float forearmDown = shooting ? -.30f : hero ? -.58f : -.80f;
                lLower[i] = new Vector3(-.08f,forearmDown,forearmForward).normalized;
                rLower[i] = new Vector3(.08f,forearmDown,forearmForward).normalized;
            }
            AimBone(clip,b,b.leftArm,b.leftForearm,times,lUpper);
            AimBone(clip,b,b.rightArm,b.rightForearm,times,rUpper);
            AimBone(clip,b,b.leftForearm,b.leftHand,times,lLower);
            AimBone(clip,b,b.rightForearm,b.rightHand,times,rLower);
        }

        static void AimBone(AnimationClip clip, BoneSet set, Transform bone, Transform child,
            float[] times, Vector3[] directions)
        {
            if (bone==null || child==null || bone.parent==null) return;
            Vector3 bindVector=child.position-bone.position;
            if (bindVector.sqrMagnitude<.00001f) return;
            var cx=new AnimationCurve();var cy=new AnimationCurve();
            var cz=new AnimationCurve();var cw=new AnimationCurve();
            for(int i=0;i<times.Length;i++)
            {
                Vector3 target=set.root.TransformDirection(directions[i]);
                Quaternion targetWorld=Quaternion.FromToRotation(bindVector,target)*bone.rotation;
                Quaternion local=Quaternion.Inverse(bone.parent.rotation)*targetWorld;
                cx.AddKey(times[i],local.x);cy.AddKey(times[i],local.y);
                cz.AddKey(times[i],local.z);cw.AddKey(times[i],local.w);
            }
            string path=AnimationUtility.CalculateTransformPath(bone,set.root);
            Set(clip,path,"m_LocalRotation.x",cx);Set(clip,path,"m_LocalRotation.y",cy);
            Set(clip,path,"m_LocalRotation.z",cz);Set(clip,path,"m_LocalRotation.w",cw);
        }

        // For readability poses are authored in Euler degrees and converted to proper
        // quaternion curves, multiplied by the original GLB bone bind rotation.
        static void Rot(AnimationClip clip, BoneSet set, Transform bone, float[] times, float[] x, float y, float z)
        { Rot(clip,set,bone,times,x,Repeat(y,times.Length),Repeat(z,times.Length)); }
        static void Rot(AnimationClip clip, BoneSet set, Transform bone, float[] times, float[] x, float[] y, float z)
        { Rot(clip,set,bone,times,x,y,Repeat(z,times.Length)); }
        // Support keyframed X and Z with a constant Y. Required by Hit/Death clips.
        static void Rot(AnimationClip clip, BoneSet set, Transform bone, float[] times, float[] x, float y, float[] z)
        { Rot(clip,set,bone,times,x,Repeat(y,times.Length),z); }
        static void Rot(AnimationClip clip, BoneSet set, Transform bone, float[] times, float[] x, float[] y, float[] z)
        {
            if (bone == null) return;
            if (x.Length != times.Length || y.Length != times.Length || z.Length != times.Length) return;
            var cx=new AnimationCurve();var cy=new AnimationCurve();var cz=new AnimationCurve();var cw=new AnimationCurve();
            for(int i=0;i<times.Length;i++)
            {
                Quaternion q = bone.localRotation * Quaternion.Euler(x[i],y[i],z[i]);
                cx.AddKey(times[i],q.x);cy.AddKey(times[i],q.y);cz.AddKey(times[i],q.z);cw.AddKey(times[i],q.w);
            }
            string path=AnimationUtility.CalculateTransformPath(bone,set.root);
            Set(clip,path,"m_LocalRotation.x",cx);Set(clip,path,"m_LocalRotation.y",cy);
            Set(clip,path,"m_LocalRotation.z",cz);Set(clip,path,"m_LocalRotation.w",cw);
        }
        static void Pos(AnimationClip clip,BoneSet set, Transform bone,float[] times,float[] y)
        {
            if (bone==null || times.Length != y.Length) return;
            var curve = new AnimationCurve();
            for(int i=0;i<times.Length;i++) curve.AddKey(times[i],bone.localPosition.y+y[i]);
            Set(clip, AnimationUtility.CalculateTransformPath(bone,set.root),"m_LocalPosition.y",curve);
        }
        static float[] Repeat(float value,int count){var a=new float[count];for(int i=0;i<count;i++)a[i]=value;return a;}
        static void Set(AnimationClip clip,string path,string prop,AnimationCurve curve)
        {
            for(int i=0;i<curve.length;i++) AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),prop),curve);
        }

        static Avatar CreateHumanAvatar(string slot,GameObject source)
        {
            // Imported glTF rigs can be rigidly skinned or not quite in a T pose; Avatar
            // validation is mandatory and this is never silently declared successful.
            var bones = source.GetComponentsInChildren<Transform>(true);
            if (source.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return null;
            var found = new List<HumanBone>();
            string[] humanNames={ "Hips","Spine","Chest","UpperChest","Neck","Head",
                "LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes",
                "RightUpperLeg","RightLowerLeg","RightFoot","RightToes",
                "LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand",
                "RightShoulder","RightUpperArm","RightLowerArm","RightHand"};
            string[] boneNames={ "hips","spine","spine2","spine3","neck","head",
                "leftupleg","leftleg","leftfoot","lefttoebase",
                "rightupleg","rightleg","rightfoot","righttoebase",
                "leftshoulder","leftarm","leftforearm","lefthand",
                "rightshoulder","rightarm","rightforearm","righthand"};
            for(int i=0;i<humanNames.Length;i++)
            {
                Transform t=Find(bones,boneNames[i]);
                if (t==null) continue;
                var limit=new HumanLimit();limit.useDefaultValues=true;
                found.Add(new HumanBone { humanName=humanNames[i],boneName=t.name,limit=limit });
            }
            if (found.Count < 15) return null;
            var skeleton=new SkeletonBone[bones.Length];
            for(int i=0;i<bones.Length;i++)
                skeleton[i]=new SkeletonBone {name=bones[i].name,position=bones[i].localPosition,
                    rotation=bones[i].localRotation,scale=bones[i].localScale};
            var human=new HumanDescription {human=found.ToArray(),skeleton=skeleton,
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,
                armStretch=.05f,legStretch=.05f,feetSpacing=0f,hasTranslationDoF=false};
            Avatar avatar=null;
            try { avatar=AvatarBuilder.BuildHumanAvatar(source,human); }
            catch(Exception ex) { Debug.LogWarning("[ZSR LUX] " + slot + " Avatar build: " + ex.Message); }
            if(avatar==null || !avatar.isValid || !avatar.isHuman)
            {
                if(avatar!=null) UnityEngine.Object.DestroyImmediate(avatar);
                return null;
            }
            string path=Dir+"/"+slot+"_HumanoidAvatar.asset";
            var old=AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if(old!=null) AssetDatabase.DeleteAsset(path);
            avatar.name=slot+"_HumanoidAvatar";
            AssetDatabase.CreateAsset(avatar,path);
            return avatar;
        }
    }
}
#endif
