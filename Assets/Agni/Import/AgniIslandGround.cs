// Agni Royale — IMPORT v1: the island ground mesh, built from AgniIsland.Height on load (server + clients + edit mode), not stored as an asset.
// Flat cells are 48 m quads, cells touching water are 6 m, the coast band 3 m; T-junctions only sit on flat edges, so no cracks.
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Import
{
    [ExecuteAlways]
    public class AgniIslandGround : MonoBehaviour
    {
        public Material grass, sand;
        static Mesh cached;
        void OnEnable() { Build(); }
        public void Build()
        {
            var mf = GetComponent<MeshFilter>(); if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>(); if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            var mc = GetComponent<MeshCollider>(); if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            if (cached == null) cached = Generate();
            mf.sharedMesh = cached; mc.sharedMesh = cached; mr.sharedMaterials = new[] { grass, sand };
        }
        public static Mesh Generate()
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tg = new List<int>(); var ts = new List<int>();
            var map = new Dictionary<long, int>();
            int V(float x, float z)
            {
                long key = ((long)Mathf.RoundToInt(x * 4) << 32) ^ (uint)Mathf.RoundToInt(z * 4);
                if (map.TryGetValue(key, out var i)) return i;
                i = verts.Count; verts.Add(new Vector3(x, AgniIsland.Height(x, z), z)); uvs.Add(new Vector2(x / 620f + 0.5f, z / 620f + 0.5f)); map[key] = i; return i;
            }
            const float R = AgniIsland.SeaFloorR + 20f, big = 48f, small = 6f;
            int cells = Mathf.CeilToInt(R * 2 / big);
            for (int cx = 0; cx < cells; cx++)
                for (int cz = 0; cz < cells; cz++)
                {
                    float x0 = -R + cx * big, z0 = -R + cz * big;
                    float nearest = new Vector2(Mathf.Clamp(0, x0, x0 + big), Mathf.Clamp(0, z0, z0 + big)).magnitude;
                    if (nearest > R) continue;
                    bool flat = true;
                    for (float sx = 0; sx <= big && flat; sx += small) for (float sz = 0; sz <= big && flat; sz += small) if (Mathf.Abs(AgniIsland.Height(x0 + sx, z0 + sz)) > 1e-4f) flat = false;
                    float far = new Vector2(Mathf.Abs(x0 + big / 2) + big / 2, Mathf.Abs(z0 + big / 2) + big / 2).magnitude;
                    bool coast = far > AgniIsland.GrassR - 6f && nearest < AgniIsland.GrassR + 6f;
                    float step = coast ? 3f : flat ? big : small;
                    for (float sx = 0; sx < big - 0.01f; sx += step)
                        for (float sz = 0; sz < big - 0.01f; sz += step)
                        {
                            float ax = x0 + sx, az = z0 + sz, bx = ax + step, bz = az + step;
                            int a = V(ax, az), b = V(bx, az), c = V(bx, bz), d = V(ax, bz);
                            float mx = (ax + bx) / 2, mz = (az + bz) / 2; float rr = Mathf.Sqrt(mx * mx + mz * mz); float hc = AgniIsland.Height(mx, mz);
                            var T = rr > AgniIsland.GrassR - 3f || hc < -0.25f ? ts : tg;
                            T.Add(a); T.Add(d); T.Add(c); T.Add(a); T.Add(c); T.Add(b);
                        }
                }
            var m = new Mesh { name = "AgniIslandGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.subMeshCount = 2; m.SetTriangles(tg, 0); m.SetTriangles(ts, 1);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            Debug.Log("[AgniIsland] ground mesh verts=" + verts.Count + " tris=" + (tg.Count + ts.Count) / 3);
            return m;
        }
    }
}
