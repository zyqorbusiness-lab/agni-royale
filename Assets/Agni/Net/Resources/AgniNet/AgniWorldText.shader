// Agni Royale — world-space TextMesh shader (village signs, loot name tags): depth-tested, back faces culled, unlit.
Shader "AgniRoyale/WorldText"
{
    Properties { _MainTex ("Font", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha  ZWrite Off  ZTest LEqual  Cull Back
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
            fixed4 frag (v2f i) : SV_Target { fixed4 c = i.c; c.a *= tex2D(_MainTex, i.uv).a; return c; }
            ENDCG
        }
    }
}
