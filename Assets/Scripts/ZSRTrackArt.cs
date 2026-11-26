using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>One shared material for the UV-mapped road hazards and gate pylons.</summary>
    public static class ZSRTrackArt
    {
        static Material paintedMetal;
        static Material shoulderGlow;
        static Mesh shoulderGlowMesh;
        static Material hazardMaterial;
        static Mesh hazardMesh;
        static readonly Material[] landscapeLenses = new Material[3];

        public static void PlaceChapterLandmark(Transform track, int chapter, int segment, bool bossLevel)
        {
            if (segment != 1 && segment != 3) return;
            var style = ZSRCampaignStyle.For(chapter);
            var station = ZSRVisuals.Pivot("Chapter landmark / " + style.Landmark, track,
                new Vector3(0f, 0f, segment == 1 ? 7f : -10f));
            ZSRVisuals.MeshPart("track_chapter_" + style.Landmark, "Authored roadside structure", station,
                Vector3.zero, Vector3.one, Surface);
            Material light = GuideLight(chapter);
            Vector3 sign = chapter == 0 ? new Vector3(0f, 7.15f, -.88f) : chapter == 1 ?
                new Vector3(0f, 6.11f, -1.27f) : new Vector3(0f, 6.40f, -.85f);
            if (chapter == 1)
            {
                ZSRVisuals.Box("Refinery suspended placard", station, sign + Vector3.forward * .09f,
                    new Vector3(4.55f, .54f, .16f), ZSRVisuals.Gunmetal);
                if (segment == 1) ZSRVisuals.WreckFire(station, new Vector3(-7.25f, 9.02f, .95f));
            }
            string wording = bossLevel && segment == 3 ? "DANGER / BOSS" : style.RoadSign;
            ZSRVisuals.WorldText(wording, station, sign, .48f, style.Accent).fontStyle = FontStyle.Bold;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 lens = chapter == 0 ? new Vector3(side * 5.93f, 5.32f, -.49f) : chapter == 1 ?
                    new Vector3(side * 7.25f, 6.83f, -1.63f) : new Vector3(side * 6.20f, 7.07f, -1.14f);
                ZSRVisuals.Box("Chapter signal lens", station, lens, new Vector3(.59f, .13f, .055f), light);
            }
        }

        public static MeshRenderer PlaceHazardMark(Transform parent, float halfWidth)
        {
            if (hazardMaterial == null)
            {
                var shader = Resources.Load<Shader>("ProductionArt/ZSRCombatFX");
                if (shader == null || !shader.isSupported) return null;
                hazardMaterial = new Material(shader) { name = "Road hazard projections" };
            }
            if (hazardMesh == null)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        float z = -5.7f + row * .83f;
                        AddStripe(side * .88f, z - .22f, side * .63f, z + .27f);
                    }
                    AddStripe(side * .967f, -4.3f, side * .967f, -2.13f);
                }
                hazardMesh = new Mesh { name = "Open danger chevrons" };
                hazardMesh.SetVertices(vertices);
                hazardMesh.SetTriangles(triangles, 0);
                var colors = new Color[vertices.Count];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                hazardMesh.colors = colors;

                void AddStripe(float x0, float z0, float x1, float z1)
                {
                    int index = vertices.Count;
                    const float width = .033f;
                    vertices.Add(new Vector3(x0 - width, .021f, z0));
                    vertices.Add(new Vector3(x0 + width, .021f, z0));
                    vertices.Add(new Vector3(x1 + width, .021f, z1));
                    vertices.Add(new Vector3(x1 - width, .021f, z1));
                    triangles.AddRange(new[] { index, index + 2, index + 1, index, index + 3, index + 2 });
                }
            }

            var mark = new GameObject("Road hazard guide", typeof(MeshFilter), typeof(MeshRenderer));
            mark.transform.SetParent(parent, false);
            mark.transform.localScale = new Vector3(halfWidth, 1f, 1f);
            mark.GetComponent<MeshFilter>().sharedMesh = hazardMesh;
            var renderer = mark.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = hazardMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public static Material GuideLight(int chapter)
        {
            if (landscapeLenses[chapter] == null)
                landscapeLenses[chapter] = ZSRVisuals.Mat(ZSRCampaignStyle.For(chapter).Accent, .34f, 1.05f);
            return landscapeLenses[chapter];
        }

        public static void PlaceShoulderGlows(Transform road, float length, float outerX, Color chapterTint)
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
                        for (int i = 0; i < 4; i++) tint.Add(new Color(1f, 1f, 1f, .21f));
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
            var tintBlock = new MaterialPropertyBlock();
            tintBlock.SetColor("_Tint", chapterTint);
            renderer.SetPropertyBlock(tintBlock);
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
