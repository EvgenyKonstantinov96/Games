using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>One shared material for the UV-mapped road hazards and gate pylons.</summary>
    public static class ZSRTrackArt
    {
        static Material paintedMetal, guideLight;
        static Material shoulderGlow;
        static Mesh shoulderGlowMesh;

        public static Material GuideLight
        {
            get
            {
                if (guideLight == null) guideLight = ZSRVisuals.Mat(new Color(1f, .46f, .12f), .45f, .72f);
                return guideLight;
            }
        }

        public static void PlaceShoulderGlows(Transform road, float length, float outerX)
        {
            if (shoulderGlow == null)
            {
                var shader = Resources.Load<Shader>("ProductionArt/ZSRCombatFX");
                var texture = Resources.Load<Texture2D>("ProductionArt/ParticleSoft");
                if (shader == null || !shader.isSupported || texture == null) return;
                shoulderGlow = new Material(shader) { name = "Soft amber by bridge rails" };
                shoulderGlow.SetTexture("_BaseMap", texture);
            }
            if (shoulderGlowMesh == null)
            {
                var vertices = new List<Vector3>(48);
                var uv = new List<Vector2>(48);
                var tint = new List<Color>(48);
                var triangles = new List<int>(72);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int marker = 0; marker < 11; marker += 2)
                    {
                        float x = side * (outerX - .65f);
                        float z = length * ((marker + .5f) / 11f - .5f) - .69f;
                        int start = vertices.Count;
                        vertices.Add(new Vector3(x - .57f, -.015f, z - 1.18f));
                        vertices.Add(new Vector3(x + .57f, -.015f, z - 1.18f));
                        vertices.Add(new Vector3(x + .57f, -.015f, z + 1.18f));
                        vertices.Add(new Vector3(x - .57f, -.015f, z + 1.18f));
                        uv.Add(Vector2.zero);
                        uv.Add(Vector2.right);
                        uv.Add(Vector2.one);
                        uv.Add(Vector2.up);
                        for (int i = 0; i < 4; i++) tint.Add(new Color(.87f, .37f, .125f, .21f));
                        triangles.Add(start);
                        triangles.Add(start + 2);
                        triangles.Add(start + 1);
                        triangles.Add(start);
                        triangles.Add(start + 3);
                        triangles.Add(start + 2);
                    }
                }
                shoulderGlowMesh = new Mesh { name = "Pooled amber reflections" };
                shoulderGlowMesh.SetVertices(vertices);
                shoulderGlowMesh.SetUVs(0, uv);
                shoulderGlowMesh.SetColors(tint);
                shoulderGlowMesh.SetTriangles(triangles, 0);
            }
            var layer = new GameObject("Road shoulder reflections", typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(road, false);
            layer.GetComponent<MeshFilter>().sharedMesh = shoulderGlowMesh;
            var renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shoulderGlow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // The last moving deck can end at z=92 just before recycling.
        // Overlap underneath it and extend beyond the far clip in the haze.
        public static GameObject BuildDistantDeck(Transform world)
        {
            const float center = 164f, length = 156f;
            var bridge = ZSRVisuals.Pivot("Distant continuation of the bridge", world, Vector3.zero);
            ZSRVisuals.Box("Distant bridge deck", bridge, new Vector3(0, -.16f, center),
                new Vector3(9.7f, .20f, length), ZSRVisuals.AsphaltDeluxe);
            for (int side = -1; side <= 1; side += 2)
                ZSRVisuals.Box("Distant bridge parapet", bridge, new Vector3(side * 4.98f, .39f, center),
                    new Vector3(.082f, .13f, length), ZSRVisuals.Armor);
            return bridge.gameObject;
        }

        public static Material Surface
        {
            get
            {
                if (paintedMetal == null)
                {
                    paintedMetal = ZSRVisuals.MatWithTexture(Color.white, "ProductionArt/TrackPropAtlas", .31f,
                        missingTextureColor: new Color(.24f, .27f, .3f));
                    paintedMetal.name = "Track prop surface / shared";
                }
                return paintedMetal;
            }
        }
    }
}
