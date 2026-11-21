using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvivalRun
{
    /// <summary>Two clean metal pylons and a translucent tinted curtain (no rings).</summary>
    public sealed class ZSRVeilGateView : MonoBehaviour
    {
        Material veilMaterial;
        Material accentMaterial;
        Mesh veilMesh;
        Transform veilTransform;
        float seed;
        Color tint;

        public static void Build(Transform parent, BonusKind kind)
        {
            var view = parent.gameObject.AddComponent<ZSRVeilGateView>();
            view.Construct(parent, kind);
        }

        static Color Tint(BonusKind kind)
        {
            switch(kind)
            {
                case BonusKind.DoubleDamage: return new Color(1f, .42f, .12f);
                case BonusKind.RapidFire: return new Color(.22f, .83f, 1f);
                case BonusKind.Shield: return new Color(.24f, .48f, 1f);
                case BonusKind.Heal: return new Color(.27f, 1f, .57f);
                case BonusKind.Magnet: return new Color(1f, .78f, .20f);
                default: return new Color(.70f, .41f, 1f);
            }
        }

        static string Title(BonusKind kind)
        {
            switch(kind)
            {
                case BonusKind.DoubleDamage: return "x2 DAMAGE";
                case BonusKind.RapidFire: return "RAPID FIRE";
                case BonusKind.Shield: return "SHIELD";
                case BonusKind.Heal: return "HEAL";
                case BonusKind.Magnet: return "MAGNET";
                default: return "DRONE";
            }
        }

        static string TextureName(BonusKind kind)
        {
            switch(kind)
            {
                case BonusKind.DoubleDamage: return "damage";
                case BonusKind.RapidFire: return "rapid";
                case BonusKind.Shield: return "shield";
                case BonusKind.Heal: return "heal";
                case BonusKind.Magnet: return "magnet";
                default: return "drone";
            }
        }

        void Construct(Transform parent, BonusKind kind)
        {
            seed = Random.Range(0f, 6.28f);
            tint = Tint(kind);
            accentMaterial = ZSRVisuals.Mat(tint, .35f, 1.65f);
            Material accent = accentMaterial;
            Material steel = ZSRVisuals.MetalWorn;
            Material dark = ZSRVisuals.Gunmetal;
            for (int side=-1; side<=1; side+=2)
            {
                Transform pillar = ZSRVisuals.Pivot(side<0 ? "LEFT / clean bonus pillar" : "RIGHT / clean bonus pillar",
                    parent, new Vector3(side*1.43f, 0f, 0f));
                ZSRVisuals.Box("chamfered graphite column",pillar,
                    new Vector3(0, 1.27f, 0), new Vector3(.25f, 2.45f, .31f),steel);
                ZSRVisuals.Box("inner dark metallic groove",pillar,
                    new Vector3(-side*.11f, 1.29f, -.18f), new Vector3(.045f, 2.23f, .045f),dark);
                ZSRVisuals.Box("vertical diffused LED",pillar,
                    new Vector3(-side*.138f, 1.28f, -.19f),new Vector3(.023f, 2.10f, .027f), accent);
                ZSRVisuals.Box("weighted foot",pillar,new Vector3(0, .09f, 0),
                    new Vector3(.46f, .18f, .50f),dark);
                ZSRVisuals.Box("anodized top cap",pillar,new Vector3(0, 2.52f, 0),
                    new Vector3(.36f, .12f, .40f),steel);
                ZSRVisuals.Box("top edge light",pillar,new Vector3(0, 2.57f, -.22f),
                    new Vector3(.28f, .026f, .025f),accent);
            }

            // The 2D veil is a deliberately translucent in-world mesh, not an opaque portal.
            GameObject sheet = new GameObject("TRANSPARENT COLOR VEIL",typeof(MeshFilter),typeof(MeshRenderer));
            sheet.transform.SetParent(parent, false);
            sheet.transform.localPosition = new Vector3(0,1.30f,-.025f);
            sheet.transform.localScale = new Vector3(2.63f,2.34f,1f);
            var mesh = new Mesh {name="Two-sided bonus veil"};
            mesh.vertices = new [] {new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            mesh.uv = new [] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            mesh.triangles = new [] {0,1,2,0,2,3,2,1,0,3,2,0}; // Front and back face: curtain remains visible from every camera angle.
            mesh.RecalculateNormals();
            veilMesh = mesh;
            sheet.GetComponent<MeshFilter>().sharedMesh=mesh;
            veilTransform=sheet.transform;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            veilMaterial = new Material(shader) {name="ZSR / " + kind + " transparent veil"};
            var texture = Resources.Load<Texture2D>("Textures/Gates/veil_" + TextureName(kind));
            if (texture != null)
            {
                if (veilMaterial.HasProperty("_BaseMap")) veilMaterial.SetTexture("_BaseMap",texture);
                if (veilMaterial.HasProperty("_MainTex")) veilMaterial.SetTexture("_MainTex",texture);
                veilMaterial.mainTexture = texture;
            }
            // Transparent URP configuration. Old built-in Sprite shader stays a safe fallback.
            if (veilMaterial.HasProperty("_Surface")) veilMaterial.SetFloat("_Surface",1f);
            if (veilMaterial.HasProperty("_Blend")) veilMaterial.SetFloat("_Blend",0f);
            if (veilMaterial.HasProperty("_SrcBlend")) veilMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
            if (veilMaterial.HasProperty("_DstBlend")) veilMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            if (veilMaterial.HasProperty("_ZWrite")) veilMaterial.SetFloat("_ZWrite",0f);
            if (veilMaterial.HasProperty("_Cull")) veilMaterial.SetFloat("_Cull",0f);
            veilMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            veilMaterial.renderQueue=(int)RenderQueue.Transparent;
            var renderer=sheet.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=veilMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;

            // Flat, readable label above pillars. No circular glyphs or hoops.
            var name=ZSRVisuals.WorldText(Title(kind),parent,
                new Vector3(0,2.72f,-.22f),.54f,Color.white);
            name.transform.localRotation=Quaternion.Euler(0,180,0);
        }

        void Update()
        {
            if (veilMaterial==null) return;
            if (veilTransform!=null)
            {
                // Gentle side-to-side energy flutter; never obscure or distort targets.
                float shimmer=Mathf.Sin(Time.time*1.35f+seed)*.009f;
                veilTransform.localScale=new Vector3(2.63f+shimmer,2.34f,1f);
            }
            float alpha=.83f + .06f*Mathf.Sin(Time.time*1.55f+seed); // Keep the road visible while making the veil readable.
            if (veilMaterial.HasProperty("_BaseColor")) veilMaterial.SetColor("_BaseColor",new Color(1f,1f,1f,alpha));
            if (veilMaterial.HasProperty("_Color")) veilMaterial.SetColor("_Color",new Color(1f,1f,1f,alpha));
        }
        void OnDestroy()
        {
            if (veilMaterial != null) Destroy(veilMaterial);
            if (accentMaterial != null) Destroy(accentMaterial);
            if (veilMesh != null) Destroy(veilMesh);
        }
    }
}
