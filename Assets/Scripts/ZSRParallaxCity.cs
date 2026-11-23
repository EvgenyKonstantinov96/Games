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
        readonly List<Cluster> pieces = new List<Cluster>(36);
        Material panoramaMaterial;
        int chapter;
        float drift;
        const float RepeatLength = 180f;
        const float WrapZ = -27f;

        public static ZSRParallaxCity Create(Transform world, int chapter, GameObject panorama)
        {
            var go = new GameObject("SCENERY / independent parallax city (0.06x to 0.18x)");
            go.transform.SetParent(world, false);
            var city = go.AddComponent<ZSRParallaxCity>();
            city.chapter = chapter;
            if (panorama != null)
            {
                if (panorama.TryGetComponent<Renderer>(out var renderer))
                {
                    city.panoramaMaterial = new Material(renderer.sharedMaterial);
                    renderer.sharedMaterial = city.panoramaMaterial;
                }
            }
            city.Construct();
            return city;
        }

        void Construct()
        {
            // The street row, mid-rise districts and distant towers pass at different speeds.
            for (int layer = 0; layer < 2; layer++)
            {
                float speed = layer == 0 ? .18f : .06f;
                float sideBase = layer == 0 ? 17.5f : 31f;
                int count = layer == 0 ? 8 : 10;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < count; i++)
                    {
                        float h = (layer == 0 ? 13f : 24f) + ((i * 7 + side + 23) % 6) * 2.8f;
                        float z = WrapZ + (i + (side < 0 ? .27f : .76f)) * RepeatLength / count;
                        float x = side * (sideBase + (i % 3) * 3.1f);
                        var pivot = ZSRVisuals.Pivot("Parallax city district", transform, new Vector3(x, 0, z));
                        pieces.Add(new Cluster { visual = pivot, multiplier = speed });
                        float w = (layer == 0 ? 8f : 10f) + (i % 3) * 1.2f;
                        float d = layer == 0 ? 8.4f : 11f;
                        int profile = (i * 3 + chapter + layer + (side > 0 ? 2 : 0)) % 4;
                        ZSRCityArchitecture.Place(pivot, chapter, profile, i % 2, Vector3.zero, new Vector3(w, h, d));
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
                if (p.z < WrapZ) p.z += RepeatLength;
                cluster.visual.localPosition = p;
            }
            if (panoramaMaterial != null)
            {
                drift = Mathf.Min(.025f, drift + travel * .00004f);
                Vector2 uv = new Vector2(drift, 0);
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
