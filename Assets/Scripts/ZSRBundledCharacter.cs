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
        bool moving, hero, firing, walking, hasItemClips, hasWalkClips;
        Motion Locomotion => walking ? Motion.Walk : Motion.Move;
        Motion Shooting => walking ? Motion.WalkFire : Motion.Fire;
        static bool IsShooting(Motion value) => value == Motion.Fire || value == Motion.WalkFire;
        static bool IsGait(Motion value) => value == Motion.Move || value == Motion.Walk || IsShooting(value);
        Transform muzzleFlash, weaponSocket, spine, head, footLeft, footRight;
        float flashTime, recoil, aimTime, kick;
        Vector3 aimPoint;
        Quaternion socketRest;
        Quaternion sampledSpine, sampledHead;
        bool sampledUpperBody;

        void RestoreSampledPose()
        {
            // Constant animation tracks can be omitted by the FBX importer.
            // Undo the previous frame's steering before evaluating that sparse take.
            if (!sampledUpperBody) return;
            spine.localRotation = sampledSpine;
            head.localRotation = sampledHead;
        }

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
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var pelvis = FindBone(instance.transform, "Pelvis");
            var forward = FindBone(instance.transform, "ForwardMarker");
            var grip = hero ? FindBone(instance.transform, "WeaponGrip") : null;
            var barrel = hero ? FindBone(instance.transform, "WeaponForward") : null;
            var head = FindBone(instance.transform, "Head");
            var leftAnkle = FindBone(instance.transform, "FootL");
            var rightAnkle = FindBone(instance.transform, "FootR");
            if (skin == null || pelvis == null || forward == null || head == null ||
                leftAnkle == null || rightAnkle == null || (hero && (grip == null || barrel == null)) ||
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
                var emission = Resources.Load<Texture2D>(ResourceRoot + "CharacterAtlas_emission");
                if (emission != null && atlasMaterial.HasProperty("_EmissionMap"))
                {
                    atlasMaterial.SetTexture("_EmissionMap", emission);
                    atlasMaterial.SetColor("_EmissionColor", new Color(1.25f, .54f, .28f));
                    atlasMaterial.EnableKeyword("_EMISSION");
                }
            }
            skin.sharedMaterial = atlasMaterial;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var actor = root.AddComponent<ZSRBundledCharacter>();
            actor.hero = hero;
            if (hero)
            {
                actor.spine = FindBone(instance.transform, "Spine");
                actor.head = FindBone(instance.transform, "Head");
                actor.footLeft = leftAnkle;
                actor.footRight = rightAnkle;
            }
            actor.hasItemClips = hero && take.length >= 7.16f;
            actor.hasWalkClips = hero && take.length >= 9.59f;
            if (hero) actor.ItemHand = FindBone(instance.transform, "HandL");
            if (hero) actor.AttachWeapon(grip, barrel, weapon);
            if (Application.isPlaying) actor.CreateGraph(animator, take);
            return actor;
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

        void AttachWeapon(Transform grip, Transform barrel, WeaponType weapon)
        {
            var socket = new GameObject("Weapon grip").transform;
            socket.SetParent(grip, false);
            weaponSocket = socket;
            socket.rotation = Quaternion.LookRotation(barrel.position - grip.position, transform.up);
            socketRest = socket.localRotation;
            kick = weapon switch
            {
                WeaponType.Pistol => .043f, WeaponType.Shotgun => .105f,
                WeaponType.Minigun => .025f, _ => .057f
            };
            Vector3 referenceScale = transform.lossyScale;
            Vector3 socketScale = socket.lossyScale;
            socket.localScale = new Vector3(
                referenceScale.x / Mathf.Max(.0001f, Mathf.Abs(socketScale.x)),
                referenceScale.y / Mathf.Max(.0001f, Mathf.Abs(socketScale.y)),
                referenceScale.z / Mathf.Max(.0001f, Mathf.Abs(socketScale.z)));
            var gun = ZSRVisuals.Rifle(socket, weapon);
            float weaponScale = weapon switch
            {
                WeaponType.Pistol => .37f, WeaponType.Rifle => .51f,
                WeaponType.Shotgun => .55f, WeaponType.Minigun => .60f, _ => .51f
            };
            gun.localScale = Vector3.one * weaponScale;
            gun.localPosition = Vector3.up * .065f;
            var muzzle = new Vector3(0f, .04f, .65f);
            float maxZ = float.NegativeInfinity;
            foreach (var filter in gun.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                // The rifle's tip sits on the axis authored into both barrel rings.
                float axisY = weapon == WeaponType.Rifle ? .04f : bounds.center.y;
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
            HideFlash();
        }

        public void Tick(bool run, float speed, bool walk = false)
        {
            moving = run;
            walking = run && walk && hasWalkClips;
            if (walk && !hasWalkClips) speed *= .59f;
            rate = Mathf.Clamp(speed, .55f, 1.75f);
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
            aimPoint = target;
            aimTime = .19f;
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
            float previousStep = gaitPhase;
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
            if (hero && striding && game != null && game.IsRunning)
            {
                if (gaitPhase < previousStep && previousStep >= .5f) game.Footstep(footLeft.position);
                else if (previousStep < .5f && gaitPhase >= .5f) game.Footstep(footRight.position);
            }
            ApplyAim();
        }

        void ApplyAim()
        {
            if (weaponSocket == null) return;
            var hand = weaponSocket.parent;
            Quaternion rest = hand.rotation * socketRest;
            Vector3 toTarget = aimPoint - weaponSocket.position;
            if (aimTime > 0f && toTarget.sqrMagnitude > .01f)
            {
                var aim = Quaternion.RotateTowards(rest, Quaternion.LookRotation(toTarget, transform.up), 28f);
                weaponSocket.rotation = Quaternion.Slerp(rest, aim, Mathf.Clamp01(aimTime * 19f));
            }
            else weaponSocket.localRotation = socketRest;
            weaponSocket.localPosition = hand.InverseTransformVector(-weaponSocket.forward * recoil);
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
            RestoreSampledPose();
            graph.Evaluate(0f);
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

        void OnDisable() { RestoreSampledPose(); HideFlash(); }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (legMask != null) Destroy(legMask);
        }
    }
}
