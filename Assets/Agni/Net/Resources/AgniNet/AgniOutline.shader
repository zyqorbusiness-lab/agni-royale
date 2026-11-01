// Agni Royale — loot outline: inverted hull, flat colour, no glow / no bloom (just a crisp rim so items read on the floor).
Shader "AgniRoyale/Outline"
{
    Properties { _Color ("Rim", Color) = (1,1,1,1) _Width ("Width (m)", Float) = 0.012 }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Cull Front  ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Width;
            struct a { float4 v : POSITION; float3 n : NORMAL; };
            struct v2f { float4 p : SV_POSITION; };
            v2f vert (a i)
            {
                v2f o; float3 wp = mul(unity_ObjectToWorld, i.v).xyz; float3 wn = normalize(UnityObjectToWorldNormal(i.n));
                float dist = length(_WorldSpaceCameraPos - wp); float w = _Width * clamp(dist * 0.35, 1.0, 6.0); // keep it visible from a few metres away
                o.p = UnityWorldToClipPos(wp + wn * w); return o;
            }
            fixed4 frag (v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
