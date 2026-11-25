// Transparent Fresnel glass for the Compressor (blobs inside stay visible).
Shader "SignalLost/Glass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (0.6, 0.95, 1, 0.22)
        _RimColor ("Rim Color", Color) = (0.23, 0.94, 1, 1)
        _RimPower ("Rim Power", Float) = 3
        _RimStrength ("Rim Strength", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(abs(dot(n, viewDir))), _RimPower);
                half3 col = _BaseColor.rgb * 0.6 + _RimColor.rgb * rim * _RimStrength;
                half a = saturate(_BaseColor.a + rim * 0.55);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
