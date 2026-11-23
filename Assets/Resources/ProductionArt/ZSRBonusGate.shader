Shader "ZSR/Bonus Gate"
{
    Properties
    {
        [MainTexture] _BaseMap ("Veil or gate sign", 2D) = "white" {}
        _Tint ("Veil hue", Color) = (1, 1, 1, 1)
        [Toggle] _IsVeil ("Animate veil", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _IsVeil;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const half4 image = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                const half border = 1 - smoothstep(.016h, .15h, min(input.uv.x, 1 - input.uv.x));
                const half falloff = smoothstep(0.0h, .15h, input.uv.y) *
                                     smoothstep(0.0h, .13h, 1 - input.uv.y);
                const half scan = smoothstep(.74h, .995h, frac(input.uv.y + _Time.y * .18h));
                const half opacity = falloff * (image.a * .34h + border * .17h + scan * .026h);
                const half3 veilColor = lerp(image.rgb, _Tint.rgb, .42h);
                return lerp(image, half4(veilColor, opacity), _IsVeil);
            }
            ENDHLSL
        }
    }
}
