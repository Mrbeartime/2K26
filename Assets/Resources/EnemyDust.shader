Shader "ProjectD/EnemyDust"
{
 Properties { _BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _Progress("Dissolve",Range(0,1))=0 _Dust("Particle",Float)=0 _AlphaClip("Alpha Clip",Float)=0 _Cutoff("Cutoff",Range(0,1))=.5 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor; float _Progress, _Dust, _AlphaClip, _Cutoff;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float4 color:COLOR; };
   V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv; o.color=i.color; return o; }
   float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
   float noise(float3 p) {
    float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
   }
   half4 frag(V i):SV_Target {
    if(_Dust>.5) { float a=saturate(1-length(i.uv*2-1)); return half4(i.color.rgb,i.color.a*a*a); }
    float n=noise(i.world*22); clip(n-_Progress*1.02);
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*_BaseMap_ST.xy+_BaseMap_ST.zw)*_BaseColor;
    if(_AlphaClip>.5) clip(c.a-_Cutoff); c.rgb=lerp(c.rgb,float3(.045,.04,.035),saturate(_Progress*2)); return half4(c.rgb,1);
   }
   ENDHLSL
  }
 }
}
