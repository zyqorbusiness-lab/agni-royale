// AgniBrowOverHair.shader — anime brows drawn over the bangs (Unity URP). Our own code.
// Put it on the M_<Name>_Brow material (5th material slot of LOD0). Queue after the hair, ZTest Always,
// faded by N·V so the brows never show through the head from the side/back.
Shader "AgniRoyale/BrowOverHair"
{
    Properties { _BaseMap ("Skin atlas", 2D) = "white" {} _Color ("Brow colour", Color) = (0.29,0.24,0.39,1) _Fade ("N.V fade start", Range(0,1)) = 0.25 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+60" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Back Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float ndv:TEXCOORD1; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap); float4 _Color; float _Fade;
            V vert(A a){ V o; VertexPositionInputs p=GetVertexPositionInputs(a.pos.xyz); o.pos=p.positionCS; o.uv=a.uv;
                float3 n=TransformObjectToWorldNormal(a.n); o.ndv=saturate(dot(normalize(n), normalize(GetWorldSpaceViewDir(p.positionWS)))); return o; }
            half4 frag(V i):SV_Target { half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_Color*4.0; c.a=smoothstep(_Fade,_Fade+0.25,i.ndv); return half4(_Color.rgb, c.a); }
            ENDHLSL
        }
    }
}
