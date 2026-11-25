// SPEC-ASSUMPTION: hand-written URP HLSL instead of SG_BlobWobble.shadergraph so it can be authored as text.
// Two looks share one shader so every blob stays one cheap draw:
//   _Mode 0 = STATIC: wobbling blob wearing flickering black/white TV snow, scanlines and a rolling bar,
//                      with a soft colored rim so its color still reads.
//   _Mode 1 = BIT:    a crisp spinning octahedron gem, flat-shaded facets, white-hot core, HDR glow (bloom).
Shader "SignalLost/BlobWobble"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.23, 0.42, 1)
        _EmissionStrength ("Emission Strength", Float) = 2.6
        _Wobble ("Wobble", Float) = 0.05
        _WobbleSpeed ("Wobble Speed", Float) = 3
        _Phase ("Phase", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Rainbow ("Rainbow", Range(0, 1)) = 0
        _RimPower ("Rim Power", Float) = 2.2
        _Mode ("Mode (0 Static, 1 Bit)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EmissionStrength;
                float _Wobble;
                float _WobbleSpeed;
                float _Phase;
                float _Flash;
                float _Rainbow;
                float _RimPower;
                float _Mode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            float WobbleNoise(float3 p, float t)
            {
                return sin(p.x * 7.0 + t * 1.7) * sin(p.y * 6.0 + t * 1.3) * sin(p.z * 8.0 + t * 2.1)
                     + 0.5 * sin(p.y * 11.0 - t * 2.7 + p.x * 3.0);
            }

            // Precision-safe hash (inputs are kept small: huge values lose their fraction in float32).
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 Hue(float h)
            {
                float3 k = abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0;
                return saturate(k);
            }

            float3 RotY(float3 v, float a)
            {
                float s = sin(a), c = cos(a);
                return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
            }

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 p;
                float3 n = v.normalOS;
                if (_Mode > 0.5)
                {
                    // Bit: squeeze the sphere into an octahedron (L1 ball), then spin it. No wobble: crisp.
                    float3 dir = normalize(v.positionOS.xyz);
                    float l1 = abs(dir.x) + abs(dir.y) + abs(dir.z);
                    p = dir / max(l1, 0.0001) * length(v.positionOS.xyz) * 1.45;
                    float spin = _Time.y * 2.4 + _Phase;
                    p = RotY(p, spin);
                    n = RotY(dir, spin);
                }
                else
                {
                    float t = _Time.y * _WobbleSpeed + _Phase;
                    p = v.positionOS.xyz + v.normalOS * WobbleNoise(v.positionOS.xyz * 1.4, t) * _Wobble;
                    // Scanline jitter: rows of the blob slide sideways in time with the snow.
                    float row = fmod(floor(v.positionOS.y * 9.0 + 40.0) + fmod(floor(_Time.y * 18.0), 61.0), 97.0);
                    float tick = fmod(floor(_Time.y * 9.0), 53.0);
                    p.x += (Hash21(float2(row, fmod(_Phase, 13.0))) - 0.5) * 0.14 * step(0.72, Hash21(float2(row * 1.7, tick)));
                }
                VertexPositionInputs pos = GetVertexPositionInputs(p);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(n);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 baseCol = _BaseColor.rgb;
                if (_Rainbow > 0.5)
                {
                    baseCol = Hue(_Time.y * 0.25 + i.positionOS.y * 1.5 + dot(n, viewDir) * 0.6) * 1.1;
                }

                if (_Mode > 0.5)
                {
                    // ---- BIT: flat facets, white-hot core, HDR glow ----
                    float3 nf = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                    if (dot(nf, viewDir) < 0.0) nf = -nf;
                    Light mainLight = GetMainLight();
                    float facet = saturate(dot(nf, mainLight.direction)) * 0.5 + saturate(dot(nf, viewDir)) * 0.5;
                    float core = pow(saturate(dot(nf, viewDir)), 4.0);
                    float pulse = 1.0 + 0.18 * sin(_Time.y * 6.0 + _Phase * 3.0);
                    half3 body = baseCol * (0.6 + 1.0 * facet) * (1.0 + _EmissionStrength * 0.38 * pulse);
                    half3 hot = lerp(baseCol, half3(1, 1, 1), 0.4) * core * (0.7 + _EmissionStrength * 0.2);
                    half3 col = body + hot + _Flash * 1.6;
                    return half4(col, 1.0);
                }

                // ---- STATIC: TV snow ----
                float2 px = floor(i.positionCS.xy / 3.0);
                float frame = fmod(floor(_Time.y * 30.0), 61.0);
                float nz = Hash21(fmod(px, 509.0) + frame * float2(13.7, 7.1) + fmod(_Phase, 13.0));
                half g = nz > 0.5 ? lerp(0.55, 1.0, (nz - 0.5) * 2.0) : lerp(0.0, 0.22, nz * 2.0);
                half scan = fmod(px.y, 2.0) < 1.0 ? 1.0 : 0.62;
                float band = smoothstep(0.0, 0.12, frac(i.positionWS.y * 0.9 - _Time.y * 0.8));
                g *= scan * (0.82 + 0.38 * (1.0 - band));
                // Faint color tint plus a colored rim so red / blue / green / gold Static stay readable.
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                half3 tint = lerp(half3(1, 1, 1), baseCol * 1.7, 0.32);
                half3 col = g * tint + baseCol * _EmissionStrength * rim * 0.55 + _Flash * 1.4;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
