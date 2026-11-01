// Agni Royale — Anime Toon + Outline (Unity 6, URP 17). Simple, mobile-friendly.
// Pass 1 "ToonLit": 2-step cel ramp from the main light, warm shadow tint, soft rim, shade mask in _BaseMap.a
//   (alpha 1 = fully toon-shaded skin/clothes/hair, ~0.15 = eyes (almost unlit), 0 = lash line).
// Pass 2 "Outline": inverted hull — vertices pushed along normals in clip space, front faces culled.
//   Width can be thinned per vertex with vertex colour R (optional; 1 = full width). Face ≈ 0.25, eyes 0.
// Material setup: one material per slot — Skin, Clothes, Hair (all this shader). The 4th slot ("Outline") in the
// FBX is only a name: the outline is this shader's 2nd pass, so no extra mesh/tris are shipped.
Shader "AgniRoyale/AnimeToonOutline"
{
    Properties
    {
        _BaseMap ("Base (RGB) ShadeMask (A)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint (skin ~0.96,0.74,0.66 / clothes ~0.62,0.58,0.74)", Color) = (0.96,0.74,0.66,1)
        _Step ("Shadow Step (N·L)", Range(-1,1)) = -0.06
        _MidStep ("Mid band start", Range(-1,1)) = -0.2
        _MidLevel ("Mid band level", Range(0,1)) = 0.55
        _Softness ("Edge softness", Range(0.001,0.2)) = 0.02
        _RimColor ("Rim Color", Color) = (1,0.86,0.7,1)
        _RimPower ("Rim threshold (1-N·V)", Range(0,1)) = 0.62
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _OutlineColor ("Outline Color", Color) = (0.10,0.06,0.09,1)
        _OutlineWidth ("Outline Width (world m)", Range(0,0.01)) = 0.0042
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off for clothes/hair cards)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _ShadowTint, _RimColor, _OutlineColor;
            half _Step, _MidStep, _MidLevel, _Softness, _RimPower, _RimStrength, _OutlineWidth, _Cull;
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; float3 nws:TEXCOORD1; float3 pws:TEXCOORD2; half fog:TEXCOORD3; };
            V vert(A i){ V o; VertexPositionInputs p=GetVertexPositionInputs(i.pos.xyz); o.cs=p.positionCS; o.pws=p.positionWS;
                o.nws=TransformObjectToWorldNormal(i.n); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z); return o; }
            half4 frag(V i, bool front:SV_IsFrontFace):SV_Target{
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                float3 n=normalize(i.nws); n = front ? n : -n;
                Light L=GetMainLight(TransformWorldToShadowCoord(i.pws));
                half ndl=dot(n,L.direction);
                half lit=smoothstep(_Step-_Softness,_Step+_Softness,ndl);
                half mid=smoothstep(_MidStep-_Softness,_MidStep+_Softness,ndl);
                half s=max(lit, mid*_MidLevel) * lerp(1, L.shadowAttenuation, 0.8);
                s=lerp(1, s, tex.a);                       // shade mask: eyes/lashes stay flat
                half3 col=lerp(tex.rgb*_ShadowTint.rgb, tex.rgb, s) * L.color;
                col += SampleSH(n)*tex.rgb*0.15;           // a touch of ambient
                float3 v=normalize(GetWorldSpaceViewDir(i.pws));
                half rim=step(_RimPower, 1-saturate(dot(n,v))) * saturate(dot(n,-L.direction)*0.5+0.6);
                col += _RimColor.rgb*rim*_RimStrength*tex.a;
                col=MixFog(col,i.fog); return half4(col,1); }
            ENDHLSL
        }
        Pass
        {
            Name "Outline" Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos:POSITION; float3 n:NORMAL; float4 c:COLOR; };
            struct V { float4 cs:SV_POSITION; };
            V vert(A i){ V o; float3 pws=TransformObjectToWorld(i.pos.xyz); float3 nws=TransformObjectToWorldNormal(i.n);
                float w=_OutlineWidth * (i.c.a>0 ? i.c.r : 1);          // optional per-vertex width in vertex colour R
                o.cs=TransformWorldToHClip(pws+nws*w); return o; }
            half4 frag(V i):SV_Target{ return half4(_OutlineColor.rgb,1); }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
