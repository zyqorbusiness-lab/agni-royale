// Agni Royale v0.2.2 — TerrainToon: the India map ground in the anime look (soft ramp, cool lavender shade, painterly layers).
// Unity terrain: first 4 layers here, layers 5-8 in the additive pass (Hidden/AgniRoyale/TerrainToonAdd), same lighting.
Shader "AgniRoyale/TerrainToon"
{
    Properties
    {
        [HideInInspector] _Control ("Control", 2D) = "red" {}
        [HideInInspector] _Splat0 ("L0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("L1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("L2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("L3", 2D) = "grey" {}
        _TerrSat ("Saturation", Range(0,2)) = 1.08
        _TerrBright ("Brightness", Range(0.5,1.5)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True" }
        Pass
        {
            Name "TerrainToon" Tags { "LightMode"="UniversalForward" }
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
                alb = AgniGrade(alb, _TerrSat) * _TerrBright;
                half3 c = AgniToonShade(alb, normalize(i.nws), i.pws, i.sh, 0.75h);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vs(float4 pos:POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }
            half4 fs() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float4 cs:SV_POSITION; half3 n:TEXCOORD0; };
            V vs(float4 pos:POSITION, float3 n:NORMAL) { V o; o.cs = TransformObjectToHClip(pos.xyz); o.n = TransformObjectToWorldNormal(n); return o; }
            half4 fs(V i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
    Dependency "AddPassShader" = "Hidden/AgniRoyale/TerrainToonAdd"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
