// Agni Royale — IMPORT v1: where the Blender landmarks stand on the island, and the island's water (river + Kayal backwater).
// Same on server and every client: AgniArenaBootstrap calls AgniLandmarks.Build() right after AgniVillages.Build().
// Loot spots inside the landmarks come from Resources/AgniImport/landmark_spots.json (generated in the editor by raycasting the kit's colliders).
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Import
{
    public static class AgniIsland
    {
        public const float GrassR = 440f, BeachR = 482f, SeaFloorR = 540f, SeaY = -1f;
        // the "Hooghly" channel that Ghanta Ghar's steel bridge crosses: straight N-S cut on the east side, from coast to coast
        public const float RiverX = 343.5f, RiverHalfW = 19.8f, RiverBank = 3.5f, RiverDepth = -3.2f;
        // Kayal backwater canal: N-S, open to the sea on the south, wadeable (bed 0.85 m under the water like the Blender kit)
        public const float CanalX = -66.5f, CanalHalfW = 18f, CanalNorthZ = -402f, CanalBed = -1.85f;
        static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        /// ground height of the island mesh (0 on the playable flat; channels and coast go under the sea plane at -1)
        public static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float h = 0f;
            if (r > GrassR) h = Mathf.Lerp(0f, -0.15f, S(GrassR, BeachR, r)) + Mathf.Lerp(0f, -3.4f, S(BeachR, SeaFloorR, r));
            // river: full depth inside the water, short steep banks (embankments in the Ghanta Ghar kit cover them at the bridge)
            float dx = Mathf.Abs(x - RiverX);
            if (dx < RiverHalfW + RiverBank && r > 120f) { float k = 1f - S(RiverHalfW - 1.5f, RiverHalfW + RiverBank, dx); h = Mathf.Min(h, Mathf.Lerp(0, RiverDepth, k)); }
            float dc = Canal(x, z);
            if (dc < CanalHalfW + 5f) { float k = 1f - S(CanalHalfW - 2f, CanalHalfW + 5f, dc); h = Mathf.Min(h, Mathf.Lerp(0, CanalBed, k)); }
            return h;
        }
        /// distance from the canal's centre line (a capsule: rounded inland end, runs out past the coast)
        public static float Canal(float x, float z) { float cz = Mathf.Clamp(z, -600f, CanalNorthZ); return new Vector2(x - CanalX, z - cz).magnitude; }
        public static bool IsWater(float x, float z) => Height(x, z) < SeaY + 0.05f;
        public static bool IsWaterOrBank(float x, float z, float pad) => Height(x, z) < -0.05f || (Mathf.Abs(x - RiverX) < RiverHalfW + RiverBank + pad && Mathf.Sqrt(x * x + z * z) > 120f) || Canal(x, z) < CanalHalfW + 5f + pad || Mathf.Sqrt(x * x + z * z) > GrassR - 4f + pad * 0.3f;
        public static float WaterHeight(Vector3 p) => SeaY;
    }

    public static class AgniLandmarks
    {
        public class Spot { public string name, display; public Vector3 pos; public float yaw, footR; public byte tier; public Spot(string n, string d, Vector3 p, float yaw, float r, byte tier) { name = n; display = d; pos = p; this.yaw = yaw; footR = r; this.tier = tier; } }
        /// MAP_DESIGN zones mapped onto the island (+z north, +x east): Kolkata clock-tower square east on the river, tea garden north-east,
        /// the peak north with the stone village at its foot, the mill on the west coast, the Kerala water village in the southern backwater.
        public static readonly List<Spot> Table = new List<Spot>
        {
            new Spot("GhantaGharSquare", "Ghanta Ghar", new Vector3(292f, 0.3f, 6f), 180f, 58f, 2),
            new Spot("ChaiBagan", "Chai Bagan", new Vector3(196f, 0.04f, 236f), 205f, 62f, 0),
            new Spot("VindhyaShikhar", "Vindhya Shikhar", new Vector3(-60f, 28f, 318f), 10f, 112f, 2),
            new Spot("PatharGaon", "Pathar Gaon", new Vector3(-128f, 0.04f, 172f), 35f, 26f, 1),
            new Spot("BambaiMill", "Bambai Mill", new Vector3(-330f, 0.05f, -60f), 90f, 62f, 2),
            new Spot("KayalVillage", "Kayal Village", new Vector3(-65f, -1f, -458f), 90f, 32f, 0),
        };
        public static Transform Root;
        public static readonly List<(Vector2 c, float r)> Footprints = new List<(Vector2, float)>();
        static AgniLandmarks() { foreach (var s in Table) Footprints.Add((new Vector2(s.pos.x, s.pos.z), s.footR)); }

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
