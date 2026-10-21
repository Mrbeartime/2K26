Shader "ProjectD/Mock/Soft Mist"
{
    Properties
    {
        _MistColor ("Mist Color", Color) = (0.13, 0.32, 0.36, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.35
        _NoiseScale ("World Noise Scale", Float) = 0.4
        _Drift ("Drift X/Z", Vector) = (0.018, 0.009, 0, 0)
        _Softness ("Depth Intersection Softness", Range(0.01, 3)) = 0.6
        _EdgeStart ("Soft Edge Start", Range(0, 0.95)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "SoftMist"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float4 _Drift;
                float _Opacity;
                float _NoiseScale;
                float _Softness;
                float _EdgeStart;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
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
            float Cloud(float2 p)
            {
                float n = Noise(p) * 0.57;
                n += Noise(p * 2.03 + 17.2) * 0.28;
                n += Noise(p * 4.01 + 8.3) * 0.15;
                return n;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cloudUV = (input.positionWS.xz + input.positionWS.y * float2(0.31, 0.19)) * _NoiseScale;
                float n = Cloud(cloudUV + _Time.y * _Drift.xy);
                float edge = 1.0 - smoothstep(_EdgeStart, 1.0, length((input.uv - 0.5) * 2.0));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = unity_OrthoParams.w > 0.5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
                float softIntersection = saturate((sceneDepth - input.eyeDepth) / max(0.01, _Softness));
                float alpha = _Opacity * edge * lerp(0.22, 1.0, smoothstep(0.18, 0.8, n)) * softIntersection;
                // Small screen-space dithering prevents stepped bands in pale mist
                // over a dark background, particularly in an 8-bit mockup export.
                float dither = (Hash(input.positionCS.xy + float2(0.31, 0.87)) - 0.5) / 255.0;
                alpha = saturate(alpha + dither * edge * softIntersection);
                half3 color = _MistColor.rgb * lerp(0.78, 1.16, n);
                return half4(color, saturate(alpha * _MistColor.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
