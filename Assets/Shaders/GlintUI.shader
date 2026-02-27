Shader "UI/LanaStudio/GlintUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)

        [Header(Glint Settings)]
        _GlintColor ("Glint Color", Color) = (1, 1, 1, 1)
        _GlintWidth ("Glint Width", Range(0, 1)) = 0.1
        _GlintSmoothness ("Glint Smoothness", Range(0.01, 0.5)) = 0.1
        _GlintAngle ("Glint Angle (Degrees)", Range(0, 360)) = 135
        _GlintSpeed ("Glint Speed", Range(0.1, 5)) = 1.0
        _GlintInterval ("Glint Interval (Seconds)", Range(1, 10)) = 3.0
        _GlintIntensity ("Glint Intensity", Range(0, 5)) = 1.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        Lighting Off
        ZWrite Off
        ZTest [Unity_GUI_ZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GlintColor;
            float _GlintWidth;
            float _GlintSmoothness;
            float _GlintAngle;
            float _GlintSpeed;
            float _GlintInterval;
            float _GlintIntensity;

            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float2 uv = IN.texcoord;
                float t = _Time.y;

                // Convert angle to radians
                float angleRad = _GlintAngle * 0.0174533;
                float2 dir = float2(cos(angleRad), sin(angleRad));
                
                // Project UV onto the direction vector
                float project = dot(uv - 0.5, dir) + 0.5;

                // Time logic: happens every _GlintInterval seconds
                float cycleTime = fmod(t, _GlintInterval);
                
                // Animate progress
                float progress = cycleTime * _GlintSpeed - 0.5;
                
                // Calculate primary glint
                float dist1 = abs(project - progress);
                float glint1 = smoothstep(_GlintWidth + _GlintSmoothness, _GlintWidth, dist1);
                
                // Calculate secondary glint (slightly offset and thinner)
                float dist2 = abs(project - (progress - _GlintWidth * 1.5));
                float glint2 = smoothstep(_GlintWidth * 0.5 + _GlintSmoothness, _GlintWidth * 0.5, dist2);
                
                float totalGlint = max(glint1, glint2 * 0.6); // Combine them
                
                // Apply intensity and color
                fixed3 glintEffect = _GlintColor.rgb * totalGlint * _GlintIntensity;
                
                // Mask by texture alpha
                color.rgb += glintEffect * color.a;

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                return color;
            }
        ENDCG
        }
    }
}
