using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ZombieSurvivalRun
{
    /// <summary>Plays the authored timeline shipped inside each character FBX.</summary>
    public sealed class ZSRBundledCharacter : MonoBehaviour
    {
        enum Motion { Idle, Move, Fire, Reload, Hit, Attack, Death, Medkit, Throw, Walk, WalkFire }
        const float Fps = 30f;
        const string ResourceRoot = "ProductionArt/";
        static readonly int[] Starts = { 0, 32, 58, 84, 116, 126, 146, 170, 198, 218, 254 };
        static readonly int[] Durations = { 30, 24, 24, 30, 8, 18, 21, 24, 18, 34, 34 };
        static Material atlasMaterial;
        static readonly System.Collections.Generic.Dictionary<string, AnimationClip> Takes = new();

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable currentPose, previousPose;
        AnimationLayerMixerPlayable heroLayers;
        AnimationMixerPlayable lowerBody;
        AnimationClipPlayable legRun, legWalk;
        AvatarMask legMask;
        float walkBlend, legWeight, steeringHeading;
        Motion motion;
        float age, gaitPhase, blend, blendDuration = .09f, rate = 1f, actionSpeed = 1f, steeringRoll;
        float desiredRate = 1f;
        bool moving, hero, firing, walking, hasItemClips, hasWalkClips;
        Motion Locomotion => walking ? Motion.Walk : Motion.Move;
        Motion Shooting => walking ? Motion.WalkFire : Motion.Fire;
        static bool IsShooting(Motion value) => value == Motion.Fire || value == Motion.WalkFire;
        static bool IsGait(Motion value) => value == Motion.Move || value == Motion.Walk || IsShooting(value);
        Transform muzzleFlash, weaponSocket, barrelRotor, spine, head, footLeft, footRight;
        float barrelSpeed, barrelAngle;
        float flashTime, recoil, aimTime, kick;
        Vector3 aimPoint;
        Quaternion socketRest;
        Vector3 socketPosition;
        Transform supportUpper, supportLower, supportHand;
        Vector3 supportPoint;
        Quaternion supportRotation, sampledSupportUpper, sampledSupportLower, sampledSupportHand;
        float supportWeight;
        bool sampledSupport;
        Quaternion sampledSpine, sampledHead;
        bool sampledUpperBody;
        readonly ZSRUpperBodyAim upperAim = new();
        float breathPhase;

        void RestoreSampledPose()
        {
            // Constant animation tracks can be omitted by the FBX importer.
            // Undo the previous frame's steering before evaluating that sparse take.
            if (!sampledUpperBody) return;
            spine.localRotation = sampledSpine;
            head.localRotation = sampledHead;
        }

        public SkinnedMeshRenderer PrimarySkin { get; private set; }
        public Transform Mouth { get; private set; }
        public Transform Muzzle { get; private set; }
        public Transform ItemHand { get; private set; }

        public static ZSRBundledCharacter Create(Transform parent, string name, bool hero, WeaponType weapon)
        {
            string path = ResourceRoot + name;
            var model = Resources.Load<GameObject>(path);
            if (!Takes.TryGetValue(path, out var take))
            {
                foreach (var clip in Resources.LoadAll<AnimationClip>(path))
                {
                    if (clip.legacy || clip.name.StartsWith("__preview__") || clip.length < 5.5f) continue;
                    take = clip;
                    Takes.Add(path, take);
                    break;
                }
            }
            if (model == null || take == null)
            {
                Debug.LogWarning("[ZSR ART] Shipped rig or motion take missing for " + name + ".");
                return null;
            }

            var root = new GameObject(name + " / skinned character");
            root.transform.SetParent(parent, false);
            var placement = ZSRVisuals.Pivot("Model alignment", root.transform, Vector3.zero);
            var instance = Instantiate(model, placement, false);
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer skin = null;
            foreach (var candidate in skins)
                if (skin == null || candidate.name.EndsWith("_LOD0")) skin = candidate;
            var pelvis = FindBone(instance.transform, "Pelvis");
            var forward = FindBone(instance.transform, "ForwardMarker");
            var grip = hero ? FindBone(instance.transform, "WeaponGrip") : null;
            var barrel = hero ? FindBone(instance.transform, "WeaponForward") : null;
            var gripHand = hero ? FindBone(instance.transform, "HandR") : null;
            var head = FindBone(instance.transform, "Head");
            var leftAnkle = FindBone(instance.transform, "FootL");
            var rightAnkle = FindBone(instance.transform, "FootR");
            if (skin == null || pelvis == null || forward == null || head == null ||
                leftAnkle == null || rightAnkle == null || (hero && (grip == null || barrel == null || gripHand == null)) ||
                !AlignRig(placement, name, pelvis, forward, head, leftAnkle, rightAnkle))
            {
                Debug.LogError("[ZSR ART] Invalid rig hierarchy in " + name + ".");
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
                return null;
            }

            if (atlasMaterial == null)
            {
                atlasMaterial = ZSRVisuals.MatWithTexture(Color.white, ResourceRoot + "CharacterAtlas", .17f,
                    missingTextureColor: new Color(.23f, .26f, .29f));
                atlasMaterial.name = "ZSR Character Atlas / shared";
                var surface = Resources.Load<Texture2D>(ResourceRoot + "CharacterAtlas_surface");
                if (surface != null && atlasMaterial.HasProperty("_MetallicGlossMap"))
                {
                    atlasMaterial.SetTexture("_MetallicGlossMap", surface);
                    atlasMaterial.SetFloat("_Smoothness", 1f);
                    atlasMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                var emission = Resources.Load<Texture2D>(ResourceRoot + "CharacterAtlas_emission");
                if (emission != null && atlasMaterial.HasProperty("_EmissionMap"))
                {
                    atlasMaterial.SetTexture("_EmissionMap", emission);
                    atlasMaterial.SetColor("_EmissionColor", new Color(1.25f, .54f, .28f));
                    atlasMaterial.EnableKeyword("_EMISSION");
                }
            }
            foreach (var renderer in skins) renderer.sharedMaterial = atlasMaterial;
            ConfigureDistanceModels(instance, skins);

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var actor = root.AddComponent<ZSRBundledCharacter>();
            actor.hero = hero;
            actor.PrimarySkin = skin;
            var mouth = new GameObject("Mouth / attack origin").transform;
            mouth.SetParent(head,false);
            mouth.position = head.position + root.transform.TransformVector(new Vector3(0,.10f,.115f));
            actor.Mouth = mouth;
            if (hero)
            {
                actor.spine = FindBone(instance.transform, "Spine");
                actor.head = FindBone(instance.transform, "Head");
                actor.footLeft = leftAnkle;
                actor.footRight = rightAnkle;
                ZSRSecondaryMotion.Attach(root.transform, head,
                    FindBone(instance.transform,"HairTie"), FindBone(instance.transform,"HairTail"),
                    FindBone(instance.transform,"Pack"));
            }
            actor.hasItemClips = hero && take.length >= 7.16f;
            actor.hasWalkClips = hero && take.length >= 9.59f;
            if (hero) actor.ItemHand = FindBone(instance.transform, "HandL");
            // Calibrate from the first authored pose, not the imported bind pose.
            // The runtime gun follows the deforming wrist directly; auxiliary
            // aim markers must not cancel the hand's running rotation.
            var culling = animator.cullingMode;
            if (Application.isPlaying)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                actor.CreateGraph(animator, take);
            }
            if (hero) actor.AttachWeapon(grip, barrel, gripHand, weapon);
            animator.cullingMode = culling;
            return actor;
        }

        static void ConfigureDistanceModels(GameObject model, SkinnedMeshRenderer[] skins)
        {
            if (skins.Length < 3) return;
            var levels = new LOD[3];
            float[] coverage = { .12f, .045f, .007f };
            for (int index = 0; index < levels.Length; index++)
            {
                SkinnedMeshRenderer renderer = null;
                foreach (var candidate in skins)
                    if (candidate.name.EndsWith("_LOD" + index)) renderer = candidate;
                if (renderer == null) return;
                levels[index] = new LOD(coverage[index], new Renderer[] { renderer });
            }
            var group = model.GetComponent<LODGroup>();
            if (group == null) group = model.AddComponent<LODGroup>();
            group.SetLODs(levels);
            group.RecalculateBounds();
        }

        static bool AlignRig(Transform placement, string name, Transform pelvis, Transform marker,
            Transform head, Transform leftAnkle, Transform rightAnkle)
        {
            // Work in the actor's local units. The enemy class scale and the
            // imported FBX root's axis/unit transforms both remain intact.
            var actor = placement.parent;
            Vector3 ankles = (leftAnkle.position + rightAnkle.position) * .5f;
            var rise = actor.InverseTransformVector(head.position - ankles);
            var forward = actor.InverseTransformVector(marker.position - pelvis.position);
            if (rise.sqrMagnitude < .00000001f) return false;
            Vector3 up = rise.normalized;
            forward = Vector3.ProjectOnPlane(forward, up);
            if (forward.sqrMagnitude < .00000001f) return false;

            float height = name switch
            {
                "Eva" => 1.81f, "Max" => 1.94f, "Walker" => 1.89f, "Runner" => 1.86f,
                "Armored" => 1.98f, "Brute" => 2.10f, "Toxic" => 1.84f,
                "Crawler" => 1.72f, "Boss" => 2.14f, _ => 1.90f
            };
            const float ankleHeight = .095f;
            float rigToGameUnits = (height * .86f - ankleHeight) / rise.magnitude;
            if (float.IsNaN(rigToGameUnits) || rigToGameUnits < .0001f || rigToGameUnits > 1000f)
                return false;

            placement.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            placement.localScale = Vector3.one * rigToGameUnits;
            var planted = actor.InverseTransformPoint((leftAnkle.position + rightAnkle.position) * .5f);
            placement.localPosition = new Vector3(-planted.x, ankleHeight - planted.y, -planted.z);
            return true;
        }

        static Transform FindBone(Transform root, string name)
        {
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                if (bone.name == name) return bone;
            return null;
        }

        void AttachWeapon(Transform grip, Transform barrel, Transform hand, WeaponType weapon)
        {
            var socket = new GameObject("Weapon grip").transform;
            socket.SetParent(hand, false);
            socket.localPosition = hand.InverseTransformPoint(grip.position);
            socketPosition = socket.localPosition;
            weaponSocket = socket;
            socket.rotation = Quaternion.LookRotation(barrel.position - grip.position, transform.up);
            socketRest = socket.localRotation;
            Vector3 referenceScale = transform.lossyScale;
            Vector3 socketScale = socket.lossyScale;
            socket.localScale = new Vector3(
                referenceScale.x / Mathf.Max(.0001f, Mathf.Abs(socketScale.x)),
                referenceScale.y / Mathf.Max(.0001f, Mathf.Abs(socketScale.y)),
                referenceScale.z / Mathf.Max(.0001f, Mathf.Abs(socketScale.z)));
            supportUpper = FindBone(transform,"UpperArmL");
            supportLower = FindBone(transform,"ForearmL");
            supportHand = FindBone(transform,"HandL");
            if (supportHand != null)
            {
                supportPoint = socket.InverseTransformPoint(supportHand.position);
                supportRotation = Quaternion.Inverse(socket.rotation)*supportHand.rotation;
                // Each weapon has its own support location. A pistol is held
                // with joined hands; its support hand must not reach past the muzzle.
                if (weapon == WeaponType.Pistol)
                {
                    supportPoint += new Vector3(.035f,-.015f,-.250f);
                    supportRotation = Quaternion.LookRotation(new Vector3(.03f,-1f,.22f),new Vector3(1f,.03f,0f)) *
                        Quaternion.Inverse(Quaternion.LookRotation(new Vector3(1f,.08f,.12f),new Vector3(-.08f,1f,0f))) * supportRotation;
                }
                else if (weapon == WeaponType.Shotgun) supportPoint += new Vector3(0f,0f,.030f);
                else if (weapon == WeaponType.Minigun) supportPoint += new Vector3(-.010f,0f,.040f);
            }
            kick = weapon switch
            {
                WeaponType.Pistol => .043f, WeaponType.Shotgun => .105f,
                WeaponType.Minigun => .025f, _ => .057f
            };
            var gun = ZSRVisuals.Rifle(socket, weapon);
            float weaponScale = weapon switch
            {
                WeaponType.Pistol => .37f, WeaponType.Rifle => .51f,
                WeaponType.Shotgun => .55f, WeaponType.Minigun => .60f, _ => .51f
            };
            gun.localScale = Vector3.one * weaponScale;
            gun.localPosition = Vector3.up * .065f;
            if (weapon == WeaponType.Minigun)
            {
                foreach (var part in gun.GetComponentsInChildren<MeshFilter>())
                {
                    if (part.name != "detailed steel barrel") continue;
                    barrelRotor = ZSRVisuals.Pivot("Barrel rotor",gun,new Vector3(0,.04f,0));
                    part.transform.SetParent(barrelRotor,false);
                    part.transform.localPosition = new Vector3(0,-.04f,0);
                    break;
                }
            }
            var muzzle = new Vector3(0f, .04f, .65f);
            float maxZ = float.NegativeInfinity;
            foreach (var filter in gun.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                // The rifle's tip sits on the axis authored into both barrel rings.
                float axisY = .04f;
                Vector3 end = filter.transform.TransformPoint(new Vector3(bounds.center.x, axisY, bounds.max.z));
                Vector3 tip = socket.InverseTransformPoint(end);
                if (tip.z <= maxZ) continue;
                maxZ = tip.z;
                muzzle = tip + Vector3.forward * .025f;
            }
            Muzzle = ZSRVisuals.Pivot("Muzzle", socket, muzzle);
            muzzleFlash = ZSRVisuals.Pivot("Muzzle flash", Muzzle, Vector3.zero);
            ZSRVisuals.Sphere("Hot muzzle core", muzzleFlash, new Vector3(0, 0, .055f),
                new Vector3(.09f, .09f, .22f), ZSRVisuals.GoldGlow);
            ZSRVisuals.Box("Muzzle flare", muzzleFlash, new Vector3(0, 0, .035f),
                new Vector3(.21f, .026f, .095f), ZSRVisuals.OrangeGlow);
            muzzleFlash.localScale = Vector3.one * (weapon == WeaponType.Shotgun ? 1.6f : 1f);
            muzzleFlash.gameObject.SetActive(false);
        }

        void CreateGraph(Animator animator, AnimationClip take)
        {
            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            currentPose = AnimationClipPlayable.Create(graph, take);
            previousPose = AnimationClipPlayable.Create(graph, take);
            currentPose.SetApplyFootIK(false);
            previousPose.SetApplyFootIK(false);
            graph.Connect(currentPose, 0, mixer, 0);
            graph.Connect(previousPose, 0, mixer, 1);
            var output = AnimationPlayableOutput.Create(graph, "Skin", animator);
            if (hero && spine != null)
            {
                // The upper-body takes include baked legs starting at frame zero.
                // Keep the pelvis and both leg chains on the same clock through every action.
                heroLayers = AnimationLayerMixerPlayable.Create(graph, 2);
                lowerBody = AnimationMixerPlayable.Create(graph, 2);
                legRun = AnimationClipPlayable.Create(graph, take);
                legWalk = AnimationClipPlayable.Create(graph, take);
                legRun.SetApplyFootIK(false);
                legWalk.SetApplyFootIK(false);
                graph.Connect(legRun, 0, lowerBody, 0);
                graph.Connect(legWalk, 0, lowerBody, 1);
                graph.Connect(mixer, 0, heroLayers, 0);
                graph.Connect(lowerBody, 0, heroLayers, 1);
                heroLayers.SetInputWeight(0, 1f);
                legMask = CreateLegMask(animator.transform, spine);
                heroLayers.SetLayerMaskFromAvatarMask(1, legMask);
                output.SetSourcePlayable(heroLayers);
            }
            else output.SetSourcePlayable(mixer);
            graph.Play();
            Sample();
        }

        static AvatarMask CreateLegMask(Transform root, Transform spine)
        {
            var mask = new AvatarMask();
            // These shipped FBXs use generic transform bindings relative to the Animator.
            // In particular the pelvis moves, while the entire Spine branch belongs to the action.
            Transform pelvis = spine.parent;
            var bones = root.GetComponentsInChildren<Transform>(true);
            mask.transformCount = bones.Length;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                string path = "";
                for (var child = bone; child != root; child = child.parent)
                    path = path.Length == 0 ? child.name : child.name + "/" + path;
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, bone.IsChildOf(pelvis) && !bone.IsChildOf(spine));
            }
            return mask;
        }

        public void ResetMotion(float stridePhase)
        {
            motion = Motion.Idle;
            age = 0f;
            gaitPhase = Mathf.Repeat(stridePhase, 1f);
            walking = false;
            blend = 0f;
            walkBlend = legWeight = steeringHeading = steeringRoll = 0f;
            moving = false;
            recoil = 0f;
            aimTime = 0f;
            upperAim.Reset();
            breathPhase = 0f;
            rate = desiredRate = 1f;
            supportWeight = 0f;
            HideFlash();
        }

        public void Tick(bool run, float speed, bool walk = false)
        {
            moving = run;
            walking = run && walk && hasWalkClips;
            if (walk && !hasWalkClips) speed *= .59f;
            desiredRate = Mathf.Clamp(speed, .55f, 1.75f);
            if (IsShooting(motion) && run && aimTime > 0f)
                Change(Shooting);
            else if (motion == Motion.Idle || IsGait(motion))
                Change(run ? Locomotion : Motion.Idle);
        }

        public bool TrySetSteering(float roll, float heading)
        {
            if (spine == null) return false;
            steeringRoll = roll;
            steeringHeading = heading;
            return true;
        }

        public void Fire(Vector3 target)
        {
            firing = true;
            flashTime = .045f;
            recoil = kick;
            if (barrelRotor != null) barrelSpeed = 1800f;
            aimPoint = target;
            aimTime = .70f;
            ApplyAim();
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(true);
            if (IsGait(motion) || motion == Motion.Idle)
                Change(Shooting);
        }

        public void UseItem(bool medkit)
        {
            aimTime = 0f;
            if (hasItemClips) Change(medkit ? Motion.Medkit : Motion.Throw, true);
        }

        public void Reload(float seconds)
        {
            actionSpeed = 1f / Mathf.Max(.1f, seconds);
            Change(Motion.Reload, true);
        }

        public void Attack() => Change(Motion.Attack, true);
        public void Hit()
        {
            if (motion != Motion.Reload && motion != Motion.Attack && motion != Motion.Medkit && motion != Motion.Throw)
                Change(Motion.Hit, true);
        }

        public void Die()
        {
            HideFlash();
            aimTime = 0f;
            Change(Motion.Death, true);
        }

        void Change(Motion next, bool restart = false)
        {
            if (motion == Motion.Death || (motion == next && !restart)) return;
            if (graph.IsValid()) previousPose.SetTime(currentPose.GetTime());
            motion = next;
            age = 0f;
            blend = blendDuration = IsShooting(next) ? .06f : .10f;
        }

        void LateUpdate()
        {
            if (!graph.IsValid()) return;
            var game = ZSRGame.Instance;
            if (game != null && game.State == ZSRState.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (firing && (flashTime -= dt) <= 0f) HideFlash();
            if (weaponSocket != null)
            {
                recoil = Mathf.MoveTowards(recoil, 0f, dt * .80f);
                aimTime = Mathf.Max(0f, aimTime - dt);
            }
            if (!hero && motion != Motion.Death && game != null && !game.IsRunning) return;
            if (barrelRotor != null)
            {
                barrelSpeed = Mathf.MoveTowards(barrelSpeed,0f,dt*2600f);
                barrelAngle = Mathf.Repeat(barrelAngle+barrelSpeed*dt,360f);
                barrelRotor.localRotation = Quaternion.AngleAxis(barrelAngle,Vector3.forward);
            }
            float previousStep = gaitPhase;
            rate = ZSRHeroGait.SettleCadence(rate,desiredRate,dt);
            bool striding = moving && motion != Motion.Death;
            walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt / .14f);
            legWeight = Mathf.MoveTowards(legWeight, striding ? 1f : 0f, dt / .10f);
            float strideDuration = Mathf.Lerp(Durations[(int)Motion.Move], Durations[(int)Motion.Walk], walkBlend) / Fps;
            if (striding)
                gaitPhase = Mathf.Repeat(gaitPhase + dt * rate / strideDuration, 1f);
            int index = (int)motion;
            float speed = motion == Motion.Reload ? actionSpeed : 1f;
            age += dt * speed;
            float duration = Durations[index] / Fps;
            if (age >= duration && motion != Motion.Death)
            {
                if (motion == Motion.Idle || motion == Locomotion) age %= duration;
                else if (!IsShooting(motion) || aimTime <= 0f)
                {
                    Change(moving ? Locomotion : Motion.Idle);
                }
            }
            blend = Mathf.Max(0f, blend - dt);
            Sample();
            if (spine != null && motion != Motion.Death &&
                (Mathf.Abs(steeringRoll) > .01f || Mathf.Abs(steeringHeading) > .01f))
            {
                float lean = steeringRoll * (walking ? .72f : 1f);
                float counterTurn = -steeringHeading * (aimTime > 0f ? .82f : .55f);
                spine.rotation = Quaternion.AngleAxis(counterTurn, transform.up) *
                    Quaternion.AngleAxis(lean, transform.forward) * spine.rotation;
                if (head != null)
                    head.rotation = Quaternion.AngleAxis(-lean * .36f, transform.forward) * head.rotation;
            }
            ApplyUpperBodyAim(dt);
            if (hero && striding && game != null && game.IsRunning)
            {
                if (gaitPhase < previousStep && previousStep >= .5f) game.Footstep(footLeft.position);
                else if (previousStep < .5f && gaitPhase >= .5f) game.Footstep(footRight.position);
            }
            ApplyAim();
            ApplySupportGrip(dt);
        }

        void RestoreSupportPose()
        {
            if (!sampledSupport) return;
            supportUpper.localRotation = sampledSupportUpper;
            supportLower.localRotation = sampledSupportLower;
            supportHand.localRotation = sampledSupportHand;
        }

        void ApplySupportGrip(float dt)
        {
            if (supportUpper == null || supportLower == null || supportHand == null || weaponSocket == null) return;
            if (motion == Motion.Death) { supportWeight = 0f; return; }
            bool carry = motion == Motion.Idle || IsGait(motion) || IsShooting(motion);
            supportWeight = Mathf.MoveTowards(supportWeight,carry ? 1f : 0f,dt/.12f);
            if (supportWeight <= 0f) return;
            Vector3 shoulder = supportUpper.position, elbow = supportLower.position, wrist = supportHand.position;
            Vector3 target = Vector3.Lerp(wrist,weaponSocket.TransformPoint(supportPoint),supportWeight);
            Quaternion rotation = Quaternion.Slerp(supportHand.rotation,weaponSocket.rotation*supportRotation,supportWeight);
            if (!ZSRArmReach.Solve(shoulder,elbow,wrist,target,out var joint,out var end)) return;
            supportUpper.rotation = Quaternion.FromToRotation(elbow-shoulder,joint-shoulder)*supportUpper.rotation;
            supportLower.rotation = Quaternion.FromToRotation(supportHand.position-supportLower.position,
                end-supportLower.position)*supportLower.rotation;
            supportHand.rotation = rotation;
        }

        void ApplyAim()
        {
            if (weaponSocket == null) return;
            var hand = weaponSocket.parent;
            Quaternion rest = hand.rotation * socketRest;
            Vector3 toTarget = aimPoint - weaponSocket.position;
            if (aimTime > 0f && toTarget.sqrMagnitude > .01f)
            {
                // The shoulders carry the large aiming motion; the mounted gun
                // only makes a small correction instead of leaving both hands.
                var aim = Quaternion.RotateTowards(rest, Quaternion.LookRotation(toTarget, rest * Vector3.up), 1f);
                weaponSocket.rotation = Quaternion.Slerp(rest, aim, Mathf.Clamp01(aimTime * 19f));
            }
            else weaponSocket.localRotation = socketRest;
            weaponSocket.localPosition = socketPosition;
        }

        void ApplyUpperBodyAim(float dt)
        {
            if (!hero || spine == null || weaponSocket == null || motion == Motion.Death) return;
            Quaternion reference = weaponSocket.parent.rotation * socketRest;
            Vector3 relative = Quaternion.Inverse(reference) * (aimPoint-weaponSocket.position);
            bool active = aimTime>0f && IsShooting(motion);
            Vector2 angles = upperAim.Tick(relative,active,dt);
            float kick = active ? ZSRUpperBodyAim.RecoilPitch(recoil) : 0f;
            breathPhase = Mathf.Repeat(breathPhase+dt*(moving?2.4f:1.43f),Mathf.PI*2f);
            float breath = Mathf.Sin(breathPhase)*(moving?.10f:.28f);
            Vector3 right = reference * Vector3.right;
            spine.rotation = Quaternion.AngleAxis(angles.x,transform.up) *
                Quaternion.AngleAxis(angles.y+kick+breath,right) * spine.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(-angles.x*.40f,transform.up) *
                    Quaternion.AngleAxis(-(angles.y+kick)*.60f,right) * head.rotation;
        }

        void Sample()
        {
            int index = (int)motion;
            float time = IsGait(motion) ? gaitPhase * Durations[index] / Fps : age;
            currentPose.SetTime((Starts[index] + Mathf.Min(time * Fps, Durations[index])) / Fps);
            float linear = Mathf.Clamp01(blend / blendDuration);
            float outgoing = linear * linear * (3f - 2f * linear);
            mixer.SetInputWeight(0, 1f - outgoing);
            mixer.SetInputWeight(1, outgoing);
            if (legMask != null)
            {
                legRun.SetTime((Starts[(int)Motion.Move] + gaitPhase * Durations[(int)Motion.Move]) / Fps);
                Motion walk = hasWalkClips ? Motion.Walk : Motion.Move;
                legWalk.SetTime((Starts[(int)walk] + gaitPhase * Durations[(int)walk]) / Fps);
                lowerBody.SetInputWeight(0, 1f - walkBlend);
                lowerBody.SetInputWeight(1, walkBlend);
                heroLayers.SetInputWeight(1, legWeight);
            }
            RestoreSupportPose();
            RestoreSampledPose();
            graph.Evaluate(0f);
            if (supportUpper != null && supportLower != null && supportHand != null)
            {
                sampledSupportUpper = supportUpper.localRotation;
                sampledSupportLower = supportLower.localRotation;
                sampledSupportHand = supportHand.localRotation;
                sampledSupport = true;
            }
            if (spine != null && head != null)
            {
                sampledSpine = spine.localRotation;
                sampledHead = head.localRotation;
                sampledUpperBody = true;
            }
        }

        void HideFlash()
        {
            firing = false;
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
        }

        void OnDisable() { RestoreSupportPose(); RestoreSampledPose(); HideFlash(); }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (legMask != null) Destroy(legMask);
        }
    }
}
