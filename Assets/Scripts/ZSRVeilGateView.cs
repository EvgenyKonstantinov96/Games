using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvivalRun
{
    /// <summary>Beveled pylons, a translucent veil and a short, readable bonus sign.</summary>
    public sealed class ZSRVeilGateView : MonoBehaviour
    {
        Material veilMaterial, signMaterial, accentMaterial;
        Mesh veilMesh, signMesh;

        public static void Build(Transform parent, BonusKind kind)
        {
            parent.gameObject.AddComponent<ZSRVeilGateView>().Construct(parent, kind);
        }

        internal static (int slot, string label, string texture, Color tint, string title) Appearance(BonusKind kind)
        {
            return kind switch
            {
                BonusKind.DoubleDamage => (0, "×2", "damage", new Color(1f, .54f, .24f), "Двойной урон"),
                BonusKind.RapidFire => (1, "ТЕМП", "rapid", new Color(.34f, .83f, 1f), "Ускорение стрельбы"),
                BonusKind.Shield => (2, "ЩИТ", "shield", new Color(.47f, .64f, 1f), "Энергощит"),
                BonusKind.Heal => (3, "HP", "heal", new Color(.37f, .94f, .59f), "Восстановление здоровья"),
                BonusKind.Magnet => (4, "МАГНИТ", "magnet", new Color(1f, .79f, .29f), "Магнит монет"),
                _ => (5, "ДРОН", "drone", new Color(.78f, .58f, 1f), "Воздушная поддержка")
            };
        }

        void Construct(Transform parent, BonusKind kind)
        {
            var appearance = Appearance(kind);
            accentMaterial = ZSRVisuals.Mat(appearance.tint, .38f, 1.25f);
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = ZSRVisuals.Pivot(side < 0 ? "Gate pylon left" : "Gate pylon right",
                    parent, new Vector3(side * 1.33f, 0f, 0f));
                ZSRVisuals.MeshPart("track_gate_pylon", "Cast steel gate pylon", pillar,
                    Vector3.zero, Vector3.one, ZSRTrackArt.Surface);
                ZSRVisuals.Box("Recessed status light", pillar, new Vector3(-side * .092f, 1.40f, -.166f),
                    new Vector3(.039f, 1.53f, .021f), accentMaterial);
                ZSRVisuals.Box("Gate head light", pillar, new Vector3(0f, 2.43f, -.201f),
                    new Vector3(.168f, .045f, .025f), accentMaterial);
            }

            Shader displayShader = Resources.Load<Shader>("ProductionArt/ZSRBonusGate");
            if (displayShader == null || !displayShader.isSupported)
            {
                Debug.LogWarning("[ZSR ART] Bonus display shader unavailable for " + appearance.label);
                BuildFallbackSign(parent, appearance.label);
                return;
            }

            var veilTexture = Resources.Load<Texture2D>("Textures/Gates/veil_" + appearance.texture);
            veilMaterial = new Material(displayShader) { name = "Gate veil / " + appearance.texture };
            if (veilTexture != null) veilMaterial.SetTexture("_BaseMap", veilTexture);
            veilMaterial.SetColor("_Tint", appearance.tint);
            veilMaterial.SetFloat("_IsVeil", 1f);
            veilMesh = PlacePanel("Translucent bonus veil", parent, new Vector3(0f, 1.26f, -.015f),
                new Vector2(2.38f, 2.28f), veilMaterial, new Rect(0f, 0f, 1f, 1f));

            var labels = Resources.Load<Texture2D>("ProductionArt/TrackGateLabels");
            if (labels == null)
            {
                BuildFallbackSign(parent, appearance.label);
                return;
            }
            signMaterial = new Material(displayShader) { name = "Gate sign / " + appearance.label };
            signMaterial.SetTexture("_BaseMap", labels);
            signMaterial.renderQueue = (int)RenderQueue.Transparent + 1;
            var uv = new Rect(appearance.slot % 2 * .5f, .75f - appearance.slot / 2 * .25f, .5f, .25f);
            signMesh = PlacePanel("Bonus sign / " + appearance.label, parent, new Vector3(0f, 2.65f, -.26f),
                new Vector2(2.42f, 1.20f), signMaterial, uv);
        }

        static void BuildFallbackSign(Transform parent, string label)
        {
            ZSRVisuals.Box("Bonus sign backing", parent, new Vector3(0f, 2.65f, -.22f),
                new Vector3(2.22f, .92f, .07f), ZSRVisuals.Gunmetal);
            ZSRVisuals.WorldText(label, parent, new Vector3(0f, 2.65f, -.27f), .62f, Color.white)
                .fontStyle = FontStyle.Bold;
        }

        static Mesh PlacePanel(string name, Transform parent, Vector3 position, Vector2 size, Material material, Rect uv)
        {
            var panel = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-size.x / 2, -size.y / 2, 0f), new Vector3(size.x / 2, -size.y / 2, 0f),
                new Vector3(size.x / 2, size.y / 2, 0f), new Vector3(-size.x / 2, size.y / 2, 0f)
            };
            mesh.uv = new[] { uv.min, new Vector2(uv.xMax, uv.yMin), uv.max, new Vector2(uv.xMin, uv.yMax) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            panel.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        void OnDestroy()
        {
            Release(veilMaterial);
            Release(signMaterial);
            Release(accentMaterial);
            Release(veilMesh);
            Release(signMesh);
        }

        static void Release(Object art)
        {
            if (art == null) return;
            if (Application.isPlaying) Destroy(art);
            else DestroyImmediate(art);
        }
    }
}
