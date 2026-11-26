using UnityEngine;

namespace ZombieSurvivalRun
{
    public enum BonusKind { DoubleDamage, RapidFire, Shield, Heal, Magnet, Turret }

    public sealed class ZSRRoadChunk : MonoBehaviour
    {
        const float Length = 46f;
        const float DeckWidth = 9.7f;
        const float RailX = 4.98f;
        const float EdgeStripeX = 4.65f;
        int level, chapter;

        public void Build(int index, int sourceLevel)
        {
            level = sourceLevel;
            chapter = Mathf.Clamp((sourceLevel - 1) / 5, 0, 2);
            transform.position = new Vector3(0, 0, Length * (index - .5f));
            ZSRVisuals.Box("cracked asphalt deck", transform, new Vector3(0, -.25f, 0),
                new Vector3(DeckWidth, .45f, Length), ZSRVisuals.AsphaltDeluxe);
            ZSRTrackArt.PlaceShoulderGlows(transform, Length, RailX, ZSRCampaignStyle.For(chapter).Accent);
            for (int side = -1; side <= 1; side += 2)
            {
                ZSRVisuals.Box("road edge stripe", transform, new Vector3(side * EdgeStripeX, -.019f, 0),
                    new Vector3(.07f, .008f, Length), ZSRVisuals.White);
                ZSRVisuals.Box("sidewalk", transform, new Vector3(side * 6.04f, -.23f, 0),
                    new Vector3(2.23f, .46f, Length), ZSRVisuals.ConcreteDeluxe);
                ZSRVisuals.Box("cast concrete curb", transform, new Vector3(side * RailX, .055f, 0),
                    new Vector3(.27f, .11f, Length), ZSRVisuals.Concrete);
                ZSRVisuals.MeshPart("track_guardrail", "Weathered double bridge rail", transform,
                    new Vector3(side * RailX, 0, 0), Vector3.one, ZSRTrackArt.Surface);
                for (int k = 0; k < 11; k++)
                {
                    float z = Length * ((k + .5f) / 11f - .5f);
                    if (k % 2 == 0)
                        ZSRVisuals.Box("recessed amber guide", transform, new Vector3(side * (RailX - .105f), .695f, z - .69f),
                            new Vector3(.019f, .041f, .32f), ZSRTrackArt.GuideLight(chapter));
                }
                for (int b = 0; b < 2; b++) BuildBuilding(side, index, b);
                BuildLamp(side, side < 0 ? -10f : 12f);
            }

            const int laneDashes = 13;
            for (int i = 0; i < laneDashes; i++)
            {
                float z = Length * ((i + .5f) / laneDashes - .5f);
                foreach (float x in new[] { -1.8f, 1.8f })
                    ZSRVisuals.Box("reflective lane marking", transform, new Vector3(x, -.019f, z),
                        new Vector3(.072f, .008f, 1.58f), ZSRVisuals.White);
            }

            // Distant skyline is authored once as a separately scrolling parallax layer.
            BuildSurfaceDetails();
            BuildStreetSetDressing();
            ZSRTrackArt.PlaceChapterLandmark(transform, chapter, index, level % 5 == 0);
        }

        void BuildBuilding(int side, int segment, int row)
        {
            int variant = segment * 2 + row;
            int profile = (variant + chapter + (side > 0 ? 2 : 0)) % 4;
            float h = (row == 0 ? 11f : 15.5f) + (variant % 3) * 2.3f + chapter * 1.6f;
            float width = (row == 0 ? 5.8f : 7.4f) + (variant % 2) * .65f;
            float depth = row == 0 ? 5.75f : 6.85f;
            float x = side * (row == 0 ? 9.9f : 13.35f);
            float z = (row == 0 ? -12.5f : 12.6f) + side * 2.45f;
            var root = ZSRVisuals.Pivot("Street-facing city block", transform, new Vector3(x, 0, z));
            if (ZSRPolyforkWorld.TryBuilding(root, variant, width, h))
            {
                DecorateImportedBuilding(root, side, variant);
                return;
            }

            ZSRCityArchitecture.Place(root, chapter, profile, (segment + (side < 0 ? 1 : 0)) % 2,
                Vector3.zero, new Vector3(width, h, depth));
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
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    float z = -17f + i * 25f + side * 2.2f;
                    ZSRVisuals.MeshPart("lux_rubble", "Broken shoulder masonry", transform,
                        new Vector3(side * (5.95f + i * .42f), 0, z), new Vector3(.33f, .25f, .34f),
                        ZSRVisuals.ConcreteDeluxe, new Vector3(0, side * (18f + i * 47f), 0));
                    ZSRVisuals.MeshPart("barrier_shell", "Abandoned shoulder barrier", transform,
                        new Vector3(side * 6.38f, .32f, z + 1.48f), new Vector3(.60f, .60f, .72f),
                        ZSRVisuals.ConcreteDark, new Vector3(0, side * 23f, 0));
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

        void BuildLamp(int side, float z)
        {
            ZSRVisuals.Cylinder("street light pole", transform, new Vector3(side * 6.37f, 3.08f, z),
                new Vector3(.09f, 3.1f, .09f), ZSRVisuals.MetalWorn);
            ZSRVisuals.Box("street light pedestal", transform, new Vector3(side * 6.37f, .14f, z),
                new Vector3(.28f, .28f, .28f), ZSRVisuals.ConcreteDark);
            ZSRVisuals.Box("lamp arm", transform, new Vector3(side * 5.88f, 6.08f, z),
                new Vector3(1.1f, .086f, .10f), ZSRVisuals.Steel);
            ZSRVisuals.Box("street lamp housing", transform, new Vector3(side * 5.28f, 6.04f, z),
                new Vector3(.36f, .17f, .25f), ZSRVisuals.Gunmetal);
            ZSRVisuals.Box("street lamp lens", transform, new Vector3(side * 5.28f, 5.946f, z),
                new Vector3(.29f, .018f, .18f), ZSRTrackArt.GuideLight(chapter));
        }

        public void Tick(float travel)
        {
            transform.position += Vector3.back * travel;
            if (transform.position.z < -Length * 1.5f) transform.position += Vector3.forward * (Length * 4f);
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
            float radius = ZSRGame.Instance.MagnetActive ? ZSRGame.MagnetRadius : 1.1f;
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
        float contactHalfWidth;
        MeshRenderer approachMarker;
        MaterialPropertyBlock markerTint;
        /// <summary>Four clear obstacle silhouettes: wreck, barricade, spikes and rubble.</summary>
        public void Build(float x,float z,int chapter,int forcedKind=-1)
        {
            transform.position=new Vector3(x,0,z);
            float yaw=Random.Range(-12f,12f);
            var root=ZSRVisuals.Pivot("obstacle detailed 3D prop",transform,Vector3.zero);
            root.localEulerAngles=new Vector3(0,yaw,0);
            int kind=forcedKind>=0?Mathf.Clamp(forcedKind,0,3):Random.Range(0,4);
            contactHalfWidth = kind == 0 ? 1.42f : kind == 1 ? 1.58f : kind == 2 ? 1.54f : 1.61f;
            root.localScale = Vector3.one * (kind == 0 ? 1.14f : kind == 1 ? 1.18f : kind == 2 ? 1.13f : 1.19f);
            bool imported = ZSRPolyforkWorld.TryObstacle(root, kind);
            if (!imported)
            {
                string model = kind switch
                {
                    0 => "track_wreck",
                    1 => "track_barrier",
                    2 => "track_spikes",
                    _ => "track_rubble"
                };
                ZSRVisuals.MeshPart(model, "Weathered road hazard", root, Vector3.zero, Vector3.one,
                    ZSRTrackArt.Surface);
            }

            if (kind == 0)
            {
                ZSRVisuals.WreckFire(root, new Vector3(.28f, 1.04f, -.83f));
            }
            else if (kind == 1)
            {
                // Recessed amber markers distinguish the upper edges of the concrete.
                float height = imported ? 1.05f : 1.60f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float xMarker = side * .63f;
                    ZSRVisuals.Box("Work-light housing", root, new Vector3(xMarker, height, -.18f),
                        new Vector3(.23f, .12f, .15f), ZSRVisuals.Gunmetal);
                    ZSRVisuals.Box("Work-light lens", root, new Vector3(xMarker, height + .009f, -.264f),
                        new Vector3(.166f, .060f, .023f), ZSRVisuals.OrangeGlow);
                }
            }
            else if (kind == 2)
            {
                for (int side = -1; side <= 1; side += 2)
                    ZSRVisuals.Box("Trap approach reflector", root, new Vector3(side * 1.03f, .106f, -1.143f),
                        new Vector3(.28f, .041f, .018f), ZSRVisuals.OrangeGlow);
            }
            approachMarker = ZSRTrackArt.PlaceHazardMark(transform, contactHalfWidth);
            if (approachMarker != null)
            {
                markerTint = new MaterialPropertyBlock();
                approachMarker.enabled = false;
            }
        }
        void Update()
        {
            var game = ZSRGame.Instance;
            if (game == null || !game.IsRunning) return;
            transform.position += Vector3.back * game.RunSpeed * game.FrameStep;
            float depth = transform.position.z;
            bool inPath = Mathf.Abs(transform.position.x - game.Hero.PositionX) < contactHalfWidth;
            if (approachMarker != null)
            {
                approachMarker.enabled = !damaged && depth > 1.65f && depth < 30f;
                if (approachMarker.enabled)
                {
                    float strength = Mathf.Clamp01((30f - depth) * .12f);
                    float pulse = .82f + .18f * Mathf.Sin(game.Elapsed * 9f);
                    markerTint.SetColor("_Tint", new Color(1f, inPath ? .26f : .55f, .13f,
                        strength * pulse * (inPath ? .86f : .53f)));
                    approachMarker.SetPropertyBlock(markerTint);
                }
            }
            if (!damaged && depth < 1.65f && depth > -1.65f && inPath)
            {
                damaged = true;
                game.HurtPlayer(20);
                game.SpawnImpact(transform.position + Vector3.up, true);
            }
            if (depth < -7f) Destroy(gameObject);
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
