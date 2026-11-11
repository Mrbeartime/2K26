Shader "ProjectD/UI/Outline"
{
    Properties
    {
        _OutlineThickness ("Thickness (px)", Range(0, 32)) = 2
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineOpacity ("Opacity", Range(0, 1)) = 0.6
        [Toggle(_SOURCE_IMAGE_OUTLINE)] _UseSourceImageAlpha ("Follow Source Image Alpha", Float) = 0
        _CornerRadius ("Corner Radius (px)", Range(0, 64)) = 8

        [HideInInspector] [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIOutline"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _SOURCE_IMAGE_OUTLINE
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                #ifdef UNITY_UI_CLIP_RECT
                float4 mask : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineOpacity;
                float _OutlineThickness;
                float _CornerRadius;
            CBUFFER_END

            sampler2D _MainTex;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.uv = input.uv;

                #ifdef UNITY_UI_CLIP_RECT
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                float2 projectionScale = abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float2 pixelSize = abs(output.position.w) / max(projectionScale, 1e-6);
                float2 softness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                output.mask = float4(
                    input.position.xy * 2.0 - rect.xy - rect.zw,
                    0.25 / max(0.25 * softness + pixelSize, 1e-6));
                #endif

                return output;
            }

            float RoundedRectDistance(float2 position, float2 halfSize, float radius)
            {
                float2 corner = abs(position) - halfSize + radius;
                return length(max(corner, 0.0)) + min(max(corner.x, corner.y), 0.0) - radius;
            }

            half SampleSourceAlpha(float2 uv)
            {
                // A standalone sprite can touch the texture edge. Treat samples beyond it as clear.
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
            }

            half SourceOutlineCoverage(float2 uv, float2 offsetX, float2 offsetY)
            {
                const float diagonal = 0.70710678;
                float2 offsetNE = (offsetX + offsetY) * diagonal;
                float2 offsetNW = (offsetY - offsetX) * diagonal;

                half source = SampleSourceAlpha(uv);
                half exterior = max(SampleSourceAlpha(uv + offsetX), SampleSourceAlpha(uv - offsetX));
                exterior = max(exterior, max(SampleSourceAlpha(uv + offsetY), SampleSourceAlpha(uv - offsetY)));
                exterior = max(exterior, max(SampleSourceAlpha(uv + offsetNE), SampleSourceAlpha(uv - offsetNE)));
                exterior = max(exterior, max(SampleSourceAlpha(uv + offsetNW), SampleSourceAlpha(uv - offsetNW)));

                // Subtract the original silhouette from the expanded silhouette.
                return saturate(exterior - source);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float thickness = max(_OutlineThickness, 0.0);
                #ifdef _SOURCE_IMAGE_OUTLINE
                half coverage = SourceOutlineCoverage(input.uv, uvDx * thickness, uvDy * thickness);
                #else
                // Full-rect UVs let one material measure any UI rectangle in render-target pixels.
                float2 uvPerPixel = sqrt(uvDx * uvDx + uvDy * uvDy);
                float2 rectSize = rcp(max(uvPerPixel, 1e-6));
                float2 halfSize = rectSize * 0.5;
                float2 position = (input.uv - 0.5) * rectSize;
                float radius = clamp(_CornerRadius, 0.0, min(halfSize.x, halfSize.y));
                float distanceToEdge = RoundedRectDistance(position, halfSize, radius);

                float outer = saturate(0.5 - distanceToEdge);
                float inner = saturate(0.5 - distanceToEdge - thickness);
                half coverage = outer - inner;
                #endif
                half alpha = coverage * _OutlineOpacity * _OutlineColor.a * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 clipCoverage = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                alpha *= clipCoverage.x * clipCoverage.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(_OutlineColor.rgb * input.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
