// Agni Royale — "Agni Rekha" zone curtain: warm haze at the base fading to clear, slow drifting embers/noise.
Shader "AgniRoyale/ZoneCurtain"
{
    Properties { _MainTex ("Noise", 2D) = "white" {} _Color ("Tint", Color) = (1,0.55,0.2,0.5) _Scroll ("Scroll", Float) = 0 _Tiling ("Tiling", Float) = 20 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha  ZWrite Off  Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; fixed4 _Color; float _Scroll, _Tiling;
            struct a { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float h = i.uv.y;                                   // 0 = 20 m under ground, 1 = top
                float n1 = tex2D(_MainTex, float2(i.uv.x * _Tiling, h * 3.0 - _Scroll * 2.0)).r;
                float n2 = tex2D(_MainTex, float2(i.uv.x * _Tiling * 0.37 + 0.31, h * 1.3 - _Scroll)).r;
                float body = saturate(1.0 - (h - 0.14) * 3.2) * smoothstep(0.12, 0.16, h);   // dense near the ground, gone by ~60 m
                float a = body * (0.32 + 0.38 * n1 * n2);
                fixed3 col = lerp(_Color.rgb, fixed3(1.0, 0.86, 0.62), saturate(n1 * n2 * 1.4 - 0.2));
                return fixed4(col, a * _Color.a);
            }
            ENDCG
        }
    }
}
