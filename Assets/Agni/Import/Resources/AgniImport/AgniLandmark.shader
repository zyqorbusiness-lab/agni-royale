// Agni Royale — landmark surface (Blender landmark kit): albedo x tint x per-piece variation (vertex B) x grime (vertex G x noise) x mild AO (vertex R),
// tangent-space normal map, URP PBR lighting with shadows + fog. No emission, no bloom.
Shader "AgniRoyale/Landmark"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal (GL)", 2D) = "bump" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _BumpScale ("Normal strength", Float) = 1
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Grime ("Grime", Range(0,1.5)) = 0.6
        _Hue ("Piece variation", Range(0,0.3)) = 0.06
        _UseVC ("Use vertex colour", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _BumpMap_ST; half4 _Tint; half _BumpScale; half _Smoothness; half _Metallic; half _Grime; half _Hue; half _UseVC;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AgniToon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap); TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 t : TANGENT; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; float3 ws : TEXCOORD1; half3 n : TEXCOORD2; half4 t : TEXCOORD3; half4 col : TEXCOORD4; float3 os : TEXCOORD5; float fog : TEXCOORD6; float4 sh : TEXCOORD7; };
            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float vnoise(float3 x) { float3 i = floor(x); float3 f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1,0,0)), f.x), lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), f.x), lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), f.x), f.y), f.z); }
            V vert(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a);
                VertexPositionInputs p = GetVertexPositionInputs(a.pos.xyz); VertexNormalInputs nn = GetVertexNormalInputs(a.n, a.t);
                o.cs = p.positionCS; o.ws = p.positionWS; o.uv = TRANSFORM_TEX(a.uv, _BaseMap); o.n = nn.normalWS;
                o.t = half4(nn.tangentWS, a.t.w * GetOddNegativeScale()); o.col = a.col; o.os = a.pos.xyz; o.fog = ComputeFogFactor(p.positionCS.z);
                o.sh = float4(SampleSH(nn.normalWS), 0);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 alb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _Tint.rgb;
                half4 vc = _UseVC > 0.5 ? i.col : half4(1, 0, 0.5, 1);
                alb *= lerp(1 - _Hue, 1 + _Hue, vc.b);
                float nz = vnoise(i.os * 0.9) * 0.6 + vnoise(i.os * 2.7) * 0.4;
                half g = saturate(vc.g * smoothstep(0.35, 0.7, nz) * _Grime);
                alb = lerp(alb, alb * half3(0.32, 0.27, 0.22), g);
                alb *= lerp(1, vc.r, 0.35);
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 nrm = normalize(i.n); half3 tan = normalize(i.t.xyz); half3 bit = cross(nrm, tan) * i.t.w;
                half3 nws = normalize(TransformTangentToWorld(nt, half3x3(tan, bit, nrm)));
                // v0.2.2 anime world: the same soft toon ramp as houses/terrain (normal map kept at half strength for a little form)
                nws = normalize(lerp(nrm, nws, 0.5));
                half3 soft = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.uv, 1.2).rgb * _Tint.rgb;
                alb = lerp(alb, alb * soft / max(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _Tint.rgb, 0.02), 0.5);
                alb = AgniGrade(alb, 1.16) * 1.05;
                half4 c = half4(AgniToonShade(alb, nws, i.ws, i.sh.rgb, 0.8), 1);
                c.rgb = MixFog(c.rgb, i.fog); c.a = 1; return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline" Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vo
            #pragma fragment fo
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vo(A a)
            {   // v0.2.2: light anime outline (switched off on LOW/NORMAL by AgniAnime)
                V o; UNITY_SETUP_INSTANCE_ID(a);
                float3 ws = TransformObjectToWorld(a.pos.xyz); float3 n = normalize(TransformObjectToWorldNormal(a.n));
                float k = clamp(distance(ws, GetCameraPositionWS()) / 10.0, 0.35, 4.0);
                o.cs = TransformWorldToHClip(ws + n * 0.028 * k); o.uv = TRANSFORM_TEX(a.uv, _BaseMap); o.fog = ComputeFogFactor(o.cs.z); return o;
            }
            half4 fo(V i) : SV_Target { half3 a = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, i.uv, 4).rgb * _Tint.rgb; return half4(MixFog(lerp(half3(0.16,0.12,0.16), a * 0.38, 0.5), i.fog), 1); }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vs(A a) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 ws = TransformObjectToWorld(a.pos.xyz); float3 nws = TransformObjectToWorldNormal(a.n);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - ws);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }
            half4 fs() : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vs(A a) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(a); return TransformObjectToHClip(a.pos.xyz); }
            half4 fs() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs : SV_POSITION; half3 n : TEXCOORD0; };
            V vs(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.cs = TransformObjectToHClip(a.pos.xyz); o.n = TransformObjectToWorldNormal(a.n); return o; }
            half4 fs(V i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
