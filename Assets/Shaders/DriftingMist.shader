Shader "ProjectD/Drifting Mist"
{
    Properties
    {
        _Color ("Mist Color", Color) = (0.12, 0.3, 0.33, 1)
        _Density ("Density", Range(0, 1)) = 0.42
        _Scale ("Cloud Size (World Units)", Range(0.5, 12)) = 4
        _Speed ("Wind Speed", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "DriftingMist"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Density;
                float _Scale;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1.0), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Shared wind, different height phases: the two sheets never look identical.
                float phase = input.positionWS.y * 1.73;
                float t = _Time.y * _Speed;
                float2 p = input.positionWS.xz / max(0.5, _Scale) + phase;
                float2 wind = float2(t * 0.45, t * 0.18);
                // Slowly deform the cloud field as it drifts, rather than sliding a fixed texture.
                float2 curl = float2(Noise(p * 0.7 + t * 0.09),
                                    Noise(p * 0.7 + 13.1 - t * 0.07));
                float2 q = p - wind + (curl - 0.5) * 1.1;
                float cloud = Noise(q) * 0.65
                            + Noise(q * 2.03 + float2(t * 0.11, -t * 0.08)) * 0.25
                            + Noise(q * 4.07 - t * 0.13) * 0.1;

                float edge = 1.0 - smoothstep(0.18, 1.0, length((input.uv - 0.5) * 2.0));
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
                float softIntersection = saturate((sceneDepth - input.eyeDepth) / 0.7);
                float alpha = _Density * _Color.a * edge * softIntersection * smoothstep(0.12, 0.85, cloud);
                // Dither only occupied mist pixels to soften bands against the dark background.
                alpha = saturate(alpha + (Hash(input.positionCS.xy) - 0.5) / 255.0 * edge * softIntersection * _Density);
                return half4(_Color.rgb * lerp(0.8, 1.15, cloud), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
