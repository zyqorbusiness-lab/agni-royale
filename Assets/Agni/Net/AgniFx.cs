// Agni Royale — client-side visual + sound effects for combat. Everything is generated here (procedural textures,
// particle systems, synthesized sounds) so there are no third-party assets. Look target: natural daylight —
// a short hot core, then dusty brown-grey smoke and dirt; no full-screen bloom, no orange wash.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Net
{
    public static class AgniFx
    {
        // ------------------------------------------------------------------ materials / textures
        static Material mAlphaSoft, mSmoke, mAddSoft, mSpark, mDebris, mScorch, mLine, mPaper;
        static Texture2D tSoft, tSmoke, tStreak, tScorch, tNoise, tChunk;
        public static Shader Alpha => Shader.Find("AgniRoyale/FxAlpha");
        public static Shader Add => Shader.Find("AgniRoyale/FxAdd");
        static Material M(Shader s, Texture t) { var m = new Material(s); m.mainTexture = t; return m; }
        static void Init()
        {
            if (mAlphaSoft != null) return;
            tSoft = SoftDot(64, 2.2f); tSmoke = SmokePuff(128, 7); tStreak = Streak(64); tScorch = ScorchTex(128); tNoise = NoiseTex(128); tChunk = SoftDot(32, 8f);
            mAlphaSoft = M(Alpha, tSoft); mSmoke = M(Alpha, tSmoke); mAddSoft = M(Add, tSoft); mSpark = M(Add, tStreak); mDebris = M(Alpha, tChunk); mScorch = M(Alpha, tScorch);
            mLine = M(Alpha, Band(64)); mPaper = M(Alpha, tChunk);
        }
        public static Material LineMaterial() { Init(); return mLine; }
        static readonly Dictionary<int, Material> glow = new Dictionary<int, Material>();
        /// v0.2.1 loot visibility: soft additive glow (disc under the item, beam above rare items), one material per colour
        public static Material Glow(Color c, bool beam)
        {
            Init(); int key = (beam ? 1 : 0) ^ (Mathf.RoundToInt(c.r * 255) << 1) ^ (Mathf.RoundToInt(c.g * 255) << 9) ^ (Mathf.RoundToInt(c.b * 255) << 17);
            if (glow.TryGetValue(key, out var m) && m != null) return m;
            m = M(Add, beam ? Band(64) : tSoft); m.SetColor("_Color", c); glow[key] = m; return m;
        }
        public static Material ZoneMaterial() { Init(); var m = new Material(Shader.Find("AgniRoyale/ZoneCurtain")); m.mainTexture = tNoise; m.SetColor("_Color", new Color(1f, 0.52f, 0.22f, 0.55f)); return m; }

        /// line texture: solid along the line (U), soft across its width (V) — a soft dot here made the arc vanish
        static Texture2D Band(int n)
        {
            var t = new Texture2D(4, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[4 * n];
            for (int y = 0; y < n; y++) { float d = Mathf.Abs((y + 0.5f) / n * 2 - 1); byte a = (byte)(Mathf.Pow(Mathf.Clamp01(1 - d), 0.8f) * 255); for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, a); }
            t.SetPixels32(px); t.Apply(); return t;
        }
        static Texture2D SoftDot(int n, float pow)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Pow(Mathf.Clamp01(1 - d), pow); px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255)); }
            t.SetPixels32(px); t.Apply(); return t;
        }
        static float Fbm(float x, float y, int oct) { float s = 0, a = 0.5f, f = 1; for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 17.3f * i, y * f + 5.1f * i); a *= 0.5f; f *= 2.03f; } return s; }
        static Texture2D SmokePuff(int n, int seed)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float nz = Fbm(x * 0.045f + seed, y * 0.045f, 5);
                    float a = Mathf.Clamp01((1 - d * (0.85f + 0.5f * nz)) * 1.6f); a = a * a * (3 - 2 * a);
                    float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(0.5f - dy * 0.5f + (nz - 0.5f))); // lit from above
                    byte c = (byte)(255 * shade); px[y * n + x] = new Color32(c, c, c, (byte)(a * 235));
                }
            t.SetPixels32(px); t.Apply(); return t;
        }
        static Texture2D Streak(int n)
        {
            var t = new Texture2D(n, n / 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; int h = n / 4; var px = new Color32[n * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < n; x++) { float u = (x + 0.5f) / n, v = Mathf.Abs((y + 0.5f) / h * 2 - 1); float a = Mathf.Clamp01(1 - v) * Mathf.Sin(u * Mathf.PI); px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255)); }
            t.SetPixels32(px); t.Apply(); return t;
        }
        static Texture2D ScorchTex(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy); float nz = Fbm(x * 0.08f, y * 0.08f, 4);
                    float a = Mathf.Clamp01((1 - d * (0.8f + nz * 0.6f)) * 1.5f); px[y * n + x] = new Color32(28, 22, 18, (byte)(a * 200));
                }
            t.SetPixels32(px); t.Apply(); return t;
        }
        static Texture2D NoiseTex(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat }; var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {   // tileable: blend 4 shifted samples
                    float u = (float)x / n, v = (float)y / n; float s = 6f;
                    float a = Fbm(u * s, v * s, 4), b = Fbm((u - 1) * s, v * s, 4), c = Fbm(u * s, (v - 1) * s, 4), d = Fbm((u - 1) * s, (v - 1) * s, 4);
                    float f = Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v); byte g = (byte)(Mathf.Clamp01((f - 0.2f) * 1.8f) * 255);
                    px[y * n + x] = new Color32(g, g, g, 255);
                }
            t.SetPixels32(px); t.Apply(); return t;
        }

        public static Mesh CylinderMesh(int seg, float r, float h)
        {
            var m = new Mesh(); var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); v.Add(p); v.Add(p + Vector3.up * h); uv.Add(new Vector2(i / (float)seg, 0)); uv.Add(new Vector2(i / (float)seg, 1)); }
            for (int i = 0; i < seg; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.bounds = new Bounds(Vector3.up * h * 0.5f, new Vector3(r * 2, h, r * 2) * 1.01f); return m;
        }
        public static float GroundY(Vector3 p) => Physics.Raycast(new Vector3(p.x, 400, p.z), Vector3.down, out var h, 900, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;

        // ------------------------------------------------------------------ particle helper
        static ParticleSystem PS(string name, Vector3 pos, Material mat, float duration, Action<ParticleSystem, ParticleSystemRenderer> cfg)
        {
            var go = new GameObject(name); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = Mathf.Max(0.05f, duration); main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            cfg(ps, r); ps.Play(); UnityEngine.Object.Destroy(go, duration + 10f); return ps;
        }
        static void Burst(ParticleSystem ps, int n, float t = 0) { var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(t, (short)n) }); }
        static ParticleSystem.MinMaxGradient Grad(params (float t, Color c)[] keys)
        {
            var g = new Gradient(); var ck = new GradientColorKey[keys.Length]; var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) { ck[i] = new GradientColorKey(keys[i].c, keys[i].t); ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t); }
            g.SetKeys(ck, ak); return new ParticleSystem.MinMaxGradient(g);
        }
        static ParticleSystem.MinMaxCurve Curve(params float[] kv) { var c = new AnimationCurve(); for (int i = 0; i < kv.Length; i += 2) c.AddKey(kv[i], kv[i + 1]); return new ParticleSystem.MinMaxCurve(1f, c); }
        static bool Near(Vector3 p, float d) { var c = Camera.main; return c != null && (c.transform.position - p).sqrMagnitude < d * d; }

        // ------------------------------------------------------------------ GOLA explosion
        public static void Explosion(Vector3 at)
        {
            Init(); var up = Vector3.up; float gy = GroundY(at); bool onGround = at.y - gy < 1.2f;
            // 1) hot core: very short, small, warm-white -> orange (additive, low alpha so it does not wash the screen)
            PS("Gola_Core", at + up * 0.4f, mAddSoft, 0.3f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f); m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3f);
                m.startColor = new Color(1f, 0.86f, 0.62f, 0.7f); Burst(ps, 9);
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1f, 0.92f, 0.75f, 0.9f)), (0.4f, new Color(1f, 0.55f, 0.2f, 0.55f)), (1, new Color(0.6f, 0.2f, 0.05f, 0f)));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.6f, 1, 1.3f);
            });
            // 2) brief fireball puffs tumbling up (alpha, darkening quickly to soot)
            PS("Gola_Fire", at + up * 0.5f, mSmoke, 0.8f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); Burst(ps, 14); var sh = ps.shape; sh.radius = 0.6f;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1f, 0.74f, 0.38f, 0.95f)), (0.25f, new Color(0.86f, 0.38f, 0.12f, 0.85f)), (0.6f, new Color(0.25f, 0.17f, 0.12f, 0.7f)), (1, new Color(0.2f, 0.17f, 0.15f, 0f)));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.6f, 1, 1.6f);
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            });
            // 3) dust + smoke column: brown-grey, rises and spreads, lingers ~4 s
            PS("Gola_Smoke", at + up * 0.6f, mSmoke, 6f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.8f); m.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 3.2f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.36f, 0.31f, 0.27f, 0.85f), new Color(0.5f, 0.45f, 0.4f, 0.75f));
                Burst(ps, 22); var sh = ps.shape; sh.radius = 1.0f; sh.shapeType = ParticleSystemShapeType.Hemisphere;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 1, 1, 0)), (0.08f, new Color(1, 1, 1, 0.9f)), (0.6f, new Color(1, 1, 1, 0.55f)), (1, new Color(1, 1, 1, 0f)));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.7f, 1, 2.6f);
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.2f; lim.dampen = 0.08f;
                var nz = ps.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 0.4f;
            });
            // 4) dirt + grit thrown out, falling back (only if near the ground)
            if (onGround) PS("Gola_Dirt", new Vector3(at.x, gy + 0.15f, at.z), mDebris, 2.2f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.7f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 13f);
                m.gravityModifier = 1.6f; m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.19f, 0.13f, 1f), new Color(0.42f, 0.33f, 0.23f, 1f)); Burst(ps, 46);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38; sh.radius = 0.3f; sh.rotation = new Vector3(-90, 0, 0);
            });
            // 5) a few hot sparks (stretched)
            PS("Gola_Sparks", at + up * 0.3f, mSpark, 1.2f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); m.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
                m.gravityModifier = 1.2f; m.startColor = new Color(1f, 0.72f, 0.38f, 1f); Burst(ps, 26);
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1f; r.maxParticleSize = 0.03f; // capped: a spark passing the lens was a screen-wide beam
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 0.9f, 0.7f, 1)), (1, new Color(1, 0.4f, 0.1f, 0)));
            });
            // 6) ground dust ring rolling outwards
            if (onGround) PS("Gola_Ring", new Vector3(at.x, gy + 0.2f, at.z), mSmoke, 2.5f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f); m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
                m.startColor = new Color(0.62f, 0.55f, 0.45f, 0.55f); Burst(ps, 20);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 1, 1, 0.8f)), (1, new Color(1, 1, 1, 0)));
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.5f; lim.dampen = 0.15f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.6f, 1, 2f);
            });
            // 7) scorch mark
            if (onGround && Physics.Raycast(at + up, Vector3.down, out var gh, 3f, ~0, QueryTriggerInteraction.Ignore)) Decal(gh.point, gh.normal, 3.2f, mScorch, 25f);
            // 8) a short warm light kick (small range, no post bloom)
            LightKick(at + up * 0.8f, new Color(1f, 0.72f, 0.45f), 5f, 11f, 0.18f);
            AgniCamRig.ShakeAt(at, 0.55f, 26f);
            AgniSynth.PlayAt(AgniSynth.Boom, at, 1f, 260f);
        }

        // ------------------------------------------------------------------ PATAKHA flash firecracker
        public static void FlashPop(Vector3 at)
        {
            Init();
            PS("Patakha_Pop", at + Vector3.up * 0.3f, mAddSoft, 0.3f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = 0.12f; m.startSize = 4.5f; m.startSpeed = 0; m.startColor = new Color(1f, 1f, 0.95f, 0.95f); Burst(ps, 1);
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, Color.white), (1, new Color(1, 1, 1, 0)));
            });
            // red paper shreds fluttering down (our firecracker identity)
            PS("Patakha_Paper", at + Vector3.up * 0.4f, mPaper, 4f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                m.gravityModifier = 0.25f; m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.12f, 0.1f), new Color(0.95f, 0.75f, 0.3f)); Burst(ps, 40);
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.0f; lim.dampen = 0.2f; var nz = ps.noise; nz.enabled = true; nz.strength = 1.2f; nz.frequency = 1.2f;
            });
            PS("Patakha_Sparks", at + Vector3.up * 0.2f, mSpark, 1f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
                m.startColor = new Color(1f, 0.95f, 0.8f); Burst(ps, 30); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.maxParticleSize = 0.03f;
            });
            PS("Patakha_Smoke", at + Vector3.up * 0.3f, mSmoke, 3f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                m.startColor = new Color(0.82f, 0.82f, 0.8f, 0.45f); Burst(ps, 8); var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.6f, 1, 1.8f);
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 1, 1, 0.8f)), (1, new Color(1, 1, 1, 0)));
            });
            LightKick(at + Vector3.up * 0.5f, Color.white, 9f, 16f, 0.12f);
            AgniCamRig.ShakeAt(at, 0.2f, 18f);
            AgniSynth.PlayAt(AgniSynth.Crack, at, 1f, 200f);
        }

        // ------------------------------------------------------------------ DHUAN smoke screen
        public static void SmokeCloud(Vector3 at, float seconds)
        {
            Init();
            PS("Dhuan", at + Vector3.up * 0.2f, mSmoke, seconds, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7.5f); m.startSize = new ParticleSystem.MinMaxCurve(2.8f, 4.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); m.maxParticles = 160;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.80f, 0.82f, 0.82f, 0.92f), new Color(0.70f, 0.73f, 0.74f, 0.92f));
                var em = ps.emission; var rc = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.08f, 1), new Keyframe(0.15f, 0.4f), new Keyframe(0.8f, 0.4f), new Keyframe(1, 0)); em.rateOverTime = new ParticleSystem.MinMaxCurve(22f, rc);
                Burst(ps, 18);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 1.2f;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 1, 1, 0)), (0.1f, new Color(1, 1, 1, 1)), (0.75f, new Color(1, 1, 1, 0.85f)), (1, new Color(1, 1, 1, 0)));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.6f, 0.4f, 1.3f, 1, 1.7f);
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.35f; lim.dampen = 0.12f;
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.25f;
            });
            AgniSynth.PlayAt(AgniSynth.Hiss, at, 0.8f, 60f);
        }

        // ------------------------------------------------------------------ gun shots
        public static void Shot(Vector3 from, Vector3 to, HitKind hit, bool mine)
        {
            Init(); if (!mine && !Near(from, 160f) && !Near(to, 120f)) return;
            // tracer: thin, warm, 60 ms
            var go = new GameObject("Tracer"); var lr = go.AddComponent<LineRenderer>(); lr.material = mAddSoft; lr.positionCount = 2;
            var dir = (to - from).normalized; var a = Vector3.Lerp(from, to, UnityEngine.Random.Range(0f, 0.25f)); lr.SetPosition(0, a); lr.SetPosition(1, Vector3.MoveTowards(a, to, Mathf.Min(16f, Vector3.Distance(a, to))));
            lr.widthMultiplier = 0.035f; lr.startColor = new Color(1f, 0.9f, 0.65f, 0.0f); lr.endColor = new Color(1f, 0.92f, 0.7f, 0.85f); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UnityEngine.Object.Destroy(go, 0.06f);
            // muzzle
            PS("Muzzle", from, mAddSoft, 0.1f, (ps, r) => { var m = ps.main; m.startLifetime = 0.04f; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSpeed = 0; m.startColor = new Color(1f, 0.8f, 0.5f, 0.85f); Burst(ps, 2); });
            if (hit == HitKind.World || hit == HitKind.Wall)
            {
                PS("Impact", to - dir * 0.05f, mSmoke, 1.2f, (ps, r) =>
                {
                    var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f); m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
                    m.startColor = hit == HitKind.Wall ? new Color(0.72f, 0.42f, 0.28f, 0.8f) : new Color(0.6f, 0.55f, 0.47f, 0.7f); Burst(ps, 5);
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25; sh.radius = 0.02f; ps.transform.rotation = Quaternion.LookRotation(-dir);
                    var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, Color.white), (1, new Color(1, 1, 1, 0)));
                });
            }
            else if (hit == HitKind.Body || hit == HitKind.Head)
            {
                PS("HitPuff", to, mAlphaSoft, 0.5f, (ps, r) => { var m = ps.main; m.startLifetime = 0.2f; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f); m.startColor = new Color(0.55f, 0.08f, 0.06f, 0.7f); Burst(ps, 4); });
            }
            AgniSynth.PlayAt(AgniSynth.Shot, from, mine ? 0.55f : 0.8f, 280f);
        }

        public static void Clink(Vector3 p, float s) { AgniSynth.PlayAt(AgniSynth.Clink, p, 0.3f + 0.5f * s, 30f); }
        public static void PinPull(Vector3 p) { AgniSynth.PlayAt(AgniSynth.Pin, p, 0.6f, 12f); }

        // ------------------------------------------------------------------ AGNI DEEWAR
        public static void WallDust(Vector3 at, Quaternion rot)
        {
            Init();
            PS("Deewar_Dust", at + Vector3.up * 0.2f, mSmoke, 2.5f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3f);
                m.startColor = new Color(0.68f, 0.52f, 0.40f, 0.6f); Burst(ps, 18);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(AgniWall.Width, 0.2f, 0.6f); ps.transform.rotation = rot;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad((0, new Color(1, 1, 1, 0.8f)), (1, new Color(1, 1, 1, 0)));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Curve(0, 0.7f, 1, 1.8f);
            });
            AgniSynth.PlayAt(AgniSynth.Thud, at, 0.9f, 60f);
        }
        public static void WallBreak(Vector3 at, Quaternion rot, bool shattered)
        {
            Init();
            PS("Deewar_Chunks", at + Vector3.up * 1.1f, mDebris, 2.5f, (ps, r) =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f); m.startSpeed = new ParticleSystem.MinMaxCurve(shattered ? 3f : 0.5f, shattered ? 7f : 2f);
                m.gravityModifier = 1.5f; m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.30f, 0.17f), new Color(0.80f, 0.45f, 0.27f)); Burst(ps, shattered ? 40 : 22);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(AgniWall.Width, AgniWall.Height, 0.4f); ps.transform.rotation = rot;
            });
            WallDust(at, rot);
        }

        // ------------------------------------------------------------------ small helpers
        static void Decal(Vector3 p, Vector3 n, float size, Material m, float life)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(q.GetComponent<Collider>()); q.name = "Scorch";
            q.transform.position = p + n * 0.03f; q.transform.rotation = Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360f)); q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.AddComponent<FadeOut>().Begin(life);
        }
        static void LightKick(Vector3 p, Color c, float intensity, float range, float life)
        {
            var go = new GameObject("FxLight"); go.transform.position = p; var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            go.AddComponent<FadeOut>().BeginLight(l, life);
        }
        class FadeOut : MonoBehaviour
        {
            float t0, life; Light l; float i0; Renderer r; MaterialPropertyBlock mpb;
            public void Begin(float s) { life = s; t0 = Time.time; r = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); }
            public void BeginLight(Light light, float s) { l = light; i0 = l.intensity; life = s; t0 = Time.time; }
            void Update()
            {
                float k = (Time.time - t0) / life; if (k >= 1) { Destroy(gameObject); return; }
                if (l != null) l.intensity = i0 * (1 - k) * (1 - k);
                if (r != null && k > 0.7f) { mpb.SetColor("_Color", new Color(1, 1, 1, 1 - (k - 0.7f) / 0.3f)); r.SetPropertyBlock(mpb); }
            }
        }

        // ------------------------------------------------------------------ held / flying throwable models (own design, built from primitives)
        public static GameObject BuildThrowableView(ThrowKind k, Transform parent)
        {
            var root = new GameObject("View_" + k); root.transform.SetParent(parent, false);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Mat(Color c, float sm = 0.3f, float met = 0f, Color? em = null) { var m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm); m.SetFloat("_Metallic", met); if (em.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", em.Value); } return m; }
            GameObject P(PrimitiveType t, Vector3 pos, Vector3 scl, Material m, Vector3? eul = null)
            {
                var g = GameObject.CreatePrimitive(t); UnityEngine.Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(root.transform, false);
                g.transform.localPosition = pos; g.transform.localScale = scl; g.transform.localEulerAngles = eul ?? Vector3.zero; g.GetComponent<Renderer>().sharedMaterial = m;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
            }
            switch (k)
            {
                case ThrowKind.Gola: // olive egg body, segmented band, brass fuse cap + lever
                    P(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.10f, 0.12f, 0.10f), Mat(new Color(0.30f, 0.34f, 0.20f), 0.35f));
                    P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.104f, 0.012f, 0.104f), Mat(new Color(0.22f, 0.25f, 0.15f), 0.3f));
                    P(PrimitiveType.Cylinder, new Vector3(0, 0.066f, 0), new Vector3(0.035f, 0.016f, 0.035f), Mat(new Color(0.72f, 0.58f, 0.30f), 0.6f, 0.8f));
                    P(PrimitiveType.Cube, new Vector3(0.022f, 0.035f, 0), new Vector3(0.012f, 0.07f, 0.02f), Mat(new Color(0.65f, 0.62f, 0.55f), 0.5f, 0.6f), new Vector3(0, 0, -12));
                    break;
                case ThrowKind.Dhuan: // grey-teal canister, white band, vent holes
                    P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.075f, 0.07f), Mat(new Color(0.42f, 0.50f, 0.52f), 0.35f, 0.3f));
                    P(PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(0.072f, 0.008f, 0.072f), Mat(new Color(0.9f, 0.9f, 0.88f), 0.3f));
                    P(PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0), new Vector3(0.03f, 0.01f, 0.03f), Mat(new Color(0.25f, 0.26f, 0.26f), 0.5f, 0.6f));
                    break;
                case ThrowKind.Patakha: // red paper tube with gold bands + a short fuse that sparkles
                    P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.055f, 0.08f, 0.055f), Mat(new Color(0.78f, 0.12f, 0.10f), 0.25f));
                    P(PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.057f, 0.008f, 0.057f), Mat(new Color(0.92f, 0.72f, 0.28f), 0.5f, 0.7f));
                    P(PrimitiveType.Cylinder, new Vector3(0, -0.05f, 0), new Vector3(0.057f, 0.008f, 0.057f), Mat(new Color(0.92f, 0.72f, 0.28f), 0.5f, 0.7f));
                    P(PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(0.008f, 0.025f, 0.008f), Mat(new Color(0.3f, 0.25f, 0.2f), 0.2f));
                    var spark = P(PrimitiveType.Sphere, new Vector3(0, 0.125f, 0), Vector3.one * 0.02f, Mat(new Color(1f, 0.8f, 0.4f), 0, 0, new Color(3f, 1.8f, 0.6f)));
                    break;
                case ThrowKind.Deewar: // fired-clay orb with ember seams
                    P(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.13f, AgniWallView.ClayMaterial());
                    break;
            }
            return root;
        }
        class LootSpin : MonoBehaviour { void Update() { transform.Rotate(0, 50 * Time.deltaTime, 0, Space.World); } }
    }

    /// Camera shake bus (the local camera rig listens).
    public static class AgniCamRig
    {
        public static float Trauma;
        public static void ShakeAt(Vector3 at, float strength, float range)
        {
            var c = Camera.main; if (c == null) return; float d = Vector3.Distance(c.transform.position, at); if (d > range) return;
            Trauma = Mathf.Min(1f, Trauma + strength * Mathf.Pow(1 - d / range, 1.5f));
        }
    }

    /// Our own synthesized sounds (no samples): boom, shot, firecracker crack, hiss, clink, pin, thud, ear ring.
    public static class AgniSynth
    {
        public static AudioClip Boom => boom ??= Make("boom", 1.6f, (t, r) => Noise(r) * Env(t, 0.004f, 1.1f) * (0.6f + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * 45f)), 0.5f);
        public static AudioClip Shot => shot ??= Make("shot", 0.35f, (t, r) => Noise(r) * Env(t, 0.001f, 0.09f) + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * 140f) * Env(t, 0.001f, 0.05f), 0.25f);
        public static AudioClip Crack => crack ??= Make("crack", 1.2f, (t, r) => { float s = 0; for (int i = 0; i < 6; i++) s += Noise(r) * Env(t - i * 0.07f - (i * i % 3) * 0.013f, 0.0005f, 0.025f); return s * 0.7f + Noise(r) * Env(t, 0.001f, 0.3f) * 0.6f; }, 0.9f);
        public static AudioClip Hiss => hiss ??= Make("hiss", 3.5f, (t, r) => Noise(r) * Env(t, 0.05f, 2.8f) * 0.35f, 0.02f);
        public static AudioClip Clink => clink ??= Make("clink", 0.25f, (t, r) => (Mathf.Sin(t * 2 * Mathf.PI * 1850f) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 2730f) * 0.3f) * Env(t, 0.0005f, 0.07f), 0f);
        public static AudioClip Pin => pin ??= Make("pin", 0.3f, (t, r) => Mathf.Sin(t * 2 * Mathf.PI * 3100f) * Env(t, 0.0005f, 0.03f) * 0.6f + Noise(r) * Env(t - 0.12f, 0.001f, 0.03f) * 0.4f, 0f);
        public static AudioClip Thud => thud ??= Make("thud", 0.8f, (t, r) => (Mathf.Sin(t * 2 * Mathf.PI * 70f) * 0.8f + Noise(r) * 0.4f) * Env(t, 0.003f, 0.35f), 0.3f);
        public static AudioClip Ring => ring ??= Make("ring", 3.5f, (t, r) => Mathf.Sin(t * 2 * Mathf.PI * 3600f) * 0.18f * Env(t, 0.01f, 3.0f), 0f);
        static AudioClip boom, shot, crack, hiss, clink, pin, thud, ring;
        static float Noise(System.Random r) => (float)(r.NextDouble() * 2 - 1);
        static float Env(float t, float a, float d) => t < 0 ? 0 : t < a ? t / a : Mathf.Exp(-(t - a) / (d * 0.35f));
        /// lp = one-pole low-pass amount (0 = bright, 0.9 = very muffled)
        static AudioClip Make(string name, float secs, Func<float, System.Random, float> f, float lp)
        {
            int sr = 22050, n = (int)(secs * sr); var d = new float[n]; var rnd = new System.Random(name.GetHashCode()); float y = 0, peak = 0.0001f;
            for (int i = 0; i < n; i++) { float x = f(i / (float)sr, rnd); y = lp * y + (1 - lp) * x; d[i] = y; peak = Mathf.Max(peak, Mathf.Abs(y)); }
            for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.9f;
            var c = AudioClip.Create("agni_" + name, n, 1, sr, false); c.SetData(d, 0); return c;
        }
        public static void PlayAt(AudioClip c, Vector3 p, float vol, float maxDist)
        {
            if (c == null || Application.isBatchMode) return;
            var go = new GameObject("Sfx_" + c.name); go.transform.position = p; var s = go.AddComponent<AudioSource>(); s.clip = c; s.spatialBlend = 1; s.rolloffMode = AudioRolloffMode.Linear; s.maxDistance = maxDist; s.minDistance = 2;
            s.volume = vol * Agni.UI.AgniSettings.Sfx; s.dopplerLevel = 0; s.Play(); UnityEngine.Object.Destroy(go, c.length + 0.1f);
        }
        public static void Play2D(AudioClip c, float vol) { if (c == null || Application.isBatchMode) return; var go = new GameObject("Sfx2D"); var s = go.AddComponent<AudioSource>(); s.clip = c; s.volume = vol * Agni.UI.AgniSettings.Sfx; s.Play(); UnityEngine.Object.Destroy(go, c.length + 0.1f); }
    }
}
