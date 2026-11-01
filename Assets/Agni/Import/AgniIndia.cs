// Agni Royale — IMPORT v1: the match map is INDIA (Natural Earth 1:10m, India point-of-view outline incl. all of J&K and Ladakh), the terrain from
// TerrainSource (heights/splats/zones/roads/bridges in map.json), rebuilt the same way on the server and every client at load from Resources/AgniImport/India.
// World = map metres - Off (so the world origin sits in central India). This class owns the height grid (after flattening the zone pads,
// carving Ghanta Ghar's river and Kayal's backwater) and every placement question (heights, water, roads, zones).
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Agni.Import
{
    public static class AgniIndia
    {
        [System.Serializable] public class Proj { public float lonc, latc, cosLat, metersPerDegLat; }
        [System.Serializable] public class TileInfo { public int i, j; public int[] layers; }
        [System.Serializable] public class Zone { public int id; public string name, inspired, kind; public float x, z, y, radius, lon, lat; }
        [System.Serializable] public class MSpot { public string name, kind; public float x, z, y; }
        [System.Serializable] public class Road { public string name, type; public float width; public float[] pts; }
        [System.Serializable] public class Bridge { public string road; public float x, z, y, len, width, yaw; public bool rail; }
        [System.Serializable] public class MapData
        {
            public int size, tiles, tileSize, heightRes, alphaRes; public float @base, range, seaLevel;
            public string[] layers; public Proj projection; public string outline;
            public TileInfo[] tilesInfo; public Zone[] zones; public MSpot[] spots; public Road[] roads; public Bridge[] bridges; public Road[] rivers;
        }
        public static readonly Vector2 Off = new Vector2(1600f, 2000f);
        public const float SeaY = 0f;
        public static MapData Map { get { Load(); return map; } }
        static MapData map; static float[] gh; static int gn; static bool loaded;
        static readonly Dictionary<string, byte[]> splats = new Dictionary<string, byte[]>();

        public static Vector3 W(float mx, float mz, float y = 0) => new Vector3(mx - Off.x, y, mz - Off.y);
        public static Zone ZoneNamed(string n) { Load(); foreach (var z in map.zones) if (z.name == n) return z; return null; }
        public static Vector2 ZoneW(string n) { var z = ZoneNamed(n); return z == null ? Vector2.zero : new Vector2(z.x - Off.x, z.z - Off.y); }

        static byte[] Gunzip(byte[] src) { using (var ms = new MemoryStream(src)) using (var gz = new GZipStream(ms, CompressionMode.Decompress)) using (var o = new MemoryStream()) { gz.CopyTo(o); return o.ToArray(); } }
        public static byte[] Splat(int i, int j) { Load(); splats.TryGetValue(i + "_" + j, out var b); return b; }

        public static void Load()
        {
            if (loaded) return; loaded = true;
            var mj = Resources.Load<TextAsset>("AgniImport/India/map");
            map = JsonUtility.FromJson<MapData>(mj.text);
            int res = map.heightRes, T = map.tiles; gn = (res - 1) * T + 1; gh = new float[gn * gn];
            foreach (var ti in map.tilesInfo)
            {
                var hb = Gunzip(Resources.Load<TextAsset>("AgniImport/India/h_" + ti.i + "_" + ti.j).bytes);
                for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { int o = (z * res + x) * 2; gh[(ti.j * (res - 1) + z) * gn + ti.i * (res - 1) + x] = map.@base + (hb[o] | (hb[o + 1] << 8)) / 65535f * map.range; }
                var sb = Resources.Load<TextAsset>("AgniImport/India/s_" + ti.i + "_" + ti.j); if (sb != null) splats[ti.i + "_" + ti.j] = Gunzip(sb.bytes);
            }
            BuildRoadIndex();
            Edits();
            Debug.Log("[AgniIndia] map loaded: " + gn + "^2 height grid, " + map.zones.Length + " zones, " + map.roads.Length + " roads");
        }

        /// raw grid height (metres) at map coords
        static float HM(float x, float z)
        {
            if (x < 0 || z < 0 || x > map.size || z > map.size) return map.@base;
            float fx = Mathf.Clamp(x / 2f, 0, gn - 1.001f), fz = Mathf.Clamp(z / 2f, 0, gn - 1.001f);
            int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
            float a = Mathf.Lerp(gh[iz * gn + ix], gh[iz * gn + ix + 1], tx), b = Mathf.Lerp(gh[(iz + 1) * gn + ix], gh[(iz + 1) * gn + ix + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }
        /// world height of the ground (after the zone pads and carves)
        public static float H(float wx, float wz) { Load(); return HM(wx + Off.x, wz + Off.y); }
        public static float GridAt(int x, int z) => gh[z * gn + x];
        public static int GridN => gn;

        // ------------------------------------------------------------ edits: flat pads for every landmark / village / spot, carved water
        public struct Pad { public Vector2 c; public float flat, blend, h; }
        public static readonly List<Pad> Pads = new List<Pad>();
        static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        public static float PeakNear(Vector2 w, float r) { float best = -99; for (float dx = -r; dx <= r; dx += 2) for (float dz = -r; dz <= r; dz += 2) if (dx * dx + dz * dz <= r * r) best = Mathf.Max(best, HM(w.x + dx + Off.x, w.y + dz + Off.y)); return best; }
        static void Edits()
        {
            // landmark + village pads come from their own tables (positions/heights fixed by the zones)
            foreach (var s in AgniLandmarks.Table) Pads.Add(new Pad { c = new Vector2(s.pos.x, s.pos.z), flat = s.flatR, blend = s.blend, h = s.padY });
            foreach (var v in Agni.Net.AgniVillages.Layout()) Pads.Add(new Pad { c = v.c, flat = v.r + 12f, blend = 38f, h = VillageY(v.c) });
            foreach (var p in Pads) FlattenCircle(p);
            // Ghanta Ghar: a 40 m river (the Hooghly) under the kit's steel bridge
            var gg = AgniLandmarks.Table.Find(t => t.name == "GhantaGharSquare");
            if (gg != null)
            {
                var rot = Quaternion.Euler(0, gg.yaw, 0); var mid = gg.pos + rot * new Vector3(-52f, 0, 0); var along = rot * Vector3.forward;
                CarveSegment(new Vector2(mid.x, mid.z) - new Vector2(along.x, along.z) * 260f, new Vector2(mid.x, mid.z) + new Vector2(along.x, along.z) * 260f, 19.5f, 7f, -3.0f);
            }
            // Siliguri corridor ("chicken's neck"): at 1 px = 2 m the real ~22 km neck is a thread the sea cuts; widen it to a ~70 m land bridge so the
            // north-east (Chai Bagan) is reachable on foot and by road, as on the real map
            RaiseSegment(new Vector2(987f, 522f), new Vector2(1265f, 584f), 34f, 20f, 3.2f);
            // Kayal: the backwater channel the stilt village stands in (kit water level = sea level, bed 0.85 m down)
            var ky = AgniLandmarks.Table.Find(t => t.name == "KayalVillage");
            if (ky != null)
            {
                var rot = Quaternion.Euler(0, ky.yaw, 0); var a = rot * Vector3.right;
                var c = new Vector2(ky.pos.x, ky.pos.z) + new Vector2((rot * new Vector3(0, 0, -1.5f)).x, (rot * new Vector3(0, 0, -1.5f)).z);
                CarveSegment(c - new Vector2(a.x, a.z) * 120f, c + new Vector2(a.x, a.z) * 120f, 18f, 6f, -0.9f);
            }
        }
        public static float VillageY(Vector2 c) { float h = HM(c.x + Off.x, c.y + Off.y); return Mathf.Max(h, 1.6f); }
        static void FlattenCircle(Pad p)
        {
            float R = p.flat + p.blend; int x0 = Mathf.Max(0, (int)((p.c.x + Off.x - R) / 2)), x1 = Mathf.Min(gn - 1, (int)((p.c.x + Off.x + R) / 2) + 1);
            int z0 = Mathf.Max(0, (int)((p.c.y + Off.y - R) / 2)), z1 = Mathf.Min(gn - 1, (int)((p.c.y + Off.y + R) / 2) + 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    float d = new Vector2(x * 2f - Off.x - p.c.x, z * 2f - Off.y - p.c.y).magnitude; if (d > R) continue;
                    float w = 1f - S(p.flat, R, d); int k = z * gn + x; gh[k] = Mathf.Lerp(gh[k], p.h, w);
                }
        }
        static void RaiseSegment(Vector2 a, Vector2 b, float half, float bank, float top)
        {
            float R = half + bank; var ab = b - a;
            int x0 = Mathf.Max(0, (int)((Mathf.Min(a.x, b.x) + Off.x - R) / 2)), x1 = Mathf.Min(gn - 1, (int)((Mathf.Max(a.x, b.x) + Off.x + R) / 2) + 1);
            int z0 = Mathf.Max(0, (int)((Mathf.Min(a.y, b.y) + Off.y - R) / 2)), z1 = Mathf.Min(gn - 1, (int)((Mathf.Max(a.y, b.y) + Off.y + R) / 2) + 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x * 2f - Off.x, z * 2f - Off.y); float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); float d = (a + ab * t - p).magnitude; if (d > R) continue;
                    float n = Mathf.PerlinNoise(p.x * 0.03f, p.y * 0.03f) * 0.8f;
                    float w = 1f - S(half * (0.8f + n * 0.4f), R, d); int k = z * gn + x; gh[k] = Mathf.Max(gh[k], Mathf.Lerp(gh[k], top + n, w));
                }
        }
        static void CarveSegment(Vector2 a, Vector2 b, float half, float bank, float bed)
        {
            float R = half + bank; var ab = b - a;
            int x0 = Mathf.Max(0, (int)((Mathf.Min(a.x, b.x) + Off.x - R) / 2)), x1 = Mathf.Min(gn - 1, (int)((Mathf.Max(a.x, b.x) + Off.x + R) / 2) + 1);
            int z0 = Mathf.Max(0, (int)((Mathf.Min(a.y, b.y) + Off.y - R) / 2)), z1 = Mathf.Min(gn - 1, (int)((Mathf.Max(a.y, b.y) + Off.y + R) / 2) + 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x * 2f - Off.x, z * 2f - Off.y); float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); float d = (a + ab * t - p).magnitude; if (d > R) continue;
                    float w = 1f - S(half - 2f, R, d); int k = z * gn + x; gh[k] = Mathf.Min(gh[k], Mathf.Lerp(gh[k], bed, w));
                }
        }

        // ------------------------------------------------------------ roads (map.json polylines, world coords) + a coarse index for distance queries
        public static readonly List<(Vector2 a, Vector2 b, float w)> RoadSegs = new List<(Vector2, Vector2, float)>();
        static Dictionary<long, List<int>> roadGrid = new Dictionary<long, List<int>>(); const float RG = 64f;
        static long Key(int i, int j) => ((long)i << 32) ^ (uint)j;
        static void BuildRoadIndex()
        {
            foreach (var r in map.roads)
            {
                if (r.type == "rail") continue;
                Vector2 last = Vector2.zero; bool has = false;
                for (int i = 0; i < r.pts.Length / 2; i++)
                {
                    var p = new Vector2(r.pts[i * 2] - Off.x, r.pts[i * 2 + 1] - Off.y);
                    if (!has) { last = p; has = true; continue; }
                    if ((p - last).sqrMagnitude < 36f && i < r.pts.Length / 2 - 1) continue;
                    int si = RoadSegs.Count; RoadSegs.Add((last, p, r.width));
                    var mn = Vector2.Min(last, p) - Vector2.one * 20f; var mx = Vector2.Max(last, p) + Vector2.one * 20f;
                    for (int gx = Mathf.FloorToInt(mn.x / RG); gx <= Mathf.FloorToInt(mx.x / RG); gx++) for (int gz = Mathf.FloorToInt(mn.y / RG); gz <= Mathf.FloorToInt(mx.y / RG); gz++) { var k = Key(gx, gz); if (!roadGrid.TryGetValue(k, out var l)) roadGrid[k] = l = new List<int>(); l.Add(si); }
                    last = p;
                }
            }
        }
        /// distance to the nearest road edge's centre line (99 if none within ~40 m)
        public static float RoadDist(float wx, float wz)
        {
            Load(); var p = new Vector2(wx, wz); float best = 99f;
            if (!roadGrid.TryGetValue(Key(Mathf.FloorToInt(wx / RG), Mathf.FloorToInt(wz / RG)), out var l)) return best;
            foreach (int si in l) { var (a, b, w) = RoadSegs[si]; var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude)); best = Mathf.Min(best, (a + ab * t - p).magnitude - w * 0.5f); }
            return best;
        }
        /// splat weight of a terrain layer (by name: sand grass dry rock snow mustard) at world xz
        public static float Layer(float wx, float wz, string name)
        {
            Load(); float mx = wx + Off.x, mz = wz + Off.y; int ti = Mathf.Clamp((int)(mx / map.tileSize), 0, map.tiles - 1), tj = Mathf.Clamp((int)(mz / map.tileSize), 0, map.tiles - 1);
            var info = System.Array.Find(map.tilesInfo, t => t.i == ti && t.j == tj); if (info == null) return 0;
            int li = System.Array.IndexOf(map.layers, name), slot = System.Array.IndexOf(info.layers, li); if (slot < 0) return 0;
            var b = Splat(ti, tj); if (b == null) return 0; int ar = map.alphaRes;
            int x = Mathf.Clamp((int)((mx - ti * map.tileSize) / map.tileSize * ar), 0, ar - 1), z = Mathf.Clamp((int)((mz - tj * map.tileSize) / map.tileSize * ar), 0, ar - 1);
            return b[(slot * ar + z) * ar + x] / 255f;
        }
        public static bool OnLand(float wx, float wz, float minH = 1.2f) => H(wx, wz) > minH;
        public static float Lat(float wz) { Load(); return map.projection.latc + (wz + Off.y - 2048f) / map.projection.metersPerDegLat; }
        public static float Lon(float wx) { Load(); return map.projection.lonc + (wx + Off.x - 2048f) / (map.projection.cosLat * map.projection.metersPerDegLat); }
    }
}
