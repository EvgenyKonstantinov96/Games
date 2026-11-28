using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>One shared material for the UV-mapped road hazards and gate pylons.</summary>
    public static class ZSRTrackArt
    {
        // The road texture spans the carriageway exactly once across and repeats every 11.5 m.
        // Paint, drains and cracks share UVs and therefore stay in contact under grazing light.
        public const float SurfaceRepeatLength = 11.5f;
        static Mesh movingDeck, distantDeck;
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
                var texture = Resources.Load<Texture2D>("ProductionArt/RoadReflection");
                if (shader == null || !shader.isSupported || texture == null) return;
                shoulderGlow = new Material(shader) { name = "Wet asphalt / guide and street lamps" };
                shoulderGlow.SetTexture("_BaseMap", texture);
            }
            if (shoulderGlowMesh == null)
            {
                var vertices = new List<Vector3>(56);
                var uv = new List<Vector2>(56);
                var tint = new List<Color>(56);
                var triangles = new List<int>(84);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int marker = 0; marker < 11; marker += 2)
                    {
                        float x = side * (outerX - .65f);
                        float z = length * ((marker + .5f) / 11f - .5f) - .69f;
                        Spill(x, z, .57f, 1.18f, .23f);
                    }
                    // The elevated fixtures throw a longer reflection toward the outer lane.
                    Spill(side * (outerX - 1.04f), side < 0 ? -10.35f : 11.65f, 1.13f, 2.68f, .27f);
                }

                void Spill(float x, float z, float halfWidth, float halfLength, float strength)
                {
                    int start = vertices.Count;
                    vertices.Add(new Vector3(x - halfWidth, -.015f, z - halfLength));
                    vertices.Add(new Vector3(x + halfWidth, -.015f, z - halfLength));
                    vertices.Add(new Vector3(x + halfWidth, -.015f, z + halfLength));
                    vertices.Add(new Vector3(x - halfWidth, -.015f, z + halfLength));
                    uv.Add(Vector2.zero);
                    uv.Add(Vector2.right);
                    uv.Add(Vector2.one);
                    uv.Add(Vector2.up);
                    for (int i = 0; i < 4; i++) tint.Add(new Color(1f, 1f, 1f, strength));
                    triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
                shoulderGlowMesh = new Mesh { name = "Lamp reflections along the bridge shoulder" };
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

        public static void PlaceRoadDeck(Transform track, float width, float length)
        {
            if (movingDeck == null) movingDeck = BuildDeckMesh(width, length, .45f);
            PlaceDeck(track, movingDeck, new Vector3(0f, -.025f, 0f));
        }

        static Mesh BuildDeckMesh(float width, float length, float thickness)
        {
            float x = width * .5f;
            float z = length * .5f;
            float repeats = length / SurfaceRepeatLength;
            var mesh = new Mesh { name = "Bridge deck with lane-aligned UVs" };
            var vertices = new List<Vector3>(12);
            var uv = new List<Vector2>(12);
            var indices = new List<int>(18);
            var left = new Vector3(-x, 0f, -z);
            var right = new Vector3(x, 0f, -z);
            var end = Vector3.forward * length;
            var down = Vector3.down * thickness;
            Face(left, right, 0f, 1f);
            Face(left + down, left, .002f, .012f);
            Face(right, right + down, .988f, .998f);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;

            void Face(Vector3 a, Vector3 b, float u0, float u1)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(a + end);
                vertices.Add(b + end);
                vertices.Add(b);
                uv.Add(new Vector2(u0, 0f));
                uv.Add(new Vector2(u0, repeats));
                uv.Add(new Vector2(u1, repeats));
                uv.Add(new Vector2(u1, 0f));
                indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        static void PlaceDeck(Transform parent, Mesh mesh, Vector3 position)
        {
            var surface = new GameObject("Weathered bridge deck", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = position;
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ZSRVisuals.AsphaltDeluxe;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The last moving deck can end at z=92 just before recycling.
        // Overlap underneath it and extend beyond the far clip in the haze.
        public static GameObject BuildDistantDeck(Transform world)
        {
            const float center = 164f, length = 156f;
            var bridge = ZSRVisuals.Pivot("Distant continuation of the bridge", world, Vector3.zero);
            if (distantDeck == null) distantDeck = BuildDeckMesh(9.7f, length, .20f);
            PlaceDeck(bridge, distantDeck, new Vector3(0, -.06f, center));
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
                    var finish = Resources.Load<Texture2D>("ProductionArt/TrackPropAtlas_surface");
                    if (finish != null && paintedMetal.HasProperty("_MetallicGlossMap"))
                    {
                        paintedMetal.SetTexture("_MetallicGlossMap",finish);
                        paintedMetal.SetFloat("_Smoothness",1f);
                        paintedMetal.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    paintedMetal.name = "Track prop surface / shared";
                }
                return paintedMetal;
            }
        }
    }
}
