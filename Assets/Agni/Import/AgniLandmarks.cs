// Agni Royale — IMPORT v1: where the Blender landmarks stand on the island, and the island's water (river + Kayal backwater).
// Same on server and every client: AgniArenaBootstrap calls AgniLandmarks.Build() right after AgniVillages.Build().
// Loot spots inside the landmarks come from Resources/AgniImport/landmark_spots.json (generated in the editor by raycasting the kit's colliders).
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Import
{
    /// placement questions on the India map (thin adapter over AgniIndia, kept so every caller asks one place)
    public static class AgniIsland
    {
        public const float SeaY = AgniIndia.SeaY, RoadHalfW = 3f;
        public static float Height(float x, float z) => AgniIndia.H(x, z);
        public static bool IsWater(float x, float z) => AgniIndia.H(x, z) < SeaY + 0.05f;
        /// water, shore or steep: not a place for a hut, a tree or a parked car
        public static bool IsWaterOrBank(float x, float z, float pad)
        {
            if (AgniIndia.H(x, z) < 1.0f) return true;
            for (int i = 0; i < 8; i++) { float a = i * 0.785f; if (AgniIndia.H(x + Mathf.Cos(a) * pad, z + Mathf.Sin(a) * pad) < 0.7f) return true; }
            return false;
        }
        public static bool IsFlat(float x, float z, float r, float tol = 0.35f)
        {
            float h0 = AgniIndia.H(x, z), mn = h0, mx = h0;
            for (int i = 0; i < 8; i++) { float a = i * 0.785f; float h = AgniIndia.H(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r); mn = Mathf.Min(mn, h); mx = Mathf.Max(mx, h); }
            return mx - mn <= tol;
        }
        public static float RoadDist(float x, float z) => AgniIndia.RoadDist(x, z);
        public static List<(Vector2 a, Vector2 b)> Roads { get { var l = new List<(Vector2, Vector2)>(); foreach (var r in AgniIndia.RoadSegs) l.Add((r.a, r.b)); return l; } }
        public static float SandAt(float x, float z) => AgniIndia.Layer(x, z, "sand");

        // ------------------------------------------------------------ landmark kit rectangles (sampled from the kit colliders in the editor)
        [System.Serializable] class PadFile { public KitPad[] pads; }
        [System.Serializable] public class KitPad { public string name; public float x0, z0, cell; public int w, h; public float[] y; [System.NonSerialized] public AgniLandmarks.Spot spot; }
        static KitPad[] pads; 
        static void LoadPads()
        {
            if (pads != null) return; pads = new KitPad[0];
            var t = Resources.Load<TextAsset>("AgniImport/landmark_pads"); if (t == null) return;
            pads = JsonUtility.FromJson<PadFile>(t.text).pads ?? new KitPad[0];
            foreach (var p in pads) p.spot = AgniLandmarks.Table.Find(s => s.name == p.name);
        }
        /// on a hard kit surface (plaza paving, yard, dock deck) sampled in the editor — the 3x3 cells around (x,z); grass stays off these
        public static bool OnKitSurface(float x, float z)
        {
            LoadPads();
            foreach (var p in pads)
            {
                var s = p.spot; if (s == null) continue;
                var l = Quaternion.Euler(0, -s.yaw, 0) * new Vector3(x - s.pos.x, 0, z - s.pos.z);
                int i = Mathf.FloorToInt((l.x - p.x0) / p.cell), j = Mathf.FloorToInt((l.z - p.z0) / p.cell);
                if (i < -1 || j < -1 || i > p.w || j > p.h) continue;
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                    {
                        int a = i + di, b = j + dj; if (a < 0 || b < 0 || a >= p.w || b >= p.h) continue;
                        if (p.y[b * p.w + a] > -90f) return true;
                    }
            }
            return false;
        }
        /// metres from (x,z) to the nearest landmark kit rectangle (0 inside one, 999 when none is near)
        public static float KitEdgeDist(float x, float z)
        {
            LoadPads(); float best = 999f;
            foreach (var p in pads)
            {
                var s = p.spot; if (s == null) continue;
                var l = Quaternion.Euler(0, -s.yaw, 0) * new Vector3(x - s.pos.x, 0, z - s.pos.z);
                float dx = Mathf.Max(0f, Mathf.Max(p.x0 - l.x, l.x - (p.x0 + p.w * p.cell))), dz = Mathf.Max(0f, Mathf.Max(p.z0 - l.z, l.z - (p.z0 + p.h * p.cell)));
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }
        /// inside a landmark kit's footprint rectangle (+margin)?
        public static bool InKit(float x, float z, float margin)
        {
            LoadPads();
            foreach (var p in pads)
            {
                var s = p.spot; if (s == null) continue;
                var l = Quaternion.Euler(0, -s.yaw, 0) * new Vector3(x - s.pos.x, 0, z - s.pos.z);
                if (l.x > p.x0 - margin && l.x < p.x0 + p.w * p.cell + margin && l.z > p.z0 - margin && l.z < p.z0 + p.h * p.cell + margin) return true;
            }
            foreach (var s in AgniLandmarks.Table) if ((s.name == "VindhyaShikhar" || s.name == "KayalVillage") && new Vector2(x - s.pos.x, z - s.pos.z).magnitude < s.footR + margin) return true;
            return false;
        }
    }

    public static class AgniLandmarks
    {
        public class Spot
        {
            public string name, display, zone; public Vector3 pos; public float yaw, footR, flatR, blend, padY; public byte tier;
            public Spot(string n, string d, string zone, float yaw, float footR, byte tier, float flatR, float blend) { name = n; display = d; this.zone = zone; this.yaw = yaw; this.footR = footR; this.tier = tier; this.flatR = flatR; this.blend = blend; }
        }
        /// the 6 Blender landmarks at their MAP_DESIGN zones on the India map (map.json zone centres). padY = the flattened ground, pos.y = the kit root.
        public static readonly List<Spot> Table = new List<Spot>
        {
            new Spot("GhantaGharSquare", "Ghanta Ghar", "Ghanta Ghar Square", 180f, 58f, 2, 74f, 60f),
            new Spot("ChaiBagan", "Chai Bagan", "Chai Bagan", 0f, 62f, 0, 68f, 50f),
            new Spot("VindhyaShikhar", "Vindhya Shikhar", "Vindhya Shikhar", 10f, 40f, 2, 24f, 30f),
            new Spot("PatharGaon", "Pathar Gaon", "Pathar Gaon", 35f, 26f, 1, 36f, 45f),
            new Spot("BambaiMill", "Bambai Mill", "Bambai Docks & Mill", 90f, 62f, 2, 70f, 45f),
            new Spot("KayalVillage", "Kayal Village", "Kayal Village", 90f, 32f, 0, 44f, 40f),
        };
        public static readonly List<(Vector2 c, float r)> Footprints = new List<(Vector2, float)>();
        static AgniLandmarks()
        {
            foreach (var s in Table)
            {
                var z = AgniIndia.ZoneNamed(s.zone); var c = AgniIndia.ZoneW(s.zone);
                switch (s.name)
                {
                    case "GhantaGharSquare": s.padY = 1.2f; s.pos = new Vector3(c.x, s.padY + 0.04f, c.y); break;          // plaza top (kit y 0) sits 4 cm over the terrain: no slab edge; the kit's own -0.3 ground is buried
                    case "VindhyaShikhar": s.padY = AgniIndia.PeakNear(c, 40f) - 0.4f; s.pos = new Vector3(c.x, s.padY, c.y); break; // the terrain peak is the hill
                    case "KayalVillage": s.padY = 0.8f; s.pos = new Vector3(c.x, AgniIndia.SeaY, c.y); break;               // kit water level = sea level
                    case "BambaiMill": s.padY = 2.2f; s.pos = new Vector3(c.x, s.padY + 0.05f, c.y); break;
                    default: s.padY = Mathf.Max(1.6f, z != null ? z.y : 2f); s.pos = new Vector3(c.x, s.padY + 0.04f, c.y); break;
                }
                Footprints.Add((new Vector2(s.pos.x, s.pos.z), s.footR));
            }
        }
        public static Transform Root;

        [System.Serializable] class SpotFile { public LmSpots[] landmarks; }
        [System.Serializable] class LmSpots { public string name; public float[] p; public byte[] tier; }

        /// Instantiate every landmark (static kit, colliders, LODs) and add their loot spots to AgniVillages.Spots.
        public static Transform Build(Transform parent)
        {
            if (Root != null) return Root;
            Root = new GameObject("AgniLandmarks").transform; if (parent != null) Root.SetParent(parent, false);
            var txt = Resources.Load<TextAsset>("AgniImport/landmark_spots");
            var file = txt != null ? JsonUtility.FromJson<SpotFile>(txt.text) : null;
            int added = 0;
            for (int i = 0; i < Table.Count; i++)
            {
                var s = Table[i];
                var pf = Resources.Load<GameObject>("AgniImport/Landmarks/" + s.name);
                if (pf == null) { Debug.LogWarning("[AgniLandmarks] missing prefab " + s.name); continue; }
                var go = Object.Instantiate(pf, s.pos, Quaternion.Euler(0, s.yaw, 0), Root); go.name = s.name;
                if (s.name == "VindhyaShikhar") foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("VS_Hill")) t.gameObject.SetActive(false); // the India terrain's own peak is the hill now
                if (file != null && file.landmarks != null)
                    foreach (var l in file.landmarks)
                    {
                        if (l.name != s.name) continue;
                        for (int k = 0; k + 2 < l.p.Length; k += 3)
                        {
                            var w = go.transform.TransformPoint(new Vector3(l.p[k], l.p[k + 1], l.p[k + 2]));
                            byte t = (byte)Mathf.Clamp((l.tier != null && k / 3 < l.tier.Length ? l.tier[k / 3] : (byte)1) + (s.tier >= 2 ? 1 : 0) - (s.tier == 0 ? 1 : 0), 0, 2);
                            Agni.Net.AgniVillages.Spots.Add(new Agni.Net.LootSpot { pos = w, tier = t, building = (ushort)(60000 + i) });
                            added++;
                        }
                    }
            }
            Debug.Log("[AgniLandmarks] built " + Root.childCount + " landmarks, loot spots +" + added);
            return Root;
        }
        public static string NameAt(Vector3 p) { foreach (var s in Table) if (new Vector2(p.x - s.pos.x, p.z - s.pos.z).magnitude < s.footR) return s.display; return null; }
    }
}
