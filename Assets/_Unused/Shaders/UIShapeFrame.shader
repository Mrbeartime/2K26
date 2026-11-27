Shader "ProjectD/UI/Shape Frame"
{
    Properties
    {
        [Enum(Rectangle,0,Rounded Rectangle,1,Ellipse,2)] _Shape ("Shape", Float) = 0
        _FrameThickness ("Thickness (px)", Range(0, 32)) = 2
        _FrameColor ("Frame Color", Color) = (1, 1, 1, 1)
        _FrameOpacity ("Opacity", Range(0, 1)) = 1
        _CornerRadius ("Rounded Rectangle Radius (px)", Range(0, 128)) = 12
        [ToggleUI] _ShowSourceImage ("Show Source Image", Float) = 0

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
            Name "UIShapeFrame"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
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
                half4 _FrameColor;
                half _FrameOpacity;
                float _Shape;
                float _FrameThickness;
                float _CornerRadius;
                float _ShowSourceImage;
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

            float ShapeDistance(float2 position, float2 halfSize)
            {
                if (_Shape >= 1.5)
                {
                    // Convert the ellipse's implicit boundary to a pixel distance near its edge.
                    float2 radii = max(halfSize, 1e-4);
                    float2 normalized = position / radii;
                    float radialDistance = length(normalized);
                    float gradient = length(normalized / radii) / max(radialDistance, 1e-4);
                    return (radialDistance - 1.0) / max(gradient, 1e-4);
                }

                float radius = _Shape >= 0.5 ? clamp(_CornerRadius, 0.0, min(halfSize.x, halfSize.y)) : 0.0;
                float2 edge = abs(position) - halfSize + radius;
                return length(max(edge, 0.0)) + min(max(edge.x, edge.y), 0.0) - radius;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // The frame Image supplies a full 0..1 UV rectangle. Keep the thickness in pixels across UI sizes.
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float2 rectSize = rcp(max(sqrt(uvDx * uvDx + uvDy * uvDy), 1e-6));
                float distanceToEdge = ShapeDistance((input.uv - 0.5) * rectSize, rectSize * 0.5);
                float outer = saturate(0.5 - distanceToEdge);
                float inner = saturate(0.5 - distanceToEdge - max(_FrameThickness, 0.0));
                half frameAlpha = (outer - inner) * _FrameColor.a * _FrameOpacity;
                half4 output = half4(_FrameColor.rgb * frameAlpha, frameAlpha);

                if (_ShowSourceImage > 0.5)
                {
                    half4 source = tex2D(_MainTex, input.uv);
                    output += half4(source.rgb * source.a, source.a) * (1.0 - frameAlpha);
                }

                // Apply UI tint and CanvasGroup fade once to the composited frame and image.
                output.rgb *= input.color.rgb;
                output *= input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 clipCoverage = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                output *= clipCoverage.x * clipCoverage.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(output.a - 0.001);
                #endif

                return output;
            }
            ENDHLSL
        }
    }
}
