Shader "Game/Red Sword Trail"
{
    Properties { _Glow("Glow", Range(0,2)) = 0.4 _Emission("HDR Emission", Float) = 6 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Glow;
                float _Emission;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color; o.uv=v.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float edge=saturate(1-abs(i.uv.x*2-1));
                float core=pow(edge,8);
                float3 color=max(i.color.rgb,0)*max(_Emission,0)*(.45+.55*core);
                return half4(color*(1+_Glow*core), i.color.a*smoothstep(0,.2,edge));
            }
            ENDHLSL
        }
    }
}
