using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ZombieSurvivalRun
{
    /// <summary>Plays the authored timeline shipped inside each character FBX.</summary>
    public sealed class ZSRBundledCharacter : MonoBehaviour
    {
        enum Motion { Idle, Move, Fire, Reload, Hit, Attack, Death }
        const float Fps = 30f;
        const string ResourceRoot = "ProductionArt/";
        static readonly int[] Starts = { 0, 32, 58, 84, 116, 126, 146 };
        static readonly int[] Durations = { 30, 24, 24, 30, 8, 18, 21 };
        static Material atlasMaterial;
        static readonly System.Collections.Generic.Dictionary<string, AnimationClip> Takes = new();

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable currentPose, previousPose;
        Motion motion;
        float age, blend, rate = 1f, actionSpeed = 1f;
        bool moving, hero, firing;
        Transform muzzleFlash, weaponSocket;
        float flashTime, recoil;

        public Transform Muzzle { get; private set; }

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
            }
            skin.sharedMaterial = atlasMaterial;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var actor = root.AddComponent<ZSRBundledCharacter>();
            actor.hero = hero;
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
                Vector3 end = filter.transform.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z));
                Vector3 tip = socket.InverseTransformPoint(end);
                if (tip.z <= maxZ) continue;
                maxZ = tip.z;
                muzzle = tip + Vector3.forward * .025f;
            }
            Muzzle = ZSRVisuals.Pivot("Muzzle", socket, muzzle);
            muzzleFlash = ZSRVisuals.Sphere("Muzzle flash", Muzzle, Vector3.zero,
                new Vector3(.085f, .085f, .16f), ZSRVisuals.OrangeGlow).transform;
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
            output.SetSourcePlayable(mixer);
            graph.Play();
            Sample();
        }

        public void ResetMotion()
        {
            motion = Motion.Idle;
            age = 0f;
            blend = 0f;
            moving = false;
            recoil = 0f;
            HideFlash();
        }

        public void Tick(bool run, float speed)
        {
            moving = run;
            rate = hero ? speed : 1f;
            if (motion == Motion.Idle || motion == Motion.Move)
                Change(run ? Motion.Move : Motion.Idle);
        }

        public void Fire()
        {
            firing = true;
            flashTime = .055f;
            recoil = .035f;
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(true);
            if (motion != Motion.Reload && motion != Motion.Hit && motion != Motion.Death)
                Change(Motion.Fire);
        }

        public void Reload(float seconds)
        {
            actionSpeed = 1f / Mathf.Max(.1f, seconds);
            Change(Motion.Reload, true);
        }

        public void Attack() => Change(Motion.Attack, true);
        public void Hit()
        {
            if (motion != Motion.Reload && motion != Motion.Attack)
                Change(Motion.Hit, true);
        }

        public void Die()
        {
            HideFlash();
            Change(Motion.Death, true);
        }

        void Change(Motion next, bool restart = false)
        {
            if (motion == Motion.Death || (motion == next && !restart)) return;
            if (graph.IsValid()) previousPose.SetTime(currentPose.GetTime());
            float phase = (motion == Motion.Move && next == Motion.Fire) ||
                (motion == Motion.Fire && next == Motion.Move) ? age : 0f;
            motion = next;
            age = phase;
            blend = next == Motion.Fire ? .035f : .09f;
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
                recoil = Mathf.MoveTowards(recoil, 0f, dt * .48f);
                // Converting a world offset also accounts for imported FBX bone scale.
                Vector3 offset = weaponSocket.parent.InverseTransformVector(-weaponSocket.forward * recoil);
                weaponSocket.localPosition = offset;
            }
            if (!hero && motion != Motion.Death && game != null && !game.IsRunning) return;
            int index = (int)motion;
            float speed = motion == Motion.Reload ? actionSpeed :
                motion == Motion.Move || motion == Motion.Fire ? rate : 1f;
            age += dt * speed;
            float duration = Durations[index] / Fps;
            if (age >= duration && motion != Motion.Death)
            {
                if (motion == Motion.Idle || motion == Motion.Move) age %= duration;
                else Change(moving ? Motion.Move : Motion.Idle);
            }
            blend = Mathf.Max(0f, blend - dt);
            Sample();
        }

        void Sample()
        {
            int index = (int)motion;
            currentPose.SetTime((Starts[index] + Mathf.Min(age * Fps, Durations[index])) / Fps);
            float outgoing = Mathf.Clamp01(blend / .09f);
            mixer.SetInputWeight(0, 1f - outgoing);
            mixer.SetInputWeight(1, outgoing);
            graph.Evaluate(0f);
        }

        void HideFlash()
        {
            firing = false;
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
        }

        void OnDisable() => HideFlash();

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
