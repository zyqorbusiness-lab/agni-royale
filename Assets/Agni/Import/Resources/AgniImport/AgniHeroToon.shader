// Agni Royale — Hero Toon v2 (Unity 6, URP 17). Mobile-friendly anime shading for the 10 hair-v3 heroes.
// ToonLit: soft 3-tone ramp (lit / warm mid / cool core shadow), colour-tinted shadows, SH ambient lift, soft view rim on the lit side,
//          an optional soft specular band (hair sheen), shade mask in _BaseMap.a (1 = shaded, low = flat like eyes).
// Outline: inverted hull, HALF the old width, scaled with camera distance (thin up close, stays visible far away),
//          coloured from the albedo (dark version of the cloth/skin colour) instead of flat black.
//          Per-vertex width from vertex colour R when _VColWidth = 1 (LOD0 carries the OutlineMask layer there).
Shader "AgniRoyale/HeroToon"
{
    Properties
    {
        _BaseMap ("Base (RGB) ShadeMask (A)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Mid shadow tint", Color) = (0.80,0.72,0.82,1)
        _CoreTint ("Core shadow tint", Color) = (0.58,0.52,0.70,1)
        _Step ("Shadow step (N.L)", Range(-1,1)) = 0.0
        _CoreStep ("Core step (N.L)", Range(-1,1)) = -0.45
        _Softness ("Ramp softness", Range(0.001,0.5)) = 0.14
        _Ambient ("SH ambient", Range(0,1)) = 0.32
        _ShadowStrength ("Received shadow strength", Range(0,1)) = 0.75
        _NormalFlatten ("Flatten shading normals toward the body axis (cloth: no body-shape emphasis)", Range(0,1)) = 0
        _RimColor ("Rim colour", Color) = (1,0.93,0.85,1)
        _RimWidth ("Rim width (1-N.V)", Range(0,1)) = 0.68
        _RimStrength ("Rim strength", Range(0,1)) = 0.28
        _SpecColor ("Sheen colour", Color) = (1,0.96,0.9,1)
        _SpecStrength ("Sheen strength", Range(0,1)) = 0
        _SpecPower ("Sheen tightness", Range(2,128)) = 24
        _OutlineColor ("Outline colour (mixed with albedo)", Color) = (0.08,0.05,0.08,1)
        _OutlineAlbedo ("Outline from albedo", Range(0,1)) = 0.55
        _OutlineWidth ("Outline width at 3 m (world m)", Range(0,0.01)) = 0.002
        _OutlineMinMax ("Outline distance scale min/max", Vector) = (0.45,2.6,0,0)
        _VColWidth ("Use vertex colour R as width", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _ShadowTint, _CoreTint, _RimColor, _SpecColor, _OutlineColor; float4 _OutlineMinMax;
            half _ShadowStrength, _NormalFlatten, _Step, _CoreStep, _Softness, _Ambient, _RimWidth, _RimStrength, _SpecStrength, _SpecPower, _OutlineAlbedo, _OutlineWidth, _VColWidth, _Cull;
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
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; float3 nws:TEXCOORD1; float3 pws:TEXCOORD2; half fog:TEXCOORD3; float3 nsh:TEXCOORD4; };
            V vert(A i){ V o; VertexPositionInputs p=GetVertexPositionInputs(i.pos.xyz); o.cs=p.positionCS; o.pws=p.positionWS;
                o.nws=TransformObjectToWorldNormal(i.n, false); o.nws*=rsqrt(max(dot(o.nws,o.nws),1e-8)); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z);
                // shading normal: blended toward the horizontal direction from the character's vertical axis -> cloth shades like a smooth cylinder
                float3 org=TransformObjectToWorld(float3(0,0,0)); float2 d=p.positionWS.xz-org.xz; float3 rad=float3(d.x,0,d.y); rad*=rsqrt(max(dot(rad,rad),1e-6));
                // keep the up/down tilt of the surface (shoulders stay lit) but never let it face down past -0.15 (no dark under-bust/fold patches)
                float ny=clamp(o.nws.y,-0.15,1); float3 nf=float3(rad.x*sqrt(1-ny*ny), ny, rad.z*sqrt(1-ny*ny));
                float3 nl=lerp(o.nws, nf, _NormalFlatten); o.nsh=nl*rsqrt(max(dot(nl,nl),1e-8)); return o; }
            half4 frag(V i, bool front:SV_IsFrontFace):SV_Target{
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                float3 n=i.nws*rsqrt(max(dot(i.nws,i.nws),1e-8)); n = front ? n : -n;
                float3 ns=i.nsh*rsqrt(max(dot(i.nsh,i.nsh),1e-8)); ns = front ? ns : -ns;
                Light L=GetMainLight(TransformWorldToShadowCoord(i.pws));
                half ndl=dot(ns,L.direction);
                half sh=lerp(1, L.shadowAttenuation, _ShadowStrength);
                half lit =smoothstep(_Step-_Softness,_Step+_Softness,ndl)*sh;
                half core=smoothstep(_CoreStep-_Softness,_CoreStep+_Softness,ndl);
                half3 shadowCol=lerp(tex.rgb*_CoreTint.rgb, tex.rgb*_ShadowTint.rgb, core);
                half3 col=lerp(shadowCol, tex.rgb, lit);
                col=lerp(tex.rgb, col, tex.a) * lerp(half3(1,1,1), L.color, 0.85);
                half3 amb=SampleSH(ns);
                col += amb*tex.rgb*_Ambient;
                float3 v=normalize(GetWorldSpaceViewDir(i.pws));
                half fres=1-saturate(dot(n,v));
                half rim=smoothstep(_RimWidth-0.08,_RimWidth+0.08,fres) * saturate(ndl*0.6+0.55);
                col += _RimColor.rgb*rim*_RimStrength*tex.a*(0.6+0.4*tex.rgb);
                float3 hdir=normalize(L.direction+v);
                half sp=pow(saturate(dot(ns,hdir)),_SpecPower);
                col += _SpecColor.rgb*smoothstep(0.35,0.65,sp)*_SpecStrength*lit*tex.a;
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
            #pragma multi_compile_fog
            struct A { float4 pos:POSITION; float3 n:NORMAL; float4 c:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 cs:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A i){ V o; float3 pws=TransformObjectToWorld(i.pos.xyz); float3 nws=TransformObjectToWorldNormal(i.n, false); nws*=rsqrt(max(dot(nws,nws),1e-8));
                float d=distance(pws, GetCameraPositionWS());
                float k=clamp(d/3.0, _OutlineMinMax.x, _OutlineMinMax.y);
                float vw=(_VColWidth>0.5 && i.c.a>0) ? i.c.r : 1;
                o.cs=TransformWorldToHClip(pws+nws*_OutlineWidth*k*vw);
                o.cs.z += 0.00002*o.cs.w * (UNITY_NEAR_CLIP_VALUE < 0 ? 1 : -1);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.fog=ComputeFogFactor(o.cs.z); return o; }
            half4 frag(V i):SV_Target{
                half3 a=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,i.uv,2).rgb*_BaseColor.rgb;
                half3 c=lerp(_OutlineColor.rgb, a*a*0.45, _OutlineAlbedo);
                return half4(MixFog(c,i.fog),1); }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
