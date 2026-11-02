// Agni Royale v0.2.2 — WorldToon: anime shading for the map's houses, trees, bushes, props and vehicles (see AgniToon.hlsl).
// Optional alpha clip (leaves), optional spherical shading normals (tree crowns shade as soft balls, like anime backgrounds),
// optional light inverted-hull outline (houses/props only; the "Outline" pass is switched off on LOW/NORMAL by AgniAnime).
Shader "AgniRoyale/WorldToon"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Sat ("Saturation", Range(0,2)) = 1.18
        _Bright ("Brightness", Range(0.5,1.5)) = 1.04
        _Paint ("Painterly softening (mip bias)", Range(0,3)) = 1.2
        _ShadowStrength ("Received shadow strength", Range(0,1)) = 0.8
        _Sphere ("Spherical normals (tree crowns)", Range(0,1)) = 0
        _SphereCenter ("Sphere centre (object space)", Vector) = (0,3,0,0)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OutlineWidth ("Outline width at 10 m (world m)", Range(0,0.1)) = 0
        _OutlineColor ("Outline colour", Color) = (0.16,0.12,0.16,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _OutlineColor; float4 _SphereCenter;
            half _Sat, _Bright, _Paint, _ShadowStrength, _Sphere, _Cutoff, _AlphaClip, _Cull, _OutlineWidth;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonLit" Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AgniToon.hlsl"
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; float3 nws:TEXCOORD1; float3 pws:TEXCOORD2; half fog:TEXCOORD3; half3 sh:TEXCOORD4; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz); o.cs = p.positionCS; o.pws = p.positionWS;
                float3 n = TransformObjectToWorldNormal(i.n);
                float3 sp = TransformObjectToWorldDir(i.pos.xyz - _SphereCenter.xyz); sp *= rsqrt(max(dot(sp, sp), 1e-6));
                n = normalize(lerp(n, sp, _Sphere)); o.nws = n;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.fog = ComputeFogFactor(p.positionCS.z); o.sh = SampleSH(n);
                return o;
            }
            half4 frag(V i, bool front:SV_IsFrontFace) : SV_Target
            {
                half4 t0 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
            #if defined(_ALPHATEST_ON)
                clip(t0.a - _Cutoff);
            #endif
                half3 soft = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.uv, _Paint).rgb;
                half3 alb = lerp(t0.rgb, soft, 0.65h) * _BaseColor.rgb;
                alb = AgniGrade(alb, _Sat) * _Bright;
                float3 n = normalize(i.nws); n = (front || _Sphere > 0.5) ? n : -n;
                half3 c = AgniToonShade(alb, n, i.pws, i.sh, _ShadowStrength);
                c = MixFog(c, i.fog); return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline" Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                float3 pws = TransformObjectToWorld(i.pos.xyz); float3 nws = normalize(TransformObjectToWorldNormal(i.n));
                float d = distance(pws, GetCameraPositionWS()); float k = clamp(d / 10.0, 0.35, 4.0);
                o.cs = TransformWorldToHClip(pws + nws * _OutlineWidth * k);
                if (_OutlineWidth <= 0) o.cs = float4(0, 0, -10, 1); // no outline on this material: cull the hull
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.fog = ComputeFogFactor(o.cs.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 a = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, i.uv, 4).rgb * _BaseColor.rgb;
                half3 c = lerp(_OutlineColor.rgb, a * 0.38, 0.5);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; };
            V vs(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a);
                float3 ws = TransformObjectToWorld(a.pos.xyz); float3 nws = TransformObjectToWorldNormal(a.n);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - ws);
            #else
                float3 ld = _LightDirection;
            #endif
                o.cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
            #if UNITY_REVERSED_Z
                o.cs.z = min(o.cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.cs.z = max(o.cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap); return o;
            }
            half4 fs(V i) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; };
            V vs(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.cs = TransformObjectToHClip(a.pos.xyz); o.uv = TRANSFORM_TEX(a.uv, _BaseMap); return o; }
            half4 fs(V i) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs:SV_POSITION; half3 n:TEXCOORD0; float2 uv:TEXCOORD1; };
            V vs(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.cs = TransformObjectToHClip(a.pos.xyz); o.n = TransformObjectToWorldNormal(a.n); o.uv = TRANSFORM_TEX(a.uv, _BaseMap); return o; }
            half4 fs(V i) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #endif
                return half4(normalize(i.n), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
