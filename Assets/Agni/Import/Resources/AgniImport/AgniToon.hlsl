// Agni Royale v0.2.2 — the anime WORLD look, shared by WorldToon (houses, trees, props, vehicles), Landmark and TerrainToon.
// One soft 2-tone ramp (lit / cool lavender shade, colour-tinted, never black), received shadows folded into the ramp, SH sky lift,
// a gentle saturation push and a "painterly" albedo (texture detail softened by sampling a blurrier mip). No emission, no glow, no bloom.
#ifndef AGNI_TOON_INCLUDED
#define AGNI_TOON_INCLUDED
#define AGNI_SHADE_TINT half3(0.60, 0.64, 0.88)
// soft terminator: N.L from -0.08 (shade) to 0.30 (lit)
half AgniRamp(half ndl) { return smoothstep(-0.08h, 0.30h, ndl); }
half3 AgniGrade(half3 c, half sat) { half l = dot(c, half3(0.299h, 0.587h, 0.114h)); return max(0, l + (c - l) * sat); }
/// alb: albedo, n: shading normal (world), sh: SampleSH(n)
half3 AgniToonShade(half3 alb, float3 n, float3 posWS, half3 sh, half shadowStrength)
{
    Light L = GetMainLight(TransformWorldToShadowCoord(posWS), posWS, half4(1, 1, 1, 1)); // this overload fades shadows out at the shadow distance
    half ndl = dot((half3)n, (half3)L.direction);
    half lit = AgniRamp(ndl) * lerp(1.0h, (half)L.shadowAttenuation, shadowStrength);
    half3 lightC = (half3)L.color;
    half3 litCol = lightC * 0.95h + sh * 0.42h;
    half3 shadeCol = AGNI_SHADE_TINT * (0.30h + 0.25h * dot(lightC, half3(0.33h, 0.33h, 0.33h))) + sh * 0.58h;
    return alb * lerp(shadeCol, litCol, lit);
}
#endif
