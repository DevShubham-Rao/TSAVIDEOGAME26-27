// Additive glow for particles, lines and trails. _Shape: 0 soft dot, 1 ring, 2 square, 3 line (across V).
Shader "SignalLost/ParticleAdditive"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 2
        _Shape ("Shape", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Shape;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float d = length(c) * 2.0;
                float a;
                if (_Shape < 0.5)
                {
                    a = pow(saturate(1.0 - d), 1.6);
                }
                else if (_Shape < 1.5)
                {
                    a = saturate(1.0 - abs(d - 0.82) * 7.0);
                }
                else if (_Shape < 2.5)
                {
                    a = step(max(abs(c.x), abs(c.y)), 0.44);
                }
                else
                {
                    a = pow(saturate(1.0 - abs(i.uv.y - 0.5) * 2.0), 1.5);
                }
                return half4(i.color.rgb * _Intensity, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
