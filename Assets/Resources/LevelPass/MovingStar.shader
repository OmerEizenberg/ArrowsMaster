Shader "Unlit/MovingStar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color1 ("Color Left", Color) = (0, 0.2, 0.5, 1)
        _Color2 ("Color Right", Color) = (0, 0.5, 1, 1)
        _Speed ("Sweep Speed", Float) = 1.0
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarDensity ("Star Density", Float) = 10.0
        _StarSize ("Star Size", Range(0, 0.1)) = 0.02
        _TwinkleSpeed ("Twinkle Speed", Float) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color1;
            fixed4 _Color2;
            float _Speed;
            fixed4 _StarColor;
            float _StarDensity;
            float _StarSize;
            float _TwinkleSpeed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            // Simple hash function for randomness
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Moving Gradient (Left to Right)
                // We use time and sine to create a smooth looping transition
                float sweep = sin(i.texcoord.x * 2.0 + _Time.y * _Speed) * 0.5 + 0.5;
                fixed4 gradientColor = lerp(_Color1, _Color2, sweep);

                // 2. Random Twinkling Stars
                float2 starUV = i.texcoord * _StarDensity;
                float2 gv = frac(starUV) - 0.5; // Grid cell coordinates
                float2 id = floor(starUV);      // Grid cell ID
                
                float n = hash(id); // Random value per cell
                
                float star = 0;
                if (n > 0.9) { // Only some cells have stars
                    // Twinkle effect based on time and the random ID
                    float twinkle = sin(_Time.y * _TwinkleSpeed + n * 10.0) * 0.5 + 0.5;
                    float dist = length(gv);
                    star = smoothstep(_StarSize, _StarSize * 0.5, dist) * twinkle;
                }

                // 3. Combine with Main Texture (the image itself)
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
                
                // Mix the gradient into the image colors
                col.rgb *= gradientColor.rgb;
                
                // Add the stars on top
                col.rgb += star * _StarColor.rgb * col.a;

                return col;
            }
            ENDCG
        }
    }
}
