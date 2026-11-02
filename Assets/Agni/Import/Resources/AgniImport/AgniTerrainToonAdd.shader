// Agni Royale v0.2.2 — TerrainToon additive pass for terrain layers 5-8 (Unity binds the second control map and layers here).
Shader "Hidden/AgniRoyale/TerrainToonAdd"
{
    Properties
    {
        [HideInInspector] _Control ("Control", 2D) = "black" {}
        [HideInInspector] _Splat0 ("L0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("L1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("L2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("L3", 2D) = "grey" {}
        _TerrSat ("Saturation", Range(0,2)) = 1.08
        _TerrBright ("Brightness", Range(0.5,1.5)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Geometry-99" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "TerrainToonAdd" Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AgniToon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Control_ST; float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST; half _TerrSat, _TerrBright;
            CBUFFER_END
            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0); TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; float3 nws:TEXCOORD1; float3 pws:TEXCOORD2; half fog:TEXCOORD3; half3 sh:TEXCOORD4; float4 uv01:TEXCOORD5; float4 uv23:TEXCOORD6; };
            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz); o.cs = p.positionCS; o.pws = p.positionWS;
                o.nws = TransformObjectToWorldNormal(i.n); o.sh = SampleSH(o.nws); o.fog = ComputeFogFactor(p.positionCS.z);
                o.uv = i.uv * _Control_ST.xy + _Control_ST.zw;
                o.uv01 = float4(i.uv * _Splat0_ST.xy + _Splat0_ST.zw, i.uv * _Splat1_ST.xy + _Splat1_ST.zw);
                o.uv23 = float4(i.uv * _Splat2_ST.xy + _Splat2_ST.zw, i.uv * _Splat3_ST.xy + _Splat3_ST.zw);
                return o;
            }
            half3 Albedo(V i, out half wsum)
            {
                // the painterly anime terrain: soft stylised layer textures; a second, 3.7x larger sample breaks up tiling
                half4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv); wsum = dot(w, half4(1,1,1,1));
                half3 c = 0;
                c += w.r * (SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.uv01.xy).rgb * 0.6h + SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.uv01.xy * 0.27).rgb * 0.4h);
                c += w.g * (SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, i.uv01.zw).rgb * 0.6h + SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, i.uv01.zw * 0.27).rgb * 0.4h);
                c += w.b * (SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, i.uv23.xy).rgb * 0.6h + SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, i.uv23.xy * 0.27).rgb * 0.4h);
                c += w.a * (SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, i.uv23.zw).rgb * 0.6h + SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, i.uv23.zw * 0.27).rgb * 0.4h);
                return c;
            }

            half4 frag(V i) : SV_Target
            {
                half ws; half3 alb = Albedo(i, ws);
                clip(ws - 0.002h);
                alb = AgniGrade(alb, _TerrSat) * _TerrBright;   // grading is linear in albedo, so the two passes still add up
                half3 c = AgniToonShade(alb, normalize(i.nws), i.pws, i.sh, 0.75h);
                return half4(MixFogColor(c, half3(0,0,0), i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
