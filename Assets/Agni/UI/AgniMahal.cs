// Agni Royale — MAHAL lobby backdrop (our own palace design; no temples / religious symbols).
// Built entirely from code at runtime: geometry is generated into a handful of merged meshes (one per material per depth zone),
// so the whole palace is ~20 draw calls. Layers (camera looks +Z):
//   foreground  : marble courtyard, podium lamps, diyas along a long water channel with fountains
//   midground   : two-storey arcaded palace wings (arches, jharokhas, chhajjas, chhatris) running to the horizon
//   gates       : a chain of darwaza gateways across the axis (z 46 → 420) = "endless palace" read, fading into haze
//   far         : domed pavilions + tall towers behind the wings, a giant far palace, layered hills, sky gradient, sun
// Animation: camera drift, waving flags, fountains, pigeons, drifting clouds, flickering diyas, light shafts, falling petals,
//            slow golden-hour <-> sunset cycle. Quality: GfxPreset (LOW/NORMAL/HIGH/VERY HIGH) scales everything.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Agni.UI
{
    public class AgniMahal : MonoBehaviour
    {
        public static int Quality => (int)AgniSettings.Gfx; // 0 LOW, 1 NORMAL, 2 HIGH, 3 VERY HIGH
        public Light sun;
        public Camera cam;
        public static string Stats = "";

        // ------------------------------------------------------------ mesh builder
        class MB
        {
            public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> t = new List<int>();
            public Matrix4x4 M = Matrix4x4.identity; public float uvs = 0.25f; // texture repeats per metre
            int V(Vector3 p, Vector3 nn, Vector2 u) { v.Add(M.MultiplyPoint3x4(p)); n.Add(M.MultiplyVector(nn).normalized); uv.Add(u); return v.Count - 1; }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                var nn = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(nn, hint) < 0) { var tmp = b; b = c; c = tmp; var tu = ub; ub = uc; uc = tu; nn = -nn; }
                nn = nn.normalized; int i = V(a, nn, ua); V(b, nn, ub); V(c, nn, uc); t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint, bool planarXY = true)
            {
                Vector2 U(Vector3 p) { var w = M.MultiplyPoint3x4(p); return Mathf.Abs(hint.y) > 0.7f ? new Vector2(w.x, w.z) * uvs : Mathf.Abs(hint.x) > Mathf.Abs(hint.z) ? new Vector2(w.z, w.y) * uvs : new Vector2(w.x, w.y) * uvs; }
                Tri(a, b, c, hint, U(a), U(b), U(c)); Tri(a, c, d, hint, U(a), U(c), U(d));
            }
            public void Box(Vector3 c, Vector3 s)
            {
                Vector3 h = s * 0.5f;
                Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Vector3.back);
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), Vector3.forward);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), Vector3.left);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Vector3.right);
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up);
                Quad(P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), Vector3.down);
            }
            /// Smooth lathe around local Y. prof = (radius, y) bottom→top.
            public void Lathe(Vector3 c, Vector2[] prof, int seg)
            {
                int b0 = v.Count; int rings = prof.Length;
                for (int i = 0; i < rings; i++)
                {
                    Vector2 d = (i < rings - 1 ? prof[i + 1] - prof[i] : prof[i] - prof[i - 1]);
                    if (i > 0 && i < rings - 1) d = prof[i + 1] - prof[i - 1];
                    Vector2 pn = new Vector2(d.y, -d.x).normalized; if (pn.x < 0) pn = -pn;
                    for (int j = 0; j <= seg; j++)
                    {
                        float a = j * Mathf.PI * 2 / seg; float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        var p = c + new Vector3(prof[i].x * ca, prof[i].y, prof[i].x * sa);
                        V(p, new Vector3(pn.x * ca, pn.y, pn.x * sa), new Vector2((float)j / seg * 2f, prof[i].y * uvs));
                    }
                }
                for (int i = 0; i < rings - 1; i++)
                    for (int j = 0; j < seg; j++)
                    {
                        int a = b0 + i * (seg + 1) + j, b = a + seg + 1, cc = b + 1, dd = a + 1;
                        // outward winding: (a, b, cc) — verified by cross product sign at build time
                        t.Add(a); t.Add(b); t.Add(cc); t.Add(a); t.Add(cc); t.Add(dd);
                    }
                // fix winding once: check first non-degenerate triangle normal against its vertex normal
                int ti = t.Count - seg * (rings - 1) * 6; if (ti < 0) return;
                for (int k = ti; k < t.Count; k += 3)
                {
                    var A = v[t[k]]; var B = v[t[k + 1]]; var C = v[t[k + 2]]; var cr = Vector3.Cross(B - A, C - A);
                    if (cr.sqrMagnitude < 1e-10f) continue;
                    if (Vector3.Dot(cr, n[t[k]] + n[t[k + 1]] + n[t[k + 2]]) < 0)
                        for (int q = ti; q < t.Count; q += 3) { int tmp = t[q + 1]; t[q + 1] = t[q + 2]; t[q + 2] = tmp; }
                    break;
                }
            }
            /// Wall panel (local XY, thickness along Z, centred on x=0, bottom at y=0) with a pointed-arch opening.
            public void ArchWall(float w, float h, float ow, float spring, float depth, int seg = 8, float pointR = 0.62f)
            {
                var C = new List<Vector2>(); var O = new List<Vector2>();
                float hw = w * 0.5f, ho = ow * 0.5f; float R = Mathf.Max(ho * 1.02f, ow * pointR);
                float cxL = -ho + R; float aEnd = Mathf.Acos(Mathf.Clamp(-cxL / R, -1, 1)); // angle where left arc meets x=0
                float apex = spring + R * Mathf.Sin(aEnd); if (apex > h - 0.25f) { float k = (h - 0.25f - spring) / (apex - spring); apex = spring + (apex - spring) * k; }
                // left side up
                C.Add(new Vector2(-ho, 0)); O.Add(new Vector2(-hw, 0));
                C.Add(new Vector2(-ho, spring)); O.Add(new Vector2(-hw, spring));
                float sc = (apex - spring) / Mathf.Max(0.001f, R * Mathf.Sin(aEnd));
                float outerLen = (h - spring) + hw;
                for (int i = 1; i <= seg; i++)
                {
                    float f = (float)i / seg; float a = Mathf.PI - (Mathf.PI - aEnd) * f;
                    C.Add(new Vector2(cxL + R * Mathf.Cos(a), spring + R * Mathf.Sin(a) * sc));
                    float L = f * outerLen; O.Add(L <= h - spring ? new Vector2(-hw, spring + L) : new Vector2(-hw + (L - (h - spring)), h));
                }
                int nl = C.Count;
                for (int i = nl - 2; i >= 0; i--) { C.Add(new Vector2(-C[i].x, C[i].y)); O.Add(new Vector2(-O[i].x, O[i].y)); }
                float dz = depth * 0.5f;
                for (int i = 0; i < C.Count - 1; i++)
                {
                    Vector3 c0 = C[i], c1 = C[i + 1], o0 = O[i], o1 = O[i + 1];
                    foreach (float z in new[] { -dz, dz })
                    {
                        var hint = z < 0 ? Vector3.back : Vector3.forward;
                        Quad(new Vector3(c0.x, c0.y, z), new Vector3(o0.x, o0.y, z), new Vector3(o1.x, o1.y, z), new Vector3(c1.x, c1.y, z), hint);
                    }
                    // reveal (inside of the opening), normal points to the opening centre
                    Vector2 mid = (C[i] + C[i + 1]) * 0.5f; Vector3 rh = new Vector3(-mid.x, (apex * 0.45f) - mid.y, 0);
                    Quad(new Vector3(c0.x, c0.y, -dz), new Vector3(c1.x, c1.y, -dz), new Vector3(c1.x, c1.y, dz), new Vector3(c0.x, c0.y, dz), rh.normalized);
                }
                // top cap + side caps
                Quad(new Vector3(-hw, h, -dz), new Vector3(hw, h, -dz), new Vector3(hw, h, dz), new Vector3(-hw, h, dz), Vector3.up);
                Quad(new Vector3(-hw, 0, -dz), new Vector3(-hw, h, -dz), new Vector3(-hw, h, dz), new Vector3(-hw, 0, dz), Vector3.left);
                Quad(new Vector3(hw, 0, -dz), new Vector3(hw, h, -dz), new Vector3(hw, h, dz), new Vector3(hw, 0, dz), Vector3.right);
            }
        }

        // material slots
        const int SAND = 0, RED = 1, MARBLE = 2, GOLD = 3, HALL = 4, DOME = 5, FLOOR = 6, WATER = 7, DARK = 8, CARPET = 9, LEAF = 10, BANNER = 11, NMAT = 12;
        Material[] mats = new Material[NMAT];
        readonly Dictionary<int, MB> builders = new Dictionary<int, MB>(); // key = zone*16 + mat
        MB B(int mat, float z) { int zone = z < 30 ? 0 : z < 90 ? 1 : z < 200 ? 2 : 3; int key = zone * 16 + mat; if (!builders.TryGetValue(key, out var mb)) { mb = new MB(); builders[key] = mb; } return mb; }

        static readonly Vector2[] DomeProf = {
            new Vector2(0.92f,0), new Vector2(1.0f,0.12f), new Vector2(1.07f,0.34f), new Vector2(1.03f,0.56f), new Vector2(0.88f,0.78f),
            new Vector2(0.62f,0.98f), new Vector2(0.36f,1.16f), new Vector2(0.16f,1.32f), new Vector2(0.05f,1.46f), new Vector2(0.0f,1.52f) };

        Texture2D skyTex; Material skyMat; Color[] skyCols = new Color[64];
        readonly List<Transform> birds = new List<Transform>(); readonly List<Vector4> birdP = new List<Vector4>();
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>(); readonly List<float> flameA = new List<float>();
        readonly List<Transform> clouds = new List<Transform>(); readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
        readonly List<Flag> flags = new List<Flag>();
        Vector3 camBase; Quaternion camRot;
        int q;

        public void Build(Camera c, Light key)
        {
            cam = c; sun = key; q = Quality;
            camBase = cam.transform.position; camRot = cam.transform.rotation;
            MakeMaterials();
            Courtyard(); Wings(); Gates(); FarPalace(); Pavilions();
            Flush();
            Sky(); Hills(); Clouds(); Birds(); Diyas(); if (q >= 1) Fountains(); if (q >= 2) Petals(); if (q >= 3) Beams();
            Atmosphere();
        }

        Material Lit(Color col, string tex = null, float smooth = 0.25f, float metal = 0f, Color? emit = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", col); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (tex != null) { var t = Resources.Load<Texture2D>("AgniMahal/" + tex); if (t != null) { m.SetTexture("_BaseMap", t); t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 2; } }
            if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            m.enableInstancing = true; return m;
        }
        void MakeMaterials()
        {
            mats[SAND] = Lit(Pal.Hex("#F0CFB0"), "sandstone", 0.12f);
            mats[RED] = Lit(Pal.Hex("#FFD2C0"), "redstone", 0.15f);
            mats[MARBLE] = Lit(Pal.Hex("#F4EEE6"), "marble", 0.4f);
            mats[GOLD] = Lit(Pal.Hex("#C99A45"), null, 0.6f, 0.8f, Pal.Hex("#1E1004"));
            mats[HALL] = Lit(Pal.Hex("#3A2016"), null, 0.1f, 0f, Pal.Hex("#FF9A48") * 0.28f);
            mats[DOME] = Lit(Pal.Hex("#FFF6EA"), "marble", 0.55f);
            mats[FLOOR] = Lit(Pal.Hex("#DCCAB4"), "marble_floor", 0.3f);
            mats[WATER] = Lit(Pal.Hex("#1E5E6A"), null, 0.95f, 0.1f, Pal.Hex("#0B2A33"));
            mats[DARK] = Lit(Pal.Hex("#5B2E1E"), null, 0.2f);
            mats[CARPET] = Lit(Color.white, "carpet", 0.05f);
            mats[LEAF] = Lit(Pal.Hex("#3E6B34"), null, 0.15f);
            mats[BANNER] = Lit(Color.white, "flag", 0.05f);
        }

        void Flush()
        {
            var root = new GameObject("Mahal_Static").transform; root.SetParent(transform, false);
            int tris = 0, meshes = 0;
            foreach (var kv in builders)
            {
                var mb = kv.Value; if (mb.v.Count == 0) continue;
                var mesh = new Mesh { name = "Mahal_" + kv.Key, indexFormat = mb.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(mb.v); mesh.SetNormals(mb.n); mesh.SetUVs(0, mb.uv); mesh.SetTriangles(mb.t, 0); mesh.RecalculateBounds();
                var go = new GameObject(mesh.name); go.transform.SetParent(root, false); go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[kv.Key % 16];
                int zone = kv.Key / 16;
                mr.shadowCastingMode = (q >= 2 && zone <= 1) ? ShadowCastingMode.On : ShadowCastingMode.Off; mr.receiveShadows = q >= 1 && zone <= 1;
                mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                tris += mb.t.Count / 3; meshes++;
            }
            Stats = "mahal static: " + meshes + " meshes, " + tris + " tris";
            Debug.Log("[AgniMahal] " + Stats + " (quality " + q + ")");
        }

        // ------------------------------------------------------------ geometry
        void Courtyard()
        {
            var f = B(FLOOR, 0); f.uvs = 1f / 3.5f; f.M = Matrix4x4.identity;
            f.Quad(new Vector3(-70, 0, -14), new Vector3(-70, 0, 26), new Vector3(70, 0, 26), new Vector3(70, 0, -14), Vector3.up);
            f = B(FLOOR, 60); f.uvs = 1f / 3.5f; f.Quad(new Vector3(-70, 0, 26), new Vector3(-70, 0, 600), new Vector3(70, 0, 600), new Vector3(70, 0, 26), Vector3.up);
            // water channel + red stone curbs, from behind the podium to the far gates
            for (int seg = 0; seg < 4; seg++)
            {
                float z0 = seg == 0 ? 4.2f : seg == 1 ? 30 : seg == 2 ? 90 : 200, z1 = seg == 0 ? 30 : seg == 1 ? 90 : seg == 2 ? 200 : 430; float zm = (z0 + z1) * 0.5f;
                var w = B(WATER, zm); w.uvs = 0.5f; w.Quad(new Vector3(-1.3f, 0.03f, z0), new Vector3(-1.3f, 0.03f, z1), new Vector3(1.3f, 0.03f, z1), new Vector3(1.3f, 0.03f, z0), Vector3.up);
                var r = B(RED, zm); r.uvs = 0.5f;
                r.Box(new Vector3(-1.5f, 0.12f, zm), new Vector3(0.4f, 0.24f, z1 - z0)); r.Box(new Vector3(1.5f, 0.12f, zm), new Vector3(0.4f, 0.24f, z1 - z0));
            }
            var r0 = B(RED, 0); r0.Box(new Vector3(0, 0.12f, 4.2f), new Vector3(3.4f, 0.24f, 0.4f));
            // big fountain basin (octagon-ish lathe) in the channel at z=16
            var bas = B(MARBLE, 16); bas.uvs = 0.5f; bas.Lathe(new Vector3(0, 0, 16), new[] { new Vector2(2.6f, 0), new Vector2(2.6f, 0.45f), new Vector2(2.3f, 0.5f), new Vector2(2.2f, 0.36f), new Vector2(0.01f, 0.36f) }, 16);
            var wat = B(WATER, 16); wat.Lathe(new Vector3(0, 0.38f, 16), new[] { new Vector2(2.21f, 0), new Vector2(0.01f, 0) }, 16);
            var cup = B(MARBLE, 16); cup.Lathe(new Vector3(0, 0, 16), new[] { new Vector2(0.35f, 0), new Vector2(0.25f, 1.2f), new Vector2(0.9f, 1.35f), new Vector2(1.0f, 1.55f), new Vector2(0.2f, 1.55f), new Vector2(0.18f, 2.0f), new Vector2(0.45f, 2.1f), new Vector2(0.01f, 2.2f) }, 14);
            // podium lamps (stambh deepak) flanking the stage
            foreach (float x in new[] { -3.6f, 3.6f })
            {
                var g = B(GOLD, 0); g.Lathe(new Vector3(x, 0, 3.2f), new[] { new Vector2(0.3f, 0), new Vector2(0.28f, 0.22f), new Vector2(0.12f, 0.3f), new Vector2(0.07f, 1.35f), new Vector2(0.13f, 1.42f), new Vector2(0.09f, 1.5f), new Vector2(0.34f, 1.62f), new Vector2(0.28f, 1.7f), new Vector2(0.01f, 1.66f) }, 16);
                var m = B(RED, 0); m.Box(new Vector3(x, 0.08f, 3.2f), new Vector3(0.9f, 0.16f, 0.9f));
            }
            // red carpet runner from the podium towards the fountain
            var cp = B(CARPET, 2); cp.uvs = 1f; cp.M = Matrix4x4.identity;
            { var a = new Vector3(-0.9f, 0.012f, 1.2f); var b = new Vector3(-0.9f, 0.012f, 13.0f); var c2 = new Vector3(0.9f, 0.012f, 13.0f); var d = new Vector3(0.9f, 0.012f, 1.2f);
              cp.Tri(a, b, c2, Vector3.up, new Vector2(0, 0), new Vector2(0, 6), new Vector2(1, 6)); cp.Tri(a, c2, d, Vector3.up, new Vector2(0, 0), new Vector2(1, 6), new Vector2(1, 0)); }
            // potted topiary + small palms along the courtyard (breaks the symmetry a little)
            var rp = new System.Random(4);
            foreach (float sx in new[] { -1f, 1f })
                for (float z = 7f; z < 46f; z += 6.5f)
                {
                    float x = sx * (6.2f + (float)rp.NextDouble() * 0.6f), s = 0.8f + (float)rp.NextDouble() * 0.4f;
                    var pot = B(RED, z); pot.Lathe(new Vector3(x, 0, z), new[] { new Vector2(0.28f * s, 0), new Vector2(0.42f * s, 0.55f * s), new Vector2(0.46f * s, 0.62f * s), new Vector2(0.01f, 0.6f * s) }, 10);
                    var g2 = B(LEAF, z); g2.Lathe(new Vector3(x, 0.55f * s, z), new[] { new Vector2(0.01f, 0), new Vector2(0.45f * s, 0.15f * s), new Vector2(0.62f * s, 0.55f * s), new Vector2(0.5f * s, 1.0f * s), new Vector2(0.22f * s, 1.3f * s), new Vector2(0.01f, 1.4f * s) }, 10);
                }
            // low marble balustrade edging the courtyard in front of the wings
            foreach (float sx in new[] { -1f, 1f })
            {
                var m = B(MARBLE, 10); m.uvs = 0.5f; m.Box(new Vector3(sx * 8.5f, 0.4f, 24), new Vector3(0.25f, 0.8f, 36));
                for (float z = 6; z < 42; z += 3f) { var p = B(MARBLE, z); p.Box(new Vector3(sx * 8.5f, 0.55f, z), new Vector3(0.42f, 1.1f, 0.42f)); }
            }
        }

        void Chhatri(Vector3 p, float s, int domeMat = DOME)
        {
            var b = B(SAND, p.z); b.Box(p + new Vector3(0, 0.15f * s, 0), new Vector3(1.6f * s, 0.3f * s, 1.6f * s));
            for (int i = 0; i < 4; i++) { float x = (i % 2 == 0 ? -0.62f : 0.62f) * s, z = (i < 2 ? -0.62f : 0.62f) * s; b.Box(p + new Vector3(x, 0.85f * s, z), new Vector3(0.16f * s, 1.1f * s, 0.16f * s)); }
            var r = B(RED, p.z); r.Box(p + new Vector3(0, 1.45f * s, 0), new Vector3(2.0f * s, 0.12f * s, 2.0f * s));
            Dome(p + new Vector3(0, 1.5f * s, 0), 0.72f * s, domeMat);
        }
        void Dome(Vector3 p, float R, int mat = DOME)
        {
            var d = B(mat, p.z); var prof = new Vector2[DomeProf.Length]; for (int i = 0; i < prof.Length; i++) prof[i] = DomeProf[i] * R;
            d.Lathe(p, prof, R > 3 ? 24 : 14);
            // plain pinnacle (no religious finial): small gold ball + spike
            var g = B(GOLD, p.z); g.Lathe(p + new Vector3(0, 1.5f * R, 0), new[] { new Vector2(0.05f * R, 0), new Vector2(0.12f * R, 0.08f * R), new Vector2(0.09f * R, 0.2f * R), new Vector2(0.03f * R, 0.24f * R), new Vector2(0.008f * R, 0.5f * R) }, 8);
        }
        void Tower(Vector3 p, float r, float h, bool flag)
        {
            var s = B(SAND, p.z); s.Lathe(p, new[] { new Vector2(r * 1.15f, 0), new Vector2(r * 1.15f, h * 0.08f), new Vector2(r, h * 0.1f), new Vector2(r * 0.88f, h) }, 12);
            var red = B(RED, p.z); for (int i = 1; i <= 3; i++) { float y = h * (0.28f * i); red.Lathe(p + new Vector3(0, y, 0), new[] { new Vector2(r * 1.18f - y / h * r * 0.12f, 0), new Vector2(r * 1.18f - y / h * r * 0.12f, 0.35f), new Vector2(0.01f, 0.35f) }, 12); }
            Chhatri(p + new Vector3(0, h, 0), r * 1.0f);
            if (flag) AddFlag(p + new Vector3(0, h + r * 2.9f, 0), r * 0.9f);
        }

        const float WX = 15f; // inner face of the palace wings (half-width of the great courtyard)
        void Bay(float sx, float z, float w, bool chhatri, bool jharokha, bool lit)
        {
            float x = sx * WX;
            // ground arcade (wall faces the axis: rotate panel so its plane is YZ)
            var M = Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, 90, 0), Vector3.one);
            var s = B(SAND, z); s.M = M; s.uvs = 0.25f; s.ArchWall(w, 5.2f, w * 0.62f, 2.9f, 0.7f, 8, 0.62f);
            var s2 = B(SAND, z); s2.M = Matrix4x4.TRS(new Vector3(x, 6.0f, z), Quaternion.Euler(0, 90, 0), Vector3.one); s2.ArchWall(w, 3.6f, w * 0.42f, 1.7f, 0.6f, 6, 0.6f);
            s.M = s2.M = Matrix4x4.identity;
            // red sandstone bands + chhajja (eaves) + parapet
            var r = B(RED, z); r.uvs = 0.5f;
            r.Box(new Vector3(x, 5.6f, z), new Vector3(1.0f, 0.8f, w)); r.Box(new Vector3(x - sx * 0.55f, 5.25f, z), new Vector3(0.9f, 0.12f, w));
            r.Box(new Vector3(x, 9.75f, z), new Vector3(0.9f, 0.3f, w)); r.Box(new Vector3(x - sx * 0.45f, 9.6f, z), new Vector3(0.8f, 0.1f, w));
            var mm = B(MARBLE, z); for (int k = 0; k < 3; k++) mm.Box(new Vector3(x, 10.15f, z - w * 0.33f + k * w * 0.33f), new Vector3(0.5f, 0.5f, 0.42f));
            // engaged pillars at bay edges
            var col = B(MARBLE, z); col.Lathe(new Vector3(x - sx * 0.38f, 0, z - w * 0.5f), new[] { new Vector2(0.34f, 0), new Vector2(0.3f, 0.3f), new Vector2(0.22f, 0.4f), new Vector2(0.2f, 4.9f), new Vector2(0.3f, 5.2f), new Vector2(0.01f, 5.2f) }, 10);
            // glowing hall behind the arches (emissive warm interior)
            var hall = B(lit ? HALL : DARK, z); hall.Box(new Vector3(x + sx * 2.6f, 4.8f, z), new Vector3(0.3f, 9.6f, w));
            var roof = B(SAND, z); roof.Box(new Vector3(x + sx * 1.6f, 9.5f, z), new Vector3(3.0f, 0.4f, w));
            if (jharokha)
            {
                var j = B(RED, z); j.Box(new Vector3(x - sx * 0.7f, 6.25f, z), new Vector3(0.9f, 0.5f, 1.7f));
                var jm = B(SAND, z); for (int k = 0; k < 2; k++) jm.Box(new Vector3(x - sx * 1.05f, 7.1f, z - 0.7f + k * 1.4f), new Vector3(0.12f, 1.2f, 0.12f));
                Dome(new Vector3(x - sx * 0.7f, 7.75f, z), 0.62f, DOME);
                var jr = B(RED, z); jr.Box(new Vector3(x - sx * 0.7f, 7.72f, z), new Vector3(1.2f, 0.1f, 1.9f));
            }
            if (chhatri) Chhatri(new Vector3(x + sx * 0.4f, 10.3f, z), 1.25f);
            // hanging banners on every 3rd bay (colour against the stone)
            if (jharokha && ((int)(z / w)) % 3 == 0)
            {
                var bn = B(BANNER, z); bn.uvs = 1f; float bx = x - sx * 0.62f;
                var a = new Vector3(bx, 9.4f, z - 0.55f); var b = new Vector3(bx, 6.6f, z - 0.55f); var c2 = new Vector3(bx, 6.6f, z + 0.55f); var d = new Vector3(bx, 9.4f, z + 0.55f);
                Vector3 nrm = new Vector3(-sx, 0, 0);
                bn.Tri(a, b, c2, nrm, new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0)); bn.Tri(a, c2, d, nrm, new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1));
            }
            // the right-hand wing is a storey taller (asymmetry reads as a real, grown palace)
            if (sx > 0 && ((int)(z / w)) % 2 == 0)
            {
                var s3 = B(SAND, z); s3.M = Matrix4x4.TRS(new Vector3(x + 0.6f, 10.0f, z), Quaternion.Euler(0, 90, 0), Vector3.one); s3.ArchWall(w, 3.2f, w * 0.36f, 1.5f, 0.6f, 6, 0.6f); s3.M = Matrix4x4.identity;
                var r3 = B(RED, z); r3.Box(new Vector3(x + 0.6f, 13.35f, z), new Vector3(0.9f, 0.3f, w));
            }
        }

        void Wings()
        {
            float zEnd = q == 0 ? 160 : q == 1 ? 260 : 420; float w = 4.2f; int i = 0;
            for (float z = 7.5f; z < zEnd; z += w, i++)
                foreach (float sx in new[] { -1f, 1f })
                    Bay(sx, z, w, i % 3 == 1, i % 2 == 0, (i + (sx > 0 ? 1 : 0)) % 3 != 2);
            // corner towers at the wing heads (frame the courtyard)
            foreach (float sx in new[] { -1f, 1f }) Tower(new Vector3(sx * (WX + 0.9f), 0, 5.4f), 1.4f, 14f, true);
            // second, taller wing behind the arcade (gives the "palace keeps going" layering)
            foreach (float sx in new[] { -1f, 1f })
                for (float z = 12; z < zEnd; z += 18)
                {
                    float bx = sx * (WX + 7.5f);
                    var s = B(SAND, z); s.Box(new Vector3(bx, 7.5f, z), new Vector3(6f, 15f, 16f));
                    var r = B(RED, z); r.Box(new Vector3(bx, 15.2f, z), new Vector3(6.6f, 0.4f, 16.6f));
                    var h = B(HALL, z); for (int k = 0; k < 3; k++) h.Box(new Vector3(bx - sx * 3.05f, 12.2f, z - 5 + k * 5), new Vector3(0.1f, 2.2f, 1.6f));
                    Chhatri(new Vector3(bx - sx * 1.5f, 15.4f, z - 5), 1.6f); Chhatri(new Vector3(bx - sx * 1.5f, 15.4f, z + 5), 1.6f);
                    Dome(new Vector3(bx + sx * 1.5f, 15.4f, z), 2.6f);
                }
        }

        void Gate(float z, float scale)
        {
            float W = WX * 2f + 0.2f, H = 19f * scale;
            var s = B(SAND, z); s.M = Matrix4x4.TRS(new Vector3(0, 0, z), Quaternion.identity, Vector3.one); s.uvs = 0.25f;
            s.ArchWall(W, H, 10f, 9.5f * scale, 2.6f, 14, 0.66f);
            s.M = Matrix4x4.identity;
            var r = B(RED, z); r.Box(new Vector3(0, H + 0.25f, z), new Vector3(W + 1f, 0.5f, 2.8f)); r.Box(new Vector3(0, H * 0.62f, z - 1.25f), new Vector3(W - 1.2f, 0.25f, 0.4f));
            // arch outline in gold
            var g = B(GOLD, z); g.M = Matrix4x4.TRS(new Vector3(0, 0, z - 1.33f), Quaternion.identity, Vector3.one); g.ArchWall(11f, 9.5f * scale + 6.6f, 10f, 9.5f * scale, 0.06f, 14, 0.66f); g.M = Matrix4x4.identity;
            // side towers + top pavilion
            Tower(new Vector3(-W * 0.5f - 0.8f, 0, z), 2.0f, H + 4f, true); Tower(new Vector3(W * 0.5f + 0.8f, 0, z), 2.0f, H + 4f, true);
            Chhatri(new Vector3(-7f, H + 0.5f, z), 1.8f); Chhatri(new Vector3(7f, H + 0.5f, z), 1.8f);
            Dome(new Vector3(0, H + 0.5f, z), 3.6f);
            var hall = B(HALL, z); for (int k = -2; k <= 2; k++) if (k != 0) hall.Box(new Vector3(k * 5.2f, H * 0.8f, z - 1.35f), new Vector3(2.0f, 2.4f, 0.05f));
        }
        void Gates() { float[] zs = q == 0 ? new[] { 80f, 170f } : new[] { 80f, 165f, 260f, 370f }; for (int i = 0; i < zs.Length; i++) Gate(zs[i], 1f + i * 0.08f); }

        void FarPalace()
        {
            float z = 520; var s = B(SAND, z); var r = B(RED, z);
            s.Box(new Vector3(0, 24, z), new Vector3(150, 48, 30)); r.Box(new Vector3(0, 48.6f, z), new Vector3(154, 1.2f, 32));
            s.Box(new Vector3(0, 40, z - 6), new Vector3(60, 80, 24));
            Dome(new Vector3(0, 80, z - 6), 22f);
            for (int i = -3; i <= 3; i++) if (i != 0) { Dome(new Vector3(i * 20f, 49, z), 6f); Tower(new Vector3(i * 21.5f, 0, z - 17), 3f, 62f + (3 - Mathf.Abs(i)) * 9f, Mathf.Abs(i) <= 2); }
            var h = B(HALL, z); for (int i = -6; i <= 6; i++) h.Box(new Vector3(i * 10f, 30, z - 15.1f), new Vector3(3f, 7f, 0.2f));
        }

        void Pavilions()
        {
            // domed pavilions and towers behind the wings, receding — breaks the skyline and sells depth
            var rnd = new System.Random(19);
            float[] zs = { 30, 70, 120, 175, 240, 320, 400 };
            for (int i = 0; i < zs.Length; i++)
                foreach (float sx in new[] { -1f, 1f })
                {
                    if (q == 0 && i > 3) continue;
                    float z = zs[i] + (float)rnd.NextDouble() * 12f, x = sx * (WX + 22f + i * 7f + (float)rnd.NextDouble() * 6f), hgt = 17f + i * 3f;
                    var s = B(SAND, z); s.Box(new Vector3(x, hgt * 0.5f, z), new Vector3(16f, hgt, 16f));
                    var r = B(RED, z); r.Box(new Vector3(x, hgt + 0.3f, z), new Vector3(17.5f, 0.6f, 17.5f));
                    Dome(new Vector3(x, hgt + 0.6f, z), 5.2f + i * 0.4f);
                    Chhatri(new Vector3(x - 6f, hgt + 0.6f, z - 6f), 1.7f); Chhatri(new Vector3(x + 6f, hgt + 0.6f, z - 6f), 1.7f);
                    var h = B(HALL, z); for (int k = -1; k <= 1; k++) h.Box(new Vector3(x + k * 4.5f, hgt * 0.62f, z - 8.05f), new Vector3(2.2f, 3.2f, 0.1f));
                    if (i % 2 == 0) Tower(new Vector3(x + sx * 11f, 0, z + 4f), 2.2f, hgt + 16f, true);
                }
        }

        // ------------------------------------------------------------ sky, hills, atmosphere
        static Material SpriteMat(Texture tex)
        {
            var m = new Material(Shader.Find("Sprites/Default")); if (tex != null) m.mainTexture = tex; return m;
        }
        void Sky()
        {
            skyTex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            SetSky(0f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(go.GetComponent<Collider>()); go.name = "Sky"; go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 150, 1400); go.transform.localScale = new Vector3(4200, 700, 1);
            skyMat = SpriteMat(skyTex); skyMat.renderQueue = 1000; go.GetComponent<MeshRenderer>().sharedMaterial = skyMat; go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // sun glow
            var sunGo = new GameObject("SunGlow"); sunGo.transform.SetParent(transform, false); sunGo.transform.position = new Vector3(-160, 70, 1350);
            var sr = sunGo.AddComponent<SpriteRenderer>(); sr.sprite = K.S("glow"); sr.color = new Color(1f, 0.85f, 0.55f, 0.95f); sunGo.transform.localScale = Vector3.one * 900; sr.sortingOrder = -50;
            var core = new GameObject("SunCore"); core.transform.SetParent(sunGo.transform, false); var cr = core.AddComponent<SpriteRenderer>(); cr.sprite = K.S("circle"); cr.color = new Color(1f, 0.96f, 0.85f, 1f); core.transform.localScale = Vector3.one * 0.06f; cr.sortingOrder = -49;
            cam.farClipPlane = 1600;
        }
        // t: 0 = golden hour, 1 = sunset
        void SetSky(float t)
        {
            Color top = Color.Lerp(Pal.Hex("#3D7BD6"), Pal.Hex("#3C4C9A"), t), mid = Color.Lerp(Pal.Hex("#A9C9E8"), Pal.Hex("#E8A79A"), t), hor = Color.Lerp(Pal.Hex("#F2E6D2"), Pal.Hex("#F6C89A"), t);
            for (int i = 0; i < 64; i++) { float y = i / 63f; skyCols[i] = y < 0.3f ? hor : y < 0.55f ? Color.Lerp(hor, mid, Mathf.SmoothStep(0, 1, (y - 0.3f) / 0.25f)) : Color.Lerp(mid, top, Mathf.SmoothStep(0, 1, (y - 0.55f) / 0.4f)); }
            skyTex.SetPixels(skyCols); skyTex.Apply(false);
            RenderSettings.fogColor = Color.Lerp(Pal.Hex("#DCD8D2"), Pal.Hex("#E8C2A2"), t);
            if (sun != null) { sun.color = Color.Lerp(Pal.Hex("#FFF3E2"), Pal.Hex("#FFC48E"), t); sun.intensity = Mathf.Lerp(1.15f, 0.95f, t); }
            RenderSettings.ambientSkyColor = Color.Lerp(Pal.Hex("#7F98B2"), Pal.Hex("#7E6690"), t);
            RenderSettings.ambientEquatorColor = Color.Lerp(Pal.Hex("#A89A8A"), Pal.Hex("#B08470"), t);
            RenderSettings.ambientGroundColor = Pal.Hex("#5A3A2A");
        }
        void Hills()
        {
            Color[] cols = { Pal.Hex("#C9A2A0"), Pal.Hex("#B79AAE"), Pal.Hex("#A592B5") };
            float[] zz = { 900, 1050, 1200 }; float[] hh = { 70, 120, 170 };
            for (int L = 0; L < 3; L++)
            {
                var mesh = new Mesh(); var vs = new List<Vector3>(); var tr = new List<int>(); var cs = new List<Color>();
                int nseg = 60; float W = 3600; var rnd = new System.Random(5 + L);
                float ph1 = (float)rnd.NextDouble() * 6, ph2 = (float)rnd.NextDouble() * 6;
                for (int i = 0; i <= nseg; i++)
                {
                    float x = -W / 2 + W * i / nseg; float y = hh[L] * (0.55f + 0.3f * Mathf.Sin(x * 0.004f + ph1) + 0.15f * Mathf.Sin(x * 0.013f + ph2)) ;
                    vs.Add(new Vector3(x, -40, zz[L])); vs.Add(new Vector3(x, y, zz[L])); cs.Add(cols[L]); cs.Add(cols[L]);
                    if (i > 0) { int a = (i - 1) * 2; tr.Add(a); tr.Add(a + 1); tr.Add(a + 3); tr.Add(a); tr.Add(a + 3); tr.Add(a + 2); }
                }
                mesh.SetVertices(vs); mesh.SetColors(cs); mesh.SetTriangles(tr, 0); mesh.RecalculateBounds();
                var go = new GameObject("Hills" + L); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); var m = SpriteMat(Texture2D.whiteTexture); m.renderQueue = 1001 + (2 - L); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                hillMats.Add(m);
            }
        }
        readonly List<Material> hillMats = new List<Material>();

        void Clouds()
        {
            int n = q == 0 ? 4 : q == 1 ? 7 : 10; var rnd = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Cloud" + i); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = K.S("mh_cloud" + (i % 3)); sr.color = new Color(1, 0.94f, 0.9f, 0.85f); sr.sortingOrder = -40;
                float z = 1250 - (float)rnd.NextDouble() * 200; go.transform.position = new Vector3(-1400 + (float)rnd.NextDouble() * 2800, 170 + (float)rnd.NextDouble() * 260, z);
                go.transform.localScale = Vector3.one * (60 + (float)rnd.NextDouble() * 70); clouds.Add(go.transform);
            }
        }

        void Birds()
        {
            int n = q == 0 ? 5 : q == 1 ? 9 : q == 2 ? 14 : 20; var rnd = new System.Random(11);
            var body = Lit(Pal.Hex("#E9E4DE"), null, 0.2f); var wingM = Lit(Pal.Hex("#B9B6BC"), null, 0.2f);
            for (int i = 0; i < n; i++)
            {
                var b = new GameObject("Pigeon" + i).transform; b.SetParent(transform, false);
                var bd = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(bd.GetComponent<Collider>()); bd.transform.SetParent(b, false); bd.transform.localScale = new Vector3(0.18f, 0.16f, 0.42f); bd.GetComponent<MeshRenderer>().sharedMaterial = body;
                for (int s = -1; s <= 1; s += 2)
                {
                    var piv = new GameObject("WingPivot").transform; piv.SetParent(b, false); piv.localPosition = new Vector3(s * 0.05f, 0.03f, 0);
                    var wg = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(wg.GetComponent<Collider>()); wg.transform.SetParent(piv, false);
                    wg.transform.localPosition = new Vector3(s * 0.28f, 0, 0); wg.transform.localScale = new Vector3(0.56f, 0.02f, 0.24f); wg.GetComponent<MeshRenderer>().sharedMaterial = wingM;
                }
                foreach (var r in b.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                float scale = 1.6f + (float)rnd.NextDouble() * 0.8f; b.localScale = Vector3.one * scale;
                // x: radius, y: height, z: speed (rad/s), w: phase
                birdP.Add(new Vector4(8f + (float)rnd.NextDouble() * 12f, 7.5f + (float)rnd.NextDouble() * 7f, (0.28f + (float)rnd.NextDouble() * 0.16f) * (i % 4 == 0 ? -1 : 1), (float)rnd.NextDouble() * 6.28f));
                birds.Add(b);
            }
        }

        void Diyas()
        {
            var mb = new MB(); var glow = K.S("glow");
            void Diya(Vector3 p, float s, bool big)
            {
                mb.Lathe(p, new[] { new Vector2(0.01f, 0), new Vector2(0.11f * s, 0.02f), new Vector2(0.15f * s, 0.08f * s), new Vector2(0.01f, 0.07f * s) }, 8);
                var go = new GameObject("Flame"); go.transform.SetParent(transform, false); go.transform.position = p + new Vector3(0, 0.12f * s, 0);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = glow; sr.color = new Color(1f, 0.6f, 0.18f, 0.8f); go.transform.localScale = Vector3.one * (big ? 0.42f : 0.5f) * s;
                flames.Add(sr); flameA.Add(Random.value * 10f);
                var core = new GameObject("Core"); core.transform.SetParent(go.transform, false); var cr = core.AddComponent<SpriteRenderer>(); cr.sprite = glow; cr.color = new Color(1f, 0.95f, 0.7f, 1f); core.transform.localScale = Vector3.one * 0.35f; cr.sortingOrder = 1;
            }
            // podium lamps
            Diya(new Vector3(-3.6f, 1.68f, 3.2f), 2.0f, true); Diya(new Vector3(3.6f, 1.68f, 3.2f), 2.0f, true);
            float zEnd = q == 0 ? 40 : q == 1 ? 90 : 160; float step = q >= 2 ? 3f : 5f;
            for (float z = 5f; z < zEnd; z += step) { Diya(new Vector3(-1.5f, 0.24f, z), 1f, false); Diya(new Vector3(1.5f, 0.24f, z), 1f, false); }
            // lamps on the wing ledges (every bay)
            if (q >= 1) for (float z = 7.5f; z < zEnd; z += 4.2f) { Diya(new Vector3(-WX + 0.65f, 5.32f, z), 1.4f, false); Diya(new Vector3(WX - 0.65f, 5.32f, z), 1.4f, false); }
            var mesh = new Mesh { name = "Diyas" }; mesh.SetVertices(mb.v); mesh.SetNormals(mb.n); mesh.SetUVs(0, mb.uv); mesh.SetTriangles(mb.t, 0); mesh.RecalculateBounds();
            var dg = new GameObject("DiyaBowls"); dg.transform.SetParent(transform, false); dg.AddComponent<MeshFilter>().sharedMesh = mesh; var dmr = dg.AddComponent<MeshRenderer>(); dmr.sharedMaterial = mats[GOLD]; dmr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Fountains()
        {
            var mat = SpriteMat(K.S("glow") != null ? K.S("glow").texture : null);
            void Jet(Vector3 p, float speed, float size, int rate, float spread)
            {
                var go = new GameObject("Fountain"); go.transform.SetParent(transform, false); go.transform.position = p; go.transform.rotation = Quaternion.Euler(-90, 0, 0);
                var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.duration = 2; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.9f, speed);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); main.gravityModifier = 1f; main.startColor = new Color(0.85f, 0.95f, 1f, 0.75f); main.maxParticles = rate * 2; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = rate;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = spread; sh.radius = 0.05f;
                var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.8f, 0.93f, 1f), 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0, 1) }); col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                ps.Simulate(2f, true, true); ps.Play();
            }
            Jet(new Vector3(0, 2.2f, 16), 4.2f, 0.16f, q >= 2 ? 160 : 80, 6);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; Jet(new Vector3(Mathf.Cos(a) * 1.6f, 0.4f, 16 + Mathf.Sin(a) * 1.6f), 2.6f, 0.1f, q >= 2 ? 40 : 20, 3); }
            int nj = q >= 2 ? 8 : 4;
            for (int i = 0; i < nj; i++) { float z = 26 + i * 7f; Jet(new Vector3(-0.9f, 0.05f, z), 3.0f, 0.1f, q >= 2 ? 36 : 18, 2); Jet(new Vector3(0.9f, 0.05f, z), 3.0f, 0.1f, q >= 2 ? 36 : 18, 2); }
        }

        void Beams()
        {
            for (int i = 0; i < (q >= 3 ? 5 : 3); i++)
            {
                var go = new GameObject("LightShaft" + i); go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(-9 + i * 4.5f, 9f, 14 + i * 5f); go.transform.rotation = Quaternion.Euler(0, 0, -24 + i * 3f);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = K.S("mh_beam"); sr.color = new Color(1f, 0.88f, 0.6f, 0.16f); go.transform.localScale = new Vector3(14f, 6f, 1f); sr.sortingOrder = 20;
                beams.Add(sr);
            }
        }

        void Petals()
        {
            var go = new GameObject("Petals"); go.transform.SetParent(cam.transform, false); go.transform.localPosition = new Vector3(0, 4.5f, 6f);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = 9f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f); main.gravityModifier = 0.025f;
            main.startRotation3D = false; main.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
            var g = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.65f, 0.95f), new Color(1f, 0.72f, 0.2f, 0.95f)); g.mode = ParticleSystemGradientMode.TwoColors; main.startColor = g;
            var em = ps.emission; em.rateOverTime = q >= 3 ? 14 : 8;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(14, 0.5f, 8);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.55f); vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) }); col.color = gr;
            var r = go.GetComponent<ParticleSystemRenderer>(); var sp = K.S("mh_petal"); r.sharedMaterial = SpriteMat(sp != null ? sp.texture : null); r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Simulate(8f, true, true); ps.Play();
        }

        void Atmosphere()
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 70f; RenderSettings.fogEndDistance = 1000f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var urp = cam.GetComponent<UniversalAdditionalCameraData>();
            if (q >= 2 && urp != null)
            {
                urp.renderPostProcessing = true;
                var vgo = new GameObject("MahalVolume"); vgo.transform.SetParent(transform, false); var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true;
                var prof = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(q >= 3 ? 0.25f : 0.15f); bloom.threshold.Override(1.3f); bloom.scatter.Override(0.5f);
                // bloom only: the HUD canvas renders through this camera, so no tonemap / vignette (they would dull the UI);
                // threshold > 1 means only HDR emissives (diyas, lit halls, sun) bloom, never the UI.
                vol.sharedProfile = prof;
            }
        }

        // ------------------------------------------------------------ flags
        class Flag { public Mesh mesh; public Vector3[] baseV, work; public float phase; public float len; }
        void AddFlag(Vector3 pole, float s)
        {
            if (q == 0 && flags.Count >= 6) return;
            var g = B(GOLD, pole.z); g.Box(pole + new Vector3(0, -s * 0.6f, 0), new Vector3(0.06f * s, s * 3.2f, 0.06f * s));
            int nx = 10, ny = 5; float W = 1.9f * s, H = 1.15f * s;
            var vs = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[vs.Length]; var tr = new List<int>();
            for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++) { int i = y * (nx + 1) + x; vs[i] = new Vector3((float)x / nx * W, -(float)y / ny * H, 0); uv[i] = new Vector2((float)x / nx, 1 - (float)y / ny); }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) { int a = y * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1; tr.AddRange(new[] { a, b, d, a, d, c, a, d, b, a, c, d }); }
            var m = new Mesh { name = "Flag" }; m.vertices = vs; m.uv = uv; m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); m.MarkDynamic();
            var go = new GameObject("Flag"); go.transform.SetParent(transform, false); go.transform.position = pole + new Vector3(0, s * 0.95f, 0);
            go.transform.rotation = Quaternion.Euler(0, -70 + Random.Range(-10f, 10f), 0);
            go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>();
            if (flagMat == null) { flagMat = Lit(Color.white, "flag", 0.1f, 0, Pal.Hex("#3A1406")); flagMat.SetFloat("_Cull", 0); }
            mr.sharedMaterial = flagMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            flags.Add(new Flag { mesh = m, baseV = vs, work = (Vector3[])vs.Clone(), phase = Random.value * 6f, len = W });
        }
        Material flagMat;

        // ------------------------------------------------------------ animation
        void Update()
        {
            float t = Time.time;
            // slow cinematic drift (keeps the podium framed)
            if (cam != null)
            {
                float dx = Mathf.Sin(t * 0.26f) * 0.55f, dy = Mathf.Sin(t * 0.17f + 1f) * 0.08f, dz = Mathf.Sin(t * 0.12f) * 0.35f;
                cam.transform.position = camBase + new Vector3(dx, dy, dz);
                cam.transform.rotation = camRot * Quaternion.Euler(Mathf.Sin(t * 0.17f) * 0.4f, -dx * 2.6f, 0);
            }
            // golden hour <-> sunset, 100 s ping-pong (starts warm)
            float cyc = 0.15f + 0.6f * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 120f));
            if (skyTex != null && Time.frameCount % 6 == 0) SetSky(cyc);
            for (int i = 0; i < flames.Count; i++)
            {
                float f = 0.75f + 0.25f * Mathf.PerlinNoise(t * 6f, flameA[i]); var c = flames[i].color; c.a = 0.55f + 0.4f * f; flames[i].color = c;
            }
            for (int i = 0; i < birds.Count; i++)
            {
                var p = birdP[i]; float a = p.w + t * p.z;
                var pos = new Vector3(4f + Mathf.Cos(a) * p.x, p.y + Mathf.Sin(a * 2.3f) * 1.2f, 30f + Mathf.Sin(a) * p.x * 0.9f);
                var tangent = new Vector3(-Mathf.Sin(a) * p.x, Mathf.Cos(a * 2.3f) * 2.76f, Mathf.Cos(a) * p.x * 0.9f) * Mathf.Sign(p.z);
                birds[i].position = pos; birds[i].rotation = Quaternion.LookRotation(tangent.normalized) * Quaternion.Euler(0, 0, Mathf.Sin(a) * 12f);
                float flap = Mathf.Sin(t * 13f + i * 1.7f) * 38f;
                birds[i].GetChild(1).localRotation = Quaternion.Euler(0, 0, -flap); birds[i].GetChild(2).localRotation = Quaternion.Euler(0, 0, flap);
            }
            for (int i = 0; i < clouds.Count; i++) { var p = clouds[i].position; p.x += Time.deltaTime * (6f + i % 3 * 2f); if (p.x > 1500) p.x = -1500; clouds[i].position = p; }
            for (int i = 0; i < beams.Count; i++) { var c = beams[i].color; c.a = 0.05f + 0.03f * Mathf.Sin(t * 0.5f + i * 1.3f); beams[i].color = c; }
            int every = q >= 2 ? 1 : 2; // flags at half rate on LOW/NORMAL
            if (Time.frameCount % every == 0)
                foreach (var f in flags)
                {
                    for (int i = 0; i < f.baseV.Length; i++)
                    {
                        var b = f.baseV[i]; float u = b.x / f.len;
                        f.work[i] = new Vector3(b.x * (1 - 0.05f * u), b.y - u * u * 0.08f * f.len, Mathf.Sin(t * 4.2f + f.phase - u * 5.5f) * 0.16f * f.len * u + Mathf.Sin(t * 7.1f - u * 9f + b.y) * 0.03f * f.len * u);
                    }
                    f.mesh.vertices = f.work; f.mesh.RecalculateNormals();
                }
        }
    }
}
