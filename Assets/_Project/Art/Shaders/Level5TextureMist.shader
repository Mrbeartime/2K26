Shader "ProjectD/Level 5 Texture Mist"
{
    Properties
    {
        _MainTex ("Mist Texture", 2D) = "white" {}
        _Color ("Mist Color", Color) = (0.12, 0.3, 0.33, 1)
        _Density ("Density", Range(0, 1)) = 1
        _Scale ("Cloud Size (World Units)", Range(0.5, 12)) = 4
        _Speed ("Wind Speed", Range(0, 1)) = 0.14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
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
                float2 wind = _Time.y * _Speed * float2(0.45, 0.18);
                float2 cloudUV = (input.positionWS.xz / max(0.5, _Scale) - wind) * _MainTex_ST.xy + _MainTex_ST.zw;
                half cloud = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cloudUV).a;
                float edge = 1.0 - smoothstep(0.18, 1.0, length((input.uv - 0.5) * 2.0));
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
                float softIntersection = saturate((sceneDepth - input.eyeDepth) / 0.7);
                half alpha = _Density * _Color.a * edge * softIntersection * cloud;
                return half4(_Color.rgb * lerp(0.8, 1.15, cloud), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
