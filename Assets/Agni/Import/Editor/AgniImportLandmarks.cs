// Agni Royale — IMPORT v1: Blender landmark kit -> Unity prefabs (worker 3ecf9325, 2026-11-01).
// Source: landmarks/<Name>/<Name>.fbx (read-only copies in Assets/Agni/Import/Landmarks). Per landmark:
//   materials rebuilt from the Blender matdefs (AgniRoyale/Landmark shader: albedo x tint x vertex-colour variation/grime/AO + normal map),
//   one LODGroup per part from <Part>_LOD0/1/2, UCX_* hulls -> convex MeshColliders (meshes copied into a collision asset), clock hands -> AgniClockHands,
//   Ghanta Ghar finial cut off the tower top and replaced by our weathervane (no religious symbols).
//   Unity -batchmode -nographics -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportLandmarks.Batch -logFile <log>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Agni.Import.EditorTools
{
    public static class AgniImportLandmarks
    {
        public const string LM = "Assets/Agni/Import/Landmarks/";
        public const string RES = "Assets/Agni/Import/Resources/AgniImport/Landmarks/";
        struct MD { public string tex; public Color tint; public float rough, metal, grime, hue, norm; public MD(string t, float r, float g, float b, float rough = 0.8f, float metal = 0, float grime = 0.6f, float hue = 0.06f, float norm = 1f) { tex = t; tint = new Color(r, g, b); this.rough = rough; this.metal = metal; this.grime = grime; this.hue = hue; this.norm = norm; } }
        static MD P(float r, float g, float b, float rough = 0.75f) => new MD(null, r, g, b, rough, 0, 0, 0.08f, 0) ; // procedural (Blender linear colour, no texture)

        public static readonly string[] Names = { "GhantaGharSquare", "VindhyaShikhar", "BambaiMill", "ChaiBagan", "KayalVillage", "PatharGaon" };

        static Dictionary<string, MD> Defs(string lm)
        {
            var d = new Dictionary<string, MD>();
            switch (lm)
            {
                case "GhantaGharSquare":
                    d["Brick"] = new MD("Bricks059", 0.95f, 0.78f, 0.7f, grime: 0.75f); d["Trim"] = new MD("TrimSheet_Agni", 1, 1, 1, grime: 0.55f, hue: 0.04f);
                    d["MetalGreen"] = new MD("Metal055B", 0.42f, 0.55f, 0.45f, 0.55f, 0, 0.4f); d["Steel"] = new MD("Metal055B", 0.33f, 0.37f, 0.42f, 0.45f, 0, 0.5f);
                    d["Leaf"] = new MD("Ground109", 0.3f, 0.55f, 0.2f, 0.85f, 0, 0, 0.12f); d["Paving"] = new MD("PavingStones142", 1.0f, 0.95f, 0.88f, grime: 0.3f, hue: 0.03f);
                    d["Concrete"] = new MD("Concrete036", 1.15f, 1.08f, 0.98f, grime: 0.5f); d["PlasYellow"] = new MD("Plaster007", 1.05f, 0.8f, 0.45f, grime: 0.8f);
                    d["PlasRed"] = new MD("PaintedPlaster016", 0.95f, 0.5f, 0.38f, grime: 0.8f); d["PlasCream"] = new MD("Plaster007", 0.72f, 0.9f, 0.78f, grime: 0.85f);
                    d["Signs"] = new MD("Signs_Agni", 1, 1, 1, 0.7f, 0, 0.35f, 0); d["Wood"] = new MD("Wood080", 1.18f, 0.8f, 0.52f, grime: 0.4f); break;
                case "VindhyaShikhar":
                    d["Stone"] = new MD("Bricks083", 1.08f, 0.84f, 0.7f, grime: 0.75f); d["Trim"] = new MD("TrimSheet_Agni", 1, 1, 1, grime: 0.55f, hue: 0.04f);
                    d["Concrete"] = new MD("Concrete036", 1.15f, 1.08f, 0.98f, grime: 0.5f); d["Rust"] = new MD("Metal055B", 0.78f, 0.5f, 0.32f, 0.7f, 0, 0.5f);
                    d["Steel"] = new MD("Metal055B", 0.2f, 0.21f, 0.23f, 0.7f, 0, 0.4f); d["CorrBlue"] = new MD("CorrugatedSteel007A", 1, 1, 1, grime: 0.5f);
                    d["CorrGreen"] = new MD("CorrugatedSteel006A", 1, 1, 1, grime: 0.5f); d["Wood"] = new MD("Wood080", 1.18f, 0.8f, 0.52f, grime: 0.5f);
                    d["Rock"] = new MD("Rock061", 1.05f, 0.85f, 0.7f, grime: 0, hue: 0.05f); d["Dirt"] = new MD("Ground109", 1.0f, 0.9f, 0.75f, grime: 0, hue: 0.04f);
                    d["DryGrass"] = new MD("Ground109", 0.72f, 0.82f, 0.45f, grime: 0, hue: 0.05f); d["Signs"] = new MD("Signs_Agni", 1, 1, 1, 0.7f, 0, 0.35f, 0);
                    d["Leaf"] = new MD("Ground109", 0.3f, 0.55f, 0.2f, 0.85f, 0, 0, 0.12f); break;
                case "BambaiMill":
                    d["Brick"] = new MD("Bricks090", 0.86f, 0.62f, 0.52f, grime: 0.9f); d["Signs"] = new MD("Signs_Agni", 1, 1, 1, 0.7f, 0, 0.35f, 0);
                    d["Trim"] = new MD("TrimSheet_Agni", 1, 1, 1, grime: 0.6f, hue: 0.04f); d["Concrete"] = new MD("Concrete036", 1.12f, 1.06f, 0.98f, grime: 0.6f, norm: 0.4f);
                    d["Yard"] = new MD("Concrete036", 1.05f, 1.0f, 0.92f, grime: 0.9f, hue: 0.12f); d["CorrRust"] = new MD("CorrugatedSteel006A", 1, 1, 1, grime: 0.6f);
                    d["Rust"] = new MD("Metal055B", 0.78f, 0.48f, 0.3f, 0.7f, 0, 0.5f); d["Steel"] = new MD("Metal055B", 0.2f, 0.21f, 0.23f, 0.7f, 0, 0.4f);
                    d["Wood"] = new MD("Wood080", 1.18f, 0.8f, 0.52f, grime: 0.4f); d["TruckPaint"] = new MD("Metal055B", 0.04f, 0.17f, 0.42f, 0.5f, 0, 0.5f); break;
                case "ChaiBagan":
                    d["PlasWhite"] = new MD("Plaster007", 1.06f, 1.03f, 0.95f, grime: 0.85f); d["PlasYellow"] = new MD("Plaster007", 1.1f, 0.88f, 0.55f, grime: 0.9f);
                    d["WoodWhite"] = new MD("Wood080", 1.4f, 1.35f, 1.25f, grime: 0.5f, hue: 0.05f); d["WoodGreen"] = new MD("Wood080", 0.35f, 0.6f, 0.42f, grime: 0.4f, hue: 0.05f);
                    d["Wood"] = new MD("Wood080", 0.6f, 0.42f, 0.3f, grime: 0.4f, hue: 0.1f); d["TinRed"] = new MD("CorrugatedSteel007A", 1.5f, 0.36f, 0.26f, grime: 0.6f);
                    d["Tin"] = new MD("CorrugatedSteel006A", 0.95f, 0.95f, 0.95f, grime: 0.6f); d["Iron"] = new MD("Metal055B", 0.32f, 0.36f, 0.34f, 0.5f, 0, 0.4f);
                    d["Brick"] = new MD("Bricks059", 0.95f, 0.78f, 0.7f, grime: 0.75f); d["Concrete"] = new MD("Concrete036", 1.1f, 1.02f, 0.92f, grime: 0.6f);
                    d["Tea"] = P(0.035f, 0.085f, 0.015f); d["Leaf"] = P(0.06f, 0.12f, 0.028f); d["Trunk"] = P(0.19f, 0.17f, 0.14f); break;
                case "KayalVillage":
                    d["Planks"] = new MD("Planks037A", 0.78f, 0.62f, 0.48f, grime: 0.6f, hue: 0.1f); d["PlanksWall"] = new MD("Planks037A", 0.62f, 0.44f, 0.3f, grime: 0.6f, hue: 0.1f);
                    d["Wood"] = new MD("Wood080", 0.42f, 0.27f, 0.17f, grime: 0.4f, hue: 0.1f); d["Tiles"] = new MD("RoofingTiles014B", 1.05f, 0.72f, 0.55f, grime: 0.7f);
                    d["PlasWhite"] = new MD("Plaster007", 1.08f, 1.04f, 0.96f, grime: 0.9f); d["PlasGreen"] = new MD("Plaster007", 0.78f, 0.95f, 0.82f, grime: 0.9f);
                    d["Thatch"] = new MD("ThatchedRoof001A", 1.15f, 0.95f, 0.62f, grime: 0.3f); d["Palm"] = P(0.09f, 0.135f, 0.022f); d["Trunk"] = P(0.075f, 0.06f, 0.045f); break;
                case "PatharGaon":
                    d["Stone"] = new MD("Bricks100", 0.98f, 0.9f, 0.8f, grime: 0.7f); d["StoneGrey"] = new MD("Bricks100", 0.82f, 0.8f, 0.77f, grime: 0.7f);
                    d["PlasBlue"] = new MD("Plaster007", 0.72f, 0.84f, 0.95f, grime: 0.85f); d["PlasOchre"] = new MD("Plaster007", 1.08f, 0.78f, 0.42f, grime: 0.85f);
                    d["PlasPink"] = new MD("PaintedPlaster016", 1.05f, 0.66f, 0.55f, grime: 0.85f); d["PlasWhite"] = new MD("Plaster007", 1.05f, 1.0f, 0.92f, grime: 0.9f);
                    d["Concrete"] = new MD("Concrete036", 1.1f, 1.02f, 0.92f, grime: 0.6f); d["Wood"] = new MD("Wood080", 0.62f, 0.45f, 0.32f, grime: 0.4f, hue: 0.12f);
                    d["WoodBlue"] = new MD("Wood080", 0.35f, 0.55f, 0.75f, grime: 0.4f, hue: 0.08f); d["Tin"] = new MD("CorrugatedSteel007A", 1, 1, 1, grime: 0.5f);
                    d["Iron"] = new MD("Metal055B", 0.25f, 0.3f, 0.28f, 0.55f, 0, 0.3f); d["Thatch"] = new MD("ThatchedRoof001A", 2.1f, 1.55f, 0.6f, grime: 0.1f);
                    d["Paving"] = new MD("PavingStones142", 0.95f, 0.88f, 0.78f, grime: 0.3f); d["Rope"] = P(0.7f, 0.58f, 0.39f); d["Cloth"] = P(0.9f, 0.45f, 0.13f); d["Leaf"] = P(0.065f, 0.11f, 0.022f); break;
            }
            return d;
        }

        static Texture2D Tex(string tex, string kind)
        {
            if (tex == null) return null;
            var dir = LM + "Textures/" + tex + "/";
            if (!Directory.Exists(dir)) return null;
            var f = Directory.GetFiles(dir).FirstOrDefault(x => (x.EndsWith("_" + kind + ".jpg") || x.EndsWith(kind + ".jpg")) && !x.EndsWith(".meta"));
            if (f == null) return null;
            var imp = (TextureImporter)AssetImporter.GetAtPath(f);
            bool normal = kind == "NormalGL";
            bool dirty = false;
            if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; dirty = true; }
            if (imp.anisoLevel != 4) { imp.anisoLevel = 4; dirty = true; }
            var andr = imp.GetPlatformTextureSettings("Android"); if (!andr.overridden || andr.maxTextureSize != 1024) { andr.overridden = true; andr.maxTextureSize = 1024; andr.format = TextureImporterFormat.ASTC_6x6; imp.SetPlatformTextureSettings(andr); dirty = true; }
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(f);
        }

        static Material MakeMat(string lm, string name, MD md, Shader sh)
        {
            var dir = LM + "Materials/"; Directory.CreateDirectory(dir);
            var path = dir + "M_" + lm + "_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            var alb = Tex(md.tex, "Color"); var nrm = Tex(md.tex, "NormalGL");
            m.SetTexture("_BaseMap", alb != null ? alb : Texture2D.whiteTexture);
            m.SetTexture("_BumpMap", nrm);
            // Blender tints are linear multipliers on an sRGB texture; procedural colours are linear albedo
            var t = md.tint; m.SetColor("_Tint", new Color(Gm(t.r), Gm(t.g), Gm(t.b)));
            m.SetFloat("_BumpScale", nrm != null ? md.norm : 0f);
            m.SetFloat("_Smoothness", Mathf.Clamp01(1f - md.rough) * 0.6f);
            m.SetFloat("_Metallic", md.metal);
            m.SetFloat("_Grime", md.grime); m.SetFloat("_Hue", md.hue);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
        // a linear multiplier k applied in linear space == multiplying the gamma colour by k^(1/2.2); the shader multiplies in linear, so pass k as a "colour" whose linear value is k
        static float Gm(float k) => Mathf.LinearToGammaSpace(Mathf.Max(0, k));

        public static void Batch()
        {
            int code = 0;
            try { Run(System.Environment.GetCommandLineArgs().Contains("-onlyReport")); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }


        // ------------------------------------------------------------------ loot spots
        static readonly string[] NoLootParts = { "VS_Hill", "CB_TeaRow", "CB_ShadeTrees", "CB_Ditch", "GG_Plaza", "GG_Planters", "GG_Bridge", "MILL_Yard", "MILL_Chimney", "MILL_Signs", "KV_Palms", "PG_Trees", "PG_Hay", "VS_RadioMast", "VS_CompoundWall", "GG_TowerStone", "KV_Walkways" };
        [System.Serializable] class SpotFile { public LmSpots[] landmarks; }
        [System.Serializable] class LmSpots { public string name; public float[] p; public byte[] tier; }
        /// raycast every building part of every landmark prefab for floors with headroom (indoors) and usable roofs; writes Resources/AgniImport/landmark_spots.json
        public static void Spots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var file = new SpotFile { landmarks = new LmSpots[Names.Length] };
            for (int li = 0; li < Names.Length; li++)
            {
                var n = Names[li];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RES + n + ".prefab"));
                Physics.SyncTransforms();
                var pts = new List<Vector3>(); var tiers = new List<byte>(); int indoor = 0, roof = 0;
                var kit = go.transform.Find("Kit");
                foreach (Transform part in kit)
                {
                    if (part.name.StartsWith("UCX") || NoLootParts.Any(x => part.name.StartsWith(x))) continue;
                    var rs = part.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.size.x < 2.5f || b.size.z < 2.5f) continue;
                    var cand = new List<(Vector3 p, byte t, float score)>();
                    for (float x = b.min.x + 0.6f; x < b.max.x - 0.6f; x += 1.1f)
                        for (float z = b.min.z + 0.6f; z < b.max.z - 0.6f; z += 1.1f)
                        {
                            var hits = Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 6f, ~0, QueryTriggerInteraction.Ignore);
                            foreach (var h in hits)
                            {
                                if (h.normal.y < 0.92f) continue;
                                var p = h.point + Vector3.up * 0.04f;
                                bool ceiling = Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, out var up, 4f, ~0, QueryTriggerInteraction.Ignore);
                                float head = ceiling ? up.distance : 99f;
                                if (head < 1.95f) continue;
                                // keep off walls: 8 short rays at knee height
                                bool tight = false; for (int k = 0; k < 8; k++) { var d = Quaternion.Euler(0, k * 45, 0) * Vector3.forward; if (Physics.Raycast(p + Vector3.up * 0.4f, d, 0.45f, ~0, QueryTriggerInteraction.Ignore)) { tight = true; break; } }
                                if (tight) continue;
                                int walls = 0; for (int k = 0; k < 8; k++) { var d = Quaternion.Euler(0, k * 45 + 22.5f, 0) * Vector3.forward; if (Physics.Raycast(p + Vector3.up * 1.0f, d, 7f, ~0, QueryTriggerInteraction.Ignore)) walls++; }
                                if (!ceiling && walls >= 7 && p.y < 6f) { cand.Add((p, (byte)1, 1.5f)); continue; }   // walled room whose roof has no collider (Kayal, tea bungalow)
                                if (ceiling && head < 4.5f) cand.Add((p, (byte)(p.y > 2.5f ? 2 : 1), 2f));             // inside a room (upper floors rarer)
                                else if (!ceiling && p.y > 2.6f && !(n == "BambaiMill" && part.name == "MILL_MainBuilding")) cand.Add((p, (byte)0, 1f));   // a roof you can stand on (the mill roof stays gun-free: fists only)
                            }
                        }
                    // greedy thinning: indoor first, >= 3.2 m apart, cap per part
                    int capIn = Mathf.Clamp((int)(b.size.x * b.size.z / 22f), 2, 12), capRoof = 2; int ci = 0, cr = 0;
                    foreach (var c in cand.OrderByDescending(c => c.score).ThenBy(c => (c.p.x * 7.13f + c.p.z * 3.7f) % 1f))
                    {
                        if (c.t > 0 && ci >= capIn) continue; if (c.t == 0 && cr >= capRoof) continue;
                        if (pts.Any(q => (q - c.p).sqrMagnitude < 3.2f * 3.2f)) continue;
                        pts.Add(c.p); tiers.Add(c.t); if (c.t > 0) { ci++; indoor++; } else { cr++; roof++; }
                    }
                }
                file.landmarks[li] = new LmSpots { name = n, p = pts.SelectMany(p => new[] { Mathf.Round(p.x * 100) / 100, Mathf.Round(p.y * 100) / 100, Mathf.Round(p.z * 100) / 100 }).ToArray(), tier = tiers.ToArray() };
                Debug.Log("[AgniImport] loot spots " + n + ": " + pts.Count + " (indoor " + indoor + ", roof " + roof + ")");
                Object.DestroyImmediate(go);
            }
            File.WriteAllText("Assets/Agni/Import/Resources/AgniImport/landmark_spots.json", JsonUtility.ToJson(file));
            AssetDatabase.ImportAsset("Assets/Agni/Import/Resources/AgniImport/landmark_spots.json");
        }

        public static void Run(bool onlyReport = false)
        {
            Directory.CreateDirectory(RES);
            var sh = Shader.Find("AgniRoyale/Landmark");
            if (sh == null) { AssetDatabase.Refresh(); sh = Shader.Find("AgniRoyale/Landmark"); }
            if (sh == null) throw new System.Exception("AgniRoyale/Landmark shader missing");
            foreach (var n in Names) One(n, sh, onlyReport);
            AssetDatabase.SaveAssets();
            if (!onlyReport) Spots();
        }

        static void One(string n, Shader sh, bool onlyReport)
        {
            var path = LM + n + ".fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { Debug.LogError("[AgniImport] missing " + path); return; }
            imp.globalScale = 1; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
            imp.importBlendShapes = false; imp.importNormals = ModelImporterNormals.Import; imp.importTangents = ModelImporterTangents.CalculateMikk; imp.meshCompression = ModelImporterMeshCompression.Low;
            imp.isReadable = false; imp.generateSecondaryUV = false; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.bakeAxisConversion = System.Environment.GetCommandLineArgs().Contains("-bake");
            var defs = Defs(n);
            foreach (var kv in defs) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), MakeMat(n, kv.Key, kv.Value, sh));
            imp.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)Object.Instantiate(model); go.name = n;
            foreach (var lgx in go.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lgx);
            // axis fix: the kit's FBX nodes come in Z-up (Unity adds +90 X on every mesh node) except the clock hands, which are right;
            // one wrapper at -90 X turns the whole kit Y-up, hands stay where they are
            var wrap = new GameObject("Kit").transform; wrap.SetParent(go.transform, false); wrap.localRotation = Quaternion.Euler(-90, 0, 0);
            foreach (var c in go.transform.Cast<Transform>().ToArray()) if (c != wrap && !c.name.StartsWith("GG_ClockHand_")) c.SetParent(wrap, false);
            foreach (var c in wrap.Cast<Transform>()) if (c.name.StartsWith("UCX") && c.localRotation == Quaternion.identity) c.localRotation = Quaternion.Euler(90, 0, 0);
            {   // UCX vs LOD sanity
                Bounds? ub = null, lb = null;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) { var wb = WB(mf); if (mf.name.StartsWith("UCX")) ub = ub.HasValue ? Enc(ub.Value, wb) : wb; else if (mf.name.Contains("_LOD0")) lb = lb.HasValue ? Enc(lb.Value, wb) : wb; }
                Debug.Log("[AgniImport] " + n + " UCX bounds " + (ub.HasValue ? ub.Value.min.ToString("F1") + ".." + ub.Value.max.ToString("F1") : "-") + " LOD0 bounds " + (lb.HasValue ? lb.Value.min.ToString("F1") + ".." + lb.Value.max.ToString("F1") : "-"));
            }
            // report
            var all = go.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(go.transform.position, Vector3.zero); foreach (var r in all) if (!r.name.StartsWith("UCX")) b.Encapsulate(r.bounds);
            Debug.Log("[AgniImport] " + n + " bounds " + b.min.ToString("F1") + " .. " + b.max.ToString("F1") + " renderers " + all.Length + " rootRot " + go.transform.rotation.eulerAngles);
            foreach (Transform part in wrap)
            {
                var rs = part.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith("UCX")).ToArray();
                if (rs.Length == 0) continue; var pb = rs[0].bounds; foreach (var r in rs) pb.Encapsulate(r.bounds);
                Debug.Log("[AgniImport]   part " + part.name + " c " + pb.center.ToString("F1") + " size " + pb.size.ToString("F1") + " rends " + rs.Length);
            }
            foreach (var r in all.Where(x => !x.name.StartsWith("UCX"))) foreach (var m in r.sharedMaterials) if (m == null || m.shader != sh) { Debug.LogWarning("[AgniImport]   unmapped material on " + r.name + ": " + (m ? m.name : "null")); break; }
            if (onlyReport) { Object.DestroyImmediate(go); return; }

            // collision asset (UCX hulls -> readable mesh copies)
            var colPath = LM + "Collision_" + n + ".asset";
            AssetDatabase.DeleteAsset(colPath);
            var holder = new Mesh { name = "UCX_" + n + "_holder" }; AssetDatabase.CreateAsset(holder, colPath);
            int ucx = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("UCX")).ToArray())
            {
                var src = mf.sharedMesh; var copy = Object.Instantiate(src); copy.name = mf.name; AssetDatabase.AddObjectToAsset(copy, colPath);
                var g = mf.gameObject; var mr = g.GetComponent<MeshRenderer>(); if (mr) Object.DestroyImmediate(mr);
                Object.DestroyImmediate(mf);
                var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = copy; mc.convex = true; ucx++;
            }
            // tea garden: the LOD0 "rows" are 510 separate instances plus a hidden master 50 m under ground -> use the merged LOD1/LOD2 rows only (1 draw call)
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CB_TeaRowInst_") || t.name == "CB_TeaRow_LOD0").ToArray()) Object.DestroyImmediate(t.gameObject);
            // the Vindhya hill has no UCX hull: give it a mesh collider from its LOD1 surface; print its height profile for placement
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "VS_Hill_LOD1").ToArray())
            {
                var copy = Object.Instantiate(mf.sharedMesh); copy.name = "VS_Hill_Collider"; AssetDatabase.AddObjectToAsset(copy, colPath);
                var hc = new GameObject("HillCollider"); hc.transform.SetParent(mf.transform, false); hc.AddComponent<MeshCollider>().sharedMesh = copy;
                var v = mf.sharedMesh.vertices; var bins = new float[14]; var mins = new float[14]; for (int i = 0; i < 14; i++) { bins[i] = -999; mins[i] = 999; }
                foreach (var p in v) { var w = go.transform.InverseTransformPoint(mf.transform.TransformPoint(p)); int bi = Mathf.Clamp((int)(new Vector2(w.x, w.z).magnitude / 10f), 0, 13); bins[bi] = Mathf.Max(bins[bi], w.y); mins[bi] = Mathf.Min(mins[bi], w.y); }
                Debug.Log("[AgniImport] hill profile (r per 10 m: max/min y): " + string.Join(" | ", Enumerable.Range(0, 14).Select(i => (i * 10) + ":" + bins[i].ToString("F1") + "/" + mins[i].ToString("F1"))));
            }
            // tower top: cut the old finial off the clock tower, add our weathervane
            if (n == "GhantaGharSquare") CutFinial(go, colPath);
            // LOD groups per part
            int lods = 0;
            foreach (var part in go.GetComponentsInChildren<Transform>(true).ToArray())
            {
                var rs = part.GetComponentsInChildren<Renderer>(true);
                var l0 = new List<Renderer>(); var l1 = new List<Renderer>(); var l2 = new List<Renderer>();
                foreach (Transform c in part) { var r = c.GetComponent<Renderer>(); if (r == null) continue; if (c.name.EndsWith("_LOD0")) l0.Add(r); else if (c.name.EndsWith("_LOD1")) l1.Add(r); else if (c.name.EndsWith("_LOD2")) l2.Add(r); }
                if (l0.Count == 0 && l1.Count > 0) { l0 = l1; l1 = l2; l2 = new List<Renderer>(); }
                if (l0.Count == 0) continue;
                var pb = l0[0].bounds; foreach (var r in l0) pb.Encapsulate(r.bounds); float size = pb.size.magnitude;
                var lg = part.gameObject.GetComponent<LODGroup>(); if (lg == null) lg = part.gameObject.AddComponent<LODGroup>();
                // big terrain-like parts never cull; small props cull early
                float cull = size > 60 ? 0f : size > 15 ? 0.004f : 0.012f;
                var L = new List<LOD> { new LOD(size > 60 ? 0.30f : 0.40f, l0.ToArray()) };
                if (l1.Count > 0) L.Add(new LOD(size > 60 ? 0.10f : 0.14f, l1.ToArray()));
                if (l2.Count > 0) L.Add(new LOD(cull, l2.ToArray()));
                if (L.Count == 3 && L[2].screenRelativeTransitionHeight >= L[1].screenRelativeTransitionHeight) { var x = L[2]; x.screenRelativeTransitionHeight = L[1].screenRelativeTransitionHeight * 0.5f; L[2] = x; }
                lg.SetLODs(L.ToArray()); lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.None; lods++;
                foreach (var r in l0.Concat(l1).Concat(l2)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
                foreach (var r in l2) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // clock hands
            var hands = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("GG_ClockHand_")).ToArray();
            if (hands.Length > 0) go.AddComponent<Agni.Import.AgniClockHands>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (var t in hands) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            var info = go.AddComponent<Agni.Import.AgniLandmarkInfo>(); info.landmark = n;
            var pf = PrefabUtility.SaveAsPrefabAsset(go, RES + n + ".prefab");
            Object.DestroyImmediate(go);
            Debug.Log("[AgniImport] prefab " + n + " ucx=" + ucx + " lodgroups=" + lods);
        }

        /// remove every triangle above the pyramid roof's tip (rod, cap, old vane) from the tower meshes; AgniWeathervane builds the new top at runtime
        static void CutFinial(GameObject go, string assetPath)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("GG_ClockTower_LOD")) continue;
                var src = mf.sharedMesh; var tr = mf.transform;
                var v = src.vertices; float top = float.MinValue; foreach (var p in v) top = Mathf.Max(top, tr.TransformPoint(p).y);
                var m = Object.Instantiate(src); m.name = src.name + "_noFinial";
                int removed = 0;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var tris = m.GetTriangles(s); var keep = new List<int>(tris.Length);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        var a = tr.TransformPoint(v[tris[i]]); var b = tr.TransformPoint(v[tris[i + 1]]); var c = tr.TransformPoint(v[tris[i + 2]]);
                        var cen = (a + b + c) / 3f; var axis = go.transform.InverseTransformPoint(cen);
                        if (cen.y > FinialCutY && new Vector2(axis.x - TowerAxis.x, axis.z - TowerAxis.y).magnitude < 1.6f) { removed++; continue; }
                        keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                    }
                    m.SetTriangles(keep, s);
                }
                m.UploadMeshData(false);
                AssetDatabase.AddObjectToAsset(m, assetPath); mf.sharedMesh = m;
                Debug.Log("[AgniImport] finial cut on " + mf.name + ": top was " + top.ToString("F2") + ", removed " + removed + " tris");
            }
            var tower = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GG_ClockTower");
            var vane = new GameObject("Weathervane"); vane.transform.SetParent(tower != null ? tower : go.transform, false);
            vane.transform.position = go.transform.TransformPoint(new Vector3(TowerAxis.x, FinialCutY - 0.12f, TowerAxis.y));
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(string nm, Color c, float metal, float sm) { var p = LM + "Materials/" + nm + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); } m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", sm); EditorUtility.SetDirty(m); return m; }
            Agni.Import.AgniWeathervane.BuildGeometry(vane.transform, M("M_VaneIron", new Color(0.13f, 0.14f, 0.15f), 0.75f, 0.45f), M("M_VaneBrass", new Color(0.72f, 0.56f, 0.28f), 0.85f, 0.55f));
            vane.AddComponent<Agni.Import.AgniWeathervane>();
        }
        static Bounds WB(MeshFilter mf) { var b = mf.sharedMesh.bounds; var w = new Bounds(mf.transform.TransformPoint(b.center), Vector3.zero); for (int i = 0; i < 8; i++) w.Encapsulate(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))); return w; }
        static Bounds Enc(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
        public static float FinialCutY = 30.55f; public static Vector2 TowerAxis = Vector2.zero;
    }
}
