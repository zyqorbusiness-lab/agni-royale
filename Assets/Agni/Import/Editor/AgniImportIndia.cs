// Agni Royale — IMPORT v1: editor side of the India match map: terrain layers + materials, the Blender tree kinds (by biome) and the Blender grass
// clumps as terrain-detail prefabs, the Blender sky (dopahar) with its own light/fog/ambient numbers. AgniNetSetup.Arena() calls Configure().
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import.EditorTools
{
    public class AgniGrassImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Agni/Import/Grass/")) return;
            var mi = (ModelImporter)assetImporter; mi.globalScale = 1f; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import; mi.isReadable = true; mi.addCollider = false; mi.importCameras = false; mi.importLights = false; mi.meshCompression = ModelImporterMeshCompression.Medium;
        }
        void OnPreprocessTexture()
        {
            if (!(assetPath.Contains("/Agni/Import/Grass/") || assetPath.Contains("/Agni/Import/Sky/"))) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Sky/")) { ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.maxTextureSize = 2048; var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 2048; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a); return; }
            ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.45f; ti.maxTextureSize = 1024;
            var b = ti.GetPlatformTextureSettings("Android"); b.overridden = true; b.maxTextureSize = 1024; b.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(b);
        }
    }

    public static class AgniImportIndia
    {
        const string D = "Assets/Agni/Import/India/";
        static Material Lit(string n, Color c, float sm = 0.1f)
        {
            var p = D + n + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        public static AgniIndiaTerrain Configure(Transform parent, Material water)
        {
            Directory.CreateDirectory(D); Directory.CreateDirectory(D + "Grass");
            // terrain layers (the map's own 6 textures in Assets/Agni/Textures)
            string[] ln = { "sand", "grass", "dry", "rock", "snow", "mustard" }; float[] tile = { 14f, 18f, 16f, 12f, 22f, 9f };
            var layers = new TerrainLayer[6];
            for (int k = 0; k < 6; k++)
            {
                var p = D + "L_" + ln[k] + ".terrainlayer"; var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p); if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, p); }
                l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Agni/Textures/" + ln[k] + ".png"); l.tileSize = new Vector2(tile[k], tile[k]); l.smoothness = ln[k] == "snow" ? 0.3f : 0f; l.metallic = 0; EditorUtility.SetDirty(l); layers[k] = l;
            }
            var tm = AssetDatabase.LoadAssetAtPath<Material>(D + "M_Terrain.mat"); if (tm == null) { tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(tm, D + "M_Terrain.mat"); }
            var go = new GameObject("IndiaTerrain"); go.transform.SetParent(parent, false); var it = go.AddComponent<AgniIndiaTerrain>();
            it.layers = layers; it.terrainMat = tm; it.waterMat = water;
            it.roadMat = Lit("M_Road", Color.white, 0.06f); it.roadMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_Road.png"));          // kachcha road: earth, tyre ruts, grassy verges
            it.highwayMat = Lit("M_Highway", Color.white, 0.16f); it.highwayMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_Highway.png")); // asphalt, dashed centre line
            it.bridgeMat = Lit("M_Bridge", new Color(0.62f, 0.61f, 0.58f), 0.1f);
            // trees: all 12 Blender species (A/B + snow deodar), by biome
            var tp = Agni.Net.EditorTools.AgniTreeSetup.Build();
            var kinds = new List<AgniIndiaTerrain.TreeKind>();
            void T(string key, string biome, float w, bool bush = false) { if (tp.TryGetValue(key, out var pf)) kinds.Add(new AgniIndiaTerrain.TreeKind { prefab = pf, biome = biome, weight = w, bush = bush }); else Debug.LogWarning("[AgniIndia] tree missing " + key); }
            T("Neem_A", "plains,dry", 1.4f); T("Neem_B", "plains,dry", 1.0f); T("Mango_A", "plains,coast", 1.2f); T("Mango_B", "plains", 0.8f);
            T("Banyan_A", "plains", 0.35f); T("Banyan_B", "plains,coast", 0.3f); T("Gulmohar_A", "plains,coast", 0.6f); T("Gulmohar_B", "plains", 0.4f);
            T("Ashoka_A", "plains,northeast", 0.6f); T("Ashoka_B", "plains", 0.4f); T("Palm_A", "coast", 1.6f); T("Palm_B", "coast", 1.2f);
            T("Banana_A", "coast,northeast", 0.7f, true); T("Banana_B", "coast,northeast", 0.5f, true); T("Bamboo_A", "northeast", 1.4f); T("Bamboo_B", "northeast,plains", 0.5f);
            T("Khejri_A", "desert,dry", 1.4f); T("Khejri_B", "desert,dry", 1.0f); T("Deodar_A", "mountain", 1.4f); T("Deodar_B", "mountain", 1.0f); T("DeodarSnow_Snow", "mountain", 1.2f);
            T("Lantana_A", "plains,dry,coast,northeast", 1.0f, true); T("Lantana_B", "plains,dry,desert", 0.8f, true); T("Hedge_A", "plains,coast", 0.5f, true);
            it.trees = kinds.ToArray();
            // grass: Blender clumps (LOD0 mesh) as instanced detail prototypes
            var gm = AssetDatabase.LoadAssetAtPath<Material>(D + "M_Grass.mat"); if (gm == null) { gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(gm, D + "M_Grass.mat"); }
            gm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Agni/Import/Grass/T_GrassAtlas_D.png")); gm.SetColor("_BaseColor", Color.white);
            gm.SetFloat("_AlphaClip", 1); gm.SetFloat("_Cutoff", 0.45f); gm.EnableKeyword("_ALPHATEST_ON"); gm.SetFloat("_Cull", 0); gm.SetFloat("_Smoothness", 0.06f); gm.renderQueue = (int)RenderQueue.AlphaTest; gm.SetOverrideTag("RenderType", "TransparentCutout"); gm.enableInstancing = true; EditorUtility.SetDirty(gm);
            var gk = new List<AgniIndiaTerrain.GrassKind>();
            void Gr(string n, string biome, float dens, float mn, float mx)
            {
                var fbx = "Assets/Agni/Import/Grass/" + n + ".fbx"; var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name.EndsWith("_LOD0")) ?? AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
                if (mesh == null) { Debug.LogWarning("[AgniIndia] grass missing " + n); return; }
                GameObject Save(Mesh me, string pn) { var g = new GameObject(pn); g.AddComponent<MeshFilter>().sharedMesh = me; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = gm; mr.shadowCastingMode = ShadowCastingMode.Off; var pf0 = PrefabUtility.SaveAsPrefabAsset(g, D + "Grass/" + pn + ".prefab"); Object.DestroyImmediate(g); return pf0; }
                var pf = Save(mesh, n);
                var low = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name.EndsWith("_LOD1"));
                var pfl = low != null ? Save(low, n + "_LOD1") : null; // NORMAL / HIGH draw the half-card LOD1 clump, VERY HIGH the full LOD0
                gk.Add(new AgniIndiaTerrain.GrassKind { prefab = pf, prefabLow = pfl, biome = biome, density = dens, minH = mn, maxH = mx });
            }
            Gr("Grass_Meadow_A", "meadow", 1.0f, 0.8f, 1.2f); Gr("Grass_Meadow_B", "meadow", 0.7f, 0.8f, 1.2f); Gr("Grass_Meadow_C", "meadow", 0.6f, 0.8f, 1.2f);
            Gr("Grass_Lawn_A", "lawn", 0.6f, 0.9f, 1.2f); Gr("Grass_DryGolden_A", "dry", 0.9f, 0.8f, 1.2f); Gr("Grass_DryGolden_B", "dry", 0.6f, 0.8f, 1.1f);
            Gr("Grass_Alpine_A", "alpine", 1.0f, 0.8f, 1.2f); Gr("Grass_Wildflower_A", "flowers", 0.6f, 0.8f, 1.2f); Gr("Grass_Wildflower_C", "flowers", 0.8f, 0.8f, 1.2f);
            Gr("Grass_TallHiding_A", "tall", 0.5f, 0.85f, 1.15f); Gr("Grass_Reeds_A", "reeds", 0.6f, 0.8f, 1.2f); Gr("Grass_KaansPlume_A", "kaans", 0.5f, 0.8f, 1.1f); Gr("Grass_Fern_A", "fern", 0.6f, 0.8f, 1.2f);
            Gr("Grass_Lawn_B", "lawn", 0.35f, 0.9f, 1.2f); Gr("Grass_DryGolden_C", "dry", 0.5f, 0.8f, 1.2f); Gr("Grass_Alpine_B", "alpine", 0.7f, 0.8f, 1.2f); Gr("Grass_Wildflower_B", "flowers", 0.5f, 0.8f, 1.2f);
            Gr("Grass_TallHiding_B", "tall", 0.35f, 0.85f, 1.15f); Gr("Grass_Reeds_B", "reeds", 0.45f, 0.8f, 1.2f); Gr("Grass_KaansPlume_B", "kaans", 0.3f, 0.8f, 1.1f); Gr("Grass_Fern_B", "fern", 0.4f, 0.8f, 1.2f);
            it.grass = gk.ToArray();
            EditorUtility.SetDirty(it);
            return it;
        }
        /// the Blender sky: Skybox/Panoramic + that sky's light, ambient and fog (sky/sky_settings.json)
        public static Light Sky(string name = "dopahar")
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Agni/Import/Sky/sky_" + name + "_pano_2048.png");
            var p = D + "M_Sky_" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, p); }
            m.SetTexture("_MainTex", tex); m.SetFloat("_Mapping", 1); m.SetFloat("_ImageType", 0); m.SetFloat("_Exposure", 1f); m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f)); m.SetFloat("_Rotation", 0); EditorUtility.SetDirty(m);
            RenderSettings.skybox = m;
            // numbers from sky_settings.json (dopahar): fog #DEE8EC exp2 0.0025 -> a little lighter for a 4 km map; Trilight ambient; sun #FFF4E0 1.25 at euler (58, 205, 0)
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            sun.color = Hex("#FFF4E0"); sun.intensity = 1.25f; sun.shadowStrength = 1f; sunGo.transform.rotation = Quaternion.Euler(58f, 205f, 0f); RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = Hex("#2F70DC") * 0.75f + Color.white * 0.12f; RenderSettings.ambientEquatorColor = Hex("#C8D8EC"); RenderSettings.ambientGroundColor = Hex("#76848C");
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = Hex("#DEE8EC"); RenderSettings.fogDensity = 0.0011f;
            return sun;
        }
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
