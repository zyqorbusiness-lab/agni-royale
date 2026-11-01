// Agni Royale — IMPORT v1: the 10 hair-v3 heroes -> Resources/AgniImport/Heroes/<Name>.prefab
//   Unity -batchmode -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportHeroes.Batch
// Source FBX + textures are copied from characters_hair_v3/<Name>/ (read-only). AgniFace.cs is the unchanged copy from characters_face/.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agni.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import.EditorTools
{
    public class AgniHeroModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath.Contains("/AgniImport/Emotes/"))
            {   // emotes v2: legacy clips played by the same Animation component as the body clips (bones bind by mixamorig path under <Hero>_Rig)
                var em = (ModelImporter)assetImporter; em.animationType = ModelImporterAnimationType.Legacy; em.importAnimation = true; em.materialImportMode = ModelImporterMaterialImportMode.None;
                em.importCameras = false; em.importLights = false; em.importBlendShapes = false; em.resampleCurves = true; em.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                return;
            }
            if (!assetPath.Contains("/Agni/Import/Heroes/")) return;
            var mi = (ModelImporter)assetImporter; mi.importBlendShapes = true; mi.importBlendShapeNormals = ModelImporterNormals.None; mi.importNormals = ModelImporterNormals.Import;
            mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importCameras = false; mi.importLights = false;
            mi.optimizeGameObjects = false; mi.importVisibility = false;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Agni/Import/Heroes/")) return;
            var ti = (TextureImporter)assetImporter; ti.maxTextureSize = 2048; ti.alphaIsTransparency = assetPath.Contains("_eye") || assetPath.Contains("_lashbrow");
            // look polish v2: crisper cloth/skin at an angle (aniso) and sharper mips; the clothes atlas is padded (Tools/hero_tex/fix_clothes_atlas.py) so mips don't bleed
            if (assetPath.Contains("_clothes") || assetPath.Contains("_skin.png") || assetPath.Contains("_hair")) { ti.anisoLevel = 4; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.mipMapBias = -0.3f; }
            if (assetPath.Contains("_normal")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("_mask")) ti.sRGBTexture = false;
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = assetPath.Contains("_skin.png") || assetPath.Contains("_eye") ? 1024 : 512; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
    }

    public static class AgniImportHeroes
    {
        const string SRC = "Assets/Agni/Import/Heroes/", OUT = "Assets/Agni/Import/Resources/AgniImport/Heroes/", FACE = "Assets/Agni/Import/Resources/AgniImport/Face/";
        public static void Batch() { int c = 0; try { Run(); } catch (System.Exception e) { Debug.LogException(e); c = 1; } EditorApplication.Exit(c); }
        static Material Mat(string dir, string n, Shader sh, System.Action<Material> f)
        {
            var p = dir + n + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
            f(m); EditorUtility.SetDirty(m); return m;
        }
        static Texture2D T(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        static void Cutout(Material m, Texture2D t, float cut, bool twoSided)
        {
            m.SetTexture("_BaseMap", t); m.SetColor("_BaseColor", Color.white); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", cut); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", twoSided ? 0 : 2); m.renderQueue = (int)RenderQueue.AlphaTest + 5;
        }
        public static void Run()
        {
            Directory.CreateDirectory(OUT);
            var toon = Shader.Find("AgniRoyale/HeroToon"); if (toon == null) toon = Shader.Find("AgniRoyale/AnimeToonOutline"); var unlit = Shader.Find("Universal Render Pipeline/Unlit"); var lit = Shader.Find("Universal Render Pipeline/Lit"); var brow = Shader.Find("AgniRoyale/BrowOverHair");
            Debug.Log("[AgniHeroImport] shaders toon=" + (toon != null) + " brow=" + (brow != null));
            TextAsset Track(string n) => AssetDatabase.LoadAssetAtPath<TextAsset>(FACE + n + ".json");
            var emoteTracks = Directory.GetFiles(FACE, "Face_*.json").Select(f => Path.GetFileNameWithoutExtension(f)).Where(n => !n.StartsWith("Face_React") && n != "Face_Idle_Blink" && n != "Face_Talk").Select(Track).Where(t => t != null).ToList();
            var reactTracks = new[] { "Face_React_Hit", "Face_React_Kill", "Face_React_Win" }.Select(Track).Where(t => t != null).ToList();
            foreach (var name in AgniHeroes.Names)
            {
                var dir = SRC + name + "/"; var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + "_HairV3.fbx");
                if (fbx == null) { Debug.LogWarning("[AgniHeroImport] missing " + name); continue; }
                Directory.CreateDirectory(dir + "Mats"); var md = dir + "Mats/";
                bool man = System.Array.IndexOf(AgniHeroes.Names, name) >= 4;
                // HeroToon v2: soft 3-tone ramp + rim, outline half the old width and scaled by camera distance, outline coloured from albedo.
                System.Action<Material> skinF = m => { m.SetTexture("_BaseMap", T(dir + name + "_skin.png")); m.SetColor("_ShadowTint", new Color(1f, 0.84f, 0.80f)); m.SetColor("_CoreTint", new Color(0.90f, 0.66f, 0.64f)); m.SetFloat("_Softness", 0.16f); m.SetFloat("_Step", -0.05f); m.SetFloat("_CoreStep", -0.5f); m.SetFloat("_OutlineWidth", 0.0016f); m.SetFloat("_RimStrength", 0.22f); m.SetFloat("_SpecStrength", 0f); m.SetFloat("_OutlineAlbedo", 0.7f); };
                System.Action<Material> clothF = m => { m.SetTexture("_BaseMap", T(dir + name + "_clothes.png")); m.SetColor("_ShadowTint", new Color(0.84f, 0.80f, 0.92f)); m.SetColor("_CoreTint", new Color(0.68f, 0.64f, 0.82f)); m.SetFloat("_Softness", 0.06f); m.SetFloat("_Step", -0.22f); m.SetFloat("_CoreStep", -0.7f); m.SetFloat("_NormalFlatten", 0.85f); m.SetFloat("_ShadowStrength", 0.35f); m.SetFloat("_Ambient", 0.38f); m.SetFloat("_OutlineWidth", 0.002f); m.SetFloat("_RimStrength", 0.16f); m.SetFloat("_RimWidth", 0.74f); m.SetFloat("_SpecStrength", 0f); m.SetFloat("_OutlineAlbedo", 0.55f); m.SetFloat("_Cull", 0); };
                System.Action<Material> hairF = m => { m.SetTexture("_BaseMap", T(dir + name + "_hair.png")); m.SetColor("_ShadowTint", new Color(0.82f, 0.80f, 0.94f)); m.SetColor("_CoreTint", new Color(0.60f, 0.56f, 0.80f)); m.SetFloat("_Softness", 0.12f); m.SetFloat("_Step", 0.05f); m.SetFloat("_CoreStep", -0.4f); m.SetFloat("_OutlineWidth", 0.0015f); m.SetFloat("_RimStrength", 0.38f); m.SetFloat("_SpecStrength", 0.28f); m.SetFloat("_SpecPower", 36f); m.SetFloat("_OutlineAlbedo", 0.6f); m.SetFloat("_Cull", 0); };
                var skin = Mat(md, "M_" + name + "_Skin", toon, m => { skinF(m); m.SetFloat("_VColWidth", 1); });
                var clothes = Mat(md, "M_" + name + "_Clothes", toon, m => { clothF(m); m.SetFloat("_VColWidth", 1); });
                var hair = Mat(md, "M_" + name + "_Hair", toon, m => { hairF(m); m.SetFloat("_VColWidth", 1); });
                // LOD1/2 carry the HairMask colour layer, not OutlineMask: their outline must ignore vertex colour
                var skinF2 = Mat(md, "M_" + name + "_Skin_Far", toon, m => { skinF(m); m.SetFloat("_VColWidth", 0); });
                var clothF2 = Mat(md, "M_" + name + "_Clothes_Far", toon, m => { clothF(m); m.SetFloat("_VColWidth", 0); });
                var hairF2 = Mat(md, "M_" + name + "_Hair_Far", toon, m => { hairF(m); m.SetFloat("_VColWidth", 0); });
                var eye = Mat(md, "M_" + name + "_Eye", unlit, m => Cutout(m, T(dir + name + "_eye.png"), 0.5f, false));
                var lash = Mat(md, "M_" + name + "_Lash", unlit, m => Cutout(m, T(dir + name + "_lashbrow.png"), 0.35f, true));
                var browM = brow != null ? Mat(md, "M_" + name + "_Brow", brow, m => { m.SetTexture("_BaseMap", T(dir + name + "_lashbrow.png")); m.SetColor("_Color", man ? new Color(0.22f, 0.18f, 0.2f, 1) : new Color(0.29f, 0.24f, 0.39f, 1)); }) : lash;
                var cornea = Mat(md, "M_" + name + "_Cornea", lit, m =>
                {
                    m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetColor("_BaseColor", new Color(1, 1, 1, 0.04f)); m.SetFloat("_Smoothness", 0.95f); m.SetFloat("_Metallic", 0); m.renderQueue = (int)RenderQueue.Transparent;
                });
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); go.name = name;
                var an = go.GetComponent<Animator>(); if (an != null) Object.DestroyImmediate(an);
                SkinnedMeshRenderer face = null; var lods = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    // the FBX carries material names only (import mode None gives one null slot per submesh): map by the slot name order stored in the mesh's submeshes
                    int lod = r.name.EndsWith("_LOD2") ? 2 : r.name.EndsWith("_LOD1") ? 1 : 0; lods[lod].Add(r);
                    var smr = r as SkinnedMeshRenderer; int n = smr != null && smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : r.sharedMaterials.Length;
                    Material[] ms;
                    if (lod == 0 && n >= 7) ms = new[] { skin, clothes, eye, lash, browM, cornea, hair };
                    else ms = new[] { skinF2, clothF2, hairF2 };
                    if (ms.Length != n) Debug.LogWarning("[AgniHeroImport] " + r.name + " submeshes " + n + " vs " + ms.Length);
                    r.sharedMaterials = ms.Take(n).ToArray();
                    if (smr != null) { smr.updateWhenOffscreen = false; smr.skinnedMotionVectors = false; if (lod == 0 && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0) face = smr; }
                    r.shadowCastingMode = lod == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                var lg = go.GetComponent<LODGroup>(); if (lg == null) lg = go.AddComponent<LODGroup>(); float[] sh = { 0.22f, 0.07f, 0.008f };
                lg.SetLODs(Enumerable.Range(0, 3).Where(i => lods[i].Count > 0).Select(i => new LOD(sh[i], lods[i].ToArray())).ToArray()); lg.RecalculateBounds();
                var f = go.AddComponent<AgniFace>(); f.face = face;
                var jig = go.AddComponent<AgniHairJiggle>(); jig.lod0 = face; // spring chains from hair v3 (jiggle_hair_*) sway at runtime
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) { if (t.name == "Eye_L") f.eyeL = t; else if (t.name == "Eye_R") f.eyeR = t; else if (t.name.EndsWith(":Head")) f.head = t; }
                f.idleTrack = Track("Face_Idle_Blink"); f.talkTrack = Track("Face_Talk"); f.emoteTracks = emoteTracks; f.reactionTracks = reactTracks; f.tier = AgniFace.Tier.High;
                PrefabUtility.SaveAsPrefabAsset(go, OUT + name + ".prefab");
                Debug.Log("[AgniHeroImport] " + name + ": face=" + (face != null ? face.sharedMesh.blendShapeCount + " shapes" : "NONE") + " eyes=" + (f.eyeL != null && f.eyeR != null) + " head=" + (f.head != null) + " lods " + lods[0].Count + "/" + lods[1].Count + "/" + lods[2].Count + " emoteTracks " + emoteTracks.Count + " reactions " + reactTracks.Count + " jiggleBones " + jig.BoneCount);
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
