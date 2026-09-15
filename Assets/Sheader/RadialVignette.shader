Shader "Custom/RadialVignette"
{
    Properties
    {
        _Color ("Колір туману/хмар", Color) = (0.6, 0.6, 0.6, 1)
        _InnerRadius ("Внутрішній радіус (повна прозорість)", Range(0, 1)) = 0.2
        _OuterRadius ("Зовнішній радіус (повна непрозорість)", Range(0, 1.5)) = 0.9
        _Power ("Різкість переходу", Range(0.1, 5)) = 1.5
        _MainTex ("Текстура хмар (опційно)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _InnerRadius;
            float _OuterRadius;
            float _Power;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Відстань від центру екрану/квада (0,0 в центрі, ~0.7 в кутах)
                float2 centered = i.uv - float2(0.5, 0.5);
                float dist = length(centered);

                // Плавний перехід прозорість -> насиченість між Inner та Outer радіусом
                float t = saturate((dist - _InnerRadius) / (_OuterRadius - _InnerRadius));
                t = pow(t, _Power); // регулює різкість/м'якість переходу

                fixed4 texColor = tex2D(_MainTex, i.uv);
                fixed4 col = _Color * texColor;
                col.a *= t; // прозорий центр -> насичені краї

                return col;
            }
            ENDCG
        }
    }
}
