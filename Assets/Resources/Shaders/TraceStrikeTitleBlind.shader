Shader "UI/TraceStrikeTitleBlind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Blind Color", Color) = (0, 0, 0, 1)
        _RingColor ("Focus Ring Color", Color) = (0.15, 0.85, 1, 0.9)
        _Center ("Focus Center", Vector) = (0.5, 0.35, 0, 0)
        _Radius ("Focus Radius", Float) = 0.115
        _Aspect ("Screen Aspect", Float) = 1.777778
        _Feather ("Edge Feather", Float) = 0.006
        _RingWidth ("Focus Ring Width", Float) = 0.008
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            fixed4 _RingColor;
            float4 _Center;
            float _Radius;
            float _Aspect;
            float _Feather;
            float _RingWidth;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 delta = input.texcoord - _Center.xy;
                delta.x *= _Aspect;
                float distanceFromCenter = length(delta);
                float outside = smoothstep(_Radius - _Feather, _Radius, distanceFromCenter);
                float ringDistance = abs(distanceFromCenter - _Radius);
                float ring = 1.0 - smoothstep(_RingWidth * 0.25, _RingWidth, ringDistance);

                fixed3 rgb = lerp(_Color.rgb, _RingColor.rgb, ring);
                float alpha = max(_Color.a * outside, _RingColor.a * ring);
                return fixed4(rgb, alpha) * input.color;
            }
            ENDCG
        }
    }
}
