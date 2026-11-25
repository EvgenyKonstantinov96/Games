Shader "ZSR/Combat FX"
{
    Properties { _BaseMap ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Vertex { float4 position : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varying { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varying Vert(Vertex input)
            {
                Varying output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varying input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
