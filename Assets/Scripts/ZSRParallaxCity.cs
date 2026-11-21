using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Distance-dependent art scrolling; no collisions or gameplay influence.
    /// Road and actual obstacles move at 1.0x speed, mid-city 0.18x, far-city 0.06x.
    /// This is intentional parallax, not a second procedural road.</summary>
    public sealed class ZSRParallaxCity : MonoBehaviour
    {
        sealed class Cluster
        {
            public Transform visual;
            public float multiplier;
        }
        readonly List<Cluster> pieces = new List<Cluster>(48);
        Renderer panoramaRenderer;
        Material panoramaMaterial;
        int chapter;
        float drift;
        const float RepeatLength = 184f;

        public static ZSRParallaxCity Create(Transform world, int chapter, GameObject panorama)
        {
            var go = new GameObject("SCENERY / independent parallax city (0.06x to 0.18x)");
            go.transform.SetParent(world, false);
            var city = go.AddComponent<ZSRParallaxCity>();
            city.chapter = chapter;
            if (panorama != null)
            {
                city.panoramaRenderer = panorama.GetComponent<Renderer>();
                if (city.panoramaRenderer != null)
                {
                    city.panoramaMaterial = new Material(city.panoramaRenderer.sharedMaterial);
                    city.panoramaRenderer.sharedMaterial = city.panoramaMaterial;
                }
            }
            city.Construct();
            return city;
        }

        void Construct()
        {
            // Two sparse side rows: the nearest buildings still belong to highway chunks.
            // These are strictly BACKGROUND meshes and never overlap playable lanes.
            for (int layer=0; layer<2; layer++)
            {
                float speed = layer == 0 ? .18f : .06f;
                float sideBase = layer == 0 ? 21f : 41f;
                float depthBase = layer == 0 ? 51f : 74f;
                int count = layer == 0 ? 11 : 13;
                for (int side=-1; side<=1; side+=2)
                {
                    for (int i=0; i<count; i++)
                    {
                        float h = (layer==0 ? 17f : 30f) +
                            Mathf.Abs(Mathf.Sin(i*2.371f + side*1.3f))* (layer==0 ? 20f : 25f);
                        float z = 20f + i*(layer==0 ? 14.2f : 12.7f);
                        float x = side * (sideBase + Mathf.Sin(i*1.73f)*3.4f + (i%3)*2.1f);
                        var pivot = ZSRVisuals.Pivot("Parallax quarter "+layer+" / "+side+" / "+i,
                            transform,new Vector3(x,0,z));
                        pieces.Add(new Cluster {visual=pivot,multiplier=speed});
                        float w = layer==0 ? 8.4f+(i%3)*1.7f : 10f+(i%4)*2.3f;
                        float d = layer==0 ? 9f : 13f;
                        Material wall = chapter==1 ? ZSRVisuals.SkylineRust :
                            chapter==2 ? ZSRVisuals.SkylineBlue : ZSRVisuals.SkylineAsh;
                        ZSRVisuals.Box("distant tower body",pivot,new Vector3(0,h*.5f,0),
                            new Vector3(w,h,d),wall);
                        ZSRVisuals.Box("architectural roof profile",pivot,new Vector3(0,h+.22f,0),
                            new Vector3(w+1f,.42f,d+1f),ZSRVisuals.Gunmetal);
                        if (i%3==0)
                        {
                            ZSRVisuals.Box("roof mechanical enclosure",pivot,
                                new Vector3(-w*.22f,h+1.7f,0),new Vector3(w*.36f,2.9f,d*.48f),ZSRVisuals.Steel);
                            ZSRVisuals.Cylinder("communication antenna",pivot,new Vector3(w*.22f,h+2.5f,0),
                                new Vector3(.06f,2.4f,.06f),ZSRVisuals.Gunmetal);
                            ZSRVisuals.Sphere("antenna blinking light",pivot,
                                new Vector3(w*.22f,h+4.9f,0),new Vector3(.16f,.16f,.16f),ZSRVisuals.RedGlow);
                        }
                        if (layer==0)
                        {
                            // Windows are grouped into a handful of continuous strips for mobile rendering.
                            for (int floor=1; floor<5;floor++)
                            {
                                Material lit = (floor+i+side)%4==0 ? ZSRVisuals.Window : ZSRVisuals.WindowDark;
                                ZSRVisuals.Box("far windows",pivot,
                                    new Vector3(0,floor*h/5f,-d*.5f-.04f),
                                    new Vector3(w*.68f,.42f,.09f),lit);
                            }
                        }
                    }
                }
            }
        }

        void Update()
        {
            if (ZSRGame.Instance == null || !ZSRGame.Instance.IsRunning) return;
            float frame = ZSRGame.Instance.FrameStep;
            float travel = ZSRGame.Instance.RunSpeed * frame;
            foreach (var cluster in pieces)
            {
                if (cluster.visual == null) continue;
                Vector3 p = cluster.visual.localPosition;
                p.z -= travel * cluster.multiplier;
                if (p.z < -36f) p.z += RepeatLength;
                cluster.visual.localPosition = p;
            }
            drift += travel * .00004f;
            if (panoramaMaterial != null)
            {
                Vector2 uv = new Vector2(Mathf.Repeat(drift,.015f),0);
                panoramaMaterial.mainTextureOffset = uv;
                if (panoramaMaterial.HasProperty("_BaseMap"))
                    panoramaMaterial.SetTextureOffset("_BaseMap",uv);
            }
        }

        void OnDestroy()
        {
            if (panoramaMaterial != null) Destroy(panoramaMaterial);
        }
    }
}
