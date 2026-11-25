// SPEC-ASSUMPTION: hand-written skybox in place of SG_Skybox.shadergraph.
// Dark gradient + star noise + purple/teal nebula noise. Set in Lighting > Environment.
Shader "SignalLost/Skybox"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.03, 0.04, 0.13, 1)
        _BottomColor ("Bottom", Color) = (0.015, 0.02, 0.07, 1)
        _NebulaA ("Nebula A", Color) = (0.55, 0.22, 1, 1)
        _NebulaB ("Nebula B", Color) = (0.08, 0.75, 0.8, 1)
        _NebulaIntensity ("Nebula Intensity", Float) = 0.9
        _StarDensity ("Star Threshold", Range(0.9, 0.999)) = 0.965
        _StarBrightness ("Star Brightness", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _NebulaA;
                half4 _NebulaB;
                float _NebulaIntensity;
                float _StarDensity;
                float _StarBrightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.03 + float3(1.7, 9.2, 3.1);
                    a *= 0.5;
                }
                return v;
            }

            float Stars(float3 d, float scale, float threshold)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float3 f = frac(p) - 0.5;
                float h = Hash(cell);
                float3 offset = float3(Hash(cell + 3.1), Hash(cell + 7.7), Hash(cell + 1.3)) - 0.5;
                float dist = length(f - offset * 0.6);
                float star = step(threshold, h) * smoothstep(0.16, 0.0, dist);
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.5 + h * 4.0) + h * 40.0);
                return star * twinkle;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float g = saturate(d.y * 0.5 + 0.5);
                half3 col = lerp(_BottomColor.rgb, _TopColor.rgb, g);

                float n1 = Fbm(d * 2.2 + float3(0.0, 0.0, _Time.y * 0.004));
                float n2 = Fbm(d * 3.7 + float3(5.2, 1.3, 0.0));
                float band = exp(-pow((d.y - 0.1 + (n1 - 0.5) * 0.6) * 2.4, 2.0));
                float neb = smoothstep(0.45, 0.85, n1) * band;
                half3 nebula = lerp(_NebulaA.rgb, _NebulaB.rgb, smoothstep(0.35, 0.75, n2)) * neb * _NebulaIntensity;
                col += nebula * 0.55 + nebula * nebula * 0.6;

                float s = Stars(d, 180.0, _StarDensity) + Stars(d, 90.0, _StarDensity + 0.015) * 1.6;
                col += s * _StarBrightness * (0.8 + 0.2 * n2);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
