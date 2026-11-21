using UnityEngine;

namespace ZombieSurvivalRun
{
    public enum BonusKind { DoubleDamage, RapidFire, Shield, Heal, Magnet, Turret }

    public sealed class ZSRRoadChunk : MonoBehaviour
    {
        const float Length = 46f;
        int level, chapter;

        public void Build(int index, int sourceLevel)
        {
            level = sourceLevel;
            chapter = Mathf.Clamp((sourceLevel - 1) / 5, 0, 2);
            transform.position = new Vector3(0, 0, -23 + Length * index);
            ZSRVisuals.Box("dark asphalt road", transform, new Vector3(0, -.25f, 0), new Vector3(11f, .45f, Length + .1f), ZSRVisuals.AsphaltDeluxe);
            ZSRVisuals.Box("road shoulder left", transform, new Vector3(-4.8f, -.18f, 0), new Vector3(.45f, .15f, Length), ZSRVisuals.ConcreteDark);
            ZSRVisuals.Box("road shoulder right", transform, new Vector3(4.8f, -.18f, 0), new Vector3(.45f, .15f, Length), ZSRVisuals.ConcreteDark);
            for (int side = -1; side <= 1; side += 2)
            {
                ZSRVisuals.Box("road edge stripe", transform, new Vector3(side * 4.95f, -.010f, 0), new Vector3(.09f, .012f, Length), ZSRVisuals.White);
                ZSRVisuals.Box("sidewalk", transform, new Vector3(side * 6.55f, -.24f, 0), new Vector3(2.4f, .48f, Length), ZSRVisuals.ConcreteDeluxe);
                ZSRVisuals.Box("steel guardrail", transform, new Vector3(side * 5.57f, .70f, 0), new Vector3(.13f, .17f, Length), ZSRVisuals.Steel);
                ZSRVisuals.Box("guardrail lower", transform, new Vector3(side * 5.56f, .22f, 0), new Vector3(.12f, .12f, Length), ZSRVisuals.Armor);
                for (int k = 0; k < 11; k++)
                {
                    float z = -22f + k * 4.3f;
                    ZSRVisuals.Box("rail mounting", transform, new Vector3(side * 5.57f, .35f, z),
                        new Vector3(.14f, 1.3f, .18f), ZSRVisuals.MetalWorn);
                    if (k % 3 == 1)
                        ZSRVisuals.Box("amber guide reflector", transform, new Vector3(side * 5.47f, .59f, z),
                            new Vector3(.07f, .09f, .58f), ZSRVisuals.OrangeGlow);
                }
                for (int b = 0; b < 2; b++) BuildBuilding(side, b * 2 + (index % 2));
                for (int lamp = 0; lamp < 2; lamp++) BuildLamp(side, -12f + lamp * 24f);
            }

            for (int i = 0; i < 13; i++)
            {
                float z = -Length * .5f + 2 + i * 3.65f;
                foreach (float x in new[] { -1.8f, 1.8f })
                    ZSRVisuals.Box("reflective lane marking", transform, new Vector3(x, -.015f, z), new Vector3(.085f, .016f, 1.65f), ZSRVisuals.White);
            }

            // Distant skyline is authored once as a separately scrolling parallax layer.
            BuildDecor();
            BuildSurfaceDetails();
            BuildStreetSetDressing();
            BuildUrbanStory(index);
        }

        void BuildSkyline()
        {
            Material distant = chapter == 0 ? ZSRVisuals.SkylineAsh : chapter == 1 ? ZSRVisuals.SkylineRust : ZSRVisuals.SkylineBlue;
            Material mist = chapter == 0 ? ZSRVisuals.HazeAsh : chapter == 1 ? ZSRVisuals.HazeBurn : ZSRVisuals.HazeBlue;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = side * (14f + i * 2.6f);
                    float h = 6f + i * 2.8f + chapter * 2f;
                    float z = -21f + i * 13.5f;
                    ZSRVisuals.Box("skyline silhouette", transform, new Vector3(x, h * .5f + .2f, z), new Vector3(4.5f, h, 2.8f), distant);
                    ZSRVisuals.Box("mist strip", transform, new Vector3(x, 2.1f, z), new Vector3(4.9f, .12f, 3.0f), mist);
                }
            }
            // World horizon now comes from a full-size distant panorama.
            // The previous opaque horizon slab obscured the skyline and broke the composition.
        }

        void BuildDecor()
        {
            if (chapter >= 1)
            {
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    ZSRVisuals.Sphere("rooftop fire glow", transform, new Vector3(side * 10f, 7.5f, -13f + i * 17f), new Vector3(.68f, .52f, .61f), ZSRVisuals.Fire);
                }
            }
            if (chapter == 2)
            {
                ZSRVisuals.Box("evacuation banner", transform, new Vector3(0, 5.5f, 12f), new Vector3(5.6f, .28f, .12f), ZSRVisuals.BlueGlow);
                var text = ZSRVisuals.WorldText("SAFE ZONE", transform, new Vector3(0, 5.8f, 11.8f), .58f, Color.white);
                text.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
            if (level % 5 == 0)
            {
                ZSRVisuals.Box("boss warning sign", transform, new Vector3(0, 3.8f, 16f), new Vector3(5.2f, .24f, .14f), ZSRVisuals.RedGlow);
                var text = ZSRVisuals.WorldText("BOSS", transform, new Vector3(0, 4.05f, 15.85f), .70f, Color.white);
                text.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }

        void BuildBuilding(int side, int index)
        {
            float h = Random.Range(10f, 25f) + (chapter == 2 ? 8 : chapter == 1 ? 4 : 0);
            float depth = Random.Range(5f, 8f);
            float width = Random.Range(4.5f, 7.2f);
            float x = side * (10.0f + index * 2.75f + Random.Range(-.5f, .5f));
            float z = -20 + index * 13f + Random.Range(-2f, 2f);
            var root = ZSRVisuals.Pivot("abandoned building", transform, new Vector3(x, 0, z));
            if (ZSRPolyforkWorld.TryBuilding(root, index, width, h))
            {
                DecorateImportedBuilding(root, side, index);
                return;
            }
            Material facade = index % 2 == 0 ? ZSRVisuals.ConcreteDark : ZSRVisuals.ConcreteDeluxe;
            if (chapter == 1 && index % 2 == 1) facade = ZSRVisuals.Blood;
            if (chapter == 2 && index % 2 == 0) facade = ZSRVisuals.WindowDark;
            // Two original architectural OBJ silhouettes are bundled in this full game.
            // A plain cube must not be the primary visual when external GLBs are unavailable.
            bool towerA = (index + (side > 0 ? 1 : 0)) % 2 == 0;
            // OBJ meshes have unit footprint but different normalized roof heights.
            // Compensate that source size so roof trims, windows and billboard remain aligned.
            float nativeHeight = towerA ? 1.22f : 1.32f;
            ZSRVisuals.MeshPart(towerA ? "lux_urban_tower_a" : "lux_urban_tower_b",
                "authored damaged building silhouette", root, Vector3.zero,
                new Vector3(width, h / nativeHeight, depth), facade);
            ZSRVisuals.Box("facade column",root,new Vector3(-width*.5f+.25f,h*.5f,-depth*.5f-.06f),
                new Vector3(.23f,h,.14f),ZSRVisuals.ConcreteDark);
            ZSRVisuals.Box("facade column",root,new Vector3(width*.5f-.25f,h*.5f,-depth*.5f-.06f),
                new Vector3(.23f,h,.14f),ZSRVisuals.ConcreteDark);
            for(int f=1;f<Mathf.FloorToInt(h/3.3f);f++)
                ZSRVisuals.Box("weathered floor stripe",root,new Vector3(0,f*3.2f,-depth*.5f-.05f),
                    new Vector3(width,.11f,.09f),ZSRVisuals.ConcreteDark);
            ZSRVisuals.Box("roof lip", root, new Vector3(0, h + .11f, 0), new Vector3(width + .30f, .26f, depth + .25f), ZSRVisuals.Steel);
            int floors = Mathf.FloorToInt(h / 2.7f);
            for (int f = 1; f < floors; f++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    bool lit = Random.value < .18f + chapter * .04f;
                    Material window = lit ? ZSRVisuals.Window : ZSRVisuals.WindowDark;
                    ZSRVisuals.Box("front window", root, new Vector3(j * width * .29f, f * 2.62f, -depth * .5f - .01f), new Vector3(width * .16f, 1.28f, .045f), window);
                    ZSRVisuals.Box("back window", root, new Vector3(j * width * .29f, f * 2.62f, depth * .5f + .012f), new Vector3(width * .16f, 1.26f, .05f), window);
                }
            }
            if (Random.value > .50f)
            {
                ZSRVisuals.Box("lit rooftop advertisement", root,
                    new Vector3(0,h+2.0f,0),new Vector3(width*.75f,.55f,.12f),
                    chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.OrangeGlow);
                ZSRVisuals.Box("billboard metal frame",root,new Vector3(0,h+1.67f,0),
                    new Vector3(width*.86f,.08f,.15f),ZSRVisuals.Gunmetal);
            }
            // Architectural layers: practical ledges, exposed metal and damaged facades.
            if (index % 2 == 0)
            {
                int balconyFloor = Mathf.Clamp(floors / 2, 1, Mathf.Max(1, floors - 1));
                float by = balconyFloor * 2.62f - .73f;
                ZSRVisuals.Box("rusted service balcony", root,
                    new Vector3(0, by, -depth * .5f - .52f),
                    new Vector3(width * .62f, .14f, .92f), ZSRVisuals.MetalWorn);
                ZSRVisuals.Box("balcony safety rail", root,
                    new Vector3(0, by + .64f, -depth * .5f - 1f),
                    new Vector3(width * .58f, .06f, .07f), ZSRVisuals.Steel);
                for (int j = -1; j <= 1; j++)
                    ZSRVisuals.Cylinder("balcony vertical railing", root,
                        new Vector3(j * width * .25f, by + .32f, -depth * .5f - 1f),
                        new Vector3(.04f, .32f, .04f), ZSRVisuals.Steel);
            }
            if (index % 3 == 0)
            {
                ZSRVisuals.Box("rooftop damaged HVAC", root, new Vector3(width * .17f, h + .46f, 0),
                    new Vector3(.98f, .74f, 1.18f), ZSRVisuals.Gunmetal);
                ZSRVisuals.Cylinder("AC fan grille", root,
                    new Vector3(width * .17f, h + .86f, 0), new Vector3(.34f, .05f, .34f), ZSRVisuals.Steel);
                ZSRVisuals.Box("pipe outside wall", root,
                    new Vector3(-width * .30f, h * .35f, -depth * .5f - .11f),
                    new Vector3(.10f, h * .7f, .11f), ZSRVisuals.MetalWorn);
            }
            if (chapter >= 1 && index == 1)
            {
                ZSRVisuals.Sphere("roof fire", root, new Vector3(-width * .24f, h + .45f, 0),
                    new Vector3(.32f, .62f, .36f), ZSRVisuals.OrangeGlow);
                ZSRVisuals.Sphere("roof ember", root, new Vector3(-width * .24f, h + .88f, 0),
                    new Vector3(.18f, .34f, .20f), ZSRVisuals.Fire);
            }
            ZSRVisuals.Box("antenna", root, new Vector3(0, h + 1.5f, 0), new Vector3(.09f, 3f, .09f), ZSRVisuals.Steel);
        }

        /// <summary>Art-pass for GLB buildings: variation in street-level storytelling,
        /// instead of identical unmodified brownstone copies on every segment.</summary>
        void DecorateImportedBuilding(Transform root, int side, int index)
        {
            Material metal=ZSRVisuals.MetalWorn;
            Material dark=ZSRVisuals.Gunmetal;
            // Keep details around the pavement even when Polyfork model dimensions vary.
            ZSRVisuals.Box("collapsed street-side concrete",root,
                new Vector3(side*.70f,.12f,-1.65f),new Vector3(1.9f,.25f,.70f),
                ZSRVisuals.ConcreteDeluxe,new Vector3(0,10+index*17,0));
            for(int n=0;n<3;n++)
                ZSRVisuals.Box("broken masonry",root,
                    new Vector3(-1.1f+n*.42f,.15f,-2.10f+(n%2)*.39f),
                    new Vector3(.42f,.29f,.36f),ZSRVisuals.ConcreteDark,
                    new Vector3(n*15,12+n*18,n*11));
            if (index%2==0)
            {
                // Asymmetric temporary evacuation signage and pipes, kept off the road.
                ZSRVisuals.Box("evacuation notice bracket",root,new Vector3(0,3.75f,-2.25f),
                    new Vector3(1.15f,.12f,.10f),metal);
                ZSRVisuals.Box("faded emergency strip",root,new Vector3(0,3.62f,-2.32f),
                    new Vector3(1.08f,.36f,.045f),chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.OrangeGlow);
                ZSRVisuals.Box("exposed service pipe",root,new Vector3(-2.2f,2.2f,-2.02f),
                    new Vector3(.11f,4.1f,.11f),dark);
            }
            else
            {
                var crate=ZSRVisuals.Pivot("abandoned maintenance crates",root,new Vector3(.76f,0f,-2.15f));
                ZSRVisuals.Box("cracked supply crate",crate,new Vector3(0,.45f,0),
                    new Vector3(.82f,.90f,.80f),dark,new Vector3(0,index*21,0));
                ZSRVisuals.Box("danger stripe",crate,new Vector3(0,.75f,-.43f),
                    new Vector3(.77f,.09f,.025f),ZSRVisuals.Yellow);
            }
        }

        void BuildSurfaceDetails()
        {
            // The wet, cracked asphalt is authored as a seamless material. Previously
            // separately drawn patch boxes created large black floating rectangles
            // on the road due to z-fighting and different tiling at grazing angles.
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    float z=-17+i*15.0f;
                    var barrier=ZSRVisuals.MeshPart("barrier_shell","portable hazard barrier",transform,
                        new Vector3(side*6.4f,.50f,z),new Vector3(.65f,.80f,.75f),ZSRVisuals.ConcreteDeluxe,
                        new Vector3(0,side*15,0));
                    ZSRVisuals.Capsule("barrier rounded cap", barrier.transform, new Vector3(0, .41f, 0), new Vector3(.75f, .16f, .46f), ZSRVisuals.ConcreteDeluxe, new Vector3(90,0,0));
                    ZSRVisuals.Cylinder("barrier beacon left", barrier.transform, new Vector3(-.30f, .51f, -.08f), new Vector3(.09f,.10f,.09f), chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.Yellow);
                    ZSRVisuals.Cylinder("barrier beacon right", barrier.transform, new Vector3(.30f, .51f, -.08f), new Vector3(.09f,.10f,.09f), chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.Yellow);
                    ZSRVisuals.Box("reflective warning stripe",barrier.transform,new Vector3(0,.29f,-.44f),
                        new Vector3(.72f,.07f,.035f),chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.Yellow);
                    for(int rock=0;rock<3;rock++)
                    {
                        float rockX=side*(5.4f+rock*.38f);
                        ZSRVisuals.Sphere("sidewalk rubble",transform,new Vector3(rockX,.10f,z+.2f+rock*.34f),
                            new Vector3(.21f+rock*.05f,.12f,.28f+rock*.04f),ZSRVisuals.ConcreteDark);
                    }
                }
            }
        }
        void BuildStreetSetDressing()
        {
            // Roadside-only props: strictly outside playable lanes, zero gameplay collision.
            for(int side=-1;side<=1;side+=2)
            {
                float z=side<0 ? -8.5f : 13f;
                var area=ZSRVisuals.Pivot("crash debris scene",transform,
                    new Vector3(side*7.90f,0f,z));
                ZSRVisuals.Box("burnt metal equipment box",area,new Vector3(0,.50f,0),
                    new Vector3(1.0f,.85f,.90f),ZSRVisuals.MetalWorn,
                    new Vector3(0,side*11,side*7));
                ZSRVisuals.Box("angled warning stripe",area,new Vector3(0,.74f,-.49f),
                    new Vector3(.70f,.08f,.02f),chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.Yellow,
                    new Vector3(0,0,-15));
                for(int j=0;j<4;j++)
                    ZSRVisuals.Box("roadside broken asphalt chunk",area,
                        new Vector3(side*(-.22f+j*.39f),.14f,-1f+j*.28f),
                        new Vector3(.38f,.22f,.37f),ZSRVisuals.ConcreteDark,
                        new Vector3(j*9,j*25,j*13));
                ZSRVisuals.Box("rusted roadside signal pole",area,
                    new Vector3(side*.65f,2.2f,1.40f),new Vector3(.08f,4.4f,.08f),ZSRVisuals.Steel);
                ZSRVisuals.Box("weathered safety placard",area,
                    new Vector3(side*.65f,3.65f,1.36f),new Vector3(.65f,.55f,.09f),
                    chapter==1?ZSRVisuals.Blood:ZSRVisuals.Gunmetal);
                ZSRVisuals.Box("placard upper reflector",area,
                    new Vector3(side*.65f,3.85f,1.29f),new Vector3(.58f,.055f,.025f),
                    chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.Yellow);
            }
        }

        /// <summary>Three small recurring roadside stories, varying per chunk and chapter.
        /// All visual props are outside the player lanes, with no gameplay colliders.</summary>
        void BuildUrbanStory(int segment)
        {
            int layout = (segment + chapter * 2) % 3;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = layout == 0 ? 4f : layout == 1 ? -13f : 15f;
                float x = side * 8.5f;
                Transform set = ZSRVisuals.Pivot("SCENERY / chapter roadside narrative", transform,
                    new Vector3(x,0f,z));
                if (layout == 0)
                {
                    // Evacuation checkpoint: steel light mast, battered road sign and crates.
                    ZSRVisuals.Box("checkpoint steel mast",set,new Vector3(0,3.15f,0),
                        new Vector3(.13f,6.3f,.15f),ZSRVisuals.MetalWorn);
                    ZSRVisuals.Box("checkpoint overhead fixture",set,new Vector3(-side*.55f,6.12f,0),
                        new Vector3(1.28f,.16f,.15f),ZSRVisuals.Gunmetal);
                    ZSRVisuals.Box("checkpoint warning beacon",set,new Vector3(-side*1.12f,6.02f,0),
                        new Vector3(.22f,.11f,.30f),chapter==2?ZSRVisuals.BlueGlow:ZSRVisuals.OrangeGlow);
                    var sign=ZSRVisuals.Box("EVAC damaged notice",set,new Vector3(0,3.9f,-.08f),
                        new Vector3(1.65f,.82f,.10f),ZSRVisuals.Gunmetal);
                    ZSRVisuals.Box("notice amber stripe",sign.transform,new Vector3(0,.31f,-.53f),
                        new Vector3(.98f,.12f,.12f),ZSRVisuals.Yellow);
                    for(int k=0;k<2;k++)
                        ZSRVisuals.Box("weathered cargo crate",set,new Vector3(side*(.7f+k*.65f),.42f,-1f+k*.45f),
                            new Vector3(.74f,.84f,.74f),ZSRVisuals.MetalWorn,new Vector3(0,k*23,0));
                }
                else if (layout == 1)
                {
                    // Ruined monorail footing and loose steel members; never spans a playable lane.
                    ZSRVisuals.Box("stripped concrete elevated support",set,new Vector3(side*.65f,3.55f,0),
                        new Vector3(1.30f,7.1f,1.25f),ZSRVisuals.ConcreteDark,new Vector3(0,0,side*8));
                    ZSRVisuals.Box("exposed bearing cap",set,new Vector3(side*.65f,7.13f,0),
                        new Vector3(2.45f,.36f,1.62f),ZSRVisuals.MetalWorn);
                    ZSRVisuals.Box("broken gantry beam",set,new Vector3(-side*1.05f,7.08f,0),
                        new Vector3(2.9f,.26f,.27f),ZSRVisuals.Steel,new Vector3(0,0,side*13));
                    ZSRVisuals.Box("loose suspended cable",set,new Vector3(-side*.36f,4.99f,-.32f),
                        new Vector3(.045f,3.35f,.04f),ZSRVisuals.Gunmetal,new Vector3(0,0,-side*8));
                    for(int k=0;k<4;k++)
                        ZSRVisuals.Box("masonry from ruined support",set,
                            new Vector3(-side*.3f+k*.25f,.12f,-1.25f+(k%2)*.51f),
                            new Vector3(.35f,.21f,.36f),ZSRVisuals.ConcreteDeluxe,
                            new Vector3(k*12,k*31,k*9));
                }
                else
                {
                    // Stranded utility van uses the actual Polyfork car mesh where available.
                    Transform carSet=ZSRVisuals.Pivot("abandoned vehicle roadside",set,
                        new Vector3(side*.1f,0,-1.05f));
                    if (!ZSRPolyforkWorld.TryObstacle(carSet,0))
                    {
                        ZSRVisuals.Box("car underbody",carSet,new Vector3(0,.62f,0),
                            new Vector3(1.9f,.52f,3.2f),ZSRVisuals.WreckRustMaterial,
                            new Vector3(0,side*15,0));
                        ZSRVisuals.Box("car battered roof",carSet,new Vector3(0,1.14f,-.18f),
                            new Vector3(1.5f,.51f,1.53f),ZSRVisuals.Gunmetal);
                    }
                    ZSRVisuals.Box("abandoned reflective cone base",set,new Vector3(-side*1.08f,.12f,1.12f),
                        new Vector3(.47f,.22f,.45f),ZSRVisuals.Gunmetal);
                    ZSRVisuals.Cylinder("hazard flare",set,new Vector3(-side*1.08f,.43f,1.12f),
                        new Vector3(.11f,.34f,.11f),ZSRVisuals.OrangeGlow);
                }
            }
        }

        void BuildLamp(int side, float z)
        {
            ZSRVisuals.Cylinder("street light pole", transform, new Vector3(side * 7.25f, 3.3f, z), new Vector3(.06f, 3.35f, .06f), ZSRVisuals.Gunmetal);
            ZSRVisuals.Box("lamp crane", transform, new Vector3(side * 6.55f, 6.6f, z), new Vector3(1.5f, .09f, .10f), ZSRVisuals.Steel);
            ZSRVisuals.Sphere("streetlamp glow", transform, new Vector3(side * 5.9f, 6.53f, z), new Vector3(.28f, .12f, .28f), chapter == 2 ? ZSRVisuals.BlueGlow : ZSRVisuals.OrangeGlow);
        }

        public void Tick(float travel)
        {
            transform.position += Vector3.back * travel;
            if (transform.position.z < -70f) transform.position += Vector3.forward * 184f;
        }
    }

    public sealed class ZSRPickup : MonoBehaviour
    {
        public int Value = 25;
        bool claimed;
        public void Build(float x, float z, int value)
        {
            Value = value;
            transform.position = new Vector3(x, .97f, z);
            ZSRVisuals.Cylinder("gold circular rim", transform, Vector3.zero, new Vector3(.53f, .060f, .53f), ZSRVisuals.GoldGlow, new Vector3(90, 0, 0));
            ZSRVisuals.Cylinder("inner coin", transform, new Vector3(0, 0, -.08f), new Vector3(.39f, .04f, .39f), ZSRVisuals.Gold, new Vector3(90, 0, 0));
            ZSRVisuals.Box("currency sign", transform, new Vector3(0, 0, -.14f), new Vector3(.05f, .45f, .03f), ZSRVisuals.GoldGlow);
        }
        void Update()
        {
            if (ZSRGame.Instance == null || !ZSRGame.Instance.IsRunning) return;
            float dt = ZSRGame.Instance.FrameStep;
            transform.position += Vector3.back * ZSRGame.Instance.RunSpeed * dt;
            transform.Rotate(0, 170f * dt, 0);
            float radius = ZSRGame.Instance.MagnetActive ? 3.6f : 1.1f;
            if (!claimed && transform.position.z < 2.2f && transform.position.z > -1.2f && Mathf.Abs(transform.position.x - ZSRGame.Instance.Hero.PositionX) < radius)
            {
                claimed = true;
                ZSRGame.Instance.CollectCoin(Value, transform.position);
                Destroy(gameObject);
            }
            if (transform.position.z < -5f) Destroy(gameObject);
        }
    }

    /// <summary>Gameplay and collision timing. Art is handled by ZSRVeilGateView.</summary>
    public sealed class ZSRBonusGate : MonoBehaviour
    {
        public BonusKind Kind;
        public ZSRBonusGate Sister;
        bool claimed;

        public void Build(BonusKind kind, float x, float z)
        {
            Kind = kind;
            transform.position = new Vector3(x, 0, z);
            ZSRVeilGateView.Build(transform, kind);
        }

        void Update()
        {
            if (ZSRGame.Instance == null || !ZSRGame.Instance.IsRunning) return;
            var game = ZSRGame.Instance;
            float previousZ = transform.position.z;
            transform.position += Vector3.back * game.RunSpeed * game.FrameStep;
            float heroZ = game.Hero.transform.position.z;
            if (!claimed && previousZ > heroZ && transform.position.z <= heroZ)
            {
                claimed = true;
                if (Mathf.Abs(transform.position.x - game.Hero.PositionX) < 1.55f)
                {
                    game.ApplyBonus(Kind);
                    if (Sister != null) Sister.claimed = true;
                }
            }
            if (transform.position.z < -7f) Destroy(gameObject);
        }
    }

    public sealed class ZSRObstacle : MonoBehaviour
    {
        bool damaged;
        int obstacleKind;
        /// <summary>Four clear obstacle silhouettes: wreck, barricade, spikes and rubble.</summary>
        public void Build(float x,float z,int chapter,int forcedKind=-1)
        {
            transform.position=new Vector3(x,0,z);
            float yaw=Random.Range(-12f,12f);
            var root=ZSRVisuals.Pivot("obstacle detailed 3D prop",transform,Vector3.zero);
            root.localEulerAngles=new Vector3(0,yaw,0);
            int kind=forcedKind>=0?Mathf.Clamp(forcedKind,0,3):Random.Range(0,4);
            obstacleKind = kind;
            root.localScale = Vector3.one * (kind == 0 ? 1.14f : kind == 1 ? 1.18f : kind == 2 ? 1.13f : 1.19f);
            if (ZSRPolyforkWorld.TryObstacle(root, kind))
            {
                // Imported GLB is primary visual. Add only subtle context, not a second car.
                if (kind==0)
                {
                    ZSRVisuals.Box("reflective debris plate",root,
                        new Vector3(-.82f,.065f,.70f),new Vector3(.72f,.07f,.50f),
                        ZSRVisuals.MetalWorn,new Vector3(0,18,0));
                    ZSRVisuals.WreckFire(root, new Vector3(.28f, 1.05f, -.83f));
                }
                else if (kind==1)
                {
                    for(int side=-1;side<=1;side+=2)
                        ZSRVisuals.Cylinder("barrier corner warning lamp",root,
                            new Vector3(side*.85f,1.05f,-.28f),
                            new Vector3(.10f,.11f,.10f),ZSRVisuals.Yellow);
                }
                return;
            }
            if(kind==0)
            {
                // Original unified sedan silhouette. Separate OBJ components keep real
                // wheel geometry, tempered glass and lights visibly distinct in URP.
                ZSRVisuals.MeshPart("lux_wreck_body", "damaged full-body sedan 3D",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.WreckRustMaterial);
                ZSRVisuals.MeshPart("lux_wreck_wheels", "four rubber wheels",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.Tire);
                ZSRVisuals.MeshPart("lux_wreck_hubs", "wheel hubs",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.MetalWorn);
                ZSRVisuals.MeshPart("lux_wreck_windows", "shattered dark glazing",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.WindowDark);
                ZSRVisuals.MeshPart("lux_wreck_lights", "broken signal lenses",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.Yellow);
                ZSRVisuals.Box("front crash grille", root, new Vector3(0, .74f, 1.9f),
                    new Vector3(1.3f, .21f, .08f), ZSRVisuals.Gunmetal);
                ZSRVisuals.WreckFire(root, new Vector3(.28f, 1.04f, -.83f));
            }
            else if(kind==1)
            {
                ZSRVisuals.MeshPart("lux_concrete_block", "beveled large 3D road blockade",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.ConcreteDeluxe);
                ZSRVisuals.Box("foot left",root,new Vector3(-.72f,.10f,0),
                    new Vector3(.32f,.17f,1.0f),ZSRVisuals.ConcreteDark);
                ZSRVisuals.Box("foot right",root,new Vector3(.72f,.10f,0),
                    new Vector3(.32f,.17f,1.0f),ZSRVisuals.ConcreteDark);
                for(int i=0;i<4;i++)
                    ZSRVisuals.Box("diagonal reflective strip",root,new Vector3(-.9f+i*.6f,.94f,-.72f),
                        new Vector3(.20f,.30f,.045f),i % 2 == 0 ? ZSRVisuals.White : ZSRVisuals.RedJacket,
                        new Vector3(0,0,-32));
                for(int side=-1;side<=1;side+=2)
                    ZSRVisuals.Sphere("amber work lamp",root,new Vector3(side*.71f,1.45f,-.05f),
                        new Vector3(.15f,.14f,.15f),ZSRVisuals.OrangeGlow);
            }
            else if (kind == 2)
            {
                ZSRVisuals.MeshPart("lux_spike_base", "reinforced bolted trap bed",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.MetalWorn);
                ZSRVisuals.MeshPart("lux_spikes", "15 tapered metal spikes",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.Steel);
                for(int side=-1;side<=1;side+=2)
                    ZSRVisuals.Box("trap danger stripe",root,new Vector3(side*.99f,.26f,0),
                        new Vector3(.12f,.012f,1.85f),ZSRVisuals.Yellow);
            }
            else
            {
                // Multiple fractured convex rocks + fallen slab are a single custom OBJ mesh.
                ZSRVisuals.MeshPart("lux_rubble", "fractured overpass debris 3D",
                    root, Vector3.zero, Vector3.one, ZSRVisuals.ConcreteDeluxe);
                for(int k=0;k<3;k++)
                    ZSRVisuals.Beam("exposed reinforcing steel",root,
                        new Vector3(-.75f+k*.62f,.65f,-1.11f),
                        new Vector3(-.67f+k*.62f,1.42f,.49f),.055f,ZSRVisuals.Steel);
                ZSRVisuals.Box("hazard marking",root,new Vector3(0,.38f,-.77f),
                    new Vector3(2.4f,.20f,.08f),ZSRVisuals.Yellow);
            }
        }
        void Update()
        {
            if (ZSRGame.Instance == null || !ZSRGame.Instance.IsRunning) return;
            transform.position += Vector3.back * ZSRGame.Instance.RunSpeed * ZSRGame.Instance.FrameStep;
            // Radius approximates the authored visual footprint plus the hero's torso;
            // keep collision analytic and deterministic instead of using unstable mesh colliders.
            float contactHalfWidth = obstacleKind == 0 ? 1.42f : obstacleKind == 1 ? 1.58f :
                obstacleKind == 2 ? 1.54f : 1.61f;
            if (!damaged && transform.position.z < 1.65f && transform.position.z > -1.65f &&
                Mathf.Abs(transform.position.x - ZSRGame.Instance.Hero.PositionX) < contactHalfWidth)
            {
                damaged = true;
                ZSRGame.Instance.HurtPlayer(20);
                ZSRGame.Instance.SpawnImpact(transform.position + Vector3.up, true);
            }
            if (transform.position.z < -7f) Destroy(gameObject);
        }
    }

    public sealed class ZSRLifetime : MonoBehaviour
    {
        public float Duration = .25f;
        public Vector3 Velocity;
        void Update()
        {
            transform.position += Velocity * Time.deltaTime;
            Duration -= Time.deltaTime;
            if (Duration <= 0) Destroy(gameObject);
        }
    }
}
