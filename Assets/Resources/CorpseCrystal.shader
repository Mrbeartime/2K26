Shader "Game/CorpseCrystal"
{
    Properties
    {
        [HDR] _CrystalColor("Crystal Color", Color) = (0.2,0.85,1,1)
        _Emission("Emission", Float) = 0.2
        _EdgeStrength("Edge Strength", Range(0,1)) = 1
        _BrightnessLimit("Brightness Limit", Float) = 1.1
        _Flash("Flash", Float) = 0
        _Dissolve("Body Dissolve", Range(0,1)) = 0
        _UseDissolve("Use Body Dissolve", Float) = 0
        _MinY("Body Bottom", Float) = 0
        _Height("Body Height", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _CrystalColor;
            float _Emission;
            float _Flash;
            float _Dissolve;
            float _UseDissolve;
            float _MinY;
            float _Height;
            float _EdgeStrength;
            float _BrightnessLimit;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 world:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                output.color=input.color;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float edgeDistance=min(min(input.uv.x,input.uv.y),abs(1-input.uv.x-input.uv.y));
                float edge=1-smoothstep(0,max(fwidth(edgeDistance)*1.5,0.008),edgeDistance);
                float3 color=saturate(_CrystalColor.rgb)*(0.6+clamp(_Emission,0,0.6));
                color+=edge*saturate(_EdgeStrength)*float3(0.1,0.18,0.25)+clamp(_Flash,0,0.15)*float3(0.2,0.3,0.35);
                color=min(color,clamp(_BrightnessLimit,0,1.1));
                float height=saturate((input.world.y-_MinY)/max(_Height,0.01));
                float noise=frac(sin(dot(floor(input.world*35),float3(12.9898,78.233,45.164)))*43758.5453);
                float threshold=height*0.8+noise*0.2;
                float bodyAlpha=lerp(1,1-smoothstep(threshold,threshold+0.06,_Dissolve*1.06),saturate(_UseDissolve));
                return half4(color*input.color.rgb,input.color.a*bodyAlpha);
            }
            ENDHLSL
        }
    }
}
