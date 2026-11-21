using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvivalRun
{
    /// <summary>
    /// Version 0.8.1 visibility recovery: skeleton animation is preserved, while
    /// parts render through ordinary MeshRenderers attached to bones. The v0.8
    /// runtime SkinnedMeshRenderer made the characters disappear on the target setup.
    /// This is a reliability hotfix, not a production-quality Humanoid rig.
    /// </summary>
    public sealed class ZSRSkinnedArt : MonoBehaviour
    {
        const string ResourceMeshFolder = "Meshes/";
        // 0.8.1 recovery: a SkinnedMeshRenderer generated at runtime was not rendering
        // reliably on the user's actual Unity setup. Keep the Animator skeleton, but
        // render independent imported OBJ meshes as children of the animated bones.
        // This eliminates GPU skinning/bindpose/importer readability dependencies.
        static readonly bool ReliableBoneRendering = true;
        readonly List<Vector3> vertices = new List<Vector3>(35000);
        readonly List<Vector3> normals = new List<Vector3>(35000);
        readonly List<Vector2> uvs = new List<Vector2>(35000);
        readonly List<BoneWeight> weights = new List<BoneWeight>(35000);
        readonly List<Material> materials = new List<Material>();
        readonly List<List<int>> indices = new List<List<int>>();
        readonly Transform[] bones = new Transform[15];
        Animator animator;
        Transform skinRoot;
        Mesh runtimeMesh;
        float fallbackPhase, fallbackSpeed = 1f;
        float hitReactionTimer;
        bool fallbackDead;
        GameObject muzzleFlash;
        public Transform WeaponAnchor { get; private set; }
        public bool IsWorking { get; private set; }
        public bool IsHero { get; private set; }
        public Mesh RigMesh { get { return runtimeMesh; } }
        public void SetGalleryMesh(Mesh savedAsset)
        {
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null) skin.sharedMesh = savedAsset;
            if (runtimeMesh != null) DestroyImmediate(runtimeMesh);
            runtimeMesh = null;
        }

        // Bone index convention used by the editor-authored animation clips.
        const int Hips = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4,
                  UpperLeftArm = 5, LowerLeftArm = 6, UpperRightArm = 7,
                  LowerRightArm = 8, UpperLeftLeg = 9, LowerLeftLeg = 10,
                  UpperRightLeg = 11, LowerRightLeg = 12, LeftFoot = 13, RightFoot = 14;

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        public static ZSRSkinnedArt CreateHero(Transform parent, int selection, WeaponType gun)
        {
            if (LoadPart("survivor_face") == null || LoadPart("max_vest") == null) return null;
            var obj = new GameObject(selection == 0 ? "MAX - RIGGED SKIN / Unity Animator" : "EVA - RIGGED SKIN / Unity Animator");
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = selection == 0 ? Vector3.one * .97f : Vector3.one * .93f;
            var rig = obj.AddComponent<ZSRSkinnedArt>();
            rig.IsHero = true;
            rig.BuildSkeleton();
            rig.AddSurvivorMesh(selection);
            rig.FinishMesh(selection == 0 ? "Max_Skinned" : "Eva_Skinned");
            var anchor = ZSRVisuals.Pivot("WeaponAnchor", rig.bones[Chest], new Vector3(.10f, -.21f, .58f));
            rig.WeaponAnchor = anchor;
            ZSRVisuals.Rifle(anchor, gun);
            rig.muzzleFlash = ZSRVisuals.Sphere("Muzzle flash", anchor, new Vector3(0, .05f, .98f), new Vector3(.14f, .17f, .22f), ZSRVisuals.OrangeGlow);
            rig.muzzleFlash.SetActive(false);
            rig.AttachAnimator(true);
            rig.IsWorking = true;
            return rig;
        }

        public static ZSRSkinnedArt CreateZombie(Transform parent, ZombieType kind)
        {
            if (LoadPart("infected_face") == null || LoadPart("walker_ribs") == null) return null;
            var obj = new GameObject("INFECTED - RIGGED SKIN / " + kind);
            obj.transform.SetParent(parent, false);
            var rig = obj.AddComponent<ZSRSkinnedArt>();
            rig.IsHero = false;
            rig.BuildSkeleton();
            rig.AddZombieMesh(kind);
            rig.FinishMesh("Zombie_Skinned_" + kind);
            if (kind == ZombieType.Toxic)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = (i - 1.5f) * .17f;
                    ZSRVisuals.Sphere("toxic luminous growth", rig.bones[Chest],
                        new Vector3(x, -.02f + i % 2 * .17f, -.33f),
                        new Vector3(.14f, .18f, .12f), ZSRVisuals.ToxicGlow);
                }
            }
            rig.AttachAnimator(false);
            rig.IsWorking = true;
            return rig;
        }

        static Mesh LoadPart(string name)
        {
            Mesh found;
            if (meshCache.TryGetValue(name, out found)) return found;
            var go = Resources.Load<GameObject>(ResourceMeshFolder + name);
            var filter = go != null ? go.GetComponentInChildren<MeshFilter>(true) : null;
            found = filter != null ? filter.sharedMesh : null;
            meshCache[name] = found;
            return found;
        }

        static Transform Bone(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        void BuildSkeleton()
        {
            skinRoot = Bone("Rig", transform, Vector3.zero);
            bones[Hips] = Bone("Hips", skinRoot, new Vector3(0, 1.03f, 0));
            bones[Spine] = Bone("Spine", bones[Hips], new Vector3(0, .31f, 0));
            bones[Chest] = Bone("Chest", bones[Spine], new Vector3(0, .35f, 0));
            bones[Neck] = Bone("Neck", bones[Chest], new Vector3(0, .39f, .02f));
            bones[Head] = Bone("Head", bones[Neck], new Vector3(0, .16f, 0));
            bones[UpperLeftArm] = Bone("LeftUpperArm", bones[Chest], new Vector3(-.48f, .15f, 0));
            bones[LowerLeftArm] = Bone("LeftLowerArm", bones[UpperLeftArm], new Vector3(-.03f, -.50f, .04f));
            bones[UpperRightArm] = Bone("RightUpperArm", bones[Chest], new Vector3(.48f, .15f, 0));
            bones[LowerRightArm] = Bone("RightLowerArm", bones[UpperRightArm], new Vector3(.03f, -.50f, .04f));
            bones[UpperLeftLeg] = Bone("LeftUpperLeg", bones[Hips], new Vector3(-.23f, -.04f, 0));
            bones[LowerLeftLeg] = Bone("LeftLowerLeg", bones[UpperLeftLeg], new Vector3(0, -.52f, 0));
            bones[UpperRightLeg] = Bone("RightUpperLeg", bones[Hips], new Vector3(.23f, -.04f, 0));
            bones[LowerRightLeg] = Bone("RightLowerLeg", bones[UpperRightLeg], new Vector3(0, -.52f, 0));
            bones[LeftFoot] = Bone("LeftFoot", bones[LowerLeftLeg], new Vector3(0, -.42f, .08f));
            bones[RightFoot] = Bone("RightFoot", bones[LowerRightLeg], new Vector3(0, -.42f, .08f));
        }

        void AddSurvivorMesh(int selected)
        {
            bool eva = selected == 1;
            var jacket = eva ? ZSRVisuals.RedClothTextured : ZSRVisuals.ClothTextured;
            var trousers = eva ? ZSRVisuals.Gunmetal : ZSRVisuals.Olive;
            var skin = eva ? ZSRVisuals.EvaSkin : ZSRVisuals.Skin;
            var pack = eva ? ZSRVisuals.RedJacket : ZSRVisuals.Olive;
            Part(eva ? "eva_jacket" : "max_vest", new Vector3(0, 1.50f, 0), Vector3.one, jacket, Spine);
            Part("max_vest", new Vector3(0, 1.57f, -.06f), new Vector3(.80f, .85f, .81f), ZSRVisuals.Armor, Chest);
            Part("lux_shoulder_armor", new Vector3(0, 1.73f, .28f),
                new Vector3(1.04f, 1.00f, .64f), ZSRVisuals.Armor, Chest);
            // Compact authored tactical pack. No over-sized first-aid suitcase, even on Eva.
            Part("lux_survival_backpack", new Vector3(0, 1.52f, -.41f),
                eva ? new Vector3(.73f, .79f, .75f) : new Vector3(.82f, .86f, .81f), pack, Spine);
            // Smaller pelvic mass: old oversized rounded armor made the body toy-like.
            Part("shoulder_shell", new Vector3(0, 1.02f, 0), new Vector3(.94f, .67f, .82f), trousers, Hips);
            Part("survivor_face", new Vector3(0, 2.28f, .01f), new Vector3(.88f, .90f, .87f), skin, Head);
            Part(eva ? "hair_pony" : "hair_short", new Vector3(0, 2.28f, -.02f), new Vector3(.93f, .89f, .94f), ZSRVisuals.Hair, Head);
            for (int s = -1; s <= 1; s += 2)
            {
                int upperArm = s < 0 ? UpperLeftArm : UpperRightArm;
                int lowerArm = s < 0 ? LowerLeftArm : LowerRightArm;
                int upperLeg = s < 0 ? UpperLeftLeg : UpperRightLeg;
                int lowerLeg = s < 0 ? LowerLeftLeg : LowerRightLeg;
                int foot = s < 0 ? LeftFoot : RightFoot;
                Part("lux_shoulder_armor", new Vector3(s * .47f, 1.82f, 0),
                    new Vector3(.74f, .66f, .78f), jacket, upperArm);
                Part("upper_arm", new Vector3(s * .52f, 1.53f, 0), new Vector3(1.12f, 1.0f, 1.06f), jacket, upperArm);
                Part("forearm", new Vector3(s * .54f, 1.11f, .09f), new Vector3(.99f, 1.04f, 1.01f), eva ? jacket : skin, lowerArm);
                Part("thigh", new Vector3(s * .23f, .69f, 0), new Vector3(1.04f, 1.12f, 1.04f), trousers, upperLeg);
                Part("calf", new Vector3(s * .23f, .30f, .02f), new Vector3(1.03f, .98f, 1.06f), trousers, lowerLeg);
                Part("tactical_boot", new Vector3(s * .23f, .14f, .15f), new Vector3(.84f, .78f, .92f), ZSRVisuals.Gunmetal, foot);
                Part("shoulder_shell", new Vector3(s * .29f, 1.39f, .28f), new Vector3(.36f, .47f, .31f), ZSRVisuals.OliveLight, Spine);
            }
        }

        void AddZombieMesh(ZombieType kind)
        {
            bool mutant = kind == ZombieType.Boss || kind == ZombieType.Brute;
            bool toxic = kind == ZombieType.Toxic;
            var flesh = toxic ? ZSRVisuals.ToxicSkin : mutant ? ZSRVisuals.ZombieSkinTextured : ZSRVisuals.ZombieSkin;
            var cloth = kind == ZombieType.Armored ? ZSRVisuals.Olive : mutant ? ZSRVisuals.ZombieSkinTextured : ZSRVisuals.ZombieCloth;
            float bulk = kind == ZombieType.Runner ? .80f : mutant ? 1.25f : .95f;
            string torso = kind == ZombieType.Boss ? "boss_core" : kind == ZombieType.Brute ? "brute_chest" : "walker_ribs";
            Part(torso, new Vector3(0, 1.50f, 0), new Vector3(bulk, .93f, bulk), cloth, Spine);
            Part("shoulder_shell", new Vector3(0, 1.02f, 0), new Vector3(1.15f, .92f, 1.11f), ZSRVisuals.ZombieDark, Hips);
            Part("infected_face", new Vector3(0, 2.24f, .01f), Vector3.one * (mutant ? 1.01f : .88f), flesh, Head);
            if (kind == ZombieType.Armored)
                Part("shoulder_shell", new Vector3(0, 1.61f, .27f), new Vector3(1.25f, .9f, .65f), ZSRVisuals.MetalWorn, Chest);
            if (kind == ZombieType.Toxic)
                Part("shoulder_shell", new Vector3(0, 1.62f, -.36f), new Vector3(1.17f, 1.3f, .76f), ZSRVisuals.ToxicSkin, Spine);
            for (int s = -1; s <= 1; s += 2)
            {
                int ua = s < 0 ? UpperLeftArm : UpperRightArm;
                int la = s < 0 ? LowerLeftArm : LowerRightArm;
                int ul = s < 0 ? UpperLeftLeg : UpperRightLeg;
                int ll = s < 0 ? LowerLeftLeg : LowerRightLeg;
                int foot = s < 0 ? LeftFoot : RightFoot;
                Part("lux_shoulder_armor", new Vector3(s * .48f, 1.81f, .01f),
                    new Vector3(mutant ? .96f : .73f, mutant ? .92f : .63f, mutant ? .94f : .80f), flesh, ua);
                Part("infected_arm", new Vector3(s * .55f, 1.52f, 0), new Vector3(mutant ? 1.17f : 1f, .87f, 1f), flesh, ua);
                Part("forearm", new Vector3(s * .58f, 1.06f, .10f), new Vector3(mutant ? 1.16f : .94f, .90f, .94f), flesh, la);
                Part("claw", new Vector3(s * .58f, .83f, .12f), new Vector3(.68f, .42f, .58f), flesh, la);
                Part("infected_leg", new Vector3(s * .24f, .68f, 0), new Vector3(.96f, .76f, .93f), ZSRVisuals.ZombieDark, ul);
                Part("calf", new Vector3(s * .24f, .31f, .04f), new Vector3(.95f, 1.0f, .94f), flesh, ll);
                Part("tactical_boot", new Vector3(s * .24f, .10f, .13f), new Vector3(.78f, .72f, .89f), ZSRVisuals.ZombieDark, foot);
                if (mutant) Part("mutant_spike", new Vector3(s * .38f, 2.01f, -.12f), new Vector3(.43f, .41f, .43f), ZSRVisuals.Blood, ua);
            }
        }

        void Part(string key, Vector3 pos, Vector3 scale, Material mat, int bone)
        {
            if (ReliableBoneRendering)
            {
                // The source OBJ MeshFilter renders without accessing mesh.vertices.
                // Calculate coordinates once in the bind pose so animation keeps
                // the piece correctly attached to its original bone.
                Vector3 bindLocalPosition = bones[bone].InverseTransformPoint(transform.TransformPoint(pos));
                var visiblePart = ZSRVisuals.MeshPart(key, "VISIBLE / " + key,
                    bones[bone], bindLocalPosition, scale, mat);
                if (visiblePart != null)
                {
                    var visibleRenderer = visiblePart.GetComponent<MeshRenderer>();
                    if (visibleRenderer != null)
                    {
                        visibleRenderer.shadowCastingMode = ShadowCastingMode.On;
                        visibleRenderer.receiveShadows = true;
                    }
                }
                return;
            }
            var source = LoadPart(key);
            if (source == null) return;
            var materialIndex = materials.IndexOf(mat);
            if (materialIndex < 0)
            {
                materialIndex = materials.Count;
                materials.Add(mat);
                indices.Add(new List<int>());
            }
            var transformMatrix = Matrix4x4.TRS(pos, Quaternion.identity, scale);
            int start = vertices.Count;
            var meshVertices = source.vertices;
            var meshNormals = source.normals;
            var meshUV = source.uv;
            for (int i = 0; i < meshVertices.Length; i++)
            {
                Vector3 vertex = transformMatrix.MultiplyPoint3x4(meshVertices[i]);
                vertices.Add(vertex);
                normals.Add(i < meshNormals.Length ? transformMatrix.MultiplyVector(meshNormals[i]).normalized : Vector3.up);
                uvs.Add(i < meshUV.Length ? meshUV[i] : Vector2.zero);
                int secondary = bone;
                float blend = 0f;
                // Two-bone weighting near major joints softens the elbow/knee/chest silhouette.
                if (bone == Spine) { secondary = Chest; blend = .70f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.44f, 1.94f, vertex.y)); }
                else if (bone == UpperLeftArm || bone == UpperRightArm)
                { secondary = bone == UpperLeftArm ? LowerLeftArm : LowerRightArm;
                  blend = .38f * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.26f, 1.48f, vertex.y))); }
                else if (bone == LowerLeftArm || bone == LowerRightArm)
                { secondary = bone == LowerLeftArm ? UpperLeftArm : UpperRightArm;
                  blend = .28f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.10f, 1.35f, vertex.y)); }
                else if (bone == UpperLeftLeg || bone == UpperRightLeg)
                { secondary = bone == UpperLeftLeg ? LowerLeftLeg : LowerRightLeg;
                  blend = .36f * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, .70f, vertex.y))); }
                else if (bone == LowerLeftLeg || bone == LowerRightLeg)
                { secondary = bone == LowerLeftLeg ? UpperLeftLeg : UpperRightLeg;
                  blend = .28f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.31f, .55f, vertex.y)); }
                weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f - blend, boneIndex1 = secondary, weight1 = blend });
            }
            var sourceTriangles = source.triangles;
            var dst = indices[materialIndex];
            for (int i = 0; i < sourceTriangles.Length; i++) dst.Add(start + sourceTriangles[i]);
        }

        void FinishMesh(string modelName)
        {
            // Each bone already owns its rendered OBJ parts. Generating an additional
            // SkinnedMeshRenderer here would recreate the invisible-actor bug.
            if (ReliableBoneRendering) return;
            var mesh = new Mesh { name = modelName };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            var bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindPoses[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            mesh.bindposes = bindPoses;
            mesh.subMeshCount = indices.Count;
            for (int i = 0; i < indices.Count; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateBounds();
            runtimeMesh = mesh;

            var rendererObject = new GameObject("ONE SKINNED MESH / skeleton animated");
            rendererObject.transform.SetParent(transform, false);
            var smr = rendererObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[Hips];
            smr.sharedMaterials = materials.ToArray();
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.localBounds = new Bounds(new Vector3(0,1.36f,0), new Vector3(6f,5f,6f));
        }

        void AttachAnimator(bool hero)
        {
            animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(hero ? "Animations/ZSRHero" : "Animations/ZSRZombie");
            if (animator.runtimeAnimatorController != null) animator.Play("Run", 0, 0);
        }

        public void SetRunningSpeed(float speed)
        {
            fallbackSpeed = Mathf.Clamp(speed, .55f, 1.8f);
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.speed = fallbackSpeed;
        }
        void Update()
        {
            // Animations are normally provided by the generated AnimatorController.
            // This deterministic pose fallback is used only if Unity has not generated it yet.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                if (hitReactionTimer > 0f)
                {
                    hitReactionTimer -= Time.deltaTime;
                    if (hitReactionTimer <= 0f && !fallbackDead)
                        animator.CrossFade("Run", .10f, 0);
                }
                return;
            }
            if (fallbackDead) return;
            float frame = ZSRGame.Instance != null ? ZSRGame.Instance.FrameStep : Mathf.Min(Time.deltaTime, .05f);
            // Biomechanical fallback for built-in OBJ characters. The two knees
            // bend only in swing phase; toes roll in opposition, so feet do not
            // look glued to rigid poles. True retargeted mocap remains preferable.
            fallbackPhase += frame * (IsHero ? 10.7f : 8.3f) * fallbackSpeed;
            float gait = Mathf.Sin(fallbackPhase);
            float leftLift = Mathf.Max(0f, -gait);
            float rightLift = Mathf.Max(0f, gait);
            float compression = Mathf.Abs(gait);
            bones[Hips].localPosition = new Vector3(0f,
                1.03f + compression * (IsHero ? .028f : .021f), 0f);
            bones[Hips].localRotation = Quaternion.Euler(IsHero ? 3.2f : 8f, gait * 2.0f, gait * 1.6f);
            bones[Spine].localRotation = Quaternion.Euler(IsHero ? -1.5f : 7f,
                gait * 2.8f, -gait * 2.0f);
            bones[Chest].localRotation = Quaternion.Euler(IsHero ? -3f : 9f,
                -gait * 2.2f, gait * 1.3f);
            bones[Head].localRotation = Quaternion.Euler(-1.4f,
                Mathf.Sin(fallbackPhase * .5f) * 2.4f, 0f);
            float stride = IsHero ? 37f : 31f;
            bones[UpperLeftLeg].localRotation = Quaternion.Euler(gait * stride, 0, 0);
            bones[UpperRightLeg].localRotation = Quaternion.Euler(-gait * stride, 0, 0);
            bones[LowerLeftLeg].localRotation = Quaternion.Euler(leftLift * (IsHero ? 53f : 43f), 0, 0);
            bones[LowerRightLeg].localRotation = Quaternion.Euler(rightLift * (IsHero ? 53f : 43f), 0, 0);
            bones[LeftFoot].localRotation = Quaternion.Euler(-gait * 13f - leftLift * 9f,0,0);
            bones[RightFoot].localRotation = Quaternion.Euler(gait * 13f - rightLift * 9f,0,0);
            if (IsHero)
            {
                // Arms hold a weapon forward with bent elbows instead of
                // flailing in a T-pose. The weapon is anchored to chest/hand rig.
                bones[UpperLeftArm].localRotation = Quaternion.Euler(-22f - gait * 4.5f,0,-9f);
                bones[UpperRightArm].localRotation = Quaternion.Euler(-22f + gait * 4.5f,0,9f);
                bones[LowerLeftArm].localRotation = Quaternion.Euler(-37f,0,0);
                bones[LowerRightArm].localRotation = Quaternion.Euler(-37f,0,0);
            }
            else
            {
                bones[UpperLeftArm].localRotation = Quaternion.Euler(-17f - gait * 15f,0,-10f);
                bones[UpperRightArm].localRotation = Quaternion.Euler(-11f + gait * 12f,0,8f);
                bones[LowerLeftArm].localRotation = Quaternion.Euler(-16f - leftLift * 8f,0,0);
                bones[LowerRightArm].localRotation = Quaternion.Euler(-28f - rightLift * 8f,0,0);
            }
        }
        public void Fire()
        {
            if (muzzleFlash == null) return;
            muzzleFlash.SetActive(true);
            CancelInvoke("HideFlash");
            Invoke("HideFlash", .055f);
        }
        void HideFlash() { if (muzzleFlash != null) muzzleFlash.SetActive(false); }
        public void Hit()
        {
            if (fallbackDead) return;
            if (animator != null && animator.runtimeAnimatorController != null &&
                animator.HasState(0, Animator.StringToHash("Hit")))
            {
                animator.CrossFade("Hit", .045f, 0);
                hitReactionTimer = .22f;
            }
            else if (bones[Chest] != null)
                bones[Chest].localRotation = Quaternion.Euler(-9f, 0f, -5f);
        }
        public void PlayRun()
        {
            fallbackDead = false;
            hitReactionTimer = 0f;
            fallbackPhase = 0f;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.speed = 1f;
                animator.Play("Run", 0, 0f);
            }
        }
        void OnDestroy()
        {
            CancelInvoke();
            if (runtimeMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(runtimeMesh);
                else Destroy(runtimeMesh);
#else
                Destroy(runtimeMesh);
#endif
            }
        }
        public void Die()
        {
            fallbackDead = true;
            hitReactionTimer = 0f;
            if (animator == null || animator.runtimeAnimatorController == null)
                bones[Hips].localRotation = Quaternion.Euler(65f, 0, -22f);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.speed = 1f;
                animator.CrossFade("Death", .08f, 0);
            }
        }
    }
}
