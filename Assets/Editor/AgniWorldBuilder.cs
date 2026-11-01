using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Builds the Agni Royale India map blockout scene from TerrainSource/ (made by Tools/terrain_gen/agni_terrain_gen.py).
//   Menu: Agni Royale -> Rebuild map scene
//   Batch: Unity -batchmode -nographics -projectPath <proj> -executeMethod AgniWorldBuilder.BuildBatch -logFile <log>
public static class AgniWorldBuilder
{
    public const string ScenePath = "Assets/Agni/Scenes/AgniMap.unity";
    const string Gen = "Assets/Agni/Generated";
    static string SrcDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../TerrainSource"));

    [Serializable] public class Proj { public float lonc, latc, cosLat, metersPerDegLat; }
    [Serializable] public class TileInfo { public int i, j; public int[] layers; }
    [Serializable] public class Zone { public int id; public string name, inspired, kind; public float x, z, y, radius, lon, lat; }
    [Serializable] public class Spot { public string name, kind; public float x, z, y; }
    [Serializable] public class Road { public string name, type; public float width; public float[] pts; }
    [Serializable] public class Bridge { public string road; public float x, z, y, len, width, yaw; public bool rail; }
    [Serializable] public class MapData
    {
        public int size, tiles, tileSize, heightRes, alphaRes; public float @base, range, seaLevel;
        public string[] layers; public Proj projection; public string outline;
        public TileInfo[] tilesInfo; public Zone[] zones; public Spot[] spots; public Road[] roads; public Bridge[] bridges; public Road[] rivers;
    }

    static MapData map;
    static float[,] gh; // global heights (metres), [z,x], 2 m grid
    static Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static System.Random rng;

    public static void BuildBatch()
    {
        try { BuildAll(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static byte[] Gunzip(string path)
    {
        using (var fs = File.OpenRead(path))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var ms = new MemoryStream()) { gz.CopyTo(ms); return ms.ToArray(); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static float H(float x, float z)
    {
        int n = gh.GetLength(0);
        float fx = Mathf.Clamp(x / 2f, 0, n - 1.001f), fz = Mathf.Clamp(z / 2f, 0, n - 1.001f);
        int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
        float a = Mathf.Lerp(gh[iz, ix], gh[iz, ix + 1], tx), b = Mathf.Lerp(gh[iz + 1, ix], gh[iz + 1, ix + 1], tx);
        if (x < 0 || z < 0 || x > 4096 || z > 4096) return -8f;
        return Mathf.Lerp(a, b, tz);
    }

    static Material Mat(string key, Color c, float smooth = 0.15f, float metal = 0f)
    {
        if (mats.TryGetValue(key, out var m)) return m;
        string p = Gen + "/M_" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        mats[key] = m; return m;
    }

    public static void BuildAll()
    {
        rng = new System.Random(1947);
        mats.Clear();
        map = JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(SrcDir, "map.json")));
        EnsureFolder(Gen); EnsureFolder("Assets/Agni/Scenes");
        ConfigureTextures();
        var layers = MakeLayers();
        ConfigureUrp();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ls = new LightingSettings { bakedGI = false, realtimeGI = false, name = "AgniLighting" };
        string lsPath = Gen + "/AgniLighting.lighting";
        AssetDatabase.DeleteAsset(lsPath); AssetDatabase.CreateAsset(ls, lsPath);
        Lightmapping.lightingSettings = ls;

        BuildTerrain(layers);
        BuildWater();
        BuildLight();
        var roadsRoot = new GameObject("Roads");
        foreach (var r in map.roads) BuildRoad(r, roadsRoot.transform);
        var bRoot = new GameObject("Bridges");
        foreach (var b in map.bridges) BuildBridge(b, bRoot.transform);
        var zRoot = new GameObject("Zones");
        foreach (var z in map.zones) BuildZone(z, zRoot.transform);
        foreach (var s in map.spots) BuildSpot(s, zRoot.transform);
        PlantTrees();
        BuildCamera();

        foreach (var go in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (go.GetComponent<TextMesh>() == null && go.GetComponentInParent<AgniBillboard>() == null)
                GameObjectUtility.SetStaticEditorFlags(go.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[AgniWorld] scene built: " + ScenePath);
    }

    // ---------------------------------------------------------------- textures / layers
    static readonly string[] LayerNames = { "sand", "grass", "dry", "rock", "snow", "mustard" };
    static readonly float[] LayerTile = { 14f, 18f, 16f, 12f, 22f, 9f };

    static void ConfigureTextures()
    {
        foreach (var n in LayerNames)
        {
            string p = "Assets/Agni/Textures/" + n + ".png";
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { Debug.LogError("missing texture " + p); continue; }
            ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 2; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false; // alpha = smoothness (kept low: no shiny terrain)
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Agni/Kenney" }))
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (mi == null) continue;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.importAnimation = false; mi.addCollider = false;
            mi.SaveAndReimport();
        }
    }

    static TerrainLayer[] MakeLayers()
    {
        var res = new TerrainLayer[LayerNames.Length];
        for (int k = 0; k < LayerNames.Length; k++)
        {
            string p = Gen + "/L_" + LayerNames[k] + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, p); }
            l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Agni/Textures/" + LayerNames[k] + ".png");
            l.tileSize = new Vector2(LayerTile[k], LayerTile[k]);
            l.smoothness = LayerNames[k] == "snow" ? 0.3f : 0.0f; l.metallic = 0f;
            EditorUtility.SetDirty(l);
            res[k] = l;
        }
        return res;
    }

    static void ConfigureUrp()
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if (rp == null) { Debug.LogWarning("Mobile_RPAsset not found"); return; }
        rp.shadowDistance = 120f; rp.shadowCascadeCount = 1; rp.msaaSampleCount = 1; rp.supportsHDR = false;
        rp.renderScale = 1f;
        EditorUtility.SetDirty(rp);
    }

    // ---------------------------------------------------------------- terrain
    static void BuildTerrain(TerrainLayer[] layers)
    {
        int res = map.heightRes, ares = map.alphaRes, T = map.tiles;
        int gn = (res - 1) * T + 1;
        gh = new float[gn, gn];
        var terr = new Terrain[T, T];
        var root = new GameObject("Terrain");
        var tmat = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/M_Terrain.mat");
        if (tmat == null) { tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(tmat, Gen + "/M_Terrain.mat"); }
        foreach (var ti in map.tilesInfo)
        {
            byte[] hb = Gunzip(Path.Combine(SrcDir, $"h_{ti.i}_{ti.j}.raw.gz"));
            var hts = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int o = (z * res + x) * 2; float v = (hb[o] | (hb[o + 1] << 8)) / 65535f;
                    hts[z, x] = v; gh[ti.j * (res - 1) + z, ti.i * (res - 1) + x] = map.@base + v * map.range;
                }
            string p = Gen + $"/T_{ti.i}_{ti.j}.asset";
            AssetDatabase.DeleteAsset(p);
            var td = new TerrainData();
            AssetDatabase.CreateAsset(td, p);
            td.heightmapResolution = res;
            td.size = new Vector3(map.tileSize, map.range, map.tileSize);
            td.SetHeights(0, 0, hts);
            td.terrainLayers = ti.layers.Select(k => layers[k]).ToArray();
            td.alphamapResolution = ares;
            td.baseMapResolution = 256;
            td.SetDetailResolution(32, 16);
            byte[] sb = Gunzip(Path.Combine(SrcDir, $"s_{ti.i}_{ti.j}.raw.gz"));
            int nl = ti.layers.Length;
            var am = new float[ares, ares, nl];
            for (int l = 0; l < nl; l++)
                for (int z = 0; z < ares; z++)
                    for (int x = 0; x < ares; x++)
                        am[z, x, l] = sb[(l * ares + z) * ares + x] / 255f;
            td.SetAlphamaps(0, 0, am);
            td.SyncTexture(TerrainData.AlphamapTextureName);
            foreach (var at in td.alphamapTextures) if (at != null && !AssetDatabase.Contains(at)) AssetDatabase.AddObjectToAsset(at, td);
            EditorUtility.SetDirty(td);
            var chk = td.GetAlphamaps(256, 256, 1, 1);
            Debug.Log($"[AgniWorld] tile {ti.i},{ti.j} layers={nl} alphaTex={td.alphamapTextureCount} sample0={chk[0,0,0]:F2} tex0px={td.alphamapTextures[0].GetPixel(256,256)}");
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = $"Terrain_{ti.i}_{ti.j}";
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(ti.i * map.tileSize, map.@base, ti.j * map.tileSize);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = tmat;
            t.heightmapPixelError = 6; t.basemapDistance = 350; t.drawInstanced = true;
            t.treeDistance = 900; t.treeBillboardDistance = 5000; t.detailObjectDistance = 0;
            t.shadowCastingMode = ShadowCastingMode.Off;
            t.groupingID = 1; t.allowAutoConnect = true;
            terr[ti.i, ti.j] = t;
        }
        for (int i = 0; i < T; i++)
            for (int j = 0; j < T; j++)
                terr[i, j].SetNeighbors(i > 0 ? terr[i - 1, j] : null, j < T - 1 ? terr[i, j + 1] : null, i < T - 1 ? terr[i + 1, j] : null, j > 0 ? terr[i, j - 1] : null);
    }

    static void BuildWater()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Sea";
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = new Vector3(2048, map.seaLevel, 2048);
        go.transform.localScale = new Vector3(1400, 1, 1400);
        var r = go.GetComponent<MeshRenderer>();
        var wm = Mat("Water", new Color(0.16f, 0.52f, 0.72f), 0.6f);
        wm.SetFloat("_EnvironmentReflections", 0f); wm.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        r.sharedMaterial = wm;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void BuildLight()
    {
        var lg = new GameObject("Sun");
        var l = lg.AddComponent<Light>();
        l.type = LightType.Directional; l.color = new Color(1f, 0.95f, 0.86f); l.intensity = 1.25f;
        l.shadows = LightShadows.Soft; l.shadowStrength = 0.7f;
        lg.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = l;
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/M_Sky.mat");
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, Gen + "/M_Sky.mat"); }
        sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 0.9f);
        sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); sky.SetColor("_GroundColor", new Color(0.55f, 0.6f, 0.65f)); sky.SetFloat("_Exposure", 1.25f);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.58f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.33f, 0.31f, 0.28f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.70f, 0.80f, 0.90f);
        RenderSettings.fogStartDistance = 650f; RenderSettings.fogEndDistance = 4200f;
    }

    // ---------------------------------------------------------------- roads & bridges
    static void BuildRoad(Road r, Transform parent)
    {
        int n = r.pts.Length / 2; if (n < 2) return;
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        float hw = r.width * 0.5f, acc = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = new Vector2(r.pts[i * 2], r.pts[i * 2 + 1]);
            Vector2 a = new Vector2(r.pts[Mathf.Max(0, i - 1) * 2], r.pts[Mathf.Max(0, i - 1) * 2 + 1]);
            Vector2 b = new Vector2(r.pts[Mathf.Min(n - 1, i + 1) * 2], r.pts[Mathf.Min(n - 1, i + 1) * 2 + 1]);
            Vector2 dir = (b - a).normalized; Vector2 nrm = new Vector2(-dir.y, dir.x);
            Vector2 L = p + nrm * hw, R = p - nrm * hw;
            float y = Mathf.Max(H(p.x, p.y), H(L.x, L.y), H(R.x, R.y)) + 0.3f;
            y = Mathf.Max(y, map.seaLevel + 0.9f);
            if (i > 0) acc += (p - new Vector2(r.pts[(i - 1) * 2], r.pts[(i - 1) * 2 + 1])).magnitude;
            verts.Add(new Vector3(L.x, y, L.y)); verts.Add(new Vector3(R.x, y, R.y));
            uvs.Add(new Vector2(0, acc / r.width)); uvs.Add(new Vector2(1, acc / r.width));
            if (i > 0) { int k = i * 2; tris.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
        }
        var m = new Mesh { name = "Road_" + r.name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetTriangles(tris, 0); m.SetUVs(0, uvs); m.RecalculateNormals(); m.RecalculateBounds();
        string p2 = Gen + "/Road_" + r.name.Replace(' ', '_').Replace('&', 'n') + ".asset";
        AssetDatabase.DeleteAsset(p2); AssetDatabase.CreateAsset(m, p2);
        var go = new GameObject("Road " + r.name);
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = r.type == "highway" ? Mat("Highway", new Color(0.20f, 0.20f, 0.22f), 0.2f)
                         : r.type == "rail" ? Mat("Rail", new Color(0.30f, 0.22f, 0.16f), 0.1f)
                         : Mat("Road", new Color(0.42f, 0.38f, 0.33f), 0.1f);
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, float yaw = 0, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = pos + Vector3.up * size.y * 0.5f;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Cyl(Transform parent, string name, Vector3 pos, float diameter, float height, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = pos + Vector3.up * height * 0.5f;
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }
    static GameObject Ball(Transform parent, string name, Vector3 center, float d, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = Vector3.one * d;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static void BuildBridge(Bridge b, Transform parent)
    {
        var m = b.rail ? Mat("Steel", new Color(0.45f, 0.30f, 0.22f), 0.3f, 0.3f) : Mat("Concrete", new Color(0.62f, 0.61f, 0.58f), 0.1f);
        var root = new GameObject("Bridge (" + b.road + ")"); root.transform.SetParent(parent);
        var c = new Vector3(b.x, b.y - 0.6f, b.z);
        Box(root.transform, "Deck", c, new Vector3(b.width, 0.9f, b.len), m, b.yaw);
        var rot = Quaternion.Euler(0, b.yaw, 0);
        foreach (float s in new[] { -1f, 1f })
            Box(root.transform, "Rail", c + rot * new Vector3(s * (b.width * 0.5f - 0.2f), 0.9f, 0), new Vector3(0.35f, 1.0f, b.len), m, b.yaw, false);
        for (float t = -b.len * 0.5f + 4; t < b.len * 0.5f - 3; t += 10f)
        {
            var pp = c + rot * new Vector3(0, 0, t);
            Box(root.transform, "Pier", new Vector3(pp.x, -3f, pp.z), new Vector3(b.width * 0.7f, c.y + 3f, 1.4f), m, b.yaw, false);
        }
    }

    // ---------------------------------------------------------------- zones
    static float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static Color ZoneColor(string kind, int k)
    {
        switch (kind)
        {
            case "bazaar": return k % 2 == 0 ? new Color(0.90f, 0.55f, 0.55f) : new Color(0.93f, 0.66f, 0.58f);
            case "city": return new Color[] { new Color(0.78f, 0.40f, 0.30f), new Color(0.85f, 0.80f, 0.68f), new Color(0.70f, 0.55f, 0.42f) }[k % 3];
            case "ghat": return new Color[] { new Color(0.95f, 0.85f, 0.55f), new Color(0.90f, 0.62f, 0.35f), new Color(0.85f, 0.85f, 0.80f) }[k % 3];
            case "beach": case "water": return new Color[] { new Color(0.35f, 0.70f, 0.80f), new Color(0.95f, 0.80f, 0.35f), new Color(0.90f, 0.45f, 0.40f), new Color(0.95f, 0.95f, 0.90f) }[k % 4];
            case "village": case "snow": return new Color[] { new Color(0.62f, 0.60f, 0.56f), new Color(0.55f, 0.50f, 0.44f) }[k % 2];
            case "desert": return new Color(0.93f, 0.90f, 0.82f);
            case "farm": case "tea": return new Color[] { new Color(0.72f, 0.42f, 0.30f), new Color(0.80f, 0.70f, 0.55f) }[k % 2];
            default: return new Color[] { new Color(0.85f, 0.78f, 0.62f), new Color(0.78f, 0.55f, 0.40f), new Color(0.88f, 0.88f, 0.84f) }[k % 3];
        }
    }

    // a 1-2 storey flat-roof house with a parapet (classic rooftop-camping house)
    static void House(Transform parent, Vector3 p, float w, float d, int floors, Material wall, Material roof, float yaw)
    {
        float fh = 3.2f, h = floors * fh;
        Box(parent, "House", p + Vector3.down * 1.2f, new Vector3(w, h + 1.2f, d), wall, yaw);
        Box(parent, "Roof", p + Vector3.up * h, new Vector3(w + 0.3f, 0.35f, d + 0.3f), roof, yaw, false);
        var rot = Quaternion.Euler(0, yaw, 0);
        // parapet walls
        Box(parent, "Parapet", p + Vector3.up * h + rot * new Vector3(0, 0, d * 0.5f), new Vector3(w, 1.0f, 0.25f), wall, yaw, false);
        Box(parent, "Parapet", p + Vector3.up * h + rot * new Vector3(0, 0, -d * 0.5f), new Vector3(w, 1.0f, 0.25f), wall, yaw, false);
        // outside stair block to the roof for 2-storey houses
        if (floors >= 2) Box(parent, "Stairs", p + Vector3.down * 1.0f + rot * new Vector3(w * 0.5f + 0.7f, 0, 0), new Vector3(1.4f, h * 0.55f + 1.0f, d * 0.6f), roof, yaw);
    }

    static bool Buildable(float x, float z, float minH = 1.4f)
    {
        float c = H(x, z);
        if (c < minH) return false;
        float s = Mathf.Max(Mathf.Abs(H(x + 4, z) - H(x - 4, z)), Mathf.Abs(H(x, z + 4) - H(x, z - 4))) / 8f;
        return s < 0.35f;
    }

    static void Town(Transform parent, Zone z, int count, float spacing, Vector2Int floors, Vector2 size, float minR = 14f)
    {
        var roof = Mat("Roof", new Color(0.55f, 0.50f, 0.46f));
        int placed = 0, tries = 0;
        float yawBase = Rnd(0, 90);
        while (placed < count && tries < count * 12)
        {
            tries++;
            // grid-ish town: snapped positions on a rotated grid inside the pad
            float r = Mathf.Sqrt(Rnd(0, 1)) * (z.radius + 15f);
            float a = Rnd(0, Mathf.PI * 2);
            float gx = Mathf.Round(Mathf.Cos(a) * r / spacing) * spacing, gz = Mathf.Round(Mathf.Sin(a) * r / spacing) * spacing;
            var off = Quaternion.Euler(0, yawBase, 0) * new Vector3(gx, 0, gz);
            float x = z.x + off.x, zz = z.z + off.z;
            if (new Vector2(off.x, off.z).magnitude < minR) continue;
            if (parent.Cast<Transform>().Any(t => t.name == "House" && (new Vector2(t.position.x - x, t.position.z - zz)).magnitude < spacing * 0.8f)) continue;
            if (!Buildable(x, zz)) continue;
            float w = Rnd(size.x, size.y), d = Rnd(size.x, size.y);
            int fl = rng.Next(floors.x, floors.y + 1);
            House(parent, new Vector3(x, H(x, zz), zz), w, d, fl, Mat("Wall_" + z.kind + "_" + (placed % 3), ZoneColor(z.kind, placed)), roof, yawBase + (rng.Next(2) * 90));
            placed++;
        }
    }

    static void Arch(Transform p, Vector3 c, float w, float h, float depth, Material m, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        float pw = w * 0.22f;
        Box(p, "ArchPier", c + rot * new Vector3(-(w - pw) * 0.5f, 0, 0), new Vector3(pw, h, depth), m, yaw);
        Box(p, "ArchPier", c + rot * new Vector3((w - pw) * 0.5f, 0, 0), new Vector3(pw, h, depth), m, yaw);
        Box(p, "ArchTop", c + Vector3.up * h * 0.72f, new Vector3(w, h * 0.28f, depth), m, yaw);
        Box(p, "ArchCrown", c + Vector3.up * h, new Vector3(w * 0.6f, h * 0.12f, depth * 0.8f), m, yaw, false);
    }

    static void Signature(Transform p, Zone z)
    {
        Vector3 c = new Vector3(z.x, H(z.x, z.z), z.z);
        switch (z.id)
        {
            case 1: // Himgiri Ridge: base camp + watchtower + radio hut
                for (int i = 0; i < 6; i++) { float a = i * 1.05f; var tp = c + new Vector3(Mathf.Cos(a) * 12, 0, Mathf.Sin(a) * 12); Box(p, "Tent", tp, new Vector3(3, 2.2f, 4), Mat("Tent", new Color(0.9f, 0.35f, 0.2f)), i * 30); }
                Box(p, "RadioHut", c + new Vector3(-20, 0, 8), new Vector3(5, 3, 4), Mat("Hut", new Color(0.5f, 0.45f, 0.4f)));
                Watchtower(p, c + new Vector3(18, 0, -10), 14);
                for (int i = 0; i < 4; i++) Box(p, "Bunting", c + new Vector3(-12 + i * 8, 4f, 0), new Vector3(0.15f, 0.15f, 18), Mat("Bunting" + i, Color.HSVToRGB(i * 0.22f, 0.8f, 0.95f)), 0, false);
                break;
            case 2: // Sarson Khet: farmhouses, haystacks, pump house
                for (int i = 0; i < 6; i++) Cyl(p, "Haystack", c + new Vector3(Rnd(-35, 35), -0.3f, Rnd(-35, 35)), 4, 3.5f, Mat("Hay", new Color(0.85f, 0.70f, 0.30f)));
                Box(p, "PumpHouse", c + new Vector3(8, 0, 8), new Vector3(4, 3, 4), Mat("Brick", new Color(0.70f, 0.38f, 0.28f)));
                break;
            case 3: // Vijay Dwar Chowk: war arch at a roundabout + red fort wall
                Arch(p, c, 16, 22, 5, Mat("Sandstone", new Color(0.82f, 0.68f, 0.52f)), 0);
                Cyl(p, "Roundabout", c + Vector3.down * 0.6f, 44, 0.9f, Mat("Lawn", new Color(0.35f, 0.55f, 0.25f)));
                var red = Mat("RedFort", new Color(0.68f, 0.28f, 0.20f));
                Box(p, "FortWall", c + new Vector3(0, 0, 58), new Vector3(80, 9, 3), red);
                Cyl(p, "FortTower", c + new Vector3(-40, 0, 58), 7, 13, red); Cyl(p, "FortTower", c + new Vector3(40, 0, 58), 7, 13, red);
                Arch(p, c + new Vector3(0, 0, 58), 12, 14, 4, red, 0);
                break;
            case 4: // Gulabi Bazaar: pink honeycomb facade
                var pink = Mat("Pink", new Color(0.92f, 0.52f, 0.50f));
                Box(p, "PinkFacade", c, new Vector3(36, 16, 4), pink);
                for (int i = 0; i < 5; i++) Box(p, "FacadeTier", c + new Vector3(0, 16 + i * 1.6f, 0), new Vector3(30 - i * 6, 1.6f, 3.5f), pink, 0, false);
                break;
            case 5: // Sangmarmar Bagh: white dome monument (not enterable) + long pool + red gate
                var marble = Mat("Marble", new Color(0.95f, 0.95f, 0.93f), 0.4f);
                Box(p, "Plinth", c + new Vector3(0, 0, 30), new Vector3(34, 3, 34), marble);
                Box(p, "Tomb", c + new Vector3(0, 3, 30), new Vector3(20, 14, 20), marble);
                Ball(p, "Dome", c + new Vector3(0, 19, 30), 15, marble);
                foreach (var q in new[] { new Vector3(-16, 0, 14), new Vector3(16, 0, 14), new Vector3(-16, 0, 46), new Vector3(16, 0, 46) }) Cyl(p, "Minaret", c + q, 2.2f, 24, marble);
                Box(p, "Pool", c + new Vector3(0, -0.2f, -10), new Vector3(5, 0.4f, 46), Mat("PoolWater", new Color(0.2f, 0.5f, 0.7f), 0.9f), 0, false);
                Arch(p, c + new Vector3(0, 0, -40), 14, 12, 5, Mat("RedFort", new Color(0.68f, 0.28f, 0.20f)), 0);
                break;
            case 6: // Ghat Nagari: stepped ghats toward the river + tall narrow houses
                var step = Mat("GhatStone", new Color(0.80f, 0.70f, 0.55f));
                Vector2 toRiver = DownhillDir(z.x, z.z);
                float yawR = Mathf.Atan2(toRiver.x, toRiver.y) * Mathf.Rad2Deg;
                for (int i = 0; i < 6; i++) { var sp = c + new Vector3(toRiver.x, 0, toRiver.y) * (20 + i * 3) + Vector3.down * (i * 0.6f); Box(p, "GhatStep", sp + Vector3.down * 2f, new Vector3(50, 2.5f, 3.2f), step, yawR); }
                break;
            case 7: // Ghanta Ghar Square: climbable clock tower in a square, trams
                var col = Mat("Colonial", new Color(0.75f, 0.32f, 0.25f));
                Box(p, "Square", c + Vector3.down * 0.7f, new Vector3(40, 0.9f, 40), Mat("Paving", new Color(0.6f, 0.58f, 0.55f)));
                Box(p, "ClockTower", c, new Vector3(5, 26, 5), col);
                Box(p, "ClockFace", c + Vector3.up * 21, new Vector3(5.6f, 3.2f, 5.6f), Mat("ClockFace", new Color(0.95f, 0.93f, 0.85f)), 0, false);
                Box(p, "TowerCap", c + Vector3.up * 26, new Vector3(3.5f, 4, 3.5f), col, 0, false);
                for (int i = 0; i < 3; i++) Box(p, "Taxi", c + new Vector3(-14 + i * 6, 0, -24), new Vector3(1.8f, 1.5f, 4), Mat("Taxi", new Color(0.95f, 0.80f, 0.10f)), 90);
                break;
            case 8: // Chai Bagan: tea rows + stilt huts
                var tea = Mat("Tea", new Color(0.20f, 0.45f, 0.18f));
                for (int i = -5; i <= 5; i++) { var rp = c + new Vector3(i * 6f, 0, 40); if (Buildable(rp.x, rp.z)) Box(p, "TeaRow", rp + Vector3.down * 0.3f, new Vector3(2.2f, 1.4f, 40), tea, 0, false); }
                for (int i = 0; i < 4; i++) StiltHut(p, c + new Vector3(-24 + i * 14, 0, -14));
                break;
            case 9: // Rann Safed: round mud huts + wind turbines
                for (int i = 0; i < 8; i++) { float a = i * 0.785f; var hp = c + new Vector3(Mathf.Cos(a) * 16, 0, Mathf.Sin(a) * 16); Cyl(p, "Bhunga", hp + Vector3.down * 0.5f, 5, 3.4f, Mat("Mud", new Color(0.93f, 0.88f, 0.78f))); Ball(p, "BhungaRoof", hp + Vector3.up * 3f, 5.6f, Mat("Thatch", new Color(0.65f, 0.52f, 0.32f))); }
                for (int i = 0; i < 3; i++) Turbine(p, c + new Vector3(-50 + i * 50, 0, 60));
                break;
            case 10: // Vindhya Shikhar: lone 2-storey haveli, water tank, radio mast
                var hav = Mat("Haveli", new Color(0.85f, 0.65f, 0.45f));
                House(p, c, 14, 11, 2, hav, Mat("Roof", new Color(0.55f, 0.50f, 0.46f)), 15);
                Cyl(p, "TankLegs", c + new Vector3(16, 0, 6), 1.2f, 9, Mat("Steel", new Color(0.45f, 0.30f, 0.22f), 0.3f, 0.3f));
                Cyl(p, "WaterTank", c + new Vector3(16, 9, 6), 6, 4, Mat("Tank", new Color(0.85f, 0.85f, 0.82f)));
                Cyl(p, "RadioMast", c + new Vector3(-14, 0, -8), 0.8f, 34, Mat("Mast", new Color(0.85f, 0.25f, 0.20f)));
                break;
            case 11: // Pathar Gaon: stone huts + well + small temple exterior
                Cyl(p, "Well", c + Vector3.down * 0.4f, 3, 1.4f, Mat("Stone", new Color(0.55f, 0.53f, 0.50f)));
                Box(p, "TempleBase", c + new Vector3(18, 0, 12), new Vector3(7, 4, 7), Mat("Stone", new Color(0.55f, 0.53f, 0.50f)));
                Cyl(p, "TempleSpire", c + new Vector3(18, 4, 12), 4.5f, 8, Mat("Stone", new Color(0.55f, 0.53f, 0.50f)));
                break;
            case 12: // Bambai Docks & Mill: containers, cranes, red-brick mill with chimney (roof reachable only by parachute)
                var brick = Mat("Brick", new Color(0.70f, 0.38f, 0.28f));
                Box(p, "MillHall", c + new Vector3(-30, 0, 20), new Vector3(42, 13, 24), brick);
                Box(p, "MillRoof", c + new Vector3(-30, 13, 20), new Vector3(43, 0.5f, 25), Mat("Roof", new Color(0.55f, 0.50f, 0.46f)), 0, false);
                Cyl(p, "MillChimney", c + new Vector3(-6, 0, 30), 3.2f, 34, brick);
                for (int i = 0; i < 18; i++) { var cp = c + new Vector3(10 + (i % 6) * 7, (i / 6) * 2.6f, -16 + (i / 6 % 2) * 3); Box(p, "Container", cp, new Vector3(2.4f, 2.6f, 6), Mat("Container" + (i % 4), Color.HSVToRGB((i % 4) * 0.27f, 0.7f, 0.75f)), 90); }
                Crane(p, c + new Vector3(30, 0, -34)); Crane(p, c + new Vector3(52, 0, -34));
                Arch(p, c + new Vector3(-10, 0, -44), 14, 16, 6, Mat("Basalt", new Color(0.62f, 0.56f, 0.48f)), 0);
                break;
            case 13: // Char Minar Chowk: four-minaret arch at a crossroads
                var lime = Mat("Lime", new Color(0.88f, 0.84f, 0.74f));
                Box(p, "CharMinarBody", c, new Vector3(16, 14, 16), lime);
                foreach (var q in new[] { new Vector3(-8, 0, -8), new Vector3(8, 0, -8), new Vector3(-8, 0, 8), new Vector3(8, 0, 8) }) { Cyl(p, "Minar", c + q, 2.6f, 28, lime); Ball(p, "MinarTop", c + q + Vector3.up * 28.5f, 3, lime); }
                Arch(p, c + new Vector3(0, 0, -8.6f), 8, 10, 1.4f, Mat("ArchShade", new Color(0.6f, 0.55f, 0.5f)), 0);
                break;
            case 14: // Sunehri Beach: lighthouse, shacks
                Cyl(p, "Lighthouse", c + new Vector3(-10, 0, -30), 5, 22, Mat("LighthouseRed", new Color(0.85f, 0.18f, 0.15f)));
                Cyl(p, "LighthouseBand", c + new Vector3(-10, 10, -30), 5.2f, 3, Mat("White", new Color(0.95f, 0.95f, 0.92f)));
                Cyl(p, "Lamp", c + new Vector3(-10, 22, -30), 3.4f, 3, Mat("Lamp", new Color(1f, 0.92f, 0.6f), 0.8f));
                for (int i = 0; i < 6; i++) Box(p, "Shack", c + new Vector3(-30 + i * 9, 0, -12), new Vector3(5, 3, 4), Mat("Shack", new Color(0.70f, 0.55f, 0.35f)));
                break;
            case 15: // Rajmahal: grand palace + domes + courtyard walls
                var pal = Mat("Palace", new Color(0.90f, 0.80f, 0.62f));
                var dome = Mat("PalaceDome", new Color(0.75f, 0.45f, 0.35f));
                Box(p, "Palace", c + new Vector3(0, 0, 18), new Vector3(52, 14, 22), pal);
                Box(p, "PalaceCentre", c + new Vector3(0, 0, 18), new Vector3(16, 22, 16), pal);
                Ball(p, "PalaceDome", c + new Vector3(0, 23, 18), 12, dome);
                foreach (float sx in new[] { -24f, 24f }) { Cyl(p, "CornerTower", c + new Vector3(sx, 0, 18), 6, 20, pal); Ball(p, "CornerDome", c + new Vector3(sx, 21, 18), 6, dome); }
                Box(p, "CourtWallS", c + new Vector3(0, 0, -30), new Vector3(90, 4, 1.5f), pal); Box(p, "CourtWallW", c + new Vector3(-45, 0, -6), new Vector3(1.5f, 4, 48), pal); Box(p, "CourtWallE", c + new Vector3(45, 0, -6), new Vector3(1.5f, 4, 48), pal);
                break;
            case 16: // Kayal Village: stilt houses + houseboats on the water
                for (int i = 0; i < 5; i++) StiltHut(p, c + new Vector3(-28 + i * 13, 0, -24));
                for (int i = 0; i < 4; i++) { Vector2 dn = DownhillDir(z.x, z.z); var bp = c + new Vector3(dn.x, 0, dn.y) * (70 + i * 8) + new Vector3(i * 9 - 14, 0, 0); bp.y = 0.1f; Box(p, "Houseboat", bp, new Vector3(3, 2.2f, 12), Mat("Boat", new Color(0.45f, 0.30f, 0.18f)), i * 20, false); }
                break;
        }
    }

    static Vector2 DownhillDir(float x, float z)
    {
        Vector2 best = Vector2.up; float bh = float.MaxValue;
        for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); float h = H(x + d.x * 90, z + d.y * 90); if (h < bh) { bh = h; best = d; } }
        return best;
    }
    static void Watchtower(Transform p, Vector3 c, float h)
    {
        var w = Mat("Wood", new Color(0.45f, 0.32f, 0.2f));
        foreach (var q in new[] { new Vector3(-1.5f, 0, -1.5f), new Vector3(1.5f, 0, -1.5f), new Vector3(-1.5f, 0, 1.5f), new Vector3(1.5f, 0, 1.5f) }) Box(p, "TowerLeg", c + q, new Vector3(0.35f, h, 0.35f), w, 0, false);
        Box(p, "TowerDeck", c + Vector3.up * h, new Vector3(4.2f, 0.4f, 4.2f), w);
        Box(p, "TowerRoof", c + Vector3.up * (h + 2.6f), new Vector3(4.6f, 0.3f, 4.6f), w, 0, false);
    }
    static void StiltHut(Transform p, Vector3 c)
    {
        var bam = Mat("Bamboo", new Color(0.70f, 0.60f, 0.38f));
        foreach (var q in new[] { new Vector3(-2, 0, -2), new Vector3(2, 0, -2), new Vector3(-2, 0, 2), new Vector3(2, 0, 2) }) Box(p, "Stilt", c + q + Vector3.down * 2, new Vector3(0.3f, 4.5f, 0.3f), bam, 0, false);
        Box(p, "HutBody", c + Vector3.up * 2.5f, new Vector3(5, 2.6f, 5), bam);
        Box(p, "HutRoof", c + Vector3.up * 5.1f, new Vector3(6, 0.6f, 6), Mat("Thatch", new Color(0.65f, 0.52f, 0.32f)), 0, false);
    }
    static void Turbine(Transform p, Vector3 c)
    {
        var wt = Mat("White", new Color(0.95f, 0.95f, 0.92f));
        Cyl(p, "TurbineMast", c, 1.2f, 30, wt);
        for (int i = 0; i < 3; i++) { var b = Box(p, "Blade", c + Vector3.up * 30, new Vector3(0.6f, 11, 0.25f), wt, 0, false); b.transform.position = c + Vector3.up * 30 + Quaternion.Euler(0, 0, i * 120) * Vector3.up * 5.5f + Vector3.back * 0.8f; b.transform.rotation = Quaternion.Euler(0, 0, i * 120); }
    }
    static void Crane(Transform p, Vector3 c)
    {
        var y = Mat("CraneYellow", new Color(0.95f, 0.70f, 0.10f));
        Box(p, "CraneLegL", c + new Vector3(-5, 0, 0), new Vector3(1, 22, 1), y, 0, false); Box(p, "CraneLegR", c + new Vector3(5, 0, 0), new Vector3(1, 22, 1), y, 0, false);
        Box(p, "CraneBoom", c + new Vector3(0, 22, -6), new Vector3(12, 1.6f, 28), y, 0, false);
    }

    static void Label(Transform parent, string text, string sub, Vector3 pos, bool big)
    {
        var lg = new GameObject("Label " + text);
        lg.transform.SetParent(parent, false);
        lg.transform.position = pos;
        var bb = lg.AddComponent<AgniBillboard>();
        bb.baseHeight = 4f; bb.minWorldHeight = big ? 8f : 6f; bb.screenFraction = big ? 0.034f : 0.026f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        TextMesh Make(string name, Color c, Vector3 off, int size, string t)
        {
            var g = new GameObject(name); g.transform.SetParent(lg.transform, false); g.transform.localPosition = off;
            var tm = g.AddComponent<TextMesh>();
            tm.text = t; tm.font = font; tm.fontSize = size; tm.characterSize = 4f / (size * 0.1f) * 0.8f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = c; tm.fontStyle = FontStyle.Bold;
            g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }
        string full = sub != null ? text + "\n<size=40>" + sub + "</size>" : text;
        // dark outline copies behind the white text
        float o = 0.09f;
        foreach (var d in new[] { new Vector3(o, 0, 0), new Vector3(-o, 0, 0), new Vector3(0, o, 0), new Vector3(0, -o, 0) })
            Make("Shadow", new Color(0.05f, 0.05f, 0.05f, 1f), d + new Vector3(0, 0, 0.05f), 64, full).richText = true;
        Make("Text", big ? new Color(1f, 0.93f, 0.55f) : Color.white, Vector3.zero, 64, full).richText = true;
    }

    static void Marker(Transform parent, Vector3 basePos, float height)
    {
        Cyl(parent, "MarkerPole", basePos, 0.9f, height, Mat("Marker", new Color(1f, 0.45f, 0.05f), 0.5f)).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Ball(parent, "MarkerBeacon", basePos + Vector3.up * height, 3.2f, Mat("Beacon", new Color(1f, 0.6f, 0.1f), 0.8f));
    }

    static void BuildZone(Zone z, Transform root)
    {
        var p = new GameObject($"Zone {z.id:00} {z.name}").transform; p.SetParent(root);
        var c = new Vector3(z.x, H(z.x, z.z), z.z);
        p.position = c;
        Signature(p, z);
        switch (z.kind)
        {
            case "city": Town(p, z, 34, 12, new Vector2Int(2, 4), new Vector2(7, 11), 26); break;
            case "bazaar": Town(p, z, 30, 10, new Vector2Int(2, 3), new Vector2(6, 9), 14); break;
            case "ghat": Town(p, z, 30, 8, new Vector2Int(3, 4), new Vector2(5, 7), 12); break;
            case "docks": Town(p, z, 18, 12, new Vector2Int(2, 3), new Vector2(7, 10), 55); break;
            case "garden": Town(p, z, 18, 11, new Vector2Int(1, 2), new Vector2(6, 9), 55); break;
            case "palace": Town(p, z, 16, 12, new Vector2Int(1, 2), new Vector2(7, 9), 55); break;
            case "peak": break;
            case "snow": Town(p, z, 6, 12, new Vector2Int(1, 1), new Vector2(5, 7), 25); break;
            case "desert": Town(p, z, 8, 12, new Vector2Int(1, 1), new Vector2(5, 7), 25); break;
            case "tea": Town(p, z, 12, 12, new Vector2Int(1, 2), new Vector2(6, 8), 22); break;
            case "water": Town(p, z, 14, 10, new Vector2Int(1, 2), new Vector2(6, 8), 22); break;
            default: Town(p, z, 20, 11, new Vector2Int(1, 2), new Vector2(6, 9), 16); break;
        }
        float top = c.y + 42f;
        Marker(p, c + new Vector3(0, 0, -z.radius * 0.0f) + new Vector3(z.radius * 0.35f, 0, -z.radius * 0.35f), 36f);
        float lx = 0f, lz = 0f;   // nudge labels that would overlap from the drop height
        if (z.id == 4) lx = -85f; if (z.id == 5) lx = 75f; if (z.id == 11) lx = -45f; if (z.id == 10) { lx = 50f; lz = 25f; }
        Label(p, z.name, z.inspired, new Vector3(z.x + lx, top, z.z + lz), true);
    }

    static void BuildSpot(Spot s, Transform root)
    {
        var p = new GameObject("Spot " + s.name).transform; p.SetParent(root);
        var c = new Vector3(s.x, H(s.x, s.z), s.z); p.position = c;
        var wall = Mat("SpotWall", new Color(0.66f, 0.64f, 0.60f));
        switch (s.kind)
        {
            case "shed": Box(p, "Godown", c, new Vector3(36, 10, 18), Mat("Godown", new Color(0.72f, 0.70f, 0.64f))); Box(p, "GodownRoof", c + Vector3.up * 10, new Vector3(37, 0.6f, 19), Mat("Roof", new Color(0.55f, 0.50f, 0.46f)), 0, false); break;
            case "mast": Cyl(p, "RadioMast", c, 1f, 40, Mat("Mast", new Color(0.85f, 0.25f, 0.20f))); Box(p, "MastHut", c + new Vector3(6, 0, 0), new Vector3(5, 3, 4), wall); break;
            case "power":
                foreach (var q in new[] { new Vector3(-14, 0, 0), new Vector3(14, 0, 0) }) Cyl(p, "CoolingTower", c + q, 18, 26, Mat("Cooling", new Color(0.78f, 0.78f, 0.76f)));
                Box(p, "Turbine Hall", c + new Vector3(0, 0, 22), new Vector3(30, 12, 14), wall); Cyl(p, "Stack", c + new Vector3(18, 0, 26), 3, 40, wall); break;
            case "airstrip":
                float yaw = 25f;
                Box(p, "Runway", c + Vector3.down * 0.5f, new Vector3(14, 0.8f, 150), Mat("Highway", new Color(0.20f, 0.20f, 0.22f), 0.2f), yaw);
                Box(p, "Hangar", c + Quaternion.Euler(0, yaw, 0) * new Vector3(22, 0, 20), new Vector3(24, 10, 30), Mat("Hangar", new Color(0.55f, 0.60f, 0.55f)), yaw); break;
            case "ruins":
                var st = Mat("Ruin", new Color(0.66f, 0.55f, 0.45f));
                for (int i = 0; i < 12; i++) Box(p, "RuinWall", c + new Vector3(Rnd(-25, 25), -0.5f, Rnd(-25, 25)), new Vector3(Rnd(3, 9), Rnd(1.5f, 5), 1.2f), st, rng.Next(4) * 90);
                for (int i = 0; i < 6; i++) Cyl(p, "RuinPillar", c + new Vector3(-10 + i * 4, 0, 10), 1.2f, Rnd(3, 6), st); break;
        }
        Marker(p, c + new Vector3(10, 0, -10), 26f);
        Label(p, s.name, null, c + Vector3.up * 34f + (s.name == "Anaj Godown" ? new Vector3(40, 0, -30) : s.name == "Bijli Ghar" ? new Vector3(-30, 0, 0) : Vector3.zero), false);
    }

    // ---------------------------------------------------------------- trees (terrain tree instances)
    static void PlantTrees()
    {
        string[] names = { "tree_palmTall", "tree_pineTallA", "tree_oak", "tree_default", "tree_fat" };
        var protos = new List<TreePrototype>();
        foreach (var n in names)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Agni/Kenney/" + n + ".fbx");
            if (go == null) { Debug.LogWarning("tree model missing: " + n); continue; }
            // recolour Kenney's teal leaves to Indian greens and save as our own prefab
            var inst = (GameObject)UnityEngine.Object.Instantiate(go);
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var sm = mr.sharedMaterials;
                for (int k = 0; k < sm.Length; k++)
                {
                    string mn = sm[k] != null ? sm[k].name.ToLower() : "";
                    if (mn.Contains("leaf")) sm[k] = n.Contains("palm") ? Mat("TreePalm", new Color(0.30f, 0.55f, 0.18f)) : mn.Contains("dark") ? Mat("TreeLeafDark", new Color(0.13f, 0.36f, 0.12f)) : Mat("TreeLeaf", new Color(0.24f, 0.48f, 0.16f));
                    else sm[k] = Mat("TreeWood", new Color(0.42f, 0.28f, 0.17f));
                }
                mr.sharedMaterials = sm;
            }
            string pp = Gen + "/Tree_" + n + ".prefab";
            var pf = PrefabUtility.SaveAsPrefabAsset(inst, pp);
            UnityEngine.Object.DestroyImmediate(inst);
            protos.Add(new TreePrototype { prefab = pf });
        }
        if (protos.Count < 5) { Debug.LogWarning("skipping trees"); return; }
        var lists = new Dictionary<Terrain, List<TreeInstance>>();
        var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (var t in terrains) { t.terrainData.treePrototypes = protos.ToArray(); lists[t] = new List<TreeInstance>(); }
        var zonePts = map.zones.Select(z => new Vector2(z.x, z.z)).ToArray();
        var roadPts = new List<Vector2>();
        foreach (var r in map.roads) for (int i = 0; i < r.pts.Length / 2; i += 2) roadPts.Add(new Vector2(r.pts[i * 2], r.pts[i * 2 + 1]));
        int total = 0;
        for (int k = 0; k < 60000 && total < 5200; k++)
        {
            float x = Rnd(0, 4096), z = Rnd(0, 4096);
            float h = H(x, z);
            if (h < 2.2f || h > 85f) continue;
            float s = Mathf.Max(Mathf.Abs(H(x + 3, z) - H(x - 3, z)), Mathf.Abs(H(x, z + 3) - H(x, z - 3))) / 6f;
            if (s > 0.45f) continue;
            var p2 = new Vector2(x, z);
            bool nearZone = false; foreach (var zp in zonePts) if ((zp - p2).sqrMagnitude < 95 * 95) { nearZone = true; break; }
            if (nearZone) continue;
            bool nearRoad = false; for (int i = 0; i < roadPts.Count; i += 3) if ((roadPts[i] - p2).sqrMagnitude < 64) { nearRoad = true; break; }
            if (nearRoad) continue;
            // biome by latitude-ish (z) and longitude (x)
            float lat = map.projection.latc + (z - 2048) / map.projection.metersPerDegLat;
            float lon = map.projection.lonc + (x - 2048) / (map.projection.cosLat * map.projection.metersPerDegLat);
            int proto;
            if (lon < 73.6f && lat > 23.3f && lat < 30.2f) { if (rng.NextDouble() > 0.12) continue; proto = 4; }        // Thar: very sparse
            else if (h > 40f) proto = 1;                                                                              // hills: pines
            else if (lat < 16.5f || h < 5f && (lat < 22.5f)) proto = rng.NextDouble() < 0.6 ? 0 : 2;                  // south/coast: palms
            else if (lon > 89.5f) proto = rng.NextDouble() < 0.5 ? 2 : 3;                                             // north-east: lush
            else { if (rng.NextDouble() > 0.55) continue; proto = rng.Next(2, 5); }
            Terrain tt = null; foreach (var t in terrains) { var o = t.transform.position; if (x >= o.x && x < o.x + 1024 && z >= o.z && z < o.z + 1024) { tt = t; break; } }
            if (tt == null) continue;
            var to = tt.transform.position;
            float sc = Rnd(4.5f, 7.5f);
            lists[tt].Add(new TreeInstance { prototypeIndex = proto, position = new Vector3((x - to.x) / 1024f, 0, (z - to.z) / 1024f), widthScale = sc, heightScale = sc * Rnd(0.9f, 1.2f), color = Color.white, lightmapColor = Color.white, rotation = Rnd(0, 6.28f) });
            total++;
        }
        foreach (var t in terrains) { t.terrainData.SetTreeInstances(lists[t].ToArray(), true); EditorUtility.SetDirty(t.terrainData); }
        Debug.Log("[AgniWorld] trees planted: " + total);
    }

    // ---------------------------------------------------------------- camera
    static void BuildCamera()
    {
        var cg = new GameObject("Main Camera"); cg.tag = "MainCamera";
        var cam = cg.AddComponent<Camera>();
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 4200f; cam.fieldOfView = 60f;
        cg.AddComponent<UniversalAdditionalCameraData>();
        var fc = cg.AddComponent<AgniFlyCam>();
        var pts = map.zones.Select(z => new Vector3(z.x, H(z.x, z.z), z.z)).ToList();
        var nms = map.zones.Select(z => z.name).ToList();
        foreach (var s in map.spots) { pts.Add(new Vector3(s.x, H(s.x, s.z), s.z)); nms.Add(s.name); }
        fc.zonePoints = pts.ToArray(); fc.zoneNames = nms.ToArray();
        cg.transform.position = new Vector3(1850, 620, 1250);
        cg.transform.rotation = Quaternion.Euler(42f, -12f, 0);
    }
}
