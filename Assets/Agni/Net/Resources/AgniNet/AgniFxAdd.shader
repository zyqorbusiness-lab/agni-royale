// Agni Royale — unlit additive particle shader (sparks, fire core). Alpha scales the added light.
Shader "AgniRoyale/FxAdd"
{
    Properties { _MainTex ("Tex", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One  ZWrite Off  Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct a { float4 v : POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
            v2f vert (a i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.c = i.c * _Color; return o; }
            fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.c; }
            ENDCG
        }
    }
}
