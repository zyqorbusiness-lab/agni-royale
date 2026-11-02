// Agni Royale v0.2.2 — ANIME MAP LOOK (editor): moves the India map's materials onto the anime world shaders.
//   houses (village kit), roads/bridges, vehicles  -> AgniRoyale/WorldToon (+ light outline on houses/vehicles, HIGH+ only at runtime)
//   Blender trees + bushes (+ impostors), grass     -> AgniRoyale/WorldToon (alpha clip, soft ball-shaped crown shading, no outline)
//   terrain                                         -> AgniRoyale/TerrainToon (+ add pass for layers 5-8)
//   Blender landmarks keep AgniRoyale/Landmark, which now shades with the same toon ramp (AgniToon.hlsl)
//   sky                                             -> Skybox/Panoramic with our painted anime sky (T_Sky_Anime.png)
// Batch: -executeMethod Agni.Import.EditorTools.AgniAnimeSetup.RunBatch   (idempotent)
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agni.Import.EditorTools
{
    public static class AgniAnimeSetup
    {
        public static void RunBatch() { Run(); EditorApplication.Exit(0); }

        [MenuItem("Agni Royale/v0.2.2 Anime map look")]
        public static void Run()
        {
            var toon = Shader.Find("AgniRoyale/WorldToon"); var terr = Shader.Find("AgniRoyale/TerrainToon");
            if (toon == null || terr == null) { Debug.LogError("[AgniAnime] shaders missing"); return; }
            int n = 0;
            n += Folder("Assets/Agni/Net/Resources/AgniNet/Village", toon, 0.022f, false, 1.18f);
            n += Folder("Assets/Agni/Import/Vehicles/Mats", toon, 0.016f, false, 1.12f);
            n += Folder("Assets/Agni/Net/Trees", toon, 0f, true, 1.15f);
            n += Folder("Assets/Agni/Import/Grass", toon, 0f, true, 1.1f);
            n += Folder("Assets/Agni/Import/India/Grass", toon, 0f, true, 1.1f);
            n += Folder("Assets/Agni/Import/Landmarks/Materials", toon, 0.022f, false, 1.15f, onlyLit: true);
            foreach (var p in new[] { "Assets/Agni/Import/India/M_Road.mat", "Assets/Agni/Import/India/M_Highway.mat", "Assets/Agni/Import/India/M_Bridge.mat", "Assets/Agni/Import/India/M_Grass.mat" })
            { var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m != null) { Convert(m, toon, 0f, p.EndsWith("M_Grass.mat"), 1.12f); n++; } }
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Agni/Import/Landmarks/Materials" }))
            { var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (m != null && m.shader != null && m.shader.name == "AgniRoyale/Landmark") { m.SetShaderPassEnabled("SRPDefaultUnlit", true); EditorUtility.SetDirty(m); } }
            var tm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Agni/Import/India/M_Terrain.mat");
            if (tm != null) { tm.shader = terr; tm.SetFloat("_TerrSat", 1.08f); tm.SetFloat("_TerrBright", 1.0f); EditorUtility.SetDirty(tm); }
            // layer textures are the painted anime versions now: no normal maps, no shine
            foreach (var g in AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets/Agni/Import/India" }))
            {
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g)); if (l == null) continue;
                l.normalMapTexture = null; l.smoothness = 0; l.metallic = 0; EditorUtility.SetDirty(l);
            }
            // sky
            const string skyTex = "Assets/Agni/Import/Resources/AgniImport/Sky/T_Sky_Anime.png", skyMat = "Assets/Agni/Import/Resources/AgniImport/Sky/M_Sky_Anime.mat";
            var ti = AssetImporter.GetAtPath(skyTex) as TextureImporter;
            if (ti != null) { ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.Compressed; ti.sRGBTexture = true; ti.SaveAndReimport(); }
            var sm = AssetDatabase.LoadAssetAtPath<Material>(skyMat);
            if (sm == null) { sm = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sm, skyMat); }
            sm.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(skyTex)); sm.SetFloat("_Mapping", 1); sm.SetFloat("_ImageType", 0); sm.SetFloat("_Exposure", 1.0f); sm.SetFloat("_Rotation", 0); sm.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(sm);
            AssetDatabase.SaveAssets();
            Debug.Log("[AgniAnime] converted " + n + " materials to WorldToon; terrain -> TerrainToon; sky -> " + skyMat);
        }

        static int Folder(string dir, Shader toon, float outline, bool foliage, float sat, bool onlyLit = false)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return 0; int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (m == null) continue;
                var sn = m.shader != null ? m.shader.name : "";
                if (sn != "Universal Render Pipeline/Lit" && sn != "Universal Render Pipeline/Simple Lit" && sn != "AgniRoyale/WorldToon") continue; // custom shaders (water, landmark, FX) stay
                Convert(m, toon, outline, foliage, sat); n++;
            }
            return n;
        }

        static void Convert(Material m, Shader toon, float outline, bool foliage, float sat)
        {
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
            Vector2 sc = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.one, of = m.HasProperty("_BaseMap") ? m.GetTextureOffset("_BaseMap") : Vector2.zero;
            Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            bool clip = (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) || m.IsKeywordEnabled("_ALPHATEST_ON");
            float cut = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.45f; float cull = m.HasProperty("_Cull") ? m.GetFloat("_Cull") : 2f;
            bool impostor = m.name.Contains("Impostor"), leaf = foliage && (clip || m.name.Contains("Leaf") || impostor);
            if (m.shader != toon) m.shader = toon;
            m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", sc); m.SetTextureOffset("_BaseMap", of);
            m.SetColor("_BaseColor", col); m.SetFloat("_Cutoff", cut); m.SetFloat("_Cull", clip ? 0f : cull);
            m.SetFloat("_AlphaClip", clip ? 1 : 0); if (clip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Sat", sat); m.SetFloat("_Bright", foliage ? 1.06f : 1.04f);
            m.SetFloat("_Paint", impostor ? 0.4f : leaf ? 0.9f : 1.3f);
            m.SetFloat("_Sphere", leaf && !impostor ? 0.65f : 0f); m.SetVector("_SphereCenter", new Vector4(0, 2.6f, 0, 0));
            m.SetFloat("_ShadowStrength", leaf ? 0.6f : 0.8f);
            m.SetFloat("_OutlineWidth", outline);
            m.renderQueue = clip ? 2450 : -1; m.SetShaderPassEnabled("SRPDefaultUnlit", true);
            EditorUtility.SetDirty(m);
        }
    }
}
