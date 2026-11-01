// Agni Royale — IMPORT v1: builds the India map at load (server + clients + edit mode): 16 terrain tiles from AgniIndia's height grid and the
// TerrainSource splats, the sea, the road ribbons and bridges from map.json, the Blender trees as terrain trees (biome by latitude/longitude/height/splat,
// colliders on the server too) and the Blender grass as terrain detail meshes (clients only; LOW = none, NORMAL = half, HIGH/VERY HIGH = full).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import
{
    [ExecuteAlways]
    public class AgniIndiaTerrain : MonoBehaviour
    {
        public TerrainLayer[] layers;            // sand grass dry rock snow mustard
        public Material terrainMat, roadMat, highwayMat, bridgeMat, waterMat;
        [System.Serializable] public class TreeKind { public GameObject prefab; public string biome; public float weight = 1f; public bool bush; }
        public TreeKind[] trees;
        [System.Serializable] public class GrassKind { public GameObject prefab, prefabLow; public string biome; public float density = 1f; public float minH = 0.4f, maxH = 0.8f; }
        public GrassKind[] grass;
        public float treeDensity = 1f;
        public static Terrain[] Tiles;
        static TerrainLayer saltLayer;
        bool built;

        void OnEnable() { if (!built) Build(); }
        void OnDisable() { Clear(); built = false; }
        void Clear() { for (int i = transform.childCount - 1; i >= 0; i--) { var c = transform.GetChild(i).gameObject; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); } }

        public void Build()
        {
            Clear(); built = true;
            var map = AgniIndia.Map; int res = map.heightRes, ares = map.alphaRes, T = map.tiles; var off = AgniIndia.Off;
            bool headless = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            var terr = new Terrain[T, T]; var all = new List<Terrain>();
            var treeProtos = new List<TreePrototype>(); if (trees != null) foreach (var t in trees) if (t.prefab != null) treeProtos.Add(new TreePrototype { prefab = t.prefab, bendFactor = 0 });
            int q = 2; try { q = Agni.UI.AgniMahal.Quality; } catch { } if (!Application.isPlaying) q = 3;
            foreach (var ti in map.tilesInfo)
            {
                var td = new TerrainData { hideFlags = HideFlags.DontSave }; td.heightmapResolution = res; td.size = new Vector3(map.tileSize, map.range, map.tileSize);
                var hts = new float[res, res]; int gx0 = ti.i * (res - 1), gz0 = ti.j * (res - 1);
                for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) hts[z, x] = Mathf.Clamp01((AgniIndia.GridAt(gx0 + x, gz0 + z) - map.@base) / map.range);
                td.SetHeights(0, 0, hts);
                if (!headless && layers != null && layers.Length >= 6)
                {
                    // the "snow" splat south of the Himalaya tile row is the Rann's white salt flat: give it a salt-crust layer (polygon cracks) instead of soft snow
                    if (saltLayer == null) saltLayer = Resources.Load<TerrainLayer>("AgniImport/India/L_salt");
                    int tj = ti.j; td.terrainLayers = System.Array.ConvertAll(ti.layers, k => k == 4 && tj < map.tiles - 1 && saltLayer != null ? saltLayer : layers[k]); td.alphamapResolution = ares; td.baseMapResolution = 256;
                    var sb = AgniIndia.Splat(ti.i, ti.j); int nl = ti.layers.Length; var am = new float[ares, ares, nl];
                    if (sb != null) for (int l = 0; l < nl; l++) for (int z = 0; z < ares; z++) for (int x = 0; x < ares; x++) am[z, x, l] = sb[(l * ares + z) * ares + x] / 255f;
                    else for (int z = 0; z < ares; z++) for (int x = 0; x < ares; x++) am[z, x, 0] = 1;
                    WornGround(am, ti, ares, nl);
                    td.SetAlphamaps(0, 0, am);
                }
                td.treePrototypes = treeProtos.ToArray();
                var go = Terrain.CreateTerrainGameObject(td); go.hideFlags = HideFlags.DontSave; go.name = "Terrain_" + ti.i + "_" + ti.j;
                go.transform.SetParent(transform, false); go.transform.position = new Vector3(ti.i * map.tileSize - off.x, map.@base, ti.j * map.tileSize - off.y);
                var t2 = go.GetComponent<Terrain>(); if (terrainMat != null) t2.materialTemplate = terrainMat;
                t2.heightmapPixelError = q <= 0 ? 10 : q == 1 ? 7 : 5; t2.basemapDistance = 400; t2.drawInstanced = true; t2.groupingID = 7; t2.allowAutoConnect = true;
                t2.treeDistance = q <= 0 ? 380 : q == 1 ? 600 : 900; t2.treeBillboardDistance = 2000; t2.treeMaximumFullLODCount = 60;
                t2.detailObjectDistance = q <= 0 ? 0 : q == 1 ? 45 : q == 2 ? 65 : 85; t2.detailObjectDensity = q <= 0 ? 0 : q == 1 ? 0.35f : q == 2 ? 0.6f : 0.85f; // LOW none, NORMAL / HIGH / VERY HIGH
                t2.shadowCastingMode = ShadowCastingMode.Off;
                go.GetComponent<TerrainCollider>().enabled = true;
                terr[ti.i, ti.j] = t2; all.Add(t2);
            }
            for (int i = 0; i < T; i++) for (int j = 0; j < T; j++) if (terr[i, j] != null) terr[i, j].SetNeighbors(i > 0 ? terr[i - 1, j] : null, j < T - 1 ? terr[i, j + 1] : null, i < T - 1 ? terr[i + 1, j] : null, j > 0 ? terr[i, j - 1] : null);
            Tiles = all.ToArray();
            BuildWater(); BuildRoads(); PlantTrees(terr);
            if (!headless && q > 0) PlantGrass(terr, q);
            Debug.Log("[AgniIndiaTerrain] built " + all.Count + " tiles (quality " + q + ", headless " + headless + ")");
        }

        /// trampled earth where a landmark meets the map: the gaps inside the kit and a 7 m ring around it go to the "dry" (earth) layer in noisy
        /// patches, so a plaza ends in worn ground and then grass instead of a hard paving-to-lawn line
        static void WornGround(float[,,] am, AgniIndia.TileInfo ti, int ares, int nl)
        {
            var map = AgniIndia.Map; var off = AgniIndia.Off; int li = System.Array.IndexOf(map.layers, "dry"), slot = System.Array.IndexOf(ti.layers, li);
            if (slot < 0) { li = System.Array.IndexOf(map.layers, "sand"); slot = System.Array.IndexOf(ti.layers, li); } if (slot < 0) return;
            float ox = ti.i * map.tileSize - off.x, oz = ti.j * map.tileSize - off.y, cs = map.tileSize / (ares - 1);
            foreach (var s in AgniLandmarks.Table)
            {
                if (s.name == "VindhyaShikhar" || s.name == "KayalVillage") continue;
                float R = s.footR + 40f;
                int x0 = Mathf.Max(0, (int)((s.pos.x - R - ox) / cs)), x1 = Mathf.Min(ares - 1, (int)((s.pos.x + R - ox) / cs) + 1);
                int z0 = Mathf.Max(0, (int)((s.pos.z - R - oz) / cs)), z1 = Mathf.Min(ares - 1, (int)((s.pos.z + R - oz) / cs) + 1);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                    {
                        float wx = ox + x * cs, wz = oz + z * cs; float d = AgniIsland.KitEdgeDist(wx, wz); if (d > 7f) continue;
                        float n = Mathf.PerlinNoise(wx * 0.11f + 3.7f, wz * 0.11f + 1.3f);
                        float wd = (d <= 0f ? 0.62f : 0.62f * (1f - d / 7f) * (1f - d / 7f)) * (0.55f + n * 0.8f); wd = Mathf.Clamp01(wd);
                        for (int l = 0; l < nl; l++) am[z, x, l] *= 1f - wd; am[z, x, slot] += wd;
                    }
            }
        }

        void BuildWater()
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Sea"; sea.hideFlags = HideFlags.DontSave; sea.transform.SetParent(transform, false);
            var c = new Vector3(2048f - AgniIndia.Off.x, AgniIndia.SeaY, 2048f - AgniIndia.Off.y);
            sea.transform.position = c; sea.transform.localScale = new Vector3(1000, 1, 1000); sea.layer = 4;
            if (Application.isPlaying) Destroy(sea.GetComponent<Collider>()); else DestroyImmediate(sea.GetComponent<Collider>());
            var mr = sea.GetComponent<MeshRenderer>(); mr.sharedMaterial = waterMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            // wading floor: a hidden collider 1.3 m under the water so fighters wade instead of sinking to the sea bed (vehicles ignore this layer)
            var floor = new GameObject("WadeFloor") { hideFlags = HideFlags.DontSave }; floor.transform.SetParent(transform, false); floor.layer = 4;
            floor.transform.position = c + Vector3.down * 1.3f; var bc = floor.AddComponent<BoxCollider>(); bc.size = new Vector3(10000, 0.2f, 10000);
        }

        void BuildRoads()
        {
            var map = AgniIndia.Map; var off = AgniIndia.Off;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); var hv = new List<Vector3>(); var hu = new List<Vector2>(); var ht = new List<int>();
            foreach (var r in map.roads)
            {
                if (r.type == "rail") continue;
                bool hw = r.type == "highway"; var V = hw ? hv : verts; var U = hw ? hu : uvs; var Tt = hw ? ht : tris;
                var pts = new List<Vector2>(); for (int i = 0; i < r.pts.Length / 2; i++) { var p = new Vector2(r.pts[i * 2] - off.x, r.pts[i * 2 + 1] - off.y); if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 16f || i == r.pts.Length / 2 - 1) pts.Add(p); }
                if (pts.Count < 2) continue;
                float hwid = r.width * 0.5f, acc = 0; int start = V.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[Mathf.Max(0, i - 1)]; var b = pts[Mathf.Min(pts.Count - 1, i + 1)]; var dir = (b - a).normalized; var n = new Vector2(-dir.y, dir.x);
                    var L = pts[i] + n * hwid; var R = pts[i] - n * hwid;
                    float y = Mathf.Max(AgniIndia.H(pts[i].x, pts[i].y), AgniIndia.H(L.x, L.y), AgniIndia.H(R.x, R.y)) + 0.12f; y = Mathf.Max(y, AgniIndia.SeaY + 0.6f);
                    if (i > 0) acc += (pts[i] - pts[i - 1]).magnitude;
                    V.Add(new Vector3(L.x, y, L.y)); V.Add(new Vector3(R.x, y, R.y)); U.Add(new Vector2(0, acc / r.width)); U.Add(new Vector2(1, acc / r.width));
                    if (i > 0) { int k = start + i * 2; Tt.Add(k - 2); Tt.Add(k); Tt.Add(k - 1); Tt.Add(k - 1); Tt.Add(k); Tt.Add(k + 1); }
                }
            }
            MakeMesh("Roads", verts, uvs, tris, roadMat); MakeMesh("Highways", hv, hu, ht, highwayMat);
            if (map.bridges != null) foreach (var b in map.bridges)
                {
                    var c = AgniIndia.W(b.x, b.z, b.y - 0.15f); var rot = Quaternion.Euler(0, b.yaw, 0);
                    Box("BridgeDeck", c, new Vector3(b.width, 0.8f, b.len), rot, bridgeMat, true);
                    foreach (float sgn in new[] { -1f, 1f }) Box("BridgeRail", c + rot * new Vector3(sgn * (b.width * 0.5f - 0.2f), 0.85f, 0), new Vector3(0.3f, 0.9f, b.len), rot, bridgeMat, true);
                    for (float t = -b.len * 0.5f + 4; t < b.len * 0.5f - 3; t += 10f) { var pp = c + rot * new Vector3(0, 0, t); Box("BridgePier", new Vector3(pp.x, (pp.y - 4f) * 0.5f - 2f, pp.z), new Vector3(b.width * 0.7f, pp.y + 4f, 1.4f), rot, bridgeMat, false); }
                }
        }
        void MakeMesh(string n, List<Vector3> v, List<Vector2> u, List<int> t, Material m)
        {
            if (v.Count == 0) return;
            var mesh = new Mesh { name = n, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave }; mesh.SetVertices(v); mesh.SetUVs(0, u); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(n) { hideFlags = HideFlags.DontSave }; go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        void Box(string n, Vector3 c, Vector3 size, Quaternion rot, Material m, bool col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.hideFlags = HideFlags.DontSave; g.transform.SetParent(transform, false);
            g.transform.SetPositionAndRotation(c, rot); g.transform.localScale = size; g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!col) { if (Application.isPlaying) Destroy(g.GetComponent<Collider>()); else DestroyImmediate(g.GetComponent<Collider>()); }
        }

        // ------------------------------------------------------------ trees: Blender species by biome, deterministic (same colliders on server and clients)
        public static string Biome(float x, float z, float h)
        {
            float lat = AgniIndia.Lat(z), lon = AgniIndia.Lon(x);
            if (h > 70f || lat > 32.2f) return "mountain";
            if (lon < 73.6f && lat > 23.0f && lat < 30.2f) return "desert";
            if (lon > 89.5f) return "northeast";
            if (lat < 16.5f || (h < 6f && lat < 22.5f)) return "coast";
            if (AgniIndia.Layer(x, z, "dry") > 0.5f) return "dry";
            return "plains";
        }
        void PlantTrees(Terrain[,] terr)
        {
            if (trees == null || trees.Length == 0) return;
            var map = AgniIndia.Map; var off = AgniIndia.Off; var rnd = new System.Random(20261101);
            var lists = new Dictionary<Terrain, List<TreeInstance>>(); foreach (var t in terr) if (t != null) lists[t] = new List<TreeInstance>();
            var byBiome = new Dictionary<string, List<int>>();
            for (int i = 0; i < trees.Length; i++) { if (trees[i].prefab == null) continue; foreach (var b in trees[i].biome.Split(',')) { if (!byBiome.TryGetValue(b, out var l)) byBiome[b] = l = new List<int>(); for (int w = 0; w < Mathf.Max(1, Mathf.RoundToInt(trees[i].weight * 2)); w++) l.Add(i); } }
            var keepOut = new List<(Vector2 c, float r)>();
            foreach (var s in AgniLandmarks.Table) keepOut.Add((new Vector2(s.pos.x, s.pos.z), s.footR + 2f));
            foreach (var v in Agni.Net.AgniVillages.Layout()) keepOut.Add((v.c, v.r + 4f));
            int total = 0, target = Mathf.RoundToInt(16000 * treeDensity);
            void Add(float x, float z, int proto, float sc)
            {
                int ti = Mathf.FloorToInt((x + off.x) / map.tileSize), tj = Mathf.FloorToInt((z + off.y) / map.tileSize); if (ti < 0 || tj < 0 || ti >= map.tiles || tj >= map.tiles) return; var tt = terr[ti, tj]; if (tt == null) return;
                var o = tt.transform.position;
                lists[tt].Add(new TreeInstance { prototypeIndex = proto, position = new Vector3((x - o.x) / map.tileSize, 0, (z - o.z) / map.tileSize), widthScale = sc, heightScale = sc * (0.92f + (float)rnd.NextDouble() * 0.2f), color = Color.white, lightmapColor = Color.white, rotation = (float)rnd.NextDouble() * 6.28f });
                total++;
            }
            bool Ok(float x, float z, float pad)
            {
                float h = AgniIndia.H(x, z); if (h < 1.6f) return false;
                float sl = Mathf.Max(Mathf.Abs(AgniIndia.H(x + 2, z) - AgniIndia.H(x - 2, z)), Mathf.Abs(AgniIndia.H(x, z + 2) - AgniIndia.H(x, z - 2))) / 4f; if (sl > 0.7f) return false;
                if (AgniIndia.RoadDist(x, z) < 3f + pad) return false;
                var p = new Vector2(x, z); foreach (var k in keepOut) if ((k.c - p).sqrMagnitude < (k.r + pad) * (k.r + pad)) return false;
                return true;
            }
            int Pick(string biome, bool bush) { if (!byBiome.TryGetValue(biome, out var l) || l.Count == 0) byBiome.TryGetValue("plains", out l); if (l == null || l.Count == 0) return -1; for (int t = 0; t < 6; t++) { int i = l[rnd.Next(l.Count)]; if (trees[i].bush == bush) return i; } return l[rnd.Next(l.Count)]; }
            // 1) groves: clumps of one species (village groves, orchards, forest patches)
            for (int g = 0; g < 900 && total < target * 0.6f; g++)
            {
                float x = (float)rnd.NextDouble() * map.size - off.x, z = (float)rnd.NextDouble() * map.size - off.y; float h = AgniIndia.H(x, z); if (h < 2f) continue;
                string bio = Biome(x, z, h); if (bio == "desert" && rnd.NextDouble() > 0.25) continue;
                int sp = Pick(bio, false); if (sp < 0) continue; int n = rnd.Next(5, 18); float gr = 12f + (float)rnd.NextDouble() * 26f;
                for (int k = 0; k < n; k++) { float a = (float)rnd.NextDouble() * 6.28f, r = Mathf.Sqrt((float)rnd.NextDouble()) * gr; float px = x + Mathf.Cos(a) * r, pz = z + Mathf.Sin(a) * r; if (Ok(px, pz, 2f)) Add(px, pz, rnd.NextDouble() < 0.85 ? sp : Pick(bio, false), 0.85f + (float)rnd.NextDouble() * 0.3f); }
            }
            // 2) singles + bushes everywhere on land
            for (int k = 0; k < 200000 && total < target; k++)
            {
                float x = (float)rnd.NextDouble() * map.size - off.x, z = (float)rnd.NextDouble() * map.size - off.y; float h = AgniIndia.H(x, z); if (h < 1.8f) continue;
                string bio = Biome(x, z, h); if (bio == "desert" && rnd.NextDouble() > 0.15) continue;
                bool bush = rnd.NextDouble() < 0.35; int sp = Pick(bio, bush); if (sp < 0 || !Ok(x, z, bush ? 0.5f : 2f)) continue;
                Add(x, z, sp, 0.8f + (float)rnd.NextDouble() * 0.35f);
            }
            // 3) the living edge round every landmark and village: trees + bushes in a ring
            foreach (var k in keepOut)
                for (int i = 0; i < (k.r > 40 ? 90 : 40); i++)
                {
                    float a = (float)rnd.NextDouble() * 6.28f, r = k.r + 2f + (float)rnd.NextDouble() * 36f; float x = k.c.x + Mathf.Cos(a) * r, z = k.c.y + Mathf.Sin(a) * r;
                    float h = AgniIndia.H(x, z); bool bush = rnd.NextDouble() < 0.45; int sp = Pick(Biome(x, z, h), bush); if (sp < 0 || !Ok(x, z, bush ? 0.3f : 1.5f)) continue; Add(x, z, sp, 0.85f + (float)rnd.NextDouble() * 0.3f);
                }
            foreach (var kv in lists) kv.Key.terrainData.SetTreeInstances(kv.Value.ToArray(), true);
            foreach (var t in terr) if (t != null) { var c = t.GetComponent<TerrainCollider>(); c.enabled = false; c.enabled = true; }
            Debug.Log("[AgniIndiaTerrain] trees " + total);
        }

        // ------------------------------------------------------------ grass: Blender clumps as instanced terrain details, by biome + splat
        void PlantGrass(Terrain[,] terr, int q)
        {
            if (grass == null || grass.Length == 0) return;
            var map = AgniIndia.Map; var off = AgniIndia.Off; int dres = 512; var protos = new List<DetailPrototype>();
            foreach (var g in grass) protos.Add(new DetailPrototype { prototype = q >= 3 || g.prefabLow == null ? g.prefab : g.prefabLow, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit, minWidth = g.minH, maxWidth = g.maxH, minHeight = g.minH, maxHeight = g.maxH, noiseSpread = 0.3f, healthyColor = Color.white, dryColor = new Color(0.92f, 0.9f, 0.8f) });
            var rnd = new System.Random(1101);
            foreach (var t in terr)
            {
                if (t == null) continue; var td = t.terrainData; td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); td.SetDetailResolution(dres, 32); td.detailPrototypes = protos.ToArray(); // counts per 2 m cell, not coverage
                var o = t.transform.position; var layersD = new int[grass.Length][,]; for (int i = 0; i < grass.Length; i++) layersD[i] = new int[dres, dres];
                for (int z = 0; z < dres; z++) for (int x = 0; x < dres; x++)
                    {
                        float wx = o.x + (x + 0.5f) / dres * map.tileSize, wz = o.z + (z + 0.5f) / dres * map.tileSize; float h = AgniIndia.H(wx, wz); if (h < AgniIndia.SeaY + 0.45f) continue;
                        if (AgniIndia.RoadDist(wx, wz) < 0.4f) continue;
                        float kd = AgniIsland.KitEdgeDist(wx, wz); if (kd < 0.5f && AgniIsland.OnKitSurface(wx, wz)) continue;
                        float trample = kd > 12f ? 1f : Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(kd / 12f)); // worn ground right at a landmark's edge
                        bool nearWater = false; if (h < 4f) for (int k = 0; k < 8 && !nearWater; k++) { float a = k * 0.785f; nearWater = AgniIndia.H(wx + Mathf.Cos(a) * 9f, wz + Mathf.Sin(a) * 9f) < AgniIndia.SeaY + 0.15f; }
                        float town = kd < 40f ? 1f - Mathf.Clamp01((kd - 10f) / 30f) : 0f; // 1 right around a landmark: short lawn/meadow, no tall cover
                        string bio = Biome(wx, wz, h); float gl = AgniIndia.Layer(wx, wz, "grass"), dl = AgniIndia.Layer(wx, wz, "dry"), sl = AgniIndia.Layer(wx, wz, "sand"), snl = AgniIndia.Layer(wx, wz, "snow"), ml = AgniIndia.Layer(wx, wz, "mustard");
                        for (int i = 0; i < grass.Length; i++)
                        {
                            var g = grass[i]; float w = 0;
                            switch (g.biome)
                            {
                                case "meadow": w = gl * (bio == "desert" ? 0.1f : 1f); break;
                                case "lawn": w = gl * (0.5f + town * 1.2f); break;
                                case "dry": w = dl + (bio == "desert" ? 0.4f : 0f); break;
                                case "alpine": w = bio == "mountain" ? Mathf.Max(gl, 0.4f) * (1f - snl) : 0f; break;
                                case "flowers": w = (ml * 2f + gl * 0.15f); break;
                                case "tall": w = gl * (bio == "northeast" || bio == "plains" ? 0.25f : 0.08f) * (1f - town); break;
                                case "reeds": w = nearWater ? 0.8f * (1f - sl * 0.5f) : 0f; break;
                                case "kaans": w = (dl + gl) * 0.06f * (1f - town) + (nearWater ? 0.05f : 0f); break;
                                case "fern": w = bio == "northeast" || bio == "coast" ? gl * 0.3f : 0f; break;
                            }
                            w *= g.density; if (w <= 0.02f) continue;
                            float n = Mathf.PerlinNoise(wx * 0.02f + i * 7.1f, wz * 0.02f + i * 3.3f); // patches
                            float cnt = w * trample * (0.4f + n * 1.6f) * 3.2f;
                            int c = (int)cnt; if (rnd.NextDouble() < cnt - c) c++; layersD[i][z, x] = c;
                        }
                    }
                for (int i = 0; i < grass.Length; i++) td.SetDetailLayer(0, 0, i, layersD[i]);
            }
        }
    }
}
