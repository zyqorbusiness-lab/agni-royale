// Agni Royale — simple mobile water for the sea, the river and the Kayal canal: opaque, teal body, fresnel sky tint,
// sun glint on two scrolling procedural ripple layers, fog. No transparency, no bloom.
Shader "AgniRoyale/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.05, 0.27, 0.33, 1)
        _Shallow ("Shallow / grazing", Color) = (0.30, 0.55, 0.58, 1)
        _Sky ("Sky reflection", Color) = (0.62, 0.74, 0.84, 1)
        _Ripple ("Ripple scale", Float) = 0.55
        _Speed ("Speed", Float) = 0.35
        _Gloss ("Glint power", Float) = 420
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Deep, _Shallow, _Sky; float _Ripple, _Speed, _Gloss;
            CBUFFER_END
            struct A { float4 pos : POSITION; };
            struct V { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; float fog : TEXCOORD1; };
            float h2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(h2(i), h2(i + float2(1,0)), f.x), lerp(h2(i + float2(0,1)), h2(i + float2(1,1)), f.x), f.y); }
            float hgt(float2 p) { float t = _Time.y * _Speed; return vn(p * _Ripple + float2(t, t * 0.6)) * 0.6 + vn(p * _Ripple * 2.3 - float2(t * 0.8, -t)) * 0.4; }
            V vert(A a) { V o; VertexPositionInputs p = GetVertexPositionInputs(a.pos.xyz); o.cs = p.positionCS; o.ws = p.positionWS; o.fog = ComputeFogFactor(p.positionCS.z); return o; }
            half4 frag(V i) : SV_Target
            {
                float e = 0.35; float2 q = i.ws.xz;
                float hx = hgt(q + float2(e, 0)) - hgt(q - float2(e, 0)); float hz = hgt(q + float2(0, e)) - hgt(q - float2(0, e));
                float3 n = normalize(float3(-hx * 0.9, 1, -hz * 0.9));
                float3 v = normalize(GetCameraPositionWS() - i.ws);
                float fres = pow(1 - saturate(dot(n, v)), 4);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                half3 body = lerp(_Deep.rgb, _Shallow.rgb, saturate(fres * 1.6)) * (0.55 + 0.45 * saturate(L.direction.y + 0.3)) ;
                half3 col = lerp(body, _Sky.rgb, saturate(fres * 0.85));
                float3 hv = normalize(L.direction + v); float spec = pow(saturate(dot(n, hv)), _Gloss) * 0.9 * L.shadowAttenuation;
                col += L.color * spec;
                col = MixFog(col, i.fog); return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vs(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 fs() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
