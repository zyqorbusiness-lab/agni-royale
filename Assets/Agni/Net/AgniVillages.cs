// Agni Royale — VILLAGES: lootable houses built from code at scene load (deterministic, same on server and every client).
// Building types: Gol Jhopdi (round mud hut, thatch cone), Kutcha Ghar (mud hut, thatch gable), Pakka Ghar (brick, clay-tile roof),
// Chhat Ghar (plaster flat roof + outside stairs to a rooftop), Do-Manzila (2-storey, inside stairs + terrace), Tin Shed, Godown (warehouse).
// Each building = one LODGroup (LOD0 full + interior, LOD1 shell, LOD2 block) + one MeshCollider (closed boxes; stairs use ramps).
// Materials: CC0 textures from ambientCG (see CREDITS.md), loaded from Resources/AgniNet/Village (M_V_*.mat made by the editor setup).
// Loot spots (floor / table / roof) are collected in Spots; the server fills them (AgniLoot).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Net
{
    public enum BType : byte { GolJhopdi, KutchaGhar, PakkaGhar, ChhatGhar, DoManzila, TinShed, Godown }
    public enum VMat : byte { PlasterA, PlasterB, PlasterC, PlasterD, Band, Brick, Mud, Lime, Thatch, Wood, Tin, Tile, Concrete, Dark, Dirt, Cloth, MudBase, Count }
    public struct LootSpot { public Vector3 pos; public byte tier; public ushort building; }

    public static class AgniVillages
    {
        public class Village { public string name; public Vector2 c; public float r; public BType[] kinds; public Village(string n, Vector2 c, float r, params BType[] k) { name = n; this.c = c; this.r = r; kinds = k; } }
        public static readonly List<LootSpot> Spots = new List<LootSpot>();
        public static readonly List<Village> Villages = new List<Village>();
        public static readonly List<(Vector2 c, float r)> Footprints = new List<(Vector2, float)>();
        public static Transform Root; static int buildingCount;

        static readonly string[] MatNames = { "M_V_PlasterA", "M_V_PlasterB", "M_V_PlasterC", "M_V_PlasterD", "M_V_Band", "M_V_Brick", "M_V_Mud", "M_V_Lime", "M_V_Thatch", "M_V_Wood", "M_V_Tin", "M_V_Tile", "M_V_Concrete", "M_V_Dark", "M_V_Dirt", "M_V_Cloth", "M_V_MudBase" };
        // metres per texture repeat
        static readonly float[] Tile = { 2.6f, 2.6f, 2.6f, 2.6f, 2.2f, 1.6f, 2.4f, 2.6f, 2.2f, 1.6f, 1.8f, 1.6f, 2.5f, 2f, 5f, 1f, 2.4f };
        static Material[] mats;

        /// Village table (also read by the editor to keep trees/rocks out of them).
        public static List<Village> Layout()
        {
            // IMPORT v1: the loot villages stand in their India-map zones (MAP_DESIGN / map.json); the 6 Blender landmarks take the other zones
            var L = new List<Village>();
            Vector2 Z(string n) => Agni.Import.AgniIndia.ZoneW(n);
            L.Add(new Village("Vijay Dwar Chowk", Z("Vijay Dwar Chowk"), 40, BType.DoManzila, BType.DoManzila, BType.ChhatGhar, BType.ChhatGhar, BType.ChhatGhar, BType.PakkaGhar, BType.PakkaGhar, BType.TinShed, BType.ChhatGhar));
            L.Add(new Village("Sarson Khet", Z("Sarson Khet"), 30, BType.GolJhopdi, BType.GolJhopdi, BType.GolJhopdi, BType.KutchaGhar, BType.KutchaGhar, BType.KutchaGhar, BType.GolJhopdi, BType.KutchaGhar, BType.TinShed, BType.GolJhopdi));
            L.Add(new Village("Gulabi Bazaar", Z("Gulabi Bazaar"), 30, BType.ChhatGhar, BType.ChhatGhar, BType.DoManzila, BType.DoManzila, BType.PakkaGhar, BType.PakkaGhar, BType.ChhatGhar));
            L.Add(new Village("Sangmarmar Bagh", Z("Sangmarmar Bagh"), 29, BType.ChhatGhar, BType.ChhatGhar, BType.KutchaGhar, BType.KutchaGhar, BType.GolJhopdi, BType.PakkaGhar, BType.GolJhopdi));
            L.Add(new Village("Ghat Nagari", Z("Ghat Nagari"), 29, BType.GolJhopdi, BType.GolJhopdi, BType.GolJhopdi, BType.KutchaGhar, BType.KutchaGhar, BType.GolJhopdi, BType.TinShed, BType.KutchaGhar));
            L.Add(new Village("Rann Safed", Z("Rann Safed"), 29, BType.DoManzila, BType.PakkaGhar, BType.PakkaGhar, BType.PakkaGhar, BType.KutchaGhar, BType.KutchaGhar, BType.ChhatGhar, BType.TinShed));
            L.Add(new Village("Char Minar Chowk", Z("Char Minar Chowk"), 30, BType.Godown, BType.Godown, BType.TinShed, BType.TinShed, BType.ChhatGhar, BType.TinShed, BType.DoManzila));
            L.Add(new Village("Himgiri Ridge", Z("Himgiri Ridge"), 24, BType.KutchaGhar, BType.KutchaGhar, BType.TinShed, BType.PakkaGhar, BType.KutchaGhar));
            L.Add(new Village("Sunehri Beach", Z("Sunehri Beach"), 26, BType.GolJhopdi, BType.GolJhopdi, BType.TinShed, BType.GolJhopdi, BType.KutchaGhar, BType.GolJhopdi));
            L.Add(new Village("Rajmahal", Z("Rajmahal"), 32, BType.DoManzila, BType.DoManzila, BType.ChhatGhar, BType.ChhatGhar, BType.PakkaGhar, BType.ChhatGhar));
            // small spots
            var sp = Agni.Import.AgniIndia.Map.spots;
            if (sp != null) foreach (var s in sp)
                {
                    var c = new Vector2(s.x - Agni.Import.AgniIndia.Off.x, s.z - Agni.Import.AgniIndia.Off.y);
                    switch (s.kind)
                    {
                        case "shed": L.Add(new Village(s.name, c, 16, BType.Godown, BType.TinShed)); break;
                        case "power": L.Add(new Village(s.name, c, 16, BType.Godown, BType.TinShed, BType.TinShed)); break;
                        case "airstrip": L.Add(new Village(s.name, c, 16, BType.TinShed, BType.TinShed, BType.ChhatGhar)); break;
                        case "ruins": L.Add(new Village(s.name, c, 14, BType.KutchaGhar, BType.KutchaGhar)); break;
                        default: L.Add(new Village(s.name, c, 12, BType.TinShed, BType.KutchaGhar)); break;
                    }
                }
            return L;
        }

        public static Transform Build(Transform parent)
        {
            if (Root != null) return Root;
            Spots.Clear(); Villages.Clear(); Footprints.Clear(); buildingCount = 0;
            Footprints.AddRange(Agni.Import.AgniLandmarks.Footprints); // IMPORT v1: Blender landmarks own their ground (no village house / farm hut / tree on them)
            mats = new Material[(int)VMat.Count];
            for (int i = 0; i < mats.Length; i++) { mats[i] = Resources.Load<Material>("AgniNet/Village/" + MatNames[i]); if (mats[i] == null) mats[i] = Fallback((VMat)i); }
            Root = new GameObject("AgniVillages").transform; if (parent != null) Root.SetParent(parent, false);
            var rnd = new System.Random(4242);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            foreach (var v in Layout())
            {
                Villages.Add(v);
                var vt = new GameObject("Village_" + v.name.Replace(' ', '_')).transform; vt.SetParent(Root, false);
                vt.localPosition = new Vector3(0, Agni.Import.AgniIndia.VillageY(v.c), 0); // the zone's flattened pad on the India terrain
                DirtPatch(vt, new Vector3(v.c.x, 0, v.c.y), v.r * 0.8f, rnd);
                int placed = 0;
                for (int i = 0; i < v.kinds.Length; i++)
                {
                    var kind = v.kinds[i]; float fr = FootR(kind);
                    for (int tries = 0; tries < 60; tries++)
                    {
                        float ang = R(0, Mathf.PI * 2), rad = Mathf.Sqrt(R(i == 0 ? 0f : 0.05f, 1f)) * (v.r - fr * 0.5f);
                        var p = v.c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                        if (!Free(p, fr + 2.2f)) continue;
                        // face the village centre (door towards the chowk) with a little jitter, snapped like a real lane
                        var toC = v.c - p; float yaw = toC.sqrMagnitude < 1 ? R(0, 360) : Mathf.Atan2(toC.x, toC.y) * Mathf.Rad2Deg + 180f;
                        yaw = Mathf.Round(yaw / 15f) * 15f + R(-4, 4);
                        Building(kind, vt, new Vector3(p.x, 0, p.y), yaw, rnd); Footprints.Add((p, fr)); placed++; break;
                    }
                }
                Sign(vt, v, rnd);
            }
            // lonely farm huts / sheds between the villages
            var lone = new[] { BType.KutchaGhar, BType.GolJhopdi, BType.TinShed, BType.ChhatGhar, BType.KutchaGhar, BType.PakkaGhar, BType.GolJhopdi, BType.TinShed, BType.KutchaGhar, BType.ChhatGhar, BType.GolJhopdi, BType.PakkaGhar,
                               BType.KutchaGhar, BType.GolJhopdi, BType.TinShed, BType.PakkaGhar, BType.GolJhopdi, BType.ChhatGhar, BType.KutchaGhar, BType.TinShed, BType.GolJhopdi, BType.PakkaGhar }; // +10 for the bigger island
            var ft = new GameObject("Farms").transform; ft.SetParent(Root, false);
            for (int pass = 0; pass < 3; pass++) foreach (var kind in lone) // the India map is ~4 km: 3 x 22 farm huts / sheds across the land
                for (int tries = 0; tries < 400; tries++)
                {
                    var p = new Vector2(R(-1550, 1750), R(-1700, 1600)); float fr = FootR(kind); if (!Agni.Import.AgniIndia.OnLand(p.x, p.y, 2f)) continue;
                    if (Agni.Import.AgniIsland.IsWaterOrBank(p.x, p.y, fr + 3f) || !Agni.Import.AgniIsland.IsFlat(p.x, p.y, fr + 2f) || Agni.Import.AgniIsland.RoadDist(p.x, p.y) < fr + 3f) continue;
                    bool inV = false; foreach (var v in Villages) if ((p - v.c).magnitude < v.r + fr + 12) inV = true;
                    if (inV || !Free(p, fr + 18)) continue;
                    Building(kind, ft, new Vector3(p.x, GroundUnder(p, fr), p.y), R(0, 360), rnd); Footprints.Add((p, fr)); break;
                }
            // IMPORT v1: a few houses at the edge of each landmark, so it stands in a lived-in place (not on a bare lawn)
            var lt = new GameObject("LandmarkEdge").transform; lt.SetParent(Root, false);
            BType[] edge = { BType.KutchaGhar, BType.PakkaGhar, BType.ChhatGhar, BType.TinShed, BType.GolJhopdi };
            foreach (var s in Agni.Import.AgniLandmarks.Table)
            {
                if (s.name == "VindhyaShikhar" || s.name == "KayalVillage") continue;
                var c = new Vector2(s.pos.x, s.pos.z); int got = 0;
                for (int tries = 0; tries < 300 && got < 8; tries++)
                {
                    var kind = edge[rnd.Next(edge.Length)]; float fr = FootR(kind);
                    float a = R(0, Mathf.PI * 2), r = s.footR * R(0.45f, 1.0f) + fr + R(2, 22); var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (Agni.Import.AgniIsland.InKit(p.x, p.y, fr + 3f) || Agni.Import.AgniIsland.IsWaterOrBank(p.x, p.y, fr + 3f) || !Agni.Import.AgniIsland.IsFlat(p.x, p.y, fr + 2f) || Agni.Import.AgniIsland.RoadDist(p.x, p.y) < fr + 2.5f || !FreeExceptLandmarks(p, fr + 4f)) continue;
                    // face the nearest road / the landmark
                    var to = c - p; float yaw = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg + 180f; yaw = Mathf.Round(yaw / 15f) * 15f + R(-5, 5);
                    Building(kind, lt, new Vector3(p.x, GroundUnder(p, fr), p.y), yaw, rnd); Footprints.Add((p, fr)); got++;
                }
            }
            Debug.Log("[AgniVillages] built " + buildingCount + " buildings in " + Villages.Count + " villages, loot spots " + Spots.Count);
            return Root;
        }
        /// a building on uneven ground: sit it at the low side (its plinth/floor slab covers the rest)
        static float GroundUnder(Vector2 p, float r) { float mn = Agni.Import.AgniIndia.H(p.x, p.y); for (int i = 0; i < 8; i++) { float a = i * 0.785f; mn = Mathf.Min(mn, Agni.Import.AgniIndia.H(p.x + Mathf.Cos(a) * r * 0.8f, p.y + Mathf.Sin(a) * r * 0.8f)); } return mn - 0.05f; }
        static bool FreeExceptLandmarks(Vector2 p, float r) { for (int i = Agni.Import.AgniLandmarks.Footprints.Count; i < Footprints.Count; i++) if ((Footprints[i].c - p).magnitude < Footprints[i].r + r) return false; return true; }
        static bool Free(Vector2 p, float r) { foreach (var f in Footprints) if ((f.c - p).magnitude < f.r + r) return false; return true; }
        public static float FootR(BType k) { switch (k) { case BType.GolJhopdi: return 3.6f; case BType.KutchaGhar: return 4.6f; case BType.PakkaGhar: return 5.4f; case BType.ChhatGhar: return 6.2f; case BType.DoManzila: return 7.2f; case BType.TinShed: return 4.8f; default: return 10f; } }
        public static string TypeName(BType k) { switch (k) { case BType.GolJhopdi: return "Gol Jhopdi"; case BType.KutchaGhar: return "Kutcha Ghar"; case BType.PakkaGhar: return "Pakka Ghar"; case BType.ChhatGhar: return "Chhat Ghar"; case BType.DoManzila: return "Do-Manzila"; case BType.TinShed: return "Tin Shed"; default: return "Godown"; } }

        static Material Fallback(VMat m)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); var mat = new Material(sh) { name = "V_" + m };
            mat.SetColor("_BaseColor", m == VMat.Thatch ? new Color(0.7f, 0.58f, 0.35f) : m == VMat.Brick ? new Color(0.6f, 0.3f, 0.22f) : m == VMat.Dark ? new Color(0.08f, 0.07f, 0.06f) : new Color(0.85f, 0.8f, 0.7f)); return mat;
        }

        // ================================================================== mesh builder
        public class MB
        {
            public class Sub { public List<Vector3> v = new List<Vector3>(); public List<Vector3> n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); }
            public readonly Dictionary<VMat, Sub>[] lod = { new Dictionary<VMat, Sub>(), new Dictionary<VMat, Sub>(), new Dictionary<VMat, Sub>() };
            public readonly List<Vector3> cv = new List<Vector3>(); public readonly List<int> ct = new List<int>();
            Sub S(int l, VMat m) { if (!lod[l].TryGetValue(m, out var s)) lod[l][m] = s = new Sub(); return s; }

            /// lods: bit0 = LOD0, bit1 = LOD1, bit2 = LOD2.  col: add to the collision mesh.
            public void Box(Vector3 c, Vector3 size, Quaternion rot, VMat m, int lods = 3, bool col = true)
            {
                Vector3 h = size * 0.5f;
                Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };
                for (int f = 0; f < 6; f++)
                {
                    int a = f / 2; float sgn = (f % 2 == 0) ? 1 : -1;
                    Vector3 N = ax[a] * sgn, T = a == 0 ? Vector3.forward : Vector3.right, B = a == 1 ? Vector3.forward : Vector3.up;
                    float hn = a == 0 ? h.x : a == 1 ? h.y : h.z, ht = a == 0 ? h.z : h.x, hb = a == 1 ? h.z : h.y;
                    var p0 = c + rot * (N * hn - T * ht - B * hb); var p1 = c + rot * (N * hn + T * ht - B * hb);
                    var p2 = c + rot * (N * hn + T * ht + B * hb); var p3 = c + rot * (N * hn - T * ht + B * hb);
                    Quad(p0, p1, p2, p3, rot * N, rot * T, rot * B, m, lods);
                    if (col) ColQuad(p0, p1, p2, p3, rot * N);
                }
            }
            public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, Vector3 T, Vector3 B, VMat m, int lods)
            {
                float tile = Tile[(int)m];
                for (int l = 0; l < 3; l++)
                {
                    if ((lods & (1 << l)) == 0) continue; var s = S(l, m); int i = s.v.Count;
                    foreach (var p in new[] { p0, p1, p2, p3 }) { s.v.Add(p); s.n.Add(n); s.uv.Add(new Vector2(Vector3.Dot(p, T), Vector3.Dot(p, B)) / tile); }
                    bool keep = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0; // Unity: cross(b-a,c-a) = front normal
                    if (keep) { s.t.Add(i); s.t.Add(i + 1); s.t.Add(i + 2); s.t.Add(i); s.t.Add(i + 2); s.t.Add(i + 3); }
                    else { s.t.Add(i); s.t.Add(i + 2); s.t.Add(i + 1); s.t.Add(i); s.t.Add(i + 3); s.t.Add(i + 2); }
                }
            }
            public void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n, Vector3 T, Vector3 B, VMat m, int lods, bool col)
            {
                float tile = Tile[(int)m];
                for (int l = 0; l < 3; l++)
                {
                    if ((lods & (1 << l)) == 0) continue; var s = S(l, m); int i = s.v.Count;
                    foreach (var p in new[] { p0, p1, p2 }) { s.v.Add(p); s.n.Add(n); s.uv.Add(new Vector2(Vector3.Dot(p, T), Vector3.Dot(p, B)) / tile); }
                    if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0) { s.t.Add(i); s.t.Add(i + 1); s.t.Add(i + 2); } else { s.t.Add(i); s.t.Add(i + 2); s.t.Add(i + 1); }
                }
                if (col) { int j = cv.Count; cv.Add(p0); cv.Add(p1); cv.Add(p2); if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0) { ct.Add(j); ct.Add(j + 1); ct.Add(j + 2); } else { ct.Add(j); ct.Add(j + 2); ct.Add(j + 1); } }
            }
            void ColQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int i = cv.Count; cv.Add(p0); cv.Add(p1); cv.Add(p2); cv.Add(p3);
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0) { ct.Add(i); ct.Add(i + 1); ct.Add(i + 2); ct.Add(i); ct.Add(i + 2); ct.Add(i + 3); }
                else { ct.Add(i); ct.Add(i + 2); ct.Add(i + 1); ct.Add(i); ct.Add(i + 3); ct.Add(i + 2); }
            }
            /// Invisible collision-only box (ramps under stairs, etc.)
            public void ColBox(Vector3 c, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f; Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };
                for (int f = 0; f < 6; f++)
                {
                    int a = f / 2; float sgn = (f % 2 == 0) ? 1 : -1;
                    Vector3 N = ax[a] * sgn, T = a == 0 ? Vector3.forward : Vector3.right, B = a == 1 ? Vector3.forward : Vector3.up;
                    float hn = a == 0 ? h.x : a == 1 ? h.y : h.z, ht = a == 0 ? h.z : h.x, hb = a == 1 ? h.z : h.y;
                    ColQuad(c + rot * (N * hn - T * ht - B * hb), c + rot * (N * hn + T * ht - B * hb), c + rot * (N * hn + T * ht + B * hb), c + rot * (N * hn - T * ht + B * hb), rot * N);
                }
            }
            /// Smooth cone shell (thatch roof): base ring radius r at y0, apex at y1, overhang thickness th; closed (underside cone) for collision.
            public void Cone(Vector3 c, float r, float y0, float y1, float th, int seg, VMat m, int lods, bool col)
            {
                float tile = Tile[(int)m]; float slant = Mathf.Sqrt(r * r + (y1 - y0) * (y1 - y0));
                for (int l = 0; l < 3; l++)
                {
                    if ((lods & (1 << l)) == 0) continue; int sg = l == 2 ? 8 : l == 1 ? seg * 2 / 3 : seg;
                    var s = S(l, m);
                    for (int side = 0; side < 2; side++)
                    {
                        float yb = side == 0 ? y0 : y0 - th, ya = side == 0 ? y1 : y1 - th * 1.6f, rr = side == 0 ? r : r - th * 0.3f;
                        int start = s.v.Count;
                        for (int i = 0; i <= sg; i++)
                        {
                            float a = i / (float)sg * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            var nrm = (dir * (y1 - y0) + Vector3.up * r).normalized; if (side == 1) nrm = -nrm;
                            for (int k = 0; k < 3; k++)
                            {
                                float f = k == 0 ? 0f : k == 1 ? 0.7f : 0.97f;
                                s.v.Add(c + dir * rr * (1 - f) + Vector3.up * Mathf.Lerp(yb, ya, f)); s.n.Add(k == 2 && side == 0 ? Vector3.up : nrm);
                                s.uv.Add(new Vector2(a * r / tile, f * slant / tile));
                            }
                        }
                        for (int i = 0; i < sg; i++) for (int k = 0; k < 2; k++)
                            {
                                int a0 = start + i * 3 + k, a1 = a0 + 1, b0 = a0 + 3, b1 = b0 + 1;
                                if (side == 0) { s.t.Add(a0); s.t.Add(a1); s.t.Add(b0); s.t.Add(b0); s.t.Add(a1); s.t.Add(b1); }
                                else { s.t.Add(a0); s.t.Add(b0); s.t.Add(a1); s.t.Add(b0); s.t.Add(b1); s.t.Add(a1); }
                            }
                    }
                    // rim band between outer and inner shells
                    int rs = s.v.Count;
                    for (int i = 0; i <= sg; i++)
                    {
                        float a = i / (float)sg * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        s.v.Add(c + dir * r + Vector3.up * y0); s.v.Add(c + dir * (r - th * 0.3f) + Vector3.up * (y0 - th));
                        s.n.Add(dir); s.n.Add(dir); s.uv.Add(new Vector2(a * r / tile, 0)); s.uv.Add(new Vector2(a * r / tile, th / tile));
                    }
                    for (int i = 0; i < sg; i++) { int a0 = rs + i * 2; s.t.Add(a0); s.t.Add(a0 + 2); s.t.Add(a0 + 1); s.t.Add(a0 + 1); s.t.Add(a0 + 2); s.t.Add(a0 + 3); }
                }
                if (col)
                {
                    int sg = 12;
                    for (int i = 0; i < sg; i++)
                    {
                        float a0 = i / (float)sg * Mathf.PI * 2, a1 = (i + 1) / (float)sg * Mathf.PI * 2;
                        Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                        var top = c + Vector3.up * y1; var bot = c + Vector3.up * (y1 - th * 1.6f);
                        var o0 = c + d0 * r + Vector3.up * y0; var o1 = c + d1 * r + Vector3.up * y0;
                        var u0 = c + d0 * r + Vector3.up * (y0 - th); var u1 = c + d1 * r + Vector3.up * (y0 - th);
                        AddColTri(top, o1, o0, Vector3.up); AddColTri(bot, u0, u1, Vector3.down);
                        AddColTri(o0, o1, u1, d0); AddColTri(o0, u1, u0, d0);
                    }
                }
            }
            void AddColTri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
            {
                int j = cv.Count; cv.Add(a); cv.Add(b); cv.Add(c);
                var n = Vector3.Cross(b - a, c - a); bool outward = Vector3.Dot(n, hint) > 0;
                if (outward) { ct.Add(j); ct.Add(j + 1); ct.Add(j + 2); } else { ct.Add(j); ct.Add(j + 2); ct.Add(j + 1); }
            }
            /// Triangular prism (gable end): triangle a,b,apex in a vertical plane, extruded by thickness along its normal.
            public void Gable(Vector3 a, Vector3 b, Vector3 apex, float th, VMat m, int lods, bool col)
            {
                var n = Vector3.Cross(b - a, apex - a).normalized; var o = n * th * 0.5f; var T = (b - a).normalized;
                Tri(a + o, b + o, apex + o, n, T, Vector3.up, m, lods, col); Tri(a - o, b - o, apex - o, -n, T, Vector3.up, m, lods, col);
                void Side(Vector3 p, Vector3 q) { var d = (q - p).normalized; var sn = Vector3.Cross(d, n).normalized; var cen = (a + b + apex) / 3f; if (Vector3.Dot(sn, (p + q) * 0.5f - cen) < 0) sn = -sn; Quad(p - o, q - o, q + o, p + o, sn, d, n, m, lods); if (col) { int i = cv.Count; cv.Add(p - o); cv.Add(q - o); cv.Add(q + o); cv.Add(p + o); var cn = Vector3.Cross(cv[i + 1] - cv[i], cv[i + 2] - cv[i]); if (Vector3.Dot(cn, sn) > 0) { ct.Add(i); ct.Add(i + 1); ct.Add(i + 2); ct.Add(i); ct.Add(i + 2); ct.Add(i + 3); } else { ct.Add(i); ct.Add(i + 2); ct.Add(i + 1); ct.Add(i); ct.Add(i + 3); ct.Add(i + 2); } } }
                Side(a, b); Side(b, apex); Side(apex, a);
            }

            public Mesh ToMesh(int l, out Material[] used, string name)
            {
                var m = new Mesh { name = name }; var V = new List<Vector3>(); var Nn = new List<Vector3>(); var U = new List<Vector2>(); var subs = new List<List<int>>(); var ms = new List<Material>();
                foreach (var kv in lod[l])
                {
                    int off = V.Count; V.AddRange(kv.Value.v); Nn.AddRange(kv.Value.n); U.AddRange(kv.Value.uv);
                    var tt = new List<int>(kv.Value.t.Count); foreach (var x in kv.Value.t) tt.Add(x + off); subs.Add(tt); ms.Add(mats[(int)kv.Key]);
                }
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(Nn); m.SetUVs(0, U); m.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i, false);
                m.RecalculateBounds(); m.RecalculateTangents(); m.UploadMeshData(false); used = ms.ToArray(); return m;
            }
            public Mesh ColMesh(string name) { var m = new Mesh { name = name }; if (cv.Count > 65000) m.indexFormat = IndexFormat.UInt32; m.SetVertices(cv); m.SetTriangles(ct, 0); m.RecalculateBounds(); return m; }
        }

        // ================================================================== building blocks
        struct Op { public float at, w, y0, y1; public bool door; public Op(float at, float w, float y0, float y1, bool door = false) { this.at = at; this.w = w; this.y0 = y0; this.y1 = y1; this.door = door; } }
        static Op Door(float at, float w = 1.2f, float h = 2.15f) => new Op(at, w, 0, h, true);
        static Op Win(float at, float w = 1.1f, float y0 = 1.0f, float y1 = 2.05f) => new Op(at, w, y0, y1);

        /// A straight wall from a to e (building-local, on the floor level y0) with door/window holes; optional dado band.
        static void Wall(MB b, Vector3 a, Vector3 e, float y0, float h, float th, VMat m, VMat band, float bandH, Op[] ops, bool frames = true, VMat frameM = VMat.Wood)
        {
            a.y = 0; e.y = 0; // a/e are given on the floor; heights come from y0
            var d = e - a; float L = d.magnitude; d /= L; var rot = Quaternion.FromToRotation(Vector3.right, d); var nOut = Vector3.Cross(Vector3.up, d); // right-hand side
            void Piece(float s0, float s1, float yb, float yt)
            {
                if (s1 - s0 < 0.01f || yt - yb < 0.01f) return;
                if (bandH > 0 && yb < bandH && yt > bandH) { Piece2(s0, s1, yb, bandH, band); Piece2(s0, s1, bandH, yt, m); }
                else Piece2(s0, s1, yb, yt, yt <= bandH + 0.001f && bandH > 0 ? band : m);
            }
            void Piece2(float s0, float s1, float yb, float yt, VMat mm) => b.Box(a + d * ((s0 + s1) * 0.5f) + Vector3.up * (y0 + (yb + yt) * 0.5f), new Vector3(s1 - s0, yt - yb, th), rot, mm, 3, true);
            var list = new List<Op>(ops ?? new Op[0]); list.Sort((p, q) => p.at.CompareTo(q.at));
            float cur = 0;
            foreach (var o in list)
            {
                float l0 = o.at - o.w / 2, l1 = o.at + o.w / 2;
                Piece(cur, l0, 0, h); Piece(l0, l1, 0, o.y0); Piece(l0, l1, o.y1, h); cur = l1;
                if (!frames) continue;
                var c = a + d * o.at + Vector3.up * y0; float ft = 0.09f, fd = th + 0.04f;
                // frame (LOD0) — top + sides; window sill sticks out a little
                b.Box(c + Vector3.up * (o.y1 + ft / 2), new Vector3(o.w + ft * 2, ft, fd), rot, frameM, 1, false);
                b.Box(c - d * (o.w / 2 + ft / 2) + Vector3.up * ((o.y0 + o.y1) / 2), new Vector3(ft, o.y1 - o.y0, fd), rot, frameM, 1, false);
                b.Box(c + d * (o.w / 2 + ft / 2) + Vector3.up * ((o.y0 + o.y1) / 2), new Vector3(ft, o.y1 - o.y0, fd), rot, frameM, 1, false);
                if (o.door)
                {   // door leaf swung fully open against the outside wall (hinge on the left)
                    var hinge = c - d * (o.w / 2) + nOut * (th / 2 + 0.04f);
                    var u = (-d * Mathf.Cos(10 * Mathf.Deg2Rad) + nOut * Mathf.Sin(10 * Mathf.Deg2Rad)).normalized;
                    b.Box(hinge + u * (o.w * 0.48f) + Vector3.up * (o.y1 * 0.5f), new Vector3(o.w * 0.95f, o.y1 - 0.06f, 0.05f), Quaternion.FromToRotation(Vector3.right, u), frameM, 1, false);
                }
                else
                {
                    b.Box(c + Vector3.up * (o.y0 - ft / 2) + nOut * 0.05f, new Vector3(o.w + 0.2f, ft, fd + 0.1f), rot, frameM, 1, false);
                    // two shutters open outwards, almost flat against the wall
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var hinge = c + d * (sgn * o.w / 2) + nOut * (th / 2 + 0.03f) + Vector3.up * ((o.y0 + o.y1) / 2);
                        var u = (d * (sgn * Mathf.Cos(25 * Mathf.Deg2Rad)) + nOut * Mathf.Sin(25 * Mathf.Deg2Rad)).normalized;
                        b.Box(hinge + u * (o.w * 0.25f), new Vector3(o.w * 0.5f, o.y1 - o.y0 - 0.06f, 0.04f), Quaternion.FromToRotation(Vector3.right, u), frameM, 1, false);
                    }
                }
            }
            Piece(cur, L, 0, h);
        }
        static void Slab(MB b, Vector3 c, Vector3 size, VMat m, int lods = 3, bool col = true) => b.Box(c, size, Quaternion.identity, m, lods, col);
        /// Floor slab with a rectangular hole (stairwell): x0..x1, z0..z1 in local metres.
        static void SlabHole(MB b, float w, float d, float y, float th, float hx0, float hx1, float hz0, float hz1, VMat m)
        {
            float X0 = -w / 2, X1 = w / 2, Z0 = -d / 2, Z1 = d / 2;
            void R(float x0, float x1, float z0, float z1) { if (x1 - x0 > 0.01f && z1 - z0 > 0.01f) Slab(b, new Vector3((x0 + x1) / 2, y - th / 2, (z0 + z1) / 2), new Vector3(x1 - x0, th, z1 - z0), m); }
            R(X0, hx0, Z0, Z1); R(hx1, X1, Z0, Z1); R(hx0, hx1, Z0, hz0); R(hx0, hx1, hz1, Z1);
        }
        /// Solid masonry stairs (visual steps) + an invisible ramp so the fighter glides up.
        static void Stairs(MB b, Vector3 start, Vector3 dir, float width, float rise, float run, VMat m, bool solid)
        {
            int n = Mathf.Max(3, Mathf.RoundToInt(rise / 0.19f)); float rs = rise / n, rn = run / n; var rot = Quaternion.LookRotation(dir, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                float top = (i + 1) * rs; float bot = solid ? 0 : top - 0.12f;
                b.Box(start + dir * ((i + 0.5f) * rn) + Vector3.up * ((top + bot) / 2), new Vector3(width, top - bot, rn + 0.01f), rot, m, solid ? 3 : 1, false);
            }
            if (!solid) b.Box(start + dir * (run / 2) + Vector3.up * (rise / 2 - 0.12f), new Vector3(width * 0.9f, 0.08f, Mathf.Sqrt(run * run + rise * rise)), rot * Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0), m, 2, false);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(run * run + rise * rise) + 0.3f;
            b.ColBox(start + dir * (run / 2) + Vector3.up * (rise / 2 - 0.06f), new Vector3(width, 0.12f, len), rot * Quaternion.Euler(-ang, 0, 0));
            if (solid) b.ColBox(start + dir * (run / 2) + Vector3.up * (rise / 4), new Vector3(width, rise / 2, run * 0.5f), rot); // fill under the ramp so nobody walks into the stairs
        }
        static void Cyl(MB b, Vector3 c, float r, float y0, float y1, int seg, VMat m, int lods, bool col)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2; float am = (a0 + a1) / 2;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), dm = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
                var T = (d1 - d0).normalized;
                b.Quad(c + d0 * r + Vector3.up * y0, c + d1 * r + Vector3.up * y0, c + d1 * r + Vector3.up * y1, c + d0 * r + Vector3.up * y1, dm, T, Vector3.up, m, lods);
                b.Tri(c + Vector3.up * y1, c + d0 * r + Vector3.up * y1, c + d1 * r + Vector3.up * y1, Vector3.up, Vector3.right, Vector3.forward, m, lods, false);
            }
            if (col) b.ColBox(c + Vector3.up * ((y0 + y1) / 2), new Vector3(r * 1.6f, y1 - y0, r * 1.6f), Quaternion.identity);
        }
        static void Spot(List<(Vector3, byte)> s, float x, float y, float z, byte tier) => s.Add((new Vector3(x, y + 0.03f, z), tier));

        // ---- props (LOD0 only)
        static void Charpai(MB b, Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            foreach (var sx in new[] { -0.88f, 0.88f }) foreach (var sz in new[] { -0.42f, 0.42f }) b.Box(p + r * new Vector3(sx, 0.21f, sz), new Vector3(0.08f, 0.42f, 0.08f), r, VMat.Wood, 1, false);
            b.Box(p + r * new Vector3(0, 0.4f, 0.42f), new Vector3(1.86f, 0.07f, 0.07f), r, VMat.Wood, 1, false); b.Box(p + r * new Vector3(0, 0.4f, -0.42f), new Vector3(1.86f, 0.07f, 0.07f), r, VMat.Wood, 1, false);
            b.Box(p + r * new Vector3(0, 0.41f, 0), new Vector3(1.78f, 0.04f, 0.8f), r, VMat.Cloth, 1, false);
            b.ColBox(p + r * new Vector3(0, 0.22f, 0), new Vector3(1.9f, 0.44f, 0.9f), r);
        }
        static void Table(MB b, Vector3 p, float yaw, float w = 1.2f, float d = 0.7f)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            b.Box(p + r * new Vector3(0, 0.76f, 0), new Vector3(w, 0.05f, d), r, VMat.Wood, 1, false);
            foreach (var sx in new[] { -w / 2 + 0.06f, w / 2 - 0.06f }) foreach (var sz in new[] { -d / 2 + 0.06f, d / 2 - 0.06f }) b.Box(p + r * new Vector3(sx, 0.37f, sz), new Vector3(0.06f, 0.74f, 0.06f), r, VMat.Wood, 1, false);
            b.ColBox(p + r * new Vector3(0, 0.39f, 0), new Vector3(w, 0.78f, d), r);
        }
        static void Crate(MB b, Vector3 p, float yaw, float s = 1f) { var r = Quaternion.Euler(0, yaw, 0); b.Box(p + Vector3.up * (s / 2), Vector3.one * s, r, VMat.Wood, 3, true); }
        static void Sack(MB b, Vector3 p, float yaw) { var r = Quaternion.Euler(0, yaw, 0); b.Box(p + Vector3.up * 0.17f, new Vector3(0.95f, 0.34f, 0.58f), r, VMat.Cloth, 1, false); b.Box(p + Vector3.up * 0.42f + r * new Vector3(0.05f, 0, 0), new Vector3(0.9f, 0.18f, 0.5f), r * Quaternion.Euler(0, 6, 0), VMat.Cloth, 1, false); b.ColBox(p + Vector3.up * 0.25f, new Vector3(0.95f, 0.5f, 0.58f), r); }
        static void Almirah(MB b, Vector3 p, float yaw) { var r = Quaternion.Euler(0, yaw, 0); b.Box(p + Vector3.up * 0.95f, new Vector3(1.0f, 1.9f, 0.5f), r, VMat.Tin, 1, true); b.Box(p + Vector3.up * 0.95f + r * new Vector3(0, 0, -0.26f), new Vector3(0.02f, 1.8f, 0.02f), r, VMat.Dark, 1, false); }
        static void Matka(MB b, Vector3 p) { Cyl(b, p, 0.2f, 0, 0.32f, 10, VMat.Mud, 1, false); Cyl(b, p, 0.13f, 0.32f, 0.42f, 10, VMat.Mud, 1, false); }
        static void Hay(MB b, Vector3 p, float yaw) { var r = Quaternion.Euler(0, yaw, 0); b.Box(p + Vector3.up * 0.4f, new Vector3(1.3f, 0.8f, 0.75f), r, VMat.Thatch, 3, true); }

        /// Four walls of a rectangular room (outer edges x0..x1, z0..z1). Front/back run the full width; sides fit between them.
        /// Opening 'at' is measured from the corner the wall starts at: front from x0, right from z0, back from x1, left from z1.
        static void Room4(MB b, float x0, float x1, float z0, float z1, float y0, float h, float th, VMat m, VMat band, float bandH, Op[] F, Op[] Rt, Op[] Bk, Op[] Lf, bool frames = true)
        {
            float t2 = th / 2;
            Op[] Shift(Op[] o) { if (o == null) return null; var r = new Op[o.Length]; for (int i = 0; i < o.Length; i++) { r[i] = o[i]; r[i].at -= th; } return r; }
            Wall(b, new Vector3(x0, y0, z0 + t2), new Vector3(x1, y0, z0 + t2), y0, h, th, m, band, bandH, F, frames);
            Wall(b, new Vector3(x1 - t2, y0, z0 + th), new Vector3(x1 - t2, y0, z1 - th), y0, h, th, m, band, bandH, Shift(Rt), frames);
            Wall(b, new Vector3(x1, y0, z1 - t2), new Vector3(x0, y0, z1 - t2), y0, h, th, m, band, bandH, Bk, frames);
            Wall(b, new Vector3(x0 + t2, y0, z1 - th), new Vector3(x0 + t2, y0, z0 + th), y0, h, th, m, band, bandH, Shift(Lf), frames);
        }
        static void GableRoof(MB b, float w, float d, float eaveY, float ridgeY, float over, float th, VMat m, VMat gableM, float wallTop, float wallTh)
        {
            float dz = d / 2 + over, zW = d / 2 - wallTh * 0.5f;
            eaveY = wallTop + 0.02f - (ridgeY - wallTop) / zW * (dz - zW); // the slab rests on the front/back wall tops (no light gap)
            float dy = ridgeY - eaveY, len = Mathf.Sqrt(dz * dz + dy * dy) + 0.12f;
            for (int s = -1; s <= 1; s += 2)
            {
                var dir = new Vector3(0, dy, -s * dz).normalized; // eave (z = s*dz) -> ridge
                var c = new Vector3(0, (eaveY + ridgeY) / 2, s * dz / 2) + Vector3.up * (th / 2);
                b.Box(c, new Vector3(w + 2 * over, th, len), Quaternion.LookRotation(dir, Vector3.up), m, 7, true);
            }
            b.Box(new Vector3(0, ridgeY + th * 0.9f, 0), new Vector3(w + 2 * over + 0.05f, th * 0.9f, th * 1.6f), Quaternion.identity, m, 3, false);
            for (int s = -1; s <= 1; s += 2)
            {   // gable end = a strip from the wall top up to where the roof slab passes the wall line, then a triangle to the ridge
                float x = s * (w / 2 - wallTh / 2), zE = d / 2 - wallTh * 0.5f;
                float yW = eaveY + (dz - zE) / dz * dy - 0.03f;
                if (yW > wallTop + 0.01f) b.Box(new Vector3(x, (wallTop + yW) / 2, 0), new Vector3(wallTh, yW - wallTop, 2 * zE), Quaternion.identity, gableM, 3, true);
                b.Gable(new Vector3(x, Mathf.Max(yW, wallTop), -zE), new Vector3(x, Mathf.Max(yW, wallTop), zE), new Vector3(x, ridgeY - 0.05f, 0), wallTh, gableM, 3, true);
            }
        }

    // ================================================================== the buildings
        static void Building(BType k, Transform parent, Vector3 pos, float yaw, System.Random rnd)
        {
            var go = new GameObject(TypeName(k)); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = new MB(); var sp = new List<(Vector3, byte)>(); float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            VMat plaster = (VMat)rnd.Next(0, 4);
            switch (k)
            {
                case BType.GolJhopdi:
                {
                    float Rr = R(2.5f, 2.9f), H = 2.3f, th = 0.3f, FY = 0.18f; int N = 16; float step = 360f / N;
                    Cyl(b, Vector3.zero, Rr + 0.3f, 0, FY, 18, VMat.Mud, 3, false);
                    b.ColBox(new Vector3(0, FY / 2, 0), new Vector3(2 * Rr + 0.3f, FY, 2 * Rr + 0.3f), Quaternion.identity);
                    b.ColBox(new Vector3(0, FY / 2, 0), new Vector3(2 * Rr + 0.3f, FY, 2 * Rr + 0.3f), Quaternion.Euler(0, 45, 0));
                    float seg = 2 * Mathf.PI * Rr / N * 1.12f;
                    for (int i = 1; i < N; i++)
                    {
                        float a = (-90f + i * step) * Mathf.Deg2Rad; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var tan = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                        var rot = Quaternion.FromToRotation(Vector3.right, tan); var c = dir * Rr;
                        bool win = i == 4 || i == 12;
                        void P(float y0, float y1) { if (y0 < 0.55f && y1 > 0.55f) { b.Box(c + Vector3.up * (FY + (y0 + 0.55f) / 2), new Vector3(seg, 0.55f - y0, th), rot, VMat.MudBase, 3, true); y0 = 0.55f; } b.Box(c + Vector3.up * (FY + (y0 + y1) / 2), new Vector3(seg, y1 - y0, th), rot, y1 <= 0.551f ? VMat.MudBase : VMat.Mud, 3, true); }
                        if (win) { P(0, 0.95f); P(1.65f, H); b.Box(c + Vector3.up * (FY + 0.95f) + dir * 0.06f, new Vector3(seg * 0.8f, 0.07f, th + 0.12f), rot, VMat.Wood, 1, false); }
                        else P(0, H);
                    }
                    // lintel over the door gap + wooden door posts
                    var dc = new Vector3(0, 0, -Rr);
                    b.Box(dc + Vector3.up * (FY + (1.95f + H) / 2), new Vector3(seg * 1.15f, H - 1.95f, th), Quaternion.identity, VMat.Mud, 3, true);
                    b.Box(dc + new Vector3(-seg * 0.5f, FY + 0.98f, 0), new Vector3(0.1f, 1.95f, th + 0.06f), Quaternion.identity, VMat.Wood, 1, false);
                    b.Box(dc + new Vector3(seg * 0.5f, FY + 0.98f, 0), new Vector3(0.1f, 1.95f, th + 0.06f), Quaternion.identity, VMat.Wood, 1, false);
                    b.Box(dc + new Vector3(0, FY + 1.98f, 0), new Vector3(seg + 0.1f, 0.1f, th + 0.06f), Quaternion.identity, VMat.Wood, 1, false);
                    Cyl(b, Vector3.zero, Rr, 0, FY + H, 8, VMat.Mud, 4, false);
                    float y0r = FY + H - 0.12f, y1r = FY + H + R(2.2f, 2.6f);
                    b.Cone(Vector3.zero, Rr + 0.95f, y0r, y1r, 0.3f, 22, VMat.Thatch, 7, true);
                    Cyl(b, Vector3.up * 0, 0.22f, y1r - 0.25f, y1r + 0.22f, 8, VMat.Thatch, 3, false);
                    Charpai(b, new Vector3(-0.9f, FY, 0.75f), 70); Matka(b, new Vector3(1.5f, FY, 1.5f)); Matka(b, new Vector3(1.95f, FY, 0.95f)); Sack(b, new Vector3(1.2f, FY, -1.3f), 40);
                    Spot(sp, 0.7f, FY, 0.1f, 0); Spot(sp, -1.3f, FY, -1.1f, 0); Spot(sp, 0.2f, FY, 1.9f, 0);
                    break;
                }
                case BType.KutchaGhar:
                {
                    float w = R(5.8f, 6.6f), d = R(4.4f, 4.9f), H = 2.45f, th = 0.3f, FY = 0.2f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.4f, FY, d + 0.4f), VMat.Mud);
                    Room4(b, -w / 2, w / 2, -d / 2, d / 2, FY, H, th, VMat.Mud, VMat.MudBase, 0.6f, new[] { Door(w * 0.36f, 1.2f, 2.05f), Win(w * 0.78f, 0.9f, 1.0f, 1.8f) }, new[] { Win(d / 2, 0.9f, 1.0f, 1.8f) }, new[] { Win(w / 2, 0.8f, 1.2f, 1.85f) }, null);
                    b.Box(new Vector3(0, FY + H / 2, 0), new Vector3(w, H, d), Quaternion.identity, VMat.Mud, 4, false);
                    GableRoof(b, w, d, FY + H - 0.3f, FY + H + 1.75f, 0.6f, 0.26f, VMat.Thatch, VMat.Mud, FY + H, th);
                    Charpai(b, new Vector3(-w / 2 + 1.35f, FY, 0.8f), 0); Sack(b, new Vector3(w / 2 - 0.9f, FY, d / 2 - 0.75f), 10); Matka(b, new Vector3(w / 2 - 0.5f, FY, -0.2f));
                    Spot(sp, 0.9f, FY, 0.5f, 0); Spot(sp, -w / 2 + 1.2f, FY, -d / 2 + 0.9f, 0); Spot(sp, w / 2 - 1.0f, FY, -d / 2 + 0.9f, 0);
                    break;
                }
                case BType.PakkaGhar:
                {
                    float w = 8.4f, d = 6.6f, H = 3.0f, th = 0.3f, FY = 0.35f, px = 0.6f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.6f, FY, d + 0.6f), VMat.Concrete);
                    Slab(b, new Vector3(-w / 2 + 2.6f, 0.09f, -d / 2 - 0.55f), new Vector3(1.8f, 0.18f, 0.5f), VMat.Concrete);
                    Room4(b, -w / 2, w / 2, -d / 2, d / 2, FY, H, th, VMat.Brick, VMat.Brick, 0, new[] { Door(2.6f), Win(6.3f) }, new[] { Win(d / 2) }, new[] { Win(2.2f), Win(6.0f) }, new[] { Win(d / 2) });
                    Wall(b, new Vector3(px, FY, d / 2 - th), new Vector3(px, FY, -d / 2 + th), FY, H, 0.22f, VMat.Lime, VMat.Lime, 0, new[] { Door(4.2f, 1.1f, 2.1f) }, false);
                    b.Box(new Vector3(0, FY + H / 2, 0), new Vector3(w, H, d), Quaternion.identity, VMat.Brick, 4, false);
                    Slab(b, new Vector3(-w / 2 + 2.6f, FY + 2.55f, -d / 2 - 0.25f), new Vector3(1.9f, 0.08f, 0.55f), VMat.Concrete, 3, false);
                    Slab(b, new Vector3(-w / 2 + 6.3f, FY + 2.35f, -d / 2 - 0.25f), new Vector3(1.6f, 0.08f, 0.55f), VMat.Concrete, 3, false);
                    GableRoof(b, w, d, FY + H - 0.25f, FY + H + 1.55f, 0.5f, 0.2f, VMat.Tile, VMat.Brick, FY + H, th);
                    Charpai(b, new Vector3(-w / 2 + 1.4f, FY, 1.4f), 0); Almirah(b, new Vector3(-w / 2 + 0.6f, FY, -1.3f), 90);
                    Table(b, new Vector3(w / 2 - 1.2f, FY, d / 2 - 0.8f), 0); Crate(b, new Vector3(w / 2 - 0.8f, FY, -d / 2 + 0.9f), 10, 0.8f); Matka(b, new Vector3(px + 0.5f, FY, d / 2 - 0.5f));
                    Spot(sp, -1.3f, FY, -1.6f, 1); Spot(sp, -2.8f, FY, 2.2f, 1); Spot(sp, w / 2 - 1.2f, FY + 0.79f, d / 2 - 0.8f, 1); Spot(sp, 2.2f, FY, -0.5f, 1);
                    break;
                }
                case BType.ChhatGhar:
                {
                    float w = 9f, d = 7f, H = 3.1f, th = 0.3f, FY = 0.3f, rt = FY + H + 0.2f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.4f, FY, d + 0.4f), VMat.Concrete);
                    Room4(b, -w / 2, w / 2, -d / 2, d / 2, FY, H, th, plaster, VMat.Band, 0.9f, new[] { Win(1.4f), Door(3.4f), Win(6.6f) }, null, new[] { Win(2.4f), Win(6.4f) }, new[] { Win(1.8f), Win(5.0f) });
                    Wall(b, new Vector3(-w / 2 + th, FY, 0.6f), new Vector3(w / 2 - th, FY, 0.6f), FY, H, 0.22f, VMat.Lime, VMat.Lime, 0, new[] { Door(4.6f, 1.1f, 2.1f) }, false);
                    b.Box(new Vector3(0, FY + H / 2, 0), new Vector3(w, H, d), Quaternion.identity, plaster, 4, false);
                    Slab(b, new Vector3(0, rt - 0.1f, 0), new Vector3(w + 0.2f, 0.2f, d + 0.2f), VMat.Concrete, 7);
                    // parapet with a gap where the outside stairs arrive (right side, back end)
                    Room4(b, -w / 2 - 0.1f, w / 2 + 0.1f, -d / 2 - 0.1f, d / 2 + 0.1f, rt, 0.9f, 0.18f, plaster, plaster, 0, null, new[] { new Op(6.45f, 1.3f, 0, 0.95f) }, null, null, false);
                    Stairs(b, new Vector3(w / 2 + 0.72f, 0, -d / 2 + 0.15f), Vector3.forward, 1.1f, rt, 6.0f, VMat.Concrete, true);
                    Slab(b, new Vector3(w / 2 + 0.6f, rt - 0.1f, d / 2 - 0.4f), new Vector3(1.4f, 0.2f, 1.0f), VMat.Concrete);
                    Slab(b, new Vector3(-w / 2 + 3.4f, FY + 2.5f, -d / 2 - 0.25f), new Vector3(1.9f, 0.08f, 0.55f), VMat.Concrete, 3, false);
                    Cyl(b, new Vector3(-w / 2 + 1.3f, 0, d / 2 - 1.3f), 0.62f, rt, rt + 1.15f, 14, VMat.Dark, 3, true);
                    Table(b, new Vector3(-w / 2 + 1.2f, FY, -d / 2 + 1.0f), 90); Charpai(b, new Vector3(1.5f, FY, d / 2 - 1.0f), 0); Almirah(b, new Vector3(w / 2 - 0.6f, FY, 2.0f), -90); Crate(b, new Vector3(w / 2 - 0.9f, FY, -d / 2 + 0.9f), 0, 0.8f);
                    Spot(sp, -w / 2 + 1.2f, FY + 0.79f, -d / 2 + 1.0f, 1); Spot(sp, 1.5f, FY, -1.4f, 1); Spot(sp, -2.6f, FY, 2.4f, 1); Spot(sp, 3.0f, FY, 1.6f, 1);
                    Spot(sp, -1.5f, rt, -1.5f, 2); Spot(sp, 2.6f, rt, 1.2f, 1);
                    break;
                }
                case BType.DoManzila:
                {
                    float w = 10f, d = 8.4f, H1 = 3.1f, H2 = 3.0f, th = 0.3f, FY = 0.3f, st = 0.22f, y1 = FY + H1 + st, rx = w / 2 - 1.8f, rz = -0.6f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.4f, FY, d + 0.4f), VMat.Concrete);
                    Room4(b, -w / 2, w / 2, -d / 2, d / 2, FY, H1, th, plaster, VMat.Band, 0.9f, new[] { Win(1.2f), Door(3.2f), Win(6.4f) }, null, new[] { Win(2.5f), Win(7.5f) }, new[] { Win(d / 2) });
                    Wall(b, new Vector3(-w / 2 + th, FY, 0.9f), new Vector3(w / 2 - 1.65f, FY, 0.9f), FY, H1, 0.22f, VMat.Lime, VMat.Lime, 0, new[] { Door(2.5f, 1.1f, 2.1f) }, false);
                    float hx0 = w / 2 - 1.5f, hx1 = w / 2 - th, sz0 = -d / 2 + 0.55f, run = 5.3f;
                    Stairs(b, new Vector3((hx0 + hx1) / 2, FY, sz0), Vector3.forward, hx1 - hx0, y1 - FY, run, VMat.Concrete, true);
                    SlabHole(b, w, d, y1, st, hx0, hx1, sz0 - 0.1f, sz0 + run, VMat.Concrete);
                    b.Box(new Vector3(0, y1 - st / 2, 0), new Vector3(w, st, d), Quaternion.identity, VMat.Concrete, 4, false);
                    // upper room (back-left block) + terrace parapets
                    Room4(b, -w / 2, rx, rz, d / 2, y1, H2, th, plaster, plaster, 0, new[] { Win(1.6f), Win(4.4f), Door(7.4f, 1.1f) }, new[] { Door(3.9f, 1.1f) }, new[] { Win(4.1f) }, new[] { Win(2.4f) });
                    b.Box(new Vector3(0, FY + H1 / 2, 0), new Vector3(w, H1, d), Quaternion.identity, plaster, 4, false);
                    b.Box(new Vector3((-w / 2 + rx) / 2, y1 + H2 / 2, (rz + d / 2) / 2), new Vector3(rx + w / 2, H2, d / 2 - rz), Quaternion.identity, plaster, 4, false);
                    Slab(b, new Vector3((-w / 2 + rx) / 2, y1 + H2 + 0.1f, (rz + d / 2) / 2), new Vector3(rx + w / 2 + 0.2f, 0.2f, d / 2 - rz + 0.2f), VMat.Concrete, 7);
                    float pt = 0.18f, ph = 0.95f;
                    Wall(b, new Vector3(-w / 2, y1, -d / 2 + pt / 2), new Vector3(w / 2, y1, -d / 2 + pt / 2), y1, ph, pt, plaster, plaster, 0, null, false);
                    Wall(b, new Vector3(w / 2 - pt / 2, y1, -d / 2 + pt), new Vector3(w / 2 - pt / 2, y1, d / 2), y1, ph, pt, plaster, plaster, 0, null, false);
                    Wall(b, new Vector3(w / 2 - pt, y1, d / 2 - pt / 2), new Vector3(rx, y1, d / 2 - pt / 2), y1, ph, pt, plaster, plaster, 0, null, false);
                    Wall(b, new Vector3(-w / 2 + pt / 2, y1, rz), new Vector3(-w / 2 + pt / 2, y1, -d / 2 + pt), y1, ph, pt, plaster, plaster, 0, null, false);
                    // railing around the stairwell on the terrace side
                    Wall(b, new Vector3(hx0 - 0.08f, y1, sz0 - 0.1f), new Vector3(hx0 - 0.08f, y1, rz), y1, ph, 0.12f, VMat.Wood, VMat.Wood, 0, null, false);
                    Wall(b, new Vector3(hx0 - 0.1f, y1, sz0 - 0.18f), new Vector3(hx1, y1, sz0 - 0.18f), y1, ph, 0.12f, VMat.Wood, VMat.Wood, 0, null, false);
                    Slab(b, new Vector3(-w / 2 + 3.2f, FY + 2.6f, -d / 2 - 0.25f), new Vector3(1.9f, 0.08f, 0.55f), VMat.Concrete, 3, false);
                    Cyl(b, new Vector3(-w / 2 + 1.2f, 0, d / 2 - 1.2f), 0.62f, y1 + H2 + 0.2f, y1 + H2 + 1.35f, 14, VMat.Dark, 3, false);
                    Table(b, new Vector3(-w / 2 + 1.2f, FY, -d / 2 + 1.2f), 0); Almirah(b, new Vector3(-w / 2 + 0.6f, FY, 3.0f), 90); Charpai(b, new Vector3(-1.0f, FY, d / 2 - 0.9f), 0);
                    Crate(b, new Vector3(1.6f, FY, -d / 2 + 0.8f), 15, 0.8f); Sack(b, new Vector3(1.8f, FY, d / 2 - 0.8f), 0);
                    Charpai(b, new Vector3(-w / 2 + 1.4f, y1, d / 2 - 1.0f), 0); Table(b, new Vector3(0.8f, y1, d / 2 - 0.8f), 0); Almirah(b, new Vector3(rx - 0.6f, y1, 0.6f), -90);
                    Spot(sp, -w / 2 + 1.2f, FY + 0.79f, -d / 2 + 1.2f, 1); Spot(sp, 0.2f, FY, -1.6f, 1); Spot(sp, -3.2f, FY, 2.6f, 1); Spot(sp, 0.4f, FY, 2.6f, 1);
                    Spot(sp, -3.4f, y1, 1.0f, 2); Spot(sp, 0.8f, y1 + 0.79f, d / 2 - 0.8f, 2); Spot(sp, -1.2f, y1, 0.4f, 2);
                    Spot(sp, -3.6f, y1, -3.3f, 2); Spot(sp, 1.4f, y1, -3.5f, 2); Spot(sp, w / 2 - 0.9f, y1, d / 2 - 1.0f, 1);
                    break;
                }
                case BType.TinShed:
                {
                    float w = 7f, d = 5f, FY = 0.15f, hf = 3.0f, hb = 2.5f, th = 0.08f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.3f, FY, d + 0.3f), VMat.Concrete);
                    Wall(b, new Vector3(w / 2, FY, d / 2 - th / 2), new Vector3(-w / 2, FY, d / 2 - th / 2), FY, hb, th, VMat.Tin, VMat.Tin, 0, null, false);
                    Wall(b, new Vector3(-w / 2 + th / 2, FY, d / 2 - th), new Vector3(-w / 2 + th / 2, FY, -d / 2), FY, hb, th, VMat.Tin, VMat.Tin, 0, null, false);
                    Wall(b, new Vector3(w / 2 - th / 2, FY, -d / 2), new Vector3(w / 2 - th / 2, FY, d / 2 - th), FY, hb, th, VMat.Tin, VMat.Tin, 0, null, false);
                    foreach (var x in new[] { -w / 2 + th / 2, w / 2 - th / 2 }) b.Gable(new Vector3(x, FY + hb, d / 2), new Vector3(x, FY + hb, -d / 2), new Vector3(x, FY + hf, -d / 2), th, VMat.Tin, 3, true);
                    foreach (var x in new[] { -w / 2 + 0.08f, 0f, w / 2 - 0.08f }) b.Box(new Vector3(x, FY + hf / 2, -d / 2 + 0.08f), new Vector3(0.14f, hf, 0.14f), Quaternion.identity, VMat.Wood, 3, true);
                    b.Box(new Vector3(0, FY + hf, -d / 2 + 0.08f), new Vector3(w, 0.14f, 0.14f), Quaternion.identity, VMat.Wood, 3, false);
                    float ang = Mathf.Atan2(hf - hb, d) * Mathf.Rad2Deg;
                    b.Box(new Vector3(0, FY + (hf + hb) / 2 + 0.12f, 0), new Vector3(w + 0.6f, 0.08f, d / Mathf.Cos(ang * Mathf.Deg2Rad) + 0.7f), Quaternion.Euler(ang, 0, 0), VMat.Tin, 7, true);
                    b.Box(new Vector3(0, FY + hb / 2, 0.3f), new Vector3(w, hb, d - 0.6f), Quaternion.identity, VMat.Tin, 4, false);
                    Hay(b, new Vector3(-2.2f, FY, 1.4f), 5); Hay(b, new Vector3(-2.2f, FY + 0.8f, 1.4f), -8); Hay(b, new Vector3(-0.8f, FY, 1.6f), 0); Crate(b, new Vector3(2.4f, FY, 1.5f), 20, 0.9f);
                    Spot(sp, 1.2f, FY, 0.2f, 0); Spot(sp, -1.6f, FY, -0.9f, 0);
                    break;
                }
                default: // Godown
                {
                    float w = 18f, d = 11f, H = 5.2f, th = 0.3f, FY = 0.3f;
                    Slab(b, new Vector3(0, FY / 2, 0), new Vector3(w + 0.4f, FY, d + 0.4f), VMat.Concrete);
                    Room4(b, -w / 2, w / 2, -d / 2, d / 2, FY, H, th, VMat.Tin, VMat.Brick, 1.3f, new[] { new Op(9f, 4.4f, 0, 3.8f) }, new[] { Win(3f, 1.6f, 3.4f, 4.3f), Win(8f, 1.6f, 3.4f, 4.3f) }, new[] { Win(4f, 1.6f, 3.4f, 4.3f), Win(9f, 1.6f, 3.4f, 4.3f), Win(14f, 1.6f, 3.4f, 4.3f) }, new[] { Door(5.5f), Win(2.2f, 1.6f, 3.4f, 4.3f) }, false);
                    b.Box(new Vector3(0, FY + H / 2, 0), new Vector3(w, H, d), Quaternion.identity, VMat.Tin, 4, false);
                    b.Box(new Vector3(4.6f, FY + 2.0f, -d / 2 - 0.12f), new Vector3(4.2f, 3.9f, 0.06f), Quaternion.identity, VMat.Tin, 3, true); // slid-open door panel
                    b.Box(new Vector3(0, FY + 4.0f, -d / 2 - 0.1f), new Vector3(10f, 0.12f, 0.1f), Quaternion.identity, VMat.Dark, 1, false);
                    GableRoof(b, w, d, FY + H - 0.15f, FY + H + 1.9f, 0.45f, 0.1f, VMat.Tin, VMat.Tin, FY + H, th);
                    Slab(b, new Vector3(0, 0.2f, -d / 2 - 1.1f), new Vector3(7f, 0.4f, 1.8f), VMat.Concrete);
                    foreach (var p in new[] { new Vector2(-6.5f, 3.2f), new Vector2(-5.3f, 3.2f), new Vector2(-6.5f, 2.0f) }) Crate(b, new Vector3(p.x, FY, p.y), 0, 1.15f);
                    Crate(b, new Vector3(-6.0f, FY + 1.15f, 3.0f), 8, 1.1f); Crate(b, new Vector3(5.5f, FY, -1.5f), 12, 1.2f); Crate(b, new Vector3(6.6f, FY, -1.2f), -6, 1.0f); Crate(b, new Vector3(5.8f, FY + 1.2f, -1.4f), 30, 0.9f);
                    for (int i = 0; i < 4; i++) { Sack(b, new Vector3(1.0f + i * 1.0f, FY, 4.2f), 90); Sack(b, new Vector3(1.0f + i * 1.0f, FY + 0.45f, 4.2f), 92); }
                    Crate(b, new Vector3(-1.5f, FY, -2.5f), 45, 1.1f); Almirah(b, new Vector3(w / 2 - 0.6f, FY, 3.5f), -90);
                    Spot(sp, -4.0f, FY, 2.0f, 2); Spot(sp, -7.6f, FY, -3.6f, 2); Spot(sp, 0.0f, FY, -1.0f, 2); Spot(sp, 4.2f, FY, 1.6f, 2); Spot(sp, 7.2f, FY, -3.8f, 2); Spot(sp, -2.6f, FY, 3.6f, 1);
                    break;
                }
            }
            Finish(go, b, sp, k);
        }

        static void Finish(GameObject go, MB b, List<(Vector3 p, byte t)> sp, BType k)
        {
            bool render = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (render)
            {
                var lods = new LOD[3]; float[] h = { 0.16f, 0.045f, 0.008f };
                for (int l = 0; l < 3; l++)
                {
                    var c = new GameObject("LOD" + l); c.transform.SetParent(go.transform, false);
                    var mesh = b.ToMesh(l, out var used, go.name + "_LOD" + l);
                    c.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = c.AddComponent<MeshRenderer>(); mr.sharedMaterials = used;
                    mr.shadowCastingMode = l == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    lods[l] = new LOD(h[l], new Renderer[] { mr });
                }
                var lg = go.AddComponent<LODGroup>(); lg.SetLODs(lods); lg.RecalculateBounds();
            }
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = b.ColMesh(go.name + "_Col");
            ushort id = (ushort)buildingCount++;
            foreach (var s in sp) Spots.Add(new LootSpot { pos = go.transform.TransformPoint(s.p), tier = s.t, building = id });
        }

        static void DirtPatch(Transform parent, Vector3 c, float r, System.Random rnd)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var b = new MB(); int n = 36; var pts = new Vector3[n];
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; float rr = r * (0.82f + 0.25f * (float)rnd.NextDouble()); pts[i] = new Vector3(Mathf.Cos(a) * rr, 0.03f, Mathf.Sin(a) * rr); }
            for (int i = 0; i < n; i++) b.Tri(new Vector3(0, 0.03f, 0), pts[(i + 1) % n], pts[i], Vector3.up, Vector3.right, Vector3.forward, VMat.Dirt, 1, false);
            var go = new GameObject("DirtPatch"); go.transform.SetParent(parent, false); go.transform.localPosition = c;
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(0, out var used, "Dirt"); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = used; mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material textMat;
        public static Material TextMat(Font font)
        {
            if (textMat != null) return textMat;
            var sh = Resources.Load<Shader>("AgniNet/AgniWorldText") ?? Shader.Find("AgniRoyale/WorldText");
            textMat = sh != null ? new Material(sh) { mainTexture = font.material.mainTexture } : font.material; return textMat;
        }
        static void Sign(Transform parent, Village v, System.Random rnd)
        {
            var dir = v.c.sqrMagnitude < 1 ? new Vector2(0, -1) : -v.c.normalized; // towards the arena centre
            var p = v.c + dir * (v.r + 3f); var go = new GameObject("Sign_" + v.name); go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, 0, p.y); go.transform.localRotation = Quaternion.LookRotation(new Vector3(-dir.x, 0, -dir.y)); // board faces away from the village
            var b = new MB();
            b.Box(new Vector3(-1.1f, 1.1f, 0), new Vector3(0.12f, 2.2f, 0.12f), Quaternion.identity, VMat.Wood, 3, true); b.Box(new Vector3(1.1f, 1.1f, 0), new Vector3(0.12f, 2.2f, 0.12f), Quaternion.identity, VMat.Wood, 3, true);
            b.Box(new Vector3(0, 1.85f, 0), new Vector3(2.7f, 0.65f, 0.07f), Quaternion.identity, VMat.Wood, 3, true);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(0, out var used, "Sign"); go.AddComponent<MeshRenderer>().sharedMaterials = used;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                for (int s = -1; s <= 1; s += 2)
                {
                    var t = new GameObject("Name").AddComponent<TextMesh>(); t.transform.SetParent(go.transform, false); t.transform.localPosition = new Vector3(0, 1.85f, s * 0.045f); t.transform.localRotation = Quaternion.Euler(0, s < 0 ? 0 : 180, 0);
                    t.text = v.name.ToUpper(); t.font = font; t.fontSize = 64; t.characterSize = 0.045f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = new Color(0.98f, 0.93f, 0.8f); t.fontStyle = FontStyle.Bold;
                    t.GetComponent<MeshRenderer>().sharedMaterial = TextMat(font);
                }
            }
            go.AddComponent<MeshCollider>().sharedMesh = b.ColMesh("SignCol");
        }
    }
}
