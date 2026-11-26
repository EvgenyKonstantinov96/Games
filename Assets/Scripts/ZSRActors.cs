using UnityEngine;

namespace ZombieSurvivalRun
{
    public enum ZombieType { Walker, Runner, Armored, Brute, Toxic, Crawler, Boss }
    public enum WeaponType { Pistol, Rifle, Shotgun, Minigun }

    /// <summary>Art-direction v0.7: actual subdivided OBJ body parts and articulated, soft running motion.</summary>
    public sealed class ZSRHero : MonoBehaviour
    {
        public float TargetX;
        public float PositionX => transform.position.x;
        public int MaxHealth { get; private set; } = 132;
        public int Health { get; set; } = 132;
        public int Selection { get; private set; }
        public Vector3 Muzzle => production != null && production.MuzzlePoint != null
            ? production.MuzzlePoint.position
            : weapon != null ? weapon.TransformPoint(new Vector3(0,.05f,.97f))
            : transform.position + Vector3.up * 1.6f + Vector3.forward;

        Transform body, hips, torso, head, leftHip, rightHip, leftKnee, rightKnee, leftShoulder, rightShoulder, leftElbow, rightElbow, weapon;
        GameObject flash, model;
        ZSRSkinnedArt rigged;
        ZSRProductionArt production;
        float stride, recoil, muzzleTime, velocityX, hitT, itemTime;
        public bool IsUsingItem => itemTime > 0f;
        public Vector3 ItemOrigin => production != null && production.ItemHand != null
            ? production.ItemHand.position + transform.forward * .055f
            : transform.TransformPoint(new Vector3(-.32f, 1.30f, .23f));

        public void Build(int selection, WeaponType gun)
        {
            Selection = selection;
            if (model != null) Destroy(model);
            bool female = selection == 1;
            model = new GameObject(female ? "EVA / skeletal character" : "MAX / skeletal character");
            model.transform.SetParent(transform,false);
            production = ZSRProductionArt.CreateHero(model.transform, selection, gun);
            if (production != null)
            {
                body = production.transform;
                weapon = production.WeaponAnchor;
                flash = null;
                rigged = null;
                return; // Genuine imported rigged model; old procedural mesh is not created.
            }
            rigged = ZSRSkinnedArt.CreateHero(model.transform, selection, gun);
            if (rigged != null && rigged.IsWorking)
            {
                body = rigged.transform;
                weapon = rigged.WeaponAnchor;
                flash = null;
                return; // FULL SKINNED RIG supersedes legacy segmented character.
            }
            model.transform.localScale = female ? new Vector3(.77f,.79f,.77f) : Vector3.one*.82f;
            body = ZSRVisuals.Pivot("animated waist origin",model.transform,Vector3.zero);
            hips = ZSRVisuals.Pivot("pelvis rig",body,Vector3.zero);
            torso = ZSRVisuals.Pivot("chest rig",body,Vector3.zero);
            var suit = female ? ZSRVisuals.RedClothTextured : ZSRVisuals.ClothTextured;
            var trousers = female ? ZSRVisuals.Gunmetal : ZSRVisuals.Olive;
            var flesh = female ? ZSRVisuals.EvaSkin : ZSRVisuals.Skin;

            // A high-resolution single, shaped, UV-mapped torso mesh. No torso cubes are used.
            ZSRVisuals.MeshPart(female ? "eva_jacket" : "max_vest", "fabric torso 3D", torso,
                new Vector3(0,1.49f,0),Vector3.one,suit);
            ZSRVisuals.MeshPart("lux_shoulder_armor","sculpted chest protection",torso,
                new Vector3(0,1.62f,.27f),new Vector3(.98f,1.03f,.90f),ZSRVisuals.Armor);
            ZSRVisuals.MeshPart("lux_survival_backpack","compact survival backpack",torso,
                new Vector3(0,1.43f,-.45f),new Vector3(.70f,.71f,.66f), female ? ZSRVisuals.RedJacket : ZSRVisuals.Olive);
            ZSRVisuals.Capsule("soft hip mass",hips,new Vector3(0,1.03f,0),new Vector3(.46f,.25f,.38f),trousers,new Vector3(0,0,90));
            ZSRVisuals.Capsule("neck",torso,new Vector3(0,2.08f,.02f),new Vector3(.18f,.23f,.18f),flesh);
            for(int side=-1;side<=1;side+=2)
            {
                ZSRVisuals.MeshPart("lux_shoulder_armor","fitted shoulder protection",torso,
                    new Vector3(side*.47f,1.84f,0),new Vector3(.64f,.66f,.78f),female?ZSRVisuals.RedJacket:ZSRVisuals.Armor);
                ZSRVisuals.Capsule("thigh cargo pocket",hips,
                    new Vector3(side*.34f,.87f,.11f),new Vector3(.13f,.23f,.12f),ZSRVisuals.Olive);
                ZSRVisuals.Capsule("vest utility webbing",torso,
                    new Vector3(side*.22f,1.55f,.30f),new Vector3(.12f,.27f,.10f),ZSRVisuals.Gunmetal);
            }
            head = ZSRVisuals.Pivot("neck and head bone",torso,new Vector3(0,2.20f,.03f));
            ZSRVisuals.MeshPart("survivor_face","sculpted human head",head,
                new Vector3(0,.08f,0),new Vector3(.86f,.87f,.87f),flesh);
            ZSRVisuals.MeshPart(female?"hair_pony":"hair_short","hair style",head,
                new Vector3(0,.06f,-.02f),new Vector3(.92f,.90f,.92f),ZSRVisuals.Hair);
            for(int s=-1;s<=1;s+=2)
            {
                ZSRVisuals.Sphere("dark pupil",head,new Vector3(s*.145f,.145f,.262f),new Vector3(.038f,.043f,.027f),ZSRVisuals.Black);
                ZSRVisuals.Capsule("brow",head,new Vector3(s*.145f,.221f,.256f),new Vector3(.10f,.031f,.03f),ZSRVisuals.Hair,new Vector3(0,0,90));
            }
            leftHip=Leg(-1,trousers,out leftKnee);rightHip=Leg(1,trousers,out rightKnee);
            leftShoulder=Arm(-1,suit,flesh,out leftElbow);rightShoulder=Arm(1,suit,flesh,out rightElbow);
            weapon=ZSRVisuals.Pivot("weapon rig",torso,new Vector3(.10f,1.48f,.52f));
            ZSRVisuals.Rifle(weapon,gun);
            flash=ZSRVisuals.Sphere("muzzle flash",weapon,new Vector3(0,.05f,.99f),new Vector3(.17f,.19f,.28f),ZSRVisuals.OrangeGlow);
            flash.SetActive(false);
            ResetPose();
        }

        Transform Leg(int side,Material trousers,out Transform knee)
        {
            Transform hip=ZSRVisuals.Pivot(side<0?"LEFT LEG hip":"RIGHT LEG hip",hips,new Vector3(side*.23f,1.02f,0));
            ZSRVisuals.MeshPart("thigh","sculpted trouser thigh",hip,new Vector3(0,-.30f,0),new Vector3(1.00f,1.04f,1.00f),trousers);
            knee=ZSRVisuals.Pivot("knee bone",hip,new Vector3(0,-.63f,0));
            ZSRVisuals.MeshPart("calf","sculpted calf",knee,new Vector3(0,-.29f,0),new Vector3(1.03f,.96f,1.02f),trousers);
            ZSRVisuals.MeshPart("tactical_boot","rounded tactical shoe",knee,new Vector3(0,-.65f,.065f),new Vector3(.78f,.67f,.78f),ZSRVisuals.Gunmetal);
            ZSRVisuals.Sphere("knee guard",knee,new Vector3(0,-.03f,.14f),new Vector3(.17f,.14f,.10f),ZSRVisuals.Armor);
            return hip;
        }
        Transform Arm(int side,Material suit,Material flesh,out Transform elbow)
        {
            Transform shoulder=ZSRVisuals.Pivot(side<0?"LEFT ARM shoulder":"RIGHT ARM shoulder",torso,new Vector3(side*.51f,1.88f,0));
            ZSRVisuals.MeshPart("upper_arm","anatomical upper arm",shoulder,new Vector3(0,-.31f,.02f),new Vector3(1.01f,.91f,.98f),suit);
            elbow=ZSRVisuals.Pivot("elbow bone",shoulder,new Vector3(0,-.58f,.04f));
            ZSRVisuals.MeshPart("forearm","forearm detailed",elbow,new Vector3(0,-.25f,.12f),new Vector3(1.03f,.93f,1.02f),flesh);
            ZSRVisuals.MeshPart("claw","gloved fingers",elbow,new Vector3(0,-.51f,.19f),new Vector3(.76f,.35f,.60f),ZSRVisuals.Black,new Vector3(180,0,0));
            return shoulder;
        }
        void ResetPose()
        {
            if(leftShoulder!=null) leftShoulder.localEulerAngles=new Vector3(-13,0,-9);
            if(rightShoulder!=null) rightShoulder.localEulerAngles=new Vector3(-13,0,9);
            if(leftElbow!=null) leftElbow.localEulerAngles=new Vector3(-22,0,0);
            if(rightElbow!=null) rightElbow.localEulerAngles=new Vector3(-22,0,0);
        }
        public void ResetHero(int selection,WeaponType weaponType)
        {
            Build(selection,weaponType);
            MaxHealth=selection==0?132:112;Health=MaxHealth;
            TargetX=0;transform.position=Vector3.zero;velocityX=0;stride=0;recoil=0;hitT=0;itemTime=0;
        }
        public void Fire(Vector3 aim)
        {
            recoil = .14f;
            muzzleTime = .066f;
            if (production != null) production.Fire(aim);
            else if (rigged != null) rigged.Fire();
            else if (flash != null) flash.SetActive(true);
        }

        public void UseItem(bool medkit)
        {
            itemTime = medkit ? .8f : .6f;
            if (production != null) production.UseItem(medkit);
        }
        public void Hit(){hitT=.20f;if(production!=null)production.Hit();}
        public void Reload(float seconds) { if (production != null) production.Reload(seconds); }
        public void Die() { if (production != null) production.Die(); }
        void Update()
        {
            var g=ZSRGame.Instance;
            if(g==null||body==null)return;
            float dt=g.FrameStep;
            bool running=g.IsRunning;
            if (running && itemTime > 0f) itemTime = Mathf.Max(0f, itemTime - dt);
            float gaitRate = (Selection == 0 ? 1f : 1.07f) * g.RunPaceMultiplier;
            if (production != null)
            {
                if (running)
                {
                    Vector3 p = transform.position;
                    p.x = Mathf.SmoothDamp(p.x, TargetX, ref velocityX, .14f, 21f, dt);
                    transform.position = p;
                }
                production.Tick(running, gaitRate);
                float steering = running ? velocityX + (TargetX - transform.position.x) * 1.7f : 0f;
                production.SetLateralLean(steering, dt);
                hitT = Mathf.MoveTowards(hitT, 0, dt * 3.2f);
                return;
            }
            if (rigged != null)
            {
                if (running)
                {
                    Vector3 p = transform.position;
                    p.x = Mathf.SmoothDamp(p.x, TargetX, ref velocityX, .14f, 21f, dt);
                    transform.position = p;
                }
                rigged.transform.localEulerAngles = new Vector3(0, 0,
                    Mathf.Clamp(-(velocityX + (TargetX - transform.position.x) * 1.5f) * 1.48f, -11f, 11f) + hitT * 4f);
                if (running) rigged.SetRunningSpeed(gaitRate);
                hitT = Mathf.MoveTowards(hitT, 0, dt * 3.2f);
                return;
            }
            if(running)
            {
                var p=transform.position;
                p.x=Mathf.SmoothDamp(p.x,TargetX,ref velocityX,.14f,21f,dt);
                transform.position=p;
                stride += dt * (Selection == 0 ? 10.2f : 11.2f) * g.RunPaceMultiplier;
            }
            float gait=running?Mathf.Sin(stride):0f;
            float leftLift=running?Mathf.Max(0,-gait):0f;
            float rightLift=running?Mathf.Max(0,gait):0f;
            body.localPosition=new Vector3(0,running?(.022f+Mathf.Abs(gait)*.060f):0,0);
            float yaw=running?Mathf.Sin(stride*.5f)*2.6f:0f;
            body.localEulerAngles=new Vector3(running?5.4f:0,yaw,Mathf.Clamp(-velocityX*2.5f,-13,13)+hitT*7f);
            hips.localEulerAngles=new Vector3(0,0,gait*3f);
            torso.localEulerAngles=new Vector3(-2+Mathf.Abs(gait)*2,0,-gait*2.8f);
            leftHip.localEulerAngles=new Vector3(gait*35f,0,0);
            rightHip.localEulerAngles=new Vector3(-gait*35f,0,0);
            leftKnee.localEulerAngles=new Vector3(leftLift*51f,0,0);
            rightKnee.localEulerAngles=new Vector3(rightLift*51f,0,0);
            leftShoulder.localEulerAngles=new Vector3(-13f-gait*16f,0,-9);
            rightShoulder.localEulerAngles=new Vector3(-13f+gait*16f,0,9);
            leftElbow.localEulerAngles=new Vector3(-28f-leftLift*6,0,0);
            rightElbow.localEulerAngles=new Vector3(-28f-rightLift*6,0,0);
            head.localEulerAngles=new Vector3(-2f+Mathf.Abs(gait)*1.4f,Mathf.Sin(stride*.5f)*2f,0);
            recoil=Mathf.MoveTowards(recoil,0,dt*3.6f);
            weapon.localPosition=new Vector3(.10f,1.48f,.52f-recoil);
            muzzleTime-=dt;if(flash!=null && muzzleTime<=0 && flash.activeSelf)flash.SetActive(false);
            hitT=Mathf.MoveTowards(hitT,0,dt*3.2f);
        }
    }

    public sealed class ZSRZombie : MonoBehaviour
    {
        public ZombieType Kind { get; private set; }
        public bool Alive { get; private set; }
        public float HealthFraction => Mathf.Clamp01(hitPoints / maxHitPoints);
        public float DistanceZ => transform.position.z;
        public float X => transform.position.x;
        public Vector3 AimPoint => transform.position + Vector3.up * (Kind == ZombieType.Boss ? 3.2f : Kind == ZombieType.Brute ? 2.6f : Kind==ZombieType.Crawler ? 0.85f:1.55f);

        Transform body, torso, armL, armR, legL, legR, head;
        ZSRSkinnedArt rigged;
        ZSRProductionArt production;
        float hitPoints, maxHitPoints, walkSpeed, phase, clock, stagger, deathTime, attackTimer;
        bool contacted, attackTelegraphed;
        int themeLevel;

        public void Construct(ZombieType type)
        {
            Kind=type;gameObject.name="INFECTED / "+type;
            float scale=type==ZombieType.Boss?2.60f:type==ZombieType.Brute?1.55f:type==ZombieType.Armored?1.14f:
                type==ZombieType.Runner?.97f:type==ZombieType.Crawler?.70f:type==ZombieType.Toxic?1.09f:1f;
            transform.localScale=Vector3.one*scale;
            production = ZSRProductionArt.CreateZombie(transform, type);
            if (production != null)
            {
                body = production.transform;
                torso = body;
                rigged = null;
                phase = Random.Range(0f, 6.283f);
                return; // Imported full character mesh with native Avatar and Animator.
            }
            rigged = ZSRSkinnedArt.CreateZombie(transform, type);
            if (rigged != null && rigged.IsWorking)
            {
                body = rigged.transform;
                torso = body;
                phase=Random.Range(0f, 6.283f);
                return; // Entire infected body uses ONE skinned mesh and Animator.
            }
            body=ZSRVisuals.Pivot("zombie animated body",transform,Vector3.zero);
            torso=ZSRVisuals.Pivot("zombie chest bone",body,Vector3.zero);
            var skin=type==ZombieType.Toxic?ZSRVisuals.ToxicSkin: type==ZombieType.Boss || type==ZombieType.Brute ? ZSRVisuals.ZombieSkinTextured:ZSRVisuals.ZombieSkin;
            var cloth=type==ZombieType.Armored?ZSRVisuals.Olive:
                type==ZombieType.Boss || type==ZombieType.Brute ? ZSRVisuals.MutantBossMaterial:ZSRVisuals.ZombieCloth;
            string bodyMesh=type==ZombieType.Boss?"boss_core":type==ZombieType.Brute?"brute_chest":"walker_ribs";
            ZSRVisuals.MeshPart(bodyMesh,"sculpted infected chest",torso,new Vector3(0,1.50f,0),Vector3.one,cloth);
            ZSRVisuals.Capsule("torn waist",torso,new Vector3(0,1.08f,0),new Vector3(.43f,.22f,.34f),cloth,new Vector3(0,0,90));
            for(int side=-1;side<=1;side+=2)
            {
                if(type==ZombieType.Boss||type==ZombieType.Brute)
                {
                    ZSRVisuals.MeshPart("shoulder_shell","deformed bone shoulder",torso,new Vector3(side*.62f,1.89f,0),
                        new Vector3(1.18f,1.05f,1.1f),skin);
                    for(int spike=0;spike<3;spike++)
                        ZSRVisuals.MeshPart("mutant_spike","spinal protrusion",torso,
                            new Vector3(side*(.22f+spike*.13f),1.91f,-.26f),new Vector3(.30f,.42f,.27f),ZSRVisuals.Blood,
                            new Vector3(29+spike*11,0,side*20));
                }
                if(type==ZombieType.Armored)
                    ZSRVisuals.MeshPart("shoulder_shell","old riot armor",torso,new Vector3(side*.43f,1.78f,.02f),
                        new Vector3(.95f,.85f,.95f),ZSRVisuals.Armor);
                if(type==ZombieType.Toxic)
                    ZSRVisuals.Sphere("bioluminescent infection",torso,new Vector3(side*.21f,1.57f,.29f),
                        new Vector3(.16f,.20f,.09f),ZSRVisuals.ToxicGlow);
            }
            if(type==ZombieType.Armored)
            {
                ZSRVisuals.MeshPart("shoulder_shell","broken front breastplate",torso,new Vector3(0,1.52f,.35f),
                    new Vector3(1.20f,.60f,.50f),ZSRVisuals.MetalWorn);
            }
            if(type==ZombieType.Toxic)
            {
                ZSRVisuals.Sphere("pulsing toxin sack",torso,new Vector3(0,1.64f,-.30f),new Vector3(.31f,.38f,.21f),ZSRVisuals.ToxicGlow);
            }
            head=ZSRVisuals.Pivot("zombie cervical spine",torso,new Vector3(0,2.14f,.025f));
            ZSRVisuals.MeshPart("infected_face","anatomical infected skull",head,new Vector3(0,.04f,0),
                new Vector3(.86f,.86f,.86f),skin);
            for(int side=-1;side<=1;side+=2)
            {
                ZSRVisuals.Sphere("glowing eye",head,new Vector3(side*.12f,.11f,.255f),new Vector3(.055f,.046f,.031f),
                    type==ZombieType.Toxic?ZSRVisuals.ToxicGlow:ZSRVisuals.RedGlow);
            }
            if(type==ZombieType.Boss)
                ZSRVisuals.MeshPart("mutant_spike","crown mutant spike",head,new Vector3(0,.43f,-.08f),
                    new Vector3(.43f,.43f,.43f),ZSRVisuals.Blood);
            armL=ZombieArm(-1,skin);armR=ZombieArm(1,skin);
            legL=ZombieLeg(-1,cloth,skin);legR=ZombieLeg(1,cloth,skin);
            phase=Random.Range(0f,6.283f);
        }
        Transform ZombieArm(int side,Material skin)
        {
            var pivot=ZSRVisuals.Pivot(side<0?"left infected arm":"right infected arm",torso,new Vector3(side*.50f,1.88f,0));
            ZSRVisuals.MeshPart("infected_arm","rotting biceps and forearm",pivot,new Vector3(0,-.54f,.13f),
                Vector3.one,skin);
            ZSRVisuals.MeshPart("claw","infected fingers",pivot,new Vector3(0,-1.04f,.22f),new Vector3(.72f,.58f,.74f),skin,new Vector3(180,0,0));
            return pivot;
        }
        Transform ZombieLeg(int side,Material cloth,Material skin)
        {
            var pivot=ZSRVisuals.Pivot(side<0?"left infected leg":"right infected leg",body,new Vector3(side*.23f,1.03f,0));
            ZSRVisuals.MeshPart("infected_leg","tattered leg musculature",pivot,new Vector3(0,-.45f,0),Vector3.one,cloth);
            ZSRVisuals.MeshPart("tactical_boot","ragged shoe",pivot,new Vector3(0,-.91f,.08f),new Vector3(.68f,.50f,.65f),ZSRVisuals.ZombieDark);
            return pivot;
        }

        public void Activate(float x, float z, int level)
        {
            themeLevel = level;
            transform.position = new Vector3(x, 0, z);
            transform.rotation = Quaternion.Euler(0, 180, 0);
            maxHitPoints = Kind == ZombieType.Boss ? 880f + Mathf.Max(0, level - 5) * 48f :
                           Kind == ZombieType.Brute ? 170f + level * 7f :
                           Kind == ZombieType.Armored ? 98f + level * 3f :
                           Kind == ZombieType.Runner ? 34f + level * 1.3f :
                           Kind == ZombieType.Toxic ? 62f + level * 2.2f :
                           Kind == ZombieType.Crawler ? 41f + level * 1.6f :
                           54f + level * 1.8f;
            hitPoints = maxHitPoints;
            walkSpeed = Kind == ZombieType.Boss ? 0f : Kind == ZombieType.Brute ? .78f : Kind == ZombieType.Runner ? 3.0f :
                Kind==ZombieType.Crawler?2.6f:Kind==ZombieType.Toxic ? 0.55f:Kind == ZombieType.Armored ? 1.15f : 1.48f;
            Alive = true; contacted = false; attackTelegraphed = false; stagger = 0; deathTime = 0; clock = phase; attackTimer = 2f;
            if(body!=null){body.localEulerAngles=Vector3.zero;body.localPosition=Vector3.zero;}
            if (rigged != null) {rigged.transform.localPosition = Vector3.zero;rigged.transform.localRotation = Quaternion.identity;}
            if(torso!=null)torso.localEulerAngles=Vector3.zero;
            gameObject.SetActive(true);
            if (rigged != null) rigged.PlayRun();
            if (production != null) production.PlayRun(phase / (2f * Mathf.PI));
        }

        public void TakeHit(int damage, Vector3 source)
        {
            if (!Alive) return;
            hitPoints -= damage;
            stagger = Mathf.Min(1f, stagger + .40f);
            ZSRGame.Instance.SpawnImpact(AimPoint, Kind == ZombieType.Armored);
            if (hitPoints > 0)
            {
                // An actual hit reaction must be played for living enemies, not only death VFX.
                if (production != null) production.Hit();
                if (rigged != null) rigged.Hit();
            }
            if (hitPoints <= 0)
            {
                Alive = false;
                deathTime = .8f;
                if (rigged != null) rigged.Die();
                if (production != null) production.Die();
                ZSRGame.Instance.OnZombieKilled(this);
            }
        }

        void Update()
        {
            if (ZSRGame.Instance == null || !ZSRGame.Instance.IsRunning || body == null) return;
            float dt = ZSRGame.Instance.FrameStep;
            if (!Alive)
            {
                deathTime -= dt;
                float t = 1 - Mathf.Clamp01(deathTime / .8f);
                if (rigged == null && production == null) body.localEulerAngles = new Vector3(t * 75, 0, t * 54);
                transform.position += Vector3.back * (ZSRGame.Instance.RunSpeed * .52f) * dt;
                if (deathTime <= 0) ZSRGame.Instance.RecycleZombie(this);
                return;
            }

            if (production != null)
            {
                float rate = Kind == ZombieType.Runner ? 1.53f : Kind == ZombieType.Crawler ? 1.18f :
                    Kind == ZombieType.Brute ? .69f : Kind == ZombieType.Armored ? .82f :
                    Kind == ZombieType.Boss ? .60f : Kind == ZombieType.Toxic ? .91f : .97f;
                production.Tick(true, rate);
                production.SetZombieMovementStyle(Kind);
            }
            else if (rigged != null)
            {
                rigged.SetRunningSpeed(Kind == ZombieType.Runner ? 1.46f : Kind == ZombieType.Brute ? .76f : Kind == ZombieType.Boss ? .67f : Kind == ZombieType.Crawler ? 1.23f : 1f);
                if (Kind == ZombieType.Crawler)
                {
                    rigged.transform.localPosition = new Vector3(0, -.32f, 0);
                    rigged.transform.localEulerAngles = new Vector3(56f, 0, 0);
                }
            }
            else if (production == null)
            {
            clock += dt * (Kind == ZombieType.Runner ? 15f : Kind == ZombieType.Boss ? 3.8f : 8.5f);
            float gait = Mathf.Sin(clock);
            legL.localEulerAngles = new Vector3(gait * 32f, 0, 0);
            legR.localEulerAngles = new Vector3(-gait * 32f, 0, 0);
            // Relaxed, asymmetric infected arms instead of a permanent 'hands up' pose.
            float armSwing = Kind == ZombieType.Runner ? 27f : Kind == ZombieType.Brute ? 9f : 18f;
            armL.localEulerAngles = new Vector3(-9f + gait * armSwing, 0, -8);
            armR.localEulerAngles = new Vector3(-15f - gait * armSwing * .7f, 0, 11);
            head.localEulerAngles = new Vector3(-5, Mathf.Sin(clock * .4f) * 12, 0);
            stagger = Mathf.MoveTowards(stagger, 0, dt * 3.8f);
            body.localPosition = new Vector3(0, Mathf.Abs(gait) * .045f, 0);
            torso.localEulerAngles = Kind==ZombieType.Crawler?
                new Vector3(62f-stagger*16f,gait*7f,stagger*9f):
                new Vector3(6 - stagger * 35, gait * 5, stagger * 23);
            if(Kind==ZombieType.Crawler)body.localPosition=new Vector3(0,-.34f+Mathf.Abs(gait)*.04f,0);
            }

            Vector3 p = transform.position;
            p.z -= (Kind == ZombieType.Boss ? 2.5f : ZSRGame.Instance.RunSpeed + walkSpeed) * dt;
            float dx = ZSRGame.Instance.Hero.PositionX - p.x;
            p.x += Mathf.Clamp(dx, -1, 1) * dt * (Kind == ZombieType.Runner ? 1.9f : Kind == ZombieType.Boss ? .38f : .84f);
            if (Kind == ZombieType.Boss && p.z < 12f) p.z = 12f;
            transform.position = p;

            if(Kind==ZombieType.Toxic && p.z>1.25f && p.z<13f)
            {
                attackTimer-=dt;
                TelegraphAttack(.25f);
                if(attackTimer<=0)
                {
                    attackTimer=3.1f;
                    attackTelegraphed = false;
                    if(Mathf.Abs(dx)<2.2f)
                    {
                        ZSRGame.Instance.HurtPlayer(8);
                        ZSRVisuals.Explosion(AimPoint,.95f,ZSRVisuals.ToxicGlow,.3f);
                    }
                }
            }
            if (Kind == ZombieType.Boss && p.z <= 12.1f)
            {
                const float strikeWidth = 2.9f;
                const float windup = .49f;
                var game = ZSRGame.Instance;
                attackTimer -= dt;
                TelegraphAttack(windup);
                if (attackTimer > 0f && attackTimer <= windup)
                    game.WarnBossStrike(p.x, p.z, strikeWidth, 1f - attackTimer / windup);
                if (attackTimer <= 0)
                {
                    attackTimer = Mathf.Max(2.5f, 3.4f - themeLevel * .03f);
                    attackTelegraphed = false;
                    game.BossStrike(p.x, p.z - .72f);
                    if (Mathf.Abs(p.x - game.Hero.PositionX) < strikeWidth) game.HurtPlayer(themeLevel >= 15 ? 18 : 15);
                    else game.Notify("УДАР МУТАНТА МИМО", .9f);
                }
            }
            if (Kind != ZombieType.Boss && Kind != ZombieType.Toxic && p.z > 1f &&
                p.z <= 1f + (ZSRGame.Instance.RunSpeed + walkSpeed) * .32f &&
                Mathf.Abs(dx) < 1.25f && !attackTelegraphed)
            {
                attackTelegraphed = true;
                if (production != null) production.Attack();
            }
            // Physical contact requires both depth overlap and a matching lane.
            // Previously every zombie dealt damage when its Z reached 1, even across the road.
            if (Kind != ZombieType.Boss && p.z <= 1.0f && p.z >= -.85f && !contacted &&
                Mathf.Abs(p.x - ZSRGame.Instance.Hero.PositionX) < (Kind == ZombieType.Brute ? 1.23f : .78f))
            {
                contacted = true;
                ZSRGame.Instance.HurtPlayer(Kind == ZombieType.Brute ? 25 : Kind == ZombieType.Armored ? 16 : Kind == ZombieType.Runner ? 12 : 8);
                Alive = false; deathTime = .6f;
                if (production != null) production.Die();
                if (rigged != null) rigged.Die();
            }
            // Enemies that missed the player must leave the active list and return to the pool.
            // Without this, after a few waves the 44-enemy cap stopped all subsequent spawns.
            if (Kind != ZombieType.Boss && p.z < -8f)
                ZSRGame.Instance.RecycleZombie(this);
        }

        void TelegraphAttack(float leadTime)
        {
            if (attackTimer > leadTime || attackTelegraphed) return;
            attackTelegraphed = true;
            if (production != null) production.Attack();
        }
    }
}
