using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvivalRun
{
    static class ZSRCityArchitecture
    {
        static readonly Material[] ChapterMaterials = new Material[3];

        public static void Place(Transform parent, int chapter, int profile, int wear,
            Vector3 position, Vector3 size)
        {
            var building = ZSRVisuals.MeshPart("zsr_city_" + profile + "_" + wear, "Weathered city block",
                parent, position, size, MaterialForChapter(chapter));
            if (building.TryGetComponent<Renderer>(out var renderer))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material MaterialForChapter(int chapter)
        {
            var material = ChapterMaterials[chapter];
            if (material != null) return material;

            Color tint = chapter switch
            {
                1 => new Color(1f, .84f, .72f),
                2 => new Color(.77f, .88f, 1f),
                _ => new Color(.91f, .95f, 1f)
            };
            material = ZSRVisuals.MatWithTexture(tint, "Textures/city_facades", .21f,
                missingTextureColor: new Color(.18f, .21f, .26f));
            material.name = "Weathered city facades " + (chapter + 1);
            var emissive = Resources.Load<Texture2D>("Textures/city_facades_emission");
            if (emissive != null && material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", emissive);
                material.SetColor("_EmissionColor", new Color(.67f, .67f, .67f));
                material.EnableKeyword("_EMISSION");
            }
            ChapterMaterials[chapter] = material;
            return material;
        }
    }
}
