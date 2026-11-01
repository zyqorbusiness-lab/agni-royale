// Agni Royale — editor setup for the villages: texture import settings + the M_V_* materials (URP Lit, CC0 textures from ambientCG).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agni.Net.EditorTools
{
    public class AgniVillageTexImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/AgniNet/Village/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Repeat;
            if (assetPath.EndsWith("_N.jpg")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
            else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; }
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
        }
    }

    public static class AgniVillageSetup
    {
        const string D = "Assets/Agni/Net/Resources/AgniNet/Village/";
        [MenuItem("Agni Royale/Net/Village materials")]
        public static void Materials()
        {
            AssetDatabase.ImportAsset(D, ImportAssetOptions.ImportRecursive);
            Mat("PlasterA", "Plaster", new Color(0.97f, 0.82f, 0.58f), 0.12f);
            Mat("PlasterB", "Plaster", new Color(0.72f, 0.85f, 0.92f), 0.12f);
            Mat("PlasterC", "Plaster", new Color(0.96f, 0.77f, 0.74f), 0.12f);
            Mat("PlasterD", "Plaster", new Color(0.79f, 0.90f, 0.78f), 0.12f);
            Mat("Band", "PlasterOld", new Color(0.66f, 0.36f, 0.28f), 0.1f);
            Mat("Brick", "Brick", new Color(0.95f, 0.92f, 0.9f), 0.1f);
            Mat("Mud", "Mud", new Color(0.9f, 0.79f, 0.64f), 0.03f);
            Mat("Lime", "Plaster", new Color(0.97f, 0.96f, 0.93f), 0.1f);
            Mat("Thatch", "Thatch", new Color(1f, 0.95f, 0.85f), 0.05f);
            Mat("Wood", "Wood", new Color(0.78f, 0.66f, 0.55f), 0.15f);
            Mat("Tin", "Tin", new Color(0.92f, 0.94f, 0.95f), 0.38f, 0.35f);
            Mat("Tile", "Tile", new Color(1f, 0.95f, 0.92f), 0.15f);
            Mat("Concrete", "Concrete", new Color(0.9f, 0.89f, 0.86f), 0.1f);
            Mat("Dark", null, new Color(0.09f, 0.09f, 0.1f), 0.35f);
            Mat("Dirt", "Dirt", new Color(1f, 0.96f, 0.9f), 0.05f);
            Mat("Cloth", "PlasterOld", new Color(0.78f, 0.66f, 0.45f), 0.05f);
            Mat("MudBase", "Mud", new Color(0.56f, 0.42f, 0.31f), 0.03f);   // dark gobar-lipai plinth: dirt + damp at the base of mud walls
            AssetDatabase.SaveAssets();
            Debug.Log("[AgniVillageSetup] materials done");
        }
        static void Mat(string name, string tex, Color tint, float smooth, float metal = 0f)
        {
            var path = D + "M_V_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            m.SetFloat("_EnvironmentReflections", metal > 0 ? 1 : 0); m.SetFloat("_SpecularHighlights", 1);
            if (tex != null)
            {
                var c = AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_V_" + tex + "_C.jpg"); var n = AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_V_" + tex + "_N.jpg");
                m.SetTexture("_BaseMap", c); m.SetTexture("_MainTex", c);
                if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            }
            m.enableInstancing = true; EditorUtility.SetDirty(m);
        }
    }
}
