// SPEC-ASSUMPTION: hand-written URP HLSL in place of SG_Planet.shadergraph.
// Banded gas-giant gradient + Fresnel atmosphere.
Shader "SignalLost/Planet"
{
    Properties
    {
        _ColorA ("Band A", Color) = (0.42, 0.25, 0.75, 1)
        _ColorB ("Band B", Color) = (0.16, 0.62, 0.78, 1)
        _ColorC ("Band C", Color) = (0.95, 0.62, 0.55, 1)
        _AtmoColor ("Atmosphere", Color) = (0.45, 0.9, 1, 1)
        _Bands ("Bands", Float) = 11
        _AtmoStrength ("Atmosphere Strength", Float) = 2.2
        _Brightness ("Brightness", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half4 _AtmoColor;
                float _Bands;
                float _AtmoStrength;
                float _Brightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = normalize(i.positionOS);
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float swirl = sin(p.x * 5.0 + p.z * 3.0 + _Time.y * 0.03) * 0.35 + sin(p.z * 9.0 - p.x * 4.0) * 0.12;
                float b = sin((p.y + swirl * 0.15) * _Bands) * 0.5 + 0.5;
                float b2 = sin((p.y + swirl * 0.1) * _Bands * 2.3 + 1.3) * 0.5 + 0.5;
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, b);
                col = lerp(col, _ColorC.rgb, smoothstep(0.75, 1.0, b2) * 0.6);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(n, mainLight.direction) * 0.8 + 0.25);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), 3.0);
                half3 outCol = col * ndl * _Brightness + _AtmoColor.rgb * rim * _AtmoStrength;
                return half4(outCol, 1.0);
            }
            ENDHLSL
        }
    }
}
