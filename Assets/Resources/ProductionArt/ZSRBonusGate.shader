Shader "ZSR/Bonus Gate"
{
    Properties
    {
        [MainTexture] _BaseMap ("Veil or gate sign", 2D) = "white" {}
        _Tint ("Veil hue", Color) = (1, 1, 1, 1)
        _Drive ("Clock, contact, pressure, release", Vector) = (0, .5, 0, 0)
        _LabelCenter ("Horizontal atlas cell midpoint", Float) = .25
        _Visibility ("Display opacity", Range(0,1)) = 1
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Drive;
                half _IsVeil, _Visibility, _LabelCenter;
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

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half2 uv = input.uv;
                // Read the same atlas cell from either side of the overhead sign.
                if (IS_FRONT_VFACE(face, 0, 1) && _IsVeil < .5h)
                    uv.x = 2 * _LabelCenter - uv.x;

                const half separation = abs(uv.x - _Drive.y);
                const half bulge = (1 - smoothstep(.03h, .48h, separation)) *
                    _Drive.z * sin(3.14159h * saturate(uv.y));
                uv.y = saturate(uv.y - .10h * bulge * (1 - _Drive.w) * _IsVeil);
                const half4 image = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                const half border = 1 - smoothstep(.016h, .15h, min(input.uv.x, 1 - input.uv.x));
                const half falloff = smoothstep(0.0h, .15h, input.uv.y) *
                                     smoothstep(0.0h, .13h, 1 - input.uv.y);
                const half scan = smoothstep(.85h, .995h, frac(uv.y + _Drive.x));
                const half opening = smoothstep(.04h, .43h, separation) * .85h + .15h;
                const half wave = (1 - smoothstep(.018h, .09h, abs(separation - (.06h + .80h * _Drive.w)))) *
                    _Drive.z * (1 - _Drive.w);
                const half opacity = falloff * ((image.a * .31h + border * .16h + scan * .026h) *
                    lerp(1, opening, _Drive.z) + wave * .12h);
                const half3 veilColor = lerp(image.rgb, _Tint.rgb, .42h + wave * .35h);
                half4 color = lerp(image, half4(veilColor, opacity), _IsVeil);
                color.a *= _Visibility;
                return color;
            }
            ENDHLSL
        }
    }
}
